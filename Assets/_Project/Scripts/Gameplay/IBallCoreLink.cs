using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// The explicit door from Unity to the Core for the ball: a physical contact is reported here and the Core decides what happens to the ball.
    /// Unity never changes the authoritative ball state any other way.
    /// </summary>
    public interface IBallCoreLink
    {
        /// <summary>A soft touch: the player wants the ball to move at this horizontal velocity (strength 0..1 = how much of the difference is applied).</summary>
        void RequestTouch(Vector3 desiredHorizontalVelocity, float strength);
    }
}
