using System;

namespace FS27.Core
{
    [Serializable]
    public class AnticipationTuning
    {
        /// <summary>Horizon multiplier for the worst (1) and best (99) Reaction attribute.</summary>
        public float AttributeFactorAtWorst = 0.7f;
        public float AttributeFactorAtBest = 1.15f;
        /// <summary>Horizon multiplier for an event at the very edge of perception range.</summary>
        public float FarDistanceFactor = 0.5f;
        /// <summary>Horizon multiplier when the body faces directly away from the event (facing directly = 1).</summary>
        public float FacingAwayFactor = 0.4f;
        /// <summary>Prediction error, as a fraction of the distance the ball travels, for the least capable AI.</summary>
        public float MaxPredictionErrorFraction = 0.35f;
    }

    /// <summary>What the AI has to work with when it looks at an event (a ball, a runner...).</summary>
    public struct PerceptionContext
    {
        public Vec2 ObserverPosition;
        /// <summary>Heading of the observer's body in radians (same convention as PlayerRuntimeState.Heading).</summary>
        public float ObserverHeading;
        public Vec2 EventPosition;
    }

    /// <summary>
    /// What an AI player can know and how far ahead it can read. Anticipation is LIMITED by perception: if the event
    /// is outside the field of view or the range, the horizon is zero. It only ever predicts forward from an observed
    /// state (use <see cref="ObservationDelayBuffer{T}"/> to get a delayed one), so it never uses future information.
    /// </summary>
    public static class AnticipationModel
    {
        /// <summary>True if the event is inside the observer's field of view and perception range.</summary>
        public static bool CanPerceive(in PerceptionContext c, AnticipationParameters p)
        {
            Vec2 toEvent = c.EventPosition - c.ObserverPosition;
            float distance = toEvent.Magnitude;
            if (distance > p.PerceptionRangeMeters) return false;
            if (distance < 1e-4f) return true;

            float angleToEvent = Math.Abs(MathUtil.DeltaAngle(c.ObserverHeading, toEvent.ToHeading())) * MathUtil.Rad2Deg;
            return angleToEvent <= p.VisionAngleDegrees * 0.5f;
        }

        /// <summary>
        /// How many seconds ahead the AI can read this event: 0 if it cannot perceive it; otherwise the level's maximum,
        /// scaled by the player's Reaction attribute, the distance to the event and body orientation, and never above the hard cap.
        /// </summary>
        public static float Horizon(in PerceptionContext c, AnticipationParameters p, in PlayerAttributes a, AnticipationTuning t)
        {
            if (!CanPerceive(c, p)) return 0f;

            Vec2 toEvent = c.EventPosition - c.ObserverPosition;
            float distance = toEvent.Magnitude;

            float attr = MathUtil.Lerp(t.AttributeFactorAtWorst, t.AttributeFactorAtBest, PlayerAttributes.Normalize(a.Reaction));
            float range = Math.Max(1e-3f, p.PerceptionRangeMeters);
            float distanceFactor = MathUtil.Lerp(1f, t.FarDistanceFactor, distance / range);

            float facing = 1f;
            if (distance > 1e-4f)
            {
                float angle = Math.Abs(MathUtil.DeltaAngle(c.ObserverHeading, toEvent.ToHeading()));
                facing = MathUtil.Lerp(1f, t.FacingAwayFactor, angle / (float)Math.PI);
            }

            float horizon = p.MaxLookaheadSeconds * attr * distanceFactor * facing;
            // The level's maximum is a ceiling: good attributes and ideal conditions can reach it, never exceed it.
            return Math.Min(DifficultyRules.AbsoluteMaxLookaheadSeconds, Math.Min(p.MaxLookaheadSeconds, Math.Max(0f, horizon)));
        }

        /// <summary>How wrong the AI's reading of a trajectory tends to be (0 = perfect), as a fraction of the distance travelled.</summary>
        public static float PredictionErrorFraction(AnticipationParameters p, AnticipationTuning t)
        {
            return (1f - MathUtil.Clamp01(p.AnticipationQuality)) * t.MaxPredictionErrorFraction;
        }

        /// <summary>
        /// Where the AI believes the ball will be after <paramref name="horizon"/> seconds, predicted from the state it
        /// OBSERVED (not the true present, not the future). The imperfection scales with the level's anticipation quality.
        /// </summary>
        public static Vec3 PredictBallPosition(in BallState observed, BallParameters ballParameters, float horizon,
                                               AnticipationParameters p, AnticipationTuning t, Vec3 noiseUnit)
        {
            BallState predicted = BallPredictor.Predict(observed, ballParameters, Math.Max(0f, horizon));
            Vec3 travel = predicted.Position - observed.Position;
            float len = noiseUnit.Magnitude;
            Vec3 n = len > 1f ? noiseUnit * (1f / len) : noiseUnit;
            return predicted.Position + n * (travel.Magnitude * PredictionErrorFraction(p, t));
        }
    }
}
