using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 32: every number of the stability of the player a person controls. One place; the twins are in <see cref="SelectionStabilityTuning"/>.
    /// The automatic choice (<see cref="AutoSelection"/>) answers "who SHOULD he control now" at every step; on its own it overrode the person's own switch within a frame and moved the marker between two defenders every second or two.
    /// </summary>
    public static class SelectionStabilityDefaults
    {
        /// <summary>After the person switches player by hand, the automatic choice leaves his player alone for this long (seconds).</summary>
        public const float ManualHoldSeconds = 3.0f;
        /// <summary>The automatic choice does not move the marker again sooner than this after it last moved it (seconds).</summary>
        public const float DwellSeconds = 0.8f;
        /// <summary>Without the ball, the automatic choice moves the marker only to a player at least this much nearer to the ball than the one he controls (metres).</summary>
        public const float SwitchMarginMeters = 1.5f;
    }

    /// <summary>The editable copy of <see cref="SelectionStabilityDefaults"/>. <see cref="Enabled"/> off = the automatic choice of Phase 8 (every step, no hold).</summary>
    [Serializable]
    public sealed class SelectionStabilityTuning
    {
        public bool Enabled = true;
        public float ManualHoldSeconds = SelectionStabilityDefaults.ManualHoldSeconds;
        public float DwellSeconds = SelectionStabilityDefaults.DwellSeconds;
        public float SwitchMarginMeters = SelectionStabilityDefaults.SwitchMarginMeters;
    }

    /// <summary>
    /// Phase 32: decides whether the automatic choice may move a person's marker NOW: never within the hold after a manual switch, never twice within the dwell, and (without the ball) only for a player clearly nearer to the ball.
    /// Stateful per side (when it last moved the marker); everything else is read from the match, so it stays deterministic.
    /// </summary>
    public sealed class SelectionStability
    {
        private readonly float[] lastMoveAt = { float.NegativeInfinity, float.NegativeInfinity };
        public SelectionStabilityTuning Tuning { get; } = new SelectionStabilityTuning();

        public bool Allow(WorldView view, MatchController match, TeamSide side, string current, string chosen)
        {
            if (chosen == null || chosen == current) return chosen != null;
            if (!Tuning.Enabled) return true;
            int keep = current != null ? view.IndexOf(current) : -1;
            bool currentValid = keep >= 0 && view[keep].IsOnPitch && view[keep].Team == side;
            if (!currentValid) return true;                                                                    // nobody to keep: the choice stands
            float now = match.State.MatchTimeSeconds;
            if (now - match.ManualSelectionAt(side) < Tuning.ManualHoldSeconds) return false;                    // the person chose: leave him
            if (now - lastMoveAt[(int)side] < Tuning.DwellSeconds) return false;                                // it moved a moment ago
            bool teamHasBall = view.Ball.HasPossessor && view.Ball.PossessingTeam == side;
            if (!teamHasBall)
            {
                int to = view.IndexOf(chosen);
                if (to >= 0)
                {
                    float gain = WorldGeometry.DistanceToBall(view[keep], view.Ball) - WorldGeometry.DistanceToBall(view[to], view.Ball);
                    if (gain < Tuning.SwitchMarginMeters) return false;                                         // not clearly nearer: keep what he has
                }
            }
            return true;
        }

        public void Moved(TeamSide side, float now) { lastMoveAt[(int)side] = now; }
    }
}
