using System;
using System.Collections.Generic;
using System.Numerics;

namespace CSharpPython
{
    /// <summary>The less common functions of the <c>math</c> module: integer maths, summation and the hyperbolic functions.</summary>
    internal static class MathExtras
    {
        private static PyError DomainError() => Ops.ValueError("math domain error");

        private static PyError RangeError() => new PyError("OverflowError", "math range error");

        private static BigInteger IntArg(Value v)
        {
            v = Ops.IndexValue(v);
            if (!v.IsInteger) throw Ops.TypeError("'" + v.TypeName + "' object cannot be interpreted as an integer");
            return Ops.ToBig(v);
        }

        private static BigInteger NonNegative(Value v, string name)
        {
            var n = IntArg(v);
            if (n.Sign < 0) throw Ops.ValueError(name + " must be a non-negative integer");
            return n;
        }

        // Multiplies without letting the result outgrow the VM's integer size limit.
        private static BigInteger Checked(BigInteger b)
        {
            if (Ops.BitLength(b) > Ops.MaxIntBits) throw Ops.IntTooLarge();
            return b;
        }

        public static void Install(PyModule m)
        {
            void Add(string name, int min, int max, NativeFn fn, bool keywords = false) =>
                m.Members[name] = Value.FromNative(new NativeFunction(name, fn, min, max, keywords));
            void One(string name, Func<double, double> f) =>
                Add(name, 1, 1, (c, a) => Value.FromFloat(f(a[0].AsFloat())));

            Add("isfinite", 1, 1, (c, a) => { double d = a[0].AsFloat(); return Value.FromBool(!double.IsNaN(d) && !double.IsInfinity(d)); });
            Add("isclose", 2, 2, IsClose, true);
            Add("copysign", 2, 2, (c, a) => Value.FromFloat(CopySign(a[0].AsFloat(), a[1].AsFloat())));
            Add("fmod", 2, 2, (c, a) =>
            {
                double x = a[0].AsFloat(), y = a[1].AsFloat();
                if (double.IsInfinity(x) && !double.IsNaN(y) || y == 0 && !double.IsNaN(x)) throw DomainError();
                return Value.FromFloat(x % y);
            });
            Add("remainder", 2, 2, (c, a) =>
            {
                double x = a[0].AsFloat(), y = a[1].AsFloat();
                if (double.IsNaN(x) || double.IsNaN(y)) return Value.FromFloat(double.NaN);
                if (double.IsInfinity(x) || y == 0) throw DomainError();
                if (double.IsInfinity(y)) return Value.FromFloat(x);
                return Value.FromFloat(Math.IEEERemainder(x, y));
            });
            Add("modf", 1, 1, (c, a) =>
            {
                double x = a[0].AsFloat();
                if (double.IsInfinity(x)) return Value.Tuple(Value.FromFloat(CopySign(0.0, x)), Value.FromFloat(x));
                double whole = Math.Truncate(x);
                return Value.Tuple(Value.FromFloat(CopySign(x - whole, x)), Value.FromFloat(whole));
            });
            Add("frexp", 1, 1, (c, a) =>
            {
                double x = a[0].AsFloat();
                if (x == 0 || double.IsNaN(x) || double.IsInfinity(x)) return Value.Tuple(Value.FromFloat(x), Value.FromInt(0));
                int shift = 0;
                if (Math.Abs(x) < 2.2250738585072014E-308) { x *= 18014398509481984.0; shift = -54; }   // subnormal: scale by 2^54 first
                long bits = BitConverter.DoubleToInt64Bits(x);
                int exp = (int)((bits >> 52) & 0x7FF) - 1022;
                bits = (bits & unchecked((long)0x800FFFFFFFFFFFFFUL)) | (1022L << 52);
                return Value.Tuple(Value.FromFloat(BitConverter.Int64BitsToDouble(bits)), Value.FromInt(exp + shift));
            });
            Add("ldexp", 2, 2, (c, a) =>
            {
                double x = a[0].AsFloat();
                var e = IntArg(a[1]);
                if (x == 0 || double.IsNaN(x) || double.IsInfinity(x)) return Value.FromFloat(x);
                if (e > 100000) throw RangeError();
                if (e < -100000) return Value.FromFloat(CopySign(0.0, x));
                double r = ScaleB(x, (int)e);
                if (double.IsInfinity(r)) throw RangeError();
                return Value.FromFloat(r);
            });

            One("cbrt", Math.Cbrt);
            One("exp2", x => Checked(Math.Pow(2, x)));
            One("expm1", Expm1);
            One("log1p", Log1p);
            One("sinh", x => Checked(Math.Sinh(x), x));
            One("cosh", x => Checked(Math.Cosh(x), x));
            One("tanh", Math.Tanh);
            One("asinh", Math.Asinh);
            One("acosh", x => x < 1 ? throw DomainError() : Math.Acosh(x));
            One("atanh", x => Math.Abs(x) >= 1 ? throw DomainError() : Math.Atanh(x));

            Add("factorial", 1, 1, (c, a) =>
            {
                var n = IntArg(a[0]);
                if (n.Sign < 0) throw Ops.ValueError("factorial() not defined for negative values");
                var result = BigInteger.One;
                for (BigInteger i = 2; i <= n; i++) result = Checked(result * i);
                return Ops.BigResult(result);
            });
            Add("gcd", 0, -1, (c, a) =>
            {
                var g = BigInteger.Zero;
                for (int i = 0; i < a.Length; i++) g = BigInteger.GreatestCommonDivisor(g, IntArg(a[i]));
                return Ops.BigResult(g);
            });
            Add("lcm", 0, -1, (c, a) =>
            {
                var l = BigInteger.One;
                for (int i = 0; i < a.Length; i++)
                {
                    var n = BigInteger.Abs(IntArg(a[i]));
                    if (n.IsZero) { l = BigInteger.Zero; continue; }
                    if (l.IsZero) continue;
                    l = Checked(l / BigInteger.GreatestCommonDivisor(l, n) * n);
                }
                return Ops.BigResult(l);
            });
            Add("isqrt", 1, 1, (c, a) =>
            {
                var n = IntArg(a[0]);
                if (n.Sign < 0) throw Ops.ValueError("isqrt() argument must be nonnegative");
                return Ops.BigResult(ISqrt(n));
            });
            Add("comb", 2, 2, (c, a) =>
            {
                var n = NonNegative(a[0], "n");
                var k = NonNegative(a[1], "k");
                if (k > n) return Value.FromInt(0);
                if (k > n - k) k = n - k;
                var result = BigInteger.One;
                for (BigInteger i = 0; i < k; i++) result = Checked(result * (n - i) / (i + 1));
                return Ops.BigResult(result);
            });
            Add("perm", 1, 2, (c, a) =>
            {
                var n = NonNegative(a[0], "n");
                if (a.Length == 1 || a[1].IsNone) return Factorial(n);
                var k = NonNegative(a[1], "k");
                if (k > n) return Value.FromInt(0);
                var result = BigInteger.One;
                for (BigInteger i = 0; i < k; i++) result = Checked(result * (n - i));
                return Ops.BigResult(result);
            });
            Add("prod", 1, 1, Prod, true);
            Add("fsum", 1, 1, (c, a) => Value.FromFloat(FSum(Ops.ToList(a[0]))));
            Add("hypot", 0, -1, (c, a) =>
            {
                var xs = new double[a.Length];
                for (int i = 0; i < a.Length; i++) xs[i] = a[i].AsFloat();
                return Value.FromFloat(Hypot(xs));
            });
            Add("dist", 2, 2, (c, a) =>
            {
                var p = Ops.ToList(a[0]);
                var q = Ops.ToList(a[1]);
                if (p.Count != q.Count) throw Ops.ValueError("both points must have the same number of dimensions");
                var d = new double[p.Count];
                for (int i = 0; i < d.Length; i++) d[i] = p[i].AsFloat() - q[i].AsFloat();
                return Value.FromFloat(Hypot(d));
            });
        }

        // The comparisons here are deliberately exact (NaN is never equal to anything, as with ==).
        private static bool Exactly(double a, double b) => !double.IsNaN(a) && a.Equals(b);

        private static double CopySign(double magnitude, double sign) =>
            BitConverter.DoubleToInt64Bits(sign) < 0 == BitConverter.DoubleToInt64Bits(magnitude) < 0 ? magnitude : -magnitude;

        // x * 2^n, applied in steps so that the power of two itself never overflows.
        private static double ScaleB(double x, int n)
        {
            while (n > 1000) { x *= Math.Pow(2, 1000); n -= 1000; if (double.IsInfinity(x)) return x; }
            while (n < -1000) { x *= Math.Pow(2, -1000); n += 1000; if (x == 0) return x; }
            return x * Math.Pow(2, n);
        }

        private static double Expm1(double x)
        {
            double u = Math.Exp(x);
            if (double.IsInfinity(u)) return Checked(u, x);
            if (Exactly(u, 1.0)) return x;
            double um1 = u - 1.0;
            return Exactly(um1, -1.0) ? -1.0 : um1 * x / Math.Log(u);
        }

        private static double Log1p(double x)
        {
            if (x <= -1) throw DomainError();
            double u = 1.0 + x;
            return Exactly(u, 1.0) ? x : Math.Log(u) * x / (u - 1.0);
        }

        private static double Checked(double r, double x = 0)
        {
            if (double.IsInfinity(r) && !double.IsInfinity(x)) throw RangeError();
            return r;
        }

        private static Value Factorial(BigInteger n)
        {
            var result = BigInteger.One;
            for (BigInteger i = 2; i <= n; i++) result = Checked(result * i);
            return Ops.BigResult(result);
        }

        private static BigInteger ISqrt(BigInteger n)
        {
            if (n < 2) return n;
            var x = (BigInteger)Math.Sqrt((double)n);
            if (x.IsZero) x = BigInteger.One;
            while (true)
            {
                var y = (x + n / x) >> 1;
                if (BigInteger.Abs(y - x) <= 1)
                {
                    x = y;
                    break;
                }
                x = y;
            }
            while (x * x > n) x -= 1;
            while ((x + 1) * (x + 1) <= n) x += 1;
            return x;
        }

        private static Value IsClose(CallContext ctx, ReadOnlySpan<Value> a)
        {
            double rel = 1e-9, abs = 0.0;
            foreach (var name in ctx.KeywordNames)
                if (name != "rel_tol" && name != "abs_tol") throw Ops.TypeError("isclose() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "rel_tol", out Value r)) rel = r.AsFloat();
            if (ctx.TryGetKeyword(a, "abs_tol", out Value t)) abs = t.AsFloat();
            if (rel < 0 || abs < 0) throw Ops.ValueError("tolerances must be non-negative");
            double x = a[0].AsFloat(), y = a[1].AsFloat();
            if (Exactly(x, y)) return Value.FromBool(true);
            if (double.IsInfinity(x) || double.IsInfinity(y)) return Value.FromBool(false);
            double diff = Math.Abs(y - x);
            return Value.FromBool(diff <= Math.Abs(rel * y) || diff <= Math.Abs(rel * x) || diff <= abs);
        }

        private static Value Prod(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value total = Value.FromInt(1);
            foreach (var name in ctx.KeywordNames)
                if (name != "start") throw Ops.TypeError("prod() got an unexpected keyword argument '" + name + "'");
            if (ctx.TryGetKeyword(a, "start", out Value start)) total = start;
            foreach (var item in Ops.ToList(a[0])) total = Ops.Mul(total, item);
            return total;
        }

        // Adds the numbers without losing precision (Shewchuk's algorithm, as in CPython's math.fsum).
        private static double FSum(List<Value> items)
        {
            var partials = new List<double>();
            bool sawInf = false, sawNegInf = false, sawNan = false;
            foreach (var item in items)
            {
                double x = item.AsFloat();
                if (double.IsNaN(x)) { sawNan = true; continue; }
                if (double.IsInfinity(x)) { if (x > 0) sawInf = true; else sawNegInf = true; continue; }
                int used = 0;
                for (int i = 0; i < partials.Count; i++)
                {
                    double y = partials[i];
                    if (Math.Abs(x) < Math.Abs(y)) { double t = x; x = y; y = t; }
                    double hi = x + y;
                    if (double.IsInfinity(hi)) throw new PyError("OverflowError", "intermediate overflow in fsum");
                    double lo = y - (hi - x);
                    if (lo != 0) partials[used++] = lo;
                    x = hi;
                }
                partials.RemoveRange(used, partials.Count - used);
                partials.Add(x);
            }
            if (sawNan) return double.NaN;
            if (sawInf && sawNegInf) throw Ops.ValueError("-inf + inf in fsum");
            if (sawInf) return double.PositiveInfinity;
            if (sawNegInf) return double.NegativeInfinity;
            int n = partials.Count;
            if (n == 0) return 0.0;
            double sum = partials[--n];
            double lastLo = 0;
            while (n > 0)
            {
                double x = sum;
                double y = partials[--n];
                sum = x + y;
                lastLo = y - (sum - x);
                if (lastLo != 0) break;
            }
            if (n > 0 && (lastLo < 0 && partials[n - 1] < 0 || lastLo > 0 && partials[n - 1] > 0))
            {
                double y = lastLo * 2;
                double x = sum + y;
                if (Exactly(y, x - sum)) sum = x;
            }
            if (double.IsInfinity(sum)) throw new PyError("OverflowError", "intermediate overflow in fsum");
            return sum;
        }

        private static double Hypot(double[] xs)
        {
            double max = 0;
            bool nan = false;
            foreach (double x in xs)
            {
                if (double.IsInfinity(x)) return double.PositiveInfinity;
                if (double.IsNaN(x)) nan = true;
                max = Math.Max(max, Math.Abs(x));
            }
            if (nan) return double.NaN;
            if (max == 0 || xs.Length == 1) return max;
            double sum = 0, compensation = 0;
            foreach (double x in xs)
            {
                double r = x / max;
                double term = r * r;
                double y = term - compensation;
                double t = sum + y;
                compensation = t - sum - y;
                sum = t;
            }
            return Math.Sqrt(sum) * max;
        }
    }
}
