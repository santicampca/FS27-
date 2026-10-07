using System;

namespace FS27.Core
{
    /// <summary>
    /// One deterministic step of a player's locomotion: input -> target speed -> acceleration/braking -> turning -> velocity -> position, with stamina scaling the result.
    ///  1. The input (joystick) is sanitised and clamped: magnitude 0..1, direction unit length. It states an INTENTION; it never sets the speed.
    ///  2. Speed intensity = SmoothStep(magnitude) (continuous: no jumps between walk, run and sprint). The part of the sprint range above the run level can only be used in proportion to the
    ///     sprint availability (stamina), so a tired player at full stick simply goes slower.
    ///  3. Target speed = top speed x stamina performance x intensity. Speed moves towards it: accelerating with Acceleration (reduced by fatigue), braking with Agility. No teleporting.
    ///  4. The facing turns towards the input at a limited rate: base x agility factor x (1 - 0.55 x speed/top speed). Velocity = facing x speed, so a sprinter needs room to turn.
    ///  5. Position += velocity x dt, and stamina drains/recovers continuously.
    /// Only the fixed step and the player's own numbers are used: no Unity time, no randomness, no global state.
    /// </summary>
    public static class LocomotionStepper
    {
        public static void Step(ref LocomotionState s, in LocomotionProfile p, LocomotionTuning t, Vec2 input, float dt)
        {
            Step(ref s, in p, t, input, dt, default(LocomotionModifiers));
        }

        /// <summary>The same step with the movement modifiers of the player's skills (Phase 6). All-zero modifiers give exactly the step above.</summary>
        public static void Step(ref LocomotionState s, in LocomotionProfile p, LocomotionTuning t, Vec2 input, float dt, in LocomotionModifiers m)
        {
            if (!(dt > 0f)) return;

            // 1. the intention
            float magnitude = 0f;
            Vec2 direction = Vec2.Zero;
            float lenSqr = input.X * input.X + input.Y * input.Y;
            if (!float.IsNaN(lenSqr) && !float.IsInfinity(lenSqr) && lenSqr > 0f)
            {
                float len = (float)Math.Sqrt(lenSqr);
                magnitude = Math.Min(1f, len);
                if (magnitude > LocomotionDefaults.InputEpsilon) direction = new Vec2(input.X / len, input.Y / len);
                else magnitude = 0f;
            }
            s.InputMagnitude = magnitude;
            s.InputDirection = direction;

            // 2 + 3. target speed
            float performance = LocomotionStamina.Performance(s.CurrentStamina, t);
            float intensity = SmoothMath.SmoothStep(magnitude);
            float runLevel = SmoothMath.SmoothStep(t.SprintThreshold);
            float usable = runLevel + (1f - runLevel) * LocomotionStamina.SprintAvailability(s.CurrentStamina, t);
            if (intensity > usable) intensity = usable;
            float target = p.MaxSpeed * performance * intensity;

            // 4. turning (needs an input direction)
            float turnError = 0f;
            if (magnitude > 0f)
            {
                float error = MathUtil.DeltaAngle(s.Heading, direction.ToHeading());
                turnError = error;
                float speedRatio = p.MaxSpeed > 0f ? MathUtil.Clamp01(s.CurrentSpeed / p.MaxSpeed) : 0f;
                float turnFactor = 1f - t.TurnSpeedPenalty * (1f - m.TurnPenaltyRelief) * speedRatio;
                float rate = t.BaseTurnRateDegrees * MathUtil.Deg2Rad * p.AgilityFactor * turnFactor;
                float maxTurn = rate * dt;
                s.Heading += Math.Abs(error) <= maxTurn ? error : Math.Sign(error) * maxTurn;
                s.Heading = Wrap(s.Heading);
            }

            // 3. speed towards the target: accelerate (fatigue reduces it) or brake (agility)
            if (target > s.CurrentSpeed)
            {
                float acceleration = p.MaxAcceleration * (t.FatigueAccelerationBase + t.FatigueAccelerationRange * performance);
                float gain = 1f;
                if (m.BurstGain > 0f && p.MaxSpeed > 0f && s.CurrentSpeed < p.MaxSpeed * m.BurstSpeedFraction) gain += m.BurstGain;
                if (m.TurnGain > 0f && Math.Abs(turnError) > m.TurnAngleThreshold) gain += m.TurnGain;
                acceleration *= gain;
                s.CurrentSpeed = Math.Min(target, s.CurrentSpeed + acceleration * dt);
            }
            else
            {
                s.CurrentSpeed = Math.Max(target, s.CurrentSpeed - p.MaxDeceleration * dt);
            }

            // 5. position and stamina
            s.Velocity = Vec2.FromHeading(s.Heading) * s.CurrentSpeed;
            s.Position = s.Position + s.Velocity * dt;
            s.CurrentStamina = LocomotionStamina.Step(s.CurrentStamina, magnitude, p.StaminaAttributeFactor, dt, t);
            s.MovementState = Classify(s, t);
        }

        /// <summary>The descriptive label: Exhausted when the stamina is ~0, otherwise by input range. It changes nothing about the movement.</summary>
        public static MovementState Classify(in LocomotionState s, LocomotionTuning t)
        {
            if (s.CurrentStamina <= t.ExhaustedThreshold) return MovementState.Exhausted;
            if (s.InputMagnitude <= LocomotionDefaults.InputEpsilon) return MovementState.Idle;
            if (s.InputMagnitude < t.WalkThreshold) return MovementState.Walk;
            if (s.InputMagnitude < t.SprintThreshold) return MovementState.Run;
            return MovementState.Sprint;
        }

        private static float Wrap(float h)
        {
            const float twoPi = (float)(2.0 * Math.PI);
            h %= twoPi;
            if (h > Math.PI) h -= twoPi;
            else if (h <= -Math.PI) h += twoPi;
            return h;
        }
    }
}
