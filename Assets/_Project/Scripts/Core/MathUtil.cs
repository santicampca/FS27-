using System;

namespace FS27.Core
{
    /// <summary>Small math helpers (Core has no access to UnityEngine.Mathf).</summary>
    public static class MathUtil
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float d = target - current;
            if (Math.Abs(d) <= maxDelta) return target;
            return current + Math.Sign(d) * maxDelta;
        }

        /// <summary>Shortest signed angle (radians) from a to b, in (-PI, PI].</summary>
        public static float DeltaAngle(float a, float b)
        {
            double d = (b - a) % (2.0 * Math.PI);
            if (d > Math.PI) d -= 2.0 * Math.PI;
            else if (d <= -Math.PI) d += 2.0 * Math.PI;
            return (float)d;
        }

        /// <summary>Normalised position of v inside [a, b] (clamped to 0..1). Safe when a == b.</summary>
        public static float InverseLerp(float a, float b, float v)
        {
            float span = b - a;
            if (Math.Abs(span) < 1e-6f) return v >= b ? 1f : 0f;
            return Clamp01((v - a) / span);
        }
    }
}
