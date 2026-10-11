using System;
using System.Collections.Generic;
using System.Linq;

namespace FS27.Core
{
    /// <summary>The knobs of the draw. Defaults are starting values, not rules, and everything is deterministic whatever they are.</summary>
    [Serializable]
    public sealed class RosterDrawSettings
    {
        /// <summary>If a draw fails the checks (e.g. an Elite team is not clearly stronger), the next attempt uses a derived seed. Same seed, same sequence of attempts.</summary>
        public int MaxAttempts = 16;
        /// <summary>
        /// The Elite criterion, in squad-strength points (see <see cref="RosterQualityModel"/>): every Elite team must beat the BEST other team by <see cref="EliteMargin"/> and the AVERAGE
        /// other team by <see cref="EliteMeanMargin"/>. The authored players are close in quality (every field player is rated 79-92), so squad strengths differ by a few points:
        /// measured over 60 seeds, the weaker Elite squad beat the best other team by 2.8 on average (never by less than 1.7) and the average team by 4.0 (never by less than 3.6).
        /// </summary>
        public double EliteMargin = 1.0;
        public double EliteMeanMargin = 3.0;
        /// <summary>Elite squads are dealt from the best (factor x needed) players of each kind, shuffled: strong, but not a mirror copy of each other.</summary>
        public double EliteCandidateFactor = 1.25;
        /// <summary>How much a team's level tilts its picks towards better (positive) or weaker (negative) players. Elite does not use it (it has its own stage).</summary>
        public double StrongBias = 0.75;
        public double BalancedBias = 0.0;
        public double ModestBias = -0.75;

        public double Bias(TeamLevel level)
        {
            switch (level)
            {
                case TeamLevel.Strong: return StrongBias;
                case TeamLevel.Modest: return ModestBias;
                default: return BalancedBias;
            }
        }
    }

    /// <summary>Whether a draw is possible with this library and these teams, with the numbers behind the answer.</summary>
    public sealed class DrawFeasibility
    {
        public int Teams;
        public int GoalkeepersAvailable, GoalkeepersRequired;
        public int FieldPlayersAvailable, FieldPlayersRequired;
        public readonly List<string> Problems = new List<string>();
        public bool IsFeasible { get { return Problems.Count == 0; } }
    }

    /// <summary>The outcome of a draw: the seed it was made with, the rosters (in team order) and, if it failed, why.</summary>
    public sealed class RosterGeneration
    {
        public int Seed;
        /// <summary>Which attempt succeeded (0 = the first). Part of the reproducible sequence.</summary>
        public int Attempt;
        public bool IsSuccess;
        public readonly List<TeamRoster> Rosters = new List<TeamRoster>();
        public readonly List<string> Problems = new List<string>();
        public DrawFeasibility Feasibility;

        public bool TryGetRoster(string teamId, out TeamRoster roster)
        {
            foreach (TeamRoster r in Rosters) if (r.TeamId == teamId) { roster = r; return true; }
            roster = null;
            return false;
        }
    }

    /// <summary>
    /// The deterministic draw (sorteo): <c>seed + player library + teams -> one roster per team</c>. It only SELECTS existing players; it never changes an attribute, a rating or a goalkeeper
    /// capability, never creates a player and never reads <c>PlayerDefinition.TeamId</c>. Same seed, same library, same teams, same settings = the same rosters, always (no clock, no global random).
    ///
    /// Algorithm (every random choice comes from <see cref="SeedMixer"/>/<see cref="SeededRandom"/>):
    ///  0. Feasibility: enough goalkeepers (2 per team), field players (8 per team) and of each line, checked BEFORE drawing; otherwise nothing is generated and the shortage is reported.
    ///  A. Elite teams first: their 2 goalkeepers and 8 field players are dealt from a shuffled pool of the best players (so they are strong but different from each other).
    ///  B. The other official teams and the user team, in a seeded random order, from what is left: first 2 goalkeepers each, then each line's minimum, then the free places; every pick is a
    ///     weighted random choice whose tilt depends on the team's level (Strong leans to better players, Modest to weaker ones, Balanced is neutral). No equalisation of any kind.
    ///  C. Each squad is split into its 6 active players (the better goalkeeper + the best field players, covering every line) and its 4 substitutes.
    ///  D. The result is validated; if an Elite team is not clearly stronger than the best and than the average of the rest (or anything else fails), the next attempt uses a derived seed. All attempts failing is reported, never hidden.
    /// </summary>
    public static class RosterGenerator
    {
        private static readonly PlayerRole[] Lines = { PlayerRole.Defender, PlayerRole.Midfielder, PlayerRole.Forward };

        public static DrawFeasibility CheckFeasibility(PlayerLibrary library, RosterQualityModel quality, IReadOnlyList<TeamDefinition> teams)
        {
            var f = new DrawFeasibility { Teams = teams == null ? 0 : teams.Count };
            if (library == null || quality == null || teams == null || teams.Count == 0) { f.Problems.Add("A library, a quality model and at least one team are required."); return f; }
            if (teams.Select(t => t.Id).Distinct().Count() != teams.Count) f.Problems.Add("Team ids must be unique.");

            int keepers = 0, field = 0;
            var lineCount = new Dictionary<PlayerRole, int>();
            foreach (PlayerDefinition p in library.All)
            {
                if (quality.IsKeeper(p.Id)) { keepers++; continue; }
                field++;
                int n; lineCount.TryGetValue(p.Role, out n); lineCount[p.Role] = n + 1;
            }
            f.GoalkeepersAvailable = keepers; f.GoalkeepersRequired = teams.Count * RosterRules.GoalkeepersPerRoster;
            f.FieldPlayersAvailable = field; f.FieldPlayersRequired = teams.Count * RosterRules.FieldPerRoster;
            if (keepers < f.GoalkeepersRequired) f.Problems.Add("Goalkeeper shortage: " + f.GoalkeepersRequired + " needed (" + RosterRules.GoalkeepersPerRoster + " per team), " + keepers + " available.");
            if (field < f.FieldPlayersRequired) f.Problems.Add("Field player shortage: " + f.FieldPlayersRequired + " needed (" + RosterRules.FieldPerRoster + " per team), " + field + " available.");
            CheckLine(f, lineCount, PlayerRole.Defender, RosterRules.MinDefenders, teams.Count);
            CheckLine(f, lineCount, PlayerRole.Midfielder, RosterRules.MinMidfielders, teams.Count);
            CheckLine(f, lineCount, PlayerRole.Forward, RosterRules.MinForwards, teams.Count);
            return f;
        }

        private static void CheckLine(DrawFeasibility f, Dictionary<PlayerRole, int> counts, PlayerRole role, int minPerTeam, int teams)
        {
            int have; counts.TryGetValue(role, out have);
            if (have < minPerTeam * teams) f.Problems.Add(role + " shortage: " + minPerTeam * teams + " needed, " + have + " available.");
        }

        public static RosterGeneration Generate(int seed, PlayerLibrary library, PlayingProfileCatalog profiles, IReadOnlyList<TeamDefinition> teams,
                                                RosterDrawSettings settings = null, PlayerRatingCalculator calculator = null)
        {
            settings = settings ?? new RosterDrawSettings();
            var result = new RosterGeneration { Seed = seed };
            if (library == null || teams == null) { result.Problems.Add("A library and teams are required."); return result; }

            var quality = new RosterQualityModel(library, profiles, calculator);
            result.Feasibility = CheckFeasibility(library, quality, teams);
            if (!result.Feasibility.IsFeasible) { result.Problems.AddRange(result.Feasibility.Problems); return result; }

            var eliteCount = teams.Count(t => t.Kind == TeamKind.Official && t.Level == TeamLevel.Elite);
            string last = "";
            for (int attempt = 0; attempt < Math.Max(1, settings.MaxAttempts); attempt++)
            {
                List<TeamRoster> rosters = TryAttempt(seed, attempt, library, quality, teams, settings, out string problem);
                if (rosters == null) { last = problem; continue; }
                RosterReport report = RosterGenerationValidator.Validate(teams, rosters, library, quality, settings.EliteMargin, settings.EliteMeanMargin);
                if (!report.IsValid) { last = report.ToString(); continue; }
                result.Attempt = attempt;
                result.Rosters.AddRange(rosters);
                result.IsSuccess = true;
                return result;
            }
            result.Problems.Add("No valid draw after " + Math.Max(1, settings.MaxAttempts) + " attempts (" + eliteCount + " elite teams). Last problem: " + last);
            return result;
        }

        // ------------------------------------------------------------------ one attempt

        private sealed class Picks
        {
            public readonly List<string> Keepers = new List<string>();
            public readonly List<string> Field = new List<string>();
        }

        private static List<TeamRoster> TryAttempt(int seed, int attempt, PlayerLibrary library, RosterQualityModel q, IReadOnlyList<TeamDefinition> teams,
                                                   RosterDrawSettings s, out string problem)
        {
            problem = "";
            SeededRandom rng = SeedMixer.Create(seed, attempt);

            // pools in a fixed order (by id) so that nothing depends on how the library happened to be filled
            var remKeepers = library.All.Where(p => q.IsKeeper(p.Id)).Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var remField = library.All.Where(p => !q.IsKeeper(p.Id)).Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var role = library.All.ToDictionary(p => p.Id, p => p.Role);

            int kMin = remKeepers.Min(q.Of), kMax = remKeepers.Max(q.Of), fMin = remField.Min(q.Of), fMax = remField.Max(q.Of);

            var picks = new Dictionary<string, Picks>();
            foreach (TeamDefinition t in teams) picks[t.Id] = new Picks();

            var elite = teams.Where(t => t.Kind == TeamKind.Official && t.Level == TeamLevel.Elite).ToList();
            var others = teams.Where(t => !(t.Kind == TeamKind.Official && t.Level == TeamLevel.Elite)).ToList();

            // ---- Stage A: the Elite squads, dealt from the best players, shuffled
            if (elite.Count > 0)
            {
                int gkNeed = elite.Count * RosterRules.GoalkeepersPerRoster;
                List<string> gkPool = Top(remKeepers, q, (int)Math.Ceiling(gkNeed * s.EliteCandidateFactor));
                Shuffle(gkPool, rng);
                for (int i = 0; i < gkNeed; i++) { picks[elite[i % elite.Count].Id].Keepers.Add(gkPool[i]); remKeepers.Remove(gkPool[i]); }

                int fNeed = elite.Count * RosterRules.FieldPerRoster;
                List<string> pool = Top(remField, q, (int)Math.Ceiling(fNeed * s.EliteCandidateFactor));
                Shuffle(pool, rng);
                foreach (TeamDefinition t in elite)
                    foreach (PlayerRole line in Lines)
                        for (int k = 0; k < MinFor(line); k++)
                            if (!TakeElite(pool, remField, role, q, picks[t.Id], line)) { problem = "No " + line + " left for elite team " + t.Id; return null; }
                foreach (TeamDefinition t in elite)
                    while (picks[t.Id].Field.Count < RosterRules.FieldPerRoster)
                        if (!TakeElite(pool, remField, role, q, picks[t.Id], null)) { problem = "No player left for elite team " + t.Id; return null; }
            }

            // ---- Stage B: everybody else, in a seeded random order, weighted by level
            var order = new List<TeamDefinition>(others);
            Shuffle(order, rng);
            foreach (TeamDefinition t in order)
                for (int k = 0; k < RosterRules.GoalkeepersPerRoster; k++)
                {
                    string id = Weighted(remKeepers, q, kMin, kMax, s.Bias(t.Level), rng);
                    if (id == null) { problem = "No goalkeeper left for " + t.Id; return null; }
                    picks[t.Id].Keepers.Add(id); remKeepers.Remove(id);
                }
            foreach (TeamDefinition t in order)
                foreach (PlayerRole line in Lines)
                    for (int k = 0; k < MinFor(line); k++)
                    {
                        string id = Weighted(remField.Where(x => role[x] == line).ToList(), q, fMin, fMax, s.Bias(t.Level), rng);
                        if (id == null) { problem = "No " + line + " left for " + t.Id; return null; }
                        picks[t.Id].Field.Add(id); remField.Remove(id);
                    }
            foreach (TeamDefinition t in order)
                while (picks[t.Id].Field.Count < RosterRules.FieldPerRoster)
                {
                    string id = Weighted(remField, q, fMin, fMax, s.Bias(t.Level), rng);
                    if (id == null) { problem = "No player left for " + t.Id; return null; }
                    picks[t.Id].Field.Add(id); remField.Remove(id);
                }

            // ---- Stage C: split each squad into 6 active + 4 substitutes
            var rosters = new List<TeamRoster>();
            foreach (TeamDefinition t in teams) rosters.Add(Compose(t.Id, seed, picks[t.Id], q, role));
            return rosters;
        }

        private static int MinFor(PlayerRole line)
        {
            return line == PlayerRole.Defender ? RosterRules.MinDefenders : line == PlayerRole.Midfielder ? RosterRules.MinMidfielders : RosterRules.MinForwards;
        }

        /// <summary>Takes the next suitable player from the shuffled elite pool; when the pool has none of that line, the best remaining player of it (still top quality).</summary>
        private static bool TakeElite(List<string> pool, List<string> remField, Dictionary<string, PlayerRole> role, RosterQualityModel q, Picks into, PlayerRole? line)
        {
            string id = null;
            foreach (string c in pool) if (!line.HasValue || role[c] == line.Value) { id = c; break; }
            if (id == null)
            {
                foreach (string c in remField.OrderByDescending(q.Of).ThenBy(x => x, StringComparer.Ordinal))
                    if (!line.HasValue || role[c] == line.Value) { id = c; break; }
            }
            if (id == null) return false;
            pool.Remove(id); remField.Remove(id); into.Field.Add(id);
            return true;
        }

        private static List<string> Top(List<string> ids, RosterQualityModel q, int n)
        {
            return ids.OrderByDescending(q.Of).ThenBy(x => x, StringComparer.Ordinal).Take(n).ToList();
        }

        /// <summary>Fisher-Yates driven by the seeded generator.</summary>
        private static void Shuffle<T>(List<T> list, SeededRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(rng.NextFloat01() * (i + 1));
                if (j > i) j = i;
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        /// <summary>A weighted random pick: better players weigh more for a positive bias, less for a negative one. Weights stay positive, so nobody is ever excluded.</summary>
        private static string Weighted(List<string> candidates, RosterQualityModel q, int min, int max, double bias, SeededRandom rng)
        {
            if (candidates.Count == 0) return null;
            double span = Math.Max(1, max - min);
            var weights = new double[candidates.Count];
            double total = 0.0;
            for (int i = 0; i < candidates.Count; i++)
            {
                double norm = (q.Of(candidates[i]) - min) / span;          // 0..1 over the whole library
                weights[i] = Math.Max(0.05, 1.0 + bias * (2.0 * norm - 1.0));
                total += weights[i];
            }
            double r = rng.NextFloat01() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                r -= weights[i];
                if (r < 0.0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static TeamRoster Compose(string teamId, int seed, Picks p, RosterQualityModel q, Dictionary<string, PlayerRole> role)
        {
            List<string> keepers = p.Keepers.OrderByDescending(q.Of).ThenBy(x => x, StringComparer.Ordinal).ToList();
            List<string> field = p.Field.OrderByDescending(q.Of).ThenBy(x => x, StringComparer.Ordinal).ToList();

            var active = new List<string>();
            foreach (PlayerRole line in Lines)
            {
                string best = field.FirstOrDefault(x => role[x] == line && !active.Contains(x));
                if (best != null) active.Add(best);                          // every line is represented among the five
            }
            foreach (string x in field) { if (active.Count >= RosterRules.ActiveField) break; if (!active.Contains(x)) active.Add(x); }
            active = active.OrderBy(x => Array.IndexOf(Lines, role[x])).ThenByDescending(q.Of).ThenBy(x => x, StringComparer.Ordinal).ToList();
            List<string> bench = field.Where(x => !active.Contains(x)).ToList();

            var activeIds = new List<string> { keepers[0] };
            activeIds.AddRange(active);
            var benchIds = new List<string> { keepers[1] };
            benchIds.AddRange(bench);
            return new TeamRoster(teamId, seed, activeIds, benchIds);
        }
    }
}
