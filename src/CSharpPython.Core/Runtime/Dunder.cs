using System;
using System.Linq;

namespace CSharpPython
{
    // Operators on instances of script classes: the special methods (__eq__, __lt__, __len__, ...) the class defines.
    // They are run with CallSync on the task that is executing, so they cannot wait. Without a running task (the host
    // using a value outside a script) an instance behaves as an object without special methods.
    internal static partial class Ops
    {
        /// <summary>Calls the special method <paramref name="name"/> of the instance's class. False if it has none.</summary>
        public static bool TryCallDunder(Value instance, string name, Value[] args, out Value result)
        {
            result = default;
            var task = PyTask.Current;
            if (task == null) return false;
            if (!((PyInstance)instance.RawObject).Class.TryFind(name, out Value member) || member.Kind != ValueKind.Function)
                return false;
            result = task.CallSync(Value.FromScriptMethod(new ScriptMethod(instance, (PyFunction)member.RawObject)), args);
            return true;
        }

        private static bool HasDunder(Value instance, string name) =>
            PyTask.Current != null &&
            ((PyInstance)instance.RawObject).Class.TryFind(name, out Value member) && member.Kind == ValueKind.Function;

        // Tries a.method(b) and then b.reflected(a), the way Python's binary operators do, except that a subclass's
        // method comes first when b's class derives from a's. A method that returns NotImplemented declines.
        private static bool TryBinary(Value a, Value b, string method, string reflected, bool sameClassReflects, out Value result)
        {
            bool bFirst = false, bReflects = b.Kind == ValueKind.Instance;
            if (a.Kind == ValueKind.Instance && bReflects)
            {
                var ca = ((PyInstance)a.RawObject).Class;
                var cb = ((PyInstance)b.RawObject).Class;
                bFirst = ca != cb && cb.IsSubclassOf(ca);
                bReflects = ca != cb || sameClassReflects;
            }
            if (bFirst && TryOne(b, reflected, a, out result)) return true;
            if (a.Kind == ValueKind.Instance && TryOne(a, method, b, out result)) return true;
            if (!bFirst && bReflects && TryOne(b, reflected, a, out result)) return true;
            result = default;
            return false;
        }

        private static bool TryOne(Value self, string name, Value other, out Value result)
        {
            result = default;
            return self.Kind == ValueKind.Instance && TryCallDunder(self, name, new[] { other }, out result) && result.Kind != ValueKind.NotImplemented;
        }

        // ---- Equality and hashing -------------------------------------------------------------------------------

        /// <summary>The <c>==</c> operator when an operand is an instance. Its result is whatever the special method returned.</summary>
        public static Value EqualOp(Value a, Value b)
        {
            if (a.Kind != ValueKind.Instance && b.Kind != ValueKind.Instance) return Value.FromBool(Equal(a, b));
            if (TryBinary(a, b, "__eq__", "__eq__", true, out Value r)) return r;
            return Value.FromBool(a.Kind == b.Kind && ReferenceEquals(a.RawObject, b.RawObject));
        }

        private static bool InstanceEqual(Value a, Value b) => EqualOp(a, b).IsTruthy;

        /// <summary>The <c>!=</c> operator: a class's <c>__ne__</c> if it has one, else the opposite of <c>==</c>.</summary>
        public static Value NotEqualOp(Value a, Value b)
        {
            if (a.Kind != ValueKind.Instance && b.Kind != ValueKind.Instance) return Value.FromBool(!Equal(a, b));
            if (TryBinary(a, b, "__ne__", "__ne__", true, out Value r)) return r;
            return Value.FromBool(!EqualOp(a, b).IsTruthy);
        }

        /// <summary>An ordering operator (<c>&lt;</c>, <c>&gt;</c>, ...) when an operand is an instance.</summary>
        public static Value CompareOp(Value a, Value b, Order o)
        {
            if (a.Kind != ValueKind.Instance && b.Kind != ValueKind.Instance) return Value.FromBool(Compare(a, b, o));
            if (TryBinary(a, b, OrderMethod(o, false), OrderMethod(o, true), true, out Value r)) return r;
            throw TypeError("'" + OrderSymbol(o) + "' not supported between instances of '" + TypeName(a) + "' and '" + TypeName(b) + "'");
        }

        private static int InstanceHash(Value v)
        {
            var instance = (PyInstance)v.RawObject;
            // The nearest class that says anything about equality decides: a class that defines __eq__ and not
            // __hash__ is unhashable, as is one that sets __hash__ = None.
            for (var c = instance.Class; c != null; c = c.Base)
            {
                if (c.Namespace.TryGetValue(Value.FromString("__hash__"), out Value hash))
                {
                    if (hash.Kind != ValueKind.Function) throw TypeError("unhashable type: '" + instance.Class.Name + "'");
                    if (!TryCallDunder(v, "__hash__", Array.Empty<Value>(), out Value result)) break;
                    if (!result.IsInteger) throw TypeError("__hash__ method should return an integer");
                    return Hash(result);
                }
                if (c.Namespace.ContainsKey(Value.FromString("__eq__"))) throw TypeError("unhashable type: '" + instance.Class.Name + "'");
            }
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(instance);
        }

        // ---- Arithmetic -----------------------------------------------------------------------------------------

        // a + b and the like, when an operand is an instance. Same-class operands do not try the reflected method.
        private static Value InstanceBinary(Value a, Value b, string method, string reflected, string symbol)
        {
            if (TryBinary(a, b, method, reflected, false, out Value r)) return r;
            throw BadOperands(symbol, a, b);
        }

        // The augmented assignments (+=, ...) use __iadd__ and friends first, and then fall back to the plain operator.
        private static bool TryInPlace(Value a, string method, Value b, out Value result)
        {
            result = default;
            return a.Kind == ValueKind.Instance && TryOne(a, method, b, out result);
        }

        // CPython words the error for a failed augmented assignment with the "op=" symbol.
        private static PyError AsInPlace(PyError e, string symbol)
        {
            return new PyError("TypeError", "unsupported operand type(s) for " + symbol + "=" + e.Problem.Substring(e.Problem.IndexOf(':')));
        }

        private static bool IsOperandError(PyError e, string symbol) =>
            e.TypeName == "TypeError" && e.Problem.StartsWith("unsupported operand type(s) for " + symbol + ":", StringComparison.Ordinal);

        public static Value AddInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__iadd__", b, out Value r)) return r;
            try { return Add(a, b); }
            catch (PyError e) when (IsOperandError(e, "+")) { throw AsInPlace(e, "+"); }
        }

        public static Value SubInPlace(Value a, Value b)
        {
            if (a.Kind == ValueKind.Set && b.Kind == ValueKind.Set && !((PySet)a.RawObject).Frozen)
            {
                var target = (PySet)a.RawObject;
                foreach (var v in ((PySet)b.RawObject).Members().ToList()) target.Remove(v);
                return a;
            }
            if (TryInPlace(a, "__isub__", b, out Value r)) return r;
            try { return Sub(a, b); }
            catch (PyError e) when (IsOperandError(e, "-")) { throw AsInPlace(e, "-"); }
        }

        public static Value MulInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__imul__", b, out Value r)) return r;
            try { return Mul(a, b); }
            catch (PyError e) when (IsOperandError(e, "*")) { throw AsInPlace(e, "*"); }
        }

        public static Value DivInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__itruediv__", b, out Value r)) return r;
            try { return Div(a, b); }
            catch (PyError e) when (IsOperandError(e, "/")) { throw AsInPlace(e, "/"); }
        }

        public static Value FloorDivInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__ifloordiv__", b, out Value r)) return r;
            try { return FloorDiv(a, b); }
            catch (PyError e) when (IsOperandError(e, "//")) { throw AsInPlace(e, "//"); }
        }

        public static Value ModInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__imod__", b, out Value r)) return r;
            try { return Mod(a, b); }
            catch (PyError e) when (IsOperandError(e, "%")) { throw AsInPlace(e, "%"); }
        }

        public static Value PowInPlace(Value a, Value b)
        {
            if (TryInPlace(a, "__ipow__", b, out Value r)) return r;
            try { return Pow(a, b); }
            catch (PyError e) when (IsOperandError(e, "** or pow()")) { throw AsInPlace(e, "**"); }
        }

        // ---- Containers and iteration ---------------------------------------------------------------------------

        /// <summary>True if the instance's class defines <c>__iter__</c>.</summary>
        public static bool IsIterable(Value v) => HasDunder(v, "__iter__");

        public static bool HasNext(Value v) =>
            ((PyInstance)v.RawObject).Class.TryFind("__next__", out Value next) && next.Kind == ValueKind.Function;

        private static Value InstanceGetItem(Value target, Value index)
        {
            if (TryCallDunder(target, "__getitem__", new[] { index }, out Value r)) return r;
            throw TypeError("'" + TypeName(target) + "' object is not subscriptable");
        }

        private static void InstanceSetItem(Value target, Value index, Value value)
        {
            if (!TryCallDunder(target, "__setitem__", new[] { index, value }, out _))
                throw TypeError("'" + TypeName(target) + "' object does not support item assignment");
        }

        private static bool InstanceContains(Value container, Value item)
        {
            if (TryCallDunder(container, "__contains__", new[] { item }, out Value r)) return r.IsTruthy;
            var it = InstanceIterate(container, "argument of type '" + TypeName(container) + "' is not a container or iterable");
            while (it.Next(out Value v))
                if (Equal(v, item)) return true;
            return false;
        }

        private static PyIterator InstanceIterate(Value v, string notIterable = null)
        {
            if (!TryCallDunder(v, "__iter__", Array.Empty<Value>(), out Value iterator))
                throw TypeError(notIterable ?? "'" + TypeName(v) + "' object is not iterable");
            if (iterator.Kind == ValueKind.Iterator) return (PyIterator)iterator.RawObject;
            if (iterator.Kind == ValueKind.Instance && ((PyInstance)iterator.RawObject).Class.TryFind("__next__", out Value next) && next.Kind == ValueKind.Function)
                return new InstanceIterator(iterator);
            throw TypeError("iter() returned non-iterator of type '" + TypeName(iterator) + "'");
        }

        // ---- Truth value and length -----------------------------------------------------------------------------

        private static bool InstanceTruthy(Value v)
        {
            if (TryCallDunder(v, "__bool__", Array.Empty<Value>(), out Value b))
            {
                if (b.Kind != ValueKind.Bool) throw TypeError("__bool__ should return bool, returned " + TypeName(b));
                return b.RawInt != 0;
            }
            return !HasDunder(v, "__len__") || InstanceLength(v) != 0;
        }

        private static long InstanceLength(Value v)
        {
            if (!TryCallDunder(v, "__len__", Array.Empty<Value>(), out Value n))
                throw TypeError("object of type '" + TypeName(v) + "' has no len()");
            if (!n.IsInteger) throw TypeError("'" + TypeName(n) + "' object cannot be interpreted as an integer");
            if (n.Kind == ValueKind.BigInt) throw IndexTooLarge();
            if (n.RawInt < 0) throw ValueError("__len__() should return >= 0");
            return n.RawInt;
        }

        // ---- Ordering -------------------------------------------------------------------------------------------

        private static string OrderMethod(Order o, bool reflected)
        {
            switch (o)
            {
                case Order.Lt: return reflected ? "__gt__" : "__lt__";
                case Order.Le: return reflected ? "__ge__" : "__le__";
                case Order.Gt: return reflected ? "__lt__" : "__gt__";
                default: return reflected ? "__le__" : "__ge__";
            }
        }
    }
}
