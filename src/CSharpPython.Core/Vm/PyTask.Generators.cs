using System;
using CSharpPython.Compilation;

namespace CSharpPython
{
    public sealed partial class PyTask
    {
        // Pushes a frame that continues the generator after its last yield. 'calleeIndex' is the stack slot where the
        // next yielded value (or the end) is delivered; see the Yield and Return instructions.
        private void ResumeGenerator(PyGenerator g, Value sent, int calleeIndex, GenMode mode, Value dflt = default)
        {
            if (g.State == GeneratorState.Running) throw new PyError("ValueError", "generator already executing");
            if (g.State == GeneratorState.NotStarted && sent.Kind != ValueKind.None)
                throw new PyError("TypeError", "can't send non-None value to a just-started generator");
            if (_frameCount >= Vm.Options.MaxCallDepth)
                throw new PyError("RecursionError", "maximum recursion depth exceeded");

            int bas = _sp;
            EnsureStack(bas + g.Saved.Length + g.Code.NumLocals + 8);
            CopyValues(g.Saved, _stack, bas, g.Saved.Length);
            _sp = bas + g.Saved.Length;
            PushFrame(g.Code, bas, calleeIndex);
            Frame f = _frames[_frameCount - 1];
            f.Closure = g.Closure;
            f.Owner = g.Owner;
            f.Pc = g.Pc;
            f.ExcInfo = g.ExcInfo;
            f.Generator = g;
            f.GenMode = mode;
            f.GenDefault = dflt;
            for (int i = 0; i < g.HandlerAddresses.Length; i++)
            {
                if (f.HandlerCount == f.Handlers.Length) Array.Resize(ref f.Handlers, f.HandlerCount * 2);
                f.Handlers[f.HandlerCount++] = new Handler { Address = g.HandlerAddresses[i], Sp = bas + g.HandlerHeights[i] };
            }
            if (g.State == GeneratorState.Suspended) Push(sent);
            g.State = GeneratorState.Running;
            g.Spare = g.Saved; // the next yield reuses it when the stack has the same height
            g.Saved = null;
        }

        // Array.Copy costs more than a loop for the few values a generator keeps.
        private static void CopyValues(Value[] from, Value[] to, int toIndex, int count) => CopyValues(from, 0, to, toIndex, count);

        private static void CopyValues(Value[] from, int fromIndex, Value[] to, int toIndex, int count)
        {
            if (count > 8)
            {
                Array.Copy(from, fromIndex, to, toIndex, count);
                return;
            }
            for (int i = 0; i < count; i++) to[toIndex + i] = from[fromIndex + i];
        }

        // Copies a yielding generator's frame back into the generator.
        private void SuspendGenerator(Frame f)
        {
            PyGenerator g = f.Generator;
            int height = _sp - f.Base;
            Value[] spare = g.Spare;
            g.Saved = spare != null && spare.Length == height ? spare : new Value[height];
            g.Spare = null;
            CopyValues(_stack, f.Base, g.Saved, 0, g.Saved.Length);
            g.Pc = f.Pc;
            g.ExcInfo = f.ExcInfo;
            g.HandlerAddresses = f.HandlerCount == 0 ? Array.Empty<int>() : new int[f.HandlerCount];
            g.HandlerHeights = f.HandlerCount == 0 ? Array.Empty<int>() : new int[f.HandlerCount];
            for (int i = 0; i < f.HandlerCount; i++)
            {
                g.HandlerAddresses[i] = f.Handlers[i].Address;
                g.HandlerHeights[i] = f.Handlers[i].Sp - f.Base;
            }
            g.State = GeneratorState.Suspended;
        }

        private static void FinishGenerator(Frame f)
        {
            if (f.Generator == null) return;
            f.Generator.State = GeneratorState.Done;
            f.Generator.Release();
        }

        // next(g), g.send(x) and g.__next__() from a script run the generator as a frame, so it can wait. Returns
        // -1 when the call is not about a generator and an ordinary native call should be made.
        private int TryStepGenerator(NativeFunction fn, int calleeIndex, int start, int count)
        {
            Value first = _stack[start];
            if (first.Kind != ValueKind.Iterator || !(first.RawObject is PyGenerator g)) return -1;
            Value sent = Value.None;
            Value dflt = Value.Unbound;
            if (fn.StepKind == 4 || fn.StepKind == 5) return ThrowIntoGenerator(g, fn.StepKind == 5, calleeIndex, start, count);
            switch (fn.StepKind)
            {
                case 1:
                    if (count > 1) dflt = _stack[start + 1];
                    break;
                case 2:
                    sent = _stack[start + 1];
                    break;
            }
            if (g.State == GeneratorState.Done)
            {
                if (dflt.Kind == ValueKind.Unbound) throw new PyError("StopIteration", "");
                _sp = calleeIndex;
                Push(dflt);
                return CallDone;
            }
            ResumeGenerator(g, sent, calleeIndex, GenMode.Call, dflt);
            return CallFrame;
        }

        // g.throw(type[, value]) and g.close(): resume the generator with an exception raised at its last yield, so its
        // except and finally blocks run (and can wait). close() raises GeneratorExit and succeeds when it comes out
        // again, or when the generator ends; see GenMode.Close in Yield, Return and Unwind.
        private int ThrowIntoGenerator(PyGenerator g, bool close, int calleeIndex, int start, int count)
        {
            PyExceptionValue raised;
            if (close) raised = new PyExceptionValue(ExceptionTypes.GeneratorExit, "");
            else raised = ExceptionToThrow(_stack[start + 1], count > 2 ? _stack[start + 2] : Value.None);

            if (g.State == GeneratorState.Running) throw new PyError("ValueError", "generator already executing");
            if (g.State != GeneratorState.Suspended)
            {
                // Never started, or finished: nothing runs. throw() raises in the caller; close() just closes.
                g.State = GeneratorState.Done;
                g.Release();
                if (!close) throw new ScriptRaise(raised);
                _sp = calleeIndex;
                Push(Value.None);
                return CallDone;
            }
            ResumeWithException(g, raised, calleeIndex, close ? GenMode.Close : GenMode.Call);
            return CallFrame;
        }

        // Resumes a suspended generator with an exception raised at its last yield. A generator that is inside
        // 'yield from' carries on at ForIterThrow instead, which hands the exception down to the generator it delegates
        // to; so the exception reaches the innermost one first, however deep the chain.
        private void ResumeWithException(PyGenerator g, PyExceptionValue exc, int calleeIndex, GenMode mode)
        {
            Instruction suspendedAt = g.Code.Instructions[g.Pc - 1];
            if (suspendedAt.Op == Op.Yield && suspendedAt.Arg != 0)
            {
                ResumeGenerator(g, ValueOf(exc), calleeIndex, mode, Value.Unbound);
                _frames[_frameCount - 1].Pc = suspendedAt.Arg;
                return;
            }
            ResumeGenerator(g, Value.None, calleeIndex, mode, Value.Unbound);
            _sp--; // the value a resumed yield would have received
            throw new ScriptRaise(exc);
        }

        // The exception g.throw(type, value) raises: an exception instance as it is, or a class called with the value.
        private PyExceptionValue ExceptionToThrow(Value type, Value value)
        {
            PyExceptionValue exc = null;
            if (type.Kind == ValueKind.ExceptionType)
            {
                string message = value.Kind == ValueKind.None ? "" : Ops.Str(value);
                exc = new PyExceptionValue((PyExceptionType)type.RawObject, message);
            }
            else if (type.Kind == ValueKind.Class && ((PyClass)type.RawObject).ExceptionRoot != null)
            {
                exc = AsException(CallSync(type, value.Kind == ValueKind.None ? Array.Empty<Value>() : new[] { value }));
            }
            else if (value.Kind == ValueKind.None)
            {
                exc = AsException(type);
            }
            if (exc == null)
                throw new PyError("TypeError", "exceptions must be classes or instances deriving from BaseException, not " + type.TypeName);
            return exc;
        }

        /// <summary>Resumes a generator for a native function and runs it to its next yield. False when it finished.</summary>
        internal bool ResumeSync(PyGenerator g, Value sent, out Value value)
        {
            value = default;
            if (g.State == GeneratorState.Done) return false;
            if (_nested >= MaxNestedCalls) throw new PyError("RecursionError", "maximum recursion depth exceeded");
            int savedPositional = _ctx.PositionalCount;
            string[] savedKeywords = _ctx.KeywordNames;
            int calleeIndex = _sp;
            _nested++;
            try
            {
                ResumeGenerator(g, sent, calleeIndex, GenMode.Sync);
                _boundaryDone = false;
                value = RunBoundary(calleeIndex);
                if (!_boundaryDone) return true;
                value = default;
                return false;
            }
            finally
            {
                _nested--;
                _ctx.SetCall(savedPositional, savedKeywords);
            }
        }
    }
}
