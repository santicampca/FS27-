using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 10: twelve real players, real rosters and formations, roles, defence and the attack without the ball, for two minutes in each mode. Measured, not balanced; compared with the same match with the attack layer off (Phase 9).</summary>
    public partial class AiInfrastructureTests
    {
        private sealed class AttackSim
        {
            public string Fingerprint, Assignment;
            public int Issues, Possessions, Losses, Goals, Restarts, Decisions, Actions, Passes, Shots, Recoveries, Fouls;
            public int Support, Forward, Width, GetOpen;
            public int AttackSamples, PassDecisions; public long PassLanes; public double Spread, PassDistance;
            public int MoreThanOnePerJob, JobSamples, SupportersNearBall;
            public int[] ToJob = new int[5];
        }

        private static AttackSim SimulateAttack(GameMode mode, int seed, bool attackLayer, int homeTeam = 3, int awayTeam = 8, float seconds = 120f)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealRoles(homeTeam, awayTeam, seed, s);
            var r = new AttackSim();
            r.Assignment = string.Join(",", c.GetRoleAssignment(TeamSide.Home).FieldPlayerIds) + "|" + string.Join(",", c.GetRoleAssignment(TeamSide.Away).FieldPlayerIds);
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c, null, null, null, new AttackPositioningTuning { Enabled = attackLayer });
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
            r.Decisions = driver.Decisions; r.Actions = driver.ActionsAttempted; r.Passes = driver.PassesCompleted; r.Shots = driver.ShotsCompleted; r.Recoveries = driver.Recoveries; r.Fouls = driver.Fouls;
            r.Support = driver.CountOf(DecisionKind.Support); r.Forward = driver.CountOf(DecisionKind.ForwardRun); r.Width = driver.CountOf(DecisionKind.HoldWidth); r.GetOpen = driver.CountOf(DecisionKind.GetOpen);
            r.AttackSamples = driver.AttackSamples; r.PassLanes = driver.PassLaneSum; r.Spread = driver.TeammateSpreadSum; r.PassDecisions = driver.PassDecisions; r.PassDistance = driver.PassDistanceSum;
            for (int j = 0; j < 5; j++) r.ToJob[j] = driver.PassesToJob((AttackJob)j);
            r.Goals = c.State.Score.Home + c.State.Score.Away;
            r.Fingerprint = Fingerprint(c) + "|" + r.Decisions + "|" + r.Actions + "|" + r.Passes + "|" + r.Shots + "|" + r.Recoveries + "|" + r.Fouls + "|" + r.Possessions + "|" + r.Losses + "|" + r.Support + "|" + r.Forward + "|" + r.Width + "|" + r.GetOpen + "|" + r.PassLanes + "|" + r.Spread.ToString("R") + "|" + c.State.Score.Home + "-" + c.State.Score.Away;
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
