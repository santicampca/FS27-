namespace FS27.Core
{
    /// <summary>
    /// Stamina rules. Sprinting drains the tank in proportion to sprint intensity; otherwise it refills
    /// gradually. Hitting zero locks sprint (exhausted) until the tank recovers past a threshold.
    /// Kept deliberately small: fatigue / long-match tiredness can hook in here later.
    /// </summary>
    public static class StaminaSystem
    {
        public static void Step(PlayerRuntimeState s, in PlayerStats stats, MovementTuning t, float stickMagnitude, float dt)
        {
            s.IsSprinting = s.SprintIntensity > 0f && s.Speed >= t.MinSprintingSpeed;

            if (s.IsSprinting)
            {
                s.Stamina -= s.SprintIntensity * dt;
                if (s.Stamina <= 0f)
                {
                    s.Stamina = 0f;
                    s.IsExhausted = true;
                    s.IsSprinting = false;
                }
            }
            else if (s.Stamina < s.StaminaCapacity)
            {
                float rate = stats.RecoverySeconds > 0f ? s.StaminaCapacity / stats.RecoverySeconds : s.StaminaCapacity;
                float factor = stickMagnitude <= t.JogEnd ? 1f : t.RunRecoveryFactor;
                s.Stamina += rate * factor * dt;
                if (s.Stamina > s.StaminaCapacity) s.Stamina = s.StaminaCapacity;
            }

            if (s.IsExhausted && s.Stamina01 >= t.ExhaustionRecoverFraction)
                s.IsExhausted = false;
        }
    }
}
