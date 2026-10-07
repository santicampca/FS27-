using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 8: the rate of decisions, the controlled player, determinism, the separation between deciding and doing, and twelve real players deciding for themselves.</summary>
    public partial class AiInfrastructureTests
    {
        private static string DecisionDir { get { return Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Decision"); } }

        // ================= the rate of decisions =================

        [Test]
        public void ThePlayersDecideAtTheConfiguredRate_StaggeredNotAllInTheSameStep()
        {
            MatchController c = Quiet();
            var driver = new BasicDecisionDriver(c);
            int steps = driver.DecisionSteps;
            Assert.AreEqual((int)Math.Round(BasicDecisionDefaults.DecisionIntervalSeconds / Dt), steps); Assert.Greater(steps, 1);
            var perStep = new List<int>();
            for (int i = 0; i < steps * 10; i++) { int before = driver.Decisions; driver.Step(c); perStep.Add(driver.Decisions - before); c.Advance(Dt); }
            Assert.AreEqual(12 * 10, driver.Decisions, "twelve players on the pitch, ten decisions each in ten intervals");
            Assert.Less(perStep.Max(), 12, "nobody decides all at once"); Assert.LessOrEqual(perStep.Max(), 3, "a few at a time: twelve players spread over " + steps + " steps");
        }

        [Test]
        public void BetweenTwoDecisions_ThePlayerKeepsDoingWhatHeDecided()
        {
            MatchController c = Quiet();
            PutBall(c, 10f, 0f);
            Put(c, TeamSide.Home, "hs3", 0f, 0f, East);
            var driver = new BasicDecisionDriver(c, new BasicDecisionTuning { DecisionIntervalSeconds = 1f });
            for (int i = 0; i < driver.DecisionSteps; i++) { driver.Step(c); c.Advance(Dt); }
            Assert.IsTrue(driver.TryGetLast("hs3", out PlayerDecision d)); Assert.AreEqual(DecisionKind.MoveToBall, d.Kind);
            float x0 = PosOf(c, "hs3", TeamSide.Home).X;
            for (int i = 0; i < 20; i++) { driver.Step(c); c.Advance(Dt); }
            Assert.AreEqual(60, driver.DecisionSteps);
            Assert.Greater(PosOf(c, "hs3", TeamSide.Home).X, x0 + 0.1f, "he went on running towards the ball without deciding again");
            Assert.AreEqual(1, new BasicDecisionDriver(c, new BasicDecisionTuning { DecisionIntervalSeconds = 0.001f }).DecisionSteps, "never less than one step");
        }

        [Test]
        public void ThePlayerARealPersonControls_IsNeverDriven_TheRestOfHisTeamIs()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs3", 0f, 0f, East);
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SelectPlayer(TeamSide.Home, "hs3");
            var driver = new BasicDecisionDriver(c);
            for (int i = 0; i < 60; i++) { driver.Step(c); c.Advance(Dt); }
            Assert.AreEqual("hs3", c.StoredSelection(TeamSide.Home), "attacking: he stays the controlled player");
            Assert.IsFalse(driver.TryGetLast("hs3", out _), "the player a person controls received no decision");
            Assert.IsTrue(driver.TryGetLast("hs4", out _)); Assert.IsTrue(driver.TryGetLast("as3", out _), "the other side is still driven");
        }

        // ================= the controlled player =================

        [Test]
        public void AutoSelection_WithoutTheBall_TheNearestFieldPlayer_WithTheBall_TheOneAlreadyControlled()
        {
            MatchController c = Quiet();
            PutBall(c, 0f, 0f);
            Put(c, TeamSide.Home, "hs3", 5f, 0f, East); Put(c, TeamSide.Home, "hs4", -3f, 1f, East); Put(c, TeamSide.Home, "hs0", -1f, 0f, East);
            WorldView v = WorldState.Capture(c).View;
            Assert.AreEqual("hs4", AutoSelection.Choose(v, TeamSide.Home, "hs3"), "free ball: the nearest field player (the keeper does not count)");
            Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
            Assert.AreEqual("hs4", AutoSelection.Choose(WorldState.Capture(c).View, TeamSide.Home, "hs3"), "defending: the nearest to the ball");
            Carrier(c, TeamSide.Home, "hs3", 0f, 0f, East);
            Assert.AreEqual("hs5", AutoSelection.Choose(WorldState.Capture(c).View, TeamSide.Home, "hs5"), "attacking: the controlled player stays");
            Assert.AreEqual("hs3", AutoSelection.Choose(WorldState.Capture(c).View, TeamSide.Home, "hb1"), "...unless he is not on the pitch: then the nearest, here the carrier");
            Assert.AreEqual("hs3", AutoSelection.Choose(WorldState.Capture(c).View, TeamSide.Home, null));
        }

        [Test]
        public void TheDriverKeepsAHumanSideSelectionUpToDate_AndTheFallbackCoversWhoLeavesThePitch()
        {
            MatchController c = Quiet();
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SelectPlayer(TeamSide.Home, "hs5");
            PutBall(c, 0f, 0f); Put(c, TeamSide.Home, "hs3", 2f, 0f, East);
            var driver = new BasicDecisionDriver(c);
            for (int i = 0; i < driver.DecisionSteps + 1; i++) { driver.Step(c); c.Advance(Dt); }
            Assert.AreEqual("hs3", c.StoredSelection(TeamSide.Home), "the loose ball is nearest to hs3");
            Carrier(c, TeamSide.Home, "hs4", 0f, 0f, East); c.SelectPlayer(TeamSide.Home, "hs4");
            for (int i = 0; i < driver.DecisionSteps + 1; i++) { driver.Step(c); c.Advance(Dt); }
            Assert.AreEqual("hs4", c.StoredSelection(TeamSide.Home), "attacking: kept");
            c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            Assert.IsTrue(c.State.Home.Find(c.ControlledPlayerId(TeamSide.Home)).IsActive, "the existing fallback takes over when he is sent off");
        }

        // ================= determinism =================

        [Test]
        public void TheSameWorldAndTheSamePlayer_GiveTheSameDecision_EveryTime_AndOnAnotherEngine()
        {
            MatchController c = WithBallAt(-3f, 0f, East);
            Put(c, TeamSide.Home, "hs3", 6f, 4f, East); Put(c, TeamSide.Away, "as3", 7f, -1f, West);
            WorldView v = WorldState.Capture(c).View;
            var a = new BasicDecisionEngine(c.Pitch); var b = new BasicDecisionEngine(c.Pitch);
            foreach (string id in new[] { "hs0", "hs1", "hs2", "hs3", "hs4", "hs5", "as0", "as1", "as2", "as3", "as4", "as5" })
            {
                PlayerDecision x = a.Decide(v, id), y = a.Decide(v, id), z = b.Decide(v, id);
                foreach (PlayerDecision o in new[] { y, z }) { Assert.AreEqual(x.Kind, o.Kind, id); Assert.AreEqual(x.Reason, o.Reason, id); Assert.AreEqual(x.TargetPlayerId, o.TargetPlayerId, id); Assert.AreEqual(x.HasAction, o.HasAction); Assert.AreEqual(x.Movement.Direction, o.Movement.Direction, id); Assert.AreEqual(x.Movement.Magnitude, o.Movement.Magnitude, id); if (x.HasAction) { Assert.AreEqual(x.Action.Kind, o.Action.Kind); Assert.AreEqual(x.Action.Direction, o.Action.Direction); Assert.AreEqual(x.Action.Power, o.Action.Power); } }
            }
        }

        // ================= the engine only decides: it changes nothing, reads nothing it should not =================

        [Test]
        public void DecidingChangesNothing_NotThePositionsNorTheStaminaNorTheBallNorThePossession()
        {
            MatchController c = WithBallAt(14f, 0f, East);
            string before = Fingerprint(c);
            var engine = new BasicDecisionEngine(c.Pitch);
            for (int round = 0; round < 20; round++)
            {
                WorldView v = WorldState.Capture(c).View;
                foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players) engine.Decide(v, p.PlayerId);
            }
            Assert.AreEqual(before, Fingerprint(c)); Assert.AreEqual("hs2", c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void TheEngineAndTheDriver_NeverTouchADefinitionAnAttributeTheMatchStateOrTheBall_ByConstruction()
        {
            foreach (Type t in new[] { typeof(BasicDecisionEngine), typeof(BasicDecisionDriver) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    foreach (Type forbidden in new[] { typeof(PlayerDefinition), typeof(PlayerAttributes), typeof(PlayerPlayingProfile), typeof(MatchState), typeof(MatchBall), typeof(MatchPlayer), typeof(BallSimulator) })
                        Assert.AreNotEqual(forbidden, f.FieldType, t.Name + "." + f.Name);
            string engine = File.ReadAllText(Path.Combine(DecisionDir, "BasicDecisionEngine.cs"));
            foreach (string banned in new[] { "PlayerDefinition", "PlayerAttributes", "PlayerPlayingProfile", "MatchState", "MatchController", "MatchBall", "BallSimulator", "ActionResolver", "Overall", "Random", "DateTime", "UnityEngine", "Time." })
                StringAssert.DoesNotContain(banned, engine, "the engine reads only the view: " + banned);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(engine, @"\.(Shooting|Finishing|Technique|Passing|Dribbling|Defense|Acceleration|Agility|Strength|Control)\b"), "no attribute is read: skills are asked through the view");
            foreach (string file in Directory.GetFiles(DecisionDir, "*.cs"))
            {
                string text = File.ReadAllText(file), name = Path.GetFileName(file);
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\.(Position|CurrentStamina|Velocity|CurrentSpeed|Locomotion|Physics|PossessingPlayerId|HasPossession)\s*=[^=]"), name + " writes state");
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "MonoBehaviour", "Time.deltaTime" }) StringAssert.DoesNotContain(banned, text, name);
            }
        }

        [Test]
        public void TheDriverActsOnlyThroughTheCoresDoors_ApplyMovementIntent_AndPerformIntent()
        {
            string driver = File.ReadAllText(Path.Combine(DecisionDir, "BasicDecisionDriver.cs"));
            StringAssert.Contains("match.ApplyMovementIntent(", driver); StringAssert.Contains("match.PerformIntent(", driver);
            foreach (string banned in new[] { "SetPlayerInput", "PlacePlayer", "GiveBallTo", "ReleaseBall", "ApplyBallImpulse", "SetBallMode", ".Perform(" }) StringAssert.DoesNotContain(banned, driver, "the driver must not use " + banned);
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs2", 14f, 0f, East);
            var results = new List<ActionResult>(); c.ActionPerformed += r => results.Add(r);
            var d = new BasicDecisionDriver(c);
            for (int i = 0; i < 40; i++) { d.Step(c); c.Advance(Dt); }
            Assert.AreEqual(d.ActionsAttempted, results.Count); Assert.IsTrue(results.All(r => r.Source == ControlSource.Ai)); Assert.Greater(results.Count, 0);
        }

        [Test]
        public void TheDecisionNumbers_LiveInOnePlace_NoLooseNumbersInTheEngine()
        {
            var t = new BasicDecisionTuning();
            foreach (FieldInfo f in typeof(BasicDecisionDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                FieldInfo mirror = typeof(BasicDecisionTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(t)), f.Name); Assert.Greater(Convert.ToSingle(f.GetValue(null)), 0f, f.Name);
            }
            foreach (FieldInfo f in typeof(BasicDecisionTuning).GetFields()) Assert.IsNotNull(typeof(BasicDecisionDefaults).GetField(f.Name), f.Name + " has no default");
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string name in new[] { "BasicDecisionEngine.cs", "BasicDecisionDriver.cs", "AutoSelection.cs" })
                foreach (string raw in File.ReadAllLines(Path.Combine(DecisionDir, name)))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(name + ": loose number " + m.Value + " in: " + line);
                }
            Assert.Less(BasicDecisionDefaults.PressureRadius, BasicDecisionDefaults.DribbleClearDistance); Assert.Less(BasicDecisionDefaults.PassMinDistance, BasicDecisionDefaults.PassMaxDistance); Assert.Less(BasicDecisionDefaults.SupportMinDistance, BasicDecisionDefaults.SupportMaxDistance);
        }

        [Test]
        public void TheDecisionKinds_AreAShortClosedList_AndTheReasonIsOnlyForDebugging()
        {
            CollectionAssert.AreEqual(new[] { "Idle", "MoveToPoint", "MoveToBall", "Support", "Pass", "Shoot", "Dribble", "Protect", "CloseDown", "Tackle", "SlidingTackle", "Challenge", "Intercept" }, Enum.GetNames(typeof(DecisionKind)), "Phase 8's eight, then Phase 9's five appended");
            Assert.LessOrEqual(Enum.GetValues(typeof(DecisionKind)).Length, 16);
            foreach (string file in Directory.GetFiles(DecisionDir, "*.cs"))
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), @"(if|while|switch)\s*\([^)]*\.Reason"), Path.GetFileName(file) + " must not branch on the reason");
        }

        // ================= twelve real players decide for themselves (thirty seconds, Arena and Classic) =================

        private sealed class SimResult
        {
            public string Fingerprint; public int Decisions, Attempted, Completed, Passes, Shots, Restarts, Issues, Goals;
            public int[] Kinds = new int[8];
            public int PlayersDecided, PlayersMoved, FieldPlayersMoved;
            public float MaxStamina = 0f, MinStamina = 100f;
        }

        private static SimResult Simulate(GameMode mode, int seed, int homeTeam = 3, int awayTeam = 8, float seconds = 30f)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealMatch(homeTeam, awayTeam, seed, s);
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c);
            var r = new SimResult();
            var start = new Dictionary<string, Vec2>(); var moved = new HashSet<string>(); var keepers = new HashSet<string>();
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players.Where(x => x.IsActive)) { start[p.PlayerId] = p.Locomotion.Position; if (p.InGoal) keepers.Add(p.PlayerId); }
            var world = new WorldState();
            int steps = (int)(seconds * 60f);
            for (int step = 0; step < steps; step++)
            {
                driver.Step(c);
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) { if (c.ConfirmRestart()) r.Restarts++; else r.Issues++; }
                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                if (step % 6 != 0) continue;
                world.Refresh(c);
                int onPitch = 0;
                for (int i = 0; i < world.PlayerCount; i++)
                {
                    WorldPlayer w = world[i];
                    if (!w.IsOnPitch) continue;
                    onPitch++;
                    if (float.IsNaN(w.Position.X) || float.IsNaN(w.Position.Y) || float.IsInfinity(w.Position.X) || float.IsNaN(w.Stamina)) r.Issues++;
                    if (Math.Abs(w.Position.X) > 21.01f || Math.Abs(w.Position.Y) > 13.51f) r.Issues++;
                    if (w.Stamina < 0f || w.Stamina > 100.001f) r.Issues++;
                    r.MinStamina = Math.Min(r.MinStamina, w.Stamina); r.MaxStamina = Math.Max(r.MaxStamina, w.Stamina);
                    if ((w.Position - start[w.PlayerId]).Magnitude > 0.5f) moved.Add(w.PlayerId);
                }
                if (onPitch != 12) r.Issues++;
                Vec3 b = c.State.Ball.Physics.Position;
                if (float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsNaN(b.Z) || Math.Abs(b.X) > 60f || Math.Abs(b.Z) > 40f) r.Issues++;
                if (mode == GameMode.Arena && (Math.Abs(b.X) > 20.6f || Math.Abs(b.Z) > 13.1f) && c.State.Phase == MatchPhase.Playing) r.Issues++;
            }
            r.Decisions = driver.Decisions; r.Attempted = driver.ActionsAttempted; r.Completed = driver.ActionsCompleted; r.Passes = driver.PassesCompleted; r.Shots = driver.ShotsCompleted;
            for (int k = 0; k < 8; k++) r.Kinds[k] = driver.CountOf((DecisionKind)k);
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players.Where(x => x.IsActive)) if (driver.TryGetLast(p.PlayerId, out _)) r.PlayersDecided++;
            r.PlayersMoved = moved.Count; r.FieldPlayersMoved = moved.Count(id => !keepers.Contains(id));
            r.Goals = c.State.Score.Home + c.State.Score.Away;
            r.Fingerprint = Fingerprint(c) + "|" + r.Decisions + "|" + r.Attempted + "|" + r.Completed + "|" + r.Passes + "|" + r.Shots + "|" + string.Join(",", r.Kinds) + "|" + c.State.Score.Home + "-" + c.State.Score.Away;
            return r;
        }

        private static void AssertAutonomous(SimResult r, string label)
        {
            Assert.AreEqual(0, r.Issues, label + ": invariants (NaN, out of the pitch, stamina, twelve on the pitch)");
            Assert.AreEqual(12, r.PlayersDecided, label + ": every one of the twelve received decisions");
            Assert.Greater(r.Decisions, 12 * 30 * 5, label + ": at least five decisions per player per second");
            Assert.GreaterOrEqual(r.FieldPlayersMoved, 8, label + ": the field players move");
            Assert.Greater(r.Kinds[(int)DecisionKind.MoveToBall] + r.Kinds[(int)DecisionKind.MoveToPoint] + r.Kinds[(int)DecisionKind.Support], 0);
            Assert.Greater(r.Kinds[(int)DecisionKind.Dribble] + r.Kinds[(int)DecisionKind.Pass] + r.Kinds[(int)DecisionKind.Shoot] + r.Kinds[(int)DecisionKind.Protect], 0, label + ": decisions with the ball");
            Assert.Greater(r.Kinds[(int)DecisionKind.MoveToBall], 0); Assert.Greater(r.Kinds[(int)DecisionKind.Support], 0);
            Assert.GreaterOrEqual(r.Passes, 3, label + ": some passes"); Assert.Greater(r.Completed, 0, label + ": some actions went through the Core");
            Assert.GreaterOrEqual(r.MinStamina, 0f); Assert.LessOrEqual(r.MaxStamina, 100.001f);
            Assert.Less(r.MinStamina, 100f, label + ": they ran, they tired");
        }

        [Test]
        public void TwelveRealPlayers_DecideForThemselves_ThirtySeconds_Classic()
        {
            SimResult a = Simulate(GameMode.Classic, 31);
            AssertAutonomous(a, "Classic");
            Assert.AreEqual(a.Fingerprint, Simulate(GameMode.Classic, 31).Fingerprint, "the same seed gives the same match");
            Assert.AreNotEqual(a.Fingerprint, Simulate(GameMode.Classic, 32).Fingerprint, "another seed gives another one (the kicks have their error)");
        }

        [Test]
        public void TwelveRealPlayers_DecideForThemselves_ThirtySeconds_Arena()
        {
            SimResult a = Simulate(GameMode.Arena, 33);
            AssertAutonomous(a, "Arena");
            Assert.AreEqual(a.Fingerprint, Simulate(GameMode.Arena, 33).Fingerprint, "the same seed gives the same match");
        }

        [Test]
        public void TwelveRealPlayers_OtherRealTeams_AlsoKeepEveryInvariant()
        {
            foreach (var pair in new[] { Tuple.Create(0, 11), Tuple.Create(5, 6) })
                foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                {
                    SimResult r = Simulate(mode, 41, pair.Item1, pair.Item2, 15f);
                    Assert.AreEqual(0, r.Issues, mode + " " + pair.Item1 + "-" + pair.Item2); Assert.AreEqual(12, r.PlayersDecided); Assert.Greater(r.Completed, 0);
                }
        }

        [Test]
        public void ADecidedMatch_BecomesMatchStateOnlyThroughTheCore_TheBallIsAlwaysTheCoresBall()
        {
            MatchController c = RealMatch(3, 8, 51);
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c);
            Vec3 last = c.State.Ball.Physics.Position;
            for (int step = 0; step < 600; step++)
            {
                Vec3 beforeStep = c.State.Ball.Physics.Position;
                driver.Step(c);
                Assert.AreEqual(beforeStep, c.State.Ball.Physics.Position, "deciding and applying intents never moves the ball by itself: only a resolved action or the simulator does");
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
            }
        }
    }
}
