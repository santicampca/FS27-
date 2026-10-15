using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 28: how hard to kick a pass so that it ARRIVES. The decision layer used to ask for power linear in the distance, which sent a 9 m pass out at 13 m/s (it passed the receiver at 12 m/s and rolled 30 m) and a 14 m "long" pass
    /// out at 19 m/s (it landed 30 m away, often over the line): only four passes in ten reached a team-mate. This is the pure maths of the reference ball (<see cref="BallSimulator"/>) turned round: given the distance and the speed the
    /// ball should have when it reaches the receiver, the launch speed; and from the launch speed and the passer's quality, the power the action wants. It reads the ball parameters, changes nothing and uses no chance, so the same
    /// view always gives the same pass. Mode-independent (Arena and Classic share the ball); the numbers live in <see cref="BasicDecisionDefaults"/>.
    /// </summary>
    public static class PassPhysics
    {
        private static readonly BallParameters Ball = new BallParameters();
        private static readonly float[] LoftSpeeds;
        private static readonly float[] LoftLanding;
        private static readonly float[] LoftTime;

        static PassPhysics()
        {
            // the first landing distance of a lofted kick of every launch speed, from the reference ball itself (so a change of the ball parameters changes the passes with it)
            float min = ActionDefaults.LongPassMinSpeed, max = ActionDefaults.LongPassMaxSpeed, step = PassPhysicsLoftStep;
            int n = (int)Math.Round((max - min) / step) + 1;
            LoftSpeeds = new float[n]; LoftLanding = new float[n]; LoftTime = new float[n];
            for (int i = 0; i < n; i++) { LoftSpeeds[i] = min + step * i; LoftLanding[i] = FirstLanding(LoftSpeeds[i], out LoftTime[i]); }
        }

        private const float PassPhysicsLoftStep = 0.25f, SimStep = 1f / 60f, SimMaxSeconds = 6f, Epsilon = 1e-4f;

        private static float FirstLanding(float speed, out float time)
        {
            time = SimMaxSeconds;
            var s = new BallState { Position = new Vec3(0f, Ball.Radius, 0f), Velocity = new Vec3(speed, speed * ActionDefaults.LongPassLoft, 0f), IsGrounded = false };
            bool wasAir = false;
            for (float t = 0f; t < SimMaxSeconds; t += SimStep)
            {
                BallSimulator.Step(ref s, Ball, SimStep);
                if (s.Position.Y > Ball.Radius + Epsilon) wasAir = true;
                else if (wasAir) { time = t + SimStep; return s.Position.X; }
            }
            return s.Position.X;
        }

        /// <summary>The launch speed (m/s) of a ground pass that still has <paramref name="arrivalSpeed"/> when it has travelled <paramref name="distance"/> (rolling resistance and air drag of the reference ball).</summary>
        public static float GroundLaunchSpeed(float distance, float arrivalSpeed)
        {
            float a = Ball.RollingDeceleration, k = Ball.AirDragCoefficient;
            if (k <= 0f) return (float)Math.Sqrt(arrivalSpeed * arrivalSpeed + 2f * a * Math.Max(0f, distance));
            double grow = Math.Exp(2.0 * k * Math.Max(0f, distance));
            double v2 = ((a + k * arrivalSpeed * arrivalSpeed) * grow - a) / k;
            return (float)Math.Sqrt(Math.Max(0.0, v2));
        }

        /// <summary>The launch speed of a lofted kick whose first bounce is at <paramref name="landing"/> metres.</summary>
        public static float LoftedLaunchSpeed(float landing)
        {
            if (landing <= LoftLanding[0]) return LoftSpeeds[0];
            for (int i = 1; i < LoftSpeeds.Length; i++)
                if (LoftLanding[i] >= landing)
                {
                    float span = LoftLanding[i] - LoftLanding[i - 1];
                    float k = span > Epsilon ? (landing - LoftLanding[i - 1]) / span : 0f;
                    return LoftSpeeds[i - 1] + (LoftSpeeds[i] - LoftSpeeds[i - 1]) * k;
                }
            return LoftSpeeds[LoftSpeeds.Length - 1];
        }

        /// <summary>Seconds a ground ball launched at <paramref name="launchSpeed"/> takes to cover <paramref name="distance"/> (rolling resistance and air drag of the reference ball).</summary>
        public static float GroundTime(float launchSpeed, float distance)
        {
            float a = Ball.RollingDeceleration, k = Ball.AirDragCoefficient;
            if (k <= 0f)
            {
                float vStop = (float)Math.Sqrt(Math.Max(0f, launchSpeed * launchSpeed - 2f * a * distance));
                return (launchSpeed - vStop) / Math.Max(Epsilon, a);
            }
            double vEnd2 = ((a + k * launchSpeed * launchSpeed) * Math.Exp(-2.0 * k * Math.Max(0f, distance)) - a) / k;
            double vEnd = Math.Sqrt(Math.Max(0.0, vEnd2));
            double root = Math.Sqrt(a * k), scale = Math.Sqrt(k / a);
            return (float)((Math.Atan(launchSpeed * scale) - Math.Atan(vEnd * scale)) / root);
        }

        /// <summary>Seconds a lofted kick of this launch speed spends in the air before its first bounce.</summary>
        public static float LoftedFlightTime(float launchSpeed)
        {
            if (launchSpeed <= LoftSpeeds[0]) return LoftTime[0];
            for (int i = 1; i < LoftSpeeds.Length; i++)
                if (LoftSpeeds[i] >= launchSpeed)
                {
                    float k = (launchSpeed - LoftSpeeds[i - 1]) / Math.Max(Epsilon, LoftSpeeds[i] - LoftSpeeds[i - 1]);
                    return LoftTime[i - 1] + (LoftTime[i] - LoftTime[i - 1]) * k;
                }
            return LoftTime[LoftTime.Length - 1];
        }

        /// <summary>The first-bounce distance of the shortest and of the longest lofted kick.</summary>
        public static float LoftedMinLanding { get { return LoftLanding[0]; } }
        public static float LoftedMaxLanding { get { return LoftLanding[LoftLanding.Length - 1]; } }

        /// <summary>The power (0..1) that makes the kick leave at <paramref name="speed"/>, for a passer of this quality (the action scales the speed by KickSpeedQualityBase + (1 - base) x quality).</summary>
        public static float PowerFor(float speed, float minSpeed, float maxSpeed, float quality)
        {
            float q = ActionModel.Clamp01(quality);
            float factor = ActionDefaults.KickSpeedQualityBase + (1f - ActionDefaults.KickSpeedQualityBase) * q;
            float wanted = speed / Math.Max(Epsilon, factor);
            return ActionModel.Clamp01((wanted - minSpeed) / Math.Max(Epsilon, maxSpeed - minSpeed));
        }

        /// <summary>Power of a ground pass (short, wall or through: <paramref name="speedBoost"/> is the through-ball boost, 1 otherwise).</summary>
        public static float GroundPower(float distance, float arrivalSpeed, float quality, float speedBoost)
        {
            return PowerFor(GroundLaunchSpeed(distance, arrivalSpeed), ActionDefaults.PassMinSpeed * speedBoost, ActionDefaults.PassMaxSpeed * speedBoost, quality);
        }

        /// <summary>Power of a lofted pass (or a long goalkeeper kick) that bounces <paramref name="landing"/> metres away.</summary>
        public static float LoftedPower(float landing, float quality)
        {
            return PowerFor(LoftedLaunchSpeed(landing), ActionDefaults.LongPassMinSpeed, ActionDefaults.LongPassMaxSpeed, quality);
        }
    }
}
