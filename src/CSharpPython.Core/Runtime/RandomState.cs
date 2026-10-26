using System;
using System.IO;
using System.Numerics;

namespace CSharpPython
{
    /// <summary>
    /// The generator behind the <c>random</c> module: the Mersenne Twister (MT19937) that CPython uses, seeded and read
    /// the same way, so that <c>random.seed(42)</c> gives the numbers CPython gives. Each task owns one, and snapshots
    /// save it, so a restored task carries on with the same sequence.
    /// </summary>
    internal sealed class RandomState
    {
        private const int N = 624;
        private const int M = 397;

        private readonly uint[] _mt = new uint[N];
        private int _index = N + 1;

        /// <summary>The second value of the last pair made by <c>gauss</c>, which the next call returns.</summary>
        public bool HasGauss;
        public double GaussNext;

        private static int _counter;

        public RandomState()
        {
            SeedFromEntropy();
        }

        /// <summary>Seeds from the system, for a task that never called <c>random.seed</c>.</summary>
        public void SeedFromEntropy()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            long mix = DateTime.UtcNow.Ticks ^ ((long)System.Threading.Interlocked.Increment(ref _counter) << 40);
            Seed(new BigInteger(bytes.Length == 16 ? bytes : new byte[16]) ^ mix);
        }

        /// <summary>Seeds the way CPython seeds from an int: the absolute value, as 32-bit words, through <c>init_by_array</c>.</summary>
        public void Seed(BigInteger seed)
        {
            seed = BigInteger.Abs(seed);
            uint[] key;
            if (seed.IsZero)
            {
                key = new uint[] { 0 };
            }
            else
            {
                var bytes = seed.ToByteArray();   // little endian, may end with a zero sign byte
                int words = (bytes.Length + 3) / 4;
                key = new uint[words];
                for (int i = 0; i < bytes.Length; i++) key[i / 4] |= (uint)bytes[i] << (8 * (i % 4));
                while (key.Length > 1 && key[key.Length - 1] == 0) Array.Resize(ref key, key.Length - 1);
            }
            InitByArray(key);
            HasGauss = false;
        }

        private void InitGenrand(uint s)
        {
            _mt[0] = s;
            for (int i = 1; i < N; i++) _mt[i] = 1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i;
            _index = N;
        }

        private void InitByArray(uint[] key)
        {
            InitGenrand(19650218u);
            int i = 1, j = 0;
            for (int k = Math.Max(N, key.Length); k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j;
                i++;
                j++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
                if (j >= key.Length) j = 0;
            }
            for (int k = N - 1; k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i;
                i++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            }
            _mt[0] = 0x80000000u;
        }

        private void Regenerate()
        {
            int k;
            for (k = 0; k < N - M; k++) _mt[k] = _mt[k + M] ^ Twist(_mt[k], _mt[k + 1]);
            for (; k < N - 1; k++) _mt[k] = _mt[k + (M - N)] ^ Twist(_mt[k], _mt[k + 1]);
            _mt[N - 1] = _mt[M - 1] ^ Twist(_mt[N - 1], _mt[0]);
            _index = 0;
        }

        private static uint Twist(uint u, uint v) => ((u & 0x80000000u) | (v & 0x7fffffffu)) >> 1 ^ ((v & 1u) != 0 ? 0x9908b0dfu : 0u);

        public uint NextUInt32()
        {
            if (_index >= N) Regenerate();
            uint y = _mt[_index++];
            y ^= y >> 11;
            y ^= (y << 7) & 0x9d2c5680u;
            y ^= (y << 15) & 0xefc60000u;
            y ^= y >> 18;
            return y;
        }

        /// <summary>A float in [0, 1) with 53 random bits.</summary>
        public double NextDouble()
        {
            uint a = NextUInt32() >> 5, b = NextUInt32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }

        /// <summary><c>getrandbits(k)</c>: a non-negative integer of at most <paramref name="k"/> bits.</summary>
        public BigInteger GetRandBits(long k)
        {
            if (k <= 0) return BigInteger.Zero;
            if (k <= 32) return NextUInt32() >> (int)(32 - k);
            int words = (int)((k - 1) / 32 + 1);
            var bytes = new byte[words * 4 + 1];
            for (int i = 0; i < words; i++, k -= 32)
            {
                uint r = NextUInt32();
                if (k < 32) r >>= (int)(32 - k);
                bytes[i * 4] = (byte)r;
                bytes[i * 4 + 1] = (byte)(r >> 8);
                bytes[i * 4 + 2] = (byte)(r >> 16);
                bytes[i * 4 + 3] = (byte)(r >> 24);
            }
            return new BigInteger(bytes);
        }

        /// <summary>A random integer in [0, n) for n &gt; 0, drawn the way CPython's <c>_randbelow</c> does.</summary>
        public BigInteger RandBelow(BigInteger n)
        {
            long k = Ops.BitLength(n);
            var r = GetRandBits(k);
            while (r >= n) r = GetRandBits(k);
            return r;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(_index);
            foreach (uint word in _mt) w.Write(word);
            w.Write(HasGauss);
            w.Write(GaussNext);
        }

        public static RandomState Read(BinaryReader r)
        {
            var state = new RandomState();
            int index = r.ReadInt32();
            if (index < 0 || index > N) throw new SnapshotException("the snapshot is damaged");
            for (int i = 0; i < N; i++) state._mt[i] = r.ReadUInt32();
            state._index = index;
            state.HasGauss = r.ReadBoolean();
            state.GaussNext = r.ReadDouble();
            return state;
        }
    }
}
