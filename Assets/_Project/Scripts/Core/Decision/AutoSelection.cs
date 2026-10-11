namespace FS27.Core
{
    /// <summary>
    /// The minimal automatic choice of the player a person controls (Phase 8): without the ball (the opponent has it, or it is free) the FIELD player of the team nearest to the ball (Phase 9: against a carrier, the PRIMARY DEFENDER of the defence); with the ball
    /// the player already controlled, for as long as he is on the pitch. When there is nobody to keep, the nearest field player; the fallback of <c>MatchController.ControlledPlayerId</c> covers the rest.
    /// No lines, no roles, no anticipation: that belongs to a later phase.
    /// </summary>
    public static class AutoSelection
    {
        public static string Choose(WorldView view, TeamSide side, string current, DefenseTuning defense = null)
        {
            // defending against a carrier: the primary defender (the one the defence would send to him)
            if (view.Ball.HasPossessor && view.Ball.PossessingTeam != side && view.TryGetBallCarrier(out WorldPlayer carrier))
            {
                DefenseRoles.Rank(view, side, carrier, defense ?? new DefenseTuning(), out int primary, out _);
                if (primary >= 0) return view[primary].PlayerId;
            }
            bool teamHasBall = view.Ball.HasPossessor && view.Ball.PossessingTeam == side;
            int keep = current != null ? view.IndexOf(current) : -1;
            if (teamHasBall && keep >= 0 && view[keep].IsOnPitch && view[keep].Team == side) return current;
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            int start = view.TeamStart(side), end = start + view.TeamCount(side);
            for (int i = start; i < end; i++)
            {
                WorldPlayer p = view[i];
                if (!p.IsOnPitch || p.IsGoalkeeper) continue;
                float d = WorldGeometry.DistanceToBall(p, view.Ball);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            if (best >= 0) return view[best].PlayerId;
            return keep >= 0 && view[keep].IsOnPitch ? current : null;
        }
    }
}
