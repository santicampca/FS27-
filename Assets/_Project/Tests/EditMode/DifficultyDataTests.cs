using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class DifficultyDataTests
    {
        private DifficultyLibrary library;

        [SetUp]
        public void SetUp()
        {
            library = DifficultyLibrary.CreateDefault();
        }

        private static DifficultyDefinition Get(DifficultyLevel level)
        {
            return DefaultDifficulties.Create(level);
        }

        private static DifficultyDefinition Good()
        {
            return Get(DifficultyLevel.Professional);
        }

        // ================= The five levels =================

        [Test]
        public void ThereAreExactlyFiveInitialDifficulties()
        {
            Assert.AreEqual(5, DefaultDifficulties.CreateAll().Count);
            Assert.AreEqual(5, library.Count);
            Assert.AreEqual(5, Enum.GetValues(typeof(DifficultyLevel)).Length);
        }

        [Test]
        public void Levels_AreNovicoAmateurProfesionalExpertoElite_InOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "novice", "amateur", "professional", "expert", "elite" },
                library.All.Select(d => d.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Novato", "Amateur", "Profesional", "Experto", "Élite" },
                library.All.Select(d => d.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { DifficultyLevel.Novice, DifficultyLevel.Amateur, DifficultyLevel.Professional, DifficultyLevel.Expert, DifficultyLevel.Elite },
                library.All.Select(d => d.Level).ToArray());
        }

        [Test]
        public void Levels_AreOrderedByLevel_NoMatterHowTheyWereAdded()
        {
            var lib = new DifficultyLibrary();
            foreach (var d in DefaultDifficulties.CreateAll().AsEnumerable().Reverse()) Assert.IsTrue(lib.TryAdd(d));
            CollectionAssert.AreEqual(
                new[] { DifficultyLevel.Novice, DifficultyLevel.Amateur, DifficultyLevel.Professional, DifficultyLevel.Expert, DifficultyLevel.Elite },
                lib.All.Select(d => d.Level).ToArray());
        }

        [Test]
        public void Library_LooksUpByLevelAndById_AndRejectsDuplicates()
        {
            Assert.IsTrue(library.TryGet(DifficultyLevel.Expert, out var byLevel));
            Assert.AreEqual("expert", byLevel.Id);
            Assert.IsTrue(library.TryGet("elite", out var byId));
            Assert.AreEqual(DifficultyLevel.Elite, byId.Level);
            Assert.IsFalse(library.TryGet("nope", out _));
            Assert.IsFalse(library.TryGet((string)null, out _));

            Assert.IsFalse(library.TryAdd(Get(DifficultyLevel.Elite)), "same id and level");
            var sameLevel = Get(DifficultyLevel.Elite); sameLevel.Id = "other";
            Assert.IsFalse(library.TryAdd(sameLevel), "same level");
            var sameId = Get(DifficultyLevel.Elite); sameId.Level = (DifficultyLevel)9;
            Assert.IsFalse(library.TryAdd(sameId), "same id");
            Assert.IsFalse(library.TryAdd(null));
            Assert.AreEqual(5, library.Count);
        }

        [Test]
        public void EachCallReturnsFreshCopies_SoEditingOneNeverAffectsTheDefaults()
        {
            var a = Get(DifficultyLevel.Elite);
            a.Reaction.BaseReactionSeconds = 0.99f;
            a.Decision.OptionsConsidered = 1;
            Assert.AreEqual(0.21f, Get(DifficultyLevel.Elite).Reaction.BaseReactionSeconds, 1e-6f);
            Assert.AreEqual(6, Get(DifficultyLevel.Elite).Decision.OptionsConsidered);
        }

        [Test]
        public void EveryLevel_HasAllEightParameterGroups()
        {
            foreach (var d in library.All)
            {
                Assert.IsNotNull(d.Decision, d.Id);
                Assert.IsNotNull(d.Reaction, d.Id);
                Assert.IsNotNull(d.Positioning, d.Id);
                Assert.IsNotNull(d.Anticipation, d.Id);
                Assert.IsNotNull(d.Pressure, d.Id);
                Assert.IsNotNull(d.Execution, d.Id);
                Assert.IsNotNull(d.Coordination, d.Id);
                Assert.IsNotNull(d.Goalkeeper, d.Id);
                Assert.IsNotNull(d.RecommendedAssists, d.Id);
            }
        }

        [Test]
        public void ParameterGroups_CoverEveryCategory_AndEachCategoryHasMetrics()
        {
            foreach (DifficultyCategory c in Enum.GetValues(typeof(DifficultyCategory)))
                Assert.IsTrue(DifficultyMetrics.All.Any(m => m.Category == c), c.ToString());
        }

        // ================= Ordering: harder = better behaviour =================

        [Test]
        public void EveryMetricThatMustImprove_ImprovesAtEveryStep()
        {
            var all = library.All;
            foreach (var m in DifficultyMetrics.All.Where(x => x.MustImproveWithLevel))
                for (int i = 1; i < all.Count; i++)
                {
                    float easier = m.Get(all[i - 1]), harder = m.Get(all[i]);
                    bool better = m.HigherIsBetter ? harder > easier : harder < easier;
                    Assert.IsTrue(better, m.Name + ": " + all[i - 1].Id + "=" + easier + " -> " + all[i].Id + "=" + harder);
                }
        }

        [Test]
        public void Reaction_GetsFasterWithLevel()
        {
            var all = library.All;
            for (int i = 1; i < all.Count; i++)
            {
                Assert.Less(all[i].Reaction.BaseReactionSeconds, all[i - 1].Reaction.BaseReactionSeconds);
                Assert.Less(all[i].Goalkeeper.ReactionSeconds, all[i - 1].Goalkeeper.ReactionSeconds);
            }
        }

        [Test]
        public void ErrorRelatedParameters_ShrinkWithLevel()
        {
            var all = library.All;
            for (int i = 1; i < all.Count; i++)
            {
                Assert.Greater(all[i].Execution.ExecutionQuality, all[i - 1].Execution.ExecutionQuality);
                Assert.Less(all[i].Execution.ErrorMagnitude, all[i - 1].Execution.ErrorMagnitude);
                Assert.Less(all[i].Execution.PressureSensitivity, all[i - 1].Execution.PressureSensitivity);
                Assert.Less(all[i].Execution.FatigueSensitivity, all[i - 1].Execution.FatigueSensitivity);
            }
        }

        [Test]
        public void PositioningAnticipationAndPressureIntelligence_GrowWithLevel()
        {
            var all = library.All;
            for (int i = 1; i < all.Count; i++)
            {
                Assert.Greater(all[i].Positioning.PositioningQuality, all[i - 1].Positioning.PositioningQuality);
                Assert.Less(all[i].Positioning.PositionErrorMeters, all[i - 1].Positioning.PositionErrorMeters);
                Assert.Greater(all[i].Anticipation.AnticipationQuality, all[i - 1].Anticipation.AnticipationQuality);
                Assert.Greater(all[i].Anticipation.MaxLookaheadSeconds, all[i - 1].Anticipation.MaxLookaheadSeconds);
                Assert.Greater(all[i].Pressure.PressureIntelligence, all[i - 1].Pressure.PressureIntelligence);
                Assert.Greater(all[i].Coordination.CoordinationQuality, all[i - 1].Coordination.CoordinationQuality);
            }
        }

        [Test]
        public void PressureIntensity_IsNotJustTheLevel_AndNobodyPressesAllTheTime()
        {
            // Intensity is separate from intelligence: it is allowed to stay flat and is never maximal.
            Assert.AreEqual(Get(DifficultyLevel.Novice).Pressure.PressureIntensity, Get(DifficultyLevel.Amateur).Pressure.PressureIntensity);
            Assert.IsTrue(library.All.All(d => d.Pressure.PressureIntensity <= 0.7f));
            Assert.IsFalse(DifficultyMetrics.All.Single(m => m.Name == "PressureIntensity").MustImproveWithLevel);
            Assert.Greater(Get(DifficultyLevel.Elite).Pressure.PressureIntelligence, 0.9f);
        }

        [Test]
        public void Elite_DoesNotMeanEveryoneRunsFaster_NoPhysicalValueExists()
        {
            // The difficulty has no field that could speed a player up.
            var numeric = AllDifficultyFields().Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int)).Select(f => f.Name.ToLowerInvariant()).ToArray();
            foreach (string forbidden in new[] { "speed", "acceleration", "stamina", "passing", "shooting", "strength", "ballcontrol", "defense" })
                Assert.IsFalse(numeric.Any(n => n == forbidden || n.StartsWith(forbidden + "bonus") || n.EndsWith("multiplier")), forbidden);
        }

        // ================= Validation =================

        [Test]
        public void Defaults_AreAllValid_AndTheSetIsValid()
        {
            foreach (var d in library.All) Assert.IsTrue(DifficultyValidator.Validate(d).IsValid, d.Id + ": " + DifficultyValidator.Validate(d));
            var set = DifficultyValidator.ValidateSet(library);
            Assert.IsTrue(set.IsValid, set.ToString());
        }

        [Test]
        public void Validate_Null_AndMissingGroups()
        {
            Assert.IsTrue(DifficultyValidator.Validate(null).Has(AiDataIssueCode.DifficultyNull));
            var d = Good(); d.Pressure = null;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        public void Validate_BadId(string id)
        {
            var d = Good(); d.Id = id;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyIdInvalid));
        }

        [TestCase(null)]
        [TestCase("   ")]
        public void Validate_BadName(string name)
        {
            var d = Good(); d.Name = name;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyNameInvalid));
        }

        [Test]
        public void Validate_UndefinedLevel()
        {
            var d = Good(); d.Level = (DifficultyLevel)42;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyLevelInvalid));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void Validate_UnitValuesMustBeBetweenZeroAndOne(float bad)
        {
            foreach (Action<DifficultyDefinition> mutate in new Action<DifficultyDefinition>[]
            {
                d => d.Decision.DecisionQuality = bad, d => d.Decision.PressureResistance = bad, d => d.Decision.RoleDiscipline = bad,
                d => d.Reaction.ReactionVariance = bad, d => d.Positioning.PositioningQuality = bad, d => d.Positioning.ZoneDiscipline = bad,
                d => d.Positioning.PassLaneCover = bad, d => d.Positioning.RoleAdaptability = bad, d => d.Anticipation.AnticipationQuality = bad,
                d => d.Anticipation.ReboundReading = bad, d => d.Pressure.PressureIntensity = bad, d => d.Pressure.PressureIntelligence = bad,
                d => d.Pressure.RecoveryRunQuality = bad, d => d.Execution.ExecutionQuality = bad, d => d.Execution.ErrorMagnitude = bad,
                d => d.Coordination.CoordinationQuality = bad, d => d.Coordination.LineCompactness = bad, d => d.Coordination.SupportRunTiming = bad,
                d => d.Goalkeeper.Positioning = bad, d => d.Goalkeeper.Anticipation = bad, d => d.Goalkeeper.DecisionMaking = bad,
                d => d.Goalkeeper.SaveTiming = bad, d => d.Goalkeeper.ShotReading = bad, d => d.Goalkeeper.ReboundResponse = bad
            })
            {
                var d = Good();
                mutate(d);
                var r = DifficultyValidator.Validate(d);
                Assert.AreEqual(1, r.Count, r.ToString());
                Assert.IsTrue(r.Has(AiDataIssueCode.DifficultyValueOutOfRange));
            }
        }

        [Test]
        public void Validate_ReactionCannotBeFasterThanHumanLimit()
        {
            var d = Good(); d.Reaction.BaseReactionSeconds = 0.02f; d.Reaction.MinReactionSeconds = 0.01f;
            var r = DifficultyValidator.Validate(d);
            Assert.IsTrue(r.Has(AiDataIssueCode.DifficultyBelowHumanLimit));
            Assert.AreEqual(2, r.CountOf(AiDataIssueCode.DifficultyBelowHumanLimit));

            d = Good(); d.Goalkeeper.ReactionSeconds = 0.05f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyBelowHumanLimit), "keeper too");

            d = Good(); d.Decision.DecisionIntervalSeconds = 0.01f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyBelowHumanLimit));
        }

        [Test]
        public void Validate_ReactionAtTheHumanLimit_IsAccepted()
        {
            var d = Good(); d.Reaction.BaseReactionSeconds = DifficultyRules.AbsoluteMinReactionSeconds; d.Reaction.MinReactionSeconds = DifficultyRules.AbsoluteMinReactionSeconds;
            Assert.IsTrue(DifficultyValidator.Validate(d).IsValid);
        }

        [Test]
        public void Validate_MinReactionAboveBase_IsAnError()
        {
            var d = Good(); d.Reaction.MinReactionSeconds = d.Reaction.BaseReactionSeconds + 0.1f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
        }

        [Test]
        public void Validate_LookaheadCannotReadTheFuture()
        {
            var d = Good(); d.Anticipation.MaxLookaheadSeconds = DifficultyRules.AbsoluteMaxLookaheadSeconds + 0.5f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyBelowHumanLimit));
            d.Anticipation.MaxLookaheadSeconds = DifficultyRules.AbsoluteMaxLookaheadSeconds;
            Assert.IsTrue(DifficultyValidator.Validate(d).IsValid);
        }

        [Test]
        public void Validate_IntegersDistancesAndAngles()
        {
            var d = Good(); d.Decision.OptionsConsidered = 0;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
            d = Good(); d.Coordination.MaxSimultaneousPressers = 9;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
            d = Good(); d.Positioning.PositionErrorMeters = -1f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
            d = Good(); d.Anticipation.VisionAngleDegrees = 10f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
            d = Good(); d.Anticipation.PerceptionRangeMeters = 500f;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyValueOutOfRange));
        }

        [Test]
        public void Validate_InvalidRecommendedAssists()
        {
            var d = Good(); d.RecommendedAssists = null;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyAssistInvalid));
            d = Good(); d.RecommendedAssists.ShotAssist = (AssistLevel)9;
            Assert.IsTrue(DifficultyValidator.Validate(d).Has(AiDataIssueCode.DifficultyAssistInvalid));
        }

        [Test]
        public void Validate_ReportsEveryProblemTogether_WithTheFieldNameInTheMessage()
        {
            var d = Good(); d.Id = ""; d.Decision.DecisionQuality = 2f; d.Reaction.BaseReactionSeconds = 0.01f; d.Reaction.MinReactionSeconds = 0.01f;
            var r = DifficultyValidator.Validate(d);
            Assert.IsTrue(r.Has(AiDataIssueCode.DifficultyIdInvalid));
            StringAssert.Contains("Decision.DecisionQuality", r.ToString());
            StringAssert.Contains("Reaction.BaseReactionSeconds", r.ToString());
        }

        [Test]
        public void ValidateSet_DetectsALevelThatPlaysWorseThanTheEasierOne()
        {
            library.TryGet(DifficultyLevel.Elite, out var elite);
            elite.Reaction.BaseReactionSeconds = 0.45f;        // slower than Expert
            elite.Pressure.PressureIntelligence = 0.10f;       // dumber than Expert
            var r = DifficultyValidator.ValidateSet(library);
            Assert.IsTrue(r.Has(AiDataIssueCode.DifficultySetNotOrdered));
            StringAssert.Contains("BaseReactionSeconds", r.ToString());
            StringAssert.Contains("PressureIntelligence", r.ToString());
        }

        [Test]
        public void ValidateSet_AllowsFlatPressureIntensity_ButNotWorseIntelligence()
        {
            library.TryGet(DifficultyLevel.Elite, out var elite);
            elite.Pressure.PressureIntensity = 0.30f; // style-like value: allowed to go down
            Assert.IsTrue(DifficultyValidator.ValidateSet(library).IsValid);
        }

        [Test]
        public void ValidateSet_Null_AndInvalidMembersAreReported()
        {
            Assert.IsTrue(DifficultyValidator.ValidateSet(null).Has(AiDataIssueCode.DifficultyNull));
            library.TryGet(DifficultyLevel.Novice, out var novice);
            novice.Decision.DecisionQuality = 5f;
            Assert.IsTrue(DifficultyValidator.ValidateSet(library).Has(AiDataIssueCode.DifficultyValueOutOfRange));
        }

        // ================= Values are configurable data =================

        [Test]
        public void Values_CanBeEdited_AndTheValidatorAndMetricsSeeTheChange()
        {
            var d = Good();
            d.Decision.DecisionQuality = 0.77f;
            d.Goalkeeper.SaveTiming = 0.12f;
            Assert.AreEqual(0.77f, DifficultyMetrics.All.Single(m => m.Name == "DecisionQuality").Get(d), 1e-6f);
            Assert.AreEqual(0.12f, DifficultyMetrics.All.Single(m => m.Name == "Goalkeeper.SaveTiming").Get(d), 1e-6f);
            Assert.IsTrue(DifficultyValidator.Validate(d).IsValid);
        }

        [Test]
        public void ACustomLevelCanBeBuiltFromScratch_WithoutTouchingTheDefaults()
        {
            var custom = new DifficultyDefinition("custom", "Custom", DifficultyLevel.Professional);
            custom.Reaction.BaseReactionSeconds = 0.28f;
            var lib = new DifficultyLibrary();
            Assert.IsTrue(lib.TryAdd(custom));
            Assert.IsTrue(DifficultyValidator.Validate(custom).IsValid);
            Assert.AreEqual(0.32f, Get(DifficultyLevel.Professional).Reaction.BaseReactionSeconds, 1e-6f);
        }

        [Test]
        public void EveryMetric_HasAUniqueName_AndReadsTheDefaults()
        {
            var names = DifficultyMetrics.All.Select(m => m.Name).ToArray();
            Assert.AreEqual(names.Length, names.Distinct().Count());
            foreach (var m in DifficultyMetrics.All)
                Assert.IsFalse(float.IsNaN(m.Get(Good())), m.Name);
        }

        // ================= Recommended assists (separate from the AI) =================

        [Test]
        public void RecommendedAssists_FollowTheProposedProgression()
        {
            var pro = Get(DifficultyLevel.Professional).RecommendedAssists;
            Assert.AreEqual(AssistLevel.Medium, pro.PassAssist);
            Assert.AreEqual(AssistLevel.Low, pro.ShotAssist);
            Assert.AreEqual(AssistLevel.Manual, pro.PlayerSwitchAssist);

            var novice = Get(DifficultyLevel.Novice).RecommendedAssists;
            Assert.AreEqual(AssistLevel.High, novice.PassAssist);
            Assert.AreEqual(AssistLevel.High, novice.ShotAssist);
            Assert.AreEqual(AssistLevel.High, novice.PlayerSwitchAssist);
        }

        [Test]
        public void Assists_PlayerOverridesBeatTheRecommendation_FieldByField()
        {
            var pro = Get(DifficultyLevel.Professional);
            var r = AssistResolver.Resolve(pro, new PlayerAssistOverrides { ShotAssist = AssistLevel.High });
            Assert.AreEqual(AssistLevel.Medium, r.PassAssist, "recommended");
            Assert.AreEqual(AssistLevel.High, r.ShotAssist, "player's choice");
            Assert.AreEqual(AssistLevel.Manual, r.PlayerSwitchAssist, "recommended");
            Assert.AreEqual(AssistLevel.Low, pro.RecommendedAssists.ShotAssist, "the difficulty itself is untouched");
        }

        [Test]
        public void Assists_NoOverrides_GiveACopyOfTheRecommendation()
        {
            var elite = Get(DifficultyLevel.Elite);
            var r = AssistResolver.Resolve(elite, null);
            r.PassAssist = AssistLevel.High;
            Assert.AreEqual(AssistLevel.Low, elite.RecommendedAssists.PassAssist, "editing the result must not edit the difficulty");
        }

        [Test]
        public void Assists_AreIndependentOfAiDifficultyParameters()
        {
            // Changing every assist must not change a single AI metric, and vice versa.
            var d = Good();
            float[] before = DifficultyMetrics.All.Select(m => m.Get(d)).ToArray();
            d.RecommendedAssists = new PlayerAssistSettings(AssistLevel.Manual, AssistLevel.Manual, AssistLevel.Manual);
            CollectionAssert.AreEqual(before, DifficultyMetrics.All.Select(m => m.Get(d)).ToArray());

            var settings = new PlayerAssistSettings(AssistLevel.High, AssistLevel.High, AssistLevel.High);
            var aiFields = AllDifficultyFields().Select(f => f.Name).ToArray();
            CollectionAssert.DoesNotContain(aiFields.Where(n => n != "RecommendedAssists"), "PassAssist");
            Assert.AreEqual(AssistLevel.High, settings.PassAssist);
        }

        [Test]
        public void Assists_NumbersGetStricterFromHighToManual()
        {
            var t = new AssistTuning();
            for (int i = 1; i < 4; i++)
            {
                Assert.Less(t.PassAimSnapDegrees[i], t.PassAimSnapDegrees[i - 1]);
                Assert.Less(t.ShotAimCorrectionDegrees[i], t.ShotAimCorrectionDegrees[i - 1]);
                Assert.Less(t.SwitchSearchRadiusMeters[i], t.SwitchSearchRadiusMeters[i - 1]);
            }
            var manual = AssistResolver.ToParameters(new PlayerAssistSettings(AssistLevel.Manual, AssistLevel.Manual, AssistLevel.Manual), t);
            Assert.AreEqual(0f, manual.PassAimSnapDegrees);
            Assert.IsFalse(manual.AutoSwitch);
            var high = AssistResolver.ToParameters(new PlayerAssistSettings(AssistLevel.High, AssistLevel.High, AssistLevel.High), t);
            Assert.IsTrue(high.AutoSwitch);
            Assert.AreEqual(25f, high.PassAimSnapDegrees);
        }

        // ================= helpers =================

        private static IEnumerable<FieldInfo> AllDifficultyFields()
        {
            Type[] types =
            {
                typeof(DifficultyDefinition), typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters),
                typeof(AnticipationParameters), typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters),
                typeof(GoalkeeperParameters)
            };
            return types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
