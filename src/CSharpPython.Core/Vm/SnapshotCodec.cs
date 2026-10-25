using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CSharpPython.Compilation;

namespace CSharpPython
{
    // Snapshot object graph encoding.
    //
    // Scalars (None, bool, int, float, str, range, handle) are written inline. Everything with identity is put in an
    // object table so that sharing and cycles survive a round trip:
    //   * containers (list, tuple, dict) come first, as empty shells, so any value can refer to any container;
    //   * leaves (functions, bound methods, iterators, exceptions, slices) follow, each as a length-prefixed record
    //     that the reader builds on demand, so a leaf may refer to another leaf in any order;
    //   * container contents are written last and filled in as tuples, then lists, then dicts, so that tuple keys
    //     are complete before a dict hashes them.
    // Host functions, modules and exception types are written by name and looked up again on restore.

    internal sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }

    internal static class SnapshotIo
    {
        public static void WriteString(BinaryWriter w, string s)
        {
            s = s ?? "";
            w.Write(s.Length);
            foreach (char c in s) w.Write((ushort)c);
        }

        public static string ReadString(BinaryReader r)
        {
            int length = ReadCount(r, 2);
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = (char)r.ReadUInt16();
            return new string(chars);
        }

        // Reads a count and rejects it if the rest of the data could not possibly hold that many items.
        public static int ReadCount(BinaryReader r, int minBytesPerItem)
        {
            int n = r.ReadInt32();
            long remaining = r.BaseStream.Length - r.BaseStream.Position;
            if (n < 0 || (long)n * minBytesPerItem > remaining) throw new SnapshotException("the snapshot is damaged");
            return n;
        }
    }

    internal sealed class SnapshotWriter
    {
        private const byte ContainerList = 0, ContainerTuple = 1, ContainerDict = 2, ContainerCell = 3, ContainerClass = 4, ContainerInstance = 5, ContainerSet = 6, ContainerFrozenSet = 7;

        private readonly PyVm _vm;
        private readonly Script _script;
        private readonly Dictionary<object, int> _containerIds = new Dictionary<object, int>(ReferenceComparer.Instance);
        private readonly Dictionary<object, int> _leafIds = new Dictionary<object, int>(ReferenceComparer.Instance);
        private readonly List<Value> _containers = new List<Value>();
        private readonly List<Value> _leaves = new List<Value>();
        private Dictionary<NativeFunction, string> _natives;

        public SnapshotWriter(PyVm vm, Script script)
        {
            _vm = vm;
            _script = script;
        }

        // ---- Pass 1: find every object that needs an id ---------------------------------------------------------

        public void Collect(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.List:
                    if (AddContainer(v))
                        foreach (var item in ((PyList)v.RawObject).Items) Collect(item);
                    break;
                case ValueKind.Tuple:
                    if (AddContainer(v))
                        foreach (var item in ((PyTuple)v.RawObject).Items) Collect(item);
                    break;
                case ValueKind.Dict:
                    if (AddContainer(v))
                        foreach (var pair in ((PyDict)v.RawObject).Pairs())
                        {
                            Collect(pair.Key);
                            Collect(pair.Value);
                        }
                    break;
                case ValueKind.Set:
                    if (AddContainer(v)) Collect(Value.FromDict(((PySet)v.RawObject).Items));
                    break;
                case ValueKind.Class:
                    if (AddContainer(v))
                    {
                        var cls = (PyClass)v.RawObject;
                        if (cls.Base != null) Collect(Value.FromClass(cls.Base));
                        CollectPairs(cls.Namespace);
                    }
                    break;
                case ValueKind.Instance:
                    if (AddContainer(v))
                    {
                        var inst = (PyInstance)v.RawObject;
                        Collect(Value.FromClass(inst.Class));
                        CollectPairs(inst.Fields);
                    }
                    break;
                case ValueKind.ScriptMethod:
                    if (AddLeaf(v))
                    {
                        var m = (ScriptMethod)v.RawObject;
                        Collect(m.Self);
                        Collect(Value.FromFunction(m.Function));
                    }
                    break;
                case ValueKind.Function:
                    if (AddLeaf(v))
                    {
                        var fn = (PyFunction)v.RawObject;
                        foreach (var d in fn.Defaults) Collect(d);
                        foreach (var cell in fn.Closure) Collect(Value.FromCell(cell));
                        if (fn.Owner != null) Collect(Value.FromClass(fn.Owner));
                    }
                    break;
                case ValueKind.Cell:
                    if (AddContainer(v)) Collect(((Cell)v.RawObject).Value);
                    break;
                case ValueKind.BoundMethod:
                    if (AddLeaf(v)) Collect(((BoundMethod)v.RawObject).Self);
                    break;
                case ValueKind.Slice:
                    if (AddLeaf(v))
                    {
                        var s = (PySlice)v.RawObject;
                        Collect(s.Start);
                        Collect(s.Stop);
                        Collect(s.Step);
                    }
                    break;
                case ValueKind.Exception:
                    AddLeaf(v);
                    break;
                case ValueKind.Iterator:
                    if (AddLeaf(v)) CollectIterator((PyIterator)v.RawObject);
                    break;
            }
        }

        private void CollectPairs(PyDict dict)
        {
            foreach (var pair in dict.Pairs())
            {
                Collect(pair.Key);
                Collect(pair.Value);
            }
        }

        private void WritePairs(BinaryWriter w, PyDict dict)
        {
            w.Write(dict.Count);
            foreach (var pair in dict.Pairs())
            {
                WriteValue(w, pair.Key);
                WriteValue(w, pair.Value);
            }
        }

        private void CollectIterator(PyIterator it)
        {
            switch (it)
            {
                case ListIterator l: Collect(Value.FromList(l.List)); break;
                case TupleIterator t: Collect(Value.FromTuple(t.Tuple)); break;
                case DictKeyIterator d: Collect(Value.FromDict(d.Dict)); break;
                case InstanceIterator i: Collect(i.Target); break;
                case PyGenerator g:
                    if (g.Closure != null) foreach (var cell in g.Closure) Collect(Value.FromCell(cell));
                    if (g.Owner != null) Collect(Value.FromClass(g.Owner));
                    if (g.Saved != null) foreach (var item in g.Saved) Collect(item);
                    Collect(g.ExcInfo);
                    Collect(g.ReturnValue);
                    break;
                case EnumerateIterator e: Collect(Value.FromIterator(e.Source)); break;
                case ZipIterator z:
                    foreach (var s in z.Sources) Collect(Value.FromIterator(s));
                    break;
                case MapIterator m:
                    Collect(m.Function);
                    foreach (var s in m.Sources) Collect(Value.FromIterator(s));
                    break;
                case FilterIterator f:
                    Collect(f.Function);
                    Collect(Value.FromIterator(f.Source));
                    break;
            }
        }

        private bool AddContainer(Value v)
        {
            if (_containerIds.ContainsKey(v.RawObject)) return false;
            _containerIds[v.RawObject] = _containers.Count;
            _containers.Add(v);
            return true;
        }

        private bool AddLeaf(Value v)
        {
            if (_leafIds.ContainsKey(v.RawObject)) return false;
            _leafIds[v.RawObject] = _leaves.Count;
            _leaves.Add(v);
            return true;
        }

        // ---- Pass 2: the object table ---------------------------------------------------------------------------

        public void WriteTable(BinaryWriter w)
        {
            w.Write(_containers.Count);
            foreach (var c in _containers)
            {
                var payload = new MemoryStream();
                var pw = new BinaryWriter(payload);
                switch (c.Kind)
                {
                    case ValueKind.List:
                    {
                        w.Write(ContainerList);
                        var items = ((PyList)c.RawObject).Items;
                        pw.Write(items.Count);
                        foreach (var item in items) WriteValue(pw, item);
                        break;
                    }
                    case ValueKind.Tuple:
                    {
                        w.Write(ContainerTuple);
                        var items = ((PyTuple)c.RawObject).Items;
                        w.Write(items.Length);
                        foreach (var item in items) WriteValue(pw, item);
                        break;
                    }
                    case ValueKind.Set:
                    {
                        w.Write(((PySet)c.RawObject).Frozen ? ContainerFrozenSet : ContainerSet);
                        WriteValue(pw, Value.FromDict(((PySet)c.RawObject).Items));
                        break;
                    }
                    case ValueKind.Cell:
                    {
                        w.Write(ContainerCell);
                        WriteValue(pw, ((Cell)c.RawObject).Value);
                        break;
                    }
                    case ValueKind.Class:
                    {
                        w.Write(ContainerClass);
                        var cls = (PyClass)c.RawObject;
                        SnapshotIo.WriteString(pw, cls.Name);
                        WriteValue(pw, cls.Base == null ? Value.None : Value.FromClass(cls.Base));
                        SnapshotIo.WriteString(pw, cls.RootType?.Name ?? "");
                        WritePairs(pw, cls.Namespace);
                        break;
                    }
                    case ValueKind.Instance:
                    {
                        w.Write(ContainerInstance);
                        var inst = (PyInstance)c.RawObject;
                        WriteValue(pw, Value.FromClass(inst.Class));
                        WritePairs(pw, inst.Fields);
                        break;
                    }
                    default:
                    {
                        w.Write(ContainerDict);
                        var dict = (PyDict)c.RawObject;
                        pw.Write(dict.Count);
                        foreach (var pair in dict.Pairs())
                        {
                            WriteValue(pw, pair.Key);
                            WriteValue(pw, pair.Value);
                        }
                        break;
                    }
                }
                pw.Flush();
                w.Write((int)payload.Length);
                w.Write(payload.GetBuffer(), 0, (int)payload.Length);
            }

            w.Write(_leaves.Count);
            foreach (var leaf in _leaves)
            {
                var payload = new MemoryStream();
                var pw = new BinaryWriter(payload);
                WriteLeaf(pw, leaf);
                pw.Flush();
                w.Write((int)payload.Length);
                w.Write(payload.GetBuffer(), 0, (int)payload.Length);
            }
        }

        private void WriteLeaf(BinaryWriter w, Value v)
        {
            w.Write((byte)v.Kind);
            switch (v.Kind)
            {
                case ValueKind.Function:
                {
                    var fn = (PyFunction)v.RawObject;
                    int index = _script.IndexOf(fn.Code);
                    if (index < 0) throw new SnapshotException("function '" + fn.Name + "' does not belong to this script");
                    w.Write(index);
                    w.Write(fn.Defaults.Length);
                    foreach (var d in fn.Defaults) WriteValue(w, d);
                    w.Write(fn.Closure.Length);
                    foreach (var cell in fn.Closure) WriteValue(w, Value.FromCell(cell));
                    WriteValue(w, fn.Owner == null ? Value.None : Value.FromClass(fn.Owner));
                    break;
                }
                case ValueKind.BoundMethod:
                {
                    var bm = (BoundMethod)v.RawObject;
                    WriteValue(w, bm.Self);
                    string name = bm.Function.Name;
                    SnapshotIo.WriteString(w, name.Substring(name.IndexOf('.') + 1));
                    break;
                }
                case ValueKind.ScriptMethod:
                {
                    var m = (ScriptMethod)v.RawObject;
                    WriteValue(w, m.Self);
                    WriteValue(w, Value.FromFunction(m.Function));
                    break;
                }
                case ValueKind.Slice:
                {
                    var s = (PySlice)v.RawObject;
                    WriteValue(w, s.Start);
                    WriteValue(w, s.Stop);
                    WriteValue(w, s.Step);
                    break;
                }
                case ValueKind.Exception:
                {
                    var e = (PyExceptionValue)v.RawObject;
                    SnapshotIo.WriteString(w, e.Type.Name);
                    SnapshotIo.WriteString(w, e.Message);
                    w.Write(e.Trace.Count);
                    foreach (var f in e.Trace)
                    {
                        SnapshotIo.WriteString(w, f.Function);
                        w.Write(f.Line);
                    }
                    break;
                }
                default:
                    WriteIterator(w, (PyIterator)v.RawObject);
                    break;
            }
        }

        private void WriteIterator(BinaryWriter w, PyIterator it)
        {
            switch (it)
            {
                case RangeIterator r:
                    w.Write((byte)0);
                    w.Write(r.Range.Start);
                    w.Write(r.Range.Stop);
                    w.Write(r.Range.Step);
                    w.Write(r.Index);
                    break;
                case ListIterator l:
                    w.Write((byte)1);
                    WriteValue(w, Value.FromList(l.List));
                    w.Write(l.Index);
                    break;
                case TupleIterator t:
                    w.Write((byte)2);
                    WriteValue(w, Value.FromTuple(t.Tuple));
                    w.Write(t.Index);
                    break;
                case StringIterator s:
                    w.Write((byte)3);
                    SnapshotIo.WriteString(w, s.Text);
                    w.Write(s.Index);
                    break;
                case DictKeyIterator d:
                    w.Write((byte)4);
                    WriteValue(w, Value.FromDict(d.Dict));
                    w.Write(d.Produced);
                    w.Write(d.IsStale);
                    break;
                case EnumerateIterator e:
                    w.Write((byte)5);
                    WriteValue(w, Value.FromIterator(e.Source));
                    w.Write(e.Index);
                    break;
                case ZipIterator z:
                    w.Write((byte)6);
                    w.Write(z.Sources.Length);
                    foreach (var src in z.Sources) WriteValue(w, Value.FromIterator(src));
                    break;
                case MapIterator m:
                    w.Write((byte)9);
                    WriteValue(w, m.Function);
                    w.Write(m.Sources.Length);
                    foreach (var src in m.Sources) WriteValue(w, Value.FromIterator(src));
                    break;
                case FilterIterator f:
                    w.Write((byte)10);
                    WriteValue(w, f.Function);
                    WriteValue(w, Value.FromIterator(f.Source));
                    break;
                case InstanceIterator i:
                    w.Write((byte)7);
                    WriteValue(w, i.Target);
                    break;
                case PyGenerator g:
                {
                    int codeIndex = _script.IndexOf(g.Code);
                    if (codeIndex < 0) throw new SnapshotException("generator '" + g.Name + "' does not belong to this script");
                    w.Write((byte)8);
                    w.Write(codeIndex);
                    w.Write((byte)g.State);
                    w.Write(g.Pc);
                    w.Write(g.Closure?.Length ?? 0);
                    if (g.Closure != null) foreach (var cell in g.Closure) WriteValue(w, Value.FromCell(cell));
                    WriteValue(w, g.Owner == null ? Value.None : Value.FromClass(g.Owner));
                    WriteValue(w, g.ExcInfo);
                    WriteValue(w, g.ReturnValue);
                    w.Write(g.Saved != null);
                    if (g.Saved != null) w.Write(g.Saved.Length);
                    if (g.Saved != null) foreach (var item in g.Saved) WriteValue(w, item);
                    w.Write(g.HandlerAddresses.Length);
                    for (int h = 0; h < g.HandlerAddresses.Length; h++)
                    {
                        w.Write(g.HandlerAddresses[h]);
                        w.Write(g.HandlerHeights[h]);
                    }
                    break;
                }
                default:
                    throw new SnapshotException("cannot save an iterator of type " + it.GetType().Name);
            }
        }

        // ---- Values ---------------------------------------------------------------------------------------------

        public void WriteValue(BinaryWriter w, Value v)
        {
            w.Write((byte)v.Kind);
            switch (v.Kind)
            {
                case ValueKind.None:
                case ValueKind.NotImplemented:
                case ValueKind.Unbound:
                    break;
                case ValueKind.Bool:
                    w.Write(v.RawInt != 0);
                    break;
                case ValueKind.Int:
                case ValueKind.Float:
                    w.Write(v.RawInt);
                    break;
                case ValueKind.Str:
                    SnapshotIo.WriteString(w, (string)v.RawObject);
                    break;
                case ValueKind.Range:
                {
                    var r = (PyRange)v.RawObject;
                    w.Write(r.Start);
                    w.Write(r.Stop);
                    w.Write(r.Step);
                    break;
                }
                case ValueKind.Handle:
                    w.Write(v.RawAux);
                    w.Write(v.RawInt);
                    break;
                case ValueKind.Native:
                    SnapshotIo.WriteString(w, NativePath((NativeFunction)v.RawObject));
                    break;
                case ValueKind.Module:
                    SnapshotIo.WriteString(w, ((PyModule)v.RawObject).Name);
                    break;
                case ValueKind.ExceptionType:
                    SnapshotIo.WriteString(w, ((PyExceptionType)v.RawObject).Name);
                    break;
                case ValueKind.Type:
                    SnapshotIo.WriteString(w, ((PyType)v.RawObject).Name);
                    break;
                case ValueKind.List:
                case ValueKind.Tuple:
                case ValueKind.Dict:
                case ValueKind.Set:
                case ValueKind.Cell:
                case ValueKind.Class:
                case ValueKind.Instance:
                    w.Write(Id(_containerIds, v));
                    break;
                default:
                    w.Write(_containers.Count + Id(_leafIds, v));
                    break;
            }
        }

        private static int Id(Dictionary<object, int> ids, Value v)
        {
            if (!ids.TryGetValue(v.RawObject, out int id))
                throw new SnapshotException("internal error: an object was not collected before saving");
            return id;
        }

        private string NativePath(NativeFunction fn)
        {
            if (_natives == null)
            {
                _natives = new Dictionary<NativeFunction, string>();
                foreach (var kv in _vm.Builtins)
                    if (kv.Value.Kind == ValueKind.Native && !_natives.ContainsKey((NativeFunction)kv.Value.RawObject))
                        _natives[(NativeFunction)kv.Value.RawObject] = "b:" + kv.Key;
                foreach (var module in _vm.Modules)
                    foreach (var member in module.Value.Members)
                        if (member.Value.Kind == ValueKind.Native && !_natives.ContainsKey((NativeFunction)member.Value.RawObject))
                            _natives[(NativeFunction)member.Value.RawObject] = "m:" + module.Key + ":" + member.Key;
                foreach (var kind in new[] { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple, ValueKind.Set, ValueKind.Iterator })
                    foreach (var name in Methods.NamesOf(kind))
                    {
                        var method = Methods.Find(kind, name);
                        if (!_natives.ContainsKey(method)) _natives[method] = "t:" + (int)kind + ":" + name;
                    }
            }
            if (_natives.TryGetValue(fn, out string path)) return path;
            throw new SnapshotException("cannot save the host function '" + fn.Name +
                "' because it is not registered as a built-in or in a module; register it so it can be found again");
        }
    }

    internal sealed class SnapshotReader
    {
        private readonly PyVm _vm;
        private readonly Script _script;
        private readonly byte[] _data;

        private object[] _objects;           // containers first, then leaves
        private int _containerCount;
        private int[] _blockOffset, _blockLength;
        private bool[] _building;
        private readonly List<DictKeyIterator> _dictIterators = new List<DictKeyIterator>();
        private bool _filled; // true once every container has its contents

        public SnapshotReader(PyVm vm, Script script, byte[] data)
        {
            _vm = vm;
            _script = script;
            _data = data;
        }

        public void ReadTable(BinaryReader r)
        {
            int containers = SnapshotIo.ReadCount(r, 5);
            var kinds = new byte[containers];
            var tupleLengths = new int[containers];
            var offsets = new int[containers];
            var lengths = new int[containers];
            var shells = new object[containers];
            for (int i = 0; i < containers; i++)
            {
                kinds[i] = r.ReadByte();
                switch (kinds[i])
                {
                    case 0: shells[i] = new PyList(); break;
                    case 1:
                        tupleLengths[i] = SnapshotIo.ReadCount(r, 1);
                        shells[i] = new PyTuple(new Value[tupleLengths[i]]);
                        break;
                    case 2: shells[i] = new PyDict(); break;
                    case 3: shells[i] = new Cell(Value.Unbound); break;
                    case 4: shells[i] = new PyClass(null, null, new PyDict()); break;
                    case 5: shells[i] = new PyInstance(null); break;
                    case 6: shells[i] = new PySet(); break;
                    case 7: shells[i] = new PySet { Frozen = true }; break;
                    default: throw new SnapshotException("the snapshot is damaged");
                }
                lengths[i] = SnapshotIo.ReadCount(r, 1);
                offsets[i] = (int)r.BaseStream.Position;
                r.BaseStream.Seek(lengths[i], SeekOrigin.Current);
            }

            int leaves = SnapshotIo.ReadCount(r, 4);
            _objects = new object[containers + leaves];
            Array.Copy(shells, _objects, containers);
            _containerCount = containers;
            _blockOffset = new int[containers + leaves];
            _blockLength = new int[containers + leaves];
            _building = new bool[containers + leaves];
            for (int i = 0; i < containers; i++)
            {
                _blockOffset[i] = offsets[i];
                _blockLength[i] = lengths[i];
            }
            for (int i = 0; i < leaves; i++)
            {
                int length = SnapshotIo.ReadCount(r, 1);
                _blockOffset[containers + i] = (int)r.BaseStream.Position;
                _blockLength[containers + i] = length;
                r.BaseStream.Seek(length, SeekOrigin.Current);
            }

            // Fill containers: tuples, then lists, then dicts.
            for (byte pass = 1; pass <= 7; pass++)
            {
                int kind = pass == 1 ? 1 : pass == 2 ? 3 : pass == 3 ? 0 : pass == 4 ? 2 : pass == 5 ? 4 : pass == 6 ? 5 : 6;
                for (int i = 0; i < containers; i++)
                    if (kinds[i] == kind || (kind == 6 && kinds[i] == 7)) FillContainer(i);
            }
            for (int i = 0; i < containers; i++)
            {
                // A damaged snapshot must not give a class itself as an ancestor, which would loop forever.
                if (kinds[i] != 4) continue;
                int steps = 0;
                for (var c = ((PyClass)_objects[i]).Base; c != null; c = c.Base)
                    if (++steps > containers) throw new SnapshotException("the snapshot is damaged");
            }

            foreach (var it in _dictIterators) it.FinishRestore();
            _filled = true;
        }

        private BinaryReader Block(int id) =>
            new BinaryReader(new MemoryStream(_data, _blockOffset[id], _blockLength[id], false));

        private void FillContainer(int id)
        {
            var r = Block(id);
            switch (_objects[id])
            {
                case PyList list:
                {
                    int n = SnapshotIo.ReadCount(r, 1);
                    for (int i = 0; i < n; i++) list.Items.Add(ReadValue(r));
                    break;
                }
                case PyTuple tuple:
                    for (int i = 0; i < tuple.Items.Length; i++) tuple.Items[i] = ReadValue(r);
                    break;
                case Cell cell:
                    cell.Value = ReadValue(r);
                    break;
                case PyClass cls:
                {
                    cls.Name = SnapshotIo.ReadString(r);
                    Value b = ReadValue(r);
                    if (b.Kind == ValueKind.Class) cls.Base = (PyClass)b.RawObject;
                    else if (b.Kind != ValueKind.None) throw new SnapshotException("the snapshot is damaged");
                    string root = SnapshotIo.ReadString(r);
                    if (root.Length > 0) cls.RootType = ExceptionTypes.GetOrCreate(root);
                    ReadPairs(r, cls.Namespace);
                    break;
                }
                case PyInstance inst:
                {
                    Value c = ReadValue(r);
                    if (c.Kind != ValueKind.Class) throw new SnapshotException("the snapshot is damaged");
                    inst.Class = (PyClass)c.RawObject;
                    ReadPairs(r, inst.Fields);
                    break;
                }
                case PySet set:
                {
                    Value d = ReadValue(r);
                    if (d.Kind != ValueKind.Dict) throw new SnapshotException("the snapshot is damaged");
                    set.Items = (PyDict)d.RawObject;
                    set.Items.ForSet = true;
                    break;
                }
                case PyDict dict:
                {
                    int n = SnapshotIo.ReadCount(r, 2);
                    for (int i = 0; i < n; i++)
                    {
                        Value key = ReadValue(r);
                        dict.RestoreEntry(key, ReadValue(r));
                    }
                    break;
                }
            }
        }

        private void ReadPairs(BinaryReader r, PyDict into)
        {
            int n = SnapshotIo.ReadCount(r, 2);
            for (int i = 0; i < n; i++)
            {
                Value key = ReadValue(r);
                into.Set(key, ReadValue(r));
            }
        }

        private object Resolve(int id)
        {
            if (id < 0 || id >= _objects.Length) throw new SnapshotException("the snapshot is damaged");
            if (id >= _containerCount && _objects[id] == null) BuildLeaf(id);
            return _objects[id];
        }

        private void BuildLeaf(int id)
        {
            if (_building[id]) throw new SnapshotException("the snapshot is damaged (circular reference)");
            _building[id] = true;
            var r = Block(id);
            var kind = (ValueKind)r.ReadByte();
            switch (kind)
            {
                case ValueKind.Function:
                {
                    int codeIndex = r.ReadInt32();
                    if (codeIndex <= 0 || codeIndex >= _script.Codes.Length)
                        throw new SnapshotException("the snapshot does not match this script");
                    int n = SnapshotIo.ReadCount(r, 1);
                    var defaults = new Value[n];
                    for (int i = 0; i < n; i++) defaults[i] = ReadValue(r);
                    var code = _script.Codes[codeIndex];
                    var function = new PyFunction(code, defaults, ReadClosure(r, code));
                    Value owner = ReadValue(r);
                    if (owner.Kind == ValueKind.Class) function.Owner = (PyClass)owner.RawObject;
                    else if (owner.Kind != ValueKind.None) throw new SnapshotException("the snapshot is damaged");
                    _objects[id] = function;
                    break;
                }
                case ValueKind.BoundMethod:
                {
                    Value self = ReadValue(r);
                    string name = SnapshotIo.ReadString(r);
                    var method = Methods.Find(self.Kind, name);
                    if (method == null) throw new SnapshotException("unknown method '" + name + "'");
                    _objects[id] = new BoundMethod(self, method);
                    break;
                }
                case ValueKind.ScriptMethod:
                {
                    Value self = ReadValue(r);
                    Value fn = ReadValue(r);
                    if (fn.Kind != ValueKind.Function) throw new SnapshotException("the snapshot is damaged");
                    _objects[id] = new ScriptMethod(self, (PyFunction)fn.RawObject);
                    break;
                }
                case ValueKind.Slice:
                {
                    Value a = ReadValue(r), b = ReadValue(r), c = ReadValue(r);
                    _objects[id] = new PySlice(a, b, c);
                    break;
                }
                case ValueKind.Exception:
                {
                    string type = SnapshotIo.ReadString(r);
                    string message = SnapshotIo.ReadString(r);
                    var exc = new PyExceptionValue(ExceptionTypes.GetOrCreate(type), message);
                    int n = SnapshotIo.ReadCount(r, 8);
                    for (int i = 0; i < n; i++)
                    {
                        string fn = SnapshotIo.ReadString(r);
                        exc.Trace.Add(new TraceFrame(fn, r.ReadInt32()));
                    }
                    _objects[id] = exc;
                    break;
                }
                case ValueKind.Iterator:
                    _objects[id] = ReadIterator(r);
                    break;
                default:
                    throw new SnapshotException("the snapshot is damaged");
            }
            _building[id] = false;
        }

        private PyIterator ReadIterator(BinaryReader r)
        {
            switch (r.ReadByte())
            {
                case 0:
                {
                    long start = r.ReadInt64(), stop = r.ReadInt64(), step = r.ReadInt64();
                    if (step == 0) throw new SnapshotException("the snapshot is damaged");
                    return new RangeIterator(new PyRange(start, stop, step)) { Index = r.ReadInt64() };
                }
                case 1:
                {
                    var list = ReadValue(r).AsList();
                    return new ListIterator(list) { Index = r.ReadInt32() };
                }
                case 2:
                {
                    var tuple = ReadValue(r).AsTuple();
                    return new TupleIterator(tuple) { Index = r.ReadInt32() };
                }
                case 3:
                {
                    string text = SnapshotIo.ReadString(r);
                    return new StringIterator(text) { Index = r.ReadInt32() };
                }
                case 4:
                {
                    var dict = ReadValue(r).AsDict();
                    int produced = r.ReadInt32();
                    var it = new DictKeyIterator(dict, produced, r.ReadBoolean());
                    // Leaves reached while containers are still being filled are finished afterwards.
                    if (_filled) it.FinishRestore();
                    else _dictIterators.Add(it);
                    return it;
                }
                case 5:
                {
                    var source = AsIterator(ReadValue(r));
                    return new EnumerateIterator(source, r.ReadInt64());
                }
                case 6:
                {
                    int n = SnapshotIo.ReadCount(r, 5);
                    var sources = new PyIterator[n];
                    for (int i = 0; i < n; i++) sources[i] = AsIterator(ReadValue(r));
                    return new ZipIterator(sources);
                }
                case 7:
                {
                    Value target = ReadValue(r);
                    if (target.Kind != ValueKind.Instance) throw new SnapshotException("the snapshot is damaged");
                    return new InstanceIterator(target);
                }
                case 8: return ReadGenerator(r);
                case 9:
                {
                    Value function = ReadValue(r);
                    int n = SnapshotIo.ReadCount(r, 5);
                    var sources = new PyIterator[n];
                    for (int i = 0; i < n; i++) sources[i] = AsIterator(ReadValue(r));
                    return new MapIterator(function, sources);
                }
                case 10:
                {
                    Value function = ReadValue(r);
                    return new FilterIterator(function, AsIterator(ReadValue(r)));
                }
                default:
                    throw new SnapshotException("the snapshot is damaged");
            }
        }

        private PyGenerator ReadGenerator(BinaryReader r)
        {
            int codeIndex = r.ReadInt32();
            if (codeIndex <= 0 || codeIndex >= _script.Codes.Length || !_script.Codes[codeIndex].IsGenerator)
                throw new SnapshotException("the snapshot does not match this script");
            var code = _script.Codes[codeIndex];
            byte state = r.ReadByte();
            if (state > (byte)GeneratorState.Done) throw new SnapshotException("the snapshot is damaged");
            int pc = r.ReadInt32();
            if (pc < 0 || pc > code.Instructions.Length) throw new SnapshotException("the snapshot is damaged");
            var closure = ReadClosure(r, code);
            Value owner = ReadValue(r);
            if (owner.Kind != ValueKind.Class && owner.Kind != ValueKind.None) throw new SnapshotException("the snapshot is damaged");
            var generator = new PyGenerator(code, closure, owner.Kind == ValueKind.Class ? (PyClass)owner.RawObject : null, null)
            {
                State = (GeneratorState)state,
                Pc = pc,
                ExcInfo = ReadValue(r),
                ReturnValue = ReadValue(r),
            };
            bool live = r.ReadBoolean();
            if (live != (generator.State == GeneratorState.Suspended || generator.State == GeneratorState.NotStarted))
                throw new SnapshotException("the snapshot is damaged");
            int saved = 0;
            if (live)
            {
                saved = SnapshotIo.ReadCount(r, 1);
                if (saved < code.NumLocals) throw new SnapshotException("the snapshot is damaged");
                generator.Saved = new Value[saved];
                for (int i = 0; i < saved; i++) generator.Saved[i] = ReadValue(r);
            }
            int handlers = SnapshotIo.ReadCount(r, 8);
            generator.HandlerAddresses = new int[handlers];
            generator.HandlerHeights = new int[handlers];
            for (int h = 0; h < handlers; h++)
            {
                generator.HandlerAddresses[h] = r.ReadInt32();
                generator.HandlerHeights[h] = r.ReadInt32();
                if (generator.HandlerAddresses[h] < 0 || generator.HandlerAddresses[h] > code.Instructions.Length || generator.HandlerHeights[h] < 0 || (live && generator.HandlerHeights[h] > saved))
                    throw new SnapshotException("the snapshot is damaged");
            }
            return generator;
        }

        private static PyIterator AsIterator(Value v)
        {
            if (v.Kind != ValueKind.Iterator) throw new SnapshotException("the snapshot is damaged");
            return (PyIterator)v.RawObject;
        }

        public Value ReadValue(BinaryReader r)
        {
            var kind = (ValueKind)r.ReadByte();
            switch (kind)
            {
                case ValueKind.None: return Value.None;
                case ValueKind.NotImplemented: return Value.NotImplemented;
                case ValueKind.Unbound: return Value.Unbound;
                case ValueKind.Bool: return Value.FromBool(r.ReadBoolean());
                case ValueKind.Int: return Value.FromInt(r.ReadInt64());
                case ValueKind.Float: return Value.FromFloat(BitConverter.Int64BitsToDouble(r.ReadInt64()));
                case ValueKind.Str: return Value.FromString(SnapshotIo.ReadString(r));
                case ValueKind.Range:
                {
                    long start = r.ReadInt64(), stop = r.ReadInt64(), step = r.ReadInt64();
                    if (step == 0) throw new SnapshotException("the snapshot is damaged");
                    return Value.FromRange(new PyRange(start, stop, step));
                }
                case ValueKind.Handle:
                {
                    int type = r.ReadInt32();
                    return Value.FromHandle(type, r.ReadInt64());
                }
                case ValueKind.Native:
                    return Value.FromNative(LookupNative(SnapshotIo.ReadString(r)));
                case ValueKind.Module:
                {
                    string name = SnapshotIo.ReadString(r);
                    if (!_vm.Modules.TryGetValue(name, out var module))
                        throw new SnapshotException("module '" + name + "' is not registered");
                    return Value.FromModule(module);
                }
                case ValueKind.ExceptionType:
                    return Value.FromExceptionType(ExceptionTypes.GetOrCreate(SnapshotIo.ReadString(r)));
                case ValueKind.Type:
                {
                    string name = SnapshotIo.ReadString(r);
                    if (!PyTypes.TryGet(name, out var type)) throw new SnapshotException("the snapshot is damaged");
                    return Value.FromType(type);
                }
                case ValueKind.List: return Value.FromList((PyList)ContainerAs(r.ReadInt32(), typeof(PyList)));
                case ValueKind.Tuple: return Value.FromTuple((PyTuple)ContainerAs(r.ReadInt32(), typeof(PyTuple)));
                case ValueKind.Dict: return Value.FromDict((PyDict)ContainerAs(r.ReadInt32(), typeof(PyDict)));
                case ValueKind.Set: return Value.FromSet((PySet)ContainerAs(r.ReadInt32(), typeof(PySet)));
                case ValueKind.Cell: return Value.FromCell((Cell)ContainerAs(r.ReadInt32(), typeof(Cell)));
                case ValueKind.Class: return Value.FromClass((PyClass)ContainerAs(r.ReadInt32(), typeof(PyClass)));
                case ValueKind.Instance: return Value.FromInstance((PyInstance)ContainerAs(r.ReadInt32(), typeof(PyInstance)));
                case ValueKind.ScriptMethod: return Value.FromScriptMethod((ScriptMethod)LeafAs(r.ReadInt32(), typeof(ScriptMethod)));
                case ValueKind.Function: return Value.FromFunction((PyFunction)LeafAs(r.ReadInt32(), typeof(PyFunction)));
                case ValueKind.BoundMethod: return Value.FromBound((BoundMethod)LeafAs(r.ReadInt32(), typeof(BoundMethod)));
                case ValueKind.Slice: return Value.FromSlice((PySlice)LeafAs(r.ReadInt32(), typeof(PySlice)));
                case ValueKind.Exception: return Value.FromException((PyExceptionValue)LeafAs(r.ReadInt32(), typeof(PyExceptionValue)));
                case ValueKind.Iterator: return Value.FromIterator((PyIterator)LeafAs(r.ReadInt32(), typeof(PyIterator)));
                default:
                    throw new SnapshotException("the snapshot is damaged");
            }
        }

        // The cells of a function or running frame; there must be exactly as many as the code has free variables.
        public Cell[] ReadClosure(BinaryReader r, Compilation.Code code)
        {
            int n = SnapshotIo.ReadCount(r, 5);
            if (n != code.FreeNames.Length) throw new SnapshotException("the snapshot does not match this script");
            var cells = new Cell[n];
            for (int i = 0; i < n; i++)
            {
                var v = ReadValue(r);
                if (v.Kind != ValueKind.Cell) throw new SnapshotException("the snapshot is damaged");
                cells[i] = (Cell)v.RawObject;
            }
            return cells;
        }

        private object ContainerAs(int id, Type type)
        {
            if (id < 0 || id >= _containerCount || !type.IsInstanceOfType(_objects[id]))
                throw new SnapshotException("the snapshot is damaged");
            return _objects[id];
        }

        private object LeafAs(int id, Type type)
        {
            if (id < _containerCount) throw new SnapshotException("the snapshot is damaged");
            object o = Resolve(id);
            if (!type.IsInstanceOfType(o)) throw new SnapshotException("the snapshot is damaged");
            return o;
        }

        private NativeFunction LookupNative(string path)
        {
            var parts = path.Split(':');
            if (parts.Length >= 2)
            {
                switch (parts[0])
                {
                    case "b":
                        if (_vm.Builtins.TryGetValue(parts[1], out var b) && b.Kind == ValueKind.Native)
                            return (NativeFunction)b.RawObject;
                        break;
                    case "m":
                        if (parts.Length == 3 && _vm.Modules.TryGetValue(parts[1], out var module) &&
                            module.Members.TryGetValue(parts[2], out var m) && m.Kind == ValueKind.Native)
                            return (NativeFunction)m.RawObject;
                        break;
                    case "t":
                        if (parts.Length == 3 && int.TryParse(parts[1], out int kind))
                        {
                            var method = Methods.Find((ValueKind)kind, parts[2]);
                            if (method != null) return method;
                        }
                        break;
                }
            }
            throw new SnapshotException("the host function '" + path + "' is not registered");
        }
    }
}
