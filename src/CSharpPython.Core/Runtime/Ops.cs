using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpPython
{
    /// <summary>The Python semantics of operators on <see cref="Value"/>s. Errors are raised as <see cref="PyError"/>.</summary>
    internal static partial class Ops
    {
        /// <summary>Upper bound for strings and sequences created by repetition or concatenation.</summary>
        public const int MaxSequenceLength = 1 << 22;

        private const int MaxCompareDepth = 100;

        // ---- Errors ---------------------------------------------------------------------------------------------

        public static PyError TypeError(string message) => new PyError("TypeError", message);

        public static PyError ValueError(string message) => new PyError("ValueError", message);

        public static PyError OverflowError() =>
            new PyError("OverflowError", "integer overflow (big integers are not supported yet)");

        private static PyError BadOperands(string op, Value a, Value b) =>
            TypeError("unsupported operand type(s) for " + op + ": '" + TypeName(a) + "' and '" + TypeName(b) + "'");

        // ---- Type information -----------------------------------------------------------------------------------

        public static string TypeName(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.None: return "NoneType";
                case ValueKind.Bool: return "bool";
                case ValueKind.Int: return "int";
                case ValueKind.Float: return "float";
                case ValueKind.Str: return "str";
                case ValueKind.List: return "list";
                case ValueKind.Tuple: return "tuple";
                case ValueKind.Dict: return "dict";
                case ValueKind.Range: return "range";
                case ValueKind.Slice: return "slice";
                case ValueKind.Function: return "function";
                case ValueKind.Native: return "builtin_function_or_method";
                case ValueKind.BoundMethod: return "method";
                case ValueKind.Module: return "module";
                case ValueKind.Iterator: return "iterator";
                case ValueKind.Exception: return ((PyExceptionValue)v.RawObject).Type.Name;
                case ValueKind.ExceptionType: return "type";
                case ValueKind.Handle: return "handle";
                default: return "unbound";
            }
        }

        public static bool IsTruthy(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.None: return false;
                case ValueKind.Bool:
                case ValueKind.Int: return v.RawInt != 0;
                case ValueKind.Float: return v.RawFloat != 0.0;
                case ValueKind.Str: return ((string)v.RawObject).Length != 0;
                case ValueKind.List: return ((PyList)v.RawObject).Count != 0;
                case ValueKind.Tuple: return ((PyTuple)v.RawObject).Count != 0;
                case ValueKind.Dict: return ((PyDict)v.RawObject).Count != 0;
                case ValueKind.Range: return ((PyRange)v.RawObject).Length != 0;
                default: return true;
            }
        }

        // ---- Equality and hashing -------------------------------------------------------------------------------

        public static bool Equal(Value a, Value b) => Equal(a, b, 0);

        private static bool Equal(Value a, Value b, int depth)
        {
            if (a.IsNumber && b.IsNumber)
            {
                if (a.Kind != ValueKind.Float && b.Kind != ValueKind.Float) return a.RawInt == b.RawInt;
                return a.AsFloat() == b.AsFloat();
            }
            if (a.Kind != b.Kind) return false;

            switch (a.Kind)
            {
                case ValueKind.None: return true;
                case ValueKind.Str: return string.Equals((string)a.RawObject, (string)b.RawObject, StringComparison.Ordinal);
                case ValueKind.List:
                    return SequenceEqual(((PyList)a.RawObject).Items, ((PyList)b.RawObject).Items, depth);
                case ValueKind.Tuple:
                    return SequenceEqual(((PyTuple)a.RawObject).Items, ((PyTuple)b.RawObject).Items, depth);
                case ValueKind.Dict:
                    return DictEqual((PyDict)a.RawObject, (PyDict)b.RawObject, depth);
                case ValueKind.Handle: return a.RawAux == b.RawAux && a.RawInt == b.RawInt;
                case ValueKind.Range:
                {
                    var x = (PyRange)a.RawObject;
                    var y = (PyRange)b.RawObject;
                    long len = x.Length;
                    if (len != y.Length) return false;
                    return len == 0 || x.Start == y.Start && (len == 1 || x.Step == y.Step);
                }
                default: return ReferenceEquals(a.RawObject, b.RawObject);
            }
        }

        private static bool SequenceEqual(IReadOnlyList<Value> x, IReadOnlyList<Value> y, int depth)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x.Count != y.Count) return false;
            if (depth > MaxCompareDepth) throw new PyError("RecursionError", "maximum recursion depth exceeded in comparison");
            for (int i = 0; i < x.Count; i++)
                if (!Equal(x[i], y[i], depth + 1)) return false;
            return true;
        }

        private static bool DictEqual(PyDict x, PyDict y, int depth)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x.Count != y.Count) return false;
            if (depth > MaxCompareDepth) throw new PyError("RecursionError", "maximum recursion depth exceeded in comparison");
            foreach (var kv in x.Pairs())
            {
                if (!y.TryGetValue(kv.Key, out Value other) || !Equal(kv.Value, other, depth + 1)) return false;
            }
            return true;
        }

        public static int Hash(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.None: return 0;
                case ValueKind.Bool:
                case ValueKind.Int: return HashInt(v.RawInt);
                case ValueKind.Float:
                {
                    double d = v.RawFloat;
                    if (d == Math.Floor(d) && d >= -9.2e18 && d <= 9.2e18) return HashInt((long)d);
                    return d.GetHashCode();
                }
                case ValueKind.Str: return ((string)v.RawObject).GetHashCode();
                case ValueKind.Tuple:
                {
                    int h = 17;
                    foreach (var item in ((PyTuple)v.RawObject).Items) h = unchecked(h * 31 + Hash(item));
                    return h;
                }
                case ValueKind.Handle: return unchecked(v.RawAux * 31 + HashInt(v.RawInt));
                case ValueKind.List:
                case ValueKind.Dict:
                case ValueKind.Slice:
                    throw TypeError("unhashable type: '" + TypeName(v) + "'");
                case ValueKind.Range:
                {
                    var r = (PyRange)v.RawObject;
                    return unchecked((int)(r.Length * 31 + r.Start * 17 + r.Step));
                }
                default: return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v.RawObject);
            }
        }

        private static int HashInt(long i) => unchecked((int)(i ^ (i >> 32)));

        // ---- Arithmetic -----------------------------------------------------------------------------------------

        public static Value Add(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                long r = unchecked(x + y);
                if (((x ^ r) & (y ^ r)) < 0) throw OverflowError();
                return Value.FromInt(r);
            }
            if (a.IsNumber && b.IsNumber) return Value.FromFloat(a.AsFloat() + b.AsFloat());
            if (a.Kind == b.Kind)
            {
                switch (a.Kind)
                {
                    case ValueKind.Str:
                    {
                        string x = (string)a.RawObject, y = (string)b.RawObject;
                        if ((long)x.Length + y.Length > MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                        return Value.FromString(string.Concat(x, y));
                    }
                    case ValueKind.List:
                    {
                        var x = ((PyList)a.RawObject).Items;
                        var y = ((PyList)b.RawObject).Items;
                        if ((long)x.Count + y.Count > MaxSequenceLength) throw new PyError("MemoryError", "list too long");
                        var result = new List<Value>(x.Count + y.Count);
                        result.AddRange(x);
                        result.AddRange(y);
                        return Value.FromList(new PyList(result));
                    }
                    case ValueKind.Tuple:
                    {
                        var x = ((PyTuple)a.RawObject).Items;
                        var y = ((PyTuple)b.RawObject).Items;
                        if ((long)x.Length + y.Length > MaxSequenceLength) throw new PyError("MemoryError", "tuple too long");
                        var result = new Value[x.Length + y.Length];
                        x.CopyTo(result, 0);
                        y.CopyTo(result, x.Length);
                        return Value.FromTuple(new PyTuple(result));
                    }
                }
            }
            throw BadOperands("+", a, b);
        }

        public static Value Sub(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                long r = unchecked(x - y);
                if (((x ^ y) & (x ^ r)) < 0) throw OverflowError();
                return Value.FromInt(r);
            }
            if (a.IsNumber && b.IsNumber) return Value.FromFloat(a.AsFloat() - b.AsFloat());
            throw BadOperands("-", a, b);
        }

        private static long MulChecked(long x, long y)
        {
            try { return checked(x * y); }
            catch (OverflowException) { throw OverflowError(); }
        }

        public static Value Mul(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike()) return Value.FromInt(MulChecked(a.RawInt, b.RawInt));
            if (a.IsNumber && b.IsNumber) return Value.FromFloat(a.AsFloat() * b.AsFloat());
            if (b.IsIntLike() && IsRepeatable(a)) return Repeat(a, b.RawInt);
            if (a.IsIntLike() && IsRepeatable(b)) return Repeat(b, a.RawInt);
            throw BadOperands("*", a, b);
        }

        private static bool IsRepeatable(Value v) =>
            v.Kind == ValueKind.Str || v.Kind == ValueKind.List || v.Kind == ValueKind.Tuple;

        private static Value Repeat(Value seq, long times)
        {
            if (times < 0) times = 0;
            switch (seq.Kind)
            {
                case ValueKind.Str:
                {
                    string s = (string)seq.RawObject;
                    if (times == 0 || s.Length == 0) return Value.FromString("");
                    if (s.Length * times > MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                    var sb = new StringBuilder(s.Length * (int)times);
                    for (long i = 0; i < times; i++) sb.Append(s);
                    return Value.FromString(sb.ToString());
                }
                case ValueKind.List:
                {
                    var items = ((PyList)seq.RawObject).Items;
                    if (items.Count * times > MaxSequenceLength) throw new PyError("MemoryError", "list too long");
                    var result = new List<Value>(items.Count * (int)times);
                    for (long i = 0; i < times; i++) result.AddRange(items);
                    return Value.FromList(new PyList(result));
                }
                default:
                {
                    var items = ((PyTuple)seq.RawObject).Items;
                    if (items.Length * times > MaxSequenceLength) throw new PyError("MemoryError", "tuple too long");
                    var result = new Value[items.Length * (int)times];
                    for (int i = 0; i < (int)times; i++) items.CopyTo(result, i * items.Length);
                    return Value.FromTuple(new PyTuple(result));
                }
            }
        }

        public static Value Div(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber) throw BadOperands("/", a, b);
            double y = b.AsFloat();
            if (y == 0.0) throw new PyError("ZeroDivisionError", "division by zero");
            return Value.FromFloat(a.AsFloat() / y);
        }

        public static Value FloorDiv(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber) throw BadOperands("//", a, b);
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                if (y == 0) throw new PyError("ZeroDivisionError", "integer division or modulo by zero");
                if (y == -1)
                {
                    if (x == long.MinValue) throw OverflowError();
                    return Value.FromInt(-x);
                }
                long q = x / y;
                if ((x % y != 0) && ((x ^ y) < 0)) q--;
                return Value.FromInt(q);
            }
            double fy = b.AsFloat();
            if (fy == 0.0) throw new PyError("ZeroDivisionError", "float floor division by zero");
            return Value.FromFloat(Math.Floor(a.AsFloat() / fy));
        }

        public static Value Mod(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber) throw BadOperands("%", a, b);
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                if (y == 0) throw new PyError("ZeroDivisionError", "integer modulo by zero");
                if (y == -1) return Value.FromInt(0);
                long r = x % y;
                if (r != 0 && ((r ^ y) < 0)) r += y;
                return Value.FromInt(r);
            }
            double fx = a.AsFloat(), fy = b.AsFloat();
            if (fy == 0.0) throw new PyError("ZeroDivisionError", "float modulo by zero");
            double m = fx % fy; // C# '%' on doubles is fmod
            if (m != 0.0 && (m < 0) != (fy < 0)) m += fy;
            return Value.FromFloat(m);
        }

        public static Value Pow(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber) throw BadOperands("** or pow()", a, b);
            if (a.IsIntLike() && b.IsIntLike() && b.RawInt >= 0)
            {
                long result = 1, bas = a.RawInt, e = b.RawInt;
                while (e > 0)
                {
                    if ((e & 1) != 0) result = MulChecked(result, bas);
                    e >>= 1;
                    if (e > 0) bas = MulChecked(bas, bas);
                }
                return Value.FromInt(result);
            }
            double x = a.AsFloat(), y = b.AsFloat();
            if (x == 0.0 && y < 0) throw new PyError("ZeroDivisionError", "0.0 cannot be raised to a negative power");
            double p = Math.Pow(x, y);
            if (double.IsNaN(p) && !double.IsNaN(x) && !double.IsNaN(y))
                throw ValueError("negative number cannot be raised to a fractional power");
            if (double.IsInfinity(p) && !double.IsInfinity(x) && !double.IsInfinity(y))
                throw new PyError("OverflowError", "(34, 'Numerical result out of range')");
            return Value.FromFloat(p);
        }

        public static Value Negate(Value v)
        {
            if (v.IsIntLike())
            {
                if (v.RawInt == long.MinValue) throw OverflowError();
                return Value.FromInt(-v.RawInt);
            }
            if (v.Kind == ValueKind.Float) return Value.FromFloat(-v.RawFloat);
            throw TypeError("bad operand type for unary -: '" + TypeName(v) + "'");
        }

        public static Value Plus(Value v)
        {
            if (v.IsIntLike()) return Value.FromInt(v.RawInt);
            if (v.Kind == ValueKind.Float) return v;
            throw TypeError("bad operand type for unary +: '" + TypeName(v) + "'");
        }

        // ---- Ordering -------------------------------------------------------------------------------------------

        internal enum Order { Lt, Le, Gt, Ge }

        private static string OrderSymbol(Order o)
        {
            switch (o)
            {
                case Order.Lt: return "<";
                case Order.Le: return "<=";
                case Order.Gt: return ">";
                default: return ">=";
            }
        }

        private static bool Apply(Order o, int cmp)
        {
            switch (o)
            {
                case Order.Lt: return cmp < 0;
                case Order.Le: return cmp <= 0;
                case Order.Gt: return cmp > 0;
                default: return cmp >= 0;
            }
        }

        public static bool Compare(Value a, Value b, Order o) => Compare(a, b, o, 0);

        private static bool Compare(Value a, Value b, Order o, int depth)
        {
            if (a.IsNumber && b.IsNumber)
            {
                if (a.Kind != ValueKind.Float && b.Kind != ValueKind.Float)
                    return Apply(o, a.RawInt.CompareTo(b.RawInt));
                double x = a.AsFloat(), y = b.AsFloat();
                switch (o)
                {
                    case Order.Lt: return x < y;
                    case Order.Le: return x <= y;
                    case Order.Gt: return x > y;
                    default: return x >= y;
                }
            }
            if (a.Kind == ValueKind.Str && b.Kind == ValueKind.Str)
                return Apply(o, string.CompareOrdinal((string)a.RawObject, (string)b.RawObject));
            if (a.Kind == ValueKind.List && b.Kind == ValueKind.List)
                return CompareSequences(((PyList)a.RawObject).Items, ((PyList)b.RawObject).Items, o, depth);
            if (a.Kind == ValueKind.Tuple && b.Kind == ValueKind.Tuple)
                return CompareSequences(((PyTuple)a.RawObject).Items, ((PyTuple)b.RawObject).Items, o, depth);
            throw TypeError("'" + OrderSymbol(o) + "' not supported between instances of '" + TypeName(a) + "' and '" + TypeName(b) + "'");
        }

        private static bool CompareSequences(IReadOnlyList<Value> x, IReadOnlyList<Value> y, Order o, int depth)
        {
            if (depth > MaxCompareDepth) throw new PyError("RecursionError", "maximum recursion depth exceeded in comparison");
            int n = Math.Min(x.Count, y.Count);
            for (int i = 0; i < n; i++)
            {
                if (!Equal(x[i], y[i], depth + 1)) return Compare(x[i], y[i], o, depth + 1);
            }
            return Apply(o, x.Count.CompareTo(y.Count));
        }

        // ---- Membership, length, indexing -----------------------------------------------------------------------

        public static bool Contains(Value container, Value item)
        {
            switch (container.Kind)
            {
                case ValueKind.Str:
                    if (item.Kind != ValueKind.Str)
                        throw TypeError("'in <string>' requires string as left operand, not " + TypeName(item));
                    return ((string)container.RawObject).IndexOf((string)item.RawObject, StringComparison.Ordinal) >= 0;
                case ValueKind.List:
                    foreach (var v in ((PyList)container.RawObject).Items) if (Equal(v, item)) return true;
                    return false;
                case ValueKind.Tuple:
                    foreach (var v in ((PyTuple)container.RawObject).Items) if (Equal(v, item)) return true;
                    return false;
                case ValueKind.Dict:
                    return ((PyDict)container.RawObject).ContainsKey(item);
                case ValueKind.Range:
                {
                    var r = (PyRange)container.RawObject;
                    if (!item.IsNumber) return false;
                    if (item.Kind == ValueKind.Float)
                    {
                        double d = item.RawFloat;
                        if (d != Math.Floor(d) || double.IsInfinity(d)) return false;
                        item = Value.FromInt((long)d);
                    }
                    long n = item.RawInt;
                    long len = r.Length;
                    if (len == 0) return false;
                    long last = r[len - 1];
                    if (r.Step > 0 ? (n < r.Start || n > last) : (n > r.Start || n < last)) return false;
                    return (n - r.Start) % r.Step == 0;
                }
                default:
                    throw TypeError("argument of type '" + TypeName(container) + "' is not iterable");
            }
        }

        public static long Length(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.Str: return ((string)v.RawObject).Length;
                case ValueKind.List: return ((PyList)v.RawObject).Count;
                case ValueKind.Tuple: return ((PyTuple)v.RawObject).Count;
                case ValueKind.Dict: return ((PyDict)v.RawObject).Count;
                case ValueKind.Range: return ((PyRange)v.RawObject).Length;
                default: throw TypeError("object of type '" + TypeName(v) + "' has no len()");
            }
        }

        private static long ToIndex(Value v, string what)
        {
            if (v.IsIntLike()) return v.RawInt;
            throw TypeError(what + " must be integers or slices, not " + TypeName(v));
        }

        private static long NormalizeIndex(long index, long length, string what)
        {
            if (index < 0) index += length;
            if (index < 0 || index >= length) throw new PyError("IndexError", what + " index out of range");
            return index;
        }

        public static Value GetItem(Value target, Value index)
        {
            if (index.Kind == ValueKind.Slice) return GetSlice(target, (PySlice)index.RawObject);

            switch (target.Kind)
            {
                case ValueKind.List:
                {
                    var items = ((PyList)target.RawObject).Items;
                    return items[(int)NormalizeIndex(ToIndex(index, "list indices"), items.Count, "list")];
                }
                case ValueKind.Tuple:
                {
                    var items = ((PyTuple)target.RawObject).Items;
                    return items[(int)NormalizeIndex(ToIndex(index, "tuple indices"), items.Length, "tuple")];
                }
                case ValueKind.Str:
                {
                    string s = (string)target.RawObject;
                    return Value.FromString(s[(int)NormalizeIndex(ToIndex(index, "string indices"), s.Length, "string")].ToString());
                }
                case ValueKind.Range:
                {
                    var r = (PyRange)target.RawObject;
                    return Value.FromInt(r[NormalizeIndex(ToIndex(index, "range indices"), r.Length, "range object")]);
                }
                case ValueKind.Dict:
                {
                    if (((PyDict)target.RawObject).TryGetValue(index, out Value v)) return v;
                    throw new PyError("KeyError", Repr(index));
                }
                default:
                    throw TypeError("'" + TypeName(target) + "' object is not subscriptable");
            }
        }

        public static void SetItem(Value target, Value index, Value value)
        {
            if (index.Kind == ValueKind.Slice) throw TypeError("slice assignment is not supported yet");
            switch (target.Kind)
            {
                case ValueKind.List:
                {
                    var items = ((PyList)target.RawObject).Items;
                    items[(int)NormalizeIndex(ToIndex(index, "list indices"), items.Count, "list assignment")] = value;
                    return;
                }
                case ValueKind.Dict:
                    ((PyDict)target.RawObject).Set(index, value);
                    return;
                default:
                    throw TypeError("'" + TypeName(target) + "' object does not support item assignment");
            }
        }

        // Resolves a slice against a sequence length, following CPython's rules.
        private static void SliceBounds(PySlice slice, long length, out long start, out long stop, out long step, out long count)
        {
            step = slice.Step.IsNone ? 1 : SliceIndex(slice.Step);
            if (step == 0) throw ValueError("slice step cannot be zero");

            long lowerLimit = step < 0 ? -1 : 0;
            long upperLimit = step < 0 ? length - 1 : length;

            start = slice.Start.IsNone ? (step < 0 ? upperLimit : lowerLimit) : Clamp(SliceIndex(slice.Start), length, lowerLimit, upperLimit);
            stop = slice.Stop.IsNone ? (step < 0 ? lowerLimit : upperLimit) : Clamp(SliceIndex(slice.Stop), length, lowerLimit, upperLimit);

            if (step > 0) count = start < stop ? (stop - start - 1) / step + 1 : 0;
            else count = start > stop ? (start - stop - 1) / (-step) + 1 : 0;
        }

        private static long SliceIndex(Value v)
        {
            if (v.IsIntLike()) return v.RawInt;
            throw TypeError("slice indices must be integers or None");
        }

        private static long Clamp(long index, long length, long lower, long upper)
        {
            if (index < 0)
            {
                index += length;
                if (index < lower) index = lower;
            }
            else if (index > upper)
            {
                index = upper;
            }
            return index;
        }

        private static Value GetSlice(Value target, PySlice slice)
        {
            switch (target.Kind)
            {
                case ValueKind.List:
                {
                    var items = ((PyList)target.RawObject).Items;
                    SliceBounds(slice, items.Count, out long start, out _, out long step, out long count);
                    var result = new List<Value>((int)count);
                    for (long i = 0, k = start; i < count; i++, k += step) result.Add(items[(int)k]);
                    return Value.FromList(new PyList(result));
                }
                case ValueKind.Tuple:
                {
                    var items = ((PyTuple)target.RawObject).Items;
                    SliceBounds(slice, items.Length, out long start, out _, out long step, out long count);
                    var result = new Value[count];
                    for (long i = 0, k = start; i < count; i++, k += step) result[i] = items[(int)k];
                    return Value.FromTuple(new PyTuple(result));
                }
                case ValueKind.Str:
                {
                    string s = (string)target.RawObject;
                    SliceBounds(slice, s.Length, out long start, out _, out long step, out long count);
                    if (step == 1) return Value.FromString(s.Substring((int)start, (int)count));
                    var sb = new StringBuilder((int)count);
                    for (long i = 0, k = start; i < count; i++, k += step) sb.Append(s[(int)k]);
                    return Value.FromString(sb.ToString());
                }
                case ValueKind.Range:
                {
                    var r = (PyRange)target.RawObject;
                    SliceBounds(slice, r.Length, out long start, out long stop, out long step, out _);
                    return Value.FromRange(new PyRange(r.Start + start * r.Step, r.Start + stop * r.Step, r.Step * step));
                }
                default:
                    throw TypeError("'" + TypeName(target) + "' object is not subscriptable");
            }
        }

        internal static Value MakeSlice(Value start, Value stop, Value step) =>
            Value.FromSlice(new PySlice(start, stop, step));

        // ---- Iteration ------------------------------------------------------------------------------------------

        public static PyIterator GetIterator(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.List: return new ListIterator((PyList)v.RawObject);
                case ValueKind.Tuple: return new TupleIterator((PyTuple)v.RawObject);
                case ValueKind.Str: return new StringIterator((string)v.RawObject);
                case ValueKind.Dict: return new DictKeyIterator((PyDict)v.RawObject);
                case ValueKind.Range: return new RangeIterator((PyRange)v.RawObject);
                case ValueKind.Iterator: return (PyIterator)v.RawObject;
                default: throw TypeError("'" + TypeName(v) + "' object is not iterable");
            }
        }

        /// <summary>Collects every item of an iterable into a list.</summary>
        public static List<Value> ToList(Value iterable)
        {
            if (iterable.Kind == ValueKind.List) return new List<Value>(((PyList)iterable.RawObject).Items);
            if (iterable.Kind == ValueKind.Tuple) return new List<Value>(((PyTuple)iterable.RawObject).Items);
            var it = GetIterator(iterable);
            var result = new List<Value>();
            while (it.Next(out Value item))
            {
                if (result.Count >= MaxSequenceLength) throw new PyError("MemoryError", "sequence too long");
                result.Add(item);
            }
            return result;
        }
    }
}
