using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 21: the referee's judgement of a contact (RefereeDecisionSystem: a pure function), the cards and the disciplinary history (MatchController.CallFoul / IssueCard), the referee's style, and that the calls do not depend on the
    /// random numbers. Field 40 x 25, Classic unless the test says Arena.
    /// </summary>
    public class RefereeDecisionTests
    {
        private static readonly RefereeTuning Tuning = new RefereeTuning();
        private static readonly RefereeStyle Neutral = RefereeStyle.Neutral;
        private static readonly Vec2 East = new Vec2(1f, 0f), West = new Vec2(-1f, 0f);

        // ---- the evidence of some typical contacts
        private static ContactEvidence CleanTackle() { return new ContactEvidence { TouchedBallFirst = true, Contact = true, ClosingSpeed01 = 0.3f }; }
        private static ContactEvidence LateFromBehind() { return new ContactEvidence { TouchedBallFirst = false, Contact = true, FromBehind = true, ClosingSpeed01 = 0.3f }; }

        // ================= contacts =================

        [Test]
        public void Contact_ACleanTackle_PlayedTheBallFirst_IsNotAFoul()
        {
            RefereeDecision d = RefereeDecisionSystem.Decide(CleanTackle(), Neutral, Tuning);
            Assert.IsFalse(d.IsFoul); Assert.AreEqual(CardDecision.None, d.Card);
            ContactEvidence strongButClean = CleanTackle(); strongButClean.ClosingSpeed01 = 1f; strongButClean.FromBehind = true;
            Assert.IsFalse(RefereeDecisionSystem.Decide(strongButClean, Neutral, Tuning).IsFoul, "playing the ball first, even at speed and from behind, is not a foul (when it is not a slide)");
        }

        [Test]
        public void Contact_ALateTackle_HitsAfterTheBall_IsAFoul()
        {
            RefereeDecision d = RefereeDecisionSystem.Decide(LateFromBehind(), Neutral, Tuning);
            Assert.IsTrue(d.IsFoul); Assert.IsTrue((d.Reasons & FoulReason.Late) != 0); Assert.IsTrue((d.Reasons & FoulReason.FromBehind) != 0);
        }

        [Test]
        public void Contact_ADangerousSlide_IsAFoul_EvenWhenItReachedTheBall()
        {
            var e = new ContactEvidence { TouchedBallFirst = true, Contact = true, IsSlide = true, FromBehind = true, ClosingSpeed01 = 0.7f };
            RefereeDecision d = RefereeDecisionSystem.Decide(e, Neutral, Tuning);
            Assert.IsTrue(d.IsFoul); Assert.IsTrue((d.Reasons & FoulReason.Dangerous) != 0); Assert.AreEqual(CardDecision.Yellow, d.Card, "a dangerous slide is booked");
            e.FromBehind = false; e.TouchedBallFirst = true;
            Assert.IsFalse(RefereeDecisionSystem.Decide(e, Neutral, Tuning).IsFoul, "the same slide from the front that plays the ball is clean");
        }

        [Test]
        public void Contact_WithoutContact_ThereIsNoFoul_HoweverLateItWas()
        {
            ContactEvidence e = LateFromBehind(); e.Contact = false; e.ClosingSpeed01 = 1f; e.Clumsy = true;
            Assert.IsFalse(RefereeDecisionSystem.Decide(e, Neutral, Tuning).IsFoul);
        }

        [Test]
        public void Contact_ALightLateBrushFromTheFront_IsLetGo()
        {
            var e = new ContactEvidence { TouchedBallFirst = false, Contact = true, ClosingSpeed01 = 0.2f };
            RefereeDecision d = RefereeDecisionSystem.Decide(e, Neutral, Tuning);
            Assert.IsFalse(d.IsFoul, "late but light, from the front: play on (severity " + d.Severity + ")");
        }

        // ================= cards =================

        [Test]
        public void Cards_AnOrdinaryFoul_HasNoCard()
        {
            RefereeDecision d = RefereeDecisionSystem.Decide(LateFromBehind(), Neutral, Tuning);
            Assert.IsTrue(d.IsFoul); Assert.AreEqual(CardDecision.None, d.Card);
        }

        [Test]
        public void Cards_AStrongFoul_IsAYellow_ButNotARed()
        {
            ContactEvidence e = LateFromBehind(); e.ClosingSpeed01 = 0.9f; e.Clumsy = true;
            RefereeDecision d = RefereeDecisionSystem.Decide(e, Neutral, Tuning);
            Assert.AreEqual(CardDecision.Yellow, d.Card, "severity " + d.Severity);
        }

        [Test]
        public void Cards_StoppingACounterAttack_AndPersistentFouls_AreYellow()
        {
            ContactEvidence tactical = LateFromBehind(); tactical.StoppedCounterAttack = true;
            Assert.AreEqual(CardDecision.Yellow, RefereeDecisionSystem.Decide(tactical, Neutral, Tuning).Card);
            ContactEvidence persistent = LateFromBehind(); persistent.PriorFouls = Tuning.PersistentFouls;
            RefereeDecision d = RefereeDecisionSystem.Decide(persistent, Neutral, Tuning);
            Assert.AreEqual(CardDecision.Yellow, d.Card); Assert.IsTrue((d.Reasons & FoulReason.Persistent) != 0);
            persistent.PriorFouls = Tuning.PersistentFouls - 1;
            Assert.AreEqual(CardDecision.None, RefereeDecisionSystem.Decide(persistent, Neutral, Tuning).Card, "one foul fewer: no card yet");
        }

        [Test]
        public void Cards_ADirectRed_IsKeptForTheLastManDenyingAClearChance_AndForExtremeDanger()
        {
            ContactEvidence lastMan = LateFromBehind(); lastMan.DeniedGoalChance = true; lastMan.ClosingSpeed01 = 0.7f;
            RefereeDecision d = RefereeDecisionSystem.Decide(lastMan, Neutral, Tuning);
            Assert.AreEqual(CardDecision.Red, d.Card); Assert.IsTrue((d.Reasons & FoulReason.DeniedGoalChance) != 0);
            ContactEvidence brush = new ContactEvidence { TouchedBallFirst = false, Contact = true, DeniedGoalChance = true, ClosingSpeed01 = 0.1f };
            Assert.AreNotEqual(CardDecision.Red, RefereeDecisionSystem.Decide(brush, Neutral, Tuning).Card, "a clear chance denied by a brush of no weight is not a red");
            var extreme = new ContactEvidence { TouchedBallFirst = false, Contact = true, IsSlide = true, FromBehind = true, ClosingSpeed01 = 1f };
            Assert.AreEqual(CardDecision.Red, RefereeDecisionSystem.Decide(extreme, Neutral, Tuning).Card, "a slide from behind at full speed");
        }

        [Test]
        public void Cards_RedsAreRare_AMildFoulNeverGetsOne_WhateverTheSituationIsNot()
        {
            for (int mask = 0; mask < 32; mask++)
            {
                var e = new ContactEvidence { TouchedBallFirst = (mask & 1) != 0, Contact = true, FromBehind = (mask & 2) != 0, IsSlide = false, Clumsy = (mask & 4) != 0, StoppedCounterAttack = (mask & 8) != 0, ClosingSpeed01 = 0.4f, PriorFouls = (mask & 16) != 0 ? 5 : 0 };
                Assert.AreNotEqual(CardDecision.Red, RefereeDecisionSystem.Decide(e, Neutral, Tuning).Card, "mask " + mask + ": no last man, no slide: at most a yellow");
            }
        }

        // ================= the referee's style =================

        [Test]
        public void Style_AStrictRefereeCallsMore_AndBooksSooner_ThanAPermissiveOne()
        {
            RefereeStyle strict = new RefereeStyle { Strictness = 1f, Cards = 1f }, loose = new RefereeStyle { Strictness = 0f, Cards = 0f };
            var borderline = new ContactEvidence { TouchedBallFirst = false, Contact = true, FromBehind = false, ClosingSpeed01 = 0.5f };   // late, from the front, brisk
            Assert.IsTrue(RefereeDecisionSystem.Decide(borderline, strict, Tuning).IsFoul, "the strict referee calls it");
            Assert.IsFalse(RefereeDecisionSystem.Decide(borderline, loose, Tuning).IsFoul, "the permissive one lets it go");
            ContactEvidence e = LateFromBehind(); e.ClosingSpeed01 = 0.8f; e.IsSlide = true;
            Assert.AreEqual(CardDecision.Yellow, RefereeDecisionSystem.Decide(e, strict, Tuning).Card);
        }

        [Test]
        public void Style_ComesFromTheRefereeProfileOfTheData_AndNoProfileIsANeutralReferee()
        {
            Assert.AreEqual(0.5f, RefereeStyle.From(null).Strictness, 1e-6f);
            RefereeStyle s = RefereeStyle.From(new RefereeProfile { Strictness = 80, Cards = 20 });
            Assert.AreEqual(0.8f, s.Strictness, 1e-6f); Assert.AreEqual(0.2f, s.Cards, 1e-6f);
            Assert.AreEqual(1f, RefereeStyle.From(new RefereeProfile { Strictness = 500 }).Strictness, 1e-6f, "clamped");
        }

        // ================= consistency =================

        [Test]
        public void Consistency_TheSameEvidenceIsTheSameDecision_Always()
        {
            ContactEvidence e = LateFromBehind(); e.ClosingSpeed01 = 0.77f; e.Clumsy = true; e.PriorFouls = 2;
            RefereeDecision first = RefereeDecisionSystem.Decide(e, Neutral, Tuning);
            for (int i = 0; i < 1000; i++)
            {
                RefereeDecision d = RefereeDecisionSystem.Decide(e, Neutral, Tuning);
                Assert.AreEqual(first.IsFoul, d.IsFoul); Assert.AreEqual(first.Card, d.Card); Assert.AreEqual(first.Reasons, d.Reasons); Assert.AreEqual(first.Severity, d.Severity);
            }
        }

        [Test]
        public void Consistency_ASeverityOnlyGrowsWithWhatMakesAContactWorse()
        {
            ContactEvidence e = new ContactEvidence { TouchedBallFirst = true, Contact = true };
            float previous = RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity;
            foreach (Action<ContactEvidence> worse in new Action<ContactEvidence>[0]) { }
            e.TouchedBallFirst = false; float s1 = RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity; Assert.Greater(s1, previous);
            e.FromBehind = true; float s2 = RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity; Assert.Greater(s2, s1);
            e.ClosingSpeed01 = 0.8f; float s3 = RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity; Assert.Greater(s3, s2);
            e.IsSlide = true; float s4 = RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity; Assert.Greater(s4, s3);
            e.Clumsy = true; Assert.Greater(RefereeDecisionSystem.Decide(e, Neutral, Tuning).Severity, s4);
        }

        // ================= the match: records, cards, history =================

        private static SquadMember[] Starters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false, 100f)); return l.ToArray(); }
        private static SquadMember[] Bench(string prefix) { return new[] { new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f), new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f) }; }

        private sealed class Lookup : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Players = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Players.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private static PlayerAttributes All(int v)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = a.Acceleration = a.Stamina = a.BallControl = a.Passing = a.Shooting = a.Defense = a.Strength = a.Agility = a.Finishing = a.Dribbling = a.Technique = v;
            return a;
        }

        /// <summary>A defender a little weaker than the carrier (so that some duels are lost, not all).</summary>
        private static Lookup WeakDefender()
        {
            var l = new Lookup();
            l.Players["hs3"] = new PlayerDefinition("hs3", "hs3", 1, PlayerRole.Defender, All(60));
            l.Players["as2"] = new PlayerDefinition("as2", "as2", 1, PlayerRole.Midfielder, All(70));
            return l;
        }

        private static MatchController Live(GameMode mode = GameMode.Classic, int seed = 1, IPlayerLookup lookup = null)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            s.Seed = seed;
            var c = new MatchController(new FieldDimensions(), s, new MatchSquad(Starters("h"), Bench("h")), new MatchSquad(Starters("a"), Bench("a")), null, "m", null, lookup);
            c.Start();
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -16f, -10f + i * 4f), East.ToHeading());
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 16f, -10f + i * 4f), West.ToHeading());
            }
            Assert.IsTrue(c.ConfirmRestartReady());
            return c;
        }

        private static RefereeDecision Call(CardDecision card, FoulReason reasons = FoulReason.Late) { return new RefereeDecision { IsFoul = true, Card = card, Reasons = reasons, Severity = 0.8f }; }

        [Test]
        public void Match_AFoul_IsCountedAgainstThePlayerAndTheTeam_RecordedAndPublished_WithoutStoppingThePlay()
        {
            MatchController c = Live();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            MatchPhase before = c.State.Phase; Vec3 ball = c.State.Ball.Physics.Position;
            MatchController.FoulCallResult r = c.CallFoul(TeamSide.Away, "as2", "hs3", Call(CardDecision.None), new Vec3(1f, 0.2f, 2f));
            Assert.IsTrue(r.Called); Assert.AreEqual(CardDecision.None, r.Card);
            MatchPlayer p = c.State.Away.Find("as2");
            Assert.AreEqual(1, p.FoulsCommitted); Assert.AreEqual(1, c.State.Away.Fouls); Assert.AreEqual(0, c.State.Home.Fouls); Assert.AreEqual(0, p.YellowCards); Assert.AreEqual(0, p.RedCards);
            Assert.AreEqual(1, c.State.Discipline.Count);
            DisciplineRecord rec = c.State.Discipline[0];
            Assert.AreEqual("as2", rec.PlayerId); Assert.AreEqual("hs3", rec.VictimId); Assert.AreEqual(TeamSide.Away, rec.Team); Assert.AreEqual(CardDecision.None, rec.Card);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.FoulCommitted && e.Team == TeamSide.Away));
            GameplayEvent fk = events.Single(e => e.Kind == GameplayEventKind.FreeKickAwarded);
            Assert.AreEqual(TeamSide.Home, fk.Team, "the fouled team is the one entitled to the free kick"); Assert.AreEqual("hs3", fk.PlayerId);
            Assert.AreEqual(before, c.State.Phase, "the free kick itself is a later phase: the play goes on"); Assert.AreEqual(ball, c.State.Ball.Physics.Position);
        }

        [Test]
        public void Match_AYellowCardIsShown_AndASecondYellowSendsTheManOff()
        {
            MatchController c = Live();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            MatchController.FoulCallResult first = c.CallFoul(TeamSide.Away, "as2", "hs3", Call(CardDecision.Yellow), Vec3.Zero);
            Assert.AreEqual(CardResult.Issued, first.CardResult);
            MatchPlayer p = c.State.Away.Find("as2");
            Assert.AreEqual(1, p.YellowCards); Assert.AreEqual(0, p.RedCards); Assert.AreEqual(MatchPlayerStatus.OnPitch, p.Status);
            MatchController.FoulCallResult second = c.CallFoul(TeamSide.Away, "as2", "hs3", Call(CardDecision.Yellow), Vec3.Zero);
            Assert.AreEqual(CardResult.DismissedBySecondYellow, second.CardResult);
            Assert.AreEqual(2, p.YellowCards); Assert.AreEqual(1, p.RedCards, "a second yellow is a red: he is sent off"); Assert.AreEqual(MatchPlayerStatus.Dismissed, p.Status); Assert.AreEqual(2, p.FoulsCommitted);
            Assert.AreEqual(2, events.Count(e => e.Kind == GameplayEventKind.YellowCardIssued)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.RedCardIssued)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PlayerDismissed));
            Assert.AreEqual(2, c.State.Discipline.Count); Assert.AreEqual(CardResult.DismissedBySecondYellow, c.State.Discipline[1].CardResult);
        }

        [Test]
        public void Match_ADirectRed_SendsHimOffAtOnce_AndTheTeamPlaysWithOneLess()
        {
            MatchController c = Live();
            MatchController.FoulCallResult r = c.CallFoul(TeamSide.Home, "hs4", "as1", Call(CardDecision.Red, FoulReason.DeniedGoalChance), Vec3.Zero);
            Assert.AreEqual(CardResult.DismissedByRed, r.CardResult);
            MatchPlayer p = c.State.Home.Find("hs4");
            Assert.AreEqual(0, p.YellowCards); Assert.AreEqual(1, p.RedCards); Assert.AreEqual(1, p.FoulsCommitted); Assert.AreEqual(MatchPlayerStatus.Dismissed, p.Status); Assert.IsFalse(p.IsActive);
            Assert.AreEqual(5, c.State.Home.Players.Count(x => x.IsActive), "five of six remain (the keeper and four)");
        }

        [Test]
        public void Match_InArena_ThereAreNoFoulsNoCardsAndNoEvents()
        {
            MatchController c = Live(GameMode.Arena);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            MatchController.FoulCallResult r = c.CallFoul(TeamSide.Away, "as2", "hs3", Call(CardDecision.Red), Vec3.Zero);
            Assert.IsFalse(r.Called); Assert.AreEqual(0, c.State.Discipline.Count); Assert.AreEqual(0, c.State.Away.Find("as2").FoulsCommitted); Assert.AreEqual(MatchPlayerStatus.OnPitch, c.State.Away.Find("as2").Status);
            Assert.IsFalse(events.Any(e => e.Kind == GameplayEventKind.FoulCommitted || e.Kind == GameplayEventKind.FreeKickAwarded || e.Kind == GameplayEventKind.RedCardIssued));
        }

        [Test]
        public void Match_ANoFoulDecisionIsNeverRecorded()
        {
            MatchController c = Live();
            Assert.IsFalse(c.CallFoul(TeamSide.Away, "as2", "hs3", new RefereeDecision { IsFoul = false }, Vec3.Zero).Called);
            Assert.AreEqual(0, c.State.Discipline.Count);
        }

        // ================= duels in a match: the referee judges them, the calls do not depend on the random numbers =================

        private static MatchController DuelScene(int seed, bool behind)
        {
            MatchController c = Live(GameMode.Classic, seed, WeakDefender());
            // the Away carrier faces west at the centre; the Home defender is behind him (east of him) or in front (west)
            c.PlacePlayer(TeamSide.Away, "as2", Vec2.Zero, West.ToHeading());
            c.State.Ball.Physics = new BallState(new Vec3(-0.8f, c.BallParameters.Radius, 0f), Vec3.Zero) { IsGrounded = true };
            c.ReleaseBall();
            Assert.IsTrue(c.GiveBallTo(TeamSide.Away, "as2"));
            c.PlacePlayer(TeamSide.Home, "hs3", behind ? new Vec2(0.9f, 0f) : new Vec2(-1.1f, 0.3f), behind ? West.ToHeading() : East.ToHeading());
            return c;
        }

        [Test]
        public void Duels_EveryFoulOfAMatchIsRecordedOnce_WithItsEventsAndCounters()
        {
            int fouls = 0, cards = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                MatchController c = DuelScene(seed, true);
                var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
                c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.PhysicalChallenge, new Vec2(-1f, 0f), 1f));
                int called = events.Count(e => e.Kind == GameplayEventKind.FoulCommitted);
                Assert.AreEqual(called, c.State.Discipline.Count, "seed " + seed);
                Assert.AreEqual(called, c.State.Home.Fouls); Assert.AreEqual(called, events.Count(e => e.Kind == GameplayEventKind.FreeKickAwarded));
                fouls += called; cards += c.State.Discipline.Count(d => d.Card != CardDecision.None);
                Assert.IsTrue(c.State.Discipline.All(d => d.Team == TeamSide.Home && d.PlayerId == "hs3" && d.VictimId == "as2"));
            }
            Assert.Greater(fouls, 0, "a challenge from behind is sometimes a foul");
            Assert.Less(fouls, 60, "and sometimes it is not (the referee does not whistle every contact)");
        }

        [Test]
        public void Duels_ATackleFromTheFront_IsMuchLessOftenAFoulThanOneFromBehind()
        {
            int behind = 0, front = 0;
            for (int seed = 1; seed <= 80; seed++)
            {
                MatchController b = DuelScene(seed, true), f = DuelScene(seed, false);
                b.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.PhysicalChallenge, new Vec2(-1f, 0f), 1f)); behind += b.State.Discipline.Count;
                f.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.PhysicalChallenge, new Vec2(-1f, 0f), 1f)); front += f.State.Discipline.Count;
            }
            Assert.Greater(behind, front, "behind " + behind + ", front " + front);
        }

        [Test]
        public void Duels_TheSameMatchGivesTheSameCalls_TheCallsDoNotComeFromTheRandomNumbers()
        {
            string Log(int seed)
            {
                MatchController c = DuelScene(seed, true);
                c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.PhysicalChallenge, new Vec2(-1f, 0f), 1f));
                return string.Join("|", c.State.Discipline.Select(d => d.PlayerId + d.Reasons + d.Card + d.Severity.ToString("F3")));
            }
            for (int seed = 1; seed <= 20; seed++) Assert.AreEqual(Log(seed), Log(seed), "seed " + seed);
        }

        [Test]
        public void Duels_InArena_NothingIsEverCalled()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                MatchController c = Live(GameMode.Arena, seed, WeakDefender());
                c.PlacePlayer(TeamSide.Away, "as2", Vec2.Zero, West.ToHeading());
                c.State.Ball.Physics = new BallState(new Vec3(-0.8f, c.BallParameters.Radius, 0f), Vec3.Zero) { IsGrounded = true };
                c.ReleaseBall(); Assert.IsTrue(c.GiveBallTo(TeamSide.Away, "as2"));
                c.PlacePlayer(TeamSide.Home, "hs3", new Vec2(0.9f, 0f), West.ToHeading());
                c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.PhysicalChallenge, new Vec2(-1f, 0f), 1f));
                Assert.AreEqual(0, c.State.Discipline.Count); Assert.AreEqual(0, c.State.Home.Fouls);
            }
        }
    }
}
