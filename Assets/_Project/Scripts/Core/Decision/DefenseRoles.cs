using System;

namespace FS27.Core
{
    /// <summary>
    /// Who defends when the opponent has the ball (Phase 9): ONE primary defender and, only when there is a reason, ONE secondary; everybody else keeps his place. A small deterministic score per player,
    /// no utility system: the nearer to the carrier, the more he stands between the carrier and his own goal, the closer to his home, the more he already was the one pressing, and the less busy or
    /// locked he is, the better. The goalkeeper and tired players are never chosen. Ties go to the earlier in the squad. A pure function of the view.
    /// </summary>
    public static class DefenseRoles
    {
        public static float Score(WorldView view, int playerIndex, in WorldPlayer carrier, DefenseTuning t)
        {
            WorldPlayer me = view[playerIndex];
            if (!me.IsOnPitch || me.IsGoalkeeper || me.Team == carrier.Team || me.Stamina < t.PressMinStamina) return float.NegativeInfinity;
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            Vec2 rel = me.Position - carrier.Position;
            float d = rel.Magnitude;
            Vec2 toGoal = (own - carrier.Position).Normalized;
            float goalSide = d > 0f ? (rel.X * toGoal.X + rel.Y * toGoal.Y) / d : 0f;
            float score = -t.ScoreDistanceWeight * d + t.ScoreGoalSideWeight * goalSide;
            if (me.HasHome) score -= t.ScoreHomeWeight * (me.Position - me.Home).Magnitude;
            if (me.IsPressing) score += t.ScoreIncumbentBonus;
            if (me.IsSliding || me.ActionLocked) score -= t.ScoreLockedPenalty;
            else if (me.KickCooldown) score -= t.ScoreBusyPenalty;
            return score;
        }

        /// <summary>The primary and the secondary defender of the team that does NOT have the ball (indices into the view, -1 for none).</summary>
        public static void Rank(WorldView view, TeamSide defending, in WorldPlayer carrier, DefenseTuning t, out int primary, out int secondary)
        {
            primary = -1; secondary = -1;
            float bestScore = float.NegativeInfinity;
            int start = view.TeamStart(defending), end = start + view.TeamCount(defending);
            for (int i = start; i < end; i++)
            {
                float s = Score(view, i, carrier, t);
                if (s > bestScore) { bestScore = s; primary = i; }
            }
            if (primary < 0) return;

            float primaryDistance = (view[primary].Position - carrier.Position).Magnitude;
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, defending);
            bool reason = primaryDistance >= t.SecondaryTriggerDistance || (carrier.Position - own).Magnitude <= t.SecondaryDangerDistance;
            if (!reason) return;
            float second = float.NegativeInfinity;
            for (int i = start; i < end; i++)
            {
                if (i == primary) continue;
                if ((view[i].Position - carrier.Position).Magnitude > t.SecondaryMaxDistance) continue;
                float s = Score(view, i, carrier, t);
                if (s > second) { second = s; secondary = i; }
            }
        }
    }
}
