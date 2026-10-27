using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CSharpPython
{
    internal static partial class Ops
    {
        private const int MaxReprDepth = 100;

        // ---- str() and repr() -----------------------------------------------------------------------------------

        public static string Str(Value v)
        {
            if (v.Kind == ValueKind.Str) return (string)v.RawObject;
            if (v.Kind == ValueKind.Exception) return ((PyExceptionValue)v.RawObject).Message;
            if (v.Kind == ValueKind.Instance)
            {
                if (CallDunder(v, "__str__", out string text)) return text;
                var instance = (PyInstance)v.RawObject;
                // An exception class without __str__ shows its arguments, but __repr__ is not used for it.
                if (instance.Class.ExceptionRoot != null && !instance.Class.TryFind("__repr__", out _)) return ExceptionArgsText(instance);
            }
            return Repr(v);
        }

        private static PyTuple ArgsOf(PyInstance instance) =>
            instance.Fields.TryGetValue(Value.FromString("args"), out Value args) && args.Kind == ValueKind.Tuple
                ? (PyTuple)args.RawObject
                : new PyTuple(new Value[0]);

        /// <summary>What <c>str(e)</c> gives for an exception instance: nothing, the one argument, or the tuple of them.</summary>
        internal static string ExceptionArgsText(PyInstance instance)
        {
            var items = ArgsOf(instance).Items;
            if (items.Length == 0) return "";
            return items.Length == 1 ? Str(items[0]) : Repr(Value.FromTuple(new PyTuple(items)));
        }

        /// <summary>The text <c>repr</c> gives for an instance that does not define <c>__repr__</c>.</summary>
        internal static string DefaultInstanceRepr(PyInstance instance)
        {
            if (instance.Class.ExceptionRoot == null) return instance.DefaultRepr();
            var items = ArgsOf(instance).Items;
            var sb = new StringBuilder(instance.Class.Name).Append('(');
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Repr(items[i]));
            }
            return sb.Append(')').ToString();
        }

        // Runs the instance's __str__ or __repr__ if its class defines one. Outside a running script (the host
        // printing a value) there is no task to run it on, so the default text is used.
        private static bool CallDunder(Value instance, string name, out string text)
        {
            text = null;
            var task = PyTask.Current;
            if (task == null) return false;
            if (!((PyInstance)instance.RawObject).Class.TryFind(name, out Value member) || member.Kind != ValueKind.Function)
                return false;
            Value result = task.CallSync(Value.FromScriptMethod(new ScriptMethod(instance, (PyFunction)member.RawObject)), Array.Empty<Value>());
            if (result.Kind != ValueKind.Str)
                throw new PyError("TypeError", name + " returned non-string (type " + result.TypeName + ")");
            text = (string)result.RawObject;
            return true;
        }

        public static string Repr(Value v)
        {
            var sb = new StringBuilder();
            WriteRepr(sb, v, new List<object>());
            return sb.ToString();
        }

        private static void WriteRepr(StringBuilder sb, Value v, List<object> seen)
        {
            switch (v.Kind)
            {
                case ValueKind.None: sb.Append("None"); return;
                case ValueKind.NotImplemented: sb.Append("NotImplemented"); return;
                case ValueKind.Ellipsis: sb.Append("Ellipsis"); return;
                case ValueKind.Bool: sb.Append(v.RawInt != 0 ? "True" : "False"); return;
                case ValueKind.Int: sb.Append(v.RawInt.ToString(CultureInfo.InvariantCulture)); return;
                case ValueKind.BigInt: sb.Append(BigToString((System.Numerics.BigInteger)v.RawObject)); return;
                case ValueKind.Float: sb.Append(FloatRepr(v.RawFloat)); return;
                case ValueKind.Str: WriteStringRepr(sb, (string)v.RawObject); return;
                case ValueKind.List:
                    WriteSequence(sb, ((PyList)v.RawObject).Items, v.RawObject, "[", "]", seen);
                    return;
                case ValueKind.Tuple when ((PyTuple)v.RawObject).Named != null:
                {
                    var tuple = (PyTuple)v.RawObject;
                    if (seen.Contains(tuple) || seen.Count >= MaxReprDepth) { sb.Append(tuple.Named.Name).Append("(...)"); return; }
                    seen.Add(tuple);
                    sb.Append(tuple.Named.Name).Append('(');
                    for (int i = 0; i < tuple.Items.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(tuple.Named.Fields[i]).Append('=');
                        WriteRepr(sb, tuple.Items[i], seen);
                    }
                    sb.Append(')');
                    seen.RemoveAt(seen.Count - 1);
                    return;
                }
                case ValueKind.Tuple:
                {
                    var items = ((PyTuple)v.RawObject).Items;
                    WriteSequence(sb, items, v.RawObject, "(", items.Length == 1 ? ",)" : ")", seen);
                    return;
                }
                case ValueKind.Dict: WriteDict(sb, (PyDict)v.RawObject, seen); return;
                case ValueKind.Deque:
                {
                    var deque = (PyDeque)v.RawObject;
                    string close = deque.MaxLen >= 0 ? "], maxlen=" + deque.MaxLen.ToString(CultureInfo.InvariantCulture) + ")" : "])";
                    WriteSequence(sb, deque.ToList(), deque, "deque([", close, seen);
                    return;
                }
                case ValueKind.Set:
                {
                    var set = (PySet)v.RawObject;
                    if (set.Count == 0) { sb.Append(set.Frozen ? "frozenset()" : "set()"); return; }
                    if (set.Frozen) sb.Append("frozenset(");
                    WriteSequence(sb, set.Members().ToList(), set, "{", "}", seen);
                    if (set.Frozen) sb.Append(')');
                    return;
                }
                case ValueKind.DictView:
                {
                    var view = (PyDictView)v.RawObject;
                    WriteSequence(sb, view.Members(), view, view.TypeName + "([", "])", seen);
                    return;
                }
                case ValueKind.Range:
                {
                    var r = (PyRange)v.RawObject;
                    sb.Append("range(");
                    sb.Append(r.Start.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(r.Stop.ToString(CultureInfo.InvariantCulture));
                    if (r.Step != 1) sb.Append(", ").Append(r.Step.ToString(CultureInfo.InvariantCulture));
                    sb.Append(')');
                    return;
                }
                case ValueKind.Slice:
                {
                    var s = (PySlice)v.RawObject;
                    sb.Append("slice(");
                    WriteRepr(sb, s.Start, seen); sb.Append(", ");
                    WriteRepr(sb, s.Stop, seen); sb.Append(", ");
                    WriteRepr(sb, s.Step, seen); sb.Append(')');
                    return;
                }
                case ValueKind.Function: sb.Append("<function ").Append(((PyFunction)v.RawObject).Name).Append('>'); return;
                case ValueKind.Native: sb.Append("<built-in function ").Append(((NativeFunction)v.RawObject).Name).Append('>'); return;
                case ValueKind.BoundMethod:
                    sb.Append("<built-in method ").Append(((BoundMethod)v.RawObject).Function.Name).Append('>');
                    return;
                case ValueKind.Functool:
                {
                    var f = (PyFunctool)v.RawObject;
                    if (f.Mode == FunctoolMode.Partial)
                    {
                        sb.Append("functools.partial(");
                        WriteRepr(sb, f.Func, seen);
                        foreach (var a in f.Args) { sb.Append(", "); WriteRepr(sb, a, seen); }
                        for (int i = 0; i < f.KwNames.Length; i++) { sb.Append(", ").Append(f.KwNames[i]).Append('='); WriteRepr(sb, f.KwValues[i], seen); }
                        sb.Append(')');
                    }
                    else
                        sb.Append("<").Append(f.Module).Append('.').Append(f.TypeName).Append(" object at 0x")
                            .Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(f).ToString("x8")).Append('>');
                    return;
                }
                case ValueKind.Module: sb.Append("<module '").Append(((PyModule)v.RawObject).Name).Append("'>"); return;
                case ValueKind.Iterator:
                    if (v.RawObject is PyGenerator gen)
                        sb.Append("<generator object ").Append(gen.Name).Append(" at 0x")
                            .Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(gen).ToString("x8")).Append('>');
                    else if (v.RawObject is ItertoolsIterator itertools)
                        sb.Append("<itertools.").Append(itertools.TypeName).Append(" object at 0x")
                            .Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(itertools).ToString("x8")).Append('>');
                    else sb.Append("<iterator>");
                    return;
                case ValueKind.Exception:
                {
                    var e = (PyExceptionValue)v.RawObject;
                    sb.Append(e.Type.Name).Append('(');
                    if (e.Message.Length > 0) WriteStringRepr(sb, e.Message);
                    sb.Append(')');
                    return;
                }
                case ValueKind.Class: sb.Append("<class '__main__.").Append(((PyClass)v.RawObject).Name).Append("'>"); return;
                case ValueKind.Instance:
                    sb.Append(CallDunder(v, "__repr__", out string text) ? text : DefaultInstanceRepr((PyInstance)v.RawObject));
                    return;
                case ValueKind.ScriptMethod:
                {
                    var m = (ScriptMethod)v.RawObject;
                    sb.Append("<bound method ").Append(m.Function.Name).Append(" of ");
                    WriteRepr(sb, m.Self, seen);
                    sb.Append('>');
                    return;
                }
                case ValueKind.Descriptor:
                    sb.Append('<').Append(((PyDescriptor)v.RawObject).TypeName).Append(" object at 0x")
                      .Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v.RawObject).ToString("x8")).Append('>');
                    return;
                case ValueKind.Type: sb.Append(((PyType)v.RawObject).ToString()); return;
                case ValueKind.ExceptionType: sb.Append("<class '").Append(((PyExceptionType)v.RawObject).Name).Append("'>"); return;
                case ValueKind.Handle:
                    sb.Append("<handle ").Append(v.RawAux.ToString(CultureInfo.InvariantCulture)).Append('#')
                      .Append(v.RawInt.ToString(CultureInfo.InvariantCulture)).Append('>');
                    return;
                default: sb.Append("<unbound>"); return;
            }
        }

        private static void WriteSequence(StringBuilder sb, IReadOnlyList<Value> items, object owner, string open, string close, List<object> seen)
        {
            if (seen.Contains(owner) || seen.Count >= MaxReprDepth)
            {
                sb.Append(open).Append("...").Append(close == ",)" ? ")" : close);
                return;
            }
            seen.Add(owner);
            sb.Append(open);
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                WriteRepr(sb, items[i], seen);
            }
            sb.Append(close);
            seen.RemoveAt(seen.Count - 1);
        }

        private static void WriteDict(StringBuilder sb, PyDict dict, List<object> seen)
        {
            if (seen.Contains(dict) || seen.Count >= MaxReprDepth)
            {
                sb.Append(dict.Flavor == DictFlavor.Counter ? "Counter(...)" : dict.Flavor == DictFlavor.Default ? "defaultdict(..., {...})" : "{...}");
                return;
            }
            seen.Add(dict);
            if (dict.Flavor == DictFlavor.Counter && dict.Count == 0)
            {
                sb.Append("Counter()");
                seen.RemoveAt(seen.Count - 1);
                return;
            }
            if (dict.Flavor == DictFlavor.Counter) sb.Append("Counter(");
            else if (dict.Flavor == DictFlavor.Default)
            {
                sb.Append("defaultdict(");
                WriteRepr(sb, dict.Factory, seen);
                sb.Append(", ");
            }
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict.Flavor == DictFlavor.Counter ? MostCommon(dict) : dict.Pairs())
            {
                if (!first) sb.Append(", ");
                first = false;
                WriteRepr(sb, kv.Key, seen);
                sb.Append(": ");
                WriteRepr(sb, kv.Value, seen);
            }
            sb.Append('}');
            if (dict.Flavor != DictFlavor.Plain) sb.Append(')');
            seen.RemoveAt(seen.Count - 1);
        }

        private static void WriteStringRepr(StringBuilder sb, string s)
        {
            char quote = s.IndexOf('\'') >= 0 && s.IndexOf('"') < 0 ? '"' : '\'';
            sb.Append(quote);
            foreach (char c in s)
            {
                if (c == quote || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ' || c == 0x7f) sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            sb.Append(quote);
        }

        /// <summary>Formats a float the way Python's <c>repr()</c> does (shortest round-trip digits).</summary>
        public static string FloatRepr(double d)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsPositiveInfinity(d)) return "inf";
            if (double.IsNegativeInfinity(d)) return "-inf";
            if (d == 0.0) return 1.0 / d < 0 ? "-0.0" : "0.0";

            string r = d.ToString("R", CultureInfo.InvariantCulture);
            bool negative = r[0] == '-';
            if (negative) r = r.Substring(1);

            int exp10 = 0;
            int e = r.IndexOf('E');
            if (e >= 0)
            {
                exp10 = int.Parse(r.Substring(e + 1), CultureInfo.InvariantCulture);
                r = r.Substring(0, e);
            }
            int point = r.IndexOf('.');
            string digits = point >= 0 ? r.Remove(point, 1) : r;
            int decpt = (point >= 0 ? point : r.Length) + exp10;

            int lead = 0;
            while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
            digits = digits.Substring(lead);
            decpt -= lead;
            digits = digits.TrimEnd('0');
            if (digits.Length == 0) digits = "0";

            var sb = new StringBuilder();
            if (negative) sb.Append('-');
            if (decpt > 16 || decpt <= -4)
            {
                sb.Append(digits[0]);
                if (digits.Length > 1) sb.Append('.').Append(digits, 1, digits.Length - 1);
                int exp = decpt - 1;
                sb.Append('e').Append(exp < 0 ? '-' : '+').Append(Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture));
            }
            else if (decpt <= 0)
            {
                sb.Append("0.").Append('0', -decpt).Append(digits);
            }
            else if (decpt >= digits.Length)
            {
                sb.Append(digits).Append('0', decpt - digits.Length).Append(".0");
            }
            else
            {
                sb.Append(digits, 0, decpt).Append('.').Append(digits, decpt, digits.Length - decpt);
            }
            return sb.ToString();
        }

        // ---- Format specs (f-strings, format(), str.format) -----------------------------------------------------

        /// <summary>Applies a format spec such as <c>&gt;10.2f</c> to a value.</summary>
        public static string FormatValue(Value v, string spec)
        {
            if (v.Kind == ValueKind.Instance && TryCallDunder(v, "__format__", new[] { Value.FromString(spec ?? "") }, out Value formatted))
            {
                if (formatted.Kind != ValueKind.Str) throw TypeError("__format__ must return a str, not " + TypeName(formatted));
                return (string)formatted.RawObject;
            }
            if (string.IsNullOrEmpty(spec)) return Str(v);
            if (v.Kind == ValueKind.Instance) throw TypeError("unsupported format string passed to " + TypeName(v) + ".__format__");

            int i = 0;
            char fill = ' ';
            char align = '\0';
            if (spec.Length >= 2 && IsAlign(spec[1])) { fill = spec[0]; align = spec[1]; i = 2; }
            else if (spec.Length >= 1 && IsAlign(spec[0])) { align = spec[0]; i = 1; }

            char sign = '-';
            if (i < spec.Length && (spec[i] == '+' || spec[i] == '-' || spec[i] == ' ')) sign = spec[i++];
            bool alternate = false;
            if (i < spec.Length && spec[i] == '#') { alternate = true; i++; }
            if (i < spec.Length && spec[i] == '0')
            {
                if (align == '\0') { fill = '0'; align = '='; }
                i++;
            }
            long width = 0;
            bool hasWidth = false;
            while (i < spec.Length && char.IsDigit(spec[i]))
            {
                width = width * 10 + (spec[i++] - '0');
                hasWidth = true;
                if (width > MaxSequenceLength) throw ValueError("format width is too large");
            }
            char grouping = '\0';
            if (i < spec.Length && (spec[i] == ',' || spec[i] == '_')) grouping = spec[i++];
            int precision = -1;
            if (i < spec.Length && spec[i] == '.')
            {
                i++;
                int start = i;
                long p = 0;
                while (i < spec.Length && char.IsDigit(spec[i]))
                {
                    p = p * 10 + (spec[i++] - '0');
                    if (p > 1000) throw ValueError("format precision is too large");
                }
                if (i == start) throw ValueError("Format specifier missing precision");
                precision = (int)p;
            }
            char type = '\0';
            if (i < spec.Length) type = spec[i++];
            if (i < spec.Length) throw ValueError("Invalid format specifier '" + spec + "'");

            string body;
            bool numeric = false;
            if (v.Kind == ValueKind.Str || (!v.IsNumber && type == '\0') || type == 's')
            {
                if (type != '\0' && type != 's') throw ValueError("Unknown format code '" + type + "' for object of type 'str'");
                if (type == 's' && v.Kind != ValueKind.Str) throw ValueError("Unknown format code 's' for object of type '" + TypeName(v) + "'");
                body = Str(v);
                if (precision >= 0 && precision < body.Length) body = body.Substring(0, precision);
                if (align == '\0') align = '<';
            }
            else if (v.IsNumber)
            {
                numeric = true;
                body = FormatNumber(v, type, precision, sign, alternate, grouping, out string signText);
                body = signText + body;
                if (align == '\0') align = '>';
            }
            else
            {
                throw TypeError("unsupported format string passed to " + TypeName(v) + ".__format__");
            }

            if (!hasWidth || body.Length >= width) return body;
            int pad = (int)width - body.Length;
            switch (align)
            {
                case '<': return body + new string(fill, pad);
                case '^': return new string(fill, pad / 2) + body + new string(fill, pad - pad / 2);
                case '=':
                {
                    if (!numeric) throw ValueError("'=' alignment not allowed in string format specifier");
                    int signLen = body.Length > 0 && (body[0] == '-' || body[0] == '+' || body[0] == ' ') ? 1 : 0;
                    if (alternate && body.Length > signLen + 1 && body[signLen] == '0' && "xXbBoO".IndexOf(body[signLen + 1]) >= 0) signLen += 2;
                    return body.Substring(0, signLen) + new string(fill, pad) + body.Substring(signLen);
                }
                default: return new string(fill, pad) + body;
            }
        }

        private static bool IsAlign(char c) => c == '<' || c == '>' || c == '^' || c == '=';

        private static string FormatNumber(Value v, char type, int precision, char sign, bool alternate, char grouping, out string signText)
        {
            bool isInt = v.IsInteger;
            if (type == '\0') type = isInt ? 'd' : (precision >= 0 ? 'g' : 'r');

            if (isInt && (type == 'f' || type == 'F' || type == 'e' || type == 'E' || type == 'g' || type == 'G' || type == '%'))
            {
                v = Value.FromFloat(v.AsFloat());
                isInt = false;
            }

            if (isInt)
            {
                if (precision >= 0) throw ValueError("Precision not allowed in integer format specifier");
                System.Numerics.BigInteger n = ToBig(v);
                bool neg = n.Sign < 0;
                System.Numerics.BigInteger mag = System.Numerics.BigInteger.Abs(n);
                string digits;
                string prefix = "";
                switch (type)
                {
                    case 'd':
                    case 'n': digits = BigDigits(mag, 10); break;
                    case 'x': digits = BigDigits(mag, 16); prefix = alternate ? "0x" : ""; break;
                    case 'X': digits = BigDigits(mag, 16).ToUpperInvariant(); prefix = alternate ? "0X" : ""; break;
                    case 'o': digits = BigDigits(mag, 8); prefix = alternate ? "0o" : ""; break;
                    case 'b': digits = BigDigits(mag, 2); prefix = alternate ? "0b" : ""; break;
                    case 'c':
                        if (n.Sign < 0 || n > 0x10FFFF) throw new PyError("OverflowError", "%c arg not in range(0x110000)");
                        signText = "";
                        return char.ConvertFromUtf32((int)n);
                    default: throw ValueError("Unknown format code '" + type + "' for object of type 'int'");
                }
                if (grouping != '\0')
                {
                    if (type == 'n' || (grouping == ',' && type != 'd')) throw ValueError("Cannot specify '" + grouping + "' with '" + type + "'.");
                    digits = Group(digits, grouping, type == 'd' ? 3 : 4);
                }
                signText = neg ? "-" : sign == '+' ? "+" : sign == ' ' ? " " : "";
                return prefix + digits;
            }

            double d = v.AsFloat();
            bool negative = d < 0 || (d == 0 && 1.0 / d < 0);
            double abs = Math.Abs(d);
            signText = negative ? "-" : sign == '+' ? "+" : sign == ' ' ? " " : "";

            if (double.IsNaN(d))
            {
                signText = sign == '+' ? "+" : sign == ' ' ? " " : "";
                return type == 'F' || type == 'E' || type == 'G' ? "NAN" : "nan";
            }
            if (double.IsInfinity(d)) return type == 'F' || type == 'E' || type == 'G' ? "INF" : "inf";

            string text;
            switch (type)
            {
                case 'r': text = FloatRepr(abs); break;
                case 'f':
                case 'F':
                    text = abs.ToString("F" + (precision < 0 ? 6 : precision), CultureInfo.InvariantCulture);
                    if (alternate && text.IndexOf('.') < 0) text += ".";
                    break;
                case '%':
                    text = (abs * 100).ToString("F" + (precision < 0 ? 6 : precision), CultureInfo.InvariantCulture) + "%";
                    break;
                case 'e':
                case 'E':
                    text = FormatExponent(abs, precision < 0 ? 6 : precision, type == 'E');
                    if (alternate && text.IndexOf('.') < 0) text = text.Insert(text.IndexOfAny(new[] { 'e', 'E' }), ".");
                    break;
                case 'g':
                case 'G':
                    text = FormatGeneral(abs, precision < 0 ? 6 : precision, type == 'G', alternate);
                    break;
                default:
                    throw ValueError("Unknown format code '" + type + "' for object of type 'float'");
            }
            if (grouping != '\0')
            {
                int dot = text.IndexOfAny(new[] { '.', 'e', 'E', '%' });
                string whole = dot < 0 ? text : text.Substring(0, dot);
                text = Group(whole, grouping) + (dot < 0 ? "" : text.Substring(dot));
            }
            return text;
        }

        private static string Group(string digits, char separator, int size = 3)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % size == 0) sb.Append(separator);
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        private static string FormatExponent(double abs, int precision, bool upper)
        {
            string format = precision == 0 ? "0e+00" : "0." + new string('0', precision) + "e+00";
            string s = abs.ToString(format, CultureInfo.InvariantCulture);
            return upper ? s.ToUpperInvariant() : s;
        }

        private static string FormatGeneral(double abs, int precision, bool upper, bool alternate)
        {
            if (precision == 0) precision = 1;
            if (abs == 0) return alternate ? "0." + new string('0', precision - 1) : "0";

            // Rounding to 'precision' significant digits can bump the exponent (9.99 -> 10.0), so ask the formatter.
            string rounded = abs.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
            int exp = int.Parse(rounded.Substring(rounded.IndexOf('E') + 1), CultureInfo.InvariantCulture);

            string text;
            if (exp >= -4 && exp < precision)
            {
                text = abs.ToString("F" + Math.Max(0, precision - 1 - exp), CultureInfo.InvariantCulture);
                if (!alternate && text.IndexOf('.') >= 0) text = text.TrimEnd('0').TrimEnd('.');
            }
            else
            {
                text = FormatExponent(abs, precision - 1, false);
                if (!alternate)
                {
                    int e = text.IndexOf('e');
                    string mant = text.Substring(0, e);
                    if (mant.IndexOf('.') >= 0) mant = mant.TrimEnd('0').TrimEnd('.');
                    text = mant + text.Substring(e);
                }
            }
            return upper ? text.ToUpperInvariant() : text;
        }
    }
}
