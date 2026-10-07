using System;

namespace FS27.Core
{
    /// <summary>
    /// What the Core's locomotion takes: the stick. A direction and how far it is pushed, folded into one vector of length 0..1 (exactly what the joystick gives). It is the ONE input of
    /// <see cref="LocomotionStepper"/>; speed, sprint and stamina follow from it as always (there is no sprint flag).
    /// </summary>
    public struct LocomotionInput
    {
        public Vec2 Stick;
        public LocomotionInput(Vec2 stick) { Stick = stick; }
    }

    /// <summary>
    /// A request to MOVE, independent of who makes it (a person's stick or, later, the AI): a direction, how hard (0..1), optionally the point he is heading to, a priority (when several
    /// sources want to move the same player in the same step, the higher wins; the same priority: the last one) and who it comes from. It carries no decision logic: it is converted to a
    /// <see cref="LocomotionInput"/> and from there everything is the Phase-3 locomotion.
    /// </summary>
    public struct MovementIntent
    {
        /// <summary>Wanted direction in field space (need not be unit length; zero = stand).</summary>
        public Vec2 Direction;
        /// <summary>0..1: how far the stick is pushed (0 = stand, 1 = full).</summary>
        public float Magnitude;
        public bool HasTarget;
        /// <summary>Where he is heading (informative, and used by <see cref="TowardsTarget"/>).</summary>
        public Vec2 Target;
        /// <summary>Higher wins within one step. 0 = default.</summary>
        public int Priority;
        public ControlSource Source;

        public static MovementIntent Stand { get { return default(MovementIntent); } }

        public static MovementIntent Move(Vec2 direction, float magnitude, ControlSource source = ControlSource.Ai, int priority = 0)
        {
            return new MovementIntent { Direction = direction, Magnitude = magnitude, Source = source, Priority = priority };
        }

        /// <summary>
        /// Head for a point: the direction from <paramref name="from"/> to the target, full <paramref name="magnitude"/> until he is within <paramref name="arriveRadius"/>, then it fades to 0 at the
        /// point (no overshoot, no jitter). Pure geometry.
        /// </summary>
        public static MovementIntent TowardsTarget(Vec2 from, Vec2 target, float magnitude, float arriveRadius, ControlSource source = ControlSource.Ai, int priority = 0)
        {
            Vec2 d = target - from;
            float dist = d.Magnitude;
            float scale = arriveRadius > 0f && dist < arriveRadius ? dist / arriveRadius : 1f;
            return new MovementIntent { Direction = dist > 0f ? d * (1f / dist) : Vec2.Zero, Magnitude = magnitude * scale, HasTarget = true, Target = target, Source = source, Priority = priority };
        }

        /// <summary>The stick this intent means: zero when it is not finite, otherwise direction x magnitude, never longer than 1.</summary>
        public LocomotionInput ToLocomotion()
        {
            if (!ActionModel.IsFinite(Direction) || float.IsNaN(Magnitude) || float.IsInfinity(Magnitude)) return new LocomotionInput(Vec2.Zero);
            Vec2 dir = Direction.Normalized;
            float m = ActionModel.Clamp01(Magnitude);
            return new LocomotionInput(dir * m);
        }
    }
}
