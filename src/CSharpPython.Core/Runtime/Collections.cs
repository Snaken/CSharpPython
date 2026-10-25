using System;
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

    /// <summary>
    /// A Python set. It keeps its members as the keys of an insertion-ordered dictionary, so iteration order is the
    /// order of insertion (CPython's depends on hashes) and it snapshots and iterates like a dict.
    /// </summary>
    public sealed class PySet
    {
        internal PyDict Items;

        /// <summary>True for a <c>frozenset</c>: it cannot change and can be a dictionary key or a member of a set.</summary>
        public bool Frozen;

        public PySet() { Items = new PyDict { ForSet = true }; }

        public int Count => Items.Count;

        // Like CPython, looking up a set finds nothing instead of complaining that it cannot be hashed (members are
        // never sets, because sets are unhashable).
        public bool Contains(Value v) => Items.ContainsKey(Keyed(v));

        public void Add(Value v) => Items.Set(v, Value.None);

        public bool Remove(Value v) => Items.Remove(Keyed(v));

        // A set that is looked up is compared as the frozenset with the same members, the way Python does.
        private static Value Keyed(Value v)
        {
            if (v.Kind != ValueKind.Set) return v;
            var set = (PySet)v.RawObject;
            if (set.Frozen) return v;
            var frozen = set.Copy();
            frozen.Frozen = true;
            return Value.FromSet(frozen);
        }

        public IEnumerable<Value> Members()
        {
            foreach (var kv in Items.Pairs()) yield return kv.Key;
        }

        /// <summary>Makes this set frozen when <paramref name="model"/> is, and returns it.</summary>
        internal PySet LikeOf(PySet model)
        {
            Frozen = model.Frozen;
            return this;
        }

        public PySet Copy()
        {
            var copy = new PySet();
            foreach (var v in Members()) copy.Add(v);
            return copy;
        }
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

        // After a snapshot restore the entries are known but the lookup index is not built yet: hashing a key may
        // run a script's __hash__, which needs the restored task to be running. EnsureIndex builds it on first use.
        private bool _pending;

        /// <summary>True when this dictionary holds the members of a set, which only changes error wording.</summary>
        internal bool ForSet;

        public int Count
        {
            get
            {
                if (!_pending) return _index.Count;
                int n = 0;
                foreach (var e in _entries) if (e.Alive) n++;
                return n;
            }
        }

        /// <summary>Adds an entry while restoring a snapshot, without hashing the key.</summary>
        internal void RestoreEntry(Value key, Value value)
        {
            _entries.Add(new Entry { Key = key, Val = value, Alive = true });
            _pending = true;
        }

        private void EnsureIndex()
        {
            if (!_pending) return;
            if (PyTask.Current == null)
            {
                foreach (var e in _entries)
                    if (e.Alive && NeedsScript(e.Key))
                        throw new InvalidOperationException("A dictionary keyed by script objects can only be looked up while its script is running.");
            }
            _index.Clear();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Alive) _index[_entries[i].Key] = i;
            _pending = false;
        }

        private static bool NeedsScript(Value key)
        {
            if (key.Kind == ValueKind.Instance) return true;
            if (key.Kind == ValueKind.Set)
                foreach (var item in ((PySet)key.RawObject).Members()) if (NeedsScript(item)) return true;
            if (key.Kind == ValueKind.Tuple)
                foreach (var item in ((PyTuple)key.RawObject).Items) if (NeedsScript(item)) return true;
            return false;
        }

        // An empty Dictionary never hashes its key, so unhashable keys would slip through; check explicitly.
        private void CheckHashable(Value key)
        {
            EnsureIndex();
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
            EnsureIndex();
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
            _pending = false;
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
            EnsureIndex();
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
