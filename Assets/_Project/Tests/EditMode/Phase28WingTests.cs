using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 28: play down the flanks, on hand-made worlds (the same quiet pitch and six-slot formation as the Phase 10 tests): a defender who overlaps only when the team has had the ball for a moment, the ball is on his
    /// flank and somebody else stays behind it; and the cross, a lofted pass from the wing to a team-mate in the box.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static string OverlapperOf(MatchController c, out BasicDecisionEngine engine, AttackPositioningTuning tuning = null)
        {
            engine = EngineOf(c, tuning);
            WorldView v = ViewOf(c);
            engine.Attack.Assign(v, v.IndexOf("hs3"));
            foreach (string id in new[] { "hs1", "hs2" }) if (engine.Attack.RunOf(v.IndexOf(id)) == RunKind.Forward && engine.Attack.JobOf(v.IndexOf(id)) == AttackJob.GetOpen) return id;
            return null;
        }

        [Test]
        public void Overlap_ADefenderJoinsOnTheFlankOfTheBall_WhenTheTeamHasHadItAndSomebodyCovers()
        {
            MatchController c = AttackScene(-2f, 8f);
            c.State.Ball.SecondsSincePossessionChange = 3f;
            string id = OverlapperOf(c, out BasicDecisionEngine e);
            Assert.IsNotNull(id, "a defender overlaps");
            Assert.Greater(HomeOf(c, id).Y, 0f, "the one on the flank of the ball");
            PlayerDecision d = e.Decide(ViewOf(c), id);
            Assert.IsTrue(d.Movement.HasTarget);
            Assert.Greater(d.Movement.Target.X, -2f, "he runs towards and past the carrier");
            Assert.Greater(d.Movement.Target.Y, 8f, "on the outside");
            Assert.AreEqual(new AttackPositioningTuning().OverlapMagnitude, d.Movement.Magnitude, 0.001f);
        }

        [Test]
        public void Overlap_NeverLeavesTheTeamWithoutCover_NorStartsInATransition_NorOnTheOtherFlank()
        {
            // nobody behind the ball: the other defender and everybody else is level with or ahead of the carrier
            MatchController c = AttackScene(-2f, 8f);
            c.State.Ball.SecondsSincePossessionChange = 3f;
            Put(c, TeamSide.Home, "hs1", 9f, -5f, East); Put(c, TeamSide.Home, "hs2", 9f, 5f, East); Put(c, TeamSide.Home, "hs4", 12f, 3f, East); Put(c, TeamSide.Home, "hs5", 14f, 0f, East);
            Assert.IsNull(OverlapperOf(c, out _), "without cover nobody goes");
            // just after winning the ball
            MatchController t = AttackScene(-2f, 8f);
            t.State.Ball.SecondsSincePossessionChange = 0.2f;
            Assert.IsNull(OverlapperOf(t, out _), "not in the middle of a transition");
            // the ball on the other flank: the defender of the far side does not go
            MatchController o = AttackScene(-2f, -8f);
            o.State.Ball.SecondsSincePossessionChange = 3f;
            string id = OverlapperOf(o, out _);
            Assert.IsTrue(id == null || HomeOf(o, id).Y < 0f, "only a defender of the flank of the ball");
            // switched off
            MatchController s = AttackScene(-2f, 8f);
            s.State.Ball.SecondsSincePossessionChange = 3f;
            Assert.IsNull(OverlapperOf(s, out _, new AttackPositioningTuning { OverlapRunners = 0 }));
        }
    }
}
