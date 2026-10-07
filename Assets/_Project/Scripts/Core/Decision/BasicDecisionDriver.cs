using System;

namespace FS27.Core
{
    /// <summary>
    /// Runs the basic decision engine for the players of ONE match (Phase 8): each player decides every <see cref="BasicDecisionTuning.DecisionIntervalSeconds"/> (his turn is staggered by his place in
    /// the squads, so not everybody decides in the same step) and, between two decisions, goes on doing what he decided: the stick stays where it was put and the Core keeps simulating.
    /// The driver only READS the match (through a <see cref="WorldState"/> snapshot) and WRITES through the Core's own doors: <c>ApplyMovementIntent</c> and <c>PerformIntent</c>. A player whom a person
    /// controls is never driven. Call <see cref="Step"/> once per fixed step, before <c>MatchController.Advance</c>. Deterministic: it counts steps, never time, and uses no chance.
    /// </summary>
    public sealed class BasicDecisionDriver
    {
        private readonly BasicDecisionEngine engine;
        private readonly int intervalSteps;
        private readonly WorldState world = new WorldState();
        private readonly PlayerDecision[] last = new PlayerDecision[WorldState.MaxPlayers];
        private readonly bool[] decided = new bool[WorldState.MaxPlayers];
        private readonly int[] kindCounts = new int[Enum.GetValues(typeof(DecisionKind)).Length];
        private int step;

        public BasicDecisionEngine Engine => engine;
        public int Decisions { get; private set; }
        public int ActionsAttempted { get; private set; }
        public int ActionsCompleted { get; private set; }
        public int PassesCompleted { get; private set; }
        public int ShotsCompleted { get; private set; }
        // ---- defence, measured (not balanced)
        public int TackleAttempts { get; private set; }
        public int SlideAttempts { get; private set; }
        public int ChallengeAttempts { get; private set; }
        public int InterceptAttempts { get; private set; }
        /// <summary>Defensive actions that left the ball with the defender (or his team).</summary>
        public int Recoveries { get; private set; }
        public int Fouls { get; private set; }
        /// <summary>How many times the primary defender was sampled (at each round of decisions with an opponent carrying the ball), and the sum of his distances to the carrier.</summary>
        public int PrimaryDefenderSamples { get; private set; }
        public double PrimaryDefenderDistanceSum { get; private set; }
        // ---- attack without the ball, measured (not balanced): sampled once per round of decisions while a team has the ball
        /// <summary>How many times the attack was sampled, the sum of the pass lanes open from the carrier to his team-mates (field players), and the sum of the mean distances between the team's field players.</summary>
        public int AttackSamples { get; private set; }
        public long PassLaneSum { get; private set; }
        public double TeammateSpreadSum { get; private set; }
        /// <summary>Passes decided, and the sum of the distances from the passer to the chosen receiver.</summary>
        public int PassDecisions { get; private set; }
        public double PassDistanceSum { get; private set; }
        private readonly int[] passesToJob = new int[Enum.GetValues(typeof(AttackJob)).Length];
        /// <summary>How many decided passes went to a player who, at that moment, had this attack job (measured by recomputing the assignment, not by reading a reason).</summary>
        public int PassesToJob(AttackJob job) { return passesToJob[(int)job]; }
        public int DecisionSteps => intervalSteps;
        public int CountOf(DecisionKind kind) { return kindCounts[(int)kind]; }

        public BasicDecisionDriver(MatchController match, BasicDecisionTuning tuning = null, DefenseTuning defense = null, RolePositioningTuning positioning = null, AttackPositioningTuning attack = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            BasicDecisionTuning t = tuning ?? new BasicDecisionTuning();
            engine = new BasicDecisionEngine(match.Pitch, t, defense, positioning, attack);
            intervalSteps = Math.Max(1, (int)Math.Round(t.DecisionIntervalSeconds / LocomotionDefaults.FixedDeltaTime));
        }

        /// <summary>The last decision a player made (by his id), if he has made one.</summary>
        public bool TryGetLast(string playerId, out PlayerDecision decision)
        {
            int i = world.IndexOf(playerId);
            decision = i >= 0 ? last[i] : default(PlayerDecision);
            return i >= 0 && decided[i];
        }

        public void Step(MatchController match)
        {
            step++;
            if (!match.State.IsBallLive) return;

            bool any = false;
            for (int side = 0; side < 2 && !any; side++)
            {
                TeamMatchState team = side == 0 ? match.State.Home : match.State.Away;
                for (int i = 0; i < team.Players.Count; i++) if (team.Players[i].IsActive && IsDue(side, i)) { any = true; break; }
            }
            if (!any) return;

            world.Refresh(match);
            WorldView view = world.View;
            if (view.TryGetBallCarrier(out WorldPlayer holder))
            {
                DefenseRoles.Rank(view, holder.Team.Opponent(), holder, engine.DefenseTuning, out int primary, out _);
                if (primary >= 0) { PrimaryDefenderSamples++; PrimaryDefenderDistanceSum += (holder.Position - view[primary].Position).Magnitude; }
            }
            if (holder.PlayerId != null && step % intervalSteps == 0) SampleAttack(view, holder);
            for (int side = 0; side < 2; side++)
            {
                TeamSide s = side == 0 ? TeamSide.Home : TeamSide.Away;
                int start = view.TeamStart(s), count = view.TeamCount(s);
                for (int i = 0; i < count; i++)
                {
                    WorldPlayer p = view[start + i];
                    if (!p.IsOnPitch || p.IsControlled || !IsDue(side, i)) continue;
                    PlayerDecision d = engine.Decide(view, p.PlayerId);
                    last[start + i] = d; decided[start + i] = true;
                    Decisions++; kindCounts[(int)d.Kind]++;
                    if (d.Kind == DecisionKind.Pass && d.TargetPlayerId != null && view.TryGetPlayer(d.TargetPlayerId, out WorldPlayer target)) 
                    {
                        PassDecisions++; PassDistanceSum += (target.Position - p.Position).Magnitude;
                        if (engine.Attack.Tuning.Enabled && target.Team == p.Team) { engine.Attack.Assign(view, start + i); passesToJob[(int)engine.Attack.JobOf(view.IndexOf(target.PlayerId))]++; }
                    }
                    if (d.HasMovement) match.ApplyMovementIntent(s, p.PlayerId, d.Movement);
                    if (d.HasAction)
                    {
                        ActionsAttempted++;
                        ActionResult r = match.PerformIntent(d.Action);
                        switch (d.Action.Kind)
                        {
                            case FootballActionKind.Tackle: TackleAttempts++; break;
                            case FootballActionKind.SlidingTackle: SlideAttempts++; break;
                            case FootballActionKind.PhysicalChallenge: ChallengeAttempts++; break;
                            case FootballActionKind.Intercept: InterceptAttempts++; break;
                        }
                        if (r.GainedPossession) Recoveries++;
                        if (r.Detail == ActionDetail.Foul) Fouls++;
                        if (r.Outcome == ActionOutcome.Completed || r.Outcome == ActionOutcome.Accepted)
                        {
                            ActionsCompleted++;
                            if (d.Kind == DecisionKind.Pass && r.Outcome == ActionOutcome.Completed) PassesCompleted++;
                            if (d.Kind == DecisionKind.Shoot && r.Outcome == ActionOutcome.Completed) ShotsCompleted++;
                        }
                    }
                }
                if (match.GetControl(s) == ControlSource.Human)
                {
                    string chosen = AutoSelection.Choose(view, s, match.StoredSelection(s), engine.DefenseTuning);
                    if (chosen != null) match.SelectPlayer(s, chosen);
                }
            }
        }

        private void SampleAttack(WorldView view, in WorldPlayer holder)
        {
            int start = view.TeamStart(holder.Team), end = start + view.TeamCount(holder.Team), lanes = 0, field = 0;
            double spread = 0.0; int pairs = 0;
            for (int i = start; i < end; i++)
            {
                WorldPlayer p = view[i];
                if (!p.IsOnPitch || p.IsGoalkeeper) continue;
                field++;
                if (p.PlayerId != holder.PlayerId && WorldGeometry.IsPassLaneClear(view, holder.Team, holder.Position, p.Position, view.Tuning.PassLaneClearance, engine.Tuning.PassMaxDistance, out _)) lanes++;
                for (int j = i + 1; j < end; j++)
                {
                    WorldPlayer q = view[j];
                    if (!q.IsOnPitch || q.IsGoalkeeper) continue;
                    spread += (p.Position - q.Position).Magnitude; pairs++;
                }
            }
            if (field < 2 || pairs == 0) return;
            AttackSamples++; PassLaneSum += lanes; TeammateSpreadSum += spread / pairs;
        }

        private bool IsDue(int side, int slot) { return (step + side * 5 + slot) % intervalSteps == 0; }
    }
}
