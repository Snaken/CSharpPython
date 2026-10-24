using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using CSharpPython.Compilation;

namespace CSharpPython
{
    /// <summary>
    /// One running script: its own globals, value stack and call frames. A game keeps one task per object, calls
    /// <see cref="Update"/> every tick and queues events with <see cref="QueueEvent"/>. A task never blocks: it
    /// runs a slice of instructions and returns, so many tasks can share one thread.
    /// </summary>
    public sealed class PyTask
    {
        private struct Handler
        {
            public int Address;
            public int Sp;
        }

        private sealed class Frame
        {
            public Code Code;
            public int Pc;
            public int Base;        // stack index of local slot 0
            public int CalleeIndex; // stack index to restore when the frame returns
            public bool IsEvent;
            public Handler[] Handlers = new Handler[4];
            public int HandlerCount;
            public Value ExcInfo;   // the exception being handled, for a bare 'raise'
        }

        // Raised by 'raise' so that it travels the same path as errors thrown by native code.
        private sealed class ScriptRaise : Exception
        {
            public readonly PyExceptionValue Exception;

            public ScriptRaise(PyExceptionValue exception)
            {
                Exception = exception;
            }
        }

        private struct PendingEvent
        {
            public string Name;
            public Value[] Args;
        }

        private const int CallDone = 0, CallFrame = 1, CallWait = 2;

        private readonly Script _script;
        private readonly CallContext _ctx = new CallContext();
        private readonly Queue<PendingEvent> _events = new Queue<PendingEvent>();

        private Value[] _stack = new Value[256];
        private int _sp;
        private Frame[] _frames = new Frame[16];
        private int _frameCount;

        private TaskState _state = TaskState.Running;
        private int _waitTicks;
        private int _mainWait = -1; // the interrupted script's wait while an event handler runs
        private bool _inEvent;
        private bool _moduleDone;
        private long _budget;
        private long _totalSteps;
        private Value _result;
        private PyRuntimeError _error;

        internal PyTask(PyVm vm, Script script)
        {
            Vm = vm;
            _script = script;
            _ctx.Task = this;
            _ctx.OutputSink = s => Vm.Output?.Invoke(s);
            var code = script.Code;
            EnsureStack(code.NumLocals + 8);
            PushFrame(code, 0, 0);
            _sp = code.NumLocals;
            for (int i = 0; i < code.NumLocals; i++) _stack[i] = Value.Unbound;
        }

        public PyVm Vm { get; }

        /// <summary>The script's global variables. Handlers are the functions stored here.</summary>
        public Dictionary<string, Value> Globals { get; } = new Dictionary<string, Value>();

        /// <summary>Free for the host to use, for example to remember which game object owns the task.</summary>
        public object Owner { get; set; }

        public TaskState State => _state;

        /// <summary>The error that ended the task, or null.</summary>
        public PyRuntimeError Error => _error;

        /// <summary>What the script's top-level code returned (always None for now).</summary>
        public Value Result => _result;

        /// <summary>The total number of instructions executed so far.</summary>
        public long StepsExecuted => _totalSteps;

        /// <summary>How many calls are currently nested.</summary>
        public int CallDepth => _frameCount;

        /// <summary>The ticks left before a waiting task resumes.</summary>
        public int WaitTicksRemaining => _state == TaskState.Waiting ? _waitTicks : 0;

        public int PendingEvents => _events.Count;

        // ---- Driving the task -----------------------------------------------------------------------------------

        /// <summary>Counts one tick off a pending wait.</summary>
        public void Tick()
        {
            if (_state == TaskState.Waiting && _waitTicks > 0) _waitTicks--;
            if (_inEvent && _mainWait > 0) _mainWait--;
        }

        /// <summary><see cref="Tick"/> followed by <see cref="Run"/>: call this once per game tick.</summary>
        public TaskState Update()
        {
            Tick();
            return Run();
        }

        /// <summary>Calls <see cref="Update"/> until the task finishes or fails. Meant for tests and tools.</summary>
        public TaskState RunUntilDone(int maxUpdates = 1000000)
        {
            for (int i = 0; i < maxUpdates; i++)
            {
                var s = Update();
                if (s == TaskState.Finished && _events.Count == 0 || s == TaskState.Error) return s;
            }
            return _state;
        }

        /// <summary>
        /// Runs the task for one slice of instructions and returns its state. <see cref="TaskState.Running"/> means
        /// the slice ran out; call again to continue.
        /// </summary>
        public TaskState Run()
        {
            if (_state == TaskState.Error) return _state;

            bool waiting = _state == TaskState.Waiting;
            if (waiting && _waitTicks > 0)
            {
                // A wait is pending, but queued events are still handled.
                if (_inEvent || _events.Count == 0 || !TryDispatchEvent()) return _state;
            }
            else
            {
                if (_state == TaskState.Finished && _events.Count == 0) return _state;
                _state = TaskState.Running;
                if (!_inEvent && _events.Count > 0) TryDispatchEvent();
                if (_state == TaskState.Error) return _state;
            }
            if (_frameCount == 0)
            {
                _state = TaskState.Finished;
                return _state;
            }

            _state = TaskState.Running;
            long slice = Vm.Options.StepsPerSlice < 1 ? 1 : Vm.Options.StepsPerSlice;
            long max = Vm.Options.MaxSteps;
            if (max > 0) slice = Math.Min(slice, Math.Max(max - _totalSteps, 0));
            _budget = slice;
            var result = Execute();
            _totalSteps += slice - Math.Max(_budget, 0);
            if (result == TaskState.Running && max > 0 && _totalSteps >= max)
            {
                Fail(new PyRuntimeError("StepLimitExceeded",
                    "the script ran more than " + max + " steps and was stopped", _script.Name, CurrentTrace()));
                return _state;
            }
            _state = result;
            return _state;
        }

        // ---- Events ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Asks the script to call its function <paramref name="name"/> with <paramref name="args"/> as soon as it
        /// is at a safe point. Events are handled one at a time, in order. Returns false if the task has failed or
        /// the queue is full. An event for a function the script does not define is ignored.
        /// </summary>
        public bool QueueEvent(string name, params Value[] args)
        {
            if (_state == TaskState.Error || _events.Count >= Vm.Options.MaxQueuedEvents) return false;
            _events.Enqueue(new PendingEvent { Name = name, Args = args ?? Array.Empty<Value>() });
            return true;
        }

        // Starts the next queued event. Returns true if a handler frame was pushed.
        private bool TryDispatchEvent()
        {
            while (_events.Count > 0)
            {
                var ev = _events.Peek();
                if (!Globals.TryGetValue(ev.Name, out Value fn) || fn.Kind != ValueKind.Function)
                {
                    // The handler may simply not be defined yet while the top-level code is still running.
                    if (!_moduleDone) return false;
                    _events.Dequeue();
                    continue;
                }
                _events.Dequeue();
                try
                {
                    EnsureStack(_sp + ev.Args.Length + 2);
                    int calleeIndex = _sp;
                    Push(fn);
                    foreach (var a in ev.Args) Push(a);
                    int mainWait = _state == TaskState.Waiting ? _waitTicks : -1;
                    CallFunction((PyFunction)fn.RawObject, calleeIndex, calleeIndex + 1, ev.Args.Length, ev.Args.Length, null);
                    _frames[_frameCount - 1].IsEvent = true;
                    _inEvent = true;
                    _mainWait = mainWait;
                    _waitTicks = 0;
                    _state = TaskState.Running;
                    return true;
                }
                catch (PyError e)
                {
                    Fail(FromError(new PyExceptionValue(ExceptionTypes.GetOrCreate(e.TypeName), e.Problem)));
                    return false;
                }
            }
            return false;
        }

        // ---- The interpreter ------------------------------------------------------------------------------------

        private TaskState Execute()
        {
            while (true)
            {
                try
                {
                    return Loop();
                }
                catch (PyError e)
                {
                    var exc = new PyExceptionValue(ExceptionTypes.GetOrCreate(e.TypeName), e.Problem);
                    if (!Unwind(exc)) return TaskState.Error;
                }
                catch (ScriptRaise r)
                {
                    if (!Unwind(r.Exception)) return TaskState.Error;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // A bug in a host function. Stop the script rather than the whole game.
                    Fail(new PyRuntimeError("HostError", e.Message, _script.Name, CurrentTrace(), e));
                    return TaskState.Error;
                }
            }
        }

        private TaskState Loop()
        {
            Frame frame = _frames[_frameCount - 1];
            Code code = frame.Code;
            Instruction[] instructions = code.Instructions;
            int pc = frame.Pc;
            int bas = frame.Base;

            while (true)
            {
                if (_budget-- <= 0)
                {
                    frame.Pc = pc;
                    return TaskState.Running;
                }

                Instruction ins = instructions[pc++];
                frame.Pc = pc;
                switch (ins.Op)
                {
                    case Op.Pop:
                        _sp--;
                        break;
                    case Op.Dup:
                        Push(_stack[_sp - 1]);
                        break;
                    case Op.Dup2:
                    {
                        Value a = _stack[_sp - 2], b = _stack[_sp - 1];
                        Push(a);
                        Push(b);
                        break;
                    }
                    case Op.Rot2:
                    {
                        Value top = _stack[_sp - 1];
                        _stack[_sp - 1] = _stack[_sp - 2];
                        _stack[_sp - 2] = top;
                        break;
                    }
                    case Op.Rot3:
                    {
                        Value c = _stack[_sp - 1];
                        _stack[_sp - 1] = _stack[_sp - 2];
                        _stack[_sp - 2] = _stack[_sp - 3];
                        _stack[_sp - 3] = c;
                        break;
                    }

                    case Op.LoadConst:
                        Push(code.Constants[ins.Arg]);
                        break;
                    case Op.LoadLocal:
                    {
                        Value v = _stack[bas + ins.Arg];
                        if (v.Kind == ValueKind.Unbound)
                            throw new PyError("UnboundLocalError", "local variable '" + code.LocalNames[ins.Arg] + "' referenced before assignment");
                        Push(v);
                        break;
                    }
                    case Op.StoreLocal:
                        _stack[bas + ins.Arg] = _stack[--_sp];
                        break;
                    case Op.LoadGlobal:
                    {
                        string name = code.Names[ins.Arg];
                        if (Globals.TryGetValue(name, out Value v) || Vm.Builtins.TryGetValue(name, out v)) Push(v);
                        else throw new PyError("NameError", "name '" + name + "' is not defined");
                        break;
                    }
                    case Op.StoreGlobal:
                        Globals[code.Names[ins.Arg]] = _stack[--_sp];
                        break;

                    case Op.LoadAttr:
                    {
                        Value obj = _stack[--_sp];
                        Push(Ops.GetAttr(obj, code.Names[ins.Arg]));
                        break;
                    }
                    case Op.StoreAttr:
                    {
                        Value obj = _stack[--_sp];
                        Value value = _stack[--_sp];
                        Ops.SetAttr(obj, code.Names[ins.Arg], value);
                        break;
                    }
                    case Op.LoadMethod:
                    {
                        Value obj = _stack[--_sp];
                        string name = code.Names[ins.Arg];
                        if (obj.Kind != ValueKind.Module && Ops.TryGetMethod(obj, name, out NativeFunction method))
                        {
                            Push(Value.FromNative(method));
                            Push(obj);
                        }
                        else
                        {
                            Push(Ops.GetAttr(obj, name));
                            Push(Value.Unbound);
                        }
                        break;
                    }
                    case Op.LoadIndex:
                    {
                        Value index = _stack[--_sp];
                        Value obj = _stack[--_sp];
                        Push(Ops.GetItem(obj, index));
                        break;
                    }
                    case Op.StoreIndex:
                    {
                        Value index = _stack[--_sp];
                        Value obj = _stack[--_sp];
                        Value value = _stack[--_sp];
                        Ops.SetItem(obj, index, value);
                        break;
                    }
                    case Op.BuildSlice:
                    {
                        Value step = _stack[--_sp];
                        Value stop = _stack[--_sp];
                        Value start = _stack[--_sp];
                        Push(Ops.MakeSlice(start, stop, step));
                        break;
                    }

                    case Op.BuildList:
                    {
                        var list = new PyList(new ArraySegment<Value>(_stack, _sp - ins.Arg, ins.Arg));
                        _sp -= ins.Arg;
                        Push(Value.FromList(list));
                        break;
                    }
                    case Op.BuildTuple:
                    {
                        var items = new Value[ins.Arg];
                        Array.Copy(_stack, _sp - ins.Arg, items, 0, ins.Arg);
                        _sp -= ins.Arg;
                        Push(Value.FromTuple(new PyTuple(items)));
                        break;
                    }
                    case Op.BuildDict:
                    {
                        var dict = new PyDict();
                        int start = _sp - ins.Arg * 2;
                        for (int k = 0; k < ins.Arg; k++) dict.Set(_stack[start + k * 2], _stack[start + k * 2 + 1]);
                        _sp = start;
                        Push(Value.FromDict(dict));
                        break;
                    }
                    case Op.BuildString:
                    {
                        int start = _sp - ins.Arg;
                        long length = 0;
                        for (int k = 0; k < ins.Arg; k++) length += ((string)_stack[start + k].RawObject).Length;
                        if (length > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                        var sb = new StringBuilder((int)length);
                        for (int k = 0; k < ins.Arg; k++) sb.Append((string)_stack[start + k].RawObject);
                        _sp = start;
                        Push(Value.FromString(sb.ToString()));
                        break;
                    }
                    case Op.FormatValue:
                    {
                        string spec = null;
                        if ((ins.Arg & 0x100) != 0) spec = (string)_stack[--_sp].RawObject;
                        Value v = _stack[--_sp];
                        switch ((char)(ins.Arg & 0xFF))
                        {
                            case 'r':
                            case 'a':
                                v = Value.FromString(Ops.Repr(v));
                                break;
                            case 's':
                                v = Value.FromString(Ops.Str(v));
                                break;
                        }
                        Push(Value.FromString(Ops.FormatValue(v, spec)));
                        break;
                    }

                    case Op.Negate:
                        _stack[_sp - 1] = Ops.Negate(_stack[_sp - 1]);
                        break;
                    case Op.Plus:
                        _stack[_sp - 1] = Ops.Plus(_stack[_sp - 1]);
                        break;
                    case Op.Not:
                        _stack[_sp - 1] = Value.FromBool(!_stack[_sp - 1].IsTruthy);
                        break;
                    case Op.Add:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Add(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Sub:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Sub(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Mul:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Mul(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Div:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Div(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.FloorDiv:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.FloorDiv(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Mod:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Mod(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Pow:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.Pow(_stack[_sp - 1], b);
                        break;
                    }

                    case Op.Eq:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Equal(_stack[_sp - 1], b));
                        break;
                    }
                    case Op.Ne:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(!Ops.Equal(_stack[_sp - 1], b));
                        break;
                    }
                    case Op.Lt:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Compare(_stack[_sp - 1], b, Ops.Order.Lt));
                        break;
                    }
                    case Op.Le:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Compare(_stack[_sp - 1], b, Ops.Order.Le));
                        break;
                    }
                    case Op.Gt:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Compare(_stack[_sp - 1], b, Ops.Order.Gt));
                        break;
                    }
                    case Op.Ge:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Compare(_stack[_sp - 1], b, Ops.Order.Ge));
                        break;
                    }
                    case Op.Is:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Is(_stack[_sp - 1], b));
                        break;
                    }
                    case Op.IsNot:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(!Ops.Is(_stack[_sp - 1], b));
                        break;
                    }
                    case Op.In:
                    {
                        Value container = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(Ops.Contains(container, _stack[_sp - 1]));
                        break;
                    }
                    case Op.NotIn:
                    {
                        Value container = _stack[--_sp];
                        _stack[_sp - 1] = Value.FromBool(!Ops.Contains(container, _stack[_sp - 1]));
                        break;
                    }

                    case Op.Jump:
                        pc = ins.Arg;
                        break;
                    case Op.JumpIfFalse:
                        if (!_stack[--_sp].IsTruthy) pc = ins.Arg;
                        break;
                    case Op.JumpIfFalseOrPop:
                        if (!_stack[_sp - 1].IsTruthy) pc = ins.Arg;
                        else _sp--;
                        break;
                    case Op.JumpIfTrueOrPop:
                        if (_stack[_sp - 1].IsTruthy) pc = ins.Arg;
                        else _sp--;
                        break;

                    case Op.GetIter:
                        _stack[_sp - 1] = Value.FromIterator(Ops.GetIterator(_stack[_sp - 1]));
                        break;
                    case Op.ForIter:
                    {
                        var iterator = (PyIterator)_stack[_sp - 1].RawObject;
                        if (iterator.Next(out Value next)) Push(next);
                        else
                        {
                            _sp--;
                            pc = ins.Arg;
                        }
                        break;
                    }

                    case Op.Call:
                    case Op.CallKw:
                    case Op.CallMethod:
                    case Op.CallMethodKw:
                    {
                        int outcome = DoCall(ins);
                        if (outcome == CallFrame)
                        {
                            frame = _frames[_frameCount - 1];
                            code = frame.Code;
                            instructions = code.Instructions;
                            pc = frame.Pc;
                            bas = frame.Base;
                        }
                        else if (outcome == CallWait)
                        {
                            return TaskState.Waiting;
                        }
                        break;
                    }
                    case Op.Return:
                    {
                        Value result = _stack[--_sp];
                        bool wasEvent = frame.IsEvent;
                        _sp = frame.CalleeIndex;
                        _frameCount--;
                        if (code.IsModule) _moduleDone = true;

                        if (wasEvent)
                        {
                            _inEvent = false;
                            int restore = _mainWait;
                            _mainWait = -1;
                            if (restore >= 0)
                            {
                                _waitTicks = restore;
                                _state = restore > 0 ? TaskState.Waiting : TaskState.Running;
                                if (restore > 0) return TaskState.Waiting;
                            }
                            else if (_frameCount == 0)
                            {
                                if (_events.Count > 0 && TryDispatchEvent())
                                {
                                    frame = _frames[_frameCount - 1];
                                    code = frame.Code;
                                    instructions = code.Instructions;
                                    pc = frame.Pc;
                                    bas = frame.Base;
                                    break;
                                }
                                return TaskState.Finished;
                            }
                        }
                        else if (_frameCount == 0)
                        {
                            _result = result;
                            if (_events.Count > 0 && TryDispatchEvent())
                            {
                                frame = _frames[_frameCount - 1];
                                code = frame.Code;
                                instructions = code.Instructions;
                                pc = frame.Pc;
                                bas = frame.Base;
                                break;
                            }
                            return TaskState.Finished;
                        }
                        else
                        {
                            Push(result);
                        }

                        // Continue the caller.
                        frame = _frames[_frameCount - 1];
                        code = frame.Code;
                        instructions = code.Instructions;
                        pc = frame.Pc;
                        bas = frame.Base;
                        break;
                    }
                    case Op.MakeFunction:
                    {
                        var fnCode = code.Functions[ins.Arg];
                        var defaults = new Value[fnCode.NumDefaults];
                        Array.Copy(_stack, _sp - fnCode.NumDefaults, defaults, 0, fnCode.NumDefaults);
                        _sp -= fnCode.NumDefaults;
                        Push(Value.FromFunction(new PyFunction(fnCode, defaults)));
                        break;
                    }

                    case Op.UnpackSequence:
                    {
                        Value seq = _stack[--_sp];
                        if (seq.Kind != ValueKind.List && seq.Kind != ValueKind.Tuple && seq.Kind != ValueKind.Str
                            && seq.Kind != ValueKind.Range && seq.Kind != ValueKind.Dict && seq.Kind != ValueKind.Iterator)
                            throw new PyError("TypeError", "cannot unpack non-iterable " + seq.TypeName + " object");
                        var items = Ops.ToList(seq);
                        if (items.Count < ins.Arg)
                            throw new PyError("ValueError", "not enough values to unpack (expected " + ins.Arg + ", got " + items.Count + ")");
                        if (items.Count > ins.Arg)
                            throw new PyError("ValueError", "too many values to unpack (expected " + ins.Arg + ")");
                        for (int k = items.Count - 1; k >= 0; k--) Push(items[k]);
                        break;
                    }

                    case Op.SetupTry:
                        if (frame.HandlerCount == frame.Handlers.Length) Array.Resize(ref frame.Handlers, frame.HandlerCount * 2);
                        frame.Handlers[frame.HandlerCount++] = new Handler { Address = ins.Arg, Sp = _sp };
                        break;
                    case Op.PopTry:
                        frame.HandlerCount--;
                        break;
                    case Op.Raise:
                    {
                        Value v = _stack[--_sp];
                        if (v.Kind == ValueKind.Exception) throw new ScriptRaise((PyExceptionValue)v.RawObject);
                        if (v.Kind == ValueKind.ExceptionType)
                            throw new ScriptRaise(new PyExceptionValue((PyExceptionType)v.RawObject, ""));
                        throw new PyError("TypeError", "exceptions must derive from BaseException");
                    }
                    case Op.Reraise:
                        throw new ScriptRaise((PyExceptionValue)_stack[--_sp].RawObject);
                    case Op.RaiseCurrent:
                        if (frame.ExcInfo.Kind != ValueKind.Exception)
                            throw new PyError("RuntimeError", "No active exception to reraise");
                        throw new ScriptRaise((PyExceptionValue)frame.ExcInfo.RawObject);
                    case Op.MatchException:
                    {
                        Value type = _stack[--_sp];
                        var exc = (PyExceptionValue)_stack[_sp - 1].RawObject;
                        Push(Value.FromBool(MatchesType(exc, type)));
                        break;
                    }

                    case Op.Import:
                    {
                        string name = code.Names[ins.Arg];
                        if (!Vm.Modules.TryGetValue(name, out PyModule module))
                            throw new PyError("ImportError", "No module named '" + name + "'");
                        Push(Value.FromModule(module));
                        break;
                    }
                    case Op.ImportFrom:
                    {
                        var module = (PyModule)_stack[_sp - 1].RawObject;
                        string name = code.Names[ins.Arg];
                        if (!module.Members.TryGetValue(name, out Value member))
                            throw new PyError("ImportError", "cannot import name '" + name + "' from '" + module.Name + "'");
                        Push(member);
                        break;
                    }
                }
            }
        }

        private static bool MatchesType(PyExceptionValue exc, Value type)
        {
            if (type.Kind == ValueKind.ExceptionType) return exc.Type.IsSubclassOf((PyExceptionType)type.RawObject);
            if (type.Kind == ValueKind.Tuple)
            {
                bool matched = false;
                foreach (var item in ((PyTuple)type.RawObject).Items)
                {
                    if (item.Kind != ValueKind.ExceptionType)
                        throw new PyError("TypeError", "catching classes that do not inherit from BaseException is not allowed");
                    if (exc.Type.IsSubclassOf((PyExceptionType)item.RawObject)) matched = true;
                }
                return matched;
            }
            throw new PyError("TypeError", "catching classes that do not inherit from BaseException is not allowed");
        }

        // ---- Calls ----------------------------------------------------------------------------------------------

        private int DoCall(Instruction ins)
        {
            bool method = ins.Op == Op.CallMethod || ins.Op == Op.CallMethodKw;
            string[] kwNames = null;
            if (ins.Op == Op.CallKw || ins.Op == Op.CallMethodKw)
            {
                var names = ((PyTuple)_stack[--_sp].RawObject).Items;
                kwNames = new string[names.Length];
                for (int i = 0; i < names.Length; i++) kwNames[i] = (string)names[i].RawObject;
            }

            int argc = ins.Arg;
            int positional = argc - (kwNames == null ? 0 : kwNames.Length);
            int calleeIndex = _sp - argc - (method ? 2 : 1);
            int argStart = calleeIndex + (method ? 2 : 1);
            Value callee = _stack[calleeIndex];

            if (method && _stack[calleeIndex + 1].Kind != ValueKind.Unbound)
            {
                // A native method with its receiver in front of the arguments.
                return CallNative((NativeFunction)callee.RawObject, calleeIndex, calleeIndex + 1, argc + 1, positional + 1, kwNames, 1);
            }

            switch (callee.Kind)
            {
                case ValueKind.Function:
                    CallFunction((PyFunction)callee.RawObject, calleeIndex, argStart, argc, positional, kwNames);
                    return CallFrame;
                case ValueKind.Native:
                    return CallNative((NativeFunction)callee.RawObject, calleeIndex, argStart, argc, positional, kwNames, 0);
                case ValueKind.BoundMethod:
                {
                    var bound = (BoundMethod)callee.RawObject;
                    _stack[argStart - 1] = bound.Self;
                    return CallNative(bound.Function, calleeIndex, argStart - 1, argc + 1, positional + 1, kwNames, 1);
                }
                case ValueKind.ExceptionType:
                {
                    if (kwNames != null) throw new PyError("TypeError", "exception classes take no keyword arguments");
                    string message = "";
                    if (argc == 1) message = Ops.Str(_stack[argStart]);
                    else if (argc > 1)
                    {
                        var items = new Value[argc];
                        Array.Copy(_stack, argStart, items, 0, argc);
                        message = Ops.Repr(Value.FromTuple(new PyTuple(items)));
                    }
                    _sp = calleeIndex;
                    Push(Value.FromException(new PyExceptionValue((PyExceptionType)callee.RawObject, message)));
                    return CallDone;
                }
                default:
                    throw new PyError("TypeError", "'" + callee.TypeName + "' object is not callable");
            }
        }

        private int CallNative(NativeFunction fn, int calleeIndex, int start, int count, int positional, string[] kwNames, int selfOffset)
        {
            if (kwNames != null && !fn.AcceptsKeywords)
                throw new PyError("TypeError", fn.Name + "() takes no keyword arguments");
            int given = positional - selfOffset;
            int min = fn.MinArgs - selfOffset, max = fn.MaxArgs < 0 ? -1 : fn.MaxArgs - selfOffset;
            if (given < min || (max >= 0 && given > max))
            {
                string expected = min == max
                    ? "exactly " + min + (min == 1 ? " argument" : " arguments")
                    : given < min ? "at least " + min + (min == 1 ? " argument" : " arguments")
                    : "at most " + max + (max == 1 ? " argument" : " arguments");
                throw new PyError("TypeError", fn.Name + "() takes " + expected + " (" + given + " given)");
            }

            _ctx.SetCall(positional, kwNames);
            _ctx.WaitTicks = -1;
            Value result = fn.Fn(_ctx, new ReadOnlySpan<Value>(_stack, start, count));
            _sp = calleeIndex;
            if (_ctx.WaitTicks >= 0)
            {
                Push(_ctx.WaitResult);
                _waitTicks = _ctx.WaitTicks;
                _state = TaskState.Waiting;
                _ctx.WaitTicks = -1;
                _ctx.WaitResult = default;
                return CallWait;
            }
            Push(result);
            return CallDone;
        }

        private void CallFunction(PyFunction fn, int calleeIndex, int argStart, int argc, int positional, string[] kwNames)
        {
            Code c = fn.Code;
            if (_frameCount >= Vm.Options.MaxCallDepth)
                throw new PyError("RecursionError", "maximum recursion depth exceeded");
            int np = c.NumParams;
            EnsureStack(argStart + c.NumLocals + 8);

            if (kwNames == null)
            {
                if (argc > np) throw new PyError("TypeError", TooManyPositional(fn, argc));
                if (argc < np)
                {
                    int firstDefault = np - c.NumDefaults;
                    List<string> missing = null;
                    for (int i = argc; i < np; i++)
                    {
                        if (i >= firstDefault) _stack[argStart + i] = fn.Defaults[i - firstDefault];
                        else (missing ?? (missing = new List<string>())).Add(c.ParamNames[i]);
                    }
                    if (missing != null) throw new PyError("TypeError", MissingArguments(fn, missing));
                }
                for (int i = np; i < c.NumLocals; i++) _stack[argStart + i] = Value.Unbound;
            }
            else
            {
                BindKeywords(fn, argStart, positional, kwNames);
            }

            _sp = argStart + c.NumLocals;
            PushFrame(c, argStart, calleeIndex);
        }

        private void BindKeywords(PyFunction fn, int argStart, int positional, string[] kwNames)
        {
            Code c = fn.Code;
            int np = c.NumParams;
            if (positional > np) throw new PyError("TypeError", TooManyPositional(fn, positional));

            var kwValues = new Value[kwNames.Length];
            Array.Copy(_stack, argStart + positional, kwValues, 0, kwNames.Length);
            for (int i = positional; i < c.NumLocals; i++) _stack[argStart + i] = Value.Unbound;

            for (int k = 0; k < kwNames.Length; k++)
            {
                int index = Array.IndexOf(c.ParamNames, kwNames[k]);
                if (index < 0)
                    throw new PyError("TypeError", fn.Name + "() got an unexpected keyword argument '" + kwNames[k] + "'");
                if (_stack[argStart + index].Kind != ValueKind.Unbound)
                    throw new PyError("TypeError", fn.Name + "() got multiple values for argument '" + kwNames[k] + "'");
                _stack[argStart + index] = kwValues[k];
            }

            int firstDefault = np - c.NumDefaults;
            List<string> missing = null;
            for (int i = positional; i < np; i++)
            {
                if (_stack[argStart + i].Kind != ValueKind.Unbound) continue;
                if (i >= firstDefault) _stack[argStart + i] = fn.Defaults[i - firstDefault];
                else (missing ?? (missing = new List<string>())).Add(c.ParamNames[i]);
            }
            if (missing != null) throw new PyError("TypeError", MissingArguments(fn, missing));
        }

        private static string TooManyPositional(PyFunction fn, int given)
        {
            int np = fn.Code.NumParams;
            string expected = fn.Code.NumDefaults == 0 ? np.ToString() : (np - fn.Code.NumDefaults) + " to " + np;
            return fn.Name + "() takes " + expected + " positional argument" + (np == 1 && fn.Code.NumDefaults == 0 ? "" : "s")
                   + " but " + given + (given == 1 ? " was" : " were") + " given";
        }

        private static string MissingArguments(PyFunction fn, List<string> names)
        {
            var sb = new StringBuilder(fn.Name).Append("() missing ").Append(names.Count)
                .Append(" required positional argument").Append(names.Count == 1 ? "" : "s").Append(": ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(names.Count == 2 ? " and " : i == names.Count - 1 ? ", and " : ", ");
                sb.Append('\'').Append(names[i]).Append('\'');
            }
            return sb.ToString();
        }

        // ---- Exceptions -----------------------------------------------------------------------------------------

        // Finds a handler for the exception, unwinding frames as needed. Returns false if the task failed.
        private bool Unwind(PyExceptionValue exc)
        {
            if (exc.Trace.Count == 0) exc.Trace = CurrentTrace();
            while (_frameCount > 0)
            {
                Frame f = _frames[_frameCount - 1];
                if (f.HandlerCount > 0)
                {
                    Handler h = f.Handlers[--f.HandlerCount];
                    _sp = h.Sp;
                    Value value = Value.FromException(exc);
                    Push(value);
                    f.ExcInfo = value;
                    f.Pc = h.Address;
                    return true;
                }
                if (f.IsEvent) break; // an event handler's error must not unwind into the interrupted script
                _sp = f.CalleeIndex;
                _frameCount--;
            }
            Fail(FromError(exc));
            return false;
        }

        private PyRuntimeError FromError(PyExceptionValue exc) =>
            new PyRuntimeError(exc.Type.Name, exc.Message, _script.Name, exc.Trace);

        private List<TraceFrame> CurrentTrace()
        {
            var trace = new List<TraceFrame>();
            for (int i = _frameCount - 1; i >= 0; i--)
            {
                Frame f = _frames[i];
                int index = Math.Max(0, Math.Min(f.Pc - 1, f.Code.Lines.Length - 1));
                trace.Add(new TraceFrame(f.Code.Name, f.Code.Lines.Length == 0 ? 0 : f.Code.Lines[index]));
            }
            return trace;
        }

        private void Fail(PyRuntimeError error)
        {
            _error = error;
            _state = TaskState.Error;
            _frameCount = 0;
            _sp = 0;
            _inEvent = false;
            _mainWait = -1;
            _events.Clear();
        }

        // ---- Stack and frames -----------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Push(Value v)
        {
            if (_sp == _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
            _stack[_sp++] = v;
        }

        private void EnsureStack(int size)
        {
            if (size <= _stack.Length) return;
            int length = _stack.Length;
            while (length < size) length *= 2;
            Array.Resize(ref _stack, length);
        }

        private void PushFrame(Code code, int bas, int calleeIndex)
        {
            if (_frameCount == _frames.Length) Array.Resize(ref _frames, _frameCount * 2);
            Frame f = _frames[_frameCount] ?? (_frames[_frameCount] = new Frame());
            _frameCount++;
            f.Code = code;
            f.Pc = 0;
            f.Base = bas;
            f.CalleeIndex = calleeIndex;
            f.IsEvent = false;
            f.HandlerCount = 0;
            f.ExcInfo = default;
        }
    }
}
