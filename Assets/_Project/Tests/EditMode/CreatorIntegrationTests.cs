using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CreatorIntegrationTests
    {
        private CreatorCatalogs catalogs;
        private PlayerLibrary library;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            library = new PlayerLibrary();
            library.TryAdd(SakData.Player());
            library.TryAdd(NicoGkData.Player());
        }

        // ================= Style: "more cartoon" is several coherent changes =================

        private CharacterSpecification Base() { return catalogs.NewSpecification("c"); }

        private CharacterSpecification Shift(CharacterSpecification s, int dir, float magnitude, bool guard = false)
        {
            return ModificationApplier.Apply(s, new[] { new SpecChange { Kind = SpecChangeKind.StyleShift, Target = "cartoon", Direction = dir, Magnitude = magnitude, GuardChildlike = guard } }, catalogs).Result;
        }

        private float Get(CharacterSpecification s, string id) { return s.Appearance.Params.Get(catalogs.Parameters, id); }

        [Test]
        public void MoreCartoon_MovesSeveralParametersTogether()
        {
            var before = Base();
            var after = Shift(before, +1, 0.3f);
            Assert.Greater(Get(after, "style.stylization"), Get(before, "style.stylization"));
            Assert.Less(Get(after, "style.realism"), Get(before, "style.realism"), "realism goes DOWN");
            Assert.Greater(Get(after, "style.exaggeration"), Get(before, "style.exaggeration"));
            Assert.Greater(Get(after, "style.expressiveness"), Get(before, "style.expressiveness"));
            Assert.Greater(Get(after, "face.expressiveness"), Get(before, "face.expressiveness"));
            Assert.Greater(Get(after, "head.scale"), Get(before, "head.scale"));
            Assert.Greater(Get(after, "face.eyeSize"), Get(before, "face.eyeSize"));
            Assert.Greater(Get(after, "body.handScale"), Get(before, "body.handScale"));
            Assert.Greater(Get(after, "body.footScale"), Get(before, "body.footScale"));
        }

        [Test]
        public void MoreCartoon_LeavesTheAthleticProportionsAlone()
        {
            var before = Base();
            var after = Shift(before, +1, 0.5f);
            foreach (string id in new[] { "style.athleticity", "body.shoulderWidth", "body.torsoWidth", "body.legLength", "body.armLength", "body.height", "body.muscularity", "body.mass" })
                Assert.AreEqual(Get(before, id), Get(after, id), 1e-6f, id);
        }

        [Test]
        public void MoreRealistic_IsTheOppositeShift()
        {
            var before = Base();
            var after = Shift(before, -1, 0.3f);
            Assert.Less(Get(after, "style.stylization"), Get(before, "style.stylization"));
            Assert.Greater(Get(after, "style.realism"), Get(before, "style.realism"));
            Assert.Less(Get(after, "head.scale"), Get(before, "head.scale"));
        }

        [Test]
        public void TheChildlikeGuard_LimitsOnlyTheParametersThatMakeACharacterLookLikeAChild()
        {
            var before = Base();
            var plain = Shift(before, +1, 0.4f);
            var guarded = Shift(before, +1, 0.4f, guard: true);
            foreach (string id in new[] { "head.scale", "face.eyeSize", "body.handScale", "body.footScale" })
            {
                float p = Get(plain, id) - Get(before, id), g = Get(guarded, id) - Get(before, id);
                Assert.Greater(g, 0f, id + " still moves");
                Assert.Less(g, p, id + " moves less");
            }
            foreach (string id in new[] { "style.stylization", "style.realism", "style.exaggeration", "style.expressiveness" })
                Assert.AreEqual(Get(plain, id), Get(guarded, id), 1e-6f, id + " is not guarded");
        }

        [Test]
        public void TheStyleStaysWithinRange_EvenPushedHard()
        {
            var s = Base();
            for (int i = 0; i < 20; i++) s = Shift(s, +1, 0.5f);
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).IsValid);
            Assert.AreEqual(1.35f, Get(s, "head.scale"), 1e-5f, "capped at the head's maximum");
        }

        [Test]
        public void TheStylePresetIsData_AndAnotherStyleCanBeAdded()
        {
            var flat = new StylePreset { Id = "FS27_FLAT", Name = "Flat" };
            flat.CartoonAxis.Add(new StyleAxisEffect("style.stylization", 1f));
            Assert.IsTrue(catalogs.Styles.TryAdd(flat));
            Assert.IsFalse(catalogs.Styles.TryAdd(flat));
            var s = catalogs.NewSpecification("c", "FS27_FLAT");
            var after = Shift(s, +1, 0.2f);
            Assert.AreEqual(Get(s, "head.scale"), Get(after, "head.scale"), "this style does not touch the head");
            Assert.Greater(Get(after, "style.stylization"), Get(s, "style.stylization"));
        }

        [Test]
        public void TheStartingLook_IsSlightlyBigHeadAndHandsAndBoots_ButNotChibi()
        {
            var s = Base();
            Assert.That(Get(s, "head.scale"), Is.InRange(1.03f, 1.15f), "ligeramente mayor, not chibi");
            Assert.That(Get(s, "body.handScale"), Is.InRange(1.03f, 1.2f));
            Assert.That(Get(s, "body.footScale"), Is.InRange(1.03f, 1.2f));
            catalogs.Appearance.TryGetBaseModel(DefaultAppearanceCatalog.BaseA, out var b);
            Assert.AreEqual(6.5f, b.HeadHeights, "6.5 heads is the base model's stated target (unverified against any asset)");
        }

        // ================= PlayerDefinition integration =================

        [Test]
        public void AppearanceDefaults_ReuseWhatThePlayerAlreadyDeclares()
        {
            var sak = SakData.Player();   // 176 cm, 68 kg, Light
            var spec = AppearanceDefaults.FromPlayer(sak, catalogs);
            Assert.AreEqual(sak.Id, spec.PlayerId);
            Assert.AreEqual("light", spec.Appearance.Choices["body.preset"]);
            Assert.AreEqual(176f / 178f, Get(spec, "body.height"), 0.01f);
            Assert.IsTrue(CharacterSpecificationValidator.Validate(spec, catalogs).IsValid);

            var tall = new PlayerDefinition("p-tall", "Generic Tall", 5, PlayerRole.Defender, TestData.Attrs(60)) { HeightCm = 195, WeightKg = 92, BodyType = BodyType.Tall };
            var spec2 = AppearanceDefaults.FromPlayer(tall, catalogs);
            Assert.Greater(Get(spec2, "body.height"), Get(spec, "body.height"));
            Assert.AreEqual("tall", spec2.Appearance.Choices["body.preset"]);
            foreach (BodyType t in Enum.GetValues(typeof(BodyType)))
            {
                var p = new PlayerDefinition("p-" + t, "G", 9, PlayerRole.Forward, TestData.Attrs(60)) { BodyType = t };
                Assert.IsTrue(CharacterSpecificationValidator.Validate(AppearanceDefaults.FromPlayer(p, catalogs), catalogs).IsValid, t.ToString());
            }
        }

        [Test]
        public void TheAppearanceDoesNotCopyAnyPlayerData()
        {
            string[] playerFields = { "Name", "ShortName", "Number", "ShirtNumber", "Age", "HeightCm", "WeightKg", "BodyType", "PreferredFoot", "Attributes", "Role", "NationalityCode", "TeamId" };
            foreach (Type t in new[] { typeof(CharacterSpecification), typeof(PlayerAppearance), typeof(FootballDNA) })
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    Assert.IsFalse(playerFields.Contains(f.Name), t.Name + "." + f.Name + " would duplicate PlayerDefinition");
            Assert.IsFalse(typeof(CharacterSpecification).GetFields().Any(f => f.FieldType == typeof(PlayerDefinition) || f.FieldType == typeof(PlayerPlayingProfile)));
            Assert.IsFalse(typeof(PlayerDefinition).GetFields().Any(f => f.FieldType == typeof(CharacterSpecification) || f.FieldType == typeof(PlayerAppearance) || f.FieldType == typeof(FootballDNA)),
                "the player definition does not hold appearance or DNA: it is linked by id");
        }

        [Test]
        public void ThereIsStillOnePlayerDefinition_OnePlayerLibrary_AndNoSecondRoleOrStaminaSystem()
        {
            var types = typeof(PlayerDefinition).Assembly.GetTypes();
            Assert.AreEqual(1, types.Count(t => t.Name == "PlayerDefinition"));
            Assert.AreEqual(1, types.Count(t => t.Name == "PlayerLibrary"));
            Assert.AreEqual(12, Enum.GetNames(typeof(PlayerArchetype)).Length, "the 12 roles are untouched");
            Assert.IsFalse(types.Any(t => t.Namespace == "FS27.Core" && (t.Name.Contains("AppearanceLibrary") || t.Name.Contains("CharacterLibrary") || t.Name.Contains("PlayerAppearanceLibrary"))));
            Assert.IsFalse(typeof(FootballDNA).GetFields().Any(f => f.Name.ToLowerInvariant().Contains("stamina")));
        }

        [Test]
        public void TheRegistry_LinksOneCharacterToOnePlayer_AndRefusesInvalidOrInconsistentData()
        {
            var registry = new CharacterRegistry(catalogs);
            var sak = AppearanceDefaults.FromPlayer(SakData.Player(), catalogs);
            var r = registry.TryAdd(sak, library);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsTrue(registry.TryGetByPlayer(SakData.Id, out var found));
            Assert.AreSame(sak, found);
            Assert.IsTrue(registry.TryGet(sak.CharacterId, out _));
            Assert.AreEqual(1, registry.Count);

            // a second character for the same player
            var again = AppearanceDefaults.FromPlayer(SakData.Player(), catalogs, "char-other");
            Assert.IsTrue(registry.TryAdd(again, library).Has(CreatorIssueCode.PlayerAlreadyHasSpecification));
            // same character id
            var dup = catalogs.NewSpecification(sak.CharacterId);
            Assert.IsTrue(registry.TryAdd(dup, library).Has(CreatorIssueCode.CharacterIdDuplicate));
            // a player that does not exist
            var ghost = catalogs.NewSpecification("char-ghost"); ghost.PlayerId = "nobody";
            Assert.IsTrue(registry.TryAdd(ghost, library).Has(CreatorIssueCode.PlayerNotFound));
            Assert.IsTrue(registry.TryAdd(ghost, null).Has(CreatorIssueCode.PlayerNotFound));
            // invalid data
            var bad = catalogs.NewSpecification("char-bad"); bad.Appearance.Choices["hair.style"] = "nope";
            Assert.IsTrue(registry.TryAdd(bad, library).Has(CreatorIssueCode.ChoicePartUnknown));
            Assert.IsTrue(registry.TryAdd(null, library).Has(CreatorIssueCode.SpecificationNull));
            Assert.AreEqual(1, registry.Count, "nothing invalid was added");
            Assert.IsFalse(registry.TryGetByPlayer("nobody", out _));
            Assert.IsFalse(registry.TryGetByPlayer(null, out _));
        }

        [Test]
        public void ACharacterWithoutAPlayer_CanBeRegistered_AsADraft()
        {
            var registry = new CharacterRegistry(catalogs);
            Assert.IsTrue(registry.TryAdd(catalogs.NewSpecification("free-001"), library).IsValid);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void ThePlayerCard_StillWorks_AndOwnsNoAppearanceData()
        {
            var sak = SakData.Player();
            var profiles = new PlayingProfileCatalog();
            profiles.TryAdd(SakData.Profile());
            var builder = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), profiles, null, null, library);
            var card = builder.Build(sak);
            Assert.AreEqual(89, card.Overall, "Sak's Overall is unchanged by the Creator Engine");
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var f in typeof(PlayerCardData).GetFields(all))
                Assert.IsFalse(f.FieldType == typeof(CharacterSpecification) || f.FieldType == typeof(PlayerAppearance) || f.FieldType == typeof(FootballDNA), f.Name);
        }

        // ================= Hints -> PlayerDefinition / PlayerPlayingProfile =================

        [Test]
        public void AttributeHints_OnlySuggest_AndOnlyTouchTheHintedAttributes()
        {
            var baseline = TestData.Attrs(60);
            var hints = new[]
            {
                new AttributeHint { Attribute = PlayerAttributeId.Acceleration, Level = 0.9f },
                new AttributeHint { Attribute = PlayerAttributeId.Strength, Level = 0.1f },
                new AttributeHint { Attribute = PlayerAttributeId.Passing, Relative = true, Delta = 0.1f }
            };
            var suggested = AttributeHintApplier.Suggest(baseline, hints);
            Assert.Greater(suggested.Acceleration, 85);
            Assert.Less(suggested.Strength, 45);
            Assert.AreEqual(60 + (int)Math.Round(0.1f * 98), suggested.Passing);
            foreach (var id in PlayerAttributeInfo.All.Where(a => a != PlayerAttributeId.Acceleration && a != PlayerAttributeId.Strength && a != PlayerAttributeId.Passing))
                Assert.AreEqual(baseline.GetValue(id), suggested.GetValue(id), id.ToString());
            Assert.AreEqual(60, baseline.Acceleration, "the baseline value is not modified");
            foreach (var id in PlayerAttributeInfo.All) Assert.That(suggested.GetValue(id), Is.InRange(PlayerAttributes.Min, PlayerAttributes.Max));
            Assert.AreEqual(baseline.Reaction, suggested.Reaction, "Reaction is never part of the creator");
        }

        [Test]
        public void AttributeHints_NeverLeaveTheValidRange()
        {
            var r = AttributeHintApplier.Suggest(TestData.Attrs(95), new[] { new AttributeHint { Attribute = PlayerAttributeId.Speed, Relative = true, Delta = 0.9f },
                                                                              new AttributeHint { Attribute = PlayerAttributeId.Agility, Relative = true, Delta = -2f } });
            Assert.AreEqual(99, r.Speed);
            Assert.AreEqual(1, r.Agility);
        }

        [Test]
        public void ProfileHints_UpdateThePlayersPlayingProfile_TheSingleHomeOfThoseValues()
        {
            var profile = SakData.Profile();   // risk 75, creativity 82, aggression 55, Explosive/Creator/Finisher
            ProfileHintApplier.Apply(profile, new[]
            {
                new ProfileHint { Kind = ProfileHintKind.Risk, Level = 0.9f },
                new ProfileHint { Kind = ProfileHintKind.Creativity, Relative = true, Delta = 0.1f },
                new ProfileHint { Kind = ProfileHintKind.Aggression, Relative = true, Delta = -0.2f }
            });
            Assert.AreEqual(90, profile.RiskPreference);
            Assert.AreEqual(92, profile.Creativity);
            Assert.AreEqual(35, profile.Aggression);
            Assert.IsTrue(PlayingProfileValidator.Validate(profile, SakData.Player()).IsValid);

            ProfileHintApplier.Apply(profile, new[] { new ProfileHint { Kind = ProfileHintKind.Risk, Relative = true, Delta = 5f }, new ProfileHint { Kind = ProfileHintKind.Aggression, Relative = true, Delta = -5f } });
            Assert.AreEqual(100, profile.RiskPreference);
            Assert.AreEqual(0, profile.Aggression);
        }

        [Test]
        public void ProfileHints_SetTheZone_AndKeepAtMostThreeRoles()
        {
            var profile = SakData.Profile();
            ProfileHintApplier.Apply(profile, new[] { new ProfileHint { Kind = ProfileHintKind.PrimaryZone, Zone = PitchZone.Attack } });
            Assert.AreEqual(PitchZone.Attack, profile.PrimaryZone);
            Assert.IsFalse(profile.SecondaryZones.Contains(PitchZone.Attack), "no zone is primary and secondary at once");
            Assert.IsTrue(PlayingProfileValidator.Validate(profile, SakData.Player()).IsValid);

            // a fourth role replaces the weakest one only if it is stronger
            ProfileHintApplier.Apply(profile, new[] { new ProfileHint { Kind = ProfileHintKind.Role, Role = PlayerArchetype.Winger, Level = 0.95f } });
            Assert.AreEqual(3, profile.Roles.Count);
            Assert.IsTrue(profile.HasArchetype(PlayerArchetype.Winger));
            Assert.IsFalse(profile.HasArchetype(PlayerArchetype.Finisher), "the weakest (82) made room");
            ProfileHintApplier.Apply(profile, new[] { new ProfileHint { Kind = ProfileHintKind.Role, Role = PlayerArchetype.Wall, Level = 0.1f } });
            Assert.IsFalse(profile.HasArchetype(PlayerArchetype.Wall), "a weaker role is not worth a slot");
            ProfileHintApplier.Apply(profile, new[] { new ProfileHint { Kind = ProfileHintKind.Role, Role = PlayerArchetype.Explosive, Level = 0.5f } });
            Assert.AreEqual(95, profile.GetAffinity(PlayerArchetype.Explosive), "an existing role keeps its higher affinity");
            Assert.IsTrue(PlayingProfileValidator.Validate(profile, SakData.Player()).IsValid);
        }

        // ================= The whole pipeline, end to end =================

        [Test]
        public void Prompt_To_Specification_To_Player_To_Behaviours()
        {
            var interpreter = new DeterministicPromptInterpreter(catalogs);
            var result = interpreter.Interpret(new PromptRequest { Text = "Quiero un extremo pequeño y explosivo, muy bueno en uno contra uno, creativo, con cambios de ritmo fuertes y atacar hacia dentro.", NewCharacterId = "char-p001" });
            Assert.IsTrue(result.CanApplyAutomatically);

            // a player to attach it to: attributes come from the hints, never from the specification
            var player = new PlayerDefinition("p-001", "Generic Winger", 11, PlayerRole.Forward, AttributeHintApplier.Suggest(TestData.Attrs(60), result.AttributeHints))
            { HeightCm = 168, WeightKg = 64, BodyType = BodyType.Light };
            Assert.IsTrue(library.TryAdd(player));
            var profile = PlayingProfileDefaults.FromPlayer(player);
            profile.Roles.Clear();
            profile.AddRole(PlayerArchetype.Finisher, 60);
            ProfileHintApplier.Apply(profile, result.ProfileHints);
            Assert.IsTrue(PlayingProfileValidator.Validate(profile, player).IsValid, "the profile, with the hints applied, is still valid");
            Assert.AreEqual(PitchZone.Wing, profile.PrimaryZone);
            Assert.Greater(profile.Creativity, 80);

            var spec = result.Draft.Clone();
            spec.PlayerId = player.Id;
            var registry = new CharacterRegistry(catalogs);
            Assert.IsTrue(registry.TryAdd(spec, library).IsValid);

            // the gameplay side reads the DNA + attributes + context: it never needs to know WHO the player is
            var ranked = BehaviorResolver.Resolve(spec.Dna, player.Attributes, BehaviorContext.HasBall | BehaviorContext.FacingDefender | BehaviorContext.OpenSpaceAhead | BehaviorContext.WideArea, catalogs.Parameters, catalogs.Behaviors);
            Assert.Contains(ranked[0].BehaviorId, new[] { "ExplosiveExit", "InsideCut", "StopAndGo", "BodyFeint" });
            Assert.IsTrue(FootballDNAValidator.CheckAgainstAttributes(spec.Dna, player.Attributes, catalogs.Behaviors).IsValid);

            // and the runtime package carries no authoring data
            string runtime = CharacterSpecificationJson.ToJson(spec.ForRuntime());
            StringAssert.DoesNotContain("uno contra uno", runtime);
            Assert.Less(runtime.Length, 2000, runtime);
        }

        // ================= Movement signature =================

        private MovementSignature Signature(BodyType body, PlayerAttributes attrs, Action<CharacterSpecification> edit = null)
        {
            var spec = catalogs.NewSpecification("c");
            edit?.Invoke(spec);
            return MovementSignatureResolver.Resolve(body, spec, attrs, catalogs);
        }

        [Test]
        public void ATallLongLeggedPlayer_HasALongerStride_AndAShortOneAShorterStride()
        {
            var attrs = TestData.Attrs(70);
            var tall = Signature(BodyType.Tall, attrs, s => { s.Appearance.Params.Set(catalogs.Parameters, "body.height", 1.1f); s.Appearance.Params.Set(catalogs.Parameters, "body.legLength", 1.08f); });
            var compact = Signature(BodyType.Compact, attrs, s => s.Appearance.Params.Set(catalogs.Parameters, "body.height", 0.9f));
            var normal = Signature(BodyType.Athletic, attrs);
            Assert.Greater(tall.StrideLength, normal.StrideLength);
            Assert.Less(compact.StrideLength, normal.StrideLength);
            Assert.Greater(compact.Cadence, tall.Cadence, "short strides come with a higher step rate");
        }

        [Test]
        public void ADnaThatLovesBurstingAndTurning_ShowsInLeanAndTurns_WithoutChangingAnyAnimation()
        {
            var attrs = TestData.Attrs(70);
            var calm = Signature(BodyType.Athletic, attrs);
            var sharp = Signature(BodyType.Athletic, attrs, s =>
            {
                s.Dna.Params.Set(catalogs.Parameters, "movement.accelerationTendency", 0.95f);
                s.Dna.Params.Set(catalogs.Parameters, "movement.turningTendency", 0.95f);
                s.Dna.Params.Set(catalogs.Parameters, "dribbling.directionChange", 0.95f);
            });
            Assert.Greater(sharp.Lean, calm.Lean);
            Assert.Greater(sharp.TurnSharpness, calm.TurnSharpness);
            Assert.Greater(sharp.AccelerationAggression, calm.AccelerationAggression);
        }

        [Test]
        public void AgilityAndAccelerationAttributes_ShapeTheSignature()
        {
            var slow = Signature(BodyType.Athletic, TestData.Attrs(70).With(PlayerAttributeId.Agility, 30).With(PlayerAttributeId.Acceleration, 30));
            var quick = Signature(BodyType.Athletic, TestData.Attrs(70).With(PlayerAttributeId.Agility, 95).With(PlayerAttributeId.Acceleration, 95));
            Assert.Greater(quick.TurnSharpness, slow.TurnSharpness);
            Assert.Greater(quick.AccelerationAggression, slow.AccelerationAggression);
            Assert.Greater(quick.Cadence, slow.Cadence);
        }

        [Test]
        public void TheSignature_IsAlwaysInRange_ForAnyBodyAndAnyData()
        {
            foreach (BodyType body in Enum.GetValues(typeof(BodyType)))
                foreach (float level in new[] { 0f, 0.5f, 1f })
                {
                    var spec = catalogs.NewSpecification("c");
                    foreach (var p in catalogs.Parameters.All) (p.Domain == ParameterDomain.Appearance ? spec.Appearance.Params : spec.Dna.Params).Set(catalogs.Parameters, p.Id, p.FromLevel(level));
                    var s = MovementSignatureResolver.Resolve(body, spec, TestData.Attrs(level < 0.5f ? 1 : 99), catalogs);
                    Assert.That(s.StrideLength, Is.InRange(0.8f, 1.2f));
                    Assert.That(s.Cadence, Is.InRange(0.8f, 1.2f));
                    Assert.That(s.Lean, Is.InRange(0f, 1f));
                    Assert.That(s.TurnSharpness, Is.InRange(0f, 1f));
                    Assert.That(s.AccelerationAggression, Is.InRange(0f, 1f));
                }
        }

        [Test]
        public void TheSignatureHasNoSpeedOrDistance_ThatIsGameplay()
        {
            var names = typeof(MovementSignature).GetFields().Select(f => f.Name.ToLowerInvariant()).ToArray();
            foreach (string gameplay in new[] { "speed", "topspeed", "velocity", "position", "stamina", "sprint" })
                CollectionAssert.DoesNotContain(names, gameplay);
            var spec = catalogs.NewSpecification("c"); var attrs = TestData.Attrs(80); var copy = attrs;
            MovementSignatureResolver.Resolve(BodyType.Strong, spec, attrs, catalogs);
            Assert.AreEqual(copy, attrs, "reading attributes never changes them");
        }

        [Test]
        public void TheMovementChain_IsDocumentedInCode_AndThereIsNoSprintAnywhere()
        {
            StringAssert.Contains("PlayerIntent", CreatorEngineRules.MovementChain);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type t in new[] { typeof(CharacterSpecification), typeof(PlayerAppearance), typeof(FootballDNA), typeof(DefaultParameters), typeof(SignatureBehaviorDefinition),
                                       typeof(MovementSignature), typeof(MovementSignatureResolver), typeof(DeterministicPromptInterpreter), typeof(PromptLexicon) })
            {
                Assert.IsFalse(t.Name.ToLowerInvariant().Contains("sprint"), t.Name);
                foreach (var m in t.GetMembers(all)) Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name);
            }
            foreach (var p in catalogs.Parameters.All) Assert.IsFalse(p.Id.ToLowerInvariant().Contains("sprint"), p.Id);
        }

        // ================= Runtime contracts =================

        [Test]
        public void TheAppearanceResolver_FillsDefaults_AndFindsTheRig()
        {
            var resolver = new CatalogAppearanceResolver(catalogs);
            var spec = catalogs.NewSpecification("c"); spec.Appearance.Choices["hair.style"] = "buzz_02"; spec.Appearance.Colors["kit.primary"] = "#112233";
            var rc = resolver.Resolve(spec);
            Assert.AreEqual("fs27_humanoid_v1", rc.RigId);
            Assert.AreEqual(catalogs.Parameters.InDomain(ParameterDomain.Appearance).Count, rc.Scales.Count, "every appearance parameter has a final value");
            Assert.AreEqual(1.0f, rc.Scales["body.height"]);
            Assert.AreEqual("buzz_02", rc.Parts["hair.style"]);
            Assert.AreEqual("#112233", rc.Colors["kit.primary"]);
            Assert.IsEmpty(rc.Warnings);
            Assert.IsTrue(resolver.TryResolveRig(DefaultAppearanceCatalog.BaseA, out _));
            Assert.IsFalse(resolver.TryResolveRig("none", out _));
        }

        [Test]
        public void TheAppearanceResolver_LeavesBadPartsEmpty_AndWarns_InsteadOfCrashing()
        {
            var resolver = new CatalogAppearanceResolver(catalogs);
            var spec = catalogs.NewSpecification("c"); spec.Appearance.Choices["hair.style"] = "nope"; spec.BaseModelId = "none";
            var rc = resolver.Resolve(spec);
            Assert.IsFalse(rc.Parts.ContainsKey("hair.style"));
            Assert.GreaterOrEqual(rc.Warnings.Count, 2);
            Assert.IsNull(rc.RigId);
        }

        [Test]
        public void ManyCharacters_ShareOneRigAndOneBase()
        {
            var resolver = new CatalogAppearanceResolver(catalogs);
            var rigs = new HashSet<string>();
            for (int i = 0; i < 50; i++)
            {
                var spec = catalogs.NewSpecification("c" + i);
                spec.Appearance.Params.Set(catalogs.Parameters, "body.height", 0.85f + 0.006f * i);
                rigs.Add(resolver.Resolve(spec).RigId);
            }
            Assert.AreEqual(1, rigs.Count);
        }

        [Test]
        public void TheRuntimeContractsExist_AndAreImplementableWithoutUnity()
        {
            Assert.IsTrue(typeof(ICharacterAssembler<object>).IsInterface);
            Assert.IsTrue(typeof(ICharacterRigResolver).IsAssignableFrom(typeof(CatalogAppearanceResolver)));
            Assert.IsTrue(typeof(ICharacterAppearanceResolver).IsAssignableFrom(typeof(CatalogAppearanceResolver)));
            foreach (Type t in new[] { typeof(IAnimationResolver), typeof(ICharacterGenerator), typeof(BodyCompatibility), typeof(AnimationCompatibility), typeof(RetargetingProfile), typeof(AnimationSetReference) })
                Assert.IsNotNull(t);
            // no implementation of the 3D parts exists: they are Creator Engine roadmap phases A-H
            var implementers = typeof(ICharacterGenerator).Assembly.GetTypes().Where(t => !t.IsInterface && (typeof(ICharacterGenerator).IsAssignableFrom(t) || typeof(IAnimationResolver).IsAssignableFrom(t))).ToArray();
            Assert.IsEmpty(implementers, "nothing pretends to generate geometry or animation");
            var assemblerImplementers = typeof(ICharacterGenerator).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICharacterAssembler<>))).ToArray();
            Assert.IsEmpty(assemblerImplementers);
        }

        [Test]
        public void TheBallStaysIndependent_AnimationOnlyReportsContact()
        {
            var contact = new ActionContactEvent { Action = FootballActionKind.Shot, NormalizedTime = 0.4f, Contact = "foot_r" };
            Assert.AreEqual(FootballActionKind.Shot, contact.Action);
            var fields = typeof(ActionContactEvent).GetFields().Select(f => f.FieldType).ToArray();
            CollectionAssert.DoesNotContain(fields, typeof(Vec2));
            CollectionAssert.DoesNotContain(fields, typeof(Vec3), "an animation event carries no ball velocity: gameplay decides that");
            foreach (var k in new[] { "Control", "Dribble", "ShortPass", "LongPass", "Cross", "Shot", "PlacedShot", "Header", "Tackle", "Intercept", "Block", "Shield", "GoalkeeperSave" })
                Assert.IsTrue(Enum.GetNames(typeof(FootballActionKind)).Contains(k), k);
        }

        [Test]
        public void SharedAnimationDoesNotNeedAnAnimationPerPlayer()
        {
            // The animation reference is per RIG, not per character; the signature only modulates it.
            var rc1 = new CatalogAppearanceResolver(catalogs).Resolve(catalogs.NewSpecification("a"));
            var rc2 = new CatalogAppearanceResolver(catalogs).Resolve(catalogs.NewSpecification("b"));
            Assert.AreEqual(rc1.RigId, rc2.RigId);
            Assert.IsFalse(typeof(AnimationSetReference).GetFields().Any(f => f.Name.ToLowerInvariant().Contains("player") || f.Name.ToLowerInvariant().Contains("character")));
        }
    }
}
