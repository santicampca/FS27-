using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 32: the player a person controls does not jump about and obeys his own switch (found by the GameplayBench: the switch button was overridden within a frame while defending, and the marker moved between two defenders every second or two).</summary>
    public partial class AiInfrastructureTests
    {
        /// <summary>Plays until the rival has the ball, switches player by hand, plays <paramref name="seconds"/> more and says whether the person's player is still his. Null when the rival never had the ball.</summary>
        private static bool? ManualSwitchHolds(int seed, bool stability, float seconds)
        {
            MatchController c = GbBuild(stability ? GbCurrent : GbReference, seed, out BasicDecisionDriver driver);
            c.SetControl(TeamSide.Home, ControlSource.Human); driver.SelectionTuning.Enabled = stability;
            for (int step = 0; step < (int)(120f / Dt); step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (step < 300 || c.State.Phase != MatchPhase.Playing || !c.State.Ball.HasPossession || c.State.Ball.PossessingTeam != TeamSide.Away) continue;
                string chosen = c.CycleSelection(TeamSide.Home);
                for (int k = 0; k < (int)(seconds / Dt); k++) { driver.Step(c); c.Advance(Dt); }
                return c.ControlledPlayerId(TeamSide.Home) == chosen;
            }
            return null;
        }

        [Test]
        public void ASwitchByHand_WhileDefending_IsKept_ForTheHold()
        {
            int trials = 0, keptWith = 0, keptWithout = 0;
            foreach (int seed in Enumerable.Range(1, 8))
            {
                bool? with = ManualSwitchHolds(seed, true, 1.0f), without = ManualSwitchHolds(seed, false, 1.0f);
                if (with == null || without == null) continue;
                trials++; if (with.Value) keptWith++; if (without.Value) keptWithout++;
            }
            Assert.GreaterOrEqual(trials, 6, "the rival must get the ball in most of the matches");
            Assert.AreEqual(trials, keptWith, "with the stability the person's own switch holds");
            Assert.Less(keptWithout, trials / 2 + 1, "(the defect this fixes) without it the automatic choice takes the marker back within a second in most cases");
        }

        [Test]
        public void TheManualHold_Ends_AndTheAutomaticChoiceTakesOver()
        {
            // after the hold the marker may move again: the person is not locked to a player who is far from the action for the rest of the match
            int moved = 0, trials = 0;
            foreach (int seed in Enumerable.Range(1, 6))
            {
                MatchController c = GbBuild(GbCurrent, seed, out BasicDecisionDriver driver);
                c.SetControl(TeamSide.Home, ControlSource.Human);
                for (int step = 0; step < (int)(120f / Dt); step++)
                {
                    driver.Step(c); c.Advance(Dt);
                    if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                    if (step < 300 || c.State.Phase != MatchPhase.Playing || !c.State.Ball.HasPossession || c.State.Ball.PossessingTeam != TeamSide.Away) continue;
                    string chosen = c.CycleSelection(TeamSide.Home); trials++;
                    for (int k = 0; k < (int)((driver.SelectionTuning.ManualHoldSeconds + 8f) / Dt); k++) { driver.Step(c); c.Advance(Dt); if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); }
                    if (c.ControlledPlayerId(TeamSide.Home) != chosen) moved++;
                    break;
                }
            }
            Assert.GreaterOrEqual(trials, 4);
            Assert.Greater(moved, 0, "after the hold the automatic choice can move the marker again");
        }

        [Test]
        public void TheMarker_DoesNotFlickerBetweenTwoPlayers()
        {
            int flickersWith = 0, flickersWithout = 0;
            foreach (int seed in Enumerable.Range(1, 4))
                foreach (bool stability in new[] { true, false })
                {
                    MatchController c = GbBuild(stability ? GbCurrent : GbReference, seed, out BasicDecisionDriver driver);
                    c.SetControl(TeamSide.Home, ControlSource.Human); driver.SelectionTuning.Enabled = stability;
                    string last = null, beforeLast = null; int lastAt = -1000, prevAt = -1000, flicker = 0;
                    for (int step = 0; step < (int)(120f / Dt) && c.State.Phase != MatchPhase.Finished; step++)
                    {
                        driver.Step(c); c.Advance(Dt);
                        if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                        if (c.State.Phase != MatchPhase.Playing) continue;
                        string id = c.ControlledPlayerId(TeamSide.Home);
                        if (last != null && id != last) { if (beforeLast != null && id == beforeLast && step - prevAt <= 90) flicker++; beforeLast = last; prevAt = lastAt; lastAt = step; }
                        last = id;
                    }
                    if (stability) flickersWith += flicker; else flickersWithout += flicker;
                }
            Assert.LessOrEqual(flickersWith, 2, "with the stability the marker almost never goes A-B-A within a second and a half");
            Assert.Greater(flickersWithout, 10, "(the defect this fixes) it did, several times a minute");
        }

        [Test]
        public void TheStability_OffIsThePhase8Behaviour_AndALoneAutomaticChoiceIsNotAManualOne()
        {
            MatchController c = GbBuild(GbCurrent, 1, out BasicDecisionDriver driver);
            c.SetControl(TeamSide.Home, ControlSource.Human);
            Assert.AreEqual(float.NegativeInfinity, c.ManualSelectionAt(TeamSide.Home), "nobody has switched by hand yet");
            for (int i = 0; i < 600; i++) { driver.Step(c); c.Advance(Dt); if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); }
            Assert.AreEqual(float.NegativeInfinity, c.ManualSelectionAt(TeamSide.Home), "the automatic choice does not count as a manual one");
            c.CycleSelection(TeamSide.Home);
            Assert.AreEqual(c.State.MatchTimeSeconds, c.ManualSelectionAt(TeamSide.Home), 1e-4f, "a switch by hand does");
            Assert.IsTrue(new SelectionStabilityTuning().Enabled);
            Assert.Greater(SelectionStabilityDefaults.ManualHoldSeconds, 0f); Assert.Greater(SelectionStabilityDefaults.DwellSeconds, 0f); Assert.Greater(SelectionStabilityDefaults.SwitchMarginMeters, 0f);
        }
    }
}
