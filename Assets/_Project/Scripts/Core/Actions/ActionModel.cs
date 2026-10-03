using System;

namespace FS27.Core
{
    /// <summary>The pure maths of the actions (no state, no randomness of its own): easy to test, shared by every action.</summary>
    public static class ActionModel
    {
        public static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }

        public static bool IsFinite(Vec2 v) { return !(float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.X) || float.IsInfinity(v.Y)); }

        /// <summary>The wanted direction as a unit vector; the fallback (the facing) when there is none.</summary>
        public static Vec2 DirectionOr(Vec2 wanted, Vec2 fallback)
        {
            if (!IsFinite(wanted)) return fallback;
            Vec2 n = wanted.Normalized;
            return n.SqrMagnitude > 0f ? n : fallback;
        }

        /// <summary>Rotates a vector by an angle in degrees. Positive turns towards +X from +Y (clockwise on the field), like the heading.</summary>
        public static Vec2 Rotate(Vec2 v, float degrees)
        {
            float r = degrees * MathUtil.Deg2Rad;
            float s = (float)Math.Sin(r), c = (float)Math.Cos(r);
            return new Vec2(v.X * c + v.Y * s, v.Y * c - v.X * s);
        }

        /// <summary>Signed angle in degrees from the facing to the direction: positive = towards the RIGHT of the player (clockwise), negative = left.</summary>
        public static float SignedAngle(Vec2 facing, Vec2 direction)
        {
            float cross = facing.Y * direction.X - facing.X * direction.Y; // >0: direction is clockwise (right) of facing
            float dot = facing.X * direction.X + facing.Y * direction.Y;
            return (float)Math.Atan2(cross, dot) * MathUtil.Rad2Deg;
        }

        /// <summary>
        /// Which foot plays a ball that lies/goes in this direction. Within +-WeakFootAngle of the facing he uses his preferred foot; further towards his weak side he uses the
        /// weak foot; further towards his preferred side he still uses the preferred foot. Also gives the quality factor of that foot (1 for the preferred, WeakFootFloor..1 for the weak one).
        /// </summary>
        public static ActionFoot ChooseFoot(in ActionProfile profile, Vec2 facing, Vec2 direction, ActionTuning t, out float footFactor)
        {
            float angle = SignedAngle(facing, direction); // + = right side
            bool preferRight = profile.PreferredFoot == PreferredFoot.Right;
            bool towardsWeakSide = preferRight ? angle < 0f : angle > 0f;
            bool useWeak = towardsWeakSide && Math.Abs(angle) >= t.WeakFootAngleDegrees;
            if (!useWeak)
            {
                footFactor = 1f;
                return preferRight ? ActionFoot.Right : ActionFoot.Left;
            }
            footFactor = t.WeakFootFloor + (1f - t.WeakFootFloor) * Clamp01(profile.WeakFootQuality);
            return preferRight ? ActionFoot.Left : ActionFoot.Right;
        }

        /// <summary>A tired player plays worse: Base at none, 1 at full performance.</summary>
        public static float StaminaFactor(float stamina, LocomotionTuning loco, ActionTuning t)
        {
            return t.StaminaQualityBase + (1f - t.StaminaQualityBase) * LocomotionStamina.Performance(stamina, loco);
        }

        /// <summary>The angular error (degrees, sign included) of a kick: the worse the quality, the bigger; more power, more error.</summary>
        public static float KickError(float maxErrorDegrees, float quality, float power, float extraShare, float noiseMinusOneToOne, ActionTuning t)
        {
            float powerFactor = 1f - t.PowerErrorShare + t.PowerErrorShare * Clamp01(power);
            return maxErrorDegrees * (1f - Clamp01(quality)) * powerFactor * (1f + extraShare) * noiseMinusOneToOne;
        }

        /// <summary>
        /// The first-touch score: control quality (x foot x stamina) minus the difficulty of the ball (its speed relative to the player and the angle it comes from) plus a little noise.
        /// At or above <see cref="ActionTuning.CleanControlThreshold"/> the ball is controlled; at or above <see cref="ActionTuning.HeavyTouchThreshold"/> it is touched heavily; below, it bounces away.
        /// </summary>
        public static float FirstTouchScore(float quality, float footFactor, float staminaFactor, float relativeBallSpeed, float angleDegrees, float bonus, float noise, ActionTuning t)
        {
            float difficulty = Clamp01(relativeBallSpeed / t.FirstTouchMaxBallSpeed) * 0.8f + Math.Abs(angleDegrees) / 180f * 0.25f;
            if (Math.Abs(angleDegrees) > t.MaxControlAngleDegrees) difficulty += 0.3f;
            return quality * footFactor * staminaFactor - difficulty + bonus + noise * t.FirstTouchNoise;
        }
    }
}
