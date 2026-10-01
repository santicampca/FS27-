namespace FS27.Core
{
    /// <summary>
    /// A surface the ball bounces off (walls, boards, later posts and crossbar). The ground is built into
    /// <see cref="BallSimulator"/> because it also drives rolling; everything else plugs in through this.
    /// </summary>
    public interface IBallCollider
    {
        /// <summary>Resolves a contact: pushes the ball out and changes its velocity. Returns true if it collided.</summary>
        bool Resolve(ref BallState state, BallParameters parameters);
    }
}
