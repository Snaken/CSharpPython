using System;
using System.Collections.Generic;
using System.Numerics;

namespace CSharpPython
{
    /// <summary>
    /// The <c>random</c> module, following CPython's <c>random.py</c> so that a seeded script gets the same numbers.
    /// The generator belongs to the running task (see <see cref="RandomState"/>), not to the module, because the module
    /// is shared by every task of a VM.
    /// </summary>
    internal static class RandomModule
    {
        private const double NvMagicConst = 1.7155277699214135;   // 4 * exp(-0.5) / sqrt(2.0)
        private const double TwoPi = 2.0 * Math.PI;

        // Used when a value is created outside any task, such as by a host calling a native function directly.
        [ThreadStatic] private static RandomState _fallback;

        private static RandomState State
        {
            get
            {
                var task = PyTask.Current;
                if (task == null) return _fallback ?? (_fallback = new RandomState());
                return task.Random ?? (task.Random = new RandomState());
            }
        }

        public static PyModule Create()
        {
            var m = new PyModule("random");
            void Add(string name, string[] names, int required, Func<RandomState, Value[], Value> fn) =>
                m.Members[name] = Value.FromNative(new NativeFunction(name, (c, a) => fn(State, Bind(c, a, name, names, required)), 0, names.Length, true));

            Add("seed", new[] { "a", "version" }, 0, (s, p) =>
            {
                if (p[0].Kind == ValueKind.Unbound || p[0].IsNone) s.SeedFromEntropy();
                else if (p[0].IsInteger) s.Seed(Ops.ToBig(p[0]));
                else throw Ops.TypeError("seed() supports only None and int seeds, not '" + p[0].TypeName + "'");
                s.HasGauss = false;
                return Value.None;
            });
            Add("random", new string[0], 0, (s, p) => Value.FromFloat(s.NextDouble()));
            Add("getrandbits", new[] { "k" }, 1, (s, p) =>
            {
                var k = MathExtras.IntArg(p[0]);
                if (k.Sign < 0) throw Ops.ValueError("number of bits must be non-negative");
                if (k > Ops.MaxIntBits) throw Ops.IntTooLarge();
                return Ops.BigResult(s.GetRandBits((long)k));
            });
            Add("randrange", new[] { "start", "stop", "step" }, 1, RandRange);
            Add("randint", new[] { "a", "b" }, 2, (s, p) =>
            {
                var a = MathExtras.IntArg(p[0]);
                var b = MathExtras.IntArg(p[1]);
                if (b < a) throw Ops.ValueError("empty range in randint(" + a + ", " + b + ")");
                return Ops.BigResult(a + s.RandBelow(b - a + 1));
            });
            Add("choice", new[] { "seq" }, 1, (s, p) =>
            {
                long n = Ops.Length(p[0]);
                if (n == 0) throw new PyError("IndexError", "Cannot choose from an empty sequence");
                return Ops.GetItem(p[0], Value.FromInt((long)s.RandBelow(n)));
            });
            Add("shuffle", new[] { "x" }, 1, (s, p) =>
            {
                if (p[0].Kind != ValueKind.List)
                {
                    if (Ops.Length(p[0]) <= 1) return Value.None;
                    throw Ops.TypeError("'" + p[0].TypeName + "' object does not support item assignment");
                }
                var items = p[0].AsList().Items;
                for (int i = items.Count - 1; i >= 1; i--)
                {
                    int j = (int)s.RandBelow(i + 1);
                    var tmp = items[i];
                    items[i] = items[j];
                    items[j] = tmp;
                }
                return Value.None;
            });
            Add("sample", new[] { "population", "k", "counts" }, 2, Sample);
            Add("choices", new[] { "population", "weights", "cum_weights", "k" }, 1, Choices);
            Add("uniform", new[] { "a", "b" }, 2, (s, p) =>
            {
                double a = p[0].AsFloat(), b = p[1].AsFloat();
                return Value.FromFloat(a + (b - a) * s.NextDouble());
            });
            Add("triangular", new[] { "low", "high", "mode" }, 0, (s, p) =>
            {
                double low = Number(p[0], 0.0), high = Number(p[1], 1.0);
                double u = s.NextDouble();
                double c = 0.5;
                if (p[2].Kind != ValueKind.Unbound && !p[2].IsNone)
                {
                    double width = high - low;
                    if (MathExtras.Exactly(width, 0.0)) return Value.FromFloat(low);
                    c = (p[2].AsFloat() - low) / width;
                }
                if (u > c)
                {
                    u = 1.0 - u;
                    c = 1.0 - c;
                    double t = low;
                    low = high;
                    high = t;
                }
                return Value.FromFloat(low + (high - low) * Math.Sqrt(u * c));
            });
            Add("normalvariate", new[] { "mu", "sigma" }, 0, (s, p) => Value.FromFloat(Normal(s, Number(p[0], 0.0), Number(p[1], 1.0))));
            Add("gauss", new[] { "mu", "sigma" }, 0, (s, p) =>
            {
                double mu = Number(p[0], 0.0), sigma = Number(p[1], 1.0);
                double z;
                if (s.HasGauss)
                {
                    z = s.GaussNext;
                    s.HasGauss = false;
                }
                else
                {
                    double x2pi = s.NextDouble() * TwoPi;
                    double g2rad = Math.Sqrt(-2.0 * Math.Log(1.0 - s.NextDouble()));
                    z = Math.Cos(x2pi) * g2rad;
                    s.GaussNext = Math.Sin(x2pi) * g2rad;
                    s.HasGauss = true;
                }
                return Value.FromFloat(mu + z * sigma);
            });
            Add("lognormvariate", new[] { "mu", "sigma" }, 2, (s, p) => Value.FromFloat(Math.Exp(Normal(s, p[0].AsFloat(), p[1].AsFloat()))));
            Add("expovariate", new[] { "lambd" }, 0, (s, p) =>
            {
                double lambd = Number(p[0], 1.0);
                double r = -Math.Log(1.0 - s.NextDouble());
                if (MathExtras.Exactly(lambd, 0.0)) throw new PyError("ZeroDivisionError", "division by zero");
                return Value.FromFloat(r / lambd);
            });
            return m;
        }

        private static double Number(Value v, double fallback) => v.Kind == ValueKind.Unbound ? fallback : v.AsFloat();

        private static double Normal(RandomState s, double mu, double sigma)
        {
            double z;
            while (true)
            {
                double u1 = s.NextDouble();
                double u2 = 1.0 - s.NextDouble();
                z = NvMagicConst * (u1 - 0.5) / u2;
                double zz = z * z / 4.0;
                if (zz <= -Math.Log(u2)) break;
            }
            return mu + z * sigma;
        }

        // Matches the arguments of a call to the parameter names, allowing each to be given by position or keyword.
        // A parameter that was not given is left as Unbound.
        private static Value[] Bind(CallContext ctx, ReadOnlySpan<Value> a, string function, string[] names, int required)
        {
            var result = new Value[names.Length];
            for (int i = 0; i < names.Length; i++) result[i] = Value.Unbound;
            for (int i = 0; i < ctx.PositionalCount && i < names.Length; i++) result[i] = a[i];
            foreach (var name in ctx.KeywordNames)
            {
                int index = Array.IndexOf(names, name);
                if (index < 0) throw Ops.TypeError(function + "() got an unexpected keyword argument '" + name + "'");
                if (result[index].Kind != ValueKind.Unbound) throw Ops.TypeError(function + "() got multiple values for argument '" + name + "'");
                ctx.TryGetKeyword(a, name, out result[index]);
            }
            for (int i = 0; i < required; i++)
                if (result[i].Kind == ValueKind.Unbound) throw Ops.TypeError(function + "() missing required argument: '" + names[i] + "'");
            return result;
        }

        private static Value RandRange(RandomState s, Value[] p)
        {
            var start = MathExtras.IntArg(p[0]);
            if (p[1].Kind == ValueKind.Unbound || p[1].IsNone)
            {
                if (p[2].Kind != ValueKind.Unbound && !MathExtras.IntArg(p[2]).IsOne) throw Ops.TypeError("Missing a non-None stop argument");
                if (start.Sign > 0) return Ops.BigResult(s.RandBelow(start));
                throw Ops.ValueError("empty range for randrange()");
            }
            var stop = MathExtras.IntArg(p[1]);
            var width = stop - start;
            var step = p[2].Kind == ValueKind.Unbound ? BigInteger.One : MathExtras.IntArg(p[2]);
            if (step.IsOne)
            {
                if (width.Sign > 0) return Ops.BigResult(start + s.RandBelow(width));
                throw Ops.ValueError("empty range in randrange(" + start + ", " + stop + ")");
            }
            BigInteger n;
            if (step.Sign > 0) n = FloorDivide(width + step - 1, step);
            else if (step.Sign < 0) n = FloorDivide(width + step + 1, step);
            else throw Ops.ValueError("zero step for randrange()");
            if (n.Sign <= 0) throw Ops.ValueError("empty range in randrange(" + start + ", " + stop + ", " + step + ")");
            return Ops.BigResult(start + step * s.RandBelow(n));
        }

        private static BigInteger FloorDivide(BigInteger a, BigInteger b)
        {
            var q = BigInteger.DivRem(a, b, out BigInteger remainder);
            return !remainder.IsZero && (remainder.Sign < 0) != (b.Sign < 0) ? q - 1 : q;
        }

        // The items of a list, tuple, range or str, read by position without copying a range.
        private static Func<long, Value> Sequence(Value population, out long count)
        {
            switch (population.Kind)
            {
                case ValueKind.List:
                {
                    var items = population.AsList().Items;
                    count = items.Count;
                    return i => items[(int)i];
                }
                case ValueKind.Tuple:
                {
                    var items = ((PyTuple)population.RawObject).Items;
                    count = items.Length;
                    return i => items[i];
                }
                case ValueKind.Range:
                {
                    var range = (PyRange)population.RawObject;
                    count = range.Length;
                    return i => Value.FromInt(range[i]);
                }
                case ValueKind.Str:
                {
                    string text = population.AsString();
                    count = text.Length;
                    return i => Value.FromString(text[(int)i].ToString());
                }
                default:
                    throw Ops.TypeError("Population must be a sequence.  For dicts or sets, use sorted(d).");
            }
        }

        private static Value Sample(RandomState s, Value[] p)
        {
            var at = Sequence(p[0], out long n);
            if (p[2].Kind != ValueKind.Unbound && !p[2].IsNone) throw Ops.TypeError("sample() does not support counts");
            var kBig = MathExtras.IntArg(p[1]);
            if (kBig.Sign < 0 || kBig > n) throw Ops.ValueError("Sample larger than population or is negative");
            long k = (long)kBig;
            var result = new List<Value>((int)Math.Min(k, 1024));
            long setSize = 21;
            if (k > 5) setSize += (long)Math.Pow(4, Math.Ceiling(Math.Log(k * 3.0) / Math.Log(4)));
            if (n <= setSize)
            {
                var pool = new Value[n];
                for (long i = 0; i < n; i++) pool[i] = at(i);
                for (long i = 0; i < k; i++)
                {
                    long j = (long)s.RandBelow(n - i);
                    result.Add(pool[j]);
                    pool[j] = pool[n - i - 1];
                }
            }
            else
            {
                var selected = new HashSet<long>();
                for (long i = 0; i < k; i++)
                {
                    long j = (long)s.RandBelow(n);
                    while (selected.Contains(j)) j = (long)s.RandBelow(n);
                    selected.Add(j);
                    result.Add(at(j));
                }
            }
            return Value.FromList(new PyList(result));
        }

        private static Value Choices(RandomState s, Value[] p)
        {
            var population = p[0];
            long n = Ops.Length(population);
            long k = p[3].Kind == ValueKind.Unbound ? 1 : (long)MathExtras.IntArg(p[3]);
            if (k > Ops.MaxSequenceLength) throw new PyError("MemoryError", "sequence too long");
            var result = new List<Value>((int)Math.Max(0, Math.Min(k, 1024)));
            bool hasWeights = p[1].Kind != ValueKind.Unbound && !p[1].IsNone;
            bool hasCumulative = p[2].Kind != ValueKind.Unbound && !p[2].IsNone;
            if (!hasWeights && !hasCumulative)
            {
                double size = n;
                for (long i = 0; i < k; i++) result.Add(Ops.GetItem(population, Value.FromInt((long)Math.Floor(s.NextDouble() * size))));
                return Value.FromList(new PyList(result));
            }
            if (hasWeights && hasCumulative) throw Ops.TypeError("Cannot specify both weights and cumulative weights");

            var cumulative = new List<double>();
            if (hasCumulative)
            {
                foreach (var w in Ops.ToList(p[2])) cumulative.Add(w.AsFloat());
            }
            else
            {
                double running = 0;
                foreach (var w in Ops.ToList(p[1]))
                {
                    running += w.AsFloat();
                    cumulative.Add(running);
                }
            }
            if (cumulative.Count != n) throw Ops.ValueError("The number of weights does not match the population");
            double total = cumulative.Count == 0 ? throw new PyError("IndexError", "list index out of range") : cumulative[cumulative.Count - 1];
            if (total <= 0.0) throw Ops.ValueError("Total of weights must be greater than zero");
            if (double.IsInfinity(total) || double.IsNaN(total)) throw Ops.ValueError("Total of weights must be finite");
            int hi = (int)n - 1;
            for (long i = 0; i < k; i++)
            {
                double x = s.NextDouble() * total;
                int lo = 0, top = hi;
                while (lo < top)
                {
                    int mid = (lo + top) / 2;
                    if (x < cumulative[mid]) top = mid;
                    else lo = mid + 1;
                }
                result.Add(Ops.GetItem(population, Value.FromInt(lo)));
            }
            return Value.FromList(new PyList(result));
        }
    }
}
