using System.Collections.Generic;

namespace CSharpPython.Editor
{
    /// <summary>
    /// Types for the built-in functions and for the <c>math</c> and <c>random</c> modules, in the form
    /// <see cref="ModuleBuilder.Describe"/> accepts. Where a result depends on the argument types (<c>abs</c>,
    /// <c>min</c>) or is a lazy object (<c>range</c>, <c>zip</c>), the type is <c>any</c>. Methods of built-in
    /// types are not typed yet. The parameter lists match <see cref="Signatures"/>.
    /// </summary>
    internal static class TypedSignatures
    {
        private static readonly Dictionary<string, string> Source = new Dictionary<string, string>
        {
            // Built-in functions.
            { "print", "*values, sep: str=' ', end: str='\\n' -> none" },
            { "len", "obj -> int" },
            { "range", "start_or_stop: int, stop: int=None, step: int=1 -> any" },
            { "int", "x=0, base: int=10 -> int" },
            { "float", "x=0.0 -> float" },
            { "str", "obj='' -> str" },
            { "bool", "x=False -> bool" },
            { "repr", "obj -> str" },
            { "abs", "x -> any" },
            { "min", "*args, key=None, default=None -> any" },
            { "max", "*args, key=None, default=None -> any" },
            { "sum", "iterable: list[any], start=0 -> any" },
            { "round", "number: float, ndigits: int=None -> any" },
            { "sorted", "iterable: list[any], *, key=None, reverse: bool=False -> list[any]" },
            { "list", "iterable=() -> list[any]" },
            { "tuple", "iterable=() -> any" },
            { "dict", "mapping=None, **kwargs -> any" },
            { "enumerate", "iterable: list[any], start: int=0 -> any" },
            { "zip", "*iterables -> any" },
            { "frozenset", "iterable=() -> any" },
            { "slice", "start, stop=None, step=None -> any" },
            { "dir", "obj -> list[str]" },
            { "vars", "obj -> any" },
            { "map", "function, *iterables -> any" },
            { "filter", "function, iterable -> any" },
            { "bin", "x: int -> str" },
            { "oct", "x: int -> str" },
            { "hex", "x: int -> str" },
            { "id", "obj -> int" },
            { "delattr", "obj, name: str -> none" },
            { "ascii", "obj -> str" },
            { "reversed", "seq -> any" },
            { "any", "iterable: list[any] -> bool" },
            { "all", "iterable: list[any] -> bool" },
            { "chr", "i: int -> str" },
            { "ord", "c: str -> int" },
            { "divmod", "a, b -> any" },
            { "pow", "base, exp, mod=None -> any" },
            { "format", "value, format_spec: str='' -> str" },
            { "type", "obj -> any" },
            { "isinstance", "obj, classinfo -> bool" },
            { "issubclass", "cls, classinfo -> bool" },
            { "callable", "obj -> bool" },
            { "getattr", "obj, name: str, default=None -> any" },
            { "setattr", "obj, name: str, value -> none" },
            { "hasattr", "obj, name: str -> bool" },
            { "hash", "obj -> int" },
            { "set", "iterable=() -> any" },
            { "iter", "obj -> any" },
            { "next", "iterator, default=None -> any" },

            // math module.
            { "math.sqrt", "x: float -> float" }, { "math.log", "x: float, base: float=e -> float" },
            { "math.log2", "x: float -> float" }, { "math.log10", "x: float -> float" },
            { "math.floor", "x: float -> int" }, { "math.ceil", "x: float -> int" }, { "math.trunc", "x: float -> int" },
            { "math.isnan", "x: float -> bool" }, { "math.isinf", "x: float -> bool" }, { "math.isfinite", "x: float -> bool" },
            { "math.sin", "x: float -> float" }, { "math.cos", "x: float -> float" }, { "math.tan", "x: float -> float" },
            { "math.asin", "x: float -> float" }, { "math.acos", "x: float -> float" }, { "math.atan", "x: float -> float" },
            { "math.atan2", "y: float, x: float -> float" },
            { "math.exp", "x: float -> float" }, { "math.fabs", "x: float -> float" },
            { "math.hypot", "*coordinates: float -> float" },
            { "math.radians", "x: float -> float" }, { "math.degrees", "x: float -> float" },
            { "math.pow", "x: float, y: float -> float" },
            { "math.isclose", "a: float, b: float, *, rel_tol: float=1e-09, abs_tol: float=0.0 -> bool" },
            { "math.copysign", "x: float, y: float -> float" }, { "math.fmod", "x: float, y: float -> float" },
            { "math.remainder", "x: float, y: float -> float" },
            { "math.modf", "x: float -> (fractional: float, whole: float)" },
            { "math.frexp", "x: float -> (mantissa: float, exponent: int)" },
            { "math.ldexp", "x: float, i: int -> float" },
            { "math.cbrt", "x: float -> float" }, { "math.exp2", "x: float -> float" },
            { "math.expm1", "x: float -> float" }, { "math.log1p", "x: float -> float" },
            { "math.sinh", "x: float -> float" }, { "math.cosh", "x: float -> float" }, { "math.tanh", "x: float -> float" },
            { "math.asinh", "x: float -> float" }, { "math.acosh", "x: float -> float" }, { "math.atanh", "x: float -> float" },
            { "math.factorial", "n: int -> int" },
            { "math.gcd", "*integers: int -> int" }, { "math.lcm", "*integers: int -> int" },
            { "math.isqrt", "n: int -> int" }, { "math.comb", "n: int, k: int -> int" },
            { "math.perm", "n: int, k: int=None -> int" },
            { "math.prod", "iterable: list[any], *, start=1 -> any" },
            { "math.fsum", "iterable: list[float] -> float" },
            { "math.dist", "p: list[float], q: list[float] -> float" },

            // random module.
            { "random.seed", "a=None, version: int=2 -> none" }, { "random.random", "-> float" },
            { "random.getrandbits", "k: int -> int" },
            { "random.randrange", "start: int, stop: int=None, step: int=1 -> int" },
            { "random.randint", "a: int, b: int -> int" }, { "random.choice", "seq: list[any] -> any" },
            { "random.shuffle", "x: list[any] -> none" },
            { "random.sample", "population: list[any], k: int, *, counts=None -> list[any]" },
            { "random.choices", "population: list[any], weights=None, *, cum_weights=None, k: int=1 -> list[any]" },
            { "random.uniform", "a: float, b: float -> float" },
            { "random.triangular", "low: float=0.0, high: float=1.0, mode: float=None -> float" },
            { "random.normalvariate", "mu: float=0.0, sigma: float=1.0 -> float" },
            { "random.gauss", "mu: float=0.0, sigma: float=1.0 -> float" },
            { "random.lognormvariate", "mu: float, sigma: float -> float" },
            { "random.expovariate", "lambd: float=1.0 -> float" },
        };

        private static readonly Dictionary<string, HostSignature> Parsed = Build();

        private static Dictionary<string, HostSignature> Build()
        {
            var parsed = new Dictionary<string, HostSignature>();
            foreach (var kv in Source) parsed[kv.Key] = HostSignature.Parse(kv.Value);
            return parsed;
        }

        /// <summary>The keys that have a typed description, such as <c>len</c> or <c>math.sqrt</c>.</summary>
        public static IEnumerable<string> Keys => Parsed.Keys;

        /// <summary>The typed description of a built-in under <paramref name="key"/>, or null.</summary>
        public static HostSignature Find(string key) => Parsed.TryGetValue(key, out var s) ? s : null;

        /// <summary>
        /// The typed description of a native function: its own (<see cref="NativeFunction.Typed"/>), else the table
        /// entry under <paramref name="key"/>, else its parameter list with every pin <c>any</c> and no declared result.
        /// </summary>
        public static HostSignature For(NativeFunction fn, string key, int selfCount)
        {
            if (fn.Typed != null) return fn.Typed;
            return Find(key) ?? HostSignature.Parse(Signatures.For(fn, key, selfCount));
        }
    }
}
