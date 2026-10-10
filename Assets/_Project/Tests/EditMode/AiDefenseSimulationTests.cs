using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 9: twelve real players, real rosters and formations, the role assignment, decisions and defence, for sixty seconds in each mode. Measured, not balanced.</summary>
    public partial class AiInfrastructureTests
    {
        private sealed class DefenseSim
        {
            public string Fingerprint, Assignment;
            public int Issues, Possessions, Losses, Goals, Restarts;
            public int Decisions, Actions, Passes, Shots, Tackles, Slides, Challenges, Intercepts, Recoveries, Fouls;
            public double PrimaryDistance;
            public int MoreThanTwoDefenders, DefenceSamples;
            public int HomeValidSamples;
        }

        private static DefenseSim SimulateDefense(GameMode mode, int seed, int homeTeam = 3, int awayTeam = 8, float seconds = 60f)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealRoles(homeTeam, awayTeam, seed, s);
            var r = new DefenseSim();
            r.Assignment = string.Join(",", c.GetRoleAssignment(TeamSide.Home).FieldPlayerIds) + "|" + string.Join(",", c.GetRoleAssignment(TeamSide.Away).FieldPlayerIds);
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c);
            var world = new WorldState(); var engine = new BasicDecisionEngine(c.Pitch);
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
                    if (!w.HasHome || float.IsNaN(w.Home.X) || float.IsNaN(w.Home.Y) || Math.Abs(w.Home.X) > 21.01f || Math.Abs(w.Home.Y) > 13.51f) r.Issues++; else r.HomeValidSamples++;
                }
                // Phase 21: a player the referee sent off does not come back and is not replaced
                int sentOffDefence = 0;
                foreach (TeamSide ts0 in new[] { TeamSide.Home, TeamSide.Away }) foreach (MatchPlayer sp in c.State.Team(ts0).Players) if (sp.Status == MatchPlayerStatus.Dismissed) sentOffDefence++;
                if (onPitch != 12 - sentOffDefence) r.Issues++;
                if (keepers != 2) r.Issues++;
                for (int side = 0; side < 2; side++)
                {
                    TeamSide ts = side == 0 ? TeamSide.Home : TeamSide.Away;
                    if (Enumerable.Range(0, world.PlayerCount).Count(i => world[i].Team == ts && world[i].IsOnPitch && world[i].IsGoalkeeper) != 1) r.Issues++;
                    RoleAssignment a = c.GetRoleAssignment(ts);
                    if (a.FieldPlayerIds.Distinct().Count() != 5 || a.FieldPlayerIds.Contains(a.GoalkeeperId)) r.Issues++;
                }
                if (PlayerSeparation.Smallest(points.ToArray(), points.Count) < c.Infrastructure.MinSeparation * 0.5f) r.Issues++;
                Vec3 b = c.State.Ball.Physics.Position;
                if (float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsNaN(b.Z) || Math.Abs(b.X) > 60f || Math.Abs(b.Z) > 40f) r.Issues++;
                if (mode == GameMode.Arena && c.State.Phase == MatchPhase.Playing && (Math.Abs(b.X) > 20.6f || Math.Abs(b.Z) > 13.1f)) r.Issues++;
                if (c.State.Ball.HasPossession)
                {
                    MatchPlayer holder = c.State.Team(c.State.Ball.PossessingTeam).Find(c.State.Ball.PossessingPlayerId);
                    if (holder == null || !holder.IsActive) r.Issues++;
                    else
                    {
                        // the defence of the team without the ball: at most a primary and a secondary
                        TeamSide defending = c.State.Ball.PossessingTeam.Opponent();
                        var ids = c.State.Team(defending).Players.Where(p => p.IsActive && !p.InGoal).Select(p => p.PlayerId).ToList();
                        WorldView v = world.View;
                        int closers = ids.Count(id => IsDefensive(engine.Decide(v, id).Kind));
                        r.DefenceSamples++; if (closers > 2) r.MoreThanTwoDefenders++;
                    }
                }
            }
            r.Decisions = driver.Decisions; r.Actions = driver.ActionsAttempted; r.Passes = driver.PassesCompleted; r.Shots = driver.ShotsCompleted;
            r.Tackles = driver.TackleAttempts; r.Slides = driver.SlideAttempts; r.Challenges = driver.ChallengeAttempts; r.Intercepts = driver.InterceptAttempts; r.Recoveries = driver.Recoveries; r.Fouls = driver.Fouls;
            r.PrimaryDistance = driver.PrimaryDefenderSamples > 0 ? driver.PrimaryDefenderDistanceSum / driver.PrimaryDefenderSamples : 0.0;
            r.Goals = c.State.Score.Home + c.State.Score.Away;
            r.Fingerprint = Fingerprint(c) + "|" + r.Decisions + "|" + r.Actions + "|" + r.Passes + "|" + r.Shots + "|" + r.Tackles + "|" + r.Slides + "|" + r.Challenges + "|" + r.Intercepts + "|" + r.Recoveries + "|" + r.Fouls + "|" + r.Possessions + "|" + r.Losses + "|" + c.State.Score.Home + "-" + c.State.Score.Away + "|" + r.PrimaryDistance.ToString("R");
            return r;
        }

        private static void AssertDefends(DefenseSim r, string label)
        {
            Assert.AreEqual(0, r.Issues, label + ": invariants (twelve players, one keeper each, finite, in the pitch, stamina, apart, homes, possession, no slot twice)");
            Assert.AreEqual(0, r.MoreThanTwoDefenders, label + ": never more than a primary and a secondary defender"); Assert.Greater(r.DefenceSamples, 20, label);
            Assert.Greater(r.HomeValidSamples, 0);
            Assert.Greater(r.Possessions, 2, label); Assert.Greater(r.Losses, 0, label + ": the ball changed hands");
            Assert.Greater(r.Tackles + r.Challenges + r.Intercepts, 0, label + ": there were defensive actions");
            Assert.Greater(r.Recoveries, 0, label + ": the defence won the ball back");
            Assert.Greater(r.Passes, 5, label); Assert.Greater(r.PrimaryDistance, 0.0);
            Assert.Less(r.Slides, r.Tackles + r.Challenges + r.Intercepts, label + ": the sliding tackle is the exception");
        }

        [Test]
        public void TwelveRealPlayers_WithRolesAndDefence_SixtySeconds_Classic()
        {
            DefenseSim a = SimulateDefense(GameMode.Classic, 61);
            AssertDefends(a, "Classic");
            DefenseSim b = SimulateDefense(GameMode.Classic, 61);
            Assert.AreEqual(a.Assignment, b.Assignment, "the same assignment"); Assert.AreEqual(a.Fingerprint, b.Fingerprint, "the same decisions, tackles, passes, goals and counters");
            Assert.AreNotEqual(a.Fingerprint, SimulateDefense(GameMode.Classic, 62).Fingerprint, "another seed, another match");
        }

        [Test]
        public void TwelveRealPlayers_WithRolesAndDefence_SixtySeconds_Arena()
        {
            DefenseSim a = SimulateDefense(GameMode.Arena, 63);
            AssertDefends(a, "Arena");
            Assert.AreEqual(0, a.Fouls, "Arena has no fouls");
            DefenseSim b = SimulateDefense(GameMode.Arena, 63);
            Assert.AreEqual(a.Assignment, b.Assignment); Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreNotEqual(a.Fingerprint, SimulateDefense(GameMode.Arena, 64).Fingerprint);
        }

        [Test]
        public void TheAssignmentOfASimulationDoesNotDependOnTheMatchSeed_OnlyOnTheRosterAndTheFormation()
        {
            Assert.AreEqual(SimulateDefense(GameMode.Classic, 71, 3, 8, 1f).Assignment, SimulateDefense(GameMode.Arena, 72, 3, 8, 1f).Assignment);
        }

        [Test]
        public void OtherRealTeams_AlsoKeepEveryInvariant_WithTheDefence()
        {
            foreach (var pair in new[] { Tuple.Create(0, 11), Tuple.Create(5, 6), Tuple.Create(10, 2) })
                foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                {
                    DefenseSim r = SimulateDefense(mode, 81, pair.Item1, pair.Item2, 20f);
                    Assert.AreEqual(0, r.Issues, mode + " " + pair.Item1 + "-" + pair.Item2); Assert.AreEqual(0, r.MoreThanTwoDefenders);
                }
        }
    }
}
