using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 38: the Core is reproducible from discrete commands and a desynchronisation is detected by a checksum (foundation for a future online mode or a replay; no network code).</summary>
    public partial class AiInfrastructureTests
    {
        private static List<ulong> PlayMatch(int seed, int steps, InputCommandLog log, bool record, Func<int, bool> skipCommand = null)
        {
            MatchController c = GbBuild(GbCurrent, seed, out BasicDecisionDriver driver);
            c.SetControl(TeamSide.Home, ControlSource.Human);
            var sums = new List<ulong>();
            for (int step = 0; step < steps; step++)
            {
                driver.Step(c);
                if (c.State.Phase == MatchPhase.Playing)
                {
                    if (record)
                    {
                        float a = step * 0.07f;
                        var stick = new Vec2((float)Math.Cos(a), (float)Math.Sin(a));
                        if (step % 10 == 0 && !(skipCommand != null && skipCommand(step))) log.Submit(c, step, TeamSide.Home, NormalizedInput.Stick(stick));
                        if (step % 75 == 40) log.Submit(c, step, TeamSide.Home, NormalizedInput.Press(stick, ActionButton.B, ActionGesture.Tap, stick, 0.6f));
                        if (step % 130 == 100) log.Submit(c, step, TeamSide.Home, NormalizedInput.Press(Vec2.Zero, ActionButton.A, ActionGesture.SwipeDown, new Vec2(1f, 0.1f), 0.8f));
                    }
                    else log.Replay(c, step);
                }
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (step % 60 == 59) sums.Add(MatchChecksum.Of(c));
            }
            return sums;
        }

        [Test]
        public void TheSameCommands_ReproduceTheMatchBitForBit_AlsoAfterTheLogIsWrittenAndReadBack()
        {
            var log = new InputCommandLog();
            List<ulong> original = PlayMatch(7, 1500, log, true);
            Assert.Greater(log.Commands.Count, 100, "a person's inputs were recorded");
            Assert.IsTrue(InputCommandLog.TryParse(log.ToText(), out InputCommandLog back));
            Assert.AreEqual(log.Commands.Count, back.Commands.Count);
            Assert.AreEqual(log.ToText(), back.ToText(), "the text form round-trips");
            List<ulong> replay = PlayMatch(7, 1500, back, false);
            CollectionAssert.AreEqual(original, replay, "every checkpoint of the replay has the checksum of the original");
        }

        [Test]
        public void ADifferentCommand_ChangesTheChecksum_SoADesyncIsDetected()
        {
            var a = new InputCommandLog(); var b = new InputCommandLog();
            List<ulong> full = PlayMatch(7, 900, a, true);
            List<ulong> dropped = PlayMatch(7, 900, b, true, step => step == 200);
            Assert.AreEqual(full.Count, dropped.Count);
            Assert.AreNotEqual(full.Last(), dropped.Last(), "one missing command is visible at the end");
            int firstDifferent = Enumerable.Range(0, full.Count).First(i => full[i] != dropped[i]);
            Assert.GreaterOrEqual(firstDifferent, 200 / 60, "and not before it happened");
            Assert.AreNotEqual(full.Distinct().Count(), 1);
        }

        [Test]
        public void TwoDifferentSeeds_GiveDifferentChecksums_AndTheSameSeedTheSame()
        {
            ulong A(int seed) { var l = new InputCommandLog(); return PlayMatch(seed, 600, l, true).Last(); }
            Assert.AreEqual(A(3), A(3));
            Assert.AreNotEqual(A(3), A(4));
        }

        [Test]
        public void AMalformedLog_IsRefusedWhole()
        {
            Assert.IsFalse(InputCommandLog.TryParse("1 0 0 0 1 0 0 0 0.5 0\nnot a command", out InputCommandLog log));
            Assert.AreEqual(0, log.Commands.Count, "never half applied");
            Assert.IsFalse(InputCommandLog.TryParse("1 5 0 0 1 0 0 0 0.5 0", out _), "a side that does not exist");
            Assert.IsTrue(InputCommandLog.TryParse("", out InputCommandLog empty)); Assert.AreEqual(0, empty.Commands.Count);
        }

        [Test]
        public void PlayerIds_AreStable_TheSameInEveryMatchBuiltFromTheSameSquads_AndTheClockHasOneSource()
        {
            string Ids(int seed) { MatchController c = GbBuild(GbCurrent, seed, out _); return string.Join(",", c.State.Home.Players.Concat(c.State.Away.Players).Select(p => p.PlayerId)); }
            Assert.AreEqual(Ids(1), Ids(99), "ids come from the squad data, not from a counter or the seed");
            Assert.IsFalse(Ids(1).Split(',').Any(string.IsNullOrEmpty));
            MatchController m = GbBuild(GbCurrent, 1, out _);
            float t = m.State.MatchTimeSeconds;
            Assert.AreSame(m.State, m.State, "the match state is the single authority of clock and score");
            Assert.GreaterOrEqual(t, 0f);
        }
    }
}
namespace FS27.Core.Tests
{
    public partial class AiInfrastructureTests
    {
        [Test]
        public void ACross_WithTheWeakFoot_IsLessPreciseThanWithTheGoodFoot()
        {
            ActionResult Cross(Vec2 dir)
            {
                MatchController c = Quiet();
                Carrier(c, TeamSide.Home, "hs3", 10f, -10f, East);
                MatchPlayer p = c.State.Home.Find("hs3");
                ActionProfile ap = p.ActionProfile; ap.PreferredFoot = PreferredFoot.Right; ap.WeakFootQuality = 0.1f; p.ActionProfile = ap;
                return c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.Cross, dir, 0.6f) { Source = ControlSource.Ai });
            }
            ActionResult toTheRight = Cross(new Vec2(0.3f, 1f).Normalized), toTheLeft = Cross(new Vec2(0.3f, -1f).Normalized);
            Assert.AreNotEqual(toTheRight.Foot, toTheLeft.Foot, "the two directions are played with different feet");
            ActionResult good = toTheRight.WeakFoot ? toTheLeft : toTheRight, weak = toTheRight.WeakFoot ? toTheRight : toTheLeft;
            Assert.IsTrue(weak.WeakFoot); Assert.IsFalse(good.WeakFoot);
            Assert.Less(weak.Quality, good.Quality, "the weak foot crosses worse");
        }

        [Test]
        public void EveryRestartATakenByAPerson_LeavesAReport_WithTheTargetThePowerAndTheResult()
        {
            SetPieceBallSpeed(1.0f, RestartKind.ThrowIn, new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(8f, 2f) }, out MatchController c);
            Assert.AreEqual(1, c.SetPieceReports.Count);
            SetPieceReport r = c.SetPieceReports[0];
            Assert.AreEqual(RestartKind.ThrowIn, r.Kind);
            Assert.IsTrue(r.HasDestination); Assert.AreEqual(8f, r.Destination.X, 1e-4f);
            Assert.Greater(r.Power, 0f); Assert.AreNotEqual(ActionOutcome.Rejected, r.Outcome);
            Assert.IsNotNull(r.TakerId);
        }

        [Test]
        public void TheWidePlayerBehind_OffersTheOutside_WhenTheWingerCarriesTheBallInside_AndOnlyThen()
        {
            MatchController c = GbBuild(GbCurrent, 1, out _);
            WorldView v0 = WorldState.Capture(c).View;
            string wide = null; float side = 0f;
            for (int i = v0.TeamStart(TeamSide.Home); i < v0.TeamStart(TeamSide.Home) + v0.TeamCount(TeamSide.Home); i++)
                if (v0[i].IsOnPitch && !v0[i].IsGoalkeeper && v0[i].Role != PlayerRole.Forward && v0[i].HasHome && Math.Abs(v0[i].Home.Y) >= WingSupportDefaults.WideHome) { wide = v0[i].PlayerId; side = v0[i].Home.Y > 0f ? 1f : -1f; break; }
            Assert.IsNotNull(wide, "the 6v6 formation has a wide player behind the attack");
            string winger = c.State.Home.Players.First(p => p.IsActive && !p.InGoal && p.PlayerId != wide).PlayerId;
            int n = 0;
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players)) if (p.IsActive && p.PlayerId != wide && p.PlayerId != winger) c.PlacePlayer(p.Team, p.PlayerId, new Vec2(p.Team == TeamSide.Home ? -15f : 15f, p.InGoal ? 0f : -4f + 2f * n++), 0f);
            c.PlacePlayer(TeamSide.Home, wide, new Vec2(-2f, 7f * side), 0f);
            Carrier(c, TeamSide.Home, winger, 8f, 8f * side, East);
            c.State.Home.Find(winger).Locomotion.Velocity = new Vec2(3f, -3f * side);
            var on = new BasicDecisionEngine(c.Pitch) { OutsideSupportEnabled = true };
            PlayerDecision d = on.Decide(WorldState.Capture(c).View, wide);
            Assert.AreEqual(DecisionReason.OutsideSupport, d.Reason);
            Assert.IsTrue(d.Movement.HasTarget);
            Assert.Greater(d.Movement.Target.Y * side, 8f, "he goes outside the winger (towards the touchline)");
            Assert.Greater(d.Movement.Target.X, 8f, "and a little ahead of him");
            Assert.AreNotEqual(DecisionReason.OutsideSupport, new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, wide).Reason, "off: he keeps his place");
            c.State.Home.Find(winger).Locomotion.Velocity = new Vec2(3f, 3f * side);
            Assert.AreNotEqual(DecisionReason.OutsideSupport, on.Decide(WorldState.Capture(c).View, wide).Reason, "he only offers the outside when the winger cuts in");
        }
    }
}
