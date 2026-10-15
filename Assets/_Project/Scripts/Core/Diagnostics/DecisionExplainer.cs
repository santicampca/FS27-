using System;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// Phase 31: why a player decided what he did, in words, on demand (nothing here runs during a normal match). It asks the real engine to decide, then reads what the engine left behind: the decision and its reason, the
    /// plan of his team and the game state, what the carrier compared (his options, the winner, the runner-up and the margin), and what the keeper weighed with the ball in his hands. It never decides anything itself: an
    /// explanation of a decision is the decision's own record, so the two cannot disagree.
    /// </summary>
    public static class DecisionExplainer
    {
        public static string Explain(BasicDecisionEngine engine, WorldView view, string playerId)
        {
            int idx = view.IndexOf(playerId);
            if (idx < 0) return playerId + ": not in the match";
            WorldPlayer me = view[idx];
            PlayerDecision d = engine.Decide(view, playerId);
            var sb = new StringBuilder();
            sb.Append(playerId).Append(" (").Append(me.Team).Append(me.IsGoalkeeper ? ", goalkeeper" : "").Append(") at ").Append(F(me.Position.X)).Append(',').Append(F(me.Position.Y)).Append("  stamina ").Append(F(me.Stamina)).Append(me.HasBall ? "  HAS THE BALL" : "").Append('\n');
            sb.Append("  decided: ").Append(d.Kind).Append(" because ").Append(d.Reason);
            if (d.HasAction) sb.Append("; action ").Append(d.Action.Kind).Append(" power ").Append(F(d.Action.Power)).Append(d.TargetPlayerId != null ? " to " + d.TargetPlayerId : "");
            if (d.HasMovement && d.Movement.HasTarget) sb.Append("; moves to ").Append(F(d.Movement.Target.X)).Append(',').Append(F(d.Movement.Target.Y)).Append(" at ").Append(F(d.Movement.Magnitude));
            sb.Append('\n');
            TeamTactic plan = engine.TacticOf(me.Team);
            if (plan != null)
            {
                float urgency = plan.Urgency(view.Match, me.Team);
                sb.Append("  plan: line ").Append(F(plan.Line)).Append(" width ").Append(F(plan.Width)).Append(" tempo ").Append(F(plan.Tempo)).Append(" risk ").Append(F(plan.Risk)).Append(" direct ").Append(F(plan.Directness)).Append(" press x").Append(F(plan.PressScale(urgency)))
                  .Append("  game: ").Append(view.Match.HomeGoals).Append('-').Append(view.Match.AwayGoals).Append(", ").Append(F(view.Match.PeriodRemaining)).Append(" s left, urgency ").Append(F(urgency)).Append('\n');
            }
            if (me.HasBall && !me.IsGoalkeeper)
            {
                CarrierSummary s = engine.Carrier.Summary;
                sb.Append("  carrier saw ").Append(s.CandidateCount).Append(" options (").Append(Mask(s.AvailableMask)).Append("): best ").Append(s.Best).Append(' ').Append(F(s.BestScore)).Append(", then ").Append(s.RunnerUpType).Append(' ').Append(float.IsNegativeInfinity(s.RunnerUpScore) ? "-" : F(s.RunnerUpScore))
                  .Append(", margin ").Append(float.IsInfinity(s.Margin) ? "clear" : F(s.Margin)).Append("; pressure ").Append(F(s.Pressure)).Append(", goal at ").Append(F(s.DistanceToGoal)).Append(" m\n");
            }
            if (me.HasBall && me.IsGoalkeeper && engine.KeeperTuning != null)
            {
                GoalkeeperPlayChoice c = GoalkeeperDistribution.Choose(view, idx, me, engine.Tuning, engine.KeeperTuning, view.Ball.SecondsSinceTouch, 1f);
                sb.Append("  keeper holds the ball for ").Append(F(view.Ball.SecondsSinceTouch)).Append(" s: his rule says ").Append(c.Play).Append(" (best chance ").Append(F(c.Chance)).Append(GoalkeeperDistribution.Hurried(view, me, engine.KeeperTuning) ? ", a rival is on him" : "").Append(")\n");
            }
            return sb.ToString();
        }

        private static string F(float v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }

        private static string Mask(int mask)
        {
            var sb = new StringBuilder();
            foreach (CandidateType t in Enum.GetValues(typeof(CandidateType))) if (t != CandidateType.None && (mask & (1 << (int)t)) != 0) sb.Append(sb.Length > 0 ? "/" : "").Append(t);
            return sb.ToString();
        }
    }
}
