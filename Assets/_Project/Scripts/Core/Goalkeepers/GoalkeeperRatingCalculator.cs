using System;

namespace FS27.Core
{
    /// <summary>
    /// The ONE place the goalkeeper rating (GRL) is calculated. The official rule is the arithmetic mean of the 8 goalkeeper attributes,
    /// rounded to the nearest whole number (a .5 tie rounds up). Nothing else takes part: not the keeper's styles/profiles, not the
    /// general attributes or the player's Overall, not height, reputation, name, nationality, potential or difficulty. It takes a
    /// <see cref="GoalkeeperProfile"/> and nothing more, so it cannot depend on any of those. It is separate from the player's Overall.
    /// A rating stored in authoring data is only ever compared with this result, never used instead of it.
    /// </summary>
    public static class GoalkeeperRatingCalculator
    {
        /// <summary>The unrounded mean of the 8 attributes (1..99).</summary>
        public static float CalculateRaw(GoalkeeperProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            int sum = 0;
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) sum += profile.GetValue(c);
            return sum / (float)GoalkeeperInfo.CapabilityCount;
        }

        /// <summary>The goalkeeper rating, 1..99: the mean of the 8 attributes rounded to the nearest integer (.5 rounds up).</summary>
        public static int Calculate(GoalkeeperProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            int sum = 0;
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) sum += profile.GetValue(c);
            // exact integer arithmetic: (sum / 8) rounded half up == (sum + 4) / 8 (no floating-point tie errors)
            return (sum + GoalkeeperInfo.CapabilityCount / 2) / GoalkeeperInfo.CapabilityCount;
        }
    }
}
