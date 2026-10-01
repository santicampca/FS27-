using System;

namespace FS27.Core
{
    [Serializable]
    public class GoalkeeperTuning
    {
        /// <summary>Multiplier on the level's goalkeeper qualities for the worst (1) and best (99) relevant capability. Results are capped at 1.</summary>
        public float AttributeFactorAtWorst = 0.80f;
        public float AttributeFactorAtBest = 1.10f;
        /// <summary>How strongly the keeper's Reflexes speed up or slow down its reaction. The level still sets the base time; Reflexes only modulate it.</summary>
        public float ReflexesReactionInfluence = 0.40f;
        /// <summary>Fastest the keeper ever reacts (never below the human limit).</summary>
        public float MinReactionSeconds = 0.12f;

        // ---- How good the keeper's raw positioning ability is: Positioning leads, general attributes help (they sum to 1) ----
        public float PositioningAbilityFromPositioning = 0.60f;
        public float PositioningAbilityFromAgility = 0.20f;
        public float PositioningAbilityFromSpeed = 0.10f;
        public float PositioningAbilityFromAcceleration = 0.10f;
    }

    /// <summary>
    /// The goalkeeper's parameters for ONE keeper, combining the level's settings with the keeper's own capabilities (read-only).
    /// This is configuration only: there is no goalkeeper AI yet. A future one will consume this.
    ///
    /// Which capability drives which value: Reflexes -> reaction time and shot reading; Positioning ability (Positioning, helped by
    /// Agility, Speed and Acceleration) -> positioning and anticipation; Command -> decision making; Diving -> save timing;
    /// Handling -> rebound response. Kicking, Distribution and Recovery are not used here: they belong to the future ball-distribution
    /// and recovery systems. Reaction is not an input anywhere: the reaction time is the level's base time modulated by Reflexes.
    /// </summary>
    public struct GoalkeeperSkill
    {
        public float ReactionSeconds;
        /// <summary>The keeper's positioning quality (0..1): the level's positioning times the keeper's positioning ability. Situation (ball, attackers, shot angle) is applied later by the AI.</summary>
        public float Positioning;
        public float Anticipation;
        public float DecisionMaking;
        public float SaveTiming;
        public float ShotReading;
        public float ReboundResponse;
    }

    public static class GoalkeeperSkillModel
    {
        /// <summary>Resolves one keeper's parameters. Nothing here writes to the attributes or the profile: the capabilities stay exactly as they are.</summary>
        public static GoalkeeperSkill Resolve(GoalkeeperParameters g, in PlayerAttributes keeper, GoalkeeperProfile profile, GoalkeeperTuning t)
        {
            float reaction = g.ReactionSeconds * ReflexesFactor(t, profile);
            return new GoalkeeperSkill
            {
                ReactionSeconds = ReactionModel.Clamp(reaction, t.MinReactionSeconds),
                Positioning = Cap(g.Positioning * Factor(t, PositioningAbility01(keeper, profile, t))),
                Anticipation = Cap(g.Anticipation * Factor(t, PositioningAbility01(keeper, profile, t))),
                DecisionMaking = Cap(g.DecisionMaking * Factor(t, profile.Command)),
                SaveTiming = Cap(g.SaveTiming * Factor(t, profile.Diving)),
                ShotReading = Cap(g.ShotReading * Factor(t, profile.Reflexes)),
                ReboundResponse = Cap(g.ReboundResponse * Factor(t, profile.Handling))
            };
        }

        /// <summary>
        /// The keeper's raw positioning ability, 0..1 (0 = worst, 1 = best): Positioning mostly, with Agility, Speed and Acceleration.
        /// The base of the future GoalkeeperPositioningQuality; the situation and the difficulty are applied on top of it.
        /// </summary>
        public static float PositioningAbility01(in PlayerAttributes keeper, GoalkeeperProfile profile, GoalkeeperTuning t)
        {
            float wsum = t.PositioningAbilityFromPositioning + t.PositioningAbilityFromAgility + t.PositioningAbilityFromSpeed + t.PositioningAbilityFromAcceleration;
            if (wsum <= 0f) return 0.5f;
            float v = t.PositioningAbilityFromPositioning * PlayerAttributes.Normalize(profile.Positioning)
                    + t.PositioningAbilityFromAgility * PlayerAttributes.Normalize(keeper.Agility)
                    + t.PositioningAbilityFromSpeed * PlayerAttributes.Normalize(keeper.Speed)
                    + t.PositioningAbilityFromAcceleration * PlayerAttributes.Normalize(keeper.Acceleration);
            return MathUtil.Clamp01(v / wsum);
        }

        private static float ReflexesFactor(GoalkeeperTuning t, GoalkeeperProfile profile)
        {
            return 1f + t.ReflexesReactionInfluence * (0.5f - PlayerAttributes.Normalize(profile.Reflexes));
        }

        private static float Factor(GoalkeeperTuning t, int capability)
        {
            return Factor(t, PlayerAttributes.Normalize(capability));
        }

        private static float Factor(GoalkeeperTuning t, float normalized01)
        {
            return MathUtil.Lerp(t.AttributeFactorAtWorst, t.AttributeFactorAtBest, normalized01);
        }

        private static float Cap(float v)
        {
            return MathUtil.Clamp01(v);
        }
    }
}
