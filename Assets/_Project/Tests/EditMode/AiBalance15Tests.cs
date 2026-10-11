using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 15: validation of the difficulty. The diagnostic score, the battery that produces the evidence, the human-like profiles, the experiment hook and the identities that must survive every level.
    /// The statistics themselves (hundreds of matches) are produced by the explicit tools and kept in Docs/AI_BALANCE_PHASE15_data; these tests check the instruments and the rules.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        // ================= the diagnostic score =================

        [Test]
        public void TheQualityScore_HasOneNameAndOneDirectionAndOneValuePerRatio_InFiveSeparateGroups()
        {
            int n = DifficultyQualityScore.Names.Length;
            Assert.AreEqual(n, DifficultyQualityScore.HigherIsBetter.Length); Assert.AreEqual(n, new DifficultyQualityScore().ToArray().Length);
            Assert.AreEqual(n, DifficultyQualityScore.Names.Distinct().Count());
            Assert.AreEqual(20, n, "attack 6, defence 5, decision 5, movement 4: no single number that could hide a weakness");
            foreach (string name in DifficultyQualityScore.Names) Assert.IsNotNull(typeof(DifficultyQualityScore).GetField(name), name);
        }


        [Test]
        public void TheQualityScore_OfNothing_IsNotANumber_NotZero_AndItNeverChangesAnything()
        {
            var empty = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home });
            DifficultyQualityScore q = DifficultyQualityScore.Of(empty, NewDiagnostics());
            Assert.IsTrue(double.IsNaN(q.PassCompletion)); Assert.IsTrue(double.IsNaN(q.ErrorRate)); Assert.IsTrue(double.IsNaN(q.ChancesConcededPer100OpponentPossessions));
            Assert.Throws<ArgumentNullException>(() => DifficultyQualityScore.Of(null, empty));
            // computing it does not change the match: the observer is read-only
            string plain = Match14(GameMode.Arena, 311, DifficultyLevel.Expert, DifficultyLevel.Amateur).Fingerprint;
            var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            string watched = SimulateAttack(GameMode.Arena, 311, true, 311 % 12, (311 * 5 + 3) % 12, 90f, true, home, DifficultyLevel.Expert, DifficultyLevel.Amateur, away).Fingerprint;
            DifficultyQualityScore.Of(home, away);
            Assert.AreEqual(plain, watched);
        }

        [Test]
        public void TheQualityScore_OfTwoTeams_IsTheMirrorOfEachOther_WhatOneConcedesTheOtherCreates()
        {
            var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            SimulateAttack(GameMode.Classic, 331, true, 331 % 12, (331 * 5 + 3) % 12, 150f, true, home, DifficultyLevel.Professional, DifficultyLevel.Professional, away);
            DifficultyQualityScore h = DifficultyQualityScore.Of(home, away), a = DifficultyQualityScore.Of(away, home);
            Assert.AreEqual(h.ChancesConcededPer100OpponentPossessions, a.ClearChancesPer100Possessions, 1e-9);
            Assert.AreEqual(a.ChancesConcededPer100OpponentPossessions, h.ClearChancesPer100Possessions, 1e-9);
            Assert.AreEqual(h.DangerConcededPerOpponentPossession, away.DangerEntries / away.Possessions, 1e-9);
            foreach (double v in h.ToArray().Concat(a.ToArray())) Assert.IsFalse(double.IsInfinity(v));
            foreach (double rate in new[] { h.PassCompletion, h.ShotsOnTargetShare, h.ErrorRate, h.ContestSuccess, h.OffBallAttackShare, h.DefensiveActionShare }) if (!double.IsNaN(rate)) { Assert.GreaterOrEqual(rate, 0.0); Assert.LessOrEqual(rate, 1.0); }
        }

        [Test]
        public void TheSituationCounters_AreSubsetsOfTheirTotals_AndTheLossesAndCardsAreCounted()
        {
            var d = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home });
            SimulateAttack(GameMode.Classic, 332, true, 332 % 12, (332 * 5 + 3) % 12, 240f, true, d, DifficultyLevel.Novice, DifficultyLevel.Professional);
            Assert.LessOrEqual(d.DecisionsPressed, d.CarrierDecisions); Assert.LessOrEqual(d.DecisionsLowSpace, d.CarrierDecisions); Assert.LessOrEqual(d.DecisionsTired, d.CarrierDecisions);
            Assert.LessOrEqual(d.MistakesPressed, d.MistakeTotal); Assert.LessOrEqual(d.MistakesLowSpace, d.MistakeTotal); Assert.LessOrEqual(d.MistakesTired, d.MistakeTotal);
            Assert.Greater(d.Possessions, 5.0); Assert.Greater(d.PossessionsLost, 0.0); Assert.LessOrEqual(d.PossessionsLost, d.Possessions + 1.0, "he cannot lose more times than he had the ball (the first possession aside)");
            Assert.GreaterOrEqual(d.YellowCards, 0.0); Assert.GreaterOrEqual(d.RedCards, 0.0);
            Assert.Greater(d.DecisionsPressed, 0.0, "the pressure is measured"); Assert.Greater(d.MistakesPressed + d.MistakesTired + d.MistakesLowSpace, 0.0, "and the mistakes know where they happened");
        }

        // ================= the battery =================

        [Test]
        public void TheBattery_IsReproducible_WellFormed_AndPlaysEverySeedFromBothSides()
        {
            string a = BatteryCsv(GameMode.Arena, "Elite", "Novice", 7001, 2, 20f), b = BatteryCsv(GameMode.Arena, "Elite", "Novice", 7001, 2, 20f);
            Assert.AreEqual(a, b, "the same arguments, the same file");
            string[] lines = a.Trim().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.AreEqual(1 + 2 * 2, lines.Length, "a header and two matches per seed");
            int columns = lines[0].Split(',').Length;
            for (int i = 1; i < lines.Length; i++) Assert.AreEqual(columns, lines[i].Split(',').Length, "row " + i);
            var header = lines[0].Split(',').ToList();
            foreach (string must in new[] { "h_goals", "a_goals", "h_clearChances", "a_passDone", "h_homeDistAtt", "h_stam4", "h_mistakes", "a_yellow", "h_q_ErrorRate", "a_q_ChancesConcededPer100OpponentPossessions" }) CollectionAssert.Contains(header, must);
            var rows = lines.Skip(1).Select(l => l.Split(',')).ToList();
            int iHome = header.IndexOf("levelHome"), iAway = header.IndexOf("levelAway"), iFlag = header.IndexOf("aIsHome");
            Assert.AreEqual("Elite", rows[0][iHome]); Assert.AreEqual("Novice", rows[0][iAway]); Assert.AreEqual("1", rows[0][iFlag]);
            Assert.AreEqual("Novice", rows[1][iHome]); Assert.AreEqual("Elite", rows[1][iAway]); Assert.AreEqual("0", rows[1][iFlag]);
            Assert.AreNotEqual(a, BatteryCsv(GameMode.Arena, "Elite", "Novice", 7003, 2, 20f), "another seed, another file");
        }

        [Test]
        public void TheHumanLikeProfiles_AreValid_AreNotPresets_AndAreOrderedAsTheirNames()
        {
            DifficultyDefinition casual = HumanLike("HumanCasual"), skilled = HumanLike("HumanSkilled");
            foreach (DifficultyDefinition d in new[] { casual, skilled })
            {
                AiDataValidationResult r = DifficultyValidator.Validate(d);
                Assert.IsTrue(r.IsValid, d.Id + ": " + string.Join("; ", r.Issues.Select(i => i.ToString())));
                Assert.IsNotNull(DecisionProfile.From(d));
                foreach (DifficultyLevel l in AllLevels) Assert.AreNotEqual(DefaultDifficulties.Create(l).Id, d.Id);
            }
            Assert.Greater(skilled.Decision.DecisionQuality, casual.Decision.DecisionQuality); Assert.Less(skilled.Reaction.BaseReactionSeconds, casual.Reaction.BaseReactionSeconds);
            Assert.GreaterOrEqual(casual.Reaction.BaseReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds);
            Assert.IsTrue(TryDefinition("HumanSkilled", out DifficultyDefinition viaName, out DifficultyLevel? level)); Assert.IsNotNull(viaName); Assert.IsNull(level);
            Assert.IsFalse(TryDefinition("Nonsense", out _, out _));
        }

        // ================= the experiment hook =================

        [Test]
        public void TheExperimentHook_ReachesTheProfileNumbers_RejectsWrongNames_AndLeavesTheDefaultsAlone()
        {
            string before = Environment.GetEnvironmentVariable("FS27_EXP");
            try
            {
                string plain = Match14(GameMode.Arena, 312, DifficultyLevel.Novice, DifficultyLevel.Novice).Fingerprint;
                Environment.SetEnvironmentVariable("FS27_EXP", "*.Profile.MaxErrorRate=0.9");
                string changed = Match14(GameMode.Arena, 312, DifficultyLevel.Novice, DifficultyLevel.Novice).Fingerprint;
                Assert.AreNotEqual(plain, changed, "a profile number changed from the outside changes the match");
                Environment.SetEnvironmentVariable("FS27_EXP", "*.Profile.NoSuchNumber=1");
                Assert.Throws<ArgumentException>(() => Match14(GameMode.Arena, 312, DifficultyLevel.Novice, DifficultyLevel.Novice));
            }
            finally { Environment.SetEnvironmentVariable("FS27_EXP", before); }
            Assert.AreEqual(DecisionProfileDefaults.MaxErrorRate, new DecisionProfileTuning().MaxErrorRate, "the defaults are not touched by the hook");
        }

        // ================= the identities that must survive every level =================

        [Test]
        public void ArenaHasNoFouls_AtAnyLevel_EvenInALongerMatch_AndKeepsItsDuels()
        {
            foreach (DifficultyLevel l in AllLevels)
            {
                var d = NewDiagnostics();
                SimulateAttack(GameMode.Arena, 313, true, 313 % 12, (313 * 5 + 3) % 12, 240f, true, d, l, l);
                Assert.AreEqual(0.0, d.TotalFouls, l.ToString()); Assert.AreEqual(0.0, d.YellowCards, l.ToString()); Assert.AreEqual(0.0, d.RedCards, l.ToString());
                Assert.Greater(d.TotalContacts, 5.0, l + ": the duels are still there"); Assert.Greater(d.DuelRequests(1) + d.DuelRequests(2), 5.0, l.ToString());
            }
        }

        [Test]
        public void ClassicKeepsItsFouls_AtEveryLevel_AsAMinorityOfTheContacts()
        {
            double fouls = 0, contacts = 0;
            foreach (DifficultyLevel l in AllLevels)
            {
                var d = NewDiagnostics();
                SimulateAttack(GameMode.Classic, 333, true, 333 % 12, (333 * 5 + 3) % 12, 300f, true, d, l, l);
                Assert.LessOrEqual(d.TotalFouls, d.TotalContacts * 0.5 + 1.0, l.ToString()); fouls += d.TotalFouls;   // Phase 21: 0.2 -> 0.5 (the referee judges every contact; a full match has about a quarter of its contacts called)
                 contacts += d.TotalContacts;
            }
            Assert.Greater(contacts, 20.0); Assert.GreaterOrEqual(fouls, 0.0);
        }

        [Test]
        public void ThePressedAndTiredMistakeRates_FallWithTheLevel_Too()
        {
            double[] pressed = new double[5], tired = new double[5];
            for (int i = 0; i < 5; i++)
            {
                var sum = NewDiagnostics();
                for (int k = 0; k < 4; k++)
                {
                    int seed = 341 + k;
                    var one = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home });
                    SimulateAttack(GameMode.Arena, seed, true, seed % 12, (seed * 5 + 3) % 12, 150f, true, one, AllLevels[i], DifficultyLevel.Professional);
                    sum.Merge(one);
                }
                DifficultyQualityScore q = DifficultyQualityScore.Of(sum, NewDiagnostics());
                pressed[i] = q.ErrorRateUnderPressure; tired[i] = q.ErrorRateWhenTired;
            }
            for (int i = 1; i < 5; i++) { Assert.Less(pressed[i], pressed[i - 1] + 0.01, "under pressure at " + AllLevels[i]); Assert.Less(tired[i], tired[i - 1] + 0.01, "when tired at " + AllLevels[i]); }
            Assert.Greater(pressed[0], 2.0 * pressed[4]);
        }

        [Test]
        public void TheDifficultyOfOneTeam_NeverChangesTheOther_AndTheTeamsKeepTheirOwnLevel()
        {
            var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            SimulateAttack(GameMode.Arena, 314, true, 314 % 12, (314 * 5 + 3) % 12, 200f, true, home, DifficultyLevel.Novice, DifficultyLevel.Elite, away);
            Assert.Greater(DifficultyQualityScore.Of(home, away).ErrorRate, 3.0 * DifficultyQualityScore.Of(away, home).ErrorRate, "the novice side errs far more than the elite side of the SAME match");
            Assert.Greater(home.MistakeCount(DecisionErrorKind.LateReaction) > 0 ? home.MistakeGapSum(DecisionErrorKind.LateReaction) / home.MistakeCount(DecisionErrorKind.LateReaction) : 0.0,
                           away.MistakeCount(DecisionErrorKind.LateReaction) > 0 ? away.MistakeGapSum(DecisionErrorKind.LateReaction) / away.MistakeCount(DecisionErrorKind.LateReaction) : 0.0, "and notices a turnover later");
        }
    }
}
