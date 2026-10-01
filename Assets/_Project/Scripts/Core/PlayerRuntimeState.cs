namespace FS27.Core
{
    /// <summary>
    /// Everything about a player that changes while playing (as opposed to attributes, which are static).
    /// Plain data, mutated only by <see cref="PlayerLocomotion"/>. Views/UI read it.
    /// </summary>
    public class PlayerRuntimeState
    {
        /// <summary>Facing / travel direction in radians (0 = +Y field axis, clockwise toward +X).</summary>
        public float Heading;
        /// <summary>Current speed in m/s along <see cref="Heading"/>.</summary>
        public float Speed;

        /// <summary>Stamina left, in seconds of full sprint.</summary>
        public float Stamina;
        /// <summary>Tank size, in seconds of full sprint.</summary>
        public float StaminaCapacity;

        /// <summary>True while the stick is in the sprint zone and the player is really moving.</summary>
        public bool IsSprinting;
        /// <summary>True after stamina hit zero, until it recovers past the threshold.</summary>
        public bool IsExhausted;
        /// <summary>0..1: how deep into the sprint zone the stick is (drain scales with it).</summary>
        public float SprintIntensity;
        /// <summary>Speed the locomotion is currently aiming for (debug / animation).</summary>
        public float TargetSpeed;

        public float Stamina01 => StaminaCapacity > 0f ? MathUtil.Clamp01(Stamina / StaminaCapacity) : 0f;
        public Vec2 Velocity => Vec2.FromHeading(Heading) * Speed;

        public void Reset(in PlayerStats stats, float heading)
        {
            Heading = heading;
            Speed = 0f;
            StaminaCapacity = stats.StaminaCapacity;
            Stamina = stats.StaminaCapacity;
            IsSprinting = false;
            IsExhausted = false;
            SprintIntensity = 0f;
            TargetSpeed = 0f;
        }

        /// <summary>Apply a new tank size (e.g. tuning edited live), keeping the same fill fraction.</summary>
        public void SetCapacity(float capacity)
        {
            if (capacity <= 0f || System.Math.Abs(capacity - StaminaCapacity) < 1e-5f) return;
            float fraction = Stamina01;
            StaminaCapacity = capacity;
            Stamina = fraction * capacity;
        }
    }
}
