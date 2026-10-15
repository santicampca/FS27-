using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 35: the cross is a real action of the Core (FS27-REQ-0161..0180): a lofted ball from the flank with a higher arc than a long pass that lands about where a long pass of the same power would.</summary>
    public partial class AiInfrastructureTests
    {
        private static void Flight(FootballActionKind kind, float power, out float apex, out float landedX, out ActionResult result)
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs3", 4f, -10f, East);
            Put(c, TeamSide.Home, "hs4", 14f, 0f, East);
            result = c.Perform(new ActionRequest(TeamSide.Home, "hs3", kind, new Vec2(10f, 10f).Normalized, power) { Source = ControlSource.Ai });
            apex = 0f; landedX = float.NaN; bool airborne = false;
            for (int i = 0; i < 300; i++)
            {
                c.Advance(Dt); float h = c.State.Ball.Height(c.BallParameters.Radius);
                apex = Math.Max(apex, h); if (h > 0.5f) airborne = true;
                if (airborne && h <= 0.3f) { landedX = c.State.Ball.Physics.Position.X; break; }
            }
        }

        [Test]
        public void ACross_IsARealAction_WithAHigherArcThanALongPass_AndLandsAboutWhereALongPassOfTheSamePowerLands()
        {
            Flight(FootballActionKind.Cross, 0.6f, out float crossApex, out float crossX, out ActionResult cross);
            Flight(FootballActionKind.LongPass, 0.6f, out float longApex, out float longX, out ActionResult lng);
            Assert.AreNotEqual(ActionOutcome.Rejected, cross.Outcome, "a cross is no longer NotSupported");
            Assert.AreEqual(FootballActionKind.Cross, cross.Kind);
            Assert.Greater(crossApex, longApex * 1.2f, "a higher arc than a long pass");
            Assert.AreEqual(longX - 4f, crossX - 4f, (longX - 4f) * 0.25f, "and it lands about where a long pass of the same power lands");
        }

        [Test]
        public void TheAI_CrossesWithTheRealAction_OnlyWhenAsked_AndStillDecidesTheSameCross()
        {
            GbScenario wing = GbScenes().First(s => s.Id == "A2_WingPlay");
            double on = 0, off = 0, onAny = 0, offAny = 0;
            foreach (int seed in Enumerable.Range(1, 12))
            {
                var a = GbRun(wing, GbCurrent, seed); var b = GbRun(wing, GbCurrent.With("nocross", x => x.Crosses = false), seed);
                on += a["h_crossKicks"]; off += b["h_crossKicks"]; onAny += a["h_crosses"]; offAny += b["h_crosses"];
            }
            Assert.Greater(on, 0, "with the real action on, the AI crosses with it");
            Assert.AreEqual(0, off, "off (the default): the Phase-28 long pass, no cross action");
            Assert.Greater(onAny, 0); Assert.Greater(offAny, 0);
        }
    
        [Test]
        public void AnAerialBall_WithTwoPlayersInReach_IsHeadedByExactlyOne_AndTheSameWayEveryTime()
        {
            string Duel(int height10)
            {
                MatchController c = Quiet(); c.HeadersEnabled = true;
                Put(c, TeamSide.Home, "hs3", 3.2f, 0f, East); Put(c, TeamSide.Away, "as3", 2.9f, 0.2f, West);
                c.State.Ball.Physics = new BallState(new Vec3(3f, height10 / 10f, 0f), Vec3.Zero) { IsGrounded = false }; c.State.Ball.Mode = BallMode.InPass;
                var who = new System.Collections.Generic.List<string>();
                c.ActionPerformed += r => { if (r.Kind == FootballActionKind.Header) who.Add(r.PlayerId); };
                for (int i = 0; i < 6; i++) c.Advance(Dt);
                return string.Join(",", who);
            }
            var winners = new System.Collections.Generic.HashSet<string>();
            foreach (int h in new[] { 14, 16, 18, 20, 12 }) { string a = Duel(h), b = Duel(h); Assert.AreEqual(a, b, "deterministic"); Assert.AreEqual(1, a.Split(',').Length, "exactly one header: " + a); winners.Add(a); }
        }
    }
}
