using System;

namespace FS27.Core
{
    [Serializable]
    public class GoalkeeperTuning
    {
        /// <summary>Multiplier on the level's goalkeeper qualities for the worst (1) and best (99) relevant attribute. Results are capped at 1.</summary>
        public float AttributeFactorAtWorst = 0.80f;
        public float AttributeFactorAtBest = 1.10f;
        /// <summary>How strongly the keeper's Reaction attribute speeds up or slows down its reaction (same meaning as for outfield players).</summary>
        public float ReactionAttributeInfluence = 0.40f;
        /// <summary>Fastest the keeper ever reacts (never below the human limit).</summary>
        public float MinReactionSeconds = 0.12f;
    }

    /// <summary>
    /// The goalkeeper's parameters for ONE keeper, combining the level's settings with the keeper's own attributes (read-only).
    /// This is configuration only: there is no goalkeeper AI yet. A future one will consume this.
    /// Reaction attribute drives reaction, anticipation, shot reading and save timing; Defense drives positioning,
    /// decision making and rebound response (there are no keeper-specific attributes yet).
    /// </summary>
    public struct GoalkeeperSkill
    {
        public float ReactionSeconds;
        public float Positioning;
        public float Anticipation;
        public float DecisionMaking;
        public float SaveTiming;
        public float ShotReading;
        public float ReboundResponse;
    }

    public static class GoalkeeperSkillModel
    {
        public static GoalkeeperSkill Resolve(GoalkeeperParameters g, in PlayerAttributes keeper, GoalkeeperTuning t)
        {
            float reflex = Factor(t, keeper.Reaction);
            float defence = Factor(t, keeper.Defense);

            float reaction = g.ReactionSeconds * ReactionModel.AttributeFactor(t.ReactionAttributeInfluence, keeper);
            return new GoalkeeperSkill
            {
                ReactionSeconds = ReactionModel.Clamp(reaction, t.MinReactionSeconds),
                Positioning = Cap(g.Positioning * defence),
                Anticipation = Cap(g.Anticipation * reflex),
                DecisionMaking = Cap(g.DecisionMaking * defence),
                SaveTiming = Cap(g.SaveTiming * reflex),
                ShotReading = Cap(g.ShotReading * reflex),
                ReboundResponse = Cap(g.ReboundResponse * defence)
            };
        }

        private static float Factor(GoalkeeperTuning t, int attribute)
        {
            return MathUtil.Lerp(t.AttributeFactorAtWorst, t.AttributeFactorAtBest, PlayerAttributes.Normalize(attribute));
        }

        private static float Cap(float v)
        {
            return MathUtil.Clamp01(v);
        }
    }
}
