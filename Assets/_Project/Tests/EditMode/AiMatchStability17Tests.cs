using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 17: where a goal comes from (GoalOriginTracker / AiDiagnostics goal log), the stamina of a whole match (walking recovery) and the reproducibility of the quick battery.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static GameplayEvent Ev(GameplayEventKind kind, float time, TeamSide team, string player = "p", string detail = "")
        {
            return new GameplayEvent { Kind = kind, MatchTime = time, HasTeam = true, Team = team, PlayerId = player, Detail = detail };
        }

        private static GoalOriginRecord ReadGoal(float goalTime, params GameplayEvent[] events)
        {
            var tracker = new GoalOriginTracker(new DiagnosticsConfig().GoalOriginRing);
            foreach (GameplayEvent e in events) tracker.Add(e);
            return tracker.Read(TeamSide.Home, "h1", false, goalTime, new DiagnosticsConfig().GoalOriginWindowSeconds);
        }

        [Test]
        public void GoalOrigin_AShotWithNothingAfterIt_IsAShot_Direct()
        {
            GoalOriginRecord g = ReadGoal(10f,
                Ev(GameplayEventKind.BallTouched, 9f, TeamSide.Home, "h1", "Shot"),
                Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"));
            Assert.AreEqual(GoalOrigin.Shot, g.Origin); Assert.AreEqual(GoalEvidence.Direct, g.Evidence); Assert.AreEqual(9f, g.ShotTime, 1e-4f);
        }

        [Test]
        public void GoalOrigin_AShotThatIsParriedAndTouchedAgainByTheScoringTeam_IsARebound_Inferred()
        {
            GoalOriginRecord g = ReadGoal(12f,
                Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"),
                Ev(GameplayEventKind.BallTouched, 9.3f, TeamSide.Away, "ak", FootballActionKind.GoalkeeperSave.ToString()),
                Ev(GameplayEventKind.BallTouched, 10f, TeamSide.Home, "h2", "Shot"));
            Assert.AreEqual(GoalOrigin.Rebound, g.Origin); Assert.AreEqual(GoalEvidence.Inferred, g.Evidence);
        }

        [Test]
        public void GoalOrigin_AShotThatIsKnockedOnAndGoesInWithoutANewTouch_IsADeflection_Direct()
        {
            GoalOriginRecord g = ReadGoal(10f,
                Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"),
                Ev(GameplayEventKind.BallDeflected, 9.2f, TeamSide.Away, "a3", "Block"));
            Assert.AreEqual(GoalOrigin.Deflection, g.Origin); Assert.AreEqual(GoalEvidence.Direct, g.Evidence);
        }

        [Test]
        public void GoalOrigin_ABlockedShotCountsAsADeflection()
        {
            GoalOriginRecord g = ReadGoal(10f,
                Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"),
                Ev(GameplayEventKind.InterceptionPerformed, 9.2f, TeamSide.Away, "a3", GoalOriginTracker.BlockedDetail));
            Assert.AreEqual(GoalOrigin.Deflection, g.Origin);
        }

        [Test]
        public void GoalOrigin_WithoutAShotOfTheScoringTeam_ItIsNotInvented_Unknown_AndSaysWhatCameBefore()
        {
            GoalOriginRecord g = ReadGoal(10f, Ev(GameplayEventKind.PassPerformed, 9f, TeamSide.Home, "h1", FootballActionKind.LongPass.ToString()));
            Assert.AreEqual(GoalOrigin.Unknown, g.Origin); Assert.AreEqual(GoalEvidence.None, g.Evidence);
            Assert.AreEqual(GameplayEventKind.PassPerformed, g.PreviousKind); Assert.AreEqual(FootballActionKind.LongPass.ToString(), g.PreviousDetail);
            Assert.Less(g.ShotTime, 0f);
        }

        [Test]
        public void GoalOrigin_AShotOlderThanTheWindow_IsNotUsed_AndAnOpponentsShotIsNeverUsed()
        {
            float window = new DiagnosticsConfig().GoalOriginWindowSeconds;
            Assert.AreEqual(GoalOrigin.Unknown, ReadGoal(30f, Ev(GameplayEventKind.ShotPerformed, 30f - window - 1f, TeamSide.Home, "h1", "Shot")).Origin, "too old");
            Assert.AreEqual(GoalOrigin.Unknown, ReadGoal(10f, Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Away, "a1", "Shot")).Origin, "the other team's shot");
        }

        [Test]
        public void GoalOrigin_AnOwnGoalWithoutAShotOfTheScoringTeam_IsNotAttributed()
        {
            var tracker = new GoalOriginTracker(8);
            tracker.Add(Ev(GameplayEventKind.PassPerformed, 9f, TeamSide.Away, "a1", FootballActionKind.LongPass.ToString()));
            GoalOriginRecord g = tracker.Read(TeamSide.Home, "a2", true, 10f, 6f);
            Assert.AreEqual(GoalOrigin.Unknown, g.Origin); Assert.IsTrue(g.OwnGoal);
        }

        [Test]
        public void GoalOrigin_AShotTheKeeperTouchedOnTheLine_IsAShot_Inferred_EvenWhenTheCoreFlagsItAsAnOwnGoal()
        {
            var tracker = new GoalOriginTracker(8);
            tracker.Add(Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"));
            tracker.Add(Ev(GameplayEventKind.BallTouched, 9.3f, TeamSide.Away, "ak", FootballActionKind.GoalkeeperCatch.ToString()));
            GoalOriginRecord g = tracker.Read(TeamSide.Home, "ak", true, 9.3f, 6f);
            Assert.AreEqual(GoalOrigin.Shot, g.Origin); Assert.AreEqual(GoalEvidence.Inferred, g.Evidence); Assert.IsTrue(g.OwnGoal);
        }

        [Test]
        public void GoalOrigin_AShotThatAnOpponentTouchesIntoHisOwnGoal_IsADeflection()
        {
            var tracker = new GoalOriginTracker(8);
            tracker.Add(Ev(GameplayEventKind.ShotPerformed, 9f, TeamSide.Home, "h1", "Shot"));
            tracker.Add(Ev(GameplayEventKind.BallTouched, 9.2f, TeamSide.Away, "a3", "Control"));
            Assert.AreEqual(GoalOrigin.Deflection, tracker.Read(TeamSide.Home, "a3", true, 9.4f, 6f).Origin);
        }

        [Test]
        public void GoalOrigin_TheRingKeepsTheLatestEntries_AndIgnoresWhatIsNotAboutTheBall()
        {
            var tracker = new GoalOriginTracker(3);
            for (int i = 0; i < 5; i++) tracker.Add(Ev(GameplayEventKind.PassPerformed, i, TeamSide.Home, "p" + i));
            tracker.Add(Ev(GameplayEventKind.FoulCommitted, 9f, TeamSide.Home));
            Assert.AreEqual(3, tracker.Count);
            Assert.AreEqual("p4", tracker.Recent(0).PlayerId); Assert.AreEqual("p2", tracker.Recent(2).PlayerId);
        }

        [Test]
        public void GoalOrigin_EveryGoalOfAMatchIsCountedOnce_ByOriginAndByEvidence_AndTheCoreScoreIsTheAuthority()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var all = NewDiagnostics();
                int coreGoals = 0;
                for (int seed = 1701; seed < 1705; seed++)
                {
                    var one = NewDiagnostics();
                    AttackSim r = SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 360f, true, one);
                    coreGoals += r.GoalsHome + r.GoalsAway;
                    Assert.AreEqual(r.GoalsHome + r.GoalsAway, one.Goals, mode + " " + seed + ": the observer counts the Core's goals");
                    Assert.AreEqual(one.Goals, one.GoalLogCount, mode + " " + seed + ": one record per goal");
                    all.Merge(one);
                }
                double byOrigin = Enum.GetValues(typeof(GoalOrigin)).Cast<GoalOrigin>().Sum(o => all.GoalsByOrigin(o));
                double byEvidence = Enum.GetValues(typeof(GoalEvidence)).Cast<GoalEvidence>().Sum(e => all.GoalsByEvidence(e));
                Assert.AreEqual(coreGoals, byOrigin, mode + ": by origin");
                Assert.AreEqual(coreGoals, byEvidence, mode + ": by evidence");
                Assert.Greater(coreGoals, 4, mode + ": there are goals to read");
                Assert.AreEqual(0.0, all.GoalsByOrigin(GoalOrigin.Cross), "the Core has no cross action yet: nothing is attributed to it");
                for (int i = 0; i < all.GoalLogCount; i++)
                {
                    GoalOriginRecord g = all.GoalAt(i);
                    Assert.IsFalse(g.Origin == GoalOrigin.Unknown && g.Evidence != GoalEvidence.None, "an unknown origin has no evidence");
                    Assert.IsFalse(g.Origin != GoalOrigin.Unknown && g.Evidence == GoalEvidence.None, "an origin without evidence is not stated");
                }
            }
        }

        [Test]
        public void GoalOrigin_TheLogIsTheSameEveryTime_AndObservingChangesNothingInTheMatch()
        {
            var a = NewDiagnostics(); var b = NewDiagnostics();
            AttackSim ra = SimulateAttack(GameMode.Arena, 1711, true, 3, 8, 240f, true, a), rb = SimulateAttack(GameMode.Arena, 1711, true, 3, 8, 240f, true, b), plain = SimulateAttack(GameMode.Arena, 1711, true, 3, 8, 240f, true);
            Assert.AreEqual(ra.Fingerprint, rb.Fingerprint); Assert.AreEqual(ra.Fingerprint, plain.Fingerprint, "with or without the observer the match is the same");
            Assert.AreEqual(a.GoalLogCount, b.GoalLogCount);
            for (int i = 0; i < a.GoalLogCount; i++) { Assert.AreEqual(a.GoalAt(i).Origin, b.GoalAt(i).Origin); Assert.AreEqual(a.GoalAt(i).MatchTime, b.GoalAt(i).MatchTime, 1e-5f); Assert.AreEqual(a.GoalAt(i).ScorerId, b.GoalAt(i).ScorerId); }
        }

        // ================= the stamina of a whole match =================

        [Test]
        public void Stamina_WalkingRecoversMoreThanItCosts_StandingStillMoreThanWalking_AndRunningNothing()
        {
            var t = new LocomotionTuning();
            Assert.AreEqual(0.6f, LocomotionDefaults.WalkRecoveryFactor, 1e-6f, "Phase 17 value");
            float walkInput = t.WalkThreshold * 0.5f;
            Assert.Greater(LocomotionStamina.RecoveryPerSecond(walkInput, 1f, t), LocomotionStamina.DrainPerSecond(walkInput, 1f, t), "a walking player gains stamina");
            Assert.Greater(LocomotionStamina.RecoveryPerSecond(0f, 1f, t), LocomotionStamina.RecoveryPerSecond(walkInput, 1f, t), "standing still recovers more than walking");
            Assert.AreEqual(0f, LocomotionStamina.RecoveryPerSecond(t.WalkThreshold + 0.1f, 1f, t));
            Assert.AreEqual(0f, LocomotionStamina.RecoveryPerSecond(1f, 1f, t));
        }

        [Test]
        public void Stamina_InAWholeArenaMatch_TheFieldPlayersAreNotEmptyAfterTheFirstQuarter_AndTheKeepersAreFresh()
        {
            AttackSim r = SimulateAttack(GameMode.Arena, 1721, true, 4, 9, 2f * MatchDefaults.ArenaPeriodSeconds, true, null, DifficultyLevel.Professional, DifficultyLevel.Professional);
            Assert.AreEqual(0, r.Issues);
            double later = 0, later_n = 0;
            for (int side = 0; side < 2; side++) for (int q = 1; q < 4; q++) { later += r.StaminaSum[side * 4 + q]; later_n += r.StaminaN[side * 4 + q]; }
            Assert.Greater(later / later_n, 11.0, "Phase 15 measured 8-11 in quarters 2-4 (walking recovery 0.25); with 0.6 the mean is about 16");
            double keeper = (r.KeeperStaminaSum[0] + r.KeeperStaminaSum[1]) / (r.KeeperStaminaN[0] + r.KeeperStaminaN[1]);
            Assert.Greater(keeper, 90.0, "the goalkeepers do not tire (they were never the problem)");
        }

        [Test]
        public void Stamina_TheLowSamplesAreCounted_AndTheShareOfVeryLowIsBelowThatOfLow()
        {
            AttackSim r = SimulateAttack(GameMode.Classic, 1731, true, 5, 2, 300f, true);
            Assert.GreaterOrEqual(r.LowN[0], r.VeryLowN[0]); Assert.GreaterOrEqual(r.LowN[1], r.VeryLowN[1]);
            Assert.Greater(r.StaminaN[0] + r.StaminaN[1], 100);
        }

        // ================= the quick battery =================

        [Test]
        public void TheBattery_PlaysEachSeedOnce_WhenBothTeamsAreTheSameLevel_AndTwiceWhenTheyDiffer()
        {
            int Rows(string csv) { return csv.Split('\n').Skip(1).Count(l => l.Length > 0); }
            Assert.AreEqual(2, Rows(BatteryCsv(GameMode.Arena, "Professional", "Professional", 1751, 2, 20f)), "two seeds, two different matches (the swapped arrangement would repeat each one exactly)");
            Assert.AreEqual(4, Rows(BatteryCsv(GameMode.Arena, "Elite", "Novice", 1751, 2, 20f)), "different levels: each seed is played with A at home and A away");
        }

        [Test]
        public void TheQuickBattery_GivesTheSameTextEveryTime_WithTheGoalRowsToo()
        {
            var goalsA = new System.Text.StringBuilder(); var goalsB = new System.Text.StringBuilder();
            string a = BatteryCsv(GameMode.Arena, "Professional", "Professional", 1741, 1, 300f, null, goalsA);
            string b = BatteryCsv(GameMode.Arena, "Professional", "Professional", 1741, 1, 300f, null, goalsB);
            Assert.AreEqual(a, b); Assert.AreEqual(goalsA.ToString(), goalsB.ToString());
            string[] header = a.Split('\n')[0].Split(',');
            foreach (string column in new[] { "h_keeperStamina", "h_stamLow20", "h_stamLow5", "h_goalsShot", "h_goalsRebound", "h_goalsDeflection", "h_goalsCross", "h_goalsUnknown", "h_goalsDirect", "h_goalsInferred", "h_goalsShotLogged" })
                CollectionAssert.Contains(header, column);
        }
    }
}
