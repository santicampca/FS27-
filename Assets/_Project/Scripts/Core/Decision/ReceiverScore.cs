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
            return Evaluate(view, passer, receiver, pressed, allowKeeper, t, a, out gain, out _, out _);
        }

        /// <summary>The same score, with the terms it is made of (their sum is the score, added in the same order) and, when the receiver is not a candidate, why (diagnostics only: nothing decides on it).</summary>
        public static float Evaluate(WorldView view, int passer, int receiver, bool pressed, bool allowKeeper, BasicDecisionTuning t, AttackPositioningTuning a, out float gain, out ReceiverParts parts, out CandidateRejection why)
        {
            gain = 0f; parts = default(ReceiverParts); why = CandidateRejection.NotApplicable;
            WorldPlayer me = view[passer], r = view[receiver];
            if (receiver == passer || !r.IsOnPitch || r.Team != me.Team) return float.NegativeInfinity;
            if (r.IsGoalkeeper && !allowKeeper) { why = CandidateRejection.KeeperNotAllowed; return float.NegativeInfinity; }
            float d = (r.Position - me.Position).Magnitude;
            if (d < t.PassMinDistance || d > t.PassMaxDistance) { why = CandidateRejection.OutOfRange; return float.NegativeInfinity; }
            float room = float.PositiveInfinity;
            if (a.Enabled)
            {
                if (!WorldGeometry.IsPassLaneClear(view, me.Team, me.Position, r.Position, view.Tuning.PassLaneClearance, t.PassMaxDistance, out room)) { why = CandidateRejection.LaneBlocked; return float.NegativeInfinity; }
            }
            else if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, r.Position, view.Tuning.PassLaneClearance)) { why = CandidateRejection.LaneBlocked; return float.NegativeInfinity; }
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position);
            if (space < t.ReceiverMinSpace) { why = CandidateRejection.NoSpace; return float.NegativeInfinity; }
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            gain = ((goal - me.Position).Magnitude - (goal - r.Position).Magnitude) / view.Field.Length;
            if (pressed ? gain < -t.PassMaxBackwardGain : gain < t.PassMinForwardGain) { why = CandidateRejection.NotForward; return float.NegativeInfinity; }
            why = CandidateRejection.None;
            float turn = Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, r.Position)) / t.FullTurnDegrees;
            float advance = t.PassForwardWeight * gain, spaceTerm = t.PassSpaceWeight * Math.Min(space, t.PassSpaceCap) / t.PassSpaceCap;
            float distanceTerm = t.PassDistanceWeight * Math.Abs(d - t.PassPreferredDistance) / t.PassMaxDistance, angleTerm = t.PassAngleWeight * (1f - turn);
            float score = advance + spaceTerm - distanceTerm + angleTerm;
            parts.Advance = advance; parts.Space = spaceTerm; parts.Distance = -distanceTerm; parts.Angle = angleTerm;
            // Phase 28: what the pass gains is worth the chance that it arrives; losing the ball costs more the nearer it is to the passer's own goal
            if (!t.PassRiskDisabled)
            {
                float chance = PassRisk.SuccessChance(view, me, r, d, t);
                float own = Math.Max(0f, Math.Min(1f, 1f - WorldGeometry.DistanceToOwnGoal(view, me) / t.PassRiskOwnZone));
                float riskTerm = t.PassRiskWeight * (1f + t.PassRiskOwnGoalFactor * own) * (1f - chance);
                score -= riskTerm;
                parts.Risk = -riskTerm;
            }
            if (!a.Enabled) return score;

            // the lane: more room on the way is better, up to a point
            float margin = float.IsPositiveInfinity(room) ? a.ReceiverLaneCap : Math.Min(room - view.Tuning.PassLaneClearance, a.ReceiverLaneCap);
            float laneTerm = a.ReceiverLaneWeight * margin / a.ReceiverLaneCap;
            score += laneTerm;
            // the pressure on him: every opponent close to him takes away from the score
            float pressureTerm = a.ReceiverPressureWeight * WorldGeometry.OpponentsWithin(view, me.Team, r.Position, a.ReceiverPressureRadius);
            score -= pressureTerm;
            parts.Lane = laneTerm; parts.Pressure = -pressureTerm;
            // his movement: running away from the passer along the pass (onto the ball) is better than coming back or standing
            if (r.Speed >= a.ReceiverMoveMinSpeed && d > 0f)
            {
                Vec2 along = (r.Position - me.Position) * (1f / d);
                float cos = (r.Velocity.X * along.X + r.Velocity.Y * along.Y) / r.Speed;
                float moveTerm = a.ReceiverMoveWeight * cos * Math.Min(1f, r.Speed / Math.Max(r.MaxSpeed, a.ReceiverMoveMinSpeed));
                score += moveTerm;
                parts.Move = moveTerm;
            }
            return score;
        }
    }
}
