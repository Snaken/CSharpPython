using System.Collections.Generic;

namespace CSharpPython.Editor
{
    /// <summary>The parameter lists of the built-in functions and methods, for signature help.</summary>
    internal static class Signatures
    {
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            // Built-in functions.
            { "print", "*values, sep=' ', end='\\n'" },
            { "len", "obj" },
            { "range", "start_or_stop, stop=None, step=1" },
            { "int", "x=0, base=10" },
            { "float", "x=0.0" },
            { "str", "obj=''" },
            { "bool", "x=False" },
            { "repr", "obj" },
            { "abs", "x" },
            { "min", "*args, key=None, default=None" },
            { "max", "*args, key=None, default=None" },
            { "sum", "iterable, start=0" },
            { "round", "number, ndigits=None" },
            { "sorted", "iterable, *, key=None, reverse=False" },
            { "list", "iterable=()" },
            { "tuple", "iterable=()" },
            { "dict", "mapping=None, **kwargs" },
            { "enumerate", "iterable, start=0" },
            { "zip", "*iterables" },
            { "reversed", "seq" },
            { "any", "iterable" },
            { "all", "iterable" },
            { "chr", "i" },
            { "ord", "c" },
            { "divmod", "a, b" },
            { "pow", "base, exp, mod=None" },
            { "format", "value, format_spec=''" },
            { "type", "obj" },
            { "isinstance", "obj, classinfo" },
            { "issubclass", "cls, classinfo" },
            { "callable", "obj" },
            { "object", "" },
            { "getattr", "obj, name, default=None" },
            { "setattr", "obj, name, value" },
            { "hasattr", "obj, name" },

            // math module.
            { "math.sqrt", "x" }, { "math.log", "x, base=e" }, { "math.log2", "x" }, { "math.log10", "x" },
            { "math.floor", "x" }, { "math.ceil", "x" }, { "math.trunc", "x" }, { "math.isnan", "x" },
            { "math.isinf", "x" }, { "math.sin", "x" }, { "math.cos", "x" }, { "math.tan", "x" },
            { "math.asin", "x" }, { "math.acos", "x" }, { "math.atan", "x" }, { "math.atan2", "y, x" },
            { "math.exp", "x" }, { "math.fabs", "x" }, { "math.hypot", "x, y" }, { "math.radians", "x" },
            { "math.degrees", "x" }, { "math.pow", "x, y" },

            // str methods.
            { "str.upper", "" }, { "str.lower", "" }, { "str.strip", "chars=None" }, { "str.lstrip", "chars=None" },
            { "str.rstrip", "chars=None" }, { "str.split", "sep=None, maxsplit=-1" }, { "str.join", "iterable" },
            { "str.replace", "old, new, count=-1" }, { "str.startswith", "prefix" }, { "str.endswith", "suffix" },
            { "str.find", "sub, start=0, end=None" }, { "str.rfind", "sub, start=0, end=None" },
            { "str.index", "sub, start=0, end=None" }, { "str.count", "sub" }, { "str.isdigit", "" },
            { "str.isalpha", "" }, { "str.isalnum", "" }, { "str.isspace", "" }, { "str.isupper", "" },
            { "str.islower", "" }, { "str.capitalize", "" }, { "str.title", "" }, { "str.format", "*args, **kwargs" },
            { "str.zfill", "width" }, { "str.ljust", "width, fillchar=' '" }, { "str.rjust", "width, fillchar=' '" },
            { "str.center", "width, fillchar=' '" }, { "str.splitlines", "keepends=False" },

            // list methods.
            { "list.append", "x" }, { "list.extend", "iterable" }, { "list.insert", "index, x" },
            { "list.pop", "index=-1" }, { "list.remove", "x" }, { "list.index", "x, start=0, end=None" },
            { "list.count", "x" }, { "list.sort", "*, key=None, reverse=False" }, { "list.reverse", "" },
            { "list.clear", "" }, { "list.copy", "" },

            // dict methods.
            { "dict.get", "key, default=None" }, { "dict.keys", "" }, { "dict.values", "" }, { "dict.items", "" },
            { "dict.pop", "key, default=None" }, { "dict.setdefault", "key, default=None" },
            { "dict.update", "other=None, **kwargs" }, { "dict.clear", "" }, { "dict.copy", "" },

            // tuple methods.
            { "tuple.index", "x, start=0, end=None" }, { "tuple.count", "x" },
        };

        /// <summary>The parameter list for a known built-in, or null. Keys look like "len", "math.sqrt" or "str.upper".</summary>
        public static string Find(string key) => Table.TryGetValue(key, out var s) ? s : null;

        /// <summary>
        /// The parameter list for a native function: its own <see cref="NativeFunction.Signature"/>, else the table
        /// entry under <paramref name="key"/>, else one made up from its argument counts.
        /// </summary>
        public static string For(NativeFunction fn, string key, int selfCount)
        {
            if (fn.Signature != null) return fn.Signature;
            var known = Find(key);
            if (known != null) return known;

            int min = fn.MinArgs - selfCount;
            int max = fn.MaxArgs < 0 ? -1 : fn.MaxArgs - selfCount;
            var parts = new List<string>();
            int count = max < 0 ? min : max;
            for (int i = 0; i < count; i++) parts.Add(i < min ? "arg" + (i + 1) : "arg" + (i + 1) + "=...");
            if (max < 0) parts.Add("*args");
            if (fn.AcceptsKeywords) parts.Add("**kwargs");
            return string.Join(", ", parts);
        }
    }
}
