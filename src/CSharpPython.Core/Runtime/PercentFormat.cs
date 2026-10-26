using System;
using System.Text;

namespace CSharpPython
{
    internal static partial class Ops
    {
        /// <summary>Implements <c>"..." % args</c>, translating each conversion to a format spec.</summary>
        public static string PercentFormat(string format, Value args)
        {
            Value[] items;
            bool mapping = false;
            if (args.Kind == ValueKind.Tuple) items = ((PyTuple)args.RawObject).Items;
            else
            {
                items = new[] { args };
                mapping = args.Kind == ValueKind.Dict || (args.Kind == ValueKind.Instance && HasDunder(args, "__getitem__"));
            }

            var sb = new StringBuilder();
            int next = 0;
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i++];
                if (c != '%') { sb.Append(c); continue; }
                if (i >= format.Length) throw ValueError("incomplete format");

                Value keyed = default;
                bool hasKey = false;
                if (format[i] == '(')
                {
                    int depth = 1, start = ++i;
                    while (i < format.Length && depth > 0)
                    {
                        if (format[i] == '(') depth++;
                        else if (format[i] == ')') depth--;
                        i++;
                    }
                    if (depth > 0) throw ValueError("incomplete format key");
                    if (!mapping) throw TypeError("format requires a mapping");
                    keyed = GetItem(args, Value.FromString(format.Substring(start, i - 1 - start)));
                    hasKey = true;
                }

                bool left = false, plus = false, space = false, alt = false, zero = false;
                for (; i < format.Length; i++)
                {
                    char f = format[i];
                    if (f == '-') left = true;
                    else if (f == '+') plus = true;
                    else if (f == ' ') space = true;
                    else if (f == '#') alt = true;
                    else if (f == '0') zero = true;
                    else break;
                }

                long width = 0;
                bool hasWidth = false;
                if (i < format.Length && format[i] == '*')
                {
                    i++;
                    width = StarArgument(items, ref next, "width");
                    if (width < 0) { left = true; width = -width; }
                    hasWidth = true;
                }
                else
                {
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                    {
                        width = width * 10 + (format[i++] - '0');
                        hasWidth = true;
                        if (width > MaxSequenceLength) throw ValueError("width too big");
                    }
                }

                long precision = -1;
                if (i < format.Length && format[i] == '.')
                {
                    i++;
                    precision = 0;
                    if (i < format.Length && format[i] == '*')
                    {
                        i++;
                        precision = Math.Max(0, StarArgument(items, ref next, "precision"));
                    }
                    else
                    {
                        while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                        {
                            precision = precision * 10 + (format[i++] - '0');
                            if (precision > 1000) throw ValueError("precision too big");
                        }
                    }
                }

                while (i < format.Length && (format[i] == 'h' || format[i] == 'l' || format[i] == 'L')) i++;
                if (i >= format.Length) throw ValueError("incomplete format");
                char type = format[i++];

                if (type == '%')
                {
                    sb.Append('%');
                    continue;
                }

                Value arg;
                if (hasKey) arg = keyed;
                else
                {
                    if (next >= items.Length) throw TypeError("not enough arguments for format string");
                    arg = items[next++];
                }

                sb.Append(PercentConvert(arg, type, i - 1, left, plus, space, alt, zero, hasWidth ? (int)width : -1, (int)precision));
            }

            if (!mapping && next < items.Length) throw TypeError("not all arguments converted during string formatting");
            return sb.ToString();
        }

        private static long StarArgument(Value[] items, ref int next, string what)
        {
            if (next >= items.Length) throw TypeError("not enough arguments for format string");
            Value v = items[next++];
            if (v.Kind != ValueKind.Int && v.Kind != ValueKind.Bool) throw TypeError("* wants int");
            return v.RawInt;
        }

        private static string PercentConvert(Value v, char type, int index, bool left, bool plus, bool space, bool alt, bool zero, int width, int precision)
        {
            switch (type)
            {
                case 's': return PercentPad(Str(v), left, width, precision);
                case 'r': return PercentPad(Repr(v), left, width, precision);
                case 'a': return PercentPad(Builtins.Ascii(Repr(v)), left, width, precision);
                case 'c':
                {
                    string text;
                    if (v.Kind == ValueKind.Str)
                    {
                        text = (string)v.RawObject;
                        if (text.Length != 1) throw TypeError("%c requires an int or a unicode character, not a string of length " + text.Length);
                    }
                    else if (v.Kind == ValueKind.Int || v.Kind == ValueKind.Bool)
                    {
                        if (v.RawInt < 0 || v.RawInt > 0x10FFFF) throw new PyError("OverflowError", "%c arg not in range(0x110000)");
                        text = char.ConvertFromUtf32((int)v.RawInt);
                    }
                    else throw TypeError("%c requires an int or a unicode character, not " + TypeName(v));
                    return PercentPad(text, left, width, -1);
                }
            }

            bool isInt = type == 'd' || type == 'i' || type == 'u' || type == 'o' || type == 'x' || type == 'X';
            bool isFloat = type == 'e' || type == 'E' || type == 'f' || type == 'F' || type == 'g' || type == 'G';
            if (!isInt && !isFloat) throw ValueError("unsupported format character '" + type + "' (0x" + ((int)type).ToString("x") + ") at index " + index);

            if (v.Kind == ValueKind.Instance)
            {
                bool decimalType = type == 'd' || type == 'i' || type == 'u';
                if (decimalType && TryCallDunder(v, "__int__", Array.Empty<Value>(), out Value asInt)) v = asInt;
                else if (isInt && TryCallDunder(v, "__index__", Array.Empty<Value>(), out Value idx)) v = idx;
                else if (isFloat && TryCallDunder(v, "__float__", Array.Empty<Value>(), out Value asFloat)) v = asFloat;
            }
            if (!v.IsNumber)
            {
                if (isFloat) throw TypeError("must be real number, not " + TypeName(v));
                string need = type == 'd' || type == 'i' || type == 'u' ? "a real number" : "an integer";
                throw TypeError("%" + type + " format: " + need + " is required, not " + TypeName(v));
            }
            if (isInt && v.Kind == ValueKind.Float)
            {
                if (type != 'd' && type != 'i' && type != 'u') throw TypeError("%" + type + " format: an integer is required, not float");
                double d = v.RawFloat;
                if (double.IsNaN(d)) throw ValueError("cannot convert float NaN to integer");
                if (double.IsInfinity(d)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
                d = Math.Truncate(d);
                if (d >= 9.2233720368547758e18 || d < -9.2233720368547758e18) throw OverflowError();
                v = Value.FromInt((long)d);
            }

            var spec = new StringBuilder();
            if (left) spec.Append('<');
            if (plus) spec.Append('+'); else if (space) spec.Append(' ');
            if (alt) spec.Append('#');
            bool padDigits = isInt && precision >= 0;
            if (zero && !left && !padDigits) spec.Append('0');
            if (width >= 0 && !padDigits) spec.Append(width);
            if (precision >= 0 && !isInt) spec.Append('.').Append(precision);
            char formatType = type == 'i' || type == 'u' ? 'd' : type;
            spec.Append(formatType);
            string body = FormatValue(v, spec.ToString());
            if (!padDigits) return body;
            return PercentPad(PadIntegerDigits(body, precision), left, width, -1);
        }

        // %.3d zero-extends the digits; the sign and any 0x/0o prefix stay in front.
        private static string PadIntegerDigits(string body, int precision)
        {
            int start = 0;
            while (start < body.Length && (body[start] == ' ' || body[start] == '-' || body[start] == '+')) start++;
            if (start + 1 < body.Length && body[start] == '0' && "xXoO".IndexOf(body[start + 1]) >= 0) start += 2;
            int digits = body.Length - start;
            return digits >= precision ? body : body.Substring(0, start) + new string('0', precision - digits) + body.Substring(start);
        }

        private static string PercentPad(string text, bool left, int width, int precision)
        {
            if (precision >= 0 && precision < text.Length) text = text.Substring(0, precision);
            if (width <= text.Length) return text;
            return left ? text.PadRight(width) : text.PadLeft(width);
        }
    }
}
