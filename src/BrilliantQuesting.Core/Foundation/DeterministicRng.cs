using System;

namespace BrilliantQuesting.Foundation
{
    /// <summary>
    /// splitmix64. Deterministic, cheap, and - importantly - forkable: any subsystem can derive
    /// a private stream from a label without disturbing the caller's sequence. That is what makes
    /// "record the seed, replay the situation" possible in tests and in the debug inspector.
    /// </summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(ulong seed)
        {
            Seed = seed;
            _state = seed;
        }

        public ulong Seed { get; }

        public ulong State => _state;

        public void RestoreState(ulong state) => _state = state;

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, exclusiveMax).</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            return (int)(NextUInt64() % (ulong)exclusiveMax);
        }

        /// <summary>Uniform in [inclusiveMin, exclusiveMax).</summary>
        public int NextInt(int inclusiveMin, int exclusiveMax)
        {
            if (exclusiveMax <= inclusiveMin)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            return inclusiveMin + NextInt(exclusiveMax - inclusiveMin);
        }

        /// <summary>1..sides, the way the game rolls dice.</summary>
        public int Roll(int sides) => NextInt(sides) + 1;

        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        public bool Chance(double probability) => NextDouble() < probability;

        /// <summary>
        /// Derives an independent stream. Same parent seed + same label always yields the same
        /// child stream, so a situation generated from seed S is byte-identical on replay.
        /// </summary>
        public DeterministicRng Fork(string label)
        {
            ulong hash = 1469598103934665603UL;
            string text = label ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return new DeterministicRng(Seed ^ hash);
        }
    }
}
