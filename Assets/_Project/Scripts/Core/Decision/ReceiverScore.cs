using System;

namespace FS27.Core
{
    /// <summary>
    /// How good a team-mate is as the target of a pass (Phase 10; the Phase 8 terms plus the lane, the pressure on him and whether he is running onto the ball). A pure function of the world view: no state,
    /// no chance. Returns negative infinity for someone who is not a candidate (not on the pitch, an opponent, too near or too far, no lane, no room, not far enough forward).
    /// With <see cref="AttackPositioningTuning.Enabled"/> off it is exactly the Phase 8 / 9 score.
    /// </summary>
    public static class ReceiverScore
    {
        public static float Evaluate(WorldView view, int passer, int receiver, bool pressed, bool allowKeeper, BasicDecisionTuning t, AttackPositioningTuning a, out float gain)
        {
            gain = 0f;
            WorldPlayer me = view[passer], r = view[receiver];
            if (receiver == passer || !r.IsOnPitch || r.Team != me.Team || (r.IsGoalkeeper && !allowKeeper)) return float.NegativeInfinity;
            float d = (r.Position - me.Position).Magnitude;
            if (d < t.PassMinDistance || d > t.PassMaxDistance) return float.NegativeInfinity;
            float room = float.PositiveInfinity;
            if (a.Enabled)
            {
                if (!WorldGeometry.IsPassLaneClear(view, me.Team, me.Position, r.Position, view.Tuning.PassLaneClearance, t.PassMaxDistance, out room)) return float.NegativeInfinity;
            }
            else if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, r.Position, view.Tuning.PassLaneClearance)) return float.NegativeInfinity;
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position);
            if (space < t.ReceiverMinSpace) return float.NegativeInfinity;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            gain = ((goal - me.Position).Magnitude - (goal - r.Position).Magnitude) / view.Field.Length;
            if (pressed ? gain < -t.PassMaxBackwardGain : gain < t.PassMinForwardGain) return float.NegativeInfinity;
            float turn = Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, r.Position)) / t.FullTurnDegrees;
            float score = t.PassForwardWeight * gain + t.PassSpaceWeight * Math.Min(space, t.PassSpaceCap) / t.PassSpaceCap
                          - t.PassDistanceWeight * Math.Abs(d - t.PassPreferredDistance) / t.PassMaxDistance + t.PassAngleWeight * (1f - turn);
            if (!a.Enabled) return score;

            // the lane: more room on the way is better, up to a point
            float margin = float.IsPositiveInfinity(room) ? a.ReceiverLaneCap : Math.Min(room - view.Tuning.PassLaneClearance, a.ReceiverLaneCap);
            score += a.ReceiverLaneWeight * margin / a.ReceiverLaneCap;
            // the pressure on him: every opponent close to him takes away from the score
            score -= a.ReceiverPressureWeight * WorldGeometry.OpponentsWithin(view, me.Team, r.Position, a.ReceiverPressureRadius);
            // his movement: running away from the passer along the pass (onto the ball) is better than coming back or standing
            if (r.Speed >= a.ReceiverMoveMinSpeed && d > 0f)
            {
                Vec2 along = (r.Position - me.Position) * (1f / d);
                float cos = (r.Velocity.X * along.X + r.Velocity.Y * along.Y) / r.Speed;
                score += a.ReceiverMoveWeight * cos * Math.Min(1f, r.Speed / Math.Max(r.MaxSpeed, a.ReceiverMoveMinSpeed));
            }
            return score;
        }
    }
}
