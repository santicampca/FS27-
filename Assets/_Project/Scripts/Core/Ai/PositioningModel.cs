using System;

namespace FS27.Core
{
    /// <summary>
    /// Something that knows where a player SHOULD be (formation + role + zone + ball + teammates). The future team AI
    /// implements this; <see cref="PositioningModel"/> then decides how close to that ideal a given difficulty gets.
    /// </summary>
    public interface IIdealPositionProvider
    {
        Vec2 GetIdealPosition(int playerIndex);
    }

    /// <summary>Tuning for how positioning quality turns into metres. All defaults are starting points.</summary>
    [Serializable]
    public class PositioningTuning
    {
        /// <summary>How far past its zone a lazy defender may wander, as a fraction of the zone radius, at zero discipline.</summary>
        public float OverchaseFraction = 1.0f;
        /// <summary>Extra position error (fraction) at full pressure.</summary>
        public float PressureErrorGrowth = 0.5f;
        /// <summary>Error multiplier for the worst (1) and best (99) Defense attribute.</summary>
        public float AttributeFactorAtWorst = 1.2f;
        public float AttributeFactorAtBest = 0.8f;
    }

    /// <summary>
    /// Positioning for AI players: the AI does not stand exactly on the ideal spot, it ends up somewhere near it, and
    /// better difficulties end up nearer. Also decides whether a defender should leave its zone to chase the ball.
    /// Pure functions; the noise sample is supplied by the caller (reproducible) and attributes are only read.
    /// </summary>
    public static class PositioningModel
    {
        /// <summary>The farthest the player can end up from the ideal position (metres).</summary>
        public static float MaxErrorMeters(PositioningParameters p, in PlayerAttributes a, float pressure01, PositioningTuning t)
        {
            float attr = MathUtil.Lerp(t.AttributeFactorAtWorst, t.AttributeFactorAtBest, PlayerAttributes.Normalize(a.Defense));
            return p.PositionErrorMeters * attr * (1f + t.PressureErrorGrowth * MathUtil.Clamp01(pressure01));
        }

        /// <param name="noiseUnit">A vector of length up to 1 (e.g. slowly varying noise); it picks the direction and size of the slip.</param>
        public static Vec2 ApplyError(Vec2 ideal, PositioningParameters p, in PlayerAttributes a, float pressure01, Vec2 noiseUnit, PositioningTuning t)
        {
            float len = noiseUnit.Magnitude;
            Vec2 n = len > 1f ? noiseUnit * (1f / len) : noiseUnit;
            return ideal + n * MaxErrorMeters(p, a, pressure01, t);
        }

        /// <summary>The distance from its ideal spot at which a defender is still willing to chase the ball.</summary>
        public static float MaxChaseDistance(float zoneRadius, PositioningParameters p, PositioningTuning t)
        {
            return zoneRadius * (1f + (1f - MathUtil.Clamp01(p.ZoneDiscipline)) * t.OverchaseFraction);
        }

        /// <summary>True if the ball is close enough to the player's zone that leaving it to chase is acceptable.</summary>
        public static bool MayChase(float distanceFromIdealToBall, float zoneRadius, PositioningParameters p, PositioningTuning t)
        {
            return distanceFromIdealToBall <= MaxChaseDistance(zoneRadius, p, t);
        }

        /// <summary>How well the player closes the passing lane between the carrier and a target (0..1; used to bias cover positions).</summary>
        public static float LaneCoverEffectiveness(PositioningParameters p, float pressure01)
        {
            return MathUtil.Clamp01(p.PassLaneCover * (1f - 0.25f * MathUtil.Clamp01(pressure01)));
        }
    }
}
