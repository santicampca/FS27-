using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 20: the short pass is an ordinary pass. It used to fall into the goalkeeper-distribution branch of the kick and was rated with the emergency-keeper profile (x0.75 of every other pass).
    /// These tests pin: the same quality as the other passes, the pass attribute, the rivals close to the passer, the body orientation (the weak foot), the receiver, and that the person's pass and the AI's are one model.
    /// Classic field 40 x 25, Home attacks +X, ball radius 0.2.
    /// </summary>
    public class AiShortPassTests
    {
        private const float Dt = LocomotionDefaults.FixedDeltaTime;
        private static readonly Vec2 East = new Vec2(1f, 0f), West = new Vec2(-1f, 0f);

        // ================= scenery =================

        private sealed class Lookup : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Players = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Players.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private static PlayerAttributes Attributes(int all, int passing = -1)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = a.Acceleration = a.Stamina = a.BallControl = a.Passing = a.Shooting = a.Defense = a.Strength = a.Agility = a.Finishing = a.Dribbling = a.Technique = all;
            if (passing >= 0) a.Passing = passing;
            return a;
        }

        private static Lookup LookupOf(PlayerAttributes passer, PreferredFoot foot = PreferredFoot.Right, int weakFoot = 50)
        {
            var l = new Lookup();
            l.Players["hs1"] = new PlayerDefinition("hs1", "hs1", 1, PlayerRole.Midfielder, passer) { PreferredFoot = foot, WeakFootQuality = weakFoot };
            foreach (string id in new[] { "hs2", "hs3", "hs4", "hs5", "as1", "as2", "as3", "as4", "as5" }) l.Players[id] = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, Attributes(60));
            return l;
        }

        private static SquadMember[] Starters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false, 100f)); return l.ToArray(); }
        private static SquadMember[] Bench(string prefix) { return new[] { new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f), new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f) }; }

        /// <summary>A live Classic match with everybody parked far away and the passer "hs1" at the centre with the ball at his feet, facing east.</summary>
        private static MatchController Scene(Lookup lookup, int seed = 1, GameMode mode = GameMode.Classic)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            s.Seed = seed;
            var c = new MatchController(new FieldDimensions(), s, new MatchSquad(Starters("h"), Bench("h")), new MatchSquad(Starters("a"), Bench("a")), null, "m", null, lookup, null);
            c.Start();
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -16f, -10f + i * 4f), East.ToHeading());
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 16f, -10f + i * 4f), West.ToHeading());
            }
            Assert.IsTrue(c.ConfirmRestartReady());
            c.PlacePlayer(TeamSide.Home, "hs1", Vec2.Zero, East.ToHeading());
            c.State.Ball.Physics = new BallState(new Vec3(0.8f, c.BallParameters.Radius, 0f), Vec3.Zero) { IsGrounded = true };
            c.ReleaseBall();
            Assert.IsTrue(c.GiveBallTo(TeamSide.Home, "hs1"));
            return c;
        }

        private static void Rival(MatchController c, string id, float x, float y) { Assert.IsTrue(c.PlacePlayer(TeamSide.Away, id, new Vec2(x, y), West.ToHeading())); }

        private static ActionResult Pass(MatchController c, FootballActionKind kind, Vec2 dir, ControlSource source = ControlSource.Ai, float power = 0.6f)
        {
            return c.Perform(new ActionRequest(TeamSide.Home, "hs1", kind, dir, power) { Source = source });
        }

        private static float QualityOf(Lookup l, FootballActionKind kind, Vec2 dir = default(Vec2), Action<MatchController> arrange = null)
        {
            MatchController c = Scene(l);
            arrange?.Invoke(c);
            ActionResult r = Pass(c, kind, dir.Magnitude > 0f ? dir : East);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome, kind + " is played");
            return r.Quality;
        }

        // ================= quality =================

        [Test]
        public void Quality_AHigherPassingAttributeGivesABetterShortPass()
        {
            float low = QualityOf(LookupOf(Attributes(60, 45)), FootballActionKind.ShortPass), mid = QualityOf(LookupOf(Attributes(60, 70)), FootballActionKind.ShortPass), high = QualityOf(LookupOf(Attributes(60, 90)), FootballActionKind.ShortPass);
            Assert.Less(low, mid); Assert.Less(mid, high);
        }

        [Test]
        public void Quality_TheShortPassHasNoFixedPenalty_ItIsRatedLikeEveryOtherPass()
        {
            foreach (int level in new[] { 40, 60, 80, 95 })
            {
                Lookup l = LookupOf(Attributes(level));
                float shortPass = QualityOf(l, FootballActionKind.ShortPass);
                foreach (FootballActionKind other in new[] { FootballActionKind.WallPass, FootballActionKind.ThroughPass, FootballActionKind.LongPass })
                    Assert.AreEqual(QualityOf(l, other), shortPass, 1e-6f, "attributes " + level + ": short pass against " + other);
                Assert.Greater(shortPass, 0.9f * QualityOf(l, FootballActionKind.LongPass), "and in particular far from the 0.75 it used to get");
            }
        }

        [Test]
        public void Quality_AShortPassOfPassing85_FreeOfRivals_IsOfHighQuality()
        {
            float q = QualityOf(LookupOf(Attributes(85)), FootballActionKind.ShortPass);
            Assert.Greater(q, 0.8f, "a player with 85 passes with high quality when he is free (it was about 0.64)");
            Assert.Less(q, 1f, "and it is not an automatic perfect pass");
        }

        [Test]
        public void Quality_TheShortPassAndTheOtherPassesKeepTheirOwnSpeedAndLoft_OnlyTheRatingChanged()
        {
            Lookup l = LookupOf(Attributes(80));
            ActionResult shortPass = Pass(Scene(l), FootballActionKind.ShortPass, East), longPass = Pass(Scene(l), FootballActionKind.LongPass, East);
            Assert.AreEqual(0f, shortPass.BallVelocity.Y, 1e-5f, "a short pass is along the ground");
            Assert.Greater(longPass.BallVelocity.Y, 0.5f, "a long pass is lofted");
            Assert.Less(shortPass.BallVelocity.Magnitude, longPass.BallVelocity.Magnitude, "and slower");
        }

        // ================= situations =================

        [Test]
        public void Situation_AFreePasserPassesBetterThanOnePressedByRivals_AndMoreRivalsTakeMore()
        {
            Lookup l = LookupOf(Attributes(80));
            float free = QualityOf(l, FootballActionKind.ShortPass);
            float far = QualityOf(l, FootballActionKind.ShortPass, default(Vec2), c => Rival(c, "as1", 4f, 0f));
            float one = QualityOf(l, FootballActionKind.ShortPass, default(Vec2), c => Rival(c, "as1", 1f, 1f));
            float two = QualityOf(l, FootballActionKind.ShortPass, default(Vec2), c => { Rival(c, "as1", 1f, 1f); Rival(c, "as2", -1f, -1f); });
            Assert.AreEqual(free, far, 1e-6f, "a rival outside the pressure radius changes nothing");
            Assert.Less(one, free, "a rival close takes quality");
            Assert.Less(two, one, "two take more");
            Assert.Greater(two, 0.7f * free, "but it never makes the pass hopeless");
        }

        [Test]
        public void Situation_ACloserRivalTakesMore()
        {
            Lookup l = LookupOf(Attributes(80));
            float near = QualityOf(l, FootballActionKind.ShortPass, default(Vec2), c => Rival(c, "as1", 0.7f, 0f)), middle = QualityOf(l, FootballActionKind.ShortPass, default(Vec2), c => Rival(c, "as1", 1.8f, 0f));
            Assert.Less(near, middle);
        }

        [Test]
        public void Situation_ThePressureAppliesToEveryKindOfPass_ButNotToShotsOrToTheGoalkeepersHands()
        {
            Lookup l = LookupOf(Attributes(80));
            foreach (FootballActionKind kind in new[] { FootballActionKind.ShortPass, FootballActionKind.WallPass, FootballActionKind.ThroughPass, FootballActionKind.LongPass })
                Assert.Less(QualityOf(l, kind, default(Vec2), c => Rival(c, "as1", 1f, 1f)), QualityOf(l, kind), kind + " under pressure");
            Assert.AreEqual(QualityOf(l, FootballActionKind.Shot), QualityOf(l, FootballActionKind.Shot, default(Vec2), c => Rival(c, "as1", 1f, 1f)), 1e-6f, "a shot is not a pass");
        }

        [Test]
        public void Situation_ABadOrientationReducesTheAccuracy_APassTowardsTheWeakSideWithAPoorWeakFoot()
        {
            // a right-footed player, facing east, with a weak left foot: south of him is his right, north his left (field Y is the width)
            Lookup l = LookupOf(Attributes(80), PreferredFoot.Right, 20);
            Vec2 strongSide = new Vec2(0.2f, -1f).Normalized, weakSide = new Vec2(0.2f, 1f).Normalized;
            float strong = QualityOf(l, FootballActionKind.ShortPass, strongSide), weak = QualityOf(l, FootballActionKind.ShortPass, weakSide), ahead = QualityOf(l, FootballActionKind.ShortPass, East);
            Assert.AreEqual(ahead, strong, 1e-6f, "towards his good foot the pass is as good as straight ahead");
            Assert.Less(weak, strong, "across the body towards the weak foot it is worse");
            Assert.Greater(QualityOf(LookupOf(Attributes(80), PreferredFoot.Right, 90), FootballActionKind.ShortPass, weakSide), weak, "and a better weak foot recovers it");
        }

        [Test]
        public void Situation_APoorQualityPassIsLessAccurate_TheSpreadOfTheDirectionGrowsAsTheQualityFalls()
        {
            float Spread(Lookup l)
            {
                double sum = 0;
                for (int seed = 1; seed <= 30; seed++)
                {
                    ActionResult r = Pass(Scene(l, seed), FootballActionKind.ShortPass, East, ControlSource.Ai, 0.8f);
                    sum += Math.Abs(ActionModel.SignedAngle(East, r.Direction));
                }
                return (float)(sum / 30.0);
            }
            Assert.Less(Spread(LookupOf(Attributes(90))), Spread(LookupOf(Attributes(40))), "the better passer sends the ball closer to where he aimed");
        }

        [Test]
        public void Situation_AMarkedReceiverIsWorthLessThanAFreeOne()
        {
            MatchController c = Scene(LookupOf(Attributes(80)));
            c.PlacePlayer(TeamSide.Home, "hs3", new Vec2(7f, -6f), East.ToHeading());     // marked
            c.PlacePlayer(TeamSide.Home, "hs4", new Vec2(7f, 6f), East.ToHeading());      // free
            Rival(c, "as3", 8f, -6.5f);
            WorldView v = WorldState.Capture(c).View;
            BasicDecisionTuning t = BasicDecisionTuning.ForMode(GameMode.Classic); AttackPositioningTuning a = AttackPositioningTuning.ForMode(GameMode.Classic);
            int me = v.IndexOf("hs1");
            float marked = ReceiverScore.Evaluate(v, me, v.IndexOf("hs3"), false, false, t, a, out _), free = ReceiverScore.Evaluate(v, me, v.IndexOf("hs4"), false, false, t, a, out _);
            Assert.Greater(free, marked);
        }

        // ================= one model for everybody =================

        [Test]
        public void OneModel_ThePersonsPassAndTheAiPassAreRatedTheSame_AndLeaveTheSameBall()
        {
            Lookup l = LookupOf(Attributes(80));
            ActionResult person = Pass(Scene(l, 7), FootballActionKind.ShortPass, East, ControlSource.Human), ai = Pass(Scene(l, 7), FootballActionKind.ShortPass, East, ControlSource.Ai);
            Assert.AreEqual(person.Quality, ai.Quality, 1e-6f);
            Assert.AreEqual(person.BallVelocity.X, ai.BallVelocity.X, 1e-5f); Assert.AreEqual(person.BallVelocity.Z, ai.BallVelocity.Z, 1e-5f);
        }

        [Test]
        public void OneModel_TheMappingOfTheButtonB_GivesTheShortPassThatIsRatedLikeAPass()
        {
            Assert.AreEqual(FootballActionKind.ShortPass, ActionMapper.Map(new ActionInput(ActionButton.B, ActionGesture.Tap, Vec2.Zero, 0.5f), new ActionContext { HasBall = true }));
            Lookup l = LookupOf(Attributes(80));
            MatchController c = Scene(l);
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, East, 0.6f));
            Assert.AreEqual(FootballActionKind.ShortPass, r.Kind); Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.AreEqual(QualityOf(l, FootballActionKind.WallPass), r.Quality, 1e-6f, "the button B is rated like a wall pass: one pass model");
        }

        // ================= compatibility =================

        [Test]
        public void Compatibility_TheOtherKicksAreRatedAsBefore()
        {
            Lookup l = LookupOf(Attributes(80));
            Assert.AreEqual(0.806f, QualityOf(l, FootballActionKind.LongPass), 0.002f, "the long pass");
            Assert.AreEqual(0.806f, QualityOf(l, FootballActionKind.ThroughPass), 0.002f, "the through ball");
            Assert.AreEqual(0.806f, QualityOf(l, FootballActionKind.WallPass), 0.002f, "the wall pass");
            Assert.Greater(QualityOf(l, FootballActionKind.Shot), 0f, "the shot");
        }

        [Test]
        public void Compatibility_TheGoalkeepersDistributionKeepsHisOwnFormula()
        {
            MatchController c = Scene(LookupOf(Attributes(80)));
            c.PlacePlayer(TeamSide.Home, "hs1", new Vec2(-19f, 0f), East.ToHeading());
            c.PlacePlayer(TeamSide.Home, "hs0", new Vec2(-16f, 11f), East.ToHeading());
            Assert.IsTrue(c.GiveBallTo(TeamSide.Home, "hs0"));
            ActionResult r = c.Perform(new ActionRequest(TeamSide.Home, "hs0", FootballActionKind.GoalkeeperDistribution, East, 0.6f));
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
        }

        [Test]
        public void Compatibility_AMatchOfTheShortPassPlaysThroughInBothModes_WithPassesCompleted()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                MatchController c = Scene(LookupOf(Attributes(80)), 3, mode);
                var done = new List<ActionResult>(); c.ActionPerformed += done.Add;
                ActionResult r = Pass(c, FootballActionKind.ShortPass, East);
                for (int i = 0; i < 90; i++) c.Advance(Dt);
                Assert.AreEqual(ActionOutcome.Completed, r.Outcome, mode.ToString());
                Assert.AreEqual(BallMode.InPass == c.State.Ball.Mode || c.State.Ball.Mode == BallMode.Free || c.State.Ball.Mode == BallMode.Controlled, true, mode + ": the ball is moving or controlled");
            }
        }
    }
}
