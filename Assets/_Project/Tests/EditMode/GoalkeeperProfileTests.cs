using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Fictional test goalkeeper "Nico Valmar GK". Numbers are test data only.</summary>
    internal static class NicoGkData
    {
        public const string Id = "fs27-p-nico-valmar-gk";

        public static PlayerDefinition Player()
        {
            return new PlayerDefinition(Id, "Nico Valmar GK", 1, PlayerRole.Goalkeeper, new PlayerAttributes
            {
                Speed = 68, Acceleration = 64, Agility = 77, Strength = 84, Stamina = 76,
                Shooting = 41, Finishing = 25, Passing = 73,
                Control = 70, Dribbling = 39, Technique = 75,
                Defense = 82, Reaction = 70
            })
            {
                ShortName = "Nico", NationalityCode = "ZQ", Age = 29, HeightCm = 192, WeightKg = 88,
                PreferredFoot = PreferredFoot.Right, WeakFootQuality = 45, BodyType = BodyType.Tall
            };
        }

        public static GoalkeeperProfile Profile()
        {
            return new GoalkeeperProfile(Id, GoalkeeperStyle.ShotStopper, 92, 88, 94, 90, 76, 83, 79, 86,
                GoalkeeperStyle.Commander, GoalkeeperStyle.Sweeper);
        }
    }

    public class GoalkeeperProfileTests
    {
        private static readonly string[] CapabilityNames = { "Reflexes", "Handling", "Positioning", "Diving", "Kicking", "Distribution", "Command", "Recovery" };

        // ================= The profile =================

        [Test]
        public void TheSampleGoalkeeperProfile_IsValid_AlsoAgainstItsPlayer()
        {
            var r = GoalkeeperProfileValidator.Validate(NicoGkData.Profile(), NicoGkData.Player());
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(NicoGkData.Profile()).IsValid);
        }

        [Test]
        public void TheEightCapabilities_AreExactlyTheOnesSpecified()
        {
            CollectionAssert.AreEqual(CapabilityNames, GoalkeeperInfo.Capabilities.Select(c => c.ToString()).ToArray());
            CollectionAssert.AreEquivalent(CapabilityNames, Enum.GetNames(typeof(GoalkeeperCapability)));
            Assert.AreEqual(8, GoalkeeperInfo.CapabilityCount);
        }

        [Test]
        public void TheProfileHoldsTheEightCapabilities_PlusIdAndStylesOnly()
        {
            var names = typeof(GoalkeeperProfile).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            var expected = CapabilityNames.Concat(new[] { "PlayerId", "PrimaryStyle", "SecondaryStyles" }).ToArray();
            CollectionAssert.AreEquivalent(expected, names);
        }

        [Test]
        public void TheSampleValuesAreRead_ByCapability()
        {
            var p = NicoGkData.Profile();
            int[] expected = { 92, 88, 94, 90, 76, 83, 79, 86 };
            for (int i = 0; i < 8; i++) Assert.AreEqual(expected[i], p.GetValue(GoalkeeperInfo.Capabilities[i]), CapabilityNames[i]);
        }

        [Test]
        public void SetValue_AndGetValue_AreConsistent()
        {
            var p = NicoGkData.Profile();
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                p.SetValue(c, 33);
                Assert.AreEqual(33, p.GetValue(c), c.ToString());
            }
        }

        [Test]
        public void EveryCapability_IsOneToNinetyNine([Range(0, 7)] int index)
        {
            var c = GoalkeeperInfo.Capabilities[index];
            foreach (int ok in new[] { 1, 50, 99 })
            {
                var p = NicoGkData.Profile(); p.SetValue(c, ok);
                Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).IsValid, c + "=" + ok);
            }
            foreach (int bad in new[] { 0, -5, 100, 500 })
            {
                var p = NicoGkData.Profile(); p.SetValue(c, bad);
                var r = GoalkeeperProfileValidator.Validate(p);
                Assert.AreEqual(1, r.CountOf(ValidationCode.GoalkeeperCapabilityOutOfRange), c + "=" + bad);
                StringAssert.Contains(c.ToString(), r.Issues[0].Message);
            }
        }

        [Test]
        public void ANullProfile_AnInvalidId_AndAMismatchedOwner_AreReported()
        {
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(null).Has(ValidationCode.GoalkeeperProfileNull));

            var p = NicoGkData.Profile(); p.PlayerId = "has space";
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).Has(ValidationCode.GoalkeeperProfilePlayerIdInvalid));

            p = NicoGkData.Profile(); p.PlayerId = "someone-else";
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p, NicoGkData.Player()).Has(ValidationCode.GoalkeeperProfilePlayerIdMismatch));
        }

        [Test]
        public void StylesMustBeDefined_AndNotRepeated()
        {
            var p = NicoGkData.Profile(); p.PrimaryStyle = (GoalkeeperStyle)42;
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).Has(ValidationCode.GoalkeeperStyleInvalid));

            p = NicoGkData.Profile(); p.SecondaryStyles[0] = (GoalkeeperStyle)42;
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).Has(ValidationCode.GoalkeeperStyleInvalid));

            p = NicoGkData.Profile(); p.SecondaryStyles.Add(GoalkeeperStyle.Commander);
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).Has(ValidationCode.GoalkeeperStyleDuplicate), "listed twice");

            p = NicoGkData.Profile(); p.SecondaryStyles.Add(p.PrimaryStyle);
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(p).Has(ValidationCode.GoalkeeperStyleDuplicate), "same as primary");
        }

        [Test]
        public void AProfileOnAFieldPlayer_IsReported()
        {
            var field = new PlayerDefinition(NicoGkData.Id, "Generic", 9, PlayerRole.Forward, TestData.Attrs(70));
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(NicoGkData.Profile(), field).Has(ValidationCode.GoalkeeperProfileOnFieldPlayer));
        }

        [Test]
        public void AllProblemsAreCollected_NotJustTheFirst()
        {
            var p = NicoGkData.Profile(); p.Reflexes = 0; p.Command = 120; p.PrimaryStyle = (GoalkeeperStyle)9; p.PlayerId = "";
            var r = GoalkeeperProfileValidator.Validate(p);
            Assert.AreEqual(2, r.CountOf(ValidationCode.GoalkeeperCapabilityOutOfRange));
            Assert.IsTrue(r.Has(ValidationCode.GoalkeeperStyleInvalid));
            Assert.IsTrue(r.Has(ValidationCode.GoalkeeperProfilePlayerIdInvalid));
        }

        [Test]
        public void EveryCapabilityAndStyle_HasADisplayName()
        {
            foreach (var c in GoalkeeperInfo.Capabilities) Assert.IsFalse(string.IsNullOrEmpty(GoalkeeperInfo.SpanishName(c)), c.ToString());
            foreach (var s in GoalkeeperInfo.Styles) Assert.IsFalse(string.IsNullOrEmpty(GoalkeeperInfo.SpanishName(s)), s.ToString());
        }

        // ================= No duplication of the player system =================

        [Test]
        public void TheProfileHoldsNoStamina_NoReaction_AndNoCoreAttribute()
        {
            var names = typeof(GoalkeeperProfile).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name.ToLowerInvariant()).ToArray();
            Assert.IsFalse(names.Any(n => n.Contains("stamina")), "stamina stays the player's");
            Assert.IsFalse(names.Any(n => n.Contains("reaction")), "reaction is not a goalkeeper attribute");
            foreach (var attr in PlayerAttributeInfo.All)
                Assert.IsFalse(names.Contains(attr.ToString().ToLowerInvariant()), attr + " stays on PlayerAttributes");
            Assert.IsFalse(names.Contains("ballcontrol"));
        }

        [Test]
        public void TheGoalkeeperKeepsAllTwelveCoreAttributes_OnItsPlayerDefinition()
        {
            var gk = NicoGkData.Player();
            Assert.AreEqual(68, gk.Attributes.Speed);
            Assert.AreEqual(76, gk.Attributes.Stamina, "the one and only stamina");
            Assert.AreEqual(70, gk.Attributes.Control);
            foreach (var id in PlayerAttributeInfo.All)
                Assert.That(gk.Attributes.GetValue(id), Is.InRange(PlayerAttributes.Min, PlayerAttributes.Max), id.ToString());
        }

        [Test]
        public void APlayerDefinition_HoldsNoGoalkeeperData()
        {
            var fields = typeof(PlayerDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsFalse(fields.Any(f => f.FieldType == typeof(GoalkeeperProfile)), "the profile lives apart, linked by id");
            foreach (var f in fields)
                Assert.IsFalse(CapabilityNames.Contains(f.Name), f.Name + " would make a goalkeeper attribute a general one");
            Assert.IsFalse(typeof(PlayerAttributes).GetFields().Any(f => CapabilityNames.Contains(f.Name)), "no goalkeeper attribute among the player attributes");
        }

        [Test]
        public void TheGoalkeeperAttributesAreNotNumberThirteenToTwenty()
        {
            Assert.AreEqual(12, PlayerAttributeInfo.All.Length);
            var attributeFields = typeof(PlayerAttributes).GetFields().Where(f => !f.IsLiteral && !f.IsStatic).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(TestData.AttributeNames, attributeFields, "PlayerAttributes did not grow");
        }

        [Test]
        public void ThereIsNoSecondPlayerDefinition_NoGoalkeeperLibrary_NoGoalkeeperPlayerType()
        {
            var types = typeof(PlayerDefinition).Assembly.GetTypes();
            Assert.AreEqual(1, types.Count(t => t.Name == "PlayerDefinition"));
            Assert.AreEqual(1, types.Count(t => t.Name == "PlayerLibrary"));
            foreach (string banned in new[] { "GoalkeeperLibrary", "GKLibrary", "GKPlayerDefinition", "GoalkeeperPlayerDefinition", "GoalkeeperDefinition", "GKPlayerAttributes", "GoalkeeperAttributes" })
                Assert.IsFalse(types.Any(t => t.Name == banned), banned + " must not exist");
            Assert.IsFalse(types.Any(t => t.Name.EndsWith("Library") && t.Name.Contains("Goalkeeper")));
        }

        [Test]
        public void GoalkeeperStylesAreNotPlayerRoles()
        {
            var roleNames = Enum.GetNames(typeof(PlayerArchetype));
            foreach (var style in Enum.GetNames(typeof(GoalkeeperStyle)))
                CollectionAssert.DoesNotContain(roleNames, style);
            Assert.AreEqual(12, roleNames.Length, "the player roles are still the 12 official ones");
            Assert.IsFalse(typeof(PlayerPlayingProfile).GetFields().Any(f => f.FieldType == typeof(GoalkeeperStyle)));
            Assert.IsFalse(typeof(PlayerPlayingProfile).GetFields().Any(f => f.FieldType == typeof(GoalkeeperProfile)));
        }

        [Test]
        public void TheGoalkeeperStillHasTheGoalZone_AndTheGuardianDefaultRole()
        {
            var gk = NicoGkData.Player();
            var profile = PlayingProfileDefaults.FromPlayer(gk);
            Assert.AreEqual(PitchZone.Goal, profile.PrimaryZone);
            Assert.IsTrue(profile.TryGetPrimaryRole(out var role));
            Assert.AreEqual(PlayerArchetype.Guardian, role);
            Assert.IsTrue(PlayingProfileValidator.Validate(profile, gk).IsValid);
        }

        [Test]
        public void TheFormationsStillPutTheKeeperInSlotZero_InTheGoalZone()
        {
            foreach (var f in DefaultFormations.CreateAll())
            {
                Assert.AreEqual(0, f.GoalkeeperIndex, f.Id);
                Assert.AreEqual(PitchZone.Goal, f.Positions[0].Zone, f.Id);
            }
            Assert.AreEqual(5, Enum.GetValues(typeof(PitchZone)).Length, "no new player zones were added for the keeper");
        }

        [Test]
        public void TheGoalkeeperTypes_AreSerializable_AndHaveNoSprint()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type t in new[] { typeof(GoalkeeperProfile), typeof(GoalkeeperWeights), typeof(GoalkeeperRatingTuning), typeof(GoalkeeperStyleWeightEntry),
                                       typeof(GoalkeeperAreaWeightEntry), typeof(GoalkeeperTuning) })
                Assert.IsTrue(t.IsSerializable, t.Name);
            foreach (Type t in new[] { typeof(GoalkeeperProfile), typeof(GoalkeeperWeights), typeof(GoalkeeperRatingTuning), typeof(GoalkeeperRatingCalculator),
                                       typeof(GoalkeeperSkillModel), typeof(GoalkeeperSkill), typeof(GoalkeeperInfo), typeof(GoalkeeperProfileValidator) })
            {
                Assert.IsFalse(t.Name.ToLowerInvariant().Contains("sprint"), t.Name);
                foreach (MemberInfo m in t.GetMembers(all)) Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name);
            }
        }

        // ================= Identity: the keeper is a player, found in the library =================

        private static PlayerLibrary LibraryWithNico()
        {
            var lib = new PlayerLibrary();
            Assert.IsTrue(lib.TryAdd(NicoGkData.Player()));
            Assert.IsTrue(lib.TryAddGoalkeeperProfile(NicoGkData.Profile()));
            return lib;
        }

        [Test]
        public void TheGoalkeeperIsAPlayerDefinition_AndTheLibraryFindsIt()
        {
            var lib = LibraryWithNico();
            Assert.IsTrue(lib.TryGet(NicoGkData.Id, out PlayerDefinition p));
            Assert.IsInstanceOf<PlayerDefinition>(p);
            Assert.AreEqual(PlayerRole.Goalkeeper, p.Role);
            Assert.AreEqual("Nico Valmar GK", p.DisplayName);
        }

        [Test]
        public void TheLibrary_ReturnsThePlayerAndItsGoalkeeperProfile_TheSameInstances()
        {
            var lib = LibraryWithNico();
            Assert.IsTrue(lib.TryGetGoalkeeper(NicoGkData.Id, out var player, out var profile));
            lib.TryGet(NicoGkData.Id, out var again);
            Assert.AreSame(again, player);
            Assert.IsTrue(lib.TryGetGoalkeeperProfile(NicoGkData.Id, out var direct));
            Assert.AreSame(direct, profile);
            Assert.AreEqual(1, lib.GoalkeeperProfiles.Count);
        }

        [Test]
        public void AFieldPlayer_HasNoGoalkeeperProfile_InTheLibrary()
        {
            var lib = LibraryWithNico();
            lib.TryAdd(TestData.Player("striker", 9, PlayerRole.Forward));
            Assert.IsFalse(lib.TryGetGoalkeeperProfile("striker", out var none));
            Assert.IsNull(none);
            Assert.IsFalse(lib.TryGetGoalkeeper("striker", out _, out _));
            Assert.IsFalse(lib.TryGetGoalkeeperProfile(null, out _));
            Assert.IsFalse(lib.TryGetGoalkeeper("nobody", out _, out _));
        }

        [Test]
        public void TheLibraryRefusesProfilesForUnknownPlayers_Nulls_AndSecondProfiles()
        {
            var lib = LibraryWithNico();
            Assert.IsFalse(lib.TryAddGoalkeeperProfile(null));
            Assert.IsFalse(lib.TryAddGoalkeeperProfile(new GoalkeeperProfile { PlayerId = "ghost" }), "player not in the library");
            Assert.IsFalse(lib.TryAddGoalkeeperProfile(NicoGkData.Profile()), "one profile per player");
            Assert.AreEqual(1, lib.GoalkeeperProfiles.Count);
        }

        [Test]
        public void TheTeamUsesTheKeepersPlayerId_NotACopy()
        {
            var lib = new PlayerLibrary();
            var team = TestData.Team(lib, "blue");
            lib.TryAdd(NicoGkData.Player());
            lib.TryAddGoalkeeperProfile(NicoGkData.Profile());
            team.PlayerIds[0] = NicoGkData.Id;
            Assert.AreEqual(NicoGkData.Id, team.PlayerIds[0]);
            Assert.AreEqual(0, team.GoalkeeperIndex(lib));
            Assert.IsTrue(TeamRoster.TryResolve(team, lib, out var squad));
            lib.TryGet(NicoGkData.Id, out var nico);
            Assert.AreSame(nico, squad[0]);
            Assert.IsTrue(DataValidator.ValidateTeam(team, lib, FormationLibrary.CreateDefault()).IsValid);
        }

        // ================= Team validation =================

        private PlayerLibrary players;
        private FormationLibrary formations;

        [SetUp]
        public void SetUp()
        {
            players = new PlayerLibrary();
            formations = FormationLibrary.CreateDefault();
        }

        private TeamDefinition NewTeam(string prefix = "blue") { return TestData.Team(players, prefix); }

        [Test]
        public void Team_OneGoalkeeperWithProfile_PlusFiveFieldPlayers_IsValid()
        {
            var t = NewTeam();
            var r = DataValidator.ValidateTeam(t, players, formations);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(1, t.PlayerIds.Count(id => players.TryGetGoalkeeperProfile(id, out _)));
            Assert.AreEqual(5, t.PlayerIds.Count(id => !players.TryGetGoalkeeperProfile(id, out _)));
        }

        [Test]
        public void Team_TwoGoalkeepers_IsInvalid()
        {
            var t = NewTeam();
            TestData.At(players, t, 1).Role = PlayerRole.Goalkeeper;
            players.TryAddGoalkeeperProfile(TestData.Keeper(70, t.PlayerIds[1]));
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_ZeroGoalkeepers_IsInvalid()
        {
            var t = NewTeam();
            TestData.At(players, t, 0).Role = PlayerRole.Defender;
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_AGoalkeeperWithoutAProfile_IsInvalid()
        {
            var lib = new PlayerLibrary();
            var t = TestData.Team(lib, "blue");
            var noProfile = new PlayerLibrary();
            foreach (string id in t.PlayerIds) { lib.TryGet(id, out var p); noProfile.TryAdd(p); }
            var r = DataValidator.ValidateTeam(t, noProfile);
            Assert.IsTrue(r.Has(ValidationCode.GoalkeeperProfileMissing));
            StringAssert.Contains("blue-gk", r.Issues.First(i => i.Code == ValidationCode.GoalkeeperProfileMissing).Subject);
        }

        [Test]
        public void Team_AFieldPlayerWithAGoalkeeperProfile_IsInvalid()
        {
            var t = NewTeam();
            players.TryAddGoalkeeperProfile(TestData.Keeper(70, t.PlayerIds[2]));
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.GoalkeeperProfileOnFieldPlayer));
            StringAssert.Contains(t.PlayerIds[2], r.Issues.First(i => i.Code == ValidationCode.GoalkeeperProfileOnFieldPlayer).Subject);
        }

        [Test]
        public void Team_ABrokenGoalkeeperProfile_IsReportedUnderTheTeam()
        {
            var t = NewTeam();
            players.TryGetGoalkeeperProfile(t.PlayerIds[0], out var profile);
            profile.Reflexes = 150;
            var r = DataValidator.ValidateTeam(t, players);
            Assert.AreEqual(1, r.CountOf(ValidationCode.GoalkeeperCapabilityOutOfRange));
            StringAssert.Contains("team 'blue'", r.Issues.First(i => i.Code == ValidationCode.GoalkeeperCapabilityOutOfRange).Subject);
        }

        [Test]
        public void Match_TwoValidSixPlayerTeams_WithTheirKeepers_AreAccepted()
        {
            var r = DataValidator.ValidateMatchTeams(NewTeam("blue"), NewTeam("red"), players, formations);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void ValidateAll_FlagsAKeeperWithoutAProfile_AndABrokenProfile_AndPassesWhenFine()
        {
            var lib = LibraryWithNico();
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(lib, new PlayerPlayingProfile[0]).IsValid);

            lib.TryAdd(TestData.Player("keeper-two", 12, PlayerRole.Goalkeeper));
            var r = PlayerSystemValidator.ValidateAll(lib, new PlayerPlayingProfile[0]);
            Assert.IsTrue(r.Player.Has(ValidationCode.GoalkeeperProfileMissing));

            lib.TryAddGoalkeeperProfile(TestData.Keeper(70, "keeper-two"));
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(lib, new PlayerPlayingProfile[0]).IsValid);

            lib.TryGetGoalkeeperProfile("keeper-two", out var broken);
            broken.Diving = 0;
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(lib, new PlayerPlayingProfile[0]).Player.Has(ValidationCode.GoalkeeperCapabilityOutOfRange));
        }

        [Test]
        public void ValidateAll_FlagsAProfileOnAFieldPlayer()
        {
            var lib = LibraryWithNico();
            lib.TryAdd(TestData.Player("striker", 9, PlayerRole.Forward));
            lib.TryAddGoalkeeperProfile(TestData.Keeper(70, "striker"));
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(lib, new PlayerPlayingProfile[0]).Player.Has(ValidationCode.GoalkeeperProfileOnFieldPlayer));
        }

        [Test]
        public void TheTeamRuleIsStillSixPlayers_OneKeeper_FiveFieldPlayers()
        {
            Assert.AreEqual(6, DataRules.PlayersPerTeam);
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
            var t = NewTeam();
            t.PlayerIds.RemoveAt(5);
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamPlayerCountInvalid));
        }
    }
}
