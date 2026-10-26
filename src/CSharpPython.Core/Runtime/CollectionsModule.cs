using System;

namespace CSharpPython
{
    /// <summary>The <c>collections</c> module: <c>defaultdict</c> and <c>Counter</c> (dictionaries that answer a missing key themselves).</summary>
    internal static class CollectionsModule
    {
        public static PyModule Create()
        {
            var m = new PyModule("collections");
            PyTypes.DefaultDict.Constructor = new NativeFunction("defaultdict", MakeDefaultDict, 0, -1, true);
            PyTypes.Counter.Constructor = new NativeFunction("Counter", MakeCounter, 0, -1, true);
            m.Members["defaultdict"] = Value.FromType(PyTypes.DefaultDict);
            PyTypes.Deque.Constructor = new NativeFunction("deque", MakeDeque, 0, 2, true);
            m.Members["Counter"] = Value.FromType(PyTypes.Counter);
            m.Members["deque"] = Value.FromType(PyTypes.Deque);
            m.Members["namedtuple"] = Value.FromNative(new NativeFunction("namedtuple", NamedTuples.Make, 0, 2, true));
            return m;
        }

        private static Value MakeDefaultDict(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var dict = new PyDict { Flavor = DictFlavor.Default, Factory = Value.None };
            if (ctx.PositionalCount > 2) throw Ops.TypeError("defaultdict expected at most 2 arguments, got " + ctx.PositionalCount);
            if (ctx.PositionalCount > 0)
            {
                if (!a[0].IsNone && !PyTypes.IsCallable(a[0])) throw Ops.TypeError("first argument must be callable or None");
                dict.Factory = a[0];
            }
            if (ctx.PositionalCount > 1) Methods.UpdateDict(dict, a[1]);
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
                dict.Set(Value.FromString(ctx.KeywordNames[i]), a[ctx.PositionalCount + i]);
            return Value.FromDict(dict);
        }

        private static Value MakeDeque(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value iterable = default, maxArg = Value.None;
            bool hasIterable = false;
            if (ctx.PositionalCount > 0) { iterable = a[0]; hasIterable = true; }
            if (ctx.PositionalCount > 1) maxArg = a[1];
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
            {
                string name = ctx.KeywordNames[i];
                Value v = a[ctx.PositionalCount + i];
                if (name == "maxlen" && ctx.PositionalCount < 2) maxArg = v;
                else if (name == "iterable" && ctx.PositionalCount < 1) { iterable = v; hasIterable = true; }
                else throw Ops.TypeError("deque() got an unexpected keyword argument '" + name + "'");
            }
            long maxLen = -1;
            if (!maxArg.IsNone)
            {
                if (!maxArg.IsInteger) throw Ops.TypeError("an integer is required");
                maxLen = maxArg.AsInt();
                if (maxLen < 0) throw Ops.ValueError("maxlen must be non-negative");
            }
            var deque = new PyDeque(maxLen);
            if (hasIterable) foreach (var item in Ops.ToList(iterable)) deque.AddRight(item);
            return Value.FromDeque(deque);
        }

        private static Value MakeCounter(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var dict = new PyDict { Flavor = DictFlavor.Counter };
            if (ctx.PositionalCount > 1) throw Ops.TypeError("expected at most 1 argument, got " + ctx.PositionalCount);
            if (ctx.PositionalCount > 0 && !a[0].IsNone) Methods.CountInto(dict, a[0], false);
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
                Methods.CountInto(dict, Value.FromDict(Single(ctx.KeywordNames[i], a[ctx.PositionalCount + i])), false);
            return Value.FromDict(dict);
        }

        private static PyDict Single(string key, Value value)
        {
            var d = new PyDict();
            d.Set(Value.FromString(key), value);
            return d;
        }
    }
}
