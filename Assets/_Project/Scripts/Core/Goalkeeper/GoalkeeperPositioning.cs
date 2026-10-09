using System;

namespace FS27.Core
{
    /// <summary>Where a keeper should stand now, and why (Phase 16). A plain value: nothing is allocated to make it.</summary>
    public struct GoalkeeperStand
    {
        /// <summary>The place to stand, in field space.</summary>
        public Vec2 Target;
        /// <summary>0..1: how dangerous the situation is (the ball near, in the feet of an opponent, the way to goal open).</summary>
        public float Danger;
        /// <summary>Metres in front of the goal line.</summary>
        public float Depth;
        /// <summary>How many players stand between the ball and the goal.</summary>
        public int Blockers;
        /// <summary>Metres by which his positioning quality puts him off the ideal place (0 for a perfect keeper).</summary>
        public float Drift;
    }

    /// <summary>
    /// The keeper's place (Phase 16): on the line from the goal centre to the ball, nearer the goal when the ball is far and further out when the danger grows; towards the near post when the ball is wide; a little to the
    /// open side when players stand in the way; and, for a keeper whose positioning is not perfect, a little off that place, drifting slowly. A pure function of what the keeper can see (the view); nothing here is a
    /// shot, a save or a number of the Core.
    /// </summary>
    public static class GoalkeeperPositioning
    {
        public static GoalkeeperStand Stand(WorldView view, in WorldPlayer me, GoalkeeperAiTuning t, in GoalkeeperSkill skill)
        {
            Vec2 goal = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            float inward = goal.X > 0f ? -1f : 1f;
            WorldBall ball = view.Ball;
            Vec2 ballPos = ball.Ground;
            float along = Math.Max(0f, (ballPos.X - goal.X) * inward);
            float across = ballPos.Y - goal.Y;
            float distance = (float)Math.Sqrt(along * along + across * across);
            float halfGoal = view.Field.GoalWidth / 2f;

            // players between the ball and the goal centre (either team, the keeper aside): they cut the danger and tell which side is open
            int blockers = 0; float blockerSide = 0f;
            Vec2 toBall = ballPos - goal;
            float toBallLength = toBall.Magnitude;
            if (toBallLength > 1e-3f)
            {
                Vec2 u = toBall * (1f / toBallLength);
                for (int i = 0; i < view.PlayerCount; i++)
                {
                    WorldPlayer o = view[i];
                    if (!o.IsOnPitch || o.IsGoalkeeper || o.PlayerId == ball.PossessorId) continue;
                    Vec2 rel = o.Position - goal;
                    float proj = rel.X * u.X + rel.Y * u.Y;
                    if (proj <= 0f || proj >= toBallLength) continue;
                    float side = rel.X * -u.Y + rel.Y * u.X;   // signed distance from the line (left positive)
                    if (Math.Abs(side) > t.CorridorHalfWidth) continue;
                    blockers++; blockerSide += side;
                }
            }

            // danger: the nearer the ball, the more; a ball in the feet of an opponent more than a free one; none when it is ours
            float proximity = MathUtil.Clamp01((t.DangerFarDistance - distance) / Math.Max(t.DangerFarDistance - t.DangerNearDistance, 1e-3f));
            float owner = ball.HasPossessor ? (ball.PossessingTeam == me.Team ? t.OwnBallDanger : 1f) : t.LooseBallDanger;
            float danger = proximity * owner * MathUtil.Clamp01(1f - t.BlockedDangerCut * blockers);

            float depth = MathUtil.Lerp(t.LineDepthMin, t.LineDepthMax, danger);
            Vec2 dir = toBallLength > 1e-3f ? toBall * (1f / toBallLength) : new Vec2(inward, 0f);
            Vec2 target = goal + dir * depth;

            // the near post: the wider the ball, the more he leans to its side; the open side when players are in the way
            float lean = MathUtil.Clamp01(Math.Abs(across) / Math.Max(halfGoal, 1e-3f));
            float lateral = (across >= 0f ? 1f : -1f) * t.NearPostShift * lean;
            target = new Vec2(target.X, target.Y + lateral);
            if (blockers > 0)
            {
                // away from the side the blockers are on, across the line from the goal to the ball
                Vec2 normal = new Vec2(-dir.Y, dir.X);
                target = target + normal * ((blockerSide > 0f ? -1f : 1f) * t.BlockedLateralShift * Math.Min(blockers, 2));
            }

            // his own quality: a slow drift off the ideal place (the common error of every keeper, and more for a poor one)
            float quality = MathUtil.Clamp01(skill.Positioning);
            float drift = (t.PositionErrorBase + t.PositionErrorMax * (1f - quality)) * t.PositionErrorScale;
            Vec2 offset = Vec2.Zero;
            if (drift > 0f)
            {
                int period = (int)(view.Match.MatchTime / Math.Max(t.PositionErrorPeriod, 1e-3f));
                float angle = DecisionDice.Roll(DecisionDice.Drift, period, me.Slot) * (float)(2.0 * Math.PI);
                float size = DecisionDice.Roll(DecisionDice.DriftAngle, period, me.Slot);
                drift *= size;
                offset = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * drift;
            }
            target = target + offset;

            // inside the goal area: not behind the line, not beyond the posts
            float lateralLimit = halfGoal - t.PostMargin;
            float x = goal.X + inward * Math.Min(t.StandMaxDepth, Math.Max(0f, (target.X - goal.X) * inward));
            float y = Math.Max(goal.Y - lateralLimit, Math.Min(goal.Y + lateralLimit, target.Y));
            return new GoalkeeperStand { Target = new Vec2(x, y), Danger = danger, Depth = depth, Blockers = blockers, Drift = drift };
        }
    }
}
