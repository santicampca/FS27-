using System;

namespace FS27.Core
{
    /// <summary>
    /// Physical constants of the ball for the Core reference model (<see cref="BallSimulator"/>).
    /// Defaults mirror the Unity ball (BallController / BallPhysics) so predictions agree with PhysX.
    /// Units: metres, seconds, radians.
    /// </summary>
    [Serializable]
    public class BallParameters
    {
        public float Radius = 0.2f;
        public float Mass = 0.43f;
        public float Gravity = 9.81f;

        /// <summary>Air drag: deceleration = coefficient * speed^2.</summary>
        public float AirDragCoefficient = 0.015f;
        /// <summary>Constant deceleration (m/s^2) while rolling on the ground.</summary>
        public float RollingDeceleration = 1.6f;
        /// <summary>Below this horizontal speed (m/s), on the ground, the ball comes to rest.</summary>
        public float RestSpeed = 0.12f;

        /// <summary>Fraction of vertical speed kept after hitting the ground (0..1).</summary>
        public float Restitution = 0.7f;
        /// <summary>Coulomb-like friction on impact: how much horizontal speed a bounce can remove.</summary>
        public float BounceFriction = 0.25f;
        /// <summary>Vertical impact speed (m/s) under which the ball stops bouncing and starts rolling.</summary>
        public float BounceThreshold = 0.5f;

        /// <summary>Magnus lift: acceleration = coefficient * (spin x velocity). 0 disables curved flight.</summary>
        public float MagnusCoefficient = 0f;
        /// <summary>Spin decay in the air, per second.</summary>
        public float SpinDamping = 0.3f;

        /// <summary>Internal integration step. Larger frames are split so results do not depend on frame rate much.</summary>
        public float MaxSubstep = 1f / 120f;
    }
}
