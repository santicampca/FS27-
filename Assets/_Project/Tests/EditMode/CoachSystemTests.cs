using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The coach system: the grade scale, the formation rules, the data file of the 20 initial coaches, the compatibility calculation and the coach boost (+1 at most, never a penalty,
    /// the base rating never changes). Mechanism tests use synthetic players and coaches; the data tests read the committed coach file.
    /// </summary>
    public class CoachSystemTests
    {
        private static readonly string[] ExpectedNames =
        {
            "Pep Guardiola", "Hansi Flick", "Mikel Arteta", "Carlo Ancelotti", "Lionel Scaloni", "Diego Simeone", "Thomas Tuchel", "José Mourinho", "Luis de la Fuente", "Zinedine Zidane",
            "Cesc Fàbregas", "Luis Enrique", "Vincent Kompany", "Yaya Touré", "Hajime Moriyasu", "Massimiliano Allegri", "Roberto Mancini", "Louis van Gaal", "Manuel Pellegrini", "Mauricio Pochettino"
        };

        private static string root;
        private static List<CoachDefinition> coaches;
        private static FormationLibrary formations;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            Assert.IsTrue(CoachJson.TryFromJson(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_COACHES_V1.json")), out coaches, out string error), error);
            formations = new FormationLibrary();
            foreach (FormationDefinition f in DefaultFormations.CreateExtended()) formations.TryAdd(f);
        }

        // ------------------------------------------------------------------ synthetic helpers (no names)

        private static PlayerDefinition Player(string id, int baseValue, PlayerRole role = PlayerRole.Midfielder)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            foreach (PlayerAttributeId attr in PlayerAttributeInfo.All) a = a.With(attr, baseValue);
            return new PlayerDefinition(id, "Synthetic " + id, 0, role, a);
        }

        private static PlayerPlayingProfile Profile(string id, PitchZone zone, params RoleAffinity[] roles)
        {
            return new PlayerPlayingProfile(id, zone, null, roles.Length > 0 ? roles : new[] { new RoleAffinity(PlayerArchetype.Engine, 90) });
        }

        /// <summary>A coach that only looks at one zone: its score for a player whose primary zone it is equals the weight.</summary>
        private static CoachDefinition ZoneOnlyCoach(string id, PitchZone zone, int weight)
        {
            var c = new CoachDefinition { CoachId = id, Name = id, FullName = id, Grade = CoachGrade.B };
            c.CompatibilityProfile.Zones.Add(new WeightedZone(zone, weight));
            c.CompatibilityProfile.ZoneImportance = 100;
            c.CompatibilityProfile.RoleImportance = 0; c.CompatibilityProfile.AttributeImportance = 0; c.CompatibilityProfile.BehaviourImportance = 0; c.CompatibilityProfile.BodyImportance = 0;
            return c;
        }

        // ============================================================ the scale

        [Test]
        public void TheScale_IsExactlyEightLetters_NoSNoD()
        {
            string[] labels = { "A+", "A", "A-", "B+", "B", "B-", "C+", "C" };
            foreach (string l in labels) { Assert.IsTrue(CoachGradeInfo.TryParse(l, out CoachGrade g), l); Assert.AreEqual(l, CoachGradeInfo.ToLabel(g)); }
            foreach (string bad in new[] { "S", "D", "A++", "a", "B--", "", "E", "C-" }) Assert.IsFalse(CoachGradeInfo.TryParse(bad, out _), bad);
            Assert.AreEqual(8, Enum.GetValues(typeof(CoachGrade)).Length);
            Assert.IsTrue(CoachGrade.APlus > CoachGrade.A && CoachGrade.A > CoachGrade.AMinus && CoachGrade.AMinus > CoachGrade.BPlus && CoachGrade.CPlus > CoachGrade.C);
        }

        [Test]
        public void TheGrade_DecidesTheNumberOfFormations_ThreeTwoOne()
        {
            foreach (CoachGrade g in new[] { CoachGrade.APlus, CoachGrade.A, CoachGrade.AMinus }) Assert.AreEqual(3, CoachGradeInfo.FormationCount(g), g.ToString());
            foreach (CoachGrade g in new[] { CoachGrade.BPlus, CoachGrade.B, CoachGrade.BMinus }) Assert.AreEqual(2, CoachGradeInfo.FormationCount(g), g.ToString());
            foreach (CoachGrade g in new[] { CoachGrade.CPlus, CoachGrade.C }) Assert.AreEqual(1, CoachGradeInfo.FormationCount(g), g.ToString());
        }

        // ============================================================ the boost rules

        [Test]
        public void TheRules_DefaultTo80_PlusOne_MaximumOne_AndNeverGiveAPenalty()
        {
            var rules = new CoachBoostRules();
            Assert.AreEqual(80, rules.CompatibilityThreshold); Assert.AreEqual(1, rules.Boost); Assert.AreEqual(1, rules.MaximumBoost);
            for (int score = 0; score <= 100; score++)
            {
                int boost = rules.BoostFor(score);
                Assert.IsTrue(boost == 0 || boost == 1, "score " + score);
                Assert.AreEqual(score >= 80 ? 1 : 0, boost, "score " + score);
            }
            Assert.AreEqual(0, rules.BoostFor(0)); Assert.AreEqual(0, rules.BoostFor(59)); Assert.AreEqual(0, rules.BoostFor(79)); Assert.AreEqual(1, rules.BoostFor(80)); Assert.AreEqual(1, rules.BoostFor(100));
        }

        [Test]
        public void TheRules_AreConfigurable_ButStillNeverNegativeAndCappedByTheMaximum()
        {
            var rules = new CoachBoostRules { CompatibilityThreshold = 70, Boost = 5, MaximumBoost = 1 };
            Assert.AreEqual(1, rules.BoostFor(100), "the maximum caps the boost");
            Assert.AreEqual(0, rules.BoostFor(69)); Assert.AreEqual(1, rules.BoostFor(70));
            Assert.IsFalse(new CoachBoostRules { Boost = -1 }.IsValid, "a negative boost is not a valid rule");
            Assert.IsFalse(new CoachBoostRules { MaximumBoost = -1 }.IsValid);
            Assert.Throws<ArgumentException>(() => new CoachCompatibilityCalculator(new CoachBoostRules { Boost = -1 }));
            Assert.AreEqual(0, new CoachBoostRules { Boost = 0 }.BoostFor(100));
        }

        // ============================================================ the important test: base never changes

        [Test]
        public void ImportantCase_Base84_CoachA85_GivesPlusOne_CoachB70_GivesZero_AndSwitchingNeverTouchesTheBase()
        {
            PlayerDefinition player = Player("p1", 70);
            PlayerPlayingProfile profile = Profile("p1", PitchZone.Midfield);
            CoachDefinition coachA = ZoneOnlyCoach("coach-a", PitchZone.Midfield, 85), coachB = ZoneOnlyCoach("coach-b", PitchZone.Midfield, 70);
            var calc = new CoachCompatibilityCalculator();

            CoachCompatibilityResult a = calc.Evaluate(coachA, player, profile), b = calc.Evaluate(coachB, player, profile);
            Assert.AreEqual(85, a.CompatibilityScore); Assert.AreEqual(1, a.CoachBoost);
            Assert.AreEqual(70, b.CompatibilityScore); Assert.AreEqual(0, b.CoachBoost);

            EffectiveRating withA = calc.ApplyToRating(84, a), withB = calc.ApplyToRating(84, b);
            Assert.AreEqual(84, withA.BaseOverall); Assert.AreEqual(1, withA.CoachBoost); Assert.AreEqual(85, withA.EffectiveOverall);
            Assert.AreEqual(84, withB.BaseOverall); Assert.AreEqual(0, withB.CoachBoost); Assert.AreEqual(84, withB.EffectiveOverall);

            // with the real calculator: the base is the player's own overall, whichever coach is used, and no stored number moves
            var rating = PlayerRatingCalculator.CreateDefault();
            int before = rating.GetOverall(player, profile);
            EffectiveRating realA = calc.GetEffectiveRating(coachA, player, profile, rating), realB = calc.GetEffectiveRating(coachB, player, profile, rating);
            Assert.AreEqual(before, realA.BaseOverall); Assert.AreEqual(before, realB.BaseOverall);
            Assert.AreEqual(before + 1, realA.EffectiveOverall); Assert.AreEqual(before, realB.EffectiveOverall);
            Assert.AreEqual(before, rating.GetOverall(player, profile), "switching coaches never changes the base overall");
            Assert.AreEqual(70, player.Attributes.Speed, "no attribute was touched");
            Assert.AreEqual(PlayerArchetype.Engine, profile.Roles[0].Role); Assert.AreEqual(90, profile.Roles[0].Affinity);
        }

        [Test]
        public void APlayerWhoDoesNotFit_KeepsTheirRating_NeverBelowTheBase()
        {
            PlayerDefinition player = Player("p2", 70);
            PlayerPlayingProfile profile = Profile("p2", PitchZone.Defense);
            CoachDefinition coach = ZoneOnlyCoach("coach-x", PitchZone.Attack, 90);
            var calc = new CoachCompatibilityCalculator();
            CoachCompatibilityResult r = calc.Evaluate(coach, player, profile);
            Assert.AreEqual(0, r.CompatibilityScore); Assert.AreEqual(0, r.CoachBoost);
            Assert.AreEqual(84, calc.ApplyToRating(84, r).EffectiveOverall);
            Assert.AreEqual(99, new EffectiveRating(99, 1).EffectiveOverall, "the rating never goes above 99");
            Assert.AreEqual(84, new EffectiveRating(84, -3).EffectiveOverall, "a negative boost is treated as none");
        }

        [Test]
        public void ASecondaryZone_CountsLessThanThePrimaryOne()
        {
            PlayerPlayingProfile primary = Profile("p3", PitchZone.Midfield);
            var secondary = new PlayerPlayingProfile("p3", PitchZone.Defense, new[] { PitchZone.Midfield }, new[] { new RoleAffinity(PlayerArchetype.Engine, 90) });
            CoachDefinition coach = ZoneOnlyCoach("coach-z", PitchZone.Midfield, 100);
            var calc = new CoachCompatibilityCalculator();
            PlayerDefinition player = Player("p3", 70);
            Assert.AreEqual(100, calc.Evaluate(coach, player, primary).CompatibilityScore);
            Assert.AreEqual(80, calc.Evaluate(coach, player, secondary).CompatibilityScore);
        }

        [Test]
        public void TheScore_IsDataDriven_RolesAttributesAndBehaviourAllMatter_AndAnEmptyPartIsLeftOut()
        {
            var coach = new CoachDefinition { CoachId = "coach-d", Name = "d", FullName = "d", Grade = CoachGrade.B };
            CoachCompatibilityProfile cp = coach.CompatibilityProfile;
            cp.Roles.Add(new WeightedRole(PlayerArchetype.Architect, 100)); cp.RoleImportance = 50;
            cp.Attributes.Set(PlayerAttributeId.Passing, 1f); cp.AttributeImportance = 30;
            cp.Creativity = new BehaviourPreference(90, 100); cp.BehaviourImportance = 20;
            cp.ZoneImportance = 0; cp.BodyImportance = 0;
            PlayerDefinition strong = Player("p4", 92); PlayerDefinition weak = Player("p5", 40);
            var fitProfile = Profile("p4", PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Architect, 100)); fitProfile.Creativity = 90;
            var missProfile = Profile("p5", PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Wall, 100)); missProfile.Creativity = 10;
            var calc = new CoachCompatibilityCalculator();
            CoachCompatibilityResult fit = calc.Evaluate(coach, strong, fitProfile), miss = calc.Evaluate(coach, weak, missProfile);
            Assert.AreEqual(100, fit.CompatibilityScore); Assert.AreEqual(1, fit.CoachBoost);
            Assert.Less(miss.CompatibilityScore, 60); Assert.AreEqual(0, miss.CoachBoost);
            Assert.AreEqual(3, fit.Components.Count, "zones and body had no importance, so only three parts were used");
            CollectionAssert.Contains(fit.MatchedCriteria, "Role:Architect"); CollectionAssert.Contains(fit.MatchedCriteria, "Behaviour:Creativity"); Assert.IsTrue(fit.MatchedCriteria.Any(m => m.StartsWith("Attribute:Passing")));
            Assert.AreEqual(0, new CoachCompatibilityCalculator().Evaluate(new CoachDefinition { CoachId = "e", Name = "e", FullName = "e" }, strong, fitProfile).CompatibilityScore, "no usable part, no score");
        }

        // ============================================================ the 20 coaches (data)

        [Test]
        public void TheTwentyCoaches_Exist_InOrder_AllValid_WithUniqueIds()
        {
            CollectionAssert.AreEqual(ExpectedNames, coaches.Select(c => c.Name).ToArray());
            Assert.AreEqual(20, coaches.Select(c => c.CoachId).Distinct().Count());
            foreach (CoachDefinition c in coaches)
            {
                CoachValidationResult v = CoachValidator.Validate(c, formations);
                Assert.IsTrue(v.IsValid, c.Name + "\n" + v);
                Assert.IsTrue(CoachGradeInfo.IsValid(c.Grade));
            }
            var library = new CoachLibrary();
            foreach (CoachDefinition c in coaches) Assert.IsTrue(library.TryAdd(c));
            Assert.AreEqual(20, library.Count); Assert.IsFalse(library.TryAdd(coaches[0]), "a repeated id is refused");
        }

        [Test]
        public void TheGrades_FollowTheScale_AndTheFormationCountsFollowTheGrades()
        {
            foreach (CoachDefinition c in coaches) Assert.AreEqual(CoachGradeInfo.FormationCount(c.Grade), c.AvailableFormations.Count, c.Name + " " + CoachGradeInfo.ToLabel(c.Grade));
            Assert.AreEqual(14, coaches.Count(c => c.Grade >= CoachGrade.AMinus)); Assert.AreEqual(5, coaches.Count(c => c.Grade >= CoachGrade.BMinus && c.Grade < CoachGrade.AMinus)); Assert.AreEqual(1, coaches.Count(c => c.Grade < CoachGrade.BMinus));
            Assert.AreEqual(14 * 3 + 5 * 2 + 1, coaches.Sum(c => c.AvailableFormations.Count));
            CoachDefinition touré = coaches.First(c => c.Name == "Yaya Touré");
            Assert.AreEqual(CoachGrade.CPlus, touré.Grade, "valued as a COACH: first head-coach job in 2026");
            Assert.IsTrue(coaches.Any(c => c.Grade == CoachGrade.APlus) && coaches.Any(c => c.Grade == CoachGrade.BPlus));
        }

        [Test]
        public void TheFormations_AreRealVariants_NotTheSameThreeForEveryone()
        {
            var shapes = new HashSet<string>();
            foreach (CoachDefinition c in coaches)
            {
                var names = c.AvailableFormations.Select(f => f.DisplayName).ToList();
                Assert.AreEqual(names.Count, names.Distinct().Count(), c.Name + " has repeated names");
                foreach (CoachFormationProfile f in c.AvailableFormations)
                {
                    Assert.AreEqual(c.CoachId, f.CoachId); StringAssert.StartsWith(c.CoachId, f.FormationId);
                    Assert.IsNotEmpty(f.TacticalPurpose); Assert.GreaterOrEqual(f.CoachInstructions.Count, 3);
                    shapes.Add(f.BaseFormation + ":" + string.Join(",", f.Slots.Select(s => s.Zone + "/" + s.Role)) + ":" + f.PressingStyle + ":" + f.BuildUpStyle);
                }
            }
            Assert.AreEqual(53, shapes.Count, "no two formations of the whole set are identical");
            // two coaches of the same grade play differently
            CoachDefinition a = coaches.First(c => c.Name == "Pep Guardiola"), b = coaches.First(c => c.Name == "Diego Simeone");
            Assert.AreEqual(a.Grade, b.Grade); Assert.AreEqual(a.AvailableFormations.Count, b.AvailableFormations.Count);
            CollectionAssert.AreNotEquivalent(a.PreferredRoles, b.PreferredRoles);
            Assert.AreNotEqual(a.AvailableFormations[0].PressingStyle, b.AvailableFormations[0].PressingStyle);
            Assert.AreNotEqual(a.AvailableFormations[0].BuildUpStyle, b.AvailableFormations[0].BuildUpStyle);
            Assert.AreNotEqual(a.PreferredZones[0], b.PreferredZones[0]);
            // coaches are not cloned from one template
            Assert.Greater(coaches.Select(c => string.Join(",", c.PreferredRoles)).Distinct().Count(), 14);
            Assert.Greater(coaches.Select(c => c.Philosophy).Distinct().Count(), 19);
        }

        [Test]
        public void TheBackThree_IsACoachCapability_GivenOnlyToTheDefensiveOrStructuralCoaches_WithAReason()
        {
            string[] expected = { "Diego Simeone", "Thomas Tuchel", "José Mourinho", "Hajime Moriyasu", "Massimiliano Allegri", "Louis van Gaal" };
            CollectionAssert.AreEquivalent(expected, coaches.Where(c => c.HasBackThree).Select(c => c.Name).ToArray());
            Assert.AreEqual(14, coaches.Count(c => !c.HasBackThree), "not everyone gets a line of three");
            foreach (CoachDefinition c in coaches.Where(c => c.HasBackThree))
            {
                Assert.IsTrue(c.Notes.Any(n => n.Kind != CoachNoteKind.Authoring && n.Text.Contains("tres")), c.Name + " has no note that justifies the line of three");
                foreach (CoachFormationProfile f in c.AvailableFormations.Where(f => f.BackLineType == BackLineType.Three))
                {
                    Assert.AreEqual(DefaultFormations.ThreeOneOne, f.BaseFormation); Assert.AreEqual(3, f.DefenderSlots);
                    Assert.AreEqual(2, f.Slots.Count(s => s.Zone != PitchZone.Defense));
                }
            }
            Assert.IsFalse(coaches.First(c => c.Name == "Luis Enrique").HasBackThree);
        }

        [Test]
        public void EveryFormation_UsesOnlyTheExistingZonesAndTheTwelveOfficialRoles_AndIsAPlayableSixAside()
        {
            var officialRoles = new HashSet<PlayerArchetype>((PlayerArchetype[])Enum.GetValues(typeof(PlayerArchetype)));
            Assert.AreEqual(12, officialRoles.Count);
            foreach (CoachDefinition c in coaches)
                foreach (CoachFormationProfile f in c.AvailableFormations)
                {
                    Assert.AreEqual(DataRules.FieldPlayersPerTeam, f.Slots.Count);
                    Assert.IsTrue(f.Slots.All(s => s.Zone != PitchZone.Goal && Enum.IsDefined(typeof(PitchZone), s.Zone) && officialRoles.Contains(s.Role)), f.FormationId);
                    Assert.IsTrue(formations.TryGet(f.BaseFormation, out FormationDefinition baseDef), f.BaseFormation);
                    FormationDefinition built = f.ToFormationDefinition(baseDef);
                    Assert.IsNotNull(built, f.FormationId);
                    Assert.AreEqual(DataRules.PlayersPerTeam, built.Positions.Count); Assert.AreEqual(DataRules.GoalkeepersPerTeam, built.Positions.Count(p => p.Role == PlayerRole.Goalkeeper));
                    Assert.AreEqual(DataRules.FieldPlayersPerTeam, built.Positions.Count(p => p.Role != PlayerRole.Goalkeeper));
                    Assert.IsTrue(DataValidator.ValidateFormation(built).IsValid, f.FormationId + "\n" + DataValidator.ValidateFormation(built));
                    Assert.AreEqual((int)f.BackLineType, built.Positions.Count(p => p.Zone == PitchZone.Defense));
                }
            Assert.AreEqual(6, DataRules.PlayersPerTeam); Assert.AreEqual(1, DataRules.GoalkeepersPerTeam); Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
        }

        [Test]
        public void TheExistingFormations_AreUntouched_AndTheBackThreeIsOnlyAnAddition()
        {
            List<FormationDefinition> three = DefaultFormations.CreateAll();
            CollectionAssert.AreEqual(new[] { "2-1-2", "1-2-2", "2-2-1" }, three.Select(f => f.Id).ToArray());
            List<FormationDefinition> four = DefaultFormations.CreateExtended();
            CollectionAssert.AreEqual(new[] { "2-1-2", "1-2-2", "2-2-1", "3-1-1" }, four.Select(f => f.Id).ToArray());
            Assert.IsTrue(DataValidator.ValidateFormation(DefaultFormations.CreateThreeOneOne()).IsValid);
            Assert.AreEqual(3, DefaultFormations.CreateThreeOneOne().Positions.Count(p => p.Zone == PitchZone.Defense));
        }

        [Test]
        public void TheValidator_RejectsBrokenCoaches_WithTheRightCodes()
        {
            CoachDefinition Good() { return CoachJsonCopy(coaches.First(c => c.Name == "Mikel Arteta")); }
            Assert.IsTrue(CoachValidator.Validate(Good(), formations).IsValid);

            CoachDefinition c1 = Good(); c1.Grade = CoachGrade.CPlus; Assert.IsTrue(CoachValidator.Validate(c1, formations).Has(CoachIssueCode.FormationCountWrong), "three formations for a C+");
            CoachDefinition c2 = Good(); c2.AvailableFormations.RemoveAt(2); Assert.IsTrue(CoachValidator.Validate(c2, formations).Has(CoachIssueCode.FormationCountWrong));
            CoachDefinition c3 = Good(); c3.AvailableFormations[0].BaseFormation = "9-9-9"; Assert.IsTrue(CoachValidator.Validate(c3, formations).Has(CoachIssueCode.BaseFormationUnknown));
            CoachDefinition c4 = Good(); c4.AvailableFormations[0].Slots.RemoveAt(4); Assert.IsTrue(CoachValidator.Validate(c4, formations).Has(CoachIssueCode.SlotCountWrong));
            CoachDefinition c5 = Good(); c5.AvailableFormations[0].Slots[2] = new CoachFormationSlot(PitchZone.Goal, PlayerArchetype.Wall); Assert.IsTrue(CoachValidator.Validate(c5, formations).Has(CoachIssueCode.SlotZoneInvalid), "a goalkeeper zone for a field slot");
            CoachDefinition c6 = Good(); c6.AvailableFormations[0].Slots[0] = new CoachFormationSlot(PitchZone.Midfield, PlayerArchetype.Wall); Assert.IsTrue(CoachValidator.Validate(c6, formations).Has(CoachIssueCode.SlotZoneNotAllowedForBase));
            CoachDefinition c7 = Good(); c7.AvailableFormations[0].BackLineType = BackLineType.Three; Assert.IsTrue(CoachValidator.Validate(c7, formations).Has(CoachIssueCode.BackLineMismatch));
            CoachDefinition c8 = Good(); c8.AvailableFormations[0].Slots[3] = new CoachFormationSlot(PitchZone.Wing, (PlayerArchetype)99); Assert.IsTrue(CoachValidator.Validate(c8, formations).Has(CoachIssueCode.SlotRoleInvalid), "a role that does not exist");
            CoachDefinition c9 = Good(); c9.AvailableFormations[0].Risk = 150; Assert.IsTrue(CoachValidator.Validate(c9, formations).Has(CoachIssueCode.DialOutOfRange));
            CoachDefinition c10 = Good(); c10.Grade = (CoachGrade)42; Assert.IsTrue(CoachValidator.Validate(c10, formations).Has(CoachIssueCode.GradeInvalid));
            CoachDefinition c11 = Good(); c11.CompatibilityProfile = new CoachCompatibilityProfile { RoleImportance = 0, ZoneImportance = 0, AttributeImportance = 0, BehaviourImportance = 0, BodyImportance = 0 };
            Assert.IsTrue(CoachValidator.Validate(c11, formations).Has(CoachIssueCode.CompatibilityProfileEmpty));
            CoachDefinition c12 = Good(); c12.CompatibilityProfile.Roles.Add(new WeightedRole(PlayerArchetype.Wall, 150)); Assert.IsTrue(CoachValidator.Validate(c12, formations).Has(CoachIssueCode.CompatibilityWeightOutOfRange));
            CoachDefinition c13 = Good(); c13.AvailableFormations[1].FormationId = c13.AvailableFormations[0].FormationId; Assert.IsTrue(CoachValidator.Validate(c13, formations).Has(CoachIssueCode.FormationIdDuplicate));
            Assert.IsTrue(CoachValidator.Validate(null, formations).Has(CoachIssueCode.CoachNull));
        }

        private static CoachDefinition CoachJsonCopy(CoachDefinition c)
        {
            Assert.IsTrue(CoachJson.TryFromJson(CoachJson.ToJson(new[] { c }), out List<CoachDefinition> list, out string error), error);
            return list[0];
        }

        // ============================================================ compatibility with the real players

        private static string P(string f) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", f)); }

        private static List<(PlayerDefinition player, PlayerPlayingProfile profile)> RealFieldPlayers()
        {
            var batches = new List<FieldPlayerLibraryBuilder.Batch>
            {
                FieldPlayerLibraryBuilder.ImportMarkdown(P("FS27_DEFENDERS_DATABASE_V1.md"), "cb", null, null, P("FS27_DEFENDERS_CORRECTIONS_V1.md")),
                FieldPlayerLibraryBuilder.ImportSectionedMarkdown(P("FS27_FULLBACKS_DATABASE_V1.md"), "fb", FieldPlayerImportOptions.Fullback(), null, P("FS27_FULLBACKS_CORRECTIONS_V1.md")),
                FieldPlayerLibraryBuilder.ImportCardMarkdown(P("FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md"), "md", FieldPlayerImportOptions.HybridMidfielder(), null, P("FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md")),
                FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(P("FS27_MCO_DATABASE_V1.md"), "mo", FieldPlayerImportOptions.OffensiveMidfielder(), null, P("FS27_MCO_CORRECTIONS_V1.md")),
                FieldPlayerLibraryBuilder.ImportListMarkdown(P("FS27_WINGERS_DATABASE_V1.md"), "wg", FieldPlayerImportOptions.Winger(), null, P("FS27_WINGERS_CORRECTIONS_V1.md")),
                FieldPlayerLibraryBuilder.ImportListMarkdown(P("FS27_DC_DATABASE_V1.md"), "dc", FieldPlayerImportOptions.Striker(), null, P("FS27_DC_CORRECTIONS_V1.md"))
            };
            return batches.SelectMany(b => b.Results.Where(r => r.IsComplete)).Select(r => (r.Player, r.Profile)).ToList();
        }

        [Test]
        public void WithTheRealPlayers_EveryScoreIsInRange_TheBoostIsZeroOrOne_AndNobodyEverLosesRating()
        {
            var players = RealFieldPlayers();
            Assert.GreaterOrEqual(players.Count, 80);
            var calc = new CoachCompatibilityCalculator(); var rating = PlayerRatingCalculator.CreateDefault();
            foreach (CoachDefinition c in coaches)
            {
                int boosted = 0;
                foreach (var (player, profile) in players)
                {
                    CoachCompatibilityResult r = calc.Evaluate(c, player, profile);
                    Assert.IsTrue(r.CompatibilityScore >= 0 && r.CompatibilityScore <= 100, c.Name + " / " + player.Id);
                    Assert.IsTrue(r.CoachBoost == 0 || r.CoachBoost == 1);
                    Assert.AreEqual(r.CompatibilityScore >= 80 ? 1 : 0, r.CoachBoost);
                    EffectiveRating e = calc.GetEffectiveRating(c, player, profile, rating);
                    Assert.AreEqual(rating.GetOverall(player, profile), e.BaseOverall);
                    Assert.IsTrue(e.EffectiveOverall >= e.BaseOverall && e.EffectiveOverall <= e.BaseOverall + 1 && e.EffectiveOverall <= 99);
                    if (r.CoachBoost > 0) { boosted++; Assert.IsNotEmpty(r.MatchedCriteria, c.Name + " boosted " + player.Id + " with nothing matched"); }
                }
                Assert.Greater(boosted, 0, c.Name + " boosts nobody"); Assert.Less(boosted, players.Count / 2, c.Name + " boosts half of the library");
            }
        }

        [Test]
        public void TheBoostIsContextual_TheSameRealPlayerFitsOneCoachAndNotAnother_AndSwitchingRecalculatesWithoutChangingTheBase()
        {
            var players = RealFieldPlayers();
            var calc = new CoachCompatibilityCalculator(); var rating = PlayerRatingCalculator.CreateDefault();
            CoachDefinition a = coaches.First(c => c.Name == "Pep Guardiola"), b = coaches.First(c => c.Name == "Diego Simeone");
            var onlyA = players.Where(p => calc.Evaluate(a, p.player, p.profile).CoachBoost == 1 && calc.Evaluate(b, p.player, p.profile).CoachBoost == 0).ToList();
            var onlyB = players.Where(p => calc.Evaluate(a, p.player, p.profile).CoachBoost == 0 && calc.Evaluate(b, p.player, p.profile).CoachBoost == 1).ToList();
            Assert.Greater(onlyA.Count, 0); Assert.Greater(onlyB.Count, 0);
            var (player, profile) = onlyA[0];
            int baseBefore = rating.GetOverall(player, profile);
            Assert.AreEqual(baseBefore + 1, calc.GetEffectiveRating(a, player, profile, rating).EffectiveOverall);
            Assert.AreEqual(baseBefore, calc.GetEffectiveRating(b, player, profile, rating).EffectiveOverall);
            Assert.AreEqual(baseBefore + 1, calc.GetEffectiveRating(a, player, profile, rating).EffectiveOverall, "back to the first coach: the same boost again");
            Assert.AreEqual(baseBefore, rating.GetOverall(player, profile), "the base overall never moved");
        }

        [Test]
        public void EvaluatingEveryCoach_ChangesNothingInThePlayersOrTheirProfiles()
        {
            var players = RealFieldPlayers();
            var calc = new CoachCompatibilityCalculator(); var rating = PlayerRatingCalculator.CreateDefault();
            string Snapshot(PlayerDefinition p, PlayerPlayingProfile pr) => p.Id + "|" + string.Join(",", PlayerAttributeInfo.All.Select(id => p.Attributes.GetValue(id))) + "|" + rating.GetOverall(p, pr) + "|" + pr.PrimaryZone + "|"
                + string.Join(",", pr.SecondaryZones) + "|" + string.Join(",", pr.Roles.Select(r => r.Role + ":" + r.Affinity)) + "|" + pr.RiskPreference + "," + pr.Creativity + "," + pr.Aggression;
            var before = players.Select(p => Snapshot(p.player, p.profile)).ToList();
            foreach (CoachDefinition c in coaches) foreach (var p in players) calc.GetEffectiveRating(c, p.player, p.profile, rating);
            CollectionAssert.AreEqual(before, players.Select(p => Snapshot(p.player, p.profile)).ToList());
        }

        [Test]
        public void ThePlayerCard_StaysTheSame_TheCoachViewAddsTheBoostOnTop()
        {
            PlayerDefinition player = Player("card", 80);
            PlayerPlayingProfile profile = Profile("card", PitchZone.Midfield);
            var rating = PlayerRatingCalculator.CreateDefault();
            var card = new PlayerCardData(player, profile, null, CardType.Standard, rating);
            var calc = new CoachCompatibilityCalculator();
            PlayerCardCoachView view = PlayerCardCoachView.Create(card, calc, ZoneOnlyCoach("coach-v", PitchZone.Midfield, 90), player, profile);
            Assert.AreEqual(card.Overall, view.BaseOverall); Assert.AreEqual(1, view.CoachBoost); Assert.AreEqual(card.Overall + 1, view.EffectiveOverall); Assert.AreEqual(90, view.CompatibilityScore);
            Assert.AreEqual(rating.GetOverall(player, profile), card.Overall, "the card has no coach number in it");
            PlayerCardCoachView none = PlayerCardCoachView.Create(card, calc, ZoneOnlyCoach("coach-w", PitchZone.Attack, 90), player, profile);
            Assert.AreEqual(card.Overall, none.EffectiveOverall);
        }

        [Test]
        public void AGoalkeeperRating_CanBeBoostedFromOutside_WithoutTouchingTheGrlCalculation()
        {
            var calc = new CoachCompatibilityCalculator();
            CoachCompatibilityResult r = calc.Evaluate(ZoneOnlyCoach("coach-g", PitchZone.Goal, 95), Player("gk", 70, PlayerRole.Goalkeeper), new PlayerPlayingProfile("gk", PitchZone.Goal, null, new[] { new RoleAffinity(PlayerArchetype.Guardian, 90) }));
            Assert.AreEqual(95, r.CompatibilityScore);
            EffectiveRating e = calc.ApplyToRating(82, r);
            Assert.AreEqual(82, e.BaseOverall); Assert.AreEqual(83, e.EffectiveOverall);
        }

        // ============================================================ style, json, no names

        [Test]
        public void EveryFormation_CanBeReadAsAnExistingTeamStyle_WithTheDialsInRange()
        {
            foreach (CoachDefinition c in coaches)
                foreach (CoachFormationProfile f in c.AvailableFormations)
                {
                    TeamStyleDefinition s = f.ToTeamStyle();
                    Assert.AreEqual(f.FormationId, s.Id);
                    Assert.IsTrue(s.PressTriggerScale >= TeamStyleDefinition.MinPressTriggerScale && s.PressTriggerScale <= TeamStyleDefinition.MaxPressTriggerScale);
                    foreach (float v in new[] { s.DefensiveLineHeight, s.AttackWidth, s.Directness, s.Tempo, s.RiskTaking }) Assert.IsTrue(v >= 0f && v <= 1f);
                }
            CoachFormationProfile low = coaches.First(c => c.Name == "Diego Simeone").AvailableFormations[0], high = coaches.First(c => c.Name == "Hansi Flick").AvailableFormations[0];
            Assert.Less(low.ToTeamStyle().PressTriggerScale, high.ToTeamStyle().PressTriggerScale);
            Assert.Less(low.ToTeamStyle().DefensiveLineHeight, high.ToTeamStyle().DefensiveLineHeight);
        }

        [Test]
        public void TheCoachFile_IsExactlyWhatTheWriterProduces_AndBadDataIsReported()
        {
            string text = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_COACHES_V1.json"));
            Assert.AreEqual(CoachJson.ToJson(coaches).Replace("\r\n", "\n").Trim(), text.Replace("\r\n", "\n").Trim(), "regenerate FS27_COACHES_V1.json when the coach data changes");
            Assert.IsFalse(CoachJson.TryFromJson(text.Replace("\"A+\"", "\"S\""), out _, out string e1)); StringAssert.Contains("grade", e1);
            Assert.IsFalse(CoachJson.TryFromJson(text.Replace("\"Midfield\"", "\"Nowhere\""), out _, out string e2)); StringAssert.Contains("zone", e2);
            Assert.IsFalse(CoachJson.TryFromJson("{ \"schemaVersion\": \"x\", \"coaches\": [] }", out _, out string e3)); StringAssert.Contains("schemaVersion", e3);
            Assert.IsFalse(CoachJson.TryFromJson("not json", out _, out _));
        }

        [Test]
        public void NoCoachOrPlayerNameAppearsInTheCoachCode_AndNothingBranchesOnAName()
        {
            string dir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Coaches");
            var forbidden = new List<string>();
            foreach (CoachDefinition c in coaches) { forbidden.Add(c.Name); forbidden.Add(c.ShortName); foreach (string part in c.FullName.Split(' ')) if (part.Length > 5) forbidden.Add(part); }
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in forbidden.Distinct()) Assert.IsFalse(text.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0, Path.GetFileName(file) + " mentions '" + name + "'");
                Assert.IsFalse(Regex.IsMatch(text, @"\.(Name|FullName|ShortName|CoachId|PlayerId|Id)\s*(==|!=)\s*""") || Regex.IsMatch(text, @"string\.Equals\([^)]*""[A-Za-z ]+""") || text.Contains("case \"coach-"), Path.GetFileName(file) + " branches on a name or an id");
            }
        }

        [Test]
        public void TheCoachSystem_IsIndependentOfThePlayerData_ItOnlyReadsPlayers()
        {
            Type[] coachTypes = typeof(CoachDefinition).Assembly.GetTypes().Where(t => t.Namespace == "FS27.Core" && t.Name.StartsWith("Coach")).ToArray();
            Assert.Greater(coachTypes.Length, 8);
            foreach (Type t in typeof(PlayerDefinition).Assembly.GetTypes().Where(t => t == typeof(PlayerDefinition) || t == typeof(PlayerPlayingProfile) || t == typeof(PlayerCardData)))
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    Assert.IsFalse(f.FieldType.Name.StartsWith("Coach"), t.Name + "." + f.Name + " refers to a coach: the player data must not know about coaches");
        }
    }
}
