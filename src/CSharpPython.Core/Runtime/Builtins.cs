using System;
using System.Numerics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CSharpPython
{
    /// <summary>The built-in functions, exception classes and standard modules every VM starts with.</summary>
    internal static class Builtins
    {
        /// <summary>Adds the built-in names to <paramref name="into"/> and the standard modules to <paramref name="modules"/>.</summary>
        public static void Install(Dictionary<string, Value> into, Dictionary<string, PyModule> modules)
        {
            foreach (var fn in Functions()) into[fn.Name] = Value.FromNative(fn);
            foreach (var type in ExceptionTypes.All) into[type.Name] = Value.FromExceptionType(type);
            modules["math"] = MathModule();
        }

        private static NativeFunction F(string name, int min, int max, NativeFn fn, bool keywords = false) =>
            new NativeFunction(name, fn, min, max, keywords);

        private static IEnumerable<NativeFunction> Functions()
        {
            yield return F("print", 0, -1, Print, true);
            yield return F("len", 1, 1, (c, a) => Value.FromInt(Ops.Length(a[0])));
            yield return F("range", 1, 3, Range);
            yield return F("int", 0, 2, ToInt);
            yield return F("float", 0, 1, ToFloat);
            yield return F("str", 0, 1, (c, a) => Value.FromString(a.Length == 0 ? "" : Ops.Str(a[0])));
            yield return F("bool", 0, 1, (c, a) => Value.FromBool(a.Length > 0 && a[0].IsTruthy));
            yield return F("repr", 1, 1, (c, a) => Value.FromString(Ops.Repr(a[0])));
            yield return F("abs", 1, 1, Abs);
            yield return F("min", 1, -1, (c, a) => MinMax(c, a, false), true);
            yield return F("max", 1, -1, (c, a) => MinMax(c, a, true), true);
            yield return F("sum", 1, 2, Sum);
            yield return F("round", 1, 2, Round);
            yield return F("sorted", 1, 1, Sorted, true);
            yield return F("list", 0, 1, (c, a) => Value.FromList(new PyList(a.Length == 0 ? new List<Value>() : Ops.ToList(a[0]))));
            yield return F("tuple", 0, 1, (c, a) => Value.FromTuple(new PyTuple(a.Length == 0 ? new Value[0] : Ops.ToList(a[0]).ToArray())));
            yield return F("dict", 0, 1, MakeDict, true);
            yield return F("enumerate", 1, 2, Enumerate, true);
            yield return F("zip", 0, -1, (c, a) =>
            {
                var sources = new PyIterator[a.Length];
                for (int i = 0; i < a.Length; i++) sources[i] = Ops.GetIterator(a[i]);
                return Value.FromIterator(new ZipIterator(sources));
            });
            yield return F("reversed", 1, 1, Reversed);
            yield return F("any", 1, 1, (c, a) => Value.FromBool(Ops.ToList(a[0]).Any(v => v.IsTruthy)));
            yield return F("all", 1, 1, (c, a) => Value.FromBool(Ops.ToList(a[0]).All(v => v.IsTruthy)));
            yield return F("chr", 1, 1, (c, a) =>
            {
                long n = a[0].AsInt();
                if (n < 0 || n > 0x10FFFF || (n >= 0xD800 && n <= 0xDFFF)) throw Ops.ValueError("chr() arg not in range(0x110000)");
                return Value.FromString(char.ConvertFromUtf32((int)n));
            });
            yield return F("ord", 1, 1, (c, a) =>
            {
                string s = a[0].Kind == ValueKind.Str ? a[0].AsString() : throw Ops.TypeError("ord() expected string of length 1, but " + a[0].TypeName + " found");
                if (s.Length == 1) return Value.FromInt(s[0]);
                if (s.Length == 2 && char.IsHighSurrogate(s[0]) && char.IsLowSurrogate(s[1])) return Value.FromInt(char.ConvertToUtf32(s[0], s[1]));
                throw Ops.TypeError("ord() expected a character, but string of length " + s.Length + " found");
            });
            yield return F("divmod", 2, 2, (c, a) => Value.Tuple(Ops.FloorDiv(a[0], a[1]), Ops.Mod(a[0], a[1])));
            yield return F("pow", 2, 3, (c, a) => a.Length == 2 ? Ops.Pow(a[0], a[1]) : ModPow(a[0], a[1], a[2]));
            yield return F("format", 1, 2, (c, a) => Value.FromString(Ops.FormatValue(a[0], a.Length > 1 ? a[1].AsString() : null)));
        }

        private static Value Print(CallContext ctx, ReadOnlySpan<Value> a)
        {
            string sep = " ", end = "\n";
            foreach (var name in ctx.KeywordNames)
                if (name != "sep" && name != "end" && name != "flush") throw Ops.TypeError("print() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "sep", out Value s) && !s.IsNone) sep = s.AsString();
            if (ctx.TryGetKeyword(a, "end", out Value e) && !e.IsNone) end = e.AsString();

            var sb = new StringBuilder();
            for (int i = 0; i < ctx.PositionalCount; i++)
            {
                if (i > 0) sb.Append(sep);
                sb.Append(Ops.Str(a[i]));
            }
            sb.Append(end);
            ctx.Write(sb.ToString());
            return Value.None;
        }

        private static Value Range(CallContext ctx, ReadOnlySpan<Value> a)
        {
            long start = 0, stop, step = 1;
            if (a.Length == 1) stop = RangeArg(a[0]);
            else
            {
                start = RangeArg(a[0]);
                stop = RangeArg(a[1]);
                if (a.Length == 3) step = RangeArg(a[2]);
            }
            if (step == 0) throw Ops.ValueError("range() arg 3 must not be zero");
            return Value.FromRange(new PyRange(start, stop, step));
        }

        private static long RangeArg(Value v)
        {
            if (v.IsIntLike()) return v.RawInt;
            throw Ops.TypeError("'" + v.TypeName + "' object cannot be interpreted as an integer");
        }

        private static Value ToInt(CallContext ctx, ReadOnlySpan<Value> a)
        {
            if (a.Length == 0) return Value.FromInt(0);
            Value v = a[0];
            if (a.Length == 2)
            {
                if (v.Kind != ValueKind.Str) throw Ops.TypeError("int() can't convert non-string with explicit base");
                return ParseInt(v.AsString(), (int)a[1].AsInt());
            }
            switch (v.Kind)
            {
                case ValueKind.Int: return v;
                case ValueKind.Bool: return Value.FromInt(v.RawInt);
                case ValueKind.Float:
                {
                    double d = v.RawFloat;
                    if (double.IsNaN(d)) throw Ops.ValueError("cannot convert float NaN to integer");
                    if (double.IsInfinity(d)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
                    d = Math.Truncate(d);
                    if (d >= 9.2233720368547758e18 || d < -9.2233720368547758e18) throw Ops.OverflowError();
                    return Value.FromInt((long)d);
                }
                case ValueKind.Str: return ParseInt(v.AsString(), 10);
                default:
                    throw Ops.TypeError("int() argument must be a string or a number, not '" + v.TypeName + "'");
            }
        }

        private static Value ParseInt(string text, int radix)
        {
            string original = text;
            if (radix != 0 && (radix < 2 || radix > 36)) throw Ops.ValueError("int() base must be >= 2 and <= 36, or 0");
            text = text.Trim().Replace("_", "");
            bool negative = false;
            if (text.StartsWith("-", StringComparison.Ordinal)) { negative = true; text = text.Substring(1); }
            else if (text.StartsWith("+", StringComparison.Ordinal)) text = text.Substring(1);
            int shownRadix = radix;
            if (radix == 0)
            {
                // Base 0 picks the base from the prefix, like a source literal.
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) radix = 16;
                else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) radix = 8;
                else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) radix = 2;
                else radix = 10;
            }
            if (radix == 16 && (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))) text = text.Substring(2);
            else if (radix == 8 && text.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            else if (radix == 2 && text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);

            Exception bad = Ops.ValueError("invalid literal for int() with base " + shownRadix + ": " + Ops.Repr(Value.FromString(original)));
            if (text.Length == 0) throw bad;
            long result = 0;
            foreach (char ch in text)
            {
                int d = ch >= '0' && ch <= '9' ? ch - '0' : ch >= 'a' && ch <= 'z' ? ch - 'a' + 10 : ch >= 'A' && ch <= 'Z' ? ch - 'A' + 10 : -1;
                if (d < 0 || d >= radix) throw bad;
                try { result = checked(result * radix + (negative ? -d : d)); }
                catch (OverflowException) { throw Ops.OverflowError(); }
            }
            return Value.FromInt(result);
        }

        private static Value ToFloat(CallContext ctx, ReadOnlySpan<Value> a)
        {
            if (a.Length == 0) return Value.FromFloat(0.0);
            Value v = a[0];
            if (v.IsNumber) return Value.FromFloat(v.AsFloat());
            if (v.Kind != ValueKind.Str) throw Ops.TypeError("float() argument must be a string or a number, not '" + v.TypeName + "'");
            string s = v.AsString().Trim().Replace("_", "");
            switch (s.ToLowerInvariant())
            {
                case "inf": case "+inf": case "infinity": case "+infinity": return Value.FromFloat(double.PositiveInfinity);
                case "-inf": case "-infinity": return Value.FromFloat(double.NegativeInfinity);
                case "nan": case "+nan": case "-nan": return Value.FromFloat(double.NaN);
            }
            bool plain = s.Length > 0 && s.All(ch => char.IsDigit(ch) || ch == '.' || ch == '-' || ch == '+' || ch == 'e' || ch == 'E');
            if (plain && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return Value.FromFloat(d);
            throw Ops.ValueError("could not convert string to float: " + Ops.Repr(v));
        }

        private static Value Abs(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value v = a[0];
            if (v.IsIntLike())
            {
                if (v.RawInt == long.MinValue) throw Ops.OverflowError();
                return Value.FromInt(Math.Abs(v.RawInt));
            }
            if (v.Kind == ValueKind.Float) return Value.FromFloat(Math.Abs(v.RawFloat));
            throw Ops.TypeError("bad operand type for abs(): '" + v.TypeName + "'");
        }

        private static Value MinMax(CallContext ctx, ReadOnlySpan<Value> a, bool max)
        {
            string name = max ? "max" : "min";
            foreach (var kw in ctx.KeywordNames)
                if (kw != "key" && kw != "default") throw Ops.TypeError(name + "() got an unexpected keyword argument '" + kw + "'");
            bool hasDefault = ctx.TryGetKeyword(a, "default", out Value fallback);
            ctx.TryGetKeyword(a, "key", out Value key);
            if (ctx.PositionalCount > 1 && hasDefault)
                throw Ops.TypeError("Cannot specify a default for " + name + "() with multiple positional arguments");

            List<Value> items = ctx.PositionalCount == 1 ? Ops.ToList(a[0]) : a.Slice(0, ctx.PositionalCount).ToArray().ToList();
            if (items.Count == 0)
            {
                if (hasDefault) return fallback;
                throw Ops.ValueError(name + "() arg is an empty sequence");
            }
            Value[] keys = Methods.KeysFor(ctx, key, items);
            int best = 0;
            for (int i = 1; i < items.Count; i++)
            {
                Value candidate = keys == null ? items[i] : keys[i];
                Value current = keys == null ? items[best] : keys[best];
                if (Ops.Compare(candidate, current, max ? Ops.Order.Gt : Ops.Order.Lt)) best = i;
            }
            return items[best];
        }

        private static Value Sum(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value total = a.Length > 1 ? a[1] : Value.FromInt(0);
            foreach (var item in Ops.ToList(a[0])) total = Ops.Add(total, item);
            return total;
        }

        private static Value Round(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value v = a[0];
            bool hasDigits = a.Length > 1 && !a[1].IsNone;
            if (v.IsIntLike())
            {
                if (hasDigits && a[1].AsInt() < 0) return RoundInt(v.RawInt, a[1].AsInt());
                if (hasDigits) a[1].AsInt();
                return Value.FromInt(v.RawInt);
            }
            if (v.Kind != ValueKind.Float) throw Ops.TypeError("type " + v.TypeName + " doesn't define __round__ method");
            double d = v.RawFloat;
            if (!hasDigits)
            {
                if (double.IsNaN(d)) throw Ops.ValueError("cannot convert float NaN to integer");
                if (double.IsInfinity(d)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
                double r = Math.Round(d, MidpointRounding.ToEven);
                if (r >= 9.2233720368547758e18 || r < -9.2233720368547758e18) throw Ops.OverflowError();
                return Value.FromInt((long)r);
            }
            long digits = a[1].AsInt();
            if (double.IsNaN(d) || double.IsInfinity(d) || digits > 330) return v;
            if (digits < -330) return Value.FromFloat(d < 0 ? -0.0 : 0.0);
            return Value.FromFloat(RoundExact(d, (int)digits));
        }

        // Rounds half to even using the exact binary value of the double, like CPython (so round(2.675, 2) is 2.67).
        private static double RoundExact(double d, int digits)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exponent == 0) exponent++;
            else mantissa |= 1L << 52;
            exponent -= 1075;

            BigInteger num = mantissa, den = BigInteger.One;
            if (exponent > 0) num <<= exponent;
            else den <<= -exponent;
            if (digits >= 0) num *= BigInteger.Pow(10, digits);
            else den *= BigInteger.Pow(10, -digits);

            BigInteger quotient = BigInteger.DivRem(num, den, out BigInteger remainder);
            int cmp = (remainder * 2).CompareTo(den);
            if (cmp > 0 || (cmp == 0 && !quotient.IsEven)) quotient += 1;

            string text = quotient.ToString(CultureInfo.InvariantCulture) + "e" + (-digits).ToString(CultureInfo.InvariantCulture);
            double result;
            try { result = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture); }
            catch (OverflowException) { throw new PyError("OverflowError", "rounded value too large to represent"); }
            if (double.IsInfinity(result)) throw new PyError("OverflowError", "rounded value too large to represent");
            return negative ? -result : result;
        }

        private static Value RoundInt(long n, long digits)
        {
            if (digits < -19) return Value.FromInt(0);
            BigInteger p = BigInteger.Pow(10, (int)-digits);
            BigInteger q = BigInteger.DivRem(BigInteger.Abs(n), p, out BigInteger r);
            int cmp = (r * 2).CompareTo(p);
            if (cmp > 0 || (cmp == 0 && !q.IsEven)) q += 1;
            BigInteger result = q * p;
            if (n < 0) result = -result;
            if (result > long.MaxValue || result < long.MinValue) throw Ops.OverflowError();
            return Value.FromInt((long)result);
        }

        private static Value ModPow(Value b, Value e, Value m)
        {
            if (!b.IsIntLike() || !e.IsIntLike() || !m.IsIntLike())
                throw Ops.TypeError("pow() 3rd argument not allowed unless all arguments are integers");
            if (e.RawInt < 0) throw Ops.ValueError("pow() 2nd argument cannot be negative when 3rd argument specified");
            if (m.RawInt == 0) throw Ops.ValueError("pow() 3rd argument cannot be 0");
            BigInteger mod = BigInteger.Abs(m.RawInt);
            BigInteger r = BigInteger.ModPow(b.RawInt, e.RawInt, mod);
            if (r.Sign < 0) r += mod;
            if (m.RawInt < 0 && !r.IsZero) r -= mod;
            return Value.FromInt((long)r);
        }

        private static Value Sorted(CallContext ctx, ReadOnlySpan<Value> a)
        {
            bool reverse = false;
            foreach (var name in ctx.KeywordNames)
                if (name != "reverse" && name != "key") throw Ops.TypeError("sorted() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "reverse", out Value r)) reverse = r.IsTruthy;
            ctx.TryGetKeyword(a, "key", out Value key);
            var items = Ops.ToList(a[0]);
            return Value.FromList(new PyList(Methods.SortValues(items, reverse, Methods.KeysFor(ctx, key, items))));
        }

        private static Value MakeDict(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var dict = new PyDict();
            if (ctx.PositionalCount > 0) Methods.UpdateDict(dict, a[0]);
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
                dict.Set(Value.FromString(ctx.KeywordNames[i]), a[ctx.PositionalCount + i]);
            return Value.FromDict(dict);
        }

        private static Value Enumerate(CallContext ctx, ReadOnlySpan<Value> a)
        {
            long start = ctx.PositionalCount > 1 ? a[1].AsInt() : 0;
            foreach (var name in ctx.KeywordNames)
                if (name != "start") throw Ops.TypeError("enumerate() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "start", out Value s)) start = s.AsInt();
            return Value.FromIterator(new EnumerateIterator(Ops.GetIterator(a[0]), start));
        }

        private static Value Reversed(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value v = a[0];
            if (v.Kind == ValueKind.Iterator || v.Kind == ValueKind.Int || v.Kind == ValueKind.Float || v.Kind == ValueKind.Bool || v.Kind == ValueKind.None)
                throw Ops.TypeError("'" + v.TypeName + "' object is not reversible");
            var items = Ops.ToList(v);
            items.Reverse();
            return Value.FromIterator(new ListIterator(new PyList(items)));
        }

        // ---- math -----------------------------------------------------------------------------------------------

        private static PyModule MathModule()
        {
            var m = new PyModule("math");
            void Fn(string name, int args, Func<double[], double> f)
            {
                m.Members[name] = Value.FromNative(F(name, args, args, (c, a) =>
                {
                    var xs = new double[a.Length];
                    for (int i = 0; i < a.Length; i++) xs[i] = a[i].AsFloat();
                    return Value.FromFloat(f(xs));
                }));
            }
            Value Int(string name, Func<double, double> f, ReadOnlySpan<Value> a)
            {
                if (a[0].IsIntLike()) return Value.FromInt(a[0].RawInt);
                double d = a[0].AsFloat();
                if (double.IsNaN(d)) throw Ops.ValueError("cannot convert float NaN to integer");
                if (double.IsInfinity(d)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
                double r = f(d);
                if (r >= 9.2233720368547758e18 || r < -9.2233720368547758e18) throw Ops.OverflowError();
                return Value.FromInt((long)r);
            }

            m.Members["pi"] = Value.FromFloat(Math.PI);
            m.Members["e"] = Value.FromFloat(Math.E);
            m.Members["tau"] = Value.FromFloat(2 * Math.PI);
            m.Members["inf"] = Value.FromFloat(double.PositiveInfinity);
            m.Members["nan"] = Value.FromFloat(double.NaN);

            m.Members["sqrt"] = Value.FromNative(F("sqrt", 1, 1, (c, a) =>
            {
                double d = a[0].AsFloat();
                if (d < 0) throw Ops.ValueError("math domain error");
                return Value.FromFloat(Math.Sqrt(d));
            }));
            m.Members["log"] = Value.FromNative(F("log", 1, 2, (c, a) =>
            {
                double x = a[0].AsFloat();
                if (x <= 0) throw Ops.ValueError("math domain error");
                if (a.Length == 1) return Value.FromFloat(Math.Log(x));
                double b = a[1].AsFloat();
                if (b == 1) throw new PyError("ZeroDivisionError", "float division by zero");
                if (b <= 0) throw Ops.ValueError("math domain error");
                return Value.FromFloat(Math.Log(x) / Math.Log(b));
            }));
            m.Members["log2"] = Value.FromNative(F("log2", 1, 1, (c, a) =>
            {
                double x = a[0].AsFloat();
                if (x <= 0) throw Ops.ValueError("math domain error");
                return Value.FromFloat(Math.Log(x, 2));
            }));
            m.Members["log10"] = Value.FromNative(F("log10", 1, 1, (c, a) =>
            {
                double x = a[0].AsFloat();
                if (x <= 0) throw Ops.ValueError("math domain error");
                return Value.FromFloat(Math.Log10(x));
            }));
            m.Members["floor"] = Value.FromNative(F("floor", 1, 1, (c, a) => Int("floor", Math.Floor, a)));
            m.Members["ceil"] = Value.FromNative(F("ceil", 1, 1, (c, a) => Int("ceil", Math.Ceiling, a)));
            m.Members["trunc"] = Value.FromNative(F("trunc", 1, 1, (c, a) => Int("trunc", Math.Truncate, a)));
            m.Members["isnan"] = Value.FromNative(F("isnan", 1, 1, (c, a) => Value.FromBool(double.IsNaN(a[0].AsFloat()))));
            m.Members["isinf"] = Value.FromNative(F("isinf", 1, 1, (c, a) => Value.FromBool(double.IsInfinity(a[0].AsFloat()))));
            double Finite(double x) => double.IsInfinity(x) ? throw Ops.ValueError("math domain error") : x;
            Fn("sin", 1, x => Math.Sin(Finite(x[0])));
            Fn("cos", 1, x => Math.Cos(Finite(x[0])));
            Fn("tan", 1, x => Math.Tan(Finite(x[0])));
            Fn("asin", 1, x => Math.Abs(x[0]) > 1 ? throw Ops.ValueError("math domain error") : Math.Asin(x[0]));
            Fn("acos", 1, x => Math.Abs(x[0]) > 1 ? throw Ops.ValueError("math domain error") : Math.Acos(x[0]));
            Fn("atan", 1, x => Math.Atan(x[0]));
            Fn("atan2", 2, x => Math.Atan2(x[0], x[1]));
            Fn("exp", 1, x => double.IsInfinity(Math.Exp(x[0])) && !double.IsInfinity(x[0]) ? throw new PyError("OverflowError", "math range error") : Math.Exp(x[0]));
            Fn("fabs", 1, x => Math.Abs(x[0]));
            Fn("hypot", 2, x => Math.Sqrt(x[0] * x[0] + x[1] * x[1]));
            Fn("radians", 1, x => x[0] * Math.PI / 180.0);
            Fn("degrees", 1, x => x[0] * 180.0 / Math.PI);
            Fn("pow", 2, x =>
            {
                if (x[0] == 0.0 && x[1] < 0) throw Ops.ValueError("math domain error");
                double p = Math.Pow(x[0], x[1]);
                bool finiteInputs = !double.IsInfinity(x[0]) && !double.IsInfinity(x[1]) && !double.IsNaN(x[0]) && !double.IsNaN(x[1]);
                if (double.IsNaN(p) && finiteInputs) throw Ops.ValueError("math domain error");
                if (double.IsInfinity(p) && finiteInputs) throw new PyError("OverflowError", "math range error");
                return p;
            });
            return m;
        }
    }
}
