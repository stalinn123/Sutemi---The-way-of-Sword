using System;

namespace Kendo.AI
{
    /// <summary>xorshift64* RNG, bit-identical to Tools/AI/krandom.py so simulations reproduce across C# and Python.</summary>
    public sealed class KRandom
    {
        private ulong state;

        public KRandom(long seed)
        {
            unchecked
            {
                state = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL;
                if (state == 0) state = 0x2545F4914F6CDD1DUL;
            }
        }

        public ulong NextU64()
        {
            unchecked
            {
                ulong x = state;
                x ^= x >> 12;
                x ^= x << 25;
                x ^= x >> 27;
                state = x;
                return x * 0x2545F4914F6CDD1DUL;
            }
        }

        public double Value() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        public double Range(double a, double b) => a + (b - a) * Value();
        public int Int(int n) => (int)(Value() * n) % n;

        public double Gauss()
        {
            double u1 = Math.Max(1e-12, Value());
            double u2 = Value();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public int Poisson(double lambda)
        {
            double l = Math.Exp(-lambda);
            int k = 0;
            double p = 1.0;
            while (true)
            {
                p *= Value();
                if (p <= l) return k;
                k++;
            }
        }
    }
}
