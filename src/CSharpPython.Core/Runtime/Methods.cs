using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpPython
{
    /// <summary>The built-in methods of str, list, dict and tuple.</summary>
    internal static partial class Methods
    {
        private static readonly Dictionary<string, NativeFunction> StrMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> ListMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> DictMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> TupleMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> IntMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> DequeMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> FloatMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> SetMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> DescriptorMethods = new Dictionary<string, NativeFunction>();
        private static readonly Dictionary<string, NativeFunction> IteratorMethods = new Dictionary<string, NativeFunction>();

        // What super() reaches once the script classes run out: the methods every object has. They are looked up by
        // name when a snapshot is restored, which is why they live in a table instead of being made on the spot.
        private static readonly Dictionary<string, NativeFunction> InstanceMethods = new Dictionary<string, NativeFunction>();

        // The set methods that change the set, which a frozenset does not have.
        private static readonly HashSet<string> SetMutators = new HashSet<string>
        {
            "add", "remove", "discard", "pop", "clear", "update",
            "intersection_update", "difference_update", "symmetric_difference_update",
        };

        /// <summary>Finds a method for a value, taking into account that a frozenset has only the set methods that do not change it.</summary>
        public static NativeFunction FindFor(Value obj, string name)
        {
            if (obj.Kind == ValueKind.Set && ((PySet)obj.RawObject).Frozen && SetMutators.Contains(name)) return null;
            if (obj.Kind == ValueKind.Dict && ((PyDict)obj.RawObject).Flavor == DictFlavor.Counter && CounterMethods.TryGetValue(name, out var counterMethod)) return counterMethod;
            if (obj.Kind == ValueKind.Descriptor && ((PyDescriptor)obj.RawObject).Kind != DescriptorKind.Property) return null;
            return Find(obj.Kind, name);
        }

        public static NativeFunction Find(ValueKind kind, string name)
        {
            Dictionary<string, NativeFunction> table;
            switch (kind)
            {
                case ValueKind.Str: table = StrMethods; break;
                case ValueKind.Int:
                case ValueKind.Bool:
                case ValueKind.BigInt: table = IntMethods; break;
                case ValueKind.Float: table = FloatMethods; break;
                case ValueKind.Deque: table = DequeMethods; break;
                case ValueKind.List: table = ListMethods; break;
                case ValueKind.Dict: table = DictMethods; break;
                case ValueKind.Tuple: table = TupleMethods; break;
                case ValueKind.Set: table = SetMethods; break;
                case ValueKind.Iterator: table = IteratorMethods; break;
                case ValueKind.Descriptor: table = DescriptorMethods; break;
                case ValueKind.Instance: table = InstanceMethods; break;
                default: return null;
            }
            return table.TryGetValue(name, out var fn) ? fn : null;
        }

        /// <summary>The methods every script object has, such as <c>__setattr__</c>.</summary>
        public static IEnumerable<string> InstanceNames => InstanceMethods.Keys;

        /// <summary>The names of the methods of a type, for editor auto-complete.</summary>
        public static IEnumerable<string> NamesOf(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Str: return StrMethods.Keys;
                case ValueKind.Int:
                case ValueKind.Bool:
                case ValueKind.BigInt: return IntMethods.Keys;
                case ValueKind.Float: return FloatMethods.Keys;
                case ValueKind.Deque: return DequeMethods.Keys;
                case ValueKind.List: return ListMethods.Keys;
                case ValueKind.Dict: return DictMethods.Keys;
                case ValueKind.Tuple: return TupleMethods.Keys;
                case ValueKind.Set: return SetMethods.Keys;
                case ValueKind.Iterator: return IteratorMethods.Keys;
                case ValueKind.Descriptor: return DescriptorMethods.Keys;
                default: return Enumerable.Empty<string>();
            }
        }

        private static void Add(Dictionary<string, NativeFunction> table, string typeName, string name, int min, int max, NativeFn fn, bool keywords = false)
        {
            // 'min' and 'max' count arguments after self; the stored function counts self too.
            table[name] = new NativeFunction(typeName + "." + name, fn, min + 1, max < 0 ? -1 : max + 1, keywords);
        }

        static Methods()
        {
            InstallStr();
            InstallList();
            InstallDict();
            InstallTuple();
            InstallSet();
            InstallIterator();
            InstallDescriptor();
            InstallInstance();
            InstallMoreStr();
            InstallNumbers();
            InstallMoreDict();
            InstallCounter();
            InstallDeque();
        }

        // property.getter / setter / deleter return a copy of the property with that function replaced.
        private static void InstallDescriptor()
        {
            var t = DescriptorMethods;
            Add(t, "property", "getter", 1, 1, (c, a) => Value.FromDescriptor(((PyDescriptor)a[0].RawObject).With(AccessorArg(a[1]), null, null)));
            Add(t, "property", "setter", 1, 1, (c, a) => Value.FromDescriptor(((PyDescriptor)a[0].RawObject).With(null, AccessorArg(a[1]), null)));
            Add(t, "property", "deleter", 1, 1, (c, a) => Value.FromDescriptor(((PyDescriptor)a[0].RawObject).With(null, null, AccessorArg(a[1]))));
        }

        private static Value AccessorArg(Value v)
        {
            if (v.Kind != ValueKind.None && !PyTypes.IsCallable(v)) throw new PyError("TypeError", "property accessor must be callable or None");
            return v;
        }

        private static void InstallInstance()
        {
            var t = InstanceMethods;
            Add(t, "object", "__init__", 0, -1, (c, a) =>
            {
                var instance = (PyInstance)a[0].RawObject;
                if (instance.Class.ExceptionRoot == null)
                {
                    if (a.Length > 1)
                        throw new PyError("TypeError", "object.__init__() takes exactly one argument (the instance to initialize)");
                    return Value.None;
                }
                var items = new Value[a.Length - 1];
                for (int i = 0; i < items.Length; i++) items[i] = a[i + 1];
                instance.Fields.Set(Value.FromString("args"), Value.FromTuple(new PyTuple(items)));
                return Value.None;
            });
            Add(t, "object", "__setattr__", 2, 2, (c, a) =>
            {
                Ops.SetAttrCore(a[0], Ops.AttrName(a[1], "__setattr__"), a[2]);
                return Value.None;
            });
            Add(t, "object", "__delattr__", 1, 1, (c, a) =>
            {
                Ops.DelAttrCore(a[0], Ops.AttrName(a[1], "__delattr__"));
                return Value.None;
            });
            Add(t, "object", "__str__", 0, 0, (c, a) =>
            {
                var instance = (PyInstance)a[0].RawObject;
                return Value.FromString(instance.Class.ExceptionRoot != null
                    ? Ops.ExceptionArgsText(instance) : Ops.DefaultInstanceRepr(instance));
            });
            Add(t, "object", "__repr__", 0, 0, (c, a) =>
                Value.FromString(Ops.DefaultInstanceRepr((PyInstance)a[0].RawObject)));
        }

        // ---- Argument helpers -----------------------------------------------------------------------------------

        // Reads the optional start and end arguments (a[2], a[3]) of index(), with slice-style negatives and clamping.
        private static void Range(ReadOnlySpan<Value> a, int length, out int from, out int to)
        {
            long start = a.Length > 2 ? Clamped(a[2]) : 0;
            long end = a.Length > 3 ? Clamped(a[3]) : length;
            if (start < 0) start = Math.Max(0, start + length);
            if (end < 0) end = Math.Max(0, end + length);
            from = (int)Math.Min(start, length);
            to = (int)Math.Min(end, length);
        }

        // An int argument that only bounds a search: a huge value counts as the nearest extreme.
        private static long Clamped(Value v)
        {
            if (v.Kind == ValueKind.BigInt) return ((System.Numerics.BigInteger)v.RawObject).Sign < 0 ? long.MinValue / 2 : long.MaxValue / 2;
            return v.AsInt();
        }

        private static string S(ReadOnlySpan<Value> a, int i) => a[i].AsString();

        private static bool Has(ReadOnlySpan<Value> a, int i) => i < a.Length && !a[i].IsNone;

        private static long OptInt(ReadOnlySpan<Value> a, int i, long fallback)
        {
            if (!Has(a, i)) return fallback;
            if (!a[i].IsInteger) throw Ops.TypeError("'" + a[i].TypeName + "' object cannot be interpreted as an integer");
            return Clamped(a[i]);
        }

        private static void CheckSize(int current, int adding, string what)
        {
            if ((long)current + adding > Ops.MaxSequenceLength) throw new PyError("MemoryError", what + " too long");
        }

        // ---- str ------------------------------------------------------------------------------------------------

        private static void InstallStr()
        {
            var t = StrMethods;
            Add(t, "str", "upper", 0, 0, (c, a) => Value.FromString(S(a, 0).ToUpperInvariant()));
            Add(t, "str", "lower", 0, 0, (c, a) => Value.FromString(S(a, 0).ToLowerInvariant()));
            Add(t, "str", "strip", 0, 1, (c, a) => Value.FromString(Strip(a, true, true)));
            Add(t, "str", "lstrip", 0, 1, (c, a) => Value.FromString(Strip(a, true, false)));
            Add(t, "str", "rstrip", 0, 1, (c, a) => Value.FromString(Strip(a, false, true)));
            Add(t, "str", "split", 0, 2, Split, true);
            Add(t, "str", "join", 1, 1, Join);
            Add(t, "str", "replace", 2, 3, Replace);
            Add(t, "str", "startswith", 1, 1, (c, a) => Value.FromBool(AnyAffix(a, true)));
            Add(t, "str", "endswith", 1, 1, (c, a) => Value.FromBool(AnyAffix(a, false)));
            Add(t, "str", "find", 1, 3, (c, a) => Value.FromInt(Find(a, false)));
            Add(t, "str", "rfind", 1, 3, (c, a) => Value.FromInt(Find(a, true)));
            Add(t, "str", "index", 1, 3, (c, a) =>
            {
                long i = Find(a, false);
                if (i < 0) throw Ops.ValueError("substring not found");
                return Value.FromInt(i);
            });
            Add(t, "str", "count", 1, 1, Count);
            Add(t, "str", "isdigit", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), char.IsDigit)));
            Add(t, "str", "isalpha", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), char.IsLetter)));
            Add(t, "str", "isalnum", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), char.IsLetterOrDigit)));
            Add(t, "str", "isspace", 0, 0, (c, a) => Value.FromBool(AllChars(S(a, 0), char.IsWhiteSpace)));
            Add(t, "str", "isupper", 0, 0, (c, a) => Value.FromBool(CasedAll(S(a, 0), true)));
            Add(t, "str", "islower", 0, 0, (c, a) => Value.FromBool(CasedAll(S(a, 0), false)));
            Add(t, "str", "capitalize", 0, 0, (c, a) =>
            {
                string s = S(a, 0);
                return Value.FromString(s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant());
            });
            Add(t, "str", "title", 0, 0, (c, a) => Value.FromString(Title(S(a, 0))));
            Add(t, "str", "format", 0, -1, FormatMethod, true);
            Add(t, "str", "zfill", 1, 1, (c, a) =>
            {
                string s = S(a, 0);
                long width = a[1].AsInt();
                if (width > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                if (s.Length >= width) return Value.FromString(s);
                int signLen = s.Length > 0 && (s[0] == '+' || s[0] == '-') ? 1 : 0;
                return Value.FromString(s.Substring(0, signLen) + new string('0', (int)width - s.Length) + s.Substring(signLen));
            });
            Add(t, "str", "ljust", 1, 2, (c, a) => Value.FromString(Justify(a, 'l')));
            Add(t, "str", "rjust", 1, 2, (c, a) => Value.FromString(Justify(a, 'r')));
            Add(t, "str", "center", 1, 2, (c, a) => Value.FromString(Justify(a, 'c')));
            Add(t, "str", "splitlines", 0, 1, (c, a) => Value.FromList(new PyList(SplitLines(S(a, 0), a.Length > 1 && a[1].IsTruthy))));
        }

        private static string Strip(ReadOnlySpan<Value> a, bool left, bool right)
        {
            string s = S(a, 0);
            char[] chars = Has(a, 1) ? S(a, 1).ToCharArray() : null;
            int start = 0, end = s.Length;
            if (left) while (start < end && (chars == null ? char.IsWhiteSpace(s[start]) : Array.IndexOf(chars, s[start]) >= 0)) start++;
            if (right) while (end > start && (chars == null ? char.IsWhiteSpace(s[end - 1]) : Array.IndexOf(chars, s[end - 1]) >= 0)) end--;
            return s.Substring(start, end - start);
        }

        private static Value Split(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value sepArg = default, maxArg = default;
            if (ctx.PositionalCount > 1) sepArg = a[1];
            if (ctx.PositionalCount > 2) maxArg = a[2];
            if (ctx.TryGetKeyword(a, "sep", out Value k)) sepArg = k;
            if (ctx.TryGetKeyword(a, "maxsplit", out k)) maxArg = k;
            foreach (var name in ctx.KeywordNames)
                if (name != "sep" && name != "maxsplit") throw Ops.TypeError("split() got an unexpected keyword argument '" + name + "'");

            string s = S(a, 0);
            bool maxGiven = ctx.PositionalCount > 2 || ctx.TryGetKeyword(a, "maxsplit", out _);
            if (maxGiven && maxArg.IsNone) throw Ops.TypeError("'NoneType' object cannot be interpreted as an integer");
            long max = maxArg.IsNone ? -1 : maxArg.AsInt();
            var parts = new List<Value>();

            if (sepArg.IsNone)
            {
                int i = 0;
                while (true)
                {
                    while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                    if (i >= s.Length) break;
                    if (max >= 0 && parts.Count >= max)
                    {
                        parts.Add(Value.FromString(s.Substring(i).TrimEnd()));
                        break;
                    }
                    int start = i;
                    while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
                    parts.Add(Value.FromString(s.Substring(start, i - start)));
                }
                return Value.FromList(new PyList(parts));
            }

            string sep = sepArg.AsString();
            if (sep.Length == 0) throw Ops.ValueError("empty separator");
            int pos = 0;
            while (true)
            {
                int idx = max >= 0 && parts.Count >= max ? -1 : s.IndexOf(sep, pos, StringComparison.Ordinal);
                if (idx < 0)
                {
                    parts.Add(Value.FromString(s.Substring(pos)));
                    break;
                }
                parts.Add(Value.FromString(s.Substring(pos, idx - pos)));
                pos = idx + sep.Length;
            }
            return Value.FromList(new PyList(parts));
        }

        private static Value Join(CallContext ctx, ReadOnlySpan<Value> a)
        {
            string sep = S(a, 0);
            var sb = new StringBuilder();
            int index = 0;
            foreach (var item in Ops.ToList(a[1]))
            {
                if (item.Kind != ValueKind.Str)
                    throw Ops.TypeError("sequence item " + index + ": expected str instance, " + item.TypeName + " found");
                if (index > 0) sb.Append(sep);
                sb.Append((string)item.RawObject);
                if (sb.Length > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                index++;
            }
            return Value.FromString(sb.ToString());
        }

        private static Value Replace(CallContext ctx, ReadOnlySpan<Value> a)
        {
            string s = S(a, 0), oldText = S(a, 1), newText = S(a, 2);
            long count = OptInt(a, 3, -1);
            if (count < 0 && oldText.Length > 0)
            {
                long growth = (long)s.Length + (long)(newText.Length - oldText.Length) * (s.Length / oldText.Length + 1);
                if (growth > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                return Value.FromString(s.Replace(oldText, newText));
            }
            var sb = new StringBuilder();
            int pos = 0;
            long done = 0;
            if (oldText.Length == 0)
            {
                // Python inserts the replacement between every character.
                for (int i = 0; i <= s.Length; i++)
                {
                    if (count < 0 || done < count) { sb.Append(newText); done++; }
                    if (i < s.Length) sb.Append(s[i]);
                    if (sb.Length > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
                }
                return Value.FromString(sb.ToString());
            }
            while (done < count)
            {
                int idx = s.IndexOf(oldText, pos, StringComparison.Ordinal);
                if (idx < 0) break;
                sb.Append(s, pos, idx - pos).Append(newText);
                pos = idx + oldText.Length;
                done++;
            }
            sb.Append(s, pos, s.Length - pos);
            return Value.FromString(sb.ToString());
        }

        private static bool AnyAffix(ReadOnlySpan<Value> a, bool prefix)
        {
            string s = S(a, 0);
            Value affix = a[1];
            IEnumerable<Value> options;
            if (affix.Kind == ValueKind.Tuple) options = ((PyTuple)affix.RawObject).Items;
            else if (affix.Kind == ValueKind.Str) options = new[] { affix };
            else throw Ops.TypeError((prefix ? "startswith" : "endswith") + " first arg must be str or a tuple of str, not " + affix.TypeName);
            foreach (var o in options)
            {
                string p = o.AsString();
                if (prefix ? s.StartsWith(p, StringComparison.Ordinal) : s.EndsWith(p, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static long Find(ReadOnlySpan<Value> a, bool last)
        {
            string s = S(a, 0), sub = S(a, 1);
            long start = OptInt(a, 2, 0), end = OptInt(a, 3, s.Length);
            if (start < 0) start = Math.Max(0, start + s.Length);
            if (end < 0) end += s.Length;
            if (end > s.Length) end = s.Length;
            if (start > end) return -1;
            int length = (int)(end - start);
            int idx = last
                ? s.LastIndexOf(sub, (int)start + length - 1 < 0 ? 0 : (int)start + length - 1, length == 0 ? 0 : length, StringComparison.Ordinal)
                : s.IndexOf(sub, (int)start, length, StringComparison.Ordinal);
            if (last && sub.Length == 0) idx = (int)end;
            return idx;
        }

        private static Value Count(CallContext ctx, ReadOnlySpan<Value> a)
        {
            string s = S(a, 0), sub = S(a, 1);
            if (sub.Length == 0) return Value.FromInt(s.Length + 1);
            long n = 0;
            int pos = 0;
            while ((pos = s.IndexOf(sub, pos, StringComparison.Ordinal)) >= 0) { n++; pos += sub.Length; }
            return Value.FromInt(n);
        }

        private static bool AllChars(string s, Func<char, bool> test)
        {
            if (s.Length == 0) return false;
            foreach (char c in s) if (!test(c)) return false;
            return true;
        }

        private static bool CasedAll(string s, bool upper)
        {
            bool cased = false;
            foreach (char c in s)
            {
                if (!char.IsLetter(c)) continue;
                if (upper ? char.IsLower(c) : char.IsUpper(c)) return false;
                cased = true;
            }
            return cased;
        }

        private static string Title(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool start = true;
            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    start = false;
                }
                else
                {
                    sb.Append(c);
                    start = true;
                }
            }
            return sb.ToString();
        }

        private static string Justify(ReadOnlySpan<Value> a, char mode)
        {
            string s = S(a, 0);
            long width = a[1].AsInt();
            string fillText = a.Length > 2 ? S(a, 2) : " ";
            if (fillText.Length != 1) throw Ops.TypeError("The fill character must be exactly one character long");
            if (width > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
            if (s.Length >= width) return s;
            int pad = (int)width - s.Length;
            char fill = fillText[0];
            switch (mode)
            {
                case 'l': return s + new string(fill, pad);
                case 'r': return new string(fill, pad) + s;
                default:
                {
                    // Python puts the extra padding character on the left when the width is odd and the string is long.
                    int left = pad / 2 + (int)(pad & width & 1);
                    return new string(fill, left) + s + new string(fill, pad - left);
                }
            }
        }

        private static List<Value> SplitLines(string s, bool keepEnds)
        {
            var lines = new List<Value>();
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\n' && s[i] != '\r') continue;
                int end = i;
                if (s[i] == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                lines.Add(Value.FromString(s.Substring(start, (keepEnds ? i + 1 : end) - start)));
                start = i + 1;
            }
            if (start < s.Length) lines.Add(Value.FromString(s.Substring(start)));
            return lines;
        }

        private static Value FormatMethod(CallContext ctx, ReadOnlySpan<Value> a)
        {
            string template = S(a, 0);
            int positional = ctx.PositionalCount - 1;
            var sb = new StringBuilder();
            int auto = 0;
            bool manual = false;
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}') { sb.Append('}'); i += 2; continue; }
                    throw Ops.ValueError("Single '}' encountered in format string");
                }
                if (c != '{') { sb.Append(c); i++; continue; }
                if (i + 1 < template.Length && template[i + 1] == '{') { sb.Append('{'); i += 2; continue; }

                int close = template.IndexOf('}', i);
                if (close < 0) throw Ops.ValueError("Single '{' encountered in format string");
                string field = template.Substring(i + 1, close - i - 1);
                i = close + 1;
                if (field.IndexOf('{') >= 0) throw Ops.ValueError("nested replacement fields are not supported yet");

                string spec = null;
                int colon = field.IndexOf(':');
                if (colon >= 0) { spec = field.Substring(colon + 1); field = field.Substring(0, colon); }
                char conversion = '\0';
                int bang = field.IndexOf('!');
                if (bang >= 0)
                {
                    if (bang + 2 != field.Length || "sra".IndexOf(field[bang + 1]) < 0)
                        throw Ops.ValueError("Invalid conversion specification");
                    conversion = field[bang + 1];
                    field = field.Substring(0, bang);
                }

                Value value;
                if (field.Length == 0)
                {
                    if (manual) throw Ops.ValueError("cannot switch from manual field specification to automatic field numbering");
                    if (auto >= positional) throw new PyError("IndexError", "Replacement index " + auto + " out of range for positional args tuple");
                    value = a[1 + auto++];
                }
                else if (char.IsDigit(field[0]))
                {
                    if (!int.TryParse(field, out int index)) throw Ops.ValueError("Only '.' or '[' may follow ']' in format field specifier");
                    if (auto > 0) throw Ops.ValueError("cannot switch from automatic field numbering to manual field specification");
                    manual = true;
                    if (index >= positional) throw new PyError("IndexError", "Replacement index " + index + " out of range for positional args tuple");
                    value = a[1 + index];
                }
                else
                {
                    if (field.IndexOfAny(new[] { '.', '[' }) >= 0) throw Ops.ValueError("attribute and index access in format fields is not supported yet");
                    if (!ctx.TryGetKeyword(a, field, out value)) throw new PyError("KeyError", Ops.Repr(Value.FromString(field)));
                }

                if (conversion == 's') value = Value.FromString(Ops.Str(value));
                else if (conversion == 'r' || conversion == 'a') value = Value.FromString(Ops.Repr(value));
                sb.Append(Ops.FormatValue(value, spec));
                if (sb.Length > Ops.MaxSequenceLength) throw new PyError("MemoryError", "string too long");
            }
            return Value.FromString(sb.ToString());
        }

        // ---- list -----------------------------------------------------------------------------------------------

        private static void InstallList()
        {
            var t = ListMethods;
            Add(t, "list", "append", 1, 1, (c, a) =>
            {
                var items = a[0].AsList().Items;
                CheckSize(items.Count, 1, "list");
                items.Add(a[1]);
                return Value.None;
            });
            Add(t, "list", "extend", 1, 1, (c, a) =>
            {
                var items = a[0].AsList().Items;
                var more = Ops.ToList(a[1]);
                CheckSize(items.Count, more.Count, "list");
                items.AddRange(more);
                return Value.None;
            });
            Add(t, "list", "insert", 2, 2, (c, a) =>
            {
                var items = a[0].AsList().Items;
                CheckSize(items.Count, 1, "list");
                long index = a[1].AsInt();
                if (index < 0) index = Math.Max(0, index + items.Count);
                if (index > items.Count) index = items.Count;
                items.Insert((int)index, a[2]);
                return Value.None;
            });
            Add(t, "list", "pop", 0, 1, (c, a) =>
            {
                var items = a[0].AsList().Items;
                if (items.Count == 0) throw new PyError("IndexError", "pop from empty list");
                long index = OptInt(a, 1, -1);
                if (index < 0) index += items.Count;
                if (index < 0 || index >= items.Count) throw new PyError("IndexError", "pop index out of range");
                var v = items[(int)index];
                items.RemoveAt((int)index);
                return v;
            });
            Add(t, "list", "remove", 1, 1, (c, a) =>
            {
                var items = a[0].AsList().Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!Ops.Equal(items[i], a[1])) continue;
                    items.RemoveAt(i);
                    return Value.None;
                }
                throw Ops.ValueError("list.remove(x): x not in list");
            });
            Add(t, "list", "index", 1, 3, (c, a) =>
            {
                var items = a[0].AsList().Items;
                Range(a, items.Count, out int from, out int to);
                for (int i = from; i < to; i++) if (Ops.Equal(items[i], a[1])) return Value.FromInt(i);
                throw Ops.ValueError(Ops.Repr(a[1]) + " is not in list");
            });
            Add(t, "list", "count", 1, 1, (c, a) =>
            {
                long n = 0;
                foreach (var v in a[0].AsList().Items) if (Ops.Equal(v, a[1])) n++;
                return Value.FromInt(n);
            });
            Add(t, "list", "sort", 0, 0, (c, a) =>
            {
                bool reverse = false;
                foreach (var name in c.KeywordNames)
                {
                    if (name != "reverse" && name != "key") throw Ops.TypeError("sort() got an unexpected keyword argument '" + name + "'");
                }
                if (c.TryGetKeyword(a, "reverse", out Value r)) reverse = r.IsTruthy;
                c.TryGetKeyword(a, "key", out Value key);
                var list = a[0].AsList();
                var sorted = SortValues(list.Items, reverse, KeysFor(c, key, list.Items));
                list.Items.Clear();
                list.Items.AddRange(sorted);
                return Value.None;
            }, true);
            Add(t, "list", "reverse", 0, 0, (c, a) => { a[0].AsList().Items.Reverse(); return Value.None; });
            Add(t, "list", "clear", 0, 0, (c, a) => { a[0].AsList().Items.Clear(); return Value.None; });
            Add(t, "list", "copy", 0, 0, (c, a) => Value.FromList(new PyList(a[0].AsList().Items)));
        }

        /// <summary>
        /// Calls the key function on every item, in order, or returns null when there is no key function. The result
        /// is what the items are compared by.
        /// </summary>
        public static Value[] KeysFor(CallContext ctx, Value key, IReadOnlyList<Value> items)
        {
            if (key.IsNone) return null;
            var keys = new Value[items.Count];
            for (int i = 0; i < keys.Length; i++) keys[i] = ctx.Call(key, items[i]);
            return keys;
        }

        /// <summary>
        /// A stable sort using Python ordering. When <paramref name="keys"/> is given, items are ordered by the key at
        /// the same position instead of by themselves. Raises TypeError for values that cannot be compared.
        /// </summary>
        public static List<Value> SortValues(IEnumerable<Value> items, bool reverse, Value[] keys = null)
        {
            try
            {
                return SortCore(items, reverse, keys);
            }
            catch (InvalidOperationException e) when (e.InnerException is PyError inner)
            {
                // LINQ wraps exceptions thrown by the comparer; scripts should see the TypeError itself.
                throw inner;
            }
        }

        private static List<Value> SortCore(IEnumerable<Value> items, bool reverse, Value[] keys)
        {
            var list = items.ToList();
            if (list.Count < 2) return list;
            if (keys == null) keys = list.ToArray();
            var comparer = Comparer<int>.Create((x, y) =>
                Ops.Compare(keys[x], keys[y], Ops.Order.Lt) ? -1 : Ops.Compare(keys[y], keys[x], Ops.Order.Lt) ? 1 : 0);
            var order = Enumerable.Range(0, list.Count).ToList();
            if (reverse)
            {
                // reverse=True keeps equal elements in their original order, like Python.
                order.Reverse();
                order = order.OrderBy(v => v, comparer).ToList();
                order.Reverse();
            }
            else order = order.OrderBy(v => v, comparer).ToList();
            return order.Select(i => list[i]).ToList();
        }

        // ---- dict -----------------------------------------------------------------------------------------------

        private static void InstallDict()
        {
            var t = DictMethods;
            Add(t, "dict", "get", 1, 2, (c, a) =>
                a[0].AsDict().TryGetValue(a[1], out Value v) ? v : (a.Length > 2 ? a[2] : Value.None));
            Add(t, "dict", "keys", 0, 0, (c, a) => Value.FromDictView(new PyDictView(a[0].AsDict(), DictViewKind.Keys)));
            Add(t, "dict", "values", 0, 0, (c, a) => Value.FromDictView(new PyDictView(a[0].AsDict(), DictViewKind.Values)));
            Add(t, "dict", "items", 0, 0, (c, a) => Value.FromDictView(new PyDictView(a[0].AsDict(), DictViewKind.Items)));
            Add(t, "dict", "pop", 1, 2, (c, a) =>
            {
                var d = a[0].AsDict();
                if (d.TryGetValue(a[1], out Value v))
                {
                    d.Remove(a[1]);
                    return v;
                }
                if (a.Length > 2) return a[2];
                throw new PyError("KeyError", Ops.Repr(a[1]));
            });
            Add(t, "dict", "setdefault", 1, 2, (c, a) =>
            {
                var d = a[0].AsDict();
                if (d.TryGetValue(a[1], out Value v)) return v;
                Value def = a.Length > 2 ? a[2] : Value.None;
                d.Set(a[1], def);
                return def;
            });
            Add(t, "dict", "update", 0, 1, (c, a) =>
            {
                var d = a[0].AsDict();
                if (c.PositionalCount > 1) UpdateDict(d, a[1]);
                for (int i = 0; i < c.KeywordNames.Length; i++)
                    d.Set(Value.FromString(c.KeywordNames[i]), a[c.PositionalCount + i]);
                return Value.None;
            }, true);
            Add(t, "dict", "clear", 0, 0, (c, a) => { a[0].AsDict().Clear(); return Value.None; });
            Add(t, "dict", "copy", 0, 0, (c, a) =>
            {
                var copy = a[0].AsDict().EmptyLike();
                foreach (var kv in a[0].AsDict().Pairs()) copy.Set(kv.Key, kv.Value);
                return Value.FromDict(copy);
            });
        }

        // ---- set ------------------------------------------------------------------------------------------------

        /// <summary>The members of a set or of any iterable, as a set (the set itself when it already is one).</summary>
        public static PySet ToSet(Value iterable)
        {
            if (iterable.Kind == ValueKind.Set) return (PySet)iterable.RawObject;
            var set = new PySet();
            foreach (var v in Ops.ToList(iterable)) set.Add(v);
            return set;
        }

        // The methods of generators; on other iterators only __next__ and __iter__ make sense. The VM runs
        // __next__ and send as frames when it can (see PyTask.TryStepGenerator), so these bodies are the fallback
        // for calls made by native code, which cannot wait.
        private static void InstallIterator()
        {
            var t = IteratorMethods;
            Add(t, "generator", "__next__", 0, 0, (c, a) => StepIterator(a[0], Value.None, "__next__"));
            Add(t, "generator", "send", 1, 1, (c, a) => StepIterator(a[0], a[1], "send"));
            // Run as frames by PyTask.ThrowIntoGenerator when called on a generator; the bodies below serve other iterators.
            Add(t, "generator", "throw", 1, 2, (c, a) => throw new PyError("AttributeError", "'iterator' object has no attribute 'throw'"));
            Add(t, "generator", "close", 0, 0, (c, a) =>
            {
                if (!(a[0].RawObject is PyGenerator g)) throw new PyError("AttributeError", "'iterator' object has no attribute 'close'");
                g.Close();
                return Value.None;
            });
            Add(t, "generator", "__iter__", 0, 0, (c, a) => a[0]);
            t["__next__"].StepKind = 3;
            t["send"].StepKind = 2;
            t["throw"].StepKind = 4;
            t["close"].StepKind = 5;
        }

        private static Value StepIterator(Value iterator, Value sent, string method)
        {
            // Generators never get here: the VM resumes them itself (see PyTask.TryStepGenerator).
            var it = (PyIterator)iterator.RawObject;
            if (method == "send") throw new PyError("AttributeError", "'iterator' object has no attribute 'send'");
            if (it.Next(out Value value)) return value;
            throw new PyError("StopIteration", "");
        }

        private static void InstallSet()
        {
            var t = SetMethods;
            Add(t, "set", "add", 1, 1, (c, a) => { a[0].AsSet().Add(a[1]); return Value.None; });
            Add(t, "set", "remove", 1, 1, (c, a) =>
            {
                if (!a[0].AsSet().Remove(a[1])) throw new PyError("KeyError", Ops.Repr(a[1]));
                return Value.None;
            });
            Add(t, "set", "discard", 1, 1, (c, a) => { a[0].AsSet().Remove(a[1]); return Value.None; });
            Add(t, "set", "pop", 0, 0, (c, a) =>
            {
                var s = a[0].AsSet();
                using (var e = s.Members().GetEnumerator())
                {
                    if (!e.MoveNext()) throw new PyError("KeyError", "'pop from an empty set'");
                    var v = e.Current;
                    s.Remove(v);
                    return v;
                }
            });
            Add(t, "set", "clear", 0, 0, (c, a) => { a[0].AsSet().Items.Clear(); return Value.None; });
            Add(t, "set", "copy", 0, 0, (c, a) => Value.FromSet(a[0].AsSet().Copy().LikeOf(a[0].AsSet())));
            Add(t, "set", "update", 0, -1, (c, a) =>
            {
                var s = a[0].AsSet();
                for (int i = 1; i < a.Length; i++) foreach (var v in Ops.ToList(a[i])) s.Add(v);
                return Value.None;
            });
            Add(t, "set", "union", 0, -1, (c, a) =>
            {
                var result = a[0].AsSet().Copy().LikeOf(a[0].AsSet());
                for (int i = 1; i < a.Length; i++) foreach (var v in Ops.ToList(a[i])) result.Add(v);
                return Value.FromSet(result);
            });
            Add(t, "set", "intersection", 0, -1, (c, a) =>
            {
                var result = a[0].AsSet().Copy();
                for (int i = 1; i < a.Length; i++) result = SetIntersection(result, ToSet(a[i]));
                return Value.FromSet(result.LikeOf(a[0].AsSet()));
            });
            Add(t, "set", "difference", 0, -1, (c, a) =>
            {
                var result = a[0].AsSet().Copy();
                for (int i = 1; i < a.Length; i++) result = Ops.SetDifference(result, ToSet(a[i]));
                return Value.FromSet(result.LikeOf(a[0].AsSet()));
            });
            Add(t, "set", "symmetric_difference", 1, 1, (c, a) =>
                Value.FromSet(SetSymmetricDifference(a[0].AsSet(), ToSet(a[1])).LikeOf(a[0].AsSet())));
            Add(t, "set", "intersection_update", 0, -1, (c, a) =>
            {
                var s = a[0].AsSet();
                for (int i = 1; i < a.Length; i++) ReplaceMembers(s, SetIntersection(s, ToSet(a[i])));
                return Value.None;
            });
            Add(t, "set", "difference_update", 0, -1, (c, a) =>
            {
                var s = a[0].AsSet();
                for (int i = 1; i < a.Length; i++) ReplaceMembers(s, Ops.SetDifference(s, ToSet(a[i])));
                return Value.None;
            });
            Add(t, "set", "symmetric_difference_update", 1, 1, (c, a) =>
            {
                var s = a[0].AsSet();
                ReplaceMembers(s, SetSymmetricDifference(s, ToSet(a[1])));
                return Value.None;
            });
            Add(t, "set", "issubset", 1, 1, (c, a) => Value.FromBool(Ops.IsSubset(a[0].AsSet(), ToSet(a[1]))));
            Add(t, "set", "issuperset", 1, 1, (c, a) => Value.FromBool(Ops.IsSubset(ToSet(a[1]), a[0].AsSet())));
            Add(t, "set", "isdisjoint", 1, 1, (c, a) =>
            {
                var x = a[0].AsSet();
                foreach (var v in Ops.ToList(a[1])) if (x.Contains(v)) return Value.False;
                return Value.True;
            });
        }

        internal static PySet SetIntersection(PySet x, PySet y)
        {
            var result = new PySet();
            foreach (var v in x.Members()) if (y.Contains(v)) result.Add(v);
            return result;
        }

        internal static PySet SetSymmetricDifference(PySet x, PySet y)
        {
            var result = new PySet();
            foreach (var v in x.Members()) if (!y.Contains(v)) result.Add(v);
            foreach (var v in y.Members()) if (!x.Contains(v)) result.Add(v);
            return result;
        }

        // Rewrites a set in place so that references to it (and iterators over it) see the new members.
        internal static void ReplaceMembers(PySet target, PySet members)
        {
            if (ReferenceEquals(target, members)) return;
            var keep = members.Members().ToList();
            target.Items.Clear();
            foreach (var v in keep) target.Add(v);
        }

        /// <summary>Copies the entries of a dict, or of an iterable of key/value pairs, into <paramref name="target"/>.</summary>
        public static void UpdateDict(PyDict target, Value source)
        {
            if (source.Kind == ValueKind.Dict)
            {
                foreach (var kv in ((PyDict)source.RawObject).Pairs().ToList()) target.Set(kv.Key, kv.Value);
                return;
            }
            int index = 0;
            foreach (var item in Ops.ToList(source))
            {
                List<Value> pair;
                try { pair = Ops.ToList(item); }
                catch (PyError) { throw Ops.TypeError("cannot convert dictionary update sequence element #" + index + " to a sequence"); }
                if (pair.Count != 2)
                    throw Ops.ValueError("dictionary update sequence element #" + index + " has length " + pair.Count + "; 2 is required");
                target.Set(pair[0], pair[1]);
                index++;
            }
        }

        // ---- tuple ----------------------------------------------------------------------------------------------

        private static void InstallTuple()
        {
            var t = TupleMethods;
            Add(t, "tuple", "index", 1, 3, (c, a) =>
            {
                var items = a[0].AsTuple().Items;
                Range(a, items.Length, out int from, out int to);
                for (int i = from; i < to; i++) if (Ops.Equal(items[i], a[1])) return Value.FromInt(i);
                throw Ops.ValueError("tuple.index(x): x not in tuple");
            });
            Add(t, "tuple", "count", 1, 1, (c, a) =>
            {
                long n = 0;
                foreach (var v in a[0].AsTuple().Items) if (Ops.Equal(v, a[1])) n++;
                return Value.FromInt(n);
            });
        }
    }
}
