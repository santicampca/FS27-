using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>System-level guarantees of Player System V2: one source of truth, no duplicates, no economy, no real names, no gameplay in the card.</summary>
    public class PlayerSystemGuardTests
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static IEnumerable<Type> CoreTypes()
        {
            return typeof(PlayerDefinition).Assembly.GetTypes()
                .Where(t => !t.FullName.StartsWith("FS27.Core.Tests") && !t.IsNested && !t.Name.StartsWith("<"));
        }

        // The types introduced or extended by Player System V2.
        private static readonly Type[] PlayerSystemTypes =
        {
            typeof(PlayerAttributeId), typeof(PlayerAttributeGroup), typeof(PlayerAttributeInfo), typeof(PreferredFoot), typeof(BodyType), typeof(CardType),
            typeof(PlayerRules), typeof(AttributeWeights), typeof(RoleWeightEntry), typeof(ZoneWeightEntry), typeof(RatingTuning), typeof(RatingTuningValidator),
            typeof(DefaultRatingTuning), typeof(PlayerRatingCalculator), typeof(RoleInfo), typeof(RoleGroup), typeof(RoleAffinity), typeof(ZoneDeclaration),
            typeof(ZoneSuitability), typeof(PlayerCardData), typeof(PlayerCardCatalog), typeof(PlayerCardBuilder), typeof(PlayerLibrary), typeof(TeamRoster),
            typeof(PlayerSystemReport), typeof(PlayerSystemValidator)
        };

        // ================= One source of truth =================

        [Test]
        public void OnlyPlayerDefinition_StoresAPlayersAttributes()
        {
            var holders = CoreTypes().Where(t => t.GetFields(All).Any(f => f.FieldType == typeof(PlayerAttributes))).Select(t => t.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "PlayerDefinition" }, holders);
        }

        [Test]
        public void NoOtherClass_HasAnIntegerFieldNamedLikeAnAttribute()
        {
            var names = new HashSet<string>(PlayerAttributeInfo.All.Select(a => a.ToString()).Concat(new[] { "BallControl", "Reaction" }));
            foreach (Type t in CoreTypes().Where(t => t != typeof(PlayerAttributes)))
                foreach (FieldInfo f in t.GetFields(All).Where(f => f.FieldType == typeof(int) && !f.IsLiteral))
                    Assert.IsFalse(names.Contains(f.Name), t.Name + "." + f.Name + " would be a second copy of an attribute");
        }

        [Test]
        public void ThereIsOnePlayerDefinitionAndOnePlayingProfile_NoParallelPlayerSystem()
        {
            var playerLike = CoreTypes().Where(t => t.IsClass || t.IsValueType && !t.IsEnum)
                .Where(t => t.Name.StartsWith("Player")).Select(t => t.Name).OrderBy(n => n).ToArray();
            // Every "Player*" type has a distinct role; none of these is a rival definition of a player.
            foreach (string forbidden in new[] { "PlayerData", "PlayerInfo", "PlayerProfile", "PlayerV2", "PlayerDefinitionV2", "PlayerRecord", "Player" })
                CollectionAssert.DoesNotContain(playerLike, forbidden);
            Assert.AreEqual(1, playerLike.Count(n => n == "PlayerDefinition"));
            Assert.AreEqual(1, playerLike.Count(n => n == "PlayerPlayingProfile"));
        }

        [Test]
        public void TheStaminaAttribute_ExistsOnce_AndRuntimeStaminaIsStateNotAnAttribute()
        {
            Assert.AreEqual(1, typeof(PlayerAttributes).GetFields().Count(f => f.Name == "Stamina"));
            var runtime = typeof(PlayerRuntimeState).GetField("Stamina");
            Assert.AreEqual(typeof(float), runtime.FieldType, "the movement system's current stamina is a float state, not an attribute");
            foreach (Type t in PlayerSystemTypes.Where(t => !t.IsEnum))
                Assert.IsFalse(t.GetFields(All).Any(f => f.FieldType == typeof(int) && f.Name.ToLowerInvariant().Contains("stamina")), t.Name);
        }

        [Test]
        public void MovementAndStamina_RemainTheExistingOnes_FedByThePlayersAttributes()
        {
            var tuning = new MovementTuning();
            var weak = SakData.Player(); weak.Attributes = weak.Attributes.With(PlayerAttributeId.Stamina, 30);
            var strong = SakData.Player(); strong.Attributes = strong.Attributes.With(PlayerAttributeId.Stamina, 95);
            var a = PlayerStats.Resolve(weak.Attributes, tuning);
            var b = PlayerStats.Resolve(strong.Attributes, tuning);
            Assert.Greater(b.StaminaCapacity, a.StaminaCapacity);

            var state = new PlayerRuntimeState();
            state.Reset(b, 0f);
            for (int i = 0; i < 60; i++) PlayerLocomotion.Step(state, b, tuning, new PlayerIntent(new Vec2(0f, 1f)), 1f / 60f);
            Assert.Greater(state.Speed, 0f, "the existing locomotion still runs with a V2 player");
        }

        // ================= Difficulty stays out of the player =================

        [Test]
        public void OnlyTheExecutionMethods_OfTheCalculator_MentionDifficulty()
        {
            foreach (MethodInfo m in typeof(PlayerRatingCalculator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                bool takesDifficulty = m.GetParameters().Any(p => p.ParameterType == typeof(DifficultyDefinition));
                Assert.AreEqual(m.Name.EndsWith("Execution01"), takesDifficulty, m.Name);
            }
        }

        [Test]
        public void NoDifficultyOrAiConfigurationType_KnowsThePlayerDefinitionOrTheCard()
        {
            Type[] config = { typeof(DifficultyDefinition), typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters),
                              typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters), typeof(DefaultDifficulties),
                              typeof(DifficultyLibrary), typeof(DifficultyValidator), typeof(TeamStyleDefinition) };
            Type[] player = { typeof(PlayerDefinition), typeof(PlayerPlayingProfile), typeof(PlayerCardData), typeof(PlayerLibrary), typeof(PlayerRatingCalculator) };
            foreach (Type t in config)
            {
                var used = t.GetFields(All).Select(f => f.FieldType)
                    .Concat(t.GetMethods(All).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Concat(new[] { m.ReturnType })))
                    .Select(x => x.IsByRef ? x.GetElementType() : x);
                foreach (Type u in used) CollectionAssert.DoesNotContain(player, u, t.Name + " -> " + u.Name);
            }
        }

        [Test]
        public void TheExampleOverallAndRatings_AreTheSameWhateverTheDifficultyLibraryHolds()
        {
            var calc = PlayerRatingCalculator.CreateDefault();
            var sak = SakData.Player(); var prof = SakData.Profile();
            int overall = calc.GetOverall(sak, prof);
            var lib = DifficultyLibrary.CreateDefault();
            foreach (var d in lib.All)
            {
                d.Decision.RoleDiscipline = 0.01f; d.Reaction.BaseReactionSeconds = 1.9f; d.Execution.ExecutionQuality = 0f;
            }
            Assert.AreEqual(overall, calc.GetOverall(sak, prof));
            Assert.AreEqual(92, sak.Attributes.Agility, "attributes are what they were");
        }

        // ================= Offline, no economy, no accounts =================

        [Test]
        public void NoPlayerSystemType_MentionsEconomyAccountsOrNetworking()
        {
            string[] banned = { "market", "pack", "coin", "currency", "price", "store", "shop", "purchase", "account", "online", "server", "backend", "monetiz",
                                "wallet", "transfer", "ultimate", "auction", "login", "network", "http" };
            foreach (Type t in PlayerSystemTypes.Concat(new[] { typeof(PlayerPlayingProfile), typeof(PlayerDefinition), typeof(PlayerAttributes) }))
            {
                Assert.IsFalse(banned.Any(b => t.Name.ToLowerInvariant().Contains(b)), t.Name);
                foreach (MemberInfo m in t.GetMembers(All))
                    Assert.IsFalse(banned.Any(b => m.Name.ToLowerInvariant().Contains(b)), t.Name + "." + m.Name);
            }
        }

        [Test]
        public void CoreReferencesNoNetworkIoOrUnityAssemblies()
        {
            var refs = typeof(PlayerDefinition).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            foreach (string r in refs)
            {
                Assert.IsFalse(r.StartsWith("UnityEngine") || r.StartsWith("UnityEditor"), r);
                Assert.IsFalse(r.Contains("Http") || r.Contains("Sockets") || r.Contains("WebClient") || r.Contains("Net.Primitives"), r);
            }
        }

        // ================= Sprint stays a joystick-intensity matter =================

        [Test]
        public void NoPlayerSystemType_IntroducesASprintConcept()
        {
            foreach (Type t in PlayerSystemTypes.Concat(new[] { typeof(PlayerPlayingProfile), typeof(PlayerDefinition), typeof(PlayerAttributes) }))
            {
                Assert.IsFalse(t.Name.ToLowerInvariant().Contains("sprint"), t.Name);
                foreach (MemberInfo m in t.GetMembers(All)) Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name);
            }
            Assert.IsFalse(typeof(PlayerIntent).GetFields().Any(f => f.Name.ToLowerInvariant().Contains("sprint")), "PlayerIntent is untouched");
        }

        // ================= Fictional universe =================

        [Test]
        public void TheSampleData_ContainsNoRealWorldNames()
        {
            var sak = SakData.Player();
            var texts = new List<string> { sak.Id, sak.Name, sak.ShortName, sak.NationalityCode, SakData.Profile().PlayerId };
            texts.AddRange(Enum.GetNames(typeof(PlayerArchetype)));
            texts.AddRange(RoleInfo.OfficialRoles.Select(RoleInfo.SpanishName));
            texts.AddRange(Enum.GetNames(typeof(CardType)));
            texts.AddRange(Enum.GetNames(typeof(BodyType)));
            DifficultyGuardTests.AssertClean(string.Join("\n", texts), "Player System sample data and enums");
        }

        // ================= Prepared for the future presentation system =================

        [Test]
        public void EverythingAFuturePresentationNeeds_IsReadableFromTheDataAlone()
        {
            var sak = SakData.Player();
            var team = new TeamDefinition("team-cobalt", "Cobalt Test Team", new TeamColors(new ColorRgb(10, 40, 200), new ColorRgb(255, 140, 0)), DefaultFormations.TwoOneTwo, sak.Id);
            // body, number and kit colours come from data; nothing visual is stored or written back.
            Assert.AreEqual(BodyType.Light, sak.BodyType);
            Assert.AreEqual(176, sak.HeightCm);
            Assert.AreEqual(68, sak.WeightKg);
            Assert.AreEqual(7, sak.ShirtNumber);
            Assert.AreEqual(10, team.Colors.Primary.R);
            foreach (var f in typeof(PlayerDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsFalse(new[] { "skin", "hair", "face", "boots", "kit", "model", "mesh", "prefab", "texture" }.Any(w => f.Name.ToLowerInvariant().Contains(w)),
                    "visual choices are not stored in the player: " + f.Name);
        }

        // ================= Serializable data =================

        [Test]
        public void TheEditableDataTypes_AreSerializable()
        {
            foreach (Type t in new[] { typeof(PlayerDefinition), typeof(PlayerAttributes), typeof(PlayerPlayingProfile), typeof(RoleAffinity), typeof(AttributeWeights),
                                       typeof(RoleWeightEntry), typeof(ZoneWeightEntry), typeof(RatingTuning) })
                Assert.IsTrue(t.IsSerializable, t.Name);
        }

        // ================= End to end =================

        [Test]
        public void EndToEnd_LibraryProfilesTeamsAndCards_FollowOneSourceOfTruth()
        {
            var formations = FormationLibrary.CreateDefault();
            var calc = PlayerRatingCalculator.CreateDefault();
            var library = new PlayerLibrary();
            var profiles = new PlayingProfileCatalog();
            var cardTypes = new PlayerCardCatalog();

            var sak = SakData.Player(); sak.TeamId = "cobalt"; sak.Role = PlayerRole.Forward;
            library.TryAdd(sak);
            profiles.TryAdd(SakData.Profile());
            library.TryAdd(TestData.Player("cobalt-gk", 1, PlayerRole.Goalkeeper));
            library.TryAddGoalkeeperProfile(TestData.Keeper(70, "cobalt-gk"));
            library.TryAdd(TestData.Player("cobalt-d1", 2, PlayerRole.Defender));
            library.TryAdd(TestData.Player("cobalt-d2", 3, PlayerRole.Defender));
            library.TryAdd(TestData.Player("cobalt-m1", 8, PlayerRole.Midfielder));
            library.TryAdd(TestData.Player("cobalt-f1", 9, PlayerRole.Forward));
            foreach (var p in library.All) library.TryAssignToTeam(p.Id, "cobalt");

            Assert.IsTrue(TeamRoster.TryBuild("cobalt", "Cobalt Test Team", new TeamColors(), DefaultFormations.TwoOneTwo,
                new[] { "cobalt-gk", "cobalt-d1", "cobalt-d2", "cobalt-m1", "cobalt-f1", "fs27-p-sak" }, library, out var team, out var problem), problem);
            Assert.IsTrue(DataValidator.ValidateTeam(team, library, formations).IsValid, DataValidator.ValidateTeam(team, library, formations).ToString());
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(library, new[] { SakData.Profile() }).IsValid);

            cardTypes.Set(sak.Id, CardType.Rare);
            var builder = new PlayerCardBuilder(calc, profiles, cardTypes, id => id == "cobalt" ? team : null);
            var card = builder.Build(sak);
            Assert.AreEqual("Cobalt Test Team", card.TeamName);
            Assert.AreEqual(CardType.Rare, card.CardType);

            int overall = card.Overall;
            library.TryGet("fs27-p-sak", out var same);
            Assert.AreSame(sak, same);
            Assert.IsTrue(TeamRoster.TryResolve(team, library, out var squad));
            Assert.AreSame(sak, squad[5], "the team (by id), the library and the card all point at the one definition");

            // One edit, seen everywhere.
            same.Attributes = same.Attributes.With(PlayerAttributeId.Speed, 99).With(PlayerAttributeId.Acceleration, 99).With(PlayerAttributeId.Finishing, 99);
            Assert.AreEqual(99, card.GetAttribute(PlayerAttributeId.Speed));
            Assert.AreEqual(99, squad[5].Attributes.Speed);
            Assert.GreaterOrEqual(card.Overall, overall);
            Assert.AreEqual(calc.GetOverall(sak, profiles.GetOrDefault(sak)), card.Overall);
        }

        // ================= 6v6 foundation =================

        [Test]
        public void TeamSizeLivesInOnePlace_AndIsSixVersusSix()
        {
            Assert.AreEqual(6, DataRules.PlayersPerTeam);
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
        }

        [Test]
        public void CoreSources_HoldNoFivePlayerTeamRule()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            string[] banned = { "5v5", "exactly 5", "1 goalkeeper + 4", "4 outfield", "five players", "int PlayersPerTeam = 5" };
            foreach (string file in System.IO.Directory.GetFiles(core, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string text = System.IO.File.ReadAllText(file);
                foreach (string phrase in banned)
                    Assert.IsFalse(text.Contains(phrase), System.IO.Path.GetFileName(file) + " still says '" + phrase + "'");
            }
        }

        [Test]
        public void Reaction_IsNotOneOfTheTwelveAttributes_AndNeverEntersRatingsOrTheCard()
        {
            Assert.AreEqual(12, PlayerAttributeInfo.All.Length);
            Assert.IsFalse(PlayerAttributeInfo.All.Select(a => a.ToString()).Contains("Reaction"));

            var calc = PlayerRatingCalculator.CreateDefault();
            var low = SakData.Player(); low.Attributes.Reaction = 1;
            var high = SakData.Player(); high.Attributes.Reaction = 99;
            var profile = SakData.Profile();
            Assert.AreEqual(calc.GetOverall(low, profile), calc.GetOverall(high, profile));
            foreach (PlayerArchetype role in RoleInfo.OfficialRoles)
                Assert.AreEqual(calc.GetRoleRating(low, role), calc.GetRoleRating(high, role), role.ToString());
            foreach (PitchZone zone in Enum.GetValues(typeof(PitchZone)))
                Assert.AreEqual(calc.GetZoneRating(low, zone), calc.GetZoneRating(high, zone), zone.ToString());
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                Assert.AreNotEqual("Reaction", id.ToString());
        }

        [Test]
        public void TheTeamRule_IsEnforcedAgainstTheLibrary_NotAgainstEmbeddedPlayers()
        {
            // A valid 6v6 team needs its players to exist in the library: ids alone are not enough.
            var lib = new PlayerLibrary();
            var team = TestData.Team(lib, "blue");
            Assert.IsTrue(DataValidator.ValidateTeam(team, lib, FormationLibrary.CreateDefault()).IsValid);
            Assert.IsTrue(DataValidator.ValidateTeam(team, new PlayerLibrary()).Has(ValidationCode.TeamPlayerNotFound));
        }
    }
}
