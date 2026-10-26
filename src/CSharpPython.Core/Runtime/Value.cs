using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CSharpPython
{
    /// <summary>
    /// A Python value. This is a small tagged union (None / bool / int / float inline, everything else a
    /// reference) so that arithmetic does not allocate. <c>default(Value)</c> is <c>None</c>.
    /// </summary>
    public readonly struct Value : IEquatable<Value>
    {
        public readonly ValueKind Kind;
        private readonly int _aux;
        private readonly long _bits;
        private readonly object _obj;

        private Value(ValueKind kind, long bits, object obj, int aux = 0)
        {
            Kind = kind;
            _bits = bits;
            _obj = obj;
            _aux = aux;
        }

        // ---- Construction ---------------------------------------------------------------------------------------

        public static readonly Value None = default;
        public static readonly Value True = new Value(ValueKind.Bool, 1, null);
        public static readonly Value False = new Value(ValueKind.Bool, 0, null);
        internal static readonly Value NotImplemented = new Value(ValueKind.NotImplemented, 0, null);
        internal static readonly Value Ellipsis = new Value(ValueKind.Ellipsis, 0, null);
        internal static readonly Value Unbound = new Value(ValueKind.Unbound, 0, null);

        public static Value FromBool(bool b) => b ? True : False;

        public static Value FromInt(long i) => new Value(ValueKind.Int, i, null);

        public static Value FromFloat(double d) => new Value(ValueKind.Float, BitConverter.DoubleToInt64Bits(d), null);

        public static Value FromString(string s) =>
            s == null ? None : new Value(ValueKind.Str, 0, s);

        public static Value FromList(PyList list) => new Value(ValueKind.List, 0, list);

        internal static Value FromDeque(PyDeque deque) => new Value(ValueKind.Deque, 0, deque);

        public static Value FromTuple(PyTuple tuple) => new Value(ValueKind.Tuple, 0, tuple);

        public static Value FromDict(PyDict dict) => new Value(ValueKind.Dict, 0, dict);

        public static Value FromSet(PySet set) => new Value(ValueKind.Set, 0, set);

        internal static Value FromBig(System.Numerics.BigInteger b) =>
            b >= long.MinValue && b <= long.MaxValue ? FromInt((long)b) : new Value(ValueKind.BigInt, 0, b);

        public static Value FromDictView(PyDictView view) => new Value(ValueKind.DictView, 0, view);

        public static Value FromRange(PyRange range) => new Value(ValueKind.Range, 0, range);

        internal static Value FromSlice(PySlice slice) => new Value(ValueKind.Slice, 0, slice);

        internal static Value FromFunction(PyFunction fn) => new Value(ValueKind.Function, 0, fn);
        internal static Value FromCell(Cell cell) => new Value(ValueKind.Cell, 0, cell);

        public static Value FromNative(NativeFunction fn) => new Value(ValueKind.Native, 0, fn);

        internal static Value FromBound(BoundMethod m) => new Value(ValueKind.BoundMethod, 0, m);

        public static Value FromModule(PyModule m) => new Value(ValueKind.Module, 0, m);

        internal static Value FromIterator(PyIterator it) => new Value(ValueKind.Iterator, 0, it);

        internal static Value FromException(PyExceptionValue e) => new Value(ValueKind.Exception, 0, e);

        internal static Value FromType(PyType t) => new Value(ValueKind.Type, 0, t);
        internal static Value FromClass(PyClass c) => new Value(ValueKind.Class, 0, c);
        internal static Value FromInstance(PyInstance i) => new Value(ValueKind.Instance, 0, i);
        internal static Value FromScriptMethod(ScriptMethod m) => new Value(ValueKind.ScriptMethod, 0, m);
        internal static Value FromDescriptor(PyDescriptor d) => new Value(ValueKind.Descriptor, 0, d);
        internal static Value FromExceptionType(PyExceptionType t) => new Value(ValueKind.ExceptionType, 0, t);

        /// <summary>Creates a handle that refers to a host object. Handles are plain data, so they can be saved.</summary>
        public static Value FromHandle(int typeId, long id) => new Value(ValueKind.Handle, id, null, typeId);

        public static Value Tuple(params Value[] items) => FromTuple(new PyTuple(items));

        public static Value List(params Value[] items) => FromList(new PyList(items));

        public static implicit operator Value(bool b) => FromBool(b);

        public static implicit operator Value(int i) => FromInt(i);

        public static implicit operator Value(long i) => FromInt(i);

        public static implicit operator Value(double d) => FromFloat(d);

        public static implicit operator Value(string s) => FromString(s);

        // ---- Inspection -----------------------------------------------------------------------------------------

        public bool IsNone => Kind == ValueKind.None;
        public bool IsBool => Kind == ValueKind.Bool;
        public bool IsInt => Kind == ValueKind.Int;
        public bool IsFloat => Kind == ValueKind.Float;
        public bool IsString => Kind == ValueKind.Str;
        public bool IsList => Kind == ValueKind.List;
        public bool IsTuple => Kind == ValueKind.Tuple;
        public bool IsDict => Kind == ValueKind.Dict;
        public bool IsSet => Kind == ValueKind.Set;
        public bool IsHandle => Kind == ValueKind.Handle;

        /// <summary>True for bool, int (of any size) and float.</summary>
        public bool IsNumber => Kind == ValueKind.Bool || Kind == ValueKind.Int || Kind == ValueKind.Float || Kind == ValueKind.BigInt;

        /// <summary>True for bool and int (of any size).</summary>
        internal bool IsInteger => Kind == ValueKind.Bool || Kind == ValueKind.Int || Kind == ValueKind.BigInt;

        /// <summary>The Python type name, as reported by <c>type(x).__name__</c>.</summary>
        public string TypeName => Ops.TypeName(this);

        /// <summary>Python truthiness.</summary>
        public bool IsTruthy => Ops.IsTruthy(this);

        internal long RawInt => _bits;
        internal double RawFloat => BitConverter.Int64BitsToDouble(_bits);
        internal object RawObject => _obj;
        internal int RawAux => _aux;

        /// <summary>The integer value of an int or bool. Raises a Python TypeError otherwise.</summary>
        public long AsInt()
        {
            if (Kind == ValueKind.Int || Kind == ValueKind.Bool) return _bits;
            if (Kind == ValueKind.BigInt) throw new PyError("OverflowError", "Python int too large to convert to C ssize_t");
            throw new PyError("TypeError", "expected an int, got " + TypeName);
        }

        /// <summary>The value of an int or bool of any size. Raises a Python TypeError otherwise.</summary>
        public System.Numerics.BigInteger AsBigInteger()
        {
            if (!IsInteger) throw new PyError("TypeError", "expected an int, got " + TypeName);
            return Ops.ToBig(this);
        }

        /// <summary>An int of any size (it becomes an ordinary int when it fits in 64 bits).</summary>
        public static Value FromBigInteger(System.Numerics.BigInteger value) => FromBig(value);

        /// <summary>The numeric value as a double. Accepts int, float and bool.</summary>
        public double AsFloat()
        {
            if (Kind == ValueKind.Float) return RawFloat;
            if (Kind == ValueKind.Int || Kind == ValueKind.Bool) return _bits;
            if (Kind == ValueKind.BigInt) return Ops.BigToDouble((System.Numerics.BigInteger)_obj);
            throw new PyError("TypeError", "expected a number, got " + TypeName);
        }

        public bool AsBool() => IsTruthy;

        /// <summary>The string value. Raises a Python TypeError if this is not a str.</summary>
        public string AsString()
        {
            if (Kind == ValueKind.Str) return (string)_obj;
            throw new PyError("TypeError", "expected a str, got " + TypeName);
        }

        public PyList AsList()
        {
            if (Kind == ValueKind.List) return (PyList)_obj;
            throw new PyError("TypeError", "expected a list, got " + TypeName);
        }

        public PyTuple AsTuple()
        {
            if (Kind == ValueKind.Tuple) return (PyTuple)_obj;
            throw new PyError("TypeError", "expected a tuple, got " + TypeName);
        }

        public PyDict AsDict()
        {
            if (Kind == ValueKind.Dict) return (PyDict)_obj;
            throw new PyError("TypeError", "expected a dict, got " + TypeName);
        }

        public PySet AsSet()
        {
            if (Kind == ValueKind.Set) return (PySet)_obj;
            throw new PyError("TypeError", "expected a set, got " + TypeName);
        }

        /// <summary>The host type id of a handle. Raises a Python TypeError for other kinds.</summary>
        public int HandleType
        {
            get
            {
                if (Kind == ValueKind.Handle) return _aux;
                throw new PyError("TypeError", "expected a handle, got " + TypeName);
            }
        }

        /// <summary>The host object id of a handle. Raises a Python TypeError for other kinds.</summary>
        public long HandleId
        {
            get
            {
                if (Kind == ValueKind.Handle) return _bits;
                throw new PyError("TypeError", "expected a handle, got " + TypeName);
            }
        }

        // ---- Equality (Python semantics: 1 == 1.0 == True) ------------------------------------------------------

        public bool Equals(Value other) => Ops.Equal(this, other);

        public override bool Equals(object obj) => obj is Value v && Equals(v);

        public override int GetHashCode() => Ops.Hash(this);

        public static bool operator ==(Value a, Value b) => Ops.Equal(a, b);

        public static bool operator !=(Value a, Value b) => !Ops.Equal(a, b);

        /// <summary>The Python <c>str()</c> of this value.</summary>
        public override string ToString() => Ops.Str(this);

        /// <summary>The Python <c>repr()</c> of this value.</summary>
        public string Repr() => Ops.Repr(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsIntLike() => Kind == ValueKind.Int || Kind == ValueKind.Bool;
    }
}
