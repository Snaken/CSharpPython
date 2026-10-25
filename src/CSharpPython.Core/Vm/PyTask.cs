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
    public sealed partial class PyTask
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
            public Cell[] Closure;  // the running function's free variables
            public bool IsBoundary; // the entry frame of a call made by a native function (see CallSync)
            public Handler[] Handlers = new Handler[4];
            public int HandlerCount;
            public Value ExcInfo;   // the exception being handled, for a bare 'raise'
            public bool IsConstructor; // running __init__ for a call of a class; the call's result is Constructed
            public Value Constructed;
            public PyClass Owner;   // the class whose method is running, for super()
        }

        // Raised by 'raise' so that it travels the same path as errors thrown by native code.
        // Thrown when a call made by a native function runs past MaxSteps; the task then fails with StepLimitExceeded.
        private sealed class StepLimitSignal : Exception
        {
        }

        private const int MaxNestedCalls = 40;

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
        private long _sliceSize, _sliceSpent; // the current slice's budget, and steps used by earlier refills within it
        /// <summary>The task whose script is running on this thread, so that str() can call a class's __str__.</summary>
        [ThreadStatic]
        internal static PyTask Current;

        private int _nested;                   // how many native-initiated calls (CallSync) are in progress
        private bool _boundaryHit;
        private Value _boundaryResult;
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

        // Used by Restore: the state is filled in afterwards.
        private PyTask(PyVm vm, Script script, bool restoring)
        {
            Vm = vm;
            _script = script;
            _ctx.Task = this;
            _ctx.OutputSink = s => Vm.Output?.Invoke(s);
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
            _sliceSize = slice;
            _sliceSpent = 0;
            var previous = Current;
            Current = this;
            TaskState result;
            try { result = Execute(); }
            finally { Current = previous; }
            _totalSteps += _sliceSpent + _sliceSize - Math.Max(_budget, 0);
            _sliceSpent = 0;
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
                catch (StepLimitSignal)
                {
                    Fail(new PyRuntimeError("StepLimitExceeded",
                        "the script ran more than " + Vm.Options.MaxSteps + " steps and was stopped", _script.Name, CurrentTrace()));
                    return TaskState.Error;
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
                    case Op.MakeCell:
                        _stack[bas + ins.Arg] = Value.FromCell(new Cell(_stack[bas + ins.Arg]));
                        break;
                    case Op.LoadCell:
                    {
                        Value v = ((Cell)_stack[bas + ins.Arg].RawObject).Value;
                        if (v.Kind == ValueKind.Unbound)
                            throw new PyError("UnboundLocalError", "local variable '" + code.LocalNames[ins.Arg] + "' referenced before assignment");
                        Push(v);
                        break;
                    }
                    case Op.StoreCell:
                        ((Cell)_stack[bas + ins.Arg].RawObject).Value = _stack[--_sp];
                        break;
                    case Op.LoadFree:
                    {
                        Value v = frame.Closure[ins.Arg].Value;
                        if (v.Kind == ValueKind.Unbound)
                            throw new PyError("NameError", "free variable '" + code.FreeNames[ins.Arg] + "' referenced before assignment in enclosing scope");
                        Push(v);
                        break;
                    }
                    case Op.StoreFree:
                        frame.Closure[ins.Arg].Value = _stack[--_sp];
                        break;
                    case Op.LoadCellRef:
                        Push(_stack[bas + ins.Arg]);
                        break;
                    case Op.LoadFreeRef:
                        Push(Value.FromCell(frame.Closure[ins.Arg]));
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
                        if (obj.Kind == ValueKind.Instance && Ops.TryGetScriptMethod(obj, name, out PyFunction scriptMethod))
                        {
                            Push(Value.FromFunction(scriptMethod));
                            Push(obj);
                        }
                        else if (obj.Kind != ValueKind.Module && Ops.TryGetMethod(obj, name, out NativeFunction method))
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
                    case Op.BuildSet:
                    {
                        var set = new PySet();
                        int start = _sp - ins.Arg;
                        for (int k = 0; k < ins.Arg; k++) set.Add(_stack[start + k]);
                        _sp = start;
                        Push(Value.FromSet(set));
                        break;
                    }
                    case Op.ListAppend:
                    {
                        Value item = _stack[--_sp];
                        var list = (PyList)_stack[_sp - 1].RawObject;
                        if (list.Count >= Ops.MaxSequenceLength) throw new PyError("MemoryError", "sequence too long");
                        list.Items.Add(item);
                        break;
                    }
                    case Op.ListExtend:
                    {
                        Value source = _stack[--_sp];
                        if (source.Kind != ValueKind.List && source.Kind != ValueKind.Tuple && source.Kind != ValueKind.Str
                            && source.Kind != ValueKind.Range && source.Kind != ValueKind.Dict && source.Kind != ValueKind.Set && source.Kind != ValueKind.Iterator
                            && !(source.Kind == ValueKind.Instance && Ops.IsIterable(source)))
                            throw new PyError("TypeError", "argument after * must be an iterable, not " + source.TypeName);
                        var list = (PyList)_stack[_sp - 1].RawObject;
                        list.Items.AddRange(Ops.ToList(source));
                        if (list.Count > Ops.MaxSequenceLength) throw new PyError("MemoryError", "sequence too long");
                        break;
                    }
                    case Op.DictSetUnique:
                    {
                        Value val = _stack[--_sp];
                        Value key = _stack[--_sp];
                        var dict = (PyDict)_stack[_sp - 1].RawObject;
                        if (dict.ContainsKey(key))
                            throw new PyError("TypeError", "got multiple values for keyword argument '" + (string)key.RawObject + "'");
                        dict.Set(key, val);
                        break;
                    }
                    case Op.DictMerge:
                    {
                        Value source = _stack[--_sp];
                        if (source.Kind != ValueKind.Dict)
                            throw new PyError("TypeError", "argument after ** must be a mapping, not " + source.TypeName);
                        var dict = (PyDict)_stack[_sp - 1].RawObject;
                        foreach (var pair in ((PyDict)source.RawObject).Pairs())
                        {
                            if (pair.Key.Kind != ValueKind.Str) throw new PyError("TypeError", "keywords must be strings");
                            if (dict.ContainsKey(pair.Key))
                                throw new PyError("TypeError", "got multiple values for keyword argument '" + (string)pair.Key.RawObject + "'");
                            dict.Set(pair.Key, pair.Value);
                        }
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
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.AddInPlace(_stack[_sp - 1], b) : Ops.Add(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Sub:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.SubInPlace(_stack[_sp - 1], b) : Ops.Sub(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Mul:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.MulInPlace(_stack[_sp - 1], b) : Ops.Mul(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Div:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.DivInPlace(_stack[_sp - 1], b) : Ops.Div(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.FloorDiv:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.FloorDivInPlace(_stack[_sp - 1], b) : Ops.FloorDiv(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Mod:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.ModInPlace(_stack[_sp - 1], b) : Ops.Mod(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Pow:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = ins.Arg != 0 ? Ops.PowInPlace(_stack[_sp - 1], b) : Ops.Pow(_stack[_sp - 1], b);
                        break;
                    }

                    case Op.Eq:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.EqualOp(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Ne:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.NotEqualOp(_stack[_sp - 1], b);
                        break;
                    }
                    case Op.Lt:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.CompareOp(_stack[_sp - 1], b, Ops.Order.Lt);
                        break;
                    }
                    case Op.Le:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.CompareOp(_stack[_sp - 1], b, Ops.Order.Le);
                        break;
                    }
                    case Op.Gt:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.CompareOp(_stack[_sp - 1], b, Ops.Order.Gt);
                        break;
                    }
                    case Op.Ge:
                    {
                        Value b = _stack[--_sp];
                        _stack[_sp - 1] = Ops.CompareOp(_stack[_sp - 1], b, Ops.Order.Ge);
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

                    case Op.ClassNamespace:
                    {
                        var ns = new PyDict();
                        for (int i = 0; i < code.NumLocals; i++)
                            if (_stack[bas + i].Kind != ValueKind.Unbound) ns.Set(Value.FromString(code.LocalNames[i]), _stack[bas + i]);
                        Push(Value.FromDict(ns));
                        break;
                    }
                    case Op.LoadSuper:
                    {
                        if (frame.Owner == null || code.NumParams == 0)
                            throw new PyError("RuntimeError", "super(): no arguments");
                        Value self = _stack[bas];
                        if (self.Kind == ValueKind.Cell) self = ((Cell)self.RawObject).Value;
                        Push(Ops.SuperAttr(frame.Owner, self, code.Names[ins.Arg]));
                        break;
                    }
                    case Op.LoadSuper2:
                    {
                        Value obj = _stack[--_sp];
                        Value cls = _stack[--_sp];
                        if (cls.Kind != ValueKind.Class)
                            throw new PyError("TypeError", "super() argument 1 must be a class, not " + cls.TypeName);
                        Push(Ops.SuperAttr((PyClass)cls.RawObject, obj, code.Names[ins.Arg]));
                        break;
                    }
                    case Op.BuildClass:
                        BuildClass(ins.Arg);
                        break;

                    case Op.Call:
                    case Op.CallKw:
                    case Op.CallMethod:
                    case Op.CallMethodKw:
                    case Op.CallEx:
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
                        if (frame.IsConstructor)
                        {
                            if (result.Kind != ValueKind.None)
                                throw new PyError("TypeError", "__init__() should return None, not '" + result.TypeName + "'");
                            result = frame.Constructed;
                        }
                        bool wasEvent = frame.IsEvent;
                        _sp = frame.CalleeIndex;
                        _frameCount--;
                        if (frame.IsBoundary)
                        {
                            _boundaryResult = result;
                            _boundaryHit = true;
                            return TaskState.Running;
                        }
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
                        Cell[] closure = null;
                        if (fnCode.FreeNames.Length > 0)
                        {
                            closure = new Cell[fnCode.FreeNames.Length];
                            for (int i = closure.Length - 1; i >= 0; i--) closure[i] = (Cell)_stack[--_sp].RawObject;
                        }
                        var defaults = new Value[fnCode.NumDefaults];
                        Array.Copy(_stack, _sp - fnCode.NumDefaults, defaults, 0, fnCode.NumDefaults);
                        _sp -= fnCode.NumDefaults;
                        Push(Value.FromFunction(new PyFunction(fnCode, defaults, closure)));
                        break;
                    }

                    case Op.UnpackSequence:
                    {
                        Value seq = _stack[--_sp];
                        if (seq.Kind != ValueKind.List && seq.Kind != ValueKind.Tuple && seq.Kind != ValueKind.Str
                            && seq.Kind != ValueKind.Range && seq.Kind != ValueKind.Dict && seq.Kind != ValueKind.Set && seq.Kind != ValueKind.Iterator
                            && !(seq.Kind == ValueKind.Instance && Ops.IsIterable(seq)))
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
                        if (v.Kind == ValueKind.ExceptionType)
                            throw new ScriptRaise(new PyExceptionValue((PyExceptionType)v.RawObject, ""));
                        if (v.Kind == ValueKind.Class && ((PyClass)v.RawObject).ExceptionRoot != null)
                            v = CallSync(v, Array.Empty<Value>()); // 'raise MyError' makes the instance
                        var raised = AsException(v);
                        if (raised == null) throw new PyError("TypeError", "exceptions must derive from BaseException");
                        throw new ScriptRaise(raised);
                    }
                    case Op.Reraise:
                        throw new ScriptRaise(AsException(_stack[--_sp]));
                    case Op.RaiseCurrent:
                    {
                        var current = AsException(frame.ExcInfo);
                        if (current == null) throw new PyError("RuntimeError", "No active exception to reraise");
                        throw new ScriptRaise(current);
                    }
                    case Op.MatchException:
                    {
                        Value type = _stack[--_sp];
                        var exc = AsException(_stack[_sp - 1]);
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

        // The exception for a value on the stack: the built-in kind, or an instance of a script exception class.
        // Null if the value is not an exception.
        private static PyExceptionValue AsException(Value v)
        {
            if (v.Kind == ValueKind.Exception) return (PyExceptionValue)v.RawObject;
            if (v.Kind == ValueKind.Instance && ((PyInstance)v.RawObject).Class.ExceptionRoot != null)
                return new PyExceptionValue((PyInstance)v.RawObject);
            return null;
        }

        private static Value ValueOf(PyExceptionValue exc) =>
            exc.Instance != null ? Value.FromInstance(exc.Instance) : Value.FromException(exc);

        private static bool MatchesType(PyExceptionValue exc, Value type)
        {
            if (type.Kind == ValueKind.Tuple)
            {
                bool matched = false;
                foreach (var item in ((PyTuple)type.RawObject).Items)
                {
                    if (item.Kind == ValueKind.Tuple)
                        throw new PyError("TypeError", "catching classes that do not inherit from BaseException is not allowed");
                    if (MatchesType(exc, item)) matched = true;
                }
                return matched;
            }
            if (type.Kind == ValueKind.ExceptionType) return exc.Type.IsSubclassOf((PyExceptionType)type.RawObject);
            if (type.Kind == ValueKind.Class && ((PyClass)type.RawObject).ExceptionRoot != null)
                return exc.Instance != null && exc.Instance.Class.IsSubclassOf((PyClass)type.RawObject);
            throw new PyError("TypeError", "catching classes that do not inherit from BaseException is not allowed");
        }

        // ---- Calls ----------------------------------------------------------------------------------------------

        private int DoCall(Instruction ins)
        {
            bool method = ins.Op == Op.CallMethod || ins.Op == Op.CallMethodKw;
            string[] kwNames = null;
            int argc = ins.Arg;
            if (ins.Op == Op.CallKw || ins.Op == Op.CallMethodKw)
            {
                var names = ((PyTuple)_stack[--_sp].RawObject).Items;
                kwNames = new string[names.Length];
                for (int i = 0; i < names.Length; i++) kwNames[i] = (string)names[i].RawObject;
            }
            else if (ins.Op == Op.CallEx)
            {
                // Spread the collected arguments back onto the stack as an ordinary call would have left them.
                method = (ins.Arg & 2) != 0;
                PyDict kwargs = (ins.Arg & 1) != 0 ? (PyDict)_stack[--_sp].RawObject : null;
                var positionalArgs = ((PyList)_stack[--_sp].RawObject).Items;
                int keywordCount = kwargs?.Count ?? 0;
                argc = positionalArgs.Count + keywordCount;
                EnsureStack(_sp + argc + 8);
                foreach (var item in positionalArgs) Push(item);
                if (keywordCount > 0)
                {
                    kwNames = new string[keywordCount];
                    int k = 0;
                    foreach (var pair in kwargs.Pairs())
                    {
                        kwNames[k++] = (string)pair.Key.RawObject;
                        Push(pair.Value);
                    }
                }
            }

            int positional = argc - (kwNames == null ? 0 : kwNames.Length);
            int calleeIndex = _sp - argc - (method ? 2 : 1);
            int argStart = calleeIndex + (method ? 2 : 1);
            Value callee = _stack[calleeIndex];

            if (method && _stack[calleeIndex + 1].Kind != ValueKind.Unbound)
            {
                // A method with its receiver in front of the arguments.
                if (callee.Kind == ValueKind.Function)
                {
                    CallFunction((PyFunction)callee.RawObject, calleeIndex, calleeIndex + 1, argc + 1, positional + 1, kwNames);
                    return CallFrame;
                }
                return CallNative((NativeFunction)callee.RawObject, calleeIndex, calleeIndex + 1, argc + 1, positional + 1, kwNames, 1);
            }

            switch (callee.Kind)
            {
                case ValueKind.Function:
                    CallFunction((PyFunction)callee.RawObject, calleeIndex, argStart, argc, positional, kwNames);
                    return CallFrame;
                case ValueKind.Native:
                    return CallNative((NativeFunction)callee.RawObject, calleeIndex, argStart, argc, positional, kwNames, 0);
                case ValueKind.Type:
                    return CallNative(ConstructorOf(callee), calleeIndex, argStart, argc, positional, kwNames, 0);
                case ValueKind.BoundMethod:
                {
                    var bound = (BoundMethod)callee.RawObject;
                    _stack[argStart - 1] = bound.Self;
                    return CallNative(bound.Function, calleeIndex, argStart - 1, argc + 1, positional + 1, kwNames, 1);
                }
                case ValueKind.ScriptMethod:
                {
                    var bound = (ScriptMethod)callee.RawObject;
                    _stack[argStart - 1] = bound.Self;
                    CallFunction(bound.Function, calleeIndex, argStart - 1, argc + 1, positional + 1, kwNames);
                    return CallFrame;
                }
                case ValueKind.Class:
                    return Construct(callee, calleeIndex, argStart, argc, positional, kwNames);
                case ValueKind.Instance:
                {
                    // An object whose class defines __call__ runs it as an ordinary frame, so it can wait.
                    var call = CallMethodOf(callee);
                    _stack[argStart - 1] = callee;
                    CallFunction(call, calleeIndex, argStart - 1, argc + 1, positional + 1, kwNames);
                    return CallFrame;
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

        // Calls a script class: makes the instance and runs __init__ in a frame of its own, so that it can wait.
        private int Construct(Value callee, int calleeIndex, int argStart, int argc, int positional, string[] kwNames)
        {
            var cls = (PyClass)callee.RawObject;
            var self = Value.FromInstance(new PyInstance(cls));
            bool isException = cls.ExceptionRoot != null;
            if (isException)
            {
                // Like BaseException, the positional arguments become .args whatever __init__ does with them.
                var given = new Value[positional];
                Array.Copy(_stack, argStart, given, 0, positional);
                ((PyInstance)self.RawObject).Fields.Set(Value.FromString("args"), Value.FromTuple(new PyTuple(given)));
            }
            if (cls.TryFind("__init__", out Value init))
            {
                if (init.Kind != ValueKind.Function) throw new PyError("TypeError", "__init__ must be a function");
                _stack[argStart - 1] = self;
                CallFunction((PyFunction)init.RawObject, calleeIndex, argStart - 1, argc + 1, positional + 1, kwNames);
                var frame = _frames[_frameCount - 1];
                frame.IsConstructor = true;
                frame.Constructed = self;
                return CallFrame;
            }
            if (isException && argc > 0 && kwNames != null) throw new PyError("TypeError", cls.Name + "() takes no keyword arguments");
            if (argc > 0 && !isException) throw new PyError("TypeError", cls.Name + "() takes no arguments");
            _sp = calleeIndex;
            Push(self);
            return CallDone;
        }

        private void BuildClass(int bases)
        {
            var ns = (PyDict)_stack[--_sp].RawObject;
            PyClass parent = null;
            PyExceptionType rootType = null;
            if (bases == 1)
            {
                Value b = _stack[--_sp];
                if (b.Kind == ValueKind.Class) parent = (PyClass)b.RawObject;
                else if (b.Kind == ValueKind.Type && (PyType)b.RawObject == PyTypes.Object) parent = null;
                else if (b.Kind == ValueKind.ExceptionType) rootType = (PyExceptionType)b.RawObject;
                else if (b.Kind == ValueKind.Type)
                    throw new PyError("TypeError", "cannot inherit from the built-in type '" + ((PyType)b.RawObject).Name + "' yet");
                else throw new PyError("TypeError", "bases must be classes, not '" + b.TypeName + "'");
            }
            string name = (string)_stack[--_sp].RawObject;
            var cls = new PyClass(name, parent, ns) { RootType = rootType };
            cls.AdoptMethods();
            Push(Value.FromClass(cls));
        }

        // The __call__ function of an instance's class.
        private static PyFunction CallMethodOf(Value instance)
        {
            var cls = ((PyInstance)instance.RawObject).Class;
            if (cls.TryFind("__call__", out Value member) && member.Kind == ValueKind.Function) return (PyFunction)member.RawObject;
            throw new PyError("TypeError", "'" + cls.Name + "' object is not callable");
        }

        /// <summary>
        /// Runs <c>__next__</c> of an iterator object for a <c>for</c> loop or <c>next()</c>. Returns false when it
        /// raises StopIteration.
        /// </summary>
        internal bool CallNext(Value iterator, out Value value)
        {
            var cls = ((PyInstance)iterator.RawObject).Class;
            if (!cls.TryFind("__next__", out Value next) || next.Kind != ValueKind.Function)
                throw new PyError("TypeError", "iter() returned non-iterator of type '" + cls.Name + "'");
            value = default;
            try
            {
                value = CallSync(Value.FromScriptMethod(new ScriptMethod(iterator, (PyFunction)next.RawObject)), Array.Empty<Value>());
                return true;
            }
            catch (ScriptRaise r) when (r.Exception.Type.IsSubclassOf(ExceptionTypes.StopIteration))
            {
                return false;
            }
            catch (PyError e) when (e.TypeName == "StopIteration")
            {
                return false;
            }
        }

        private static NativeFunction ConstructorOf(Value type)
        {
            var t = (PyType)type.RawObject;
            return t.Constructor ?? throw new PyError("TypeError", "cannot create '" + t.Name + "' instances");
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

            if (c.IsComplex)
            {
                BindGeneral(fn, argStart, positional, kwNames);
            }
            else if (kwNames == null)
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
            _frames[_frameCount - 1].Closure = fn.Closure;
            _frames[_frameCount - 1].Owner = fn.Owner;
        }

        // Argument binding for functions with keyword-only parameters, *args or **kwargs.
        private void BindGeneral(PyFunction fn, int argStart, int positional, string[] kwNames)
        {
            Code c = fn.Code;
            int np = c.NumParams, nk = c.NumKwOnly;
            int kwCount = kwNames == null ? 0 : kwNames.Length;
            if (positional > np && !c.HasVarArgs) throw new PyError("TypeError", TooManyPositional(fn, positional));

            PyTuple extras = null;
            if (c.HasVarArgs)
            {
                var items = new Value[Math.Max(0, positional - np)];
                Array.Copy(_stack, argStart + np, items, 0, items.Length);
                extras = new PyTuple(items);
            }
            var kwValues = new Value[kwCount];
            Array.Copy(_stack, argStart + positional, kwValues, 0, kwCount);
            for (int i = Math.Min(positional, np); i < c.NumLocals; i++) _stack[argStart + i] = Value.Unbound;

            PyDict leftover = c.HasVarKeywords ? new PyDict() : null;
            for (int k = 0; k < kwCount; k++)
            {
                int index = Array.IndexOf(c.ParamNames, kwNames[k]);
                if (index < 0)
                {
                    if (leftover == null)
                        throw new PyError("TypeError", fn.Name + "() got an unexpected keyword argument '" + kwNames[k] + "'");
                    leftover.Set(Value.FromString(kwNames[k]), kwValues[k]);
                    continue;
                }
                if (_stack[argStart + index].Kind != ValueKind.Unbound)
                    throw new PyError("TypeError", fn.Name + "() got multiple values for argument '" + kwNames[k] + "'");
                _stack[argStart + index] = kwValues[k];
            }

            int firstDefault = np - c.NumPosDefaults;
            List<string> missing = null;
            for (int i = Math.Min(positional, np); i < np; i++)
            {
                if (_stack[argStart + i].Kind != ValueKind.Unbound) continue;
                if (i >= firstDefault) _stack[argStart + i] = fn.Defaults[i - firstDefault];
                else (missing ?? (missing = new List<string>())).Add(c.ParamNames[i]);
            }
            if (missing != null) throw new PyError("TypeError", MissingArguments(fn, missing, "positional"));

            for (int j = 0; j < nk; j++)
            {
                if (_stack[argStart + np + j].Kind != ValueKind.Unbound) continue;
                if (c.KwDefaultIndex[j] >= 0) _stack[argStart + np + j] = fn.Defaults[c.NumPosDefaults + c.KwDefaultIndex[j]];
                else (missing ?? (missing = new List<string>())).Add(c.ParamNames[np + j]);
            }
            if (missing != null) throw new PyError("TypeError", MissingArguments(fn, missing, "keyword-only"));

            int slot = argStart + np + nk;
            if (c.HasVarArgs) _stack[slot++] = Value.FromTuple(extras);
            if (c.HasVarKeywords) _stack[slot] = Value.FromDict(leftover);
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
            int defaults = fn.Code.NumPosDefaults;
            string expected = defaults == 0 ? np.ToString() : "from " + (np - defaults) + " to " + np;
            return fn.Name + "() takes " + expected + " positional argument" + (np == 1 && defaults == 0 ? "" : "s")
                   + " but " + given + (given == 1 ? " was" : " were") + " given";
        }

        private static string MissingArguments(PyFunction fn, List<string> names, string kind = "positional")
        {
            var sb = new StringBuilder(fn.Name).Append("() missing ").Append(names.Count)
                .Append(" required ").Append(kind).Append(" argument").Append(names.Count == 1 ? "" : "s").Append(": ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(names.Count == 2 ? " and " : i == names.Count - 1 ? ", and " : ", ");
                sb.Append('\'').Append(names[i]).Append('\'');
            }
            return sb.ToString();
        }

        // ---- Exceptions -----------------------------------------------------------------------------------------

        // Finds a handler for the exception, unwinding frames as needed. Returns false if the task failed.
        // With a floor, only frames above it are unwound and the task does not fail: false means the exception
        // left the call made by a native function and must continue through that native.
        private bool Unwind(PyExceptionValue exc, int floor = 0)
        {
            if (exc.Trace.Count == 0) exc.Trace = CurrentTrace();
            if (exc.Instance != null) exc.Instance.Trace = exc.Trace;
            while (_frameCount > floor)
            {
                Frame f = _frames[_frameCount - 1];
                if (f.HandlerCount > 0)
                {
                    Handler h = f.Handlers[--f.HandlerCount];
                    _sp = h.Sp;
                    Value value = ValueOf(exc);
                    Push(value);
                    f.ExcInfo = value;
                    f.Pc = h.Address;
                    return true;
                }
                if (f.IsEvent) break; // an event handler's error must not unwind into the interrupted script
                _sp = f.CalleeIndex;
                _frameCount--;
            }
            if (floor > 0) return false;
            Fail(FromError(exc));
            return false;
        }

        private PyRuntimeError FromError(PyExceptionValue exc) =>
            new PyRuntimeError(exc.TypeName, exc.Message, _script.Name, exc.Trace);

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

        // ---- Calls made by native functions ---------------------------------------------------------------------

        /// <summary>
        /// Calls a script function (or built-in) from inside a native function and returns its result. The call runs to
        /// completion before this returns, so the function cannot wait. An exception it raises propagates through the
        /// caller as a script exception.
        /// </summary>
        internal Value CallSync(Value callee, Value[] args)
        {
            if (_nested >= MaxNestedCalls) throw new PyError("RecursionError", "maximum recursion depth exceeded");
            int savedPositional = _ctx.PositionalCount;
            string[] savedKeywords = _ctx.KeywordNames;
            int calleeIndex = _sp;
            int argc = args.Length;
            int argStart = calleeIndex + 1;
            EnsureStack(argStart + argc + 8);
            Push(callee);
            foreach (var a in args) Push(a);

            _nested++;
            try
            {
                int status;
                switch (callee.Kind)
                {
                    case ValueKind.Function:
                        CallFunction((PyFunction)callee.RawObject, calleeIndex, argStart, argc, argc, null);
                        status = CallFrame;
                        break;
                    case ValueKind.Native:
                        status = CallNative((NativeFunction)callee.RawObject, calleeIndex, argStart, argc, argc, null, 0);
                        break;
                    case ValueKind.Type:
                        status = CallNative(ConstructorOf(callee), calleeIndex, argStart, argc, argc, null, 0);
                        break;
                    case ValueKind.BoundMethod:
                    {
                        var bound = (BoundMethod)callee.RawObject;
                        _stack[argStart - 1] = bound.Self;
                        status = CallNative(bound.Function, calleeIndex, argStart - 1, argc + 1, argc + 1, null, 1);
                        break;
                    }
                    case ValueKind.ScriptMethod:
                    {
                        var bound = (ScriptMethod)callee.RawObject;
                        _stack[argStart - 1] = bound.Self;
                        CallFunction(bound.Function, calleeIndex, argStart - 1, argc + 1, argc + 1, null);
                        status = CallFrame;
                        break;
                    }
                    case ValueKind.Class:
                        status = Construct(callee, calleeIndex, argStart, argc, argc, null);
                        break;
                    case ValueKind.Instance:
                    {
                        var call = CallMethodOf(callee);
                        _stack[argStart - 1] = callee;
                        CallFunction(call, calleeIndex, argStart - 1, argc + 1, argc + 1, null);
                        status = CallFrame;
                        break;
                    }
                    default:
                        _sp = calleeIndex;
                        throw new PyError("TypeError", "'" + callee.TypeName + "' object is not callable");
                }

                if (status == CallDone) return _stack[--_sp];
                if (status == CallWait) return CancelWait(calleeIndex);

                int floor = _frameCount - 1;
                _frames[floor].IsBoundary = true;
                while (true)
                {
                    try
                    {
                        var state = Loop();
                        if (_boundaryHit)
                        {
                            _boundaryHit = false;
                            Value result = _boundaryResult;
                            _boundaryResult = default;
                            return result;
                        }
                        if (state == TaskState.Waiting) return CancelWait(calleeIndex, floor);
                        RefillBudget();
                    }
                    catch (PyError e)
                    {
                        var exc = new PyExceptionValue(ExceptionTypes.GetOrCreate(e.TypeName), e.Problem);
                        if (!Unwind(exc, floor)) throw new ScriptRaise(exc);
                    }
                    catch (ScriptRaise r)
                    {
                        if (!Unwind(r.Exception, floor)) throw;
                    }
                }
            }
            finally
            {
                _nested--;
                _ctx.SetCall(savedPositional, savedKeywords);
            }
        }

        // The called function asked to wait, which a call made by a native function cannot do.
        private Value CancelWait(int calleeIndex, int floor = -1)
        {
            if (floor >= 0) _frameCount = floor;
            _sp = calleeIndex;
            _state = TaskState.Running;
            _waitTicks = 0;
            throw new PyError("RuntimeError", "a function called from a built-in such as sorted() cannot wait");
        }

        // The slice ran out in the middle of a nested call: keep going, but still stop at MaxSteps.
        private void RefillBudget()
        {
            long size = Vm.Options.StepsPerSlice < 1 ? 1 : Vm.Options.StepsPerSlice;
            long max = Vm.Options.MaxSteps;
            _sliceSpent += _sliceSize;
            if (max > 0)
            {
                long left = max - _totalSteps - _sliceSpent;
                if (left <= 0) throw new StepLimitSignal();
                size = Math.Min(size, left);
            }
            _sliceSize = size;
            _budget = size;
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
            f.IsBoundary = false;
            f.Closure = null;
            f.HandlerCount = 0;
            f.ExcInfo = default;
            f.IsConstructor = false;
            f.Constructed = default;
            f.Owner = null;
        }
    }
}
