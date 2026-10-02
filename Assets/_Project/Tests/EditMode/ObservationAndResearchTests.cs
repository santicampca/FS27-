using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class ObservationAndResearchTests
    {
        private CreatorCatalogs catalogs;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
        }

        private static FootballObservation Obs(string id, string pattern, float value, float confidence = 0.8f, ObservationSourceType src = ObservationSourceType.Statistics, int evidence = 10,
                                               ObservationKind kind = ObservationKind.Tendency, BehaviorContext ctx = BehaviorContext.None)
        {
            return new FootballObservation { Id = id, Subject = "ref.001", Kind = kind, Pattern = pattern, Value = value, Confidence = confidence, SourceType = src, EvidenceCount = evidence, Context = ctx, Timestamp = "2026-01-01" };
        }

        // ---------------- validation ----------------

        [Test]
        public void AValidObservation_Passes()
        {
            Assert.IsTrue(ObservationValidator.Validate(Obs("a", "dribbling.takeOn", 0.9f), catalogs).IsValid);
            Assert.IsTrue(ObservationValidator.Validate(Obs("b", "StopAndGo", 0.9f, kind: ObservationKind.Behavior), catalogs).IsValid);
        }

        [Test]
        public void BadObservations_AreRejected()
        {
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "body.height", 0.5f), catalogs).IsValid, "an appearance parameter is not a tendency");
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "dribbling.nope", 0.5f), catalogs).IsValid);
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "dribbling.takeOn", 1.5f), catalogs).IsValid);
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "dribbling.takeOn", float.NaN), catalogs).IsValid);
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "dribbling.takeOn", 0.5f, confidence: 2f), catalogs).IsValid);
            Assert.IsFalse(ObservationValidator.Validate(Obs("a", "Nope", 0.5f, kind: ObservationKind.Behavior), catalogs).IsValid);
            var noSubject = Obs("a", "dribbling.takeOn", 0.5f); noSubject.Subject = "";
            Assert.IsFalse(ObservationValidator.Validate(noSubject, catalogs).IsValid);
        }

        // ---------------- aggregation ----------------

        [Test]
        public void TwoAgreeingSources_AreMoreConfidentThanOne()
        {
            float one = ObservationAggregator.Aggregate(new[] { Obs("a", "dribbling.takeOn", 0.8f, 0.6f) }).Single().Confidence;
            float two = ObservationAggregator.Aggregate(new[] { Obs("a", "dribbling.takeOn", 0.8f, 0.6f), Obs("b", "dribbling.takeOn", 0.8f, 0.6f, ObservationSourceType.VideoAnalysis) }).Single().Confidence;
            Assert.Greater(two, one);
        }

        [Test]
        public void DisagreeingSources_LowerTheConfidence_AndAreMeasured()
        {
            AggregatedPattern agree = ObservationAggregator.Aggregate(new[] { Obs("a", "dribbling.takeOn", 0.8f), Obs("b", "dribbling.takeOn", 0.8f) }).Single();
            AggregatedPattern fight = ObservationAggregator.Aggregate(new[] { Obs("a", "dribbling.takeOn", 0.9f), Obs("b", "dribbling.takeOn", 0.3f) }).Single();
            Assert.Less(fight.Confidence, agree.Confidence);
            Assert.Greater(fight.Disagreement, 0.2f);
            Assert.AreEqual(0f, agree.Disagreement, 1e-5f);
        }

        [Test]
        public void ATrustedSource_CountsForMoreThanAModel()
        {
            AggregatedPattern p = ObservationAggregator.Aggregate(new[]
            {
                Obs("a", "dribbling.takeOn", 0.9f, 0.8f, ObservationSourceType.Statistics), Obs("b", "dribbling.takeOn", 0.1f, 0.8f, ObservationSourceType.Model)
            }).Single();
            Assert.Greater(p.Value, 0.5f, "the statistic wins the tie");
        }

        [Test]
        public void MoreEvidence_CountsForMore()
        {
            AggregatedPattern p = ObservationAggregator.Aggregate(new[]
            {
                Obs("a", "dribbling.takeOn", 0.9f, 0.8f, evidence: 100), Obs("b", "dribbling.takeOn", 0.1f, 0.8f, evidence: 1)
            }).Single();
            Assert.Greater(p.Value, 0.7f);
            Assert.AreEqual(101, p.EvidenceCount);
        }

        [Test]
        public void AggregationDoesNotDependOnTheOrder()
        {
            var list = new List<FootballObservation>
            {
                Obs("a", "dribbling.takeOn", 0.9f, 0.7f), Obs("b", "dribbling.takeOn", 0.4f, 0.5f, ObservationSourceType.Scouting), Obs("c", "shooting.frequency", 0.7f), Obs("d", "dribbling.takeOn", 0.6f, 0.9f, ObservationSourceType.Model)
            };
            string Describe(IEnumerable<FootballObservation> o) => string.Join("|", ObservationAggregator.Aggregate(o).Select(p => p.Pattern + ":" + p.Value.ToString("R") + ":" + p.Confidence.ToString("R")));
            string a = Describe(list);
            list.Reverse();
            Assert.AreEqual(a, Describe(list));
        }

        [Test]
        public void TheSituation_IsKeptOnlyWhenMostEvidenceSharesIt()
        {
            AggregatedPattern shared = ObservationAggregator.Aggregate(new[]
            {
                Obs("a", "dribbling.takeOn", 0.9f, ctx: BehaviorContext.FacingDefender), Obs("b", "dribbling.takeOn", 0.9f, ctx: BehaviorContext.FacingDefender | BehaviorContext.WideArea)
            }).Single();
            Assert.IsTrue((shared.Context & BehaviorContext.FacingDefender) != 0);
            Assert.IsTrue((shared.Context & BehaviorContext.WideArea) == 0, "only half of the evidence saw it");
        }

        // ---------------- composer ----------------

        private ComposerResult Compose(ComposerInput input, PlayerAttributes? attributes = null)
        {
            return FootballDnaComposer.Compose(input, catalogs, attributes);
        }

        [Test]
        public void WithNoInput_TheComposerReturnsNeutralDna_AndInventsNothing()
        {
            ComposerResult r = Compose(new ComposerInput());
            Assert.AreEqual(0, r.Dna.Params.Values.Count);
            Assert.AreEqual(0, r.Dna.Behaviors.Count);
            Assert.AreEqual(0, r.Dna.Sequences.Count);
        }

        [Test]
        public void Attributes_OnlyNudge()
        {
            var fast = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Acceleration, 99).With(PlayerAttributeId.Dribbling, 99).With(PlayerAttributeId.Agility, 99);
            ComposerResult r = Compose(new ComposerInput(), fast);
            float takeOn = r.Dna.Get(catalogs.Parameters, "dribbling.takeOn");
            Assert.Greater(takeOn, 0.6f);
            Assert.LessOrEqual(takeOn, 0.76f, "attributes alone may not push a tendency to the extreme");
        }

        [Test]
        public void ARoleAndProfile_PullTowardsWhatThatKindOfPlayerDoes()
        {
            var profile = new PlayerPlayingProfile("x", PitchZone.Wing, null, PlayerArchetype.Winger).WithBehaviour(80, 70, 30);
            ComposerResult r = Compose(new ComposerInput { Profile = profile });
            Assert.Greater(r.Dna.Get(catalogs.Parameters, "positioning.width"), 0.6f);
            Assert.Greater(r.Dna.Get(catalogs.Parameters, "passing.risky"), 0.6f);
            Assert.Less(r.Dna.Get(catalogs.Parameters, "defending.aggression"), 0.5f);
        }

        [Test]
        public void Observations_PullInProportionToConfidence_AndRecordIt()
        {
            ComposerResult sure = Compose(new ComposerInput { Observations = new[] { Obs("a", "dribbling.takeOn", 0.95f, 0.95f) } });
            ComposerResult unsure = Compose(new ComposerInput { Observations = new[] { Obs("a", "dribbling.takeOn", 0.95f, 0.2f) } });
            Assert.Greater(sure.Dna.Get(catalogs.Parameters, "dribbling.takeOn"), unsure.Dna.Get(catalogs.Parameters, "dribbling.takeOn"));
            Assert.Less(unsure.Dna.ConfidenceOf("dribbling.takeOn"), 1f);
            Assert.AreEqual(0.2f * ObservationAggregator.Trust(ObservationSourceType.Statistics), unsure.Dna.ConfidenceOf("dribbling.takeOn"), 0.01f);
        }

        [Test]
        public void ManualPreferences_WinOverEverything()
        {
            var manual = new ManualPreferences();
            manual.Params["dribbling.takeOn"] = 0.1f;
            ComposerResult r = Compose(new ComposerInput { Observations = new[] { Obs("a", "dribbling.takeOn", 0.95f, 0.95f) }, Manual = manual },
                                       PlayerAttributes.CreateDefault().With(PlayerAttributeId.Dribbling, 99));
            Assert.AreEqual(0.1f, r.Dna.Get(catalogs.Parameters, "dribbling.takeOn"), 1e-4f);
            Assert.AreEqual(1f, r.Dna.ConfidenceOf("dribbling.takeOn"), "a manual value is certain");
        }

        [Test]
        public void ManualBehaviours_AreAddedAndRemoved_AndMarkedManual()
        {
            var manual = new ManualPreferences();
            manual.Behaviors.Add(new BehaviorEntry("BodyFeint", 0.9f));
            manual.RemoveBehaviors.Add("StopAndGo");
            ComposerResult r = Compose(new ComposerInput { Manual = manual, Observations = new[] { Obs("a", "StopAndGo", 0.9f, kind: ObservationKind.Behavior) } });
            Assert.IsTrue(r.Dna.TryGetBehavior("BodyFeint", out BehaviorEntry feint));
            Assert.AreEqual("manual", feint.Origin);
            Assert.IsFalse(r.Dna.HasBehavior("StopAndGo"));
        }

        [Test]
        public void ObservedBehaviours_KeepTheirConfidenceOriginAndSituation()
        {
            ComposerResult r = Compose(new ComposerInput { Observations = new[] { Obs("a", "InsideCut", 0.85f, 0.7f, kind: ObservationKind.Behavior, ctx: BehaviorContext.WideArea) } });
            Assert.IsTrue(r.Dna.TryGetBehavior("InsideCut", out BehaviorEntry e));
            Assert.AreEqual("observed", e.Origin);
            Assert.AreEqual(BehaviorContext.WideArea, e.Condition.Prefers);
            Assert.Less(e.Confidence, 1f);
        }

        [Test]
        public void StrongTendencies_DeriveSignatureBehaviours_ButNotMoreThanTheLimit()
        {
            var obs = new List<FootballObservation>();
            foreach (string p in new[] { "dribbling.stopAndGo", "dribbling.takeOn", "dribbling.bodyFeint", "dribbling.directionChange", "dribbling.insideCut", "dribbling.outsideCut", "dribbling.changeOfPace", "movement.accelerationTendency", "positioning.halfSpace", "positioning.width" })
                obs.Add(Obs("o-" + p, p, 0.98f, 0.99f, ObservationSourceType.Manual, 100));
            ComposerResult r = Compose(new ComposerInput { Observations = obs });
            int derived = r.Dna.Behaviors.Count(b => b.Origin == "derived");
            Assert.That(derived, Is.InRange(1, FootballDnaComposer.MaxDerivedBehaviors));
        }

        [Test]
        public void AFastPlayerWhoCannotDoIt_DoesNotGetABehaviourTheirAttributesCannotSupport()
        {
            var weak = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 20).With(PlayerAttributeId.Control, 20).With(PlayerAttributeId.Technique, 20).With(PlayerAttributeId.Dribbling, 20);
            var obs = new[] { Obs("a", "dribbling.stopAndGo", 1f, 1f, ObservationSourceType.Manual, 100), Obs("b", "dribbling.takeOn", 1f, 1f, ObservationSourceType.Manual, 100), Obs("c", "movement.decelerationTendency", 1f, 1f, ObservationSourceType.Manual, 100) };
            ComposerResult r = Compose(new ComposerInput { Observations = obs }, weak);
            Assert.IsFalse(r.Dna.HasBehavior("StopAndGo"));
        }

        [Test]
        public void ACompleteChain_IsAddedAsASequence()
        {
            var manual = new ManualPreferences();
            manual.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.8f));
            manual.Behaviors.Add(new BehaviorEntry("ExplosiveExit", 0.7f));
            ComposerResult r = Compose(new ComposerInput { Manual = manual, DeriveBehaviors = false });
            Assert.IsTrue(r.Dna.Sequences.Any(q => q.Id == "stop_go_burst"));
        }

        [Test]
        public void TheComposer_IsDeterministic_AndOrderIndependent()
        {
            List<FootballObservation> Make() => new List<FootballObservation>
            {
                Obs("a", "dribbling.takeOn", 0.9f, 0.7f), Obs("b", "shooting.frequency", 0.7f, 0.9f), Obs("c", "dribbling.takeOn", 0.5f, 0.4f, ObservationSourceType.Scouting)
            };
            var profile = new PlayerPlayingProfile("x", PitchZone.Attack, null, PlayerArchetype.GoalHunter);
            ComposerInput In(List<FootballObservation> o) => new ComposerInput { Profile = profile, Observations = o };
            PlayerAttributes shooter = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Shooting, 90);
            string a = CharacterSpecificationJson.ToJson(Wrap(Compose(In(Make()), shooter).Dna));
            var reversed = Make(); reversed.Reverse();
            string b = CharacterSpecificationJson.ToJson(Wrap(Compose(In(reversed), shooter).Dna));
            Assert.AreEqual(a, b);
        }

        private CharacterSpecification Wrap(FootballDNA dna)
        {
            CharacterSpecification s = catalogs.NewSpecification("w-1");
            s.Dna = dna;
            return s;
        }

        [Test]
        public void TheVariationSeed_MakesArchetypeMatesDiffer_ButIsReproducible()
        {
            var profile = new PlayerPlayingProfile("x", PitchZone.Wing, null, PlayerArchetype.Winger);
            float Width(uint seed) => Compose(new ComposerInput { Profile = profile, VariationSeed = seed }).Dna.Get(catalogs.Parameters, "positioning.width");
            Assert.AreEqual(Width(11), Width(11));
            Assert.AreNotEqual(Width(11), Width(12));
            Assert.AreEqual(Width(0), Width(0));
            Assert.LessOrEqual(Math.Abs(Width(11) - Width(0)), FootballDnaComposer.VariationSize + 1e-4f);
        }

        [Test]
        public void TheComposedDna_AlwaysPassesTheValidator()
        {
            var obs = new[] { Obs("a", "dribbling.takeOn", 0.9f, 0.7f), Obs("b", "StopAndGo", 0.9f, 0.7f, kind: ObservationKind.Behavior, ctx: BehaviorContext.FacingDefender) };
            ComposerResult r = Compose(new ComposerInput { Profile = new PlayerPlayingProfile("x", PitchZone.Wing, null, PlayerArchetype.Winger), Observations = obs }, PlayerAttributes.CreateDefault());
            CreatorValidationResult v = FootballDNAValidator.Validate(r.Dna, catalogs);
            Assert.IsTrue(v.IsValid, v.ToString());
            Assert.IsNotEmpty(r.Explanation);
        }

        [Test]
        public void AnObservationOfSomethingUnknown_IsIgnoredWithAWarning_NotCrashed()
        {
            ComposerResult r = Compose(new ComposerInput { Observations = new[] { Obs("a", "dribbling.nope", 0.9f) } });
            Assert.IsNotEmpty(r.Warnings);
            Assert.AreEqual(0, r.Dna.Params.Values.Count);
        }

        // ---------------- stat analysis ----------------

        private static ResearchFinding Stats(string id, params (string, float)[] stats)
        {
            var f = new ResearchFinding { Id = id, SourceType = ObservationSourceType.Statistics, Reliability = 1f, Timestamp = "2026-02-01", Reference = "cite.001" };
            foreach ((string k, float v) in stats) f.Stats[k] = v;
            return f;
        }

        [Test]
        public void StatLines_BecomeTendencies_ByHowFarTheyAreFromTheTypicalPlayer()
        {
            var a = new StatLineAnalyzer();
            AnalysisResult high = a.Analyze(new ResearchQuery { Subject = "ref.001" }, Stats("s1", ("minutes", 1800f), ("dribbles_attempted_p90", 6f)));
            AnalysisResult low = a.Analyze(new ResearchQuery { Subject = "ref.001" }, Stats("s2", ("minutes", 1800f), ("dribbles_attempted_p90", 0.5f)));
            FootballObservation h = high.Observations.Single(o => o.Pattern == "dribbling.takeOn");
            FootballObservation l = low.Observations.Single(o => o.Pattern == "dribbling.takeOn");
            Assert.Greater(h.Value, 0.8f);
            Assert.Less(l.Value, 0.3f);
            Assert.AreEqual(20, h.EvidenceCount);
        }

        [Test]
        public void FewMinutes_MeansLowConfidence()
        {
            var a = new StatLineAnalyzer();
            float many = a.Analyze(new ResearchQuery { Subject = "r" }, Stats("s1", ("minutes", 2700f), ("shots_p90", 4f))).Observations[0].Confidence;
            float few = a.Analyze(new ResearchQuery { Subject = "r" }, Stats("s2", ("minutes", 90f), ("shots_p90", 4f))).Observations[0].Confidence;
            float none = a.Analyze(new ResearchQuery { Subject = "r" }, Stats("s3", ("shots_p90", 4f))).Observations[0].Confidence;
            Assert.Greater(many, few);
            Assert.Greater(few, none);
        }

        [Test]
        public void ARemarkableStatistic_SuggestsTheMatchingBehaviour()
        {
            var a = new StatLineAnalyzer();
            AnalysisResult r = a.Analyze(new ResearchQuery { Subject = "r" }, Stats("s1", ("minutes", 1800f), ("shots_outside_box_share", 0.7f)));
            Assert.IsTrue(r.Observations.Any(o => o.Kind == ObservationKind.Behavior && o.Pattern == "LongRangeShot"));
            AnalysisResult ordinary = a.Analyze(new ResearchQuery { Subject = "r" }, Stats("s2", ("minutes", 1800f), ("shots_outside_box_share", 0.35f)));
            Assert.IsFalse(ordinary.Observations.Any(o => o.Kind == ObservationKind.Behavior));
        }

        // ---------------- the pipeline ----------------

        private sealed class ThrowingProvider : IResearchProvider
        {
            public string Name => "throwing";
            public ResearchResult Research(ResearchQuery q) { throw new InvalidOperationException("offline"); }
        }

        private sealed class LyingAnalyzer : IFootballAnalysisProvider
        {
            public string Name => "lying";
            public AnalysisResult Analyze(ResearchQuery q, ResearchFinding f)
            {
                var r = new AnalysisResult();
                r.Observations.Add(Obs("bad1", "body.height", 0.9f));
                r.Observations.Add(Obs("bad2", "dribbling.takeOn", 7f));
                r.Observations.Add(Obs("good", "dribbling.takeOn", 0.9f));
                return r;
            }
        }

        private sealed class CrashingAnalyzer : IFootballAnalysisProvider
        {
            public string Name => "crashing";
            public AnalysisResult Analyze(ResearchQuery q, ResearchFinding f) { throw new Exception("boom"); }
        }

        [Test]
        public void TheWholePipeline_TurnsStatisticsIntoValidatedDna()
        {
            var provider = new InlineResearchProvider(Stats("s1", ("minutes", 2000f), ("dribbles_attempted_p90", 6f), ("shots_p90", 1f), ("pressures_p90", 8f)));
            ResearchPipelineResult r = ResearchPipeline.Run(new ResearchQuery { Subject = "ref.001" }, provider, new IFootballAnalysisProvider[] { new StatLineAnalyzer() }, catalogs);
            Assert.IsTrue(r.Success, string.Join("; ", r.Errors));
            Assert.Greater(r.Composed.Dna.Get(catalogs.Parameters, "dribbling.takeOn"), 0.65f);
            Assert.Less(r.Composed.Dna.Get(catalogs.Parameters, "shooting.frequency"), 0.45f);
            Assert.IsTrue(FootballDNAValidator.Validate(r.Composed.Dna, catalogs).IsValid);
        }

        [Test]
        public void AFailingProvider_IsReported_AndMakesUpNothing()
        {
            ResearchPipelineResult r = ResearchPipeline.Run(new ResearchQuery { Subject = "x" }, new ThrowingProvider(), new IFootballAnalysisProvider[] { new StatLineAnalyzer() }, catalogs);
            Assert.IsFalse(r.Success);
            Assert.IsNull(r.Composed);
            StringAssert.Contains("offline", r.Errors.Single());
        }

        [Test]
        public void UntrustedObservations_AreValidated_AndTheBadOnesDropped()
        {
            var provider = new InlineResearchProvider(Stats("s1"));
            ResearchPipelineResult r = ResearchPipeline.Run(new ResearchQuery { Subject = "x" }, provider, new IFootballAnalysisProvider[] { new LyingAnalyzer() }, catalogs);
            Assert.AreEqual(1, r.Accepted.Count);
            Assert.AreEqual(2, r.Rejected.Count);
            Assert.IsTrue(r.Success);
        }

        [Test]
        public void ACrashingAnalyzer_IsAWarning_NotAFatalError()
        {
            var provider = new InlineResearchProvider(Stats("s1", ("minutes", 900f), ("shots_p90", 3f)));
            ResearchPipelineResult r = ResearchPipeline.Run(new ResearchQuery { Subject = "x" }, provider, new IFootballAnalysisProvider[] { new CrashingAnalyzer(), new StatLineAnalyzer() }, catalogs);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("crashing")));
            Assert.IsNotEmpty(r.Accepted);
        }

        // ---------------- json ----------------

        [Test]
        public void Observations_RoundTripThroughJson()
        {
            var list = new List<FootballObservation> { Obs("a", "dribbling.takeOn", 0.9f, 0.7f, ctx: BehaviorContext.FacingDefender), Obs("b", "StopAndGo", 0.6f, kind: ObservationKind.Behavior) };
            string json = ObservationJson.ToJson(list);
            var r = new CreatorValidationResult();
            Assert.IsTrue(ObservationJson.TryFromJson(json, out List<FootballObservation> back, r), r.ToString());
            Assert.AreEqual(json, ObservationJson.ToJson(back));
        }

        [Test]
        public void BrokenObservationJson_IsRefusedClearly()
        {
            var r = new CreatorValidationResult();
            Assert.IsFalse(ObservationJson.TryFromJson("not json", out _, r));
            r = new CreatorValidationResult();
            Assert.IsFalse(ObservationJson.TryFromJson("{\"schemaVersion\":\"other\"}", out _, r));
            r = new CreatorValidationResult();
            Assert.IsFalse(ObservationJson.TryFromJson("{\"schemaVersion\":\"FS27.Observations.v1\",\"observations\":[{\"id\":\"a\",\"kind\":\"Nope\",\"source\":\"Manual\"}]}", out _, r));
        }
    }
}
