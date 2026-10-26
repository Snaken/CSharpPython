using System;
using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>
    /// A <c>collections.deque</c>: a ring buffer, so adding and removing at either end takes constant time.
    /// With a <see cref="MaxLen"/>, adding to a full deque drops an item from the other end.
    /// </summary>
    public sealed class PyDeque
    {
        private Value[] _buf = new Value[8];   // the length is always a power of two
        private int _head;
        private int _count;

        /// <summary>The most items the deque may hold, or -1 for no limit.</summary>
        public readonly long MaxLen;

        /// <summary>Incremented by every change, so an iterator can notice that the deque changed under it.</summary>
        internal int Version { get; private set; }

        public PyDeque(long maxLen = -1) { MaxLen = maxLen; }

        public int Count => _count;

        public Value this[int index]
        {
            get => _buf[(_head + index) & (_buf.Length - 1)];
            set { _buf[(_head + index) & (_buf.Length - 1)] = value; Version++; }
        }

        private void Grow()
        {
            if (_count < _buf.Length) return;
            var bigger = new Value[_buf.Length * 2];
            for (int i = 0; i < _count; i++) bigger[i] = this[i];
            _buf = bigger;
            _head = 0;
        }

        public void AddRight(Value v)
        {
            if (MaxLen == 0) return;
            if (MaxLen > 0 && _count == MaxLen) PopLeft();
            Grow();
            _buf[(_head + _count) & (_buf.Length - 1)] = v;
            _count++;
            Version++;
        }

        public void AddLeft(Value v)
        {
            if (MaxLen == 0) return;
            if (MaxLen > 0 && _count == MaxLen) PopRight();
            Grow();
            _head = (_head - 1) & (_buf.Length - 1);
            _buf[_head] = v;
            _count++;
            Version++;
        }

        public Value PopRight()
        {
            if (_count == 0) throw new PyError("IndexError", "pop from an empty deque");
            int at = (_head + _count - 1) & (_buf.Length - 1);
            Value v = _buf[at];
            _buf[at] = default;
            _count--;
            Version++;
            return v;
        }

        public Value PopLeft()
        {
            if (_count == 0) throw new PyError("IndexError", "pop from an empty deque");
            Value v = _buf[_head];
            _buf[_head] = default;
            _head = (_head + 1) & (_buf.Length - 1);
            _count--;
            Version++;
            return v;
        }

        public void Clear()
        {
            _buf = new Value[8];
            _head = 0;
            _count = 0;
            Version++;
        }

        public List<Value> ToList()
        {
            var list = new List<Value>(_count);
            for (int i = 0; i < _count; i++) list.Add(this[i]);
            return list;
        }

        /// <summary>Replaces the contents (used by the operations that are easiest as list operations). The limit still applies.</summary>
        internal void ReplaceWith(List<Value> items)
        {
            Clear();
            foreach (var item in items) AddRight(item);
        }

        public void Rotate(long steps)
        {
            if (_count <= 1) return;
            int n = (int)(steps % _count);
            if (n == 0) return;
            var items = ToList();
            var rotated = new List<Value>(_count);
            for (int i = 0; i < _count; i++) rotated.Add(items[((i - n) % _count + _count) % _count]);
            ReplaceWith(rotated);
        }
    }
}
