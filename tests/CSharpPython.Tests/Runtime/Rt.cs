using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpPython.Tests.Runtime
{
    /// <summary>Helpers for calling built-ins and methods directly, without compiling a script.</summary>
    public static class Rt
    {
        private static readonly Dictionary<string, Value> BuiltinTable = CreateBuiltins(out Modules);
        public static readonly Dictionary<string, PyModule> Modules;

        private static Dictionary<string, Value> CreateBuiltins(out Dictionary<string, PyModule> modules)
        {
            var table = new Dictionary<string, Value>();
            modules = new Dictionary<string, PyModule>();
            Builtins.Install(table, modules);
            return table;
        }

        public static Value V(object o)
        {
            switch (o)
            {
                case null: return Value.None;
                case Value v: return v;
                case bool b: return Value.FromBool(b);
                case int i: return Value.FromInt(i);
                case long l: return Value.FromInt(l);
                case double d: return Value.FromFloat(d);
                case string s: return Value.FromString(s);
                default: throw new ArgumentException("cannot convert " + o.GetType());
            }
        }

        public static Value L(params object[] items) => Value.FromList(new PyList(items.Select(V)));

        public static Value T(params object[] items) => Value.Tuple(items.Select(V).ToArray());

        public static Value D(params object[] keyValues)
        {
            var d = new PyDict();
            for (int i = 0; i < keyValues.Length; i += 2) d.Set(V(keyValues[i]), V(keyValues[i + 1]));
            return Value.FromDict(d);
        }

        public static Value Range(long start, long stop, long step = 1) => Value.FromRange(new PyRange(start, stop, step));

        public static string R(Value v) => v.Repr();

        public static string Output;

        public static CallContext Context(int positional, string[] keywordNames = null)
        {
            var ctx = new CallContext();
            ctx.SetCall(positional, keywordNames);
            ctx.OutputSink = s => Output += s;
            return ctx;
        }

        public static Value Builtin(string name) => BuiltinTable[name];

        public static Value Call(string name, params object[] args)
        {
            var fn = (NativeFunction)BuiltinTable[name].RawObject;
            return Invoke(fn, args.Select(V).ToArray(), null);
        }

        public static Value CallKw(string name, object[] positional, params (string, object)[] keywords)
        {
            var fn = (NativeFunction)BuiltinTable[name].RawObject;
            var all = positional.Select(V).Concat(keywords.Select(k => V(k.Item2))).ToArray();
            return Invoke(fn, all, keywords.Select(k => k.Item1).ToArray(), positional.Length);
        }

        public static Value Method(Value self, string name, params object[] args)
        {
            if (!Ops.TryGetMethod(self, name, out var fn)) throw new MissingMethodException(name);
            var all = new[] { self }.Concat(args.Select(V)).ToArray();
            return Invoke(fn, all, null);
        }

        public static Value MethodKw(Value self, string name, object[] positional, params (string, object)[] keywords)
        {
            Ops.TryGetMethod(self, name, out var fn);
            var all = new[] { self }.Concat(positional.Select(V)).Concat(keywords.Select(k => V(k.Item2))).ToArray();
            return Invoke(fn, all, keywords.Select(k => k.Item1).ToArray(), positional.Length + 1);
        }

        private static Value Invoke(NativeFunction fn, Value[] args, string[] keywords, int? positional = null)
        {
            int pos = positional ?? args.Length;
            if (pos < fn.MinArgs || (fn.MaxArgs >= 0 && pos > fn.MaxArgs))
                throw new PyError("TypeError", fn.Name + "() takes a different number of arguments (" + pos + ")");
            var ctx = Context(pos, keywords);
            return fn.Fn(ctx, args);
        }

        public static PyError Raises(Action action)
        {
            return Assert.Throws<PyError>(() => action());
        }
    }
}
