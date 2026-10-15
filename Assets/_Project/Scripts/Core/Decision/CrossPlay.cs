using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 36: every number of the cross as a team play. One place; the twins are in <see cref="CrossPlayTuning"/>. Metres. The pitch is 40 x 25 by default and the goal 7.3 wide.
    /// </summary>
    public static class CrossPlayDefaults
    {
        // ---- the intention: when the team without the ball understands that a cross is coming
        /// <summary>The carrier is this close to a touchline (metres from it) or closer.</summary>
        public const float WingBand = 6.0f;
        /// <summary>... and within this distance of the goal line he attacks (metres along the pitch).</summary>
        public const float ThreatGoalLineDistance = 16.0f;
        /// <summary>The team needs at least this many field players in the attack to send runners (the carrier and the one who stays back do not count).</summary>
        public const int MinPlayers = 3;

        // ---- where the runners go (distance from the goal line, and sideways from the middle: near = the carrier's side)
        public const float CentralDepth = 6.0f;
        public const float NearDepth = 3.0f, NearWide = 2.6f;
        public const float FarDepth = 3.8f, FarWide = 3.6f;
        public const float EdgeDepth = 11.5f, EdgeWide = 1.8f;
        /// <summary>A runner further than this from his point does not go (metres).</summary>
        public const float MaxRunDistance = 24.0f;
        /// <summary>The preference of a role for a point, added to the closeness (a role a player is built for).</summary>
        public const float RoleWeight = 2.0f;
        /// <summary>Closeness: every metre between a player and the point costs this much.</summary>
        public const float DistanceWeight = 0.12f;
        /// <summary>The runners arrive at this effort (the effort of the forward runs).</summary>
        public const float RunMagnitude = 0.95f;
        /// <summary>The carrier waits for the runners by carrying the ball along the flank until he is this close to the goal line (metres); only then does he have to cross or cut in.</summary>
        public const float DeliverDepth = 6.0f;
        /// <summary>He needs at least this many runners on their way to wait for them.</summary>
        public const int MinRunnersToWait = 1;
        /// <summary>The effort of the carry along the line while the runners get there (a jog, not a sprint).</summary>
        public const float CarryMagnitude = 0.55f;
        /// <summary>A runner counts as arrived when he is this close to his place (metres).</summary>
        public const float ArrivedDistance = 4.5f;

        // ---- the defence of the box (against a cross)
        /// <summary>An attacker is a runner of the cross when he is within this distance of the goal line and this far from the middle.</summary>
        public const float BoxDepth = 15.0f, BoxHalfWidth = 9.0f;
        /// <summary>The marker stands this far from his man, towards the goal (metres).</summary>
        public const float MarkStandOff = 1.3f;
        /// <summary>The defender who covers the near post stands this far in front of the goal line and this far from the middle, on the side of the ball.</summary>
        public const float PostDepth = 1.6f, PostWide = 3.0f;
        /// <summary>A defender further than this from the man he would mark (or the post) stays where he is.</summary>
        public const float MaxMarkDistance = 16.0f;
        public const float MarkMagnitude = 1.0f;

        // ---- the style of the cross
        /// <summary>Ground cross: the receiver is this far from the goal line or further (a cut-back) and the lane has this much room (metres).</summary>
        public const float GroundMinDepth = 8.0f, GroundLaneRoom = 1.4f;
        /// <summary>Driven cross: the receiver is this close to the goal line, nearer than this to the carrier, and the lane has this much room.</summary>
        public const float DrivenMaxDepth = 7.0f, DrivenMaxDistance = 13.0f, DrivenLaneRoom = 1.8f;
    }

    [Serializable]
    public sealed class CrossPlayTuning
    {
        public float WingBand = CrossPlayDefaults.WingBand;
        public float ThreatGoalLineDistance = CrossPlayDefaults.ThreatGoalLineDistance;
        public int MinPlayers = CrossPlayDefaults.MinPlayers;
        public float CentralDepth = CrossPlayDefaults.CentralDepth;
        public float NearDepth = CrossPlayDefaults.NearDepth, NearWide = CrossPlayDefaults.NearWide;
        public float FarDepth = CrossPlayDefaults.FarDepth, FarWide = CrossPlayDefaults.FarWide;
        public float EdgeDepth = CrossPlayDefaults.EdgeDepth, EdgeWide = CrossPlayDefaults.EdgeWide;
        public float MaxRunDistance = CrossPlayDefaults.MaxRunDistance;
        public float RoleWeight = CrossPlayDefaults.RoleWeight;
        public float DistanceWeight = CrossPlayDefaults.DistanceWeight;
        public float RunMagnitude = CrossPlayDefaults.RunMagnitude;
        public float DeliverDepth = CrossPlayDefaults.DeliverDepth;
        public int MinRunnersToWait = CrossPlayDefaults.MinRunnersToWait;
        public float CarryMagnitude = CrossPlayDefaults.CarryMagnitude;
        public float ArrivedDistance = CrossPlayDefaults.ArrivedDistance;
        public float BoxDepth = CrossPlayDefaults.BoxDepth, BoxHalfWidth = CrossPlayDefaults.BoxHalfWidth;
        public float MarkStandOff = CrossPlayDefaults.MarkStandOff;
        public float PostDepth = CrossPlayDefaults.PostDepth, PostWide = CrossPlayDefaults.PostWide;
        public float MaxMarkDistance = CrossPlayDefaults.MaxMarkDistance;
        public float MarkMagnitude = CrossPlayDefaults.MarkMagnitude;
        public float GroundMinDepth = CrossPlayDefaults.GroundMinDepth, GroundLaneRoom = CrossPlayDefaults.GroundLaneRoom;
        public float DrivenMaxDepth = CrossPlayDefaults.DrivenMaxDepth, DrivenMaxDistance = CrossPlayDefaults.DrivenMaxDistance, DrivenLaneRoom = CrossPlayDefaults.DrivenLaneRoom;
    }

    /// <summary>Where a team-mate goes when a cross is coming: the middle (penalty spot), the near post (the carrier's side), the far post, or the edge of the box (the cut-back and the second ball).</summary>
    public enum BoxRole
    {
        None = 0,
        Central,
        NearPost,
        FarPost,
        Edge
    }

    /// <summary>
    /// Phase 36: the cross as a play of the whole team, pure over the world view (same view, same answer). The attack reads the INTENTION (a team-mate with the ball on a flank in the attacking third) and sends runners to DIFFERENT points
    /// according to what each one is (role, closeness): nobody runs to a point somebody already runs to. The defence reads the same threat and answers with a marker for each attacker in the box and a defender on the near post.
    /// It decides where a player WANTS to go; the physics and the separation of the Core decide what happens.
    /// </summary>
    public sealed class CrossPlay
    {
        private readonly CrossPlayTuning t;
        private readonly BoxRole[] roles = new BoxRole[WorldState.MaxPlayers];
        private readonly Vec2[] points = new Vec2[WorldState.MaxPlayers];
        private readonly int[] marking = new int[WorldState.MaxPlayers];
        private readonly Vec2[] standAt = new Vec2[WorldState.MaxPlayers];
        private readonly bool[] taken = new bool[WorldState.MaxPlayers];
        public CrossPlayTuning Tuning => t;
        public CrossPlay(CrossPlayTuning tuning = null) { t = tuning ?? new CrossPlayTuning(); }

        // ------------------------------------------------------------------ the intention

        /// <summary>The carrier is on a flank in the attacking third: a cross is possible, so the team-mates read it as one.</summary>
        public bool Threat(WorldView view, in WorldPlayer carrier)
        {
            if (carrier.IsGoalkeeper) return false;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, carrier.Team);
            float toLine = Math.Abs(goal.X - carrier.Position.X);
            return Math.Abs(carrier.Position.Y) >= view.Field.Width / 2f - t.WingBand && toLine <= t.ThreatGoalLineDistance;
        }

        /// <summary>The point of the pitch of a box role for an attack towards <paramref name="goal"/>, with the ball on the side <paramref name="side"/> (+1 / -1 along Y).</summary>
        public Vec2 PointOf(BoxRole role, Vec2 goal, Vec2 attack, float side)
        {
            switch (role)
            {
                case BoxRole.Central: return new Vec2(goal.X - attack.X * t.CentralDepth, goal.Y);
                case BoxRole.NearPost: return new Vec2(goal.X - attack.X * t.NearDepth, goal.Y + side * t.NearWide);
                case BoxRole.FarPost: return new Vec2(goal.X - attack.X * t.FarDepth, goal.Y - side * t.FarWide);
                case BoxRole.Edge: return new Vec2(goal.X - attack.X * t.EdgeDepth, goal.Y - side * t.EdgeWide);
                default: return goal;
            }
        }

        private static float Lean(PlayerArchetype a, BoxRole role)
        {
            switch (role)
            {
                case BoxRole.Central: return a == PlayerArchetype.Finisher || a == PlayerArchetype.GoalHunter || a == PlayerArchetype.Target ? 1f : 0f;
                case BoxRole.NearPost: return a == PlayerArchetype.GoalHunter || a == PlayerArchetype.Explosive || a == PlayerArchetype.Target ? 1f : 0f;
                case BoxRole.FarPost: return a == PlayerArchetype.Winger || a == PlayerArchetype.Explosive || a == PlayerArchetype.Finisher ? 1f : 0f;
                case BoxRole.Edge: return a == PlayerArchetype.Creator || a == PlayerArchetype.Architect || a == PlayerArchetype.Engine || a == PlayerArchetype.Builder ? 1f : 0f;
                default: return 0f;
            }
        }

        private float RoleBias(in WorldPlayer p, BoxRole role)
        {
            float best = 0f;
            if (p.Roles != null)
                for (int i = 0; i < p.Roles.Count; i++) { float v = Lean(p.Roles[i].Role, role) * p.Roles[i].Affinity; if (v > best) best = v; }
            else if (p.Role == PlayerRole.Forward && (role == BoxRole.Central || role == BoxRole.NearPost)) best = 1f;
            else if (p.Role == PlayerRole.Midfielder && role == BoxRole.Edge) best = 1f;
            return best;
        }

        /// <summary>
        /// Gives the field players of the carrier's team their box role (read it with <see cref="RoleOf"/> and <see cref="TargetOf"/>). The deepest team-mate stays back to cover; of the rest, the roles are filled in the order central, far post,
        /// near post, edge: each is taken by the player with the best score (his role for that point, minus the metres to it), never by two.
        /// </summary>
        public void AssignRunners(WorldView view, int carrierIdx)
        {
            WorldPlayer carrier = view[carrierIdx];
            TeamSide team = carrier.Team;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, team), own = WorldGeometry.OwnGoal(view.Match, view.Field, team);
            Vec2 attack = (goal - own).Normalized; float side = carrier.Position.Y >= 0f ? 1f : -1f;
            int start = view.TeamStart(team), end = start + view.TeamCount(team);
            for (int i = 0; i < roles.Length; i++) { roles[i] = BoxRole.None; taken[i] = false; }
            int candidates = 0, deepest = -1; float deepestAlong = float.PositiveInfinity;
            for (int i = start; i < end; i++)
            {
                WorldPlayer p = view[i];
                if (i == carrierIdx || !p.IsOnPitch || p.IsGoalkeeper || p.IsControlled) continue;
                candidates++; float along = p.Position.X * attack.X + p.Position.Y * attack.Y;
                if (along < deepestAlong) { deepestAlong = along; deepest = i; }
            }
            if (candidates < t.MinPlayers) return;
            if (deepest >= 0) taken[deepest] = true;   // the one who stays back
            foreach (BoxRole role in new[] { BoxRole.Central, BoxRole.FarPost, BoxRole.NearPost, BoxRole.Edge })
            {
                Vec2 point = PointOf(role, goal, attack, side); int best = -1; float bestScore = float.NegativeInfinity;
                for (int i = start; i < end; i++)
                {
                    WorldPlayer p = view[i];
                    if (i == carrierIdx || taken[i] || !p.IsOnPitch || p.IsGoalkeeper || p.IsControlled) continue;
                    float d = (p.Position - point).Magnitude; if (d > t.MaxRunDistance) continue;
                    float score = t.RoleWeight * RoleBias(p, role) - t.DistanceWeight * d;
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) continue;
                taken[best] = true; roles[best] = role; points[best] = point;
            }
        }

        /// <summary>How many team-mates have a box role after <see cref="AssignRunners"/>.</summary>
        public int RunnerCount { get { int n = 0; for (int i = 0; i < roles.Length; i++) if (roles[i] != BoxRole.None) n++; return n; } }

        /// <summary>How many of the runners are already at their place.</summary>
        public int ArrivedCount(WorldView view)
        {
            int n = 0;
            for (int i = 0; i < roles.Length; i++) if (roles[i] != BoxRole.None && (points[i] - view[i].Position).Magnitude <= t.ArrivedDistance) n++;
            return n;
        }

        public BoxRole RoleOf(int index) { return index >= 0 && index < roles.Length ? roles[index] : BoxRole.None; }
        public Vec2 TargetOf(int index) { return points[index]; }

        // ------------------------------------------------------------------ the defence

        /// <summary>The opponent carrier is on a flank near MY goal: the defence reads a cross.</summary>
        public bool DefenceThreat(WorldView view, TeamSide defending, in WorldPlayer carrier)
        {
            if (carrier.IsGoalkeeper || carrier.Team == defending) return false;
            return Threat(view, carrier);
        }

        /// <summary>
        /// Gives the defenders of <paramref name="defending"/> (other than <paramref name="primary"/> and <paramref name="secondary"/>) their task against the cross: the attackers in the box, the most dangerous (nearest to the goal) first, are
        /// marked by the nearest free defender, who stands between his man and the goal; the next free defender covers the near post. Read with <see cref="IsMarking"/> / <see cref="StandPointOf"/>.
        /// </summary>
        public void AssignDefenders(WorldView view, TeamSide defending, int carrierIdx, int primary, int secondary)
        {
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, defending), goalOfAttackers = own;
            Vec2 inward = (WorldGeometry.AttackGoal(view.Match, view.Field, defending) - own).Normalized;   // from my goal into the pitch
            WorldPlayer carrier = view[carrierIdx]; float side = carrier.Position.Y >= 0f ? 1f : -1f;
            int ds = view.TeamStart(defending), de = ds + view.TeamCount(defending);
            int os = view.TeamStart(defending.Opponent()), oe = os + view.TeamCount(defending.Opponent());
            for (int i = 0; i < marking.Length; i++) { marking[i] = -1; taken[i] = false; }
            // the attackers in the box, nearest to my goal first
            var runners = new int[WorldState.MaxPlayers]; int n = 0;
            for (int o = os; o < oe; o++)
            {
                WorldPlayer a = view[o];
                if (o == carrierIdx || !a.IsOnPitch || a.IsGoalkeeper) continue;
                if (Math.Abs(a.Position.X - own.X) > t.BoxDepth || Math.Abs(a.Position.Y - own.Y) > t.BoxHalfWidth) continue;
                runners[n++] = o;
            }
            for (int i = 1; i < n; i++) { int v = runners[i]; float dv = (view[v].Position - own).Magnitude; int j = i - 1; while (j >= 0 && (view[runners[j]].Position - own).Magnitude > dv) { runners[j + 1] = runners[j]; j--; } runners[j + 1] = v; }
            for (int r = 0; r < n; r++)
            {
                WorldPlayer a = view[runners[r]]; int best = -1; float bestD = float.PositiveInfinity;
                for (int d = ds; d < de; d++)
                {
                    WorldPlayer p = view[d];
                    if (d == primary || d == secondary || taken[d] || !p.IsOnPitch || p.IsGoalkeeper || p.IsControlled) continue;
                    float dist = (p.Position - a.Position).Magnitude; if (dist > t.MaxMarkDistance) continue;
                    if (dist < bestD) { bestD = dist; best = d; }
                }
                if (best < 0) continue;
                taken[best] = true; marking[best] = runners[r];
                standAt[best] = a.Position + (own - a.Position).Normalized * t.MarkStandOff;
            }
            // the near post: the nearest free defender, on the side of the ball
            Vec2 post = new Vec2(own.X + inward.X * t.PostDepth, own.Y + side * t.PostWide); int cover = -1; float coverD = float.PositiveInfinity;
            for (int d = ds; d < de; d++)
            {
                WorldPlayer p = view[d];
                if (d == primary || d == secondary || taken[d] || !p.IsOnPitch || p.IsGoalkeeper || p.IsControlled) continue;
                float dist = (p.Position - post).Magnitude; if (dist > t.MaxMarkDistance) continue;
                if (dist < coverD) { coverD = dist; cover = d; }
            }
            if (cover >= 0) { taken[cover] = true; marking[cover] = -2; standAt[cover] = post; }
        }

        /// <summary>The task after <see cref="AssignDefenders"/>: -1 none, -2 covers the near post, otherwise the index of the attacker he marks.</summary>
        public int MarkOf(int index) { return index >= 0 && index < marking.Length ? marking[index] : -1; }
        public Vec2 StandPointOf(int index) { return standAt[index]; }

        // ------------------------------------------------------------------ the style

        /// <summary>The style a cross to <paramref name="receiver"/> is played with: ground to a cut-back (the edge of the box) or along the byline on a clear lane, driven to the near post or the penalty spot when it is close and the lane is open, high otherwise.</summary>
        public CrossStyle StyleFor(WorldView view, in WorldPlayer carrier, in WorldPlayer receiver)
        {
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, carrier.Team);
            float depth = Math.Abs(goal.X - receiver.Position.X), dist = (receiver.Position - carrier.Position).Magnitude;
            float room = WorldGeometry.PassLaneRoom(view, carrier.Team, carrier.Position, receiver.Position);
            if (float.IsPositiveInfinity(room)) room = 99f;
            if (depth >= t.GroundMinDepth && room >= t.GroundLaneRoom) return CrossStyle.Ground;
            if (depth <= t.DrivenMaxDepth && dist <= t.DrivenMaxDistance && room >= t.DrivenLaneRoom) return CrossStyle.Driven;
            return CrossStyle.High;
        }
    }
}
