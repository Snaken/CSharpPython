using System;
using System.Collections.Generic;
using System.Numerics;

namespace CSharpPython
{
    public sealed partial class PyTask
    {
        // What the last measurement found, and an upper bound on how much the task can have grown since then (see
        // MemoryLimitExceeded).
        private long _memoryUsed;
        private long _memoryGrowth;

        /// <summary>
        /// An estimate of the memory the script holds, in bytes: everything the task can reach (its globals, stack, call
        /// frames, queued events and what they hold), with approximate sizes for each kind of value. It is not exact (it
        /// ignores the allocator's rounding and what the host holds), but it grows and shrinks with what the script keeps,
        /// so it is what <see cref="VmOptions.MaxMemoryBytes"/> is compared with. Objects shared with other tasks are
        /// counted in each. Walking a large heap takes time, so call it for display, not every frame.
        /// </summary>
        public long EstimatedMemoryBytes => MeasureMemory(long.MaxValue);

        // Called at the end of a slice: stops the task with an uncatchable error when it holds more than MaxMemoryBytes.
        // Walking the heap costs about as much as running a step for every object in it, so it is not done every slice.
        // Instead the task keeps a bound on how much it can have grown since the last walk (a script allocates at most a
        // small object for each step, and building a big value is charged as steps, see Work) and walks again only when
        // the last measurement plus that bound could reach the limit. A script far below its limit is almost never
        // walked; one close to it is walked often enough not to pass it by much.
        private bool MemoryLimitExceeded(long stepsRun)
        {
            long limit = Vm.Options.MaxMemoryBytes;
            if (limit <= 0) return false;
            _memoryGrowth += stepsRun * MemorySizes.BytesPerStep;
            if (_memoryUsed + _memoryGrowth <= limit) return false;

            _memoryUsed = MeasureMemory(limit);
            _memoryGrowth = 0;
            if (_memoryUsed <= limit) return false;
            Fail(new PyRuntimeError("MemoryLimitExceeded",
                "the script held more than " + limit + " bytes of memory and was stopped", _script.Name, CurrentTrace()));
            return true;
        }

        // Adds up the estimated size of everything reachable from the task, and stops as soon as it is over
        // stopAbove (so a measurement against a limit costs at most about the limit's worth of objects).
        private long MeasureMemory(long stopAbove)
        {
            var meter = new MemoryMeter(stopAbove);
            meter.AddBytes(MemorySizes.Slot * (_stack.Length + 8L * _frames.Length));
            meter.Visit(_result);
            foreach (var kv in Globals) meter.Visit(kv.Value);
            foreach (var ev in _events) foreach (var a in ev.Args) meter.Visit(a);
            for (int i = 0; i < _sp && !meter.Over; i++) meter.Visit(_stack[i]);
            for (int i = 0; i < _frameCount && !meter.Over; i++)
            {
                var f = _frames[i];
                meter.Visit(f.ExcInfo);
                meter.Visit(f.Constructed);
                meter.Visit(f.GenDefault);
                meter.Visit(f.CacheKey);
                if (f.Closure != null) foreach (var cell in f.Closure) meter.Visit(Value.FromCell(cell));
                if (f.Generator != null) meter.Visit(Value.FromIterator(f.Generator));
            }
            meter.Visit(_lastReturn);
            return meter.Measure();
        }
    }

    // Approximate sizes in bytes of the objects a script builds. They are estimates: the point is that the total grows
    // and shrinks with what a script keeps, in about the right proportion for each kind of value.
    internal static class MemorySizes
    {
        public const long Slot = 24;        // one Value
        public const long Object = 40;      // header and fields of a small object
        public const long ListHead = 32;
        public const long DictEntry = 3 * Slot; // key, value and the bookkeeping of the entry
        public const long SetEntry = 2 * Slot;
        public const long StringHead = 24;
        public const long TraceFrame = 16;  // a function name and a line, for each frame of an exception's traceback

        // The most a step is assumed to allocate on average, for deciding when to measure again. Work done on a big value
        // is charged as steps, so a bulk allocation counts for more (see Work.Items).
        public const long BytesPerStep = 32;

        // A string shorter than this is counted every time a slot holds it, which is cheaper than finding out whether it was seen.
        public const int SharedStringLength = 16;
    }

    // Walks the objects reachable from some values with a work list (no recursion, so nesting cannot overflow the stack)
    // and adds up their estimated sizes. Each object is counted once, however many places hold it.
    internal sealed class MemoryMeter
    {
        private readonly long _stopAbove;
        private readonly HashSet<object> _seen = new HashSet<object>(ReferenceComparer.Instance);
        private readonly Stack<Value> _pending = new Stack<Value>();
        private long _bytes;

        public MemoryMeter(long stopAbove) => _stopAbove = stopAbove;

        public bool Over => _bytes > _stopAbove;

        public void AddBytes(long n) => _bytes += n;

        /// <summary>Queues a value to be counted.</summary>
        public void Visit(Value v)
        {
            if (Counted(v.Kind) && !Over) _pending.Push(v);
        }

        /// <summary>Counts everything queued so far (and what it holds) and returns the total.</summary>
        public long Measure()
        {
            while (_pending.Count > 0 && !Over) Count(_pending.Pop());
            return _bytes;
        }

        // The kinds that own memory of their own; numbers, handles and host functions are only the slot that holds them.
        private static bool Counted(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Str: case ValueKind.BigInt: case ValueKind.List: case ValueKind.Tuple: case ValueKind.Deque:
                case ValueKind.Dict: case ValueKind.Set: case ValueKind.DictView: case ValueKind.Class: case ValueKind.Instance:
                case ValueKind.Function: case ValueKind.Cell: case ValueKind.ScriptMethod: case ValueKind.BoundMethod:
                case ValueKind.Descriptor: case ValueKind.Functool: case ValueKind.Slice: case ValueKind.Exception:
                case ValueKind.Iterator:
                    return true;
                default:
                    return false;
            }
        }

        private bool First(object o) => _seen.Add(o);

        private void Count(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.Str:
                {
                    var text = (string)v.RawObject;
                    if (text.Length < MemorySizes.SharedStringLength || First(text)) _bytes += MemorySizes.StringHead + 2L * text.Length;
                    break;
                }
                case ValueKind.BigInt:
                    if (First(v.RawObject)) _bytes += MemorySizes.Object + Ops.BitLength((BigInteger)v.RawObject) / 8;
                    break;
                case ValueKind.List:
                    if (First(v.RawObject))
                    {
                        var items = ((PyList)v.RawObject).Items;
                        _bytes += MemorySizes.ListHead + MemorySizes.Slot * items.Capacity;
                        foreach (var item in items) Visit(item);
                    }
                    break;
                case ValueKind.Tuple:
                    if (First(v.RawObject))
                    {
                        var items = ((PyTuple)v.RawObject).Items;
                        _bytes += MemorySizes.ListHead + MemorySizes.Slot * items.Length;
                        foreach (var item in items) Visit(item);
                    }
                    break;
                case ValueKind.Deque:
                    if (First(v.RawObject))
                    {
                        var deque = (PyDeque)v.RawObject;
                        _bytes += MemorySizes.ListHead + MemorySizes.Slot * deque.Count;
                        foreach (var item in deque.ToList()) Visit(item);
                    }
                    break;
                case ValueKind.Dict:
                    if (First(v.RawObject)) CountDict((PyDict)v.RawObject);
                    break;
                case ValueKind.Set:
                    if (First(v.RawObject))
                    {
                        var set = ((PySet)v.RawObject).Items;
                        _bytes += MemorySizes.Object + MemorySizes.SetEntry * set.Count;
                        foreach (var pair in set.Pairs()) Visit(pair.Key);
                    }
                    break;
                case ValueKind.DictView:
                    if (First(v.RawObject)) { _bytes += MemorySizes.Object; Visit(Value.FromDict(((PyDictView)v.RawObject).Dict)); }
                    break;
                case ValueKind.Class:
                    if (First(v.RawObject))
                    {
                        var cls = (PyClass)v.RawObject;
                        _bytes += 4 * MemorySizes.Object;
                        if (cls.Base != null) Visit(Value.FromClass(cls.Base));
                        Visit(Value.FromDict(cls.Namespace));
                    }
                    break;
                case ValueKind.Instance:
                    if (First(v.RawObject))
                    {
                        var inst = (PyInstance)v.RawObject;
                        _bytes += MemorySizes.Object;
                        Visit(Value.FromClass(inst.Class));
                        Visit(Value.FromDict(inst.Fields));
                    }
                    break;
                case ValueKind.Function:
                    if (First(v.RawObject))
                    {
                        var fn = (PyFunction)v.RawObject;
                        _bytes += 2 * MemorySizes.Object + MemorySizes.Slot * (fn.Defaults.Length + fn.Closure.Length);
                        foreach (var d in fn.Defaults) Visit(d);
                        foreach (var cell in fn.Closure) Visit(Value.FromCell(cell));
                        if (fn.Owner != null) Visit(Value.FromClass(fn.Owner));
                    }
                    break;
                case ValueKind.Cell:
                    if (First(v.RawObject)) { _bytes += MemorySizes.Object; Visit(((Cell)v.RawObject).Value); }
                    break;
                case ValueKind.ScriptMethod:
                    if (First(v.RawObject))
                    {
                        var m = (ScriptMethod)v.RawObject;
                        _bytes += MemorySizes.Object;
                        Visit(m.Self);
                        Visit(Value.FromFunction(m.Function));
                    }
                    break;
                case ValueKind.BoundMethod:
                    if (First(v.RawObject)) { _bytes += MemorySizes.Object; Visit(((BoundMethod)v.RawObject).Self); }
                    break;
                case ValueKind.Descriptor:
                    if (First(v.RawObject))
                    {
                        var d = (PyDescriptor)v.RawObject;
                        _bytes += 2 * MemorySizes.Object;
                        Visit(d.Function);
                        Visit(d.Getter);
                        Visit(d.Setter);
                        Visit(d.Deleter);
                    }
                    break;
                case ValueKind.Functool:
                    if (First(v.RawObject))
                    {
                        var f = (PyFunctool)v.RawObject;
                        _bytes += 2 * MemorySizes.Object + MemorySizes.Slot * (f.Args.Length + f.KwValues.Length);
                        Visit(f.Func);
                        foreach (var a in f.Args) Visit(a);
                        foreach (var a in f.KwValues) Visit(a);
                        if (f.Cache != null) Visit(Value.FromDict(f.Cache));
                    }
                    break;
                case ValueKind.Slice:
                    if (First(v.RawObject))
                    {
                        var s = (PySlice)v.RawObject;
                        _bytes += MemorySizes.Object;
                        Visit(s.Start);
                        Visit(s.Stop);
                        Visit(s.Step);
                    }
                    break;
                case ValueKind.Exception:
                    if (First(v.RawObject))
                    {
                        var e = (PyExceptionValue)v.RawObject;
                        _bytes += 3 * MemorySizes.Object + MemorySizes.TraceFrame * (e.Trace?.Count ?? 0);
                        Visit(e.Payload);
                        Visit(e.Cause);
                        Visit(e.Context);
                    }
                    break;
                case ValueKind.Iterator:
                    if (First(v.RawObject)) CountIterator((PyIterator)v.RawObject);
                    break;
                default:
                    break;
            }
        }

        private void CountDict(PyDict dict)
        {
            _bytes += MemorySizes.Object + MemorySizes.DictEntry * dict.Count;
            foreach (var pair in dict.Pairs())
            {
                Visit(pair.Key);
                Visit(pair.Value);
            }
            if (dict.Flavor == DictFlavor.Default) Visit(dict.Factory);
        }

        private void CountIterator(PyIterator it)
        {
            _bytes += 2 * MemorySizes.Object;
            switch (it)
            {
                case ListIterator l: Visit(Value.FromList(l.List)); break;
                case DequeIterator q: Visit(Value.FromDeque(q.Deque)); break;
                case TupleIterator t: Visit(Value.FromTuple(t.Tuple)); break;
                case DictKeyIterator d: Visit(Value.FromDict(d.Dict)); break;
                case InstanceIterator i: Visit(i.Target); break;
                case PyGenerator g:
                    _bytes += 4 * MemorySizes.Object + MemorySizes.Slot * ((g.Saved?.Length ?? 0) + (g.Spare?.Length ?? 0));
                    if (g.Closure != null) foreach (var cell in g.Closure) Visit(Value.FromCell(cell));
                    if (g.Owner != null) Visit(Value.FromClass(g.Owner));
                    if (g.Saved != null) foreach (var item in g.Saved) Visit(item);
                    Visit(g.ExcInfo);
                    Visit(g.ReturnValue);
                    break;
                case EnumerateIterator e: Visit(Value.FromIterator(e.Source)); break;
                case ZipIterator z: foreach (var s in z.Sources) Visit(Value.FromIterator(s)); break;
                case MapIterator m:
                    Visit(m.Function);
                    foreach (var s in m.Sources) Visit(Value.FromIterator(s));
                    break;
                case FilterIterator f:
                    Visit(f.Function);
                    Visit(Value.FromIterator(f.Source));
                    break;
                case ItertoolsIterator t:
                    foreach (var s in t.Sources) if (s != null) Visit(Value.FromIterator(s));
                    foreach (var a in t.Args) Visit(a);
                    _bytes += MemorySizes.Slot * t.Items.Count;
                    foreach (var item in t.Items) Visit(item);
                    break;
            }
        }
    }
}
