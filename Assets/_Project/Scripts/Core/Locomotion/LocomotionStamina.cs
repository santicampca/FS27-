using System;

namespace FS27.Core
{
    /// <summary>
    /// The stamina rules of locomotion, all continuous. CurrentStamina is 0..100 and is never below 0 nor above 100.
    ///  Performance: a curve through the points of the tuning (100 -> 1.00 ... 0 -> 0.40); it scales top speed and (partly) acceleration.
    ///  Drain per second = BaseDrain x effort^2 x sprint multiplier / attribute factor, where effort is the input magnitude and the multiplier ramps from 1 at the sprint threshold to
    ///  its full value at full input (no jump). Walking drains almost nothing.
    ///  Recovery per second = BaseRecovery x attribute factor x zone factor: standing still 1, walking 0.25, running and sprinting 0.
    ///  Sprint availability (0..1) falls linearly from 1 at 40 stamina to 0 at 10: it limits how much of the sprint range can be used; the input is never blocked.
    /// </summary>
    public static class LocomotionStamina
    {
        public static float Clamp(float stamina, LocomotionTuning t)
        {
            if (float.IsNaN(stamina)) return 0f;
            return Math.Max(0f, Math.Min(t.MaxStamina, stamina));
        }

        /// <summary>1.0 at full stamina down to 0.40 at none (with the default points), linear between them.</summary>
        public static float Performance(float stamina, LocomotionTuning t)
        {
            float[] xs = t.PerformanceStamina, ys = t.PerformanceFactor;
            float s = Clamp(stamina, t);
            if (s <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
            {
                if (s <= xs[i])
                {
                    float k = (s - xs[i - 1]) / (xs[i] - xs[i - 1]);
                    return ys[i - 1] + (ys[i] - ys[i - 1]) * k;
                }
            }
            return ys[ys.Length - 1];
        }

        /// <summary>1 at or above the "full" stamina, 0 at or below the "low" one.</summary>
        public static float SprintAvailability(float stamina, LocomotionTuning t)
        {
            float k = (Clamp(stamina, t) - t.SprintAvailabilityLow) / (t.SprintAvailabilityFull - t.SprintAvailabilityLow);
            return Math.Max(0f, Math.Min(1f, k));
        }

        /// <summary>Stamina points lost per second at this input magnitude.</summary>
        public static float DrainPerSecond(float inputMagnitude, float attributeFactor, LocomotionTuning t)
        {
            float m = Math.Max(0f, Math.Min(1f, inputMagnitude));
            if (m <= LocomotionDefaults.InputEpsilon) return 0f;
            float sprint = Math.Max(0f, Math.Min(1f, (m - t.SprintThreshold) / (1f - t.SprintThreshold)));
            float multiplier = 1f + (t.SprintMultiplier - 1f) * sprint;
            return t.BaseStaminaDrain * m * m * multiplier / attributeFactor;
        }

        /// <summary>Stamina points gained per second at this input magnitude.</summary>
        public static float RecoveryPerSecond(float inputMagnitude, float attributeFactor, LocomotionTuning t)
        {
            float m = Math.Max(0f, Math.Min(1f, inputMagnitude));
            float zone = m <= LocomotionDefaults.InputEpsilon ? 1f : (m < t.WalkThreshold ? t.WalkRecoveryFactor : 0f);
            return t.BaseStaminaRecovery * attributeFactor * zone;
        }

        /// <summary>One step of drain and recovery. Returns the new stamina, always inside 0..100.</summary>
        public static float Step(float stamina, float inputMagnitude, float attributeFactor, float dt, LocomotionTuning t)
        {
            float net = RecoveryPerSecond(inputMagnitude, attributeFactor, t) - DrainPerSecond(inputMagnitude, attributeFactor, t);
            return Clamp(stamina + net * dt, t);
        }

        /// <summary>The recovery of an interval (half-time, side switch, extra-time breaks): a fraction of the MAXIMUM stamina, never above it.</summary>
        public static float RecoverFraction(float stamina, float fraction, LocomotionTuning t)
        {
            if (!(fraction > 0f)) return Clamp(stamina, t);
            return Math.Min(t.MaxStamina, Clamp(stamina, t) + t.MaxStamina * fraction);
        }
    }
}
