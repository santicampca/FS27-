using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CreatorPipelineTests
    {
        private CreatorPipeline pipeline;
        private CreatorCatalogs catalogs;

        [SetUp]
        public void SetUp()
        {
            pipeline = CreatorPipeline.CreateDefault();
            catalogs = pipeline.Catalogs;
        }

        private CreatorResult Run(string text, AuthoringDraft current = null, AuthoringDraft previous = null, uint seed = 1, bool transactional = true)
        {
            return pipeline.Run(new CreatorRequest { Text = text, Current = current, Previous = previous, Seed = seed, Transactional = transactional });
        }

        private float Level(CharacterSpecification s, string id)
        {
            return catalogs.Parameters.TryGet(id, out ParameterDefinition p) && p.Domain == ParameterDomain.Appearance ? s.Appearance.Params.GetLevel(catalogs.Parameters, id) : s.Dna.Params.GetLevel(catalogs.Parameters, id);
        }

        private AuthoringDraft Start(string text = "Crea un extremo")
        {
            CreatorResult r = Run(text);
            Assert.IsTrue(r.Success, r.DebugReport);
            return r.Draft;
        }

        private static AuthoringDraft With(AuthoringDraft d, Action<AuthoringDraft> tweak)
        {
            AuthoringDraft c = d.Clone();
            tweak(c);
            return c;
        }

        // ================= the sixteen reference prompts, end to end =================

        [Test]
        public void P01_NoDemasiadoMusculoso_LimitsTheMuscle()
        {
            AuthoringDraft big = With(Start(), d => d.Spec.Appearance.Params.Set(catalogs.Parameters, "body.muscularity", 0.97f));
            CreatorResult r = Run("no demasiado musculoso", big);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Less(Level(r.Specification, "body.muscularity"), 0.75f);
        }

        [Test]
        public void P02_QueNoSeaMuyAlto_NeverRaisesTheHeight_AndLimitsATallOne()
        {
            AuthoringDraft tall = With(Start(), d => d.Spec.Appearance.Params.Set(catalogs.Parameters, "body.height", 1.14f));
            CreatorResult r = Run("que no sea muy alto", tall);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Less(Level(r.Specification, "body.height"), Level(tall.Spec, "body.height"));
            CreatorResult avg = Run("que no sea muy alto", Start());
            Assert.LessOrEqual(Level(avg.Specification, "body.height"), 0.5f + 1e-3f);
        }

        [Test]
        public void P03_SinBarba_RemovesIt()
        {
            AuthoringDraft bearded = With(Start(), d => d.Spec.Appearance.Choices["face.beard"] = "short_beard");
            CreatorResult r = Run("sin barba", bearded);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreEqual("none", r.Specification.Appearance.Choices["face.beard"]);
            Assert.LessOrEqual(Level(r.Specification, "facialHair.density"), 0.06f, "no beard, no coverage");
        }

        [Test]
        public void P04_NoQuieroQueDriblee_LowersTheTendency()
        {
            AuthoringDraft dribbler = Start("Crea un extremo que regatee mucho");
            Assert.Greater(Level(dribbler.Spec, "dribbling.takeOn"), 0.6f);
            CreatorResult r = Run("no quiero que driblee tanto", dribbler);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Less(Level(r.Specification, "dribbling.takeOn"), Level(dribbler.Spec, "dribbling.takeOn"));
        }

        [Test]
        public void P05_CreativoPeroNoArriesgado_IsBothAtOnce()
        {
            CreatorResult r = Run("Crea un mediocampista creativo pero no arriesgado");
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(r.Draft.ProfileLevels["Creativity"], 0.5f);
            Assert.Less(r.Draft.ProfileLevels["Risk"], 0.5f);
            Assert.IsEmpty(r.Conflicts.Where(c => c.Blocks));
        }

        [Test]
        public void P06_MuyAltoYExtremadamenteBajo_IsAContradiction_NothingChanges()
        {
            AuthoringDraft b = Start();
            string before = CharacterSpecificationJson.ToJson(b.Spec);
            CreatorResult r = Run("muy alto y extremadamente bajo", b);
            Assert.IsFalse(r.Success);
            Assert.IsTrue(r.NeedsClarification);
            Assert.AreEqual(SemanticConflictKind.Contradiction, r.Conflicts.Single().Kind);
            Assert.AreEqual(before, CharacterSpecificationJson.ToJson(r.Specification), "transactional: nothing applied");
            Assert.IsNotEmpty(r.Clarifications);
        }

        [Test]
        public void P07_SinPeloPeroConPeloLargo_IsRemoveAndAdd()
        {
            CreatorResult r = Run("sin pelo pero con pelo largo", Start());
            Assert.IsFalse(r.Success);
            Assert.AreEqual(SemanticConflictKind.RemoveAndAdd, r.Conflicts.Single().Kind);
        }

        [Test]
        public void P08_QuieroQueSeVeaFuerte_IsVisualOnly()
        {
            CreatorResult r = Run("quiero que se vea fuerte", Start());
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(Level(r.Specification, "body.muscularity"), 0.5f);
            Assert.IsFalse(r.Draft.Attributes.ContainsKey("Strength"));
        }

        [Test]
        public void P09_FuerteEnLosDuelos_IsGameplayInDuels()
        {
            CreatorResult r = Run("fuerte en los duelos", Start());
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(r.Draft.Attributes["Strength"], 0.5f);
            Assert.Greater(Level(r.Specification, "possession.shielding"), 0.5f);
            Assert.AreEqual(0.5f, Level(r.Specification, "body.muscularity"), 1e-3f);
        }

        [Test]
        public void P10_CorraRapido_IsSpeed()
        {
            CreatorResult r = Run("quiero que corra rápido", Start());
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(r.Draft.Attributes["Speed"], 0.5f);
            Assert.IsTrue(r.AttributeHints.Any(h => h.Attribute == PlayerAttributeId.Speed));
        }

        [Test]
        public void P11_HazloMasAlto_ChangesOnlyTheHeight()
        {
            AuthoringDraft b = Start("Crea un extremo rápido con pelo rizado");
            CreatorResult r = Run("hazlo más alto", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(Level(r.Specification, "body.height"), Level(b.Spec, "body.height"));
            Assert.AreEqual(b.Spec.Appearance.Choices["hair.style"], r.Specification.Appearance.Choices["hair.style"]);
            Assert.AreEqual(b.Attributes["Speed"], r.Draft.Attributes["Speed"], 1e-5f);
            Assert.AreEqual(b.Spec.Appearance.Params.GetLevel(catalogs.Parameters, "head.scale"), r.Spec().Appearance.Params.GetLevel(catalogs.Parameters, "head.scale"), 1e-5f);
        }

        [Test]
        public void P12_ComoElAnterior_StartsFromThePreviousCharacter()
        {
            AuthoringDraft previous = Start("Crea un extremo muy rápido, alto");
            AuthoringDraft current = Run("Crea un portero", previous).Draft;
            CreatorResult r = Run("como el anterior", current, previous);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreEqual(Level(previous.Spec, "body.height"), Level(r.Specification, "body.height"), 1e-4f);
            Assert.AreEqual(previous.Attributes["Speed"], r.Draft.Attributes["Speed"], 1e-4f);
        }

        [Test]
        public void P13_CambialeElPelo_ChangesOnlyTheHairStyle()
        {
            AuthoringDraft b = Run("Crea un extremo rápido con pelo rizado").Draft;
            CreatorResult r = Run("cámbiale el pelo", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreNotEqual(b.Spec.Appearance.Choices["hair.style"], r.Specification.Appearance.Choices["hair.style"]);
            Assert.AreEqual(b.Attributes["Speed"], r.Draft.Attributes["Speed"]);
            Assert.AreEqual(Level(b.Spec, "body.height"), Level(r.Specification, "body.height"), 1e-5f);
        }

        [Test]
        public void P14_ManténLaCara_OnItsOwn_ChangesNothing_AndSaysSo()
        {
            AuthoringDraft b = Start();
            CreatorResult r = Run("mantén la cara", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreEqual(CharacterSpecificationJson.ToJson(b.Spec), CharacterSpecificationJson.ToJson(r.Draft.Spec), "the character itself is untouched");
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("kept")));
            Assert.IsNotNull(r.RuntimeData, "and the result is complete, not a stub");
        }

        [Test]
        public void P15_SoloCambiaSuEstilo_SwapsTheStyle_AndNothingElseDeliberate()
        {
            AuthoringDraft b = Run("Crea un extremo rápido con pelo negro rizado").Draft;
            CreatorResult r = Run("solo cambia su estilo", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreNotEqual(b.Spec.Appearance.StyleId, r.Specification.Appearance.StyleId);
            Assert.AreEqual(b.Spec.Appearance.Choices["hair.style"], r.Specification.Appearance.Choices["hair.style"]);
            Assert.AreEqual(b.Spec.Appearance.Colors["hair.color"], r.Specification.Appearance.Colors["hair.color"]);
            Assert.AreEqual(b.Attributes["Speed"], r.Draft.Attributes["Speed"]);
        }

        [Test]
        public void P16_MantenTodoLoDemas_OnItsOwn_ChangesNothing()
        {
            AuthoringDraft b = Start();
            CreatorResult r = Run("mantén todo lo demás", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreEqual(CharacterSpecificationJson.ToJson(b.Spec), CharacterSpecificationJson.ToJson(r.Draft.Spec));
            Assert.IsNotNull(r.GenerationPlan);
        }

        // ================= conversations =================

        [Test]
        public void TheFiveTurnScenario_PreservesEverythingNotMentioned()
        {
            CreatorResult t1 = Run("Crea un extremo");
            CreatorResult t2 = Run("Hazlo más rápido", t1.Draft, null, 2);
            float speed = t2.Draft.Attributes["Speed"];
            CreatorResult t3 = Run("Más pequeño", t2.Draft, null, 3);
            CreatorResult t4 = Run("Cámbiale el pelo", t3.Draft, null, 4);
            CreatorResult t5 = Run("Déjalo menos agresivo", t4.Draft, null, 5);
            foreach (CreatorResult t in new[] { t1, t2, t3, t4, t5 }) Assert.IsTrue(t.Success, t.DebugReport);
            Assert.Less(Level(t3.Specification, "body.height"), Level(t2.Specification, "body.height"));
            Assert.AreEqual(speed, t5.Draft.Attributes["Speed"], 1e-5f);
            Assert.AreEqual(t4.Specification.Appearance.Choices["hair.style"], t5.Specification.Appearance.Choices["hair.style"]);
            Assert.AreEqual(Level(t4.Specification, "body.height"), Level(t5.Specification, "body.height"), 1e-5f);
            Assert.Less(t5.Draft.ProfileLevels["Aggression"], 0.5f);
            Assert.IsTrue(t5.Draft.Roles.ContainsKey("Winger"), "the role survives every later turn");
        }

        [TestCase("más rápido")]
        [TestCase("quiero que sea más rápido")]
        [TestCase("hazlo rápido")]
        [TestCase("dale más velocidad")]
        [TestCase("quiero que corra más")]
        public void FiveWaysOfSayingFaster_GiveTheSameCharacter(string text)
        {
            AuthoringDraft b = Start();
            CreatorResult r = Run(text, b);
            CreatorResult reference = Run("hazlo más rápido", b);
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.AreEqual(CharacterSpecificationJson.ToJson(reference.Specification), CharacterSpecificationJson.ToJson(r.Specification), text);
            Assert.AreEqual(reference.Draft.Attributes["Speed"], r.Draft.Attributes["Speed"], 1e-6f, text);
        }

        [Test]
        public void SpanishAndEnglish_MeanTheSame()
        {
            AuthoringDraft b = Start();
            Assert.AreEqual(CharacterSpecificationJson.ToJson(Run("hazlo más alto y más rápido", b).Specification), CharacterSpecificationJson.ToJson(Run("make him taller and faster", b).Specification));
        }

        // ================= the vision example: a goalkeeper =================

        [Test]
        public void AGoalkeeperPrompt_BecomesProfile_Attributes_Dna_Behaviours_Movement_AndAnimation()
        {
            CreatorResult r = Run("Crea un portero muy alto, ágil, con buenos reflejos, estiradas espectaculares y buen juego con los pies, seguro y tranquilo");
            Assert.IsTrue(r.Success, r.DebugReport);
            // appearance
            Assert.Greater(Level(r.Specification, "body.height"), 0.7f);
            Assert.AreEqual("gk_standard", r.Specification.Appearance.Choices["kit.gloves"]);
            // role and zone
            Assert.IsTrue(r.Draft.Roles.ContainsKey("Guardian"));
            Assert.AreEqual("Goal", r.Draft.PrimaryZone);
            // goalkeeper profile and attributes
            Assert.IsNotNull(r.Goalkeeper);
            Assert.Greater(r.Goalkeeper.Reflexes, 70);
            Assert.Greater(r.Goalkeeper.Diving, 70);
            Assert.Greater(r.Goalkeeper.Distribution, 70);
            Assert.IsTrue(r.AttributeHints.Any(h => h.Attribute == PlayerAttributeId.Agility));
            // DNA enriched by the role, behaviour preview, movement and animation
            Assert.Greater(Level(r.Specification, "positioning.defensive"), 0.6f);
            Assert.IsNotEmpty(r.BehaviorPreview);
            Assert.IsNotNull(r.GenerationPlan);
            Assert.Greater(r.GenerationPlan.Signature.StrideLength, 0f);
            Assert.IsTrue(r.GenerationPlan.SampleAnimations.Any(a => a.StartsWith("standing: idle")));
            Assert.IsTrue(r.GenerationPlan.SampleAnimations.All(a => a.Contains("(no clip yet)")), "no animation clip exists, and the report says so");
            Assert.Less(r.Draft.ProfileLevels["Risk"], 0.5f);
        }

        // ================= the engine never claims success falsely =================

        private sealed class FixedInterpreter : ISemanticInterpreter
        {
            private readonly SemanticProgram program;
            public FixedInterpreter(SemanticProgram p) { program = p; }
            public string Name => "fixed";
            public SemanticProgram Interpret(string text) { return program; }
        }

        private sealed class ThrowingInterpreter : ISemanticInterpreter
        {
            public string Name => "throwing";
            public SemanticProgram Interpret(string text) { throw new InvalidOperationException("model offline"); }
        }

        private CreatorPipeline With(ISemanticInterpreter i) { return new CreatorPipeline(catalogs, DefaultConcepts.Create(), i); }

        [Test]
        public void AnInvalidInterpretation_IsNeverASuccess()
        {
            var p = new SemanticProgram();
            p.Commands.Add(new SemanticCommand { Id = 1, Intent = SemanticIntent.Increase, Operation = SemanticIntent.Increase, Target = "flying", Confidence = 7f });
            CreatorResult r = With(new FixedInterpreter(p)).Run(new CreatorRequest { Text = "x" });
            Assert.IsFalse(r.Success);
            Assert.IsNotEmpty(r.Errors);
            Assert.IsNull(r.Specification);
        }

        [Test]
        public void AFailingInterpreter_IsAnErrorNotACrash()
        {
            CreatorResult r = With(new ThrowingInterpreter()).Run(new CreatorRequest { Text = "x" });
            Assert.IsFalse(r.Success);
            StringAssert.Contains("model offline", r.Errors[0]);
        }

        [Test]
        public void NonsenseText_AsksInsteadOfInventing()
        {
            CreatorResult r = Run("xyzzy plugh");
            Assert.IsFalse(r.Success);
            Assert.IsTrue(r.NeedsClarification);
            Assert.IsNull(r.Specification);
        }

        [Test]
        public void AnUnsupportedRequest_IsAnError_WithWhatItNeeds()
        {
            CreatorResult r = Run("hazlo más joven", Start());
            Assert.IsFalse(r.Success);
            Assert.AreEqual(1, r.Unsupported.Count);
            Assert.IsNotEmpty(r.Unsupported[0].SuggestedFutureImplementation);
        }

        [Test]
        public void ACreationWithAnImpossibleCombination_IsRejected()
        {
            var p = new SemanticProgram();
            p.Commands.Add(new SemanticCommand { Id = 1, Intent = SemanticIntent.Create, Operation = SemanticIntent.Create });
            p.Commands.Add(new SemanticCommand { Id = 2, Intent = SemanticIntent.Set, Operation = SemanticIntent.Set, Target = "hairStyle", Value = "afro_08", Domain = SemanticDomain.Visual });
            p.Commands.Add(new SemanticCommand { Id = 3, Intent = SemanticIntent.Set, Operation = SemanticIntent.Set, Target = "hairLength", Direction = -1, Magnitude = MagnitudeLevel.Maximum, Domain = SemanticDomain.Visual });
            CreatorResult r = With(new FixedInterpreter(p)).Run(new CreatorRequest { Text = "afro rapado" });
            Assert.IsFalse(r.Success, "an afro on a shaved head is impossible");
            Assert.IsTrue(r.Errors.Any(e => e.Contains("Incompatible")));
        }

        [Test]
        public void WhenNotTransactional_TheSafePartIsApplied_StillNotASuccess()
        {
            CreatorResult r = Run("hazlo más rápido, muy alto y extremadamente bajo", Start(), null, 1, transactional: false);
            Assert.IsFalse(r.Success, "an unresolved conflict is never a success");
            Assert.IsNotNull(r.Specification);
            Assert.IsTrue(r.Draft.Attributes.ContainsKey("Speed"), "the part that was safe was applied");
        }

        [Test]
        public void ABrokenBaseCharacter_IsReported_NotHidden()
        {
            AuthoringDraft b = Start();
            b.Spec.Dna.Behaviors.Add(new BehaviorEntry("Nope", 0.5f));
            CreatorResult r = Run("hazlo más alto", b);
            Assert.IsFalse(r.Success);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("Nope")));
        }

        // ================= determinism, exports, report =================

        [Test]
        public void TheSameRequest_GivesTheSameResult_ByteForByte()
        {
            CreatorResult a = Run("Crea un delantero muy alto, rápido, con pelo rizado y barba corta", null, null, 9);
            CreatorResult b = Run("Crea un delantero muy alto, rápido, con pelo rizado y barba corta", null, null, 9);
            Assert.AreEqual(a.RuntimeData.Json, b.RuntimeData.Json);
            Assert.AreEqual(a.AuthoringData.Json, b.AuthoringData.Json);
            Assert.AreEqual(a.DebugReport, b.DebugReport);
        }

        [Test]
        public void TheRuntimeRecord_RoundTripsToTheRuntimeSpecification_AndIsSmaller()
        {
            CreatorResult r = Run("Crea un extremo rápido, alto, con pelo negro rizado, barba corta, que regatee mucho", null, null, 3);
            Assert.IsTrue(r.Success, r.DebugReport);
            var v = new CreatorValidationResult();
            Assert.IsTrue(RuntimeCharacterJson.TryFromJson(r.RuntimeData.Json, pipeline.Index, catalogs, out CharacterSpecification back, v), v.ToString());
            CharacterSpecification expected = r.Specification.ForRuntime();
            Assert.AreEqual(CharacterSpecificationJson.ToJson(expected), CharacterSpecificationJson.ToJson(back));
            Assert.Less(r.RuntimeData.Bytes, r.AuthoringData.Json.Length);
            Assert.Less(r.RuntimeData.Bytes, 2500);
            Assert.IsTrue(r.RuntimeData.ContentIds.All(id => pipeline.Index.TryResolve(id, out _)));
            StringAssert.DoesNotContain("Crea un extremo", r.RuntimeData.Json);
            StringAssert.DoesNotContain("authoring", r.RuntimeData.Json);
        }

        [Test]
        public void TheAuthoringRecord_KeepsThePromptAndTheInterpretation()
        {
            CreatorResult r = Run("Crea un extremo rápido");
            StringAssert.Contains("Crea un extremo rápido", r.AuthoringData.Json);
            StringAssert.Contains("Speed", r.AuthoringData.InterpretationText);
        }

        [Test]
        public void ARuntimeRecord_WithAnUnknownContentId_IsRejected()
        {
            var v = new CreatorValidationResult();
            string json = "{\"schemaVersion\":\"FS27.RuntimeCharacter.v1\",\"id\":\"x\",\"style\":\"style.cartoon_sports\",\"look\":{\"parts\":[\"hair.not_a_real_hairstyle\"]}}";
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson(json, pipeline.Index, catalogs, out _, v));
            Assert.IsTrue(v.Has(CreatorIssueCode.ContentUnresolved));
            v = new CreatorValidationResult();
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson("{\"schemaVersion\":\"other\"}", pipeline.Index, catalogs, out _, v));
            v = new CreatorValidationResult();
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson("{\"schemaVersion\":\"FS27.RuntimeCharacter.v1\",\"id\":\"x\",\"style\":\"style.nope\"}", pipeline.Index, catalogs, out _, v));
        }

        [Test]
        public void ARuntimeRecord_WithAContentIdOfTheWrongKind_IsRejected()
        {
            var v = new CreatorValidationResult();
            string behaviourAsPart = "{\"schemaVersion\":\"FS27.RuntimeCharacter.v1\",\"id\":\"x\",\"style\":\"style.cartoon_sports\",\"look\":{\"parts\":[\"behavior.stop_and_go\"]}}";
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson(behaviourAsPart, pipeline.Index, catalogs, out _, v));
            Assert.IsTrue(v.Has(CreatorIssueCode.ContentUnresolved));
            v = new CreatorValidationResult();
            string partAsBehaviour = "{\"schemaVersion\":\"FS27.RuntimeCharacter.v1\",\"id\":\"x\",\"style\":\"style.cartoon_sports\",\"dna\":{\"behaviors\":[[\"hair.short_curly_07\",0.5]]}}";
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson(partAsBehaviour, pipeline.Index, catalogs, out _, v));
            v = new CreatorValidationResult();
            string materialAsStyle = "{\"schemaVersion\":\"FS27.RuntimeCharacter.v1\",\"id\":\"x\",\"style\":\"material.skin_toon\"}";
            Assert.IsFalse(RuntimeCharacterJson.TryFromJson(materialAsStyle, pipeline.Index, catalogs, out _, v));
        }

        [Test]
        public void TheDebugReport_ShowsEveryStage()
        {
            CreatorResult r = Run("Crea un extremo rápido, no muy alto");
            foreach (string section in new[] { "== REQUEST ==", "== SEMANTIC MODEL", "== INTERPRETATION ==", "== PATCH ==", "== SPECIFICATION ==", "== BEHAVIOUR CANDIDATES ==", "== GENERATION PLAN ==", "runtime record" })
                StringAssert.Contains(section, r.DebugReport);
            CollectionAssert.IsSupersetOf(r.Stages.Select(s => s.Split(' ')[0]), new[] { "interpret", "compile", "apply", "validate", "export" });
        }

        [Test]
        public void TheDnaEnrichment_IsInTheResult_ButNeverWrittenBackIntoTheDraft()
        {
            CreatorResult plain = pipeline.Run(new CreatorRequest { Text = "Crea un extremo", EnrichDna = false });
            CreatorResult rich = pipeline.Run(new CreatorRequest { Text = "Crea un extremo", EnrichDna = true });
            Assert.Greater(rich.Specification.Dna.Params.Values.Count, plain.Specification.Dna.Params.Values.Count, "the role fills in more tendencies");
            Assert.AreEqual(plain.Specification.Dna.Params.Values.Count, rich.Draft.Spec.Dna.Params.Values.Count, "the draft stays exactly what the prompts said");
            Assert.AreEqual(CharacterSpecificationJson.ToJson(plain.Specification), CharacterSpecificationJson.ToJson(rich.Draft.Spec), "plain output equals the draft");
            Assert.Greater(Level(rich.Specification, "positioning.width"), 0.6f);
        }

        [Test]
        public void ThePreview_IsOnlyBuiltWhenAsked()
        {
            Assert.IsNull(Run("Crea un extremo").Preview);
            CreatorResult r = pipeline.Run(new CreatorRequest { Text = "Crea un extremo", WantPreviewPng = true, WantGlb = true });
            Assert.IsNotNull(r.Preview.PngBytes);
            Assert.IsNotNull(r.Preview.GlbBytes);
        }

        // ================= the content validator =================

        [Test]
        public void TheDefaultContent_PassesTheContentValidator()
        {
            CreatorValidationResult v = CreatorContentValidator.Validate(catalogs, DefaultConcepts.Create(), MaterialCatalog.CreateDefault(), AnimationCatalog.CreateDefault(), new[] { LanguagePackEs.Create(), LanguagePackEn.Create() });
            Assert.IsTrue(v.IsValid, v.ToString());
        }

        [Test]
        public void ABrokenContent_IsCaughtBeforeItReachesAPlayer()
        {
            var animations = AnimationCatalog.CreateDefault();
            CreatorCatalogs cat = CreatorCatalogs.CreateDefault();
            cat.Behaviors.TryAdd(new SignatureBehaviorDefinition { Id = "Moonwalk", Category = BehaviorCategory.Movement, Description = "x", AnimationTags = new List<string> { "moonwalk" }, Drivers = { new WeightedParameter("body.height", 1f) } });
            var packs = new[] { LanguagePackEs.Create() };
            packs[0].Word("zzzz", "no.such.concept");
            CreatorValidationResult v = CreatorContentValidator.Validate(cat, DefaultConcepts.Create(), MaterialCatalog.CreateDefault(), animations, packs);
            Assert.IsFalse(v.IsValid);
            Assert.IsTrue(v.Has(CreatorIssueCode.ContentUnresolved), "unknown animation tag");
            Assert.IsTrue(v.Has(CreatorIssueCode.ParameterUnknown), "an appearance parameter as a DNA driver");
            Assert.IsTrue(v.Has(CreatorIssueCode.SemanticTargetUnknown), "a word pointing at a missing concept");
        }
    }

    internal static class CreatorResultTestExtensions
    {
        internal static CharacterSpecification Spec(this CreatorResult r) { return r.Specification; }
    }
}
