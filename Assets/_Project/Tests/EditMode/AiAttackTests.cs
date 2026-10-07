using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 10: the attack WITHOUT the ball. Support runs, runs forward, width, getting open, the lane of a pass, the receiver score, the roles as a preference, and the coordination (one of each job).
    /// Each rule is tested on its own, on hand-made worlds: a quiet pitch, the home team in a six-slot formation, the carrier in the middle, the chosen players standing where the test needs them.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static readonly string[] AttackField = { "hs1", "hs2", "hs3", "hs4", "hs5" };

        /// <summary>
        /// The home team lined up in the six-slot test formation (so every player has a home), each field player at his home, hs3 holding the ball at (cx, cy) facing east. The away team is far away. The roles
        /// are given as "id:Archetype" (the others have none).
        /// </summary>
        private static MatchController AttackScene(float cx = 0f, float cy = 0f, params string[] roles)
        {
            MatchController c = Quiet();
            c.SetFormation(TeamSide.Home, TestFormation()); c.SetFormation(TeamSide.Away, TestFormation());
            var catalog = new PlayingProfileCatalog();
            foreach (string spec in roles)
            {
                string[] parts = spec.Split(':');
                catalog.TryAdd(new PlayerPlayingProfile(parts[0], PitchZone.Midfield, null, new[] { (PlayerArchetype)Enum.Parse(typeof(PlayerArchetype), parts[1]) }));
            }
            c.SetPlayingProfiles(catalog);
            c.RefreshHomePositions();
            WorldState w = WorldState.Capture(c);
            foreach (string id in AttackField) { WorldPlayer p = w[w.IndexOf(id)]; Assert.IsTrue(p.HasHome, id); Put(c, TeamSide.Home, id, p.Home.X, p.Home.Y, East); }
            Carrier(c, TeamSide.Home, "hs3", cx, cy, East);
            return c;
        }

        private static WorldView ViewOf(MatchController c) { return WorldState.Capture(c).View; }

        private static BasicDecisionEngine EngineOf(MatchController c, AttackPositioningTuning tuning = null) { return new BasicDecisionEngine(c.Pitch, null, null, null, tuning); }

        private static Vec2 HomeOf(MatchController c, string id) { WorldState w = WorldState.Capture(c); return w[w.IndexOf(id)].Home; }

        private static AttackJob JobOfPlayer(MatchController c, string id, AttackPositioningTuning tuning = null)
        {
            BasicDecisionEngine e = EngineOf(c, tuning);
            WorldView v = ViewOf(c);
            e.Attack.Assign(v, v.IndexOf("hs3"));
            return e.Attack.JobOf(v.IndexOf(id));
        }

        private static string[] PlayersWithJob(MatchController c, AttackJob job, AttackPositioningTuning tuning = null)
        {
            BasicDecisionEngine e = EngineOf(c, tuning);
            WorldView v = ViewOf(c);
            e.Attack.Assign(v, v.IndexOf("hs3"));
            return AttackField.Where(id => e.Attack.JobOf(v.IndexOf(id)) == job).ToArray();
        }

        private static float OpponentRoom(MatchController c, Vec2 point) { return WorldGeometry.NearestOpponentDistanceTo(ViewOf(c), TeamSide.Home, point); }

        // ================= the lane of a pass =================

        [Test]
        public void PassLane_IsClearOnlyWhenNoOpponentIsBetweenThePasserAndTheReceiver_AndWithinRange()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Away, "as1", 4f, 0.2f, West);
            WorldView v = ViewOf(c);
            Assert.IsFalse(WorldGeometry.IsPassLaneClear(v, TeamSide.Home, new Vec2(0f, 0f), new Vec2(8f, 0f), 1f, 22f, out float room), "an opponent on the line"); Assert.Less(room, 1f);
            Assert.IsTrue(WorldGeometry.IsPassLaneClear(v, TeamSide.Home, new Vec2(0f, 0f), new Vec2(0f, 8f), 1f, 22f, out room), "a lane at another angle"); Assert.Greater(room, 1f);
            Assert.IsTrue(WorldGeometry.IsPassLaneClear(v, TeamSide.Home, new Vec2(0f, 0f), new Vec2(3f, 0f), 1f, 22f, out _), "an opponent beyond the receiver cannot stop the ball");
            Assert.IsTrue(WorldGeometry.IsPassLaneClear(v, TeamSide.Home, new Vec2(6f, 0f), new Vec2(12f, 0f), 1f, 22f, out _), "an opponent behind the passer cannot stop it either");
            Assert.IsFalse(WorldGeometry.IsPassLaneClear(v, TeamSide.Home, new Vec2(0f, 8f), new Vec2(0f, -20f), 1f, 22f, out _), "out of range");
            Assert.IsTrue(float.IsPositiveInfinity(WorldGeometry.PassLaneRoom(v, TeamSide.Away, new Vec2(-18f, 0f), new Vec2(-12f, 0f))), "nobody of the other team there");
        }

        // ================= the open point =================

        [Test]
        public void FindBestOpenPoint_ReturnsAnInsideLeashedPoint_AwayFromOpponentsAndTeammates_AndIsDeterministic()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Away, "as1", 5f, 2f, West); Put(c, TeamSide.Away, "as2", 4f, -2f, West);
            WorldView v = ViewOf(c);
            int idx = v.IndexOf("hs4");
            var tuning = new AttackPositioningTuning();
            var search = new OpenPointSearch { Center = new Vec2(5f, 0f), Home = v[idx].Home, MaxHomeDeviation = tuning.JobMaxHomeDeviation, HasCarrier = true, Carrier = new Vec2(0f, 0f), Forward = East };
            Vec2 p = WorldGeometry.FindBestOpenPoint(v, idx, search, tuning, out float score);
            Vec2 again = WorldGeometry.FindBestOpenPoint(v, idx, search, tuning, out float score2);
            Assert.AreEqual(p, again); Assert.AreEqual(score, score2);
            Assert.LessOrEqual((p - v[idx].Home).Magnitude, tuning.JobMaxHomeDeviation + 0.001f, "inside his leash");
            Assert.LessOrEqual(Math.Abs(p.X), 20f); Assert.LessOrEqual(Math.Abs(p.Y), 12.5f);
            Assert.Greater(WorldGeometry.NearestOpponentDistanceTo(v, TeamSide.Home, p), WorldGeometry.NearestOpponentDistanceTo(v, TeamSide.Home, search.Center) - 0.001f, "with more room than the base point");
        }

        [Test]
        public void FindBestOpenPoint_WhenNothingFitsTheLeash_PullsTheBasePointToIt()
        {
            MatchController c = AttackScene();
            WorldView v = ViewOf(c);
            int idx = v.IndexOf("hs4");
            var tuning = new AttackPositioningTuning();
            var search = new OpenPointSearch { Center = new Vec2(18f, 11f), Home = v[idx].Home, MaxHomeDeviation = 1f, Forward = East };
            Vec2 p = WorldGeometry.FindBestOpenPoint(v, idx, search, tuning, out float score);
            Assert.LessOrEqual((p - v[idx].Home).Magnitude, 1.001f);
        }

        // ================= support run =================

        [Test]
        public void SupportRun_OffersAPassAtAComfortableDistance_NotOnTheBall_NearHisHome()
        {
            MatchController c = AttackScene();
            var tuning = new BasicDecisionTuning(); var attack = new AttackPositioningTuning();
            string id = PlayersWithJob(c, AttackJob.Support).Single();
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.Support, d.Kind); Assert.AreEqual(DecisionReason.SupportRole, d.Reason); Assert.IsFalse(d.HasAction, "a run is a movement, never an action");
            if (d.Movement.HasTarget)
            {
                float dist = (d.Movement.Target - new Vec2(0f, 0f)).Magnitude;
                Assert.GreaterOrEqual(dist, tuning.SupportMinDistance - 1f, "not on the ball"); Assert.LessOrEqual(dist, tuning.SupportMaxDistance + 1f, "within reach of a pass");
                Assert.LessOrEqual((d.Movement.Target - HomeOf(c, id)).Magnitude, attack.JobMaxHomeDeviation + 0.01f, "keeps his relation with his place");
            }
        }

        [Test]
        public void SupportRun_IsNotCovered_WhenAnOpponentStandsWhereHeWouldGo()
        {
            MatchController c = AttackScene();
            string id = PlayersWithJob(c, AttackJob.Support).Single();
            Vec2 free = EngineOf(c).Decide(ViewOf(c), id).Movement.Target;
            Put(c, TeamSide.Away, "as1", free.X, free.Y, West);
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.Support, d.Kind);
            Assert.Greater(OpponentRoom(c, d.Movement.Target), 1.5f, "he asks for the ball where an opponent is not on top of him");
        }

        [Test]
        public void SupportRun_StaysInsideThePitch_EvenWithTheCarrierOnTheTouchline()
        {
            foreach (float cy in new[] { -12f, 12f })
            {
                MatchController c = AttackScene(8f, cy);
                foreach (string id in AttackField.Where(x => x != "hs3"))
                {
                    PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
                    if (!d.Movement.HasTarget) continue;
                    Assert.LessOrEqual(Math.Abs(d.Movement.Target.X), 20.6f, id); Assert.LessOrEqual(Math.Abs(d.Movement.Target.Y), 13.1f, id);
                }
            }
        }

        // ================= forward run =================

        [Test]
        public void ForwardRun_GoesAhead_TowardsTheGoal_WithoutLeavingHisZone()
        {
            MatchController c = AttackScene(-4f, 0f);
            var attack = new AttackPositioningTuning();
            string id = PlayersWithJob(c, AttackJob.Forward).Single();
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.ForwardRun, d.Kind); Assert.AreEqual(DecisionReason.ForwardRunRole, d.Reason); Assert.IsFalse(d.HasAction);
            Vec2 me = PosOf(c, id, TeamSide.Home);
            if (d.Movement.HasTarget)
            {
                Assert.Greater(d.Movement.Target.X, me.X + attack.ForwardMinGain - 0.01f, "ahead of where he is, towards the goal he attacks (+X)");
                Assert.LessOrEqual((d.Movement.Target - HomeOf(c, id)).Magnitude, attack.ForwardMaxHomeDeviation + 0.01f, "does not abandon his zone");
                Assert.Greater(d.Movement.Direction.X, 0f);
            }
        }

        [Test]
        public void ForwardRun_DoesNotRunIntoAnOpponent()
        {
            MatchController c = AttackScene(-4f, 0f);
            string id = PlayersWithJob(c, AttackJob.Forward).Single();
            Vec2 open = EngineOf(c).Decide(ViewOf(c), id).Movement.Target;
            Put(c, TeamSide.Away, "as1", open.X, open.Y, West);
            string again = PlayersWithJob(c, AttackJob.Forward).SingleOrDefault();
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            if (d.Movement.HasTarget) Assert.Greater(OpponentRoom(c, d.Movement.Target), 1.5f, "never into the opponent");
        }

        [Test]
        public void ForwardRun_IsNotGiven_ToATiredPlayer()
        {
            MatchController c = AttackScene(-4f, 0f);
            string runner = PlayersWithJob(c, AttackJob.Forward).Single();
            c.State.Home.Find(runner).Locomotion.CurrentStamina = 5f;
            Assert.AreNotEqual(runner, PlayersWithJob(c, AttackJob.Forward).SingleOrDefault(), "a tired player is not sent running");
            Assert.AreNotEqual(AttackJob.Forward, JobOfPlayer(c, runner));
            Assert.AreNotEqual(DecisionKind.ForwardRun, EngineOf(c).Decide(ViewOf(c), runner).Kind);
        }

        [Test]
        public void ForwardRun_WithNoRoomAhead_BecomesGettingOpen()
        {
            MatchController c = AttackScene(-4f, 0f);
            string id = PlayersWithJob(c, AttackJob.Forward).Single();
            Vec2 me = PosOf(c, id, TeamSide.Home);
            // the wall of opponents right in front of him, and the touchline next to him: no ground to gain
            Put(c, TeamSide.Away, "as1", me.X + 2f, me.Y, West); Put(c, TeamSide.Away, "as2", me.X + 2f, me.Y + 2f, West); Put(c, TeamSide.Away, "as3", me.X + 2f, me.Y - 2f, West);
            foreach (string other in AttackField.Where(x => x != "hs3"))
            {
                PlayerDecision d = EngineOf(c).Decide(ViewOf(c), other);
                Assert.IsTrue(new[] { DecisionKind.Support, DecisionKind.ForwardRun, DecisionKind.HoldWidth, DecisionKind.GetOpen }.Contains(d.Kind), "every player without the ball has a job: " + other);
                if (d.Kind == DecisionKind.ForwardRun && d.Movement.HasTarget) Assert.Greater(OpponentRoom(c, d.Movement.Target), 1f, "a run only where there is room");
            }
            Assert.AreNotEqual(DecisionKind.ForwardRun, EngineOf(c).Decide(ViewOf(c), id).Kind, "the player in front of the wall is not the one sent running");
        }

        // ================= hold the width =================

        [Test]
        public void HoldWidth_KeepsALateralOption_InsideThePitch_WithALaneToTheCarrier()
        {
            MatchController c = AttackScene();
            string id = PlayersWithJob(c, AttackJob.Width).Single();
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.HoldWidth, d.Kind); Assert.AreEqual(DecisionReason.HoldWidthRole, d.Reason); Assert.IsFalse(d.HasAction);
            Assert.IsTrue(d.Movement.HasTarget || d.Movement.Magnitude == 0f);
            if (d.Movement.HasTarget)
            {
                Vec2 t = d.Movement.Target;
                Assert.GreaterOrEqual(Math.Abs(t.Y), 7f, "out wide"); Assert.LessOrEqual(Math.Abs(t.Y), 12.6f, "inside the pitch"); Assert.LessOrEqual(Math.Abs(t.X), 20.6f);
                Assert.GreaterOrEqual(WorldGeometry.PassLaneRoom(ViewOf(c), TeamSide.Home, new Vec2(0f, 0f), t), ViewOf(c).Tuning.PassLaneClearance, "a lane from the carrier");
            }
        }

        [Test]
        public void HoldWidth_StaysOnTheSideOfHisHome()
        {
            MatchController c = AttackScene();
            string id = PlayersWithJob(c, AttackJob.Width).Single();
            Vec2 home = HomeOf(c, id);
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            if (d.Movement.HasTarget && Math.Abs(home.Y) > 1f) Assert.AreEqual(Math.Sign(home.Y), Math.Sign(d.Movement.Target.Y), "his own touchline");
        }

        // ================= getting open =================

        [Test]
        public void GetOpen_MovesAwayFromAnOpponentWhoIsOnTopOfHim_AndStaysNearHisPlace()
        {
            MatchController c = AttackScene();
            string id = AttackField.First(x => x != "hs3" && JobOfPlayer(c, x) == AttackJob.GetOpen);
            Vec2 me = PosOf(c, id, TeamSide.Home);
            Put(c, TeamSide.Away, "as1", me.X + 1f, me.Y + 0.5f, West);
            Assert.AreEqual(AttackJob.GetOpen, JobOfPlayer(c, id));
            PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.GetOpen, d.Kind); Assert.AreEqual(DecisionReason.GetOpenRole, d.Reason);
            if (d.Movement.HasTarget)
            {
                Vec2 opp = PosOf(c, "as1", TeamSide.Away);
                Assert.Greater((d.Movement.Target - opp).Magnitude, (me - opp).Magnitude - 0.01f, "he moves to where the opponent is less on top of him");
                Assert.LessOrEqual((d.Movement.Target - HomeOf(c, id)).Magnitude, new AttackPositioningTuning().GetOpenMaxHomeDeviation + 0.01f);
            }
        }

        [Test]
        public void GetOpen_AvoidsStandingOnATeammate()
        {
            MatchController c = AttackScene();
            var allOpen = new AttackPositioningTuning { SupportRunners = 0, ForwardRunners = 0, WidthHolders = 0 };
            string id = "hs4", other = "hs5";
            PlayerDecision before = EngineOf(c, allOpen).Decide(ViewOf(c), id);
            Vec2 spot = before.Movement.HasTarget ? before.Movement.Target : PosOf(c, id, TeamSide.Home);
            Put(c, TeamSide.Home, other, spot.X, spot.Y, East);
            PlayerDecision after = EngineOf(c, allOpen).Decide(ViewOf(c), id);
            Vec2 target = after.Movement.HasTarget ? after.Movement.Target : PosOf(c, id, TeamSide.Home);
            Assert.Greater((target - PosOf(c, other, TeamSide.Home)).Magnitude, 1.5f, "he does not choose the spot where a team-mate already is");
        }

        // ================= who does what: the coordination =================

        [Test]
        public void TheJobs_AreOneSupport_OneRunner_OneWidth_AndTheOthersGetOpen_WhereverTheBallIs()
        {
            foreach (float cx in new[] { -15f, -6f, 0f, 7f, 15f })
                foreach (float cy in new[] { -11f, -4f, 0f, 5f, 11f })
                {
                    MatchController c = AttackScene(cx, cy);
                    Assert.LessOrEqual(PlayersWithJob(c, AttackJob.Support).Length, 1, cx + "," + cy);
                    Assert.LessOrEqual(PlayersWithJob(c, AttackJob.Forward).Length, 1, cx + "," + cy);
                    Assert.LessOrEqual(PlayersWithJob(c, AttackJob.Width).Length, 1, cx + "," + cy);
                    int jobs = PlayersWithJob(c, AttackJob.Support).Length + PlayersWithJob(c, AttackJob.Forward).Length + PlayersWithJob(c, AttackJob.Width).Length;
                    Assert.LessOrEqual(jobs, 3);
                    Assert.AreEqual(4 - jobs, PlayersWithJob(c, AttackJob.GetOpen).Length, "everybody else gets open: " + cx + "," + cy);
                    Assert.AreEqual(AttackJob.None, JobOfPlayer(c, "hs3"), "the carrier has no job");
                    Assert.AreEqual(AttackJob.None, JobOfPlayer(c, "hs0"), "nor the keeper");
                }
        }

        [Test]
        public void TheLimits_AreConfigurable()
        {
            MatchController c = AttackScene();
            var more = new AttackPositioningTuning { SupportRunners = 2, ForwardRunners = 0, WidthHolders = 0 };
            Assert.AreEqual(2, PlayersWithJob(c, AttackJob.Support, more).Length); Assert.AreEqual(0, PlayersWithJob(c, AttackJob.Forward, more).Length); Assert.AreEqual(0, PlayersWithJob(c, AttackJob.Width, more).Length);
            Assert.AreEqual(2, PlayersWithJob(c, AttackJob.GetOpen, more).Length);
        }

        [Test]
        public void NotEveryoneRunsToTheBall_OrToTheGoal_AndTheTargetsAreApart()
        {
            MatchController c = AttackScene(-2f, 1f);
            WorldView v = ViewOf(c);
            var targets = new List<Vec2>();
            foreach (string id in AttackField.Where(x => x != "hs3"))
            {
                PlayerDecision d = EngineOf(c).Decide(v, id);
                Vec2 target = d.Movement.HasTarget ? d.Movement.Target : PosOf(c, id, TeamSide.Home);
                targets.Add(target);
                Assert.Greater((target - new Vec2(-2f, 1f)).Magnitude, 3f, id + " does not run to the ball");
                Assert.Less(Math.Abs(target.X - HomeOf(c, id).X), 12f, id + " does not run away from his place");
            }
            Assert.LessOrEqual(targets.Count(t => t.X > 8f), 2, "not all at the goal");
            for (int i = 0; i < targets.Count; i++) for (int j = i + 1; j < targets.Count; j++) Assert.Greater((targets[i] - targets[j]).Magnitude, 1.5f, "the targets are not on top of each other");
        }

        // ================= the roles are a preference =================

        [Test]
        public void TheRoles_LeanTowardsTheirJobs_TheCreatorAndTheBuilderTowardsSupport_TheWingerTowardsWidth_TheFinisherTowardsRunning()
        {
            var e = new AttackSupport(new BasicDecisionTuning(), new AttackPositioningTuning());
            WorldPlayer Of(PlayerArchetype r) { return new WorldPlayer { Roles = new List<RoleAffinity> { new RoleAffinity(r, 90) } }; }
            Assert.Greater(e.RoleBias(Of(PlayerArchetype.Creator), AttackJob.Support), e.RoleBias(Of(PlayerArchetype.Finisher), AttackJob.Support));
            Assert.Greater(e.RoleBias(Of(PlayerArchetype.Builder), AttackJob.Support), e.RoleBias(Of(PlayerArchetype.Winger), AttackJob.Support));
            Assert.Greater(e.RoleBias(Of(PlayerArchetype.Winger), AttackJob.Width), e.RoleBias(Of(PlayerArchetype.Finisher), AttackJob.Width));
            Assert.Greater(e.RoleBias(Of(PlayerArchetype.Finisher), AttackJob.Forward), e.RoleBias(Of(PlayerArchetype.Builder), AttackJob.Forward));
            Assert.Greater(e.RoleBias(Of(PlayerArchetype.Creator), AttackJob.Support), 0f);
            foreach (PlayerArchetype r in Enum.GetValues(typeof(PlayerArchetype))) foreach (AttackJob j in new[] { AttackJob.Support, AttackJob.Forward, AttackJob.Width })
                Assert.LessOrEqual(e.RoleBias(Of(r), j), new AttackPositioningTuning().RoleBiasWeight + 0.0001f, "a role is a bounded preference: " + r + " " + j);
            Assert.AreEqual(0f, e.RoleBias(new WorldPlayer(), AttackJob.Width), 0.0001f, "no roles, no preference");
            Assert.Greater(e.RoleBias(new WorldPlayer { Role = PlayerRole.Forward }, AttackJob.Forward), 0f, "without profiles the coarse position still leans a little");
        }

        [Test]
        public void TheRole_BreaksATieBetweenPlayersInTheSameSituation_InBothDirections()
        {
            var flat = new AttackPositioningTuning { SupportHomeWeight = 0f };
            foreach (bool swap in new[] { false, true })
            {
                // hs1 and hs2 stand symmetrically about the carrier: the situation is the same for both
                MatchController c = AttackScene(0f, 0f, swap ? new[] { "hs1:Finisher", "hs2:Creator" } : new[] { "hs1:Creator", "hs2:Finisher" });
                Put(c, TeamSide.Home, "hs1", -6f, 5f, East); Put(c, TeamSide.Home, "hs2", -6f, -5f, East);
                Put(c, TeamSide.Home, "hs4", -17f, 11f, East); Put(c, TeamSide.Home, "hs5", -17f, -11f, East);
                string creator = swap ? "hs2" : "hs1";
                Assert.AreEqual(creator, PlayersWithJob(c, AttackJob.Support, flat).Single(), "the Creator offers himself (swapped: " + swap + ")");
            }
        }

        [Test]
        public void NoRoleForcesADecision_AWingerWhoIsTheBestPlacedSupports_AFinisherWhoIsTiredDoesNotRun()
        {
            MatchController c = AttackScene(0f, 0f, "hs4:Winger", "hs5:Creator");
            Put(c, TeamSide.Home, "hs4", -6f, 3f, East);   // the winger is the one at a supporting distance
            Put(c, TeamSide.Home, "hs5", 17f, -11f, East); // the creator is far away (out of reach of a pass)
            Put(c, TeamSide.Home, "hs1", -17f, 10f, East); Put(c, TeamSide.Home, "hs2", -17f, -10f, East);
            Assert.AreEqual(AttackJob.Support, JobOfPlayer(c, "hs4"), "context first: the nearest useful player supports, whatever his role");
            Assert.AreNotEqual(AttackJob.Support, JobOfPlayer(c, "hs5"));

            MatchController d = AttackScene(-4f, 0f, "hs5:Finisher");
            d.State.Home.Find("hs5").Locomotion.CurrentStamina = 5f;
            Assert.AreNotEqual(AttackJob.Forward, JobOfPlayer(d, "hs5"), "the role does not override the legs");
        }

        // ================= the receiver score =================

        [Test]
        public void ReceiverScore_PrefersAFreeReceiverToAMarkedOne_AtTheSameDistance()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Home, "hs4", 6f, 5f, East); Put(c, TeamSide.Home, "hs5", 6f, -5f, East);
            Put(c, TeamSide.Away, "as1", 7f, 5.5f, West);
            WorldView v = ViewOf(c);
            var t = new BasicDecisionTuning(); var a = new AttackPositioningTuning();
            float marked = ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs4"), false, false, t, a, out _), free = ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs5"), false, false, t, a, out _);
            Assert.Greater(free, marked);
        }

        [Test]
        public void ReceiverScore_RejectsABlockedLane_ARival_ATooCloseOrTooFarMate_AndTheKeeperUnlessAsked()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Home, "hs4", 9f, 0f, East); Put(c, TeamSide.Home, "hs5", 1f, 1f, East); Put(c, TeamSide.Home, "hs1", -15f, 0f, East);
            Put(c, TeamSide.Away, "as1", 4.5f, 0.1f, West);
            WorldView v = ViewOf(c);
            var t = new BasicDecisionTuning(); var a = new AttackPositioningTuning();
            int me = v.IndexOf("hs3");
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("hs4"), false, false, t, a, out _)), "an opponent in the lane");
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("as1"), false, false, t, a, out _)), "never a rival");
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("hs5"), false, false, t, a, out _)), "too close");
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("hs3"), false, false, t, a, out _)), "not himself");
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("hs0"), true, false, t, a, out _)), "the keeper only when asked");
            Assert.IsFalse(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, me, v.IndexOf("hs1"), true, true, t, a, out _)) && false);
        }

        [Test]
        public void ReceiverScore_DoesNotDependOnlyOnDistance_AFartherFreeForwardMateBeatsANearerMarkedBackwardOne()
        {
            MatchController c = AttackScene(-6f, 0f);
            Put(c, TeamSide.Home, "hs4", -9f, 4f, East);   // near and a bit behind, with an opponent next to him
            Put(c, TeamSide.Home, "hs5", 4f, -3f, East);   // farther, ahead, free
            Put(c, TeamSide.Away, "as1", -9.5f, 5.5f, West);
            WorldView v = ViewOf(c);
            var t = new BasicDecisionTuning(); var a = new AttackPositioningTuning();
            int me = v.IndexOf("hs3");
            Assert.Greater(ReceiverScore.Evaluate(v, me, v.IndexOf("hs5"), true, false, t, a, out _), ReceiverScore.Evaluate(v, me, v.IndexOf("hs4"), true, false, t, a, out _));
            var engine = EngineOf(c);
            PlayerDecision d = engine.Decide(v, "hs3");
            if (d.Kind == DecisionKind.Pass) Assert.AreEqual("hs5", d.TargetPlayerId);
        }

        [Test]
        public void ReceiverScore_LikesAReceiverRunningOntoTheBall_MoreThanOneComingBack()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Home, "hs4", 8f, 4f, East); Put(c, TeamSide.Home, "hs5", 8f, -4f, East);
            var t = new BasicDecisionTuning(); var a = new AttackPositioningTuning();
            Vec2 away = ActionModel.DirectionOr(new Vec2(8f, 4f), East), back = ActionModel.DirectionOr(new Vec2(-8f, -4f), East);
            WorldView v = ViewOf(c);
            float still = ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs4"), false, false, t, a, out _);
            c.State.Home.Find("hs4").Locomotion.Velocity = away * 4f; c.State.Home.Find("hs4").Locomotion.CurrentSpeed = 4f; c.State.Home.Find("hs5").Locomotion.Velocity = back * 4f; c.State.Home.Find("hs5").Locomotion.CurrentSpeed = 4f;
            v = ViewOf(c);
            float running = ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs4"), false, false, t, a, out _), returning = ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs5"), false, false, t, a, out _);
            Assert.Greater(running, returning);
            Assert.Greater(running, still - 0.0001f);
        }

        [Test]
        public void ReceiverScore_WithTheAttackLayerOff_IsThePhase9Score_AnOpponentBehindThePasserStillBlocks()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Home, "hs4", 8f, 0f, East);
            Put(c, TeamSide.Away, "as1", -0.5f, 0f, East); // behind the passer
            WorldView v = ViewOf(c);
            var t = new BasicDecisionTuning();
            Assert.IsTrue(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs4"), false, false, t, new AttackPositioningTuning { Enabled = false }, out _)), "Phase 9's lane test counts him");
            Assert.IsFalse(float.IsNegativeInfinity(ReceiverScore.Evaluate(v, v.IndexOf("hs3"), v.IndexOf("hs4"), false, false, t, new AttackPositioningTuning(), out _)), "he cannot stop a ball that goes the other way");
        }

        // ================= transitions =================

        [Test]
        public void JustAfterWinningTheBall_TheRunnerGoesFullSpeed_ThenAtTheRunningPace()
        {
            MatchController c = AttackScene(-4f, 0f);
            string id = PlayersWithJob(c, AttackJob.Forward).Single();
            var tuning = new BasicDecisionTuning(); var attack = new AttackPositioningTuning();
            Assert.Less(c.State.Ball.SecondsSincePossessionChange, attack.RecoverySeconds);
            PlayerDecision fresh = EngineOf(c).Decide(ViewOf(c), id);
            Assert.AreEqual(DecisionKind.ForwardRun, fresh.Kind);
            if (fresh.Movement.HasTarget) Assert.AreEqual(tuning.ChaseMagnitude, fresh.Movement.Magnitude, 0.001f, "the first moments after the recovery: the most advanced runs");
            c.State.Ball.SecondsSincePossessionChange = attack.RecoverySeconds + 1f;
            PlayerDecision later = EngineOf(c).Decide(ViewOf(c), id);
            if (later.Movement.HasTarget && later.Kind == DecisionKind.ForwardRun) Assert.AreEqual(attack.ForwardRunMagnitude, later.Movement.Magnitude, 0.001f);
        }

        [Test]
        public void AfterLosingTheBall_ThePhase9DefenceIsUnchanged_TheNearestClosesAndTheRestGoBack()
        {
            MatchController c = AttackScene();
            Carrier(c, TeamSide.Away, "as2", -2f, 0f, West);
            PlayerDecision near = EngineOf(c).Decide(ViewOf(c), "hs3");
            Assert.AreEqual(DecisionKind.CloseDown, near.Kind); Assert.AreEqual(DecisionReason.PrimaryDefender, near.Reason);
            int closing = AttackField.Count(id => EngineOf(c).Decide(ViewOf(c), id).Kind == DecisionKind.CloseDown);
            Assert.LessOrEqual(closing, 2);
            foreach (string id in AttackField) Assert.IsFalse(new[] { DecisionKind.Support, DecisionKind.ForwardRun, DecisionKind.HoldWidth, DecisionKind.GetOpen }.Contains(EngineOf(c).Decide(ViewOf(c), id).Kind), id + " is defending now");
        }

        // ================= the goalkeeper has the ball =================

        [Test]
        public void WhenTheKeeperHasTheBall_AFieldPlayerOffersHimselfNearBy_WithoutLeavingHisStructure()
        {
            MatchController c = AttackScene();
            Carrier(c, TeamSide.Home, "hs0", -19f, 0f, East);
            BasicDecisionEngine e = EngineOf(c);
            WorldView v = ViewOf(c);
            e.Attack.Assign(v, v.IndexOf("hs0"));
            string supporter = AttackField.Single(id => e.Attack.JobOf(v.IndexOf(id)) == AttackJob.Support);
            PlayerDecision d = e.Decide(v, supporter);
            Assert.AreEqual(DecisionKind.Support, d.Kind);
            if (d.Movement.HasTarget)
            {
                Assert.LessOrEqual((d.Movement.Target - new Vec2(-19f, 0f)).Magnitude, new BasicDecisionTuning().SupportMaxDistance + 1f);
                Assert.LessOrEqual((d.Movement.Target - HomeOf(c, supporter)).Magnitude, new AttackPositioningTuning().JobMaxHomeDeviation + 0.01f);
            }
        }

        // ================= purity, determinism, authority =================

        [Test]
        public void TheAttackDecisions_ArePureAndDeterministic_AndNeverTouchTheMatch()
        {
            MatchController c = AttackScene(-3f, 2f, "hs1:Creator", "hs2:Guardian", "hs4:Winger", "hs5:Finisher");
            Put(c, TeamSide.Away, "as1", 4f, 3f, West); Put(c, TeamSide.Away, "as2", 6f, -4f, West);
            string before = Fingerprint(c);
            var first = new List<string>(); var second = new List<string>();
            foreach (string id in AttackField) { PlayerDecision d = EngineOf(c).Decide(ViewOf(c), id); first.Add(id + d.Kind + d.Reason + d.Movement.Target + d.Movement.Magnitude + d.HasAction); }
            var shared = EngineOf(c);
            for (int round = 0; round < 3; round++)
            {
                second.Clear();
                foreach (string id in AttackField) { PlayerDecision d = shared.Decide(ViewOf(c), id); second.Add(id + d.Kind + d.Reason + d.Movement.Target + d.Movement.Magnitude + d.HasAction); }
                CollectionAssert.AreEqual(first, second, "the same world, the same answers, from a new engine or an old one");
            }
            Assert.AreEqual(before, Fingerprint(c), "deciding changes nothing in the match");
        }

        [Test]
        public void TheAttackNumbers_LiveInOnePlace_NoLooseNumbersInTheAttackFiles()
        {
            foreach (var f in typeof(AttackPositioningDefaults).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var mirror = typeof(AttackPositioningTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(new AttackPositioningTuning())), f.Name);
                Assert.Greater(Convert.ToSingle(f.GetValue(null)), 0f, f.Name);
            }
            foreach (var f in typeof(AttackPositioningTuning).GetFields()) Assert.IsNotNull(typeof(AttackPositioningDefaults).GetField(f.Name), f.Name + " has no default");
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string file in new[] { Path.Combine(core, "Decision", "AttackSupport.cs"), Path.Combine(core, "Decision", "ReceiverScore.cs"), Path.Combine(core, "Decision", "BasicDecisionEngine.cs"), Path.Combine(core, "Decision", "BasicDecisionDriver.cs") })
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//") || line.StartsWith("///")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(Path.GetFileName(file) + ": loose number " + m.Value + " in: " + line);
                }
            foreach (string name in new[] { "AttackSupport.cs", "ReceiverScore.cs", "AttackPositioningDefaults.cs" })
            {
                string text = File.ReadAllText(Path.Combine(core, "Decision", name));
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "Linq" }) StringAssert.DoesNotContain(banned, text, name);
            }
        }

        [Test]
        public void TheDecisionKinds_AreStillAShortClosedList_AndThePlayerRolesComeFromTheWorldView()
        {
            Assert.AreEqual(17, Enum.GetValues(typeof(DecisionKind)).Length);
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            string support = File.ReadAllText(Path.Combine(core, "Decision", "AttackSupport.cs"));
            StringAssert.DoesNotContain("PlayerDefinition", support); StringAssert.DoesNotContain("PlayerPlayingProfile", support); StringAssert.DoesNotContain("MatchState", support);
            StringAssert.DoesNotContain(".Reason", support);
        }
    }
}
