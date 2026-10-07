using System;

namespace FS27.Core
{
    /// <summary>What the carrier finds ahead of him when he thinks of carrying the ball.</summary>
    public struct DribbleAssessment
    {
        /// <summary>There is a free way (straight to goal or round the blocker).</summary>
        public bool Free;
        /// <summary>The direction to carry the ball (unit), when <see cref="Free"/>.</summary>
        public Vec2 Direction;
        /// <summary>The direction whose cone was found clear (straight to goal, or the turned one round the blocker): the one the free distance was measured along.</summary>
        public Vec2 ProbeDirection;
        /// <summary>How well the way goes towards the goal (0..1: the cosine of the direction with the way to goal, floored at 0).</summary>
        public float Progress;
        /// <summary>Free distance ahead in that direction (the clear distance when nobody is in the way).</summary>
        public float FreeDistance;
        /// <summary>Both ways are blocked, the blocker is close and the carrier may play a skill move.</summary>
        public bool SkillMove;
        public Vec2 SkillDirection;
    }

    /// <summary>The look ahead of a carrier (Phase 8's dribble rule, moved here so that the one rule serves both the comparison and the move). Pure.</summary>
    public static class DribbleSpace
    {
        /// <summary>Is there room ahead in this direction? (no opponent inside the cone within the clear distance); <paramref name="blocker"/> is the nearest one in it, or -1.</summary>
        public static bool Clear(WorldView view, in WorldPlayer me, Vec2 dir, BasicDecisionTuning t, out int blocker, out float distance)
        {
            blocker = -1;
            distance = t.DribbleClearDistance;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == me.Team) continue;
                Vec2 off = o.Position - me.Position;
                float d = off.Magnitude;
                if (d > t.DribbleClearDistance || d <= 0f) continue;
                if (WorldGeometry.AngleBetween(dir, off) > t.DribbleConeDegrees) continue;
                if (d < distance) { distance = d; blocker = i; }
            }
            return blocker < 0;
        }

        /// <summary>The free distance ahead in a direction: the distance of the nearest opponent inside the dribble cone and within <paramref name="horizon"/>, or the horizon itself when nobody is.</summary>
        public static float FreeAhead(WorldView view, in WorldPlayer me, Vec2 dir, BasicDecisionTuning t, float horizon)
        {
            float free = horizon;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == me.Team) continue;
                Vec2 off = o.Position - me.Position;
                float d = off.Magnitude;
                if (d > horizon || d <= 0f) continue;
                if (WorldGeometry.AngleBetween(dir, off) > t.DribbleConeDegrees) continue;
                if (d < free) free = d;
            }
            return free;
        }

        public static DribbleAssessment Assess(WorldView view, in WorldPlayer me, Vec2 goal, bool canKick, BasicDecisionTuning t)
        {
            var result = new DribbleAssessment();
            Vec2 toGoal = ActionModel.DirectionOr(goal - me.Position, me.Facing);
            Vec2 dir = toGoal;
            bool space = Clear(view, me, toGoal, t, out int blocker, out float free);
            Vec2 probe = toGoal;
            if (!space)
            {
                // try the side away from whoever blocks the way
                Vec2 off = view[blocker].Position - me.Position;
                float sign = ActionModel.SignedAngle(toGoal, off) >= 0f ? -1f : 1f;
                Vec2 alt = ActionModel.Rotate(toGoal, sign * t.DribbleAvoidDegrees);
                if (Clear(view, me, alt, t, out _, out float altFree)) { dir = ActionModel.DirectionOr(toGoal + (alt - toGoal) * t.AvoidWeight, alt); space = true; free = altFree; probe = alt; }
                else if (canKick && off.Magnitude <= t.SkillMoveTriggerDistance && view.CanUse(me.PlayerId, FootballActionKind.SkillMove).Allowed)
                {
                    result.SkillMove = true; result.SkillDirection = alt;
                }
            }
            result.Free = space;
            result.Direction = dir;
            result.ProbeDirection = probe;
            result.FreeDistance = free;
            float cos = dir.X * toGoal.X + dir.Y * toGoal.Y;
            result.Progress = cos > 0f ? cos : 0f;
            return result;
        }
    }
}
