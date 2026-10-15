using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Phase 36: a person crosses with the controls he already has. No new button: with the ball on a flank in the attacking third, the swipe UP on A (which is a chip at goal anywhere else) is a cross.
    /// The length of the swipe is the power and decides the style: short = ground cross (a cut-back along the grass), medium = driven (fast, chest high), long = high (over the defenders); the direction of the stick
    /// chooses the place of the box (middle, near post, far post, edge), and a team-mate near that place is the target. Off by default in the Core (the games turn it on).
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly CrossPlay crossGeometry = new CrossPlay();

        /// <summary>Phase 36: the swipe up on A is a cross on the flank (off = it is the chip of Phase 35 everywhere).</summary>
        public bool HumanCrossEnabled { get; set; }

        /// <summary>Power below which a person's cross is a ground cross, and below which it is a driven one (above that, high).</summary>
        public const float CrossGroundBelowPower = 0.66f, CrossDrivenBelowPower = 0.86f;

        /// <summary>True when the controlled carrier of this side is on a flank in the attacking third: the swipe up on A is a cross now (the client shows it on the button).</summary>
        public bool CanHumanCross(TeamSide side)
        {
            if (!HumanCrossEnabled || State.Phase != MatchPhase.Playing) return false;
            MatchPlayer p = BallCarrier;
            if (p == null || p.Team != side || p.InGoal || GetControl(side) != ControlSource.Human || p.PlayerId != ControlledPlayerId(side)) return false;
            return InCrossZone(p);
        }

        private bool InCrossZone(MatchPlayer p)
        {
            Vec2 goal = GoalCentreOf(p.Team); CrossPlayTuning t = crossGeometry.Tuning;
            return Math.Abs(p.Locomotion.Position.Y) >= field.Width / 2f - t.WingBand && Math.Abs(goal.X - p.Locomotion.Position.X) <= t.ThreatGoalLineDistance;
        }

        /// <summary>The style a swipe of this power gives a cross.</summary>
        public static CrossStyle StyleOfPower(float power) { return power < CrossGroundBelowPower ? CrossStyle.Ground : (power < CrossDrivenBelowPower ? CrossStyle.Driven : CrossStyle.High); }

        /// <summary>Turns a person's chip (swipe up on A) into a cross when he is in the zone; otherwise the request is returned unchanged.</summary>
        private ActionRequest AssistCross(MatchPlayer p, ActionRequest r, ActionInput input)
        {
            if (!HumanCrossEnabled || r.Kind != FootballActionKind.Chip || !IsCarrier(p) || p.InGoal || !InCrossZone(p)) return r;
            Vec2 from = p.Locomotion.Position, goal = GoalCentreOf(p.Team), attack = AttackDirectionOf(p.Team);
            float side = from.Y >= 0f ? 1f : -1f;
            Vec2 aim = input.Direction.Magnitude > 0.2f ? input.Direction.Normalized : (goal - from).Normalized;
            // the place of the box the stick points at (the best aligned of the four), and a team-mate near it
            BoxRole best = BoxRole.Central; float bestCos = -2f; Vec2 bestPoint = goal;
            foreach (BoxRole role in new[] { BoxRole.Central, BoxRole.NearPost, BoxRole.FarPost, BoxRole.Edge })
            {
                Vec2 pt = crossGeometry.PointOf(role, goal, attack, side); Vec2 v = pt - from; float m = v.Magnitude; if (m < 1e-3f) continue;
                float cos = (v.X * aim.X + v.Y * aim.Y) / m;
                if (cos > bestCos) { bestCos = cos; best = role; bestPoint = pt; }
            }
            MatchPlayer mate = null; float mateD = 4f;
            foreach (MatchPlayer q in State.Team(p.Team).Players) { if (q == p || !q.IsActive || q.InGoal) continue; float d = (q.Locomotion.Position - bestPoint).Magnitude; if (d < mateD) { mateD = d; mate = q; } }
            float distance = (bestPoint - from).Magnitude, power = ActionModel.Clamp01(input.Magnitude > 0f ? input.Magnitude : actions.DefaultPower);
            CrossStyle style = StyleOfPower(power);
            float quality = p.ActionProfile.PassQuality * BasicDecisionDefaults.PlannedQualityShare;
            r.Kind = FootballActionKind.Cross; r.Style = style;
            r.Power = style == CrossStyle.Ground ? PassPhysics.GroundPower(distance, BasicDecisionDefaults.GroundArrivalSpeed, quality, actions.CrossGroundSpeedBoost)
                    : style == CrossStyle.Driven ? PassPhysics.DrivenCrossPower(distance * BasicDecisionDefaults.LandingShare, quality)
                    : PassPhysics.LoftedPower(distance * BasicDecisionDefaults.LandingShare, quality);
            r.Target = mate != null ? PassTarget.ToPlayer(mate.PlayerId) : PassTarget.ToPosition(Pitch.ClampPlayer(bestPoint));
            r.Direction = ActionModel.DirectionOr(((mate != null ? mate.Locomotion.Position : bestPoint) - from), aim);
            return r;
        }
    }
}
