using System;
using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>The <c>itertools</c> module.</summary>
    internal static class ItertoolsModule
    {
        /// <summary><c>itertools.chain</c>, which has a <c>from_iterable</c> of its own.</summary>
        public static readonly NativeFunction Chain = new NativeFunction("chain", MakeChain, 0, -1);

        public static readonly NativeFunction ChainFromIterable = new NativeFunction("from_iterable", MakeChainFromIterable, 1, 1);

        public static PyModule Create()
        {
            var m = new PyModule("itertools");
            Add(m, "count", MakeCount, 0, 2, true, "start=0, step=1");
            Add(m, "cycle", (c, a) => Make(ItertoolsKind.Cycle, new[] { Ops.GetIterator(a[0]) }, new Value[0], new long[1]), 1, 1);
            Add(m, "repeat", MakeRepeat, 0, 2, true, "object, times=None");
            m.Members["chain"] = Value.FromNative(Chain);
            Add(m, "islice", MakeIslice, 2, 4);
            Add(m, "accumulate", MakeAccumulate, 1, 2, true, "iterable, func=None, *, initial=None");
            Add(m, "compress", (c, a) => Make(ItertoolsKind.Compress, new[] { Ops.GetIterator(a[0]), Ops.GetIterator(a[1]) }, new Value[0], new long[0]), 2, 2);
            Add(m, "dropwhile", (c, a) => Make(ItertoolsKind.DropWhile, new[] { Ops.GetIterator(a[1]) }, new[] { Callable(a[0], "dropwhile") }, new long[] { 1 }), 2, 2);
            Add(m, "takewhile", (c, a) => Make(ItertoolsKind.TakeWhile, new[] { Ops.GetIterator(a[1]) }, new[] { Callable(a[0], "takewhile") }, new long[1]), 2, 2);
            Add(m, "filterfalse", (c, a) => Make(ItertoolsKind.FilterFalse, new[] { Ops.GetIterator(a[1]) }, new[] { a[0].IsNone ? a[0] : Callable(a[0], "filterfalse") }, new long[0]), 2, 2);
            Add(m, "starmap", (c, a) => Make(ItertoolsKind.StarMap, new[] { Ops.GetIterator(a[1]) }, new[] { Callable(a[0], "starmap") }, new long[0]), 2, 2);
            Add(m, "zip_longest", MakeZipLongest, 0, -1, true, "*iterables, fillvalue=None");
            Add(m, "pairwise", (c, a) => Make(ItertoolsKind.Pairwise, new[] { Ops.GetIterator(a[0]) }, new Value[1], new long[1]), 1, 1);
            Add(m, "batched", MakeBatched, 2, 2, true, "iterable, n, *, strict=False");
            Add(m, "product", MakeProduct, 0, -1, true, "*iterables, repeat=1");
            Add(m, "permutations", MakePermutations, 1, 2, false, "iterable, r=None");
            Add(m, "combinations", (c, a) => MakeCombinations(a, ItertoolsKind.Combinations), 2, 2, false, "iterable, r");
            Add(m, "combinations_with_replacement", (c, a) => MakeCombinations(a, ItertoolsKind.CombinationsWithReplacement), 2, 2, false, "iterable, r");
            Add(m, "groupby", MakeGroupBy, 1, 2, true, "iterable, key=None");
            Add(m, "tee", MakeTee, 1, 2, false, "iterable, n=2");
            return m;
        }

        private static void Add(PyModule m, string name, NativeFn fn, int min, int max, bool keywords = false, string signature = null)
        {
            m.Members[name] = Value.FromNative(new NativeFunction(name, fn, min, max, keywords) { Signature = signature });
        }

        private static Value Make(ItertoolsKind kind, PyIterator[] sources, Value[] args, long[] state, List<Value> items = null) =>
            Value.FromIterator(new ItertoolsIterator(kind, sources, args, state, items));

        private static Value Callable(Value f, string function)
        {
            if (!PyTypes.IsCallable(f)) throw Ops.TypeError("'" + f.TypeName + "' object is not callable");
            return f;
        }

        // The keyword arguments given, each of which must be one of 'allowed'.
        private static void CheckKeywords(CallContext ctx, string function, params string[] allowed)
        {
            foreach (var name in ctx.KeywordNames)
                if (Array.IndexOf(allowed, name) < 0)
                    throw Ops.TypeError(function + "() got an unexpected keyword argument '" + name + "'");
        }

        private static Value MakeCount(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "count", "start", "step");
            Value start = Value.FromInt(0), step = Value.FromInt(1);
            if (ctx.PositionalCount > 0) start = a[0];
            if (ctx.PositionalCount > 1) step = a[1];
            if (ctx.TryGetKeyword(a, "start", out Value s)) start = ctx.PositionalCount > 0 ? throw Ops.TypeError("argument for count() given by name ('start') and position (1)") : s;
            if (ctx.TryGetKeyword(a, "step", out Value t)) step = ctx.PositionalCount > 1 ? throw Ops.TypeError("argument for count() given by name ('step') and position (2)") : t;
            if (!start.IsNumber || !step.IsNumber) throw Ops.TypeError("a number is required");
            return Make(ItertoolsKind.Count, new PyIterator[0], new[] { start, step }, new long[0]);
        }

        private static Value MakeRepeat(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "repeat", "object", "times");
            Value obj = ctx.PositionalCount > 0 ? a[0] : default;
            Value times = ctx.PositionalCount > 1 ? a[1] : Value.None;
            if (ctx.PositionalCount == 0 && !ctx.TryGetKeyword(a, "object", out obj)) throw Ops.TypeError("repeat() missing required argument 'object' (pos 1)");
            if (ctx.PositionalCount < 2) ctx.TryGetKeyword(a, "times", out times);
            long left = -1;
            if (!times.IsNone)
            {
                if (!times.IsInteger) throw Ops.TypeError("'" + times.TypeName + "' object cannot be interpreted as an integer");
                left = Math.Max(0, times.AsInt());
            }
            return Make(ItertoolsKind.Repeat, new PyIterator[0], new[] { obj }, new[] { left });
        }

        private static Value MakeChain(CallContext ctx, ReadOnlySpan<Value> a) =>
            Make(ItertoolsKind.Chain, new PyIterator[1], a.ToArray(), new long[1]);

        private static Value MakeChainFromIterable(CallContext ctx, ReadOnlySpan<Value> a) =>
            Make(ItertoolsKind.ChainFrom, new[] { Ops.GetIterator(a[0]), null }, new Value[0], new long[0]);

        private static Value MakeIslice(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var source = Ops.GetIterator(a[0]);
            long start = 0, stop = -1, step = 1;
            if (a.Length == 2) stop = Bound(a[1], "Stop argument for islice() must be None or an integer: 0 <= x <= sys.maxsize.", -1);
            else
            {
                start = Bound(a[1], "Indices for islice() must be None or an integer: 0 <= x <= sys.maxsize.", 0);
                stop = Bound(a[2], "Indices for islice() must be None or an integer: 0 <= x <= sys.maxsize.", -1);
                if (a.Length == 4 && !a[3].IsNone)
                {
                    step = a[3].IsInteger ? a[3].AsInt() : 0;
                    if (step < 1) throw Ops.ValueError("Step for islice() must be a positive integer or None.");
                }
            }
            return Make(ItertoolsKind.Islice, new[] { source }, new Value[0], new[] { start, stop, step, 0L });
        }

        // A non-negative integer, or 'none' for None.
        private static long Bound(Value v, string message, long none)
        {
            if (v.IsNone) return none;
            if (!v.IsInteger) throw Ops.ValueError(message);
            long n = v.AsInt();
            if (n < 0) throw Ops.ValueError(message);
            return n;
        }

        private static Value MakeAccumulate(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "accumulate", "func", "initial");
            Value func = ctx.PositionalCount > 1 ? a[1] : Value.None;
            if (ctx.PositionalCount < 2) ctx.TryGetKeyword(a, "func", out func);
            if (!func.IsNone) Callable(func, "accumulate");
            var source = Ops.GetIterator(a[0]);
            Value total = default;
            long phase = 0;
            if (ctx.TryGetKeyword(a, "initial", out Value initial) && !initial.IsNone)
            {
                total = initial;
                phase = 2;
            }
            return Make(ItertoolsKind.Accumulate, new[] { source }, new[] { func, total }, new[] { phase });
        }

        private static Value MakeZipLongest(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "zip_longest", "fillvalue");
            Value fill = Value.None;
            ctx.TryGetKeyword(a, "fillvalue", out fill);
            var sources = new PyIterator[ctx.PositionalCount];
            for (int i = 0; i < sources.Length; i++) sources[i] = Ops.GetIterator(a[i]);
            return Make(ItertoolsKind.ZipLongest, sources, new[] { fill }, new long[] { sources.Length });
        }

        private static Value MakeBatched(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "batched", "strict");
            if (!a[1].IsInteger) throw Ops.TypeError("'" + a[1].TypeName + "' object cannot be interpreted as an integer");
            long n = a[1].AsInt();
            if (n < 1) throw Ops.ValueError("n must be at least one");
            bool strict = ctx.TryGetKeyword(a, "strict", out Value s) && s.IsTruthy;
            return Make(ItertoolsKind.Batched, new[] { Ops.GetIterator(a[0]) }, new Value[0], new[] { n, strict ? 1L : 0L });
        }

        private static Value MakeTee(CallContext ctx, ReadOnlySpan<Value> a)
        {
            long n = 2;
            if (a.Length > 1)
            {
                if (!a[1].IsInteger) throw Ops.TypeError("'" + a[1].TypeName + "' object cannot be interpreted as an integer");
                n = a[1].AsInt();
            }
            if (n < 0) throw Ops.ValueError("n must be >= 0");
            if (n > 1000000) throw new PyError("MemoryError", "tee() cannot make that many iterators");
            var source = Ops.GetIterator(a[0]);
            var seen = Value.FromList(new PyList(new List<Value>()));
            var copies = new Value[n];
            for (int i = 0; i < n; i++) copies[i] = Make(ItertoolsKind.Tee, new[] { source }, new[] { seen }, new long[2]);
            return Value.Tuple(copies);
        }

        private static List<Value> Pool(Value iterable) => Ops.ToList(iterable);

        private static Value MakeProduct(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "product", "repeat");
            long repeat = 1;
            if (ctx.TryGetKeyword(a, "repeat", out Value r))
            {
                if (!r.IsInteger) throw Ops.TypeError("'" + r.TypeName + "' object cannot be interpreted as an integer");
                repeat = r.AsInt();
                if (repeat < 0) throw Ops.ValueError("repeat argument cannot be negative");
            }
            var pools = new List<List<Value>>();
            for (int i = 0; i < ctx.PositionalCount; i++) pools.Add(Pool(a[i]));
            int given = pools.Count;
            if ((long)given * repeat > 1000) throw new PyError("MemoryError", "too many iterables");
            for (long k = 1; k < repeat; k++)
                for (int i = 0; i < given; i++) pools.Add(pools[i]);
            if (repeat == 0) pools.Clear();
            var state = new long[3 + 2 * pools.Count];
            state[0] = pools.Count;
            var items = new List<Value>();
            for (int i = 0; i < pools.Count; i++)
            {
                state[3 + i] = pools[i].Count;
                items.AddRange(pools[i]);
            }
            return Make(ItertoolsKind.Product, new PyIterator[0], new Value[0], state, items);
        }

        private static long Count(Value r)
        {
            if (!r.IsInteger) throw Ops.TypeError("Expected int as r");
            long n = r.AsInt();
            if (n < 0) throw Ops.ValueError("r must be non-negative");
            return n;
        }

        private static Value MakePermutations(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var pool = Pool(a[0]);
            int n = pool.Count;
            long r = ctx.PositionalCount > 1 && !a[1].IsNone ? Count(a[1]) : n;
            bool empty = r > n; // nothing to give: a finished one with no positions
            if (empty) r = 0;
            var state = new long[4 + n + r];
            state[0] = n;
            state[1] = r;
            if (empty) state[3] = 1;
            for (int i = 0; i < n; i++) state[4 + i] = i;
            for (int i = 0; i < r; i++) state[4 + n + i] = n - i;
            return Make(ItertoolsKind.Permutations, new PyIterator[0], new Value[0], state, pool);
        }

        private static Value MakeCombinations(ReadOnlySpan<Value> a, ItertoolsKind kind)
        {
            var pool = Pool(a[0]);
            long n = pool.Count;
            long r = Count(a[1]);
            bool repeats = kind == ItertoolsKind.CombinationsWithReplacement;
            bool empty = repeats ? n == 0 && r > 0 : r > n;
            var state = new long[4 + (empty ? 0 : r)];
            state[0] = n;
            state[1] = empty ? 0 : r;
            if (empty) state[3] = 1;
            else if (!repeats) for (int i = 0; i < r; i++) state[4 + i] = i;
            return Make(kind, new PyIterator[0], new Value[0], state, pool);
        }

        private static Value MakeGroupBy(CallContext ctx, ReadOnlySpan<Value> a)
        {
            CheckKeywords(ctx, "groupby", "key");
            Value key = ctx.PositionalCount > 1 ? a[1] : Value.None;
            if (ctx.PositionalCount < 2) ctx.TryGetKeyword(a, "key", out key);
            if (!key.IsNone) Callable(key, "groupby");
            return Make(ItertoolsKind.GroupBy, new[] { Ops.GetIterator(a[0]) }, new[] { key, default, default, default }, new long[4]);
        }
    }
}
