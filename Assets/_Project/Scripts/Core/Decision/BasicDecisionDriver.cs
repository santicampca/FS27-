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
        private readonly int[] kindCounts = new int[8];
        private int step;

        public BasicDecisionEngine Engine => engine;
        public int Decisions { get; private set; }
        public int ActionsAttempted { get; private set; }
        public int ActionsCompleted { get; private set; }
        public int PassesCompleted { get; private set; }
        public int ShotsCompleted { get; private set; }
        public int DecisionSteps => intervalSteps;
        public int CountOf(DecisionKind kind) { return kindCounts[(int)kind]; }

        public BasicDecisionDriver(MatchController match, BasicDecisionTuning tuning = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            BasicDecisionTuning t = tuning ?? new BasicDecisionTuning();
            engine = new BasicDecisionEngine(match.Pitch, t);
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
                    if (d.HasMovement) match.ApplyMovementIntent(s, p.PlayerId, d.Movement);
                    if (d.HasAction)
                    {
                        ActionsAttempted++;
                        ActionResult r = match.PerformIntent(d.Action);
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
                    string chosen = AutoSelection.Choose(view, s, match.StoredSelection(s));
                    if (chosen != null) match.SelectPlayer(s, chosen);
                }
            }
        }

        private bool IsDue(int side, int slot) { return (step + side * 5 + slot) % intervalSteps == 0; }
    }
}
