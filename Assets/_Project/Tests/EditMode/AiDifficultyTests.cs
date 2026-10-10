using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 14: the difficulty of the AI is a way of CHOOSING (how often, how late, how clearly, how safely, how well placed, how well the legs are spent), never a change of the players. These tests check the
    /// profile that is read from each level, every mechanism on its own, that nothing is faked, and that the levels really play differently in the same two-minute matches.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static readonly DifficultyLevel[] AllLevels = { DifficultyLevel.Novice, DifficultyLevel.Amateur, DifficultyLevel.Professional, DifficultyLevel.Expert, DifficultyLevel.Elite };
        private static DecisionProfile ProfileOf14(DifficultyLevel l) { return DecisionProfile.From(DefaultDifficulties.Create(l)); }

        private static ActionCandidate Cand(CandidateType t, float score, int receiver = -1) { return new ActionCandidate { Type = t, Score = score, ReceiverIndex = receiver }; }

        // ================= the profile =================

        [Test]
        public void EveryLevel_BuildsAValidProfile_WithEveryNumberFiniteAndInRange()
        {
            foreach (DifficultyLevel l in AllLevels)
            {
                DecisionProfile p = ProfileOf14(l);
                foreach (FieldInfo f in typeof(DecisionProfile).GetFields().Where(f => f.FieldType == typeof(float)))
                {
                    float v = (float)f.GetValue(p);
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), l + "." + f.Name);
                }
                foreach (string q in new[] { "PassSelectionQuality", "ShotSelectionQuality", "DefensiveReactionQuality", "AnticipationQuality", "PositioningQuality", "PressureQuality", "SkillUsageQuality", "SituationAwareness", "StaminaManagement", "ErrorRate" })
                {
                    float v = (float)typeof(DecisionProfile).GetField(q).GetValue(p);
                    Assert.GreaterOrEqual(v, 0f, l + "." + q); Assert.LessOrEqual(v, 1f, l + "." + q);
                }
                Assert.AreEqual(l, p.Level);
            }
        }

        [Test]
        public void TheProfile_GetsBetterWithTheLevel_Monotonically()
        {
            DecisionProfile[] p = AllLevels.Select(ProfileOf14).ToArray();
            for (int i = 1; i < p.Length; i++)
            {
                foreach (string up in new[] { "PassSelectionQuality", "ShotSelectionQuality", "DefensiveReactionQuality", "AnticipationQuality", "PositioningQuality", "PressureQuality", "SkillUsageQuality", "SituationAwareness", "StaminaManagement" })
                    Assert.GreaterOrEqual((float)typeof(DecisionProfile).GetField(up).GetValue(p[i]), (float)typeof(DecisionProfile).GetField(up).GetValue(p[i - 1]), up + " at " + AllLevels[i]);
                foreach (string down in new[] { "ErrorRate", "MinimumDecisionMargin", "DecisionIntervalScale", "PerceptionDelaySeconds", "ReactionDelaySeconds", "PositionErrorMeters" })
                    Assert.LessOrEqual((float)typeof(DecisionProfile).GetField(down).GetValue(p[i]), (float)typeof(DecisionProfile).GetField(down).GetValue(p[i - 1]), down + " at " + AllLevels[i]);
            }
        }

        [Test]
        public void Elite_IsNotPerfect_AndNoviceIsNotHopeless()
        {
            DecisionProfile e = ProfileOf14(DifficultyLevel.Elite), n = ProfileOf14(DifficultyLevel.Novice);
            Assert.Greater(e.ErrorRate, 0f); Assert.Greater(e.PositionErrorMeters, 0f); Assert.Greater(e.ReactionDelaySeconds, 0f); Assert.Greater(e.PerceptionDelaySeconds, 0f); Assert.Greater(e.MinimumDecisionMargin, 0f);
            Assert.Less(e.PassSelectionQuality, 1f); Assert.Less(e.DecisionIntervalScale, 1f); Assert.GreaterOrEqual(e.DecisionIntervalScale * 0.32f, DifficultyRules.AbsoluteMinDecisionInterval * 0.5f);
            Assert.Less(n.ErrorRate, 0.5f, "even the worst level takes the better option most of the time");
            Assert.Greater(n.PassSelectionQuality, 0.2f); Assert.Greater(n.PositioningQuality, 0.2f);
            Assert.Less(n.DecisionIntervalScale, DecisionProfileDefaults.DecisionIntervalMaxScale + 0.001f);
            Assert.AreEqual(1f, ProfileOf14(DifficultyLevel.Professional).DecisionIntervalScale, 0.001f, "Professional is the reference rhythm");
        }

        [Test]
        public void TheProfile_HoldsNoAttributeAndNoBonus_AndCannotChangeThePlayers()
        {
            foreach (FieldInfo f in typeof(DecisionProfile).GetFields())
            {
                Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, f.Name); Assert.AreNotEqual(typeof(PlayerDefinition), f.FieldType, f.Name);
                foreach (string banned in new[] { "Speed", "Shooting", "Passing", "Defense", "BallControl", "Strength", "Agility", "Finishing", "Dribbling" }) Assert.IsFalse(f.Name == banned, f.Name);
            }
            // reading a level into a team changes nothing about its players
            MatchController c = RealRoles(3, 8, 311, MatchSettings.CreateArena());
            WorldView before = WorldState.Capture(c).View;
            var snapshot = new List<float>();
            for (int i = 0; i < before.PlayerCount; i++) { snapshot.Add(before[i].MaxSpeed); snapshot.Add(before[i].Profile.PassQuality); snapshot.Add(before[i].Profile.ShotQualityClose); snapshot.Add(before[i].Profile.TackleSkill); }
            var driver = new BasicDecisionDriver(c);
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Elite), Db.Players, c); driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Novice), Db.Players, c);
            WorldView after = WorldState.Capture(c).View; int k = 0;
            for (int i = 0; i < after.PlayerCount; i++) { Assert.AreEqual(snapshot[k++], after[i].MaxSpeed); Assert.AreEqual(snapshot[k++], after[i].Profile.PassQuality); Assert.AreEqual(snapshot[k++], after[i].Profile.ShotQualityClose); Assert.AreEqual(snapshot[k++], after[i].Profile.TackleSkill); }
        }

        // ================= the dice =================

        [Test]
        public void TheDice_AreDeterministic_InRange_AndDependOnlyOnTheirInputs()
        {
            for (int tick = 0; tick < 500; tick++)
            {
                float a = DecisionDice.Roll(DecisionDice.Choice, tick, 3), b = DecisionDice.Roll(DecisionDice.Choice, tick, 3);
                Assert.AreEqual(a, b); Assert.GreaterOrEqual(a, 0f); Assert.Less(a, 1f);
            }
            Assert.AreNotEqual(DecisionDice.Roll(DecisionDice.Choice, 10, 3), DecisionDice.Roll(DecisionDice.Choice, 10, 4));
            Assert.AreNotEqual(DecisionDice.Roll(DecisionDice.Choice, 10, 3), DecisionDice.Roll(DecisionDice.Drift, 10, 3));
            double mean = 0; for (int i = 0; i < 4000; i++) mean += DecisionDice.Roll(DecisionDice.Choice, i, i % 12); mean /= 4000;
            Assert.AreEqual(0.5, mean, 0.03, "evenly spread");
        }

        // ================= the mistakes =================

        private static int Mistakes(DecisionProfile p, float gap, float pressure = 0f, float stamina01 = 1f, int ticks = 3000)
        {
            int n = 0;
            for (int tick = 0; tick < ticks; tick++) if (p.ChoosesSecond(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Dribble, 3f - gap), pressure, stamina01, 0.5f, tick, tick % 10)) n++;
            return n;
        }

        [Test]
        public void ALevelThatErrsLess_ErrsLess_AtTheSameGap()
        {
            int previous = int.MaxValue;
            foreach (DifficultyLevel l in AllLevels)
            {
                int n = Mistakes(ProfileOf14(l), 0.05f);
                Assert.LessOrEqual(n, previous, l.ToString()); previous = n;
            }
            Assert.Greater(Mistakes(ProfileOf14(DifficultyLevel.Novice), 0.05f), 5 * Mistakes(ProfileOf14(DifficultyLevel.Elite), 0.05f), "the worst errs far more than the best");
            Assert.Greater(Mistakes(ProfileOf14(DifficultyLevel.Elite), 0f, 0f, 1f, 20000), 0, "and the best still errs sometimes");
        }

        [Test]
        public void AMistake_IsAlwaysASmallOne_NeverAChoiceFarWorseThanTheBest()
        {
            DecisionProfile n = ProfileOf14(DifficultyLevel.Novice);
            Assert.AreEqual(0, Mistakes(n, n.Tuning.ErrorMaxScoreGap + 0.01f, 1f, 0f), "beyond the gap he never takes the worse option, however tired and pressed");
            Assert.AreEqual(0, Mistakes(n, 2f));
            Assert.Greater(Mistakes(n, 0f), Mistakes(n, n.Tuning.ErrorMaxScoreGap * 0.9f), "the closer the options, the more likely the slip");
        }

        [Test]
        public void PressureAndTiredness_RaiseTheChanceOfAMistake_AndSoDoesAWeakerPlayer()
        {
            DecisionProfile p = ProfileOf14(DifficultyLevel.Professional);
            Assert.Greater(Mistakes(p, 0.05f, 1f, 1f), Mistakes(p, 0.05f, 0f, 1f));
            Assert.Greater(Mistakes(p, 0.05f, 0f, 0f), Mistakes(p, 0.05f, 0f, 1f));
            int weak = 0, strong = 0;
            for (int tick = 0; tick < 3000; tick++)
            {
                if (p.ChoosesSecond(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Dribble, 2.95f), 0f, 1f, 0.1f, tick, 1)) weak++;
                if (p.ChoosesSecond(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Dribble, 2.95f), 0f, 1f, 0.9f, tick, 1)) strong++;
            }
            Assert.Greater(weak, strong, "a player who is bad at the action chosen slips more often");
        }

        [Test]
        public void TheMistakes_AreClassified_ByWhatWasTakenInsteadOfWhatWasBest()
        {
            Assert.AreEqual(DecisionErrorKind.WrongReceiver, DecisionProfile.Classify(Cand(CandidateType.Pass, 3f, 1), Cand(CandidateType.Pass, 2.9f, 2)));
            Assert.AreEqual(DecisionErrorKind.BadPass, DecisionProfile.Classify(Cand(CandidateType.Dribble, 3f), Cand(CandidateType.Pass, 2.9f, 2)));
            Assert.AreEqual(DecisionErrorKind.BadShot, DecisionProfile.Classify(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Shoot, 2.9f)));
            Assert.AreEqual(DecisionErrorKind.UnnecessaryDribble, DecisionProfile.Classify(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Dribble, 2.9f)));
            Assert.AreEqual(DecisionErrorKind.ExcessiveRisk, DecisionProfile.Classify(Cand(CandidateType.Pass, 3f), Cand(CandidateType.ThroughPass, 2.9f)));
            Assert.AreEqual(DecisionErrorKind.Passive, DecisionProfile.Classify(Cand(CandidateType.Pass, 3f), Cand(CandidateType.Hold, 2.9f)));
        }

        // ================= the game: score and clock =================

        private static WorldMatch MatchAt(int home, int away, float remaining) { return new WorldMatch { HomeGoals = home, AwayGoals = away, PeriodRemaining = remaining }; }

        [Test]
        public void TheUrgency_AsksForRiskWhenBehindLate_AndForCareWhenAheadLate_AndNothingEarly()
        {
            DecisionProfile p = ProfileOf14(DifficultyLevel.Elite);
            Assert.Greater(p.Urgency(MatchAt(0, 1, 5f), TeamSide.Home), 0.3f); Assert.Less(p.Urgency(MatchAt(2, 0, 5f), TeamSide.Home), -0.5f);
            Assert.AreEqual(0f, p.Urgency(MatchAt(0, 1, 500f), TeamSide.Home), 0.001f, "early on the score does not matter");
            Assert.AreEqual(0f, p.Urgency(MatchAt(1, 1, 5f), TeamSide.Home), 0.001f, "level: nothing to chase or protect");
            Assert.Greater(p.Urgency(MatchAt(1, 0, 5f), TeamSide.Away), 0f, "the same score is the opposite for the other team");
            Assert.Greater(Math.Abs(p.Urgency(MatchAt(0, 2, 5f), TeamSide.Home)), Math.Abs(p.Urgency(MatchAt(0, 1, 5f), TeamSide.Home)), "a bigger gap asks for more");
            Assert.Greater(Math.Abs(p.Urgency(MatchAt(0, 1, 5f), TeamSide.Home)), Math.Abs(p.Urgency(MatchAt(0, 1, 60f), TeamSide.Home)), "less time asks for more");
            Assert.Greater(Math.Abs(p.Urgency(MatchAt(0, 1, 5f), TeamSide.Home)), Math.Abs(ProfileOf14(DifficultyLevel.Novice).Urgency(MatchAt(0, 1, 5f), TeamSide.Home)), "a better level reads the game better");
        }

        [Test]
        public void TheBias_FavoursRiskWhenChasingAndSafetyWhenProtectingALead_AndTheSkillOfThePlayer()
        {
            DecisionProfile p = ProfileOf14(DifficultyLevel.Elite);
            MatchController c = CarrierScene(5f, 0f); WorldView v = ViewOf(c); WorldPlayer me = v[v.IndexOf("hs3")];
            ICandidateBias chasing = p.BiasFor(me, MatchAt(0, 2, 5f), 11f, 15f), protecting = p.BiasFor(me, MatchAt(2, 0, 5f), 11f, 15f), neutral = p.BiasFor(me, MatchAt(0, 0, 500f), 11f, 15f);
            var summary = new CarrierSummary();
            Assert.Greater(chasing.Bias(Cand(CandidateType.ThroughPass, 3f), summary), neutral.Bias(Cand(CandidateType.ThroughPass, 3f), summary));
            Assert.Greater(chasing.Bias(Cand(CandidateType.Shoot, 3f), summary), neutral.Bias(Cand(CandidateType.Shoot, 3f), summary));
            Assert.Less(protecting.Bias(Cand(CandidateType.ThroughPass, 3f), summary), neutral.Bias(Cand(CandidateType.ThroughPass, 3f), summary));
            Assert.Greater(protecting.Bias(Cand(CandidateType.Pass, 3f), summary), neutral.Bias(Cand(CandidateType.Pass, 3f), summary));
            Assert.Greater(protecting.Bias(Cand(CandidateType.Hold, 3f), summary), neutral.Bias(Cand(CandidateType.Hold, 3f), summary));
            // the same pass is worth more to a good passer than to a bad one
            WorldPlayer good = me, bad = me; good.Profile.PassQuality = 0.9f; bad.Profile.PassQuality = 0.2f;
            Assert.Greater(p.BiasFor(good, MatchAt(0, 0, 500f), 11f, 15f).Bias(Cand(CandidateType.Pass, 3f), summary), p.BiasFor(bad, MatchAt(0, 0, 500f), 11f, 15f).Bias(Cand(CandidateType.Pass, 3f), summary));
            // and a level that selects its passes badly dribbles too much
            Assert.Greater(ProfileOf14(DifficultyLevel.Novice).BiasFor(me, MatchAt(0, 0, 500f), 11f, 15f).Bias(Cand(CandidateType.Dribble, 2f), summary), ProfileOf14(DifficultyLevel.Elite).BiasFor(me, MatchAt(0, 0, 500f), 11f, 15f).Bias(Cand(CandidateType.Dribble, 2f), summary));
        }

        // ================= without the ball =================

        [Test]
        public void ThePositionDrift_IsSmall_Slow_Deterministic_AndSmallerForABetterLevel()
        {
            DecisionProfile n = ProfileOf14(DifficultyLevel.Novice), e = ProfileOf14(DifficultyLevel.Elite);
            float max = n.PositionErrorMeters * n.Tuning.PositionErrorShare * (1f - n.PositioningQuality);
            double sumN = 0, sumE = 0;
            for (int i = 0; i < 400; i++)
            {
                Vec2 a = n.PositionDrift(i % 10, i * 0.7f, out float mn), b = n.PositionDrift(i % 10, i * 0.7f, out _);
                Assert.AreEqual(a.X, b.X); Assert.AreEqual(a.Y, b.Y); Assert.LessOrEqual(a.Magnitude, max + 0.001f); sumN += a.Magnitude;
                sumE += e.PositionDrift(i % 10, i * 0.7f, out _).Magnitude;
            }
            Assert.Greater(sumN, sumE);
            Vec2 early = n.PositionDrift(3, 10.0f, out _), same = n.PositionDrift(3, 10.5f, out _);
            Assert.AreEqual(early.X, same.X, "the drift holds for the whole period (it is not a jitter)");
        }

        [Test]
        public void TheEffortCap_FallsWithStamina_ToAWalk_AndABetterManagerSavesEarlier()
        {
            DecisionProfile e = ProfileOf14(DifficultyLevel.Elite), n = ProfileOf14(DifficultyLevel.Novice);
            Assert.AreEqual(1f, e.EffortCap(100f), 0.001f); Assert.AreEqual(1f, n.EffortCap(100f), 0.001f);
            Assert.Less(e.EffortCap(0f), LocomotionDefaults.WalkThreshold, "with nothing left the movement is a walk, which lets him recover");
            float last = 0f; for (float st = 0f; st <= 100f; st += 5f) { Assert.GreaterOrEqual(e.EffortCap(st), last); last = e.EffortCap(st); }
            Assert.Less(e.EffortCap(50f), n.EffortCap(50f), "the better manager already saves his legs at half stamina");
        }

        [Test]
        public void TheDefenceOfALevel_IsTheCommonOneRetuned_AndArenaKeepsItsAggression()
        {
            var common = DefenseTuning.ForMode(GameMode.Classic); float before = common.InterceptHorizonSeconds, risk = common.ChallengeMaxRisk;
            DefenseTuning n = ProfileOf14(DifficultyLevel.Novice).TuneDefense(common, GameMode.Classic), e = ProfileOf14(DifficultyLevel.Elite).TuneDefense(common, GameMode.Classic);
            Assert.AreEqual(before, common.InterceptHorizonSeconds); Assert.AreEqual(risk, common.ChallengeMaxRisk, "the common tuning is not changed");
            Assert.Greater(n.ChallengeMaxRisk, e.ChallengeMaxRisk, "the careless defender risks fouls the careful one does not");
            Assert.Greater(e.InterceptHorizonSeconds, n.InterceptHorizonSeconds, "a better anticipation reads further ahead");
            foreach (DifficultyLevel l in AllLevels)
            {
                DefenseTuning a = ProfileOf14(l).TuneDefense(DefenseTuning.ForMode(GameMode.Arena), GameMode.Arena);
                Assert.AreEqual(DefenseDefaults.ChallengeMaxRisk, a.ChallengeMaxRisk, l + ": Arena is never held back"); Assert.AreEqual(DefenseDefaults.ChallengeDistance, a.ChallengeDistance); Assert.AreEqual(DefenseDefaults.TackleDistance, a.TackleDistance);
            }
        }

        // ================= the engine and the driver =================

        [Test]
        public void TheEngine_KeepsTheTeamsApart_AProfileForOneNeverChangesTheOther()
        {
            MatchController c = RealRoles(3, 8, 312, MatchSettings.CreateClassic()); c.ConfirmRestart();
            var engine = new BasicDecisionEngine(c.Pitch);
            engine.SetProfile(TeamSide.Home, ProfileOf14(DifficultyLevel.Novice));
            Assert.IsNotNull(engine.ProfileOf(TeamSide.Home)); Assert.IsNull(engine.ProfileOf(TeamSide.Away));
            var plain = new BasicDecisionEngine(c.Pitch);
            WorldView v = WorldState.Capture(c).View;
            for (int i = v.TeamStart(TeamSide.Away); i < v.TeamStart(TeamSide.Away) + v.TeamCount(TeamSide.Away); i++)
            {
                PlayerDecision a = engine.Decide(v, v[i].PlayerId), b = plain.Decide(v, v[i].PlayerId);
                Assert.AreEqual(b.Kind, a.Kind); Assert.AreEqual(b.Movement.Magnitude, a.Movement.Magnitude); Assert.AreEqual(b.Movement.Direction.X, a.Movement.Direction.X);
            }
            engine.SetProfile(TeamSide.Home, null); Assert.IsNull(engine.ProfileOf(TeamSide.Home));
        }

        [Test]
        public void TheDriver_GivesEachTeamItsOwnRhythm_AndNoLevelMeansTheOldRhythm()
        {
            MatchController c = RealRoles(3, 8, 313, MatchSettings.CreateArena());
            var driver = new BasicDecisionDriver(c);
            int baseSteps = driver.DecisionSteps;
            Assert.AreEqual(baseSteps, driver.DecisionStepsOf(TeamSide.Home)); Assert.AreEqual(baseSteps, driver.DecisionStepsOf(TeamSide.Away));
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Novice)); driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Elite));
            Assert.Greater(driver.DecisionStepsOf(TeamSide.Home), baseSteps); Assert.Less(driver.DecisionStepsOf(TeamSide.Away), baseSteps);
            driver.SetDifficulty(TeamSide.Home, null); Assert.AreEqual(baseSteps, driver.DecisionStepsOf(TeamSide.Home));
        }

        [Test]
        public void ALevelThatReactsSlowly_NoticesTurnoversLate_AndCommitsContestsLate_AndNoLevelMeansNeither()
        {
            BasicDecisionDriver none = null, novice = null;
            SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 60f, true, null, null, null); none = LastDriver;
            SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 60f, true, null, DifficultyLevel.Novice, DifficultyLevel.Novice); novice = LastDriver;
            Assert.AreEqual(0, none.LateNoticed); Assert.AreEqual(0, none.LateContests); Assert.AreEqual(0.0, none.ReactionDelaySum);
            Assert.Greater(novice.LateNoticed, 20); Assert.Greater(novice.LateContests, 0); Assert.Greater(novice.ReactionDelaySum, 0.0);
        }

        // ================= the matches =================

        private static AttackSim Match14(GameMode mode, int seed, DifficultyLevel? home, DifficultyLevel? away, AiDiagnostics d = null, float seconds = 90f)
        {
            return SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, seconds, true, d, home, away);
        }

        [Test]
        public void EveryLevel_PlaysAWholeMatch_InBothModes_WithTheSameCore_AndNothingBreaks()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                foreach (DifficultyLevel l in AllLevels)
                {
                    AttackSim r = Match14(mode, mode == GameMode.Arena ? 303 : 323, l, l);
                    Assert.AreEqual(0, r.Issues, mode + " " + l + ": twelve players, finite, in the pitch, stamina in range, possession valid");
                    Assert.Greater(r.Decisions, 100, mode + " " + l); Assert.Greater(r.Possessions, 2, mode + " " + l); Assert.Greater(r.Passes, 3, mode + " " + l);
                    Assert.IsInstanceOf<BasicDecisionEngine>(LastDriver.Engine);
                }
        }

        [Test]
        public void ThePhase14Matches_AreTheSameEveryTime_AndTheObserverChangesNothing()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                int seed = mode == GameMode.Arena ? 304 : 324;
                string a = Match14(mode, seed, DifficultyLevel.Elite, DifficultyLevel.Novice).Fingerprint;
                Assert.AreEqual(a, Match14(mode, seed, DifficultyLevel.Elite, DifficultyLevel.Novice).Fingerprint, mode + " repeats");
                Assert.AreEqual(a, Match14(mode, seed, DifficultyLevel.Elite, DifficultyLevel.Novice, NewDiagnostics()).Fingerprint, mode + ": the observer changes nothing");
                Assert.AreEqual(a, Match14(mode, seed, DifficultyLevel.Elite, DifficultyLevel.Novice, NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away })).Fingerprint, mode + ": nor does a one-team observer");
            }
        }

        [Test]
        public void DifferentLevels_PlayDifferently_AndTheSameLevelPlaysTheSame()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                int seed = mode == GameMode.Arena ? 305 : 325;
                var prints = new HashSet<string>(AllLevels.Select(l => Match14(mode, seed, l, l).Fingerprint));
                Assert.AreEqual(5, prints.Count, mode + ": five levels, five different matches");
                Assert.AreNotEqual(Match14(mode, seed, null, null).Fingerprint, Match14(mode, seed, DifficultyLevel.Professional, DifficultyLevel.Professional).Fingerprint, "a level is not the same as no level");
            }
        }

        private static AiDiagnostics Aggregate14(GameMode mode, DifficultyLevel level, int matches, float seconds = 90f)
        {
            var sum = NewDiagnostics();
            int first = mode == GameMode.Arena ? 301 : 321;
            for (int k = 0; k < matches; k++)
            {
                int seed = first + k;
                var one = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home });
                SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, seconds, true, one, level, DifficultyLevel.Professional);
                sum.Merge(one);
            }
            return sum;
        }

        [Test]
        public void TheMistakesPerDecision_FallWithTheLevel_InBothModes()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var rates = new List<double>();
                foreach (DifficultyLevel l in AllLevels)
                {
                    AiDiagnostics d = Aggregate14(mode, l, 6);
                    Assert.Greater(d.CarrierDecisions, 100.0);
                    rates.Add(d.MistakeTotal / Math.Max(1.0, d.CarrierDecisions));
                }
                for (int i = 1; i < rates.Count; i++) Assert.Less(rates[i], rates[i - 1] + 0.004, mode + " " + AllLevels[i] + ": never clearly worse than the level below");
                Assert.Greater(rates[0], 3.0 * rates[4], mode + ": the worst errs far more than the best");
                // the errors of a tired player are almost all of the mistakes, and since Phase 17 (walking recovery 0.6) nobody is tired in 90 s: the best level is checked over longer matches
                AiDiagnostics best = Aggregate14(mode, AllLevels[4], 3, 360f);
                Assert.Greater(best.CarrierDecisions, 100.0);
                Assert.Greater(best.MistakeTotal, 0.0, mode + ": and the best still errs");
            }
        }

        [Test]
        public void ASlowerLevel_DecidesLessOften_AndNoticesLater_ThanAFasterOne()
        {
            AiDiagnostics n = Aggregate14(GameMode.Arena, DifficultyLevel.Novice, 3), e = Aggregate14(GameMode.Arena, DifficultyLevel.Elite, 3);
            Assert.Less(n.Decisions, e.Decisions);
            Assert.Greater(n.MistakeGapSum(DecisionErrorKind.LateReaction) / Math.Max(1.0, n.MistakeCount(DecisionErrorKind.LateReaction)), e.MistakeGapSum(DecisionErrorKind.LateReaction) / Math.Max(1.0, e.MistakeCount(DecisionErrorKind.LateReaction)), "a novice takes longer to notice a turnover");
        }

        [Test]
        public void Arena_StaysAggressiveAtEveryLevel_AndHasNoFouls()
        {
            foreach (DifficultyLevel l in AllLevels)
            {
                AiDiagnostics d = Aggregate14(GameMode.Arena, l, 3);
                Assert.AreEqual(0.0, d.TotalFouls, l.ToString());
                Assert.Greater(d.TotalContacts, 5.0, l + ": contacts still happen"); Assert.Greater(d.DuelRequests(2), 5.0, l + ": the challenge is still made");
            }
        }

        [Test]
        public void Classic_KeepsItsFoulsLimited_AtEveryLevel_AndTheyAreStillPossible()
        {
            double fouls = 0, contacts = 0;
            foreach (DifficultyLevel l in AllLevels) { AiDiagnostics d = Aggregate14(GameMode.Classic, l, 3); fouls += d.TotalFouls; contacts += d.TotalContacts; Assert.Less(d.TotalFouls, d.TotalContacts * 0.5 + 1.0, l.ToString()); }   // Phase 21: the referee calls every late contact (before: only the clumsy ones from behind, under 20 %); a minority of the contacts is still the rule
            Assert.Greater(contacts, 30.0);
        }

        [Test]
        public void TheTwoObservers_OfTheSameMatch_AddUpToTheWholeOne()
        {
            var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home });
            var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            var whole = NewDiagnostics();
            Match14(GameMode.Arena, 306, DifficultyLevel.Amateur, DifficultyLevel.Expert, whole, 90f);
            Match14(GameMode.Arena, 306, DifficultyLevel.Amateur, DifficultyLevel.Expert, home, 90f);
            Match14(GameMode.Arena, 306, DifficultyLevel.Amateur, DifficultyLevel.Expert, away, 90f);
            Assert.AreEqual(whole.Decisions, home.Decisions + away.Decisions);
            Assert.AreEqual(whole.CarrierDecisions, home.CarrierDecisions + away.CarrierDecisions);
            Assert.AreEqual(whole.PassAttempts, home.PassAttempts + away.PassAttempts);
            Assert.AreEqual(whole.ShotAttempts, home.ShotAttempts + away.ShotAttempts);
            Assert.AreEqual(whole.Goals, home.Goals + away.Goals);
            Assert.AreEqual(whole.MistakeTotal, home.MistakeTotal + away.MistakeTotal);
            // and the twin feeds a second observer of the same match
            var h2 = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var a2 = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            SimulateAttack(GameMode.Arena, 306, true, 306 % 12, (306 * 5 + 3) % 12, 90f, true, h2, DifficultyLevel.Amateur, DifficultyLevel.Expert, a2);
            Assert.AreEqual(home.Decisions, h2.Decisions); Assert.AreEqual(away.Decisions, a2.Decisions);
        }

        // ================= the Core: a kicker does not take his own kick back =================

        [Test]
        public void TheBall_NeverComesBackToThePasserInTheNextInstants_UnlessTheRuleIsSwitchedOff()
        {
            var on = NewDiagnostics(); SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, true, on);
            Assert.AreEqual(0.0, on.ReturnedBucket(0) + on.ReturnedBucket(1), "no pass returns to its passer within a quarter of a second");
            string before = Environment.GetEnvironmentVariable("FS27_EXP");
            try
            {
                Environment.SetEnvironmentVariable("FS27_EXP", "*.Actions.KickerKeepsOffHisKick=0");
                var off = NewDiagnostics(); SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, true, off);
                Assert.Greater(off.ReturnedBucket(0) + off.ReturnedBucket(1), 0.0, "(with the rule off the old quirk is back: the guard is real)");
            }
            finally { Environment.SetEnvironmentVariable("FS27_EXP", before); }
        }

        // ================= no loose numbers, no per-level branches =================

        [Test]
        public void TheDifficultyNumbers_LiveInOnePlace_NoLooseNumbers_NoBranchOnTheLevel_NoBannedWords()
        {
            foreach (FieldInfo f in typeof(DecisionProfileDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                FieldInfo mirror = typeof(DecisionProfileTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(new DecisionProfileTuning())), f.Name);
            }
            foreach (FieldInfo f in typeof(DecisionProfileTuning).GetFields()) Assert.IsNotNull(typeof(DecisionProfileDefaults).GetField(f.Name), f.Name + " has no default");
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string name in new[] { "DecisionProfile.cs", "DecisionProfile.Play.cs", "BasicDecisionEngine.cs", "BasicDecisionDriver.cs" })
            {
                string text = File.ReadAllText(Path.Combine(core, "Decision", name));
                if (name != "DecisionProfile.cs")
                    foreach (string raw in File.ReadAllLines(Path.Combine(core, "Decision", name)))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("//")) continue;
                        foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(name + ": loose number " + m.Value + " in: " + line);
                    }
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "Linq" }) StringAssert.DoesNotContain(banned, text, name);
                foreach (string level in new[] { "DifficultyLevel.Novice", "DifficultyLevel.Amateur", "DifficultyLevel.Professional", "DifficultyLevel.Expert", "DifficultyLevel.Elite" }) StringAssert.DoesNotContain(level, text, name + " must not branch on a level");
            }
            string engine = File.ReadAllText(Path.Combine(core, "Decision", "BasicDecisionEngine.cs"));
            StringAssert.DoesNotContain("PlayerDefinition", engine); StringAssert.DoesNotContain("PlayerAttributes", engine);
        }
    }
}
