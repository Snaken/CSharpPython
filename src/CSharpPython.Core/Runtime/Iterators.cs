using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>Base class of all iterators. They are plain data so a running script can be saved.</summary>
    internal abstract class PyIterator
    {
        /// <summary>Produces the next value, or returns false when the sequence is exhausted.</summary>
        public abstract bool Next(out Value value);
    }

    internal sealed class RangeIterator : PyIterator
    {
        private readonly PyRange _range;
        private long _index;
        private readonly long _length;

        public PyRange Range => _range;

        /// <summary>How many values have been produced. Settable so a snapshot can resume mid-way.</summary>
        public long Index { get => _index; set => _index = value; }

        public RangeIterator(PyRange range)
        {
            _range = range;
            _length = range.Length;
        }

        public override bool Next(out Value value)
        {
            if (_index >= _length) { value = default; return false; }
            value = Value.FromInt(_range[_index++]);
            return true;
        }
    }

    internal sealed class ListIterator : PyIterator
    {
        private readonly PyList _list;
        private int _index;

        public ListIterator(PyList list) { _list = list; }

        public PyList List => _list;

        public int Index { get => _index; set => _index = value; }

        public override bool Next(out Value value)
        {
            if (_index >= _list.Items.Count) { value = default; return false; }
            value = _list.Items[_index++];
            return true;
        }
    }

    internal sealed class DequeIterator : PyIterator
    {
        private readonly PyDeque _deque;
        private int _index;
        private int _version;

        public DequeIterator(PyDeque deque) { _deque = deque; _version = deque.Version; }

        public PyDeque Deque => _deque;

        public int Index { get => _index; set => _index = value; }

        /// <summary>After a restore the saved iterator takes the version of the deque as it is now.</summary>
        public void Resync() { _version = _deque.Version; }

        public override bool Next(out Value value)
        {
            if (_version != _deque.Version) throw new PyError("RuntimeError", "deque mutated during iteration");
            if (_index >= _deque.Count) { value = default; return false; }
            value = _deque[_index++];
            return true;
        }
    }

    internal sealed class TupleIterator : PyIterator
    {
        private readonly PyTuple _tuple;
        private int _index;

        public TupleIterator(PyTuple tuple) { _tuple = tuple; }

        public PyTuple Tuple => _tuple;

        public int Index { get => _index; set => _index = value; }

        public override bool Next(out Value value)
        {
            if (_index >= _tuple.Items.Length) { value = default; return false; }
            value = _tuple.Items[_index++];
            return true;
        }
    }

    internal sealed class StringIterator : PyIterator
    {
        private readonly string _text;
        private int _index;

        public StringIterator(string text) { _text = text; }

        public string Text => _text;

        public int Index { get => _index; set => _index = value; }

        public override bool Next(out Value value)
        {
            if (_index >= _text.Length) { value = default; return false; }
            value = Value.FromString(_text[_index++].ToString());
            return true;
        }
    }

    internal sealed class DictKeyIterator : PyIterator
    {
        private readonly PyDict _dict;
        private int _version;
        private int _count;
        private int _slot;
        private bool _staleOnRestore;

        public DictKeyIterator(PyDict dict)
        {
            _dict = dict;
            _version = dict.Version;
            _count = dict.Count;
        }

        // Restores an iterator over a rebuilt (tombstone-free) dict that has already produced 'produced' keys.
        // The dict is still being filled when this is called, so <see cref="FinishRestore"/> must follow.
        public DictKeyIterator(PyDict dict, int produced, bool stale)
        {
            _dict = dict;
            _slot = produced;
            _staleOnRestore = stale;
        }

        public void FinishRestore()
        {
            _version = _dict.Version;
            _count = _staleOnRestore ? -1 : _dict.Count;
        }

        public PyDict Dict => _dict;

        /// <summary>True if the dict was modified since the iterator was made, so the next step raises.</summary>
        public bool IsStale => _dict.Count != _count || _dict.Version != _version;

        /// <summary>How many keys have been produced (the number of live entries before the cursor).</summary>
        public int Produced
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slot && i < _dict.SlotCount; i++)
                    if (_dict.TryGetEntry(i, out _, out _)) n++;
                return n;
            }
        }

        public override bool Next(out Value value)
        {
            if (_dict.Count != _count)
                throw new PyError("RuntimeError", _dict.ForSet ? "Set changed size during iteration" : "dictionary changed size during iteration");
            if (_dict.Version != _version)
                throw new PyError("RuntimeError", _dict.ForSet ? "Set changed size during iteration" : "dictionary changed during iteration");
            while (_slot < _dict.SlotCount)
            {
                if (_dict.TryGetEntry(_slot++, out value, out _)) return true;
            }
            value = default;
            return false;
        }
    }

    /// <summary>Iterates an object of a script class by calling its <c>__next__</c> method.</summary>
    internal sealed class InstanceIterator : PyIterator
    {
        private readonly Value _target;

        public InstanceIterator(Value target) { _target = target; }

        public Value Target => _target;

        public override bool Next(out Value value)
        {
            var task = PyTask.Current;
            if (task == null) throw new PyError("RuntimeError", "a script object can only be iterated while its script is running");
            return task.CallNext(_target, out value);
        }
    }

    internal sealed class EnumerateIterator : PyIterator
    {
        private readonly PyIterator _source;
        private long _index;

        public EnumerateIterator(PyIterator source, long start)
        {
            _source = source;
            _index = start;
        }

        public PyIterator Source => _source;

        public long Index => _index;

        public override bool Next(out Value value)
        {
            if (!_source.Next(out Value item)) { value = default; return false; }
            value = Value.Tuple(Value.FromInt(_index++), item);
            return true;
        }
    }

    internal sealed class ZipIterator : PyIterator
    {
        private readonly PyIterator[] _sources;

        public ZipIterator(PyIterator[] sources) { _sources = sources; }

        public PyIterator[] Sources => _sources;

        public override bool Next(out Value value)
        {
            if (_sources.Length == 0) { value = default; return false; }
            var items = new Value[_sources.Length];
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Next(out items[i])) { value = default; return false; }
            }
            value = Value.Tuple(items);
            return true;
        }
    }
    internal sealed class MapIterator : PyIterator
    {
        private readonly Value _function;
        private readonly PyIterator[] _sources;

        public MapIterator(Value function, PyIterator[] sources)
        {
            _function = function;
            _sources = sources;
        }

        public Value Function => _function;

        public PyIterator[] Sources => _sources;

        public override bool Next(out Value value)
        {
            var task = PyTask.Current;
            if (_sources.Length == 1)
            {
                if (!_sources[0].Next(out Value single)) { value = default; return false; }
                if (task == null) throw new PyError("RuntimeError", "map() can only be consumed while a script is running");
                value = task.CallSync(_function, single);
                return true;
            }
            var args = new Value[_sources.Length];
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Next(out args[i])) { value = default; return false; }
            }
            if (task == null) throw new PyError("RuntimeError", "map() can only be consumed while a script is running");
            value = task.CallSync(_function, args);
            return true;
        }
    }

    internal sealed class FilterIterator : PyIterator
    {
        private readonly Value _function;
        private readonly PyIterator _source;

        public FilterIterator(Value function, PyIterator source)
        {
            _function = function;
            _source = source;
        }

        public Value Function => _function;

        public PyIterator Source => _source;

        public override bool Next(out Value value)
        {
            while (_source.Next(out value))
            {
                if (_function.IsNone)
                {
                    if (value.IsTruthy) return true;
                    continue;
                }
                var task = PyTask.Current;
                if (task == null) throw new PyError("RuntimeError", "filter() can only be consumed while a script is running");
                if (task.CallSync(_function, value).IsTruthy) return true;
            }
            return false;
        }
    }
}
