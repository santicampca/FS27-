using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CharacterGenerationTests
    {
        private CreatorCatalogs catalogs;
        private MaterialCatalog materials;
        private ProceduralCharacterGenerator generator;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            materials = MaterialCatalog.CreateDefault();
            generator = new ProceduralCharacterGenerator(catalogs, materials);
        }

        private CharacterSpecification Spec(Action<CharacterSpecification> tweak = null)
        {
            CharacterSpecification s = catalogs.NewSpecification("gen-1");
            tweak?.Invoke(s);
            return s;
        }

        private void Set(CharacterSpecification s, string param, float level)
        {
            catalogs.Parameters.TryGet(param, out ParameterDefinition p);
            s.Appearance.Params.Set(catalogs.Parameters, param, p.FromLevel(level));
        }

        private CharacterAssemblyPlan Plan(CharacterSpecification s) { return CharacterAssemblyPlanner.Plan(s, catalogs, materials); }

        // ---------------- appearance 2.0 parameters ----------------

        [Test]
        public void TheCatalog_HasTheFinerAppearanceGroups()
        {
            var groups = new HashSet<string>(catalogs.Parameters.InDomain(ParameterDomain.Appearance).Select(p => p.Group));
            foreach (string g in new[] { "body", "head", "face", "eyes", "brows", "nose", "mouth", "ears", "hair", "skin", "facialHair", "style" })
                CollectionAssert.Contains(groups, g);
            foreach (ParameterDefinition p in catalogs.Parameters.All)
            {
                Assert.IsNotEmpty(p.Description, p.Id + " has no meaning");
                Assert.That(p.Default, Is.InRange(p.Min, p.Max), p.Id);
            }
        }

        // ---------------- compatibility ----------------

        [Test]
        public void ABuzzCutWithLongHairParameter_IsNormalised_AndSaid()
        {
            CharacterSpecification s = Spec(x => { x.Appearance.Choices["hair.style"] = "buzz_02"; x.Appearance.Params.Set(catalogs.Parameters, "hair.length", 0.4f); });
            NormalizationReport r = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault());
            Assert.IsFalse(r.Rejected);
            Assert.AreEqual(1, r.Changes.Count);
            Assert.LessOrEqual(r.Result.Appearance.Params.GetLevel(catalogs.Parameters, "hair.length"), 0.12f + 1e-3f);
            Assert.AreEqual(0.4f, s.Appearance.Params.Get(catalogs.Parameters, "hair.length"), 1e-4f, "the input must not change");
        }

        [Test]
        public void AnAfroOnAShavedHead_IsRejected_NotSilentlyRewritten()
        {
            CharacterSpecification s = Spec(x => { x.Appearance.Choices["hair.style"] = "afro_08"; x.Appearance.Params.Set(catalogs.Parameters, "hair.length", 0.0f); });
            NormalizationReport r = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault());
            Assert.IsTrue(r.Rejected);
            CreatorValidationResult v = AppearanceNormalizer.Validate(s, catalogs, CompatibilityRules.CreateDefault());
            Assert.IsFalse(v.IsValid);
            Assert.IsTrue(v.Has(CreatorIssueCode.AppearanceIncompatible));
        }

        [Test]
        public void AValueNobodySet_IsBroughtInLine_NeverRejected()
        {
            CharacterSpecification s = Spec(x => x.Appearance.Choices["hair.style"] = "long_tied_05");
            NormalizationReport r = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault());
            Assert.IsFalse(r.Rejected);
            Assert.GreaterOrEqual(r.Result.Appearance.Params.GetLevel(catalogs.Parameters, "hair.length"), 0.6f - 1e-3f);
        }

        [Test]
        public void ABeardChoice_GetsItsCoverage_AndAnAfroGetsTightTexture()
        {
            CharacterSpecification s = Spec(x => { x.Appearance.Choices["face.beard"] = "short_beard"; x.Appearance.Choices["hair.style"] = "afro_08"; x.Appearance.Choices["hair.texture"] = "straight"; x.Appearance.Params.Set(catalogs.Parameters, "hair.length", 0.5f); });
            NormalizationReport r = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault());
            Assert.GreaterOrEqual(r.Result.Appearance.Params.GetLevel(catalogs.Parameters, "facialHair.density"), 0.5f - 1e-3f);
            Assert.AreEqual("coily", r.Result.Appearance.Choices["hair.texture"]);
        }

        [Test]
        public void ACoherentAppearance_NeedsNoChanges()
        {
            CharacterSpecification s = Spec(x => x.Appearance.Choices["hair.style"] = "short_crop_01");
            Assert.AreEqual(0, AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault()).Changes.Count);
        }

        [Test]
        public void EveryCompatibilityRule_PointsAtRealCatalogEntries()
        {
            CompatibilityRules rules = CompatibilityRules.CreateDefault();
            foreach (PartParamRule r in rules.PartParam)
            {
                Assert.IsTrue(catalogs.Appearance.TryGetPart(r.Slot, r.Part, out PartDefinition _), r.Id);
                Assert.IsTrue(catalogs.Parameters.Contains(r.Param), r.Id);
                Assert.LessOrEqual(r.HardMin, r.SoftMin, r.Id); Assert.GreaterOrEqual(r.HardMax, r.SoftMax, r.Id);
            }
            foreach (PartPartRule r in rules.PartPart)
            {
                Assert.IsTrue(catalogs.Appearance.TryGetPart(r.Slot, r.Part, out PartDefinition _), r.Id);
                foreach (string a in r.Allowed) Assert.IsTrue(catalogs.Appearance.TryGetPart(r.OtherSlot, a, out PartDefinition _), r.Id + " allows " + a);
            }
        }

        // ---------------- style presets ----------------

        [Test]
        public void TheFourStyles_Exist_AndTheShortNameIsAnAlias()
        {
            foreach (string id in new[] { "FS27_CARTOON_SPORTS", StylePresets.CartoonExpressive, StylePresets.StylizedAthletic, StylePresets.RealisticStylized })
                Assert.IsTrue(catalogs.Styles.Contains(id), id);
            Assert.IsTrue(catalogs.Styles.Contains(StylePresets.CartoonSport));
            Assert.AreEqual("FS27_CARTOON_SPORTS", catalogs.Styles.Canonical(StylePresets.CartoonSport));
            Assert.AreEqual(4, catalogs.Styles.All.Count(), "an alias is not a style of its own");
        }

        [Test]
        public void TheStyles_AreOrderedFromCartoonToRealistic_InProportionsAndLook()
        {
            float Heads(string id) { catalogs.Styles.TryGet(id, out StylePreset s); return s.HeadHeights; }
            Assert.Less(Heads(StylePresets.CartoonExpressive), Heads("FS27_CARTOON_SPORTS"));
            Assert.Less(Heads("FS27_CARTOON_SPORTS"), Heads(StylePresets.StylizedAthletic));
            Assert.Less(Heads(StylePresets.StylizedAthletic), Heads(StylePresets.RealisticStylized));
            float Realism(string id) { catalogs.Styles.TryGet(id, out StylePreset s); return s.StartLevels["style.realism"]; }
            Assert.Less(Realism(StylePresets.CartoonExpressive), Realism(StylePresets.RealisticStylized));
        }

        [Test]
        public void AStyleBlend_IsTheWeightedMix_AndIsDeterministic()
        {
            var blend = new StyleBlend().Add("FS27_CARTOON_SPORTS", 0.5f).Add(StylePresets.RealisticStylized, 0.5f);
            StylePreset mix = StyleComposer.Blend(catalogs.Styles, blend, out string error);
            Assert.IsNull(error);
            catalogs.Styles.TryGet("FS27_CARTOON_SPORTS", out StylePreset a);
            catalogs.Styles.TryGet(StylePresets.RealisticStylized, out StylePreset b);
            Assert.AreEqual((a.HeadHeights + b.HeadHeights) / 2f, mix.HeadHeights, 1e-4f);
            Assert.AreEqual((a.StartLevels["style.realism"] + b.StartLevels["style.realism"]) / 2f, mix.StartLevels["style.realism"], 1e-4f);
            Assert.AreEqual(mix.Id, StyleComposer.Blend(catalogs.Styles, blend, out _).Id);
        }

        [Test]
        public void ABadBlend_IsRefused()
        {
            Assert.IsNull(StyleComposer.Blend(catalogs.Styles, new StyleBlend().Add("NOPE", 1f), out string e1)); StringAssert.Contains("Unknown", e1);
            Assert.IsNull(StyleComposer.Blend(catalogs.Styles, new StyleBlend(), out string e2)); Assert.IsNotNull(e2);
            Assert.IsNull(StyleComposer.Blend(catalogs.Styles, new StyleBlend().Add("FS27_CARTOON_SPORTS", -1f), out string e3)); Assert.IsNotNull(e3);
        }

        [Test]
        public void Restyling_KeepsThePersonsOwnTweaks_OnTopOfTheNewStyle()
        {
            CharacterSpecification s = Spec();
            Set(s, "head.scale", 0.9f);   // the person made the head bigger than the style's start
            CharacterSpecification re = StyleApplier.Restyle(s, StylePresets.RealisticStylized, catalogs);
            Assert.AreEqual(StylePresets.RealisticStylized, re.Appearance.StyleId);
            float before = s.Appearance.Params.GetLevel(catalogs.Parameters, "head.scale");
            float after = re.Appearance.Params.GetLevel(catalogs.Parameters, "head.scale");
            Assert.Less(after, before, "a realistic style has a smaller head");
            Assert.Greater(after, 0.2f + 0.3f, "but the person's bigger head is still bigger than the style's start");
            Assert.IsNull(StyleApplier.Restyle(s, "NOPE", catalogs));
        }

        [Test]
        public void ChangingTheStyleWithAPatch_CanBeUndone_Exactly()
        {
            var session = AuthoringSession.CreateDefault(3);
            session.Submit("Crea un extremo rápido");
            string before = CharacterSpecificationJson.ToJson(session.Current.Spec);
            var patch = new CharacterSpecificationPatch();
            patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.StyleId, Key = "style", Value = StylePresets.CartoonExpressive });
            PatchResult r = PatchApplier.Apply(session.Current, patch, catalogs);
            Assert.AreEqual(StylePresets.CartoonExpressive, r.Draft.Spec.Appearance.StyleId);
            Assert.AreNotEqual(before, CharacterSpecificationJson.ToJson(r.Draft.Spec));
            AuthoringDraft back = PatchApplier.Apply(r.Draft, r.Inverse, catalogs).Draft;
            Assert.AreEqual(before, CharacterSpecificationJson.ToJson(back.Spec));
        }

        [Test]
        public void SwapStyleByPrompt_UsesAnotherPreset()
        {
            var session = AuthoringSession.CreateDefault(3);
            session.Submit("Crea un extremo");
            session.Submit("solo cambia su estilo");
            Assert.AreNotEqual("FS27_CARTOON_SPORTS", session.Current.Spec.Appearance.StyleId);
        }

        // ---------------- content ids ----------------

        [TestCase("hair.short_curly_07", true)]
        [TestCase("behavior.stop_and_go", true)]
        [TestCase("style.cartoon_sports", true)]
        [TestCase("Hair.short", false)]
        [TestCase("hair", false)]
        [TestCase("hair.", false)]
        [TestCase("hair.short.curly", false)]
        [TestCase("hair.7curly", false)]
        [TestCase("hair.curly_", false)]
        [TestCase("", false)]
        public void ContentIdsFollowTheNamingRule(string id, bool valid)
        {
            Assert.AreEqual(valid, ContentRef.IsValid(id));
        }

        [Test]
        public void LegacyIds_ConvertToStableContentIds()
        {
            Assert.AreEqual("stop_and_go", ContentRef.ToSnake("StopAndGo"));
            Assert.AreEqual("first_time_finish", ContentRef.ToSnake("FirstTimeFinish"));
            Assert.AreEqual("fs27_cartoon_sports", ContentRef.ToSnake("FS27_CARTOON_SPORTS"));
            Assert.AreEqual("short_curly_07", ContentRef.ToSnake("short_curly_07"));
        }

        [Test]
        public void TheContentIndex_CoversEverything_WithUniqueValidIds_AndResolvesBothWays()
        {
            ContentIndex idx = ContentIndex.Build(catalogs, materials, AnimationCatalog.CreateDefault());
            Assert.IsEmpty(idx.Problems, string.Join("\n", idx.Problems));
            Assert.AreEqual("hair.short_curly_07", idx.PartId("hair.style", "short_curly_07"));
            Assert.AreEqual("behavior.stop_and_go", idx.BehaviorId("StopAndGo"));
            Assert.AreEqual("style.cartoon_sports", idx.StyleId("FS27_CARTOON_SPORTS"));
            Assert.IsTrue(idx.TryResolve("hair.short_curly_07", out ContentTarget t));
            Assert.AreEqual("hair.style", t.Slot);
            Assert.AreEqual("short_curly_07", t.Id);
            Assert.IsFalse(idx.TryResolve("hair.nope", out _));
            foreach (string id in idx.Ids) Assert.IsTrue(ContentRef.IsValid(id), id);
            Assert.GreaterOrEqual(idx.Count, catalogs.Behaviors.Count + catalogs.Styles.All.Count() + 30);
        }

        [Test]
        public void EveryMaterial_IsAValidMaterialContentId()
        {
            foreach (MaterialDefinition m in materials.All)
            {
                Assert.IsTrue(ContentRef.IsValid(m.Id), m.Id);
                Assert.AreEqual("material", ContentRef.Category(m.Id));
            }
            Assert.IsFalse(materials.TryAdd(new MaterialDefinition { Id = "shader.foo" }));
        }

        // ---------------- the assembly plan ----------------

        [Test]
        public void ThePlan_HasEveryPartOfAPlayer_AllProcedural_NoAssets()
        {
            CharacterAssemblyPlan plan = Plan(Spec());
            foreach (string id in new[] { "body.pelvis", "body.chest", "body.neck", "head.skull", "face.eye_l", "face.eye_r", "face.nose", "face.mouth", "hair.cap", "body.upperarm_l", "body.thigh_skin_r", "boot.boot_l", "boot.boot_r" })
                Assert.IsNotNull(plan.Find(id), id);
            Assert.AreEqual(plan.Parts.Count, plan.ProceduralParts);
            Assert.IsTrue(plan.Parts.All(p => p.Source == PartSource.Procedural && p.AssetId == ""));
            Assert.IsTrue(plan.Parts.All(p => materials.Contains(p.MaterialId)), "every part uses a catalog material");
            Assert.IsEmpty(plan.Warnings);
        }

        [Test]
        public void TheHeight_IsWhatTheParameterSays_AndHeadsTallFollowsTheStyle()
        {
            Assert.AreEqual(1.78f, Plan(Spec()).Proportions.HeightMeters, 0.01f);
            CharacterSpecification tall = Spec(); Set(tall, "body.height", 1f);
            Assert.AreEqual(1.78f * 1.15f, Plan(tall).Proportions.HeightMeters, 0.01f);
            CharacterSpecification re = StyleApplier.Restyle(Spec(), StylePresets.RealisticStylized, catalogs);
            CharacterSpecification ex = StyleApplier.Restyle(Spec(), StylePresets.CartoonExpressive, catalogs);
            Assert.Greater(Plan(re).Proportions.HeadsTall, Plan(ex).Proportions.HeadsTall, "realistic bodies have smaller heads");
        }

        [Test]
        public void LongLegsAndBigShoulders_ChangeTheBody_NotTheHeight()
        {
            CharacterSpecification baseS = Spec();
            CharacterSpecification legs = Spec(); Set(legs, "body.legLength", 1f);
            CharacterSpecification sh = Spec(); Set(sh, "body.shoulderWidth", 1f);
            Assert.Greater(Plan(legs).Proportions.LegLength, Plan(baseS).Proportions.LegLength);
            Assert.AreEqual(Plan(baseS).Proportions.HeightMeters, Plan(legs).Proportions.HeightMeters, 0.01f);
            Assert.Greater(Plan(sh).Proportions.ShoulderWidth, Plan(baseS).Proportions.ShoulderWidth);
        }

        [Test]
        public void TheBigHeadParameter_MakesAnActuallyBiggerHead()
        {
            CharacterSpecification big = Spec(); Set(big, "head.scale", 1f);
            Assert.Greater(Plan(big).Proportions.HeadHeightMeters, Plan(Spec()).Proportions.HeadHeightMeters * 1.15f);
        }

        [Test]
        public void TheColours_FollowTheSpecification()
        {
            CharacterSpecification s = Spec(x =>
            {
                x.Appearance.Colors["hair.color"] = "#112233"; x.Appearance.Colors["kit.primary"] = "#FF0000"; x.Appearance.Colors["eyes.color"] = "#00FF00";
            });
            CharacterAssemblyPlan p = Plan(s);
            Assert.AreEqual("#112233", p.MaterialColors["material.hair_toon"]);
            Assert.AreEqual("#FF0000", p.MaterialColors["material.shirt_cloth"]);
            Assert.AreEqual("#00FF00", p.MaterialColors["material.eye_iris"]);
        }

        [Test]
        public void TheSkinRamp_GoesFromLightToDark_AndTheUndertoneShiftsIt()
        {
            string light = DefaultColors.Skin(0f, 0.5f), mid = DefaultColors.Skin(0.5f, 0.5f), dark = DefaultColors.Skin(1f, 0.5f);
            float Lum(string h) { DefaultColors.TryParse(h, out float r, out float g, out float b); return 0.299f * r + 0.587f * g + 0.114f * b; }
            Assert.Greater(Lum(light), Lum(mid)); Assert.Greater(Lum(mid), Lum(dark));
            Assert.AreNotEqual(DefaultColors.Skin(0.5f, 0f), DefaultColors.Skin(0.5f, 1f));
            Assert.IsTrue(ContentRefHex(light));
        }

        private static bool ContentRefHex(string hex) { return CharacterSpecificationValidator.IsHexColor(hex); }

        [Test]
        public void HairFollowsTheStyleAndTheLength()
        {
            CharacterAssemblyPlan buzz = Plan(Spec(x => x.Appearance.Choices["hair.style"] = "buzz_02"));
            CharacterAssemblyPlan tied = Plan(Spec(x => x.Appearance.Choices["hair.style"] = "long_tied_05"));
            CharacterAssemblyPlan afro = Plan(Spec(x => x.Appearance.Choices["hair.style"] = "afro_08"));
            Assert.IsNotNull(buzz.Find("hair.cap")); Assert.IsNull(buzz.Find("hair.tail"));
            Assert.IsNotNull(tied.Find("hair.tail"));
            Assert.IsNotNull(afro.Find("hair.afro"));
            Assert.Greater(afro.Find("hair.afro").Size.X, buzz.Find("hair.cap").Size.X * 1.2f);
            CharacterSpecification longHair = Spec(); Set(longHair, "hair.length", 1f);
            Assert.IsNotNull(Plan(longHair).Find("hair.tail"), "no style chosen: a long length implies a long style");
        }

        [Test]
        public void ABeardAppears_OnlyWhenChosen_AndStubbleIsAMixOfSkinAndHair()
        {
            Assert.IsNull(Plan(Spec()).Find("facial.beard"));
            CharacterAssemblyPlan stubble = Plan(Spec(x => x.Appearance.Choices["face.beard"] = "stubble"));
            AssemblyPart b = stubble.Find("facial.beard");
            Assert.IsNotNull(b);
            Assert.AreNotEqual("stubble", b.ColorOverride);
            Assert.IsTrue(CharacterSpecificationValidator.IsHexColor(b.ColorOverride));
        }

        [Test]
        public void GoalkeeperGloves_AreAGlovePart_AndOtherwiseTheHandsAreSkin()
        {
            Assert.AreEqual(PartKind.Body, Plan(Spec()).Find("body.hand_l").Kind);
            AssemblyPart g = Plan(Spec(x => x.Appearance.Choices["kit.gloves"] = "gk_standard")).Find("body.hand_l");
            Assert.AreEqual(PartKind.Glove, g.Kind);
            Assert.AreEqual("material.gloves_grip", g.MaterialId);
        }

        [Test]
        public void ThePlan_IsDeterministic_AndCarriesNoMeshData()
        {
            CharacterSpecification s = Spec(x => { x.Appearance.Choices["hair.style"] = "afro_08"; x.AppearanceSeed = 9; });
            string Dump(CharacterAssemblyPlan p) => string.Join("\n", p.Parts.Select(a => a.Id + a.Position.X.ToString("R") + a.Position.Y.ToString("R") + a.Size.X.ToString("R") + string.Join(",", a.Parameters.Select(k => k.Key + k.Value.ToString("R")))));
            Assert.AreEqual(Dump(Plan(s)), Dump(Plan(s)));
            Assert.AreEqual(9u, Plan(s).AppearanceSeed);
            // a plan is recipes: its parts expose numbers, never vertex or index arrays
            foreach (System.Reflection.FieldInfo f in typeof(AssemblyPart).GetFields())
                Assert.IsFalse(f.FieldType.IsArray || (f.FieldType.IsGenericType && f.FieldType.GetGenericArguments().Any(t => t == typeof(int))), f.Name);
        }

        [Test]
        public void AnUnknownMaterial_IsWarnedAbout_NotHidden()
        {
            var empty = new MaterialCatalog();
            Assert.IsNotEmpty(CharacterAssemblyPlanner.Plan(Spec(), catalogs, empty).Warnings);
        }

        // ---------------- real geometry ----------------

        [Test]
        public void TheMesh_IsWellFormed_AndMatchesTheRequestedHeight()
        {
            CharacterAssemblyPlan plan = Plan(Spec());
            ProceduralCharacterMesh mesh = generator.Generate(plan);
            Assert.Greater(mesh.Triangles, 2000);
            foreach (MeshData m in mesh.Meshes)
            {
                Assert.AreEqual(m.Positions.Count, m.Normals.Count);
                Assert.AreEqual(0, m.Indices.Count % 3);
                Assert.IsTrue(m.Indices.All(i => i >= 0 && i < m.VertexCount), "index out of range in " + m.Name);
                Assert.IsTrue(m.Positions.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
            }
            Assert.AreEqual(plan.Proportions.HeightMeters, mesh.BoundsMax.Y, 0.03f);
            Assert.AreEqual(0f, mesh.BoundsMin.Y, 0.05f, "the feet are on the ground");
            Assert.Less(mesh.BoundsMax.X - mesh.BoundsMin.X, 1.3f);
        }

        [Test]
        public void TheMesh_IsDeterministic()
        {
            CharacterSpecification s = Spec(x => x.Appearance.Choices["hair.style"] = "long_tied_05");
            ProceduralCharacterMesh a = generator.Generate(Plan(s)), b = generator.Generate(Plan(s));
            Assert.AreEqual(Convert.ToBase64String(GlbWriter.Write(a)), Convert.ToBase64String(GlbWriter.Write(b)));
        }

        [Test]
        public void TheGlb_IsAValidGltfBinary_WithConsistentAccessors()
        {
            byte[] glb = GlbWriter.Write(generator.Generate(Plan(Spec(x => x.Appearance.Choices["face.beard"] = "short_beard"))));
            Assert.AreEqual(0x46546C67u, BitConverter.ToUInt32(glb, 0), "magic");
            Assert.AreEqual(2u, BitConverter.ToUInt32(glb, 4), "version");
            Assert.AreEqual((uint)glb.Length, BitConverter.ToUInt32(glb, 8), "total length");
            uint jsonLen = BitConverter.ToUInt32(glb, 12);
            Assert.AreEqual(0x4E4F534Au, BitConverter.ToUInt32(glb, 16), "JSON chunk");
            Assert.AreEqual(0u, jsonLen % 4);
            string jsonText = Encoding.UTF8.GetString(glb, 20, (int)jsonLen);
            int binHeader = 20 + (int)jsonLen;
            uint binLen = BitConverter.ToUInt32(glb, binHeader);
            Assert.AreEqual(0x004E4942u, BitConverter.ToUInt32(glb, binHeader + 4), "BIN chunk");
            Assert.AreEqual(glb.Length, binHeader + 8 + (int)binLen);

            Assert.IsTrue(Json.TryParse(jsonText.TrimEnd(), out JsonValue root, out string err), err);
            Assert.AreEqual("2.0", root.Members["asset"].GetString("version"));
            JsonValue buffers = root.Members["buffers"];
            Assert.AreEqual(binLen, (uint)buffers.Items[0].GetNumber("byteLength"));
            JsonValue views = root.Members["bufferViews"], accessors = root.Members["accessors"], meshes = root.Members["meshes"], mats = root.Members["materials"];
            foreach (JsonValue v in views.Items) Assert.LessOrEqual(v.GetNumber("byteOffset") + v.GetNumber("byteLength"), binLen, "a view runs past the buffer");
            Assert.AreEqual(meshes.Items.Count, mats.Items.Count);
            foreach (JsonValue mesh in meshes.Items)
            {
                JsonValue prim = mesh.Members["primitives"].Items[0];
                JsonValue attrs = prim.Members["attributes"];
                JsonValue pos = accessors.Items[(int)attrs.GetNumber("POSITION")], nor = accessors.Items[(int)attrs.GetNumber("NORMAL")], idx = accessors.Items[(int)prim.GetNumber("indices")];
                Assert.AreEqual(pos.GetNumber("count"), nor.GetNumber("count"));
                Assert.AreEqual(0, (int)idx.GetNumber("count") % 3);
                Assert.IsTrue(pos.Members.ContainsKey("min") && pos.Members.ContainsKey("max"), "POSITION needs min/max");
                // every index really is inside the vertex range (read from the binary chunk)
                JsonValue idxView = views.Items[(int)idx.GetNumber("bufferView")];
                int off = binHeader + 8 + (int)idxView.GetNumber("byteOffset");
                bool wide = (int)idx.GetNumber("componentType") == 5125;
                for (int i = 0; i < (int)idx.GetNumber("count"); i++)
                {
                    uint v = wide ? BitConverter.ToUInt32(glb, off + i * 4) : BitConverter.ToUInt16(glb, off + i * 2);
                    Assert.Less(v, (uint)pos.GetNumber("count"));
                }
            }
        }

        [Test]
        public void ThePng_HasAValidStructure_AndRealContent()
        {
            byte[] png = generator.RenderPng(Plan(Spec()), 120, 210);
            CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8).ToArray());
            Assert.AreEqual(360u, ReadBE(png, 16), "width: three views");
            Assert.AreEqual(210u, ReadBE(png, 20));
            // walk the chunks, check each CRC, and that IDAT inflates to the right size
            int pos = 8; var idat = new MemoryStream(); bool end = false;
            while (pos < png.Length)
            {
                uint len = ReadBE(png, pos); string type = Encoding.ASCII.GetString(png, pos + 4, 4);
                byte[] td = png.Skip(pos + 4).Take((int)len + 4).ToArray();
                Assert.AreEqual(ReadBE(png, pos + 8 + (int)len), PngWriter.Crc32(td), "CRC of " + type);
                if (type == "IDAT") idat.Write(png, pos + 8, (int)len);
                if (type == "IEND") end = true;
                pos += 12 + (int)len;
            }
            Assert.IsTrue(end);
            using (var z = new System.IO.Compression.ZLibStream(new MemoryStream(idat.ToArray()), System.IO.Compression.CompressionMode.Decompress))
            {
                var raw = new MemoryStream(); z.CopyTo(raw);
                Assert.AreEqual((360 * 3 + 1) * 210, raw.Length);
                byte[] data = raw.ToArray();
                var distinct = new HashSet<int>();
                for (int i = 1; i + 2 < data.Length; i += 97) distinct.Add(data[i] << 16 | data[i + 1] << 8 | data[i + 2]);
                Assert.Greater(distinct.Count, 20, "the picture must contain a shaded character, not a flat colour");
            }
        }

        private static uint ReadBE(byte[] b, int at) { return (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]); }

        [Test]
        public void DifferentCharacters_RenderDifferently_AndTheSameOneIdentically()
        {
            CharacterSpecification a = Spec(x => x.Appearance.Colors["kit.primary"] = "#FF0000");
            CharacterSpecification b = Spec(x => x.Appearance.Colors["kit.primary"] = "#00AA00");
            Assert.AreEqual(Convert.ToBase64String(generator.RenderPng(Plan(a), 80, 140)), Convert.ToBase64String(generator.RenderPng(Plan(a), 80, 140)));
            Assert.AreNotEqual(Convert.ToBase64String(generator.RenderPng(Plan(a), 80, 140)), Convert.ToBase64String(generator.RenderPng(Plan(b), 80, 140)));
        }

        [Test]
        public void ThePreview_IsHonestAboutWhatItIs()
        {
            CharacterPreviewData p = generator.Preview(Plan(Spec()), withPng: true, withGlb: true);
            Assert.IsNotNull(p.PngBytes);
            Assert.IsNotNull(p.GlbBytes);
            Assert.IsTrue(p.Notes.Any(n => n.Contains("not final art") && n.Contains("not rigged")));
            Assert.Greater(p.Triangles, 0);
            Assert.IsNull(generator.Preview(Plan(Spec()), false, false).PngBytes);
        }

        [Test]
        public void TheGeneratorContracts_AreImplementedWithoutUnity()
        {
            ResolvedCharacter rc = new CatalogAppearanceResolver(catalogs).Resolve(Spec());
            ICharacterAssembler<ProceduralCharacterMesh> assembler = generator;
            Assert.Greater(assembler.Assemble(rc, null).Triangles, 1000);
            Assert.AreEqual("FS27.ProceduralCharacterGenerator.v1", generator.Name);
        }

        [Test]
        public void ManyDifferentCharacters_AllBuildCleanMeshes()
        {
            var session = AuthoringSession.CreateDefault(21);
            string[] prompts = { "Crea un extremo rápido con pelo rizado", "Crea un portero enorme con guantes", "Crea un delantero muy pequeño y delgado, sin pelo", "Crea un defensa muy musculoso con barba corta", "Crea un mediocampista con afro y ojos verdes" };
            foreach (string t in prompts)
            {
                session.Submit(t);
                CharacterSpecification s = session.Current.Spec.Clone();
                s.Appearance = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault()).Result.Appearance;
                ProceduralCharacterMesh m = generator.Generate(Plan(s));
                Assert.IsTrue(m.Meshes.All(x => x.Indices.All(i => i < x.VertexCount)), t);
                Assert.IsTrue(m.Meshes.SelectMany(x => x.Positions).All(v => !float.IsNaN(v)), t);
            }
        }
    }
}
