using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 28: the throw-in, the goal kick and the corner, and the carrier who is pressed, on hand-made worlds. What is tested: the ball waits on the spot until the taker plays it (before, the carried ball was dragged over the
    /// line and 31 % of the throw-ins went out again in 0.2 s), the opponents keep their distance, the team-mates are set up as options, the taker plays the free option and does not play into a rival, and a pass that a presser
    /// can take in the first metres is worth less than the same pass away from him. Arena never has these restarts.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController ThrowInScene(out Vec2 spot)
        {
            MatchController c = Quiet();
            SendBallOut(c, 5f, 13.5f, TeamSide.Home);
            Assert.AreEqual(RestartKind.ThrowIn, c.State.PendingRestart.Kind);
            Assert.IsTrue(c.AssignRestartTaker("as3"));
            Assert.IsTrue(c.ConfirmRestart());   // the set-up puts the team-mates where they offer a pass; the tests move them afterwards
            spot = c.State.PendingRestart.Position;
            return c;
        }

        /// <summary>The taker's other team-mates (except the one named) far from the thrower, so that only the options a test builds exist.</summary>
        private static void ParkTheOthers(MatchController c, params string[] keep)
        {
            string[] ids = { "as1", "as2", "as4", "as5" }; float y = -10f;
            foreach (string id in ids) { if (!keep.Contains(id)) Put(c, TeamSide.Away, id, -17f, y, East); y += 3f; }
        }

        [Test]
        public void SetPiece_TheBallWaitsOnTheSpotInTheTakersHands_UntilHePlaysIt()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            Assert.IsTrue(c.State.SetPiece.Active); Assert.AreEqual("as3", c.State.SetPiece.TakerId);
            // the taker "walks" towards the line for a second (the old carried ball was dragged out and the throw-in was lost)
            for (int i = 0; i < 60; i++) { c.SetPlayerInput(TeamSide.Away, "as3", new Vec2(0f, 1f)); c.Advance(Dt); if (!c.State.SetPiece.Active) break; }
            Vec3 b = c.State.Ball.Physics.Position;
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase, "the ball did not go out");
            Assert.Less((new Vec2(b.X, b.Z) - spot).Magnitude, 0.05f, "it stayed on the spot while the hold lasted");
        }

        [Test]
        public void SetPiece_ThePlayedBallEndsTheHold_AndTheKickIsAnOrdinaryPass()
        {
            MatchController c = ThrowInScene(out _);
            Put(c, TeamSide.Away, "as4", 5f, 6f, North);
            ActionResult r = c.PerformIntent(new ActionIntent(TeamSide.Away, "as3", FootballActionKind.ShortPass, new Vec2(0f, -1f), 0.6f) { Target = PassTarget.ToPlayer("as4") });
            Assert.IsTrue(r.Succeeded, r.ToString());
            c.Advance(Dt);
            Assert.IsFalse(c.State.SetPiece.Active); Assert.IsTrue(c.State.SetPiece.PlayedByTaker);
            Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);
        }

        [Test]
        public void SetPiece_TheOpponentsKeepTheirDistance_AndMoreAtACornerAndAGoalKick()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            Put(c, TeamSide.Home, "hs2", spot.X + 0.6f, spot.Y - 0.5f, East);
            c.Advance(Dt);
            Assert.GreaterOrEqual((PosOf(c, "hs2", TeamSide.Home) - spot).Magnitude, c.SetPiece.ThrowInOpponentRadius - 0.05f, "a throw-in");

            MatchController k = Live();
            SendBallOut(k, 20.6f, 3f, TeamSide.Away);
            Assert.AreEqual(RestartKind.Corner, k.State.PendingRestart.Kind);
            Assert.IsTrue(k.ConfirmRestart());
            Vec2 corner = k.State.PendingRestart.Position;
            Put(k, TeamSide.Away, "as2", corner.X - 1.5f, corner.Y - 1.5f, West);
            k.Advance(Dt);
            Assert.GreaterOrEqual((PosOf(k, "as2", TeamSide.Away) - corner).Magnitude, k.SetPiece.CornerOpponentRadius - 0.05f, "a corner");

            MatchController g = Live();
            SendBallOut(g, 20.6f, 4f, TeamSide.Home);
            Assert.AreEqual(RestartKind.GoalKick, g.State.PendingRestart.Kind);
            Assert.IsTrue(g.ConfirmRestart());
            Vec2 kick = g.State.PendingRestart.Position;
            Put(g, TeamSide.Home, "hs2", kick.X - 2.5f, kick.Y, East);
            g.Advance(Dt);
            Assert.GreaterOrEqual((PosOf(g, "hs2", TeamSide.Home) - kick).Magnitude, g.SetPiece.GoalKickOpponentRadius - 0.05f, "a goal kick");
        }

        [Test]
        public void SetPiece_ATakerWhoWalksAwayWithTheBall_LosesTheHold()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            Put(c, TeamSide.Away, "as3", spot.X, spot.Y - c.SetPiece.TakerMaxDistance - 1f, North);
            c.Advance(Dt);
            Assert.IsFalse(c.State.SetPiece.Active);
        }

        [Test]
        public void SetPiece_TheTeammatesAreSetUpAsOptions_FreeOfRivals_WithTheDeepestOneCovering()
        {
            MatchController c = Live();
            SendBallOut(c, 5f, 13.5f, TeamSide.Home);
            // rivals near the spot, the taker's team-mates in a bunch far from it
            string[] mates = { "as1", "as2", "as4", "as5" };
            for (int i = 0; i < mates.Length; i++) Put(c, TeamSide.Away, mates[i], 12f + i, -4f + i, West);
            Put(c, TeamSide.Away, "as3", 5f, 11.7f, North);
            Put(c, TeamSide.Home, "hs1", 3f, 9f, East); Put(c, TeamSide.Home, "hs2", 6f, 8f, East); Put(c, TeamSide.Home, "hs3", 9f, 9.5f, East);
            c.AssignRestartTaker("as3");
            Vec2 deepest = PosOf(c, "as1", TeamSide.Away);   // Away attacks west: the deepest team-mate is the one furthest east
            foreach (string m in mates) if (PosOf(c, m, TeamSide.Away).X > deepest.X) deepest = PosOf(c, m, TeamSide.Away);
            Assert.IsTrue(c.PrepareRestart());
            Vec2 spot = c.State.PendingRestart.Position;
            var placed = mates.Where(m => (PosOf(c, m, TeamSide.Away) - spot).Magnitude <= 16f).ToList();
            Assert.GreaterOrEqual(placed.Count, 3, "three options near the thrower");
            foreach (string m in placed)
            {
                Vec2 p = PosOf(c, m, TeamSide.Away);
                Assert.GreaterOrEqual((p - spot).Magnitude, 3f, m + " is not on top of the thrower");
                foreach (string o in placed) if (o != m) Assert.GreaterOrEqual((p - PosOf(c, o, TeamSide.Away)).Magnitude, 2f, m + " and " + o + " are apart");
            }
            Assert.IsTrue(mates.Any(m => (PosOf(c, m, TeamSide.Away) - deepest).Magnitude < 0.01f), "the deepest team-mate stayed where he was");
        }

        [Test]
        public void SetPiece_TheTakerPlaysTheFreeTeammate_NotTheOneWithARivalOnTheLane()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            // free option to the left (as4), covered option to the right (as5, with a rival on the lane)
            ParkTheOthers(c, "as4", "as5");
            Put(c, TeamSide.Away, "as4", spot.X - 2f, spot.Y - 6f, North); Put(c, TeamSide.Away, "as5", spot.X + 7f, spot.Y - 6f, North);
            Put(c, TeamSide.Home, "hs2", spot.X + 3.5f, spot.Y - 3f, East); Put(c, TeamSide.Home, "hs3", spot.X + 7f, spot.Y - 4.5f, East);
            PlayerDecision d = Decide(c, "as3");
            Assert.AreEqual(DecisionKind.Pass, d.Kind, d.Reason.ToString());
            Assert.AreEqual("as4", d.TargetPlayerId, "the free one");
            Assert.AreEqual(DecisionReason.SetPiecePass, d.Reason);
        }

        [Test]
        public void SetPiece_WithNoSafeOption_TheTakerWaitsAMoment_ThenSettlesOrClears_NeverDribbles()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            ParkTheOthers(c, "as4");
            Put(c, TeamSide.Away, "as4", spot.X - 2f, spot.Y - 6f, North);
            foreach (string r in new[] { "hs1", "hs2", "hs3" }) Put(c, TeamSide.Home, r, spot.X - 2f + (r == "hs1" ? 0.5f : 0f), spot.Y - 5.5f + (r == "hs2" ? 1.5f : 0f) + (r == "hs3" ? -1.5f : 0f), East);
            PlayerDecision first = Decide(c, "as3");
            Assert.AreEqual(DecisionReason.SetPieceWait, first.Reason, "nothing safe yet: he waits on the spot");
            Assert.AreEqual(DecisionKind.Idle, first.Kind, "standing is Idle: Hold belongs to the Phase 11 carrier");
            Step(c, (int)(1f / Dt));
            PlayerDecision later = Decide(c, "as3");
            Assert.AreNotEqual(DecisionKind.Dribble, later.Kind); Assert.AreNotEqual(DecisionKind.Protect, later.Kind);
            Assert.IsTrue(later.Reason == DecisionReason.SetPieceClear || later.Reason == DecisionReason.SetPiecePass || later.Reason == DecisionReason.SetPieceWait, later.Reason.ToString());
        }

        [Test]
        public void SetPiece_TeammatesOfTheTakerStayAndStepAwayOnlyFromARivalWhoComesTooClose()
        {
            MatchController c = ThrowInScene(out Vec2 spot);
            Put(c, TeamSide.Away, "as4", spot.X - 2f, spot.Y - 6f, North);
            PlayerDecision stay = Decide(c, "as4");
            Assert.AreEqual(DecisionReason.SetPieceOffer, stay.Reason); Assert.IsFalse(stay.Movement.HasTarget, "he stays where the set-up put him");
            Put(c, TeamSide.Home, "hs2", spot.X - 2f + 1f, spot.Y - 6f, East);
            PlayerDecision step = Decide(c, "as4");
            Assert.AreEqual(DecisionReason.SetPieceOffer, step.Reason); Assert.IsTrue(step.Movement.HasTarget);
            Assert.Greater((step.Movement.Target - PosOf(c, "hs2", TeamSide.Home)).Magnitude, 1.5f, "he steps away from the rival");
        }

        [Test]
        public void SetPiece_ArenaHasNoThrowIns_GoalKicksOrCorners_SoNothingOfThisRuns()
        {
            Assert.IsFalse(GameModeRules.For(GameMode.Arena).HasThrowIns); Assert.IsFalse(GameModeRules.For(GameMode.Arena).HasGoalKicks); Assert.IsFalse(GameModeRules.For(GameMode.Arena).HasCorners);
            MatchController a = Live(MatchSettings.CreateArena());
            Step(a, 600);
            Assert.IsTrue(a.State.SetPiece == null || !a.State.SetPiece.Active);
        }

        [Test]
        public void PressedCarrier_APassWhereAPresserCanTakeTheBallInTheFirstMetres_IsWorthLess_ThanTheSamePassAwayFromHim()
        {
            // the carrier at the middle, a presser 1.6 m to the south; team-mates 8 m to the north and to the south
            MatchController c = AttackScene(0f, 0f);
            Put(c, TeamSide.Away, "as1", 0.4f, -1.6f, North);
            Put(c, TeamSide.Home, "hs4", 0f, 8f, East); Put(c, TeamSide.Home, "hs5", 0f, -8f, East);
            WorldView v = ViewOf(c);
            double Score(string receiver, float releaseWeight)
            {
                var carrier = new BallCarrierTuning { ReleaseWeight = releaseWeight, CrossBonus = 0.0001f };
                var ev = new BallCarrierEvaluator(new BasicDecisionTuning(), new AttackPositioningTuning(), carrier);
                ev.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
                int idx = v.IndexOf(receiver);
                for (int k = 0; k < ev.Count; k++) if (ev.At(k).ReceiverIndex == idx && (ev.At(k).Type == CandidateType.Pass || ev.At(k).Type == CandidateType.ForwardPass)) return ev.At(k).Score;
                return double.NaN;
            }
            double awayOff = Score("hs4", 0.0001f), awayOn = Score("hs4", 2f), towardOff = Score("hs5", 0.0001f), towardOn = Score("hs5", 2f);
            if (!double.IsNaN(towardOff) && !double.IsNaN(towardOn)) Assert.Less(towardOn, towardOff - 0.2, "into the presser's reach: worth less");
            Assert.AreEqual(awayOff, awayOn, 0.05, "away from him: the same");
        }

        [Test]
        public void Cross_FromTheWingToATeammateInTheBox_IsWorthMoreThanTheSamePassWithoutTheBonus_AndIsLofted()
        {
            MatchController c = AttackScene(13f, 11f);
            Put(c, TeamSide.Home, "hs5", 16f, 1f, East); Put(c, TeamSide.Home, "hs4", 4f, 9f, East);
            WorldView v = ViewOf(c);
            ActionCandidate Cand(float bonus)
            {
                var ev = new BallCarrierEvaluator(new BasicDecisionTuning(), new AttackPositioningTuning(), new BallCarrierTuning { CrossBonus = bonus });
                ev.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
                for (int k = 0; k < ev.Count; k++) if (ev.At(k).ReceiverIndex == v.IndexOf("hs5") && (ev.At(k).Type == CandidateType.Pass || ev.At(k).Type == CandidateType.ForwardPass)) return ev.At(k);
                Assert.Fail("no pass to the man in the box"); return default(ActionCandidate);
            }
            ActionCandidate with = Cand(1.6f), without = Cand(0f);
            Assert.AreEqual(DecisionReason.CrossToBox, with.Reason);
            Assert.Greater(with.Score, without.Score + 0.2f);
            Assert.AreNotEqual(DecisionReason.CrossToBox, without.Reason);
            // and he plays it lofted
            PlayerDecision d = new BasicDecisionEngine(c.Pitch, null, null, null, null, new BallCarrierTuning { ShootMinScore = 99f }).Decide(v, "hs3");
            if (d.Reason == DecisionReason.CrossToBox) Assert.AreEqual(FootballActionKind.LongPass, d.Action.Kind);
        }
    }
}
