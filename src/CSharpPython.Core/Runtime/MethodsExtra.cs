using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CSharpPython
{
    // The less common methods of str, dict, int and float.
    internal static partial class Methods
    {
        private static readonly Dictionary<string, NativeFunction> CounterMethods = new Dictionary<string, NativeFunction>();

        public static IEnumerable<string> CounterNames => CounterMethods.Keys;

        public static NativeFunction FindCounter(string name) => CounterMethods.TryGetValue(name, out var fn) ? fn : null;

        public static NativeFunction DictFromKeysNative => DictFromKeys;

        private static readonly NativeFunction DictFromKeys =
            new NativeFunction("dict.fromkeys", (c, a) => FromKeys(a), 1, 2);

        /// <summary>Finds a method on a built-in type itself, as in <c>str.lower</c> or <c>dict.fromkeys</c>; the instance is then the first argument.</summary>
        public static NativeFunction FindOnType(PyType type, string name)
        {
            if (type == PyTypes.Dict && name == "fromkeys") return DictFromKeys;
            if (type == PyTypes.Counter && CounterMethods.TryGetValue(name, out var counterMethod)) return counterMethod;
            if (type == PyTypes.FrozenSet && SetMutators.Contains(name)) return null;
            if (type == PyTypes.Generator || type == PyTypes.Iterator || type.Kind == null) return null;
            switch (type.Kind.Value)
            {
                case ValueKind.Str:
                case ValueKind.Int:
                case ValueKind.Bool:
                case ValueKind.Float:
                case ValueKind.List:
                case ValueKind.Tuple:
                case ValueKind.Dict:
                case ValueKind.Set:
                    return Find(type.Kind.Value, name);
                default:
                    return null;
            }
        }

        private static Value FromKeys(ReadOnlySpan<Value> a)
        {
            var result = new PyDict();
            Value fill = a.Length > 1 ? a[1] : Value.None;
            foreach (var key in Ops.ToList(a[0])) result.Set(key, fill);
            return Value.FromDict(result);
        }

        // Adds (or, with a negative sign, subtracts) the counts of a mapping, or of each item of an iterable, into a counter.
        internal static void CountInto(PyDict counts, Value source, bool subtract)
        {
            var one = Value.FromInt(1);
            if (source.Kind == ValueKind.Dict)
            {
                foreach (var kv in ((PyDict)source.RawObject).Pairs().ToList())
                    AddCount(counts, kv.Key, kv.Value, subtract);
                return;
            }
            foreach (var item in Ops.ToList(source)) AddCount(counts, item, one, subtract);
        }

        private static void AddCount(PyDict counts, Value key, Value amount, bool subtract)
        {
            Value current = counts.TryGetValue(key, out Value existing) ? existing : Value.FromInt(0);
            counts.Set(key, subtract ? Ops.Sub(current, amount) : Ops.Add(current, amount));
        }

        private static void InstallCounter()
        {
            var t = CounterMethods;
            Add(t, "Counter", "most_common", 0, 1, (c, a) =>
            {
                var items = Ops.MostCommon(a[0].AsDict());
                int count = items.Count;
                if (a.Length > 1 && !a[1].IsNone) count = (int)Math.Max(0, Math.Min(a[1].AsInt(), items.Count));
                var result = new List<Value>(count);
                for (int i = 0; i < count; i++) result.Add(Value.Tuple(items[i].Key, items[i].Value));
                return Value.FromList(new PyList(result));
            });
            Add(t, "Counter", "elements", 0, 0, (c, a) =>
            {
                var result = new List<Value>();
                foreach (var kv in a[0].AsDict().Pairs())
                {
                    long n = kv.Value.IsInteger ? (kv.Value.Kind == ValueKind.BigInt ? Ops.MaxSequenceLength : kv.Value.RawInt) : 0;
                    if (result.Count + Math.Max(0, n) > Ops.MaxSequenceLength) throw new PyError("MemoryError", "sequence too long");
                    for (long i = 0; i < n; i++) result.Add(kv.Key);
                }
                return Value.FromIterator(new ListIterator(new PyList(result)));
            });
            Add(t, "Counter", "total", 0, 0, (c, a) =>
            {
                Value sum = Value.FromInt(0);
                foreach (var kv in a[0].AsDict().Pairs()) sum = Ops.Add(sum, kv.Value);
                return sum;
            });
            Add(t, "Counter", "update", 0, 1, (c, a) => CounterUpdate(c, a, false), true);
            Add(t, "Counter", "subtract", 0, 1, (c, a) => CounterUpdate(c, a, true), true);
            Add(t, "Counter", "fromkeys", 0, 2, (c, a) =>
                throw new PyError("NotImplementedError", "Counter.fromkeys() is undefined.  Use Counter(iterable) instead."));
        }

        private static Value CounterUpdate(CallContext ctx, ReadOnlySpan<Value> a, bool subtract)
        {
            var counts = a[0].AsDict();
            if (ctx.PositionalCount > 1 && !a[1].IsNone) CountInto(counts, a[1], subtract);
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
                AddCount(counts, Value.FromString(ctx.KeywordNames[i]), a[ctx.PositionalCount + i], subtract);
            return Value.None;
        }

        private static void InstallMoreDict()
        {
            var t = DictMethods;
            Add(t, "dict", "fromkeys", 1, 2, (c, a) => FromKeys(a.Slice(1)));
            Add(t, "dict", "popitem", 0, 0, (c, a) =>
            {
                var d = a[0].AsDict();
                for (int slot = d.SlotCount - 1; slot >= 0; slot--)
                {
                    if (!d.TryGetEntry(slot, out Value key, out Value value)) continue;
                    d.Remove(key);
                    return Value.Tuple(key, value);
                }
                throw new PyError("KeyError", "'popitem(): dictionary is empty'");
            });
        }

        private static void InstallNumbers()
        {
            Add(IntMethods, "int", "bit_length", 0, 0, (c, a) => Value.FromInt(Ops.BitLength(a[0])));
            Add(IntMethods, "int", "bit_count", 0, 0, (c, a) =>
            {
                var n = BigInteger.Abs(Ops.ToBig(a[0]));
                long count = 0;
                foreach (byte b in n.ToByteArray())
                    for (int x = b; x != 0; x &= x - 1) count++;
                return Value.FromInt(count);
            });
            Add(IntMethods, "int", "is_integer", 0, 0, (c, a) => Value.FromBool(true));
            Add(FloatMethods, "float", "is_integer", 0, 0, (c, a) =>
            {
                double d = a[0].AsFloat();
                return Value.FromBool(!double.IsInfinity(d) && !double.IsNaN(d) && Ops.IsIntegral(d));
            });
        }

        private static void InstallMoreStr()
        {
            var t = StrMethods;
            Add(t, "str", "casefold", 0, 0, (c, a) => Value.FromString(S(a, 0).ToLowerInvariant().Replace("ß", "ss")));
            Add(t, "str", "swapcase", 0, 0, (c, a) => Value.FromString(SwapCase(S(a, 0))));
            Add(t, "str", "isascii", 0, 0, (c, a) =>
            {
                foreach (char ch in S(a, 0)) if (ch > 127) return Value.FromBool(false);
                return Value.FromBool(true);
            });
            Add(t, "str", "isdecimal", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), ch => char.GetUnicodeCategory(ch) == UnicodeCategory.DecimalDigitNumber)));
            Add(t, "str", "isnumeric", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), char.IsNumber)));
            Add(t, "str", "isidentifier", 0, 0, (c, a) =>
            {
                string s = S(a, 0);
                if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return Value.FromBool(false);
                for (int i = 1; i < s.Length; i++)
                    if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_')) return Value.FromBool(false);
                return Value.FromBool(true);
            });
            Add(t, "str", "isprintable", 0, 0, (c, a) => Value.FromBool(AllCharsOrEmpty(S(a, 0), IsPrintable)));
            Add(t, "str", "istitle", 0, 0, (c, a) => Value.FromBool(IsTitle(S(a, 0))));
            Add(t, "str", "expandtabs", 0, 1, (c, a) =>
            {
                foreach (var name in c.KeywordNames)
                    if (name != "tabsize") throw Ops.TypeError("expandtabs() got an unexpected keyword argument '" + name + "'");
                long size = 8;
                if (c.PositionalCount > 1) size = a[1].AsInt();
                else if (c.TryGetKeyword(a, "tabsize", out Value k)) size = k.AsInt();
                return Value.FromString(ExpandTabs(S(a, 0), (int)Math.Max(-1, Math.Min(size, 1 << 20))));
            }, true);
            Add(t, "str", "partition", 1, 1, (c, a) => Partition(a, false));
            Add(t, "str", "rpartition", 1, 1, (c, a) => Partition(a, true));
            Add(t, "str", "removeprefix", 1, 1, (c, a) =>
            {
                string s = S(a, 0), p = AffixArg(a[1], "removeprefix");
                return Value.FromString(p.Length > 0 && s.StartsWith(p, StringComparison.Ordinal) ? s.Substring(p.Length) : s);
            });
            Add(t, "str", "removesuffix", 1, 1, (c, a) =>
            {
                string s = S(a, 0), p = AffixArg(a[1], "removesuffix");
                return Value.FromString(p.Length > 0 && s.EndsWith(p, StringComparison.Ordinal) ? s.Substring(0, s.Length - p.Length) : s);
            });
            Add(t, "str", "rindex", 1, 3, (c, a) =>
            {
                long i = Find(a, true);
                if (i < 0) throw Ops.ValueError("substring not found");
                return Value.FromInt(i);
            });
            Add(t, "str", "rsplit", 0, 2, RSplit, true);
        }

        private static string AffixArg(Value v, string method)
        {
            if (v.Kind != ValueKind.Str) throw Ops.TypeError(method + "() argument must be str, not " + v.TypeName);
            return v.AsString();
        }

        private static bool AllCharsOrEmpty(string s, Func<char, bool> test)
        {
            foreach (char ch in s) if (!test(ch)) return false;
            return true;
        }

        private static bool IsPrintable(char ch)
        {
            if (ch == ' ') return true;
            switch (char.GetUnicodeCategory(ch))
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OtherNotAssigned:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.SpaceSeparator:
                    return false;
                default:
                    return true;
            }
        }

        private static string SwapCase(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == 'ß') sb.Append("SS");
                else if (char.IsUpper(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (char.IsLower(ch)) sb.Append(char.ToUpperInvariant(ch));
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static bool IsTitle(string s)
        {
            bool cased = false, previousCased = false;
            foreach (char ch in s)
            {
                if (char.IsUpper(ch))
                {
                    if (previousCased) return false;
                    previousCased = true;
                    cased = true;
                }
                else if (char.IsLower(ch))
                {
                    if (!previousCased) return false;
                    previousCased = true;
                    cased = true;
                }
                else
                {
                    previousCased = false;
                }
            }
            return cased;
        }

        private static string ExpandTabs(string s, int tabSize)
        {
            var sb = new StringBuilder();
            int column = 0;
            foreach (char ch in s)
            {
                if (ch == '\t')
                {
                    if (tabSize > 0)
                    {
                        int pad = tabSize - column % tabSize;
                        if (sb.Length + pad > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                        sb.Append(' ', pad);
                        column += pad;
                    }
                }
                else
                {
                    sb.Append(ch);
                    column = ch == '\n' || ch == '\r' ? 0 : column + 1;
                }
            }
            return sb.ToString();
        }

        private static Value Partition(ReadOnlySpan<Value> a, bool last)
        {
            string s = S(a, 0);
            if (a[1].Kind != ValueKind.Str) throw Ops.TypeError("must be str, not " + a[1].TypeName);
            string sep = a[1].AsString();
            if (sep.Length == 0) throw Ops.ValueError("empty separator");
            int i = last ? s.LastIndexOf(sep, StringComparison.Ordinal) : s.IndexOf(sep, StringComparison.Ordinal);
            if (i < 0)
                return last ? Value.Tuple(Value.FromString(""), Value.FromString(""), Value.FromString(s))
                            : Value.Tuple(Value.FromString(s), Value.FromString(""), Value.FromString(""));
            return Value.Tuple(Value.FromString(s.Substring(0, i)), Value.FromString(sep), Value.FromString(s.Substring(i + sep.Length)));
        }

        private static Value RSplit(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value sepArg = default, maxArg = default;
            if (ctx.PositionalCount > 1) sepArg = a[1];
            if (ctx.PositionalCount > 2) maxArg = a[2];
            if (ctx.TryGetKeyword(a, "sep", out Value k)) sepArg = k;
            if (ctx.TryGetKeyword(a, "maxsplit", out k)) maxArg = k;
            foreach (var name in ctx.KeywordNames)
                if (name != "sep" && name != "maxsplit") throw Ops.TypeError("rsplit() got an unexpected keyword argument '" + name + "'");

            string s = S(a, 0);
            bool maxGiven = ctx.PositionalCount > 2 || ctx.TryGetKeyword(a, "maxsplit", out _);
            if (maxGiven && maxArg.IsNone) throw Ops.TypeError("'NoneType' object cannot be interpreted as an integer");
            long max = maxArg.IsNone ? -1 : maxArg.AsInt();
            var parts = new List<Value>();

            if (sepArg.IsNone)
            {
                int end = s.Length;
                while (true)
                {
                    while (end > 0 && char.IsWhiteSpace(s[end - 1])) end--;
                    if (end == 0) break;
                    if (max >= 0 && parts.Count >= max)
                    {
                        parts.Add(Value.FromString(s.Substring(0, end)));
                        break;
                    }
                    int stop = end;
                    while (end > 0 && !char.IsWhiteSpace(s[end - 1])) end--;
                    parts.Add(Value.FromString(s.Substring(end, stop - end)));
                }
            }
            else
            {
                string sep = sepArg.AsString();
                if (sep.Length == 0) throw Ops.ValueError("empty separator");
                int end = s.Length;
                while (true)
                {
                    int idx = max >= 0 && parts.Count >= max || end < sep.Length ? -1 : s.LastIndexOf(sep, end - 1, end, StringComparison.Ordinal);
                    if (idx < 0)
                    {
                        parts.Add(Value.FromString(s.Substring(0, end)));
                        break;
                    }
                    parts.Add(Value.FromString(s.Substring(idx + sep.Length, end - idx - sep.Length)));
                    end = idx;
                }
            }
            parts.Reverse();
            return Value.FromList(new PyList(parts));
        }
    }
}
