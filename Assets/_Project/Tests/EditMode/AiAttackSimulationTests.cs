using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 10: twelve real players, real rosters and formations, roles, defence and the attack without the ball, for two minutes in each mode. Measured, not balanced; compared with the same match with the attack layer off (Phase 9).</summary>
    public partial class AiInfrastructureTests
    {
        private static void ApplyExploration(GameMode mode, BasicDecisionTuning basic, DefenseTuning defense, AttackPositioningTuning attack, BallCarrierTuning carrier)
        {
            string spec = Environment.GetEnvironmentVariable("FS27_EXP");
            if (string.IsNullOrEmpty(spec)) return;
            foreach (string item in spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = item.Split('='), path = kv[0].Trim().Split('.');
                if (path.Length != 3 || (path[0] != "A" && path[0] != "C" && path[0] != "*")) throw new ArgumentException("bad exploration item " + item);
                if (path[0] == "A" && mode != GameMode.Arena) continue;
                if (path[0] == "C" && mode != GameMode.Classic) continue;
                object target = path[1] == "Basic" ? (object)basic : path[1] == "Defense" ? defense : path[1] == "Attack" ? attack : path[1] == "BallCarrier" ? (object)carrier : throw new ArgumentException(item);
                System.Reflection.FieldInfo f = target.GetType().GetField(path[2]);
                if (f == null) throw new ArgumentException("no field " + item);
                f.SetValue(target, f.FieldType == typeof(bool) ? (object)(kv[1].Trim() == "1" || kv[1].Trim() == "true") : f.FieldType == typeof(int) ? (object)int.Parse(kv[1]) : (object)float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The driver of the last simulation (so that diagnostics can be checked against the driver's own counters).</summary>
        private static BasicDecisionDriver LastDriver;

        private sealed class AttackSim
        {
            public string Fingerprint, Assignment;
            public int Issues, Possessions, Losses, Goals, Restarts, Decisions, Actions, Passes, Shots, Recoveries, Fouls;
            public int Support, Forward, Width, GetOpen;
            public int AttackSamples, PassDecisions; public long PassLanes; public double Spread, PassDistance;
            public int MoreThanOnePerJob, JobSamples, SupportersNearBall;
            public int[] ToJob = new int[5];
            public int[] Kinds = new int[17]; public int Dribbles, Protects, Holds, SpacePasses, ShotOpportunities, CarrierDecisions; public long PassCandidates; public int[] Chosen = new int[8]; public double[] ChosenScore = new double[8];
        }

        private static AttackSim SimulateAttack(GameMode mode, int seed, bool attackLayer, int homeTeam = 3, int awayTeam = 8, float seconds = 120f, bool carrierLayer = true, AiDiagnostics diagnostics = null)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealRoles(homeTeam, awayTeam, seed, s);
            var r = new AttackSim();
            r.Assignment = string.Join(",", c.GetRoleAssignment(TeamSide.Home).FieldPlayerIds) + "|" + string.Join(",", c.GetRoleAssignment(TeamSide.Away).FieldPlayerIds);
            Assert.IsTrue(c.ConfirmRestart());
            // the tunings of the mode (what the engine itself would use), with the switches of this run and, for Phase 13 exploration only, overrides from FS27_EXP ("A.BallCarrier.DribbleSpaceHorizon=7;C.Defense.X=1")
            BasicDecisionTuning basicT = BasicDecisionTuning.ForMode(mode); DefenseTuning defenseT = DefenseTuning.ForMode(mode);
            AttackPositioningTuning attackT = AttackPositioningTuning.ForMode(mode); attackT.Enabled = attackLayer;
            BallCarrierTuning carrierT = BallCarrierTuning.ForMode(mode); carrierT.Enabled = carrierLayer;
            ApplyExploration(mode, basicT, defenseT, attackT, carrierT);
            var driver = new BasicDecisionDriver(c, basicT, defenseT, null, attackT, carrierT);
            if (diagnostics != null) { diagnostics.Attach(c); driver.Diagnostics = diagnostics; }
            var world = new WorldState();
            bool hasLast = false; TeamSide last = TeamSide.Home;
            int steps = (int)(seconds * 60f);
            for (int step = 0; step < steps; step++)
            {
                driver.Step(c);
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) { if (c.ConfirmRestart()) r.Restarts++; else r.Issues++; }
                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                if (c.State.Ball.HasPossession)
                {
                    TeamSide t = c.State.Ball.PossessingTeam;
                    if (!hasLast || t != last) { r.Possessions++; if (hasLast) r.Losses++; hasLast = true; last = t; }
                }
                if (step % 10 != 0) continue;

                world.Refresh(c);
                int onPitch = 0, keepers = 0;
                var points = new List<Vec2>();
                for (int i = 0; i < world.PlayerCount; i++)
                {
                    WorldPlayer w = world[i];
                    if (!w.IsOnPitch) continue;
                    onPitch++; if (w.IsGoalkeeper) keepers++;
                    points.Add(w.Position);
                    if (float.IsNaN(w.Position.X) || float.IsNaN(w.Position.Y) || float.IsInfinity(w.Position.X) || float.IsInfinity(w.Position.Y)) r.Issues++;
                    if (Math.Abs(w.Position.X) > 21.01f || Math.Abs(w.Position.Y) > 13.51f) r.Issues++;
                    if (w.Stamina < 0f || w.Stamina > 100.001f || float.IsNaN(w.Stamina)) r.Issues++;
                    if (!w.HasHome || float.IsNaN(w.Home.X) || float.IsNaN(w.Home.Y)) r.Issues++;
                    if (!w.IsGoalkeeper && (w.Roles == null || w.Roles.Count == 0)) r.Issues++;
                }
                if (onPitch != 12 || keepers != 2) r.Issues++;
                for (int side = 0; side < 2; side++)
                {
                    TeamSide ts = side == 0 ? TeamSide.Home : TeamSide.Away;
                    RoleAssignment a = c.GetRoleAssignment(ts);
                    if (a.FieldPlayerIds.Distinct().Count() != 5 || a.FieldPlayerIds.Contains(a.GoalkeeperId)) r.Issues++;
                }
                if (PlayerSeparation.Smallest(points.ToArray(), points.Count) < c.Infrastructure.MinSeparation * 0.5f) r.Issues++;
                Vec3 b = c.State.Ball.Physics.Position;
                if (float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsNaN(b.Z) || Math.Abs(b.X) > 60f || Math.Abs(b.Z) > 40f) r.Issues++;
                if (c.State.Ball.HasPossession)
                {
                    MatchPlayer holder = c.State.Team(c.State.Ball.PossessingTeam).Find(c.State.Ball.PossessingPlayerId);
                    if (holder == null || !holder.IsActive) r.Issues++;
                    else if (attackLayer)
                    {
                        // the jobs of the team with the ball: never more than one of each, and nobody left running to the ball
                        var engine = driver.Engine;
                        WorldView v = world.View;
                        int ci = v.IndexOf(holder.PlayerId);
                        engine.Attack.Assign(v, ci);
                        int sup = 0, fwd = 0, wid = 0, open = 0;
                        for (int i = v.TeamStart(c.State.Ball.PossessingTeam); i < v.TeamStart(c.State.Ball.PossessingTeam) + v.TeamCount(c.State.Ball.PossessingTeam); i++)
                            switch (engine.Attack.JobOf(i)) { case AttackJob.Support: sup++; break; case AttackJob.Forward: fwd++; break; case AttackJob.Width: wid++; break; case AttackJob.GetOpen: open++; break; }
                        r.JobSamples++;
                        if (sup > 1 || fwd > 1 || wid > 1) r.MoreThanOnePerJob++;
                    }
                }
            }
            if (diagnostics != null) { diagnostics.Finish(); diagnostics.Detach(); }
            LastDriver = driver;
            r.Decisions = driver.Decisions; r.Actions = driver.ActionsAttempted; r.Passes = driver.PassesCompleted; r.Shots = driver.ShotsCompleted; r.Recoveries = driver.Recoveries; r.Fouls = driver.Fouls;
            r.Support = driver.CountOf(DecisionKind.Support); r.Forward = driver.CountOf(DecisionKind.ForwardRun); r.Width = driver.CountOf(DecisionKind.HoldWidth); r.GetOpen = driver.CountOf(DecisionKind.GetOpen);
            r.AttackSamples = driver.AttackSamples; r.PassLanes = driver.PassLaneSum; r.Spread = driver.TeammateSpreadSum; r.PassDecisions = driver.PassDecisions; r.PassDistance = driver.PassDistanceSum;
            for (int j = 0; j < 5; j++) r.ToJob[j] = driver.PassesToJob((AttackJob)j);
            for (int j = 0; j < 17; j++) r.Kinds[j] = driver.CountOf((DecisionKind)j);
            r.Dribbles = driver.CountOf(DecisionKind.Dribble); r.Protects = driver.CountOf(DecisionKind.Protect); r.Holds = driver.CountOf(DecisionKind.Hold); r.SpacePasses = driver.SpacePasses; r.ShotOpportunities = driver.ShotOpportunities; r.CarrierDecisions = driver.CarrierDecisions; r.PassCandidates = driver.PassCandidateSum; for (int j = 0; j < 8; j++) { r.Chosen[j] = driver.ChosenCount((CandidateType)j); r.ChosenScore[j] = driver.ChosenScoreSum((CandidateType)j); }
            r.Goals = c.State.Score.Home + c.State.Score.Away;
            r.Fingerprint = Fingerprint(c) + "|" + r.Decisions + "|" + r.Actions + "|" + r.Passes + "|" + r.Shots + "|" + r.Recoveries + "|" + r.Fouls + "|" + r.Possessions + "|" + r.Losses + "|" + r.Support + "|" + r.Forward + "|" + r.Width + "|" + r.GetOpen + "|" + r.PassLanes + "|" + r.Spread.ToString("R") + "|" + r.Dribbles + "|" + r.Protects + "|" + r.Holds + "|" + r.SpacePasses + "|" + r.ShotOpportunities + "|" + c.State.Score.Home + "-" + c.State.Score.Away;
            return r;
        }

        private static string Describe(AttackSim r)
        {
            double lanes = r.AttackSamples > 0 ? (double)r.PassLanes / r.AttackSamples : 0.0, spread = r.AttackSamples > 0 ? r.Spread / r.AttackSamples : 0.0, pd = r.PassDecisions > 0 ? r.PassDistance / r.PassDecisions : 0.0;
            return string.Format("decisions {0} actions {1} passes {2} shots {3} goals {4} possessions {5} losses {6} recoveries {7} fouls {8} | support {9} forward {10} width {11} getopen {12} | lanes/sample {13:F2} spread {14:F2} m passDist {15:F2} m",
                r.Decisions, r.Actions, r.Passes, r.Shots, r.Goals, r.Possessions, r.Losses, r.Recoveries, r.Fouls, r.Support, r.Forward, r.Width, r.GetOpen, lanes, spread, pd);
        }

        private static void AssertAttacks(AttackSim r, string label)
        {
            Assert.AreEqual(0, r.Issues, label + ": invariants (twelve players, one keeper each, finite, in the pitch, stamina, apart, homes, roles, possession, no slot twice)");
            Assert.Greater(r.Possessions, 2, label); Assert.Greater(r.Losses, 0, label); Assert.Greater(r.Passes, 5, label);
            Assert.Greater(r.Support, 0, label + ": support runs"); Assert.Greater(r.Forward, 0, label + ": runs forward"); Assert.Greater(r.Width, 0, label + ": width held"); Assert.Greater(r.GetOpen, 0, label + ": players getting open");
            Assert.Greater(r.JobSamples, 20, label); Assert.AreEqual(0, r.MoreThanOnePerJob, label + ": never more than one support, one runner and one width player");
            Assert.Greater(r.AttackSamples, 20, label); Assert.Greater(r.PassLanes, 0L, label + ": passing lanes were open");
            Assert.Greater(r.PassDecisions, 5, label);
            Assert.Greater(r.Support + r.Forward + r.Width + r.GetOpen, r.Decisions / 20, label + ": the attack layer decides a good part of the time");
        }

        /// <summary>Eight matches of two minutes with different teams and seeds, added up.</summary>
        private static AttackSim Aggregate(GameMode mode, bool carrierLayer, int firstSeed, int matches = 8)
        {
            var tot = new AttackSim();
            for (int k = 0; k < matches; k++)
            {
                int seed = firstSeed + k;
                AttackSim x = SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 120f, carrierLayer);
                tot.Decisions += x.Decisions; tot.Actions += x.Actions; tot.Passes += x.Passes; tot.Shots += x.Shots; tot.Goals += x.Goals; tot.Possessions += x.Possessions; tot.Losses += x.Losses; tot.Recoveries += x.Recoveries; tot.Fouls += x.Fouls;
                tot.Support += x.Support; tot.Forward += x.Forward; tot.Width += x.Width; tot.GetOpen += x.GetOpen;
                tot.AttackSamples += x.AttackSamples; tot.PassLanes += x.PassLanes; tot.Spread += x.Spread; tot.PassDecisions += x.PassDecisions; tot.PassDistance += x.PassDistance; tot.Issues += x.Issues;
                tot.MoreThanOnePerJob += x.MoreThanOnePerJob; tot.JobSamples += x.JobSamples;
                for (int j = 0; j < 5; j++) tot.ToJob[j] += x.ToJob[j];
                for (int j = 0; j < 17; j++) tot.Kinds[j] += x.Kinds[j];
                tot.Dribbles += x.Dribbles; tot.Protects += x.Protects; tot.Holds += x.Holds; tot.SpacePasses += x.SpacePasses; tot.ShotOpportunities += x.ShotOpportunities; tot.CarrierDecisions += x.CarrierDecisions; tot.PassCandidates += x.PassCandidates;
                for (int j = 0; j < 8; j++) { tot.Chosen[j] += x.Chosen[j]; tot.ChosenScore[j] += x.ChosenScore[j]; }
            }
            return tot;
        }

        /// <summary>How varied the play with the ball is: the Shannon entropy (bits) of the kinds of decision a carrier takes (pass, shot, dribble, protect, hold).</summary>
        private static double CarrierEntropy(AttackSim r)
        {
            int[] kinds = { (int)DecisionKind.Pass, (int)DecisionKind.Shoot, (int)DecisionKind.Dribble, (int)DecisionKind.Protect, (int)DecisionKind.Hold };
            double total = kinds.Sum(k => (double)r.Kinds[k]), h = 0.0;
            foreach (int k in kinds) if (r.Kinds[k] > 0) { double p = r.Kinds[k] / total; h -= p * Math.Log(p, 2.0); }
            return h;
        }

        private static string Describe11(AttackSim r)
        {
            return string.Format("passes {0} (to support/runner/width/open {1}/{2}/{3}/{4}, into space {5}) shots {6} (opportunities {7}) goals {8} | dribble {9} protect {10} hold {11} pass-decisions {12} | possessions {13} losses {14} recoveries {15} fouls {16} | lanes {17:F2} passDist {18:F2} entropy {19:F2}",
                r.Passes, r.ToJob[1], r.ToJob[2], r.ToJob[3], r.ToJob[4], r.SpacePasses, r.Shots, r.ShotOpportunities, r.Goals, r.Kinds[(int)DecisionKind.Dribble], r.Kinds[(int)DecisionKind.Protect], r.Kinds[(int)DecisionKind.Hold], r.Kinds[(int)DecisionKind.Pass],
                r.Possessions, r.Losses, r.Recoveries, r.Fouls, (double)r.PassLanes / Math.Max(1, r.AttackSamples), r.PassDecisions > 0 ? r.PassDistance / r.PassDecisions : 0.0, CarrierEntropy(r));
        }

        private static void AssertCarrierPlays(GameMode mode, int firstSeed)
        {
            AttackSim f11 = Aggregate(mode, true, firstSeed);
            Assert.AreEqual(0, f11.Issues, mode + ": invariants in all eight matches"); Assert.AreEqual(0, f11.MoreThanOnePerJob);
            Assert.Greater(f11.Passes, 20); Assert.Greater(f11.Kinds[(int)DecisionKind.Dribble], 0); Assert.Greater(f11.Kinds[(int)DecisionKind.Protect], 0); Assert.Greater(f11.Kinds[(int)DecisionKind.Hold], 0);
            Assert.Greater(f11.SpacePasses, 0, mode + ": passes into space happen"); Assert.Greater(f11.ShotOpportunities, 0); Assert.Greater(f11.Shots, 0);
            Assert.Greater(f11.ToJob[2], 0, "passes to runners"); Assert.Greater(f11.ToJob[1], 0); Assert.Greater(f11.ToJob[3], 0); Assert.Greater(f11.ToJob[4], 0);
            Assert.Greater(Enumerable.Range(1, 7).Count(j => f11.Chosen[j] > 0), 5, "the carrier does not always do the same thing");
            Assert.Less(f11.Kinds[(int)DecisionKind.Hold], f11.CarrierDecisions / 2, "holding is an instant, not the game");
            Assert.Less(f11.Kinds[(int)DecisionKind.Protect], f11.CarrierDecisions / 2, "protecting is not the answer to everything");
            AttackSim f10 = Aggregate(mode, false, firstSeed);
            Assert.AreEqual(0, f10.Issues); Assert.AreEqual(0, f10.Kinds[(int)DecisionKind.Hold], "the Phase 10 carrier never holds");
            TestContext.Progress.WriteLine("PHASE11 " + mode + " F10: " + Describe11(f10));
            TestContext.Progress.WriteLine("PHASE11 " + mode + " F11: " + Describe11(f11) + " chosen " + string.Join(" ", Enumerable.Range(1, 7).Select(j => (CandidateType)j + " " + f11.Chosen[j])));
        }

        [Test]
        public void EightRealMatches_WithTheIntelligentCarrier_TwoMinutes_Arena()
        {
            AssertCarrierPlays(GameMode.Arena, 301);
            AttackSim a = SimulateAttack(GameMode.Arena, 311, true), b = SimulateAttack(GameMode.Arena, 311, true);
            Assert.AreEqual(a.Fingerprint, b.Fingerprint, "the same decisions, passes into space, holds, shots, goals and counters");
            Assert.AreNotEqual(a.Fingerprint, SimulateAttack(GameMode.Arena, 312, true).Fingerprint);
            Assert.AreEqual(0, a.Fouls, "Arena has no fouls");
        }

        [Test]
        public void EightRealMatches_WithTheIntelligentCarrier_TwoMinutes_Classic()
        {
            AssertCarrierPlays(GameMode.Classic, 321);
            AttackSim a = SimulateAttack(GameMode.Classic, 331, true), b = SimulateAttack(GameMode.Classic, 331, true);
            Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreNotEqual(a.Fingerprint, SimulateAttack(GameMode.Classic, 332, true).Fingerprint);
        }

        [Test]
        public void TwelveRealPlayers_WithTheAttackWithoutTheBall_TwoMinutes_Arena()
        {
            AttackSim a = SimulateAttack(GameMode.Arena, 111, true);
            AssertAttacks(a, "Arena");
            Assert.AreEqual(0, a.Fouls, "Arena has no fouls");
            TestContext.Progress.WriteLine("PHASE10 Arena 111 P10: " + Describe(a) + " goals " + a.Goals);
            AttackSim b = SimulateAttack(GameMode.Arena, 111, true);
            Assert.AreEqual(a.Assignment, b.Assignment); Assert.AreEqual(a.Fingerprint, b.Fingerprint, "the same movements, decisions, passes, shots, goals and counters");
            Assert.AreNotEqual(a.Fingerprint, SimulateAttack(GameMode.Arena, 112, true).Fingerprint, "another seed, another match");
            AttackSim p9 = SimulateAttack(GameMode.Arena, 111, false);
            TestContext.Progress.WriteLine("PHASE10 Arena 111 P9 : " + Describe(p9) + " goals " + p9.Goals);
            Assert.AreEqual(0, p9.Issues); Assert.AreEqual(0, p9.Forward + p9.Width + p9.GetOpen, "with the layer off there are no Phase 10 decisions"); Assert.Greater(p9.Support, 0);
        }

        [Test]
        public void TwelveRealPlayers_WithTheAttackWithoutTheBall_TwoMinutes_Classic()
        {
            AttackSim a = SimulateAttack(GameMode.Classic, 121, true);
            AssertAttacks(a, "Classic");
            TestContext.Progress.WriteLine("PHASE10 Classic 121 P10: " + Describe(a) + " goals " + a.Goals);
            AttackSim b = SimulateAttack(GameMode.Classic, 121, true);
            Assert.AreEqual(a.Assignment, b.Assignment); Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreNotEqual(a.Fingerprint, SimulateAttack(GameMode.Classic, 122, true).Fingerprint);
            AttackSim p9 = SimulateAttack(GameMode.Classic, 121, false);
            TestContext.Progress.WriteLine("PHASE10 Classic 121 P9 : " + Describe(p9) + " goals " + p9.Goals);
            Assert.AreEqual(0, p9.Issues); Assert.AreEqual(0, p9.Forward + p9.Width + p9.GetOpen);
        }

        [Test]
        public void OtherRealTeams_AlsoKeepEveryInvariant_WithTheAttackLayer()
        {
            foreach (var pair in new[] { Tuple.Create(0, 11), Tuple.Create(5, 6), Tuple.Create(10, 2) })
                foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                {
                    AttackSim r = SimulateAttack(mode, 131, true, pair.Item1, pair.Item2, 20f);
                    Assert.AreEqual(0, r.Issues, mode + " " + pair.Item1 + "-" + pair.Item2); Assert.AreEqual(0, r.MoreThanOnePerJob); Assert.Greater(r.Decisions, 100);
                }
        }
    }
}
