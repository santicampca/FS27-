using System;

namespace FS27.Core
{
    /// <summary>
    /// The four walls of the Arena pitch, as one ball collider. The ball REBOUNDS off them (same maths as <see cref="PlaneCollider"/>), so it never leaves play:
    /// there is no throw-in, corner or goal kick. The two goal mouths are open (a goal is judged by the rules, not by a wall). The walls have no top: the
    /// ball cannot fly out over them. Restitution and friction come from <see cref="MatchSettings"/> and are TUNING parameters.
    /// Every bounce is counted, so the engine can publish a "bounced off a wall" event without the physics knowing about events.
    /// </summary>
    public sealed class ArenaWalls : IBallCollider
    {
        private readonly FieldDimensions field;
        private readonly PlaneCollider sidePositive, sideNegative, endPositive, endNegative;

        /// <summary>How many wall contacts have happened so far. Grows only.</summary>
        public int BounceCount { get; private set; }
        public Vec3 LastBouncePosition { get; private set; }

        public ArenaWalls(FieldDimensions field, float restitution, float friction)
        {
            this.field = field ?? throw new ArgumentNullException(nameof(field));
            float halfL = field.Length * 0.5f;
            float halfW = field.Width * 0.5f;
            // normals point INTO the pitch; Offset = signed distance of the plane along its normal
            sidePositive = new PlaneCollider(new Vec3(0f, 0f, -1f), -halfW, restitution, friction);
            sideNegative = new PlaneCollider(new Vec3(0f, 0f, 1f), -halfW, restitution, friction);
            endPositive = new PlaneCollider(new Vec3(-1f, 0f, 0f), -halfL, restitution, friction);
            endNegative = new PlaneCollider(new Vec3(1f, 0f, 0f), -halfL, restitution, friction);
        }

        public bool Resolve(ref BallState state, BallParameters p)
        {
            bool hit = false;
            hit |= Touch(sidePositive, ref state, p);
            hit |= Touch(sideNegative, ref state, p);
            if (!InGoalMouth(state, p))
            {
                hit |= Touch(endPositive, ref state, p);
                hit |= Touch(endNegative, ref state, p);
            }
            return hit;
        }

        private bool Touch(PlaneCollider plane, ref BallState state, BallParameters p)
        {
            Vec3 before = state.Velocity;
            if (!plane.Resolve(ref state, p)) return false;
            // a contact that was already moving away (pushed out only) is not a bounce
            if (state.Velocity.X == before.X && state.Velocity.Y == before.Y && state.Velocity.Z == before.Z) return true;
            BounceCount++;
            LastBouncePosition = state.Position;
            return true;
        }

        /// <summary>The ball is between the posts and under the bar: the end wall is open there (same test as the goal judgement).</summary>
        private bool InGoalMouth(in BallState s, BallParameters p)
        {
            return Math.Abs(s.Position.Z) <= field.GoalWidth * 0.5f && s.Position.Y - p.Radius <= field.GoalHeight;
        }
    }
}
