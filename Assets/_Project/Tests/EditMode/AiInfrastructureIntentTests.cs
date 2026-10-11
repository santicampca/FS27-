using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 7: pass target, movement and action intents, normalised input, human and AI control, selection, the goal slot.</summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController Passing(int seed = 1)
        {
            MatchController c = Live(seed: seed);
            foreach (string id in new[] { "hs2", "hs3" }) c.State.Home.Find(id).Locomotion.CurrentStamina = 100f;
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Put(c, TeamSide.Home, "hs3", 0f, 10f, East);
            return c;
        }

        private static float OffBy(Vec2 wanted, Vec2 got) { return Math.Abs(ActionModel.SignedAngle(wanted.Normalized, got.Normalized)); }

        // ================= pass target =================

        [Test]
        public void PassTarget_None_IsThePhase4Behaviour_TheDirectionDecides()
        {
            MatchController c = Passing();
            ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.8f));
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(PassTargetKind.None, r.TargetKind); Assert.IsNull(r.TargetPlayerId);
            Assert.Less(OffBy(East, r.Direction), 8f); Assert.IsNull(c.State.Ball.IntendedReceiverId);
            Assert.IsTrue(PassTarget.None.IsNone); Assert.IsFalse(PassTarget.ToPlayer("x").IsNone);
        }

        [Test]
        public void PassTarget_APlayer_TurnsTheKickTowardsHim_TheDirectionOfTheRequestIsOverridden()
        {
            MatchController c = Passing();
            var req = new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, West, 0.8f) { Target = PassTarget.ToPlayer("hs3") };
            ActionResult r = c.Perform(req);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(PassTargetKind.Player, r.TargetKind); Assert.AreEqual("hs3", r.TargetPlayerId);
            Assert.Less(OffBy(North, r.Direction), 8f, "towards hs3, not west"); Assert.Greater(r.BallVelocity.Z, 2f);
            Assert.AreEqual("hs3", c.State.Ball.IntendedReceiverId); Assert.AreEqual("hs3", WorldState.Capture(c).Ball.IntendedReceiverId);
            c.RegisterTouch(TeamSide.Away, "as1");
            Assert.IsNull(c.State.Ball.IntendedReceiverId, "any touch ends the pass");
        }

        [Test]
        public void PassTarget_APosition_TurnsTheKickTowardsThatPoint()
        {
            MatchController c = Passing();
            ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, West, 0.8f) { Target = PassTarget.ToPosition(new Vec2(10f, -10f)) });
            Assert.AreEqual(PassTargetKind.Position, r.TargetKind); Assert.IsNull(r.TargetPlayerId);
            Assert.Less(OffBy(new Vec2(1f, -1f), r.Direction), 8f); Assert.IsNull(c.State.Ball.IntendedReceiverId, "a point is not a receiver");
        }

        [Test]
        public void PassTarget_UnknownPlayers_FallBackToTheDirection_AndSayUnknown()
        {
            foreach (string id in new[] { "nobody", "as1", "hb1", "hs2", null })
            {
                MatchController c = Passing();
                ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.8f) { Target = PassTarget.ToPlayer(id) });
                Assert.AreEqual(ActionOutcome.Completed, r.Outcome, id); Assert.AreEqual(PassTargetKind.Unknown, r.TargetKind, id); Assert.IsNull(r.TargetPlayerId); Assert.Less(OffBy(East, r.Direction), 8f, id);
                Assert.IsNull(c.State.Ball.IntendedReceiverId);
            }
            MatchController d = Passing();
            ActionResult bad = d.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.8f) { Target = PassTarget.ToPosition(new Vec2(float.NaN, 0f)) });
            Assert.AreEqual(PassTargetKind.Unknown, bad.TargetKind);
        }

        [Test]
        public void PassTarget_OnlyKicksUseIt_ATackleIgnoresIt()
        {
            MatchController c = Passing();
            Put(c, TeamSide.Away, "as2", 5f, 5f, West);
            ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.Pressure, East) { Target = PassTarget.ToPlayer("hs2") });
            Assert.AreEqual(PassTargetKind.None, r.TargetKind);
        }

        [Test]
        public void PassAToB_IsDeterministic_AndTheBallReachesTheReceiver()
        {
            ActionResult Run(int seed, out float distanceToB)
            {
                MatchController c = Passing(seed);
                ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.7f) { Target = PassTarget.ToPlayer("hs3") });
                Step(c, 60);
                Vec3 b = c.State.Ball.Physics.Position; Vec2 pb = c.State.Home.Find("hs3").Locomotion.Position;
                distanceToB = (new Vec2(b.X, b.Z) - pb).Magnitude;
                return r;
            }
            ActionResult a = Run(5, out float da), b = Run(5, out float db), other = Run(6, out _);
            Assert.AreEqual(a.BallVelocity, b.BallVelocity); Assert.AreEqual(da, db); Assert.AreNotEqual(a.BallVelocity, other.BallVelocity);
            Assert.Less(da, 4f, "the ball ends up with the receiver");
        }

        // ================= movement intent =================

        [Test]
        public void MovementIntent_BecomesAStick_DirectionTimesMagnitude_NeverLongerThanOne()
        {
            Assert.AreEqual(new Vec2(0.5f, 0f), MovementIntent.Move(new Vec2(7f, 0f), 0.5f).ToLocomotion().Stick);
            Assert.AreEqual(1f, MovementIntent.Move(new Vec2(1f, 1f), 3f).ToLocomotion().Stick.Magnitude, 0.0001f);
            Assert.AreEqual(Vec2.Zero, MovementIntent.Move(new Vec2(1f, 0f), -2f).ToLocomotion().Stick);
            Assert.AreEqual(Vec2.Zero, MovementIntent.Move(Vec2.Zero, 1f).ToLocomotion().Stick);
            Assert.AreEqual(Vec2.Zero, MovementIntent.Move(new Vec2(float.NaN, 1f), 1f).ToLocomotion().Stick); Assert.AreEqual(Vec2.Zero, MovementIntent.Move(East, float.NaN).ToLocomotion().Stick);
            Assert.AreEqual(Vec2.Zero, MovementIntent.Stand.ToLocomotion().Stick);
            MovementIntent m = MovementIntent.Move(North, 0.4f, ControlSource.Human, 3);
            Assert.AreEqual(ControlSource.Human, m.Source); Assert.AreEqual(3, m.Priority); Assert.IsFalse(m.HasTarget);
        }

        [Test]
        public void MovementIntent_TowardsATarget_FadesInsideTheArriveRadius_AndStopsAtThePoint()
        {
            MovementIntent far = MovementIntent.TowardsTarget(Vec2.Zero, new Vec2(10f, 0f), 0.8f, 2f);
            Assert.IsTrue(far.HasTarget); Assert.AreEqual(new Vec2(10f, 0f), far.Target); Assert.AreEqual(0.8f, far.ToLocomotion().Stick.Magnitude, 0.0001f); Assert.AreEqual(1f, far.ToLocomotion().Stick.X / 0.8f, 0.0001f);
            MovementIntent near = MovementIntent.TowardsTarget(Vec2.Zero, new Vec2(0.5f, 0f), 0.8f, 2f);
            Assert.AreEqual(0.8f * 0.25f, near.ToLocomotion().Stick.Magnitude, 0.0001f);
            Assert.AreEqual(Vec2.Zero, MovementIntent.TowardsTarget(new Vec2(3f, 3f), new Vec2(3f, 3f), 1f, 2f).ToLocomotion().Stick);
            Assert.AreEqual(1f, MovementIntent.TowardsTarget(Vec2.Zero, new Vec2(0.1f, 0f), 1f, 0f).ToLocomotion().Stick.Magnitude, 0.0001f, "no radius, no fade");
        }

        [Test]
        public void ApplyingAMovementIntent_IsExactlyTheSameAsTheStick_ItEntersTheSameLocomotion()
        {
            MatchController a = Live(), b = Live();
            Put(a, TeamSide.Home, "hs2", 0f, 0f, East); Put(b, TeamSide.Home, "hs2", 0f, 0f, East);
            Vec2 stick = new Vec2(0.6f, 0.3f);
            for (int i = 0; i < 90; i++)
            {
                Assert.IsTrue(a.ApplyMovementIntent(TeamSide.Home, "hs2", MovementIntent.Move(stick, stick.Magnitude)));
                b.SetPlayerInput(TeamSide.Home, "hs2", stick);
                a.Advance(Dt); b.Advance(Dt);
            }
            Assert.AreEqual(b.State.Home.Find("hs2").Locomotion.Position, a.State.Home.Find("hs2").Locomotion.Position);
            Assert.AreEqual(b.State.Home.Find("hs2").Locomotion.CurrentStamina, a.State.Home.Find("hs2").Locomotion.CurrentStamina);
            Assert.Greater(a.State.Home.Find("hs2").Locomotion.Position.X, 1f);
        }

        [Test]
        public void MovementIntents_OfTheSameStep_AreArbitratedByPriority_ThenTheNextStepStartsFresh()
        {
            MatchController c = Live();
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Assert.IsTrue(c.ApplyMovementIntent(TeamSide.Home, "hs2", MovementIntent.Move(East, 1f, ControlSource.Ai, 5)));
            Assert.IsFalse(c.ApplyMovementIntent(TeamSide.Home, "hs2", MovementIntent.Move(West, 1f, ControlSource.Ai, 1)), "a lower priority loses the step");
            Assert.IsTrue(c.ApplyMovementIntent(TeamSide.Home, "hs2", MovementIntent.Move(North, 1f, ControlSource.Human, 5)), "the same priority: the last one");
            c.Advance(Dt);
            Assert.Greater(c.State.Home.Find("hs2").Locomotion.InputDirection.Y, 0.9f);
            Assert.IsTrue(c.ApplyMovementIntent(TeamSide.Home, "hs2", MovementIntent.Move(West, 1f, ControlSource.Ai, 0)), "a new step: anything may drive him again");
            Assert.IsFalse(c.ApplyMovementIntent(TeamSide.Home, "nobody", MovementIntent.Move(East, 1f))); Assert.IsFalse(c.ApplyMovementIntent(TeamSide.Home, "hb1", MovementIntent.Move(East, 1f)), "a bench player cannot be moved");
        }

        // ================= action intent =================

        [Test]
        public void ActionIntent_BecomesTheOneActionRequest_WithEverythingItCarries()
        {
            var intent = new ActionIntent(TeamSide.Away, "as3", FootballActionKind.CurvedShot, new Vec2(-1f, 0.2f), 0.6f, ControlSource.Human)
            { Curve = -0.5f, SkillId = "curve_shot", Long = true, Target = PassTarget.ToPosition(new Vec2(-20f, 0f)) };
            ActionRequest r = intent.ToRequest();
            Assert.AreEqual(TeamSide.Away, r.Team); Assert.AreEqual("as3", r.PlayerId); Assert.AreEqual(FootballActionKind.CurvedShot, r.Kind); Assert.AreEqual(new Vec2(-1f, 0.2f), r.Direction);
            Assert.AreEqual(0.6f, r.Power); Assert.AreEqual(-0.5f, r.Curve); Assert.AreEqual("curve_shot", r.SkillId); Assert.IsTrue(r.Long); Assert.AreEqual(PassTargetKind.Position, r.Target.Kind); Assert.AreEqual(ControlSource.Human, r.Source);
            Assert.IsFalse(r.SkillGranted, "an intent never carries the legacy grant");
            Assert.AreEqual(ActionDefaults.DefaultPower, new ActionIntent(TeamSide.Home, "x", FootballActionKind.Shot).ToRequest().Power, "no power means the default power");
            Assert.AreEqual(ControlSource.Ai, new ActionIntent(TeamSide.Home, "x", FootballActionKind.Shot).Source);
        }

        [Test]
        public void ActionIntent_GivesTheSameResultAsTheRequestItStandsFor()
        {
            MatchController a = Passing(4), b = Passing(4);
            ActionResult viaIntent = a.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.7f) { Target = PassTarget.ToPlayer("hs3") });
            ActionResult viaRequest = b.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.7f) { Target = PassTarget.ToPlayer("hs3") });
            Assert.AreEqual(viaRequest.BallVelocity, viaIntent.BallVelocity); Assert.AreEqual(viaRequest.Quality, viaIntent.Quality); Assert.AreEqual(viaRequest.StaminaCost, viaIntent.StaminaCost); Assert.AreEqual(viaRequest.Detail, viaIntent.Detail);
            Assert.AreEqual(ControlSource.Ai, viaIntent.Source);
        }

        [Test]
        public void ActionIntent_WithASkill_IsResolvedBySkillsLikeAnyOtherRequest()
        {
            string catalogJson = File.ReadAllText(Path.Combine(new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName, "Docs", "authoring", "FS27_SKILLS_V1.json"));
            Assert.IsTrue(SkillCatalogJson.TryFromJson(catalogJson, out SkillCatalog catalog, out string err), err);
            var players = new Players(); players.Defs["hs2"] = new PlayerDefinition("hs2", "hs2", 1, PlayerRole.Midfielder, All(90)); players.Defs["hs3"] = new PlayerDefinition("hs3", "hs3", 1, PlayerRole.Midfielder, All(90));
            var skills = new SkillLookup(new Dictionary<string, PlayerSkillSet> { { "hs2", new PlayerSkillSet { PlayerId = "hs2", SkillIds = { "elusive_dribble", "curve_shot" } } } });
            var c = new MatchController(new FieldDimensions(), MatchSettings.CreateClassic(), Squad("h"), Squad("a"), null, "m", null, players, null, skills, catalog);
            c.Start(); Park(c); c.ConfirmRestartReady();
            Carrier(c, TeamSide.Home, "hs2", 4f, 0f, East);
            ActionResult curved = c.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.CurvedShot, East, 1f) { Curve = 0.7f });
            Assert.AreEqual(ActionOutcome.Completed, curved.Outcome); Assert.AreEqual("curve_shot", curved.SkillId);
            Step(c, 30);
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            ActionResult move = c.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.SkillMove, North, 1f) { SkillId = "elusive_dribble" });
            Assert.AreEqual("elusive_dribble", move.SkillId); Assert.AreNotEqual(ActionOutcome.Rejected, move.Outcome);
            Carrier(c, TeamSide.Home, "hs3", 0f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, c.PerformIntent(new ActionIntent(TeamSide.Home, "hs3", FootballActionKind.SkillMove, North, 1f)).Rejection, "no skill, no skill action: the intent cannot bypass it");
        }

        // ================= human and AI end in the same Core =================

        [Test]
        public void AHumanInputAndAnAiIntent_ForTheSameAction_GiveTheSameResult()
        {
            MatchController human = Passing(9), ai = Passing(9);
            human.SetControl(TeamSide.Home, ControlSource.Human); human.SelectPlayer(TeamSide.Home, "hs2");
            HumanInputResult h = human.SubmitHumanInput(TeamSide.Home, NormalizedInput.Press(Vec2.Zero, ActionButton.B, ActionGesture.Tap, East, 0.7f));
            ActionResult a = ai.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.7f));
            Assert.IsTrue(h.ActionAttempted); Assert.AreEqual("hs2", h.PlayerId); Assert.AreEqual(FootballActionKind.ShortPass, h.Action.Kind);
            Assert.AreEqual(a.BallVelocity, h.Action.BallVelocity); Assert.AreEqual(a.Quality, h.Action.Quality); Assert.AreEqual(a.StaminaCost, h.Action.StaminaCost); Assert.AreEqual(a.Detail, h.Action.Detail);
            Assert.AreEqual(ControlSource.Human, h.Action.Source); Assert.AreEqual(ControlSource.Ai, a.Source);
        }

        [Test]
        public void BothRoutes_RaiseTheSameActionEvent_AndThereIsNoOtherWayIntoTheResolver()
        {
            MatchController c = Passing();
            var seen = new List<ControlSource>();
            c.ActionPerformed += r => seen.Add(r.Source);
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SelectPlayer(TeamSide.Home, "hs2");
            c.SubmitHumanInput(TeamSide.Home, NormalizedInput.Press(Vec2.Zero, ActionButton.A, ActionGesture.Tap, East, 1f));
            c.PerformIntent(new ActionIntent(TeamSide.Home, "hs3", FootballActionKind.Pressure, East));
            CollectionAssert.AreEqual(new[] { ControlSource.Human, ControlSource.Ai }, seen);

            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            string actions = File.ReadAllText(Path.Combine(dir, "Match", "MatchController.Actions.cs")), infra = File.ReadAllText(Path.Combine(dir, "Match", "MatchController.Infrastructure.cs"));
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(actions, @"[^\w]Resolve\(request\)").Count, "the resolver is reached from Perform only");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(infra, @"[^.\w]Resolve\("), "the infrastructure never calls the resolver itself"); StringAssert.Contains("Perform(", infra);
            foreach (MethodInfo m in typeof(MatchController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.ReturnType == typeof(ActionResult) && !m.IsSpecialName))
                CollectionAssert.Contains(new[] { "Perform", "PerformInput", "PerformIntent" }, m.Name, "an action can only be started through the three entries that end in Perform");
        }

        [Test]
        public void SubmitHumanInput_DrivesTheControlledPlayer_ByStickAndButton_AndNothingWithoutAControlledPlayer()
        {
            MatchController c = Live();
            c.SetControl(TeamSide.Home, ControlSource.Human);
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East);
            c.SelectPlayer(TeamSide.Home, "hs2");
            HumanInputResult r = c.SubmitHumanInput(TeamSide.Home, NormalizedInput.Stick(new Vec2(1f, 0f)));
            Assert.AreEqual("hs2", r.PlayerId); Assert.IsTrue(r.Moved); Assert.IsFalse(r.ActionAttempted);
            Step(c, 60); Assert.Greater(c.State.Home.Find("hs2").Locomotion.Position.X, 1f); Assert.AreEqual(0f, c.State.Home.Find("hs3").Locomotion.Position.X + 16f, 0.01f, "the others are not touched");
            Carrier(c, TeamSide.Home, "hs2", 4f, 0f, East);
            HumanInputResult shot = c.SubmitHumanInput(TeamSide.Home, NormalizedInput.Press(Vec2.Zero, ActionButton.A, ActionGesture.SwipeDown, East, 1f));
            Assert.AreEqual(FootballActionKind.Shot, shot.Action.Kind); Assert.AreEqual(ActionOutcome.Completed, shot.Action.Outcome);
            HumanInputResult none = c.SubmitHumanInput(TeamSide.Away, NormalizedInput.Stick(East));
            Assert.IsNotNull(none.PlayerId, "the away side also has a fallback controlled player");
            var empty = new MatchController(new FieldDimensions(), MatchSettings.CreateClassic());
            Assert.IsNull(empty.SubmitHumanInput(TeamSide.Home, NormalizedInput.Stick(East)).PlayerId, "a match with no squads has nobody to control");
        }

        // ================= normalised input =================

        [Test]
        public void NormalizedInput_IsMadeSafe_NaNLongSticksAndWildValuesCannotReachTheCore()
        {
            NormalizedInput n = new NormalizedInput { Move = new Vec2(3f, 4f), Aim = new Vec2(float.NaN, 0f), Power = 7f, Curve = -9f, Button = (ActionButton)99, Gesture = (ActionGesture)42 }.Sanitized();
            Assert.AreEqual(1f, n.Move.Magnitude, 0.0001f); Assert.AreEqual(Vec2.Zero, n.Aim); Assert.AreEqual(1f, n.Power); Assert.AreEqual(-1f, n.Curve); Assert.AreEqual(ActionButton.None, n.Button); Assert.AreEqual(ActionGesture.Tap, n.Gesture);
            Assert.AreEqual(Vec2.Zero, new NormalizedInput { Move = new Vec2(float.PositiveInfinity, 0f) }.Sanitized().Move); Assert.AreEqual(0f, new NormalizedInput { Power = float.NaN, Curve = float.NaN }.Sanitized().Curve);
            Assert.AreEqual(new Vec2(0.3f, 0.2f), new NormalizedInput { Move = new Vec2(0.3f, 0.2f) }.Sanitized().Move, "an ordinary stick is left alone");
            Assert.IsFalse(NormalizedInput.Stick(East).HasAction); Assert.IsTrue(NormalizedInput.Press(East, ActionButton.C, ActionGesture.Tap, East, 1f).HasAction);
        }

        [Test]
        public void NormalizedInput_EveryButtonAndGesture_BecomesTheActionInputTheMapperReads()
        {
            foreach (ActionButton b in new[] { ActionButton.A, ActionButton.B, ActionButton.C })
                foreach (ActionGesture g in Enum.GetValues(typeof(ActionGesture)))
                {
                    ActionInput ai = NormalizedInput.Press(East, b, g, new Vec2(0f, 2f), 0.7f, 0.3f).ToActionInput();
                    Assert.AreEqual(b, ai.Button); Assert.AreEqual(g, ai.Gesture); Assert.AreEqual(new Vec2(0f, 2f), ai.Direction); Assert.AreEqual(0.7f, ai.Magnitude); Assert.AreEqual(0.3f, ai.Curve);
                    foreach (bool ball in new[] { true, false })
                    {
                        FootballActionKind k = ActionMapper.Map(ai, new ActionContext { HasBall = ball });
                        if (b == ActionButton.C && !ball) Assert.AreEqual(FootballActionKind.None, k); else Assert.AreNotEqual(FootballActionKind.None, k, b + " " + g + " " + ball);
                    }
                }
            Assert.AreEqual(new Vec2(0.5f, 0f), NormalizedInput.Stick(new Vec2(0.5f, 0f)).ToLocomotion().Stick);
            Assert.AreEqual(ActionButton.None, NormalizedInput.Stick(East).ToActionInput().Button);
        }

        [Test]
        public void TheInputContracts_AreCorePure_NoUnityNoPixelsNoTouchIds()
        {
            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string file in Directory.GetFiles(Path.Combine(dir, "Intents"), "*.cs").Concat(Directory.GetFiles(Path.Combine(dir, "World"), "*.cs")).Concat(new[] { Path.Combine(dir, "Match", "MatchController.Infrastructure.cs"), Path.Combine(dir, "Match", "PitchBounds.cs"), Path.Combine(dir, "Match", "PlayerSeparation.cs"), Path.Combine(dir, "Match", "FormationPlacement.cs"), Path.Combine(dir, "Match", "GoalkeeperSlot.cs"), Path.Combine(dir, "Match", "InfrastructureDefaults.cs") }))
            {
                string text = File.ReadAllText(file), name = Path.GetFileName(file);
                foreach (string banned in new[] { "UnityEngine", "MonoBehaviour", "DateTime", "System.Random", "Time.time", "Time.deltaTime", "Input.Get", "Screen.width", "Stopwatch" })
                    StringAssert.DoesNotContain(banned, text, name);
            }
            Assert.AreEqual(typeof(Vec2), typeof(NormalizedInput).GetField("Move").FieldType);
        }

        // ================= player selection =================

        [Test]
        public void Selection_TheFallbackIsThePlayerWithTheBall_ElseTheFirstFieldPlayer_AndItIsStored()
        {
            MatchController c = Live();
            Assert.AreEqual("hs1", c.ControlledPlayerId(TeamSide.Home), "never the goalkeeper while there is a field player"); Assert.AreEqual("hs1", c.StoredSelection(TeamSide.Home));
            c.ClearSelection(TeamSide.Home);
            Carrier(c, TeamSide.Home, "hs4", 0f, 0f, East);
            Assert.AreEqual("hs4", c.ControlledPlayerId(TeamSide.Home), "the ball carrier of the team");
            Assert.AreEqual("as1", c.ControlledPlayerId(TeamSide.Away), "the other side is independent");
        }

        [Test]
        public void Selection_ManualSelectionAndChange_OnlyOnPlayersOnThePitch()
        {
            MatchController c = Live();
            Assert.IsTrue(c.SelectPlayer(TeamSide.Home, "hs3")); Assert.AreEqual("hs3", c.ControlledPlayerId(TeamSide.Home));
            Assert.IsTrue(c.SelectPlayer(TeamSide.Home, "hs0"), "the goalkeeper can be selected by hand");
            Assert.IsFalse(c.SelectPlayer(TeamSide.Home, "hb1"), "a bench player"); Assert.IsFalse(c.SelectPlayer(TeamSide.Home, "as1"), "an opponent"); Assert.IsFalse(c.SelectPlayer(TeamSide.Home, "nobody"));
            Assert.AreEqual("hs0", c.ControlledPlayerId(TeamSide.Home), "a refused selection changes nothing");
            c.SelectPlayer(TeamSide.Home, "hs5");
            Assert.AreEqual("hs0", c.CycleSelection(TeamSide.Home, 1), "next, wrapping round the squad list"); Assert.AreEqual("hs1", c.CycleSelection(TeamSide.Home, 1));
            Assert.AreEqual("hs0", c.CycleSelection(TeamSide.Home, -1)); Assert.AreEqual("hs5", c.CycleSelection(TeamSide.Home, -1));
        }

        [Test]
        public void Selection_CyclingSkipsWhoCannotPlay_AndAnExpelledSelectedPlayerFallsBack()
        {
            MatchController c = Live();
            c.SelectPlayer(TeamSide.Home, "hs2");
            c.IssueCard(TeamSide.Home, "hs3", CardKind.Red);
            Assert.AreEqual("hs4", c.CycleSelection(TeamSide.Home, 1), "hs3 is out: skipped");
            c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            Assert.AreEqual("hs1", c.ControlledPlayerId(TeamSide.Home), "the selected player was sent off: the fallback takes over"); Assert.AreEqual("hs1", c.StoredSelection(TeamSide.Home));
        }

        [Test]
        public void Selection_ASubstitutedPlayer_IsNoLongerTheControlledOne()
        {
            MatchController c = Live(confirm: false);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs2", "hb1"));
            c.SelectPlayer(TeamSide.Home, "hb1"); Assert.AreEqual("hb1", c.ControlledPlayerId(TeamSide.Home));
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hb1", "hb2"));
            Assert.AreNotEqual("hb1", c.ControlledPlayerId(TeamSide.Home));
            Assert.IsTrue(c.State.Home.Find(c.ControlledPlayerId(TeamSide.Home)).IsActive);
        }

        [Test]
        public void Selection_OnlyAHumanSideHasAControlledPlayerInTheWorld()
        {
            MatchController c = Live();
            Assert.AreEqual(ControlSource.Ai, c.GetControl(TeamSide.Home)); Assert.AreEqual(ControlSource.Ai, c.GetControl(TeamSide.Away));
            c.SelectPlayer(TeamSide.Home, "hs2");
            WorldState w = WorldState.Capture(c);
            Assert.IsFalse(Enumerable.Range(0, w.PlayerCount).Any(i => w[i].IsControlled), "nobody is controlled by a person until a side is human");
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SetControl(TeamSide.Away, ControlSource.Human); c.SelectPlayer(TeamSide.Away, "as3");
            w = WorldState.Capture(c);
            Assert.AreEqual(2, Enumerable.Range(0, w.PlayerCount).Count(i => w[i].IsControlled)); Assert.IsTrue(w[w.IndexOf("as3")].IsControlled);
        }

        // ================= goalkeeper slot =================

        [Test]
        public void GoalkeeperSlot_StartsWithTheStarterInGoal_AndTheBenchKeeperKnown()
        {
            MatchController c = Live();
            GoalkeeperSlot s = c.State.Home.GoalkeeperSlot;
            Assert.AreEqual("hs0", s.StarterId); Assert.AreEqual("hs0", s.OccupantId); Assert.AreEqual("hb0", s.BenchKeeperId); Assert.AreEqual(GoalkeeperSlotMode.Starter, s.Mode); Assert.IsTrue(s.OccupiedByStarter); Assert.IsFalse(s.NeedsGoalkeeper); Assert.AreEqual(TeamSide.Home, s.Team);
            Assert.AreEqual("as0", WorldState.Capture(c).GoalkeeperSlotOf(TeamSide.Away).OccupantId);
        }

        [Test]
        public void GoalkeeperSlot_AnExpelledKeeperLeavesItVacant_AndASubstituteKeeperFillsIt()
        {
            MatchController c = Live();
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            GoalkeeperSlot s = c.State.Home.GoalkeeperSlot;
            Assert.AreEqual(GoalkeeperSlotMode.Vacant, s.Mode); Assert.IsNull(s.OccupantId); Assert.IsTrue(s.NeedsGoalkeeper); Assert.AreEqual("hs0", s.StarterId, "the starter is still known"); Assert.AreEqual("hb0", s.BenchKeeperId);
            Assert.AreEqual(GoalkeeperSlotMode.Vacant, WorldState.Capture(c).GoalkeeperSlotOf(TeamSide.Home).Mode);
            Assert.AreEqual(SubstitutionResult.NotSafeMoment, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs4"), "in the middle of live play no substitution is made (the slot stays vacant until a stoppage)");
        }

        [Test]
        public void GoalkeeperSlot_ASubstituteKeeperAtASafeMoment_IsASubstitute_NotTheStarter()
        {
            MatchController c = Live(confirm: false);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs0", "hb0"));
            GoalkeeperSlot s = c.State.Home.GoalkeeperSlot;
            Assert.AreEqual(GoalkeeperSlotMode.Substitute, s.Mode); Assert.AreEqual("hb0", s.OccupantId); Assert.AreEqual("hs0", s.StarterId); Assert.IsNull(s.BenchKeeperId, "no keeper left on the bench"); Assert.IsFalse(s.OccupiedByStarter);
        }

        [Test]
        public void GoalkeeperSlot_ADismissedKeeperReplacedByAnotherKeeper_IsASubstitute()
        {
            MatchController c = Live(confirm: false);
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs4"));
            GoalkeeperSlot s = c.State.Home.GoalkeeperSlot;
            Assert.AreEqual(GoalkeeperSlotMode.Substitute, s.Mode); Assert.AreEqual("hb0", s.OccupantId); Assert.IsFalse(s.NeedsGoalkeeper);
            c.IssueCard(TeamSide.Home, "hb0", CardKind.Red);
            Assert.AreEqual(GoalkeeperSlotMode.Vacant, c.State.Home.GoalkeeperSlot.Mode, "and when he is sent off too, vacant again");
        }

        [Test]
        public void GoalkeeperSlot_AnEmergencyKeeper_IsAFieldPlayerWhoTookTheGloves()
        {
            MatchController c = Live(confirm: false);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs1", "hb1")); Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs2", "hb2")); Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs3", "hb3"));
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.AreEqual(EmergencyKeeperResult.Assigned, c.TryAssignEmergencyKeeper(TeamSide.Home, "hs4"));
            GoalkeeperSlot s = c.State.Home.GoalkeeperSlot;
            Assert.AreEqual(GoalkeeperSlotMode.Emergency, s.Mode); Assert.AreEqual("hs4", s.OccupantId); Assert.AreEqual("hs0", s.StarterId); Assert.AreEqual("hb0", s.BenchKeeperId, "the bench keeper exists but the substitutions are used up"); Assert.IsFalse(s.NeedsGoalkeeper);
            WorldPlayer p = WorldState.Capture(c)[WorldState.Capture(c).IndexOf("hs4")];
            Assert.IsTrue(p.IsGoalkeeper); Assert.IsTrue(p.IsEmergencyKeeper); Assert.AreEqual(PitchZone.Goal, p.Zone);
            c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            Assert.AreEqual(GoalkeeperSlotMode.Vacant, c.State.Home.GoalkeeperSlot.Mode);
        }
    }
}
