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
                throw new PyError("RuntimeError", "dictionary changed size during iteration");
            if (_dict.Version != _version)
                throw new PyError("RuntimeError", "dictionary changed during iteration");
            while (_slot < _dict.SlotCount)
            {
                if (_dict.TryGetEntry(_slot++, out value, out _)) return true;
            }
            value = default;
            return false;
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
}
