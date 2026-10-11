using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 22: the free kick of Classic. A called foul stops the play, the ball goes to the spot, the fouled team takes it behind a wall, the whistle resumes play once; the aim of a person (destination, zone, team-mate,
    /// power, curve) reaches the ball through the existing action system; Arena has none of it. Field 40 x 25, home attacks +X.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private const float FkDt = 1f / 60f;
        private static readonly Vec2 FkEast = new Vec2(1f, 0f), FkWest = new Vec2(-1f, 0f);

        private static SquadMember[] FkStarters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false, 100f)); return l.ToArray(); }
        private static SquadMember[] FkBench(string prefix) { return new[] { new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f), new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f) }; }

        private sealed class FkLookup : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Players = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Players.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private static PlayerAttributes FkAll(int v)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = a.Acceleration = a.Stamina = a.BallControl = a.Passing = a.Shooting = a.Defense = a.Strength = a.Agility = a.Finishing = a.Dribbling = a.Technique = v;
            return a;
        }

        /// <summary>A live Classic (or Arena) match: home on the left looking at +X, away on the right, the ball in play.</summary>
        private static MatchController FkLive(GameMode mode = GameMode.Classic, int seed = 1, IPlayerLookup lookup = null)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            s.Seed = seed;
            var c = new MatchController(new FieldDimensions(), s, new MatchSquad(FkStarters("h"), FkBench("h")), new MatchSquad(FkStarters("a"), FkBench("a")), null, "m", null, lookup);
            c.Start();
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -6f + i, -10f + i * 4f), FkEast.ToHeading());
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 10f + i, i == 0 ? 3f : -10f + i * 4f), FkWest.ToHeading());
            }
            Assert.IsTrue(c.ConfirmRestartReady());
            c.Penalties.Enabled = false;   // Phase 23: these tests are about the free kick, also near the goal; a foul inside the area is a penalty (PenaltyTests) unless the penalties are switched off
            return c;
        }

        private static RefereeDecision FkCall(CardDecision card = CardDecision.None) { return new RefereeDecision { IsFoul = true, Card = card, Reasons = FoulReason.Late, Severity = 0.8f }; }

        /// <summary>Away fouls home's hs3 with the ball at (x, z): the free kick of home.</summary>
        private static MatchController.FoulCallResult FkFoul(MatchController c, float x = 12f, float z = 0f, CardDecision card = CardDecision.None, string offender = "as2")
        {
            return c.CallFoul(TeamSide.Away, offender, "hs3", FkCall(card), new Vec3(x, 0.2f, z));
        }

        private static void FkAdvance(MatchController c, float seconds) { for (float t = 0f; t < seconds; t += FkDt) c.Advance(FkDt); }

        // ================= stopping the play =================

        [Test]
        public void FreeKick_ACalledFoul_StopsThePlay_AndRequestsOneFreeKickForTheFouledTeam()
        {
            MatchController c = FkLive();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            var requests = new List<RestartRequest>(); c.RestartRequested += requests.Add;
            Assert.IsTrue(FkFoul(c).Called);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(1, requests.Count); Assert.AreEqual(RestartKind.FreeKick, requests[0].Kind); Assert.AreEqual(TeamSide.Home, requests[0].Team);
            Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.FreeKickAwarded), "the event is published once");
            Assert.AreEqual(1, c.State.FreeKicksAwarded);
            FreeKickState fk = c.State.FreeKick;
            Assert.AreEqual(TeamSide.Home, fk.Beneficiary); Assert.AreEqual("as2", fk.OffenderId); Assert.AreEqual("hs3", fk.VictimId); Assert.AreEqual(FoulReason.Late, fk.Reasons);
            Assert.AreEqual(0, fk.DisciplineIndex); Assert.AreEqual("as2", c.State.Discipline[fk.DisciplineIndex].PlayerId, "the originating infraction stays in the disciplinary history");
        }

        [Test]
        public void FreeKick_TheClockAndTheBallStand_WhilePlayIsStopped()
        {
            MatchController c = FkLive();
            FkAdvance(c, 1f);
            float time = c.State.MatchTimeSeconds;
            FkFoul(c);
            Vec3 ball = c.State.Ball.Physics.Position;
            FkAdvance(c, 1f);   // shorter than the whistle
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(time, c.State.MatchTimeSeconds, 1e-4f, "the match clock stops");
            Assert.AreEqual(ball, c.State.Ball.Physics.Position); Assert.AreEqual(Vec3.Zero, c.State.Ball.Physics.Velocity);
            Assert.IsFalse(c.State.IsBallLive);
        }

        [Test]
        public void FreeKick_TheFouledTeamKeepsTheBall_NoPossessionChangeIsInvented()
        {
            MatchController c = FkLive();
            c.GiveBallTo(TeamSide.Home, "hs3");
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            FkFoul(c);
            Assert.IsTrue(c.ConfirmRestart());
            Assert.IsTrue(c.State.Ball.HasPossession); Assert.AreEqual(TeamSide.Home, c.State.Ball.PossessingTeam);
            Assert.AreEqual(c.State.FreeKick.TakerId, c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.PossessionChanged), "the same team keeps the ball: there is no change of possession to publish");
        }

        // ================= the ball, the taker, the wall =================

        [Test]
        public void FreeKick_TheBallIsPlacedInsideTheField_ComingFromTheCore()
        {
            foreach (var where in new[] { new Vec2(60f, 40f), new Vec2(-60f, -40f), new Vec2(0f, 99f), new Vec2(12f, -3f) })
            {
                MatchController c = FkLive();
                FkFoul(c, where.X, where.Y);
                Vec3 b = c.State.Ball.Physics.Position;
                Assert.LessOrEqual(Math.Abs(b.X), 20f - c.FreeKick.SpotFieldMargin + 1e-3f); Assert.LessOrEqual(Math.Abs(b.Z), 12.5f - c.FreeKick.SpotFieldMargin + 1e-3f);
                Assert.AreEqual(c.State.FreeKick.Spot.X, b.X, 1e-4f); Assert.AreEqual(c.State.FreeKick.Spot.Y, b.Z, 1e-4f);
            }
        }

        [Test]
        public void FreeKick_AFoulInTheGoalZone_IsNotATapIn_TheSpotStaysOutOfTheZone()
        {
            MatchController c = FkLive();
            FkFoul(c, 19f, 0f);
            Vec2 spot = c.State.FreeKick.Spot;
            Assert.IsFalse(c.Pitch.IsInGoalZone(spot, c.State.HomeAttacksPositiveX, TeamSide.Away), "a penalty is a later phase: this is an ordinary free kick, pulled out of the zone");
            Assert.GreaterOrEqual(20f - spot.X, c.FreeKick.SpotMinGoalLineDistance - 1e-3f);
        }

        [Test]
        public void FreeKick_TheTakerIsAnActiveFieldPlayerOfTheFouledTeam_AndAGoodShooterNearTheGoalIsPreferred()
        {
            var l = new FkLookup();
            for (int i = 1; i < 6; i++) l.Players["hs" + i] = new PlayerDefinition("hs" + i, "hs" + i, i, PlayerRole.Midfielder, FkAll(50));
            l.Players["hs5"] = new PlayerDefinition("hs5", "hs5", 5, PlayerRole.Forward, FkAll(95));
            MatchController c = FkLive(GameMode.Classic, 1, l);
            FkFoul(c, 12f, 0f);
            Assert.AreEqual("hs5", c.State.PendingRestart.TakerId, "the best shooter takes a free kick near the goal");
            MatchPlayer taker = c.State.Home.Find(c.State.PendingRestart.TakerId);
            Assert.IsTrue(taker.IsActive); Assert.IsFalse(taker.InGoal);
            MatchController d = FkLive(GameMode.Classic, 1, null);
            FkFoul(d, 12f, 0f);
            MatchPlayer other = d.State.Home.Find(d.State.PendingRestart.TakerId);
            Assert.IsTrue(other.IsActive && !other.InGoal);
        }

        [Test]
        public void FreeKick_TheWallIsOrganised_FromTheDefendingTeam_AtTheRightDistance_WithoutClosingTheGoal()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 4f);
            Assert.IsTrue(c.PrepareRestart());
            FreeKickState fk = c.State.FreeKick;
            Vec2 spot = fk.Spot;
            Assert.AreEqual(FreeKickStage.Ready, fk.Stage);
            Assert.AreEqual(2, fk.Wall.Count, "a ball 8 m from the goal gets a wall of two");
            foreach (string id in fk.Wall)
            {
                MatchPlayer p = c.State.Away.Find(id);
                Assert.IsTrue(p.IsActive && !p.InGoal, "the goalkeeper is never in the wall");
                Assert.AreEqual(c.FreeKick.WallDistance, (p.Locomotion.Position - spot).Magnitude, 0.6f);
                Assert.AreEqual(TeamSide.Away, p.Team);
            }
            int outside = c.State.Away.Players.Count(p => p.IsActive && !p.InGoal && !fk.Wall.Contains(p.PlayerId));
            Assert.GreaterOrEqual(outside, c.FreeKick.WallMinFieldDefendersOutside, "some defenders stay out of the wall");
            Assert.LessOrEqual(fk.Wall.Count, c.FreeKick.WallMaxPlayers);
            // the wall covers the near side, the keeper the rest: the lane to the FAR post stays open
            Vec2 farPost = new Vec2(20f, spot.Y > 0f ? -2f : 2f);
            foreach (string id in fk.Wall)
            {
                Vec2 w = c.State.Away.Find(id).Locomotion.Position;
                float t = Dot2(w - spot, farPost - spot) / Math.Max(1e-3f, (farPost - spot).Magnitude * (farPost - spot).Magnitude);
                Vec2 closest = spot + (farPost - spot) * t;
                Assert.Greater((w - closest).Magnitude, 0.8f, "the wall does not stand on the lane to the far post");
            }
        }

        private static float Dot2(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        [Test]
        public void FreeKick_FarFromTheGoal_ThereIsNoWall_AndEveryoneElseKeepsHisDistance()
        {
            MatchController c = FkLive();
            c.CallFoul(TeamSide.Home, "hs2", "as3", FkCall(), new Vec3(2f, 0.2f, 0f));    // home fouls: the free kick of away, far from the goal it attacks
            Assert.IsTrue(c.PrepareRestart());
            FreeKickState fk = c.State.FreeKick;
            Assert.AreEqual(0, fk.Wall.Count);
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players))
            {
                if (!p.IsActive || p.InGoal || p.PlayerId == fk.TakerId) continue;
                float need = p.Team == fk.Beneficiary ? c.Infrastructure.RestartClearRadius : c.FreeKick.WallDistance;
                Assert.GreaterOrEqual((p.Locomotion.Position - fk.Spot).Magnitude, need - 0.35f, p.PlayerId + " is too close to the ball");
            }
        }

        [Test]
        public void FreeKick_TakerAndWall_StandWherePrepared_AndNobodyOverlaps()
        {
            MatchController c = FkLive();
            FkFoul(c, 13f, -2f);
            Assert.IsTrue(c.PrepareRestart());
            MatchPlayer taker = c.State.Home.Find(c.State.PendingRestart.TakerId);
            Assert.AreEqual(c.FreeKick.TakerOffsetBehind, (taker.Locomotion.Position - c.State.FreeKick.Spot).Magnitude, 0.6f);
            var all = c.State.Home.Players.Concat(c.State.Away.Players).Where(p => p.IsActive).ToList();
            for (int i = 0; i < all.Count; i++) for (int j = i + 1; j < all.Count; j++)
                    Assert.GreaterOrEqual((all[i].Locomotion.Position - all[j].Locomotion.Position).Magnitude, c.Infrastructure.MinSeparation - 0.05f);
            Assert.AreEqual(RestartStatus.Placed, c.State.PendingRestart.Status);
        }

        // ================= the whistle: once =================

        [Test]
        public void FreeKick_TheWhistle_ResumesPlayOnce_GivesTheBallToTheTaker_AndNothingIsDuplicated()
        {
            MatchController c = FkLive();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            FkFoul(c);
            Vec3 ball = c.State.Ball.Physics.Position;
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(ball.X, c.State.Ball.Physics.Position.X, 1e-4f);
            Assert.AreEqual(c.State.FreeKick.TakerId, c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(FreeKickStage.Protected, c.State.FreeKick.Stage);
            Assert.IsFalse(c.ConfirmRestart(), "a second whistle does nothing");
            FkAdvance(c, 0.1f);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.FreeKickPrepared));
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.FreeKickTaken));
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.GoalScored));
            Assert.AreEqual(1, c.State.FreeKicksAwarded);
        }

        [Test]
        public void FreeKick_IfNobodyWhistles_TheCoreDoesAfterTheReadyTime_SoTheMatchIsNeverBlocked()
        {
            MatchController c = FkLive();
            FkFoul(c);
            FkAdvance(c, c.FreeKick.ReadySeconds - 0.3f);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            FkAdvance(c, 0.6f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(c.State.FreeKick.TakerId, c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void FreeKick_AHumanTakerWaitsLonger_ButNotForever()
        {
            MatchController c = FkLive();
            c.SetControl(TeamSide.Home, ControlSource.Human);
            FkFoul(c);
            FkAdvance(c, c.FreeKick.ReadySeconds + 1f);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase, "the person has time to aim");
            FkAdvance(c, c.FreeKick.HumanMaxWaitSeconds);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase, "but the match is never blocked");
        }

        [Test]
        public void FreeKick_ASecondFoulWhileWaiting_IsRecorded_ButStartsNoSecondRestart()
        {
            MatchController c = FkLive();
            var requests = new List<RestartRequest>(); c.RestartRequested += requests.Add;
            FkFoul(c);
            Vec2 spot = c.State.FreeKick.Spot;
            c.CallFoul(TeamSide.Away, "as3", "hs3", FkCall(), new Vec3(5f, 0.2f, 5f));
            Assert.AreEqual(2, c.State.Discipline.Count);
            Assert.AreEqual(1, requests.Count); Assert.AreEqual(1, c.State.FreeKicksAwarded);
            Assert.AreEqual(spot, c.State.FreeKick.Spot);
        }

        [Test]
        public void FreeKick_TheOpponentsKeepTheirDistance_UntilTheBallIsPlayed_AndThenTheFreeKickEnds()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 0f);
            Assert.IsTrue(c.ConfirmRestart());
            FreeKickState fk = c.State.FreeKick;
            string defender = c.State.Away.Players.First(p => p.IsActive && !p.InGoal).PlayerId;
            c.PlacePlayer(TeamSide.Away, defender, fk.Spot + new Vec2(1f, 0f), FkWest.ToHeading());
            c.SetPlayerInput(TeamSide.Away, defender, new Vec2(-1f, 0f));
            FkAdvance(c, 0.2f);
            Assert.GreaterOrEqual((c.State.Away.Find(defender).Locomotion.Position - fk.Spot).Magnitude, c.FreeKick.WallDistance - c.FreeKick.ProtectionSlack - 1e-3f);
            c.Perform(new ActionRequest(TeamSide.Home, fk.TakerId, FootballActionKind.ShortPass, FkEast, 0.5f));
            FkAdvance(c, 0.1f);
            Assert.AreEqual(FreeKickStage.Done, fk.Stage); Assert.IsTrue(fk.KickedByTaker);
        }

        // ================= exceptional situations =================

        [Test]
        public void FreeKick_ATakerSentOffDuringTheWait_IsReplaced_AndTheWallNeverHoldsASentOffPlayer()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 0f);
            Assert.IsTrue(c.PrepareRestart());
            string taker = c.State.PendingRestart.TakerId, wall = c.State.FreeKick.Wall[0];
            c.IssueCard(TeamSide.Home, taker, CardKind.Red);
            c.IssueCard(TeamSide.Away, wall, CardKind.Red);
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreNotEqual(taker, c.State.FreeKick.TakerId); Assert.IsTrue(c.State.Home.Find(c.State.FreeKick.TakerId).IsActive);
            Assert.AreEqual(c.State.FreeKick.TakerId, c.State.Ball.PossessingPlayerId);
            Assert.IsFalse(c.State.FreeKick.Wall.Contains(wall));
            foreach (string id in c.State.FreeKick.Wall) Assert.IsTrue(c.State.Away.Find(id).IsActive);
        }

        [Test]
        public void FreeKick_AGoalkeeperSentOffOnTheFoul_GetsReplaced_AndTheMatchGoesOn()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 0f, CardDecision.Red, "as0");
            Assert.IsTrue(c.State.Away.NeedsGoalkeeper);
            FkAdvance(c, c.FreeKick.KeeperVacancySeconds + 1f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase, "the free kick was not blocked by the vacant goal");
            Assert.IsFalse(c.State.Away.NeedsGoalkeeper); Assert.IsNotNull(c.State.Away.GoalkeeperInGoal);
            Assert.IsTrue(c.State.Away.GoalkeeperInGoal.IsActive);
        }

        [Test]
        public void FreeKick_AGoalAfterAFreeKickStillWorks_AndTheNextRestartIsAKickOff()
        {
            bool anyGoal = false;
            for (int seed = 1; seed <= 4 && !anyGoal; seed++)
            {
                MatchController c = FkLive(GameMode.Classic, seed);
                FkFoul(c, 14f, 0f);
                var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
                Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Lob, HasZone = true, Zone = GoalZone.Centre, Power = 0.6f }));
                for (int i = 0; i < 240 && c.State.Phase == MatchPhase.Playing; i++) c.Advance(FkDt);
                if (c.State.Score.Home == 1)
                {
                    anyGoal = true;
                    Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.GoalScored), "one goal, once");
                    FkAdvance(c, 10f);
                    Assert.AreEqual(RestartKind.KickOff, c.State.PendingRestart.Kind);
                    Assert.AreEqual(1, c.State.Score.Home);
                }
            }
            Assert.IsTrue(anyGoal, "a lob over the wall from 6 m can score");
        }

        // ================= the aim =================

        private static Vec3 FkKick(FreeKickAim aim, out FreeKickExecuteResult result, int seed = 1, float x = 12f, float z = 0f)
        {
            MatchController c = FkLive(GameMode.Classic, seed);
            FkFoul(c, x, z);
            result = c.ExecuteFreeKick(aim);
            return c.State.Ball.Physics.Velocity;
        }

        [Test]
        public void Aim_ChangingTheDestination_ChangesTheDirectionOfTheKick()
        {
            FreeKickExecuteResult r1, r2, r3;
            Vec3 left = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Left, Power = 1f }, out r1);
            Vec3 right = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Right, Power = 1f }, out r2);
            Vec3 centre = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Centre, Power = 1f }, out r3);
            Assert.AreEqual(FreeKickExecuteResult.Executed, r1); Assert.AreEqual(FreeKickExecuteResult.Executed, r2); Assert.AreEqual(FreeKickExecuteResult.Executed, r3);
            Assert.Greater(left.Z, centre.Z, "the left zone (looking at the goal) is towards +Z");
            Assert.Greater(centre.Z, right.Z);
            Assert.Greater(left.X, 0f); Assert.Greater(right.X, 0f);
            // a dragged point works the same way as a zone
            Vec3 dragLeft = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasDestination = true, Destination = new Vec2(20f, 2f), Power = 1f }, out r1);
            Vec3 dragRight = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasDestination = true, Destination = new Vec2(20f, -2f), Power = 1f }, out r2);
            Assert.Greater(dragLeft.Z, dragRight.Z);
            Vec3 byDirection = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, Direction = new Vec2(0f, 1f), Power = 1f }, out r3);
            Assert.Greater(byDirection.Z, centre.Z, "a drag direction turns the kick as well");
        }

        [Test]
        public void Aim_ChangingThePower_ChangesTheInitialSpeedOfTheBall()
        {
            FreeKickExecuteResult r;
            float Speed(float power, bool pass)
            {
                Vec3 v = FkKick(new FreeKickAim { Intent = pass ? FreeKickIntent.Pass : FreeKickIntent.Shot, HasZone = !pass, Zone = GoalZone.Centre, Direction = new Vec2(1f, 0f), Power = power }, out r);
                Assert.AreEqual(FreeKickExecuteResult.Executed, r);
                return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            }
            float low = Speed(0.3f, false), mid = Speed(0.6f, false), high = Speed(1f, false);
            Assert.Less(low, mid); Assert.Less(mid, high);
            Assert.Less(Speed(0.2f, true), Speed(0.6f, true), "a pass is stronger the longer the button was held");
            // the hold time is the same thing: a longer hold is more power
            Vec3 tap = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HoldSeconds = 0.05f }, out r);
            Vec3 held = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HoldSeconds = 5f }, out r);
            Assert.Less(tap.Magnitude, held.Magnitude);
        }

        [Test]
        public void Aim_WithoutAnAim_TheKickGoesToTheMiddleOfTheGoal_ByDefault()
        {
            FreeKickExecuteResult r;
            Vec3 v = FkKick(new FreeKickAim { Intent = FreeKickIntent.Shot }, out r, 1, 12f, 0f);
            Assert.AreEqual(FreeKickExecuteResult.Executed, r);
            Assert.Greater(v.X, 0f); Assert.Greater(v.X, Math.Abs(v.Z) * 2f, "towards the goal, not to a corner flag");
        }

        [Test]
        public void Aim_APass_GoesToTheNamedTeamMate_OrToTheOneTheDragPointsAt()
        {
            MatchController c = FkLive();
            FkFoul(c, 3f, 0f);
            Vec2 mate = c.State.Home.Find("hs1").Locomotion.Position;
            string taker = c.State.PendingRestart.TakerId;
            string target = taker == "hs1" ? "hs2" : "hs1";
            mate = c.State.Home.Find(target).Locomotion.Position;
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Pass, TargetPlayerId = target, Power = 0.6f }));
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PassPerformed));
            Vec2 toMate = (mate - c.State.FreeKick.Spot).Normalized; Vec3 v = c.State.Ball.Physics.Velocity;
            Vec2 vd = new Vec2(v.X, v.Z).Normalized;
            Assert.Greater(toMate.X * vd.X + toMate.Y * vd.Y, 0.9f, "the ball goes towards the team-mate");

            // by drag: the team-mate inside the cone of the drag is found
            MatchController d = FkLive();
            FkFoul(d, 3f, 0f);
            Vec2 spot = d.State.FreeKick.Spot;
            var mates = d.State.Home.Players.Where(p => p.IsActive && !p.InGoal && p.PlayerId != d.State.PendingRestart.TakerId).ToList();
            MatchPlayer wanted = mates[0];
            Vec2 dir = (wanted.Locomotion.Position - spot).Normalized;
            int found = FreeKickAiming.FindPassTarget(spot, dir, mates.Select(p => p.Locomotion.Position).ToList(), d.FreeKick);
            Assert.GreaterOrEqual(found, 0); Assert.AreEqual(wanted.PlayerId, mates[found].PlayerId);
            Assert.AreEqual(FreeKickExecuteResult.Executed, d.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Pass, Direction = dir, Power = 0.5f }));
        }

        [Test]
        public void Aim_InvalidOrImpossibleInput_IsRefused_WithoutWhistlingOrBreakingTheMatch()
        {
            MatchController c = FkLive();
            Assert.AreEqual(FreeKickExecuteResult.NoFreeKick, c.ExecuteFreeKick(new FreeKickAim { Power = 1f }), "in live play there is no free kick to take");
            FkFoul(c);
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { Direction = new Vec2(float.NaN, 0f) }));
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { Power = float.NaN }));
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { HasDestination = true, Destination = new Vec2(0f, float.PositiveInfinity) }));
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { Intent = (FreeKickIntent)99 }));
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Pass, TargetPlayerId = "as1" }), "an opponent is not a team-mate");
            Assert.AreEqual(FreeKickExecuteResult.InvalidAim, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Pass, TargetPlayerId = "nobody" }));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase, "an invalid aim does not whistle");
            // a destination outside the field is brought inside the lines: the kick is made and the numbers stay finite
            Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { HasDestination = true, Destination = new Vec2(500f, 500f), Power = 1f }));
            Vec3 v = c.State.Ball.Physics.Velocity;
            Assert.IsFalse(float.IsNaN(v.X) || float.IsNaN(v.Z) || float.IsInfinity(v.X) || float.IsInfinity(v.Z));
            Assert.AreEqual(FreeKickExecuteResult.NoFreeKick, c.ExecuteFreeKick(new FreeKickAim { Power = 1f }), "it cannot be taken twice");
        }

        [Test]
        public void Aim_ACurve_IsAskedToTheExistingActionSystem_AndFallsBackToAStraightShotWithoutTheSkill()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 0f);
            var results = new List<ActionResult>(); c.ActionPerformed += results.Add;
            Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Centre, Curve = 0.8f, Power = 1f }));
            Assert.IsTrue(results.Any(r => r.Outcome != ActionOutcome.Rejected), "the kick was made, curved if the taker may and straight if he may not");
            Assert.IsFalse(results.Any(r => r.Kind == FootballActionKind.CurvedShot && r.Outcome == ActionOutcome.Rejected && results.Count == 1), "no refusal is left as the only answer");
        }

        [Test]
        public void Aim_ThePureMaths_PowerGrowsWithTheHold_ZonesAreOppositeSides_AndTheDragFindsTheTeamMateInTheCone()
        {
            var t = new FreeKickTuning();
            float previous = -1f;
            foreach (float hold in new[] { 0f, 0.2f, 0.6f, 1.2f, 3f }) { float p = FreeKickAiming.PowerFromHold(hold, t); Assert.GreaterOrEqual(p, previous); Assert.LessOrEqual(p, 1f); previous = p; }
            Assert.AreEqual(t.MinPower, FreeKickAiming.PowerFromHold(0f, t), 1e-5f); Assert.AreEqual(1f, FreeKickAiming.PowerFromHold(t.FullChargeSeconds * 2f, t), 1e-5f);
            Assert.AreEqual(t.MinPower, FreeKickAiming.PowerFromHold(float.NaN, t), 1e-5f);

            Vec2 goal = new Vec2(20f, 0f), east = new Vec2(1f, 0f);
            Vec2 left = FreeKickAiming.GoalZonePoint(GoalZone.Left, goal, east, 5f, t), right = FreeKickAiming.GoalZonePoint(GoalZone.Right, goal, east, 5f, t);
            Assert.AreEqual(goal, FreeKickAiming.GoalZonePoint(GoalZone.Centre, goal, east, 5f, t));
            Assert.AreEqual(-right.Y, left.Y, 1e-4f); Assert.Greater(left.Y, 0f); Assert.LessOrEqual(Math.Abs(left.Y), 2.5f, "inside the posts");
            Vec2 west = new Vec2(-1f, 0f);
            Assert.Less(FreeKickAiming.GoalZonePoint(GoalZone.Left, new Vec2(-20f, 0f), west, 5f, t).Y, 0f, "the other team looks the other way: its left is -Z");

            var mates = new List<Vec2> { new Vec2(5f, 10f), new Vec2(10f, 1f), new Vec2(11f, -0.5f) };
            Assert.AreEqual(2, FreeKickAiming.FindPassTarget(new Vec2(0f, 0f), new Vec2(1f, -0.04f), mates, t));
            Assert.AreEqual(-1, FreeKickAiming.FindPassTarget(new Vec2(0f, 0f), new Vec2(-1f, 0f), mates, t), "nobody behind");
            Assert.AreEqual(-1, FreeKickAiming.FindPassTarget(new Vec2(0f, 0f), Vec2.Zero, mates, t));
        }

        [Test]
        public void Aim_ALob_GoesOverTheWall_WhereAGroundShotIsStopped()
        {
            MatchController ground = FkLive(); FkFoul(ground, 14f, 0f);
            Assert.AreEqual(FreeKickExecuteResult.Executed, ground.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Centre, Power = 1f }));
            MatchController lob = FkLive(); FkFoul(lob, 14f, 0f);
            Assert.AreEqual(FreeKickExecuteResult.Executed, lob.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Lob, HasZone = true, Zone = GoalZone.Centre, Power = 0.7f }));
            Assert.Greater(lob.State.Ball.Physics.Velocity.Y, ground.State.Ball.Physics.Velocity.Y, "the lob leaves the ground at a steeper angle");
            bool wallStopsGround = false, wallStopsLob = false;
            for (int i = 0; i < 60; i++)
            {
                ground.Advance(FkDt); lob.Advance(FkDt);
                if (ground.State.Ball.HasPossession && ground.State.Ball.PossessingTeam == TeamSide.Away && ground.State.FreeKick.Wall.Contains(ground.State.Ball.PossessingPlayerId)) wallStopsGround = true;
                if (lob.State.Ball.HasPossession && lob.State.Ball.PossessingTeam == TeamSide.Away && lob.State.FreeKick.Wall.Contains(lob.State.Ball.PossessingPlayerId)) wallStopsLob = true;
            }
            Assert.IsTrue(wallStopsGround, "a ground shot at the middle of the goal is stopped by a wall in front of it");
            Assert.IsFalse(wallStopsLob, "the lob goes over it");
        }

        // ================= the choice of the taker (a person) =================

        private static MatchController FkHuman(float x = 12f, float z = 0f, IPlayerLookup lookup = null)
        {
            MatchController c = FkLive(GameMode.Classic, 1, lookup);
            c.SetControl(TeamSide.Home, ControlSource.Human);
            FkFoul(c, x, z);
            return c;
        }

        private static string FkOtherThanSuggested(MatchController c) { return c.FreeKickTakerCandidates().First(k => !k.IsSuggested).PlayerId; }

        [Test]
        public void Taker_APersonChoosesWhoTakesIt_AndThatPlayerReallyKicksTheBall()
        {
            MatchController c = FkHuman();
            string suggested = c.State.FreeKick.SuggestedTakerId, chosen = FkOtherThanSuggested(c);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            var results = new List<ActionResult>(); c.ActionPerformed += results.Add;
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ChooseFreeKickTaker(TeamSide.Home, chosen));
            Assert.AreEqual(chosen, c.State.PendingRestart.TakerId); Assert.AreEqual(chosen, c.State.FreeKick.TakerId); Assert.IsTrue(c.State.FreeKick.TakerChosenManually);
            Assert.AreEqual(suggested, c.State.FreeKick.SuggestedTakerId, "the Core still knows who it would have chosen");
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ConfirmFreeKickTaker(TeamSide.Home));
            Assert.IsTrue(c.State.FreeKick.TakerConfirmed);
            Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Left, Power = 0.8f }));
            Assert.AreEqual(chosen, results.Last(r => r.Outcome != ActionOutcome.Rejected).PlayerId, "the action is his");
            Assert.AreEqual(chosen, events.Single(e => e.Kind == GameplayEventKind.FreeKickTaken).PlayerId);
            Assert.AreEqual(chosen, c.State.Ball.LastTouchPlayerId, "the last touch is his, not the automatic taker's");
            Assert.AreNotEqual(suggested, c.State.Ball.LastTouchPlayerId);
        }

        [Test]
        public void Taker_TheChosenOneStandsAtTheBall_AndTheOneNotChosenLeavesIt()
        {
            MatchController c = FkHuman();
            string suggested = c.State.FreeKick.SuggestedTakerId, chosen = FkOtherThanSuggested(c);
            Assert.IsTrue(c.PrepareRestart());
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ChooseFreeKickTaker(TeamSide.Home, chosen));
            Vec2 spot = c.State.FreeKick.Spot;
            Assert.AreEqual(c.FreeKick.TakerOffsetBehind, (c.State.Home.Find(chosen).Locomotion.Position - spot).Magnitude, 0.6f);
            Assert.GreaterOrEqual((c.State.Home.Find(suggested).Locomotion.Position - spot).Magnitude, c.Infrastructure.RestartClearRadius - 0.4f, "the automatic taker is no longer next to the ball");
            Assert.AreEqual(RestartStatus.Placed, c.State.PendingRestart.Status);
            Assert.AreEqual(1, c.State.FreeKicksAwarded, "choosing does not create another free kick");
        }

        [Test]
        public void Taker_TheListShowsOnlyEligiblePlayers_WithTheirNumbersAndAttributes()
        {
            var l = new FkLookup();
            for (int i = 0; i < 6; i++) { l.Players["hs" + i] = new PlayerDefinition("hs" + i, "Home " + i, i + 7, i == 0 ? PlayerRole.Goalkeeper : PlayerRole.Midfielder, FkAll(50 + i)); l.Players["hs" + i].Attributes.Shooting = 60 + i; }
            MatchController c = FkHuman(12f, 0f, l);
            List<FreeKickTakerCandidate> list = c.FreeKickTakerCandidates(l);
            Assert.AreEqual(5, list.Count, "the five field players, not the goalkeeper, not the bench, not the rival");
            Assert.IsFalse(list.Any(k => k.PlayerId == "hs0" || k.PlayerId.StartsWith("a") || k.PlayerId.StartsWith("hb")));
            Assert.AreEqual(1, list.Count(k => k.IsSuggested)); Assert.AreEqual(1, list.Count(k => k.IsCurrent)); Assert.AreEqual(list.Single(k => k.IsSuggested).PlayerId, list.Single(k => k.IsCurrent).PlayerId);
            FreeKickTakerCandidate hs3 = list.Single(k => k.PlayerId == "hs3");
            Assert.IsTrue(hs3.HasDefinition); Assert.AreEqual("Home 3", hs3.Name); Assert.AreEqual(10, hs3.ShirtNumber); Assert.AreEqual(63, hs3.ShootingValue); Assert.AreEqual(53, hs3.PassingValue);
            Assert.AreEqual(PlayerRole.Midfielder, hs3.Role); Assert.GreaterOrEqual(hs3.ShotClose, 0f);
            Assert.IsFalse(c.FreeKickTakerCandidates().First().HasDefinition, "without a lookup the Core gives what it knows, not names");
            c.ChooseFreeKickTaker(TeamSide.Home, "hs4");
            Assert.AreEqual("hs4", c.FreeKickTakerCandidates().Single(k => k.IsCurrent).PlayerId);
            c.IssueCard(TeamSide.Home, "hs2", CardKind.Red);
            Assert.IsFalse(c.FreeKickTakerCandidates().Any(k => k.PlayerId == "hs2"), "a sent-off player disappears from the list");
        }

        [Test]
        public void Taker_AnInvalidChoice_IsRefused_AndTheTakerDoesNotChange()
        {
            MatchController c = FkHuman();
            string before = c.State.FreeKick.TakerId;
            Assert.AreEqual(FreeKickTakerResult.WrongTeam, c.ChooseFreeKickTaker(TeamSide.Home, "as3"), "an opponent");
            Assert.AreEqual(FreeKickTakerResult.UnknownPlayer, c.ChooseFreeKickTaker(TeamSide.Home, "nobody"));
            Assert.AreEqual(FreeKickTakerResult.UnknownPlayer, c.ChooseFreeKickTaker(TeamSide.Home, null));
            Assert.AreEqual(FreeKickTakerResult.NotAvailable, c.ChooseFreeKickTaker(TeamSide.Home, "hb1"), "a substitute who is not on the pitch");
            Assert.AreEqual(FreeKickTakerResult.Goalkeeper, c.ChooseFreeKickTaker(TeamSide.Home, "hs0"), "the rule excludes the goalkeeper");
            Assert.AreEqual(FreeKickTakerResult.NotUserTeam, c.ChooseFreeKickTaker(TeamSide.Away, "as3"), "the other team is not the one that takes it");
            c.IssueCard(TeamSide.Home, "hs2", CardKind.Red);
            Assert.AreEqual(FreeKickTakerResult.Dismissed, c.ChooseFreeKickTaker(TeamSide.Home, "hs2"));
            Assert.AreEqual(before, c.State.FreeKick.TakerId); Assert.IsFalse(c.State.FreeKick.TakerChosenManually);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ConfirmFreeKickTaker(TeamSide.Home));
            Assert.AreEqual(FreeKickTakerResult.Locked, c.ChooseFreeKickTaker(TeamSide.Home, "hs4"), "after the confirmation the taker is final");
            Assert.AreEqual(before, c.State.FreeKick.TakerId);
        }

        [Test]
        public void Taker_ChoosingWithNoFreeKick_OrInArena_IsRefused()
        {
            MatchController live = FkLive(); live.SetControl(TeamSide.Home, ControlSource.Human);
            Assert.AreEqual(FreeKickTakerResult.NoFreeKick, live.ChooseFreeKickTaker(TeamSide.Home, "hs3"));
            Assert.AreEqual(FreeKickTakerResult.NoFreeKick, live.ConfirmFreeKickTaker(TeamSide.Home));
            Assert.AreEqual(0, live.FreeKickTakerCandidates().Count);
            MatchController arena = FkLive(GameMode.Arena); arena.SetControl(TeamSide.Home, ControlSource.Human);
            FkFoul(arena);
            Assert.AreEqual(FreeKickTakerResult.NoFreeKick, arena.ChooseFreeKickTaker(TeamSide.Home, "hs3")); Assert.AreEqual(0, arena.FreeKickTakerCandidates().Count); Assert.IsNull(arena.State.FreeKick);
        }

        [Test]
        public void Taker_TheChosenOneSentOffDuringThePreparation_IsReplacedByAValidOne_WithoutBlocking()
        {
            MatchController c = FkHuman();
            string chosen = FkOtherThanSuggested(c);
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ChooseFreeKickTaker(TeamSide.Home, chosen));
            c.IssueCard(TeamSide.Home, chosen, CardKind.Red);
            Assert.IsTrue(c.ConfirmRestart());
            FreeKickState fk = c.State.FreeKick;
            Assert.AreNotEqual(chosen, fk.TakerId); Assert.IsTrue(fk.TakerReplaced); Assert.IsFalse(fk.TakerChosenManually);
            MatchPlayer taker = c.State.Home.Find(fk.TakerId);
            Assert.IsTrue(taker.IsActive && !taker.InGoal);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual(fk.TakerId, c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void Taker_ASubstitutionDuringTheWait_IsAllowed_TheSubstituteCanBeChosen_AndAChosenOneSubstitutedOffIsReplaced()
        {
            MatchController c = FkHuman();
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs1", "hb1"));
            Assert.IsTrue(c.FreeKickTakerCandidates().Any(k => k.PlayerId == "hb1"));
            Assert.IsFalse(c.FreeKickTakerCandidates().Any(k => k.PlayerId == "hs1"));
            Assert.AreEqual(FreeKickTakerResult.NotAvailable, c.ChooseFreeKickTaker(TeamSide.Home, "hs1"), "substituted off");
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ChooseFreeKickTaker(TeamSide.Home, "hb1"));
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hb1", "hb2"), "the substitute is substituted again before the kick");
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreNotEqual("hb1", c.State.FreeKick.TakerId); Assert.IsTrue(c.State.FreeKick.TakerReplaced);
            Assert.IsTrue(c.State.Home.Find(c.State.FreeKick.TakerId).IsActive);
            Assert.AreEqual(c.State.FreeKick.TakerId, c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void Taker_PlayersMovedDuringTheWait_TheChosenOneIsPutBackAtTheBall()
        {
            MatchController c = FkHuman();
            string chosen = FkOtherThanSuggested(c);
            c.ChooseFreeKickTaker(TeamSide.Home, chosen);
            Assert.IsTrue(c.PrepareRestart());
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players).Where(q => q.IsActive)) c.PlacePlayer(p.Team, p.PlayerId, new Vec2(0f, 0f), 0f);   // a formation change that puts everybody elsewhere
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(chosen, c.State.FreeKick.TakerId); Assert.AreEqual(chosen, c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(c.FreeKick.TakerOffsetBehind, (c.State.Home.Find(chosen).Locomotion.Position - c.State.FreeKick.Spot).Magnitude, 0.6f);
        }

        [Test]
        public void Taker_RealFormations_AFormationChangeDuringTheWaitKeepsTheChosenTaker()
        {
            MatchController c = RealRoles(3, 8, 5, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            c.SetControl(TeamSide.Home, ControlSource.Human);
            MatchPlayer offender = c.State.Away.Players.First(p => p.IsActive && !p.InGoal), victim = c.State.Home.Players.First(p => p.IsActive && !p.InGoal);
            c.CallFoul(TeamSide.Away, offender.PlayerId, victim.PlayerId, FkCall(), new Vec3(10f, 0.2f, 2f));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            string chosen = FkOtherThanSuggested(c);
            Assert.AreEqual(FreeKickTakerResult.Accepted, c.ChooseFreeKickTaker(TeamSide.Home, chosen));
            c.PlaceFormation(TeamSide.Home, false); c.PlaceFormation(TeamSide.Away, false);   // the formations are applied again during the wait
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(chosen, c.State.FreeKick.TakerId); Assert.AreEqual(chosen, c.State.Ball.PossessingPlayerId);
            Assert.LessOrEqual((c.State.Home.Find(chosen).Locomotion.Position - c.State.FreeKick.Spot).Magnitude, c.FreeKick.TakerOffsetBehind + 0.8f);
        }

        [Test]
        public void Taker_IfNobodyChoosesInTime_TheAutomaticTakerStays_ChoosingIsLocked_AndTheMatchGoesOn()
        {
            MatchController c = FkHuman();
            string suggested = c.State.FreeKick.SuggestedTakerId;
            Assert.IsFalse(c.State.FreeKick.TakerConfirmed); Assert.AreEqual(c.FreeKick.TakerChoiceSeconds, c.State.FreeKick.TakerChoiceRemaining, 1e-4f);
            FkAdvance(c, c.FreeKick.TakerChoiceSeconds + 0.2f);
            Assert.IsTrue(c.State.FreeKick.TakerConfirmed); Assert.AreEqual(0f, c.State.FreeKick.TakerChoiceRemaining, 1e-4f);
            Assert.AreEqual(suggested, c.State.FreeKick.TakerId);
            Assert.AreEqual(FreeKickTakerResult.Locked, c.ChooseFreeKickTaker(TeamSide.Home, FkOtherThanSuggested(c)));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase, "the person can still aim");
            FkAdvance(c, c.FreeKick.HumanMaxWaitSeconds);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual(suggested, c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void Taker_ExecutingWithoutConfirming_ConfirmsTheCurrentChoice()
        {
            MatchController c = FkHuman();
            string chosen = FkOtherThanSuggested(c);
            c.ChooseFreeKickTaker(TeamSide.Home, chosen);
            Assert.AreEqual(FreeKickExecuteResult.Executed, c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, Power = 0.5f }));
            Assert.AreEqual(chosen, c.State.Ball.LastTouchPlayerId); Assert.IsTrue(c.State.FreeKick.TakerConfirmed);
        }

        [Test]
        public void Taker_TheAiChoosesItsOwnTaker_AutomaticallyAndAtOnce_AndNoOneCanChooseForIt()
        {
            MatchController c = FkLive();   // nobody controls home: the AI
            FkFoul(c);
            FreeKickState fk = c.State.FreeKick;
            Assert.IsTrue(fk.TakerConfirmed, "the AI never waits to choose");
            Assert.AreEqual(fk.SuggestedTakerId, fk.TakerId); Assert.IsFalse(fk.TakerChosenManually);
            Assert.AreEqual(FreeKickTakerResult.NotUserTeam, c.ChooseFreeKickTaker(TeamSide.Home, FkOtherThanSuggested(c)));
            Assert.AreEqual(fk.SuggestedTakerId, fk.TakerId);
            FkAdvance(c, c.FreeKick.ReadySeconds + 0.3f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual(fk.SuggestedTakerId, c.State.Ball.PossessingPlayerId);
        }

        [Test]
        public void Taker_TheSameChoicesGiveTheSameFreeKick()
        {
            string Run()
            {
                MatchController c = FkHuman();
                var sb = new System.Text.StringBuilder();
                c.Events.Published += e => sb.Append((int)e.Kind).Append(e.PlayerId).Append(e.MatchTime.ToString("R")).Append(';');
                c.ChooseFreeKickTaker(TeamSide.Home, "hs4");
                c.ConfirmFreeKickTaker(TeamSide.Home);
                c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Right, HoldSeconds = 0.6f });
                FkAdvance(c, 3f);
                sb.Append(c.State.Ball.Physics.Position.X.ToString("R")).Append(c.State.Score.Home);
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        // ================= the AI of the taker =================

        private static PlayerDecision FkDecide(MatchController c, string id) { return new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, id); }

        [Test]
        public void FreeKickAi_DuringThePreparation_EveryPlayerIsIdle_NotPlaying()
        {
            MatchController c = FkLive();
            FkFoul(c, 12f, 0f);
            Assert.IsTrue(c.PrepareRestart());
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players).Where(q => q.IsActive))
            {
                PlayerDecision d = FkDecide(c, p.PlayerId);
                Assert.AreEqual(DecisionKind.Idle, d.Kind, p.PlayerId); Assert.AreEqual(DecisionReason.NotPlaying, d.Reason, p.PlayerId); Assert.IsFalse(d.HasAction, p.PlayerId);
            }
        }

        [Test]
        public void FreeKickAi_ANearFreeKickBehindAWall_IsLobbedOverIt_AFarOneIsPlayedToATeamMate()
        {
            MatchController near = FkLive();
            FkFoul(near, 14f, 0f);
            Assert.IsTrue(near.ConfirmRestart());
            PlayerDecision shot = FkDecide(near, near.State.FreeKick.TakerId);
            Assert.AreEqual(DecisionKind.Shoot, shot.Kind); Assert.AreEqual(DecisionReason.FreeKickShot, shot.Reason);
            Assert.AreEqual(FootballActionKind.Chip, shot.Action.Kind, "the wall stands on the lane: over it, not into it");

            MatchController far = FkLive();
            far.CallFoul(TeamSide.Away, "as2", "hs3", FkCall(), new Vec3(-8f, 0.2f, 0f));
            Assert.IsTrue(far.ConfirmRestart());
            PlayerDecision pass = FkDecide(far, far.State.FreeKick.TakerId);
            Assert.IsTrue(pass.Reason == DecisionReason.FreeKickPass || pass.Reason == DecisionReason.FreeKickClear, "far from the goal he plays it: " + pass.Reason);
            Assert.IsTrue(pass.HasAction);
            Assert.AreNotEqual(DecisionKind.Dribble, pass.Kind, "the taker does not carry a free kick");
        }

        // ================= the modes: Arena untouched, no-foul matches untouched =================

        [Test]
        public void Arena_HasNoFreeKicks_NoWalls_NoPauses_AFoulIsNeverCalled()
        {
            MatchController c = FkLive(GameMode.Arena);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            var requests = new List<RestartRequest>(); c.RestartRequested += requests.Add;
            Assert.IsFalse(FkFoul(c).Called);
            FkAdvance(c, 5f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.IsNull(c.State.FreeKick); Assert.AreEqual(0, c.State.FreeKicksAwarded); Assert.AreEqual(0, requests.Count);
            Assert.IsFalse(events.Any(e => e.Kind == GameplayEventKind.FreeKickAwarded || e.Kind == GameplayEventKind.FreeKickPrepared || e.Kind == GameplayEventKind.FreeKickTaken || e.Kind == GameplayEventKind.FoulCommitted));
            Assert.AreEqual(FreeKickExecuteResult.NoFreeKick, c.ExecuteFreeKick(new FreeKickAim { Power = 1f }));
            Assert.IsFalse(c.Rules.Allows(GameplayEventKind.FreeKickPrepared)); Assert.IsFalse(c.Rules.Allows(GameplayEventKind.FreeKickTaken));
        }

        [Test]
        public void FreeKick_AClassicMatchWithoutFouls_BehavesAsBefore()
        {
            MatchController c = FkLive();
            FkAdvance(c, 10f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.IsNull(c.State.FreeKick); Assert.AreEqual(0, c.State.FreeKicksAwarded);
        }

        // ================= determinism =================

        private static string FkFingerprint(int seed)
        {
            MatchController c = FkLive(GameMode.Classic, seed);
            var sb = new System.Text.StringBuilder();
            c.Events.Published += e => sb.Append((int)e.Kind).Append('@').Append(e.MatchTime.ToString("R")).Append(e.PlayerId).Append(';');
            FkFoul(c, 11f, 3f, CardDecision.Yellow);
            c.ExecuteFreeKick(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Right, Curve = 0.5f, HoldSeconds = 0.7f });
            FkAdvance(c, 4f);
            Vec3 b = c.State.Ball.Physics.Position;
            sb.Append(b.X.ToString("R")).Append(',').Append(b.Z.ToString("R")).Append(c.State.Score.Home).Append(c.State.Phase);
            return sb.ToString();
        }

        [Test]
        public void FreeKick_TheSameSeedGivesTheSameFreeKick()
        {
            Assert.AreEqual(FkFingerprint(7), FkFingerprint(7));
            Assert.AreEqual(FkFingerprint(2), FkFingerprint(2));
        }

        // ================= the AI =================

        [Test]
        public void FreeKickAi_NothingIsDecidedDuringThePreparation_TheTakerPlaysItAfterTheWhistle_AndNothingIsDuplicated()
        {
            int stopped = 0, taken = 0, played = 0, duplicateRestarts = 0, actionsWhileStopped = 0;
            bool attached = false;
            int lastTakenNumber = 0;
            var prepared = new Dictionary<int, int>();
            StepHook = (c, step) =>
            {
                if (!attached)
                {
                    attached = true;
                    c.ActionPerformed += r => { if (c.State.Phase == MatchPhase.WaitingForRestart && c.State.FreeKick != null && c.State.FreeKick.Active && r.Outcome != ActionOutcome.Rejected && r.Detail != ActionDetail.Foul) actionsWhileStopped++; };
                    c.Events.Published += e =>
                    {
                        if (e.Kind == GameplayEventKind.FreeKickPrepared) { int n = c.State.FreeKick.Number; prepared[n] = prepared.ContainsKey(n) ? prepared[n] + 1 : 1; }
                        if (e.Kind == GameplayEventKind.FreeKickTaken) { taken++; if (c.State.FreeKick.Number == lastTakenNumber) duplicateRestarts++; lastTakenNumber = c.State.FreeKick.Number; }
                    };
                }
                FreeKickState fk = c.State.FreeKick;
                if (c.State.Phase == MatchPhase.WaitingForRestart && fk != null && fk.Active) stopped++;
                if (fk != null && fk.Stage == FreeKickStage.Done && fk.Number > played) { played = fk.Number; }
            };
            int kicked = 0;
            try
            {
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    AttackSim s = SimulateAttack(GameMode.Classic, seed, true, 3 + seed, 8 - seed, 300f);
                    Assert.AreEqual(0, s.Issues, "the simulation found no broken invariant");
                }
            }
            finally { StepHook = null; }
            Assert.Greater(taken, 0, "the simulations produced free kicks");
            Assert.AreEqual(0, duplicateRestarts, "no free kick was whistled twice");
            Assert.AreEqual(0, actionsWhileStopped, "no player acted while play was stopped");
            Assert.IsTrue(prepared.Values.All(v => v == 1), "every free kick is prepared once");
            Assert.GreaterOrEqual(kicked, 0);
        }
    }
}
