using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>The deterministic match core: settings, periods, draws, extra time, shoot-out, walls, boundaries, events, substitutions, discipline, result.</summary>
    public class MatchEngineTests
    {
        private const float Step = 1f / 60f;
        private static readonly BallObservation Centre = new BallObservation(new Vec3(0f, 0.2f, 0f), 0.2f);
        private static readonly BallObservation HomeGoal = new BallObservation(new Vec3(20.6f, 0.2f, 0f), 0.2f);

        // ---------- helpers ----------

        private static SquadMember[] Starters(string prefix)
        {
            var list = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) };
            for (int i = 1; i < 6; i++) list.Add(new SquadMember(prefix + "s" + i, false, 100f));
            return list.ToArray();
        }

        private static SquadMember[] Bench(string prefix)
        {
            return new[]
            {
                new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f),
                new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f)
            };
        }

        private static MatchSquad Squad(string prefix) { return new MatchSquad(Starters(prefix), Bench(prefix)); }

        private static MatchController New(MatchSettings s, bool squads = true)
        {
            return squads ? new MatchController(new FieldDimensions(), s, Squad("h"), Squad("a")) : new MatchController(new FieldDimensions(), s);
        }

        private static MatchSettings Arena(CompetitionMatchType t = CompetitionMatchType.Normal) { var s = MatchSettings.CreateArena(t); s.ExtraTimePeriodSeconds = 60f; return s; }
        private static MatchSettings Classic(CompetitionMatchType t = CompetitionMatchType.Normal) { var s = MatchSettings.CreateClassic(t); s.ExtraTimePeriodSeconds = 60f; return s; }
        /// <summary>The factory defaults as they are, without the shortened extra time the other helpers use.</summary>

        private static void Kickoff(MatchController c)
        {
            if (c.State.Phase == MatchPhase.NotStarted) c.Start();
            Assert.IsTrue(c.ConfirmRestartReady());
        }

        /// <summary>Plays with the ball at rest until the current period's clock runs out (the ball never reaches a goal or a line).</summary>
        private static void RunPeriod(MatchController c)
        {
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            int guard = 0;
            while (c.State.Phase == MatchPhase.Playing && guard++ < 5000) c.Advance(1f);
            Assert.Less(guard, 5000);
        }

        /// <summary>Takes the pending kick-off and, when there is a break, lets it end.</summary>
        private static void Resume(MatchController c)
        {
            int guard = 0;
            while (c.State.Phase == MatchPhase.Break && guard++ < 100) c.Advance(1f);
            Assert.IsTrue(c.ConfirmRestartReady(), "expected a kick-off to be pending, phase " + c.State.Phase + " period " + c.State.Period);
        }

        private static void CheckInvariant(MatchState s)
        {
            Assert.AreEqual(s.Phase == MatchPhase.NotStarted, s.Period == MatchPeriod.PreMatch, "NotStarted <=> PreMatch");
            Assert.AreEqual(s.Phase == MatchPhase.Break, s.Period == MatchPeriod.HalfTime || s.Period == MatchPeriod.ExtraTimeBreak, "Break <=> a break period");
            Assert.AreEqual(s.Phase == MatchPhase.Shootout, s.Period == MatchPeriod.PenaltyShootout, "Shootout <=> PenaltyShootout");
            Assert.AreEqual(s.Phase == MatchPhase.Finished, s.Period == MatchPeriod.FullTime, "Finished <=> FullTime");
        }

        private static void RunWholeRegulation(MatchController c)
        {
            Kickoff(c);
            RunPeriod(c);
            while (c.State.Period == MatchPeriod.HalfTime || c.State.Period == MatchPeriod.SecondHalf || c.State.Phase == MatchPhase.WaitingForKickoff)
            {
                CheckInvariant(c.State);
                if (c.State.Phase == MatchPhase.Break) { c.Advance(1f); continue; }
                if (c.State.Phase == MatchPhase.WaitingForKickoff) { c.ConfirmRestartReady(); }
                if (c.State.Phase == MatchPhase.Playing) RunPeriod(c);
                if (c.State.Period != MatchPeriod.HalfTime && c.State.Period != MatchPeriod.SecondHalf) break;
            }
        }

        // ================= settings =================

        [Test]
        public void ArenaSettings_AreTwoPeriodsOfSixMinutes_WithTenPercentRecovery()
        {
            MatchSettings s = MatchSettings.CreateArena();
            Assert.AreEqual(GameMode.Arena, s.Mode);
            Assert.AreEqual(2, s.RegulationPeriods);
            Assert.AreEqual(360f, s.RegulationPeriodSeconds);
            Assert.AreEqual(0.10f, s.SideSwitchStaminaRecovery);
            Assert.AreEqual(3, s.EffectiveSubstitutionLimit);
        }

        [Test]
        public void ClassicSettings_AreTwoPeriodsOfEightMinutes_WithTwentyFivePercentRecovery()
        {
            MatchSettings s = MatchSettings.CreateClassic();
            Assert.AreEqual(GameMode.Classic, s.Mode);
            Assert.AreEqual(480f, s.RegulationPeriodSeconds);
            Assert.AreEqual(0.25f, s.HalfTimeStaminaRecovery);
        }

        [Test]
        public void Settings_AreConfigurable_AndTheDefaultsLiveInOnePlace()
        {
            var s = MatchSettings.CreateArena();
            s.PeriodSeconds = 30f; s.SideSwitchStaminaRecovery = 0.2f;
            Assert.AreEqual(30f, s.RegulationPeriodSeconds);
            Assert.IsTrue(s.IsValid(out _));
            Assert.AreEqual(MatchDefaults.ArenaPeriodSeconds, MatchSettings.CreateArena().PeriodSeconds);
        }

        [Test]
        public void Settings_RejectBadValues_AndNeverAllowMoreThanThreeSubstitutions()
        {
            var s = MatchSettings.CreateClassic();
            s.SubstitutionLimit = 4;
            Assert.IsFalse(s.IsValid(out string why)); StringAssert.Contains("SubstitutionLimit", why);
            Assert.Throws<ArgumentException>(() => new MatchController(new FieldDimensions(), s));
            s.SubstitutionLimit = 3; s.HalfTimeStaminaRecovery = 1.5f;
            Assert.IsFalse(s.IsValid(out _));
            Assert.AreEqual(3, new MatchSettings { SubstitutionLimit = 99 }.EffectiveSubstitutionLimit, "the effective limit is capped");
        }

        [Test]
        public void ExtraTime_UsesAThirdOfARegulationPeriod_AndTheModesOwnIntervalRecovery()
        {
            MatchSettings arena = MatchSettings.CreateArena(CompetitionMatchType.Knockout), classic = MatchSettings.CreateClassic(CompetitionMatchType.Knockout);
            Assert.AreEqual(arena.RegulationPeriodSeconds / 3f, arena.ExtraTimePeriodSeconds, 1e-3f);
            Assert.AreEqual(120f, arena.ExtraTimePeriodSeconds, 1e-3f);
            Assert.AreEqual(classic.RegulationPeriodSeconds / 3f, classic.ExtraTimePeriodSeconds, 1e-3f);
            Assert.AreEqual(arena.SideSwitchStaminaRecovery, arena.ExtraTimeStaminaRecovery);
            Assert.AreEqual(arena.SideSwitchStaminaRecovery, arena.PreExtraTimeStaminaRecovery);
            Assert.AreEqual(classic.HalfTimeStaminaRecovery, classic.ExtraTimeStaminaRecovery);
            Assert.AreEqual(classic.HalfTimeStaminaRecovery, classic.PreExtraTimeStaminaRecovery);
        }

        [Test]
        public void ExtraTime_RecoversStamina_BeforeItAndAtItsBreak()
        {
            foreach (var s in new[] { MatchSettings.CreateClassic(CompetitionMatchType.Knockout), MatchSettings.CreateArena(CompetitionMatchType.Knockout) })
            {
                var c = New(s);
                RunWholeRegulation(c);
                MatchPlayer p = c.State.Home.Find("hs1");
                float tank = p.Runtime.StaminaCapacity, step = tank * (s.Mode == GameMode.Classic ? 0.25f : 0.10f);
                // the pre-extra-time recovery was applied when extra time was entered
                Assert.AreEqual(MatchPeriod.ExtraTimeFirstHalf, c.State.Period);
                p.Runtime.Stamina = tank * 0.5f;
                c.ConfirmRestartReady(); RunPeriod(c);          // end of the first extra-time half: the break recovery applies
                Assert.AreEqual(tank * 0.5f + step, p.Runtime.Stamina, 1e-3f, s.Mode.ToString());
            }
        }

        [Test]
        public void ClassicHasATenSecondBreak_ArenaHasNone()
        {
            Assert.AreEqual(10f, MatchSettings.CreateClassic().HalftimeSeconds);
            Assert.AreEqual(0f, MatchSettings.CreateArena().HalftimeSeconds);
            var c = New(MatchSettings.CreateClassic());
            Kickoff(c); RunPeriod(c);
            c.Advance(9f); Assert.AreEqual(MatchPhase.Break, c.State.Phase);
            c.Advance(1.5f); Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
        }

        [Test]
        public void ModeAndCompetition_AreIndependentSettings()
        {
            foreach (GameMode m in Enum.GetValues(typeof(GameMode)))
                foreach (CompetitionMatchType t in Enum.GetValues(typeof(CompetitionMatchType)))
                {
                    var s = new MatchSettings { Mode = m, CompetitionType = t };
                    Assert.AreEqual(m, s.Mode); Assert.AreEqual(t, s.CompetitionType);
                }
        }

        [Test]
        public void Squad_NeedsSixStarters_FourSubstitutes_AndAtLeastOneBenchKeeper()
        {
            Assert.IsTrue(Squad("x").IsValid(out _));
            Assert.IsFalse(new MatchSquad(Starters("x").Take(5), Bench("x")).IsValid(out _));
            Assert.IsFalse(new MatchSquad(Starters("x"), Bench("x").Take(3)).IsValid(out _));
            var noKeeper = Bench("x"); noKeeper[0] = new SquadMember("xb0", false);
            Assert.IsFalse(new MatchSquad(Starters("x"), noKeeper).IsValid(out string why)); StringAssert.Contains("goalkeeper", why);
            Assert.Throws<ArgumentException>(() => new MatchController(new FieldDimensions(), Classic(), new MatchSquad(Starters("x"), noKeeper), Squad("a")));
        }

        // ================= match flow =================

        [Test]
        public void Match_IsCreatedInPreMatch_WithAuthoritativeState()
        {
            var c = New(Classic());
            MatchState s = c.State;
            Assert.AreEqual(MatchPhase.NotStarted, s.Phase);
            Assert.AreEqual(MatchPeriod.PreMatch, s.Period);
            Assert.AreEqual(GameMode.Classic, s.Mode);
            Assert.AreEqual(CompetitionMatchType.Normal, s.CompetitionType);
            Assert.AreEqual(0, s.Score.Home + s.Score.Away);
            Assert.AreEqual(BallMode.Dead, s.Ball.Mode);
            Assert.AreEqual(6, s.Home.ActivePlayerCount);
            Assert.AreEqual(10, s.Home.Players.Count);
            Assert.IsFalse(s.HasResult);
            Assert.AreEqual("match", s.MatchId);
        }

        [Test]
        public void Match_PreMatchToKickoffToFirstHalf()
        {
            var c = New(Classic());
            c.Start();
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(MatchPeriod.FirstHalf, c.State.Period);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.MatchStarted));
            Assert.IsFalse(c.Events.Has(GameplayEventKind.PeriodStarted), "the period starts at the kick-off, not before");
            c.ConfirmRestartReady();
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.PeriodStarted));
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
        }

        [Test]
        public void Clock_RunsOnlyWhileTheBallIsLive_AndIsCoreTime()
        {
            var c = New(Classic());
            c.Start();
            c.Advance(5f);
            Assert.AreEqual(0f, c.State.MatchTimeSeconds, "waiting for the kick-off: no time passes");
            c.ConfirmRestartReady();
            c.Advance(2f);
            Assert.AreEqual(2f, c.State.MatchTimeSeconds, 1e-4f);
            Assert.AreEqual(2f, c.State.Clock.Elapsed, 1e-4f);
            c.Pause();
            c.Advance(10f);
            Assert.AreEqual(2f, c.State.MatchTimeSeconds, 1e-4f, "paused");
            c.Resume();
            c.Advance(1f);
            Assert.AreEqual(3f, c.State.MatchTimeSeconds, 1e-4f);
        }

        [Test]
        public void Classic_FirstPeriodEnds_WithHalfTime_AndRecoversTwentyFivePercent()
        {
            var c = New(Classic());
            Kickoff(c);
            MatchPlayer p = c.State.Home.Find("hs1"); p.Runtime.Stamina = 50f;
            MatchPlayer q = c.State.Away.Find("as2"); q.Runtime.Stamina = 90f;
            MatchPlayer bench = c.State.Home.Find("hb1"); bench.Runtime.Stamina = 10f;
            RunPeriod(c);
            Assert.AreEqual(MatchPhase.Break, c.State.Phase);
            Assert.AreEqual(MatchPeriod.HalfTime, c.State.Period);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.HalfTimeStarted));
            Assert.AreEqual(75f, p.Runtime.Stamina, 1e-3f);
            Assert.AreEqual(100f, q.Runtime.Stamina, "never above the maximum");
            Assert.AreEqual(10f, bench.Runtime.Stamina, "only players on the pitch recover");
            CheckInvariant(c.State);
        }

        [Test]
        public void Arena_FirstPeriodEnds_WithASideSwitch_NoBreak_AndRecoversTenPercent()
        {
            var c = New(Arena());
            Kickoff(c);
            MatchPlayer p = c.State.Home.Find("hs1"); p.Runtime.Stamina = 50f;
            bool before = c.State.HomeAttacksPositiveX;
            bool sawBreak = false;
            c.PhaseChanged += (a, b) => { if (b == MatchPhase.Break) sawBreak = true; };
            RunPeriod(c);
            Assert.IsFalse(sawBreak, "Arena has no traditional break");
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(MatchPeriod.SecondHalf, c.State.Period);
            Assert.AreNotEqual(before, c.State.HomeAttacksPositiveX, "sides switched");
            Assert.IsTrue(c.Events.Has(GameplayEventKind.SidesSwitched));
            Assert.IsFalse(c.Events.Has(GameplayEventKind.HalfTimeStarted));
            Assert.AreEqual(60f, p.Runtime.Stamina, 1e-3f);
            CheckInvariant(c.State);
        }

        [Test]
        public void Classic_SecondHalf_FollowsHalfTime_WithTheOtherTeamKickingOff()
        {
            var c = New(Classic());
            Kickoff(c);
            RunPeriod(c);
            Resume(c);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(MatchPeriod.SecondHalf, c.State.Period);
            Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.SecondHalfStarted));
            Assert.AreEqual(0f, c.State.Clock.Elapsed, 1e-4f, "the period clock restarts");
            Assert.AreEqual(480f, c.State.MatchTimeSeconds, 1e-2f, "the match time keeps counting");
        }

        [Test]
        public void HalfTimeLength_IsConfigurable_AndUsesCoreTime()
        {
            var s = Classic(); s.HalftimeSeconds = 5f;
            var c = New(s);
            Kickoff(c); RunPeriod(c);
            c.Advance(4f);
            Assert.AreEqual(MatchPhase.Break, c.State.Phase);
            c.Advance(1.5f);
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
        }

        [Test]
        public void Side_Switch_ChangesWhoScoresAtWhichEnd()
        {
            var c = New(Classic());
            Kickoff(c); RunPeriod(c); Resume(c);
            c.Tick(0.1f, HomeGoal);          // +X end: after the switch Away attacks it
            Assert.AreEqual(1, c.State.Score.Away);
        }

        [Test]
        public void Match_PlaysToFullTime_Classic_AndArena()
        {
            foreach (var s in new[] { Classic(), Arena() })
            {
                var c = New(s);
                RunWholeRegulation(c);
                Assert.AreEqual(MatchPhase.Finished, c.State.Phase, s.Mode.ToString());
                Assert.AreEqual(MatchPeriod.FullTime, c.State.Period);
                Assert.IsTrue(c.State.HasResult);
                Assert.IsTrue(c.Events.Has(GameplayEventKind.MatchEnded));
                CheckInvariant(c.State);
            }
        }

        // ================= draws, extra time, shoot-out =================

        [Test]
        public void NormalMatches_EndInADraw_InBothModes_WithoutExtraTimeOrShootout()
        {
            foreach (var s in new[] { Classic(), Arena() })
            {
                var c = New(s);
                var phases = new List<MatchPhase>();
                c.PhaseChanged += (a, b) => phases.Add(b);
                RunWholeRegulation(c);
                Assert.IsTrue(c.State.Result.IsDraw, s.Mode.ToString());
                Assert.AreEqual(MatchTerminationReason.Regulation, c.State.Result.Reason);
                Assert.IsFalse(c.State.ExtraTimeReached);
                Assert.IsNull(c.State.Shootout);
                CollectionAssert.DoesNotContain(phases, MatchPhase.Shootout);
                Assert.IsFalse(c.Events.Has(GameplayEventKind.ExtraTimeStarted));
                Assert.IsFalse(c.Events.Has(GameplayEventKind.PenaltyShootoutStarted));
            }
        }

        [Test]
        public void NormalMatch_WithAWinner_Finishes_WithoutDraw()
        {
            var c = New(Classic());
            Kickoff(c);
            c.Tick(0.1f, HomeGoal);
            c.Advance(3.1f); Resume(c);
            RunPeriod(c); Resume(c); RunPeriod(c);
            Assert.IsFalse(c.State.Result.IsDraw);
            Assert.AreEqual(TeamSide.Home, c.State.Result.Winner);
            Assert.AreEqual(1, c.State.Result.HomeGoals);
        }

        [Test]
        public void KnockoutDraw_GoesToExtraTime_InBothModes_AndNeverBecauseOfTheMode()
        {
            foreach (var s in new[] { Classic(CompetitionMatchType.Knockout), Arena(CompetitionMatchType.Knockout) })
            {
                var c = New(s);
                RunWholeRegulation(c);
                Assert.AreEqual(MatchPeriod.ExtraTimeFirstHalf, c.State.Period, s.Mode.ToString());
                Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
                Assert.IsTrue(c.State.ExtraTimeReached);
                Assert.IsFalse(c.State.HasResult);
                c.ConfirmRestartReady();
                Assert.IsTrue(c.Events.Has(GameplayEventKind.ExtraTimeStarted));
                Assert.AreEqual(60f, c.State.Clock.Duration, "the extra-time length comes from the settings");
            }
        }

        [Test]
        public void KnockoutWithAWinner_DoesNotPlayExtraTime()
        {
            var c = New(Classic(CompetitionMatchType.Knockout));
            Kickoff(c);
            c.Tick(0.1f, HomeGoal); c.Advance(3.1f); Resume(c);
            RunPeriod(c); Resume(c); RunPeriod(c);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.IsFalse(c.State.ExtraTimeReached);
            Assert.AreEqual(MatchTerminationReason.Regulation, c.State.Result.Reason);
        }

        [Test]
        public void ExtraTime_Classic_HasABreak_Arena_DoesNot_ThenALevelMatchGoesToTheShootout()
        {
            foreach (var s in new[] { Classic(CompetitionMatchType.Knockout), Arena(CompetitionMatchType.Knockout) })
            {
                var c = New(s);
                RunWholeRegulation(c);
                Resume(c); RunPeriod(c);
                if (s.Mode == GameMode.Classic)
                {
                    Assert.AreEqual(MatchPeriod.ExtraTimeBreak, c.State.Period);
                    Assert.AreEqual(MatchPhase.Break, c.State.Phase);
                    CheckInvariant(c.State);
                }
                else
                {
                    Assert.AreEqual(MatchPeriod.ExtraTimeSecondHalf, c.State.Period);
                    Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
                }
                Resume(c);
                Assert.AreEqual(MatchPeriod.ExtraTimeSecondHalf, c.State.Period);
                RunPeriod(c);
                Assert.AreEqual(MatchPhase.Shootout, c.State.Phase, s.Mode.ToString());
                Assert.AreEqual(MatchPeriod.PenaltyShootout, c.State.Period);
                Assert.IsTrue(c.Events.Has(GameplayEventKind.ExtraTimeEnded));
                Assert.IsTrue(c.Events.Has(GameplayEventKind.PenaltyShootoutStarted));
                CheckInvariant(c.State);
            }
        }

        [Test]
        public void ExtraTime_DecidedByAGoal_Finishes_WithoutShootout()
        {
            var c = New(Arena(CompetitionMatchType.Knockout));
            RunWholeRegulation(c);
            c.ConfirmRestartReady();
            c.Tick(0.1f, HomeGoal);          // sides switched once in regulation: Away attacks +X now
            Assert.AreEqual(1, c.State.Score.Away);
            c.Advance(3.1f);
            Resume(c); RunPeriod(c);          // the clock was finished? continue to the end of extra time
            while (c.State.Phase != MatchPhase.Finished && c.State.Phase != MatchPhase.Shootout)
            {
                if (c.State.Phase == MatchPhase.WaitingForKickoff) c.ConfirmRestartReady();
                if (c.State.Phase == MatchPhase.Playing) RunPeriod(c);
            }
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(MatchTerminationReason.ExtraTime, c.State.Result.Reason);
            Assert.AreEqual(TeamSide.Away, c.State.Result.Winner);
            Assert.IsNull(c.State.Shootout);
        }

        [Test]
        public void Shootout_ReachedFromAKnockout_DecidesAWinner_AndDoesNotChangeTheScore()
        {
            var c = New(Classic(CompetitionMatchType.Knockout));
            RunWholeRegulation(c);
            Resume(c); RunPeriod(c); Resume(c); RunPeriod(c);
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase);
            Assert.IsFalse(c.RecordShootoutKick(true) == false);
            int guard = 0;
            while (c.State.Phase == MatchPhase.Shootout && guard++ < 50) c.RecordShootoutKick(c.State.Shootout.NextTeam == TeamSide.Home);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(MatchTerminationReason.PenaltyShootout, c.State.Result.Reason);
            Assert.IsFalse(c.State.Result.IsDraw);
            Assert.AreEqual(TeamSide.Home, c.State.Result.Winner);
            Assert.AreEqual(0, c.State.Result.HomeGoals); Assert.AreEqual(0, c.State.Result.AwayGoals);
            Assert.Greater(c.State.Result.ShootoutHome, c.State.Result.ShootoutAway);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.PenaltyShootoutEnded));
            Assert.IsFalse(c.RecordShootoutKick(true), "nothing to record once finished");
        }

        [Test]
        public void ExtraTimeDisabled_GoesStraightToTheShootout_AndShootoutDisabledEndsTheDraw()
        {
            var s = Classic(CompetitionMatchType.Knockout); s.ExtraTimeEnabled = false;
            var c = New(s); RunWholeRegulation(c);
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase);
            s = Classic(CompetitionMatchType.Knockout); s.ExtraTimeEnabled = false; s.ShootoutEnabled = false;
            c = New(s); RunWholeRegulation(c);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.IsTrue(c.State.Result.IsDraw);
        }

        [Test]
        public void CompetitionResolver_DependsOnTheCompetition_NotOnTheMode()
        {
            var s = new MatchSettings();
            Assert.AreEqual(StageOutcome.Finish, CompetitionResolver.AfterRegulation(1, 1, CompetitionMatchType.Normal, s));
            Assert.AreEqual(StageOutcome.Finish, CompetitionResolver.AfterRegulation(2, 1, CompetitionMatchType.Knockout, s));
            Assert.AreEqual(StageOutcome.ExtraTime, CompetitionResolver.AfterRegulation(1, 1, CompetitionMatchType.Knockout, s));
            Assert.AreEqual(StageOutcome.PenaltyShootout, CompetitionResolver.AfterExtraTime(1, 1, s));
            Assert.AreEqual(StageOutcome.Finish, CompetitionResolver.AfterExtraTime(2, 1, s));
            foreach (MethodInfo m in typeof(CompetitionResolver).GetMethods(BindingFlags.Public | BindingFlags.Static))
                Assert.IsFalse(m.GetParameters().Any(p => p.ParameterType == typeof(GameMode)), m.Name + " must not know the game mode");
            foreach (MethodInfo m in typeof(IGameModeRules).GetMethods())
                Assert.IsFalse(m.GetParameters().Any(p => p.ParameterType == typeof(CompetitionMatchType)), m.Name + " must not know the competition");
        }

        // ================= penalty shoot-out state =================

        private static void Kick(PenaltyShootoutState s, params bool[] results) { foreach (bool r in results) Assert.IsTrue(s.RecordKick(r)); }

        [Test]
        public void ShootoutState_AlternatesTeams_StartingWithTheConfiguredOne()
        {
            var s = new PenaltyShootoutState(5, true, TeamSide.Away);
            Assert.AreEqual(TeamSide.Away, s.NextTeam);
            Kick(s, true);
            Assert.AreEqual(TeamSide.Home, s.NextTeam);
            Kick(s, false);
            Assert.AreEqual(TeamSide.Away, s.NextTeam);
            Assert.AreEqual(1, s.Round);
            Assert.AreEqual(1, s.Goals(TeamSide.Away)); Assert.AreEqual(1, s.Misses(TeamSide.Home));
        }

        [Test]
        public void ShootoutState_EndsEarly_WhenOneTeamCannotBeCaught()
        {
            var s = new PenaltyShootoutState(5, true, TeamSide.Home);
            Kick(s, true, false, true, false, true);       // H A H A H
            Assert.IsFalse(s.IsDecided);
            Kick(s, false);                                 // A: Home 3, Away 0 with two kicks left -> decided
            Assert.IsTrue(s.IsDecided);
            Assert.AreEqual(TeamSide.Home, s.Winner);
            Assert.AreEqual(6, s.Kicks.Count);
            Assert.IsFalse(s.RecordKick(true), "decided: no more kicks");
        }

        [Test]
        public void ShootoutState_LevelAfterTheRegularKicks_GoesToSuddenDeath_AndEndsWithTheFirstDifference()
        {
            var s = new PenaltyShootoutState(3, true, TeamSide.Home);
            Kick(s, true, true, true, true, true, true);    // 3-3
            Assert.IsFalse(s.IsDecided);
            Assert.IsTrue(s.InSuddenDeath);
            Kick(s, true, true);                            // both score: still level
            Assert.IsFalse(s.IsDecided);
            Kick(s, false, true);                           // Home misses, Away scores
            Assert.IsTrue(s.IsDecided);
            Assert.AreEqual(TeamSide.Away, s.Winner);
        }

        [Test]
        public void ShootoutState_AWinnerNeedsBothTeamsToHaveKickedTheSameNumber_InSuddenDeath()
        {
            var s = new PenaltyShootoutState(1, true, TeamSide.Home);
            Kick(s, true, true);
            Assert.IsFalse(s.IsDecided);
            Kick(s, true);
            Assert.IsFalse(s.IsDecided, "the other team still has its kick of the round");
            Kick(s, false);
            Assert.IsTrue(s.IsDecided); Assert.AreEqual(TeamSide.Home, s.Winner);
        }

        [Test]
        public void ShootoutState_WithoutSuddenDeath_EndsLevelWithNoWinner()
        {
            var s = new PenaltyShootoutState(2, false, TeamSide.Home);
            Kick(s, true, true, false, false);
            Assert.IsTrue(s.IsDecided); Assert.IsFalse(s.HasWinner);
        }

        // ---- the shoot-out of the match: 3 kicks per team, then sudden death, all from the settings ----

        private static MatchController ReachShootout(MatchSettings s)
        {
            var c = New(s);
            RunWholeRegulation(c);
            Resume(c); RunPeriod(c); Resume(c); RunPeriod(c);
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase);
            return c;
        }

        [Test]
        public void Shootout_IsThreeKicksPerTeam_ByDefault_InEveryMode_FromOnePlace()
        {
            Assert.AreEqual(3, MatchDefaults.ShootoutKicksPerTeam);
            Assert.AreEqual(3, new MatchSettings().ShootoutKicksPerTeam);
            Assert.AreEqual(3, MatchSettings.CreateArena().ShootoutKicksPerTeam);
            Assert.AreEqual(3, MatchSettings.CreateClassic().ShootoutKicksPerTeam);
            foreach (var s in new[] { Classic(CompetitionMatchType.Knockout), Arena(CompetitionMatchType.Knockout) })
                Assert.AreEqual(3, ReachShootout(s).State.Shootout.KicksPerTeam, s.Mode.ToString());
            // the number is not repeated in the engine: it only reads the setting
            string engine = File.ReadAllText(Path.Combine(MatchSourceDirectory(), "MatchController.cs"));
            Assert.IsTrue(engine.Contains("settings.ShootoutKicksPerTeam"));
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(engine, @"new PenaltyShootoutState\(\s*\d"), "no literal kick count in the engine");
        }

        [Test]
        public void Shootout_LevelAfterThreeKicksEach_GoesToSuddenDeath()
        {
            var c = ReachShootout(Classic(CompetitionMatchType.Knockout));
            for (int i = 0; i < 6; i++) Assert.IsTrue(c.RecordShootoutKick(true));     // 3-3
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase, "still level: the shoot-out goes on");
            Assert.IsTrue(c.State.Shootout.InSuddenDeath);
            Assert.IsFalse(c.State.Shootout.IsDecided);
            c.RecordShootoutKick(true); c.RecordShootoutKick(true);                     // 4-4: level again
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase);
            c.RecordShootoutKick(false); c.RecordShootoutKick(true);                    // Home misses, Away scores
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(TeamSide.Away, c.State.Result.Winner);
            Assert.AreEqual(MatchTerminationReason.PenaltyShootout, c.State.Result.Reason);
            Assert.AreEqual(4, c.State.Result.ShootoutHome); Assert.AreEqual(5, c.State.Result.ShootoutAway);
        }

        [Test]
        public void Shootout_ThreeKicksEach_EndsEarly_WhenOneTeamCanNoLongerBeCaught()
        {
            var s = new PenaltyShootoutState(MatchDefaults.ShootoutKicksPerTeam, true, TeamSide.Home);
            Kick(s, true, false, true);                     // H A H: 2-0, Away has 2 kicks left
            Assert.IsFalse(s.IsDecided);
            Kick(s, false);                                 // A misses: 2-0 with one kick left each... Away can reach 1 at most
            Assert.IsTrue(s.IsDecided, "2-0 with one Away kick left is out of reach");
            Assert.AreEqual(TeamSide.Home, s.Winner);
            Assert.AreEqual(4, s.Kicks.Count);
        }

        [Test]
        public void Shootout_ThreeKicksEach_IsNotDecidedWhileTheOtherTeamCanStillCatchUp()
        {
            var s = new PenaltyShootoutState(3, true, TeamSide.Home);
            Kick(s, true, false, false, true);              // H scores, A misses, H misses, A scores: 1-1, one kick each left
            Assert.IsFalse(s.IsDecided);
            Kick(s, true, false);                           // H scores, A misses: 2-1 after 3 each: decided
            Assert.IsTrue(s.IsDecided);
            Assert.AreEqual(TeamSide.Home, s.Winner);
            Assert.AreEqual(6, s.Kicks.Count);
        }

        [Test]
        public void Shootout_EngineEndsEarly_AndNeverSimulatesFurtherKicks()
        {
            var c = ReachShootout(Arena(CompetitionMatchType.Knockout));
            c.RecordShootoutKick(true); c.RecordShootoutKick(false); c.RecordShootoutKick(true); c.RecordShootoutKick(false);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(TeamSide.Home, c.State.Result.Winner);
            Assert.AreEqual(4, c.State.Shootout.Kicks.Count);
            Assert.IsFalse(c.RecordShootoutKick(true));
        }

        // ================= the ball =================

        [Test]
        public void Advance_MovesTheBallWithBallSimulator_NotWithAnObservation()
        {
            var c = New(Classic());
            Kickoff(c);
            c.State.Ball.Physics = new BallState(new Vec3(0f, 0.2f, 0f), new Vec3(6f, 0f, 2f)) { IsGrounded = true };
            BallState copy = c.State.Ball.Physics;
            c.Advance(0.5f);
            BallSimulator.Step(ref copy, c.BallParameters, 0.5f);
            Assert.AreEqual(copy.Position.X, c.State.Ball.Physics.Position.X, 1e-5f);
            Assert.AreEqual(copy.Position.Z, c.State.Ball.Physics.Position.Z, 1e-5f);
            Assert.AreEqual(copy.Velocity.X, c.State.Ball.Physics.Velocity.X, 1e-5f);
            Assert.Greater(c.State.Ball.Physics.Position.X, 1f);
        }

        [Test]
        public void TheBall_IsOnlyChangedByTheCore_ExplicitApiAndRestarts()
        {
            var c = New(Classic());
            Kickoff(c);
            Assert.IsTrue(c.ApplyBallImpulse(new Vec3(5f, 0f, 0f), TeamSide.Home, "hs1"));
            Assert.AreEqual(5f, c.State.Ball.Physics.Velocity.X, 1e-5f);
            Assert.AreEqual(BallMode.InPass, c.State.Ball.Mode);
            Assert.AreEqual("hs1", c.State.Ball.LastTouchPlayerId);
            c.Pause();
            Assert.IsFalse(c.ApplyBallImpulse(new Vec3(1f, 0f, 0f), TeamSide.Home, "hs1"), "no impulse while the ball is not live");
        }

        [Test]
        public void Simulation_IsDeterministic_SameCallsSameMatch()
        {
            string Run()
            {
                var c = New(Arena());
                Kickoff(c);
                c.ApplyBallImpulse(new Vec3(9f, 4f, 7f), TeamSide.Home, "hs2");
                for (int i = 0; i < 1200; i++) c.Advance(Step);
                BallState b = c.State.Ball.Physics;
                return b.Position.X.ToString("R") + "|" + b.Position.Y.ToString("R") + "|" + b.Position.Z.ToString("R") + "|" + string.Join(",", c.Events.Log.Select(e => e.Sequence + e.Kind.ToString()));
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void SimulationClock_GivesTheSameSteps_WhateverTheFrameRate()
        {
            var a = new SimulationClock(Step); var b = new SimulationClock(Step);
            int sa = 0, sb = 0;
            for (int i = 0; i < 60; i++) sa += a.Advance(1f / 60f);
            for (int i = 0; i < 30; i++) sb += b.Advance(1f / 30f);
            Assert.AreEqual(sa, sb, 1);
            Assert.AreEqual(Step, a.StepSeconds);
            Assert.AreEqual(8, new SimulationClock(Step).Advance(10f), "a very long frame is capped");
            Assert.AreEqual(0, new SimulationClock(Step).Advance(-1f));
        }

        // ================= Arena walls =================

        [Test]
        public void Arena_BallReboundsOffTheSideWall_AndTheMatchGoesOn()
        {
            var c = New(Arena());
            Kickoff(c);
            c.State.Ball.Physics = new BallState(new Vec3(0f, 0.2f, 11f), new Vec3(0f, 0f, 12f)) { IsGrounded = true };
            for (int i = 0; i < 60; i++) c.Advance(Step);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.LessOrEqual(c.State.Ball.Physics.Position.Z, 12.5f - 0.2f + 1e-3f);
            Assert.Less(c.State.Ball.Physics.Velocity.Z, 0f, "it came back");
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallBouncedOffWall));
        }

        [Test]
        public void Arena_WallRestitution_IsConfigurable_AndKeepsTheBallSlower()
        {
            float Speed(float restitution)
            {
                var s = Arena(); s.ArenaWallRestitution = restitution;
                var c = New(s); Kickoff(c);
                c.State.Ball.Physics = new BallState(new Vec3(0f, 5f, 12f), new Vec3(0f, 0f, 10f));
                BallParameters bp = c.BallParameters;
                for (int i = 0; i < 30; i++) c.Advance(Step);
                return Math.Abs(c.State.Ball.Physics.Velocity.Z);
            }
            Assert.Greater(Speed(0.9f), Speed(0.3f));
        }

        [Test]
        public void Arena_EndWallOutsideTheGoalMouth_Rebounds_WithNoCornerThrowInOrGoalKick()
        {
            var c = New(Arena());
            Kickoff(c);
            c.RegisterTouch(TeamSide.Away, "as1");
            c.State.Ball.Physics = new BallState(new Vec3(19f, 0.2f, 9f), new Vec3(15f, 0f, 0f)) { IsGrounded = true };
            for (int i = 0; i < 120; i++) c.Advance(Step);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(0, c.State.Score.Home + c.State.Score.Away);
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.CornerAwarded));
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.ThrowInAwarded));
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.GoalKickAwarded));
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.BallOutOfBounds));
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallBouncedOffWall));
        }

        [Test]
        public void Arena_TheGoalMouthIsOpen_AGoalStillCounts()
        {
            var c = New(Arena());
            Kickoff(c);
            c.RegisterTouch(TeamSide.Home, "hs1");
            c.State.Ball.Physics = new BallState(new Vec3(19f, 0.2f, 0f), new Vec3(20f, 0f, 0f)) { IsGrounded = true };
            for (int i = 0; i < 60 && c.State.Phase == MatchPhase.Playing; i++) c.Advance(Step);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
            Assert.AreEqual(1, c.State.Score.Home);
        }

        [Test]
        public void Arena_OutOfBoundsObservation_IsNeverJudgedOut()
        {
            var c = New(Arena());
            Kickoff(c);
            c.Tick(0.1f, new BallObservation(new Vec3(5f, 0.2f, 13.5f), 0.2f));
            c.Tick(0.1f, new BallObservation(new Vec3(30f, 0.2f, 10f), 0.2f));
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.ThrowInAwarded) + c.Events.Count(GameplayEventKind.CornerAwarded) + c.Events.Count(GameplayEventKind.GoalKickAwarded));
        }

        // ================= Classic boundaries =================

        private static MatchController ClassicWithBall(Vec3 pos, Vec3 vel, TeamSide lastTouch)
        {
            var c = New(Classic());
            Kickoff(c);
            c.RegisterTouch(lastTouch, lastTouch == TeamSide.Home ? "hs1" : "as1");
            c.State.Ball.Physics = new BallState(pos, vel) { IsGrounded = true };
            for (int i = 0; i < 120 && c.State.Phase == MatchPhase.Playing; i++) c.Advance(Step);
            return c;
        }

        [Test]
        public void Classic_BallLeavingTheSideline_IsAThrowIn_ForTheOtherTeam()
        {
            var c = ClassicWithBall(new Vec3(0f, 0.2f, 12f), new Vec3(0f, 0f, 8f), TeamSide.Home);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(RestartKind.ThrowIn, c.State.PendingRestart.Kind);
            Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.BallOutOfBounds));
            Assert.IsTrue(c.Events.Has(GameplayEventKind.ThrowInAwarded));
            Assert.IsFalse(c.Events.Has(GameplayEventKind.BallBouncedOffWall));
            Assert.AreEqual(BallMode.Dead, c.State.Ball.Mode);
        }

        [Test]
        public void Classic_BallLeavingTheEndLine_IsACorner_OrAGoalKick()
        {
            // Home attacks +X at the start: the +X end line is Away's goal line
            var corner = ClassicWithBall(new Vec3(19f, 0.2f, 9f), new Vec3(15f, 0f, 0f), TeamSide.Away);
            Assert.AreEqual(RestartKind.Corner, corner.State.PendingRestart.Kind);
            Assert.AreEqual(TeamSide.Home, corner.State.PendingRestart.Team);
            Assert.IsTrue(corner.Events.Has(GameplayEventKind.CornerAwarded));

            var goalKick = ClassicWithBall(new Vec3(19f, 0.2f, 9f), new Vec3(15f, 0f, 0f), TeamSide.Home);
            Assert.AreEqual(RestartKind.GoalKick, goalKick.State.PendingRestart.Kind);
            Assert.AreEqual(TeamSide.Away, goalKick.State.PendingRestart.Team);
            Assert.IsTrue(goalKick.Events.Has(GameplayEventKind.GoalKickAwarded));
        }

        [Test]
        public void Classic_TheBallIsPlacedAtTheRestartSpot_AndPlayResumesWhenConfirmed()
        {
            var c = ClassicWithBall(new Vec3(0f, 0.2f, 12f), new Vec3(0f, 0f, 8f), TeamSide.Home);
            Assert.AreEqual(12.5f, Math.Abs(c.State.Ball.Physics.Position.Z), 1e-4f);
            Assert.AreEqual(0f, c.State.Ball.Physics.Velocity.Magnitude);
            Assert.IsTrue(c.ConfirmRestartReady());
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
        }

        // ================= rules and events =================

        [Test]
        public void ModeRules_SayWhatEachModeHas()
        {
            IGameModeRules a = GameModeRules.For(GameMode.Arena), c = GameModeRules.For(GameMode.Classic);
            Assert.IsTrue(a.UsesWalls); Assert.IsFalse(c.UsesWalls);
            foreach (var r in new[] { a.HasFouls, a.HasOffside, a.HasCorners, a.HasThrowIns, a.HasGoalKicks, a.HasCards, a.HasPenalties, a.HasReferee, a.HasInjuries, a.HasHalfTimeBreak }) Assert.IsFalse(r);
            foreach (var r in new[] { c.HasFouls, c.HasOffside, c.HasCorners, c.HasThrowIns, c.HasGoalKicks, c.HasCards, c.HasPenalties, c.HasReferee, c.HasHalfTimeBreak }) Assert.IsTrue(r);
            Assert.IsFalse(c.HasInjuries, "no injuries in any mode");
        }

        [Test]
        public void EventBus_RefusesEventsTheModeDoesNotHave()
        {
            var arena = New(Arena());
            foreach (var k in new[] { GameplayEventKind.FoulCommitted, GameplayEventKind.OffsideDetected, GameplayEventKind.CornerAwarded, GameplayEventKind.ThrowInAwarded, GameplayEventKind.GoalKickAwarded, GameplayEventKind.PenaltyAwarded, GameplayEventKind.YellowCardIssued, GameplayEventKind.RedCardIssued })
                Assert.IsFalse(arena.Events.Publish(new GameplayEvent { Kind = k }), k.ToString());
            Assert.AreEqual(8, arena.Events.RejectedCount);
            Assert.AreEqual(0, arena.Events.Log.Count);
            Assert.IsTrue(arena.Events.Publish(new GameplayEvent { Kind = GameplayEventKind.BallBouncedOffWall }));

            var classic = New(Classic());
            Assert.IsTrue(classic.Events.Publish(new GameplayEvent { Kind = GameplayEventKind.FoulCommitted }));
            Assert.IsTrue(classic.Events.Publish(new GameplayEvent { Kind = GameplayEventKind.OffsideDetected }));
            Assert.IsFalse(classic.Events.Publish(new GameplayEvent { Kind = GameplayEventKind.BallBouncedOffWall }), "Classic has no walls");
        }

        [Test]
        public void EventBus_NumbersEventsInOrder_AndTellsSubscribers()
        {
            var c = New(Classic());
            var seen = new List<GameplayEvent>();
            c.Events.Published += seen.Add;
            Kickoff(c);
            CollectionAssert.AreEqual(Enumerable.Range(1, seen.Count).ToArray(), seen.Select(e => e.Sequence).ToArray());
            Assert.AreEqual(c.Events.Log.Count, seen.Count);
            Assert.AreEqual(GameplayEventKind.MatchStarted, seen[0].Kind);
        }

        [Test]
        public void EngineAsksTheRules_NotTheMode()
        {
            string dir = MatchSourceDirectory();
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string name = Path.GetFileName(file);
                if (name == "GameModeRules.cs" || name == "MatchModes.cs" || name == "MatchTypes.cs") continue;
                string src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("GameMode.Arena") || src.Contains("GameMode.Classic"), name + " must ask the rules, not compare the mode");
            }
        }

        // ================= goals =================

        [Test]
        public void Goal_UpdatesTheScore_RecordsScorerAndAssist_AndRaisesTheEvent()
        {
            var c = New(Classic());
            Kickoff(c);
            c.RegisterTouch(TeamSide.Home, "hs2");
            c.RegisterTouch(TeamSide.Home, "hs4");
            int raised = 0; c.GoalScored += t => raised++;
            c.Tick(0.1f, HomeGoal);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
            GoalRecord g = c.State.Goals.Single();
            Assert.AreEqual("hs4", g.ScorerId); Assert.AreEqual("hs2", g.AssistId); Assert.IsFalse(g.OwnGoal);
            Assert.AreEqual(1, raised);
            GameplayEvent e = c.Events.Log.Last(x => x.Kind == GameplayEventKind.GoalScored);
            Assert.AreEqual("hs4", e.PlayerId); Assert.AreEqual("hs2", e.OtherPlayerId);
        }

        [Test]
        public void Goal_ByTheOtherTeamsLastTouch_IsAnOwnGoal_WithNoAssist()
        {
            var c = New(Classic());
            Kickoff(c);
            c.RegisterTouch(TeamSide.Away, "as3");
            c.Tick(0.1f, HomeGoal);
            GoalRecord g = c.State.Goals.Single();
            Assert.IsTrue(g.OwnGoal); Assert.IsNull(g.AssistId);
            Assert.AreEqual(TeamSide.Home, g.Team);
        }

        [Test]
        public void Goal_AfterTheCelebration_ResumesWithAKickoffForTheConcedingTeam()
        {
            var c = New(Classic());
            Kickoff(c);
            c.Tick(0.1f, HomeGoal);
            c.Advance(3.1f);
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.AreEqual(0f, c.State.Ball.Physics.Position.X, 1e-5f);
        }

        [Test]
        public void ClockStopsDuringTheCelebration_AndTheMatchTimeIsNotLost()
        {
            var c = New(Classic());
            Kickoff(c);
            c.Advance(10f);
            c.Tick(0.5f, HomeGoal);
            float t = c.State.MatchTimeSeconds;
            c.Advance(2f);
            Assert.AreEqual(t, c.State.MatchTimeSeconds, 1e-4f);
        }

        // ================= possession =================

        [Test]
        public void Possession_FreeControlledAndBackToFree()
        {
            var c = New(Classic());
            Kickoff(c);
            Assert.IsFalse(c.State.Ball.HasPossession);
            Assert.IsTrue(c.GiveBallTo(TeamSide.Home, "hs1"));
            Assert.AreEqual(BallMode.Controlled, c.State.Ball.Mode);
            Assert.AreEqual(TeamSide.Home, c.State.Ball.PossessingTeam);
            Assert.AreEqual("hs1", c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(1, c.Events.Count(GameplayEventKind.PossessionChanged));
            c.GiveBallTo(TeamSide.Home, "hs2");
            Assert.AreEqual(1, c.Events.Count(GameplayEventKind.PossessionChanged), "same team: not a change");
            c.GiveBallTo(TeamSide.Away, "as1");
            Assert.AreEqual(2, c.Events.Count(GameplayEventKind.PossessionChanged));
            c.ReleaseBall();
            Assert.IsFalse(c.State.Ball.HasPossession);
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
        }

        [Test]
        public void Possession_RefusesAPlayerWhoIsNotOnThePitch_OrWhenTheBallIsNotLive()
        {
            var c = New(Classic());
            Assert.IsFalse(c.GiveBallTo(TeamSide.Home, "hs1"));
            Kickoff(c);
            Assert.IsFalse(c.GiveBallTo(TeamSide.Home, "hb1"), "a substitute is not playing");
            Assert.IsFalse(c.GiveBallTo(TeamSide.Home, "nobody"));
        }

        [Test]
        public void TheBall_IsDeadWhenNotInPlay_AndFreeWhenPlayResumes()
        {
            var c = New(Classic());
            Assert.AreEqual(BallMode.Dead, c.State.Ball.Mode);
            c.Start();
            Assert.AreEqual(BallMode.Dead, c.State.Ball.Mode);
            c.ConfirmRestartReady();
            Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
            c.Pause(); Assert.AreEqual(BallMode.Free, c.State.Ball.Mode, "pausing does not change what the ball was doing");
            c.Resume();
        }

        // ================= substitutions =================

        [Test]
        public void Substitutions_ThreeAreAccepted_TheFourthIsRejected()
        {
            var c = New(Classic());
            c.Start();
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs1", "hb1"));
            Assert.AreEqual(1, c.State.Home.SubstitutionsUsed);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs2", "hb2"));
            Assert.AreEqual(2, c.State.Home.SubstitutionsUsed);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs3", "hb3"));
            Assert.AreEqual(3, c.State.Home.SubstitutionsUsed);
            Assert.AreEqual(SubstitutionResult.LimitReached, c.TrySubstitute(TeamSide.Home, "hs0", "hb0"));
            Assert.AreEqual(3, c.State.Home.SubstitutionsUsed);
            Assert.AreEqual(0, c.State.Away.SubstitutionsUsed, "the counter is per team");
            Assert.AreEqual(3, c.Events.Count(GameplayEventKind.SubstitutionMade));
        }

        [Test]
        public void Substitutions_ChangeWhoIsPlaying_AndASubstitutedPlayerCannotReturn()
        {
            var c = New(Classic());
            c.Start();
            c.TrySubstitute(TeamSide.Home, "hs1", "hb1");
            Assert.AreEqual(MatchPlayerStatus.SubstitutedOff, c.State.Home.Find("hs1").Status);
            Assert.AreEqual(MatchPlayerStatus.OnPitch, c.State.Home.Find("hb1").Status);
            Assert.AreEqual(6, c.State.Home.ActivePlayerCount);
            Assert.AreEqual(SubstitutionResult.SubstituteNotAvailable, c.TrySubstitute(TeamSide.Home, "hs2", "hs1"));
            Assert.AreEqual(SubstitutionResult.PlayerNotOnPitch, c.TrySubstitute(TeamSide.Home, "hs1", "hb2"));
            Assert.AreEqual(SubstitutionResult.PlayerNotOnPitch, c.TrySubstitute(TeamSide.Home, "nobody", "hb2"));
        }

        [Test]
        public void Substitutions_TheGoalSlotOnlyGoesToAKeeper()
        {
            var c = New(Classic());
            c.Start();
            Assert.AreEqual(SubstitutionResult.GoalkeeperMismatch, c.TrySubstitute(TeamSide.Home, "hs1", "hb0"));
            Assert.AreEqual(SubstitutionResult.GoalkeeperMismatch, c.TrySubstitute(TeamSide.Home, "hs0", "hb1"));
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs0", "hb0"));
            Assert.IsTrue(c.State.Home.Find("hb0").InGoal);
            Assert.AreEqual(1, c.State.Home.SubstitutionsUsed);
        }

        [Test]
        public void Substitutions_ArenaOnlyAtASafeMoment_ClassicOnlyAtValidInterruptions()
        {
            foreach (var s in new[] { Arena(), Classic() })
            {
                var c = New(s);
                Assert.AreEqual(SubstitutionResult.MatchNotActive, c.TrySubstitute(TeamSide.Home, "hs1", "hb1"));
                c.Start();
                Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs1", "hb1"), "waiting for a kick-off is safe");
                c.ConfirmRestartReady();
                Assert.AreEqual(SubstitutionResult.NotSafeMoment, c.TrySubstitute(TeamSide.Home, "hs2", "hb2"), s.Mode + ": never during live play");
                c.Pause();
                Assert.AreEqual(SubstitutionResult.NotSafeMoment, c.TrySubstitute(TeamSide.Home, "hs2", "hb2"), "a pause is not a match interruption");
                c.Resume();
                c.Tick(0.1f, HomeGoal);
                Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, "hs2", "hb2"), "after a goal");
            }
        }

        [Test]
        public void Substitutions_ClassicAllowsADeadBallRestart_ArenaHasNone()
        {
            var classic = ClassicWithBall(new Vec3(0f, 0.2f, 12f), new Vec3(0f, 0f, 8f), TeamSide.Home);
            Assert.AreEqual(MatchPhase.WaitingForRestart, classic.State.Phase);
            Assert.AreEqual(SubstitutionResult.Accepted, classic.TrySubstitute(TeamSide.Away, "as1", "ab1"));
            Assert.IsFalse(GameModeRules.For(GameMode.Arena).IsSafeMomentForSubstitution(MatchPhase.WaitingForRestart));
        }

        [Test]
        public void Substitutions_WorkAtTheBreak()
        {
            var c = New(Classic());
            Kickoff(c); RunPeriod(c);
            Assert.AreEqual(MatchPhase.Break, c.State.Phase);
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Away, "as1", "ab1"));
        }

        [Test]
        public void Substitutions_NeedASquad()
        {
            var c = New(Classic(), false);
            c.Start();
            Assert.AreEqual(SubstitutionResult.NoSquad, c.TrySubstitute(TeamSide.Home, "a", "b"));
        }

        // ================= discipline =================

        [Test]
        public void Discipline_FirstYellow_IsJustAYellow()
        {
            var c = New(Classic());
            Kickoff(c);
            Assert.AreEqual(CardResult.Issued, c.IssueCard(TeamSide.Home, "hs1", CardKind.Yellow));
            Assert.AreEqual(1, c.State.Home.Find("hs1").YellowCards);
            Assert.AreEqual(MatchPlayerStatus.OnPitch, c.State.Home.Find("hs1").Status);
            Assert.AreEqual(1, c.Events.Count(GameplayEventKind.YellowCardIssued));
            Assert.AreEqual(0, c.Events.Count(GameplayEventKind.RedCardIssued));
        }

        [Test]
        public void Discipline_SecondYellow_IsRecordedFirst_AndBecomesARed()
        {
            var c = New(Classic());
            Kickoff(c);
            c.IssueCard(TeamSide.Home, "hs1", CardKind.Yellow);
            Assert.AreEqual(CardResult.DismissedBySecondYellow, c.IssueCard(TeamSide.Home, "hs1", CardKind.Yellow));
            MatchPlayer p = c.State.Home.Find("hs1");
            Assert.AreEqual(2, p.YellowCards);
            Assert.AreEqual(MatchPlayerStatus.Dismissed, p.Status);
            Assert.AreEqual(5, c.State.Home.ActivePlayerCount);
            var kinds = c.Events.Log.Select(e => e.Kind).ToList();
            Assert.Less(kinds.LastIndexOf(GameplayEventKind.YellowCardIssued), kinds.IndexOf(GameplayEventKind.RedCardIssued), "the second yellow is registered before the dismissal");
            Assert.AreEqual(2, c.Events.Count(GameplayEventKind.YellowCardIssued));
            Assert.AreEqual("SecondYellow", c.Events.Log.Last(e => e.Kind == GameplayEventKind.RedCardIssued).Detail);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.PlayerDismissed));
        }

        [Test]
        public void Discipline_DirectRed_UsesTheSameDismissalAsASecondYellow()
        {
            var c = New(Classic());
            Kickoff(c);
            Assert.AreEqual(CardResult.DismissedByRed, c.IssueCard(TeamSide.Away, "as2", CardKind.Red));
            Assert.AreEqual(MatchPlayerStatus.Dismissed, c.State.Away.Find("as2").Status);
            Assert.AreEqual(CardResult.PlayerNotOnPitch, c.IssueCard(TeamSide.Away, "as2", CardKind.Yellow), "a dismissed player cannot be carded again");
            c.Tick(0.1f, HomeGoal);          // a safe moment for substitutions
            Assert.AreEqual(SubstitutionResult.PlayerNotOnPitch, c.TrySubstitute(TeamSide.Away, "as2", "ab1"), "and cannot be substituted");
        }

        [Test]
        public void Discipline_ArenaHasNoCards_AndNothingIsPublished()
        {
            var c = New(Arena());
            Kickoff(c);
            int before = c.Events.Log.Count;
            Assert.AreEqual(CardResult.NotAllowedInMode, c.IssueCard(TeamSide.Home, "hs1", CardKind.Yellow));
            Assert.AreEqual(CardResult.NotAllowedInMode, c.IssueCard(TeamSide.Home, "hs1", CardKind.Red));
            Assert.AreEqual(before, c.Events.Log.Count);
            Assert.AreEqual(6, c.State.Home.ActivePlayerCount);
        }

        // ================= insufficient players =================

        [Test]
        public void ThreeActivePlayers_StillPlay_FewerEndsTheMatch_WithATechnicalVictory()
        {
            var c = New(Classic());
            Kickoff(c);
            c.Tick(0.1f, HomeGoal); c.Advance(3.1f); c.ConfirmRestartReady();     // Home 1-0
            foreach (string id in new[] { "hs1", "hs2", "hs3" }) c.IssueCard(TeamSide.Home, id, CardKind.Red);
            Assert.AreEqual(3, c.State.Home.ActivePlayerCount);
            Assert.IsFalse(c.State.HasResult, "three is the minimum: play goes on");
            c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            Assert.IsTrue(c.State.HasResult);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(MatchTerminationReason.InsufficientPlayers, c.State.Result.Reason);
            Assert.AreEqual(TeamSide.Away, c.State.Result.Winner, "the rival wins");
            Assert.IsFalse(c.State.Result.IsDraw);
            Assert.AreEqual(1, c.State.Result.HomeGoals, "the score is kept as it was");
            Assert.AreEqual(0, c.State.Result.AwayGoals);
            Assert.IsTrue(c.Events.Has(GameplayEventKind.MatchEnded));
        }

        [Test]
        public void InsufficientPlayers_BeatsExtraTimeAndPenalties_EvenInAKnockoutDraw()
        {
            foreach (var s in new[] { Classic(CompetitionMatchType.Knockout) })
            {
                var c = New(s);
                Kickoff(c);
                foreach (string id in new[] { "hs1", "hs2", "hs3", "hs4" }) c.IssueCard(TeamSide.Home, id, CardKind.Red);
                Assert.AreEqual(MatchTerminationReason.InsufficientPlayers, c.State.Result.Reason);
                Assert.IsFalse(c.State.ExtraTimeReached);
                Assert.IsNull(c.State.Shootout);
                Assert.IsFalse(c.Events.Has(GameplayEventKind.ExtraTimeStarted));
                Assert.IsFalse(c.Events.Has(GameplayEventKind.PenaltyShootoutStarted));
                Assert.IsFalse(c.RecordShootoutKick(true));
                c.Advance(1000f);
                Assert.AreEqual(MatchPhase.Finished, c.State.Phase, "nothing is simulated any more");
            }
        }

        [Test]
        public void InsufficientPlayers_TheMinimumIsConfigurable()
        {
            var s = Classic(); s.MinimumActivePlayers = 5;
            var c = New(s);
            Kickoff(c);
            c.IssueCard(TeamSide.Home, "hs1", CardKind.Red);
            Assert.IsFalse(c.State.HasResult);
            c.IssueCard(TeamSide.Home, "hs2", CardKind.Red);
            Assert.AreEqual(MatchTerminationReason.InsufficientPlayers, c.State.Result.Reason);
        }

        // ================= expelled goalkeeper and emergency keeper =================

        [Test]
        public void ExpelledKeeper_BlocksTheRestart_UntilSomeoneIsInGoal()
        {
            var c = New(Classic());
            c.Start();
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.IsTrue(c.State.Home.NeedsGoalkeeper);
            Assert.IsFalse(c.ConfirmRestartReady(), "nobody is in goal");
            Assert.AreEqual(SubstitutionResult.Accepted, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs1"));
            Assert.IsFalse(c.State.Home.NeedsGoalkeeper);
            Assert.IsTrue(c.ConfirmRestartReady());
        }

        [Test]
        public void ExpelledKeeper_WithASubstituteAndASubstitutionLeft_TheSubstituteComes_AndCostsOne()
        {
            var c = New(Classic());
            c.Start();
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.AreEqual(EmergencyKeeperResult.ReplacementAvailable, c.TryAssignEmergencyKeeper(TeamSide.Home, "hs1"), "the substitute keeper comes first");
            Assert.AreEqual(SubstitutionResult.Accepted, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs1"));
            Assert.AreEqual(1, c.State.Home.SubstitutionsUsed);
            Assert.IsTrue(c.State.Home.Find("hb0").InGoal);
            Assert.AreEqual(MatchPlayerStatus.SubstitutedOff, c.State.Home.Find("hs1").Status);
            Assert.AreEqual(5, c.State.Home.ActivePlayerCount, "the team plays one man short");
        }

        [Test]
        public void ExpelledKeeper_WithNoSubstitutionLeft_AFieldPlayerTakesTheGloves_WithoutAnExtraSubstitution()
        {
            var c = New(Classic());
            c.Start();
            c.TrySubstitute(TeamSide.Home, "hs1", "hb1"); c.TrySubstitute(TeamSide.Home, "hs2", "hb2"); c.TrySubstitute(TeamSide.Home, "hs3", "hb3");
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.AreEqual(SubstitutionResult.LimitReached, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs4"));
            Assert.AreEqual(EmergencyKeeperResult.Assigned, c.TryAssignEmergencyKeeper(TeamSide.Home, "hs4"));
            MatchPlayer p = c.State.Home.Find("hs4");
            Assert.IsTrue(p.InGoal); Assert.IsTrue(p.IsEmergencyKeeper); Assert.IsFalse(p.IsGoalkeeper, "he is still a field player");
            Assert.AreEqual(3, c.State.Home.SubstitutionsUsed, "never above the limit");
            Assert.IsTrue(c.ConfirmRestartReady());
        }

        [Test]
        public void ExpelledKeeper_WithoutASubstituteKeeperOnTheBench_AFieldPlayerTakesTheGloves()
        {
            var c = New(Classic());
            c.Start();
            c.TrySubstitute(TeamSide.Home, "hs0", "hb0");              // the substitute keeper already played
            c.IssueCard(TeamSide.Home, "hb0", CardKind.Red);
            Assert.AreEqual(SubstitutionResult.SubstituteNotAvailable, c.TryReplaceDismissedKeeper(TeamSide.Home, "hb1", "hs1"));
            Assert.AreEqual(EmergencyKeeperResult.Assigned, c.TryAssignEmergencyKeeper(TeamSide.Home, "hs1"));
        }

        [Test]
        public void SubstitutionLimit_IsNeverExceeded_ByAnyPath()
        {
            var c = New(Classic());
            c.Start();
            c.TrySubstitute(TeamSide.Home, "hs1", "hb1"); c.TrySubstitute(TeamSide.Home, "hs2", "hb2"); c.TrySubstitute(TeamSide.Home, "hs3", "hb3");
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            c.TryReplaceDismissedKeeper(TeamSide.Home, "hb0", "hs4");
            c.TrySubstitute(TeamSide.Home, "hs4", "hb0");
            Assert.AreEqual(3, c.State.Home.SubstitutionsUsed);
            Assert.AreEqual(3, c.Events.Count(GameplayEventKind.SubstitutionMade));
        }

        [Test]
        public void EmergencyKeeperProfile_IsDerivedFromTheTwelveAttributes_AndNeverStored()
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Agility = 80; a.Control = 70; a.Technique = 60; a.Defense = 50; a.Speed = 90; a.Passing = 40; a.Stamina = 30;
            EmergencyKeeperProfile exact = EmergencyKeeperProfile.Derive(a, new EmergencyKeeperTuning { UnspecializedFactor = 1f });
            Assert.AreEqual(75, exact.Reflexes);      // (Agility + Control) / 2
            Assert.AreEqual(65, exact.Handling);      // (Control + Technique) / 2
            Assert.AreEqual(65, exact.Positioning);   // (Defense + Agility) / 2
            Assert.AreEqual(85, exact.Diving);        // (Agility + Speed) / 2
            Assert.AreEqual(50, exact.Kicking);       // (Passing + Technique) / 2
            Assert.AreEqual(55, exact.Distribution);  // (Passing + Control) / 2
            Assert.AreEqual(55, exact.Recovery);      // (Stamina + Agility) / 2
            Assert.IsTrue(exact.CommandIsContextual);
            Assert.AreEqual(0, exact.Get(GoalkeeperCapability.Command), "Command has no derived number");

            EmergencyKeeperProfile worse = EmergencyKeeperProfile.Derive(a);
            Assert.Less(worse.Reflexes, exact.Reflexes, "a field player is clearly worse than a specialist");
            Assert.Less(worse.Diving, exact.Diving);

            foreach (FieldInfo f in typeof(PlayerDefinition).GetFields()) StringAssert.DoesNotContain("Emergency", f.Name);
            foreach (FieldInfo f in typeof(PlayerAttributes).GetFields()) StringAssert.DoesNotContain("Reflexes", f.Name);
        }

        // ================= isolation guards =================

        private static string MatchSourceDirectory()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            return Path.Combine(core, "Match");
        }

        [Test]
        public void CoachBoost_IsNotConnectedToTheSimulation()
        {
            string dir = MatchSourceDirectory();
            string[] files = Directory.GetFiles(dir, "*.cs").Concat(Directory.GetFiles(Path.Combine(dir, "..", "Ball"), "*.cs")).ToArray();
            foreach (string file in files)
            {
                string src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("Coach") || src.Contains("EffectiveOverall") || src.Contains("CoachBoost"), Path.GetFileName(file) + " must not read the coach boost");
            }
            foreach (Type t in typeof(MatchState).Assembly.GetTypes().Where(t => t.Namespace == "FS27.Core" && t.Name.StartsWith("Match")))
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    Assert.IsFalse(f.FieldType.Name.StartsWith("Coach"), t.Name + "." + f.Name);
        }

        [Test]
        public void TheCoreMatch_UsesNoUnityTimeNoRandomnessAndNoWallClock()
        {
            string dir = MatchSourceDirectory();
            string[] files = Directory.GetFiles(dir, "*.cs").Concat(Directory.GetFiles(Path.Combine(dir, "..", "Ball"), "*.cs")).Append(Path.Combine(dir, "..", "SimulationClock.cs")).ToArray();
            foreach (string file in files)
            {
                string src = File.ReadAllText(file);
                foreach (string bad in new[] { "UnityEngine", "Time.time", "Time.deltaTime", "DateTime", "Stopwatch", "System.Random", "new Random", "Guid.NewGuid" })
                    Assert.IsFalse(src.Contains(bad), Path.GetFileName(file) + " uses " + bad);
            }
        }

        [Test]
        public void TheBallIsAuthoritativeInCore_AndUnityOnlyReflectsIt()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            string gameplay = Path.Combine(core, "..", "Gameplay");
            string bridge = File.ReadAllText(Path.Combine(gameplay, "MatchBridge.cs"));
            string ball = File.ReadAllText(Path.Combine(gameplay, "BallController.cs"));
            string physics = File.ReadAllText(Path.Combine(gameplay, "BallPhysics.cs"));
            Assert.IsTrue(bridge.Contains("match.Advance"), "the bridge advances the Core");
            Assert.IsTrue(bridge.Contains("ApplyCoreState"), "and reflects the Core ball");
            Assert.IsFalse(bridge.Contains("linearVelocity") || bridge.Contains(".Body"), "the bridge never reads the rigidbody back");
            Assert.IsTrue(ball.Contains("SetCoreDriven") && ball.Contains("isKinematic = driven"), "the rigidbody becomes kinematic when the Core drives it");
            Assert.IsTrue(physics.Contains("if (ball.IsCoreDriven) return;"), "Unity physics adds no forces to a core-driven ball");
            Assert.IsFalse(bridge.Contains("MatchState =") || bridge.Contains(".State.Score.Add"), "Unity does not write the match state");
        }

        [Test]
        public void ThereIsOnlyOneMatchState()
        {
            var states = typeof(MatchState).Assembly.GetTypes().Where(t => t.Name == "GameState" || t.Name == "MatchState2" || (t.Name.EndsWith("MatchState") && t.Name != "MatchState" && t.Name != "TeamMatchState" )).ToArray();
            Assert.AreEqual(0, states.Length, string.Join(",", states.Select(t => t.Name)));
        }

        [Test]
        public void ActionKindValues_StayUntouched()
        {
            Assert.AreEqual(27, Enum.GetValues(typeof(FootballActionKind)).Length);
            Assert.AreEqual(15, (int)FootballActionKind.GoalkeeperDistribution);
            Assert.AreEqual(22, (int)FootballActionKind.GoalkeeperRush);
        }
    }
}
