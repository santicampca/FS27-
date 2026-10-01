using System;

namespace FS27.Core
{
    /// <summary>
    /// Critically-damped smoothing (same model as Unity's SmoothDamp), so smoothing logic can live in Core
    /// and be unit-tested. Stable for any dt; never overshoots the target.
    /// </summary>
    public static class SmoothMath
    {
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float dt)
        {
            if (dt <= 0f) return current;
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;

            // Prevent overshoot.
            if ((target - current > 0f) == (output > target))
            {
                output = target;
                velocity = (output - target) / dt;
            }
            return output;
        }

        public static Vec3 SmoothDamp(Vec3 current, Vec3 target, ref Vec3 velocity, float smoothTime, float dt)
        {
            float vx = velocity.X, vy = velocity.Y, vz = velocity.Z;
            Vec3 result = new Vec3(
                SmoothDamp(current.X, target.X, ref vx, smoothTime, dt),
                SmoothDamp(current.Y, target.Y, ref vy, smoothTime, dt),
                SmoothDamp(current.Z, target.Z, ref vz, smoothTime, dt));
            velocity = new Vec3(vx, vy, vz);
            return result;
        }

        /// <summary>0..1 ease in/out.</summary>
        public static float SmoothStep(float t)
        {
            t = MathUtil.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
