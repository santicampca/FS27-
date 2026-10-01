namespace FS27.Core
{
    /// <summary>
    /// Stick deflection (0..1) -> speed and sprint intensity. Piecewise linear, driven only by tuning:
    /// 0..JogEnd = walk/jog, JogEnd..RunEnd = run, RunEnd..SprintFullAt = progressive sprint, then max.
    /// </summary>
    public static class InputCurve
    {
        /// <summary>Removes the deadzone and rescales so the usable range is still 0..1.</summary>
        public static float ApplyDeadzone(float magnitude, MovementTuning t)
        {
            float m = MathUtil.Clamp01(magnitude);
            if (m <= t.InputDeadzone) return 0f;
            return (m - t.InputDeadzone) / (1f - t.InputDeadzone);
        }

        /// <summary>Fraction (0..1) of top speed requested at this (deadzone-free) deflection.</summary>
        public static float SpeedFraction(float m, MovementTuning t)
        {
            if (m <= 0f) return 0f;
            if (m <= t.JogEnd)
                return t.JogSpeedFraction * MathUtil.InverseLerp(0f, t.JogEnd, m);
            if (m <= t.RunEnd)
                return MathUtil.Lerp(t.JogSpeedFraction, t.RunSpeedFraction, MathUtil.InverseLerp(t.JogEnd, t.RunEnd, m));
            if (m < t.SprintFullAt)
                return MathUtil.Lerp(t.RunSpeedFraction, 1f, MathUtil.InverseLerp(t.RunEnd, t.SprintFullAt, m));
            return 1f;
        }

        /// <summary>0 below the sprint zone, rising to 1 at SprintFullAt. Scales stamina drain.</summary>
        public static float SprintIntensity(float m, MovementTuning t)
        {
            return MathUtil.InverseLerp(t.RunEnd, t.SprintFullAt, m);
        }
    }
}
