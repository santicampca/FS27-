using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 7: formations, placing, kick-off and the restart contract.</summary>
    public partial class AiInfrastructureTests
    {
        // ================= formations -> starting positions (the 12 real teams) =================

        private static IEnumerable<int> TeamIndexes { get { return Enumerable.Range(0, 12); } }

        [Test]
        public void TheRealDatabaseHasTwelveTeams_EachWithAPlayableFormationOfSixSlots()
        {
            Assert.AreEqual(12, Db.Teams.Count);
            foreach (TeamDefinition t in Db.Teams)
            {
                Assert.IsTrue(Db.TryGetFormation(t, out FormationDefinition f), t.Id);
                Assert.AreEqual(6, f.Positions.Count, t.Id); Assert.AreEqual(1, f.Positions.Count(p => p.Role == PlayerRole.Goalkeeper), t.Id); Assert.AreEqual(5, f.Positions.Count(p => p.Role != PlayerRole.Goalkeeper), t.Id);
                Assert.IsTrue(Rosters.TryGetRoster(t.Id, out TeamRoster roster), t.Id);
                Assert.IsTrue(roster.TryToMatchSquad(Db.Players, out MatchSquad squad, out string problem), t.Id + ": " + problem);
                Assert.AreEqual(6, squad.Starters.Length); Assert.AreEqual(1, squad.Starters.Count(m => m.IsGoalkeeper)); Assert.AreEqual(4, squad.Bench.Length);
            }
        }

        [Test]
        public void EveryRealFormation_ProducesTwelveValidStartingPositions_ForBothTeams()
        {
            foreach (int i in TeamIndexes)
            {
                MatchController c = RealMatch(i, (i + 1) % 12);
                Assert.IsTrue(c.PlaceFormation(TeamSide.Home), "home " + Db.Teams[i].Id); Assert.IsTrue(c.PlaceFormation(TeamSide.Away), "away");
                foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                    Assert.GreaterOrEqual(PlayerSeparation.Smallest(c.State.Team(side).Players.Where(p => p.IsActive).Select(p => p.Locomotion.Position).ToArray(), 6), c.Infrastructure.MinSeparation * 0.95f, "one team overlaps itself: " + Db.Teams[i].Id);
                Assert.IsTrue(c.PrepareRestart());
                var points = new List<Vec2>();
                foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                {
                    TeamMatchState team = c.State.Team(side);
                    var onPitch = team.Players.Where(p => p.IsActive).ToList();
                    Assert.AreEqual(6, onPitch.Count); Assert.AreEqual(1, onPitch.Count(p => p.InGoal)); Assert.AreEqual(5, onPitch.Count(p => !p.InGoal));
                    foreach (MatchPlayer p in onPitch)
                    {
                        Vec2 pos = p.Locomotion.Position;
                        Assert.IsTrue(c.Pitch.Contains(pos), p.PlayerId + " outside the lines at " + pos.X + "," + pos.Y);
                        Assert.IsTrue(PitchBounds.InOwnHalf(pos, c.State.HomeAttacksPositiveX, side), p.PlayerId + " is not in his own half for the kick-off");
                        if (p.InGoal) Assert.IsTrue(c.Pitch.IsInGoalZone(pos, c.State.HomeAttacksPositiveX, side), p.PlayerId + " the keeper must stand in the goal zone");
                        points.Add(pos);
                    }
                }
                Assert.AreEqual(12, points.Count);
                Assert.GreaterOrEqual(PlayerSeparation.Smallest(points.ToArray(), 12), c.Infrastructure.MinSeparation * 0.95f, "no overlap at the start, team " + Db.Teams[i].Id);
            }
        }

        [Test]
        public void TheFormationFacesTheRightWay_ForAttackingPlusXAndMinusX_AndAfterTheSideSwitch()
        {
            MatchController c = RealMatch(0, 1);
            c.PlaceFormation(TeamSide.Home); c.PlaceFormation(TeamSide.Away);
            float MeanX(TeamSide s) { return c.State.Team(s).Players.Where(p => p.IsActive && !p.InGoal).Average(p => p.Locomotion.Position.X); }
            float KeeperX(TeamSide s) { return c.State.Team(s).GoalkeeperInGoal.Locomotion.Position.X; }
            Assert.Greater(MeanX(TeamSide.Home), KeeperX(TeamSide.Home)); Assert.Less(MeanX(TeamSide.Away), KeeperX(TeamSide.Away));
            Assert.Less(KeeperX(TeamSide.Home), 0f); Assert.Greater(KeeperX(TeamSide.Away), 0f);
            Assert.AreEqual(East.ToHeading(), c.State.Home.GoalkeeperInGoal.Locomotion.Heading, 0.001f); Assert.AreEqual(West.ToHeading(), c.State.Away.GoalkeeperInGoal.Locomotion.Heading, 0.001f);
            c.State.HomeAttacksPositiveX = false;
            c.PlaceFormation(TeamSide.Home); c.PlaceFormation(TeamSide.Away);
            Assert.Greater(KeeperX(TeamSide.Home), 0f, "after the side switch the home keeper defends +X"); Assert.Less(KeeperX(TeamSide.Away), 0f);
            Assert.Less(MeanX(TeamSide.Home), KeeperX(TeamSide.Home));
        }

        [Test]
        public void TwoTeamsWithTheSameFormation_AreExactMirrorImages()
        {
            var bounds = new PitchBounds(new FieldDimensions(), false);
            foreach (TeamDefinition t in Db.Teams)
            {
                Db.TryGetFormation(t, out FormationDefinition f);
                var h = new List<PlacedPlayer>(); var a = new List<PlacedPlayer>();
                var ids = new[] { "f1", "f2", "f3", "f4", "f5" };
                Assert.IsTrue(FormationPlacement.Compute(f, bounds, true, TeamSide.Home, "gk", ids, true, h)); Assert.IsTrue(FormationPlacement.Compute(f, bounds, true, TeamSide.Away, "gk", ids, true, a));
                Assert.AreEqual(6, h.Count);
                for (int i = 0; i < 6; i++)
                {
                    Assert.AreEqual(-h[i].Position.X, a[i].Position.X, 0.0001f, t.Id + " slot " + i); Assert.AreEqual(-h[i].Position.Y, a[i].Position.Y, 0.0001f, t.Id + " slot " + i);
                    Assert.AreEqual(h[i].PlayerId, a[i].PlayerId);
                }
                Assert.AreEqual(East.ToHeading(), h[0].Heading, 0.001f); Assert.AreEqual(West.ToHeading(), a[0].Heading, 0.001f);
            }
        }

        [Test]
        public void FormationPlacement_RefusesWhatItCannotPlace_AndWritesNothing()
        {
            var bounds = new PitchBounds(new FieldDimensions(), false);
            Db.TryGetFormation(Db.Teams[0], out FormationDefinition f);
            var r = new List<PlacedPlayer>();
            Assert.IsFalse(FormationPlacement.Compute(null, bounds, true, TeamSide.Home, "gk", new[] { "a" }, true, r)); Assert.AreEqual(0, r.Count);
            Assert.IsFalse(FormationPlacement.Compute(f, bounds, true, TeamSide.Home, "gk", new[] { "a", "b" }, true, r), "too few field players");
            Assert.IsFalse(FormationPlacement.Compute(f, bounds, true, TeamSide.Home, null, new[] { "a", "b", "c", "d", "e" }, true, r), "no keeper");
            var noKeeper = new FormationDefinition("x", "x", new FormationPosition(0, PlayerRole.Defender, 0.2f, 0.5f));
            Assert.IsFalse(FormationPlacement.Compute(noKeeper, bounds, true, TeamSide.Home, "gk", new[] { "a" }, true, r));
            MatchController c = Live();
            Assert.IsFalse(c.PlaceFormation(TeamSide.Home), "a team with no formation is left as it is");
        }

        [Test]
        public void TheFormationDoesNotCareWhereTheKeeperIsInTheSquadList_TheKeeperInGoalTakesTheGoalSlot()
        {
            foreach (int i in TeamIndexes)
            {
                MatchController c = RealMatch(i, (i + 1) % 12);
                Assert.IsTrue(c.PlaceFormation(TeamSide.Home));
                Assert.IsTrue(c.Pitch.IsInGoalZone(c.State.Home.GoalkeeperInGoal.Locomotion.Position, true, TeamSide.Home), Db.Teams[i].Id);
            }
        }

        // ================= placing players =================

        [Test]
        public void PlacePlayer_PlacesPositionHeadingAndStamina_AndRefusesWhoIsNotOnThePitch()
        {
            MatchController c = Live();
            Assert.IsTrue(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "hs3", Position = new Vec2(4f, -2f), Heading = 1.25f, SetStamina = true, Stamina = 37f }));
            MatchPlayer p = c.State.Home.Find("hs3");
            Assert.AreEqual(new Vec2(4f, -2f), p.Locomotion.Position); Assert.AreEqual(1.25f, p.Locomotion.Heading, 0.0001f); Assert.AreEqual(37f, p.Locomotion.CurrentStamina, 0.0001f); Assert.AreEqual(0f, p.Locomotion.CurrentSpeed);
            Assert.IsTrue(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "hs3", Position = Vec2.Zero, SetStamina = true, Stamina = 400f }));
            Assert.AreEqual(100f, p.Locomotion.CurrentStamina, 0.0001f, "clamped to the tank");
            Assert.IsTrue(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "hs3", Position = Vec2.Zero }));
            Assert.AreEqual(100f, p.Locomotion.CurrentStamina, "stamina is kept when it is not set");
            Assert.IsFalse(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "hb1", Position = Vec2.Zero }), "a bench player cannot be placed");
            Assert.IsFalse(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "nobody", Position = Vec2.Zero }));
        }

        // ================= kick-off =================

        [Test]
        public void Kickoff_IsTakenByTheRightTeam_WithAnExplicitTaker_TheBallAtTheCentre()
        {
            MatchController c = RealMatch(0, 1);
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(RestartKind.KickOff, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Home, c.State.PendingRestart.Team); Assert.IsNull(c.State.PendingRestart.TakerId); Assert.AreEqual(RestartStatus.Pending, c.State.PendingRestart.Status);
            string taker = c.State.Home.Players[3].PlayerId;
            Assert.IsTrue(c.AssignRestartTaker(taker));
            Assert.IsTrue(c.PrepareRestart()); Assert.AreEqual(RestartStatus.Placed, c.State.PendingRestart.Status); Assert.AreEqual(taker, c.State.PendingRestart.TakerId);
            Assert.AreEqual(Vec2.Zero, new Vec2(c.State.Ball.Physics.Position.X, c.State.Ball.Physics.Position.Z)); Assert.IsFalse(c.State.Ball.HasPossession, "nothing is given before the restart is confirmed");
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(RestartStatus.Confirmed, c.State.PendingRestart.Status); Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.IsTrue(c.State.Ball.HasPossession); Assert.AreEqual(TeamSide.Home, c.State.Ball.PossessingTeam); Assert.AreEqual(taker, c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(BallMode.Controlled, c.State.Ball.Mode);
        }

        [Test]
        public void Kickoff_TheFirstPlayerOfTheRoster_NeverGetsTheBallJustForBeingFirst()
        {
            for (int i = 0; i < 12; i++)
            {
                MatchController c = RealMatch(i, (i + 5) % 12);
                string first = c.State.Home.Players[0].PlayerId;
                Assert.IsTrue(c.ConfirmRestart());
                Assert.AreNotEqual(first, c.State.Ball.PossessingPlayerId, "team " + i + ": the keeper at the top of the list must not take the kick-off");
                Assert.IsFalse(c.State.Home.Find(c.State.Ball.PossessingPlayerId).InGoal);
            }
            MatchController legacy = Live(confirm: false);
            Park(legacy); Assert.IsTrue(legacy.ConfirmRestartReady());
            Assert.IsFalse(legacy.State.Ball.HasPossession, "the older confirmation gives the ball to nobody");
        }

        [Test]
        public void Kickoff_TheDefaultTaker_IsTheFieldPlayerNearestToTheBall_Deterministically()
        {
            MatchController c = RealMatch(2, 3);
            c.PlaceFormation(TeamSide.Home); c.PlaceFormation(TeamSide.Away);
            MatchPlayer taker = c.DefaultTaker(c.State.PendingRestart);
            Assert.IsFalse(taker.InGoal); Assert.AreEqual(TeamSide.Home, taker.Team);
            float best = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).Min(p => p.Locomotion.Position.Magnitude);
            Assert.AreEqual(best, taker.Locomotion.Position.Magnitude, 0.0001f);
            Assert.AreEqual(taker.PlayerId, c.DefaultTaker(c.State.PendingRestart).PlayerId);
        }

        [Test]
        public void Kickoff_EverybodyElseIsOutOfTheCircleAndInHisOwnHalf_SoNobodyGetsTheBallByProximity()
        {
            MatchController c = RealMatch(4, 7);
            Assert.IsTrue(c.ConfirmRestart());
            string taker = c.State.Ball.PossessingPlayerId;
            Vec2 centre = Vec2.Zero;
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away })
                foreach (MatchPlayer p in t.Players.Where(x => x.IsActive && x.PlayerId != taker))
                {
                    Assert.GreaterOrEqual((p.Locomotion.Position - centre).Magnitude, c.Infrastructure.RestartClearRadius - 0.01f, p.PlayerId);
                    Assert.IsTrue(PitchBounds.InOwnHalf(p.Locomotion.Position, c.State.HomeAttacksPositiveX, p.Team), p.PlayerId + " in the wrong half");
                }
            Step(c, 12);
            Assert.AreEqual(taker, c.State.Ball.PossessingPlayerId, "nobody took the ball off him in the first moments");
            Assert.IsTrue(c.State.Ball.HasPossession);
        }

        [Test]
        public void Kickoff_TheTakerStandsNextToTheBallFacingTheGoalHeAttacks()
        {
            MatchController c = RealMatch(0, 1);
            Assert.IsTrue(c.ConfirmRestart());
            MatchPlayer taker = c.BallCarrier;
            Assert.AreEqual(-c.Infrastructure.TakerOffset, taker.Locomotion.Position.X, 0.01f); Assert.AreEqual(East.ToHeading(), taker.Locomotion.Heading, 0.001f);
            MatchController away = RealMatch(0, 1, settings: new MatchSettings { Mode = GameMode.Classic, DurationSeconds = 300f, KickoffTeam = TeamSide.Away });
            Assert.IsTrue(away.ConfirmRestart());
            Assert.AreEqual(TeamSide.Away, away.State.Ball.PossessingTeam); Assert.AreEqual(c.Infrastructure.TakerOffset, away.BallCarrier.Locomotion.Position.X, 0.01f); Assert.AreEqual(West.ToHeading(), away.BallCarrier.Locomotion.Heading, 0.001f);
        }

        [Test]
        public void SecondHalfAndExtraTime_RestartWithTheOtherTeam_PlacedForTheNewDirection()
        {
            var s = MatchSettings.CreateClassic(CompetitionMatchType.Knockout); s.ExtraTimePeriodSeconds = 60f; s.Seed = 3;
            MatchController c = RealMatch(0, 1, settings: s);
            Assert.IsTrue(c.ConfirmRestart());
            int guard = 0;
            while (c.State.Period == MatchPeriod.FirstHalf && guard++ < 5000) { c.Advance(1f); }
            while (c.State.Phase == MatchPhase.Break && guard++ < 5000) c.Advance(1f);
            Assert.AreEqual(MatchPeriod.SecondHalf, c.State.Period); Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team, "the other team kicks off the second half"); Assert.IsFalse(c.State.HomeAttacksPositiveX, "sides switched");
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(TeamSide.Away, c.State.Ball.PossessingTeam); Assert.IsFalse(c.BallCarrier.InGoal);
            Assert.Greater(c.State.Home.GoalkeeperInGoal.Locomotion.Position.X, 0f, "the home keeper now defends +X"); Assert.Less(c.State.Away.GoalkeeperInGoal.Locomotion.Position.X, 0f);
            Assert.AreEqual(West.ToHeading(), c.State.Home.GoalkeeperInGoal.Locomotion.Heading, 0.001f);
            while (c.State.Period == MatchPeriod.SecondHalf && guard++ < 9000) c.Advance(1f);
            Assert.AreEqual(MatchPeriod.ExtraTimeFirstHalf, c.State.Period, "level after regulation in a knockout: extra time");
            Assert.IsTrue(c.ConfirmRestart(), "extra time restarts the same way"); Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.IsTrue(c.State.Ball.HasPossession);
        }

        // ================= the restart contract =================

        [Test]
        public void RestartRequest_CarriesKindTeamPositionTakerAndStatus_AndTheKindsAreAppendOnly()
        {
            var r = new RestartRequest(RestartKind.Corner, TeamSide.Away, new Vec2(19.5f, 12f));
            Assert.AreEqual(RestartKind.Corner, r.Kind); Assert.AreEqual(TeamSide.Away, r.Team); Assert.AreEqual(new Vec2(19.5f, 12f), r.Position); Assert.IsNull(r.TakerId); Assert.AreEqual(RestartStatus.Pending, r.Status);
            Assert.AreEqual(0, (int)RestartKind.KickOff); Assert.AreEqual(1, (int)RestartKind.ThrowIn); Assert.AreEqual(2, (int)RestartKind.GoalKick); Assert.AreEqual(3, (int)RestartKind.Corner); Assert.AreEqual(4, (int)RestartKind.FreeKick); Assert.AreEqual(5, (int)RestartKind.Penalty);
            Assert.AreEqual(0, (int)RestartStatus.Pending); Assert.AreEqual(2, (int)RestartStatus.Confirmed);
            Assert.AreEqual(MatchPhase.Shootout, (MatchPhase)8, "the shoot-out remains a phase of its own, not a restart");
        }

        private static void SendBallOut(MatchController c, float x, float z, TeamSide lastTouch)
        {
            c.State.Ball.Physics = new BallState(new Vec3(x, c.BallParameters.Radius, z), Vec3.Zero) { IsGrounded = true };
            c.ReleaseBall(); c.RegisterTouch(lastTouch, lastTouch == TeamSide.Home ? "hs2" : "as2");
            c.Tick(Dt, new BallObservation(new Vec3(x, c.BallParameters.Radius, z), c.BallParameters.Radius));
        }

        [Test]
        public void ThrowIn_GoalKick_AndCorner_AreSetUpWithAnExplicitTaker_AndPlayResumesWithHim()
        {
            // throw-in: the opponent of the last toucher, at the line, taken by a field player
            MatchController c = Live();
            SendBallOut(c, 5f, 13.5f, TeamSide.Home);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(RestartKind.ThrowIn, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Put(c, TeamSide.Away, "as3", 5f, 5f, North);
            Assert.IsTrue(c.AssignRestartTaker("as3")); Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual("as3", c.State.Ball.PossessingPlayerId); Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(RestartStatus.Confirmed, c.State.PendingRestart.Status);

            // goal kick: the goalkeeper by default
            MatchController g = Live();
            SendBallOut(g, 20.6f, 4f, TeamSide.Home);
            Assert.AreEqual(RestartKind.GoalKick, g.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, g.State.PendingRestart.Team);
            Assert.IsTrue(g.ConfirmRestart()); Assert.AreEqual("as0", g.State.Ball.PossessingPlayerId, "a goal kick is taken by the goalkeeper in goal");

            // corner: a field player, the nearest to the corner
            MatchController k = Live();
            SendBallOut(k, 20.6f, 3f, TeamSide.Away);
            Assert.AreEqual(RestartKind.Corner, k.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Home, k.State.PendingRestart.Team);
            Put(k, TeamSide.Home, "hs4", 17f, 11f, East);
            Assert.IsTrue(k.ConfirmRestart()); Assert.AreEqual("hs4", k.State.Ball.PossessingPlayerId, "the nearest field player to the spot");
        }

        [Test]
        public void ARestart_KeepsTheOthersAwayFromTheBall_OpponentsAtLeastTheClearRadius()
        {
            MatchController c = Live();
            SendBallOut(c, 5f, 13.5f, TeamSide.Home);
            Put(c, TeamSide.Away, "as3", 5f, 8f, North); Put(c, TeamSide.Home, "hs2", 5.5f, 12.2f, North); Put(c, TeamSide.Home, "hs3", 4.5f, 12.4f, North);
            c.AssignRestartTaker("as3");
            Assert.IsTrue(c.ConfirmRestart());
            foreach (string id in new[] { "hs2", "hs3" })
                Assert.GreaterOrEqual((c.State.Home.Find(id).Locomotion.Position - c.State.PendingRestart.Position).Magnitude, c.Infrastructure.RestartClearRadius - 0.01f, id);
            Assert.AreEqual("as3", c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void TheTaker_MustBeAnActivePlayerOfTheRestartingTeam_AndTheRestartMustBePending()
        {
            MatchController c = Live(confirm: false);
            Assert.IsFalse(c.AssignRestartTaker("as1"), "the other team"); Assert.IsFalse(c.AssignRestartTaker("hb1"), "a bench player"); Assert.IsFalse(c.AssignRestartTaker("nobody"));
            c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            Assert.IsFalse(c.AssignRestartTaker("hs4"), "a dismissed player");
            Assert.IsTrue(c.AssignRestartTaker("hs5")); Assert.AreEqual("hs5", c.State.PendingRestart.TakerId);
            Park(c); Assert.IsTrue(c.ConfirmRestart());
            Assert.IsFalse(c.AssignRestartTaker("hs2"), "play is on: there is nothing to assign"); Assert.IsFalse(c.PrepareRestart()); Assert.IsFalse(c.ConfirmRestart());
            Assert.AreEqual("hs5", c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void ARestart_CannotStartWhileATeamStillNeedsAGoalkeeper()
        {
            MatchController c = Live();
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            SendBallOut(c, 5f, 13.5f, TeamSide.Away);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.IsTrue(c.State.Home.NeedsGoalkeeper);
            Assert.IsFalse(c.ConfirmRestart(), "the goal must be filled first");
            Assert.AreEqual(SubstitutionResult.Accepted, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs3"));
            Assert.IsTrue(c.ConfirmRestart());
        }

        [Test]
        public void PlacingThePlayerWhoTakesTheRestart_IsEnoughToMakeHimTheTaker()
        {
            MatchController c = Live(confirm: false);
            Assert.IsTrue(c.PlacePlayer(new PlayerPlacement { Side = TeamSide.Home, PlayerId = "hs2", Position = new Vec2(-0.8f, 0f), Heading = East.ToHeading(), TakesRestart = true }));
            Assert.AreEqual("hs2", c.State.PendingRestart.TakerId);
            Park(c); c.PlacePlayer(TeamSide.Home, "hs2", new Vec2(-0.8f, 0f), East.ToHeading());
            Assert.IsTrue(c.ConfirmRestart()); Assert.AreEqual("hs2", c.State.Ball.PossessingPlayerId);
        }
    }
}
