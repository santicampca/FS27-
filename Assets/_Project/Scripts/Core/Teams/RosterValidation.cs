using System;
using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    public enum RosterIssueCode
    {
        TeamWithoutRoster,
        RosterForUnknownTeam,
        DuplicateRoster,
        TotalPlayersMismatch,
        WrongPlayerCount,
        WrongActiveCount,
        WrongBenchCount,
        UnknownPlayer,
        PlayerRepeatedInRoster,
        PlayerInTwoTeams,
        KeeperInTwoTeams,
        WrongGoalkeeperCount,
        WrongFieldPlayerCount,
        ActiveSlotNotAKeeper,
        BenchSlotNotAKeeper,
        MissingRoleInSquad,
        MissingRoleInActive,
        EliteNotClearlyStronger
    }

    public sealed class RosterIssue
    {
        public RosterIssueCode Code;
        public string TeamId = "";
        public string Message = "";
        public override string ToString() { return Code + " [" + TeamId + "]: " + Message; }
    }

    public sealed class RosterReport
    {
        public readonly List<RosterIssue> Issues = new List<RosterIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
        public bool Has(RosterIssueCode code) { foreach (RosterIssue i in Issues) if (i.Code == code) return true; return false; }
        internal void Add(RosterIssueCode code, string team, string message) { Issues.Add(new RosterIssue { Code = code, TeamId = team ?? "", Message = message }); }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (RosterIssue i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Checks the result of a draw: one roster per team, no player or goalkeeper in two teams, and every roster has the right shape (10 players: 6 active = 1 goalkeeper + 5 field players,
    /// 4 substitutes = 1 goalkeeper + 3 field players, every id in the library, a plausible mix of roles). Also checks that every Elite team is clearly stronger than any other team.
    /// It reports; it never fixes anything and never changes a player.
    /// </summary>
    public static class RosterGenerationValidator
    {
        public static RosterReport Validate(IReadOnlyList<TeamDefinition> teams, IReadOnlyList<TeamRoster> rosters, PlayerLibrary library, RosterQualityModel quality, double eliteMargin, double eliteMeanMargin = 0.0)
        {
            var r = new RosterReport();
            var teamIds = new HashSet<string>();
            foreach (TeamDefinition t in teams) teamIds.Add(t.Id);

            var byTeam = new Dictionary<string, TeamRoster>();
            foreach (TeamRoster ro in rosters)
            {
                if (!teamIds.Contains(ro.TeamId)) r.Add(RosterIssueCode.RosterForUnknownTeam, ro.TeamId, "There is no such team.");
                else if (byTeam.ContainsKey(ro.TeamId)) r.Add(RosterIssueCode.DuplicateRoster, ro.TeamId, "Two rosters for one team.");
                else byTeam.Add(ro.TeamId, ro);
            }
            foreach (TeamDefinition t in teams)
                if (!byTeam.ContainsKey(t.Id)) r.Add(RosterIssueCode.TeamWithoutRoster, t.Id, "The team has no roster.");

            // global: nobody in two teams (a keeper in two teams is reported as that, too)
            var owner = new Dictionary<string, string>();
            int assigned = 0;
            foreach (TeamRoster ro in rosters)
            {
                foreach (string id in ro.AllPlayerIds)
                {
                    assigned++;
                    if (owner.TryGetValue(id, out string first) && first != ro.TeamId)
                        r.Add(quality != null && quality.IsKeeper(id) ? RosterIssueCode.KeeperInTwoTeams : RosterIssueCode.PlayerInTwoTeams, ro.TeamId, "'" + id + "' is also in '" + first + "'.");
                    else if (!owner.ContainsKey(id)) owner.Add(id, ro.TeamId);
                }
            }
            if (assigned != teams.Count * RosterRules.Size) r.Add(RosterIssueCode.TotalPlayersMismatch, "", assigned + " players assigned, " + teams.Count * RosterRules.Size + " expected.");

            foreach (TeamRoster ro in rosters) CheckRoster(r, ro, library, quality);

            // Elite: clearly stronger than the best other team AND than the average one
            if (quality != null)
            {
                double bestOther = double.MinValue, sumOther = 0.0; int nOther = 0;
                foreach (TeamDefinition t in teams)
                    if (!(t.Kind == TeamKind.Official && t.Level == TeamLevel.Elite) && byTeam.TryGetValue(t.Id, out TeamRoster ro)) { double st = quality.Strength(ro); bestOther = Math.Max(bestOther, st); sumOther += st; nOther++; }
                foreach (TeamDefinition t in teams)
                {
                    if (!(t.Kind == TeamKind.Official && t.Level == TeamLevel.Elite) || !byTeam.TryGetValue(t.Id, out TeamRoster ro) || nOther == 0) continue;
                    double strength = quality.Strength(ro);
                    if (strength < bestOther + eliteMargin)
                        r.Add(RosterIssueCode.EliteNotClearlyStronger, t.Id, "Strength " + strength.ToString("0.0") + " is not " + eliteMargin.ToString("0.0") + " above the best other team (" + bestOther.ToString("0.0") + ").");
                    else if (strength < sumOther / nOther + eliteMeanMargin)
                        r.Add(RosterIssueCode.EliteNotClearlyStronger, t.Id, "Strength " + strength.ToString("0.0") + " is not " + eliteMeanMargin.ToString("0.0") + " above the average other team (" + (sumOther / nOther).ToString("0.0") + ").");
                }
            }
            return r;
        }

        private static void CheckRoster(RosterReport r, TeamRoster ro, PlayerLibrary library, RosterQualityModel quality)
        {
            string id = ro.TeamId;
            if (ro.ActiveIds.Count != RosterRules.ActiveCount) r.Add(RosterIssueCode.WrongActiveCount, id, ro.ActiveIds.Count + " active players, " + RosterRules.ActiveCount + " expected.");
            if (ro.BenchIds.Count != RosterRules.BenchCount) r.Add(RosterIssueCode.WrongBenchCount, id, ro.BenchIds.Count + " substitutes, " + RosterRules.BenchCount + " expected.");
            IReadOnlyList<string> all = ro.AllPlayerIds;
            if (all.Count != RosterRules.Size) r.Add(RosterIssueCode.WrongPlayerCount, id, all.Count + " players, " + RosterRules.Size + " expected.");

            var seen = new HashSet<string>();
            int keepers = 0, field = 0, d = 0, m = 0, f = 0, activeD = 0, activeM = 0, activeF = 0;
            for (int i = 0; i < all.Count; i++)
            {
                string pid = all[i];
                if (!seen.Add(pid)) { r.Add(RosterIssueCode.PlayerRepeatedInRoster, id, "'" + pid + "' appears twice."); continue; }
                if (!library.TryGet(pid, out PlayerDefinition p)) { r.Add(RosterIssueCode.UnknownPlayer, id, "'" + pid + "' is not in the player library."); continue; }
                bool active = i < ro.ActiveIds.Count;
                bool keeper = quality != null ? quality.IsKeeper(pid) : p.Role == PlayerRole.Goalkeeper;
                if (keeper) { keepers++; continue; }
                field++;
                if (p.Role == PlayerRole.Defender) { d++; if (active) activeD++; }
                else if (p.Role == PlayerRole.Midfielder) { m++; if (active) activeM++; }
                else if (p.Role == PlayerRole.Forward) { f++; if (active) activeF++; }
            }

            if (keepers != RosterRules.GoalkeepersPerRoster) r.Add(RosterIssueCode.WrongGoalkeeperCount, id, keepers + " goalkeepers, " + RosterRules.GoalkeepersPerRoster + " expected.");
            if (field != RosterRules.FieldPerRoster) r.Add(RosterIssueCode.WrongFieldPlayerCount, id, field + " field players, " + RosterRules.FieldPerRoster + " expected.");
            if (ro.ActiveIds.Count > 0 && quality != null && !quality.IsKeeper(ro.ActiveIds[0])) r.Add(RosterIssueCode.ActiveSlotNotAKeeper, id, "The first active player must be the goalkeeper.");
            if (ro.BenchIds.Count > 0 && quality != null && !quality.IsKeeper(ro.BenchIds[0])) r.Add(RosterIssueCode.BenchSlotNotAKeeper, id, "The first substitute must be the substitute goalkeeper.");
            if (d < RosterRules.MinDefenders || m < RosterRules.MinMidfielders || f < RosterRules.MinForwards) r.Add(RosterIssueCode.MissingRoleInSquad, id, "Field mix D" + d + " M" + m + " F" + f + " is not plausible.");
            if (activeD < RosterRules.MinActivePerRole || activeM < RosterRules.MinActivePerRole || activeF < RosterRules.MinActivePerRole) r.Add(RosterIssueCode.MissingRoleInActive, id, "Active field mix D" + activeD + " M" + activeM + " F" + activeF + " lacks a line.");
        }
    }
}
