using System;

namespace FS27.Core
{
    /// <summary>
    /// Pure locomotion model: intent -> heading + speed (+ stamina). No Unity types, so it is fully unit-testable.
    ///
    /// Model: the player has a heading and a scalar speed along it. Each step:
    ///  1. stick deflection (after deadzone) picks a target speed (jog / run / progressive sprint),
    ///     capped while exhausted;
    ///  2. heading turns toward the stick direction at a speed-dependent rate;
    ///  3. a large heading error lowers the target speed (you must slow down to turn sharply);
    ///  4. speed moves toward the target: accelerating (with falloff near top speed) or braking.
    /// Velocity = heading * speed, so there is always a little inertia.
    /// </summary>
    public static class PlayerLocomotion
    {
        public static void Step(PlayerRuntimeState s, in PlayerStats stats, MovementTuning t, in PlayerIntent intent, float dt)
        {
            if (dt <= 0f) return;

            float m = InputCurve.ApplyDeadzone(intent.MoveMagnitude, t);

            // 1. Target speed from stick deflection.
            float fraction = InputCurve.SpeedFraction(m, t);
            if (s.IsExhausted && fraction > t.ExhaustedSpeedFraction) fraction = t.ExhaustedSpeedFraction;
            s.SprintIntensity = s.IsExhausted ? 0f : InputCurve.SprintIntensity(m, t);

            float targetSpeed = fraction * stats.TopSpeed;

            // 2 + 3. Turn toward the stick; sharp turns cost speed.
            if (m > 0f)
            {
                float desired = intent.Move.ToHeading();
                float error = MathUtil.DeltaAngle(s.Heading, desired);
                float absErrorDeg = Math.Abs(error) * MathUtil.Rad2Deg;

                float speed01 = stats.TopSpeed > 0f ? MathUtil.Clamp01(s.Speed / stats.TopSpeed) : 0f;
                float turnRate = MathUtil.Lerp(t.TurnRateAtRest, t.TurnRateAtTopSpeed, speed01) * MathUtil.Deg2Rad;
                float maxTurn = turnRate * dt;
                s.Heading += Math.Abs(error) <= maxTurn ? error : Math.Sign(error) * maxTurn;
                s.Heading = WrapHeading(s.Heading);

                float penalty = MathUtil.InverseLerp(t.TurnSlowdownStartDeg, t.TurnSlowdownFullDeg, absErrorDeg);
                targetSpeed *= MathUtil.Lerp(1f, t.MinTurnSpeedFactor, penalty);
            }
            else
            {
                targetSpeed = 0f;
            }
            s.TargetSpeed = targetSpeed;

            // 4. Accelerate or brake toward the target.
            if (targetSpeed > s.Speed)
            {
                float speed01 = stats.TopSpeed > 0f ? MathUtil.Clamp01(s.Speed / stats.TopSpeed) : 0f;
                float accel = stats.Acceleration * (1f - t.AccelerationFalloff * speed01);
                s.Speed = Math.Min(targetSpeed, s.Speed + accel * dt);
            }
            else
            {
                s.Speed = Math.Max(targetSpeed, s.Speed - stats.Braking * dt);
            }

            StaminaSystem.Step(s, stats, t, m, dt);
        }

        private static float WrapHeading(float h)
        {
            const float twoPi = (float)(2.0 * Math.PI);
            h %= twoPi;
            if (h > Math.PI) h -= twoPi;
            else if (h <= -Math.PI) h += twoPi;
            return h;
        }
    }
}
