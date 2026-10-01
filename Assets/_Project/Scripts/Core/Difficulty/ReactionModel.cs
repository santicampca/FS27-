using System;

namespace FS27.Core
{
    /// <summary>
    /// How long an AI-controlled player takes to react. The difficulty sets the base time; the player's own
    /// Reaction attribute nudges it; a hard floor (the level's minimum and the human limit) keeps it plausible.
    /// The attribute is only READ: nothing here writes to PlayerAttributes.
    /// </summary>
    public static class ReactionModel
    {
        /// <summary>1 at an average Reaction attribute; below 1 (faster) for good reflexes, above 1 (slower) for poor ones.</summary>
        public static float AttributeFactor(float attributeInfluence, in PlayerAttributes attributes)
        {
            return 1f + attributeInfluence * (0.5f - PlayerAttributes.Normalize(attributes.Reaction));
        }

        /// <param name="jitter01">A sample in 0..1 (0.5 = no variation) so the result is reproducible.</param>
        public static float ReactionSeconds(ReactionParameters p, in PlayerAttributes attributes, float jitter01 = 0.5f)
        {
            float jitter = 1f + p.ReactionVariance * (2f * MathUtil.Clamp01(jitter01) - 1f);
            float seconds = p.BaseReactionSeconds * AttributeFactor(p.AttributeInfluence, attributes) * jitter;
            return Clamp(seconds, p.MinReactionSeconds);
        }

        public static float ReactionSeconds(ReactionParameters p, in PlayerAttributes attributes, IRandomSource random)
        {
            return ReactionSeconds(p, attributes, random.NextFloat01());
        }

        /// <summary>Never below the level's own floor, never below the human limit, never absurdly long.</summary>
        public static float Clamp(float seconds, float levelMinimum)
        {
            float floor = Math.Max(levelMinimum, DifficultyRules.AbsoluteMinReactionSeconds);
            return Math.Min(DifficultyRules.MaxReactionSeconds, Math.Max(floor, seconds));
        }

        /// <summary>The moment in the past whose information the AI is acting on when it acts at <paramref name="now"/>.</summary>
        public static float ObservationTime(float now, float reactionSeconds)
        {
            return now - Math.Max(0f, reactionSeconds);
        }
    }
}
