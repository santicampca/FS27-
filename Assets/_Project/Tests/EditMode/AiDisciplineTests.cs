using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 21: the AI knows what a card means. A defender with a yellow accepts less risk and does not slide; one who is close to a red accepts none; with a clean sheet nothing changes.</summary>
    public partial class AiInfrastructureTests
    {
        private static void Book(MatchController c, string id, int yellows, int fouls)
        {
            TeamSide side = id.StartsWith("h") ? TeamSide.Home : TeamSide.Away;
            for (int i = 0; i < fouls; i++) c.CallFoul(side, id, "as2", new RefereeDecision { IsFoul = true }, Vec3.Zero);
            if (yellows > 0) c.IssueCard(side, id, CardKind.Yellow);
        }

        private static MatchController BehindTheCarrier()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs3", 0.9f, 0f, West);   // right behind the carrier: a risky contact (risk 1 of the 1.2 a Classic defender accepts)
            return c;
        }

        [Test]
        public void Discipline_ACleanSheetDefender_StillChallengesFromBehind_AsBefore()
        {
            Assert.AreEqual(DecisionKind.Challenge, Decide(BehindTheCarrier(), "hs3").Kind);
        }

        [Test]
        public void Discipline_ADefenderWithAYellow_AcceptsLessRisk_AndStopsChallengingFromBehind()
        {
            MatchController c = BehindTheCarrier();
            Book(c, "hs3", 1, 0);
            PlayerDecision d = Decide(c, "hs3");
            Assert.AreNotEqual(DecisionKind.Challenge, d.Kind, "booked: he does not go in from behind"); Assert.IsFalse(d.HasAction && d.Action.Kind == FootballActionKind.PhysicalChallenge);
        }

        [Test]
        public void Discipline_ADefenderCloseToARed_AcceptsNoRiskyContact_ButStillContestsTheSafeOnes()
        {
            MatchController c = BehindTheCarrier();
            Book(c, "hs3", 1, new BasicDecisionTuning() is BasicDecisionTuning ? new DefenseTuning().NearRedFouls : 2);
            Assert.AreNotEqual(DecisionKind.Challenge, Decide(c, "hs3").Kind);
            // from the side the contact has no risk: he still does it
            MatchController side = Defending();
            Put(side, TeamSide.Home, "hs3", 0f, 1.3f, East);
            Book(side, "hs3", 1, new DefenseTuning().NearRedFouls);
            Assert.AreEqual(DecisionKind.Challenge, Decide(side, "hs3").Kind, "a safe contact from the side");
        }

        [Test]
        public void Discipline_ADefenderWithAYellow_NeverSlides_OneWithoutItDoes()
        {
            MatchController free = Runner(4f, new Vec2(-4f, 0f));
            Put(free, TeamSide.Home, "hs3", -2.3f, 0.2f, East);
            Assert.AreEqual(DecisionKind.SlidingTackle, Decide(free, "hs3").Kind, "without a card he slides (the scene of the sliding-tackle test)");
            MatchController booked = Runner(4f, new Vec2(-4f, 0f));
            Put(booked, TeamSide.Home, "hs3", -2.3f, 0.2f, East);
            Book(booked, "hs3", 1, 0);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(booked, "hs3").Kind, "booked: no slide");
        }

        [Test]
        public void Discipline_TheFoulsWithoutACardChangeNothing_OnlyTheCardDoes()
        {
            MatchController c = BehindTheCarrier();
            Book(c, "hs3", 0, 3);
            Assert.AreEqual(DecisionKind.Challenge, Decide(c, "hs3").Kind, "three fouls but no yellow yet: the AI's caution is about the cards");
        }

        [Test]
        public void Discipline_InArenaThereAreNoCards_SoNothingChanges()
        {
            MatchSettings s = MatchSettings.CreateArena();
            MatchController c = Defending(80, 80, s);
            Put(c, TeamSide.Home, "hs3", 0.9f, 0f, West);
            PlayerDecision d = Decide(c, "hs3");
            c.IssueCard(TeamSide.Home, "hs3", CardKind.Yellow);     // refused in this mode
            Assert.AreEqual(0, c.State.Home.Find("hs3").YellowCards);
            Assert.AreEqual(d.Kind, Decide(c, "hs3").Kind);
        }

        [Test]
        public void Discipline_TheWorldViewCarriesTheCardsAndTheFouls()
        {
            MatchController c = Defending();
            Book(c, "hs3", 1, 2);
            WorldState w = WorldState.Capture(c);
            WorldPlayer p = w[w.IndexOf("hs3")];
            Assert.AreEqual(1, p.YellowCards); Assert.AreEqual(2, p.FoulsCommitted);
            Assert.AreEqual(0, w[w.IndexOf("hs4")].YellowCards);
        }
    }
}
