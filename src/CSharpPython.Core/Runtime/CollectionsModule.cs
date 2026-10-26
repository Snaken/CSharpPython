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
            m.Members["Counter"] = Value.FromType(PyTypes.Counter);
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
