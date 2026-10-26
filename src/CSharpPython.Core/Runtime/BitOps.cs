using System.Linq;

namespace CSharpPython
{
    internal static partial class Ops
    {
        // &, |, ^, << and >> on ints (bool & bool stays a bool) and on sets, and ~ on ints.

        private static Value Bitwise(Value a, Value b, int kind, string method, string reflected, string symbol)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, y = b.RawInt;
                long r = kind == 0 ? x | y : kind == 1 ? x ^ y : x & y;
                return a.Kind == ValueKind.Bool && b.Kind == ValueKind.Bool ? Value.FromBool(r != 0) : Value.FromInt(r);
            }
            if (a.Kind == ValueKind.Set && b.Kind == ValueKind.Set)
            {
                var x = (PySet)a.RawObject;
                var y = (PySet)b.RawObject;
                if (kind == 2) return Value.FromSet(Methods.SetIntersection(x, y).LikeOf(x));
                if (kind == 1) return Value.FromSet(Methods.SetSymmetricDifference(x, y).LikeOf(x));
                var union = x.Copy().LikeOf(x);
                foreach (var v in y.Members()) union.Add(v);
                return Value.FromSet(union);
            }
            if (kind == 0 && a.Kind == ValueKind.Dict && b.Kind == ValueKind.Dict)
            {
                var merged = new PyDict();
                foreach (var kv in ((PyDict)a.RawObject).Pairs()) merged.Set(kv.Key, kv.Value);
                foreach (var kv in ((PyDict)b.RawObject).Pairs()) merged.Set(kv.Key, kv.Value);
                return Value.FromDict(merged);
            }
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, method, reflected, symbol);
            throw BadOperands(symbol, a, b);
        }

        public static Value BitOr(Value a, Value b) => Bitwise(a, b, 0, "__or__", "__ror__", "|");
        public static Value BitXor(Value a, Value b) => Bitwise(a, b, 1, "__xor__", "__rxor__", "^");
        public static Value BitAnd(Value a, Value b) => Bitwise(a, b, 2, "__and__", "__rand__", "&");

        public static Value LShift(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, n = b.RawInt;
                if (n < 0) throw ValueError("negative shift count");
                if (x == 0) return Value.FromInt(0);
                if (n >= 64) throw OverflowError();
                long r = x << (int)n;
                if (r >> (int)n != x) throw OverflowError();
                return Value.FromInt(r);
            }
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__lshift__", "__rlshift__", "<<");
            throw BadOperands("<<", a, b);
        }

        public static Value RShift(Value a, Value b)
        {
            if (a.IsIntLike() && b.IsIntLike())
            {
                long x = a.RawInt, n = b.RawInt;
                if (n < 0) throw ValueError("negative shift count");
                if (n >= 64) return Value.FromInt(x < 0 ? -1 : 0);
                return Value.FromInt(x >> (int)n);
            }
            if (a.Kind == ValueKind.Instance || b.Kind == ValueKind.Instance) return InstanceBinary(a, b, "__rshift__", "__rrshift__", ">>");
            throw BadOperands(">>", a, b);
        }

        public static Value Invert(Value v)
        {
            if (v.IsIntLike()) return Value.FromInt(~v.RawInt);
            if (v.Kind == ValueKind.Instance && TryCallDunder(v, "__invert__", System.Array.Empty<Value>(), out Value inverted)) return inverted;
            throw TypeError("bad operand type for unary ~: '" + TypeName(v) + "'");
        }

        // a |= b, a &= b and a ^= b change a set in place; everything else is the plain operator after __ior__ and friends.
        private static bool SetInPlace(Value a, Value b, int kind)
        {
            if (a.Kind != ValueKind.Set || b.Kind != ValueKind.Set) return false;
            var x = (PySet)a.RawObject;
            var y = (PySet)b.RawObject;
            if (x.Frozen) return false;
            if (kind == 0) foreach (var v in y.Members().ToList()) x.Add(v);
            else Methods.ReplaceMembers(x, kind == 1 ? Methods.SetSymmetricDifference(x, y) : Methods.SetIntersection(x, y));
            return true;
        }

        private static Value InPlaceBit(Value a, Value b, int kind, string method, string symbol)
        {
            if (SetInPlace(a, b, kind)) return a;
            if (kind == 0 && a.Kind == ValueKind.Dict)
            {
                Methods.UpdateDict((PyDict)a.RawObject, b);
                return a;
            }
            if (TryInPlace(a, method, b, out Value r)) return r;
            try
            {
                return kind == 0 ? BitOr(a, b) : kind == 1 ? BitXor(a, b) : BitAnd(a, b);
            }
            catch (PyError e) when (IsOperandError(e, symbol)) { throw AsInPlace(e, symbol); }
        }

        public static Value BitOrInPlace(Value a, Value b) => InPlaceBit(a, b, 0, "__ior__", "|");
        public static Value BitXorInPlace(Value a, Value b) => InPlaceBit(a, b, 1, "__ixor__", "^");
        public static Value BitAndInPlace(Value a, Value b) => InPlaceBit(a, b, 2, "__iand__", "&");

        public static Value LShiftInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__ilshift__", b, out Value r)) return r;
            try { return LShift(a, b); }
            catch (PyError e) when (IsOperandError(e, "<<")) { throw AsInPlace(e, "<<"); }
        }

        public static Value RShiftInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__irshift__", b, out Value r)) return r;
            try { return RShift(a, b); }
            catch (PyError e) when (IsOperandError(e, ">>")) { throw AsInPlace(e, ">>"); }
        }
    }
}
