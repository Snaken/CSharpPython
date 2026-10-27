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
            m.Members["partial"] = Value.FromNative(new NativeFunction("partial", Partial, 1, -1, true) { Signature = "func, /, *args, **keywords" });
            m.Members["lru_cache"] = Value.FromNative(new NativeFunction("lru_cache", LruCache, 0, 2, true) { Signature = "maxsize=128, typed=False" });
            m.Members["cache"] = Value.FromNative(new NativeFunction("cache", (c, a) => Cached(a[0], -1, false), 1, 1) { Signature = "user_function" });
            m.Members["wraps"] = Value.FromNative(new NativeFunction("wraps", (c, a) =>
                Value.FromFunctool(new PyFunctool(FunctoolMode.Wraps, a[0], Array.Empty<Value>())), 1, 3, true) { Signature = "wrapped" });
            return m;
        }

        private static readonly PyType CacheInfoType = NamedTuples.CreateType("CacheInfo", new[] { "hits", "misses", "maxsize", "currsize" }, Array.Empty<Value>());

        /// <summary>What <c>cache_info()</c> returns: <c>CacheInfo(hits, misses, maxsize, currsize)</c>.</summary>
        public static Value CacheInfo(PyFunctool f) =>
            NamedTuples.Instance(CacheInfoType, new[]
            {
                Value.FromInt(f.Hits), Value.FromInt(f.Misses), f.MaxSize < 0 ? Value.None : Value.FromInt(f.MaxSize), Value.FromInt(f.Cache.Count),
            });

        private static Value Cached(Value function, long maxSize, bool typed)
        {
            if (!PyTypes.IsCallable(function)) throw Ops.TypeError("the first argument must be callable");
            return Value.FromFunctool(new PyFunctool(FunctoolMode.Cached, function, Array.Empty<Value>(), maxSize: maxSize, typed: typed));
        }

        private static Value Partial(CallContext ctx, ReadOnlySpan<Value> a)
        {
            if (ctx.PositionalCount < 1) throw Ops.TypeError("type 'partial' takes at least one argument");
            if (!PyTypes.IsCallable(a[0])) throw Ops.TypeError("the first argument must be callable");
            var args = a.Slice(1, ctx.PositionalCount - 1).ToArray();
            var kwValues = a.Slice(ctx.PositionalCount).ToArray();
            return Value.FromFunctool(new PyFunctool(FunctoolMode.Partial, a[0], args, (string[])ctx.KeywordNames.Clone(), kwValues));
        }

        // lru_cache(function), lru_cache(maxsize=128, typed=False) and lru_cache(maxsize=None).
        private static Value LruCache(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value maxArg = Value.FromInt(128), typedArg = Value.FromBool(false);
            if (ctx.PositionalCount > 0) maxArg = a[0];
            if (ctx.PositionalCount > 1) typedArg = a[1];
            foreach (var name in ctx.KeywordNames)
                if (name != "maxsize" && name != "typed") throw Ops.TypeError("lru_cache() got an unexpected keyword argument '" + name + "'");
            if (ctx.PositionalCount < 1 && ctx.TryGetKeyword(a, "maxsize", out Value m)) maxArg = m;
            if (ctx.PositionalCount < 2 && ctx.TryGetKeyword(a, "typed", out Value t)) typedArg = t;
            bool typed = typedArg.IsTruthy;
            if (ctx.PositionalCount == 1 && PyTypes.IsCallable(maxArg)) return Cached(maxArg, 128, typed);
            long maxSize;
            if (maxArg.IsNone) maxSize = -1;
            else if (maxArg.IsInteger) maxSize = Math.Max(0, maxArg.AsInt());
            else throw Ops.TypeError("Expected first argument to be an integer, a callable, or None");
            return Value.FromFunctool(new PyFunctool(FunctoolMode.CacheMaker, Value.None, Array.Empty<Value>(), maxSize: maxSize, typed: typed));
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
            m.Members["itemgetter"] = Value.FromNative(new NativeFunction("itemgetter", (c, a) =>
                Value.FromFunctool(new PyFunctool(FunctoolMode.ItemGetter, Value.None, a.ToArray())), 1, -1) { Signature = "item, /, *items" });
            m.Members["attrgetter"] = Value.FromNative(new NativeFunction("attrgetter", (c, a) =>
            {
                foreach (var name in a)
                    if (name.Kind != ValueKind.Str) throw Ops.TypeError("attribute name must be a string");
                return Value.FromFunctool(new PyFunctool(FunctoolMode.AttrGetter, Value.None, a.ToArray()));
            }, 1, -1) { Signature = "attr, /, *attrs" });
            m.Members["methodcaller"] = Value.FromNative(new NativeFunction("methodcaller", (c, a) =>
            {
                if (a[0].Kind != ValueKind.Str) throw Ops.TypeError("method name must be a string");
                if (c.KeywordNames.Length > 0) throw Ops.TypeError("methodcaller() does not take keyword arguments");
                return Value.FromFunctool(new PyFunctool(FunctoolMode.MethodCaller, Value.None, a.ToArray()));
            }, 1, -1, true) { Signature = "name, /, *args" });
            return m;
        }

        private static void Binary(PyModule m, string name, Func<Value, Value, Value> f) =>
            m.Members[name] = Value.FromNative(new NativeFunction(name, (c, a) => f(a[0], a[1]), 2, 2) { Signature = "a, b" });

        private static void Unary(PyModule m, string name, Func<Value, Value> f) =>
            m.Members[name] = Value.FromNative(new NativeFunction(name, (c, a) => f(a[0]), 1, 1) { Signature = "a" });
    }
}
