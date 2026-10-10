using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 4: actions and ball. The Core decides; these tests drive the match engine only through its public API.</summary>
    public partial class ActionsAndBallTests
    {
        private const float Dt = LocomotionDefaults.FixedDeltaTime;
        private static readonly Vec2 East = new Vec2(1f, 0f);
        private static readonly Vec2 West = new Vec2(-1f, 0f);

        // ================= scenery =================

        private static SquadMember[] Starters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false, 100f)); return l.ToArray(); }
        private static SquadMember[] Bench(string prefix) { return new[] { new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f), new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f) }; }
        private static MatchSquad Squad(string prefix) { return new MatchSquad(Starters(prefix), Bench(prefix)); }

        private class Lookup : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Players = new Dictionary<string, PlayerDefinition>();
            public readonly Dictionary<string, GoalkeeperProfile> Keepers = new Dictionary<string, GoalkeeperProfile>();
            public bool TryGet(string id, out PlayerDefinition p) { return Players.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { return Keepers.TryGetValue(id, out g); }

            public Lookup Set(string id, Func<PlayerAttributes, PlayerAttributes> edit = null, PreferredFoot foot = PreferredFoot.Right, int weakFoot = 50)
            {
                PlayerAttributes a = PlayerAttributes.CreateDefault();
                if (edit != null) a = edit(a);
                Players[id] = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, a) { PreferredFoot = foot, WeakFootQuality = weakFoot };
                return this;
            }
        }

        /// <summary>Sets every attribute of a player to the same value.</summary>
        private static PlayerAttributes All(int v)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = a.Acceleration = a.Stamina = a.BallControl = a.Passing = a.Shooting = a.Defense = a.Strength = a.Agility = a.Finishing = a.Dribbling = a.Technique = v;
            return a;
        }

        private static Lookup Uniform(int v, params string[] ids)
        {
            var l = new Lookup();
            foreach (string id in ids) l.Players[id] = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, All(v));
            return l;
        }

        private static float Heading(Vec2 direction) { return direction.ToHeading(); }

        /// <summary>A live Classic match with everybody parked far from the ball (nobody touches anything until the test wants it).</summary>
        private static MatchController Live(MatchSettings settings = null, IPlayerLookup lookup = null, ActionTuning tuning = null, int seed = 1)
        {
            settings = settings ?? MatchSettings.CreateClassic();
            settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, lookup, tuning);
            c.Start();
            Park(c);
            Assert.IsTrue(c.ConfirmRestartReady());
            return c;
        }

        private static void Park(MatchController c)
        {
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -16f, -10f + i * 4f), Heading(East));
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 16f, -10f + i * 4f), Heading(West));
            }
        }

        private static void Put(MatchController c, TeamSide side, string id, float x, float y, Vec2 facing)
        {
            Assert.IsTrue(c.PlacePlayer(side, id, new Vec2(x, y), Heading(facing)));
        }

        private static void PutBall(MatchController c, float x, float z, float vx = 0f, float vz = 0f, float vy = 0f)
        {
            c.State.Ball.Physics = new BallState(new Vec3(x, c.BallParameters.Radius + (vy > 0f ? 0.05f : 0f), z), new Vec3(vx, vy, vz)) { IsGrounded = vy <= 0f };
            c.ReleaseBall();
        }

        private static void Step(MatchController c, int n) { for (int i = 0; i < n; i++) c.Advance(Dt); }

        /// <summary>The player stands at (x,y) facing the given way with the ball controlled at his feet.</summary>
        private static MatchPlayer Carrier(MatchController c, TeamSide side, string id, float x, float y, Vec2 facing)
        {
            Put(c, side, id, x, y, facing);
            PutBall(c, x + facing.X * 0.8f, y + facing.Y * 0.8f);
            Assert.IsTrue(c.GiveBallTo(side, id));
            return c.State.Team(side).Find(id);
        }

        private static ActionResult Do(MatchController c, TeamSide side, string id, FootballActionKind kind, Vec2 dir, float power = 1f)
        {
            return c.Perform(new ActionRequest(side, id, kind, dir, power));
        }

        private static float Lateral(Vec3 v) { return Math.Abs(v.Z); }

        private static float AngleBetween(Vec2 a, Vec2 b)
        {
            return Math.Abs(ActionModel.SignedAngle(a, b));
        }

        // ================= contracts: input, mapping, defaults =================

        [Test]
        public void TheNeutralInput_CarriesButtonGestureDirectionAndMagnitude_AndNothingElse()
        {
            var i = new ActionInput(ActionButton.A, ActionGesture.SwipeDown, new Vec2(1f, 2f), 0.7f);
            Assert.AreEqual(ActionButton.A, i.Button); Assert.AreEqual(ActionGesture.SwipeDown, i.Gesture);
            Assert.AreEqual(1f, i.Direction.X); Assert.AreEqual(0.7f, i.Magnitude);
            Assert.AreEqual(4, Enum.GetValues(typeof(ActionButton)).Length, "None, A, B and (Phase 6) the skill button C");
            Assert.AreEqual(3, Enum.GetValues(typeof(ActionGesture)).Length, "tap, swipe up, swipe down");
            Assert.AreEqual(5, typeof(ActionInput).GetFields().Length, "button, gesture, direction, magnitude and (Phase 6) the curve of a curved shot");
        }

        private static FootballActionKind Map(ActionButton b, ActionGesture g, bool ball, bool keeper = false)
        {
            return ActionMapper.Map(new ActionInput(b, g, Vec2.Zero, 1f), new ActionContext { HasBall = ball, IsGoalkeeper = keeper });
        }

        [Test]
        public void WithTheBall_TheButtonsAre_ShortPass_WallPass_ThroughBall_Shot_Chip()
        {
            Assert.AreEqual(FootballActionKind.ShortPass, Map(ActionButton.B, ActionGesture.Tap, true));
            Assert.AreEqual(FootballActionKind.WallPass, Map(ActionButton.B, ActionGesture.SwipeDown, true));
            Assert.AreEqual(FootballActionKind.ThroughPass, Map(ActionButton.B, ActionGesture.SwipeUp, true));
            Assert.AreEqual(FootballActionKind.Shot, Map(ActionButton.A, ActionGesture.SwipeDown, true));
            Assert.AreEqual(FootballActionKind.Chip, Map(ActionButton.A, ActionGesture.SwipeUp, true));
            Assert.AreEqual(FootballActionKind.Shield, Map(ActionButton.A, ActionGesture.Tap, true));
        }

        [Test]
        public void WithoutTheBall_TheButtonsAre_Pressure_Tackle_Slide_Challenge_Intercept_Clearance()
        {
            Assert.AreEqual(FootballActionKind.Pressure, Map(ActionButton.A, ActionGesture.Tap, false));
            Assert.AreEqual(FootballActionKind.Tackle, Map(ActionButton.A, ActionGesture.SwipeDown, false));
            Assert.AreEqual(FootballActionKind.SlidingTackle, Map(ActionButton.A, ActionGesture.SwipeUp, false));
            Assert.AreEqual(FootballActionKind.PhysicalChallenge, Map(ActionButton.B, ActionGesture.Tap, false));
            Assert.AreEqual(FootballActionKind.Intercept, Map(ActionButton.B, ActionGesture.SwipeDown, false));
            Assert.AreEqual(FootballActionKind.Clearance, Map(ActionButton.B, ActionGesture.SwipeUp, false));
        }

        [Test]
        public void TheGoalkeeper_UsesTheSameScheme_PlusDistributionAndTheRushContract()
        {
            Assert.AreEqual(FootballActionKind.GoalkeeperDistribution, Map(ActionButton.B, ActionGesture.Tap, true, true));
            Assert.AreEqual(FootballActionKind.GoalkeeperDistribution, Map(ActionButton.B, ActionGesture.SwipeUp, true, true));
            Assert.AreEqual(FootballActionKind.Shot, Map(ActionButton.A, ActionGesture.SwipeDown, true, true), "the rest is the common scheme");
            Assert.AreEqual(FootballActionKind.GoalkeeperRush, Map(ActionButton.A, ActionGesture.SwipeUp, false, true));
            Assert.AreEqual(FootballActionKind.Tackle, Map(ActionButton.A, ActionGesture.SwipeDown, false, true));
            Assert.AreEqual(FootballActionKind.None, Map(ActionButton.None, ActionGesture.Tap, true));
        }

        [Test]
        public void TheMapping_IsTotal_EveryButtonGestureAndContextGivesARealAction()
        {
            foreach (ActionButton b in new[] { ActionButton.A, ActionButton.B })
                foreach (ActionGesture g in Enum.GetValues(typeof(ActionGesture)))
                    foreach (bool ball in new[] { true, false })
                        foreach (bool keeper in new[] { true, false })
                            Assert.AreNotEqual(FootballActionKind.None, Map(b, g, ball, keeper), b + " " + g + " ball=" + ball + " keeper=" + keeper);
        }

        [Test]
        public void TheActionNumbers_LiveInOnePlace_AndTheTuningStartsFromThem()
        {
            var t = new ActionTuning();
            foreach (FieldInfo f in typeof(ActionDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                FieldInfo mirror = typeof(ActionTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual((float)f.GetValue(null), (float)mirror.GetValue(t), f.Name);
            }
            foreach (FieldInfo f in typeof(ActionTuning).GetFields()) Assert.IsNotNull(typeof(ActionDefaults).GetField(f.Name), f.Name + " has no default");
            Assert.Greater(typeof(ActionDefaults).GetFields().Length, 60, "the specified parameters are all there");
            foreach (string name in new[] { "ControlDistance", "ControlSpeed", "LooseBallDistance", "FirstTouchDistance", "MaxControlAngleDegrees" })
                Assert.IsNotNull(typeof(ActionDefaults).GetField(name), name);
        }

        [Test]
        public void EveryDefault_IsSane_PositiveOrAFraction()
        {
            foreach (FieldInfo f in typeof(ActionDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                float v = (float)f.GetValue(null);
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), f.Name);
                if (f.Name.EndsWith("Margin")) continue; // margins and thresholds may be negative
                if (f.Name == "HeavyTouchThreshold" || f.Name == "SlideBonus") continue;
                Assert.Greater(v, 0f, f.Name);
            }
            Assert.Less(ActionDefaults.ControlDistance, ActionDefaults.LooseBallDistance);
            Assert.Less(ActionDefaults.TouchReach, ActionDefaults.LooseBallDistance);
            Assert.Less(ActionDefaults.PassMinSpeed, ActionDefaults.PassMaxSpeed);
            Assert.Less(ActionDefaults.ShotMinSpeed, ActionDefaults.ShotMaxSpeed);
            Assert.Greater(ActionDefaults.TackleCleanMargin, ActionDefaults.TackleDeflectMargin);
            Assert.Greater(ActionDefaults.CleanControlThreshold, ActionDefaults.HeavyTouchThreshold);
        }

        [Test]
        public void TheActionResult_CarriesEnoughForAnimation_WhoWhatWithWhichFootHowHardWhichWayAndWhatCameOfIt()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.8f);
            Assert.AreEqual("hs1", r.PlayerId); Assert.AreEqual(TeamSide.Home, r.Team); Assert.AreEqual(FootballActionKind.ShortPass, r.Kind);
            Assert.AreNotEqual(ActionFoot.None, r.Foot); Assert.AreEqual(0.8f, r.Intensity, 1e-5f);
            Assert.Greater(r.Direction.X, 0.9f); Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Kicked, r.Detail);
            Assert.Greater(r.BallVelocity.Magnitude, 1f); Assert.Greater(r.StaminaCost, 0f); Assert.Greater(r.Quality, 0f);
            Assert.AreSame(typeof(ActionResult), r.GetType());
        }

        // ================= results: explicit rejections, never exceptions =================

        [Test]
        public void ARefusal_IsAResultWithItsReason_NotAnException()
        {
            var c = new MatchController(new FieldDimensions(), MatchSettings.CreateClassic(), Squad("h"), Squad("a"));
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection, "before the match");
            c.Start();
            Park(c);
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection, "waiting for the kick-off");
            c.ConfirmRestartReady();
            Assert.AreEqual(ActionRejection.UnknownPlayer, Do(c, TeamSide.Home, "ghost", FootballActionKind.ShortPass, East).Rejection);
            Assert.AreEqual(ActionRejection.PlayerNotActive, Do(c, TeamSide.Home, "hb1", FootballActionKind.ShortPass, East).Rejection, "a substitute is not on the pitch");
            Assert.AreEqual(ActionRejection.InvalidInput, Do(c, TeamSide.Home, "hs1", FootballActionKind.None, East).Rejection);
            Assert.AreEqual(ActionRejection.InvalidInput, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, new Vec2(float.NaN, 0f)).Rejection);
            Assert.AreEqual(ActionRejection.InvalidInput, c.Perform(new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, float.NaN)).Rejection);
            Assert.AreEqual(ActionOutcome.Rejected, c.LastActionResult.Outcome);
            c.Pause();
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection, "paused");
        }

        [Test]
        public void ARejectedAction_ChangesNothing_NoBallNoStaminaNoEvents()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 10f, 5f);
            BallState ball = c.State.Ball.Physics;
            float stamina = c.State.Home.Find("hs1").Locomotion.CurrentStamina;
            int events = c.Events.Log.Count;
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East);
            Assert.AreEqual(ActionOutcome.Rejected, r.Outcome); Assert.AreEqual(ActionRejection.BallOutOfReach, r.Rejection);
            Assert.AreEqual(ball.Position.X, c.State.Ball.Physics.Position.X); Assert.AreEqual(ball.Velocity.X, c.State.Ball.Physics.Velocity.X);
            Assert.AreEqual(stamina, c.State.Home.Find("hs1").Locomotion.CurrentStamina); Assert.AreEqual(events, c.Events.Log.Count);
            Assert.AreEqual(0f, r.StaminaCost);
        }

        [Test]
        public void ContractOnlyActions_AreRefusedClearly_NotImplementedAndSkillsNeedTheirSkill()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            foreach (FootballActionKind k in new[] { FootballActionKind.Header, FootballActionKind.Cross, FootballActionKind.PlacedShot, FootballActionKind.GoalkeeperSave, FootballActionKind.GoalkeeperCatch })
                Assert.AreEqual(ActionRejection.NotSupported, Do(c, TeamSide.Home, "hs1", k, East).Rejection, k.ToString());
            foreach (FootballActionKind k in new[] { FootballActionKind.SkillMove, FootballActionKind.SkillLobPass, FootballActionKind.CurvedShot })
                Assert.AreEqual(ActionRejection.SkillNotGranted, Do(c, TeamSide.Home, "hs1", k, East).Rejection, k.ToString());
            var granted = new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.SkillMove, East) { SkillGranted = true };
            Assert.AreEqual(ActionRejection.None, c.Perform(granted).Rejection, "Phase 6: a granted skill move is real now (it completes or fails, it is never refused)");
            var lob = new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.SkillLobPass, East) { SkillGranted = true };
            ActionResult r = c.Perform(lob);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Lofted, r.Detail); Assert.Greater(r.BallVelocity.Y, 0f);
        }

        // ================= the ball model: single source, no gluing =================

        [Test]
        public void ThereIsOnePossessionSource_AndItNeverPointsAtTwoPlayers()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.IsTrue(c.State.Ball.HasPossession); Assert.AreEqual(TeamSide.Home, c.State.Ball.PossessingTeam); Assert.AreEqual("hs1", c.State.Ball.PossessingPlayerId);
            Assert.AreSame(c.State.Home.Find("hs1"), c.BallCarrier);
            Assert.AreEqual(BallMode.Controlled, c.State.Ball.Mode);
            Assert.IsTrue(c.GetActionContext(TeamSide.Home, "hs1").HasBall);
            Assert.IsFalse(c.GetActionContext(TeamSide.Home, "hs2").HasBall);
            Assert.IsFalse(c.GetActionContext(TeamSide.Away, "as1").HasBall);
            int carriers = 0;
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players) if (c.GetActionContext(p.Team, p.PlayerId).HasBall) carriers++;
            Assert.AreEqual(1, carriers);
            Assert.AreEqual(2, typeof(MatchBall).GetProperties().Count(p => p.Name == "PossessingTeam" || p.Name == "PossessingPlayerId"));
        }

        [Test]
        public void TheBall_IsNeverGluedToThePlayer_ItHasItsOwnPositionAndVelocity()
        {
            var c = Live();
            MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", -5f, 0f, East);
            c.SetPlayerInput(TeamSide.Home, "hs1", new Vec2(1f, 0f));
            Step(c, 10);
            Assert.AreNotEqual(p.Locomotion.Position.X + 0.8f, c.State.Ball.Physics.Position.X, "the ball is its own object");
            Assert.AreEqual(c.BallParameters.Radius, c.State.Ball.Physics.Position.Y, 1e-4f, "on the ground");
            c.SetPlayerInput(TeamSide.Home, "hs1", new Vec2(1f, 0f));
            Step(c, 120);
            Assert.IsTrue(c.State.Ball.HasPossession, "he still has it");
            float gap = c.State.Ball.Physics.Position.X - p.Locomotion.Position.X;
            Assert.Greater(gap, 0.3f); Assert.Less(gap, ActionDefaults.LooseBallDistance, "in front of him, within the control limit");
            Assert.Greater(p.Locomotion.Position.X, -5f + 2f, "and he really ran");
        }

        [Test]
        public void ARunningPlayer_KeepsTheBall_AtTopSpeedInAStraightLine()
        {
            var c = Live(lookup: Uniform(85, "hs1"));
            MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", -15f, 0f, East);
            c.SetPlayerInput(TeamSide.Home, "hs1", East);
            for (int i = 0; i < 60 * 3; i++)
            {
                c.SetPlayerInput(TeamSide.Home, "hs1", East);
                c.Advance(Dt);
                Assert.IsTrue(c.State.Ball.HasPossession, "lost at step " + i);
            }
            Assert.Greater(p.Locomotion.CurrentSpeed, 4f);
            Assert.AreEqual(p.Locomotion.Velocity.X, c.State.Ball.Physics.Velocity.X, 1.5f, "the ball goes with him");
        }

        [Test]
        public void TheBall_IsLostWhenItGetsBeyondLooseBallDistance()
        {
            var tuning = new ActionTuning { CatchUpAcceleration = 0.5f };
            var c = Live(lookup: Uniform(90, "hs1"), tuning: tuning);
            Carrier(c, TeamSide.Home, "hs1", -15f, 0f, East);
            for (int i = 0; i < 180; i++) { c.SetPlayerInput(TeamSide.Home, "hs1", East); c.Advance(Dt); if (!c.State.Ball.HasPossession) break; }
            Assert.IsFalse(c.State.Ball.HasPossession, "a ball that cannot follow him is lost");
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
            Assert.IsNull(c.State.Ball.PossessingPlayerId);
            Assert.IsNull(c.BallCarrier);
        }

        [Test]
        public void ABetterControlledPlayer_KeepsTheBallInTightTurns_ABadOneLosesIt()
        {
            int Lost(int skill)
            {
                int lost = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    var c = Live(lookup: Uniform(skill, "hs1"), tuning: new ActionTuning { CatchUpAcceleration = 9f }, seed: seed);
                    MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", -10f, 0f, East);
                    for (int i = 0; i < 60 * 8 && c.State.Ball.HasPossession; i++)
                    {
                        Vec2 input = (i / 40) % 2 == 0 ? East : new Vec2(0f, 1f);
                        c.SetPlayerInput(TeamSide.Home, "hs1", input);
                        c.Advance(Dt);
                    }
                    if (!c.State.Ball.HasPossession) lost++;
                }
                return lost;
            }
            Assert.Less(Lost(95), Lost(10) + 1);
            Assert.GreaterOrEqual(Lost(10), Lost(95));
        }

        [Test]
        public void TheBallMode_FollowsWhatTheBallIsDoing()
        {
            var c = Live();
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(BallMode.Controlled, c.State.Ball.Mode);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.3f);
            Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);
            Step(c, 60 * 10);
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode, "a ball that has stopped is simply free");
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Assert.AreEqual(BallMode.InShot, c.State.Ball.Mode);
        }

        [Test]
        public void TheEnumNames_OfTheBallModel_MapOntoTheExistingModes()
        {
            Assert.AreEqual(0, (int)BallMode.Free); Assert.AreEqual(1, (int)BallMode.Controlled); Assert.AreEqual(2, (int)BallMode.InPass);
            Assert.AreEqual(3, (int)BallMode.InShot); Assert.AreEqual(4, (int)BallMode.Rebound); Assert.AreEqual(5, (int)BallMode.Deflected);
            Assert.AreEqual(6, (int)BallMode.Dead, "out of play"); Assert.AreEqual(7, (int)BallMode.Goal, "added at the end");
        }

        [Test]
        public void ADeadBall_IsNeverTouched_AndAGoalPutsTheBallInGoalMode()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 15f, 0f, East);
            PutBall(c, 15.5f, 0f, 15f, 0f);
            Step(c, 3);
            Put(c, TeamSide.Home, "hs2", 18f, 0f, East);
            PutBall(c, 19.5f, 0f, 18f, 0f);
            c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 15f));
            Step(c, 20);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
            Assert.AreEqual(BallMode.Goal, c.State.Ball.Mode);
            Assert.AreEqual(1, c.State.Score.Home);
            int touches = c.Events.Count(GameplayEventKind.BallTouched);
            Step(c, 30);
            Assert.AreEqual(touches, c.Events.Count(GameplayEventKind.BallTouched), "nobody touches the ball during the celebration");
            c.ConfirmRestartReady();
        }

        // ================= first touch and control =================

        [Test]
        public void ASlowBallAtTheFeet_IsControlledCleanly()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 0.6f, 0f, 1f, 0f);
            var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
            Step(c, 3);
            Assert.IsTrue(c.State.Ball.HasPossession); Assert.AreEqual("hs1", c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(1, seen.Count); Assert.AreEqual(FootballActionKind.Control, seen[0].Kind); Assert.AreEqual(ActionDetail.CleanControl, seen[0].Detail);
            Assert.IsTrue(seen[0].GainedPossession); Assert.AreEqual("hs1", c.State.Ball.LastTouchPlayerId);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallTouched)); Assert.IsTrue(c.Events.Has(GameplayEventKind.PossessionChanged));
        }

        [Test]
        public void ABallThatIsTooFast_CannotBeControlled_ItBouncesOff()
        {
            var c = Live(lookup: Uniform(1, "hs1"));
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, -2f, 0f, 30f, 0f);
            var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
            Step(c, 12);
            Assert.IsFalse(c.State.Ball.HasPossession);
            Assert.IsTrue(seen.Any(r => r.Detail == ActionDetail.Deflected), "deflected");
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallDeflected));
            Assert.AreEqual("hs1", c.State.Ball.LastTouchPlayerId);
            Assert.IsTrue(c.State.Ball.Mode == BallMode.Deflected || c.State.Ball.Mode == BallMode.Free);
        }

        [Test]
        public void AMiddlingTouch_IsHeavy_TheBallRunsAwayFromHimButIsNotLost()
        {
            ActionResult heavy = default(ActionResult);
            bool found = false;
            for (int seed = 1; seed <= 40 && !found; seed++)
            {
                var c = Live(lookup: Uniform(30, "hs1"), seed: seed);
                Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
                PutBall(c, 0.5f, 0f, -4f, 0f);
                var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
                Step(c, 2);
                foreach (ActionResult r in seen) if (r.Detail == ActionDetail.HeavyTouch) { heavy = r; found = true; Assert.IsFalse(c.State.Ball.HasPossession); Assert.AreEqual(BallMode.Free, c.State.Ball.Mode); Assert.Greater(c.State.Ball.Physics.Velocity.X, 0f, "pushed ahead of him"); }
            }
            Assert.IsTrue(found, "some seed gives a heavy touch");
            Assert.AreEqual(ActionOutcome.Completed, heavy.Outcome);
        }

        [Test]
        public void BetterControl_MeansMoreCleanFirstTouches()
        {
            int Clean(int control, float ballSpeed)
            {
                int n = 0;
                for (int seed = 1; seed <= 60; seed++)
                {
                    var l = new Lookup().Set("hs1", a => { a.Control = control; a.Technique = control; a.Agility = control; a.Speed = control; return a; });
                    var c = Live(lookup: l, seed: seed);
                    Put(c, TeamSide.Home, "hs1", 0f, 0f, West);
                    PutBall(c, -3f, 0f, ballSpeed, 0f);
                    Step(c, 40);
                    if (c.State.Ball.HasPossession) n++;
                }
                return n;
            }
            Assert.Greater(Clean(95, 14f), Clean(15, 14f));
        }

        [Test]
        public void AFasterBall_IsHarderToControl()
        {
            int Clean(float ballSpeed)
            {
                int n = 0;
                for (int seed = 1; seed <= 60; seed++)
                {
                    var c = Live(seed: seed);
                    Put(c, TeamSide.Home, "hs1", 0f, 0f, West);
                    PutBall(c, -3f, 0f, ballSpeed, 0f);
                    Step(c, 40);
                    if (c.State.Ball.HasPossession) n++;
                }
                return n;
            }
            Assert.Greater(Clean(6f), Clean(22f));
        }

        [Test]
        public void ABallFromBehind_IsHarderToControl_ThanOneFromTheFront()
        {
            int Clean(Vec2 facing)
            {
                int n = 0;
                for (int seed = 1; seed <= 80; seed++)
                {
                    var c = Live(seed: seed);
                    Put(c, TeamSide.Home, "hs1", 0f, 0f, facing);
                    PutBall(c, -3f, 0f, 12f, 0f); // arrives from the west
                    Step(c, 40);
                    if (c.State.Ball.HasPossession) n++;
                }
                return n;
            }
            Assert.Greater(Clean(West), Clean(East), "facing the ball (west) is easier than having it arrive at his back (east)");
        }

        [Test]
        public void ATiredPlayer_ControlsWorse()
        {
            int Clean(float stamina)
            {
                int n = 0;
                for (int seed = 1; seed <= 80; seed++)
                {
                    var c = Live(seed: seed);
                    Put(c, TeamSide.Home, "hs1", 0f, 0f, West);
                    c.State.Home.Find("hs1").Locomotion.CurrentStamina = stamina;
                    PutBall(c, -3f, 0f, 15f, 0f);
                    Step(c, 40);
                    if (c.State.Ball.HasPossession) n++;
                }
                return n;
            }
            Assert.GreaterOrEqual(Clean(100f), Clean(0f));
            float fresh = ActionModel.StaminaFactor(100f, new LocomotionTuning(), new ActionTuning()), tired = ActionModel.StaminaFactor(0f, new LocomotionTuning(), new ActionTuning());
            Assert.AreEqual(1f, fresh, 1e-5f); Assert.Less(tired, fresh); Assert.Greater(tired, 0.85f, "tiredness is a mild penalty, not a lock-out");
        }

        [Test]
        public void TheWeakFoot_ReducesTheQualityOfAKickOrATouch_AndThePreferredFootDoesNot()
        {
            var t = new ActionTuning();
            var right = ActionProfile.Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Right, 20, null);
            var left = ActionProfile.Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Left, 20, null);
            Vec2 facing = new Vec2(0f, 1f);
            Vec2 toRight = new Vec2(1f, 0f), toLeft = new Vec2(-1f, 0f), ahead = new Vec2(0f, 1f);
            ActionFoot f = ActionModel.ChooseFoot(right, facing, toRight, t, out float k);
            Assert.AreEqual(ActionFoot.Right, f); Assert.AreEqual(1f, k);
            f = ActionModel.ChooseFoot(right, facing, toLeft, t, out k);
            Assert.AreEqual(ActionFoot.Left, f); Assert.Less(k, 1f); Assert.AreEqual(t.WeakFootFloor + (1f - t.WeakFootFloor) * 0.2f, k, 1e-5f);
            f = ActionModel.ChooseFoot(right, facing, ahead, t, out k);
            Assert.AreEqual(ActionFoot.Right, f, "straight ahead: the preferred foot"); Assert.AreEqual(1f, k);
            f = ActionModel.ChooseFoot(left, facing, toLeft, t, out k);
            Assert.AreEqual(ActionFoot.Left, f); Assert.AreEqual(1f, k);
            f = ActionModel.ChooseFoot(left, facing, toRight, t, out k);
            Assert.AreEqual(ActionFoot.Right, f); Assert.Less(k, 1f);
            var perfectWeak = ActionProfile.Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Right, 100, null);
            ActionModel.ChooseFoot(perfectWeak, facing, toLeft, t, out k);
            Assert.AreEqual(1f, k, 1e-5f, "a perfect weak foot is as good as the other");
            var worst = ActionProfile.Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Right, 0, null);
            ActionModel.ChooseFoot(worst, facing, toLeft, t, out k);
            Assert.AreEqual(t.WeakFootFloor, k, 1e-5f);
        }

        [Test]
        public void ThePassingKickToTheWeakSide_IsLessAccurate_ThanToThePreferredSide()
        {
            float MeanError(Vec2 dir)
            {
                float sum = 0f;
                for (int seed = 1; seed <= 40; seed++)
                {
                    var l = new Lookup().Set("hs1", null, PreferredFoot.Right, 10);
                    var c = Live(lookup: l, seed: seed);
                    Carrier(c, TeamSide.Home, "hs1", 0f, 0f, new Vec2(0f, 1f));
                    ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, dir, 1f);
                    sum += AngleBetween(r.Direction, dir);
                }
                return sum / 40f;
            }
            Assert.Greater(MeanError(new Vec2(-1f, 0f)), MeanError(new Vec2(1f, 0f)));
        }

        [Test]
        public void ADeliberateControl_AddsABonus_AndIsRefusedWhenTheBallIsFarOrHeld()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 5f, 5f);
            Assert.AreEqual(ActionRejection.BallOutOfReach, Do(c, TeamSide.Home, "hs1", FootballActionKind.Control, East).Rejection);
            PutBall(c, 0.7f, 0f);
            c.Perform(new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.Control, East));
            Assert.IsTrue(c.State.Ball.HasPossession);
            Carrier(c, TeamSide.Away, "as1", 10f, 0f, West);
            Assert.AreEqual(ActionRejection.BallNotAvailable, Do(c, TeamSide.Home, "hs2", FootballActionKind.Control, East).Rejection);
            Assert.AreEqual(ActionOutcome.Completed, Do(c, TeamSide.Away, "as1", FootballActionKind.Control, West).Outcome, "already his");
        }

        [Test]
        public void TheFirstTouchScore_IsMonotonic_InQualitySpeedAndAngle()
        {
            var t = new ActionTuning();
            float S(float q, float v, float a) { return ActionModel.FirstTouchScore(q, 1f, 1f, v, a, 0f, 0f, t); }
            Assert.Greater(S(0.9f, 5f, 0f), S(0.5f, 5f, 0f));
            Assert.Greater(S(0.7f, 5f, 0f), S(0.7f, 15f, 0f));
            Assert.Greater(S(0.7f, 5f, 0f), S(0.7f, 5f, 90f));
            Assert.Greater(S(0.7f, 5f, 100f), S(0.7f, 5f, 170f));
            Assert.Less(S(0.7f, 5f, t.MaxControlAngleDegrees + 1f), S(0.7f, 5f, t.MaxControlAngleDegrees - 1f) - 0.2f, "beyond the maximum angle there is a clear drop");
        }

        // ================= passes =================

        [Test]
        public void AShortPass_GoesWhereItIsAimed_AndIsRecordedEverywhere()
        {
            var c = Live(lookup: Uniform(90, "hs1"));
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.7f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Vec3 v = c.State.Ball.Physics.Velocity;
            Assert.Greater(v.X, 5f); Assert.Less(Lateral(v), v.X * 0.15f); Assert.AreEqual(0f, v.Y);
            Assert.IsFalse(c.State.Ball.HasPossession); Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);
            Assert.AreEqual("hs1", c.State.Ball.LastTouchPlayerId); Assert.AreEqual(FootballActionKind.ShortPass, c.State.Ball.LastAction); Assert.AreEqual("hs1", c.State.Ball.LastActionPlayerId);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.PassPerformed)); Assert.IsTrue(c.Events.Has(GameplayEventKind.BallTouched));
            Assert.AreEqual(r.BallVelocity.X, v.X, 1e-4f);
        }

        [Test]
        public void MorePower_MeansAFasterPass_AndLessPowerASlowerOne()
        {
            float Speed(float power)
            {
                var c = Live();
                Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, power);
                return c.State.Ball.Physics.HorizontalSpeed;
            }
            Assert.Greater(Speed(1f), Speed(0.5f)); Assert.Greater(Speed(0.5f), Speed(0.1f));
            Assert.GreaterOrEqual(Speed(0.0f), ActionDefaults.PassMinSpeed * 0.85f);
            Assert.LessOrEqual(Speed(1f), ActionDefaults.PassMaxSpeed + 0.01f);
        }

        [Test]
        public void ABetterPasser_IsMoreAccurate()
        {
            float MeanError(int passing)
            {
                float sum = 0f;
                for (int seed = 1; seed <= 40; seed++)
                {
                    var l = new Lookup().Set("hs1", a => { a.Passing = passing; a.Technique = passing; a.Control = passing; return a; });
                    var c = Live(lookup: l, seed: seed);
                    Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                    ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 1f);
                    sum += AngleBetween(r.Direction, East);
                }
                return sum / 40f;
            }
            Assert.Less(MeanError(95), MeanError(10));
            Assert.Less(MeanError(95), ActionDefaults.PassMaxError);
        }

        [Test]
        public void AHarderPass_IsLessAccurate_AtTheSameQuality()
        {
            var t = new ActionTuning();
            Assert.Greater(ActionModel.KickError(12f, 0.5f, 1f, 0f, 1f, t), ActionModel.KickError(12f, 0.5f, 0.1f, 0f, 1f, t));
            Assert.AreEqual(0f, ActionModel.KickError(12f, 1f, 1f, 0f, 1f, t), 1e-5f, "a perfect kick has no error");
            Assert.AreEqual(0f, ActionModel.KickError(12f, 0.2f, 0.5f, 0f, 0f, t), 1e-5f, "no noise, no error");
        }

        [Test]
        public void AThroughBall_IsFasterAndRiskierThanAShortPass()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult s = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.8f);
            float shortSpeed = s.BallVelocity.Magnitude;
            var d = Live();
            Carrier(d, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult th = Do(d, TeamSide.Home, "hs1", FootballActionKind.ThroughPass, East, 0.8f);
            Assert.AreEqual(ActionDetail.Through, th.Detail);
            Assert.Greater(th.BallVelocity.Magnitude, shortSpeed * 1.05f);
            Assert.Greater(ActionDefaults.ThroughExtraError, 0f);
            Assert.AreEqual(BallMode.InPass, d.State.Ball.Mode);
        }

        [Test]
        public void AWallPass_IsAPassWithWallMetadata_AndNoAllyAIIsInvolved()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.WallPass, East, 0.6f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Wall, r.Detail);
            GameplayEvent e = c.Events.Log.Last(x => x.Kind == GameplayEventKind.PassPerformed);
            Assert.AreEqual("WallPass", e.Detail); Assert.AreEqual("hs1", e.PlayerId);
            Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);
            Assert.AreEqual(FootballActionKind.WallPass, c.State.Ball.LastAction);
        }

        [Test]
        public void AChip_LiftsTheBall_AndIsAShotEvent()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.Chip, East, 0.7f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.Greater(r.BallVelocity.Y, 3f); Assert.IsFalse(c.State.Ball.Physics.IsGrounded);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.ShotPerformed)); Assert.IsFalse(c.Events.Has(GameplayEventKind.PassPerformed));
            Step(c, 10);
            Assert.Greater(c.State.Ball.Physics.Position.Y, c.BallParameters.Radius + 0.3f, "it goes up");
            Step(c, 100);
            Assert.Greater(c.State.Ball.Physics.Position.X, 3f);
        }

        [Test]
        public void TheKicker_DoesNotRecollectHisOwnPassAtOnce_ButAMateDoes()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c, TeamSide.Home, "hs2", 8f, 0f, West);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.5f);
            Step(c, 10);
            Assert.IsFalse(c.State.Ball.HasPossession, "0.16 s after the kick the ball is still in flight");
            Step(c, 120);
            Assert.IsTrue(c.State.Ball.HasPossession);
            Assert.AreEqual("hs2", c.State.Ball.PossessingPlayerId, "the team-mate controls it");
            Assert.AreEqual("hs1", c.State.Ball.PreviousTouchPlayerId, "the pass is the previous touch (for an assist)");
        }

        [Test]
        public void ARecipientWhoCannotControl_LeavesThePassFree_ForTheOtherTeamToFight()
        {
            var c = Live(lookup: Uniform(1, "hs2"));
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c, TeamSide.Home, "hs2", 6f, 0f, West);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 1f);
            Step(c, 90);
            Assert.AreNotEqual("hs2", c.State.Ball.PossessingPlayerId, "a hopeless first touch does not keep a hard pass");
        }

        [Test]
        public void ALofted_Pass_NeedsItsSkill_AndClearanceIsLoftedToo()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult lob = Do(c, TeamSide.Home, "hs1", FootballActionKind.LongPass, East, 1f);
            Assert.AreEqual(ActionOutcome.Completed, lob.Outcome, "the plain long pass is a normal action");
            Assert.Greater(lob.BallVelocity.Y, 0f);
            var d = Live();
            Carrier(d, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult clear = Do(d, TeamSide.Home, "hs1", FootballActionKind.Clearance, East, 1f);
            Assert.AreEqual(ActionOutcome.Completed, clear.Outcome); Assert.Greater(clear.BallVelocity.Y, 0f);
            Assert.IsTrue(d.Events.Has(GameplayEventKind.ClearancePerformed));
            Assert.GreaterOrEqual(clear.BallVelocity.Magnitude, ActionDefaults.ClearanceMinSpeed * 0.85f);
        }

        [Test]
        public void AKick_RefusesAnOpponentsOrAMatesBall_AndAFarBall()
        {
            var c = Live();
            Carrier(c, TeamSide.Away, "as1", 0f, 0f, West);
            Put(c, TeamSide.Home, "hs1", -0.8f, 0f, East);
            Assert.AreEqual(ActionRejection.BallNotAvailable, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection);
            Carrier(c, TeamSide.Home, "hs2", 5f, 5f, East);
            Assert.AreEqual(ActionRejection.BallNotAvailable, Do(c, TeamSide.Home, "hs3", FootballActionKind.Shot, East).Rejection, "a team-mate has it");
            PutBall(c, -12f, 3f);
            Assert.AreEqual(ActionRejection.BallOutOfReach, Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East).Rejection);
            PutBall(c, -0.5f, 0.4f);
            c.State.Ball.Physics.Position = new Vec3(-0.5f, 2.5f, 0.4f);
            Assert.AreEqual(ActionRejection.BallOutOfReach, Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East).Rejection, "too high");
        }

        [Test]
        public void ALooseBall_CanBeKickedAtOnce_WithoutControllingItFirst()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 0.9f, 0f);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.Greater(c.State.Ball.Physics.Velocity.X, 10f);
        }

        [Test]
        public void AFastKickCooldown_StopsTwoKicksInTheSameMoment_AndLetsTheNextOneThrough()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 0.9f, 0f);
            Assert.AreEqual(ActionOutcome.Completed, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Outcome);
            PutBall(c, 0.9f, 0f);
            Assert.AreEqual(ActionRejection.ActionLocked, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection);
            Step(c, (int)(ActionDefaults.KickCooldownSeconds / Dt) + 2);
            PutBall(c, 0.9f, 0f);
            Assert.AreEqual(ActionOutcome.Completed, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Outcome);
        }

        // ================= shots =================

        [Test]
        public void AShot_IsFasterThanAPass_AndScalesWithPower()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult full = Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            var d = Live();
            Carrier(d, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult soft = Do(d, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 0.2f);
            var e = Live();
            Carrier(e, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult pass = Do(e, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 1f);
            Assert.Greater(full.BallVelocity.Magnitude, soft.BallVelocity.Magnitude);
            Assert.Greater(full.BallVelocity.Magnitude, pass.BallVelocity.Magnitude);
            Assert.LessOrEqual(full.BallVelocity.Magnitude, ActionDefaults.ShotMaxSpeed * (1f + ActionDefaults.ShotLoft) + 0.01f);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.ShotPerformed));
            Assert.AreEqual(BallMode.InShot, c.State.Ball.Mode);
        }

        [Test]
        public void ACloseShot_UsesFinishing_AndAFarShotUsesShooting()
        {
            var finisher = ActionProfile.Resolve(Edit(All(50), a => { a.Finishing = 95; a.Shooting = 30; return a; }), PreferredFoot.Right, 50, null);
            var shooter = ActionProfile.Resolve(Edit(All(50), a => { a.Finishing = 30; a.Shooting = 95; return a; }), PreferredFoot.Right, 50, null);
            Assert.Greater(finisher.ShotQualityClose, shooter.ShotQualityClose);
            Assert.Greater(shooter.ShotQualityFar, finisher.ShotQualityFar);

            float Err(string id, float x, int fin, int sho)
            {
                float sum = 0f;
                for (int seed = 1; seed <= 40; seed++)
                {
                    var l = new Lookup().Set(id, a => { a.Finishing = fin; a.Shooting = sho; return a; });
                    var c = Live(lookup: l, seed: seed);
                    Carrier(c, TeamSide.Home, id, x, 0f, East);
                    sum += AngleBetween(Do(c, TeamSide.Home, id, FootballActionKind.Shot, East, 1f).Direction, East);
                }
                return sum / 40f;
            }
            Assert.Less(Err("hs1", 14f, 95, 20), Err("hs1", 14f, 20, 95), "close to the goal the finisher is better");
            Assert.Less(Err("hs1", -5f, 20, 95), Err("hs1", -5f, 95, 20), "far from it the shooter is better");
        }

        private static PlayerAttributes Edit(PlayerAttributes a, Func<PlayerAttributes, PlayerAttributes> edit) { return edit(a); }

        [Test]
        public void ACurvedShot_BendsInFlight_AndAStraightOneDoesNot()
        {
            float Drift(float curve)
            {
                var c = Live(lookup: Uniform(99, "hs1"));
                Carrier(c, TeamSide.Home, "hs1", -10f, 0f, East);
                var req = new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.CurvedShot, East, 1f) { SkillGranted = true, Curve = curve };
                ActionResult r = c.Perform(req);
                Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
                float firstZ = c.State.Ball.Physics.Velocity.Z;
                Step(c, 40);
                return c.State.Ball.Physics.Velocity.Z - firstZ;
            }
            float straight = Drift(0f), right = Drift(1f), left = Drift(-1f);
            Assert.Less(Math.Abs(straight), 0.2f, "no spin, no bend");
            Assert.Greater(Math.Abs(right), 0.5f); Assert.Greater(Math.Abs(left), 0.5f);
            Assert.Less(right * left, 0f, "opposite curves bend opposite ways");
        }

        [Test]
        public void APlainShot_MayCarryABasicCurve_ButOnlyUpToTheLimit()
        {
            var c = Live(lookup: Uniform(99, "hs1"));
            Carrier(c, TeamSide.Home, "hs1", -10f, 0f, East);
            ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f) { Curve = 1f });
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Curved, r.Detail);
            float basicSpin = Math.Abs(c.State.Ball.Physics.Spin.Y);
            var d = Live(lookup: Uniform(99, "hs1"));
            Carrier(d, TeamSide.Home, "hs1", -10f, 0f, East);
            d.Perform(new ActionRequest(TeamSide.Home, "hs1", FootballActionKind.CurvedShot, East, 1f) { Curve = 1f, SkillGranted = true });
            Assert.Greater(Math.Abs(d.State.Ball.Physics.Spin.Y), basicSpin * 1.5f, "the skill unlocks the full curve");
            Assert.AreEqual(ActionDefaults.BasicCurveLimit, new ActionTuning().BasicCurveLimit);
        }

        [Test]
        public void AShotIntoTheEmptyNet_IsAGoal_AndCreditsTheScorer()
        {
            var c = Live(lookup: Uniform(95, "hs1"));
            Carrier(c, TeamSide.Home, "hs1", 12f, 0f, East);
            c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 12f));
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Step(c, 90);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual("hs1", c.State.Goals[0].ScorerId);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.GoalScored));
            Assert.AreEqual(BallMode.Goal, c.State.Ball.Mode);
        }

        // ================= defending =================

        [Test]
        public void Pressure_IsAnOngoingAction_ThatRunsOutAndCostsStamina()
        {
            var c = Live();
            Put(c, TeamSide.Away, "as1", 3f, 0f, West);
            ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.Pressure, West);
            Assert.AreEqual(ActionOutcome.Accepted, r.Outcome); Assert.AreEqual(ActionDetail.Pressing, r.Detail);
            MatchPlayer p = c.State.Away.Find("as1");
            Assert.IsTrue(p.IsPressing);
            float before = p.Locomotion.CurrentStamina;
            Step(c, 30);
            Assert.Less(p.Locomotion.CurrentStamina, before + 0.0001f);
            Step(c, (int)(ActionDefaults.PressureSeconds / Dt) + 5);
            Assert.IsFalse(p.IsPressing);
        }

        [Test]
        public void Pressure_ReducesTheDribblersControl_OnlyWhenCloseAndOnlyUpToTwoCount()
        {
            var c = Live();
            MatchPlayer carrier = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            float free = c.DribbleControlQuality(carrier);
            Put(c, TeamSide.Away, "as1", 2f, 0f, West);
            Do(c, TeamSide.Away, "as1", FootballActionKind.Pressure, West);
            float one = c.DribbleControlQuality(carrier);
            Put(c, TeamSide.Away, "as2", 0f, 2f, West);
            Do(c, TeamSide.Away, "as2", FootballActionKind.Pressure, West);
            float two = c.DribbleControlQuality(carrier);
            Put(c, TeamSide.Away, "as3", 2f, 2f, West);
            Do(c, TeamSide.Away, "as3", FootballActionKind.Pressure, West);
            float three = c.DribbleControlQuality(carrier);
            Assert.Less(one, free); Assert.Less(two, one); Assert.AreEqual(two, three, 1e-6f, "at most two count");
            Put(c, TeamSide.Away, "as1", 12f, 0f, West); Put(c, TeamSide.Away, "as2", 0f, 12f, West); Put(c, TeamSide.Away, "as3", 12f, 12f, West);
            Assert.AreEqual(free, c.DribbleControlQuality(carrier), 1e-6f, "far pressers do nothing");
        }

        [Test]
        public void AShield_HelpsTheCarrier_AndRunsOut()
        {
            var c = Live();
            MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            float before = c.DribbleControlQuality(p);
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.Shield, East);
            Assert.AreEqual(ActionDetail.Shielding, r.Detail); Assert.IsTrue(p.IsShielding);
            Assert.Greater(c.DribbleControlQuality(p), before);
            Step(c, (int)(ActionDefaults.ShieldSeconds / Dt) + 5);
            Assert.IsFalse(p.IsShielding);
            Assert.AreEqual(ActionRejection.NotInPossession, Do(c, TeamSide.Home, "hs2", FootballActionKind.Shield, East).Rejection);
        }

        [Test]
        public void ATackleWithNobodyToTackle_IsRefused_AndOnlyReachesTheBallCarrier()
        {
            var c = Live();
            Put(c, TeamSide.Away, "as1", 0f, 0f, East);
            PutBall(c, 0.5f, 0f);
            Assert.AreEqual(ActionRejection.NoTarget, Do(c, TeamSide.Away, "as1", FootballActionKind.Tackle, East).Rejection, "no carrier");
            Carrier(c, TeamSide.Home, "hs1", 20f, 8f, East);
            Assert.AreEqual(ActionRejection.NoTarget, Do(c, TeamSide.Away, "as1", FootballActionKind.Tackle, East).Rejection, "carrier too far");
            Assert.AreEqual(ActionRejection.NoTarget, Do(c, TeamSide.Away, "as1", FootballActionKind.PhysicalChallenge, East).Rejection);
            Carrier(c, TeamSide.Away, "as2", 10f, 10f, West);
            Put(c, TeamSide.Away, "as3", 10f, 10.5f, West);
            Assert.AreEqual(ActionRejection.NoTarget, Do(c, TeamSide.Away, "as3", FootballActionKind.Tackle, East).Rejection, "you cannot tackle a team-mate");
        }

        private static (MatchController c, MatchPlayer tackler, MatchPlayer carrier) Duel(int tackler, int carrierSkill, int seed, ActionTuning tuning = null, bool fromBehind = false)
        {
            var l = new Lookup(); l.Players["as1"] = new PlayerDefinition("as1", "as1", 1, PlayerRole.Defender, All(tackler)); l.Players["hs1"] = new PlayerDefinition("hs1", "hs1", 1, PlayerRole.Midfielder, All(carrierSkill));
            var c = Live(lookup: l, seed: seed, tuning: tuning);
            MatchPlayer carrier = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c, TeamSide.Away, "as1", fromBehind ? -0.15f : 1.6f, 0f, fromBehind ? East : West);
            return (c, c.State.Away.Find("as1"), carrier);
        }

        [Test]
        public void ABetterTackler_WinsTheBallMoreOften_AndABetterDribblerHoldsIt()
        {
            int Won(int tackler, int dribbler)
            {
                int n = 0;
                for (int seed = 1; seed <= 60; seed++)
                {
                    var d = Duel(tackler, dribbler, seed);
                    ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
                    if (r.GainedPossession) n++;
                }
                return n;
            }
            Assert.Greater(Won(95, 40), Won(40, 40));
            Assert.Greater(Won(40, 40), Won(40, 95));
            Assert.Greater(Won(95, 20), 40, "a big mismatch is almost always won");
            Assert.Less(Won(20, 95), 5, "and almost always lost");
        }

        [Test]
        public void AWonTackle_GivesTheBallToTheTackler_AndRaisesPossessionAndTackleEvents()
        {
            var d = Duel(99, 1, 1);
            ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Won, r.Detail); Assert.IsTrue(r.GainedPossession);
            Assert.AreEqual("hs1", r.OtherPlayerId);
            Assert.AreEqual(TeamSide.Away, d.c.State.Ball.PossessingTeam); Assert.AreEqual("as1", d.c.State.Ball.PossessingPlayerId);
            Assert.IsTrue(d.c.Events.Has(GameplayEventKind.TacklePerformed));
            Assert.AreEqual(TeamSide.Away, d.c.Events.Log.Last(e => e.Kind == GameplayEventKind.PossessionChanged).Team);
            Assert.Greater(r.StaminaCost, 0f);
        }

        [Test]
        public void ALostTackle_LeavesTheTacklerUnbalancedForAMoment_AndTheCarrierKeepsTheBall()
        {
            var d = Duel(1, 99, 1);
            ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
            Assert.AreEqual(ActionOutcome.Failed, r.Outcome);
            Assert.AreEqual("hs1", d.c.State.Ball.PossessingPlayerId);
            Assert.Greater(d.tackler.ActionLockRemaining, 0f);
            Assert.AreEqual(ActionRejection.ActionLocked, Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West).Rejection);
            Step(d.c, (int)(ActionDefaults.FailedTackleLockSeconds / Dt) + 3);
            Assert.AreEqual(0f, d.tackler.ActionLockRemaining);
        }

        [Test]
        public void AnEvenTackle_OftenKnocksTheBallLoose_ForNobody()
        {
            int loose = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var d = Duel(60, 60, seed);
                ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
                if (r.Detail == ActionDetail.Loose)
                {
                    loose++;
                    Assert.IsFalse(d.c.State.Ball.HasPossession); Assert.AreEqual(BallMode.Deflected, d.c.State.Ball.Mode);
                    Assert.Greater(d.c.State.Ball.Physics.HorizontalSpeed, 1f);
                    Assert.AreEqual("as1", d.c.State.Ball.LastTouchPlayerId);
                }
            }
            Assert.Greater(loose, 5);
        }

        [Test]
        public void ASlidingTackle_MovesThePlayerAndLocksHim_AndCostsMoreStamina()
        {
            var d = Duel(60, 60, 1);
            d.c.PlacePlayer(TeamSide.Away, "as1", new Vec2(1.6f, 0f), Heading(West));
            float stamina = d.tackler.Locomotion.CurrentStamina;
            ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.SlidingTackle, West);
            Assert.AreEqual(ActionDefaults.SlideStaminaCost * 1f, r.StaminaCost, 1e-4f);
            Assert.Greater(ActionDefaults.SlideStaminaCost, ActionDefaults.TackleStaminaCost);
            Assert.IsTrue(d.tackler.IsSliding);
            float x = d.tackler.Locomotion.Position.X;
            Step(d.c, 20);
            Assert.Less(d.tackler.Locomotion.Position.X, x - 0.5f, "he slides along the direction");
            Assert.Greater(d.tackler.ActionLockRemaining, 0f);
            Assert.AreEqual(ActionRejection.ActionLocked, Do(d.c, TeamSide.Away, "as1", FootballActionKind.Pressure, West).Rejection);
            Step(d.c, (int)(ActionDefaults.SlideLockSeconds / Dt) + 5);
            Assert.IsFalse(d.tackler.IsSliding); Assert.AreEqual(0f, d.tackler.ActionLockRemaining);
        }

        [Test]
        public void ASlideWithNobodyThere_StillHappens_AndPokesALooseBall()
        {
            var c = Live();
            Put(c, TeamSide.Away, "as1", 3f, 0f, West);
            ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.SlidingTackle, West);
            Assert.AreEqual(ActionOutcome.Accepted, r.Outcome); Assert.AreEqual(ActionDetail.Sliding, r.Detail);
            var d = Live();
            Put(d, TeamSide.Away, "as1", 3f, 0f, West);
            PutBall(d, 2.2f, 0f);
            ActionResult poke = Do(d, TeamSide.Away, "as1", FootballActionKind.SlidingTackle, West);
            Assert.AreEqual(ActionDetail.Loose, poke.Detail); Assert.Less(d.State.Ball.Physics.Velocity.X, 0f, "the ball goes the way of the slide");
        }

        [Test]
        public void AFailedTackleFromBehind_IsAFoul_InClassicAndNotInArena()
        {
            var tuning = new ActionTuning { StandingFoulMargin = 5f }; // any lost duel from behind is a foul
            var d = Duel(1, 99, 1, tuning, fromBehind: true);
            ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, East);
            Assert.AreEqual(ActionOutcome.Failed, r.Outcome); Assert.AreEqual(ActionDetail.Foul, r.Detail);
            GameplayEvent e = d.c.Events.Log.Last(x => x.Kind == GameplayEventKind.FoulCommitted);
            Assert.AreEqual("as1", e.PlayerId); Assert.AreEqual("hs1", e.OtherPlayerId); Assert.AreEqual(TeamSide.Away, e.Team);

            var l = new Lookup(); l.Players["as1"] = new PlayerDefinition("as1", "as1", 1, PlayerRole.Defender, All(1)); l.Players["hs1"] = new PlayerDefinition("hs1", "hs1", 1, PlayerRole.Midfielder, All(99));
            var arena = Live(MatchSettings.CreateArena(), l, tuning);
            Carrier(arena, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(arena, TeamSide.Away, "as1", -0.15f, 0f, East);
            ActionResult lost = Do(arena, TeamSide.Away, "as1", FootballActionKind.Tackle, East);
            Assert.AreEqual(ActionOutcome.Failed, lost.Outcome); Assert.AreNotEqual(ActionDetail.Foul, lost.Detail);
            Assert.IsFalse(arena.Events.Has(GameplayEventKind.FoulCommitted), "Arena has no fouls");
        }

        [Test]
        public void AFairTackleFromTheFront_IsNeverAFoul()
        {
            var tuning = new ActionTuning { StandingFoulMargin = 5f };
            var d = Duel(1, 99, 1, tuning);
            ActionResult r = Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
            Assert.AreEqual(ActionOutcome.Failed, r.Outcome); Assert.AreNotEqual(ActionDetail.Foul, r.Detail);
            Assert.IsFalse(d.c.Events.Has(GameplayEventKind.FoulCommitted));
        }

        [Test]
        public void ThePhysicalChallenge_IsWonByTheStronger_AndSendsTheBallLoose()
        {
            int won = 0, lost = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var l = new Lookup();
                l.Players["as1"] = new PlayerDefinition("as1", "as1", 1, PlayerRole.Defender, Edit(All(50), a => { a.Strength = 99; return a; }));
                l.Players["hs1"] = new PlayerDefinition("hs1", "hs1", 1, PlayerRole.Midfielder, Edit(All(50), a => { a.Strength = 10; return a; }));
                var c = Live(lookup: l, seed: seed);
                Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                Put(c, TeamSide.Away, "as1", 1.3f, 0f, West);
                ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.PhysicalChallenge, West);
                if (r.Outcome == ActionOutcome.Completed) { won++; Assert.AreNotEqual("hs1", c.State.Ball.PossessingPlayerId, "the carrier lost the ball"); } else lost++;
            }
            Assert.Greater(won, 30); Assert.Less(lost, 10);
            var c2 = Live();
            Carrier(c2, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c2, TeamSide.Away, "as1", 5f, 0f, West);
            Assert.AreEqual(ActionRejection.NoTarget, Do(c2, TeamSide.Away, "as1", FootballActionKind.PhysicalChallenge, West).Rejection, "out of reach");
        }

        [Test]
        public void AnIntercept_TakesAPassThatIsInReach_ButNotYourOwnTeamsPass()
        {
            var c = Live(lookup: Uniform(90, "as1"));
            Put(c, TeamSide.Away, "as1", 5f, 0f, West);
            PutBall(c, 4f, 0.5f, 7f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1");
            c.SetBallMode(BallMode.InPass);
            ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.Intercept, West);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Intercepted, r.Detail); Assert.IsTrue(r.GainedPossession);
            Assert.AreEqual("as1", c.State.Ball.PossessingPlayerId); Assert.IsTrue(c.Events.Has(GameplayEventKind.InterceptionPerformed));

            var d = Live();
            Put(d, TeamSide.Home, "hs2", 5f, 0f, West);
            PutBall(d, 4f, 0.5f, 7f, 0f);
            d.RegisterTouch(TeamSide.Home, "hs1");
            d.SetBallMode(BallMode.InPass);
            Assert.AreEqual(ActionRejection.BallNotAvailable, Do(d, TeamSide.Home, "hs2", FootballActionKind.Intercept, West).Rejection, "it is his own team's pass");
            var e = Live();
            Put(e, TeamSide.Away, "as1", 12f, 0f, West);
            PutBall(e, 0f, 0f, 5f, 0f);
            Assert.AreEqual(ActionRejection.BallOutOfReach, Do(e, TeamSide.Away, "as1", FootballActionKind.Intercept, West).Rejection);
        }

        [Test]
        public void ABlock_DeflectsAHardShot_InsteadOfControllingIt()
        {
            var c = Live(lookup: Uniform(99, "as1"));
            Put(c, TeamSide.Away, "as1", 5f, 0f, West);
            PutBall(c, 4.5f, 0f, 26f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1");
            c.SetBallMode(BallMode.InShot);
            ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.Intercept, West);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Blocked, r.Detail);
            Assert.IsFalse(c.State.Ball.HasPossession); Assert.AreEqual(BallMode.Deflected, c.State.Ball.Mode);
            Assert.Less(c.State.Ball.Physics.Speed, 26f * 0.5f);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallDeflected));
            var b = Live(lookup: Uniform(99, "as1"));
            Put(b, TeamSide.Away, "as1", 5f, 0f, West);
            PutBall(b, 4.5f, 0f, 8f, 0f);
            b.RegisterTouch(TeamSide.Home, "hs1");
            b.SetBallMode(BallMode.InPass);
            Assert.AreEqual(ActionDetail.Blocked, Do(b, TeamSide.Away, "as1", FootballActionKind.Block, West).Detail, "the explicit block never controls");
        }

        [Test]
        public void AClearance_SendsTheBallFarAndHigh_AndAnyoneCanDoItToALooseBall()
        {
            var c = Live(lookup: Uniform(90, "as1"));
            Put(c, TeamSide.Away, "as1", 0f, 0f, West);
            PutBall(c, -0.8f, 0f);
            ActionResult r = Do(c, TeamSide.Away, "as1", FootballActionKind.Clearance, West, 1f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.Greater(Math.Abs(r.BallVelocity.X), 12f); Assert.Greater(r.BallVelocity.Y, 3f);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.ClearancePerformed));
            Assert.AreEqual(FootballActionKind.Clearance, c.State.Ball.LastAction);
            float a = ClearanceError(95), b = ClearanceError(10);
            Assert.Less(a, b);
        }

        private static float ClearanceError(int quality)
        {
            float sum = 0f;
            for (int seed = 1; seed <= 40; seed++)
            {
                var c = Live(lookup: Uniform(quality, "as1"), seed: seed);
                Put(c, TeamSide.Away, "as1", 0f, 0f, West);
                PutBall(c, -0.8f, 0f);
                sum += AngleBetween(Do(c, TeamSide.Away, "as1", FootballActionKind.Clearance, West, 1f).Direction, West);
            }
            return sum / 40f;
        }

        // ================= goalkeeper =================

        private static Lookup WithKeeper(int handling, int reflexes)
        {
            var l = new Lookup();
            l.Players["as0"] = new PlayerDefinition("as0", "as0", 1, PlayerRole.Goalkeeper, All(60));
            l.Keepers["as0"] = new GoalkeeperProfile("as0", GoalkeeperStyle.ShotStopper, reflexes, handling, 70, 70, 70, 70, 70, 70);
            return l;
        }

        private int Saved(Lookup l, float shotSpeed, int trials, Action<MatchController> arrange = null)
        {
            int n = 0;
            for (int seed = 1; seed <= trials; seed++)
            {
                var c = Live(lookup: l, seed: seed);
                c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 0f), Heading(West));
                PutBall(c, 12f, 0.5f, shotSpeed, 0f);
                c.RegisterTouch(TeamSide.Home, "hs1");
                c.SetBallMode(BallMode.InShot);
                arrange?.Invoke(c);
                Step(c, 60);
                if (c.State.Score.Home == 0) n++;
            }
            return n;
        }

        [Test]
        public void TheGoalkeeper_CatchesASlowShot_UsingTheSameFirstTouchSystem()
        {
            var c = Live(lookup: WithKeeper(95, 95));
            c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 0f), Heading(West));
            PutBall(c, 12f, 0.5f, 9f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1");
            c.SetBallMode(BallMode.InShot);
            var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
            Step(c, 90);
            Assert.AreEqual("as0", c.State.Ball.PossessingPlayerId);
            Assert.IsTrue(seen.Any(r => r.Kind == FootballActionKind.GoalkeeperCatch && r.Detail == ActionDetail.Caught));
            Assert.AreEqual(0, c.State.Score.Home);
        }

        [Test]
        public void ABetterKeeper_SavesMore_AndAHardShotBeatsABadKeeper()
        {
            int good = Saved(WithKeeper(95, 95), 24f, 40), bad = Saved(WithKeeper(5, 5), 24f, 40);
            Assert.Greater(good, bad);
            Assert.Greater(Saved(WithKeeper(95, 95), 8f, 20), Saved(WithKeeper(95, 95), 28f, 20) - 1);
        }

        [Test]
        public void AParriedBall_GoesAwayFromGoal_AndIsAGoalkeeperSave()
        {
            bool found = false;
            for (int seed = 1; seed <= 60 && !found; seed++)
            {
                var c = Live(lookup: WithKeeper(60, 90), seed: seed);
                c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 0f), Heading(West));
                PutBall(c, 12f, 0.5f, 22f, 0f);
                c.RegisterTouch(TeamSide.Home, "hs1");
                c.SetBallMode(BallMode.InShot);
                var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
                Step(c, 40);
                foreach (ActionResult r in seen) if (r.Kind == FootballActionKind.GoalkeeperSave)
                    {
                        found = true; Assert.AreEqual(ActionDetail.Parried, r.Detail); Assert.IsFalse(c.State.Ball.HasPossession);
                        Assert.IsTrue(c.Events.Has(GameplayEventKind.BallDeflected)); Assert.Less(r.BallVelocity.X, 0f, "back towards the pitch"); Assert.Less(r.Direction.X, 0f);
                        Assert.AreEqual(0, c.State.Score.Home);
                    }
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void AFieldPlayerInGoal_IsAWorseKeeperThanASpecialist_UsingDerivedCapabilities()
        {
            var l = new Lookup();
            l.Players["as1"] = new PlayerDefinition("as1", "as1", 1, PlayerRole.Midfielder, All(70));
            l.Players["as0"] = new PlayerDefinition("as0", "as0", 1, PlayerRole.Goalkeeper, All(40));
            l.Keepers["as0"] = new GoalkeeperProfile("as0", GoalkeeperStyle.ShotStopper, 85, 85, 80, 80, 70, 70, 70, 70);
            ActionProfile real = ActionProfile.Resolve(All(40), PreferredFoot.Right, 50, l.Keepers["as0"]);
            ActionProfile derived = ActionProfile.Resolve(All(70), PreferredFoot.Right, 50, null);
            Assert.Greater(real.KeeperHandling, derived.KeeperHandling);
            Assert.Greater(real.KeeperReflexes, derived.KeeperReflexes);
            Assert.AreEqual((70f - 1f) / 98f * 0.75f, derived.KeeperHandling, 0.02f, "the emergency factor of the architecture (0.75)");
        }

        [Test]
        public void AFieldPlayerWhoTakesTheGloves_ActsAsAKeeper_WithoutBecomingARealOne()
        {
            var c = Live(new MatchSettings { Mode = GameMode.Classic, SubstitutionLimit = 0, DurationSeconds = 180f });
            c.IssueCard(TeamSide.Away, "as0", CardKind.Red);
            Assert.AreEqual(EmergencyKeeperResult.Assigned, c.TryAssignEmergencyKeeper(TeamSide.Away, "as1"));
            MatchPlayer gk = c.State.Away.Find("as1");
            Assert.IsTrue(gk.InGoal); Assert.IsTrue(gk.IsEmergencyKeeper); Assert.IsFalse(gk.IsGoalkeeper);
            c.ConfirmRestartReady();
            Put(c, TeamSide.Away, "as1", 19f, 0f, West);
            PutBall(c, 17f, 0f, 6f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1");
            c.SetBallMode(BallMode.InShot);
            var seen = new List<ActionResult>(); c.ActionPerformed += seen.Add;
            Step(c, 60);
            Assert.IsTrue(seen.Any(r => r.PlayerId == "as1" && (r.Kind == FootballActionKind.GoalkeeperCatch || r.Kind == FootballActionKind.GoalkeeperSave)), "the emergency keeper uses the keeper path");
            Assert.AreEqual(ActionFoot.Body, seen.First(r => r.PlayerId == "as1").Foot);
        }

        [Test]
        public void GoalkeeperDistribution_NeedsTheKeeperInGoalWithTheBall_AndCanBeShortOrLong()
        {
            var c = Live(lookup: WithKeeper(80, 80));
            Put(c, TeamSide.Away, "as0", 19f, 0f, West);
            Assert.AreEqual(ActionRejection.NotInPossession, Do(c, TeamSide.Away, "as0", FootballActionKind.GoalkeeperDistribution, West).Rejection);
            Carrier(c, TeamSide.Away, "as1", 10f, 5f, West);
            Assert.AreEqual(ActionRejection.NotGoalkeeper, Do(c, TeamSide.Away, "as1", FootballActionKind.GoalkeeperDistribution, West).Rejection, "a field player is not a keeper");
            Carrier(c, TeamSide.Away, "as0", 19f, 0f, West);
            ActionResult shortThrow = Do(c, TeamSide.Away, "as0", FootballActionKind.GoalkeeperDistribution, West, 0.6f);
            Assert.AreEqual(ActionOutcome.Completed, shortThrow.Outcome); Assert.AreEqual(0f, shortThrow.BallVelocity.Y); Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);

            var d = Live(lookup: WithKeeper(80, 80));
            Carrier(d, TeamSide.Away, "as0", 19f, 0f, West);
            ActionResult lng = d.Perform(new ActionRequest(TeamSide.Away, "as0", FootballActionKind.GoalkeeperDistribution, West, 1f) { Long = true });
            Assert.Greater(lng.BallVelocity.Y, 0f); Assert.Greater(lng.BallVelocity.Magnitude, shortThrow.BallVelocity.Magnitude);
            Assert.IsTrue(d.Events.Has(GameplayEventKind.PassPerformed));
        }

        [Test]
        public void TheGoalkeeperInput_GoesThroughTheSameSystem_AsAnyPlayer()
        {
            var c = Live(lookup: WithKeeper(80, 80));
            Carrier(c, TeamSide.Away, "as0", 19f, 0f, West);
            ActionResult r = c.PerformInput(TeamSide.Away, "as0", new ActionInput(ActionButton.B, ActionGesture.SwipeUp, West, 1f));
            Assert.AreEqual(FootballActionKind.GoalkeeperDistribution, r.Kind); Assert.Greater(r.BallVelocity.Y, 0f, "swipe up = the long kick");
            var d = Live(lookup: WithKeeper(80, 80));
            Carrier(d, TeamSide.Away, "as0", 19f, 0f, West);
            Assert.AreEqual(FootballActionKind.GoalkeeperDistribution, d.PerformInput(TeamSide.Away, "as0", new ActionInput(ActionButton.B, ActionGesture.Tap, West, 0.5f)).Kind);
        }

        [Test]
        public void TheGoalkeeperRush_IsAContract_AcceptedOnlyForTheKeeperWithTheBallNear()
        {
            var c = Live(lookup: WithKeeper(80, 80));
            c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 0f), Heading(West));
            PutBall(c, 12f, 0f, -3f, 0f);
            ActionResult r = Do(c, TeamSide.Away, "as0", FootballActionKind.GoalkeeperRush, West);
            Assert.AreEqual(ActionOutcome.Accepted, r.Outcome); Assert.AreEqual(ActionDetail.Rushing, r.Detail);
            Assert.Greater(c.State.Away.Find("as0").RushRemaining, 0f);
            Assert.AreEqual(ActionDefaults.RushStaminaCost, r.StaminaCost, 1e-4f);
            Assert.AreEqual(ActionRejection.NotGoalkeeper, Do(c, TeamSide.Away, "as1", FootballActionKind.GoalkeeperRush, West).Rejection);
            PutBall(c, -10f, 0f);
            Assert.AreEqual(ActionRejection.BallOutOfReach, Do(c, TeamSide.Away, "as0", FootballActionKind.GoalkeeperRush, West).Rejection, "the ball is too far");
        }

        // ================= stamina =================

        [Test]
        public void EveryActionCostsItsDefaultStamina_AndPhase3FormulasAreUntouched()
        {
            float Cost(FootballActionKind kind, float power = 1f)
            {
                var c = Live();
                Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                return Do(c, TeamSide.Home, "hs1", kind, East, power).StaminaCost;
            }
            Assert.AreEqual(ActionDefaults.PassStaminaCost, Cost(FootballActionKind.ShortPass, 1f), 1e-4f);
            Assert.AreEqual(ActionDefaults.PassStaminaCost * 0.5f, Cost(FootballActionKind.ShortPass, 0f), 1e-4f, "half at zero power");
            Assert.AreEqual(ActionDefaults.ShotStaminaCost, Cost(FootballActionKind.Shot), 1e-4f);
            Assert.AreEqual(ActionDefaults.ClearanceStaminaCost, Cost(FootballActionKind.Clearance), 1e-4f);
            Assert.Greater(Cost(FootballActionKind.Shot), Cost(FootballActionKind.ShortPass));
            var t = new LocomotionTuning();
            Assert.AreEqual(1.0f, LocomotionStamina.Performance(100f, t)); Assert.AreEqual(0.40f, LocomotionStamina.Performance(0f, t), 1e-5f);
            Assert.AreEqual(3.0f, LocomotionDefaults.BaseStaminaDrain); Assert.AreEqual(2.0f, LocomotionDefaults.BaseStaminaRecovery);
        }

        [Test]
        public void StaminaNeverGoesBelowZero_EvenAfterManyActions()
        {
            var c = Live();
            MatchPlayer p = c.State.Home.Find("hs1");
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            p.Locomotion.CurrentStamina = 1f;
            for (int i = 0; i < 20; i++)
            {
                PutBall(c, 0.9f, 0f);
                Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
                Step(c, 20);
            }
            Assert.GreaterOrEqual(p.Locomotion.CurrentStamina, 0f);
            Assert.LessOrEqual(p.Locomotion.CurrentStamina, 100f);
        }

        [Test]
        public void ATiredPlayer_KicksWorse_ButStillKicks()
        {
            float Err(float stamina)
            {
                float sum = 0f;
                for (int seed = 1; seed <= 50; seed++)
                {
                    var c = Live(seed: seed);
                    Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                    c.State.Home.Find("hs1").Locomotion.CurrentStamina = stamina;
                    ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 1f);
                    Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
                    sum += AngleBetween(r.Direction, East);
                }
                return sum / 50f;
            }
            Assert.Greater(Err(0f), Err(100f));
        }

        // ================= match core integration =================

        [Test]
        public void TheCompleteFlow_InputToBallToStateToEvents_PassReceiveDribbleShootScore()
        {
            var l = Uniform(92, "hs1", "hs2");
            var c = Live(lookup: l);
            c.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 14f));
            Carrier(c, TeamSide.Home, "hs1", -8f, 0f, East);
            Put(c, TeamSide.Home, "hs2", 2f, 0f, West);

            ActionResult pass = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, East, 0.8f));
            Assert.AreEqual(FootballActionKind.ShortPass, pass.Kind); Assert.AreEqual(ActionOutcome.Completed, pass.Outcome);
            Step(c, 120);
            Assert.AreEqual("hs2", c.State.Ball.PossessingPlayerId, "received");

            c.PlacePlayer(TeamSide.Home, "hs2", c.State.Home.Find("hs2").Locomotion.Position, Heading(East));
            for (int i = 0; i < 60 * 4; i++) { c.SetPlayerInput(TeamSide.Home, "hs2", East); c.Advance(Dt); if (c.State.Phase != MatchPhase.Playing || c.State.Home.Find("hs2").Locomotion.Position.X > 11f) break; }
            Assert.AreEqual("hs2", c.State.Ball.PossessingPlayerId, "dribbled");
            Assert.Greater(c.State.Home.Find("hs2").Locomotion.Position.X, 5f);

            c.SetPlayerInput(TeamSide.Home, "hs2", Vec2.Zero);
            Step(c, 30);
            ActionResult shot = c.PerformInput(TeamSide.Home, "hs2", new ActionInput(ActionButton.A, ActionGesture.SwipeDown, East, 1f));
            Assert.AreEqual(FootballActionKind.Shot, shot.Kind);
            Step(c, 120);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual("hs2", c.State.Goals[0].ScorerId);
            Assert.IsNull(c.State.Goals[0].AssistId, "he controlled the ball himself after the pass, so the previous touch is his own: no assist (Phase 1 rule)");
        }

        [Test]
        public void AnArenaBall_StillBouncesOffTheWalls_AndIsTouchedAfterwards()
        {
            var c = Live(MatchSettings.CreateArena());
            Put(c, TeamSide.Home, "hs1", 0f, 8f, East);
            PutBall(c, 0f, 11f, 0f, 12f);
            Step(c, 20);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallBouncedOffWall));
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.FoulCommitted));
        }

        [Test]
        public void TheAutomaticTouches_AreRaisedThroughTheSameEventAsRequestedActions()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 0.5f, 0f);
            int n = 0; c.ActionPerformed += r => n++;
            Step(c, 2);
            Assert.AreEqual(1, n);
            Assert.AreEqual(FootballActionKind.Control, c.LastActionResult.Kind);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.5f);
            Assert.AreEqual(2, n);
        }

        [Test]
        public void TheClosestPlayer_WinsTheTouch_AndOnlyOnePlayerTouchesPerStep()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0.8f, East);
            Put(c, TeamSide.Away, "as1", 0f, -0.4f, East);
            PutBall(c, 0f, 0f);
            Step(c, 1);
            Assert.AreEqual("as1", c.State.Ball.LastTouchPlayerId, "he was closer");
            Assert.AreEqual(1, c.Events.Count(GameplayEventKind.BallTouched));
        }

        [Test]
        public void TheOpponentWhoStealsTheBall_CausesPossessionChanged()
        {
            var d = Duel(99, 1, 1);
            Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
            Assert.AreEqual(2, d.c.Events.Count(GameplayEventKind.PossessionChanged), "first his, then theirs");
        }

        [Test]
        public void SubstitutedAndDismissedPlayers_CannotActOrTouchTheBall()
        {
            var c = Live();
            c.Pause(); c.Resume();
            c.IssueCard(TeamSide.Home, "hs1", CardKind.Red);
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East);
            c.State.Home.Find("hs1").Locomotion.Position = new Vec2(0f, 0f);
            PutBall(c, 0.4f, 0f);
            Assert.AreEqual(ActionRejection.PlayerNotActive, Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East).Rejection);
            Step(c, 2);
            Assert.AreNotEqual("hs1", c.State.Ball.LastTouchPlayerId);
        }

        // ================= match: out of play, kick-off, modes, incompatible states =================

        [Test]
        public void ABallKickedOverTheEndLine_IsOutInClassic_WithItsEvents_AndTheBallIsDead()
        {
            var c = Live(MatchSettings.CreateClassic());
            Put(c, TeamSide.Home, "hs1", 15f, 8f, East);
            PutBall(c, 15.8f, 8f);
            Assert.AreEqual(ActionOutcome.Completed, Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f).Outcome);
            Step(c, 120);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallOutOfBounds));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(BallMode.Dead, c.State.Ball.Mode);
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs2", FootballActionKind.Pressure, East).Rejection);
        }

        [Test]
        public void TheSameKick_InArena_BouncesOffTheWall_AndNothingGoesOut()
        {
            var c = Live(MatchSettings.CreateArena());
            Put(c, TeamSide.Home, "hs1", 15f, 8f, East);
            PutBall(c, 15.8f, 8f);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Step(c, 120);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallBouncedOffWall));
            Assert.IsFalse(c.Events.Has(GameplayEventKind.BallOutOfBounds));
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
        }

        [Test]
        public void TheKickOff_IsTakenWithTheNormalActions_OnceTheMatchIsLive()
        {
            var c = new MatchController(new FieldDimensions(), MatchSettings.CreateClassic(), Squad("h"), Squad("a"));
            c.Start();
            Park(c);
            Assert.AreEqual(RestartKind.KickOff, c.State.PendingRestart.Kind);
            Put(c, TeamSide.Home, "hs1", -0.8f, 0f, East);
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East).Rejection, "not before the whistle");
            Assert.IsTrue(c.ConfirmRestartReady());
            ActionResult r = Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.5f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.AreEqual(TeamSide.Home, c.State.Ball.LastTouchTeam);
        }

        [Test]
        public void NoActionAndNoTouch_IsPossibleInAnIncompatibleState()
        {
            var c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 0.5f, 0f);
            c.Pause();
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East).Rejection);
            Step(c, 5);
            Assert.IsFalse(c.State.Ball.HasPossession, "a paused match touches nothing");
            c.Resume();
            Step(c, 5);
            Assert.IsTrue(c.State.Ball.HasPossession);

            var f = Live(lookup: Uniform(95, "hs1"));
            Carrier(f, TeamSide.Home, "hs1", 12f, 0f, East);
            f.PlacePlayer(TeamSide.Away, "as0", new Vec2(19f, 12f));
            Do(f, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Step(f, 90);
            Assert.AreEqual(MatchPhase.GoalCelebration, f.State.Phase);
            Assert.AreEqual(ActionRejection.MatchNotLive, Do(f, TeamSide.Home, "hs2", FootballActionKind.Pressure, East).Rejection, "celebrating");
        }

        // ================= integration: locomotion + ball =================

        [Test]
        public void ASprintingPlayer_ControlsAHardBallWorse_ThanAStandingOne()
        {
            int Clean(bool sprinting)
            {
                int n = 0;
                for (int seed = 1; seed <= 80; seed++)
                {
                    var c = Live(seed: seed);
                    Put(c, TeamSide.Home, "hs1", 2f, 0f, West);
                    PutBall(c, -3f, 0f, 14f, 0f);
                    if (sprinting) { c.State.Home.Find("hs1").Locomotion.CurrentSpeed = 6f; c.State.Home.Find("hs1").Locomotion.Velocity = West * 6f; }
                    for (int i = 0; i < 90 && !c.State.Ball.HasPossession; i++)
                    {
                        if (sprinting) c.SetPlayerInput(TeamSide.Home, "hs1", West);
                        c.Advance(Dt);
                    }
                    if (c.State.Ball.HasPossession) n++;
                }
                return n;
            }
            Assert.Greater(Clean(false), Clean(true), "running into the ball adds its speed to the difficulty");
        }

        [Test]
        public void MovingWhileKicking_IsAllowed_TheKickDoesNotStopThePlayer_ButCostsStamina()
        {
            var c = Live();
            MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", -10f, 0f, East);
            for (int i = 0; i < 90; i++) { c.SetPlayerInput(TeamSide.Home, "hs1", East); c.Advance(Dt); }
            float speed = p.Locomotion.CurrentSpeed, stamina = p.Locomotion.CurrentStamina;
            Assert.Greater(speed, 3f);
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, East, 1f));
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome, "a pass on the run");
            Assert.AreEqual(speed, p.Locomotion.CurrentSpeed, 1e-4f, "the kick itself does not brake him");
            Assert.AreEqual(stamina - r.StaminaCost, p.Locomotion.CurrentStamina, 1e-3f);
            c.SetPlayerInput(TeamSide.Home, "hs1", East);
            c.Advance(Dt);
            Assert.Greater(p.Locomotion.CurrentSpeed, 3f);
        }

        [Test]
        public void ARunningShooter_CanShoot_AndTheShotComesFromTheBallNotFromThePlayerSpeed()
        {
            var c = Live(lookup: Uniform(80, "hs1"));
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            for (int i = 0; i < 60; i++) { c.SetPlayerInput(TeamSide.Home, "hs1", East); c.Advance(Dt); }
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.A, ActionGesture.SwipeDown, East, 1f));
            Assert.AreEqual(FootballActionKind.Shot, r.Kind); Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.LessOrEqual(r.BallVelocity.Magnitude, ActionDefaults.ShotMaxSpeed * 1.07f);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.ShotPerformed));
        }

        [Test]
        public void ActionPlusStamina_TheActionsDrainOnTopOfRunning_AndRecoveryStillWorks()
        {
            var c = Live();
            MatchPlayer p = c.State.Home.Find("hs1");
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            p.Locomotion.CurrentStamina = 50f;
            PutBall(c, 0.9f, 0f);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.Shot, East, 1f);
            Assert.AreEqual(50f - ActionDefaults.ShotStaminaCost, p.Locomotion.CurrentStamina, 1e-3f);
            c.SetPlayerInput(TeamSide.Home, "hs1", Vec2.Zero);
            Step(c, 120);
            Assert.Greater(p.Locomotion.CurrentStamina, 50f - ActionDefaults.ShotStaminaCost, "standing still recovers, as in Phase 3");
        }

        [Test]
        public void ActionPlusPossession_AKickReleasesIt_ATackleTransfersIt_AControlTakesIt()
        {
            var c = Live();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 0.4f);
            Assert.IsFalse(c.State.Ball.HasPossession);
            Assert.IsNull(c.BallCarrier);
            var d = Duel(99, 1, 3);
            Assert.AreEqual(TeamSide.Home, d.c.State.Ball.PossessingTeam);
            Do(d.c, TeamSide.Away, "as1", FootballActionKind.Tackle, West);
            Assert.AreEqual(TeamSide.Away, d.c.State.Ball.PossessingTeam);
            Assert.AreSame(d.tackler, d.c.BallCarrier);
        }

        // ================= determinism =================

        private static List<string> Script(int seed)
        {
            var c = Live(lookup: Uniform(75, "hs1", "hs2", "as1"), seed: seed);
            var log = new List<string>();
            c.ActionPerformed += r => log.Add(r.ToString() + "|" + r.BallVelocity.X.ToString("R") + "|" + r.BallVelocity.Z.ToString("R") + "|" + r.Quality.ToString("R"));
            Carrier(c, TeamSide.Home, "hs1", -6f, 0f, East);
            Put(c, TeamSide.Home, "hs2", 4f, 1f, West);
            Put(c, TeamSide.Away, "as1", 0f, -2f, East);
            c.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, East));
            for (int i = 0; i < 60 * 6; i++)
            {
                c.SetPlayerInput(TeamSide.Home, "hs1", East);
                c.SetPlayerInput(TeamSide.Away, "as1", new Vec2(-1f, 0.3f));
                if (i == 90) c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, East, 0.7f));
                if (i == 150) c.PerformInput(TeamSide.Away, "as1", new ActionInput(ActionButton.A, ActionGesture.SwipeDown, East, 1f));
                c.Advance(Dt);
            }
            foreach (GameplayEvent e in c.Events.Log) log.Add(e.ToString() + "@" + e.MatchTime.ToString("R"));
            log.Add(c.State.Ball.Physics.Position.X.ToString("R") + "," + c.State.Ball.Physics.Position.Z.ToString("R"));
            return log;
        }

        [Test]
        public void TheSameSeedAndInputs_GiveTheSameMatch_ToTheBit()
        {
            List<string> a = Script(7), b = Script(7);
            Assert.Greater(a.Count, 8);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void ADifferentSeed_ChangesTheRandomOutcomes()
        {
            var logs = new HashSet<string>();
            for (int seed = 1; seed <= 6; seed++) logs.Add(string.Join("\n", Script(seed)));
            Assert.Greater(logs.Count, 1);
        }

        [Test]
        public void TheOrderOfTheActions_IsTheOrderOfTheRandomDraws_SoReplayingNeedsNoExtraState()
        {
            ActionResult First(int seed) { var c = Live(seed: seed); Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East); return Do(c, TeamSide.Home, "hs1", FootballActionKind.ShortPass, East, 1f); }
            Assert.AreEqual(First(3).Direction.Y, First(3).Direction.Y);
            Assert.AreEqual(First(3).BallVelocity.Z, First(3).BallVelocity.Z);
            Assert.AreNotEqual(First(3).Direction.Y, First(4).Direction.Y);
        }

        // ================= guards =================

        private static string CoreDir() { string core = DifficultyGuardTests.FindCoreSourceDirectory(); if (core == null) Assert.Ignore("Core sources not reachable."); return core; }

        [Test]
        public void TheActionCore_UsesNoUnityNoWallClockNoSystemRandom()
        {
            var files = Directory.GetFiles(Path.Combine(CoreDir(), "Actions"), "*.cs").Concat(new[] { Path.Combine(CoreDir(), "Match", "MatchController.Actions.cs") });
            foreach (string file in files)
            {
                string src = File.ReadAllText(file), name = Path.GetFileName(file);
                foreach (string bad in new[] { "UnityEngine", "Time.time", "Time.deltaTime", "DateTime", "Stopwatch", "System.Random", "new Random(", "Rigidbody", "Guid.NewGuid", "Environment.TickCount" }) Assert.IsFalse(src.Contains(bad), name + " uses " + bad);
                foreach (string notYet in new[] { "Coach", "Formation", "Tactic", "RosterGenerator", "PlayerLibrary" }) Assert.IsFalse(src.Contains(notYet), name + " must not know " + notYet);
            }
        }

        [Test]
        public void NoNewAttribute_TheProfileIsDerived_AndNothingStoresAttributes()
        {
            Assert.AreEqual(12, PlayerAttributeInfo.Count);
            foreach (FieldInfo f in typeof(ActionProfile).GetFields())
            {
                Assert.AreNotEqual(typeof(int), f.FieldType, f.Name);
                Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, f.Name);
            }
            foreach (FieldInfo f in typeof(MatchPlayer).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, f.Name);
            foreach (PropertyInfo f in typeof(MatchPlayer).GetProperties()) Assert.AreNotEqual(typeof(PlayerAttributes), f.PropertyType, f.Name);
            Assert.AreEqual(1, typeof(PlayerAttributes).GetField("Reaction").GetCustomAttributes(typeof(ObsoleteAttribute), false).Length, "Reaction stays the legacy value it was");
        }

        [Test]
        public void TheNewActionKinds_AreAppendedAndAnimated_AndTheOldOnesKeepTheirNumbers()
        {
            Assert.AreEqual(22, (int)FootballActionKind.GoalkeeperRush);
            Assert.AreEqual(23, (int)FootballActionKind.Pressure); Assert.AreEqual(24, (int)FootballActionKind.SlidingTackle);
            Assert.AreEqual(25, (int)FootballActionKind.PhysicalChallenge); Assert.AreEqual(26, (int)FootballActionKind.Clearance);
            Assert.AreEqual(9, (int)FootballActionKind.Tackle); Assert.AreEqual(10, (int)FootballActionKind.Intercept); Assert.AreEqual(11, (int)FootballActionKind.Block);
        }

        [Test]
        public void TheUnityPresenters_OnlyMirrorTheCore_AndNeverDecideAnything()
        {
            string gameplay = Path.Combine(CoreDir(), "..", "Gameplay");
            foreach (string file in Directory.GetFiles(gameplay, "*.cs"))
            {
                string name = Path.GetFileName(file);
                if (name != "MatchBridge.cs" && name != "BallController.cs" && name != "ActionBridge.cs") continue;
                string src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("Random."), name + " draws randomness");
                Assert.IsFalse(src.Contains("PossessingPlayerId ="), name + " writes possession");
            }
        }

        [Test]
        public void TheFormulaNumbers_AreCentralised_NoLooseTuningLiteralsInTheActionCode()
        {
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f");
            foreach (string file in new[] { Path.Combine(CoreDir(), "Match", "MatchController.Actions.cs"), Path.Combine(CoreDir(), "Actions", "ActionModel.cs") })
            {
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//") || line.StartsWith("///")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line))
                    {
                        bool allowed = (m.Value == "0.5f" && line.Contains("field.Length")) || (m.Value == "180f" && line.Contains("angleDegrees")) || m.Value == "999f" || m.Value == "0.0f";
                        Assert.IsTrue(allowed, Path.GetFileName(file) + ": loose number " + m.Value + " in: " + line);
                    }
                }
            }
        }

        [Test]
        public void TheUnityBridge_DoesNotLetTheOlderTouchStealTheBall_NorHandTheKickOffToWhoeverIsFirst()
        {
            string src = File.ReadAllText(Path.Combine(CoreDir(), "..", "Gameplay", "MatchBridge.cs"));
            Assert.IsTrue(src.Contains("if (HasPlayers) return;"), "RequestTouch must yield to the Core when it has players");
            Assert.IsTrue(src.Contains("if (!HasPlayers) match.ConfirmRestartReady();"), "with squads the host places the players first");
        }

        [Test]
        public void TheEventKinds_OfPhase4_AreAppended_AndTheArenaKeepsThem()
        {
            Assert.AreEqual(25, (int)GameplayEventKind.MatchEnded);
            Assert.Greater((int)GameplayEventKind.BallTouched, (int)GameplayEventKind.MatchEnded);
            var arena = GameModeRules.For(GameMode.Arena);
            foreach (GameplayEventKind k in new[] { GameplayEventKind.BallTouched, GameplayEventKind.PassPerformed, GameplayEventKind.ShotPerformed, GameplayEventKind.BallDeflected, GameplayEventKind.TacklePerformed, GameplayEventKind.InterceptionPerformed, GameplayEventKind.ClearancePerformed })
                Assert.IsTrue(arena.Allows(k), k.ToString());
            Assert.IsFalse(arena.Allows(GameplayEventKind.FoulCommitted));
        }
    }
}
