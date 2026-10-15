using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 29 ScenarioBench: the keeper with the ball in his hands, measured on the real Core. Every run is a real <see cref="MatchController"/> (real teams and attributes, the real <see cref="BasicDecisionDriver"/>
    /// deciding for all twelve players, the controller's own physics and rules): the scene only places the players and gives the ball to the keeper; everything after that is the Core playing. Nothing here decides for
    /// the keeper or imitates his rule: the action, the instant and the reason are read from the controller's <c>ActionPerformed</c> event and the driver's last decision.
    /// Scenarios (Home keeper at -18, Home attacks +X; 'covered' = a rival stands on the only receiver in range, the rest of the players are out of the keeper's range):
    ///   S1 FreeReceiver  — one team-mate in range, nobody near him.
    ///   S2 CoveredReceiver — the same team-mate with a rival at half a metre; no rival near the keeper.
    ///   S3 Rival3m — covered receiver and a second rival 3 m in front of the keeper.
    ///   S4 Rival7m — covered receiver and a second rival 7 m in front of the keeper.
    ///   S5 DecisionMaking — covered receiver; the same keeper under a difficulty whose goalkeeper decision making is high (0.9) and low (0.2).
    ///   S6 Difficulty — covered receiver; the home team under Novice and Elite.
    /// Each is played with the distribution on and off (<see cref="GoalkeeperAiTuning.DistributionEnabled"/>) over the same seeds with the same starting positions (the seed only moves the receiver sideways by a fixed
    /// amount and seeds the controller's own noise). Expected / suspect results per scenario: Docs/GOALKEEPER_SCENARIO_BENCH.md.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private const float BenchKeeperX = -18f;
        private const float BenchHoldSeconds = 3f;
        private const float BenchMaxSeconds = 9f;
        private const float BenchOffSeconds = 1.0f;
        private const float BenchOnLatestRelease = 3.2f;

        private sealed class BenchVariant { public string Scenario, Variant; public float? DecisionMaking; public DifficultyLevel Level; public Func<int, Vec2[]> HomeField, AwayField; }

        private static readonly string[] BenchHeader =
        {
            "scenario", "variant", "seed", "distributionEnabled", "homeLevel", "levelDecisionMaking", "keeperId", "keeperCommand", "keeperKicking", "keeperDistribution", "skillDecisionMaking",
            "nearestRivalToKeeper", "nearestRivalToReceiver", "released", "releaseSeconds", "action", "isLong", "targetId", "reason", "waitSecondsBeforeRelease", "ballOwnerAt3s", "retainedAt3s", "ballOwnerAfterRelease3s", "retainedAfterRelease3s", "finalPhase", "goalBy", "firstAwayTouchSeconds"
        };

        private static float BenchJitter(int seed) { return ((seed % 5) - 2) * 0.5f; }

        private static List<BenchVariant> BenchVariants()
        {
            // Home field players (5) are the first array, Away field players (5) the second; the keepers are placed by the bench (Home at -18, Away at +18)
            Func<int, Vec2[]> homeUp = seed => new[] { new Vec2(-9f, 7f + BenchJitter(seed)), new Vec2(6f, -8f), new Vec2(8f, 0f), new Vec2(10f, 6f), new Vec2(12f, -4f) };
            Func<int, Vec2[]> awayFar = seed => new[] { new Vec2(8f, -6f), new Vec2(10f, 2f), new Vec2(12f, 8f), new Vec2(14f, -2f), new Vec2(16f, 5f) };
            Func<int, Vec2[]> awayCovered = seed => new[] { new Vec2(-9.5f, 7f + BenchJitter(seed)), new Vec2(10f, 2f), new Vec2(12f, 8f), new Vec2(14f, -2f), new Vec2(16f, 5f) };
            Func<int, Vec2[]> away3 = seed => new[] { new Vec2(-9.5f, 7f + BenchJitter(seed)), new Vec2(-15f, 0.3f * BenchJitter(seed)), new Vec2(12f, 8f), new Vec2(14f, -2f), new Vec2(16f, 5f) };
            Func<int, Vec2[]> away7 = seed => new[] { new Vec2(-9.5f, 7f + BenchJitter(seed)), new Vec2(-11f, -0.3f * BenchJitter(seed)), new Vec2(12f, 8f), new Vec2(14f, -2f), new Vec2(16f, 5f) };
            var list = new List<BenchVariant>
            {
                new BenchVariant { Scenario = "S1_FreeReceiver", Variant = "Professional", Level = DifficultyLevel.Professional, HomeField = homeUp, AwayField = awayFar },
                new BenchVariant { Scenario = "S2_CoveredReceiver", Variant = "Professional", Level = DifficultyLevel.Professional, HomeField = homeUp, AwayField = awayCovered },
                new BenchVariant { Scenario = "S3_Rival3m", Variant = "Professional", Level = DifficultyLevel.Professional, HomeField = homeUp, AwayField = away3 },
                new BenchVariant { Scenario = "S4_Rival7m", Variant = "Professional", Level = DifficultyLevel.Professional, HomeField = homeUp, AwayField = away7 },
                new BenchVariant { Scenario = "S5_DecisionMaking", Variant = "High", Level = DifficultyLevel.Professional, DecisionMaking = 0.9f, HomeField = homeUp, AwayField = awayCovered },
                new BenchVariant { Scenario = "S5_DecisionMaking", Variant = "Low", Level = DifficultyLevel.Professional, DecisionMaking = 0.2f, HomeField = homeUp, AwayField = awayCovered },
                new BenchVariant { Scenario = "S6_Difficulty", Variant = "Novice", Level = DifficultyLevel.Novice, HomeField = homeUp, AwayField = awayCovered },
                new BenchVariant { Scenario = "S6_Difficulty", Variant = "Elite", Level = DifficultyLevel.Elite, HomeField = homeUp, AwayField = awayCovered }
            };
            return list;
        }

        private static string BenchOwner(MatchController c)
        {
            if (c.State.Phase != MatchPhase.Playing) return "stopped";
            if (!c.State.Ball.HasPossession) return "loose";
            return c.State.Ball.PossessingTeam == TeamSide.Home ? "home" : "away";
        }

        /// <summary>Whether Home still has the ball in his play: he owns it, or nobody does and the last to touch it was a Home player (the ball is on its way to a team-mate); 'stopped' when the play is dead (goal, out of play).</summary>
        private static string BenchRetained(MatchController c)
        {
            string owner = BenchOwner(c);
            if (owner == "stopped") return "stopped";
            if (owner == "home") return "yes";
            if (owner == "away") return "no";
            return c.State.Ball.HasLastTouch && c.State.Ball.LastTouchTeam == TeamSide.Home ? "yes" : "no";
        }

        private static int BenchCol(string name) { int i = Array.IndexOf(BenchHeader, name); Assert.GreaterOrEqual(i, 0, name); return i; }
        private static string BenchCell(string[] row, string name) { return row[BenchCol(name)]; }

        private static string BenchF(float v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        private static string BenchRun(BenchVariant v, int seed, bool distribution)
        {
            MatchController c = RealRoles(3, 8, seed, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart(), "kick-off");
            Assert.IsTrue(c.State.HomeAttacksPositiveX, "the scenes are written for Home attacking +X");
            BasicDecisionTuning basicT = BasicDecisionTuning.ForMode(GameMode.Classic); DefenseTuning defenseT = DefenseTuning.ForMode(GameMode.Classic);
            AttackPositioningTuning attackT = AttackPositioningTuning.ForMode(GameMode.Classic); BallCarrierTuning carrierT = BallCarrierTuning.ForMode(GameMode.Classic);
            GoalkeeperAiTuning keeperT = GoalkeeperAiTuning.ForMode(GameMode.Classic); keeperT.DistributionEnabled = distribution;
            var profileT = new DecisionProfileTuning();
            var driver = new BasicDecisionDriver(c, basicT, defenseT, null, attackT, carrierT, keeperT);
            DifficultyDefinition home = DefaultDifficulties.Create(v.Level); if (v.DecisionMaking.HasValue) home.Goalkeeper.DecisionMaking = v.DecisionMaking.Value;
            driver.SetDifficulty(TeamSide.Home, home, Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);

            // the scene: where the twelve stand; then the ball goes to the keeper (the only thing the bench does by hand)
            MatchPlayer keeper = c.State.Home.Players.First(p => p.IsActive && p.InGoal), rivalKeeper = c.State.Away.Players.First(p => p.IsActive && p.InGoal);
            List<MatchPlayer> homeField = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).ToList(), awayField = c.State.Away.Players.Where(p => p.IsActive && !p.InGoal).ToList();
            Assert.AreEqual(5, homeField.Count); Assert.AreEqual(5, awayField.Count);
            Vec2[] hf = v.HomeField(seed), af = v.AwayField(seed);
            for (int i = 0; i < 5; i++) { c.PlacePlayer(TeamSide.Home, homeField[i].PlayerId, hf[i], East.ToHeading()); c.PlacePlayer(TeamSide.Away, awayField[i].PlayerId, af[i], West.ToHeading()); }
            c.PlacePlayer(TeamSide.Away, rivalKeeper.PlayerId, new Vec2(18f, 0f), West.ToHeading());
            Carrier(c, TeamSide.Home, keeper.PlayerId, BenchKeeperX, 0f, East);
            Assert.IsTrue(c.State.Ball.HasPossession && c.State.Ball.PossessingTeam == TeamSide.Home, "the keeper has the ball");

            WorldView view = WorldState.Capture(c).View; WorldPlayer me = view[view.IndexOf(keeper.PlayerId)];
            GoalkeeperSkill skill = GoalkeeperSkillModel.ResolveNormalized(home.Goalkeeper, me.Profile.KeeperReflexes, me.Profile.KeeperPositioning, me.Profile.KeeperCommand, me.Profile.KeeperDiving, me.Profile.KeeperHandling, keeperT.Skill);
            float nearestToKeeper = WorldGeometry.NearestOpponentDistanceTo(view, TeamSide.Home, me.Position);
            float nearestToReceiver = WorldGeometry.NearestOpponentDistanceTo(view, TeamSide.Home, hf[0]);

            bool released = false; float releaseAt = 0f; string action = "", targetId = "", reason = "", ownerAfterRelease = "NA", retainedAfterRelease = "NA"; bool isLong = false;
            float t0 = c.State.MatchTimeSeconds; int waits = 0; string goalBy = "none"; float awayTouch = -1f; string at3 = null, retained3 = null;
            c.ActionPerformed += res =>
            {
                if (released || res.PlayerId != keeper.PlayerId || res.Outcome == ActionOutcome.Rejected) return;
                released = true; releaseAt = c.State.MatchTimeSeconds - t0; action = res.Kind.ToString(); targetId = res.TargetPlayerId ?? "";
            };
            c.Events.Published += ev =>
            {
                if (ev.Kind == GameplayEventKind.GoalScored && ev.HasTeam && goalBy == "none") goalBy = ev.Team == TeamSide.Home ? "home" : "away";
                if (released && ev.Kind == GameplayEventKind.BallTouched && ev.HasTeam && ev.Team == TeamSide.Away && awayTouch < 0f) awayTouch = c.State.MatchTimeSeconds - t0 - releaseAt;
            };
            int maxSteps = (int)(BenchMaxSeconds / Dt);
            for (int step = 0; step < maxSteps; step++)
            {
                driver.Step(c);
                if (driver.TryGetLast(keeper.PlayerId, out PlayerDecision d))
                {
                    if (!released && d.Reason == DecisionReason.KeeperWaitForLane) waits++;   // only the first possession: before he plays the ball
                    if (released && reason == "") { reason = d.Reason.ToString(); isLong = d.HasAction && d.Action.Long; }
                }
                c.Advance(Dt);
                float t = c.State.MatchTimeSeconds - t0;
                if (at3 == null && t >= BenchHoldSeconds) { at3 = BenchOwner(c); retained3 = BenchRetained(c); }
                if (released && ownerAfterRelease == "NA" && t >= releaseAt + BenchHoldSeconds) { ownerAfterRelease = BenchOwner(c); retainedAfterRelease = BenchRetained(c); }
                if (at3 != null && (released ? ownerAfterRelease != "NA" : true) && (released || t >= BenchMaxSeconds - Dt * 2)) break;
                if (at3 != null && !released && BenchOwner(c) != "home") break;   // he lost the ball before playing it
                if (c.State.Phase != MatchPhase.Playing) break;
            }
            if (at3 == null) { at3 = BenchOwner(c); retained3 = BenchRetained(c); }
            var f = new List<string>
            {
                v.Scenario, v.Variant, seed.ToString(CultureInfo.InvariantCulture), distribution ? "on" : "off", v.Level.ToString(), BenchF(home.Goalkeeper.DecisionMaking), keeper.PlayerId,
                BenchF(me.Profile.KeeperCommand), BenchF(me.Profile.KeeperKicking), BenchF(me.Profile.KeeperDistribution), BenchF(skill.DecisionMaking), BenchF(nearestToKeeper), BenchF(nearestToReceiver),
                released ? "yes" : "no", released ? BenchF(releaseAt) : "NA", action == "" ? "none" : action, released ? (isLong ? "yes" : "no") : "NA", targetId == "" ? "none" : targetId, reason == "" ? "none" : reason,
                BenchF(waits * Dt), at3, retained3, ownerAfterRelease, retainedAfterRelease, c.State.Phase.ToString(), goalBy, awayTouch < 0f ? "NA" : BenchF(awayTouch)
            };
            return string.Join(",", f);
        }

        /// <summary>The whole bench as CSV text (header, then one row per scenario variant x configuration x seed, in a fixed order).</summary>
        private static string BenchCsv(int seeds)
        {
            var sb = new StringBuilder(string.Join(",", BenchHeader)).Append('\n');
            foreach (BenchVariant v in BenchVariants())
                foreach (bool on in new[] { true, false })
                    for (int seed = 1; seed <= seeds; seed++) sb.Append(BenchRun(v, seed, on)).Append('\n');
            return sb.ToString();
        }

        private static string[][] BenchRows(string csv) { return csv.Split('\n').Where(l => l != "").Select(l => l.Split(',')).ToArray(); }

        /// <summary>Writes the bench CSV (SCEN_OUT) and prints a summary per scenario; SCEN_SEEDS (default 8).</summary>
        [Test, Explicit]
        public void ScenarioBench_Goalkeeper_WritesTheCsv()
        {
            int seeds = int.Parse(Environment.GetEnvironmentVariable("SCEN_SEEDS") ?? "8", CultureInfo.InvariantCulture);
            string path = Environment.GetEnvironmentVariable("SCEN_OUT");
            string csv = BenchCsv(seeds);
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, csv);
            string[][] rows = BenchRows(csv).Skip(1).ToArray();
            var sb = new StringBuilder("scenario/variant/config: n, released, mean release s, retained / lost / dead ball at 3 s\n");
            foreach (var g in rows.GroupBy(r => BenchCell(r, "scenario") + "/" + BenchCell(r, "variant") + "/" + BenchCell(r, "distributionEnabled")))
            {
                var rel = g.Where(r => BenchCell(r, "released") == "yes").Select(r => float.Parse(BenchCell(r, "releaseSeconds"), CultureInfo.InvariantCulture)).ToList();
                sb.Append(g.Key).Append(": ").Append(g.Count()).Append(", ").Append(rel.Count).Append(", ").Append(rel.Count == 0 ? "NA" : BenchF(rel.Average())).Append(", retained ")
                  .Append(g.Count(r => BenchCell(r, "retainedAt3s") == "yes")).Append(" lost ").Append(g.Count(r => BenchCell(r, "retainedAt3s") == "no")).Append(" dead ").Append(g.Count(r => BenchCell(r, "retainedAt3s") == "stopped")).Append(", goals against ").Append(g.Count(r => BenchCell(r, "goalBy") == "away")).Append(", goals for ").Append(g.Count(r => BenchCell(r, "goalBy") == "home")).Append('\n');
            }
            Console.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        [Test]
        public void ScenarioBench_Goalkeeper_HasEveryScenarioConfigurationAndSeed()
        {
            const int seeds = 3;
            string[][] rows = BenchRows(BenchCsv(seeds));
            Assert.AreEqual(string.Join(",", BenchHeader), string.Join(",", rows[0]));
            foreach (string[] r in rows) Assert.AreEqual(BenchHeader.Length, r.Length, "every row has every column");
            string[][] data = rows.Skip(1).ToArray();
            string[] scenarios = { "S1_FreeReceiver", "S2_CoveredReceiver", "S3_Rival3m", "S4_Rival7m", "S5_DecisionMaking", "S6_Difficulty" };
            foreach (string s in scenarios) Assert.IsTrue(data.Any(r => BenchCell(r, "scenario") == s), "scenario missing: " + s);
            string[] variants = { "S1_FreeReceiver/Professional", "S2_CoveredReceiver/Professional", "S3_Rival3m/Professional", "S4_Rival7m/Professional", "S5_DecisionMaking/High", "S5_DecisionMaking/Low", "S6_Difficulty/Novice", "S6_Difficulty/Elite" };
            foreach (string vr in variants)
                foreach (string cfg in new[] { "on", "off" })
                    for (int seed = 1; seed <= seeds; seed++)
                        Assert.AreEqual(1, data.Count(r => BenchCell(r, "scenario") + "/" + BenchCell(r, "variant") == vr && BenchCell(r, "distributionEnabled") == cfg && BenchCell(r, "seed") == seed.ToString(CultureInfo.InvariantCulture)), "missing or duplicated: " + vr + " " + cfg + " seed " + seed);
            Assert.AreEqual(variants.Length * 2 * seeds, data.Length);
            foreach (string[] r in data) foreach (string cell in r) Assert.IsFalse(string.IsNullOrEmpty(cell), "empty cell in " + string.Join(",", r));
            foreach (string[] r in data) Assert.Contains(BenchCell(r, "goalBy"), new[] { "none", "home", "away" });
        }

        [Test]
        public void ScenarioBench_Goalkeeper_IsByteForByteTheSameTwice()
        {
            Assert.AreEqual(BenchCsv(2), BenchCsv(2));
        }

        [Test]
        public void ScenarioBench_Goalkeeper_MeasuresTheRealCore_NotAParallelSimulation()
        {
            // 1) the source of the bench never calls the keeper's rule itself: everything it records comes from the controller's event and the driver's last decision
            string bench = File.ReadAllText(Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "..", "..", "Tests", "EditMode", "GoalkeeperScenarioBenchTests.cs"));
            foreach (string banned in new[] { "GoalkeeperDistribution" + ".Choose(", "KeeperPlay" + "Ball(", "PassRisk" + ".SuccessChance(", "new BasicDecision" + "Engine(" })
                Assert.IsFalse(bench.Contains(banned), "the bench must not imitate the rule: " + banned);
            // 2) what a row says matches what the real Core did: the reason of a release is the real decision, a wait is a real wait decision, and the switched-off keeper never waits
            string[][] data = BenchRows(BenchCsv(2)).Skip(1).ToArray();
            foreach (string[] r in data)
            {
                string line = string.Join(",", r);
                if (BenchCell(r, "distributionEnabled") == "off") Assert.AreEqual(BenchF(0f), BenchCell(r, "waitSecondsBeforeRelease"), "switched off he never waits: " + line);
                if (BenchCell(r, "released") == "yes") { Assert.AreEqual("KeeperDistribute", BenchCell(r, "reason"), "a release by the keeper is a KeeperDistribute decision: " + line); Assert.AreEqual("GoalkeeperDistribution", BenchCell(r, "action"), line); }
            }
            Assert.IsTrue(data.Any(r => BenchCell(r, "distributionEnabled") == "on" && BenchCell(r, "waitSecondsBeforeRelease") != BenchF(0f)), "with the distribution on, some real wait decisions show up in the covered scenes");
            // 3) the instants are the controller's own clock: with the distribution off he releases at the first decision; on, never later than patience plus a decision
            foreach (string[] r in data.Where(r => BenchCell(r, "released") == "yes"))
            {
                float t = float.Parse(BenchCell(r, "releaseSeconds"), CultureInfo.InvariantCulture);
                if (BenchCell(r, "distributionEnabled") == "off") Assert.LessOrEqual(t, BenchOffSeconds, string.Join(",", r));
                else Assert.LessOrEqual(t, BenchOnLatestRelease, string.Join(",", r));
            }
            // 4) he always plays the ball or loses it: never holds it for the whole run
            foreach (string[] r in data) Assert.IsTrue(BenchCell(r, "released") == "yes" || BenchCell(r, "ballOwnerAt3s") != "home", "held the ball without playing it: " + string.Join(",", r));
        }
    }
}
