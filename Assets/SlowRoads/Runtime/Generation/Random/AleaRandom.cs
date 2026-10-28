using System;

namespace SlowRoads.Generation.Random
{
    /// <summary>Alea 0.9 / Mash 0.9; Dev.source.txt L00147-L00216.</summary>
    public sealed class AleaRandom
    {
        const double Unit = 23283064365386963e-26; // 2^-32, same literal as the archived JS.
        double s0, s1, s2;
        int carry;

        public AleaRandom(string seed)
        {
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            var mash = new Mash();
            s0 = mash.Next(" ");
            s1 = mash.Next(" ");
            s2 = mash.Next(" ");
            double value = mash.Next(seed);
            s0 -= value; if (s0 < 0) s0 += 1;
            value = mash.Next(seed);
            s1 -= value; if (s1 < 0) s1 += 1;
            value = mash.Next(seed);
            s2 -= value; if (s2 < 0) s2 += 1;
            carry = 1;
        }

        public double Next()
        {
            double value = 2091639 * s0 + carry * Unit;
            s0 = s1;
            s1 = s2;
            carry = ToInt32(value);
            s2 = value - carry;
            return s2;
        }

        public State ExportState() => new State(s0, s1, s2, carry);

        public void ImportState(State state)
        {
            s0 = state.S0;
            s1 = state.S1;
            s2 = state.S2;
            carry = state.Carry;
        }

        public readonly struct State
        {
            public readonly double S0, S1, S2;
            public readonly int Carry;
            public State(double s0, double s1, double s2, int carry)
            {
                S0 = s0; S1 = s1; S2 = s2; Carry = carry;
            }
        }

        static int ToInt32(double value) => unchecked((int)ToUint32(value));

        static uint ToUint32(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0) return 0;
            double residue = Math.Truncate(value) % 4294967296.0;
            if (residue < 0) residue += 4294967296.0;
            return (uint)residue;
        }

        sealed class Mash
        {
            double state = 4022871197.0;

            public double Next(string value)
            {
                // C# string iteration is UTF-16 code units, matching JS charCodeAt.
                for (int i = 0; i < value.Length; i++)
                {
                    state += value[i];
                    double work = 0.02519603282416938 * state;
                    state = ToUint32(work);
                    work -= state;
                    work *= state;
                    state = ToUint32(work);
                    work -= state;
                    state += work * 4294967296.0;
                }
                return ToUint32(state) * Unit;
            }
        }
    }
}
