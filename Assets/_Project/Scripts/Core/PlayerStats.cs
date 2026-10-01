namespace FS27.Core
{
    /// <summary>
    /// Attributes already converted to game values (m/s, seconds). Cheap struct, resolved from
    /// <see cref="PlayerAttributes"/> + <see cref="MovementTuning"/>. Behaviour reads these, never raw ratings.
    /// </summary>
    public struct PlayerStats
    {
        /// <summary>Max sprint speed in m/s.</summary>
        public float TopSpeed;
        /// <summary>Acceleration in m/s^2.</summary>
        public float Acceleration;
        /// <summary>Braking deceleration in m/s^2.</summary>
        public float Braking;
        /// <summary>Tank size, expressed as seconds of full sprint.</summary>
        public float StaminaCapacity;
        /// <summary>Seconds to refill an empty tank when resting.</summary>
        public float RecoverySeconds;

        public static PlayerStats Resolve(in PlayerAttributes a, MovementTuning t)
        {
            float speed = PlayerAttributes.Normalize(a.Speed);
            float accel = PlayerAttributes.Normalize(a.Acceleration);
            float stamina = PlayerAttributes.Normalize(a.Stamina);

            float acceleration = MathUtil.Lerp(t.MinAcceleration, t.MaxAcceleration, accel);
            return new PlayerStats
            {
                TopSpeed = MathUtil.Lerp(t.MinTopSpeed, t.MaxTopSpeed, speed),
                Acceleration = acceleration,
                Braking = acceleration * t.BrakingMultiplier,
                StaminaCapacity = MathUtil.Lerp(t.MinSprintSeconds, t.MaxSprintSeconds, stamina),
                RecoverySeconds = MathUtil.Lerp(t.SlowestRecoverySeconds, t.FastestRecoverySeconds, stamina)
            };
        }
    }
}
