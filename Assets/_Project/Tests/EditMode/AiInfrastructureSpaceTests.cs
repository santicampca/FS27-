using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 7: geometry and observation, pitch bounds, player separation.</summary>
    public partial class AiInfrastructureTests
    {
        // ================= distances and relations =================

        [Test]
        public void Distances_AngleAndSegmentAreCentralisedAndRight()
        {
            Assert.AreEqual(5f, WorldGeometry.Distance(new Vec2(0f, 0f), new Vec2(3f, 4f)), 0.0001f);
            Assert.AreEqual(0f, WorldGeometry.RelativeAngle(East, Vec2.Zero, new Vec2(5f, 0f)), 0.001f);
            Assert.AreEqual(90f, WorldGeometry.RelativeAngle(North, Vec2.Zero, new Vec2(5f, 0f)), 0.001f, "to the right of a player who faces +Y is +X");
            Assert.AreEqual(-90f, WorldGeometry.RelativeAngle(North, Vec2.Zero, new Vec2(-5f, 0f)), 0.001f);
            Assert.AreEqual(180f, WorldGeometry.AngleBetween(East, West), 0.001f); Assert.AreEqual(90f, WorldGeometry.AngleBetween(East, North), 0.001f);
            Assert.AreEqual(2f, WorldGeometry.DistanceToSegment(new Vec2(5f, 2f), new Vec2(0f, 0f), new Vec2(10f, 0f)), 0.0001f);
            Assert.AreEqual(5f, WorldGeometry.DistanceToSegment(new Vec2(-3f, 4f), new Vec2(0f, 0f), new Vec2(10f, 0f)), 0.0001f, "beyond the end of the segment: the distance to the end point");
            Assert.AreEqual(3f, WorldGeometry.DistanceToSegment(new Vec2(0f, 3f), new Vec2(0f, 0f), Vec2.Zero), 0.0001f, "a degenerate segment is a point");
        }

        [Test]
        public void GoalsAndDistancesToGoal_FollowTheSideSwitch()
        {
            MatchController c = Live();
            WorldView v = WorldState.Capture(c).View;
            Assert.AreEqual(new Vec2(20f, 0f), WorldGeometry.AttackGoal(v.Match, v.Field, TeamSide.Home)); Assert.AreEqual(new Vec2(-20f, 0f), WorldGeometry.AttackGoal(v.Match, v.Field, TeamSide.Away));
            Assert.AreEqual(new Vec2(-20f, 0f), WorldGeometry.OwnGoal(v.Match, v.Field, TeamSide.Home));
            WorldPlayer p = v[v.IndexOf("hs2")];
            Assert.AreEqual(WorldGeometry.Distance(p.Position, new Vec2(20f, 0f)), WorldGeometry.DistanceToAttackGoal(v, p), 0.0001f);
            Assert.AreEqual(WorldGeometry.Distance(p.Position, new Vec2(-20f, 0f)), WorldGeometry.DistanceToOwnGoal(v, p), 0.0001f);
            c.State.HomeAttacksPositiveX = false;
            WorldView flipped = WorldState.Capture(c).View;
            Assert.AreEqual(new Vec2(-20f, 0f), WorldGeometry.AttackGoal(flipped.Match, flipped.Field, TeamSide.Home));
        }

        [Test]
        public void NearestOpponentAndTeammate_OnlyCountPlayersOnThePitch_TiesGoToTheEarlierInTheSquad()
        {
            MatchController c = Live();
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East); Put(c, TeamSide.Home, "hs3", 4f, 0f, East); Put(c, TeamSide.Home, "hs4", -4f, 0f, East);
            Put(c, TeamSide.Away, "as2", 0f, 3f, West); Put(c, TeamSide.Away, "as3", 0f, -3f, West);
            WorldView v = WorldState.Capture(c).View;
            int me = v.IndexOf("hs2");
            int op = WorldGeometry.NearestOpponent(v, me, out float od);
            Assert.AreEqual("as2", v[op].PlayerId, "equal distance: the earlier in the squad"); Assert.AreEqual(3f, od, 0.0001f);
            int mate = WorldGeometry.NearestTeammate(v, me, out float md);
            Assert.AreEqual("hs3", v[mate].PlayerId); Assert.AreEqual(4f, md, 0.0001f);
            c.IssueCard(TeamSide.Away, "as2", CardKind.Red); c.IssueCard(TeamSide.Home, "hs3", CardKind.Red);
            v = WorldState.Capture(c).View;
            Assert.AreEqual("as3", v[WorldGeometry.NearestOpponent(v, v.IndexOf("hs2"), out _)].PlayerId, "a dismissed player is not an opponent any more");
            Assert.AreEqual("hs4", v[WorldGeometry.NearestTeammate(v, v.IndexOf("hs2"), out _)].PlayerId);
            Assert.AreEqual(-1, WorldGeometry.NearestOpponent(v, 99, out float inf)); Assert.IsTrue(float.IsPositiveInfinity(inf));
        }

        [Test]
        public void ThePassLane_IsGeometry_ABlockedLaneIsNotAvailable_AFarTeammateIsNot()
        {
            MatchController c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East); Put(c, TeamSide.Home, "hs2", 8f, 0f, East); Put(c, TeamSide.Home, "hs3", 0f, 9f, East); Put(c, TeamSide.Home, "hs4", -30f, 10f, East); Put(c, TeamSide.Home, "hs5", -30f, -10f, East);
            Put(c, TeamSide.Away, "as2", 4f, 0.3f, West); // on the line to hs2
            WorldView v = WorldState.Capture(c).View;
            Assert.IsFalse(WorldGeometry.IsLaneClear(v, TeamSide.Home, new Vec2(0f, 0f), new Vec2(8f, 0f), v.Tuning.PassLaneClearance));
            Assert.IsTrue(WorldGeometry.IsLaneClear(v, TeamSide.Home, new Vec2(0f, 0f), new Vec2(0f, 9f), v.Tuning.PassLaneClearance));
            int open = WorldGeometry.NearestAvailableTeammate(v, v.IndexOf("hs1"), out float d);
            Assert.AreEqual("hs3", v[open].PlayerId, "hs2 is nearer but blocked"); Assert.AreEqual(9f, d, 0.0001f);
            Assert.AreEqual(1, WorldGeometry.OpponentsWithin(v, TeamSide.Home, new Vec2(4f, 0f), 1f));
            int far = WorldGeometry.NearestAvailableTeammate(v, v.IndexOf("hs5"), out float farDistance);
            Assert.AreEqual("hs0", v[far].PlayerId, "the keeper is the nearest team-mate with a clear lane: a back pass is a pass"); Assert.AreEqual(11f, farDistance, 0.0001f);
            c.Infrastructure.MaxPassDistance = 5f;
            Assert.AreEqual(-1, WorldGeometry.NearestAvailableTeammate(WorldState.Capture(c).View, v.IndexOf("hs5"), out float none), "nobody within the longest pass"); Assert.IsTrue(float.IsPositiveInfinity(none));
        }

        // ================= observation =================

        [Test]
        public void PlayerObservation_ExposesWhatOnePlayerSees_Nearest_First()
        {
            MatchController c = Live();
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East); Put(c, TeamSide.Home, "hs2", 2f, 0f, East); Put(c, TeamSide.Home, "hs3", -6f, 0f, East); Put(c, TeamSide.Home, "hs4", 10f, 0f, East); Put(c, TeamSide.Home, "hs5", 0f, 7f, East);
            Put(c, TeamSide.Away, "as1", 3f, 1f, West); Put(c, TeamSide.Away, "as2", 9f, 0f, West);
            Carrier(c, TeamSide.Away, "as3", 5f, 5f, West);
            var o = new PlayerObservation();
            WorldView v = WorldState.Capture(c).View;
            Assert.IsTrue(v.Observe("hs1", o)); Assert.IsTrue(o.Valid); Assert.AreEqual("hs1", o.Self.PlayerId); Assert.AreEqual(Vec2.Zero, o.Self.Position);
            Assert.IsTrue(o.OpponentHasBall); Assert.IsFalse(o.TeamHasBall); Assert.IsFalse(o.HasBall); Assert.IsFalse(o.BallFree); Assert.AreEqual("as3", o.PossessorId); Assert.IsTrue(o.BallLive);
            Assert.AreEqual(WorldGeometry.Distance(Vec2.Zero, new Vec2(c.State.Ball.Physics.Position.X, c.State.Ball.Physics.Position.Z)), o.BallDistance, 0.0001f);
            Assert.AreEqual(5, o.TeammateCount, "the keeper and four other field players");
            float last = -1f;
            for (int i = 0; i < o.TeammateCount; i++) { Assert.GreaterOrEqual(o.TeammateDistance(i), last); last = o.TeammateDistance(i); }
            Assert.AreEqual("hs2", v[o.TeammateIndex(0)].PlayerId); Assert.AreEqual(2f, o.TeammateDistance(0), 0.0001f);
            Assert.AreEqual("as1", v[o.OpponentIndex(0)].PlayerId); Assert.AreEqual(o.NearestOpponentDistance, o.OpponentDistance(0), 0.0001f);
            Assert.AreEqual(PitchZone.Midfield, o.Zone); Assert.IsTrue(o.CanAct); Assert.AreEqual(MatchPhase.Playing, o.Phase);
            Assert.AreEqual(20f, o.DistanceToAttackGoal, 0.0001f); Assert.AreEqual(20f, o.DistanceToOwnGoal, 0.0001f);
        }

        [Test]
        public void PlayerObservation_TheBallBearingFollowsTheWayThePlayerFaces_AndFreeSpaceIsCounted()
        {
            MatchController c = Live();
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East); PutBall(c, 0f, 5f);
            Put(c, TeamSide.Away, "as2", 1f, 1f, West); Put(c, TeamSide.Away, "as3", -1f, 1f, West); Put(c, TeamSide.Away, "as4", 10f, 10f, West);
            var o = new PlayerObservation();
            WorldState.Capture(c).View.Observe("hs2", o);
            Assert.AreEqual(-90f, o.BallBearing, 0.01f, "the ball is on his left (facing +X, the ball at +Y)"); Assert.IsTrue(o.BallFree); Assert.AreEqual(5f, o.BallDistance, 0.0001f);
            Assert.AreEqual(2, o.OpponentsNearby); Assert.IsFalse(o.IsFree); Assert.AreEqual(Math.Sqrt(2), o.NearestOpponentDistance, 0.0001);
            Put(c, TeamSide.Away, "as2", 15f, 15f, West); Put(c, TeamSide.Away, "as3", -15f, -15f, West);
            WorldState.Capture(c).View.Observe("hs2", o);
            Assert.IsTrue(o.IsFree); Assert.AreEqual(0, o.OpponentsNearby);
        }

        [Test]
        public void PlayerObservation_IgnoresPlayersOffThePitch_AndFlagsTheControlledOne_AndFailsForAnUnknownPlayer()
        {
            MatchController c = Live();
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SelectPlayer(TeamSide.Home, "hs2");
            c.IssueCard(TeamSide.Away, "as1", CardKind.Red);
            var o = new PlayerObservation();
            WorldView v = WorldState.Capture(c).View;
            Assert.IsTrue(v.Observe("hs2", o)); Assert.IsTrue(o.IsControlled); Assert.AreEqual(5, o.OpponentCount, "six opponents minus the dismissed one"); Assert.AreEqual(5, o.TeammateCount, "his five team-mates");
            Assert.IsFalse(v.Observe("nobody", o)); Assert.IsFalse(o.Valid); Assert.IsFalse(v.Observe(-1, o)); Assert.IsFalse(v.Observe(99, o));
            Assert.IsTrue(v.Observe("hb1", o), "a bench player can be observed (he is simply not on the pitch)"); Assert.IsFalse(o.CanAct);
        }

        [Test]
        public void PlayerObservation_IsReusable_AndKeepsAtMostSixNeighbours()
        {
            MatchController c = Live();
            var o = new PlayerObservation();
            for (int round = 0; round < 3; round++)
            {
                Put(c, TeamSide.Home, "hs2", round, 0f, East);
                WorldState.Capture(c).View.Observe("hs2", o);
                Assert.AreEqual(5, o.TeammateCount); Assert.AreEqual(6, o.OpponentCount); Assert.LessOrEqual(o.TeammateCount, PlayerObservation.MaxNeighbours);
            }
        }

        // ================= pitch bounds =================

        [Test]
        public void PitchBounds_KnowTheGeometry_ForBothModes()
        {
            var f = new FieldDimensions();
            var classic = new PitchBounds(f, false); var arena = new PitchBounds(f, true);
            Assert.AreEqual(BallLimit.LeavesPlay, classic.BallLimit); Assert.AreEqual(BallLimit.Walls, arena.BallLimit);
            Assert.AreEqual(20f, classic.HalfLength); Assert.AreEqual(12.5f, classic.HalfWidth);
            Assert.IsTrue(classic.Contains(new Vec2(20f, 12.5f))); Assert.IsFalse(classic.Contains(new Vec2(20.1f, 0f))); Assert.IsFalse(classic.Contains(new Vec2(0f, -12.6f)));
            Assert.AreEqual(new Vec2(21f, 13.5f), classic.ClampPlayer(new Vec2(50f, 50f))); Assert.AreEqual(new Vec2(-21f, -13.5f), classic.ClampPlayer(new Vec2(-50f, -50f))); Assert.AreEqual(new Vec2(3f, 4f), classic.ClampPlayer(new Vec2(3f, 4f)));
        }

        [Test]
        public void TheGoalZone_IsInFrontOfTheGoalALineDefends_AndFollowsTheSideSwitch()
        {
            var b = new PitchBounds(new FieldDimensions(), false);
            Assert.AreEqual(-1, PitchBounds.OwnGoalSign(true, TeamSide.Home)); Assert.AreEqual(1, PitchBounds.OwnGoalSign(true, TeamSide.Away)); Assert.AreEqual(1, PitchBounds.OwnGoalSign(false, TeamSide.Home));
            Assert.IsTrue(b.IsInGoalZone(new Vec2(-18f, 1f), true, TeamSide.Home)); Assert.IsFalse(b.IsInGoalZone(new Vec2(-10f, 0f), true, TeamSide.Home)); Assert.IsFalse(b.IsInGoalZone(new Vec2(-18f, 8f), true, TeamSide.Home));
            Assert.IsTrue(b.IsInGoalZone(new Vec2(18f, 0f), true, TeamSide.Away)); Assert.IsFalse(b.IsInGoalZone(new Vec2(18f, 0f), false, TeamSide.Away), "after the side switch the away goal is at -X");
            Vec2 clamped = b.ClampToGoalZone(new Vec2(-5f, 9f), true, TeamSide.Home);
            Assert.IsTrue(b.IsInGoalZone(clamped, true, TeamSide.Home)); Assert.AreEqual(new Vec2(-16f, 5f), clamped);
            Assert.AreEqual(new Vec2(-20f, 0f), b.OwnGoalCentre(true, TeamSide.Home)); Assert.AreEqual(new Vec2(20f, 0f), b.OpponentGoalCentre(true, TeamSide.Home));
            Assert.IsTrue(PitchBounds.InOwnHalf(new Vec2(-1f, 0f), true, TeamSide.Home)); Assert.IsFalse(PitchBounds.InOwnHalf(new Vec2(1f, 0f), true, TeamSide.Home));
        }

        [Test]
        public void TheEngineExposesThePitchOfItsMode_Arena_RebouncesOffTheWalls_Classic_LeavesPlay()
        {
            MatchController arena = Live(MatchSettings.CreateArena()), classic = Live(MatchSettings.CreateClassic());
            Assert.AreEqual(BallLimit.Walls, arena.Pitch.BallLimit); Assert.AreEqual(BallLimit.LeavesPlay, classic.Pitch.BallLimit);
            PutBall(arena, 18f, 11f, 12f, 12f); PutBall(classic, 18f, 11f, 12f, 12f);
            Step(arena, 120); Step(classic, 120);
            Assert.IsTrue(arena.Events.Has(GameplayEventKind.BallBouncedOffWall), "Arena: the ball rebounded off a wall"); Assert.AreEqual(MatchPhase.Playing, arena.State.Phase); Assert.IsTrue(arena.Pitch.Contains(new Vec2(arena.State.Ball.Physics.Position.X, arena.State.Ball.Physics.Position.Z)));
            Assert.IsTrue(classic.State.Phase == MatchPhase.WaitingForRestart || classic.State.Phase == MatchPhase.GoalCelebration, "Classic: the ball left play and a restart (or a goal) followed, phase " + classic.State.Phase);
        }

        [Test]
        public void PlayersAreKeptInsidePitchPlusMargin_OnceTheSpaceRulesAreOn()
        {
            MatchController c = Live();
            c.Infrastructure.EnforcePlayerSpace = true;
            Put(c, TeamSide.Home, "hs2", 19f, 0f, East); c.SetPlayerInput(TeamSide.Home, "hs2", East);
            Step(c, 300);
            Assert.LessOrEqual(c.State.Home.Find("hs2").Locomotion.Position.X, 20f + c.Infrastructure.PlayerBoundsMargin + 0.0001f);
            Assert.Greater(c.State.Home.Find("hs2").Locomotion.Position.X, 19.9f);
            MatchController off = Live();
            Put(off, TeamSide.Home, "hs2", 19f, 0f, East); off.SetPlayerInput(TeamSide.Home, "hs2", East); Step(off, 300);
            Assert.Greater(off.State.Home.Find("hs2").Locomotion.Position.X, 22f, "the older behaviour is untouched until the rules are switched on");
        }

        // ================= separation =================

        [Test]
        public void Separation_TwoOverlappingPoints_AreSeparatedToTheMinimum_Symmetrically()
        {
            var p = new[] { new Vec2(0f, 0f), new Vec2(0.1f, 0f) };
            Assert.IsFalse(PlayerSeparation.Resolve(p, 2, 0.5f, 3));
            Assert.AreEqual(0.5f, (p[1] - p[0]).Magnitude, 0.001f); Assert.AreEqual(0.1f, (p[0] + p[1]).X, 0.0001f, "the centre of the pair did not move");
            Assert.IsTrue(PlayerSeparation.Resolve(new[] { new Vec2(0f, 0f), new Vec2(3f, 0f) }, 2, 0.5f, 3), "far apart: nothing to do");
        }

        [Test]
        public void Separation_IdenticalPoints_AreSplitDeterministically_NeverNaN()
        {
            Vec2[] Run() { var q = Enumerable.Repeat(Vec2.Zero, 12).ToArray(); PlayerSeparation.Resolve(q, 12, 0.4f, 80); return q; }
            Vec2[] a = Run(), b = Run();
            for (int i = 0; i < 12; i++) { Assert.AreEqual(a[i], b[i]); Assert.IsFalse(float.IsNaN(a[i].X) || float.IsNaN(a[i].Y)); }
            Assert.GreaterOrEqual(PlayerSeparation.Smallest(a, 12), 0.4f * 0.9f, "twelve players on one point end up apart");
        }

        [Test]
        public void Separation_IsStable_AResolvedSetDoesNotMoveAgain()
        {
            var q = new[] { new Vec2(0f, 0f), new Vec2(0.2f, 0.1f), new Vec2(0.1f, 0.3f), new Vec2(5f, 5f) };
            PlayerSeparation.Resolve(q, 4, 0.5f, 20);
            var copy = (Vec2[])q.Clone();
            Assert.IsTrue(PlayerSeparation.Resolve(q, 4, 0.5f, 20)); CollectionAssert.AreEqual(copy, q);
            Assert.IsTrue(PlayerSeparation.Resolve(null, 0, 0.5f, 3)); Assert.IsTrue(PlayerSeparation.Resolve(q, 4, 0f, 3), "no minimum, no work");
            Assert.IsTrue(float.IsPositiveInfinity(PlayerSeparation.Smallest(q, 1)));
        }

        [Test]
        public void TheEngineKeepsTwelvePlayersApart_WhenTheSpaceRulesAreOn_AndOnlyThen()
        {
            MatchController c = Live();
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players) c.PlacePlayer(t.Side, p.PlayerId, Vec2.Zero);
            Step(c, 5);
            Assert.AreEqual(0f, (c.State.Home.Find("hs1").Locomotion.Position - c.State.Home.Find("hs2").Locomotion.Position).Magnitude, 0.0001f, "off: the older behaviour");
            c.Infrastructure.EnforcePlayerSpace = true;
            Step(c, 90);
            var pos = new List<Vec2>();
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away }) foreach (MatchPlayer p in t.Players.Where(x => x.IsActive)) pos.Add(p.Locomotion.Position);
            Assert.AreEqual(12, pos.Count);
            Assert.GreaterOrEqual(PlayerSeparation.Smallest(pos.ToArray(), 12), c.Infrastructure.MinSeparation * 0.9f);
            string a = Fingerprint(c);
            MatchController d = Live();
            foreach (TeamMatchState t in new[] { d.State.Home, d.State.Away }) foreach (MatchPlayer p in t.Players) d.PlacePlayer(t.Side, p.PlayerId, Vec2.Zero);
            Step(d, 5); d.Infrastructure.EnforcePlayerSpace = true; Step(d, 90);
            Assert.AreEqual(a, Fingerprint(d), "deterministic");
        }

        [Test]
        public void TheSeparationMinimum_IsConfigurable()
        {
            MatchController c = Live();
            c.Infrastructure.EnforcePlayerSpace = true; c.Infrastructure.MinSeparation = 3f;
            Put(c, TeamSide.Home, "hs2", 0f, 0f, East); Put(c, TeamSide.Home, "hs3", 0.5f, 0f, East);
            Step(c, 3);
            Assert.GreaterOrEqual((c.State.Home.Find("hs2").Locomotion.Position - c.State.Home.Find("hs3").Locomotion.Position).Magnitude, 3f - 0.1f);
        }
    }
}
