using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>One kick of the shoot-out.</summary>
    public struct ShootoutKick
    {
        public int Round;
        public TeamSide Team;
        public bool Scored;
    }

    /// <summary>
    /// The state of a penalty shoot-out and its rules (only the state and the counting: the physical execution of a penalty comes later).
    /// Teams alternate, starting with <see cref="FirstTeam"/>. During the first <see cref="KicksPerTeam"/> kicks of each team it ends early when one team can no longer
    /// be caught; if still level, sudden death follows (one more kick each, decided after both have kicked). Deterministic: no randomness.
    /// </summary>
    public sealed class PenaltyShootoutState
    {
        private readonly List<ShootoutKick> kicks = new List<ShootoutKick>();
        private readonly int[] taken = new int[2];
        private readonly int[] goals = new int[2];

        public int KicksPerTeam { get; }
        public bool SuddenDeath { get; }
        public TeamSide FirstTeam { get; }
        public bool IsDecided { get; private set; }
        public bool HasWinner { get; private set; }
        public TeamSide Winner { get; private set; }
        public IReadOnlyList<ShootoutKick> Kicks => kicks;

        public PenaltyShootoutState(int kicksPerTeam, bool suddenDeath, TeamSide firstTeam)
        {
            KicksPerTeam = Math.Max(1, kicksPerTeam);
            SuddenDeath = suddenDeath;
            FirstTeam = firstTeam;
        }

        public int Goals(TeamSide t) { return goals[(int)t]; }
        public int Taken(TeamSide t) { return taken[(int)t]; }
        public int Misses(TeamSide t) { return taken[(int)t] - goals[(int)t]; }

        /// <summary>Who kicks next. Meaningless once decided.</summary>
        public TeamSide NextTeam => taken[(int)FirstTeam] <= taken[(int)FirstTeam.Opponent()] ? FirstTeam : FirstTeam.Opponent();

        /// <summary>0-based round of the next kick (a round is one kick by each team).</summary>
        public int Round => Math.Min(taken[0], taken[1]);

        /// <summary>True once both teams have used their regular kicks and the shoot-out is in sudden death.</summary>
        public bool InSuddenDeath => taken[0] >= KicksPerTeam && taken[1] >= KicksPerTeam && goals[0] == goals[1];

        /// <summary>Records the next kick. Returns false when the shoot-out is already decided.</summary>
        public bool RecordKick(bool scored)
        {
            if (IsDecided) return false;
            TeamSide team = NextTeam;
            kicks.Add(new ShootoutKick { Round = Round, Team = team, Scored = scored });
            taken[(int)team]++;
            if (scored) goals[(int)team]++;
            Evaluate();
            return true;
        }

        private void Evaluate()
        {
            int h = (int)TeamSide.Home, a = (int)TeamSide.Away;

            // Regular kicks still to come for each team: a team that cannot be caught any more wins early.
            int remH = Math.Max(0, KicksPerTeam - taken[h]);
            int remA = Math.Max(0, KicksPerTeam - taken[a]);
            if (taken[h] <= KicksPerTeam && taken[a] <= KicksPerTeam && (remH > 0 || remA > 0))
            {
                if (goals[h] > goals[a] + remA) { Decide(TeamSide.Home); return; }
                if (goals[a] > goals[h] + remH) { Decide(TeamSide.Away); return; }
                return;
            }

            // Both took the same number of kicks and that number reaches the regular kicks: level = sudden death, otherwise decided.
            if (taken[h] == taken[a] && taken[h] >= KicksPerTeam)
            {
                if (goals[h] != goals[a]) { Decide(goals[h] > goals[a] ? TeamSide.Home : TeamSide.Away); return; }
                if (!SuddenDeath) { IsDecided = true; HasWinner = false; }
            }
        }

        private void Decide(TeamSide winner)
        {
            IsDecided = true;
            HasWinner = true;
            Winner = winner;
        }
    }
}
