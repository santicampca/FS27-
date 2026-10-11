using System;

namespace FS27.Core
{
    /// <summary>
    /// The frame of one goal (Phase 23): two posts and the crossbar, as cylinders the ball rebounds off with <see cref="BallSimulator"/>'s own substeps. Plugs in like any <see cref="IBallCollider"/>; it counts the hits so the
    /// match can say "post" or "bar" without duplicating the physics. In this phase it is active during penalties only (open play still ignores the frame: making every shot rebound off the posts would change the balance of Classic).
    /// </summary>
    public sealed class GoalFrameCollider : IBallCollider
    {
        /// <summary>Signed X of the goal line this frame stands on.</summary>
        public readonly float GoalLineX;
        public readonly float HalfWidth;
        public readonly float Height;
        public readonly float PostRadius;
        public readonly float Restitution;

        public int PostHits { get; private set; }
        public int BarHits { get; private set; }
        /// <summary>Where the last hit was (the ball's position).</summary>
        public Vec3 LastHitPosition { get; private set; }
        /// <summary>1 = the last hit was a post, 2 = the bar, 0 = none yet.</summary>
        public int LastHitKind { get; private set; }

        public GoalFrameCollider(float goalLineX, float goalWidth, float goalHeight, float postRadius, float restitution)
        {
            GoalLineX = goalLineX;
            HalfWidth = goalWidth * 0.5f;
            Height = goalHeight;
            PostRadius = postRadius;
            Restitution = restitution;
        }

        public void ResetCounters() { PostHits = 0; BarHits = 0; LastHitKind = 0; }

        public bool Resolve(ref BallState state, BallParameters p)
        {
            bool hit = false;
            float minDistance = PostRadius + p.Radius;

            // the posts: vertical cylinders at the two ends of the goal line, from the ground to the crossbar
            if (state.Position.Y <= Height + p.Radius)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float dx = state.Position.X - GoalLineX, dz = state.Position.Z - s * HalfWidth;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d >= minDistance) continue;
                    Vec3 n = d > 1e-5f ? new Vec3(dx / d, 0f, dz / d) : new Vec3(-Math.Sign(GoalLineX), 0f, s);
                    Bounce(ref state, n, minDistance - d);
                    PostHits++; LastHitKind = 1; LastHitPosition = state.Position; hit = true;
                }
            }

            // the crossbar: a horizontal cylinder along the goal, between the posts
            if (Math.Abs(state.Position.Z) <= HalfWidth + PostRadius)
            {
                float dx = state.Position.X - GoalLineX, dy = state.Position.Y - Height;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < minDistance)
                {
                    Vec3 n = d > 1e-5f ? new Vec3(dx / d, dy / d, 0f) : new Vec3(0f, 1f, 0f);
                    Bounce(ref state, n, minDistance - d);
                    BarHits++; LastHitKind = 2; LastHitPosition = state.Position; hit = true;
                }
            }
            return hit;
        }

        private void Bounce(ref BallState state, Vec3 normal, float penetration)
        {
            state.Position += normal * penetration;
            float vn = Vec3.Dot(state.Velocity, normal);
            if (vn < 0f) state.Velocity -= normal * ((1f + Restitution) * vn);
            state.IsGrounded = false;
        }
    }
}
