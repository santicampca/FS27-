using System;

namespace FS27.Core
{
    /// <summary>
    /// Everything about one shot at the moment it is struck (Phase 16). Plain data read from the world and from the Core's answer to the kick; it is made for statistics, balance and difficulty and nothing in the
    /// match ever reads it back.
    /// </summary>
    public struct ShotContext
    {
        public Vec2 Shooter;
        public Vec2 GoalCentre;
        public float GoalWidth;
        /// <summary>The ball's velocity at the strike (field space: X along the pitch, Y across).</summary>
        public Vec2 BallVelocity;
        /// <summary>Distance (m) from the shooter to the nearest opposing field player, and how many of them stand in the corridor between the shooter and the goal.</summary>
        public float NearestDefender;
        public int DefendersInLane;
        public Vec2 Keeper;
        public bool HasKeeper;
        public bool WeakFoot;
    }

    /// <summary>The quality of one shot, as separate parts (each 0..1, 1 = a good chance) and the probability the calibrated model gives it. NOT the result: the Core decides that.</summary>
    public struct ShotQuality
    {
        public float Distance, Angle, Pressure, KeeperPosition, Foot, Speed, Aim;
        /// <summary>0..1: the weighted mean of the parts times the aim.</summary>
        public float Value;
        /// <summary>The probability of a goal that the model gives this quality (see <see cref="ShotQualityDefaults"/>). For statistics only.</summary>
        public float ExpectedGoal;
        /// <summary>Metres from the shooter to the centre of the goal, and the angle (radians) the goal mouth subtends from where he shoots.</summary>
        public float Metres, Radians;
    }

    public static class ShotQualityDefaults
    {
        /// <summary>The distance at which the distance part is 1 and 0 (m).</summary>
        public const float NearDistance = 2.0f;
        public const float FarDistance = 14.0f;
        /// <summary>The angle of the goal mouth (radians) that counts as fully open.</summary>
        public const float FullAngle = 1.2f;
        /// <summary>A defender this far (m) or further is no pressure; each defender in the shooting corridor takes this much of the pressure part.</summary>
        public const float FreeDefenderDistance = 3.0f;
        public const float LaneBlockerCut = 0.3f;
        public const float LaneHalfWidth = 1.0f;
        /// <summary>The keeper covers a shot whose crossing point is within this many metres of him sideways; the part is 1 when he is that far or further.</summary>
        public const float KeeperCoverRadius = 3.0f;
        /// <summary>The weak foot's part of the foot (1 for the strong foot).</summary>
        public const float WeakFootPart = 0.6f;
        /// <summary>The ball speed (m/s) at which the speed part is 1.</summary>
        public const float FullSpeed = 22.0f;
        /// <summary>The aim: 1 when the ball crosses inside the posts by at least this margin (m), 0 when it crosses this far outside them.</summary>
        public const float AimMargin = 0.4f;
        public const float AimOutside = 0.6f;
        /// <summary>The weights of the parts (they need not add to 1). Chosen from a logistic fit of the goals on the parts (angle and the keeper's position carry the most; the distance is mostly inside the angle, and the foot and the speed are confounded with distance in these matches, so they weigh little).</summary>
        public const float WeightDistance = 0.10f;
        public const float WeightAngle = 0.45f;
        public const float WeightPressure = 0.10f;
        public const float WeightKeeper = 0.30f;
        public const float WeightFoot = 0.03f;
        public const float WeightSpeed = 0.02f;
        /// <summary>The calibrated model: probability = 1 / (1 + exp(-(Intercept + Slope * value))). Fitted on the Phase 16 reference matches (see the report); changing it never changes a match.</summary>
        public const float Intercept = -5.576f;
        public const float Slope = 5.685f;
        /// <summary>Added to the intercept in Classic (its keepers stop more of the same shots). Fitted with the two numbers above on 3580 shots of Professional against Professional in both modes.</summary>
        public const float ClassicInterceptOffset = -0.695f;
    }

    /// <summary>The numbers of <see cref="ShotQualityEvaluator"/>, editable.</summary>
    [Serializable]
    public sealed class ShotQualityTuning
    {
        public float NearDistance = ShotQualityDefaults.NearDistance;
        public float FarDistance = ShotQualityDefaults.FarDistance;
        public float FullAngle = ShotQualityDefaults.FullAngle;
        public float FreeDefenderDistance = ShotQualityDefaults.FreeDefenderDistance;
        public float LaneBlockerCut = ShotQualityDefaults.LaneBlockerCut;
        public float LaneHalfWidth = ShotQualityDefaults.LaneHalfWidth;
        public float KeeperCoverRadius = ShotQualityDefaults.KeeperCoverRadius;
        public float WeakFootPart = ShotQualityDefaults.WeakFootPart;
        public float FullSpeed = ShotQualityDefaults.FullSpeed;
        public float AimMargin = ShotQualityDefaults.AimMargin;
        public float AimOutside = ShotQualityDefaults.AimOutside;
        public float WeightDistance = ShotQualityDefaults.WeightDistance;
        public float WeightAngle = ShotQualityDefaults.WeightAngle;
        public float WeightPressure = ShotQualityDefaults.WeightPressure;
        public float WeightKeeper = ShotQualityDefaults.WeightKeeper;
        public float WeightFoot = ShotQualityDefaults.WeightFoot;
        public float WeightSpeed = ShotQualityDefaults.WeightSpeed;
        public float Intercept = ShotQualityDefaults.Intercept;
        public float Slope = ShotQualityDefaults.Slope;
        public float ClassicInterceptOffset = ShotQualityDefaults.ClassicInterceptOffset;
        /// <summary>1 in Classic, 0 otherwise: set by the observer from the mode of the match it watches.</summary>
        public float ModeIsClassic;
    }

    /// <summary>
    /// Rates a shot (Phase 16): how near, how open the angle, how much pressure, how far the keeper is from where the ball will cross, which foot, how fast and how well aimed. It does not decide anything: it is read by
    /// the diagnostics for statistics, balance and difficulty (expected goals against real goals). A pure function; nothing is allocated.
    /// </summary>
    public static class ShotQualityEvaluator
    {
        public static ShotQuality Evaluate(in ShotContext c, ShotQualityTuning t)
        {
            var q = new ShotQuality();
            Vec2 toGoal = c.GoalCentre - c.Shooter;
            q.Metres = toGoal.Magnitude;
            q.Distance = 1f - MathUtil.Clamp01((q.Metres - t.NearDistance) / Math.Max(t.FarDistance - t.NearDistance, 1e-3f));

            float half = c.GoalWidth * 0.5f;
            Vec2 postA = new Vec2(c.GoalCentre.X, c.GoalCentre.Y + half) - c.Shooter, postB = new Vec2(c.GoalCentre.X, c.GoalCentre.Y - half) - c.Shooter;
            float la = postA.Magnitude, lb = postB.Magnitude;
            float cos = la > 1e-3f && lb > 1e-3f ? (postA.X * postB.X + postA.Y * postB.Y) / (la * lb) : 1f;
            q.Radians = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, cos)));
            q.Angle = MathUtil.Clamp01(q.Radians / Math.Max(t.FullAngle, 1e-3f));

            float free = MathUtil.Clamp01(c.NearestDefender / Math.Max(t.FreeDefenderDistance, 1e-3f));
            q.Pressure = free * MathUtil.Clamp01(1f - t.LaneBlockerCut * c.DefendersInLane);

            // where the ball will cross the goal line (a straight line from the strike) and how far the keeper is from there
            float vx = c.BallVelocity.X, vy = c.BallVelocity.Y;
            float crossY = c.Shooter.Y;
            bool toward = (c.GoalCentre.X - c.Shooter.X) * vx > 0f;
            if (toward) crossY = c.Shooter.Y + vy * ((c.GoalCentre.X - c.Shooter.X) / vx); else crossY = c.Shooter.Y + (vx == 0f && vy == 0f ? 0f : 1e3f);
            float off = Math.Abs(crossY - c.GoalCentre.Y);
            q.Aim = toward ? MathUtil.Clamp01((half + t.AimOutside - off) / Math.Max(t.AimOutside + t.AimMargin, 1e-3f)) : 0f;
            q.KeeperPosition = c.HasKeeper ? MathUtil.Clamp01(Math.Abs(c.Keeper.Y - crossY) / Math.Max(t.KeeperCoverRadius, 1e-3f)) : 1f;

            q.Foot = c.WeakFoot ? t.WeakFootPart : 1f;
            float speed = (float)Math.Sqrt(vx * vx + vy * vy);
            q.Speed = MathUtil.Clamp01(speed / Math.Max(t.FullSpeed, 1e-3f));

            float wsum = t.WeightDistance + t.WeightAngle + t.WeightPressure + t.WeightKeeper + t.WeightFoot + t.WeightSpeed;
            float mean = (t.WeightDistance * q.Distance + t.WeightAngle * q.Angle + t.WeightPressure * q.Pressure + t.WeightKeeper * q.KeeperPosition + t.WeightFoot * q.Foot + t.WeightSpeed * q.Speed) / Math.Max(wsum, 1e-3f);
            q.Value = MathUtil.Clamp01(mean * q.Aim);
            q.ExpectedGoal = 1f / (1f + (float)Math.Exp(-(t.Intercept + t.ClassicInterceptOffset * t.ModeIsClassic + t.Slope * q.Value)));
            return q;
        }
    }
}
