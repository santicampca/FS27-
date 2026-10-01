namespace FS27.Core
{
    /// <summary>
    /// What a camera mode may look at this frame. The game fills it; modes never reach into the scene.
    /// Velocities are supplied by the caller, so modes need no history of their own.
    /// </summary>
    public struct CameraContext
    {
        public Vec3 TargetPosition;
        public Vec3 TargetVelocity;
        public bool HasBall;
        public Vec3 BallPosition;
        public Vec3 BallVelocity;
    }
}
