using System;

namespace FS27.Core
{
    /// <summary>
    /// Development helper: makes digital keys (WASD) behave like an analogue stick so the whole
    /// jog -> run -> sprint range can be tested on a PC. It only produces a stick DEFLECTION; the
    /// "push to the edge" modifier is the keyboard equivalent of moving your thumb to the rim, and
    /// nothing in the output says "sprint". The deflection ramps up instead of jumping.
    /// </summary>
    public sealed class DigitalStickEmulator
    {
        public float LightDeflection = 0.22f;
        public float NormalDeflection = 0.60f;
        public float FullDeflection = 1.0f;
        /// <summary>How fast the emulated stick moves toward its target, deflection per second.</summary>
        public float RampPerSecond = 1.5f;

        private float current;

        public PlayerIntent Update(float x, float y, bool light, bool pushToEdge, float dt)
        {
            if (x == 0f && y == 0f)
            {
                current = 0f;
                return PlayerIntent.None;
            }

            float target = pushToEdge ? FullDeflection : (light ? LightDeflection : NormalDeflection);
            current = current <= 0f
                ? Math.Min(target, NormalDeflection)
                : MathUtil.MoveTowards(current, target, RampPerSecond * Math.Max(0f, dt));

            Vec2 dir = new Vec2(x, y).Normalized;
            return new PlayerIntent(dir * current);
        }

        public void Reset()
        {
            current = 0f;
        }
    }
}
