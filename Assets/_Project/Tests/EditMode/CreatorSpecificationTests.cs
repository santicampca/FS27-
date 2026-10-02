using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CreatorSpecificationTests
    {
        private CreatorCatalogs catalogs;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
        }

        private CharacterSpecification Valid()
        {
            var s = catalogs.NewSpecification("char-001");
            s.Appearance.Params.Set(catalogs.Parameters, "head.scale", 1.1f);
            s.Appearance.Choices["hair.style"] = "short_curly_07";
            s.Appearance.Colors["hair.color"] = "#1A1A1A";
            s.Appearance.Colors["kit.primary"] = "#D62828";
            s.Dna.Params.Set(catalogs.Parameters, "dribbling.takeOn", 0.9f);
            s.Dna.SetBehavior(DefaultBehaviors.StopAndGo, 0.8f);
            return s;
        }

        // ================= Parameter catalog =================

        [Test]
        public void TheCatalog_HasUniqueIds_SensibleRanges_AndBothDomains()
        {
            var ids = catalogs.Parameters.All.Select(p => p.Id).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            foreach (var p in catalogs.Parameters.All)
            {
                Assert.Less(p.Min, p.Max, p.Id);
                Assert.That(p.Default, Is.InRange(p.Min, p.Max), p.Id);
                Assert.IsTrue(p.Id.StartsWith(p.Group + "."), p.Id + " is in group " + p.Group);
            }
            Assert.Greater(catalogs.Parameters.InDomain(ParameterDomain.Appearance).Count, 25);
            Assert.Greater(catalogs.Parameters.InDomain(ParameterDomain.FootballDna).Count, 40);
        }

        [Test]
        public void FootballDnaCoversEveryRequestedCategory()
        {
            var groups = new HashSet<string>(catalogs.Parameters.InDomain(ParameterDomain.FootballDna).Select(p => p.Group));
            foreach (string g in new[] { "movement", "dribbling", "passing", "shooting", "positioning", "decision", "defending" })
                Assert.IsTrue(groups.Contains(g), g);
            foreach (string id in new[]
            {
                "movement.accelerationTendency", "movement.decelerationTendency", "movement.turningTendency", "movement.aggression", "movement.runTiming",
                "movement.supportMovement", "movement.spaceSeeking", "movement.diagonal", "movement.delayedRuns", "movement.blindSideRuns",
                "dribbling.takeOn", "dribbling.closeControl", "dribbling.changeOfPace", "dribbling.stopAndGo", "dribbling.bodyFeint", "dribbling.directionChange",
                "passing.short", "passing.progressive", "passing.throughBall", "passing.cross", "passing.safe", "passing.risky", "passing.oneTouch",
                "shooting.frequency", "shooting.longShot", "shooting.finesse", "shooting.power", "shooting.firstTime", "shooting.weakFootUsage",
                "positioning.boxPresence", "positioning.halfSpace", "positioning.width", "positioning.depth", "positioning.dropping", "positioning.attackingRuns", "positioning.defensive",
                "decision.patience", "decision.directness", "decision.pressureResponse", "decision.scanning",
                "defending.pressing", "defending.marking", "defending.interception", "defending.aggression", "defending.retreat", "defending.laneBlocking"
            })
                Assert.IsTrue(catalogs.Parameters.Contains(id), id);
        }

        [Test]
        public void TheCatalogRefusesDuplicatesAndNonsenseRanges()
        {
            var c = new ParameterCatalog();
            Assert.IsTrue(c.TryAdd(new ParameterDefinition { Id = "a.b", Min = 0, Max = 1, Default = 0.5f }));
            Assert.IsFalse(c.TryAdd(new ParameterDefinition { Id = "a.b", Min = 0, Max = 1, Default = 0.5f }), "same id");
            Assert.IsFalse(c.TryAdd(new ParameterDefinition { Id = "a.c", Min = 1, Max = 0, Default = 0.5f }), "min > max");
            Assert.IsFalse(c.TryAdd(new ParameterDefinition { Id = "a.d", Min = 0, Max = 1, Default = 2f }), "default outside range");
            Assert.IsFalse(c.TryAdd(new ParameterDefinition { Id = "", Min = 0, Max = 1, Default = 0.5f }));
            Assert.AreEqual(1, c.Count);
        }

        [Test]
        public void ANewTendency_IsOneRowInTheCatalog_AndWorksEverywhere()
        {
            catalogs.Parameters.TryAdd(new ParameterDefinition { Id = "dribbling.nutmeg", Domain = ParameterDomain.FootballDna, Group = "dribbling", Min = 0, Max = 1, Default = 0.5f });
            var spec = Valid();
            Assert.IsTrue(spec.Dna.Params.Set(catalogs.Parameters, "dribbling.nutmeg", 0.9f));
            Assert.IsTrue(CharacterSpecificationValidator.Validate(spec, catalogs).IsValid);
            var back = RoundTrip(spec);
            Assert.AreEqual(0.9f, back.Dna.Get(catalogs.Parameters, "dribbling.nutmeg"), 1e-4f);
        }

        [Test]
        public void ParameterSet_StoresOnlyWhatDiffersFromNeutral_AndClamps()
        {
            var set = new ParameterSet();
            Assert.IsTrue(set.Set(catalogs.Parameters, "body.height", 1.0f));
            Assert.AreEqual(0, set.Values.Count, "neutral is not stored");
            set.Set(catalogs.Parameters, "body.height", 5f);
            Assert.AreEqual(1.15f, set.Get(catalogs.Parameters, "body.height"), 1e-5f, "clamped to the range");
            set.Set(catalogs.Parameters, "body.height", 1.0f);
            Assert.AreEqual(0, set.Values.Count, "back to neutral removes it");
            Assert.IsFalse(set.Set(catalogs.Parameters, "no.such", 1f));
            set.Set(catalogs.Parameters, "head.jaw", float.NaN);
            Assert.AreEqual(0.5f, set.Get(catalogs.Parameters, "head.jaw"), "NaN becomes the default");
        }

        [Test]
        public void LevelsAndValuesConvert_BothWays()
        {
            catalogs.Parameters.TryGet("body.height", out var p);
            Assert.AreEqual(0.85f, p.FromLevel(0f), 1e-5f);
            Assert.AreEqual(1.15f, p.FromLevel(1f), 1e-5f);
            Assert.AreEqual(0.5f, p.ToLevel(1.0f), 1e-5f);
            Assert.AreEqual(1.15f, p.FromLevel(7f), 1e-5f, "levels are clamped");
        }

        // ================= Specification validation =================

        [Test]
        public void AValidSpecification_Passes_AndTheNewSpecificationIsValid()
        {
            var r = CharacterSpecificationValidator.Validate(Valid(), catalogs);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsTrue(CharacterSpecificationValidator.Validate(catalogs.NewSpecification("char-new"), catalogs).IsValid);
        }

        [Test]
        public void TheNewSpecification_StartsWithTheStylesLook()
        {
            var s = catalogs.NewSpecification("c");
            Assert.AreEqual(DefaultStyles.CartoonSports, s.Appearance.StyleId);
            Assert.Greater(s.Appearance.Params.Get(catalogs.Parameters, "head.scale"), 1.0f, "slightly big head");
            Assert.Greater(s.Appearance.Params.Get(catalogs.Parameters, "body.handScale"), 1.0f, "hands emphasised");
            Assert.Greater(s.Appearance.Params.Get(catalogs.Parameters, "body.footScale"), 1.0f, "boots emphasised");
            Assert.AreEqual("athletic", s.Appearance.Choices["body.preset"]);
        }

        [Test]
        public void ANullSpecification_IsReported()
        {
            Assert.IsTrue(CharacterSpecificationValidator.Validate(null, catalogs).Has(CreatorIssueCode.SpecificationNull));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        public void ABadCharacterId_IsReported(string id)
        {
            var s = Valid(); s.CharacterId = id;
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).Has(CreatorIssueCode.CharacterIdInvalid));
        }

        [Test]
        public void UnknownParameters_WrongDomains_AndOutOfRangeValues_AreReportedWithTheirNames()
        {
            var s = Valid();
            s.Appearance.Params.Values["no.such"] = 1f;
            s.Appearance.Params.Values["dribbling.takeOn"] = 0.5f;          // a DNA parameter in appearance data
            s.Appearance.Params.Values["body.height"] = 9f;
            s.Appearance.Params.Values["head.scale"] = float.NaN;
            s.Dna.Params.Values["body.mass"] = 1f;                          // an appearance parameter in DNA data
            s.Dna.Params.Values["dribbling.stopAndGo"] = 1.5f;
            var r = CharacterSpecificationValidator.Validate(s, catalogs);
            Assert.AreEqual(1, r.CountOf(CreatorIssueCode.ParameterUnknown));
            Assert.AreEqual(2, r.CountOf(CreatorIssueCode.ParameterWrongDomain));
            Assert.AreEqual(2, r.CountOf(CreatorIssueCode.ParameterOutOfRange));
            Assert.AreEqual(1, r.CountOf(CreatorIssueCode.ParameterNotANumber));
            StringAssert.Contains("body.height", r.Issues.First(i => i.Code == CreatorIssueCode.ParameterOutOfRange).Subject);
        }

        [Test]
        public void ChoicesColoursStyleAndBaseModel_AreChecked()
        {
            var s = Valid();
            s.Appearance.Choices["no.slot"] = "x";
            s.Appearance.Choices["hair.style"] = "no_such_style";
            s.Appearance.Colors["no.slot"] = "#FFFFFF";
            s.Appearance.Colors["kit.accent"] = "red";
            s.Appearance.StyleId = "NOPE";
            s.BaseModelId = "no_base";
            var r = CharacterSpecificationValidator.Validate(s, catalogs);
            Assert.IsTrue(r.Has(CreatorIssueCode.ChoiceSlotUnknown));
            Assert.IsTrue(r.Has(CreatorIssueCode.ChoicePartUnknown));
            Assert.IsTrue(r.Has(CreatorIssueCode.ColorSlotUnknown));
            Assert.IsTrue(r.Has(CreatorIssueCode.ColorInvalid));
            Assert.IsTrue(r.Has(CreatorIssueCode.StyleUnknown));
            Assert.IsTrue(r.Has(CreatorIssueCode.BaseModelUnknown));
        }

        [TestCase("#000000", true)]
        [TestCase("#aBc123", true)]
        [TestCase("#FFF", false)]
        [TestCase("FFFFFF", false)]
        [TestCase("#GGGGGG", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HexColours(string value, bool valid)
        {
            Assert.AreEqual(valid, CharacterSpecificationValidator.IsHexColor(value));
        }

        [Test]
        public void APartThatDoesNotFitTheBaseModel_IsRejected()
        {
            catalogs.Appearance.TryAddBaseModel(new BaseModelDefinition { Id = "base_b", RigId = "rig_b" });
            catalogs.Appearance.TryAddPart(new PartDefinition { Slot = "hair.style", Id = "only_for_a", BaseModels = new List<string> { DefaultAppearanceCatalog.BaseA } });
            var s = Valid(); s.Appearance.Choices["hair.style"] = "only_for_a";
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).IsValid);
            s.BaseModelId = "base_b";
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).Has(CreatorIssueCode.ChoicePartIncompatibleWithBase));
        }

        [Test]
        public void TheBaseModelIsReplaceable_TheSameSpecificationWorksOnAnotherBase()
        {
            catalogs.Appearance.TryAddBaseModel(new BaseModelDefinition { Id = "base_b", RigId = "rig_b" });
            var s = Valid(); s.BaseModelId = "base_b";
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).IsValid, "parts that fit every base keep working");
            var resolver = new CatalogAppearanceResolver(catalogs);
            Assert.AreEqual("rig_b", resolver.Resolve(s).RigId);
        }

        [Test]
        public void AllProblemsAreCollected_NotJustTheFirst()
        {
            var s = Valid(); s.CharacterId = ""; s.Appearance.StyleId = "x"; s.Dna.Behaviors.Add(new BehaviorEntry("Nope", 0.5f)); s.SchemaVersion = "bad";
            var r = CharacterSpecificationValidator.Validate(s, catalogs);
            Assert.GreaterOrEqual(r.Issues.Count(i => i.Severity == CreatorSeverity.Error), 4);
        }

        // ================= JSON, size, runtime vs authoring =================

        private CharacterSpecification RoundTrip(CharacterSpecification s, bool authoring = false)
        {
            string text = CharacterSpecificationJson.ToJson(s, authoring);
            var r = new CreatorValidationResult();
            Assert.IsTrue(CharacterSpecificationJson.TryFromJson(text, new SchemaMigrator(), out var back, r), r.ToString());
            return back;
        }

        [Test]
        public void JsonRoundTrip_KeepsEverything()
        {
            var s = Valid();
            var back = RoundTrip(s);
            Assert.AreEqual(s.CharacterId, back.CharacterId);
            Assert.AreEqual(s.BaseModelId, back.BaseModelId);
            Assert.AreEqual(s.Appearance.StyleId, back.Appearance.StyleId);
            CollectionAssert.AreEqual(s.Appearance.Params.Values, back.Appearance.Params.Values);
            CollectionAssert.AreEqual(s.Appearance.Choices, back.Appearance.Choices);
            CollectionAssert.AreEqual(s.Appearance.Colors, back.Appearance.Colors);
            CollectionAssert.AreEqual(s.Dna.Params.Values, back.Dna.Params.Values);
            Assert.AreEqual(1, back.Dna.Behaviors.Count);
            Assert.AreEqual(DefaultBehaviors.StopAndGo, back.Dna.Behaviors[0].Id);
            Assert.AreEqual(0.8f, back.Dna.Behaviors[0].Weight, 1e-4f);
            Assert.IsTrue(CharacterSpecificationValidator.Validate(back, catalogs).IsValid);
        }

        [Test]
        public void TheJson_IsDeterministic_WhateverTheOrderThingsWereAdded()
        {
            var a = catalogs.NewSpecification("c"); var b = catalogs.NewSpecification("c");
            a.Appearance.Params.Set(catalogs.Parameters, "head.jaw", 0.8f); a.Appearance.Params.Set(catalogs.Parameters, "face.noseSize", 1.1f);
            b.Appearance.Params.Set(catalogs.Parameters, "face.noseSize", 1.1f); b.Appearance.Params.Set(catalogs.Parameters, "head.jaw", 0.8f);
            Assert.AreEqual(CharacterSpecificationJson.ToJson(a), CharacterSpecificationJson.ToJson(b));
            Assert.AreEqual(CharacterSpecificationJson.ToJson(a), CharacterSpecificationJson.ToJson(a));
        }

        [Test]
        public void ATypicalCharacter_IsTiny()
        {
            var s = Valid();
            string text = CharacterSpecificationJson.ToJson(s);
            Assert.Less(text.Length, 1500, text);
            StringAssert.DoesNotContain("body.height", text, "neutral values are not stored");
        }

        [Test]
        public void AFullyCustomisedCharacter_StaysSmall()
        {
            var s = catalogs.NewSpecification("c");
            foreach (var p in catalogs.Parameters.All)
                (p.Domain == ParameterDomain.Appearance ? s.Appearance.Params : s.Dna.Params).Set(catalogs.Parameters, p.Id, p.FromLevel(0.9f));
            foreach (var b in DefaultBehaviors.Create().All) s.Dna.SetBehavior(b.Id, 0.9f);
            foreach (string slot in catalogs.Appearance.Slots) s.Appearance.Choices[slot] = catalogs.Appearance.PartIds(slot)[0];
            foreach (string slot in catalogs.Appearance.ColorSlots) s.Appearance.Colors[slot] = "#123456";
            int bytes = System.Text.Encoding.UTF8.GetByteCount(CharacterSpecificationJson.ToJson(s));
            Assert.Less(bytes, 6000, "the worst case (everything set) is still a few KB, not MB");
        }

        [Test]
        public void TheSpecificationHoldsNoBinaryData_OnlyStringsNumbersAndIds()
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.Instance;
            foreach (Type t in new[] { typeof(CharacterSpecification), typeof(PlayerAppearance), typeof(FootballDNA), typeof(AuthoringData), typeof(ReferenceProfile) })
                foreach (FieldInfo field in t.GetFields(f))
                {
                    Assert.AreNotEqual(typeof(byte[]), field.FieldType, t.Name + "." + field.Name);
                    string n = field.FieldType.Name;
                    Assert.IsFalse(n.Contains("Texture") || n.Contains("Mesh") || n.Contains("Stream"), t.Name + "." + field.Name);
                }
        }

        [Test]
        public void TheRuntimeVersion_DropsAuthoringData_AndTheJsonOmitsItByDefault()
        {
            var s = Valid();
            s.Authoring = new AuthoringData { Source = "prompt", Generator = "x", Prompt = "a long prompt that must not ship", Notes = "n",
                Reference = new ReferenceProfile { SourceDescription = "public footage", AnalysisNotes = "notes", Provenance = "p", Confidence = 0.6f, GeneratedDate = "2026-01-01" } };
            string authoring = CharacterSpecificationJson.ToJson(s, includeAuthoring: true);
            string runtime = CharacterSpecificationJson.ToJson(s);
            StringAssert.Contains("a long prompt that must not ship", authoring);
            StringAssert.DoesNotContain("a long prompt", runtime);
            StringAssert.DoesNotContain("public footage", runtime);
            Assert.IsNull(s.ForRuntime().Authoring);
            Assert.IsNotNull(s.Authoring, "ForRuntime does not modify the original");
            Assert.Less(runtime.Length, authoring.Length);

            var back = RoundTrip(s, authoring: true);
            Assert.AreEqual("a long prompt that must not ship", back.Authoring.Prompt);
            Assert.AreEqual(0.6f, back.Authoring.Reference.Confidence, 1e-4f);
        }

        [Test]
        public void Clone_IsDeep()
        {
            var s = Valid();
            var c = s.Clone();
            c.Appearance.Params.Set(catalogs.Parameters, "head.scale", 1.3f);
            c.Appearance.Choices["hair.style"] = "buzz_02";
            c.Dna.SetBehavior(DefaultBehaviors.StopAndGo, 0.1f);
            Assert.AreEqual(1.1f, s.Appearance.Params.Get(catalogs.Parameters, "head.scale"), 1e-5f);
            Assert.AreEqual("short_curly_07", s.Appearance.Choices["hair.style"]);
            Assert.AreEqual(0.8f, s.Dna.Behaviors[0].Weight, 1e-5f);
        }

        // ================= Bad JSON =================

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("[1,2")]
        [TestCase("{\"a\":1} extra")]
        [TestCase("{\"a\":1,\"a\":2}")]
        public void InvalidJson_IsReported_NotThrown(string text)
        {
            var r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson(text, new SchemaMigrator(), out var spec, r));
            Assert.IsNull(spec);
            Assert.IsTrue(r.Has(CreatorIssueCode.JsonInvalid));
        }

        [Test]
        public void WrongShapes_AreReported()
        {
            foreach (string text in new[]
            {
                "[]",
                "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"appearance\":[]}",
                "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"appearance\":{\"params\":{\"body.height\":\"tall\"}}}",
                "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"appearance\":{\"choices\":{\"hair.style\":5}}}",
                "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"footballDna\":{\"behaviors\":[{\"weight\":1}]}}",
                "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"footballDna\":{\"behaviors\":{}}}"
            })
            {
                var r = new CreatorValidationResult();
                Assert.IsFalse(CharacterSpecificationJson.TryFromJson(text, new SchemaMigrator(), out _, r), text);
                Assert.IsTrue(r.Has(CreatorIssueCode.JsonShapeInvalid), text + " -> " + r);
            }
        }

        [Test]
        public void OutOfRangeValuesInJson_AreKeptSoValidationCanReportThem()
        {
            string text = "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"characterId\":\"c\",\"appearance\":{\"params\":{\"body.height\":3}}}";
            var r = new CreatorValidationResult();
            Assert.IsTrue(CharacterSpecificationJson.TryFromJson(text, new SchemaMigrator(), out var spec, r));
            Assert.AreEqual(3f, spec.Appearance.Params.Values["body.height"]);
            Assert.IsTrue(CharacterSpecificationValidator.Validate(spec, catalogs).Has(CreatorIssueCode.ParameterOutOfRange));
        }

        [Test]
        public void MiniJson_HandlesEscapesUnicodeAndNesting()
        {
            var v = Json.Parse("{\"a\":\"x\\ny\\u00e9\\\"\",\"b\":[1,2.5,-3e2,true,null],\"c\":{}}");
            Assert.AreEqual("x\nyé\"", v.GetString("a"));
            Assert.AreEqual(5, v.Members["b"].Items.Count);
            Assert.AreEqual(-300.0, v.Members["b"].Items[2].Number);
            Assert.IsTrue(Json.TryParse(Json.Write(v), out var again, out _));
            Assert.AreEqual(Json.Write(v), Json.Write(again));
            string deep = new string('[', 100) + new string(']', 100);
            Assert.IsFalse(Json.TryParse(deep, out _, out string err), "very deep nesting is refused");
            StringAssert.Contains("deep", err);
        }

        // ================= Schema versions and migration =================

        [Test]
        public void SchemaVersions_AreStableStrings()
        {
            // v2 (Creator Engine intelligence phase): optional behaviour settings, sequences, tendency confidence, seeds. v1 files still load (see FootballDna2Tests).
            Assert.AreEqual("FS27.CharacterSpecification.v2", CharacterSpecification.CurrentSchema);
            Assert.AreEqual("FS27.CharacterSpecification.v1", CharacterSpecification.SchemaV1);
            Assert.AreEqual("FS27.FootballDNA.v2", FootballDNA.CurrentSchema);
            Assert.AreEqual(CharacterSpecification.CurrentSchema, catalogs.NewSpecification("c").SchemaVersion);
            Assert.AreEqual(2, SchemaMigrator.VersionNumber("FS27.CharacterSpecification.v2"));
            Assert.AreEqual(12, SchemaMigrator.VersionNumber("X.v12"));
            Assert.AreEqual(-1, SchemaMigrator.VersionNumber("nonsense"));
        }

        [Test]
        public void ASchemaFromTheFuture_IsRefused_NotGuessed()
        {
            var r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson("{\"schemaVersion\":\"FS27.CharacterSpecification.v3\",\"characterId\":\"c\"}", new SchemaMigrator(), out _, r));
            Assert.IsTrue(r.Has(CreatorIssueCode.SchemaVersionNewer));
            var s = Valid(); s.SchemaVersion = "FS27.CharacterSpecification.v9";
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).Has(CreatorIssueCode.SchemaVersionNewer));
        }

        [Test]
        public void AMissingOrForeignSchema_IsReported()
        {
            var r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson("{\"characterId\":\"c\"}", new SchemaMigrator(), out _, r));
            Assert.IsTrue(r.Has(CreatorIssueCode.SchemaVersionMissing));
            r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson("{\"schemaVersion\":\"Other.Thing.v1\"}", new SchemaMigrator(), out _, r));
            Assert.IsTrue(r.Has(CreatorIssueCode.SchemaVersionUnsupported));
        }

        [Test]
        public void AnOldVersion_IsMigratedForwardStepByStep()
        {
            // A made-up "v0" that called the id "name": proves the mechanism, not a real legacy format.
            var m = new SchemaMigrator();
            m.Register("FS27.CharacterSpecification.v0", "FS27.CharacterSpecification.v1", old =>
            {
                var fixedUp = old;
                if (fixedUp.TryGet("name", out JsonValue name)) fixedUp.Set("characterId", name);
                return fixedUp;
            });
            var r = new CreatorValidationResult();
            Assert.IsTrue(CharacterSpecificationJson.TryFromJson("{\"schemaVersion\":\"FS27.CharacterSpecification.v0\",\"name\":\"old-001\"}", m, out var spec, r), r.ToString());
            Assert.AreEqual("old-001", spec.CharacterId);
            Assert.AreEqual(CharacterSpecification.CurrentSchema, spec.SchemaVersion);

            // And without the migration registered, the same file is refused clearly.
            r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson("{\"schemaVersion\":\"FS27.CharacterSpecification.v0\",\"name\":\"old-001\"}", new SchemaMigrator(), out _, r));
            Assert.IsTrue(r.Has(CreatorIssueCode.SchemaVersionUnsupported));
        }

        [Test]
        public void ABrokenMigrationStep_IsReported()
        {
            var m = new SchemaMigrator();
            m.Register("X.v0", "FS27.CharacterSpecification.v2", old => null);
            var r = new CreatorValidationResult();
            Assert.IsFalse(CharacterSpecificationJson.TryFromJson("{\"schemaVersion\":\"X.v0\"}", m, out _, r));
            Assert.IsTrue(r.Has(CreatorIssueCode.JsonShapeInvalid));
        }
    }
}
