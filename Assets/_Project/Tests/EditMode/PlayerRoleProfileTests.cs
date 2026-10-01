using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Fictional test data only. "Sak" is an invented character, not a real player.</summary>
    internal static class SakData
    {
        public const string Id = "fs27-p-sak";

        public static PlayerDefinition Player()
        {
            return new PlayerDefinition(Id, "Sak", 7, PlayerRole.Forward, new PlayerAttributes
            {
                Speed = 94, Acceleration = 96, Agility = 92, Strength = 70, Stamina = 88,
                Shooting = 82, Finishing = 84, Passing = 79,
                Control = 91, Dribbling = 94, Technique = 89,
                Defense = 42, Reaction = 80
            })
            {
                ShortName = "Sak", NationalityCode = "ZQ", TeamId = "", Age = 24, HeightCm = 176, WeightKg = 68,
                PreferredFoot = PreferredFoot.Right, WeakFootQuality = 68, BodyType = BodyType.Light
            };
        }

        public static PlayerPlayingProfile Profile()
        {
            return new PlayerPlayingProfile(Id, PitchZone.Wing, new[] { PitchZone.Attack, PitchZone.Midfield },
                    new[] { new RoleAffinity(PlayerArchetype.Explosive, 95), new RoleAffinity(PlayerArchetype.Creator, 84), new RoleAffinity(PlayerArchetype.Finisher, 82) })
                .WithBehaviour(75, 82, 55);
        }
    }

    public class PlayerRoleProfileTests
    {
        private static PlayerPlayingProfile Valid()
        {
            return SakData.Profile();
        }

        private static AiDataValidationResult Check(PlayerPlayingProfile p, PlayerDefinition player = null)
        {
            return PlayingProfileValidator.Validate(p, player);
        }

        // ================= Role set =================

        [Test]
        public void TheOfficialRoleSet_HasElevenRoles_InFourGroups()
        {
            Assert.AreEqual(11, RoleInfo.OfficialRoles.Length);
            Assert.AreEqual(11, RoleInfo.OfficialRoles.Distinct().Count());
            Assert.AreEqual(2, RoleInfo.OfficialRoles.Count(r => RoleInfo.GroupOf(r) == RoleGroup.Defense));
            Assert.AreEqual(3, RoleInfo.OfficialRoles.Count(r => RoleInfo.GroupOf(r) == RoleGroup.Creation));
            Assert.AreEqual(3, RoleInfo.OfficialRoles.Count(r => RoleInfo.GroupOf(r) == RoleGroup.Mobility));
            Assert.AreEqual(3, RoleInfo.OfficialRoles.Count(r => RoleInfo.GroupOf(r) == RoleGroup.Attack));
        }

        [Test]
        public void TheOfficialRoles_HaveTheirSpanishNames()
        {
            CollectionAssert.AreEqual(
                new[] { "Guardián", "Muro", "Constructor", "Creador", "Arquitecto", "Motor", "Ala", "Explosivo", "Finalizador", "Cazagoles", "Objetivo" },
                RoleInfo.OfficialRoles.Select(RoleInfo.SpanishName).ToArray());
        }

        [Test]
        public void ExistingRoleNamesAndNumbers_WereKept_NotRenamed()
        {
            var expected = new System.Collections.Generic.Dictionary<string, int>
            {
                { "Explosive", 0 }, { "Creator", 1 }, { "Finisher", 2 }, { "Destroyer", 3 }, { "Anchor", 4 }, { "Engine", 5 }, { "ShotStopper", 6 }, { "Sweeper", 7 }
            };
            foreach (var kv in expected) Assert.AreEqual(kv.Value, (int)Enum.Parse(typeof(PlayerArchetype), kv.Key), kv.Key);
        }

        [Test]
        public void EveryRole_HasAGroupAndADisplayName()
        {
            foreach (PlayerArchetype r in Enum.GetValues(typeof(PlayerArchetype)))
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RoleGroup), RoleInfo.GroupOf(r)), r.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(RoleInfo.SpanishName(r)), r.ToString());
            }
            Assert.IsFalse(RoleInfo.IsOfficial(PlayerArchetype.ShotStopper));
            Assert.IsTrue(RoleInfo.IsOfficial(PlayerArchetype.Explosive));
        }

        [Test]
        public void ARoleIsData_NotCode_AnyPlayerCanHoldAnyRole()
        {
            foreach (var r in RoleInfo.OfficialRoles)
            {
                var p = new PlayerPlayingProfile("p-any", PitchZone.Midfield, null, new[] { new RoleAffinity(r, 70) });
                Assert.IsTrue(Check(p).IsValid, r.ToString());
            }
        }

        // ================= Up to three roles, each with an affinity =================

        [Test]
        public void AProfileHoldsAtMostThreeRoles()
        {
            var p = Valid();
            Assert.IsTrue(Check(p).IsValid);
            p.AddRole(PlayerArchetype.Engine, 60);
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileArchetypeCountInvalid));
        }

        [Test]
        public void AProfileNeedsAtLeastOneRole()
        {
            var p = new PlayerPlayingProfile("p-x", PitchZone.Attack, null, new RoleAffinity[0]);
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileArchetypeCountInvalid));
        }

        [Test]
        public void InvalidOrRepeatedRoles_AreReported()
        {
            var p = Valid(); p.Archetypes[1] = PlayerArchetype.Explosive;
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileArchetypeDuplicate));
            p = Valid(); p.Archetypes[0] = (PlayerArchetype)77;
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileArchetypeInvalid));
        }

        [Test]
        public void Affinities_AreStoredPerRole_AndReadBack()
        {
            var p = Valid();
            Assert.AreEqual(95, p.GetAffinity(PlayerArchetype.Explosive));
            Assert.AreEqual(84, p.GetAffinity(PlayerArchetype.Creator));
            Assert.AreEqual(82, p.GetAffinity(PlayerArchetype.Finisher));
            Assert.AreEqual(0, p.GetAffinity(PlayerArchetype.Wall), "a role the player does not have");
        }

        [TestCase(0, true)]
        [TestCase(50, true)]
        [TestCase(100, true)]
        [TestCase(-1, false)]
        [TestCase(101, false)]
        public void Affinity_IsZeroToOneHundred(int value, bool valid)
        {
            var p = Valid(); p.ArchetypeAffinities[0] = value;
            Assert.AreEqual(valid, !Check(p).Has(AiDataIssueCode.ProfileAffinityOutOfRange), value.ToString());
        }

        [Test]
        public void AffinityCount_MustMatchTheRoles_OrBeEmpty()
        {
            var p = Valid(); p.ArchetypeAffinities.RemoveAt(2);
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileAffinityCountMismatch));
            p.ArchetypeAffinities.Clear();
            Assert.IsTrue(Check(p).IsValid, "empty means: derive from the rank");
        }

        [Test]
        public void WithoutStatedAffinities_TheyFollowTheRank()
        {
            var p = new PlayerPlayingProfile("p-r", PitchZone.Midfield, null, PlayerArchetype.Builder, PlayerArchetype.Engine, PlayerArchetype.Guardian);
            Assert.AreEqual(90, p.GetAffinity(PlayerArchetype.Builder));
            Assert.AreEqual(75, p.GetAffinity(PlayerArchetype.Engine));
            Assert.AreEqual(60, p.GetAffinity(PlayerArchetype.Guardian));
            Assert.IsTrue(Check(p).IsValid);
        }

        [Test]
        public void AddRole_KeepsEarlierDerivedAffinities_WhenAnExplicitOneIsAdded()
        {
            var p = new PlayerPlayingProfile("p-r", PitchZone.Midfield, null, PlayerArchetype.Builder);
            p.AddRole(PlayerArchetype.Engine, 55);
            Assert.AreEqual(90, p.GetAffinity(PlayerArchetype.Builder));
            Assert.AreEqual(55, p.GetAffinity(PlayerArchetype.Engine));
            Assert.IsTrue(Check(p).IsValid);
        }

        [Test]
        public void ThePrimaryRole_IsTheOneWithTheHighestAffinity()
        {
            Assert.IsTrue(Valid().TryGetPrimaryRole(out var role));
            Assert.AreEqual(PlayerArchetype.Explosive, role);

            var p = Valid(); p.ArchetypeAffinities[2] = 99;
            p.TryGetPrimaryRole(out role);
            Assert.AreEqual(PlayerArchetype.Finisher, role);

            var tie = new PlayerPlayingProfile("p-t", PitchZone.Attack, null, new[] { new RoleAffinity(PlayerArchetype.Target, 80), new RoleAffinity(PlayerArchetype.Finisher, 80) });
            tie.TryGetPrimaryRole(out role);
            Assert.AreEqual(PlayerArchetype.Target, role, "first listed wins a tie");

            Assert.IsFalse(new PlayerPlayingProfile().TryGetPrimaryRole(out _));
        }

        [Test]
        public void GetRoleAffinities_ReturnsRolesWithTheirAffinity_InOrder()
        {
            var list = Valid().GetRoleAffinities();
            Assert.AreEqual(3, list.Length);
            Assert.AreEqual(PlayerArchetype.Explosive, list[0].Role);
            Assert.AreEqual(95, list[0].Affinity);
            Assert.AreEqual(82, list[2].Affinity);
        }

        // ================= Zones =================

        [Test]
        public void ThereAreFiveZones()
        {
            CollectionAssert.AreEquivalent(new[] { "Goal", "Defense", "Midfield", "Wing", "Attack" }, Enum.GetNames(typeof(PitchZone)));
        }

        [Test]
        public void APlayerHasOnePrimaryZone_AndSeveralSecondaryOnes()
        {
            var p = Valid();
            Assert.AreEqual(PitchZone.Wing, p.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Attack, PitchZone.Midfield }, p.SecondaryZones);
            p.SecondaryZones.Add(PitchZone.Defense);
            Assert.IsTrue(Check(p).IsValid, "three secondary zones are fine");
            Assert.IsTrue(p.PlaysIn(PitchZone.Defense));
        }

        [Test]
        public void InvalidDuplicateOrPrimaryEchoZones_AreReported()
        {
            var p = Valid(); p.PrimaryZone = (PitchZone)40;
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileZoneInvalid));
            p = Valid(); p.SecondaryZones.Add(PitchZone.Attack);
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileSecondaryZoneDuplicate));
            p = Valid(); p.SecondaryZones.Add(PitchZone.Wing);
            Assert.IsTrue(Check(p).Has(AiDataIssueCode.ProfileSecondaryEqualsPrimary));
        }

        [Test]
        public void ZonesAreEditableData()
        {
            var p = Valid();
            p.PrimaryZone = PitchZone.Attack;
            p.SecondaryZones.Clear(); p.SecondaryZones.Add(PitchZone.Wing);
            Assert.IsTrue(Check(p).IsValid);
            Assert.AreEqual(PitchZone.Attack, p.PrimaryZone);
        }

        // ================= Behaviour profile =================

        [Test]
        public void BehaviourProfile_HasNeutralDefaults_AndHoldsTheSakValues()
        {
            var fresh = new PlayerPlayingProfile();
            Assert.AreEqual(50, fresh.RiskPreference);
            Assert.AreEqual(50, fresh.Creativity);
            Assert.AreEqual(50, fresh.Aggression);

            var p = Valid();
            Assert.AreEqual(75, p.RiskPreference);
            Assert.AreEqual(82, p.Creativity);
            Assert.AreEqual(55, p.Aggression);
        }

        [TestCase(0, true)]
        [TestCase(100, true)]
        [TestCase(-1, false)]
        [TestCase(101, false)]
        public void EachBehaviourValue_IsZeroToOneHundred(int value, bool valid)
        {
            var a = Valid(); a.RiskPreference = value;
            var b = Valid(); b.Creativity = value;
            var c = Valid(); c.Aggression = value;
            foreach (var p in new[] { a, b, c })
                Assert.AreEqual(valid, !Check(p).Has(AiDataIssueCode.ProfileBehaviourOutOfRange), value.ToString());
            Assert.AreEqual(valid ? 0 : 1, Check(a).CountOf(AiDataIssueCode.ProfileBehaviourOutOfRange));
        }

        [Test]
        public void Behaviour_IsNotAnAttribute_AndNotPartOfThePlayerDefinition()
        {
            foreach (Type t in new[] { typeof(PlayerAttributes), typeof(PlayerDefinition) })
            {
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name.ToLowerInvariant()).ToArray();
                Assert.IsFalse(fields.Any(n => n.Contains("risk") || n.Contains("creativity") || n.Contains("aggression")), t.Name);
            }
        }

        [Test]
        public void Behaviour_IsEditable_WithoutChangingAnythingElse()
        {
            var p = Valid();
            p.RiskPreference = 10; p.Creativity = 20; p.Aggression = 30;
            Assert.IsTrue(Check(p).IsValid);
            Assert.AreEqual(PitchZone.Wing, p.PrimaryZone);
            Assert.AreEqual(95, p.GetAffinity(PlayerArchetype.Explosive));
        }

        // ================= Link with the player =================

        [Test]
        public void TheProfile_MustBelongToThePlayerItIsCheckedAgainst()
        {
            Assert.IsTrue(Check(Valid(), SakData.Player()).IsValid);
            var wrong = Valid(); wrong.PlayerId = "someone-else";
            Assert.IsTrue(Check(wrong, SakData.Player()).Has(AiDataIssueCode.ProfilePlayerIdMismatch));
        }

        [Test]
        public void TheProfile_HoldsNoAttributes_SoThereIsNoSecondCopy()
        {
            foreach (var f in typeof(PlayerPlayingProfile).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsFalse(f.FieldType == typeof(PlayerAttributes), f.Name);
            var names = typeof(PlayerPlayingProfile).GetFields().Select(f => f.Name.ToLowerInvariant()).ToArray();
            foreach (var attr in PlayerAttributeInfo.All)
                CollectionAssert.DoesNotContain(names, attr.ToString().ToLowerInvariant());
        }

        [Test]
        public void ExistingDefaultsFromTheCoarseRole_StillWork()
        {
            var player = new PlayerDefinition("p-def", "Generic", 4, PlayerRole.Defender, TestData.Attrs(60));
            var profile = PlayingProfileDefaults.FromPlayer(player);
            Assert.AreEqual(PitchZone.Defense, profile.PrimaryZone);
            Assert.IsTrue(Check(profile, player).IsValid);
            Assert.AreEqual(90, profile.GetAffinity(PlayerArchetype.Destroyer));
        }
    }
}
