using System;

namespace FS27.Core
{
    /// <summary>The kinds of mistake the AI can make. Each one is driven by a different skill (see <see cref="AiErrorModel"/>).</summary>
    public enum AiErrorKind
    {
        BadPassChoice = 0,
        PoorControl = 1,
        LatePress = 2,
        PoorCover = 3,
        OverAggressiveMove = 4,
        RushedShot = 5,
        MisreadTrajectory = 6,
        LateReaction = 7
    }

    /// <summary>All the numbers of the error model, in one place (defaults; nothing here is hidden in the logic).</summary>
    [Serializable]
    public class ErrorTuning
    {
        /// <summary>Highest base chance of each mistake for the weakest AI, indexed by <see cref="AiErrorKind"/>.</summary>
        public float[] MaxRatePerKind = { 0.30f, 0.35f, 0.40f, 0.35f, 0.30f, 0.30f, 0.35f, 0.40f };
        /// <summary>Curve of the skill effect: higher = skilled AIs make far fewer mistakes.</summary>
        public float SkillExponent = 1.5f;

        /// <summary>Multiplier on the chance for the worst (1) and best (99) relevant attribute.</summary>
        public float AttributeFactorAtWorst = 1.4f;
        public float AttributeFactorAtBest = 0.5f;

        public float PressureWeight = 1.0f;
        public float FatigueWeight = 1.0f;

        /// <summary>Multiplier on the chance for the easiest and hardest action (distance, angle, tight timing).</summary>
        public float ActionDifficultyMin = 0.6f;
        public float ActionDifficultyMax = 1.6f;

        public float MaxProbability = 0.9f;

        /// <summary>Severity multiplier for the worst and best attribute.</summary>
        public float MagnitudeAttributeAtWorst = 1.2f;
        public float MagnitudeAttributeAtBest = 0.7f;
        /// <summary>Severity with zero pressure, as a fraction of the value at full pressure.</summary>
        public float MagnitudeCalmFraction = 0.5f;

        /// <summary>Reaction times used to turn the reaction setting into a 0..1 skill (for <see cref="AiErrorKind.LateReaction"/>).</summary>
        public float ReactionBestSeconds = 0.15f;
        public float ReactionWorstSeconds = 0.55f;
    }

    /// <summary>The situation around an action. Use <see cref="Calm"/> as a starting point.</summary>
    public struct ErrorContext
    {
        /// <summary>0 = free, 1 = heavily marked / very little time.</summary>
        public float Pressure01;
        /// <summary>1 = fresh, 0 = exhausted.</summary>
        public float Stamina01;
        /// <summary>0 = trivial (short, open), 1 = very hard (long, tight angle, awkward body position).</summary>
        public float ActionDifficulty01;

        public static ErrorContext Calm => new ErrorContext { Pressure01 = 0f, Stamina01 = 1f, ActionDifficulty01 = 0.5f };
    }

    public struct ErrorAssessment
    {
        /// <summary>Chance (0..1) that this mistake happens now.</summary>
        public float Probability;
        /// <summary>Largest severity (0..1) if it happens.</summary>
        public float Magnitude;
    }

    public struct ErrorOutcome
    {
        public bool Occurs;
        /// <summary>0 when no error; otherwise 0..1 (the caller scales it into degrees, power, metres...).</summary>
        public float Severity01;
    }

    /// <summary>
    /// Controllable mistakes for AI players. The CHANCE and SIZE of a mistake are a deterministic function of:
    /// the difficulty's relevant skill, the player's own relevant attribute, the pressure, the player's stamina and how hard
    /// the action is. Randomness only decides the final coin flip (<see cref="Resolve"/>), and it is injected, so every
    /// decision is reproducible. The attributes are only READ.
    /// </summary>
    public static class AiErrorModel
    {
        /// <summary>The difficulty skill (0..1, 1 = best) that protects against this kind of mistake.</summary>
        public static float SkillFor(AiErrorKind kind, DifficultyDefinition d, ErrorTuning t)
        {
            float s;
            switch (kind)
            {
                case AiErrorKind.BadPassChoice: s = d.Decision.DecisionQuality; break;
                case AiErrorKind.PoorControl: s = d.Execution.ExecutionQuality; break;
                case AiErrorKind.RushedShot: s = d.Execution.ExecutionQuality; break;
                case AiErrorKind.LatePress: s = d.Pressure.PressureIntelligence; break;
                case AiErrorKind.PoorCover: s = d.Positioning.PositioningQuality; break;
                case AiErrorKind.OverAggressiveMove: s = d.Positioning.ZoneDiscipline; break;
                case AiErrorKind.MisreadTrajectory: s = d.Anticipation.AnticipationQuality; break;
                case AiErrorKind.LateReaction: s = MathUtil.InverseLerp(t.ReactionWorstSeconds, t.ReactionBestSeconds, d.Reaction.BaseReactionSeconds); break;
                default: s = 0f; break;
            }
            return MathUtil.Clamp01(s);
        }

        /// <summary>The player attribute (1..99) that limits this kind of action.</summary>
        public static int AttributeFor(AiErrorKind kind, in PlayerAttributes a)
        {
            switch (kind)
            {
                case AiErrorKind.BadPassChoice: return a.Passing;
                case AiErrorKind.PoorControl: return a.BallControl;
                case AiErrorKind.RushedShot: return a.Shooting;
                case AiErrorKind.LatePress:
                case AiErrorKind.PoorCover:
                case AiErrorKind.OverAggressiveMove: return a.Defense;
                case AiErrorKind.MisreadTrajectory:
                case AiErrorKind.LateReaction: return a.Reaction;
                default: return PlayerAttributes.Min;
            }
        }

        public static ErrorAssessment Assess(AiErrorKind kind, DifficultyDefinition d, in PlayerAttributes attributes, in ErrorContext context, ErrorTuning t)
        {
            float skill = SkillFor(kind, d, t);
            float attr = PlayerAttributes.Normalize(AttributeFor(kind, attributes));
            float pressure = MathUtil.Clamp01(context.Pressure01);
            float fatigue = 1f - MathUtil.Clamp01(context.Stamina01);

            float rate = t.MaxRatePerKind[(int)kind] * (float)Math.Pow(1f - skill, t.SkillExponent);
            rate *= MathUtil.Lerp(t.AttributeFactorAtWorst, t.AttributeFactorAtBest, attr);
            rate *= 1f + d.Execution.PressureSensitivity * pressure * t.PressureWeight;
            rate *= 1f + d.Execution.FatigueSensitivity * fatigue * t.FatigueWeight;
            rate *= MathUtil.Lerp(t.ActionDifficultyMin, t.ActionDifficultyMax, context.ActionDifficulty01);

            float magnitude = d.Execution.ErrorMagnitude
                              * MathUtil.Lerp(t.MagnitudeCalmFraction, 1f, pressure)
                              * MathUtil.Lerp(t.MagnitudeAttributeAtWorst, t.MagnitudeAttributeAtBest, attr);

            return new ErrorAssessment
            {
                Probability = Math.Min(t.MaxProbability, Math.Max(0f, rate)),
                Magnitude = MathUtil.Clamp01(magnitude)
            };
        }

        /// <summary>The only place randomness enters. Always consumes exactly two samples, so streams stay aligned.</summary>
        public static ErrorOutcome Resolve(in ErrorAssessment assessment, IRandomSource random)
        {
            float roll = random.NextFloat01();
            float severityRoll = random.NextFloat01();
            if (roll >= assessment.Probability) return new ErrorOutcome();
            return new ErrorOutcome { Occurs = true, Severity01 = assessment.Magnitude * (0.5f + 0.5f * severityRoll) };
        }
    }
}
