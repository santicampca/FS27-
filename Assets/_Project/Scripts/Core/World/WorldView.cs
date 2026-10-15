using System;

namespace FS27.Core
{
    /// <summary>
    /// The READ-ONLY window the AI looks through (Phase 7): a thin struct over a <see cref="WorldState"/>. It has no way to change the match (it holds no <see cref="MatchState"/>, no player, no ball, no
    /// definition), builds in constant time and allocates nothing. The future AI observes this and answers with intentions (<see cref="MovementIntent"/>, <see cref="ActionIntent"/>); it never writes.
    /// No decision is made here: only what is, and the questions that have a single geometric answer (<see cref="WorldGeometry"/>).
    /// </summary>
    public readonly struct WorldView
    {
        private readonly WorldState state;
        private readonly InfrastructureTuning tuning;

        public WorldView(WorldState state) : this(state, null) { }

        public WorldView(WorldState state, InfrastructureTuning tuning)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.tuning = tuning ?? DefaultTuning;
        }

        private static readonly InfrastructureTuning DefaultTuning = new InfrastructureTuning();

        public WorldMatch Match => state.Match;
        public WorldBall Ball => state.Ball;
        public FieldDimensions Field => state.Field;
        public InfrastructureTuning Tuning => tuning;
        public int PlayerCount => state.PlayerCount;
        public WorldPlayer this[int index] => state[index];
        public int TeamStart(TeamSide side) { return state.TeamStart(side); }
        public int TeamCount(TeamSide side) { return state.TeamCount(side); }
        public GoalkeeperSlot GoalkeeperSlotOf(TeamSide side) { return state.GoalkeeperSlotOf(side); }
        public string ControlledPlayerOf(TeamSide side) { return state.ControlledPlayerOf(side); }
        public int IndexOf(string playerId) { return state.IndexOf(playerId); }
        public bool TryGetPlayer(string playerId, out WorldPlayer player) { return state.TryGetPlayer(playerId, out player); }

        /// <summary>The player who has the ball, if any.</summary>
        public bool TryGetBallCarrier(out WorldPlayer player)
        {
            WorldBall b = state.Ball;
            if (b.HasPossessor && state.TryGetPlayer(b.PossessorId, out player)) return true;
            player = default(WorldPlayer);
            return false;
        }

        /// <summary>How many players of a side are on the pitch.</summary>
        public int OnPitchCount(TeamSide side)
        {
            int n = 0, start = state.TeamStart(side), end = start + state.TeamCount(side);
            for (int i = start; i < end; i++) if (state[i].IsOnPitch) n++;
            return n;
        }

        // ---- the ball ahead (basic trajectory)

        /// <summary>
        /// Where the ball will be after <paramref name="seconds"/> if nothing touches it (the reference ball model on a copy: the real ball is never touched). Walls are not simulated here: a ball that
        /// will bounce is predicted as if the pitch had no walls.
        /// </summary>
        public Vec3 PredictBallPosition(float seconds)
        {
            var s = new BallState(state.Ball.Position, state.Ball.Velocity) { IsGrounded = state.Ball.IsGrounded };
            return BallPredictor.Predict(s, state.BallParameters, seconds).Position;
        }

        /// <summary>
        /// Phase 37: the ball's positions every <paramref name="step"/> seconds ahead (index 0 = one step ahead), if nothing touches it: one simulation on a copy, so a whole path costs what its longest point costs.
        /// Returns how many were written.
        /// </summary>
        public int PredictBallPath(float step, Vec3[] positions, int count)
        {
            if (positions == null || step <= 0f) return 0;
            var s = new BallState(state.Ball.Position, state.Ball.Velocity) { IsGrounded = state.Ball.IsGrounded };
            int n = Math.Min(count, positions.Length);
            for (int i = 0; i < n; i++)
            {
                BallSimulator.Step(ref s, state.BallParameters, step, null);
                positions[i] = s.Position;
            }
            return n;
        }

        // ---- skills of a player

        /// <summary>The skills of a player (requirements met = active; the rest listed as ignored). Null for an unknown id.</summary>
        public ResolvedSkills SkillsOf(string playerId)
        {
            return state.TryGetPlayer(playerId, out WorldPlayer p) ? p.Skills : null;
        }

        /// <summary>
        /// May this player perform this action now, as far as his skills and the ball say? (the same <see cref="SkillResolver.CanUseAction"/> the engine uses: nothing is duplicated). It does not say that
        /// the action is wise, nor that the ball is in reach.
        /// </summary>
        public SkillPermission CanUse(string playerId, FootballActionKind action, string skillId = null)
        {
            if (!state.TryGetPlayer(playerId, out WorldPlayer p) || !p.IsOnPitch) return SkillPermission.Refuse(ActionRejection.PlayerNotActive);
            return SkillResolver.CanUseAction(p.Skills, action, p.HasBall, false, skillId);
        }

        // ---- observation

        /// <summary>Fills an observation for a player (reuse the same object every step: it allocates nothing). Returns false, leaving it invalid, for an unknown player.</summary>
        public bool Observe(string playerId, PlayerObservation into)
        {
            return into.Fill(this, state.IndexOf(playerId));
        }

        public bool Observe(int playerIndex, PlayerObservation into)
        {
            return into.Fill(this, playerIndex);
        }
    }

    /// <summary>
    /// What ONE player observes (Phase 7): himself, the ball (distance, direction, who has it), his team-mates and the opponents on the pitch ordered by distance, and a basic measure of free space.
    /// Observation only: no decision, no scoring, no marking, no support. Reusable (<see cref="Fill"/> refills it with no allocation); a copy of data, it cannot change the match.
    /// </summary>
    public sealed class PlayerObservation
    {
        public const int MaxNeighbours = 6;

        public bool Valid { get; private set; }
        public int SelfIndex { get; private set; }
        public WorldPlayer Self { get; private set; }
        public MatchPhase Phase { get; private set; }
        public PitchZone Zone { get; private set; }
        public bool IsControlled { get; private set; }
        public bool CanAct { get; private set; }

        // ---- the ball
        public Vec2 BallPosition { get; private set; }
        public float BallDistance { get; private set; }
        /// <summary>Degrees from where he faces to the ball (+ right, - left).</summary>
        public float BallBearing { get; private set; }
        public bool BallLive { get; private set; }
        public bool HasBall { get; private set; }
        /// <summary>Someone of his team has the ball (he may be the one).</summary>
        public bool TeamHasBall { get; private set; }
        public bool OpponentHasBall { get; private set; }
        public bool BallFree { get; private set; }
        public string PossessorId { get; private set; }

        // ---- the others, nearest first (indices into the world state)
        public int TeammateCount { get; private set; }
        public int OpponentCount { get; private set; }
        private readonly int[] teammates = new int[MaxNeighbours];
        private readonly float[] teammateDistances = new float[MaxNeighbours];
        private readonly int[] opponents = new int[MaxNeighbours];
        private readonly float[] opponentDistances = new float[MaxNeighbours];

        public int TeammateIndex(int rank) { return teammates[rank]; }
        public float TeammateDistance(int rank) { return teammateDistances[rank]; }
        public int OpponentIndex(int rank) { return opponents[rank]; }
        public float OpponentDistance(int rank) { return opponentDistances[rank]; }

        // ---- space
        /// <summary>Distance to the nearest opponent (infinity when there is none).</summary>
        public float NearestOpponentDistance { get; private set; }
        /// <summary>Opponents within <see cref="InfrastructureTuning.OpenSpaceRadius"/>.</summary>
        public int OpponentsNearby { get; private set; }
        public bool IsFree { get { return OpponentsNearby == 0; } }
        public float DistanceToAttackGoal { get; private set; }
        public float DistanceToOwnGoal { get; private set; }

        internal bool Fill(WorldView view, int index)
        {
            Valid = false;
            TeammateCount = 0; OpponentCount = 0;
            if (index < 0 || index >= view.PlayerCount) return false;
            WorldPlayer me = view[index];
            WorldBall ball = view.Ball;
            SelfIndex = index; Self = me; Phase = view.Match.Phase; Zone = me.Zone; IsControlled = me.IsControlled; CanAct = me.CanAct;

            BallPosition = ball.Ground; BallDistance = WorldGeometry.DistanceToBall(me, ball);
            BallBearing = WorldGeometry.RelativeAngle(me.Facing, me.Position, ball.Ground);
            BallLive = view.Match.BallLive;
            HasBall = me.HasBall;
            TeamHasBall = ball.HasPossessor && ball.PossessingTeam == me.Team;
            OpponentHasBall = ball.HasPossessor && ball.PossessingTeam != me.Team;
            BallFree = !ball.HasPossessor;
            PossessorId = ball.PossessorId;

            NearestOpponentDistance = float.PositiveInfinity;
            OpponentsNearby = 0;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == index) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch) continue;
                float d = (o.Position - me.Position).Magnitude;
                if (o.Team == me.Team) Insert(teammates, teammateDistances, ref teammateCount, i, d);
                else
                {
                    Insert(opponents, opponentDistances, ref opponentCount, i, d);
                    if (d < NearestOpponentDistance) NearestOpponentDistance = d;
                    if (d <= view.Tuning.OpenSpaceRadius) OpponentsNearby++;
                }
            }
            TeammateCount = teammateCount; OpponentCount = opponentCount;
            teammateCount = 0; opponentCount = 0;
            DistanceToAttackGoal = WorldGeometry.DistanceToAttackGoal(view, me);
            DistanceToOwnGoal = WorldGeometry.DistanceToOwnGoal(view, me);
            Valid = true;
            return true;
        }

        private int teammateCount, opponentCount;

        // insertion into a short list ordered by distance; equal distances keep squad order (the earlier index stays first)
        private static void Insert(int[] ids, float[] dists, ref int count, int id, float d)
        {
            int pos = count;
            while (pos > 0 && dists[pos - 1] > d) pos--;
            if (pos >= MaxNeighbours) return;
            int last = Math.Min(count, MaxNeighbours - 1);
            for (int k = last; k > pos; k--) { ids[k] = ids[k - 1]; dists[k] = dists[k - 1]; }
            ids[pos] = id; dists[pos] = d;
            if (count < MaxNeighbours) count++;
        }
    }
}
