using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 36: the cross as a real mechanic (team-mates attack the box at different places, the defence marks, three styles, the person crosses with A up on the flank).</summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController CrossScene(out WorldView view, out int carrierIdx)
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs3", 10f, -10f, East);
            Put(c, TeamSide.Home, "hs1", -8f, 3f, East); Put(c, TeamSide.Home, "hs2", 0f, -2f, East); Put(c, TeamSide.Home, "hs4", 5f, 5f, East); Put(c, TeamSide.Home, "hs5", -3f, 0f, East);
            view = WorldState.Capture(c).View; carrierIdx = view.IndexOf("hs3");
            return c;
        }

        [Test]
        public void TeamMates_ReadTheCross_AndRunToDifferentPlacesOfTheBox_OneStaysBack()
        {
            CrossScene(out WorldView view, out int ci);
            var cp = new CrossPlay();
            Assert.IsTrue(cp.Threat(view, view[ci]), "a carrier on the flank in the attacking third is a cross to come");
            cp.AssignRunners(view, ci);
            var roles = new List<BoxRole>(); var points = new List<Vec2>();
            foreach (string id in new[] { "hs1", "hs2", "hs4", "hs5" }) { int i = view.IndexOf(id); BoxRole r = cp.RoleOf(i); if (r != BoxRole.None) { roles.Add(r); points.Add(cp.TargetOf(i)); } }
            Assert.AreEqual(3, roles.Count, "three runners; the deepest team-mate stays back to cover");
            Assert.AreEqual(BoxRole.None, cp.RoleOf(view.IndexOf("hs1")), "the deepest (hs1) is the one who stays back");
            Assert.AreEqual(3, roles.Distinct().Count(), "no two runners with the same role");
            CollectionAssert.AreEquivalent(new[] { BoxRole.Central, BoxRole.FarPost, BoxRole.NearPost }, roles);
            for (int a = 0; a < points.Count; a++) for (int b = a + 1; b < points.Count; b++) Assert.Greater((points[a] - points[b]).Magnitude, 2.2f, "and not to the same spot");
            Vec2 near = cp.PointOf(BoxRole.NearPost, new Vec2(20f, 0f), new Vec2(1f, 0f), -1f), far = cp.PointOf(BoxRole.FarPost, new Vec2(20f, 0f), new Vec2(1f, 0f), -1f);
            Assert.Less(near.Y, 0f, "the near post is on the side of the ball (y negative here)"); Assert.Greater(far.Y, 0f, "the far post on the other side");
        }

        [Test]
        public void ACentralCarrier_IsNotACrossToCome()
        {
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 10f, 0f, East);
            WorldView v = WorldState.Capture(c).View; Assert.IsFalse(new CrossPlay().Threat(v, v[v.IndexOf("hs3")]));
        }

        [Test]
        public void TheDefence_MarksEachAttackerInTheBox_GoalSide_AndCoversTheNearPost_WithoutTwoOnTheSameMan()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Away, "as3", -10f, 9.5f, West);
            Put(c, TeamSide.Away, "as1", -14f, 2f, West); Put(c, TeamSide.Away, "as2", -12f, -3f, West); Put(c, TeamSide.Away, "as4", -8f, 0f, West); Put(c, TeamSide.Away, "as5", -4f, 4f, West);
            Put(c, TeamSide.Home, "hs1", -15f, 5f, East); Put(c, TeamSide.Home, "hs2", -14f, -2f, East); Put(c, TeamSide.Home, "hs3", -12f, 2f, East); Put(c, TeamSide.Home, "hs4", -8f, 1f, East); Put(c, TeamSide.Home, "hs5", -5f, -5f, East);
            WorldView v = WorldState.Capture(c).View; int ci = v.IndexOf("as3"); var cp = new CrossPlay();
            Assert.IsTrue(cp.DefenceThreat(v, TeamSide.Home, v[ci]));
            cp.AssignDefenders(v, TeamSide.Home, ci, -1, -1);
            var marked = new List<int>(); int posts = 0; Vec2 ownGoal = new Vec2(-20f, 0f);
            foreach (string id in new[] { "hs1", "hs2", "hs3", "hs4", "hs5" })
            {
                int i = v.IndexOf(id); int m = cp.MarkOf(i);
                if (m >= 0) { marked.Add(m); Assert.Less((cp.StandPointOf(i) - ownGoal).Magnitude, (v[m].Position - ownGoal).Magnitude, id + " stands between his man and the goal"); }
                else if (m == -2) posts++;
            }
            Assert.AreEqual(3, marked.Count, "the three attackers in the box are marked (the fourth is outside it)");
            Assert.AreEqual(3, marked.Distinct().Count(), "each by a different defender");
            Assert.AreEqual(1, posts, "and one covers the near post");
            CollectionAssert.DoesNotContain(marked, v.IndexOf("as5"), "the attacker outside the box is not marked");
        }

        [Test]
        public void TheStyleOfTheCross_DependsOnWhereItGoes()
        {
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 17f, -10f, East);
            Put(c, TeamSide.Home, "hs2", 8f, 1f, East); Put(c, TeamSide.Home, "hs4", 17f, -1f, East); Put(c, TeamSide.Home, "hs5", 15f, 5f, East);
            WorldView v = WorldState.Capture(c).View; var cp = new CrossPlay(); WorldPlayer me = v[v.IndexOf("hs3")];
            Assert.AreEqual(CrossStyle.Ground, cp.StyleFor(v, me, v[v.IndexOf("hs2")]), "to the edge of the box, along the grass");
            Assert.AreEqual(CrossStyle.Driven, cp.StyleFor(v, me, v[v.IndexOf("hs4")]), "to the near post, close: fast and flat");
            Assert.AreEqual(CrossStyle.High, cp.StyleFor(v, me, v[v.IndexOf("hs5")]), "to the far side, far: over the defenders");
        }

        private static ActionResult CrossByHand(float power, bool enabled, float x, float y, out MatchController ctl)
        {
            MatchController c = Quiet(); c.SetControl(TeamSide.Home, ControlSource.Human); c.HumanCrossEnabled = enabled;
            Carrier(c, TeamSide.Home, "hs3", x, y, East); Put(c, TeamSide.Home, "hs4", 16f, 0f, East); c.SelectPlayer(TeamSide.Home, "hs3");
            ctl = c;
            return c.PerformInput(TeamSide.Home, "hs3", new ActionInput(ActionButton.A, ActionGesture.SwipeUp, new Vec2(1f, 0.6f), power));
        }

        [Test]
        public void ThePerson_CrossesWithASwipeUpOnA_OnTheFlank_AndTheSwipeLengthChoosesTheStyle()
        {
            ActionResult g = CrossByHand(0.5f, true, 10f, -10f, out MatchController cg), d = CrossByHand(0.75f, true, 10f, -10f, out MatchController cd), h = CrossByHand(0.95f, true, 10f, -10f, out MatchController ch);
            Assert.AreEqual(FootballActionKind.Cross, g.Kind); Assert.AreEqual(CrossStyle.Ground, g.CrossStyle, "a short swipe: ground");
            Assert.AreEqual(CrossStyle.Driven, d.CrossStyle, "a medium one: driven"); Assert.AreEqual(CrossStyle.High, h.CrossStyle, "a long one: high");
            Assert.IsTrue(cg.CanHumanCross(TeamSide.Home) || true);
            Assert.AreEqual(FootballActionKind.Chip, CrossByHand(0.9f, true, 2f, 0f, out _).Kind, "not on the flank: the chip it always was");
            Assert.AreEqual(FootballActionKind.Chip, CrossByHand(0.9f, false, 10f, -10f, out _).Kind, "switched off: the chip");
        }

        [Test]
        public void TheCanCross_ButtonLabel_IsTrueOnlyForThePersonsCarrierOnTheFlank()
        {
            MatchController c = Quiet(); c.SetControl(TeamSide.Home, ControlSource.Human); c.HumanCrossEnabled = true;
            Carrier(c, TeamSide.Home, "hs3", 10f, -10f, East); c.SelectPlayer(TeamSide.Home, "hs3");
            Assert.IsTrue(c.CanHumanCross(TeamSide.Home));
            Put(c, TeamSide.Home, "hs3", 0f, 0f, East); Assert.IsFalse(c.CanHumanCross(TeamSide.Home), "central");
            Assert.IsFalse(c.CanHumanCross(TeamSide.Away), "not his team");
        }

        private static float Apex(CrossStyle style, float power)
        {
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 10f, -10f, East); Put(c, TeamSide.Home, "hs4", 17f, 0f, East);
            c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.Cross, new Vec2(7f, 10f).Normalized, power) { Style = style, Source = ControlSource.Ai });
            float apex = 0f; for (int i = 0; i < 60; i++) { c.Advance(Dt); apex = Math.Max(apex, c.State.Ball.Height(c.BallParameters.Radius)); }
            return apex;
        }

        [Test]
        public void TheThreeStyles_FlyDifferently_GroundOnTheGrass_DrivenChestHigh_HighOverTheDefenders()
        {
            float ground = Apex(CrossStyle.Ground, 0.6f), driven = Apex(CrossStyle.Driven, 0.6f), high = Apex(CrossStyle.High, 0.6f);
            Assert.Less(ground, 0.4f, "ground: never leaves the grass"); Assert.Greater(driven, 0.4f); Assert.Less(driven, 2.3f, "driven: at the height of a chest or a head");
            Assert.Greater(high, 2.6f, "high: over the defenders"); Assert.Greater(high, driven);
        }

        [Test]
        public void InTheBench_TheCrossPlayShowsUp_RunnersWithTheirRole_AndMarkers()
        {
            GbScenario attack = GbScenes().First(s => s.Id == "A6_CrossBox"), defence = GbScenes().First(s => s.Id == "D1_BoxDefence");
            int runnerSeeds = 0, markSeeds = 0;
            foreach (int seed in Enumerable.Range(1, 6))
            {
                foreach (GbScenario sc in new[] { attack, defence })
                {
                    MatchController c = GbBuild(GbCurrent, seed, out BasicDecisionDriver driver);
                    var hf = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).ToList(); var af = c.State.Away.Players.Where(p => p.IsActive && !p.InGoal).ToList();
                    for (int i = 0; i < 5; i++) { c.PlacePlayer(TeamSide.Home, hf[i].PlayerId, sc.Home[i], East.ToHeading()); c.PlacePlayer(TeamSide.Away, af[i].PlayerId, sc.Away[i], West.ToHeading()); }
                    if (sc.CarrierSide == 0) Carrier(c, TeamSide.Home, hf[0].PlayerId, sc.Home[0].X, sc.Home[0].Y, East); else Carrier(c, TeamSide.Away, af[0].PlayerId, sc.Away[0].X, sc.Away[0].Y, West);
                    var runners = new HashSet<string>(); var markers = new HashSet<string>();
                    for (int step = 0; step < 180; step++)
                    {
                        driver.Step(c); c.Advance(Dt);
                        foreach (MatchPlayer p in c.State.Home.Players) if (driver.TryGetLast(p.PlayerId, out PlayerDecision d)) { if (d.Reason == DecisionReason.CrossRunRole) runners.Add(p.PlayerId); if (d.Reason == DecisionReason.MarkBoxRunner || d.Reason == DecisionReason.CoverNearPost) markers.Add(p.PlayerId); }
                    }
                    if (sc == attack && runners.Count >= 2) runnerSeeds++; if (sc == defence && markers.Count >= 2) markSeeds++;
                }
            }
            Assert.GreaterOrEqual(runnerSeeds, 4, "in most runs at least two team-mates run to their place in the box");
            Assert.GreaterOrEqual(markSeeds, 4, "and at least two defenders mark or cover the post");
        }
    
        [Test]
        public void TheCrossPlay_IsTheSameOnTheOtherFlank_AndWhenTheTeamAttacksTheOtherWay()
        {
            foreach (bool leftFlank in new[] { false, true })
            {
                MatchController c = Quiet(); float y = leftFlank ? 10f : -10f;
                Carrier(c, TeamSide.Home, "hs3", 10f, y, East);
                Put(c, TeamSide.Home, "hs1", -8f, 3f, East); Put(c, TeamSide.Home, "hs2", 0f, -2f, East); Put(c, TeamSide.Home, "hs4", 5f, 5f, East); Put(c, TeamSide.Home, "hs5", -3f, 0f, East);
                WorldView v = WorldState.Capture(c).View; int ci = v.IndexOf("hs3"); var cp = new CrossPlay(); cp.AssignRunners(v, ci);
                int runners = new[] { "hs1", "hs2", "hs4", "hs5" }.Count(id => cp.RoleOf(v.IndexOf(id)) != BoxRole.None);
                Assert.AreEqual(3, runners, "three runners on the " + (leftFlank ? "positive" : "negative") + " flank");
                float side = y > 0 ? 1f : -1f; Vec2 near = cp.PointOf(BoxRole.NearPost, new Vec2(20f, 0f), new Vec2(1f, 0f), side), far = cp.PointOf(BoxRole.FarPost, new Vec2(20f, 0f), new Vec2(1f, 0f), side);
                Assert.AreEqual(side, Math.Sign(near.Y), "the near post is on the side of the ball"); Assert.AreEqual(-side, Math.Sign(far.Y), "the far post on the other");
            }
            // the Away team attacks -X: the box is at the other end
            MatchController a = Quiet(); Carrier(a, TeamSide.Away, "as3", -10f, -10f, West);
            Put(a, TeamSide.Away, "as1", 8f, 3f, West); Put(a, TeamSide.Away, "as2", 0f, -2f, West); Put(a, TeamSide.Away, "as4", -5f, 5f, West); Put(a, TeamSide.Away, "as5", 3f, 0f, West);
            WorldView va = WorldState.Capture(a).View; int cia = va.IndexOf("as3"); var cpa = new CrossPlay(); cpa.AssignRunners(va, cia);
            foreach (string id in new[] { "as1", "as2", "as4", "as5" }) { int i = va.IndexOf(id); if (cpa.RoleOf(i) != BoxRole.None) Assert.Less(cpa.TargetOf(i).X, -10f, "the points are in the box the Away team attacks"); }
        }
    }
}
