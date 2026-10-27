using System;

namespace CSharpPython
{
    /// <summary>The <c>functools</c> module (so far, <c>reduce</c>) and the <c>operator</c> functions that go with it.</summary>
    internal static class FunctoolsModule
    {
        public static PyModule Create()
        {
            var m = new PyModule("functools");
            m.Members["reduce"] = Value.FromNative(new NativeFunction("reduce", Reduce, 2, 3) { Signature = "function, iterable, initial=<missing>" });
            return m;
        }

        private static Value Reduce(CallContext ctx, ReadOnlySpan<Value> a)
        {
            if (!PyTypes.IsCallable(a[0])) throw Ops.TypeError("'" + a[0].TypeName + "' object is not callable");
            var it = Ops.GetIterator(a[1]);
            Value total;
            if (a.Length == 3) total = a[2];
            else if (!it.Next(out total)) throw Ops.TypeError("reduce() of empty iterable with no initial value");
            int count = 0;
            while (it.Next(out Value item))
            {
                total = ctx.Call(a[0], total, item);
                if (++count < 1024) continue;
                ctx.Charge(count);
                count = 0;
            }
            ctx.Charge(count);
            return total;
        }

        public static PyModule CreateOperator()
        {
            var m = new PyModule("operator");
            Binary(m, "add", Ops.Add);
            Binary(m, "sub", Ops.Sub);
            Binary(m, "mul", Ops.Mul);
            Binary(m, "truediv", Ops.Div);
            Binary(m, "floordiv", Ops.FloorDiv);
            Binary(m, "mod", Ops.Mod);
            Binary(m, "pow", Ops.Pow);
            Binary(m, "and_", Ops.BitAnd);
            Binary(m, "or_", Ops.BitOr);
            Binary(m, "xor", Ops.BitXor);
            Binary(m, "lshift", Ops.LShift);
            Binary(m, "rshift", Ops.RShift);
            Binary(m, "concat", Ops.Add);
            Binary(m, "eq", Ops.EqualOp);
            Binary(m, "ne", Ops.NotEqualOp);
            Binary(m, "lt", (x, y) => Ops.CompareOp(x, y, Ops.Order.Lt));
            Binary(m, "le", (x, y) => Ops.CompareOp(x, y, Ops.Order.Le));
            Binary(m, "gt", (x, y) => Ops.CompareOp(x, y, Ops.Order.Gt));
            Binary(m, "ge", (x, y) => Ops.CompareOp(x, y, Ops.Order.Ge));
            Binary(m, "is_", (x, y) => Value.FromBool(Ops.Is(x, y)));
            Binary(m, "is_not", (x, y) => Value.FromBool(!Ops.Is(x, y)));
            Binary(m, "contains", (x, y) => Value.FromBool(Ops.Contains(x, y)));
            Binary(m, "getitem", Ops.GetItem);
            Unary(m, "neg", Ops.Negate);
            Unary(m, "pos", Ops.Plus);
            Unary(m, "invert", Ops.Invert);
            Unary(m, "inv", Ops.Invert);
            Unary(m, "not_", x => Value.FromBool(!x.IsTruthy));
            Unary(m, "truth", x => Value.FromBool(x.IsTruthy));
            return m;
        }

        private static void Binary(PyModule m, string name, Func<Value, Value, Value> f) =>
            m.Members[name] = Value.FromNative(new NativeFunction(name, (c, a) => f(a[0], a[1]), 2, 2) { Signature = "a, b" });

        private static void Unary(PyModule m, string name, Func<Value, Value> f) =>
            m.Members[name] = Value.FromNative(new NativeFunction(name, (c, a) => f(a[0]), 1, 1) { Signature = "a" });
    }
}
