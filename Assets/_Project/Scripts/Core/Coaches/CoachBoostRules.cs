using System;

namespace FS27.Core
{
    /// <summary>
    /// The tunable numbers of the coach boost. One place, no magic numbers elsewhere. The boost can only be 0 or positive: there is no penalty.
    /// Defaults: a compatibility score of 80..100 gives +1, anything lower gives +0, the boost is never more than +1.
    /// </summary>
    [Serializable]
    public sealed class CoachBoostRules
    {
        /// <summary>The lowest compatibility score (0..100) that earns the boost.</summary>
        public int CompatibilityThreshold = 80;
        /// <summary>The boost a compatible player gets (0 or more).</summary>
        public int Boost = 1;
        /// <summary>The boost can never exceed this (0 or more).</summary>
        public int MaximumBoost = 1;
        /// <summary>The weighted attribute average that counts as 0 / 100 for the attribute part of the score.</summary>
        public int AttributeFloor = 62;
        public int AttributeCeiling = 92;
        /// <summary>A single criterion at or above this (0..100) is listed as matched, for debugging.</summary>
        public int MatchedCriterionThreshold = 75;

        public bool IsValid
        {
            get
            {
                return CompatibilityThreshold >= 0 && CompatibilityThreshold <= 100 && Boost >= 0 && MaximumBoost >= 0 && AttributeFloor < AttributeCeiling
                       && AttributeFloor >= 1 && AttributeCeiling <= 99 && MatchedCriterionThreshold >= 0 && MatchedCriterionThreshold <= 100;
            }
        }

        /// <summary>The boost for a compatibility score: 0 below the threshold, otherwise <see cref="Boost"/> limited to <see cref="MaximumBoost"/>. Never negative.</summary>
        public int BoostFor(int compatibilityScore)
        {
            if (compatibilityScore < CompatibilityThreshold) return 0;
            return Math.Max(0, Math.Min(Boost, MaximumBoost));
        }
    }
}
