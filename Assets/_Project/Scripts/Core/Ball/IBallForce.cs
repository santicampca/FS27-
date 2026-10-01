namespace FS27.Core
{
    /// <summary>Extra acceleration acting on the ball (wind, a power-up, a scripted effect...).</summary>
    public interface IBallForce
    {
        /// <summary>Acceleration in m/s^2 for the ball in the given state.</summary>
        Vec3 Acceleration(in BallState state, BallParameters parameters);
    }
}
