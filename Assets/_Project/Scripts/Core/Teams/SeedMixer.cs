namespace FS27.Core
{
    /// <summary>Turns a seed (and an attempt number) into a well-spread generator state, so that nearby seeds do not give nearby draws. Pure integer arithmetic: same on every platform.</summary>
    public static class SeedMixer
    {
        public static uint Mix(int seed, int salt)
        {
            unchecked
            {
                uint z = (uint)seed + 0x9E3779B9u * (uint)(salt + 1);
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return z == 0 ? 0x9E3779B9u : z;
            }
        }

        public static SeededRandom Create(int seed, int salt) { return new SeededRandom(Mix(seed, salt)); }
    }
}
