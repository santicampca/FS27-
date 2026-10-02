using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class SemanticCompilerTests
    {
        private CreatorCatalogs catalogs;
        private ConceptCatalog concepts;
        private SemanticParser parser;
        private SemanticCompiler compiler;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            concepts = DefaultConcepts.Create();
            parser = new SemanticParser(concepts, LanguagePackEs.Create(), LanguagePackEn.Create());
            compiler = new SemanticCompiler(catalogs, concepts);
        }

        private AuthoringDraft Fresh()
        {
            return new AuthoringDraft(catalogs.NewSpecification("c-1"));
        }

        private CompileResult C(string text, AuthoringDraft cur = null)
        {
            return compiler.Compile(parser.Parse(text), cur ?? Fresh());
        }

        private AuthoringDraft Run(string text, AuthoringDraft cur)
        {
            CompileResult r = C(text, cur);
            return PatchApplier.Apply(cur, r.Patch, catalogs).Draft;
        }

        private float Level(AuthoringDraft d, string id)
        {
            return catalogs.Parameters.TryGet(id, out ParameterDefinition p) && p.Domain == ParameterDomain.Appearance
                ? d.Spec.Appearance.Params.GetLevel(catalogs.Parameters, id)
                : d.Spec.Dna.Params.GetLevel(catalogs.Parameters, id);
        }

        // ---------------- magnitude and direction ----------------

        [Test]
        public void MoreAmount_MovesMore()
        {
            float little = Level(Run("hazlo un poco más alto", Fresh()), "body.height");
            float normal = Level(Run("hazlo más alto", Fresh()), "body.height");
            float much = Level(Run("hazlo mucho más alto", Fresh()), "body.height");
            float max = Level(Run("hazlo al máximo más alto", Fresh()), "body.height");
            Assert.Greater(little, 0.5f);
            Assert.Greater(normal, little);
            Assert.Greater(much, normal);
            Assert.Greater(max, much);
            Assert.AreEqual(1f, max, 1e-3f);
        }

        [Test]
        public void AbsoluteWords_SetFromNeutral_ByTheScale()
        {
            float tall = Level(Run("alto", Fresh()), "body.height");
            float veryTall = Level(Run("muy alto", Fresh()), "body.height");
            float extreme = Level(Run("extremadamente alto", Fresh()), "body.height");
            float low = Level(Run("bajo", Fresh()), "body.height");
            Assert.Greater(tall, 0.5f);
            Assert.Greater(veryTall, tall);
            Assert.Greater(extreme, veryTall);
            Assert.Less(low, 0.5f);
        }

        // ---------------- negation ----------------

        [Test]
        public void NoDemasiadoMusculoso_LimitsAnAlreadyMuscularCharacter_AndLeavesAnAverageOneAlone()
        {
            AuthoringDraft big = Fresh();
            big.Spec.Appearance.Params.Set(catalogs.Parameters, "body.muscularity", 0.95f);
            Assert.Less(Level(Run("no demasiado musculoso", big), "body.muscularity"), 0.7f);
            AuthoringDraft average = Fresh();
            Assert.AreEqual(0.5f, Level(Run("no demasiado musculoso", average), "body.muscularity"), 1e-3f);
        }

        [Test]
        public void NoMuyAlto_NeverRaisesTheHeight()
        {
            AuthoringDraft d = Run("que no sea muy alto", Fresh());
            Assert.LessOrEqual(Level(d, "body.height"), 0.5f + 1e-3f);
        }

        [Test]
        public void PlainNegation_ReversesTheDirection()
        {
            Assert.Less(Level(Run("no alto", Fresh()), "body.height"), 0.5f);
        }

        [Test]
        public void SinBarba_RemovesAnExistingBeard()
        {
            AuthoringDraft d = Fresh();
            d.Spec.Appearance.Choices["face.beard"] = "short_beard";
            AuthoringDraft r = Run("sin barba", d);
            Assert.AreEqual("none", r.Spec.Appearance.Choices["face.beard"]);
        }

        [Test]
        public void NoQuieroQueDriblee_LowersTheDribbleTendency_NotTheAbility()
        {
            AuthoringDraft d = Run("no quiero que driblee tanto", Fresh());
            Assert.Less(Level(d, "dribbling.takeOn"), 0.5f);
            Assert.IsFalse(d.Attributes.ContainsKey("Dribbling"), "the ability must not change");
        }

        // ---------------- context ----------------

        [Test]
        public void FuerteEnLosDuelos_IsGameplayStrength_NotLooks()
        {
            AuthoringDraft d = Run("fuerte en los duelos", Fresh());
            Assert.Greater(d.Attributes["Strength"], 0.5f);
            Assert.Greater(Level(d, "possession.shielding"), 0.5f);
            Assert.AreEqual(0.5f, Level(d, "body.muscularity"), 1e-3f);
        }

        [Test]
        public void SeVeaFuerte_IsLooks_NotAttributes()
        {
            AuthoringDraft d = Run("quiero que se vea fuerte", Fresh());
            Assert.Greater(Level(d, "body.muscularity"), 0.5f);
            Assert.IsFalse(d.Attributes.ContainsKey("Strength"), "looking strong is not being strong");
        }

        [Test]
        public void CorraRapido_IsGameplaySpeed_AndLeavesTheBodyAlone()
        {
            AuthoringDraft d = Run("quiero que corra rápido", Fresh());
            Assert.Greater(d.Attributes["Speed"], 0.5f);
            Assert.AreEqual(0.5f, Level(d, "body.legLength"), 1e-3f);
        }

        [Test]
        public void SeVeaRapido_IsVisual()
        {
            AuthoringDraft d = Run("que se vea rápido", Fresh());
            Assert.IsFalse(d.Attributes.ContainsKey("Speed"));
            Assert.Greater(Level(d, "body.legLength"), 0.5f);
        }

        [Test]
        public void StrongWithNoHint_AppliesGameplayFirst_AndALighterVisualGuess()
        {
            AuthoringDraft d = Run("un jugador fuerte", Fresh());
            Assert.Greater(d.Attributes["Strength"], 0.5f);
            Assert.Greater(Level(d, "body.muscularity"), 0.5f);
            CompileResult r = C("un jugador fuerte");
            Assert.IsTrue(r.Patch.Operations.Any(o => o.Target == PatchTarget.Attribute && !o.Collateral));
        }

        // ---------------- conflicts ----------------

        [Test]
        public void MuyAltoYExtremadamenteBajo_IsAContradiction_NotAnAverage()
        {
            CompileResult r = C("muy alto y extremadamente bajo");
            SemanticConflict c = r.Conflicts.Single();
            Assert.AreEqual(SemanticConflictKind.Contradiction, c.Kind);
            Assert.AreEqual(ConflictSeverity.Error, c.Severity);
            Assert.AreEqual(ConflictResolution.NeedsUserInput, c.Resolution);
            CollectionAssert.Contains(c.Targets, "height");
            Assert.AreEqual(2, c.CommandIds.Count);
            Assert.IsEmpty(r.Patch.Operations.Where(o => o.Key == "body.height"));
            Assert.IsTrue(r.HasBlockingConflict);
            Assert.IsNotEmpty(r.Clarifications);
        }

        [Test]
        public void SinPeloPeroConPeloLargo_IsRemoveAndAdd_AndTouchesNothing()
        {
            CompileResult r = C("sin pelo pero con pelo largo");
            Assert.AreEqual(1, r.Conflicts.Count);
            Assert.AreEqual(SemanticConflictKind.RemoveAndAdd, r.Conflicts[0].Kind);
            Assert.IsEmpty(r.Patch.Operations);
        }

        [Test]
        public void ContrastBetweenDifferentThings_IsNotAConflict()
        {
            CompileResult r = C("creativo pero no arriesgado");
            Assert.IsFalse(r.HasBlockingConflict);
            Assert.IsNotEmpty(r.Patch.Operations);
        }

        [Test]
        public void ASideEffect_NeverOverrulesWhatWasAskedDirectly()
        {
            // "rápido" (looks) lowers the mass as a side effect; "gordo" asks for mass directly
            CompileResult r = C("que se vea rápido y gordo");
            Assert.IsTrue(r.Patch.Operations.Any(o => o.Key == "body.mass" && !o.Collateral));
            Assert.IsFalse(r.Patch.Operations.Any(o => o.Key == "body.mass" && o.Collateral && o.Level < 0.5f));
            Assert.IsFalse(r.HasBlockingConflict);
        }

        // ---------------- preservation and scope ----------------

        [Test]
        public void KeepingEverythingElse_DropsTheSideEffects()
        {
            CompileResult plain = C("hazlo más fuerte");
            CompileResult kept = C("hazlo más fuerte, mantén todo lo demás");
            Assert.Greater(plain.Patch.Operations.Count, kept.Patch.Operations.Count);
            Assert.IsTrue(kept.Patch.Operations.All(o => !o.Collateral));
        }

        [Test]
        public void KeepingTheFace_BlocksAnyChangeToIt_WithAConflictRecord()
        {
            CompileResult r = C("cabeza grande, mantén la cara");
            Assert.IsFalse(r.Patch.Operations.Any(o => o.Key.StartsWith("head.")));
            Assert.IsTrue(r.Conflicts.Any(c => c.Kind == SemanticConflictKind.PreserveViolation && c.Resolution == ConflictResolution.AutoResolved));
        }

        [Test]
        public void SinPerderFuerza_ProtectsStrengthFromOtherChanges()
        {
            CompileResult r = C("hazlo más rápido pero sin perder fuerza");
            Assert.IsTrue(r.Patch.Operations.Any(o => o.Kind == PatchOpKind.Preserve && o.Key == "Strength"));
            Assert.IsTrue(r.Patch.Operations.Any(o => o.Key == "Speed"));
        }

        // ---------------- unsupported ----------------

        [Test]
        public void MasJoven_IsReportedAsUnsupported_WithWhatItNeeds()
        {
            CompileResult r = C("hazlo más joven");
            Assert.AreEqual(1, r.Unsupported.Count);
            Assert.AreEqual(UnsupportedReason.RequiresAsset, r.Unsupported[0].Reason);
            Assert.IsNotEmpty(r.Unsupported[0].RequiredSystem);
            Assert.IsNotEmpty(r.Unsupported[0].SuggestedFutureImplementation);
            Assert.IsEmpty(r.Patch.Operations);
        }

        [Test]
        public void AnAnimationRequest_IsReportedAsUnsupported()
        {
            CompileResult r = C("animación más rápida");
            Assert.IsTrue(r.Unsupported.Any(u => u.Reason == UnsupportedReason.RequiresFutureRuntime));
        }

        [Test]
        public void LowConfidence_AsksInsteadOfApplying()
        {
            CompileResult r = C("hazlo más rápido y blorpificado");
            // confidence 0.7 is above the threshold: applied; with a stricter threshold it must ask
            compiler.ConfidenceThreshold = 0.9f;
            CompileResult strict = compiler.Compile(parser.Parse("hazlo más rápido y blorpificado"), Fresh());
            Assert.IsEmpty(strict.Patch.Operations);
            Assert.IsNotEmpty(strict.Clarifications);
            Assert.IsNotEmpty(r.Patch.Operations);
        }

        // ---------------- the report ----------------

        [Test]
        public void TheReport_ExplainsWhatWasUnderstoodAppliedAndNot()
        {
            CompileResult r = C("hazlo más rápido y blorpificado");
            Assert.IsNotEmpty(r.Report.Understood);
            Assert.IsNotEmpty(r.Report.Applied);
            CollectionAssert.Contains(r.Report.NotUnderstood, "blorpificado");
            StringAssert.Contains("Speed", r.Report.ToText());
        }

        // ---------------- patch application ----------------

        [Test]
        public void ApplyingAPatch_NeverTouchesTheInput_AndOnlyChangesWhatItNames()
        {
            AuthoringDraft before = Fresh();
            before.Spec.Appearance.Choices["hair.style"] = "afro_08";
            string beforeJson = CharacterSpecificationJson.ToJson(before.Spec, true);
            var patch = new CharacterSpecificationPatch();
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Increment, Target = PatchTarget.AppearanceParam, Key = "body.height", Delta = 0.2f });
            PatchResult r = PatchApplier.Apply(before, patch, catalogs);
            Assert.AreEqual(beforeJson, CharacterSpecificationJson.ToJson(before.Spec, true));
            Assert.AreEqual("afro_08", r.Draft.Spec.Appearance.Choices["hair.style"]);
            Assert.Greater(r.Draft.Spec.Appearance.Params.GetLevel(catalogs.Parameters, "body.height"), 0.6f);
        }

        [Test]
        public void TheInversePatch_RestoresTheDraftExactly()
        {
            AuthoringDraft before = Fresh();
            CompileResult r = C("crea un extremo rápido, alto, con pelo negro rizado y barba corta, que driblee mucho");
            PatchResult applied = PatchApplier.Apply(before, r.Patch, catalogs);
            Assert.IsFalse(DraftDiff.Create(before, applied.Draft, catalogs).IsEmpty);
            AuthoringDraft undone = PatchApplier.Apply(applied.Draft, applied.Inverse, catalogs).Draft;
            Assert.IsTrue(DraftDiff.Create(before, undone, catalogs).IsEmpty, string.Join("\n", DraftDiff.Create(before, undone, catalogs).Operations));
            Assert.AreEqual(CharacterSpecificationJson.ToJson(before.Spec), CharacterSpecificationJson.ToJson(undone.Spec));
        }

        [Test]
        public void APreserveInThePatch_BlocksOtherOperationsOnThatKey()
        {
            var patch = new CharacterSpecificationPatch();
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Preserve, Target = PatchTarget.AppearanceParam, Key = "body.height" });
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = PatchTarget.AppearanceParam, Key = "body.height", Level = 1f });
            PatchResult r = PatchApplier.Apply(Fresh(), patch, catalogs);
            Assert.AreEqual(0.5f, r.Draft.Spec.Appearance.Params.GetLevel(catalogs.Parameters, "body.height"), 1e-3f);
            Assert.AreEqual(1, r.Blocked.Count);
        }

        [Test]
        public void OutOfRangeIncrements_AreClampedAndReported()
        {
            var patch = new CharacterSpecificationPatch();
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Increment, Target = PatchTarget.AppearanceParam, Key = "body.height", Delta = 3f });
            PatchResult r = PatchApplier.Apply(Fresh(), patch, catalogs);
            Assert.AreEqual(1f, r.Draft.Spec.Appearance.Params.GetLevel(catalogs.Parameters, "body.height"), 1e-3f);
            Assert.AreEqual(1, r.Clamped.Count);
        }

        [Test]
        public void UnknownKeys_AreSkipped_NotInvented()
        {
            var patch = new CharacterSpecificationPatch();
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = PatchTarget.AppearanceParam, Key = "body.wings", Level = 1f });
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Choice, Key = "hair.style", Value = "mohawk_99" });
            PatchResult r = PatchApplier.Apply(Fresh(), patch, catalogs);
            Assert.AreEqual(2, r.Skipped.Count);
            Assert.AreEqual(0, r.Applied);
        }

        // ---------------- authoring session (multi-turn) ----------------

        [Test]
        public void TheMultiTurnScenario_ChangesOnlyWhatEachTurnSays()
        {
            AuthoringSession s = AuthoringSession.CreateDefault(7);
            s.Submit("Crea un extremo");
            Assert.IsTrue(s.Current.Roles.ContainsKey("Winger"));
            s.Submit("Hazlo más rápido");
            float speed1 = s.Current.Attributes["Speed"];
            Assert.Greater(speed1, 0.5f);
            AuthoringDraft afterSpeed = s.Current.Clone();

            s.Submit("Más pequeño");
            Assert.Less(Level(s.Current, "body.height"), 0.5f);
            Assert.AreEqual(speed1, s.Current.Attributes["Speed"], 1e-4f, "speed must survive");
            Assert.IsTrue(s.Current.Roles.ContainsKey("Winger"));

            string hairBefore = s.Current.Spec.Appearance.Choices.ContainsKey("hair.style") ? s.Current.Spec.Appearance.Choices["hair.style"] : "";
            float heightBefore = Level(s.Current, "body.height");
            s.Submit("Cámbiale el pelo");
            string hairAfter = s.Current.Spec.Appearance.Choices["hair.style"];
            Assert.AreNotEqual(hairBefore, hairAfter);
            Assert.AreEqual(heightBefore, Level(s.Current, "body.height"), 1e-4f, "only the hair changes");
            Assert.AreEqual(speed1, s.Current.Attributes["Speed"], 1e-4f);

            float aggressionBefore = s.Current.ProfileLevels.TryGetValue("Aggression", out float ab) ? ab : 0.5f;
            s.Submit("Déjalo menos agresivo");
            Assert.Less(s.Current.ProfileLevels["Aggression"], aggressionBefore);
            Assert.AreEqual(hairAfter, s.Current.Spec.Appearance.Choices["hair.style"]);
            Assert.AreEqual(heightBefore, Level(s.Current, "body.height"), 1e-4f);
            Assert.AreEqual(speed1, s.Current.Attributes["Speed"], 1e-4f);
            Assert.IsNotNull(afterSpeed);
        }

        [Test]
        public void Undo_RestoresTheCharacterBeforeTheLastChange()
        {
            AuthoringSession s = AuthoringSession.CreateDefault(3);
            s.Submit("Crea un delantero");
            s.Submit("hazlo más alto");
            float tall = Level(s.Current, "body.height");
            s.Submit("hazlo más pequeño");
            Assert.Less(Level(s.Current, "body.height"), tall);
            s.Submit("deshaz");
            Assert.AreEqual(tall, Level(s.Current, "body.height"), 1e-4f);
        }

        [Test]
        public void ComoElAnterior_StartsFromThePreviousCharacter()
        {
            AuthoringSession s = AuthoringSession.CreateDefault(5);
            s.Submit("Crea un extremo muy rápido");
            float speed = s.Current.Attributes["Speed"];
            s.Submit("Crea un portero");
            Assert.IsFalse(s.Current.Attributes.ContainsKey("Speed"));
            s.Submit("como el anterior pero con pelo largo");
            Assert.AreEqual(speed, s.Current.Attributes["Speed"], 1e-4f);
            Assert.Greater(Level(s.Current, "hair.length"), 0.5f);
        }

        [Test]
        public void RepeatedHairChanges_KeepGivingDifferentStyles()
        {
            AuthoringSession s = AuthoringSession.CreateDefault(11);
            s.Submit("Crea un jugador");
            s.Submit("cámbiale el pelo");
            string first = s.Current.Spec.Appearance.Choices["hair.style"];
            s.Submit("cámbiale el pelo");
            Assert.AreNotEqual(first, s.Current.Spec.Appearance.Choices["hair.style"]);
        }

        [Test]
        public void TheSameSessionSeedAndTexts_GiveTheSameCharacter()
        {
            string Run(uint seed)
            {
                AuthoringSession s = AuthoringSession.CreateDefault(seed);
                s.Submit("Crea un extremo rápido");
                s.Submit("cámbiale el pelo");
                s.Submit("más pequeño");
                return CharacterSpecificationJson.ToJson(s.Current.Spec, false);
            }
            Assert.AreEqual(Run(42), Run(42));
        }

        [Test]
        public void ASessionTurn_ExplainsExactlyWhatChanged()
        {
            AuthoringSession s = AuthoringSession.CreateDefault(1);
            s.Submit("Crea un extremo");
            AuthoringTurn t = s.Submit("hazlo más alto");
            Assert.AreEqual(1, t.Changes.Operations.Count(o => o.Key == "body.height"));
            Assert.IsTrue(t.Changes.Operations.All(o => o.Key == "body.height" || o.Collateral || o.Target == PatchTarget.AppearanceParam));
            StringAssert.Contains("height", t.Report.ToText());
        }
    }
}
