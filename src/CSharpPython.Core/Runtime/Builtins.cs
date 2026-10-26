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
            foreach (var fn in Functions())
            {
                // The names of the built-in types hold the type, which runs the function when called.
                if (PyTypes.TryGet(fn.Name, out var type))
                {
                    type.Constructor = fn;
                    into[fn.Name] = Value.FromType(type);
                }
                else
                {
                    into[fn.Name] = Value.FromNative(fn);
                }
            }
            foreach (var type in ExceptionTypes.All) into[type.Name] = Value.FromExceptionType(type);
            into["object"] = Value.FromType(PyTypes.Object);

            // Names no script can write, used by the code a 'with' statement is rewritten into.
            into[".type"] = into["type"];
            into[".iter"] = into["iter"];
            into[".Exception"] = Value.FromExceptionType(ExceptionTypes.Exception);
            into["NotImplemented"] = Value.NotImplemented;
            into["Ellipsis"] = Value.Ellipsis;
            modules["math"] = MathModule();
            modules["random"] = RandomModule.Create();
            modules["collections"] = CollectionsModule.Create();
        }

        private static string NameArg(Value v, string function)
        {
            if (v.Kind != ValueKind.Str) throw new PyError("TypeError", function + "(): attribute name must be string");
            return (string)v.RawObject;
        }

        private static Value GetAttrFn(CallContext c, ReadOnlySpan<Value> a)
        {
            string name = NameArg(a[1], "getattr");
            if (a.Length < 3) return Ops.GetAttr(a[0], name);
            try
            {
                return Ops.GetAttr(a[0], name);
            }
            catch (Exception e) when (PyTask.IsAttributeError(e))
            {
                return a[2];
            }
        }

        private static NativeFunction F(string name, int min, int max, NativeFn fn, bool keywords = false) =>
            new NativeFunction(name, fn, min, max, keywords);

        private static IEnumerable<NativeFunction> Functions()
        {
            yield return F("print", 0, -1, Print, true);
            yield return F(".with", 1, 1, ContextManager);
            yield return F("iter", 1, 1, (c, a) => Value.FromIterator(Ops.GetIterator(a[0])));
            var next = F("next", 1, 2, (c, a) =>
            {
                var it = a[0].Kind == ValueKind.Iterator ? (PyIterator)a[0].RawObject
                    : a[0].Kind == ValueKind.Instance && c.Task != null && Ops.HasNext(a[0]) ? new InstanceIterator(a[0])
                    : throw Ops.TypeError("'" + a[0].TypeName + "' object is not an iterator");
                if (it.Next(out Value item)) return item;
                if (a.Length > 1) return a[1];
                throw new PyError("StopIteration", "");
            });
            next.StepKind = 1;
            yield return next;
            yield return F("hash", 1, 1, (c, a) => Value.FromInt(Ops.Hash(a[0])));
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
            yield return F("set", 0, 1, (c, a) => a.Length == 0 ? Value.FromSet(new PySet()) : Value.FromSet(Methods.ToSet(a[0]).Copy()));
            yield return F("staticmethod", 1, 1, (c, a) =>
            {
                if (!PyTypes.IsCallable(a[0])) throw new PyError("TypeError", "staticmethod() argument must be callable");
                return Value.FromDescriptor(new PyDescriptor(DescriptorKind.StaticMethod, a[0]));
            });
            yield return F("classmethod", 1, 1, (c, a) =>
            {
                if (a[0].Kind != ValueKind.Function) throw new PyError("TypeError", "classmethod() argument must be a script function");
                return Value.FromDescriptor(new PyDescriptor(DescriptorKind.ClassMethod, a[0]));
            });
            yield return F("property", 0, 3, (c, a) =>
            {
                for (int i = 0; i < a.Length; i++)
                    if (a[i].Kind != ValueKind.None && !PyTypes.IsCallable(a[i]))
                        throw new PyError("TypeError", "property() arguments must be callable or None");
                return Value.FromDescriptor(new PyDescriptor(DescriptorKind.Property, default,
                    a.Length > 0 ? a[0] : default, a.Length > 1 ? a[1] : default, a.Length > 2 ? a[2] : default));
            });
            yield return F("frozenset", 0, 1, (c, a) =>
            {
                var set = a.Length == 0 ? new PySet() : Methods.ToSet(a[0]).Copy();
                set.Frozen = true;
                return Value.FromSet(set);
            });
            yield return F("slice", 1, 3, (c, a) => a.Length == 1
                ? Ops.MakeSlice(Value.None, a[0], Value.None)
                : Ops.MakeSlice(a[0], a[1], a.Length > 2 ? a[2] : Value.None));
            yield return F("dict", 0, 1, MakeDict, true);
            yield return F("enumerate", 1, 2, Enumerate, true);
            yield return F("zip", 0, -1, (c, a) =>
            {
                var sources = new PyIterator[a.Length];
                for (int i = 0; i < a.Length; i++) sources[i] = Ops.GetIterator(a[i]);
                return Value.FromIterator(new ZipIterator(sources));
            });
            yield return F("map", 2, -1, (c, a) =>
            {
                var sources = new PyIterator[a.Length - 1];
                for (int i = 1; i < a.Length; i++) sources[i - 1] = Ops.GetIterator(a[i]);
                return Value.FromIterator(new MapIterator(a[0], sources));
            });
            yield return F("filter", 2, 2, (c, a) => Value.FromIterator(new FilterIterator(a[0], Ops.GetIterator(a[1]))));
            yield return F("bin", 1, 1, (c, a) => Value.FromString(Radix(a[0], 2, "0b", "bin")));
            yield return F("oct", 1, 1, (c, a) => Value.FromString(Radix(a[0], 8, "0o", "oct")));
            yield return F("hex", 1, 1, (c, a) => Value.FromString(Radix(a[0], 16, "0x", "hex")));
            yield return F("id", 1, 1, (c, a) => Value.FromInt(a[0].RawObject == null ? Ops.Hash(a[0]) : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a[0].RawObject)));
            yield return F("delattr", 2, 2, (c, a) =>
            {
                Ops.DelAttr(a[0], NameArg(a[1], "delattr"));
                return Value.None;
            });
            yield return F("dir", 1, 1, (c, a) => Dir(a[0]));
            yield return F("vars", 1, 1, (c, a) => Vars(a[0]));
            yield return F("ascii", 1, 1, (c, a) => Value.FromString(Ascii(Ops.Repr(a[0]))));
            yield return F("reversed", 1, 1, Reversed);
            yield return F("any", 1, 1, (c, a) => Value.FromBool(AnyAll(c, a[0], true)));
            yield return F("all", 1, 1, (c, a) => Value.FromBool(!AnyAll(c, a[0], false)));
            yield return F("chr", 1, 1, (c, a) =>
            {
                if (a[0].Kind == ValueKind.BigInt) throw Ops.ValueError("chr() arg not in range(0x110000)");
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
            yield return F("type", 1, 1, (c, a) => PyTypes.TypeOf(a[0]));
            yield return F("isinstance", 2, 2, (c, a) => Value.FromBool(PyTypes.IsInstance(a[0], a[1])));
            yield return F("issubclass", 2, 2, (c, a) => Value.FromBool(PyTypes.IsSubclass(a[0], a[1])));
            yield return F("getattr", 2, 3, GetAttrFn);
            yield return F("setattr", 3, 3, (c, a) =>
            {
                Ops.SetAttr(a[0], NameArg(a[1], "setattr"), a[2]);
                return Value.None;
            });
            yield return F("hasattr", 2, 2, (c, a) =>
            {
                try
                {
                    Ops.GetAttr(a[0], NameArg(a[1], "hasattr"));
                    return Value.True;
                }
                catch (Exception e) when (PyTask.IsAttributeError(e))
                {
                    return Value.False;
                }
            });
            yield return F("callable", 1, 1, (c, a) => Value.FromBool(PyTypes.IsCallable(a[0])));
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
            v = Ops.IndexValue(v);
            if (v.IsIntLike()) return v.RawInt;
            if (v.Kind == ValueKind.BigInt) throw Ops.IndexTooLarge();
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
                case ValueKind.Int:
                case ValueKind.BigInt: return v;
                case ValueKind.Bool: return Value.FromInt(v.RawInt);
                case ValueKind.Float: return Ops.FloatToInt(v.RawFloat);
                case ValueKind.Str: return ParseInt(v.AsString(), 10);
                case ValueKind.Instance:
                    if (Ops.TryCallDunder(v, "__int__", Array.Empty<Value>(), out Value converted))
                    {
                        if (converted.Kind != ValueKind.Int && converted.Kind != ValueKind.BigInt) throw Ops.TypeError("__int__ returned non-int (type " + converted.TypeName + ")");
                        return converted;
                    }
                    Value index = Ops.IndexValue(v);
                    if (index.Kind == ValueKind.BigInt) return index;
                    if (index.Kind != ValueKind.Instance) return Value.FromInt(index.RawInt);
                    throw Ops.TypeError("int() argument must be a string or a number, not '" + v.TypeName + "'");
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
                if (text.Length <= 12) result = result * radix + d;
            }
            if (text.Length <= 12) return Value.FromInt(negative ? -result : result);
            return Ops.ParseDigits(text, radix, negative);
        }

        private static Value ToFloat(CallContext ctx, ReadOnlySpan<Value> a)
        {
            if (a.Length == 0) return Value.FromFloat(0.0);
            Value v = a[0];
            if (v.IsNumber) return Value.FromFloat(v.AsFloat());
            if (v.Kind == ValueKind.Instance && Ops.TryCallDunder(v, "__float__", Array.Empty<Value>(), out Value converted))
            {
                if (converted.Kind != ValueKind.Float) throw Ops.TypeError(v.TypeName + ".__float__ returned non-float (type " + converted.TypeName + ")");
                return converted;
            }
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
            if (v.Kind == ValueKind.BigInt) return Ops.BigResult(BigInteger.Abs((BigInteger)v.RawObject));
            if (v.IsIntLike())
            {
                if (v.RawInt == long.MinValue) return Ops.BigResult(-new BigInteger(v.RawInt));
                return Value.FromInt(Math.Abs(v.RawInt));
            }
            if (v.Kind == ValueKind.Float) return Value.FromFloat(Math.Abs(v.RawFloat));
            if (v.Kind == ValueKind.Instance && Ops.TryCallDunder(v, "__abs__", Array.Empty<Value>(), out Value result)) return result;
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

            int count = 0;
            PyIterator source = ctx.PositionalCount == 1 ? Ops.GetIterator(a[0]) : new ListIterator(new PyList(a.Slice(0, ctx.PositionalCount).ToArray().ToList()));
            if (!source.Next(out Value best))
            {
                if (hasDefault) return fallback;
                throw Ops.ValueError(name + "() arg is an empty sequence");
            }
            Value bestKey = key.IsNone ? best : ctx.Call(key, best);
            while (source.Next(out Value item))
            {
                Tick(ctx, ref count);
                Value itemKey = key.IsNone ? item : ctx.Call(key, item);
                bool better = itemKey.Kind == ValueKind.Int && bestKey.Kind == ValueKind.Int
                    ? (max ? itemKey.RawInt > bestKey.RawInt : itemKey.RawInt < bestKey.RawInt)
                    : Ops.Compare(itemKey, bestKey, max ? Ops.Order.Gt : Ops.Order.Lt);
                if (better) { best = item; bestKey = itemKey; }
            }
            ctx.Charge(count);
            return best;
        }

        // Counts work done by a built-in that walks a whole iterable, a batch at a time (see CallContext.Charge).
        private static void Tick(CallContext ctx, ref int count)
        {
            if (++count < 1024) return;
            ctx.Charge(count);
            count = 0;
        }

        // True when an item has the truth value 'seen'; stops at the first one, like CPython (so an endless generator is fine).
        private static bool AnyAll(CallContext ctx, Value iterable, bool seen)
        {
            var it = Ops.GetIterator(iterable);
            int count = 0;
            while (it.Next(out Value item))
            {
                if (item.IsTruthy == seen)
                {
                    ctx.Charge(count);
                    return true;
                }
                Tick(ctx, ref count);
            }
            ctx.Charge(count);
            return false;
        }

        private static Value Sum(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value total = a.Length > 1 ? a[1] : Value.FromInt(0);
            var it = Ops.GetIterator(a[0]);
            int count = 0;
            bool more = it.Next(out Value item);
            // Like CPython 3.12+, floats are added with compensation, so sum([0.1, 0.2, 0.3]) is 0.6.
            if (total.Kind == ValueKind.Float || more && item.Kind == ValueKind.Float)
            {
                if (total.IsInteger && total.Kind != ValueKind.BigInt || total.Kind == ValueKind.Float)
                {
                    double sum = total.AsFloat(), compensation = 0;
                    while (more && (item.Kind == ValueKind.Float || item.IsInteger && item.Kind != ValueKind.BigInt))
                    {
                        double x = item.AsFloat();
                        double t = sum + x;
                        if (double.IsInfinity(t) || double.IsNaN(t))
                        {
                            sum = t;
                            compensation = 0;
                        }
                        else
                        {
                            if (Math.Abs(sum) >= Math.Abs(x)) compensation += (sum - t) + x;
                            else compensation += (x - t) + sum;
                            sum = t;
                        }
                        more = it.Next(out item);
                        Tick(ctx, ref count);
                    }
                    if (compensation != 0 && !double.IsInfinity(sum) && !double.IsNaN(sum)) sum += compensation;
                    total = Value.FromFloat(sum);
                }
            }
            while (more)
            {
                long x = total.RawInt, y = item.RawInt, r = unchecked(x + y);
                if (total.Kind == ValueKind.Int && item.Kind == ValueKind.Int && ((x ^ r) & (y ^ r)) >= 0) total = Value.FromInt(r);
                else total = Ops.Add(total, item);
                more = it.Next(out item);
                Tick(ctx, ref count);
            }
            ctx.Charge(count);
            return total;
        }

        private static Value Round(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value v = a[0];
            bool hasDigits = a.Length > 1 && !a[1].IsNone;
            if (v.Kind == ValueKind.Instance)
            {
                var args = a.Length > 1 && !a[1].IsNone ? new[] { a[1] } : Array.Empty<Value>();
                if (Ops.TryCallDunder(v, "__round__", args, out Value rounded)) return rounded;
                throw Ops.TypeError("type " + v.TypeName + " doesn't define __round__ method");
            }
            if (v.IsInteger)
            {
                if (hasDigits && a[1].AsInt() < 0) return RoundInt(v, a[1].AsInt());
                if (hasDigits) a[1].AsInt();
                return v.Kind == ValueKind.Bool ? Value.FromInt(v.RawInt) : v;
            }
            if (v.Kind != ValueKind.Float) throw Ops.TypeError("type " + v.TypeName + " doesn't define __round__ method");
            double d = v.RawFloat;
            if (!hasDigits)
            {
                return Ops.FloatToInt(Math.Round(d, MidpointRounding.ToEven));
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

        private static Value RoundInt(Value value, long digits)
        {
            BigInteger n = Ops.ToBig(value);
            if (-digits > Ops.BitLength(n) / 3 + 2) return Value.FromInt(0); // 10 ** -digits is far above |n|
            BigInteger p = BigInteger.Pow(10, (int)-digits);
            BigInteger q = BigInteger.DivRem(BigInteger.Abs(n), p, out BigInteger r);
            int cmp = (r * 2).CompareTo(p);
            if (cmp > 0 || (cmp == 0 && !q.IsEven)) q += 1;
            BigInteger result = q * p;
            if (n.Sign < 0) result = -result;
            return Ops.BigResult(result);
        }

        private static Value ModPow(Value b, Value e, Value m)
        {
            if (!b.IsInteger || !e.IsInteger || !m.IsInteger)
                throw Ops.TypeError("pow() 3rd argument not allowed unless all arguments are integers");
            BigInteger exponent = Ops.ToBig(e), modulus = Ops.ToBig(m);
            if (exponent.Sign < 0) throw Ops.ValueError("pow() 2nd argument cannot be negative when 3rd argument specified");
            if (modulus.IsZero) throw Ops.ValueError("pow() 3rd argument cannot be 0");
            BigInteger mod = BigInteger.Abs(modulus);
            BigInteger r = BigInteger.ModPow(Ops.ToBig(b), exponent, mod);
            if (r.Sign < 0) r += mod;
            if (modulus.Sign < 0 && !r.IsZero) r -= mod;
            return Ops.BigResult(r);
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

        // The attribute names of a value, sorted. Unlike CPython's, the list leaves out the methods every object has.
        private static Value Dir(Value v)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            switch (v.Kind)
            {
                case ValueKind.Instance:
                {
                    var inst = (PyInstance)v.RawObject;
                    foreach (var kv in inst.Fields.Pairs()) if (kv.Key.Kind == ValueKind.Str) names.Add((string)kv.Key.RawObject);
                    for (var cls = inst.Class; cls != null; cls = cls.Base) AddNames(names, cls.Namespace);
                    break;
                }
                case ValueKind.Class:
                    for (var cls = (PyClass)v.RawObject; cls != null; cls = cls.Base) AddNames(names, cls.Namespace);
                    break;
                case ValueKind.Module:
                    foreach (var name in ((PyModule)v.RawObject).Members.Keys) names.Add(name);
                    break;
                case ValueKind.Exception:
                    names.Add("args");
                    break;
                case ValueKind.Slice:
                    names.Add("start");
                    names.Add("stop");
                    names.Add("step");
                    break;
                default:
                    foreach (var name in Methods.NamesOf(v.Kind))
                        if (Methods.FindFor(v, name) != null && name[0] != '.') names.Add(name);
                    break;
            }
            var items = new List<Value>();
            foreach (var name in names) items.Add(Value.FromString(name));
            return Value.FromList(new PyList(items));
        }

        private static void AddNames(SortedSet<string> names, PyDict namespaceDict)
        {
            foreach (var kv in namespaceDict.Pairs())
                if (kv.Key.Kind == ValueKind.Str && ((string)kv.Key.RawObject).Length > 0 && ((string)kv.Key.RawObject)[0] != '.') names.Add((string)kv.Key.RawObject);
        }

        private static Value Vars(Value v)
        {
            var copy = new PyDict();
            switch (v.Kind)
            {
                case ValueKind.Instance:
                    foreach (var kv in ((PyInstance)v.RawObject).Fields.Pairs()) copy.Set(kv.Key, kv.Value);
                    break;
                case ValueKind.Class:
                    foreach (var kv in ((PyClass)v.RawObject).Namespace.Pairs())
                        if (kv.Key.Kind != ValueKind.Str || ((string)kv.Key.RawObject).Length == 0 || ((string)kv.Key.RawObject)[0] != '.') copy.Set(kv.Key, kv.Value);
                    break;
                case ValueKind.Module:
                    foreach (var kv in ((PyModule)v.RawObject).Members) copy.Set(Value.FromString(kv.Key), kv.Value);
                    break;
                default:
                    throw Ops.TypeError("vars() argument must have __dict__ attribute");
            }
            return Value.FromDict(copy);
        }

        private static string Radix(Value v, int radix, string prefix, string function)
        {
            v = Ops.IndexValue(v);
            if (!v.IsInteger)
                throw Ops.TypeError("'" + v.TypeName + "' object cannot be interpreted as an integer");
            BigInteger n = Ops.ToBig(v);
            return (n.Sign < 0 ? "-" : "") + prefix + Ops.BigDigits(BigInteger.Abs(n), radix);
        }

        internal static string Ascii(string text)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
                if (cp > 0xFFFF) i++;
                if (cp < 128) sb.Append((char)cp);
                else if (cp < 0x100) sb.Append("\\x").Append(cp.ToString("x2"));
                else if (cp < 0x10000) sb.Append("\\u").Append(cp.ToString("x4"));
                else sb.Append("\\U").Append(cp.ToString("x8"));
            }
            return sb.ToString();
        }

        private static Value Enumerate(CallContext ctx, ReadOnlySpan<Value> a)
        {
            long start = ctx.PositionalCount > 1 ? a[1].AsInt() : 0;
            foreach (var name in ctx.KeywordNames)
                if (name != "start") throw Ops.TypeError("enumerate() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "start", out Value s)) start = s.AsInt();
            return Value.FromIterator(new EnumerateIterator(Ops.GetIterator(a[0]), start));
        }

        // The (__enter__, __exit__) pair of a context manager; the 'with' statement calls them as ordinary methods.
        private static Value ContextManager(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value mgr = a[0];
            Value enter = default, exit = default;
            if (mgr.Kind == ValueKind.Instance)
            {
                var cls = ((PyInstance)mgr.RawObject).Class;
                if (cls.TryFind("__enter__", out Value e) && e.Kind == ValueKind.Function) enter = Value.FromScriptMethod(new ScriptMethod(mgr, (PyFunction)e.RawObject));
                if (cls.TryFind("__exit__", out Value x) && x.Kind == ValueKind.Function) exit = Value.FromScriptMethod(new ScriptMethod(mgr, (PyFunction)x.RawObject));
            }
            string missing = exit.Kind == ValueKind.None ? "__exit__" : enter.Kind == ValueKind.None ? "__enter__" : null;
            if (missing != null)
                throw Ops.TypeError("'" + mgr.TypeName + "' object does not support the context manager protocol (missed " + missing + " method)");
            return Value.Tuple(enter, exit);
        }

        private static Value Reversed(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value v = a[0];
            if (v.Kind == ValueKind.Instance && Ops.TryCallDunder(v, "__reversed__", Array.Empty<Value>(), out Value custom)) return custom;
            if (v.Kind == ValueKind.Iterator || v.Kind == ValueKind.Set || v.Kind == ValueKind.Int || v.Kind == ValueKind.Float || v.Kind == ValueKind.Bool || v.Kind == ValueKind.None)
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
                if (a[0].IsInteger) return a[0].Kind == ValueKind.Bool ? Value.FromInt(a[0].RawInt) : a[0];
                double d = a[0].AsFloat();
                if (double.IsNaN(d)) throw Ops.ValueError("cannot convert float NaN to integer");
                return Ops.FloatToInt(f(d));
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
                if (MathExtras.Exactly(b, 1.0)) throw new PyError("ZeroDivisionError", "float division by zero");
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
            MathExtras.Install(m);
            return m;
        }
    }
}
