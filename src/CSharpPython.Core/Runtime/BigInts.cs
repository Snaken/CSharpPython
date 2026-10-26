using System;
using System.Globalization;
using System.Numerics;

namespace CSharpPython
{
    // Integers that do not fit in 64 bits are kept as ValueKind.BigInt. A BigInt value is always outside the range
    // of long: every result goes through BigResult (or Value.FromBig), which turns values that fit back into Int.
    internal static partial class Ops
    {
        public const int DefaultMaxIntBits = 16384;

        // CPython refuses to convert integers of more than this many digits to or from text.
        public const int MaxIntStrDigits = 4300;

        public static int MaxIntBits
        {
            get
            {
                var task = PyTask.Current;
                return task != null ? task.Vm.Options.MaxIntBits : DefaultMaxIntBits;
            }
        }

        public static PyError IntTooLarge() =>
            new PyError("OverflowError", "integer result exceeds the limit of " + MaxIntBits + " bits");

        public static PyError IndexTooLarge() =>
            new PyError("OverflowError", "cannot fit 'int' into an index-sized integer");

        public static BigInteger ToBig(Value v) => v.Kind == ValueKind.BigInt ? (BigInteger)v.RawObject : new BigInteger(v.RawInt);

        /// <summary>The number of bits in the magnitude of the value.</summary>
        public static long BitLength(Value v)
        {
            if (v.Kind == ValueKind.BigInt) return BitLength((BigInteger)v.RawObject);
            long n = v.RawInt;
            ulong m = n < 0 ? (ulong)(-(n + 1)) + 1 : (ulong)n;
            long bits = 0;
            while (m != 0) { bits++; m >>= 1; }
            return bits;
        }

        public static long BitLength(BigInteger b)
        {
            if (b.IsZero) return 0;
            byte[] bytes = BigInteger.Abs(b).ToByteArray();
            int n = bytes.Length;
            while (n > 1 && bytes[n - 1] == 0) n--;
            int top = bytes[n - 1];
            int bits = 0;
            while (top != 0) { bits++; top >>= 1; }
            return (long)(n - 1) * 8 + bits;
        }

        /// <summary>Wraps a computed integer: an Int if it fits in 64 bits, otherwise a BigInt (limited by MaxIntBits).</summary>
        public static Value BigResult(BigInteger b)
        {
            if (b >= long.MinValue && b <= long.MaxValue) return Value.FromInt((long)b);
            if (BitLength(b) > MaxIntBits) throw IntTooLarge();
            return Value.FromBig(b);
        }

        // ---- Arithmetic on integers (at least one operand is big, or a 64-bit operation overflowed) ---------------

        private static Value BigMul(Value a, Value b)
        {
            if (BitLength(a) + BitLength(b) > MaxIntBits + 1) throw IntTooLarge();
            return BigResult(ToBig(a) * ToBig(b));
        }

        private static void BigDivMod(BigInteger x, BigInteger y, out BigInteger quotient, out BigInteger remainder)
        {
            quotient = BigInteger.DivRem(x, y, out remainder);
            if (!remainder.IsZero && (remainder.Sign < 0) != (y.Sign < 0))
            {
                quotient -= BigInteger.One;
                remainder += y;
            }
        }

        private static Value BigFloorDiv(Value a, Value b)
        {
            BigInteger y = ToBig(b);
            if (y.IsZero) throw new PyError("ZeroDivisionError", "integer division or modulo by zero");
            BigDivMod(ToBig(a), y, out BigInteger q, out _);
            return BigResult(q);
        }

        private static Value BigMod(Value a, Value b)
        {
            BigInteger y = ToBig(b);
            if (y.IsZero) throw new PyError("ZeroDivisionError", "integer modulo by zero");
            BigDivMod(ToBig(a), y, out _, out BigInteger r);
            return BigResult(r);
        }

        // a ** b for integers with b >= 0.
        private static Value BigPow(Value a, Value b)
        {
            BigInteger x = ToBig(a);
            if (x.IsZero || x.IsOne) return b.Kind != ValueKind.BigInt && b.RawInt == 0 ? Value.FromInt(1) : BigResult(x);
            if (x == BigInteger.MinusOne) return Value.FromInt(b.Kind != ValueKind.BigInt && b.RawInt == 0 || ToBig(b).IsEven ? 1 : -1);
            if (b.Kind == ValueKind.BigInt) throw IntTooLarge();
            long e = b.RawInt;
            if (e == 0) return Value.FromInt(1);
            if (e >= MaxIntBits) throw IntTooLarge();
            if ((BitLength(x) - 1) * e >= MaxIntBits + 1) throw IntTooLarge();
            return BigResult(BigInteger.Pow(x, (int)e));
        }

        private static Value BigLeftShift(Value a, Value n)
        {
            if (n.Kind != ValueKind.BigInt && n.RawInt < 0 || n.Kind == ValueKind.BigInt && n.RawObject is BigInteger neg && neg.Sign < 0)
                throw ValueError("negative shift count");
            BigInteger x = ToBig(a);
            if (x.IsZero) return Value.FromInt(0);
            if (n.Kind == ValueKind.BigInt) throw IntTooLarge();
            if (BitLength(x) + n.RawInt > MaxIntBits) throw IntTooLarge();
            return BigResult(x << (int)n.RawInt);
        }

        private static Value BigRightShift(Value a, Value n)
        {
            BigInteger x = ToBig(a);
            if (n.Kind == ValueKind.BigInt)
            {
                if (((BigInteger)n.RawObject).Sign < 0) throw ValueError("negative shift count");
                return Value.FromInt(x.Sign < 0 ? -1 : 0);
            }
            if (n.RawInt < 0) throw ValueError("negative shift count");
            if (n.RawInt >= int.MaxValue) return Value.FromInt(x.Sign < 0 ? -1 : 0);
            return BigResult(x >> (int)n.RawInt);
        }

        // ---- Conversions with float -------------------------------------------------------------------------------

        private static double Pow2(int e) => BitConverter.Int64BitsToDouble((long)(e + 1023) << 52); // e in -1022..1023

        private static double Ldexp(double x, int e)
        {
            while (e > 1000 && !double.IsInfinity(x)) { x *= Pow2(1000); e -= 1000; }
            while (e < -1000 && x != 0.0) { x *= Pow2(-1000); e += 1000; }
            if (double.IsInfinity(x) || x == 0.0) return x;
            return e > 1023 ? x * Pow2(1000) * Pow2(e - 1000) : x * Pow2(e);
        }

        /// <summary>The nearest double (ties to even), or OverflowError when the value is too large for one.</summary>
        public static double BigToDouble(BigInteger b)
        {
            if (b >= long.MinValue && b <= long.MaxValue) return (double)(long)b;
            bool negative = b.Sign < 0;
            BigInteger mag = BigInteger.Abs(b);
            long bits = BitLength(mag);
            if (bits > 1024) throw new PyError("OverflowError", "int too large to convert to float");
            int shift = (int)(bits - 53);
            BigInteger top = mag >> shift;
            BigInteger rest = mag - (top << shift);
            BigInteger half = BigInteger.One << (shift - 1);
            int cmp = rest.CompareTo(half);
            if (cmp > 0 || (cmp == 0 && !top.IsEven)) top += BigInteger.One;
            double d = Ldexp((double)(long)top, shift);
            if (double.IsInfinity(d)) throw new PyError("OverflowError", "int too large to convert to float");
            return negative ? -d : d;
        }

        /// <summary>The integer part of a float as an int, raising the errors int() raises for nan and inf.</summary>
        public static Value FloatToInt(double d)
        {
            if (double.IsNaN(d)) throw ValueError("cannot convert float NaN to integer");
            if (double.IsInfinity(d)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
            d = Math.Truncate(d);
            if (d >= -9.2233720368547758e18 && d < 9.2233720368547758e18) return Value.FromInt((long)d);
            return BigResult(new BigInteger(d));
        }

        // a / b for two integers, correctly rounded even when the operands do not fit a double.
        private static Value BigTrueDivide(Value a, Value b)
        {
            BigInteger x = ToBig(a), y = ToBig(b);
            if (y.IsZero) throw new PyError("ZeroDivisionError", "division by zero");
            bool negative = (x.Sign < 0) != (y.Sign < 0);
            if (x.IsZero) return Value.FromFloat(negative ? -0.0 : 0.0);
            x = BigInteger.Abs(x);
            y = BigInteger.Abs(y);
            long shift = 55 - (BitLength(x) - BitLength(y));
            if (shift > 0) x <<= (int)shift;
            else if (shift < 0) y <<= (int)-shift;
            BigInteger q = BigInteger.DivRem(x, y, out BigInteger rest);
            q = (q << 1) | (rest.IsZero ? BigInteger.Zero : BigInteger.One); // the low bit records "inexact" so ties round correctly
            double d = Ldexp(BigToDouble(q), (int)(-shift - 1));
            if (double.IsInfinity(d)) throw new PyError("OverflowError", "integer division result too large for a float");
            return Value.FromFloat(negative ? -d : d);
        }

        // ---- Exact comparison of numbers ---------------------------------------------------------------------------

        /// <summary>Compares two numbers exactly (an int and a float are compared by value, not through a double).</summary>
        private static int CompareNumbers(Value a, Value b, out bool unordered)
        {
            unordered = false;
            bool aFloat = a.Kind == ValueKind.Float, bFloat = b.Kind == ValueKind.Float;
            if (!aFloat && !bFloat)
            {
                if (a.Kind != ValueKind.BigInt && b.Kind != ValueKind.BigInt) return a.RawInt.CompareTo(b.RawInt);
                return ToBig(a).CompareTo(ToBig(b));
            }
            if (aFloat && bFloat)
            {
                double x = a.RawFloat, y = b.RawFloat;
                if (double.IsNaN(x) || double.IsNaN(y)) { unordered = true; return 0; }
                return x.CompareTo(y);
            }
            return aFloat ? -CompareIntFloat(b, a.RawFloat, out unordered) : CompareIntFloat(a, b.RawFloat, out unordered);
        }

        private static int CompareIntFloat(Value i, double f, out bool unordered)
        {
            unordered = false;
            if (double.IsNaN(f)) { unordered = true; return 0; }
            if (double.IsInfinity(f)) return f > 0 ? -1 : 1;
            if (i.Kind != ValueKind.BigInt && i.RawInt >= -(1L << 53) && i.RawInt <= (1L << 53)) return ((double)i.RawInt).CompareTo(f);
            double floor = Math.Floor(f);
            int c = ToBig(i).CompareTo(new BigInteger(floor));
            if (c != 0) return c;
            return f > floor ? -1 : 0;
        }

        // ---- Text -------------------------------------------------------------------------------------------------

        private static PyError StrDigitLimit(string verb) =>
            ValueError("Exceeds the limit (" + MaxIntStrDigits + " digits) for integer string conversion; use sys.set_int_max_str_digits() to increase the limit");

        public static string BigToString(BigInteger b)
        {
            if (BitLength(b) > 14500) throw StrDigitLimit("convert");
            string s = b.ToString(CultureInfo.InvariantCulture);
            if (s.Length - (b.Sign < 0 ? 1 : 0) > MaxIntStrDigits) throw StrDigitLimit("convert");
            return s;
        }

        /// <summary>The digits of a non-negative integer in a base from 2 to 36.</summary>
        public static string BigDigits(BigInteger magnitude, int radix)
        {
            if (magnitude.IsZero) return "0";
            if (radix == 10) return BigToString(magnitude);
            var chars = new System.Collections.Generic.List<char>();
            while (!magnitude.IsZero)
            {
                magnitude = BigInteger.DivRem(magnitude, radix, out BigInteger digit);
                chars.Add("0123456789abcdefghijklmnopqrstuvwxyz"[(int)digit]);
            }
            chars.Reverse();
            return new string(chars.ToArray());
        }

        /// <summary>Parses digits in the given base into an integer. The caller has checked every digit.</summary>
        public static Value ParseDigits(string digits, int radix, bool negative)
        {
            if (digits.Length > MaxIntStrDigits && (radix & (radix - 1)) != 0)
                throw ValueError("Exceeds the limit (" + MaxIntStrDigits + " digits) for integer string conversion: value has " + digits.Length + " digits; use sys.set_int_max_str_digits() to increase the limit");
            BigInteger result = BigInteger.Zero;
            if (radix == 10) result = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
            else
                foreach (char ch in digits)
                    result = result * radix + (ch >= '0' && ch <= '9' ? ch - '0' : ch >= 'a' && ch <= 'z' ? ch - 'a' + 10 : ch - 'A' + 10);
            return BigResult(negative ? -result : result);
        }
    }
}
