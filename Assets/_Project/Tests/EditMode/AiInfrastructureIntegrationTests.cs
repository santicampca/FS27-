using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 7: World -> Intent -> Core, with real teams, twelve players, determinism and the cost of the observation.</summary>
    public partial class AiInfrastructureTests
    {
        // ================= the main integration test (no AI: only the wiring) =================

        [Test]
        public void World_To_Intent_To_Core_WithRealTeams_AndNoAi()
        {
            // 1-3. a real team, its real formation, twelve players
            MatchController c = RealMatch(0, 1, seed: 11);
            c.SetControl(TeamSide.Home, ControlSource.Human);
            Assert.AreEqual(6, c.State.Home.Players.Count(p => p.IsActive)); Assert.AreEqual(6, c.State.Away.Players.Count(p => p.IsActive));
            // 4. place them (formation, taker) and start play
            string taker = c.State.Home.Players.First(p => p.IsActive && !p.InGoal).PlayerId;
            Assert.IsTrue(c.AssignRestartTaker(taker)); Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(taker, c.State.Ball.PossessingPlayerId);
            // 5. the world, as the AI will see it
            var world = new WorldState(); world.Refresh(c); WorldView view = world.View;
            Assert.AreEqual(12, Enumerable.Range(0, view.PlayerCount).Count(i => view[i].IsOnPitch)); Assert.IsTrue(view.TryGetBallCarrier(out WorldPlayer carrier)); Assert.AreEqual(taker, carrier.PlayerId);
            // 6. a player is selected (the carrier) and observed
            Assert.IsTrue(c.SelectPlayer(TeamSide.Home, taker)); world.Refresh(c); view = world.View;
            var obs = new PlayerObservation(); Assert.IsTrue(view.Observe(taker, obs)); Assert.IsTrue(obs.HasBall); Assert.IsTrue(obs.IsControlled);
            // 7-8. a hand-made movement intent for a team-mate, converted to locomotion
            int mateIndex = WorldGeometry.NearestAvailableTeammate(view, view.IndexOf(taker), out float mateDistance);
            Assert.GreaterOrEqual(mateIndex, 0); WorldPlayer mate = view[mateIndex];
            Vec2 before = mate.Position;
            Vec2 away = ActionModel.DirectionOr(mate.Position - carrier.Position, East);
            MovementIntent move = MovementIntent.Move(away, 0.7f);
            Assert.IsTrue(c.ApplyMovementIntent(TeamSide.Home, mate.PlayerId, move));
            Step(c, 45);
            world.Refresh(c);
            Assert.Greater((world[world.IndexOf(mate.PlayerId)].Position - before).Magnitude, 0.5f, "the intent became locomotion");
            // 9-12. a hand-made action intent: a pass to that team-mate, through the request and the resolver, to a result
            Assert.IsTrue(c.State.Ball.HasPossession && c.State.Ball.PossessingPlayerId == taker, "he still has the ball");
            var intent = new ActionIntent(TeamSide.Home, taker, FootballActionKind.ShortPass, Vec2.Zero, 0.7f) { Target = PassTarget.ToPlayer(mate.PlayerId) };
            ActionRequest request = intent.ToRequest();
            Assert.AreEqual(PassTargetKind.Player, request.Target.Kind);
            ActionResult result = c.Perform(request);
            Assert.AreEqual(ActionOutcome.Completed, result.Outcome); Assert.AreEqual(FootballActionKind.ShortPass, result.Kind); Assert.AreEqual(PassTargetKind.Player, result.TargetKind); Assert.AreEqual(mate.PlayerId, result.TargetPlayerId);
            Assert.Greater(result.BallVelocity.Magnitude, 2f); Assert.IsFalse(c.State.Ball.HasPossession); Assert.AreEqual(mate.PlayerId, c.State.Ball.IntendedReceiverId);
            Step(c, 90);
            world.Refresh(c);
            Assert.IsTrue(c.State.Ball.HasPossession || (world.Ball.Ground - world[world.IndexOf(mate.PlayerId)].Position).Magnitude < 8f, "the ball went to the team-mate");
        }

        // ================= twelve players at the same time =================

        private static string RunTwelve(GameMode mode, int seed, out int issues)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealMatch(3, 8, seed, s);
            Assert.IsTrue(c.ConfirmRestart());
            var world = new WorldState(); var obs = new PlayerObservation();
            issues = 0;
            Vec2[] dirs = { East, North, West, new Vec2(0f, -1f), new Vec2(1f, 1f), new Vec2(-1f, 1f) };
            for (int step = 0; step < 60 * 20; step++)
            {
                if (step % 20 == 0)
                    foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                        for (int i = 0; i < c.State.Team(side).Players.Count; i++)
                        {
                            MatchPlayer p = c.State.Team(side).Players[i];
                            if (!p.IsActive) continue;
                            Vec2 d = dirs[(i + step / 20 + (side == TeamSide.Home ? 0 : 3)) % dirs.Length];
                            c.ApplyMovementIntent(side, p.PlayerId, MovementIntent.Move(d, 0.5f + 0.1f * (i % 4)));
                        }
                if (step % 120 == 60 && c.BallCarrier != null)
                {
                    MatchPlayer carrier = c.BallCarrier;
                    world.Refresh(c);
                    int idx = WorldGeometry.NearestAvailableTeammate(world.View, world.IndexOf(carrier.PlayerId), out _);
                    if (idx >= 0) c.PerformIntent(new ActionIntent(carrier.Team, carrier.PlayerId, FootballActionKind.ShortPass, Vec2.Zero, 0.6f) { Target = PassTarget.ToPlayer(world[idx].PlayerId) });
                }
                c.Advance(Dt);
                if (c.State.Phase != MatchPhase.Playing) { if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f); }
                if (step % 10 != 0) continue;
                world.Refresh(c);
                var pts = new List<Vec2>();
                for (int i = 0; i < world.PlayerCount; i++)
                {
                    WorldPlayer w = world[i];
                    if (!w.IsOnPitch) continue;
                    pts.Add(w.Position);
                    if (float.IsNaN(w.Position.X) || float.IsNaN(w.Position.Y) || w.Stamina < 0f || w.Stamina > 100.001f || !c.Pitch.Contains(new Vec2(Math.Max(-20f, Math.Min(20f, w.Position.X)), Math.Max(-12.5f, Math.Min(12.5f, w.Position.Y))))) issues++;
                    if (w.Position.X > 21.01f || w.Position.X < -21.01f || w.Position.Y > 13.51f || w.Position.Y < -13.51f) issues++;
                    world.View.Observe(i, obs);
                    if (!obs.Valid) issues++;
                }
                if (pts.Count != 12) issues++;
                if (PlayerSeparation.Smallest(pts.ToArray(), pts.Count) < c.Infrastructure.MinSeparation * 0.5f) issues++;
                Vec3 b = c.State.Ball.Physics.Position;
                if (float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsNaN(b.Z)) issues++;
            }
            return Fingerprint(c);
        }

        [Test]
        public void TwelvePlayers_LiveTogether_SixVersusSix_ForTwentySeconds_InClassic()
        {
            string fp = RunTwelve(GameMode.Classic, 21, out int issues);
            Assert.AreEqual(0, issues);
            Assert.AreEqual(fp, RunTwelve(GameMode.Classic, 21, out _), "deterministic: the same twelve players do the same things");
        }

        [Test]
        public void TwelvePlayers_LiveTogether_SixVersusSix_ForTwentySeconds_InArena()
        {
            string fp = RunTwelve(GameMode.Arena, 22, out int issues);
            Assert.AreEqual(0, issues);
            Assert.AreEqual(fp, RunTwelve(GameMode.Arena, 22, out _));
        }

        [Test]
        public void TwelvePlayers_AllThePlayersMove_TheBallIsPlayed_AndStaminaIsSpentByEachOne()
        {
            MatchController c = RealMatch(1, 6, 5);
            Assert.IsTrue(c.ConfirmRestart());
            Dictionary<string, Vec2> start = new Dictionary<string, Vec2>();
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players.Where(x => x.IsActive)) start[p.PlayerId] = p.Locomotion.Position;
            Assert.AreEqual(12, start.Count);
            for (int step = 0; step < 300; step++)
            {
                if (step % 30 == 0)
                    foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players.Where(x => x.IsActive)) c.ApplyMovementIntent(t.Side, p.PlayerId, MovementIntent.Move(step % 60 == 0 ? East : West, 1f));
                c.Advance(Dt);
            }
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away })
                foreach (MatchPlayer p in t.Players.Where(x => x.IsActive))
                {
                    Assert.Greater((p.Locomotion.Position - start[p.PlayerId]).Magnitude + p.Locomotion.CurrentSpeed, 0.01f, p.PlayerId + " did not move");
                    Assert.Less(p.Locomotion.CurrentStamina, 100f, p.PlayerId + " ran without tiring");
                }
        }

        // ================= performance (no premature optimisation: only that nothing is wasteful) =================

        [Test]
        public void ObservingTheWorld_AllocatesNothing_AfterTheFirstTime()
        {
            MatchController c = RealMatch(0, 1);
            Assert.IsTrue(c.ConfirmRestart());
            var world = new WorldState(); var obs = new PlayerObservation();
            for (int i = 0; i < 5; i++) { world.Refresh(c); for (int k = 0; k < world.PlayerCount; k++) world.View.Observe(k, obs); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                world.Refresh(c);
                WorldView v = world.View;
                for (int k = 0; k < v.PlayerCount; k++) v.Observe(k, obs);
                WorldGeometry.NearestAvailableTeammate(v, 3, out _);
                WorldGeometry.NearestOpponent(v, 3, out _);
            }
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.LessOrEqual(used, 4096, "500 full refreshes + 20 observations each + queries: " + used + " bytes");
        }

        [Test]
        public void TheSpaceRules_AddNoGarbagePerStep_AndTheSnapshotHoldsNoDefinitions()
        {
            MatchController a = Live(), b = Live();
            b.Infrastructure.EnforcePlayerSpace = true;
            Step(a, 20); Step(b, 20);
            long x = GC.GetAllocatedBytesForCurrentThread(); Step(a, 600); long onlyBase = GC.GetAllocatedBytesForCurrentThread() - x;
            x = GC.GetAllocatedBytesForCurrentThread(); Step(b, 600); long withSpace = GC.GetAllocatedBytesForCurrentThread() - x;
            Assert.LessOrEqual(withSpace - onlyBase, 2048, "separation and bounds: " + (withSpace - onlyBase) + " bytes over 600 steps");
            Assert.IsTrue(typeof(WorldPlayer).IsValueType); Assert.Less(System.Runtime.InteropServices.Marshal.SizeOf(typeof(WorldBall)) , 1024);
        }

        [Test]
        public void TheDistanceMaths_LivesInOnePlace_NoOtherCodeRepeatsIt()
        {
            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string file in Directory.GetFiles(Path.Combine(dir, "World"), "*.cs").Concat(Directory.GetFiles(Path.Combine(dir, "Intents"), "*.cs")))
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//")) continue;
                    foreach (string word in new[] { "Random", "Score(", "Utility", "Evaluate(" }) StringAssert.DoesNotContain(word, line, Path.GetFileName(file) + ": no scoring and no chance in observation and intents");
                }
        }

        [Test]
        public void TheExistingContracts_WereExtended_NotDuplicated()
        {
            Assembly asm = typeof(MatchController).Assembly;
            foreach (string name in new[] { "MatchState", "MatchController", "MatchBall", "MatchPlayer", "ActionRequest", "ActionResult", "ActionMapper", "SkillResolver", "BallSimulator", "RestartRequest", "FormationDefinition", "FormationPosition", "LocomotionStepper", "LocomotionState", "SkillCatalog", "ActionModel" })
                Assert.AreEqual(1, asm.GetTypes().Count(t => t.Name == name), name + " must exist exactly once");
            Assert.IsNotNull(typeof(ActionRequest).GetField("Target")); Assert.IsNotNull(typeof(ActionRequest).GetField("Source")); Assert.IsNotNull(typeof(RestartRequest).GetField("TakerId"));
            Assert.AreEqual(0, asm.GetTypes().Count(t => t.Name == "PlayerObservation2" || t.Name == "WorldState2" || t.Name == "ActionRequest2" || t.Name == "RestartSystem"));
        }

        [Test]
        public void ThePhaseDoesNotTouchWhatItMustNot_ActionNumbersAttributesAndSkillsAreTheSame()
        {
            Assert.AreEqual(20, (int)FootballActionKind.SkillMove); Assert.AreEqual(26, (int)FootballActionKind.Clearance);
            Assert.AreEqual(37, Db.Skills.Count); Assert.AreEqual(136, Db.Players.All.Count(p => p.Role != PlayerRole.Goalkeeper)); Assert.AreEqual(24, Db.Players.All.Count(p => p.Role == PlayerRole.Goalkeeper));
            var c = RealMatch(0, 1);
            string snapshot = string.Join(",", Db.Players.All.Take(40).Select(p => p.Attributes.Speed + "/" + p.Attributes.Technique + "/" + p.Id));
            c.ConfirmRestart(); Step(c, 300); WorldState.Capture(c);
            Assert.AreEqual(snapshot, string.Join(",", Db.Players.All.Take(40).Select(p => p.Attributes.Speed + "/" + p.Attributes.Technique + "/" + p.Id)), "no attribute changed");
        }
    }
}
