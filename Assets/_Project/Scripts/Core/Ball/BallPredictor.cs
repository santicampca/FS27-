namespace FS27.Core
{
    /// <summary>
    /// Looks ahead along the ball's trajectory using the reference model. Works on copies, so it never
    /// touches the real ball. Foundation for goalkeeper reactions, interception and camera anticipation.
    /// </summary>
    public static class BallPredictor
    {
        /// <summary>Where the ball will be after <paramref name="seconds"/>, if nothing touches it.</summary>
        public static BallState Predict(BallState state, BallParameters p, float seconds,
                                        float step = 1f / 60f, IBallCollider[] colliders = null)
        {
            if (step <= 0f) return state;
            float remaining = seconds;
            while (remaining > 1e-6f)
            {
                float dt = remaining < step ? remaining : step;
                BallSimulator.Step(ref state, p, dt, colliders);
                remaining -= dt;
            }
            return state;
        }

        /// <summary>
        /// Fills <paramref name="positions"/> with the predicted positions at each step (index 0 = one step ahead).
        /// Returns how many were written. Allocation-free: the caller owns the buffer.
        /// </summary>
        public static int PredictPath(BallState state, BallParameters p, float step, Vec3[] positions,
                                      IBallCollider[] colliders = null)
        {
            if (positions == null || step <= 0f) return 0;
            for (int i = 0; i < positions.Length; i++)
            {
                BallSimulator.Step(ref state, p, step, colliders);
                positions[i] = state.Position;
            }
            return positions.Length;
        }
    }
}
