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
        private readonly List<Value> _items;
        private int _index;

        public ListIterator(List<Value> items) { _items = items; }

        public override bool Next(out Value value)
        {
            if (_index >= _items.Count) { value = default; return false; }
            value = _items[_index++];
            return true;
        }
    }

    internal sealed class TupleIterator : PyIterator
    {
        private readonly Value[] _items;
        private int _index;

        public TupleIterator(Value[] items) { _items = items; }

        public override bool Next(out Value value)
        {
            if (_index >= _items.Length) { value = default; return false; }
            value = _items[_index++];
            return true;
        }
    }

    internal sealed class StringIterator : PyIterator
    {
        private readonly string _text;
        private int _index;

        public StringIterator(string text) { _text = text; }

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
        private readonly int _version;
        private readonly int _count;
        private int _slot;

        public DictKeyIterator(PyDict dict)
        {
            _dict = dict;
            _version = dict.Version;
            _count = dict.Count;
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
