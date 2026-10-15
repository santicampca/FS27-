using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Why <see cref="MatchController.ExecuteSetPiece"/> did not (or did) take the throw-in or the corner.</summary>
    public enum SetPieceExecuteResult
    {
        Executed = 0,
        /// <summary>No throw-in or corner is waiting to be taken (or the set pieces are off).</summary>
        NoSetPiece,
        /// <summary>The aim is not valid (not a number, an unknown intent, a team-mate who is not on the pitch): nothing is whistled.</summary>
        InvalidAim,
        /// <summary>The restart cannot be whistled yet (a team still needs a goalkeeper).</summary>
        NotReady,
        /// <summary>The restart was whistled but the action was refused (the reason is in <see cref="MatchController.LastActionResult"/>).</summary>
        ActionRefused
    }

    /// <summary>
    /// Phase 33: a person takes the throw-in or the corner with the SAME aiming contract as the free kick (<see cref="FreeKickAim"/>: drag direction, dragged point, goal zone, charge by holding the button, team-mate by name or by drag),
    /// resolved by the same functions (<see cref="FreeKickAiming"/>), and performed through the same action system as any kick. A throw-in is a throw, not a boot: its power is the charge times
    /// <see cref="SetPieceTuning.ThrowMaxPower"/>, so holding longer throws further (up to a limit) and a long throw is lofted. A corner is a lofted ball to the point, zone or team-mate aimed at (the Core has no cross action: it is a long pass).
    /// </summary>
    public sealed partial class MatchController
    {
        /// <summary>True while a throw-in or a corner of this side, controlled by a person, waits for him (the host gives him <see cref="SetPieceTuning.HumanWaitSeconds"/> before the AI takes it).</summary>
        public bool AwaitingHumanSetPiece(TeamSide side)
        {
            if (!setPiece.Enabled || State.Phase != MatchPhase.WaitingForRestart || GetControl(side) != ControlSource.Human) return false;
            RestartRequest r = State.PendingRestart;
            return (r.Kind == RestartKind.ThrowIn || r.Kind == RestartKind.Corner) && r.Team == side;
        }

        /// <summary>The power a charge of <paramref name="holdSeconds"/> gives a throw-in (the free kick's charge, scaled).</summary>
        public float ThrowPowerFromHold(float holdSeconds) { return FreeKickAiming.PowerFromHold(holdSeconds, freeKick) * setPiece.ThrowMaxPower; }

        public SetPieceExecuteResult ExecuteSetPiece(FreeKickAim aim)
        {
            RestartRequest r = State.PendingRestart;
            if (!setPiece.Enabled || State.Phase != MatchPhase.WaitingForRestart || (r.Kind != RestartKind.ThrowIn && r.Kind != RestartKind.Corner)) return SetPieceExecuteResult.NoSetPiece;
            if (r.Status == RestartStatus.Pending) { PrepareRestart(); r = State.PendingRestart; }   // the team-mates take their places and the Core names the taker
            if (r.TakerId == null) return SetPieceExecuteResult.NoSetPiece;
            if (!Enum.IsDefined(typeof(FreeKickIntent), aim.Intent) || !ActionModel.IsFinite(aim.Direction) || float.IsNaN(aim.Power) || float.IsNaN(aim.HoldSeconds) || float.IsNaN(aim.Curve)) return SetPieceExecuteResult.InvalidAim;
            if ((aim.HasDestination && !ActionModel.IsFinite(aim.Destination)) || (aim.HasZone && !Enum.IsDefined(typeof(GoalZone), aim.Zone))) return SetPieceExecuteResult.InvalidAim;
            if (aim.TargetPlayerId != null)
            {
                MatchPlayer t = State.Team(r.Team).Find(aim.TargetPlayerId);
                if (t == null || !t.IsActive || t.PlayerId == r.TakerId || t.InGoal) return SetPieceExecuteResult.InvalidAim;
            }
            TeamSide team = r.Team; RestartKind kind = r.Kind; string takerId = r.TakerId; Vec2 spot = r.Position;
            if (!ConfirmRestart()) return SetPieceExecuteResult.NotReady;
            MatchPlayer taker = State.Team(team).Find(takerId);
            if (taker == null) return SetPieceExecuteResult.NotReady;

            Vec2 attack = AttackDirectionOf(team), goal = GoalCentreOf(team);
            float charge = aim.Power > 0f ? ActionModel.Clamp01(aim.Power) : FreeKickAiming.PowerFromHold(aim.HoldSeconds, freeKick);
            float power = kind == RestartKind.ThrowIn ? charge * setPiece.ThrowMaxPower : charge;
            Vec2 inward = new Vec2(0f, spot.Y > 0f ? -1f : 1f);
            Vec2 fallback = kind == RestartKind.ThrowIn ? spot + inward * 8f + attack * 3f : goal;
            Vec2 destination = FreeKickAiming.ResolveDestination(aim, spot, goal, attack, field.GoalWidth, freeKick, InsideLines, fallback, out bool hasDestination);
            Vec2 dir = ActionModel.DirectionOr(destination - spot, ActionModel.DirectionOr(fallback - spot, attack));

            MatchPlayer target = null;
            if (aim.TargetPlayerId != null) target = State.Team(team).Find(aim.TargetPlayerId);
            else if (aim.Intent == FreeKickIntent.Pass || (kind == RestartKind.ThrowIn && aim.Intent != FreeKickIntent.PassToPoint)) target = MateByDrag(team, taker, spot, dir);
            var request = new ActionRequest(team, taker.PlayerId, FootballActionKind.ShortPass, dir, power) { Source = ControlSource.Human };
            if (target != null && target.IsActive && target != taker && !target.InGoal) request.Target = PassTarget.ToPlayer(target.PlayerId);
            else if (hasDestination) request.Target = PassTarget.ToPosition(destination);
            bool lofted = kind == RestartKind.Corner || power >= setPiece.ThrowLoftedPower || (request.Target.Kind == PassTargetKind.Player && target != null && (target.Locomotion.Position - spot).Magnitude >= freeKick.PassLongDistance);
            request.Kind = lofted ? FootballActionKind.LongPass : FootballActionKind.ShortPass;
            ActionResult result = Perform(request);
            return result.Outcome == ActionOutcome.Rejected ? SetPieceExecuteResult.ActionRefused : SetPieceExecuteResult.Executed;
        }

        private MatchPlayer MateByDrag(TeamSide side, MatchPlayer taker, Vec2 spot, Vec2 dir)
        {
            TeamMatchState team = State.Team(side);
            var mates = new List<MatchPlayer>(team.Players.Count); var points = new List<Vec2>(team.Players.Count);
            for (int i = 0; i < team.Players.Count; i++) { MatchPlayer p = team.Players[i]; if (!p.IsActive || p == taker || p.InGoal) continue; mates.Add(p); points.Add(p.Locomotion.Position); }
            int index = FreeKickAiming.FindPassTarget(spot, dir, points, freeKick);
            return index >= 0 ? mates[index] : null;
        }
    }
}
