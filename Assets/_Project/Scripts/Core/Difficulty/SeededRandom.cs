namespace FS27.Core
{
    /// <summary>Source of random numbers, injected so AI decisions can be reproduced in tests and replays.</summary>
    public interface IRandomSource
    {
        /// <summary>A number in [0, 1).</summary>
        float NextFloat01();
    }

    /// <summary>Small deterministic generator (xorshift32): same seed, same sequence, on every platform.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private uint state;

        public SeededRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public float NextFloat01()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            // 24 high-quality bits -> [0, 1)
            return (x >> 8) * (1f / 16777216f);
        }
    }
}
