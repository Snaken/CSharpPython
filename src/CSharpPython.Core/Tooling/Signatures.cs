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
            { "frozenset", "iterable=()" },
            { "slice", "start, stop=None, step=None" },
            { "staticmethod", "function" }, { "classmethod", "function" }, { "property", "fget=None, fset=None, fdel=None" },
            { "dir", "obj" },
            { "vars", "obj" },
            { "map", "function, *iterables" },
            { "filter", "function, iterable" },
            { "bin", "x" },
            { "oct", "x" },
            { "hex", "x" },
            { "id", "obj" },
            { "delattr", "obj, name" },
            { "ascii", "obj" },
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
            { "hash", "obj" },
            { "set", "iterable=()" },
            { "iter", "obj" },
            { "next", "iterator, default=None" },

            // math module.
            { "math.sqrt", "x" }, { "math.log", "x, base=e" }, { "math.log2", "x" }, { "math.log10", "x" },
            { "math.floor", "x" }, { "math.ceil", "x" }, { "math.trunc", "x" }, { "math.isnan", "x" },
            { "math.isinf", "x" }, { "math.sin", "x" }, { "math.cos", "x" }, { "math.tan", "x" },
            { "math.asin", "x" }, { "math.acos", "x" }, { "math.atan", "x" }, { "math.atan2", "y, x" },
            { "math.exp", "x" }, { "math.fabs", "x" }, { "math.hypot", "*coordinates" }, { "math.radians", "x" },
            { "math.degrees", "x" }, { "math.pow", "x, y" },
            { "math.isfinite", "x" }, { "math.isclose", "a, b, *, rel_tol=1e-09, abs_tol=0.0" },
            { "math.copysign", "x, y" }, { "math.fmod", "x, y" }, { "math.remainder", "x, y" },
            { "math.modf", "x" }, { "math.frexp", "x" }, { "math.ldexp", "x, i" },
            { "math.cbrt", "x" }, { "math.exp2", "x" }, { "math.expm1", "x" }, { "math.log1p", "x" },
            { "math.sinh", "x" }, { "math.cosh", "x" }, { "math.tanh", "x" },
            { "math.asinh", "x" }, { "math.acosh", "x" }, { "math.atanh", "x" },
            { "math.factorial", "n" }, { "math.gcd", "*integers" }, { "math.lcm", "*integers" },
            { "math.isqrt", "n" }, { "math.comb", "n, k" }, { "math.perm", "n, k=None" },
            { "math.prod", "iterable, *, start=1" }, { "math.fsum", "iterable" }, { "math.dist", "p, q" },

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
            { "str.casefold", "" }, { "str.swapcase", "" }, { "str.isascii", "" }, { "str.isdecimal", "" },
            { "str.isnumeric", "" }, { "str.isidentifier", "" }, { "str.isprintable", "" }, { "str.istitle", "" },
            { "str.expandtabs", "tabsize=8" }, { "str.partition", "sep" }, { "str.rpartition", "sep" },
            { "str.removeprefix", "prefix" }, { "str.removesuffix", "suffix" },
            { "str.rindex", "sub, start=0, end=None" }, { "str.rsplit", "sep=None, maxsplit=-1" },

            // list methods.
            { "list.append", "x" }, { "list.extend", "iterable" }, { "list.insert", "index, x" },
            { "list.pop", "index=-1" }, { "list.remove", "x" }, { "list.index", "x, start=0, end=None" },
            { "list.count", "x" }, { "list.sort", "*, key=None, reverse=False" }, { "list.reverse", "" },
            { "list.clear", "" }, { "list.copy", "" },

            // dict methods.
            { "dict.get", "key, default=None" }, { "dict.keys", "" }, { "dict.values", "" }, { "dict.items", "" },
            { "dict.pop", "key, default=None" }, { "dict.setdefault", "key, default=None" },
            { "dict.update", "other=None, **kwargs" }, { "dict.clear", "" }, { "dict.copy", "" },
            { "dict.fromkeys", "iterable, value=None" }, { "dict.popitem", "" },

            // int and float methods.
            { "int.bit_length", "" }, { "int.bit_count", "" }, { "int.is_integer", "" }, { "float.is_integer", "" },

            // set methods.
            { "property.getter", "function" }, { "property.setter", "function" }, { "property.deleter", "function" },
            { "set.add", "x" }, { "set.remove", "x" }, { "set.discard", "x" }, { "set.pop", "" }, { "set.clear", "" },
            { "set.copy", "" }, { "set.update", "*iterables" }, { "set.union", "*iterables" },
            { "set.intersection", "*iterables" }, { "set.difference", "*iterables" },
            { "set.symmetric_difference", "other" }, { "set.intersection_update", "*iterables" },
            { "set.difference_update", "*iterables" }, { "set.symmetric_difference_update", "other" },
            { "set.issubset", "other" }, { "set.issuperset", "other" }, { "set.isdisjoint", "other" },

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
