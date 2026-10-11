namespace FS27.Core
{
    /// <summary>
    /// The chance of a controlled mistake (Phase 14), without a generator: a number in [0, 1) that depends ONLY on the three integers it is given (what it is for, when, and who), mixed by a fixed integer hash.
    /// The same match always rolls the same numbers, whatever else was asked in between; nothing consumes a stream, so adding or removing an observer can never move a roll. It is not the Core's random
    /// source and never touches it.
    /// </summary>
    public static class DecisionDice
    {
        public static float Roll(uint purpose, int tick, int who)
        {
            uint h = purpose * 0x9E3779B1u;
            h ^= (uint)tick * 0x85EBCA6Bu;
            h = (h << 13) | (h >> 19);
            h ^= (uint)who * 0xC2B2AE35u;
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        // the purposes (fixed numbers: changing one changes every match that uses it)
        public const uint Choice = 1u;
        public const uint Drift = 2u;
        public const uint DriftAngle = 3u;
        public const uint Reaction = 4u;
    }
}
