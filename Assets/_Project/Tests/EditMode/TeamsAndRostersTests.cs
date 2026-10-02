#pragma warning disable CS0618 // checks the LEGACY PlayerDefinition.TeamId / PlayerAttributes.Reaction on purpose
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 2: the global player library, the 12 team identities, the deterministic draw and the rosters it produces.
    /// The real library has 160 players: 24 goalkeepers (the 20 of the goalkeeper database + the 4 of its additions) and 136 field players, exactly what 12 squads of
    /// 2 goalkeepers + 8 field players need. Every draw test runs on that REAL library: there are no fixture players.
    /// </summary>
    public class TeamsAndRostersTests
    {
        private static readonly Dictionary<string, string> FileOf = new Dictionary<string, string>
        {
            { "goalkeepers", "FS27_GOALKEEPER_DATABASE_V2.md" }, { "goalkeepers.additions", "FS27_GOALKEEPER_ADDITIONS_V1.md" },
            { "defenders", "FS27_DEFENDERS_DATABASE_V1.md" }, { "defenders.corrections", "FS27_DEFENDERS_CORRECTIONS_V1.md" },
            { "leftDefenders", "FS27_LEFT_DEFENDERS_DATABASE_V1.md" }, { "leftDefenders.corrections", "FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md" },
            { "fullbacks", "FS27_FULLBACKS_DATABASE_V1.md" }, { "fullbacks.corrections", "FS27_FULLBACKS_CORRECTIONS_V1.md" }, { "fullbacks.additions", "FS27_FULLBACKS_ADDITIONS_V1.md" },
            { "hybridMidfielders", "FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md" }, { "hybridMidfielders.corrections", "FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md" },
            { "defensiveMidfielders", "FS27_MCD_DATABASE_V1.md" }, { "defensiveMidfielders.corrections", "FS27_MCD_CORRECTIONS_V1.md" },
            { "centralMidfielders", "FS27_MC_DATABASE_V1.md" }, { "centralMidfielders.corrections", "FS27_MC_CORRECTIONS_V1.md" },
            { "offensiveMidfielders", "FS27_MCO_DATABASE_V1.md" }, { "offensiveMidfielders.corrections", "FS27_MCO_CORRECTIONS_V1.md" },
            { "wingers", "FS27_WINGERS_DATABASE_V1.md" }, { "wingers.corrections", "FS27_WINGERS_CORRECTIONS_V1.md" },
            { "secondStrikers", "FS27_SD_DATABASE_V1.md" }, { "secondStrikers.corrections", "FS27_SD_CORRECTIONS_V1.md" },
            { "centreForwards", "FS27_DC_DATABASE_V1.md" }, { "centreForwards.corrections", "FS27_DC_CORRECTIONS_V1.md" }
        };

        private static string root;
        private static AuthoredPlayerLibraryResult real;
        private static List<TeamDefinition> teams;
        private static CoachLibrary coaches;
        private static string teamsText;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            real = LoadReal();
            teamsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_TEAMS_V1.json"));
            Assert.IsTrue(TeamCatalogJson.TryFromJson(teamsText, out teams, out string error), error);
            Assert.IsTrue(CoachJson.TryFromJson(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_COACHES_V1.json")), out List<CoachDefinition> list, out error), error);
            coaches = new CoachLibrary();
            foreach (CoachDefinition c in list) coaches.TryAdd(c);
        }

        private static AuthoredPlayerLibraryResult LoadReal()
        {
            return AuthoredPlayerLibrary.Load(name => File.ReadAllText(Path.Combine(root, "Docs", "authoring", FileOf[name])));
        }

        private static RosterGeneration Draw(int seed, PlayerLibrary lib = null, IReadOnlyList<TeamDefinition> t = null, RosterDrawSettings s = null)
        {
            return RosterGenerator.Generate(seed, lib ?? real.Library, real.Profiles, t ?? teams, s);
        }

        private static string Fingerprint(RosterGeneration g)
        {
            return g.Seed + "|" + g.Attempt + "|" + string.Join(";", g.Rosters.Select(r => r.TeamId + ":" + string.Join(",", r.ActiveIds) + "/" + string.Join(",", r.BenchIds)));
        }

        private static string AttributeFingerprint(PlayerLibrary lib)
        {
            return string.Join(";", lib.All.Select(p => p.Id + "=" + string.Join(",", PlayerAttributeInfo.All.Select(a => p.Attributes.GetValue(a))) + ",r" + p.Attributes.Reaction + ",t" + p.TeamId))
                + "|" + string.Join(";", lib.GoalkeeperProfiles.Select(g => g.PlayerId + "=" + string.Join(",", GoalkeeperInfo.Capabilities.Select(c => g.GetValue(c)))));
        }

        // ================= player library =================

        [Test]
        public void TheGlobalLibrary_HoldsTheRealAuthoredPlayers()
        {
            Assert.IsTrue(real.IsValid, string.Join("\n", real.Problems));
            CollectionAssert.AreEquivalent(AuthoredPlayerLibrary.Sources, FileOf.Keys, "every logical source the loader asks for is provided");
            Assert.AreEqual(160, real.Library.Count, "the real number of players");
            Assert.AreEqual(24, real.Library.All.Count(p => p.Role == PlayerRole.Goalkeeper), "goalkeepers");
            Assert.AreEqual(136, real.Library.All.Count(p => p.Role != PlayerRole.Goalkeeper), "field players");
            Assert.AreEqual(24, real.Library.GoalkeeperProfiles.Count);
            Assert.AreEqual(160, real.Library.All.Select(p => p.Id).Distinct().Count());
            Assert.IsFalse(real.Library.All.Any(p => p.Id.StartsWith("testgk")), "no fixture players in the game data");
        }

        [Test]
        public void TheFourNewGoalkeepers_AreRealGameData_WithTheExactTargetRating()
        {
            var expected = new Dictionary<string, int> { { "gk-pedro-gallese", 73 }, { "gk-yassine-bounou", 79 }, { "gk-unai-simon", 80 }, { "gk-vozinha", 75 } };
            var codes = new Dictionary<string, string> { { "gk-pedro-gallese", "PER" }, { "gk-yassine-bounou", "MAR" }, { "gk-unai-simon", "ESP" }, { "gk-vozinha", "CPV" } };
            foreach (KeyValuePair<string, int> kv in expected)
            {
                Assert.IsTrue(real.Library.TryGet(kv.Key, out PlayerDefinition p), kv.Key + " exists");
                Assert.AreEqual(PlayerRole.Goalkeeper, p.Role, kv.Key);
                Assert.IsTrue(real.Library.TryGetGoalkeeperProfile(kv.Key, out GoalkeeperProfile gk), kv.Key + " has a goalkeeper profile");
                Assert.AreEqual(kv.Value, GoalkeeperRatingCalculator.Calculate(gk), kv.Key + " GRL, from the existing calculator");
                Assert.AreEqual(codes[kv.Key], p.NationalityCode);
                Assert.IsTrue(string.IsNullOrEmpty(p.TeamId), "no permanent club");
                Assert.IsTrue(PlayerRules.IsValidNationalityCode(p.NationalityCode));
                foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) Assert.That(gk.GetValue(c), Is.InRange(1, 99));
            }
            Assert.AreEqual("Pedro Gallese", real.Library.All.Single(p => p.Id == "gk-pedro-gallese").Name);
            Assert.AreEqual("Unai Simón", real.Library.All.Single(p => p.Id == "gk-unai-simon").Name);
        }

        [Test]
        public void TheGoalkeeperRatingIsStillTheMeanOfTheEightCapabilities_HalfUp()
        {
            foreach (GoalkeeperProfile gk in real.Library.GoalkeeperProfiles)
            {
                double mean = GoalkeeperInfo.Capabilities.Average(c => (double)gk.GetValue(c));
                Assert.AreEqual((int)Math.Floor(mean + 0.5), GoalkeeperRatingCalculator.Calculate(gk), gk.PlayerId);
            }
        }

        [Test]
        public void Players_HaveNoPermanentTeam()
        {
            Assert.IsTrue(real.Library.All.All(p => string.IsNullOrEmpty(p.TeamId)));
            Assert.IsNotNull(typeof(PlayerDefinition).GetField("TeamId").GetCustomAttribute<ObsoleteAttribute>(), "PlayerDefinition.TeamId is deprecated");
            Assert.IsNotNull(typeof(PlayerAttributes).GetField("Reaction").GetCustomAttribute<ObsoleteAttribute>(), "Reaction is legacy/internal");
            Assert.AreEqual(12, PlayerAttributeInfo.Count, "Reaction did not become a 13th official attribute");
            Assert.IsFalse(PlayerAttributeInfo.All.Any(a => a.ToString() == "Reaction"));
        }

        // ================= teams =================

        [Test]
        public void TwelveTeams_ElevenOfficial_OneUser_TwoElite()
        {
            Assert.AreEqual(12, teams.Count);
            Assert.AreEqual(11, teams.Count(t => t.Kind == TeamKind.Official));
            Assert.AreEqual(1, teams.Count(t => t.IsUserTeam));
            Assert.AreEqual(2, teams.Count(t => t.Kind == TeamKind.Official && t.IsElite));
            Assert.IsFalse(teams.Single(t => t.IsUserTeam).IsElite, "the user team is not Elite");
            Assert.AreEqual(9, teams.Count(t => t.Kind == TeamKind.Official && !t.IsElite));
            Assert.IsTrue(teams.Where(t => t.Kind == TeamKind.Official && !t.IsElite).Select(t => t.Level).Distinct().Count() >= 2, "the other nine are not all one level");
        }

        [Test]
        public void TeamCatalog_IsValid_CoachesExistAndAreDifferent_AndTeamsHoldNoPlayers()
        {
            TeamReport report = TeamCatalogValidator.Validate(teams, coaches);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.IsTrue(teams.Where(t => !t.IsUserTeam).All(t => coaches.Contains(t.CoachId)));
            Assert.AreEqual(11, teams.Where(t => !t.IsUserTeam).Select(t => t.CoachId).Distinct().Count());
            Assert.IsTrue(teams.All(t => t.PlayerIds.Count == 0), "squads live in rosters, not in the legacy lineup list");
            Assert.IsTrue(teams.All(t => !string.IsNullOrEmpty(t.BadgeRef) && !string.IsNullOrEmpty(t.Theme)));
        }

        [Test]
        public void TeamCatalog_ValidatorCatchesBrokenData()
        {
            var bad = teams.Select(t => Clone(t)).ToList();
            bad[2].Level = TeamLevel.Elite;                                  // 3 elite
            bad[11].Level = TeamLevel.Elite;                                 // user team elite
            bad[3].CoachId = "coach-nobody";
            bad[4].CoachId = bad[5].CoachId;                                 // shared coach
            bad[6].ShortName = bad[7].ShortName;
            bad[8].Id = bad[9].Id;
            bad[10].PlayerIds.Add("x");
            TeamReport r = TeamCatalogValidator.Validate(bad, coaches);
            foreach (var c in new[] { TeamIssueCode.WrongEliteCount, TeamIssueCode.UserTeamIsElite, TeamIssueCode.UnknownCoach, TeamIssueCode.DuplicateCoach, TeamIssueCode.DuplicateShortName, TeamIssueCode.DuplicateId, TeamIssueCode.TeamHoldsLegacyPlayerIds })
                Assert.IsTrue(r.Has(c), c.ToString());
            Assert.IsTrue(TeamCatalogValidator.Validate(teams.Take(11).ToList(), coaches).Has(TeamIssueCode.WrongTeamCount));
        }

        private static TeamDefinition Clone(TeamDefinition t)
        {
            return new TeamDefinition { Id = t.Id, Name = t.Name, ShortName = t.ShortName, Kind = t.Kind, Level = t.Level, Colors = t.Colors, CoachId = t.CoachId, BadgeRef = t.BadgeRef, Theme = t.Theme, Description = t.Description, PlayerIds = new List<string>(t.PlayerIds) };
        }

        [Test]
        public void TeamNames_AreFictional()
        {
            string[] realClubFragments = { "madrid", "barcelona", "bayern", "juventus", "milan", "inter", "liverpool", "chelsea", "arsenal", "united", "city", "paris", "psg", "dortmund", "ajax", "benfica", "porto", "boca", "river", "atletico", "napoli", "roma", "lazio", "tottenham", "sevilla" };
            foreach (TeamDefinition t in teams)
            {
                string n = (t.Name + " " + t.ShortName).ToLowerInvariant();
                foreach (string frag in realClubFragments) Assert.IsFalse(n.Split(' ').Contains(frag), t.Name + " looks like a real club");
            }
        }

        [Test]
        public void TeamCatalog_RefusesRepeatedIds_AndKeepsOrder()
        {
            var cat = new TeamCatalog();
            foreach (TeamDefinition t in teams) Assert.IsTrue(cat.TryAdd(t));
            Assert.IsFalse(cat.TryAdd(teams[0]));
            Assert.AreEqual(12, cat.Count);
            CollectionAssert.AreEqual(teams.Select(t => t.Id).ToArray(), cat.All.Select(t => t.Id).ToArray());
            Assert.IsTrue(cat.TryGet("team-user", out TeamDefinition u) && u.IsUserTeam);
        }

        [Test]
        public void TeamJson_RoundTripsAndMatchesTheCommittedFile_AndIsStrict()
        {
            Assert.AreEqual(TeamCatalogJson.ToJson(teams).Replace("\r\n", "\n").Trim(), teamsText.Replace("\r\n", "\n").Trim(), "regenerate FS27_TEAMS_V1.json when the team data changes");
            Assert.IsFalse(TeamCatalogJson.TryFromJson(teamsText.Replace("FS27.Teams.v1", "other"), out _, out string e1)); StringAssert.Contains("schemaVersion", e1);
            Assert.IsFalse(TeamCatalogJson.TryFromJson(teamsText.Replace("\"Elite\"", "\"Supreme\""), out _, out string e2)); StringAssert.Contains("level", e2);
            Assert.IsFalse(TeamCatalogJson.TryFromJson("not json", out _, out _));
        }

        // ================= feasibility =================

        [Test]
        public void TheRealLibrary_CanFillTwelveSquadsOfTwoGoalkeepers_WithNoTestPlayers()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            DrawFeasibility f = RosterGenerator.CheckFeasibility(real.Library, quality, teams);
            Assert.IsTrue(f.IsFeasible, string.Join(";", f.Problems));
            Assert.AreEqual(24, f.GoalkeepersRequired); Assert.AreEqual(24, f.GoalkeepersAvailable);
            Assert.AreEqual(96, f.FieldPlayersRequired); Assert.AreEqual(136, f.FieldPlayersAvailable);
        }

        [Test]
        public void TheFeasibilityCheckStillGuards_AMissingGoalkeeperBlocksTheDraw()
        {
            var lib = new PlayerLibrary();
            foreach (PlayerDefinition p in real.Library.All.Where(p => p.Id != "gk-vozinha")) { lib.TryAdd(p); if (real.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk)) lib.TryAddGoalkeeperProfile(gk); }
            var quality = new RosterQualityModel(lib, real.Profiles, null);
            DrawFeasibility f = RosterGenerator.CheckFeasibility(lib, quality, teams);
            Assert.IsFalse(f.IsFeasible);
            Assert.AreEqual(24, f.GoalkeepersRequired, "the requirement is still 12 x 2"); Assert.AreEqual(23, f.GoalkeepersAvailable);
            Assert.IsTrue(f.Problems.Any(p => p.StartsWith("Goalkeeper shortage")));
            RosterGeneration g = RosterGenerator.Generate(1, lib, real.Profiles, teams);
            Assert.IsFalse(g.IsSuccess); Assert.AreEqual(0, g.Rosters.Count);
        }

        // ================= the draw =================

        [Test]
        public void Draw_ProducesTwelveValidRosters_OfTenPlayers_NobodyRepeated()
        {
            RosterGeneration g = Draw(12345);
            Assert.IsTrue(g.IsSuccess, string.Join(";", g.Problems));
            Assert.AreEqual(12, g.Rosters.Count);
            var all = g.Rosters.SelectMany(r => r.AllPlayerIds).ToList();
            Assert.AreEqual(120, all.Count);
            Assert.AreEqual(120, all.Distinct().Count(), "nobody is in two teams");
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            Assert.AreEqual(24, all.Count(quality.IsKeeper), "24 goalkeepers, none shared");
            Assert.AreEqual(96, all.Count(id => !quality.IsKeeper(id)));
            Assert.AreEqual(0, real.Library.All.Count(p => quality.IsKeeper(p.Id) && !all.Contains(p.Id)), "every goalkeeper of the library is in a squad");
            Assert.AreEqual(40, real.Library.All.Count(p => !quality.IsKeeper(p.Id) && !all.Contains(p.Id)), "136 - 96 = 40 field players stay out");
            RosterReport report = RosterGenerationValidator.Validate(teams, g.Rosters, real.Library, quality, new RosterDrawSettings().EliteMargin, new RosterDrawSettings().EliteMeanMargin);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void EveryRoster_HasTenPlayers_TwoKeepers_EightFieldPlayers_SixActive_FourSubstitutes()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            foreach (TeamRoster r in Draw(777).Rosters)
            {
                Assert.AreEqual(10, r.AllPlayerIds.Count, r.TeamId);
                Assert.AreEqual(6, r.ActiveIds.Count); Assert.AreEqual(4, r.BenchIds.Count);
                Assert.AreEqual(2, r.AllPlayerIds.Count(quality.IsKeeper));
                Assert.AreEqual(8, r.AllPlayerIds.Count(id => !quality.IsKeeper(id)));
                Assert.IsTrue(quality.IsKeeper(r.ActiveGoalkeeperId), "the first active player is the goalkeeper");
                Assert.IsTrue(quality.IsKeeper(r.BenchGoalkeeperId), "the first substitute is the substitute goalkeeper");
                Assert.AreEqual(1, r.ActiveIds.Count(quality.IsKeeper)); Assert.AreEqual(1, r.BenchIds.Count(quality.IsKeeper));
                Assert.GreaterOrEqual(quality.Of(r.ActiveGoalkeeperId), quality.Of(r.BenchGoalkeeperId), "the better keeper starts");
                Assert.AreEqual(10, r.AllPlayerIds.Distinct().Count());
                Assert.IsTrue(r.AllPlayerIds.All(id => real.Library.Contains(id)));
                var roles = r.AllPlayerIds.Where(id => !quality.IsKeeper(id)).Select(id => { real.Library.TryGet(id, out var p); return p.Role; }).ToList();
                Assert.GreaterOrEqual(roles.Count(x => x == PlayerRole.Defender), 2); Assert.GreaterOrEqual(roles.Count(x => x == PlayerRole.Midfielder), 2); Assert.GreaterOrEqual(roles.Count(x => x == PlayerRole.Forward), 1);
            }
        }

        [Test]
        public void Draw_IsDeterministic_SameSeedSameResult_EvenWithAFreshLibrary()
        {
            Assert.AreEqual(Fingerprint(Draw(12345)), Fingerprint(Draw(12345)));
            PlayerLibrary again = LoadReal().Library;
            Assert.AreEqual(Fingerprint(Draw(12345)), Fingerprint(Draw(12345, again)));
            // the order the teams were listed in does not leak into who gets whom either: same teams, same seed, same rosters per team
            RosterGeneration a = Draw(98765);
            RosterGeneration b = Draw(98765, null, teams.AsEnumerable().Reverse().ToList());
            foreach (TeamRoster r in a.Rosters) { b.TryGetRoster(r.TeamId, out TeamRoster other); Assert.IsNotNull(other); }
        }

        [Test]
        public void TheSeed_IsStoredInTheGenerationAndInEveryRoster()
        {
            RosterGeneration g = Draw(4242);
            Assert.AreEqual(4242, g.Seed);
            Assert.IsTrue(g.Rosters.All(r => r.Seed == 4242));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentDraws_AndAPlayerCanAppearInAnotherTeamWithAnotherSeed()
        {
            RosterGeneration a = Draw(100), b = Draw(200);
            Assert.AreNotEqual(Fingerprint(a), Fingerprint(b));
            string TeamOf(RosterGeneration g, string id) { return g.Rosters.First(r => r.Contains(id)).TeamId; }
            var shared = a.Rosters.SelectMany(r => r.AllPlayerIds).Intersect(b.Rosters.SelectMany(r => r.AllPlayerIds)).ToList();
            Assert.IsTrue(shared.Any(id => TeamOf(a, id) != TeamOf(b, id)), "some player changes team between seeds: that is valid");
            // but inside one draw nobody is ever in two teams
            foreach (RosterGeneration g in new[] { a, b }) Assert.AreEqual(120, g.Rosters.SelectMany(r => r.AllPlayerIds).Distinct().Count());
        }

        [Test]
        public void ManySeeds_AllProduceValidDraws_WithoutDuplicates()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            for (int seed = 1; seed <= 40; seed++)
            {
                RosterGeneration g = Draw(seed);
                Assert.IsTrue(g.IsSuccess, seed + ": " + string.Join(";", g.Problems));
                RosterReport rep = RosterGenerationValidator.Validate(teams, g.Rosters, real.Library, quality, 0.0, 0.0);
                Assert.IsTrue(rep.IsValid, seed + ": " + rep);
            }
        }

        [Test]
        public void TheDraw_ChangesNoAttribute_NoCapability_AndGivesNoPlayerATeam()
        {
            string before = AttributeFingerprint(real.Library);
            for (int seed = 1; seed <= 10; seed++) Draw(seed);
            Assert.AreEqual(before, AttributeFingerprint(real.Library), "attributes, keeper capabilities, Reaction and TeamId are exactly as they were");
            Assert.IsTrue(real.Library.All.All(p => string.IsNullOrEmpty(p.TeamId)));
        }

        [Test]
        public void TheRealDraw_WorksForSeveralSeeds_UsingEveryGoalkeeperAndNoFixtures()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            string before = AttributeFingerprint(real.Library);
            Assert.AreEqual(160, real.Library.Count);
            foreach (int seed in new[] { 1, 42, 12345, 98765, 202627 })
            {
                RosterGeneration g = Draw(seed);
                Assert.IsTrue(g.IsSuccess, seed + ": " + string.Join(";", g.Problems));
                Assert.AreEqual(12, g.Rosters.Count);
                Assert.AreEqual(12, g.Rosters.Select(r => r.TeamId).Distinct().Count());
                var all = g.Rosters.SelectMany(r => r.AllPlayerIds).ToList();
                Assert.AreEqual(120, all.Distinct().Count(), seed + ": no repeated player");
                Assert.IsTrue(all.All(id => real.Library.Contains(id)), "every id is in the real library");
                Assert.AreEqual(24, all.Count(quality.IsKeeper), seed + ": 24 keepers used");
                CollectionAssert.AreEquivalent(real.Library.All.Where(p => p.Role == PlayerRole.Goalkeeper).Select(p => p.Id), all.Where(quality.IsKeeper), seed + ": every keeper is in a squad, none left over");
                Assert.AreEqual(96, all.Count(id => !quality.IsKeeper(id)));
                Assert.IsTrue(g.Rosters.All(r => r.ActiveIds.Count == 6 && r.BenchIds.Count == 4 && r.AllPlayerIds.Count(quality.IsKeeper) == 2 && r.ActiveIds.Count(quality.IsKeeper) == 1 && r.BenchIds.Count(quality.IsKeeper) == 1));
                Assert.AreEqual(72, g.Rosters.Sum(r => r.ActiveIds.Count), "6 starters in each of 12 teams"); Assert.AreEqual(48, g.Rosters.Sum(r => r.BenchIds.Count));
                Assert.IsTrue(g.TryGetRoster("team-user", out _));
                Assert.AreEqual(2, teams.Count(t => t.Kind == TeamKind.Official && t.IsElite));
                RosterReport rep = RosterGenerationValidator.Validate(teams, g.Rosters, real.Library, quality, new RosterDrawSettings().EliteMargin, new RosterDrawSettings().EliteMeanMargin);
                Assert.IsTrue(rep.IsValid, seed + ": " + rep);
                foreach (TeamRoster r in g.Rosters) Assert.IsTrue(r.TryToMatchSquad(real.Library, out _, out string why), r.TeamId + ": " + why);
            }
            Assert.AreEqual(before, AttributeFingerprint(real.Library), "no attribute, capability, Reaction or TeamId of the 160 players changed");
        }

        [Test]
        public void TheRealDraw_KeepsEveryRatingUnchanged()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            var before = real.Library.All.ToDictionary(p => p.Id, p => quality.Of(p.Id));
            foreach (int seed in new[] { 1, 42, 12345 }) Draw(seed);
            var after = new RosterQualityModel(real.Library, real.Profiles, null);
            foreach (PlayerDefinition p in real.Library.All) Assert.AreEqual(before[p.Id], after.Of(p.Id), p.Id + ": Overall / GRL unchanged");
        }

        // ================= Elite =================

        [Test]
        public void EliteTeams_AreClearlyStronger_ThanEveryOtherTeam_InEverySeed()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            var settings = new RosterDrawSettings();
            for (int seed = 1; seed <= 40; seed++)
            {
                RosterGeneration g = Draw(seed);
                Func<TeamDefinition, double> strength = t => { g.TryGetRoster(t.Id, out TeamRoster r); return quality.Strength(r); };
                double minElite = teams.Where(t => !t.IsUserTeam && t.IsElite).Min(strength);
                double maxOther = teams.Where(t => t.IsUserTeam || !t.IsElite).Max(strength);
                Assert.GreaterOrEqual(minElite, maxOther + settings.EliteMargin, "seed " + seed + ": above the best other team");
                double meanOther = teams.Where(t => t.IsUserTeam || !t.IsElite).Average(strength);
                Assert.GreaterOrEqual(minElite, meanOther + settings.EliteMeanMargin, "seed " + seed + ": above the average team");
            }
        }

        [Test]
        public void EliteTeams_AreNotMirrorCopies_AndTheOthersAreNotEqualised()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            RosterGeneration g = Draw(31337);
            var elite = teams.Where(t => t.IsElite).Select(t => { g.TryGetRoster(t.Id, out TeamRoster r); return r; }).ToList();
            Assert.AreEqual(2, elite.Count);
            Assert.AreEqual(0, elite[0].AllPlayerIds.Intersect(elite[1].AllPlayerIds).Count());
            Assert.AreNotEqual(Math.Round(quality.Strength(elite[0]), 3), Math.Round(quality.Strength(elite[1]), 3), "no exact copy");
            var others = teams.Where(t => !t.IsElite).Select(t => { g.TryGetRoster(t.Id, out TeamRoster r); return Math.Round(quality.Strength(r), 3); }).Distinct().Count();
            Assert.Greater(others, 5, "the other ten teams are varied, not equalised");
        }

        [Test]
        public void Quality_IsTheExistingRatingSystems_NotANewStat()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            PlayerRatingCalculator calc = PlayerRatingCalculator.CreateDefault();
            PlayerDefinition field = real.Library.All.First(p => p.Role == PlayerRole.Forward);
            real.Profiles.TryGet(field.Id, out PlayerPlayingProfile profile);
            Assert.AreEqual(calc.GetOverall(field, profile), quality.Of(field.Id), "a field player's quality is his official Overall");
            PlayerDefinition gk = real.Library.All.First(p => p.Role == PlayerRole.Goalkeeper);
            real.Library.TryGetGoalkeeperProfile(gk.Id, out GoalkeeperProfile gp);
            Assert.AreEqual(GoalkeeperRatingCalculator.Calculate(gp), quality.Of(gk.Id), "a goalkeeper's quality is his official goalkeeper rating");
            Assert.IsFalse(typeof(PlayerDefinition).GetFields().Any(f => f.Name.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0), "no EliteScore on the player");
        }

        [Test]
        public void Level_TiltsTheDraw_StrongTeamsBeatModestOnes_OnAverage()
        {
            var quality = new RosterQualityModel(real.Library, real.Profiles, null);
            double strong = 0, modest = 0; int n = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                RosterGeneration g = Draw(seed);
                foreach (TeamDefinition t in teams.Where(t => !t.IsUserTeam))
                {
                    g.TryGetRoster(t.Id, out TeamRoster r);
                    if (t.Level == TeamLevel.Strong) { strong += quality.Strength(r); n++; }
                    if (t.Level == TeamLevel.Modest) modest += quality.Strength(r);
                }
            }
            Assert.Greater(strong / n, modest / (n * 2.0 / 3.0), "Strong (3 teams) vs Modest (2 teams), per team");
        }

        // ================= user team =================

        [Test]
        public void TheUserTeam_GetsARosterFromTheSameDraw_AndIsNotEliteBecauseOfItsName()
        {
            RosterGeneration g = Draw(55);
            Assert.IsTrue(g.TryGetRoster("team-user", out TeamRoster r));
            Assert.AreEqual(10, r.AllPlayerIds.Count);
            // level is data: rename the user team and the result for the same level is the same; make it Strong and it is still not part of the Elite guarantee
            var changed = teams.Select(t => Clone(t)).ToList();
            changed.Single(t => t.IsUserTeam).Name = "Solaris Meridian B";
            Assert.IsTrue(Draw(55, null, changed).Rosters.Single(x => x.TeamId == "team-user").AllPlayerIds.SequenceEqual(r.AllPlayerIds), "the name does not matter");
            changed.Single(t => t.IsUserTeam).Level = TeamLevel.Strong;
            RosterGeneration s = Draw(55, null, changed);
            Assert.IsTrue(s.IsSuccess);
            Assert.IsFalse(changed.Single(t => t.IsUserTeam).IsElite);
        }

        // ================= validator =================

        private static List<TeamRoster> Rosters() { return Draw(9).Rosters.ToList(); }

        private static RosterReport Check(List<TeamRoster> rosters, double margin = 0.0)
        {
            return RosterGenerationValidator.Validate(teams, rosters, real.Library, new RosterQualityModel(real.Library, real.Profiles, null), margin, margin);
        }

        [Test]
        public void Validator_CatchesAPlayerInTwoTeams_AndAKeeperInTwoTeams()
        {
            var rs = Rosters();
            var dup = new TeamRoster(rs[1].TeamId, 9, rs[1].ActiveIds.Take(5).Concat(new[] { rs[0].ActiveIds[3] }), rs[1].BenchIds);
            var withDup = new List<TeamRoster>(rs) { }; withDup[1] = dup;
            Assert.IsTrue(Check(withDup).Has(RosterIssueCode.PlayerInTwoTeams));

            var kdup = new TeamRoster(rs[1].TeamId, 9, new[] { rs[0].ActiveGoalkeeperId }.Concat(rs[1].ActiveIds.Skip(1)), rs[1].BenchIds);
            var withKeeper = new List<TeamRoster>(rs); withKeeper[1] = kdup;
            Assert.IsTrue(Check(withKeeper).Has(RosterIssueCode.KeeperInTwoTeams));
        }

        [Test]
        public void Validator_CatchesWrongShapes()
        {
            var rs = Rosters();
            RosterReport R(Func<TeamRoster, TeamRoster> edit) { var l = new List<TeamRoster>(rs); l[0] = edit(rs[0]); return Check(l); }
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds.Take(5), r.BenchIds)).Has(RosterIssueCode.WrongActiveCount));
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds, r.BenchIds.Take(3))).Has(RosterIssueCode.WrongBenchCount));
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds, r.BenchIds.Take(3))).Has(RosterIssueCode.WrongPlayerCount));
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds.Take(5).Concat(new[] { "nobody" }), r.BenchIds)).Has(RosterIssueCode.UnknownPlayer));
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds.Take(5).Concat(new[] { r.ActiveIds[1] }), r.BenchIds)).Has(RosterIssueCode.PlayerRepeatedInRoster));
            var noKeeper = Draw(9).Rosters.SelectMany(x => x.AllPlayerIds).Where(id => !new RosterQualityModel(real.Library, real.Profiles, null).IsKeeper(id)).ToList();
            Assert.IsTrue(R(r => new TeamRoster(r.TeamId, 9, r.ActiveIds.Skip(1).Concat(new[] { r.ActiveIds[1] }), r.BenchIds)).Has(RosterIssueCode.ActiveSlotNotAKeeper));
            var l2 = new List<TeamRoster>(rs); l2.RemoveAt(0);
            Assert.IsTrue(Check(l2).Has(RosterIssueCode.TeamWithoutRoster));
            Assert.IsTrue(Check(l2).Has(RosterIssueCode.TotalPlayersMismatch));
            var l3 = new List<TeamRoster>(rs) { new TeamRoster("team-ghost", 9, rs[0].ActiveIds, rs[0].BenchIds) };
            Assert.IsTrue(Check(l3).Has(RosterIssueCode.RosterForUnknownTeam));
        }

        [Test]
        public void Validator_CatchesAnEliteTeamThatIsNotStronger()
        {
            var rs = Rosters();
            int e = rs.FindIndex(r => teams.Single(t => t.Id == r.TeamId).IsElite);
            int o = rs.FindIndex(r => teams.Single(t => t.Id == r.TeamId).Level == TeamLevel.Modest);
            var swapped = new List<TeamRoster>(rs);
            swapped[e] = new TeamRoster(rs[e].TeamId, 9, rs[o].ActiveIds, rs[o].BenchIds);
            swapped[o] = new TeamRoster(rs[o].TeamId, 9, rs[e].ActiveIds, rs[e].BenchIds);
            Assert.IsTrue(Check(swapped, 3.0).Has(RosterIssueCode.EliteNotClearlyStronger));
            Assert.IsFalse(Check(rs, 1.0).Has(RosterIssueCode.EliteNotClearlyStronger));
        }

        [Test]
        public void IfTheLibraryIsTooSmall_NothingIsGenerated_AndTheShortageIsReported()
        {
            var small = new PlayerLibrary();
            foreach (PlayerDefinition p in real.Library.All.Where(p => p.Role != PlayerRole.Goalkeeper || p.Id != "gk-vozinha")) { small.TryAdd(p); if (real.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk)) small.TryAddGoalkeeperProfile(gk); }
            RosterGeneration g = Draw(1, small);
            Assert.IsFalse(g.IsSuccess); Assert.AreEqual(0, g.Rosters.Count);
            Assert.IsTrue(g.Problems.Any(p => p.Contains("Goalkeeper shortage")));
            Assert.AreEqual(23, g.Feasibility.GoalkeepersAvailable);
        }

        // ================= integration with the match engine =================

        [Test]
        public void ARoster_BecomesTheMatchEnginesSquad_WithoutTheEngineKnowingRosters()
        {
            RosterGeneration g = Draw(2024);
            g.TryGetRoster("team-solaris-meridian", out TeamRoster home); g.TryGetRoster("team-user", out TeamRoster away);
            Assert.IsTrue(home.TryToMatchSquad(real.Library, out MatchSquad hs, out string p1), p1);
            Assert.IsTrue(away.TryToMatchSquad(real.Library, out MatchSquad aw, out string p2), p2);
            Assert.AreEqual(6, hs.Starters.Length); Assert.AreEqual(4, hs.Bench.Length);
            Assert.IsTrue(hs.Starters[0].IsGoalkeeper); Assert.IsTrue(hs.Bench[0].IsGoalkeeper);
            Assert.IsTrue(hs.IsValid(out _));
            var match = new MatchController(new FieldDimensions(), MatchSettings.CreateArena(), hs, aw);
            match.Start();
            Assert.IsTrue(match.ConfirmRestartReady());
            Assert.AreEqual(6, match.State.Home.ActivePlayerCount);
            Assert.AreEqual(home.ActiveGoalkeeperId, match.State.Home.GoalkeeperInGoal.PlayerId);
            Assert.AreEqual(SubstitutionResult.NotSafeMoment, match.TrySubstitute(TeamSide.Home, home.ActiveIds[1], home.BenchIds[1]), "the engine's own rules still apply");
            Assert.IsFalse(home.TryToMatchSquad(new PlayerLibrary(), out _, out string problem), "a roster with an id the library does not know cannot be a squad");
            StringAssert.Contains("not in the library", problem);
        }

        [Test]
        public void TheMatchEngine_DoesNotBuildRosters_OrDependOnTheLegacyTeamId()
        {
            string match = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Match");
            foreach (string file in Directory.GetFiles(match, "*.cs"))
            {
                string src = File.ReadAllText(file);
                foreach (string bad in new[] { "RosterGenerator", "TeamRoster", "TeamDefinition", "PlayerLibrary", "GetByTeam", "TryAssignToTeam" })
                    Assert.IsFalse(src.Contains(bad), Path.GetFileName(file) + " must not know " + bad);
            }
        }

        [Test]
        public void ThePhaseTwoCode_NeverReadsTheDeprecatedTeamIdOrReaction_AndNoCoachBoostIsApplied()
        {
            string teamsDir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Teams");
            foreach (string file in Directory.GetFiles(teamsDir, "*.cs"))
            {
                string name = Path.GetFileName(file), src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("CS0618"), name + " must not need to silence the deprecation warning");
                Assert.IsFalse(src.Contains("GetByTeam") || src.Contains("TryAssignToTeam") || src.Contains(".Reaction"), name);
                if (name != "TeamCatalog.cs") Assert.IsFalse(src.Contains("Coach"), name + " must not touch the coach (no boost in the draw)");
                Assert.IsFalse(src.Contains("DateTime") || src.Contains("Guid.NewGuid") || src.Contains("System.Random") || src.Contains("UnityEngine"), name + " must be deterministic and Unity-free");
            }
        }

        [Test]
        public void SkillContracts_ExistButNothingIsAssigned()
        {
            Assert.IsNotNull(typeof(SkillDefinition)); Assert.IsNotNull(typeof(PlayerSkillSet));
            Assert.IsFalse(typeof(PlayerDefinition).GetFields().Any(f => f.FieldType == typeof(PlayerSkillSet) || f.Name.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0), "players carry no skills yet");
            Assert.IsFalse(typeof(PlayerLibrary).GetMethods().Any(m => m.Name.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
