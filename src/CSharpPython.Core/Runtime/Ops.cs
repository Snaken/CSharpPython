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

        private static PyError BadOperands(string op, Value a, Value b) =>
            TypeError("unsupported operand type(s) for " + op + ": '" + TypeName(a) + "' and '" + TypeName(b) + "'");

        // ---- Type information -----------------------------------------------------------------------------------

        public static string TypeName(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.None: return "NoneType";
                case ValueKind.NotImplemented: return "NotImplementedType";
                case ValueKind.Ellipsis: return "ellipsis";
                case ValueKind.Bool: return "bool";
                case ValueKind.Int:
                case ValueKind.BigInt: return "int";
                case ValueKind.Float: return "float";
                case ValueKind.Str: return "str";
                case ValueKind.List: return "list";
                case ValueKind.Tuple: return "tuple";
                case ValueKind.Dict: return DictTypeName((PyDict)v.RawObject);
                case ValueKind.Set: return ((PySet)v.RawObject).Frozen ? "frozenset" : "set";
                case ValueKind.Range: return "range";
                case ValueKind.DictView: return ((PyDictView)v.RawObject).TypeName;
                case ValueKind.Slice: return "slice";
                case ValueKind.Function: return "function";
                case ValueKind.Native: return "builtin_function_or_method";
                case ValueKind.BoundMethod: return "method";
                case ValueKind.Module: return "module";
                case ValueKind.Iterator: return v.RawObject is PyGenerator ? "generator" : "iterator";
                case ValueKind.Exception: return ((PyExceptionValue)v.RawObject).Type.Name;
                case ValueKind.Instance: return ((PyInstance)v.RawObject).Class.Name;
                case ValueKind.ScriptMethod: return "method";
                case ValueKind.Descriptor: return ((PyDescriptor)v.RawObject).TypeName;
                case ValueKind.ExceptionType:
                case ValueKind.Class:
                case ValueKind.Type: return "type";
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
                case ValueKind.Set: return ((PySet)v.RawObject).Count != 0;
                case ValueKind.Range: return ((PyRange)v.RawObject).Length != 0;
                case ValueKind.DictView: return ((PyDictView)v.RawObject).Dict.Count != 0;
                case ValueKind.Instance: return InstanceTruthy(v);
                default: return true;
            }
        }

        // ---- Equality and hashing -------------------------------------------------------------------------------

        private static string DictTypeName(PyDict d) => d.Flavor == DictFlavor.Counter ? "Counter" : d.Flavor == DictFlavor.Default ? "defaultdict" : "dict";

        public static bool Equal(Value a, Value b) => Equal(a, b, 0);

        private static bool Equal(Value a, Value b, int depth)
        {
            if (a.IsNumber && b.IsNumber)
            {
                if (a.Kind != ValueKind.Float && b.Kind != ValueKind.Float && a.Kind != ValueKind.BigInt && b.Kind != ValueKind.BigInt) return a.RawInt == b.RawInt;
                return CompareNumbers(a, b, out bool unordered) == 0 && !unordered;
            }
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceEqual(a, b);
            if (a.Kind != b.Kind)
                return IsSetLike(a) && IsSetLike(b) && SetEqual(AsSetOperand(a), AsSetOperand(b));

            switch (a.Kind)
            {
                case ValueKind.None: return true;
                case ValueKind.NotImplemented: return true;
                case ValueKind.Ellipsis: return true;
                case ValueKind.Str: return string.Equals((string)a.RawObject, (string)b.RawObject, StringComparison.Ordinal);
                case ValueKind.List:
                    return SequenceEqual(((PyList)a.RawObject).Items, ((PyList)b.RawObject).Items, depth);
                case ValueKind.Tuple:
                    return SequenceEqual(((PyTuple)a.RawObject).Items, ((PyTuple)b.RawObject).Items, depth);
                case ValueKind.Dict:
                    if (IsCounter(a) && IsCounter(b)) return CounterEqual((PyDict)a.RawObject, (PyDict)b.RawObject, depth);
                    return DictEqual((PyDict)a.RawObject, (PyDict)b.RawObject, depth);
                case ValueKind.Set: return SetEqual((PySet)a.RawObject, (PySet)b.RawObject);
                case ValueKind.DictView:
                    return IsSetLike(a) && IsSetLike(b) && ((PyDictView)a.RawObject).Mode == ((PyDictView)b.RawObject).Mode
                        ? SetEqual(AsSetOperand(a), AsSetOperand(b))
                        : ReferenceEquals(a.RawObject, b.RawObject);
                case ValueKind.Handle: return a.RawAux == b.RawAux && a.RawInt == b.RawInt;
                case ValueKind.Slice:
                {
                    var x = (PySlice)a.RawObject;
                    var y = (PySlice)b.RawObject;
                    return Equal(x.Start, y.Start, depth + 1) && Equal(x.Stop, y.Stop, depth + 1) && Equal(x.Step, y.Step, depth + 1);
                }
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
                case ValueKind.BigInt: return ((System.Numerics.BigInteger)v.RawObject).GetHashCode();
                case ValueKind.Float:
                {
                    double d = v.RawFloat;
                    if (IsIntegral(d))
                    {
                        // An integral float hashes like the integer of the same value, whatever its size.
                        if (d >= -9.2233720368547758e18 && d < 9.2233720368547758e18) return HashInt((long)d);
                        return new System.Numerics.BigInteger(d).GetHashCode();
                    }
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
                case ValueKind.Instance: return InstanceHash(v);
                case ValueKind.Set:
                {
                    var set = (PySet)v.RawObject;
                    if (!set.Frozen) throw TypeError("unhashable type: 'set'");
                    int h = 0x5bd1e995;
                    foreach (var item in set.Members()) h = unchecked(h + Hash(item) * 0x2545F491);
                    return h;
                }
                case ValueKind.Slice:
                {
                    var slice = (PySlice)v.RawObject;
                    return unchecked((Hash(slice.Start) * 31 + Hash(slice.Stop)) * 31 + Hash(slice.Step));
                }
                case ValueKind.List:
                case ValueKind.Dict:
                case ValueKind.DictView:
                    throw TypeError("unhashable type: '" + TypeName(v) + "'");
                case ValueKind.Range:
                {
                    var r = (PyRange)v.RawObject;
                    return unchecked((int)(r.Length * 31 + r.Start * 17 + r.Step));
                }
                default: return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v.RawObject);
            }
        }

        // True for a finite float with no fractional part (the difference is exactly 0, or at least double.Epsilon).
        internal static bool IsIntegral(double d) => Math.Abs(d - Math.Floor(d)) < double.Epsilon;

        private static int HashInt(long i) => unchecked((int)(i ^ (i >> 32)));

        // ---- Arithmetic -----------------------------------------------------------------------------------------

        public static Value Add(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                long r = unchecked(x + y);
                if (((x ^ r) & (y ^ r)) < 0) return BigResult(new System.Numerics.BigInteger(x) + y);
                return Value.FromInt(r);
            }
            if (a.IsInteger && b.IsInteger) return BigResult(ToBig(a) + ToBig(b));
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
                    case ValueKind.Dict:
                        if (IsCounter(a) && IsCounter(b)) return CounterArithmetic(a, b, '+');
                        break;
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
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__add__", "__radd__", "+");
            throw BadOperands("+", a, b);
        }

        public static Value Sub(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                long r = unchecked(x - y);
                if (((x ^ y) & (x ^ r)) < 0) return BigResult(new System.Numerics.BigInteger(x) - y);
                return Value.FromInt(r);
            }
            if (a.IsInteger && b.IsInteger) return BigResult(ToBig(a) - ToBig(b));
            if (a.IsNumber && b.IsNumber) return Value.FromFloat(a.AsFloat() - b.AsFloat());
            if (TryViewSetOperation(a, b, 3, out Value viewResult)) return viewResult;
            if (IsCounter(a) && IsCounter(b)) return CounterArithmetic(a, b, '-');
            if (a.Kind == ValueKind.Set && b.Kind == ValueKind.Set) return Value.FromSet(SetDifference((PySet)a.RawObject, (PySet)b.RawObject).LikeOf((PySet)a.RawObject));
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__sub__", "__rsub__", "-");
            throw BadOperands("-", a, b);
        }

        public static Value Mul(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                return TryMul(a.RawInt, b.RawInt, out long product) ? Value.FromInt(product) : BigMul(a, b);
            }
            if (a.IsInteger && b.IsInteger) return BigMul(a, b);
            if (a.IsNumber && b.IsNumber) return Value.FromFloat(a.AsFloat() * b.AsFloat());
            if (b.IsInteger && IsRepeatable(a)) return Repeat(a, RepeatCount(b));
            if (a.IsInteger && IsRepeatable(b)) return Repeat(b, RepeatCount(a));
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__mul__", "__rmul__", "*");
            throw BadOperands("*", a, b);
        }

        // A repeat count: 0 for a negative count, and an OverflowError for one that cannot be an index.
        private static long RepeatCount(Value n)
        {
            if (n.Kind == ValueKind.BigInt) throw IndexTooLarge();
            return n.RawInt;
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
            if (!a.IsNumber || !b.IsNumber)
            {
                if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__truediv__", "__rtruediv__", "/");
                throw BadOperands("/", a, b);
            }
            if (a.IsInteger && b.IsInteger && (a.Kind == ValueKind.BigInt || b.Kind == ValueKind.BigInt || BitLength(a) > 53 || BitLength(b) > 53))
                return BigTrueDivide(a, b);
            double y = b.AsFloat();
            if (y == 0.0) throw new PyError("ZeroDivisionError", "division by zero");
            return Value.FromFloat(a.AsFloat() / y);
        }

        public static Value FloorDiv(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber)
            {
                if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__floordiv__", "__rfloordiv__", "//");
                throw BadOperands("//", a, b);
            }
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                if (y == 0) throw new PyError("ZeroDivisionError", "integer division or modulo by zero");
                if (y == -1)
                {
                    if (x == long.MinValue) return BigResult(-new System.Numerics.BigInteger(x));
                    return Value.FromInt(-x);
                }
                long q = x / y;
                if ((x % y != 0) && ((x ^ y) < 0)) q--;
                return Value.FromInt(q);
            }
            if (a.IsInteger && b.IsInteger) return BigFloorDiv(a, b);
            double fy = b.AsFloat();
            if (fy == 0.0) throw new PyError("ZeroDivisionError", "float floor division by zero");
            return Value.FromFloat(Math.Floor(a.AsFloat() / fy));
        }

        public static Value Mod(Value a, Value b)
        {
            if (a.Kind == ValueKind.Str) return Value.FromString(PercentFormat((string)a.RawObject, b));
            if (!a.IsNumber || !b.IsNumber)
            {
                if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__mod__", "__rmod__", "%");
                throw BadOperands("%", a, b);
            }
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                if (y == 0) throw new PyError("ZeroDivisionError", "integer modulo by zero");
                if (y == -1) return Value.FromInt(0);
                long r = x % y;
                if (r != 0 && ((r ^ y) < 0)) r += y;
                return Value.FromInt(r);
            }
            if (a.IsInteger && b.IsInteger) return BigMod(a, b);
            double fx = a.AsFloat(), fy = b.AsFloat();
            if (fy == 0.0) throw new PyError("ZeroDivisionError", "float modulo by zero");
            double m = fx % fy; // C# '%' on doubles is fmod
            if (m == 0.0) m = fy < 0 ? -0.0 : 0.0; // a zero result takes the sign of the divisor
            else if ((m < 0) != (fy < 0)) m += fy;
            return Value.FromFloat(m);
        }

        public static Value Pow(Value a, Value b)
        {
            if (!a.IsNumber || !b.IsNumber)
            {
                if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__pow__", "__rpow__", "** or pow()");
                throw BadOperands("** or pow()", a, b);
            }
            if (a.IsInteger && b.IsInteger && (b.Kind == ValueKind.BigInt ? ((System.Numerics.BigInteger)b.RawObject).Sign >= 0 : b.RawInt >= 0))
            {
                if (a.IsIntLike() && b.IsIntLike())
                {
                    long result = 1, bas = a.RawInt, e = b.RawInt;
                    bool overflow = false;
                    while (e > 0 && !overflow)
                    {
                        if ((e & 1) != 0) overflow = !TryMul(result, bas, out result);
                        e >>= 1;
                        if (e > 0 && !overflow) overflow = !TryMul(bas, bas, out bas);
                    }
                    if (!overflow) return Value.FromInt(result);
                }
                return BigPow(a, b);
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

        private static bool TryMul(long x, long y, out long product)
        {
            try
            {
                product = checked(x * y);
                return true;
            }
            catch (OverflowException)
            {
                product = 0;
                return false;
            }
        }

        public static Value Negate(Value v)
        {
            if (v.IsIntLike())
            {
                if (v.RawInt == long.MinValue) return BigResult(-new System.Numerics.BigInteger(v.RawInt));
                return Value.FromInt(-v.RawInt);
            }
            if (v.Kind == ValueKind.BigInt) return BigResult(-(System.Numerics.BigInteger)v.RawObject);
            if (v.Kind == ValueKind.Float) return Value.FromFloat(-v.RawFloat);
            if (v.Kind == ValueKind.Instance && TryCallDunder(v, "__neg__", Array.Empty<Value>(), out Value negated)) return negated;
            throw TypeError("bad operand type for unary -: '" + TypeName(v) + "'");
        }

        public static Value Plus(Value v)
        {
            if (v.IsIntLike()) return Value.FromInt(v.RawInt);
            if (v.Kind == ValueKind.Float || v.Kind == ValueKind.BigInt) return v;
            if (v.Kind == ValueKind.Instance && TryCallDunder(v, "__pos__", Array.Empty<Value>(), out Value positive)) return positive;
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
                int cmp = CompareNumbers(a, b, out bool unordered);
                return !unordered && Apply(o, cmp);
            }
            if (a.Kind == ValueKind.Str && b.Kind == ValueKind.Str)
                return Apply(o, string.CompareOrdinal((string)a.RawObject, (string)b.RawObject));
            if (a.Kind == ValueKind.List && b.Kind == ValueKind.List)
                return CompareSequences(((PyList)a.RawObject).Items, ((PyList)b.RawObject).Items, o, depth);
            if (a.Kind == ValueKind.Tuple && b.Kind == ValueKind.Tuple)
                return CompareSequences(((PyTuple)a.RawObject).Items, ((PyTuple)b.RawObject).Items, o, depth);
            if (IsSetLike(a) && IsSetLike(b) && (a.Kind == ValueKind.DictView || b.Kind == ValueKind.DictView))
                return CompareSets(AsSetOperand(a), AsSetOperand(b), o);
            if (a.Kind == ValueKind.Set && b.Kind == ValueKind.Set) return CompareSets((PySet)a.RawObject, (PySet)b.RawObject, o);
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return CompareOp(a, b, o).IsTruthy;
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

        // ---- Sets -----------------------------------------------------------------------------------------------

        private static bool IsSetLike(Value v) =>
            v.Kind == ValueKind.Set || (v.Kind == ValueKind.DictView && ((PyDictView)v.RawObject).IsSetLike);

        private static PySet AsSetOperand(Value v)
        {
            if (v.Kind == ValueKind.Set) return (PySet)v.RawObject;
            var set = new PySet();
            foreach (var item in ((PyDictView)v.RawObject).Members()) set.Add(item);
            return set;
        }

        // keys() and items() views take part in set operators with any iterable on the other side; the result is a set.
        private static bool TryViewSetOperation(Value a, Value b, int kind, out Value result)
        {
            result = default;
            bool aView = a.Kind == ValueKind.DictView && ((PyDictView)a.RawObject).IsSetLike;
            bool bView = b.Kind == ValueKind.DictView && ((PyDictView)b.RawObject).IsSetLike;
            if (!aView && !bView) return false;
            Value other = aView ? b : a;
            if (other.Kind == ValueKind.Instance || (other.Kind == ValueKind.DictView && !((PyDictView)other.RawObject).IsSetLike)) return false;
            if (other.IsNumber || other.Kind == ValueKind.None) throw TypeError("'" + TypeName(other) + "' object is not iterable");
            var x = aView ? AsSetOperand(a) : Methods.ToSet(a);
            var y = bView ? AsSetOperand(b) : Methods.ToSet(b);
            switch (kind)
            {
                case 0:
                    var union = x.Copy();
                    foreach (var v in y.Members()) union.Add(v);
                    result = Value.FromSet(union);
                    break;
                case 1: result = Value.FromSet(Methods.SetSymmetricDifference(x, y)); break;
                case 2: result = Value.FromSet(Methods.SetIntersection(x, y)); break;
                default: result = Value.FromSet(SetDifference(x, y)); break;
            }
            return true;
        }

        private static bool SetEqual(PySet x, PySet y) => ReferenceEquals(x, y) || x.Count == y.Count && IsSubset(x, y);

        internal static bool IsSubset(PySet x, PySet y)
        {
            if (x.Count > y.Count) return false;
            foreach (var v in x.Members()) if (!y.Contains(v)) return false;
            return true;
        }

        private static bool CompareSets(PySet x, PySet y, Order o)
        {
            switch (o)
            {
                case Order.Le: return IsSubset(x, y);
                case Order.Lt: return x.Count < y.Count && IsSubset(x, y);
                case Order.Ge: return IsSubset(y, x);
                default: return y.Count < x.Count && IsSubset(y, x);
            }
        }

        internal static PySet SetDifference(PySet x, PySet y)
        {
            var result = new PySet();
            foreach (var v in x.Members()) if (!y.Contains(v)) result.Add(v);
            return result;
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
                case ValueKind.Set:
                    return ((PySet)container.RawObject).Contains(item);
                case ValueKind.DictView:
                {
                    var view = (PyDictView)container.RawObject;
                    if (view.Mode == DictViewKind.Keys) return view.Dict.ContainsKey(item);
                    if (view.Mode == DictViewKind.Items)
                        return item.Kind == ValueKind.Tuple && ((PyTuple)item.RawObject).Count == 2
                            && view.Dict.TryGetValue(((PyTuple)item.RawObject).Items[0], out Value found) && Equal(found, ((PyTuple)item.RawObject).Items[1]);
                    foreach (var kv in view.Dict.Pairs()) if (Equal(kv.Value, item)) return true;
                    return false;
                }
                case ValueKind.Range:
                {
                    var r = (PyRange)container.RawObject;
                    if (!item.IsNumber || item.Kind == ValueKind.BigInt) return false;
                    if (item.Kind == ValueKind.Float)
                    {
                        double d = item.RawFloat;
                        if (!IsIntegral(d) || d >= 9.2233720368547758e18 || d < -9.2233720368547758e18) return false;
                        item = Value.FromInt((long)d);
                    }
                    long n = item.RawInt;
                    long len = r.Length;
                    if (len == 0) return false;
                    long last = r[len - 1];
                    if (r.Step > 0 ? (n < r.Start || n > last) : (n > r.Start || n < last)) return false;
                    return (n - r.Start) % r.Step == 0;
                }
                case ValueKind.Instance: return InstanceContains(container, item);
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
                case ValueKind.Set: return ((PySet)v.RawObject).Count;
                case ValueKind.Range: return ((PyRange)v.RawObject).Length;
                case ValueKind.DictView: return ((PyDictView)v.RawObject).Dict.Count;
                case ValueKind.Instance: return InstanceLength(v);
                default: throw TypeError("object of type '" + TypeName(v) + "' has no len()");
            }
        }

        /// <summary>
        /// The integer an object stands for as an index: itself for an int or bool, the result of <c>__index__</c>
        /// for an instance that defines it, else the value unchanged.
        /// </summary>
        public static Value IndexValue(Value v)
        {
            if (v.Kind != ValueKind.Instance || !TryCallDunder(v, "__index__", Array.Empty<Value>(), out Value result)) return v;
            if (!result.IsInteger)
                throw TypeError("__index__ returned non-int (type " + TypeName(result) + ")");
            return result;
        }

        private static long ToIndex(Value v, string what)
        {
            v = IndexValue(v);
            if (v.IsIntLike()) return v.RawInt;
            if (v.Kind == ValueKind.BigInt) throw new PyError("IndexError", "cannot fit 'int' into an index-sized integer");
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
            if (target.Kind == ValueKind.Instance) return InstanceGetItem(target, index);
            if (index.Kind == ValueKind.Slice && target.Kind != ValueKind.Dict) return GetSlice(target, (PySlice)index.RawObject);

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
                    var dict = (PyDict)target.RawObject;
                    if (dict.TryGetValue(index, out Value v)) return v;
                    return MissingKey(dict, index);
                }
                default:
                    throw TypeError("'" + TypeName(target) + "' object is not subscriptable");
            }
        }

        public static void SetItem(Value target, Value index, Value value)
        {
            if (target.Kind == ValueKind.Instance)
            {
                InstanceSetItem(target, index, value);
                return;
            }
            if (index.Kind == ValueKind.Slice && target.Kind == ValueKind.List)
            {
                AssignSlice(((PyList)target.RawObject).Items, (PySlice)index.RawObject, value);
                return;
            }
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

        // xs[a:b] = iterable replaces the items (the count may change); xs[a:b:c] = iterable needs the same count.
        private static void AssignSlice(List<Value> items, PySlice slice, Value value)
        {
            if (value.Kind <= ValueKind.Float) throw TypeError("can only assign an iterable");
            var source = new List<Value>(ToList(value));
            SliceBounds(slice, items.Count, out long start, out _, out long step, out long count);
            if (step == 1)
            {
                items.RemoveRange((int)start, (int)count);
                items.InsertRange((int)start, source);
                return;
            }
            if (source.Count != count)
                throw ValueError("attempt to assign sequence of size " + source.Count + " to extended slice of size " + count);
            for (long i = 0, k = start; i < count; i++, k += step) items[(int)k] = source[(int)i];
        }

        public static void DelItem(Value target, Value index)
        {
            switch (target.Kind)
            {
                case ValueKind.Instance:
                    if (!TryCallDunder(target, "__delitem__", new[] { index }, out _))
                        throw TypeError("'" + TypeName(target) + "' object doesn't support item deletion");
                    return;
                case ValueKind.List:
                {
                    var items = ((PyList)target.RawObject).Items;
                    if (index.Kind == ValueKind.Slice)
                    {
                        SliceBounds((PySlice)index.RawObject, items.Count, out long start, out _, out long step, out long count);
                        if (step == 1)
                        {
                            items.RemoveRange((int)start, (int)count);
                            return;
                        }
                        var doomed = new List<int>();
                        for (long i = 0, k = start; i < count; i++, k += step) doomed.Add((int)k);
                        doomed.Sort();
                        for (int j = doomed.Count - 1; j >= 0; j--) items.RemoveAt(doomed[j]);
                        return;
                    }
                    items.RemoveAt((int)NormalizeIndex(ToIndex(index, "list indices"), items.Count, "list assignment"));
                    return;
                }
                case ValueKind.Dict:
                    if (!((PyDict)target.RawObject).Remove(index) && !IsCounter(target)) throw new PyError("KeyError", Repr(index));
                    return;
                case ValueKind.Tuple:
                case ValueKind.Str:
                case ValueKind.Set:
                case ValueKind.Range:
                    throw TypeError("'" + TypeName(target) + "' object doesn't support item deletion");
                default:
                    throw TypeError("'" + TypeName(target) + "' object does not support item deletion");
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
            v = IndexValue(v);
            if (v.IsIntLike()) return v.RawInt;
            if (v.Kind == ValueKind.BigInt) return ((System.Numerics.BigInteger)v.RawObject).Sign < 0 ? long.MinValue / 2 : long.MaxValue / 2;
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
                case ValueKind.Set: return new DictKeyIterator(((PySet)v.RawObject).Items);
                case ValueKind.Range: return new RangeIterator((PyRange)v.RawObject);
                case ValueKind.DictView:
                {
                    var view = (PyDictView)v.RawObject;
                    return view.Mode == DictViewKind.Keys ? new DictKeyIterator(view.Dict) : (PyIterator)new ListIterator(new PyList(view.Members()));
                }
                case ValueKind.Iterator: return (PyIterator)v.RawObject;
                case ValueKind.Instance: return InstanceIterate(v);
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
