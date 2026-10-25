using System;

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
            Array.Copy(g.Saved, 0, _stack, bas, g.Saved.Length);
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
            g.Saved = null;
        }

        // Copies a yielding generator's frame back into the generator.
        private void SuspendGenerator(Frame f)
        {
            PyGenerator g = f.Generator;
            g.Saved = new Value[_sp - f.Base];
            Array.Copy(_stack, f.Base, g.Saved, 0, g.Saved.Length);
            g.Pc = f.Pc;
            g.ExcInfo = f.ExcInfo;
            g.HandlerAddresses = new int[f.HandlerCount];
            g.HandlerHeights = new int[f.HandlerCount];
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
            f.Generator.Saved = null;
        }

        // next(g), g.send(x) and g.__next__() from a script run the generator as a frame, so it can wait. Returns
        // -1 when the call is not about a generator and an ordinary native call should be made.
        private int TryStepGenerator(NativeFunction fn, int calleeIndex, int start, int count)
        {
            Value first = _stack[start];
            if (first.Kind != ValueKind.Iterator || !(first.RawObject is PyGenerator g)) return -1;
            Value sent = Value.None;
            Value dflt = Value.Unbound;
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
