using System;

namespace FS27.Core
{
    /// <summary>The keeper's decision to come out for a ball nobody has, or to stay (Phase 16).</summary>
    public struct GoalkeeperExitChoice
    {
        public bool Considered;
        /// <summary>He goes for it.</summary>
        public bool Go;
        public Vec2 Target;
        /// <summary>By how many seconds he gets there before the first opponent, as the TRUTH is (negative: the opponent is earlier). Used only to say whether the decision was a mistake.</summary>
        public float TrueMargin;
        /// <summary>Whether the decision, taken against the truth, was a mistake (went and was late, or stayed and would have been first).</summary>
        public bool Mistake;
    }

    /// <summary>
    /// Leave or stay (Phase 16). The ball must be free, near enough to the goal and not a shot. The keeper compares when HE can get to where the ball will be with when each opponent can and when each team-mate can,
    /// using the speeds the players really have (the same rule as the interception of the field players) and his own judgement of it: a keeper who reads the game badly misjudges the opponents' time by a deterministic
    /// error that changes every fraction of a second. He goes only if he is earlier than everybody by a margin; otherwise he stays and keeps his place.
    /// </summary>
    public static class GoalkeeperExit
    {
        private const int Samples = 3;

        /// <summary>
        /// Phase 37 (REQ-0174): a HIGH ball coming down in his area. The same judgement as <see cref="Choose"/> but with the point where the ball comes down to head height and the seconds it takes (not the first fraction of a second):
        /// he goes only if he is under it before the ball, the rivals' best header and his own team-mates' best header (with the same deterministic misjudgement of the rivals' time), otherwise he stays on his line.
        /// </summary>
        public static GoalkeeperExitChoice ChooseHigh(WorldView view, in WorldPlayer me, GoalkeeperAiTuning t, in GoalkeeperSkill skill, float command01, Vec2 point, float ballSeconds, float opponent, float mate)
        {
            var c = new GoalkeeperExitChoice();
            if (me.MaxSpeed <= 0f) return c;
            Vec2 goal = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            float inward = goal.X > 0f ? -1f : 1f;
            float along = (point.X - goal.X) * inward;
            if (along > t.ExitMaxDepth || along < 0f) return c;
            c.Considered = true;
            float keeperTime = (point - me.Position).Magnitude / (me.MaxSpeed * t.ExitSpeedShare);
            int period = (int)(view.Match.MatchTime / Math.Max(t.ExitJudgementPeriod, 1e-3f));
            float error = t.ExitJudgementError * (1f - MathUtil.Clamp01(skill.DecisionMaking * (1f - t.CommandExitInfluence) + command01 * t.CommandExitInfluence)) * (2f * DecisionDice.Roll(DecisionDice.Choice, period, me.Slot) - 1f);
            float believed = opponent * (1f + error);
            c.TrueMargin = Math.Min(opponent, mate) - keeperTime;
            c.Target = point;
            bool inTime = keeperTime <= ballSeconds + t.ExitMarginSeconds;
            bool goes = inTime && keeperTime + t.ExitMarginSeconds <= believed && keeperTime + t.ExitMarginSeconds <= mate;
            bool should = inTime && c.TrueMargin >= t.ExitMarginSeconds;
            c.Go = goes; c.Mistake = goes != should && (goes || c.TrueMargin >= t.ExitMarginSeconds * 2f);
            return c;
        }

        public static GoalkeeperExitChoice Choose(WorldView view, in WorldPlayer me, GoalkeeperAiTuning t, in GoalkeeperSkill skill, float command01, float reboundWait)
        {
            var c = new GoalkeeperExitChoice();
            WorldBall ball = view.Ball;
            if (ball.HasPossessor || me.MaxSpeed <= 0f) return c;
            Vec2 goal = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            float inward = goal.X > 0f ? -1f : 1f;
            float along = (ball.Ground.X - goal.X) * inward;
            if (along > t.ExitMaxDistance || along < 0f) return c;
            // after a parry or a rebound he needs a moment to see where it went
            if (ball.HasLastTouch && ball.LastTouchTeam == me.Team && ball.SecondsSinceTouch < reboundWait) return c;
            c.Considered = true;

            // the first sample (a fraction of a second ahead) at which the ball is within his reach in time
            float step = t.ExitMarginSeconds;
            float keeperSpeed = me.MaxSpeed * t.ExitSpeedShare;
            int found = -1; Vec2 point = ball.Ground; float keeperTime = 0f;
            for (int k = 0; k < Samples; k++)
            {
                float when = (k + 1) * step;
                Vec3 p = view.PredictBallPosition(when);
                Vec2 q = new Vec2(p.X, p.Z);
                float d = (q - me.Position).Magnitude;
                float needed = d / keeperSpeed;
                if (needed <= when + step && (q.X - goal.X) * inward <= t.ExitMaxDepth) { found = k; point = q; keeperTime = needed; break; }
            }
            if (found < 0) return c;

            // everybody else: the earliest opponent and the earliest team-mate (goalkeepers aside)
            float opponent = float.PositiveInfinity, mate = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.IsGoalkeeper || o.MaxSpeed <= 0f) continue;
                float arrival = (point - o.Position).Magnitude / (o.MaxSpeed * t.ExitSpeedShare);
                if (o.Team == me.Team) { if (arrival < mate) mate = arrival; }
                else if (arrival < opponent) opponent = arrival;
            }

            // his judgement of the opponents' time: wrong by a deterministic amount that depends on how well he reads the game
            int period = (int)(view.Match.MatchTime / Math.Max(t.ExitJudgementPeriod, 1e-3f));
            float error = t.ExitJudgementError * (1f - MathUtil.Clamp01(skill.DecisionMaking * (1f - t.CommandExitInfluence) + command01 * t.CommandExitInfluence)) * (2f * DecisionDice.Roll(DecisionDice.Choice, period, me.Slot) - 1f);
            float believed = opponent * (1f + error);

            c.TrueMargin = Math.Min(opponent, mate) - keeperTime;
            bool goes = keeperTime + t.ExitMarginSeconds <= believed && keeperTime + t.ExitMarginSeconds <= mate;
            bool should = c.TrueMargin >= t.ExitMarginSeconds;
            c.Go = goes; c.Target = point; c.Mistake = goes != should && (goes || c.TrueMargin >= t.ExitMarginSeconds * 2f);
            return c;
        }
    }
}
