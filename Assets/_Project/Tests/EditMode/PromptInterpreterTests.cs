using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PromptInterpreterTests
    {
        private CreatorCatalogs catalogs;
        private DeterministicPromptInterpreter interpreter;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            interpreter = new DeterministicPromptInterpreter(catalogs);
        }

        private PromptResult Create(string text) { return interpreter.Interpret(new PromptRequest { Text = text, NewCharacterId = "char-test" }); }
        private PromptResult Modify(CharacterSpecification existing, string text) { return interpreter.Interpret(new PromptRequest { Text = text, Existing = existing }); }

        private float Param(PromptResult r, string id)
        {
            return catalogs.Parameters.TryGet(id, out var p) && p.Domain == ParameterDomain.Appearance
                ? r.Draft.Appearance.Params.Get(catalogs.Parameters, id) : r.Draft.Dna.Params.Get(catalogs.Parameters, id);
        }

        private float Level(PromptResult r, string id)
        {
            catalogs.Parameters.TryGet(id, out var p);
            return p.ToLevel(Param(r, id));
        }

        private static AttributeHint? Hint(PromptResult r, PlayerAttributeId a)
        {
            foreach (var h in r.AttributeHints) if (h.Attribute == a) return h;
            return null;
        }

        private static void AssertValid(CreatorCatalogs c, PromptResult r)
        {
            var spec = CharacterSpecificationValidator.Validate(r.Draft, c);
            Assert.IsTrue(spec.IsValid, spec.ToString());
            var prompt = PromptSpecificationValidator.Validate(r, c);
            Assert.IsTrue(prompt.IsValid, prompt.ToString());
        }

        // ================= The ten prompts of the specification =================

        [Test]
        public void P01_SmallExplosiveAthleticCreativeWinger()
        {
            var r = Create("Crea un extremo pequeño, explosivo, atlético y creativo.");
            Assert.AreEqual(PromptIntentKind.CreateCharacter, r.Intent);
            AssertValid(catalogs, r);
            Assert.Less(Level(r, "body.height"), 0.3f, "small");
            Assert.Greater(Level(r, "style.athleticity"), 0.8f, "athletic");
            Assert.Greater(Level(r, "movement.accelerationTendency"), 0.8f, "explosive");
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"));
            Assert.Greater(Hint(r, PlayerAttributeId.Acceleration).Value.Level, 0.85f);
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.Creativity && h.Level > 0.8f), "creative goes to the playing profile");
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.PrimaryZone && h.Zone == PitchZone.Wing));
            Assert.IsTrue(r.Conflicts.Count == 0);
        }

        [Test]
        public void P02_TallStrongStrikerGoodAtFinishing()
        {
            var r = Create("Crea un delantero alto y fuerte que sea muy bueno definiendo.");
            AssertValid(catalogs, r);
            Assert.Greater(Level(r, "body.height"), 0.8f);
            Assert.Greater(Level(r, "body.muscularity"), 0.7f);
            Assert.Greater(Hint(r, PlayerAttributeId.Strength).Value.Level, 0.8f);
            Assert.Greater(Hint(r, PlayerAttributeId.Finishing).Value.Level, 0.9f, "'muy bueno definiendo' is stronger than plain finishing");
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.PrimaryZone && h.Zone == PitchZone.Attack));
            Assert.IsTrue(r.Draft.Dna.HasBehavior("FirstTimeFinish"));
            Assert.AreEqual("tall", r.Draft.Appearance.Choices["body.preset"]);
        }

        [Test]
        public void P03_Thinner_ChangesTheExistingCharacterOnly()
        {
            var first = Create("Crea un extremo atlético.");
            var r = Modify(first.Draft, "Hazlo un poco más delgado.");
            Assert.AreEqual(PromptIntentKind.ModifyBody, r.Intent);
            Assert.Less(Param(r, "body.mass"), Param(first, "body.mass"));
            Assert.Less(Param(r, "body.muscularity"), Param(first, "body.muscularity"));
            AssertValid(catalogs, r);
        }

        [Test]
        public void P04_ShorterHair()
        {
            var first = Create("Crea un extremo con el pelo largo.");
            var r = Modify(first.Draft, "Hazle el pelo más corto.");
            Assert.AreEqual(PromptIntentKind.ModifyHair, r.Intent);
            Assert.Less(Param(r, "hair.length"), Param(first, "hair.length"));
            AssertValid(catalogs, r);
        }

        [Test]
        public void P05_MoreCreativeAndRisky_GoesToTheProfileAndTheDna()
        {
            var first = Create("Crea un extremo atlético.");
            var r = Modify(first.Draft, "Quiero que sea más creativo y arriesgado.");
            Assert.AreEqual(PromptIntentKind.ChangePlayStyle, r.Intent);
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.Creativity && h.Relative && h.Delta > 0));
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.Risk && h.Relative && h.Delta > 0), "'y arriesgado' inherits the 'more'");
            Assert.Greater(Param(r, "dribbling.takeOnRisk"), Param(first, "dribbling.takeOnRisk"));
            Assert.Greater(Param(r, "passing.risky"), Param(first, "passing.risky"));
        }

        [Test]
        public void P06_AttackSpace()
        {
            var r = Create("Quiero que ataque mucho el espacio.");
            Assert.Greater(Level(r, "movement.spaceSeeking"), 0.85f);
            Assert.Greater(Level(r, "movement.aggression"), 0.75f);
            Assert.Greater(Level(r, "positioning.attackingRuns"), 0.8f);
        }

        [Test]
        public void P07_ChangeOfPaceAndOneOnOneSpecialist()
        {
            var r = Create("Quiero un jugador especializado en cambios de ritmo y uno contra uno.");
            AssertValid(catalogs, r);
            Assert.Greater(Level(r, "dribbling.changeOfPace"), 0.85f);
            Assert.Greater(Level(r, "dribbling.takeOn"), 0.85f);
            Assert.IsTrue(r.Draft.Dna.HasBehavior("StopAndGo"));
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"));
            Assert.IsTrue(r.Draft.Dna.HasBehavior("BodyFeint"));
            Assert.Greater(Hint(r, PlayerAttributeId.Dribbling).Value.Level, 0.8f);
        }

        [Test]
        public void P08_DropToReceiveThenAttackTheBox_TwoRequestsInOneSentence()
        {
            var r = Create("Quiero un jugador que baje a recibir y después ataque el área.");
            AssertValid(catalogs, r);
            Assert.Greater(Level(r, "positioning.dropping"), 0.8f);
            Assert.Greater(Level(r, "positioning.boxPresence"), 0.8f);
            Assert.Greater(Level(r, "positioning.attackingRuns"), 0.75f);
            Assert.IsEmpty(r.Unresolved, "both halves were understood");
        }

        [Test]
        public void P09_MoreCartoonButNotChildish()
        {
            var first = Create("Crea un extremo atlético.");
            var plain = Modify(first.Draft, "Hazlo más cartoon.");
            var guarded = Modify(first.Draft, "Hazlo más cartoon pero no infantil.");
            Assert.AreEqual(PromptIntentKind.ModifyStyle, guarded.Intent);
            Assert.Greater(Param(guarded, "style.stylization"), Param(first, "style.stylization"));
            Assert.Greater(Param(guarded, "head.scale"), Param(first, "head.scale"), "still bigger");
            Assert.Less(Param(guarded, "head.scale") - Param(first, "head.scale"), Param(plain, "head.scale") - Param(first, "head.scale"), "but less than without the guard");
            Assert.Less(Param(guarded, "face.eyeSize") - Param(first, "face.eyeSize"), Param(plain, "face.eyeSize") - Param(first, "face.eyeSize"));
            Assert.AreEqual(Param(plain, "style.stylization"), Param(guarded, "style.stylization"), 1e-5f, "the style itself moves the same");
            Assert.IsTrue(guarded.Changes.Single(c => c.Kind == SpecChangeKind.StyleShift).GuardChildlike);
        }

        [Test]
        public void P10_FastButNotMuscular()
        {
            var r = Create("Quiero un jugador rápido pero no musculoso.");
            Assert.Greater(Hint(r, PlayerAttributeId.Speed).Value.Level, 0.8f, "fast is about gameplay by default");
            Assert.Less(Level(r, "body.muscularity"), 0.3f, "'no musculoso' is the opposite of muscular");
            Assert.IsEmpty(r.Conflicts, "fast and not muscular do not contradict");
        }

        // ================= The three end-to-end examples =================

        [Test]
        public void Example58_SmallExplosiveWinger_OneOnOne_InsideCut()
        {
            var r = Create("Quiero un extremo pequeño y explosivo, muy bueno en uno contra uno, creativo, con cambios de ritmo fuertes y tendencia a recibir abierto, encarar al defensor y atacar hacia dentro.");
            AssertValid(catalogs, r);
            // appearance
            Assert.Less(Level(r, "body.height"), 0.3f);
            Assert.AreEqual("compact", r.Draft.Appearance.Choices["body.preset"]);
            // attributes (hints)
            foreach (var a in new[] { PlayerAttributeId.Acceleration, PlayerAttributeId.Agility, PlayerAttributeId.Dribbling, PlayerAttributeId.Technique })
                Assert.Greater(Hint(r, a).Value.Level, 0.7f, a.ToString());
            // football DNA
            Assert.Greater(Level(r, "dribbling.takeOn"), 0.9f);
            Assert.Greater(Level(r, "dribbling.changeOfPace"), 0.85f);
            Assert.Greater(Level(r, "dribbling.insideCut"), 0.85f);
            Assert.Greater(Level(r, "positioning.width"), 0.8f);
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.Creativity && h.Level > 0.8f));
            // signature behaviours
            foreach (string b in new[] { "StopAndGo", "BodyFeint", "ExplosiveExit", "InsideCut" })
                Assert.IsTrue(r.Draft.Dna.HasBehavior(b), b);
            Assert.IsEmpty(r.Conflicts);
            Assert.Greater(r.Confidence, 0.8f);
        }

        [Test]
        public void Example59_TallStrongStriker_HoldUp_DropsDeep_LateInTheBox()
        {
            var r = Create("Quiero un delantero alto y fuerte que sea excelente definiendo, pueda bajar a recibir, proteger la pelota y después aparecer tarde en el área.");
            AssertValid(catalogs, r);
            Assert.Greater(Level(r, "body.height"), 0.8f);
            Assert.Greater(Level(r, "body.muscularity"), 0.7f);
            Assert.Greater(Hint(r, PlayerAttributeId.Strength).Value.Level, 0.8f);
            Assert.Greater(Hint(r, PlayerAttributeId.Finishing).Value.Level, 0.85f);
            Assert.Greater(Hint(r, PlayerAttributeId.Shooting).Value.Level, 0.75f);
            Assert.Greater(Level(r, "possession.holdUp"), 0.75f);
            Assert.Greater(Level(r, "positioning.dropping"), 0.8f);
            Assert.Greater(Level(r, "movement.delayedRuns"), 0.8f);
            Assert.Greater(Level(r, "positioning.boxPresence"), 0.7f);
            Assert.Greater(Level(r, "shooting.firstTime"), 0.7f);
            foreach (string b in new[] { "HoldUpPlay", "LateBoxArrival", "FirstTimeFinish" }) Assert.IsTrue(r.Draft.Dna.HasBehavior(b), b);
        }

        [Test]
        public void Example60_Modification_ChangesOnlyWhatWasAskedFor()
        {
            var created = Create("Crea un extremo atlético, con el pelo rizado y corto, ojos expresivos, camiseta roja y que haga regates.");
            var spec = created.Draft;
            Assert.AreEqual("short_curly_07", spec.Appearance.Choices["hair.style"], "curly + short");
            Assert.AreEqual("#D62828", spec.Appearance.Colors["kit.primary"]);
            string hairBefore = spec.Appearance.Choices["hair.style"];
            string faceBefore = spec.Appearance.Choices["face.eyeShape"];
            float eyesBefore = spec.Appearance.Params.Get(catalogs.Parameters, "face.eyeSize");
            float takeOnBefore = spec.Dna.Get(catalogs.Parameters, "dribbling.takeOn");
            float widthBefore = spec.Dna.Get(catalogs.Parameters, "positioning.width");

            var r = Modify(spec, "Hazlo más pequeño, menos musculoso y mucho más explosivo.");

            Assert.AreEqual(PromptIntentKind.ModifyCharacter, r.Intent);
            Assert.Less(Param(r, "body.height"), spec.Appearance.Params.Get(catalogs.Parameters, "body.height"));
            Assert.Less(Param(r, "body.muscularity"), spec.Appearance.Params.Get(catalogs.Parameters, "body.muscularity"));
            Assert.Greater(Param(r, "movement.accelerationTendency"), spec.Dna.Get(catalogs.Parameters, "movement.accelerationTendency"));
            // unrelated parts are NOT reset
            Assert.AreEqual(hairBefore, r.Draft.Appearance.Choices["hair.style"]);
            Assert.AreEqual(faceBefore, r.Draft.Appearance.Choices["face.eyeShape"]);
            Assert.AreEqual(eyesBefore, r.Draft.Appearance.Params.Get(catalogs.Parameters, "face.eyeSize"), 1e-6f);
            Assert.AreEqual("#D62828", r.Draft.Appearance.Colors["kit.primary"]);
            Assert.AreEqual(takeOnBefore, r.Draft.Dna.Get(catalogs.Parameters, "dribbling.takeOn"), 1e-6f);
            Assert.AreEqual(widthBefore, r.Draft.Dna.Get(catalogs.Parameters, "positioning.width"), 1e-6f);
            Assert.AreEqual(spec.CharacterId, r.Draft.CharacterId, "it is the same character, not a new one");
            AssertValid(catalogs, r);
        }

        // ================= Modification =================

        [Test]
        public void ModifyAndCombinedRequest_ThinnerAndShortHair()
        {
            var created = Create("Crea un extremo con el pelo largo.");
            var r = Modify(created.Draft, "Hazlo un poco más delgado y cambia el pelo a corto.");
            Assert.Less(Param(r, "body.mass"), Param(created, "body.mass"));
            Assert.Less(Level(r, "hair.length"), 0.2f, "'cambia el pelo a corto' sets it, it is not relative");
            Assert.AreEqual("short_crop_01", r.Draft.Appearance.Choices["hair.style"]);
            AssertValid(catalogs, r);
        }

        [Test]
        public void TheExistingSpecification_IsNeverModified()
        {
            var created = Create("Crea un extremo atlético.").Draft;
            string before = CharacterSpecificationJson.ToJson(created, true);
            Modify(created, "Hazlo mucho más alto, más cartoon y menos creativo.");
            Assert.AreEqual(before, CharacterSpecificationJson.ToJson(created, true));
        }

        [Test]
        public void AnExistingCharacter_IsOnlyReplaced_WhenAskedToCreateANewOne()
        {
            var created = Create("Crea un extremo atlético.").Draft;
            var again = Modify(created, "Crea un delantero alto.");
            Assert.AreEqual(PromptIntentKind.CreateCharacter, again.Intent);
            Assert.AreEqual("character-new", again.Draft.CharacterId, "a fresh character, not the existing one");
        }

        [Test]
        public void ModificationApplier_AppliesStructuredChanges_WithoutAnyText()
        {
            var spec = catalogs.NewSpecification("c");
            float h0 = spec.Appearance.Params.Get(catalogs.Parameters, "body.height");
            var changes = new List<SpecChange>
            {
                new SpecChange { Kind = SpecChangeKind.ScalarDelta, Target = "body.height", Direction = 1, Magnitude = 0.2f },
                new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = "hair.style", Value = "fade_06" },
                new SpecChange { Kind = SpecChangeKind.ColorSet, Target = "hair.color", Value = "#5a3825" },
                new SpecChange { Kind = SpecChangeKind.BehaviorAdd, Target = "InsideCut", Level = 0.7f },
                new SpecChange { Kind = SpecChangeKind.ScalarSet, Target = "dribbling.takeOn", Level = 0.9f }
            };
            var report = ModificationApplier.Apply(spec, changes, catalogs);
            Assert.AreEqual(5, report.Applied);
            Assert.Greater(report.Result.Appearance.Params.Get(catalogs.Parameters, "body.height"), h0);
            Assert.AreEqual("fade_06", report.Result.Appearance.Choices["hair.style"]);
            Assert.AreEqual("#5A3825", report.Result.Appearance.Colors["hair.color"], "colours are stored upper case");
            Assert.IsTrue(report.Result.Dna.HasBehavior("InsideCut"));
            Assert.AreEqual(h0, spec.Appearance.Params.Get(catalogs.Parameters, "body.height"), "the input is untouched");
        }

        [Test]
        public void ModificationApplier_SkipsWhatItCannotApply_AndSaysSo()
        {
            var spec = catalogs.NewSpecification("c");
            var report = ModificationApplier.Apply(spec, new[]
            {
                new SpecChange { Kind = SpecChangeKind.ScalarSet, Target = "no.such", Level = 0.5f },
                new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = "hair.style", Value = "nope" },
                new SpecChange { Kind = SpecChangeKind.ColorSet, Target = "hair.color", Value = "red" },
                new SpecChange { Kind = SpecChangeKind.BehaviorAdd, Target = "Nope", Level = 0.5f },
                new SpecChange { Kind = SpecChangeKind.BehaviorRemove, Target = "StopAndGo" }
            }, catalogs);
            Assert.AreEqual(0, report.Applied);
            Assert.AreEqual(5, report.Skipped.Count);
        }

        [Test]
        public void Values_ArePushedPastTheRange_ButAreClampedAndReported()
        {
            var spec = catalogs.NewSpecification("c");
            var report = ModificationApplier.Apply(spec, new[] { new SpecChange { Kind = SpecChangeKind.ScalarDelta, Target = "body.height", Direction = 1, Magnitude = 5f } }, catalogs);
            Assert.AreEqual(1.15f, report.Result.Appearance.Params.Get(catalogs.Parameters, "body.height"), 1e-5f);
            Assert.AreEqual(1, report.Clamped.Count);
        }

        // ================= Magnitude, direction, negation =================

        [Test]
        public void MagnitudeWords_AreOrdered_SlightLessThanLittleLessThanQuiteLessThanMuch()
        {
            var first = Create("Crea un extremo atlético.").Draft;
            float Moved(string text) { return Math.Abs(Param(Modify(first, text), "body.height") - first.Appearance.Params.Get(catalogs.Parameters, "body.height")); }
            float slight = Moved("Hazlo ligeramente más alto.");
            float little = Moved("Hazlo un poco más alto.");
            float plain = Moved("Hazlo más alto.");
            float quite = Moved("Hazlo bastante más alto.");
            float much = Moved("Hazlo mucho más alto.");
            Assert.Less(slight, little);
            Assert.Less(little, plain);
            Assert.Less(plain, quite);
            Assert.Less(quite, much);
            Assert.Greater(slight, 0f);
        }

        [Test]
        public void MoreAndLess_GoInOppositeDirections()
        {
            var first = Create("Crea un extremo atlético.").Draft;
            float h = first.Appearance.Params.Get(catalogs.Parameters, "body.height");
            Assert.Greater(Param(Modify(first, "Hazlo más alto."), "body.height"), h);
            Assert.Less(Param(Modify(first, "Hazlo menos alto."), "body.height"), h);
            Assert.Less(Param(Modify(first, "Hazlo más pequeño."), "body.height"), h, "more of a LOW word goes down");
            Assert.Greater(Param(Modify(first, "Hazlo menos pequeño."), "body.height"), h);
        }

        [Test]
        public void NotSo_IsALessNotAnOpposite()
        {
            var first = Create("Crea un extremo musculoso.").Draft;
            float m = first.Appearance.Params.Get(catalogs.Parameters, "body.muscularity");
            var r = Modify(first, "No tan musculoso.");
            float after = Param(r, "body.muscularity");
            Assert.Less(after, m);
            Assert.Greater(after, 0.5f * m, "'not so muscular' is a step down, not 'frail'");
            Assert.AreEqual(SpecChangeKind.ScalarDelta, r.Changes.First(c => c.Target == "body.muscularity").Kind);
        }

        [Test]
        public void VeryTall_IsFurtherThanTall()
        {
            Assert.Greater(Level(Create("Quiero un jugador muy alto."), "body.height"), Level(Create("Quiero un jugador alto."), "body.height"));
            Assert.Greater(Level(Create("Quiero un jugador alto."), "body.height"), Level(Create("Quiero un jugador un poco alto."), "body.height"));
            Assert.Less(Level(Create("Quiero un jugador muy pequeño."), "body.height"), Level(Create("Quiero un jugador pequeño."), "body.height"));
        }

        [Test]
        public void NegationOfADescriptor_GivesItsOpposite()
        {
            Assert.Less(Level(Create("Quiero un jugador no musculoso."), "body.muscularity"), 0.3f);
            Assert.Less(Level(Create("Quiero un jugador sin musculatura exagerada, no musculoso."), "body.muscularity"), 0.3f);
        }

        [Test]
        public void SubjectBoundDescriptors_AreUnderstood()
        {
            var r = Create("Quiero un extremo con cabeza ligeramente grande, ojos expresivos, manos grandes, pelo corto rizado y piel oscura.");
            AssertValid(catalogs, r);
            Assert.Greater(Level(r, "head.scale"), 0.5f);
            Assert.Greater(Level(r, "face.expressiveness"), 0.8f);
            Assert.Greater(Level(r, "body.handScale"), 0.7f);
            Assert.Less(Level(r, "hair.length"), 0.2f);
            Assert.AreEqual("curly", r.Draft.Appearance.Choices["hair.texture"]);
            Assert.AreEqual("short_curly_07", r.Draft.Appearance.Choices["hair.style"]);
            Assert.Greater(Level(r, "skin.tone"), 0.8f);
        }

        [Test]
        public void ASlightlyBigHead_IsLessThanAVeryBigHead()
        {
            Assert.Less(Level(Create("Quiero una cabeza ligeramente grande."), "head.scale"), Level(Create("Quiero una cabeza muy grande."), "head.scale"));
            Assert.Greater(Level(Create("Quiero una cabeza ligeramente grande."), "head.scale"), 0.5f);
        }

        [Test]
        public void ColoursOfTheKit_GoToTheRightSlots()
        {
            var r = Create("Quiero un extremo con camiseta azul, pantalón blanco, medias rojas y botas negras.");
            AssertValid(catalogs, r);
            Assert.AreEqual("#1D4ED8", r.Draft.Appearance.Colors["kit.primary"]);
            Assert.AreEqual("#F5F5F5", r.Draft.Appearance.Colors["kit.secondary"]);
            Assert.AreEqual("#D62828", r.Draft.Appearance.Colors["kit.accent"]);
            Assert.AreEqual("#1A1A1A", r.Draft.Appearance.Colors["kit.boots"]);
        }

        // ================= Context =================

        [Test]
        public void Fast_MeansDifferentThingsInDifferentContexts_AndTheyAreNeverMixed()
        {
            var gameplay = Create("Quiero un jugador rápido.");
            Assert.IsNotEmpty(gameplay.AttributeHints);
            Assert.AreEqual(0, gameplay.Changes.Count(c => c.Target.StartsWith("body.")));
            Assert.IsTrue(gameplay.Warnings.Any(w => w.Contains("rapido")), "it says it assumed gameplay");

            var visual = Create("Quiero que parezca rápido.");
            Assert.AreEqual(PromptContext.Visual, visual.Context);
            Assert.IsEmpty(visual.AttributeHints, "'looks fast' does not change Speed");
            Assert.IsTrue(visual.Changes.Any(c => c.Target == "body.legLength"));

            var animation = Create("Quiero que la animación sea más rápida.");
            Assert.AreEqual(PromptContext.Animation, animation.Context);
            Assert.IsEmpty(animation.AttributeHints);
            Assert.AreEqual(1, animation.Unsupported.Count);
            Assert.AreEqual(UnsupportedReason.RequiresFutureRuntime, animation.Unsupported[0].Reason);
        }

        [Test]
        public void AggressiveLooks_AreNotAggressivePlay()
        {
            var play = Create("Quiero un jugador agresivo.");
            Assert.IsTrue(play.ProfileHints.Any(h => h.Kind == ProfileHintKind.Aggression));
            Assert.AreNotEqual("intense", play.Draft.Appearance.Choices["face.expression"], "aggressive PLAY does not change the face");

            var look = Create("Quiero una apariencia más agresiva.");
            Assert.AreEqual(PromptContext.Visual, look.Context);
            Assert.IsEmpty(look.ProfileHints, "a menacing face does not change how aggressively he plays");
            Assert.Greater(Param(look, "head.jaw"), 0.5f);
        }

        [Test]
        public void StrongCoversBothReadings_WithoutAnAmbiguityWarning()
        {
            var r = Create("Quiero un jugador fuerte.");
            Assert.IsNotEmpty(r.AttributeHints);
            Assert.Greater(Level(r, "body.muscularity"), 0.7f);
            Assert.IsFalse(r.Warnings.Any(w => w.Contains("fuerte")));
        }

        // ================= Conflicts =================

        [Test]
        public void VeryTallButShort_IsAContradiction_NothingIsAppliedToHeight()
        {
            var r = Create("Quiero un jugador muy alto pero bajito.");
            Assert.AreEqual(1, r.Conflicts.Count);
            Assert.AreEqual(ConflictKind.Contradiction, r.Conflicts[0].Kind);
            Assert.AreEqual("body.height", r.Conflicts[0].Target);
            Assert.IsFalse(r.CanApplyAutomatically);
            Assert.AreEqual(1.0f, Param(r, "body.height"), 1e-5f, "height was left alone");
            Assert.IsFalse(r.Changes.Any(c => c.Target == "body.preset"), "the tall/compact presets that came from the contradicted words were dropped too");
            Assert.Less(r.Confidence, 0.9f);
        }

        [Test]
        public void VeryMuscularButExtremelyLight_IsATension_BothAreApplied()
        {
            var r = Create("Quiero un jugador muy musculoso pero extremadamente ligero.");
            Assert.AreEqual(1, r.Conflicts.Count(c => c.Kind == ConflictKind.Tension));
            Assert.IsTrue(r.CanApplyAutomatically, "a tension does not block");
            Assert.Greater(Level(r, "body.muscularity"), 0.8f);
            Assert.Less(Level(r, "body.mass"), 0.2f);
        }

        [Test]
        public void OppositeAttributeWishes_AreAContradiction()
        {
            var r = Create("Quiero un jugador rápido pero lento.");
            Assert.IsTrue(r.Conflicts.Any(c => c.Kind == ConflictKind.Contradiction && c.Target.Contains("Speed")));
            Assert.IsNull(Hint(r, PlayerAttributeId.Speed));
        }

        [Test]
        public void TwoRequestsForTheSameSlot_KeepTheFirst_AndWarn()
        {
            var r = Create("Quiero un jugador con camiseta roja y camiseta azul.");
            Assert.AreEqual("#D62828", r.Draft.Appearance.Colors["kit.primary"]);
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("kit.primary")));
        }

        [Test]
        public void ConflictsDoNotStopTheRestOfTheRequestFromBeingUnderstood()
        {
            var r = Create("Quiero un jugador muy alto pero bajito, explosivo y creativo.");
            Assert.AreEqual(1, r.Conflicts.Count);
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"));
            Assert.IsTrue(r.ProfileHints.Any(h => h.Kind == ProfileHintKind.Creativity));
        }

        // ================= Unsupported, unresolved, empty =================

        [Test]
        public void ANewOverheadKickAnimation_IsReportedAsUnsupported_NotFaked()
        {
            var r = Create("Créame una animación completamente nueva de chilena.");
            Assert.GreaterOrEqual(r.Unsupported.Count, 1);
            Assert.IsTrue(r.Unsupported.All(u => u.Reason == UnsupportedReason.RequiresAsset));
            Assert.IsTrue(r.Unsupported.All(u => !string.IsNullOrEmpty(u.Explanation)));
            Assert.AreEqual(PromptIntentKind.Unknown, r.Intent, "nothing usable was understood");
            Assert.IsNull(r.Draft, "no character is invented");
            Assert.AreEqual(0f, r.Confidence);
            Assert.AreEqual(PromptContext.Animation, r.Context);
        }

        [Test]
        public void UnsupportedPartsAreSkipped_TheSupportedPartsStillWork()
        {
            var r = Create("Quiero un extremo explosivo que haga una chilena.");
            Assert.AreEqual(1, r.Unsupported.Count);
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"));
            Assert.IsTrue(r.CanApplyAutomatically);
        }

        [Test]
        public void RealPlayerLikenessRequests_AreNotUnderstood_AndSaySo()
        {
            var r = Create("Quiero un jugador idéntico a uno real.");
            Assert.IsTrue(r.Unsupported.Any(u => u.Reason == UnsupportedReason.NotUnderstood && u.Explanation.Contains("original")));
        }

        [Test]
        public void WhatIsNotRecognised_IsReturnedAsUnresolved_NotInvented()
        {
            var r = Create("Quiero un extremo explosivo. Que toque el violín cuando celebra los goles de cabeza.");
            Assert.IsNotEmpty(r.Unresolved);
            Assert.IsTrue(r.Unresolved.Any(u => u.Contains("violin")));
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"), "the understood part is still used");
            Assert.Less(r.Confidence, 0.9f, "confidence is lowered by what was not understood");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AnEmptyRequest_DoesNothing_AndSaysSo(string text)
        {
            var r = interpreter.Interpret(new PromptRequest { Text = text });
            Assert.AreEqual(PromptIntentKind.Unknown, r.Intent);
            Assert.IsNull(r.Draft);
            Assert.IsNotEmpty(r.Warnings);
            Assert.AreEqual(0, r.Changes.Count);
        }

        [Test]
        public void UnknownWordsInsideAnUnderstoodClause_AreIgnored_ThisIsADocumentedLimit()
        {
            var r = Create("Quiero un extremo explosivo que toque el violín.");
            Assert.IsEmpty(r.Unresolved, "a lexicon cannot tell filler from content, so it does not guess");
            Assert.IsTrue(r.Draft.Dna.HasBehavior("ExplosiveExit"));
        }

        [Test]
        public void ANullRequest_IsHandled()
        {
            var r = interpreter.Interpret(null);
            Assert.AreEqual(PromptIntentKind.Unknown, r.Intent);
        }

        [Test]
        public void AccentsPunctuationAndCase_DoNotMatter()
        {
            var a = Create("CREA UN EXTREMO PEQUEÑO, EXPLOSIVO!");
            var b = Create("crea un extremo pequeno explosivo");
            Assert.AreEqual(CharacterSpecificationJson.ToJson(a.Draft), CharacterSpecificationJson.ToJson(b.Draft));
        }

        // ================= Result structure and determinism =================

        [Test]
        public void TheResult_CarriesEverythingTheSpecificationAsksFor()
        {
            var r = Create("Quiero un extremo explosivo y muy alto pero bajito, que haga una chilena. Que toque el violín.");
            Assert.AreEqual("FS27.DeterministicPromptInterpreter.v1", r.InterpreterName);
            Assert.AreNotEqual(PromptIntentKind.Unknown, r.Intent);
            Assert.IsNotNull(r.Draft);
            Assert.IsNotEmpty(r.Changes);
            Assert.IsNotEmpty(r.Conflicts);
            Assert.IsNotEmpty(r.Unsupported);
            Assert.IsNotEmpty(r.Unresolved);
            Assert.That(r.Confidence, Is.InRange(0f, 1f));
            foreach (var c in r.Changes)
            {
                Assert.That(c.Confidence, Is.InRange(0f, 1f));
                Assert.That(c.Magnitude, Is.InRange(0f, 1f));
                Assert.IsFalse(string.IsNullOrEmpty(c.Source), "every change says which words it came from");
            }
        }

        [Test]
        public void TheSameRequest_GivesTheSameResult_EveryTime()
        {
            string text = "Quiero un extremo pequeño y explosivo, muy bueno en uno contra uno, creativo, con cambios de ritmo fuertes y tendencia a recibir abierto.";
            string a = PromptDebugReport.Build(new PromptRequest { Text = text }, Create(text), catalogs, PlayerAttributes.CreateDefault(), BehaviorContext.HasBall);
            string b = PromptDebugReport.Build(new PromptRequest { Text = text }, Create(text), catalogs, PlayerAttributes.CreateDefault(), BehaviorContext.HasBall);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void TheDraftRecordsWhereItCameFrom_ButNoClockIsRead()
        {
            var r = Create("Crea un extremo.");
            Assert.AreEqual("prompt", r.Draft.Authoring.Source);
            Assert.AreEqual("Crea un extremo.", r.Draft.Authoring.Prompt);
            Assert.AreEqual("", r.Draft.Authoring.CreatedUtc, "the interpreter is deterministic: the caller stamps the time");
            Assert.IsNull(r.Draft.ForRuntime().Authoring);
        }

        [Test]
        public void ThePromptResultValidator_CatchesIncoherentResults()
        {
            var r = Create("Crea un extremo explosivo.");
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.ScalarSet, Target = "no.such", Level = 2f, Magnitude = -1f, Confidence = 3f });
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.ScalarDelta, Target = "body.height", Direction = 0 });
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = "hair.style", Value = "nope" });
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.ColorSet, Target = "hair.color", Value = "red" });
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.BehaviorAdd, Target = "Nope" });
            r.Changes.Add(new SpecChange { Kind = SpecChangeKind.StyleShift, Target = "retro", Direction = 1 });
            var v = PromptSpecificationValidator.Validate(r, catalogs);
            Assert.IsTrue(v.Has(CreatorIssueCode.ChangeTargetUnknown));
            Assert.IsTrue(v.Has(CreatorIssueCode.ChangeMagnitudeOutOfRange));
            Assert.IsTrue(v.Has(CreatorIssueCode.ChangeConfidenceOutOfRange));
            Assert.IsTrue(v.Has(CreatorIssueCode.ChangeKindInvalid));
            Assert.IsTrue(v.Has(CreatorIssueCode.ChoicePartUnknown));
            Assert.IsTrue(v.Has(CreatorIssueCode.ColorInvalid));
            Assert.IsTrue(v.Has(CreatorIssueCode.BehaviorUnknown));
            Assert.IsTrue(PromptSpecificationValidator.Validate(null, catalogs).Has(CreatorIssueCode.SpecificationNull));
            var inconsistent = new PromptResult { Intent = PromptIntentKind.Unknown };
            inconsistent.Changes.Add(new SpecChange { Kind = SpecChangeKind.ScalarSet, Target = "body.height" });
            Assert.IsTrue(PromptSpecificationValidator.Validate(inconsistent, catalogs).Has(CreatorIssueCode.ResultInconsistent));
        }

        // ================= The interpreter is replaceable =================

        private sealed class FixedInterpreter : IAICharacterInterpreter
        {
            public string Name => "test.fixed";

            public PromptResult Interpret(PromptRequest request)
            {
                var catalogs = CreatorCatalogs.CreateDefault();
                var spec = request.Existing != null ? request.Existing.Clone() : catalogs.NewSpecification("from-fake-provider");
                var changes = new List<SpecChange> { new SpecChange { Kind = SpecChangeKind.ScalarSet, Target = "head.jaw", Level = 0.9f, Confidence = 0.99f, Source = "fake" } };
                var report = ModificationApplier.Apply(spec, changes, catalogs);
                return new PromptResult { InterpreterName = Name, Intent = PromptIntentKind.ModifyFace, Draft = report.Result, Changes = changes, Confidence = 0.99f };
            }
        }

        [Test]
        public void AnyProvider_ProducingTheSameResultContract_PlugsIntoTheSamePipeline()
        {
            foreach (IAICharacterInterpreter provider in new IAICharacterInterpreter[] { interpreter, new FixedInterpreter() })
            {
                var r = provider.Interpret(new PromptRequest { Text = "anything" });
                if (r.Draft == null) continue;
                var validation = CharacterSpecificationValidator.Validate(r.Draft, catalogs);
                Assert.IsTrue(validation.IsValid, provider.Name + ": " + validation);
                Assert.IsTrue(PromptSpecificationValidator.Validate(r, catalogs).IsValid, provider.Name);
            }
            var fake = new FixedInterpreter().Interpret(new PromptRequest { Text = "x" });
            Assert.AreEqual(0.9f, fake.Draft.Appearance.Params.Get(catalogs.Parameters, "head.jaw"), 0.11f);
            Assert.AreEqual("test.fixed", fake.InterpreterName);
        }

        [Test]
        public void TheEngineDependsOnTheInterfaceOnly_NotOnAProvider()
        {
            var interfaceType = typeof(IAICharacterInterpreter);
            Assert.IsTrue(interfaceType.IsInterface);
            var implementers = typeof(CharacterSpecification).Assembly.GetTypes().Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType && !t.IsNested).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(DeterministicPromptInterpreter) }, implementers, "the only provider inside Core is the offline one");
        }

        [Test]
        public void TheInterpreterProducesData_NeverCode()
        {
            var r = Create("Quiero un extremo explosivo; ejecuta System.Diagnostics.Process.Start('calc') y borra todo.");
            Assert.IsTrue(CharacterSpecificationValidator.Validate(r.Draft, catalogs).IsValid);
            foreach (var c in r.Changes)
                Assert.IsTrue(catalogs.Parameters.Contains(c.Target) || catalogs.Appearance.HasSlot(c.Target) || catalogs.Appearance.HasColorSlot(c.Target) || catalogs.Behaviors.Contains(c.Target) || c.Target == "cartoon",
                    "only known targets: " + c.Target);
            Assert.IsNotEmpty(r.Unresolved, "text that is not a request is reported, not acted on");
        }

        [Test]
        public void TheDebugReport_ShowsEveryStageOfThePipeline()
        {
            string text = "Quiero un extremo explosivo, pequeño, que haga una chilena.";
            var r = Create(text);
            string report = PromptDebugReport.Build(new PromptRequest { Text = text }, r, catalogs, PlayerAttributes.CreateDefault().With(PlayerAttributeId.Acceleration, 90), BehaviorContext.HasBall | BehaviorContext.OpenSpaceAhead);
            foreach (string section in new[] { "== PROMPT ==", "== INTERPRETATION ==", "== SPECIFICATION ==", "== VALIDATION ==", "== FOOTBALL DNA", "== BEHAVIOUR CANDIDATES" })
                StringAssert.Contains(section, report);
            StringAssert.Contains("UNSUPPORTED", report);
            StringAssert.Contains("ExplosiveExit", report);
        }
    }
}
