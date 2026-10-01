using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class StyleAndProfileTests
    {
        private DifficultyLibrary difficulties;
        private TeamStyleLibrary styles;

        [SetUp]
        public void SetUp()
        {
            difficulties = DifficultyLibrary.CreateDefault();
            styles = TeamStyleLibrary.CreateDefault();
        }

        // A style that only exists in tests: shows the architecture accepts new styles as pure data.
        private static TeamStyleDefinition HighPressStyle()
        {
            return new TeamStyleDefinition("test-high-press", "Test High Press") { PressTriggerScale = 1.4f, DefensiveLineHeight = 0.85f, Tempo = 0.8f, RiskTaking = 0.65f };
        }

        private static TeamStyleDefinition CounterStyle()
        {
            return new TeamStyleDefinition("test-counter", "Test Counter") { PressTriggerScale = 0.7f, DefensiveLineHeight = 0.25f, Directness = 0.85f, Tempo = 0.75f };
        }

        private static string[] FieldNames(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
        }

        // ================= Team style =================

        [Test]
        public void OnlyTheNeutralBalancedStyleExistsForNow()
        {
            Assert.AreEqual(1, styles.Count);
            Assert.IsTrue(styles.TryGet("balanced", out var s));
            Assert.AreEqual(1f, s.PressTriggerScale);
            Assert.AreEqual(0.5f, s.DefensiveLineHeight);
            Assert.IsTrue(TeamStyleValidator.Validate(s).IsValid);
        }

        [Test]
        public void StyleLibrary_RejectsDuplicatesAndBadIds_AcceptsNewStylesAsData()
        {
            Assert.IsFalse(styles.TryAdd(DefaultTeamStyles.CreateBalanced()));
            Assert.IsFalse(styles.TryAdd(null));
            Assert.IsFalse(styles.TryAdd(new TeamStyleDefinition("bad id", "x")));
            Assert.IsTrue(styles.TryAdd(HighPressStyle()));
            Assert.IsTrue(styles.TryAdd(CounterStyle()));
            Assert.AreEqual(3, styles.Count);
            Assert.IsFalse(styles.TryGet("nope", out _));
            Assert.IsFalse(styles.TryGet(null, out _));
        }

        [Test]
        public void StyleValidator_CatchesBadValues()
        {
            Assert.IsTrue(TeamStyleValidator.Validate(null).Has(AiDataIssueCode.StyleNull));
            Assert.IsTrue(TeamStyleValidator.Validate(new TeamStyleDefinition("", "x")).Has(AiDataIssueCode.StyleIdInvalid));
            Assert.IsTrue(TeamStyleValidator.Validate(new TeamStyleDefinition("a", " ")).Has(AiDataIssueCode.StyleNameInvalid));

            foreach (Action<TeamStyleDefinition> bad in new Action<TeamStyleDefinition>[]
            {
                s => s.PressTriggerScale = 0.1f, s => s.PressTriggerScale = 3f, s => s.PressTriggerScale = float.NaN,
                s => s.DefensiveLineHeight = -0.1f, s => s.AttackWidth = 1.1f, s => s.Directness = 2f, s => s.Tempo = -1f, s => s.RiskTaking = float.NaN
            })
            {
                var s = DefaultTeamStyles.CreateBalanced();
                bad(s);
                Assert.IsTrue(TeamStyleValidator.Validate(s).Has(AiDataIssueCode.StyleValueOutOfRange));
            }
            Assert.IsTrue(TeamStyleValidator.Validate(HighPressStyle()).IsValid);
        }

        // ---- style is NOT difficulty

        [Test]
        public void StyleAndDifficulty_ShareNoParameters_OnlyIdentity()
        {
            var style = FieldNames(typeof(TeamStyleDefinition));
            var difficulty = FieldNames(typeof(DifficultyDefinition));
            CollectionAssert.AreEquivalent(new[] { "Id", "Name" }, style.Intersect(difficulty).ToArray());

            // No skill concept lives in a style and no style concept lives in the difficulty groups.
            string[] skillWords = { "quality", "error", "reaction", "intelligence", "anticipation", "coordination", "discipline" };
            Assert.IsFalse(style.Any(n => skillWords.Any(w => n.ToLowerInvariant().Contains(w))), string.Join(",", style));

            var groupFields = new[] { typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters),
                                      typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters) }
                              .SelectMany(FieldNames).ToArray();
            foreach (string s in new[] { "PressTriggerScale", "DefensiveLineHeight", "AttackWidth", "Directness", "Tempo", "RiskTaking" })
                CollectionAssert.DoesNotContain(groupFields, s);
        }

        [Test]
        public void SameDifficulty_DifferentStyles_PlayDifferently_ButAreTheSameDifficulty()
        {
            styles.TryAdd(HighPressStyle());
            styles.TryAdd(CounterStyle());
            var a = new TeamAiProfile("team-a", "professional", "test-high-press");
            var b = new TeamAiProfile("team-b", "professional", "test-counter");

            Assert.IsTrue(AiProfileResolver.TryResolve(a, difficulties, styles, out var ra));
            Assert.IsTrue(AiProfileResolver.TryResolve(b, difficulties, styles, out var rb));

            Assert.AreSame(ra.Difficulty, rb.Difficulty, "same difficulty definition");
            Assert.AreNotSame(ra.Style, rb.Style);
            Assert.Greater(ra.Style.PressTriggerScale, rb.Style.PressTriggerScale);
            Assert.Greater(ra.Style.DefensiveLineHeight, rb.Style.DefensiveLineHeight);
        }

        [Test]
        public void SameStyle_OnDifferentDifficulties_IsTheSameIdeaWithADifferentSkillLevel()
        {
            Assert.IsTrue(AiProfileResolver.TryResolve(new TeamAiProfile("a", "novice", "balanced"), difficulties, styles, out var novice));
            Assert.IsTrue(AiProfileResolver.TryResolve(new TeamAiProfile("b", "elite", "balanced"), difficulties, styles, out var elite));
            Assert.AreSame(novice.Style, elite.Style);
            Assert.AreNotSame(novice.Difficulty, elite.Difficulty);
        }

        [Test]
        public void ResolvingAProfile_NeverModifiesTheDifficultyOrTheStyle()
        {
            styles.TryAdd(HighPressStyle());
            var dBefore = DifficultyMetrics.All.Select(m => DifficultyMetrics.All.Count + m.Get(difficulties.All[2])).ToArray();
            var scale = styles.All.Single(s => s.Id == "test-high-press").PressTriggerScale;
            AiProfileResolver.TryResolve(new TeamAiProfile("a", "professional", "test-high-press"), difficulties, styles, out _);
            CollectionAssert.AreEqual(dBefore, DifficultyMetrics.All.Select(m => DifficultyMetrics.All.Count + m.Get(difficulties.All[2])).ToArray());
            Assert.AreEqual(scale, styles.All.Single(s => s.Id == "test-high-press").PressTriggerScale);
        }

        [Test]
        public void Resolver_FailsCleanly_ForUnknownIdsOrNulls()
        {
            Assert.IsFalse(AiProfileResolver.TryResolve(new TeamAiProfile("a", "nope", "balanced"), difficulties, styles, out _));
            Assert.IsFalse(AiProfileResolver.TryResolve(new TeamAiProfile("a", "elite", "nope"), difficulties, styles, out _));
            Assert.IsFalse(AiProfileResolver.TryResolve(null, difficulties, styles, out _));
            Assert.IsFalse(AiProfileResolver.TryResolve(new TeamAiProfile(), null, styles, out _));
            Assert.IsFalse(AiProfileResolver.TryResolve(new TeamAiProfile(), difficulties, null, out _));
        }

        [Test]
        public void TeamAiProfile_DefaultsToProfessionalBalanced_AndTeamDefinitionKnowsNeitherConcept()
        {
            var p = new TeamAiProfile();
            Assert.AreEqual("professional", p.DifficultyId);
            Assert.AreEqual("balanced", p.StyleId);
            var teamFields = FieldNames(typeof(TeamDefinition)).Select(n => n.ToLowerInvariant()).ToArray();
            Assert.IsFalse(teamFields.Any(n => n.Contains("difficulty") || n.Contains("style")), "TeamDefinition stays free of AI settings");
        }

        // ================= Versatile players =================

        private static PlayerDefinition Nico()
        {
            // The fictional first player (see Docs/FIRST_PLAYER_SHEET.md).
            return new PlayerDefinition("fs27-p-nico-valmar", "Nico Valmar", 10, PlayerRole.Forward, TestData.Attrs(80));
        }

        private static PlayerPlayingProfile NicoProfile()
        {
            return new PlayerPlayingProfile("fs27-p-nico-valmar", PitchZone.Wing, new[] { PitchZone.Attack, PitchZone.Midfield },
                PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher);
        }

        [Test]
        public void AVersatilePlayer_HasAPrimaryZone_SecondaryZonesAndSeveralRoles()
        {
            var p = NicoProfile();
            Assert.IsTrue(PlayingProfileValidator.Validate(p, Nico()).IsValid);
            Assert.AreEqual(PitchZone.Wing, p.PrimaryZone);
            Assert.IsTrue(p.PlaysIn(PitchZone.Wing));
            Assert.IsTrue(p.PlaysIn(PitchZone.Attack));
            Assert.IsTrue(p.PlaysIn(PitchZone.Midfield));
            Assert.IsFalse(p.PlaysIn(PitchZone.Defense));
            Assert.IsTrue(p.HasArchetype(PlayerArchetype.Explosive));
            Assert.IsTrue(p.HasArchetype(PlayerArchetype.Creator));
            Assert.IsTrue(p.HasArchetype(PlayerArchetype.Finisher));
            Assert.IsFalse(p.HasArchetype(PlayerArchetype.Wall));
        }

        [Test]
        public void ThePlayerDefinition_HoldsIdentityAndAttributes_NotThePlayingProfile()
        {
            var fields = FieldNames(typeof(PlayerDefinition)).Select(n => n.ToLowerInvariant()).ToArray();
            // Identity, basic data and attributes live in the definition (Player System V2 extended it)...
            foreach (string expected in new[] { "id", "name", "number", "role", "attributes", "shortname", "nationalitycode", "teamid", "age", "heightcm", "weightkg", "preferredfoot", "weakfootquality", "bodytype" })
                CollectionAssert.Contains(fields, expected);
            // ...while zones, roles and behaviour stay in the playing profile.
            Assert.IsFalse(fields.Any(n => n.Contains("zone") || n.Contains("archetype") || n.Contains("affinity") || n.Contains("risk") || n.Contains("creativity") || n.Contains("aggression")),
                string.Join(",", fields));
        }

        [Test]
        public void Defaults_FollowTheExistingSingleRole()
        {
            Assert.AreEqual(PitchZone.Goal, PlayingProfileDefaults.ZoneForRole(PlayerRole.Goalkeeper));
            Assert.AreEqual(PitchZone.Defense, PlayingProfileDefaults.ZoneForRole(PlayerRole.Defender));
            Assert.AreEqual(PitchZone.Midfield, PlayingProfileDefaults.ZoneForRole(PlayerRole.Midfielder));
            Assert.AreEqual(PitchZone.Attack, PlayingProfileDefaults.ZoneForRole(PlayerRole.Forward));
            Assert.AreEqual(PlayerArchetype.Guardian, PlayingProfileDefaults.ArchetypeForRole(PlayerRole.Goalkeeper));
            Assert.AreEqual(PlayerArchetype.Wall, PlayingProfileDefaults.ArchetypeForRole(PlayerRole.Defender));
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                var player = new PlayerDefinition("p-" + role, "Generic", 7, role, TestData.Attrs(50));
                Assert.IsTrue(PlayingProfileValidator.Validate(PlayingProfileDefaults.FromPlayer(player), player).IsValid, role.ToString());
            }
        }

        [Test]
        public void Catalog_FindsExplicitProfiles_AndFallsBackToTheRoleDefault()
        {
            var catalog = new PlayingProfileCatalog();
            Assert.IsTrue(catalog.TryAdd(NicoProfile()));
            Assert.IsFalse(catalog.TryAdd(NicoProfile()), "duplicate player id");
            Assert.IsFalse(catalog.TryAdd(null));
            Assert.IsFalse(catalog.TryAdd(new PlayerPlayingProfile("bad id", PitchZone.Attack, null, PlayerArchetype.Finisher)));

            Assert.AreEqual(PitchZone.Wing, catalog.GetOrDefault(Nico()).PrimaryZone);
            var other = new PlayerDefinition("p-def", "Generic", 4, PlayerRole.Defender, TestData.Attrs(60));
            Assert.AreEqual(PitchZone.Defense, catalog.GetOrDefault(other).PrimaryZone);
            Assert.IsFalse(catalog.TryGet(null, out _));
        }

        [Test]
        public void ProfileValidator_CatchesEveryKindOfProblem()
        {
            Assert.IsTrue(PlayingProfileValidator.Validate(null).Has(AiDataIssueCode.ProfileNull));

            var p = NicoProfile(); p.PlayerId = "";
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfilePlayerIdInvalid));

            p = NicoProfile(); p.PrimaryZone = (PitchZone)50;
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileZoneInvalid));

            p = NicoProfile(); p.SecondaryZones.Add((PitchZone)50);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileZoneInvalid));

            p = NicoProfile(); p.SecondaryZones.Add(PitchZone.Attack);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileSecondaryZoneDuplicate));

            p = NicoProfile(); p.SecondaryZones.Add(PitchZone.Wing);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileSecondaryEqualsPrimary));

            p = NicoProfile(); p.Roles.Clear();
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileArchetypeCountInvalid));

            p = NicoProfile(); p.AddRole(PlayerArchetype.Anchor, 50);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileArchetypeCountInvalid), "more than three");

            p = NicoProfile(); SakData.SetRole(p, 1, PlayerArchetype.Explosive);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileArchetypeDuplicate));

            p = NicoProfile(); SakData.SetRole(p, 0, (PlayerArchetype)99);
            Assert.IsTrue(PlayingProfileValidator.Validate(p).Has(AiDataIssueCode.ProfileArchetypeInvalid));
        }

        [Test]
        public void ProfileValidator_KeepsTheGoalZoneForGoalkeepersOnly()
        {
            var keeper = new PlayerDefinition("p-gk", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(70));
            var ok = new PlayerPlayingProfile("p-gk", PitchZone.Goal, null, PlayerArchetype.Guardian, PlayerArchetype.Anchor);
            Assert.IsTrue(PlayingProfileValidator.Validate(ok, keeper).IsValid);

            var keeperOutfield = new PlayerPlayingProfile("p-gk", PitchZone.Midfield, null, PlayerArchetype.Engine);
            Assert.IsTrue(PlayingProfileValidator.Validate(keeperOutfield, keeper).Has(AiDataIssueCode.ProfileGoalkeeperMismatch));

            var outfieldInGoal = new PlayerPlayingProfile("fs27-p-nico-valmar", PitchZone.Attack, new[] { PitchZone.Goal }, PlayerArchetype.Finisher);
            Assert.IsTrue(PlayingProfileValidator.Validate(outfieldInGoal, Nico()).Has(AiDataIssueCode.ProfileGoalkeeperMismatch));
        }

        [Test]
        public void RelativeFormationPositions_MapToZones_AndDefaultFormationsAreSensible()
        {
            Assert.AreEqual(PitchZone.Goal, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.06f, 0.5f)));
            Assert.AreEqual(PitchZone.Defense, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.28f, 0.5f)));
            Assert.AreEqual(PitchZone.Midfield, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.50f, 0.5f)));
            Assert.AreEqual(PitchZone.Attack, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.78f, 0.5f)));
            Assert.AreEqual(PitchZone.Wing, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.5f, 0.05f)));
            Assert.AreEqual(PitchZone.Wing, PlayingProfileDefaults.ZoneOfRelativePosition(new Vec2(0.7f, 0.95f)));

            foreach (var f in DefaultFormations.CreateAll())
            {
                Assert.AreEqual(PitchZone.Goal, PlayingProfileDefaults.ZoneOfRelativePosition(f.Positions.First(p => p.Role == PlayerRole.Goalkeeper).Relative), f.Id);
                Assert.IsTrue(f.Positions.Where(p => p.Role != PlayerRole.Goalkeeper)
                    .All(p => PlayingProfileDefaults.ZoneOfRelativePosition(p.Relative) != PitchZone.Goal), f.Id);
            }
        }

        // ---- difficulty changes HOW WELL a role is played, never the role

        private readonly ZoneFitnessTuning fit = new ZoneFitnessTuning();

        [Test]
        public void InThePrimaryZone_RoleQualityIsTheLevelsRoleDiscipline()
        {
            foreach (var d in difficulties.All)
                Assert.AreEqual(d.Decision.RoleDiscipline, RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Wing, fit), 1e-6f, d.Id);
        }

        [Test]
        public void PlayingOutOfPosition_IsWorse_AndSecondaryZonesAreBetterThanUnrelatedOnes()
        {
            var d = difficulties.All[2];
            float primary = RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Wing, fit);
            float secondary = RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Attack, fit);
            float unrelated = RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Defense, fit);
            Assert.Greater(primary, secondary);
            Assert.Greater(secondary, unrelated);
        }

        [Test]
        public void HarderLevels_PlayTheSameRoleBetter_InEveryZone()
        {
            foreach (PitchZone z in Enum.GetValues(typeof(PitchZone)))
            {
                float prev = -1f;
                foreach (var d in difficulties.All)
                {
                    float q = RoleExecutionModel.Quality(d, NicoProfile(), z, fit);
                    Assert.Greater(q, prev, d.Id + " in " + z);
                    prev = q;
                }
            }
        }

        [Test]
        public void HarderLevels_LoseLessWhenOutOfPosition()
        {
            float Loss(DifficultyDefinition d)
            {
                float home = RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Wing, fit);
                float away = RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Defense, fit);
                return (home - away) / home;
            }
            Assert.Less(Loss(difficulties.All[4]), Loss(difficulties.All[0]), "relative loss out of position shrinks with adaptability");
        }

        [Test]
        public void ComputingRoleQuality_NeverChangesTheProfile()
        {
            var p = NicoProfile();
            foreach (var d in difficulties.All)
                foreach (PitchZone z in Enum.GetValues(typeof(PitchZone)))
                    RoleExecutionModel.Quality(d, p, z, fit);

            Assert.AreEqual(PitchZone.Wing, p.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Attack, PitchZone.Midfield }, p.SecondaryZones);
            CollectionAssert.AreEqual(new[] { PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher }, p.Roles.Select(r => r.Role).ToArray());
        }

        [Test]
        public void ZoneFitnessIsConfigurable()
        {
            var d = difficulties.All[2];
            var strict = new ZoneFitnessTuning { OtherFitness = 0f };
            var lenient = new ZoneFitnessTuning { OtherFitness = 1f };
            Assert.Less(RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Defense, strict),
                        RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Defense, lenient));
            Assert.AreEqual(d.Decision.RoleDiscipline, RoleExecutionModel.Quality(d, NicoProfile(), PitchZone.Defense, lenient), 1e-6f);
        }
    }
}
