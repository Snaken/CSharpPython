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

        /// <summary>The <c>namedtuple</c> type this tuple is an instance of, or null for a plain tuple.</summary>
        internal PyType Named;

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

    /// <summary>Which kind of dictionary a <see cref="PyDict"/> is: a plain one, or one of the <c>collections</c> types.</summary>
    internal enum DictFlavor : byte { Plain, Default, Counter }

    /// <summary>A Python range. Lengths are computed, never materialised.</summary>
    public enum DictViewKind : byte { Keys, Values, Items }

    /// <summary>A live view of a dictionary's keys, values or (key, value) pairs.</summary>
    public sealed class PyDictView
    {
        public readonly PyDict Dict;
        public readonly DictViewKind Mode;

        public PyDictView(PyDict dict, DictViewKind mode)
        {
            Dict = dict;
            Mode = mode;
        }

        public string TypeName => Mode == DictViewKind.Keys ? "dict_keys" : Mode == DictViewKind.Values ? "dict_values" : "dict_items";

        /// <summary>Keys and items views behave like sets; a values view does not.</summary>
        public bool IsSetLike => Mode != DictViewKind.Values;

        public List<Value> Members()
        {
            var result = new List<Value>(Dict.Count);
            foreach (var kv in Dict.Pairs())
                result.Add(Mode == DictViewKind.Keys ? kv.Key : Mode == DictViewKind.Values ? kv.Value : Value.Tuple(kv.Key, kv.Value));
            return result;
        }
    }

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
    /// when they outnumber live entries. A small dictionary whose keys are all strings (an object's fields, say) is
    /// searched in place, and only gets a hash index when it grows or meets another kind of key.
    /// </summary>
    public sealed class PyDict
    {
        private struct Entry
        {
            public Value Key, Val;
            public bool Alive;
        }

        private const int SmallLimit = 8;
        private static readonly Entry[] NoEntries = new Entry[0];

        private Dictionary<Value, int> _index;   // null while the dictionary is small
        private Entry[] _entries = NoEntries;
        private int _used;                        // entry slots in use, including deleted ones
        private int _count;                       // live entries (not kept up to date while _pending)

        /// <summary>Incremented whenever entries move, so iterators can detect concurrent modification.</summary>
        internal int Version { get; private set; }

        // After a snapshot restore the entries are known but the lookup index is not built yet: hashing a key may
        // run a script's __hash__, which needs the restored task to be running. EnsureIndex builds it on first use.
        private bool _pending;

        /// <summary>True when this dictionary holds the members of a set, which only changes error wording.</summary>
        internal bool ForSet;

        /// <summary>Plain, or a <c>defaultdict</c> or <c>Counter</c>, which answer a missing key differently.</summary>
        internal DictFlavor Flavor;

        /// <summary>The <c>default_factory</c> of a <c>defaultdict</c> (None for none).</summary>
        internal Value Factory;

        /// <summary>An empty dictionary of the same flavour, with the same factory.</summary>
        internal PyDict EmptyLike() => new PyDict { Flavor = Flavor, Factory = Factory };

        public int Count
        {
            get
            {
                if (!_pending) return _count;
                int n = 0;
                for (int i = 0; i < _used; i++) if (_entries[i].Alive) n++;
                return n;
            }
        }

        /// <summary>Adds an entry while restoring a snapshot, without hashing the key.</summary>
        internal void RestoreEntry(Value key, Value value)
        {
            Append(key, value);
            _pending = true;
        }

        private void Append(Value key, Value value)
        {
            if (_used == _entries.Length) Array.Resize(ref _entries, _used == 0 ? 4 : _used * 2);
            _entries[_used++] = new Entry { Key = key, Val = value, Alive = true };
        }

        private void EnsureIndex()
        {
            if (!_pending) return;
            if (PyTask.Current == null)
            {
                for (int i = 0; i < _used; i++)
                    if (_entries[i].Alive && NeedsScript(_entries[i].Key))
                        throw new InvalidOperationException("A dictionary keyed by script objects can only be looked up while its script is running.");
            }
            _pending = false;
            _index = null;
            _count = 0;
            bool small = _used <= SmallLimit;
            for (int i = 0; i < _used; i++)
            {
                if (!_entries[i].Alive) continue;
                _count++;
                if (_entries[i].Key.Kind != ValueKind.Str) small = false;
            }
            if (!small) BuildIndex();
        }

        private void BuildIndex()
        {
            var index = new Dictionary<Value, int>(Math.Max(_used, 16), ValueComparer.Instance);
            for (int i = 0; i < _used; i++)
                if (_entries[i].Alive) index[_entries[i].Key] = i;
            _index = index;
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

        // The slot holding the key, or -1. A small dictionary is searched in place; any key that is not a string
        // gives it a hash index first, which also makes an unhashable key raise its TypeError.
        private int Find(Value key)
        {
            EnsureIndex();
            if (_index == null)
            {
                if (key.Kind == ValueKind.Str)
                {
                    string name = (string)key.RawObject;
                    for (int i = 0; i < _used; i++)
                    {
                        ref Entry e = ref _entries[i];
                        if (e.Key.Kind == ValueKind.Str && ((object)e.Key.RawObject == name || (string)e.Key.RawObject == name) && e.Alive) return i;
                    }
                    return -1;
                }
                BuildIndex();
            }
            // An empty Dictionary never hashes its key, so unhashable keys would slip through; check explicitly.
            if (_index.Count == 0) Ops.Hash(key);
            return _index.TryGetValue(key, out int slot) ? slot : -1;
        }

        public bool ContainsKey(Value key) => Find(key) >= 0;

        public bool TryGetValue(Value key, out Value value)
        {
            int slot = Find(key);
            if (slot < 0)
            {
                value = default;
                return false;
            }
            value = _entries[slot].Val;
            return true;
        }

        public void Set(Value key, Value value)
        {
            int slot = Find(key);
            if (slot >= 0)
            {
                _entries[slot].Val = value;
                return;
            }
            if (_index == null && _used >= SmallLimit) BuildIndex();
            if (_index != null)
            {
                if (_used - _count > 16 && _used - _count > _count) Compact();
                _index[key] = _used;
            }
            Append(key, value);
            _count++;
        }

        public bool Remove(Value key)
        {
            int slot = Find(key);
            if (slot < 0) return false;
            _index?.Remove(key);
            _entries[slot] = default;
            _count--;
            return true;
        }

        public void Clear()
        {
            _pending = false;
            _index = null;
            _entries = NoEntries;
            _used = 0;
            _count = 0;
            Version++;
        }

        /// <summary>Number of entry slots, including deleted ones. Use with <see cref="TryGetEntry"/>.</summary>
        internal int SlotCount => _used;

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
            for (int i = 0; i < _used; i++)
            {
                var e = _entries[i];
                if (e.Alive) yield return new KeyValuePair<Value, Value>(e.Key, e.Val);
            }
        }

        private void Compact()
        {
            EnsureIndex();
            var fresh = new Entry[Math.Max(_count, 4)];
            int n = 0;
            for (int i = 0; i < _used; i++)
            {
                if (!_entries[i].Alive) continue;
                if (_index != null) _index[_entries[i].Key] = n;
                fresh[n++] = _entries[i];
            }
            _entries = fresh;
            _used = n;
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
