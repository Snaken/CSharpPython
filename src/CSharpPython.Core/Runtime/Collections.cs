using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>A Python list.</summary>
    public sealed class PyList
    {
        public readonly List<Value> Items;

        public PyList() { Items = new List<Value>(); }

        public PyList(IEnumerable<Value> items) { Items = new List<Value>(items); }

        public int Count => Items.Count;
    }

    /// <summary>A Python tuple (immutable).</summary>
    public sealed class PyTuple
    {
        public readonly Value[] Items;

        public PyTuple(Value[] items) { Items = items; }

        public int Count => Items.Length;
    }

    /// <summary>A Python range. Lengths are computed, never materialised.</summary>
    public sealed class PyRange
    {
        public readonly long Start, Stop, Step;

        public PyRange(long start, long stop, long step)
        {
            Start = start;
            Stop = stop;
            Step = step;
        }

        public long Length
        {
            get
            {
                if (Step > 0) return Start < Stop ? (long)(((ulong)(Stop - Start) - 1) / (ulong)Step) + 1 : 0;
                return Start > Stop ? (long)(((ulong)(Start - Stop) - 1) / (ulong)(-Step)) + 1 : 0;
            }
        }

        public long this[long index] => Start + index * Step;
    }

    /// <summary>The value of a slice expression such as <c>1:3</c>.</summary>
    internal sealed class PySlice
    {
        public readonly Value Start, Stop, Step;

        public PySlice(Value start, Value stop, Value step)
        {
            Start = start;
            Stop = stop;
            Step = step;
        }
    }

    /// <summary>
    /// An insertion-ordered dictionary, like Python's. Deleted entries leave tombstones that are compacted away
    /// when they outnumber live entries.
    /// </summary>
    public sealed class PyDict
    {
        private struct Entry
        {
            public Value Key, Val;
            public bool Alive;
        }

        private readonly Dictionary<Value, int> _index = new Dictionary<Value, int>(ValueComparer.Instance);
        private List<Entry> _entries = new List<Entry>();

        /// <summary>Incremented whenever entries move, so iterators can detect concurrent modification.</summary>
        internal int Version { get; private set; }

        public int Count => _index.Count;

        // An empty Dictionary never hashes its key, so unhashable keys would slip through; check explicitly.
        private void CheckHashable(Value key)
        {
            if (_index.Count == 0) Ops.Hash(key);
        }

        public bool ContainsKey(Value key)
        {
            CheckHashable(key);
            return _index.ContainsKey(key);
        }

        public bool TryGetValue(Value key, out Value value)
        {
            CheckHashable(key);
            if (_index.TryGetValue(key, out int i))
            {
                value = _entries[i].Val;
                return true;
            }
            value = default;
            return false;
        }

        public void Set(Value key, Value value)
        {
            if (_index.TryGetValue(key, out int i))
            {
                var e = _entries[i];
                e.Val = value;
                _entries[i] = e;
                return;
            }
            if (_entries.Count - _index.Count > 16 && _entries.Count - _index.Count > _index.Count) Compact();
            _index[key] = _entries.Count;
            _entries.Add(new Entry { Key = key, Val = value, Alive = true });
        }

        public bool Remove(Value key)
        {
            CheckHashable(key);
            if (!_index.TryGetValue(key, out int i)) return false;
            _index.Remove(key);
            _entries[i] = default;
            return true;
        }

        public void Clear()
        {
            _index.Clear();
            _entries = new List<Entry>();
            Version++;
        }

        /// <summary>Number of entry slots, including deleted ones. Use with <see cref="TryGetEntry"/>.</summary>
        internal int SlotCount => _entries.Count;

        internal bool TryGetEntry(int slot, out Value key, out Value value)
        {
            var e = _entries[slot];
            key = e.Key;
            value = e.Val;
            return e.Alive;
        }

        /// <summary>The live entries in insertion order.</summary>
        public IEnumerable<KeyValuePair<Value, Value>> Pairs()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Alive) yield return new KeyValuePair<Value, Value>(e.Key, e.Val);
            }
        }

        private void Compact()
        {
            var fresh = new List<Entry>(_index.Count);
            foreach (var e in _entries)
            {
                if (!e.Alive) continue;
                _index[e.Key] = fresh.Count;
                fresh.Add(e);
            }
            _entries = fresh;
            Version++;
        }
    }

    internal sealed class ValueComparer : IEqualityComparer<Value>
    {
        public static readonly ValueComparer Instance = new ValueComparer();

        public bool Equals(Value x, Value y) => Ops.Equal(x, y);

        public int GetHashCode(Value v) => Ops.Hash(v);
    }

    /// <summary>A module: a named bag of members, used for host APIs such as <c>import game</c>.</summary>
    public sealed class PyModule
    {
        public readonly string Name;
        public readonly Dictionary<string, Value> Members = new Dictionary<string, Value>();

        public PyModule(string name) { Name = name; }
    }
}
