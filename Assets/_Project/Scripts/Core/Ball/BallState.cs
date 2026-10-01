namespace FS27.Core
{
    /// <summary>Complete kinematic state of the ball. Plain data: copy it freely (e.g. to predict ahead).</summary>
    public struct BallState
    {
        public Vec3 Position;
        public Vec3 Velocity;
        /// <summary>Angular velocity in rad/s (axis * speed).</summary>
        public Vec3 Spin;
        /// <summary>True while resting/rolling on the ground.</summary>
        public bool IsGrounded;

        public BallState(Vec3 position, Vec3 velocity)
        {
            Position = position;
            Velocity = velocity;
            Spin = Vec3.Zero;
            IsGrounded = false;
        }

        public float Speed => Velocity.Magnitude;
        public float HorizontalSpeed => new Vec3(Velocity.X, 0f, Velocity.Z).Magnitude;
    }
}
