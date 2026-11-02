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

        private PyList(List<Value> owned, bool adopt) { Items = owned; }

        /// <summary>A list that takes over <paramref name="owned"/> without copying it; the caller must not use the list afterwards.</summary>
        internal static PyList Adopt(List<Value> owned) => new PyList(owned, true);

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
    /// An insertion-ordered dictionary, like Python's. Keys, values and hashes sit in arrays in insertion order and a
    /// table of positions finds them (open addressing, as CPython's compact dict does), so an entry costs its key,
    /// its value and a few bytes of index. A set keeps no values at all: the value array exists only once a value
    /// other than None is stored. Deleted entries leave tombstones that are compacted away when they outnumber live
    /// entries. A small dictionary whose keys are all strings (an object's fields, say) is searched in place, and only
    /// gets the table when it grows or meets another kind of key.
    /// </summary>
    public sealed class PyDict
    {
        private const int SmallLimit = 8;
        private static readonly Value[] NoKeys = new Value[0];

        // A deleted entry's key is Unbound, which is never a real key.
        private Value[] _keys = NoKeys;

        // While every key is an integer, or every key is a plain tuple of the same number of integers (a cell (x, y)),
        // the keys live here instead of in _keys: _arity numbers a key (8 bytes each, against 24 and an object of its own
        // for a tuple), no stored hash (it follows from the numbers, and is the hash Ops.Hash gives the same tuple), and
        // nothing for the collector to scan. The first key of another shape moves everything back to _keys (ToGeneral).
        // A deleted entry is a set bit in _dead. A key read back from a tuple dictionary is a new tuple each time.
        private long[] _ints;
        private int _arity = 1;
        private ulong[] _dead;
        private Value[] _vals;                    // null while every value is None (always for a set)
        private int[] _hashes;                    // valid only once there is a table
        private int[] _table;                     // null while the dictionary is small; 0 empty, -1 deleted, else entry + 1
        private int _tableBits;
        private int _used;                        // entry slots in use, including deleted ones
        private int _count;                       // live entries (not kept up to date while _pending)

        /// <summary>Incremented whenever entries move, so iterators can detect concurrent modification.</summary>
        internal int Version { get; private set; }

        // After a snapshot restore the entries are known but the lookup table is not built yet: hashing a key may
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
                for (int i = 0; i < _used; i++) if (Alive(i)) n++;
                return n;
            }
        }

        /// <summary>True while every key is an integer and the keys are kept as plain numbers.</summary>
        internal bool IntKeyed => _ints != null;

        /// <summary>How many numbers each key takes while <see cref="IntKeyed"/> (1 for integers, 2 for a cell (x, y)).</summary>
        internal int KeyWidth => _arity;

        private const int MaxKeyWidth = 8;

        private int Capacity => _ints != null ? _ints.Length / _arity : _keys.Length;

        // How many numbers the key would take as a compact key: 1 for an integer, the length for a plain tuple of two to
        // MaxKeyWidth integers, 0 for anything else.
        private static int ShapeOf(Value key)
        {
            if (key.Kind == ValueKind.Int) return 1;
            if (key.Kind != ValueKind.Tuple) return 0;
            var tuple = (PyTuple)key.RawObject;
            if (tuple.Named != null) return 0;
            var items = tuple.Items;
            if (items.Length < 2 || items.Length > MaxKeyWidth) return 0;
            foreach (var item in items) if (item.Kind != ValueKind.Int) return 0;
            return items.Length;
        }

        private static int HashOfKey(Value key) =>
            key.Kind == ValueKind.Int ? Ops.HashInt(key.RawInt) : Ops.HashIntTuple(((PyTuple)key.RawObject).Items);

        private bool Alive(int i) =>
            _ints != null ? _dead == null || (_dead[i >> 6] & (1UL << (i & 63))) == 0 : _keys[i].Kind != ValueKind.Unbound;

        private Value KeyAt(int i)
        {
            if (_ints == null) return _keys[i];
            if (_arity == 1) return Value.FromInt(_ints[i]);
            var items = new Value[_arity];
            for (int j = 0; j < items.Length; j++) items[j] = Value.FromInt(_ints[i * _arity + j]);
            return Value.FromTuple(new PyTuple(items));
        }

        private int HashAt(int i)
        {
            if (_ints == null) return _hashes[i];
            if (_arity == 1) return Ops.HashInt(_ints[i]);
            return Ops.HashIntTuple(_ints, i * _arity, _arity);
        }

        private Value ValueAt(int i) => _vals == null ? default : _vals[i];

        private void StoreValue(int i, Value value)
        {
            if (_vals == null)
            {
                if (value.Kind == ValueKind.None) return;
                _vals = new Value[Capacity];
            }
            _vals[i] = value;
        }

        /// <summary>Adds an entry while restoring a snapshot, without hashing the key.</summary>
        internal void RestoreEntry(Value key, Value value)
        {
            if (_ints != null) ToGeneral();
            Append(key, value, 0);
            _pending = true;
        }

        private void Append(Value key, Value value, int hash)
        {
            if (_used == _keys.Length)
            {
                int size = _used == 0 ? 4 : _used * 2;
                Array.Resize(ref _keys, size);
                if (_vals != null) Array.Resize(ref _vals, size);
                if (_hashes != null) Array.Resize(ref _hashes, size);
            }
            _keys[_used] = key;
            if (_hashes != null) _hashes[_used] = hash;
            StoreValue(_used, value);
            _used++;
        }

        private void AppendInt(Value key, Value value)
        {
            if (_used == Capacity)
            {
                int size = _used == 0 ? 4 : _used * 2;
                Array.Resize(ref _ints, size * _arity);
                if (_vals != null) Array.Resize(ref _vals, size);
                if (_dead != null) Array.Resize(ref _dead, (size + 63) >> 6);
            }
            if (_arity == 1) _ints[_used] = key.RawInt;
            else
            {
                var items = ((PyTuple)key.RawObject).Items;
                for (int j = 0; j < _arity; j++) _ints[_used * _arity + j] = items[j].RawInt;
            }
            StoreValue(_used, value);
            _used++;
        }

        // Moves the keys back to Value slots, for the first key of another shape.
        private void ToGeneral()
        {
            int capacity = Capacity;
            var keys = new Value[capacity];
            var hashes = new int[capacity];
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i)) { keys[i] = Value.Unbound; continue; }
                keys[i] = KeyAt(i);
                hashes[i] = HashAt(i);
            }
            for (int i = _used; i < keys.Length; i++) keys[i] = Value.Unbound;
            _keys = keys;
            _hashes = hashes;
            _ints = null;
            _arity = 1;
            _dead = null;
            BuildTable(_used + 1);
        }

        // Keeps the keys of a dictionary that just had its index rebuilt as plain numbers when they all are integers, or
        // all are plain tuples of the same number of integers. Every entry keeps its slot (a deleted one stays deleted),
        // so an iteration in progress is not disturbed.
        private void TryUseInts()
        {
            if (_count == 0) return;
            int arity = 0;
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i)) continue;
                int shape = ShapeOf(_keys[i]);
                if (shape == 0 || (arity != 0 && shape != arity)) return;
                arity = shape;
            }
            int size = _keys.Length;
            var ints = new long[size * arity];
            ulong[] dead = null;
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i))
                {
                    if (dead == null) dead = new ulong[(size + 63) >> 6];
                    dead[i >> 6] |= 1UL << (i & 63);
                    continue;
                }
                if (arity == 1) ints[i] = _keys[i].RawInt;
                else
                {
                    var items = ((PyTuple)_keys[i].RawObject).Items;
                    for (int j = 0; j < arity; j++) ints[i * arity + j] = items[j].RawInt;
                }
            }
            _arity = arity;
            _ints = ints;
            _dead = dead;
            _keys = NoKeys;
            _hashes = null;
            BuildTable(_used + 1);
        }

        /// <summary>
        /// Gives a dictionary keyed by cells real key objects, so that a snapshot can refer to each key by identity (a
        /// compact one makes a new tuple every time a key is read). Returns false when nothing changed. Pair it with
        /// <see cref="Repack"/>.
        /// </summary>
        internal bool Unpack()
        {
            if (_ints == null || _arity == 1) return false;
            ToGeneral();
            return true;
        }

        /// <summary>Puts a dictionary that <see cref="Unpack"/> opened back to the compact layout, entries in the same slots.</summary>
        internal void Repack() => TryUseInts();

        private void EnsureIndex()
        {
            if (!_pending) return;
            if (PyTask.Current == null)
            {
                for (int i = 0; i < _used; i++)
                    if (Alive(i) && NeedsScript(_keys[i]))
                        throw new InvalidOperationException("A dictionary keyed by script objects can only be looked up while its script is running.");
            }
            _pending = false;
            _table = null;
            _hashes = null;
            _count = 0;
            bool small = _used <= SmallLimit;
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i)) continue;
                _count++;
                if (_keys[i].Kind != ValueKind.Str) small = false;
            }
            if (!small)
            {
                BuildIndex();
                TryUseInts();
            }
        }

        // Hashes every live key and builds the table. Nothing changes if hashing a key throws.
        private void BuildIndex()
        {
            var hashes = new int[_keys.Length];
            for (int i = 0; i < _used; i++)
                if (Alive(i)) hashes[i] = Ops.Hash(_keys[i]);
            _hashes = hashes;
            BuildTable(_used + 1);
        }

        // A table big enough for the given number of entries to fill no more than two thirds of it.
        private void BuildTable(int entries)
        {
            int bits = 3;
            while ((long)entries * 3 > (1L << bits) * 2) bits++;
            var table = new int[1 << bits];
            _tableBits = bits;
            _table = table;
            for (int i = 0; i < _used; i++)
                if (Alive(i)) Place(i, HashAt(i));
        }

        private int Spot(int hash) => (int)(((uint)hash * 2654435769u) >> (32 - _tableBits));

        private void Place(int entry, int hash)
        {
            int mask = _table.Length - 1;
            int pos = Spot(hash);
            while (_table[pos] > 0) pos = (pos + 1) & mask;
            _table[pos] = entry + 1;
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

        private static bool SameKey(Value a, Value b)
        {
            if (a.Kind == b.Kind)
            {
                if (a.Kind == ValueKind.Int) return a.RawInt == b.RawInt;
                if (a.Kind == ValueKind.Str) return (string)a.RawObject == (string)b.RawObject;
            }
            return Ops.Equal(a, b);
        }

        // The slot holding the key, or -1. A small dictionary is searched in place; any key that is not a string
        // gives it a table first, which also makes an unhashable key raise its TypeError. When there is a table, hash
        // is the key's hash and tablePos where the key sits in the table (-1 if it is absent).
        private int Find(Value key, out int hash, out bool hashed, out int tablePos)
        {
            hash = 0;
            hashed = false;
            tablePos = -1;
            EnsureIndex();
            if (_ints != null)
            {
                if (_arity == 1)
                {
                    if (key.Kind == ValueKind.Int)
                    {
                        hash = Ops.HashInt(key.RawInt);
                        hashed = true;
                        return ProbeInt(key.RawInt, hash, out tablePos);
                    }
                }
                else if (ShapeOf(key) == _arity)
                {
                    hash = Ops.HashIntTuple(((PyTuple)key.RawObject).Items);
                    hashed = true;
                    return ProbeCell(((PyTuple)key.RawObject).Items, hash, out tablePos);
                }
                ToGeneral();
            }
            if (_table == null)
            {
                if (key.Kind == ValueKind.Str)
                {
                    string name = (string)key.RawObject;
                    for (int i = 0; i < _used; i++)
                    {
                        ref Value k = ref _keys[i];
                        if (k.Kind == ValueKind.Str && ((object)k.RawObject == name || (string)k.RawObject == name)) return i;
                    }
                    return -1;
                }
                BuildIndex();
            }
            hash = Ops.Hash(key);
            hashed = true;
            return Probe(key, hash, out tablePos);
        }

        private int ProbeInt(long key, int hash, out int tablePos)
        {
            var table = _table;
            var ints = _ints;
            int mask = table.Length - 1;
            int pos = Spot(hash);
            while (true)
            {
                int t = table[pos];
                if (t == 0) break;
                if (t > 0 && ints[t - 1] == key)
                {
                    tablePos = pos;
                    return t - 1;
                }
                pos = (pos + 1) & mask;
            }
            tablePos = -1;
            return -1;
        }

        // The same for keys that are tuples of _arity integers.
        private int ProbeCell(Value[] wanted, int hash, out int tablePos)
        {
            var table = _table;
            var ints = _ints;
            int arity = _arity;
            int mask = table.Length - 1;
            int pos = Spot(hash);
            while (true)
            {
                int t = table[pos];
                if (t == 0) break;
                if (t > 0)
                {
                    int start = (t - 1) * arity, j = 0;
                    while (j < arity && ints[start + j] == wanted[j].RawInt) j++;
                    if (j == arity)
                    {
                        tablePos = pos;
                        return t - 1;
                    }
                }
                pos = (pos + 1) & mask;
            }
            tablePos = -1;
            return -1;
        }

        // Looking at a key may run a script's __eq__, which may change this dictionary; then the search starts again.
        private int Probe(Value key, int hash, out int tablePos)
        {
            while (true)
            {
                var table = _table;
                var hashes = _hashes;
                int version = Version, used = _used;
                int mask = table.Length - 1;
                int pos = Spot(hash);
                bool again = false;
                while (true)
                {
                    int t = table[pos];
                    if (t == 0) break;
                    if (t > 0 && hashes[t - 1] == hash)
                    {
                        var candidate = _keys[t - 1];
                        if (SameKey(candidate, key))
                        {
                            if (_table != table || Version != version || _used != used)
                            {
                                again = true;
                                break;
                            }
                            tablePos = pos;
                            return t - 1;
                        }
                        if (_table != table || Version != version || _used != used)
                        {
                            again = true;
                            break;
                        }
                    }
                    pos = (pos + 1) & mask;
                }
                if (!again)
                {
                    tablePos = -1;
                    return -1;
                }
            }
        }

        private int Find(Value key) => Find(key, out _, out _, out _);

        public bool ContainsKey(Value key) => Find(key) >= 0;

        /// <summary>Looks up a string key; the usual case of an attribute name, which a small dictionary can answer in place.</summary>
        internal bool TryGetStr(string name, out Value value)
        {
            if (_table == null && !_pending)
            {
                for (int i = 0; i < _used; i++)
                {
                    ref Value k = ref _keys[i];
                    if (k.Kind == ValueKind.Str && ((object)k.RawObject == name || (string)k.RawObject == name))
                    {
                        value = ValueAt(i);
                        return true;
                    }
                }
                value = default;
                return false;
            }
            return TryGetValue(Value.FromString(name), out value);
        }

        public bool TryGetValue(Value key, out Value value)
        {
            int slot = Find(key);
            if (slot < 0)
            {
                value = default;
                return false;
            }
            value = ValueAt(slot);
            return true;
        }

        public void Set(Value key, Value value)
        {
            int slot = Find(key, out int hash, out bool hashed, out _);
            if (slot >= 0)
            {
                StoreValue(slot, value);
                return;
            }
            int shape;
            if (_used == 0 && _ints == null && !_pending && (shape = ShapeOf(key)) > 0)
            {
                // The first key is an integer, or a tuple of integers: keep the keys as plain numbers until one of
                // another shape turns up.
                _arity = shape;
                _ints = new long[4 * shape];
                _keys = NoKeys;
                _hashes = null;
                _table = new int[8];
                _tableBits = 3;
                hash = HashOfKey(key);
            }
            if (_ints != null)
            {
                if (_used - _count > 16 && _used - _count > _count) CompactInts();
                if ((_used + 1) * 3 > _table.Length * 2)
                {
                    if (_used > _count) CompactInts();
                    else BuildTable(_used + 1);
                }
                AppendInt(key, value);
                Place(_used - 1, hash);
                _count++;
                return;
            }
            if (_table == null && _used >= SmallLimit) BuildIndex();
            if (_table != null)
            {
                if (!hashed) hash = Ops.Hash(key);
                if (_used - _count > 16 && _used - _count > _count) Compact();
                if ((_used + 1) * 3 > _table.Length * 2)
                {
                    if (_used > _count) Compact();
                    else BuildTable(_used + 1);
                }
            }
            Append(key, value, hash);
            if (_table != null) Place(_used - 1, hash);
            _count++;
        }

        public bool Remove(Value key)
        {
            int slot = Find(key, out _, out _, out int tablePos);
            if (slot < 0) return false;
            if (_table != null) _table[tablePos] = -1;
            if (_ints != null)
            {
                if (_dead == null) _dead = new ulong[(Capacity + 63) >> 6];
                _dead[slot >> 6] |= 1UL << (slot & 63);
            }
            else _keys[slot] = Value.Unbound;
            if (_vals != null) _vals[slot] = default;
            _count--;
            return true;
        }

        public void Clear()
        {
            _pending = false;
            _table = null;
            _hashes = null;
            _vals = null;
            _keys = NoKeys;
            _ints = null;
            _arity = 1;
            _dead = null;
            _used = 0;
            _count = 0;
            Version++;
        }

        /// <summary>Number of entry slots, including deleted ones. Use with <see cref="TryGetEntry"/>.</summary>
        internal int SlotCount => _used;

        internal bool TryGetEntry(int slot, out Value key, out Value value)
        {
            key = KeyAt(slot);
            value = ValueAt(slot);
            return Alive(slot);
        }

        /// <summary>The live entries in insertion order.</summary>
        public IEnumerable<KeyValuePair<Value, Value>> Pairs()
        {
            for (int i = 0; i < _used; i++)
            {
                if (Alive(i)) yield return new KeyValuePair<Value, Value>(KeyAt(i), ValueAt(i));
            }
        }

        private void CompactInts()
        {
            int size = Math.Max(_count, 4);
            var ints = new long[size * _arity];
            var vals = _vals == null ? null : new Value[size];
            int n = 0;
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i)) continue;
                Array.Copy(_ints, i * _arity, ints, n * _arity, _arity);
                if (vals != null) vals[n] = _vals[i];
                n++;
            }
            _ints = ints;
            _vals = vals;
            _dead = null;
            _used = n;
            BuildTable(_used + 1);
            Version++;
        }

        private void Compact()
        {
            EnsureIndex();
            int size = Math.Max(_count, 4);
            var keys = new Value[size];
            var vals = _vals == null ? null : new Value[size];
            var hashes = _hashes == null ? null : new int[size];
            int n = 0;
            for (int i = 0; i < _used; i++)
            {
                if (!Alive(i)) continue;
                keys[n] = _keys[i];
                if (vals != null) vals[n] = _vals[i];
                if (hashes != null) hashes[n] = _hashes[i];
                n++;
            }
            _keys = keys;
            _vals = vals;
            _hashes = hashes;
            _used = n;
            if (_table != null) BuildTable(_used + 1);
            Version++;
        }
    }

    /// <summary>A module: a named bag of members, used for host APIs such as <c>import game</c>.</summary>
    public sealed class PyModule
    {
        public readonly string Name;
        public readonly Dictionary<string, Value> Members = new Dictionary<string, Value>();

        public PyModule(string name) { Name = name; }
    }
}
