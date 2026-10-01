namespace FS27.Core
{
    /// <summary>
    /// What a player "wants to do" this frame. Produced by an <see cref="IIntentSource"/>
    /// (touch, keyboard, gamepad, AI, replay...) and consumed by player systems.
    /// The consumer never knows where the intent came from.
    /// Phase 1 only needs movement; actions (pass/shoot/...) will be added here later.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>
        /// Desired movement in FIELD space (X = right, Y = up the screen), length 0..1.
        /// The length is the stick deflection: it selects jog / run / sprint.
        /// </summary>
        public Vec2 Move;

        public static readonly PlayerIntent None = new PlayerIntent();

        public PlayerIntent(Vec2 move)
        {
            float sqr = move.SqrMagnitude;
            Move = sqr > 1f ? move.Normalized : move;
        }

        public float MoveMagnitude => Move.Magnitude;
    }
}
