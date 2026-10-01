using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PlayerCardTests
    {
        private PlayerRatingCalculator calc;
        private PlayerDefinition sak;
        private PlayerPlayingProfile profile;
        private PlayingProfileCatalog profiles;
        private PlayerCardCatalog cardTypes;
        private TeamDefinition team;
        private PlayerCardBuilder builder;

        [SetUp]
        public void SetUp()
        {
            calc = PlayerRatingCalculator.CreateDefault();
            sak = SakData.Player();
            sak.TeamId = "team-cobalt";
            profile = SakData.Profile();
            profiles = new PlayingProfileCatalog();
            profiles.TryAdd(profile);
            cardTypes = new PlayerCardCatalog();
            team = new TeamDefinition("team-cobalt", "Cobalt Test Team", new TeamColors(new ColorRgb(10, 40, 200), new ColorRgb(255, 140, 0)), DefaultFormations.TwoTwo, sak);
            builder = new PlayerCardBuilder(calc, profiles, cardTypes, id => id == "team-cobalt" ? team : null);
        }

        // ================= The card shows everything it should =================

        [Test]
        public void TheCard_ShowsTheWholePlayerFromTheDefinition()
        {
            var card = builder.Build(sak);
            Assert.AreEqual("fs27-p-sak", card.PlayerId);
            Assert.AreEqual("Sak", card.DisplayName);
            Assert.AreEqual("Sak", card.ShortName);
            Assert.AreEqual(7, card.ShirtNumber);
            Assert.AreEqual("ZQ", card.NationalityCode);
            Assert.AreEqual("team-cobalt", card.TeamId);
            Assert.AreEqual("Cobalt Test Team", card.TeamName);
            Assert.AreEqual(PitchZone.Wing, card.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Attack, PitchZone.Midfield }, card.SecondaryZones);
            Assert.AreEqual(CardType.Standard, card.CardType);
            Assert.AreEqual(PreferredFoot.Right, card.PreferredFoot);
            Assert.AreEqual(68, card.WeakFootQuality);
            Assert.AreEqual(BodyType.Light, card.BodyType);
            Assert.AreEqual(176, card.HeightCm);
            Assert.AreEqual(68, card.WeightKg);
            Assert.AreEqual(24, card.Age);
        }

        [Test]
        public void TheOverall_IsTheCalculatorsOverall()
        {
            var card = builder.Build(sak);
            Assert.AreEqual(calc.GetOverall(sak, profile), card.Overall);
            Assert.That(card.Overall, Is.InRange(1, 99));
        }

        [Test]
        public void TheCard_ShowsAllTwelveAttributes_WithTheirCurrentValues()
        {
            var card = builder.Build(sak);
            foreach (var id in PlayerAttributeInfo.All)
                Assert.AreEqual(sak.Attributes.GetValue(id), card.GetAttribute(id), id.ToString());
            Assert.AreEqual(94, card.GetAttribute(PlayerAttributeId.Speed));
            Assert.AreEqual(91, card.GetAttribute(PlayerAttributeId.Control));
            Assert.AreEqual(sak.Attributes, card.Attributes);
        }

        [Test]
        public void TheCard_ShowsThePrimaryRoleAndStyle_AndAllRolesWithAffinity()
        {
            var card = builder.Build(sak);
            Assert.IsTrue(card.TryGetPrimaryRole(out var role));
            Assert.AreEqual(PlayerArchetype.Explosive, role);
            Assert.AreEqual("Explosivo", card.StyleLabel);
            var roles = card.GetRoles();
            CollectionAssert.AreEqual(
                new[] { PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher },
                roles.Select(r => r.Role).ToArray());
            CollectionAssert.AreEqual(new[] { 95, 84, 82 }, roles.Select(r => r.Affinity).ToArray());
        }

        [Test]
        public void ThePolyvalencePanel_ListsEveryZone_BestFirst_WithHowTheyAreDeclared()
        {
            var card = builder.Build(sak);
            var panel = card.GetPolyvalence();
            Assert.AreEqual(5, panel.Length);
            Assert.AreEqual(5, panel.Select(z => z.Zone).Distinct().Count());
            for (int i = 1; i < panel.Length; i++) Assert.GreaterOrEqual(panel[i - 1].Suitability, panel[i].Suitability);
            Assert.AreEqual(PitchZone.Wing, panel[0].Zone);
            Assert.AreEqual(ZoneDeclaration.Primary, panel[0].Declaration);
            Assert.AreEqual(ZoneDeclaration.Secondary, panel.First(z => z.Zone == PitchZone.Attack).Declaration);
            Assert.AreEqual(ZoneDeclaration.Secondary, panel.First(z => z.Zone == PitchZone.Midfield).Declaration);
            Assert.AreEqual(ZoneDeclaration.None, panel.First(z => z.Zone == PitchZone.Defense).Declaration);
            foreach (var z in panel) Assert.AreEqual(calc.GetZoneSuitability(sak, profile, z.Zone), z.Suitability, z.Zone.ToString());
            Assert.AreEqual(card.GetZoneSuitability(PitchZone.Attack), panel.First(z => z.Zone == PitchZone.Attack).Suitability);
        }

        [Test]
        public void TheCard_CanReportRoleSuitability()
        {
            var card = builder.Build(sak);
            Assert.AreEqual(calc.GetRoleSuitability(sak, profile, PlayerArchetype.Explosive), card.GetRoleSuitability(PlayerArchetype.Explosive));
        }

        // ================= Derived, never copied =================

        [Test]
        public void IfAnAttributeChanges_TheCardShowsTheNewValueAtOnce()
        {
            var card = builder.Build(sak);
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Speed, 82);
            Assert.AreEqual(82, card.GetAttribute(PlayerAttributeId.Speed));
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Speed, 86);
            Assert.AreEqual(86, card.GetAttribute(PlayerAttributeId.Speed), "82 -> 86 shows up without rebuilding the card");
            Assert.AreEqual(86, card.Attributes.Speed);
        }

        [Test]
        public void TheOverall_FollowsAttributeChanges_WithoutRebuildingTheCard()
        {
            var card = builder.Build(sak);
            int before = card.Overall;
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Dribbling, 40).With(PlayerAttributeId.Control, 40).With(PlayerAttributeId.Speed, 40);
            Assert.Less(card.Overall, before);
            Assert.AreEqual(calc.GetOverall(sak, profile), card.Overall);
        }

        [Test]
        public void IdentityEditsAppearOnTheCard_WithoutRebuilding()
        {
            var card = builder.Build(sak);
            sak.Name = "Renamed"; sak.Number = 11; sak.ShortName = "Ren"; sak.NationalityCode = "AB"; sak.TeamId = "team-other";
            Assert.AreEqual("Renamed", card.DisplayName);
            Assert.AreEqual(11, card.ShirtNumber);
            Assert.AreEqual("Ren", card.ShortName);
            Assert.AreEqual("AB", card.NationalityCode);
            Assert.AreEqual("team-other", card.TeamId);
        }

        [Test]
        public void ProfileEditsAppearOnTheCard_WithoutRebuilding()
        {
            var card = builder.Build(sak);
            profile.PrimaryZone = PitchZone.Attack;
            profile.SecondaryZones.Clear();
            Assert.AreEqual(PitchZone.Attack, card.PrimaryZone);
            Assert.AreEqual(0, card.SecondaryZones.Count);
            profile.ArchetypeAffinities[1] = 99;
            card.TryGetPrimaryRole(out var role);
            Assert.AreEqual(PlayerArchetype.Creator, role);
        }

        [Test]
        public void TheCard_HoldsOnlyReferencesAndItsType_NeverACopyOfTheStats()
        {
            var allowed = new HashSet<Type>
            {
                typeof(PlayerDefinition), typeof(PlayerPlayingProfile), typeof(TeamDefinition), typeof(CardType), typeof(PlayerRatingCalculator)
            };
            var fields = typeof(PlayerCardData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var f in fields)
            {
                Type t = f.FieldType; // auto-property backing fields are included
                Assert.IsTrue(allowed.Contains(t), f.Name + " : " + t.Name);
                Assert.AreNotEqual(typeof(PlayerAttributes), t, f.Name);
                Assert.AreNotEqual(typeof(int), t, f.Name);
            }
        }

        [Test]
        public void TheCard_DoesNotHoldAStaminaOrAnyAttributeValue()
        {
            var names = typeof(PlayerCardData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.Name.ToLowerInvariant()).ToArray();
            foreach (var attr in PlayerAttributeInfo.All)
                Assert.IsFalse(names.Any(n => n.Contains(attr.ToString().ToLowerInvariant())), attr.ToString());
            Assert.IsFalse(names.Any(n => n.Contains("stamina") || n.Contains("overall")));
        }

        [Test]
        public void TheCard_SharesTheSourceObjects_NotClones()
        {
            var card = builder.Build(sak);
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Finishing, 99);
            Assert.AreEqual(99, card.GetAttribute(PlayerAttributeId.Finishing));
            // And it does not write back: reading everything leaves the source as it was.
            var snapshot = sak.Attributes;
            card.GetPolyvalence(); card.GetRoles(); _ = card.Overall; _ = card.StyleLabel;
            Assert.AreEqual(snapshot, sak.Attributes);
        }

        // ================= Without an explicit profile =================

        [Test]
        public void ACardForAPlayerWithoutAProfile_UsesTheCoarseRoleDefaults()
        {
            var plain = new PlayerDefinition("p-plain", "Plain Player", 5, PlayerRole.Midfielder, TestData.Attrs(70));
            var card = new PlayerCardBuilder(calc).Build(plain);
            Assert.AreEqual(PitchZone.Midfield, card.PrimaryZone);
            Assert.IsTrue(card.TryGetPrimaryRole(out var role));
            Assert.AreEqual(PlayerArchetype.Engine, role);
            Assert.AreEqual("", card.TeamName);
            Assert.AreEqual(70, card.Overall);
        }

        [Test]
        public void ShortName_IsDerivedWhenNotSet()
        {
            var p = new PlayerDefinition("p-short", "Nico Valmar", 10, PlayerRole.Forward, TestData.Attrs(70));
            Assert.AreEqual("Valmar", new PlayerCardBuilder(calc).Build(p).ShortName);
        }

        [Test]
        public void ATeamLookupThatFindsNothing_LeavesTheTeamNameEmpty_ButKeepsTheId()
        {
            var b = new PlayerCardBuilder(calc, profiles, cardTypes, id => null);
            var card = b.Build(sak);
            Assert.AreEqual("", card.TeamName);
            Assert.AreEqual("team-cobalt", card.TeamId);
        }

        // ================= Card types =================

        [Test]
        public void ThereAreSixCardTypes()
        {
            CollectionAssert.AreEqual(new[] { "Standard", "Rare", "Special", "Legend", "Event", "Custom" }, Enum.GetNames(typeof(CardType)));
        }

        [Test]
        public void CardType_DefaultsToStandard_AndComesFromTheCatalog()
        {
            Assert.AreEqual(CardType.Standard, builder.Build(sak).CardType);
            Assert.IsTrue(cardTypes.Set("fs27-p-sak", CardType.Legend));
            Assert.AreEqual(CardType.Legend, builder.Build(sak).CardType);
            Assert.IsTrue(cardTypes.Set("fs27-p-sak", CardType.Event));
            Assert.AreEqual(CardType.Event, builder.Build(sak).CardType, "editable data");
        }

        [Test]
        public void EveryCardType_Works()
        {
            foreach (CardType t in Enum.GetValues(typeof(CardType)))
            {
                Assert.IsTrue(cardTypes.Set("fs27-p-sak", t));
                Assert.AreEqual(t, builder.Build(sak).CardType);
            }
        }

        [Test]
        public void CardCatalog_RejectsBadIdsAndUndefinedTypes()
        {
            Assert.IsFalse(cardTypes.Set("bad id", CardType.Rare));
            Assert.IsFalse(cardTypes.Set(null, CardType.Rare));
            Assert.IsFalse(cardTypes.Set("fs27-p-sak", (CardType)42));
            Assert.AreEqual(CardType.Standard, cardTypes.Get("fs27-p-sak"));
            Assert.AreEqual(CardType.Standard, cardTypes.Get(null));
            Assert.AreEqual(0, cardTypes.Count);
        }

        [Test]
        public void TheCardType_ChangesNothingAboutThePlayer()
        {
            var ratings = new List<(int overall, int wing, int explosive, int speed)>();
            foreach (CardType t in Enum.GetValues(typeof(CardType)))
            {
                cardTypes.Set("fs27-p-sak", t);
                var c = builder.Build(sak);
                ratings.Add((c.Overall, c.GetZoneSuitability(PitchZone.Wing), c.GetRoleSuitability(PlayerArchetype.Explosive), c.GetAttribute(PlayerAttributeId.Speed)));
            }
            Assert.AreEqual(1, ratings.Distinct().Count(), "Standard, Rare, Special, Legend, Event and Custom rate identically");
        }

        // ================= A card is not gameplay =================

        [Test]
        public void TheCard_HasNoSettersAndNoMethodsThatTouchThePlayer()
        {
            var t = typeof(PlayerCardData);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsTrue(p.GetSetMethod(true) == null || !p.GetSetMethod(true).IsPublic, p.Name + " has a public setter");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.Fail("public field " + f.Name);
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(x => !x.IsSpecialName))
                foreach (var prm in m.GetParameters())
                    Assert.IsFalse(prm.ParameterType == typeof(PlayerDefinition) || prm.ParameterType == typeof(PlayerPlayingProfile) || prm.ParameterType == typeof(PlayerAttributes),
                        m.Name + " takes " + prm.ParameterType.Name);
        }

        [Test]
        public void NothingInGameplayOrTheAiInfrastructure_KnowsTheCard()
        {
            var cardTypes = new[] { typeof(PlayerCardData), typeof(PlayerCardBuilder), typeof(PlayerCardCatalog), typeof(ZoneSuitability), typeof(ZoneDeclaration) };
            var cardNames = new HashSet<string>(cardTypes.Select(t => t.FullName));
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (Type t in typeof(PlayerDefinition).Assembly.GetTypes())
            {
                if (cardNames.Contains(t.FullName) || t.FullName.StartsWith("FS27.Core.Tests") || t.IsNested) continue;
                var used = t.GetFields(all).Select(f => f.FieldType)
                    .Concat(t.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Concat(new[] { m.ReturnType })))
                    .Concat(t.GetProperties(all).Select(p => p.PropertyType))
                    .Select(x => x.IsByRef ? x.GetElementType() : x);
                foreach (Type u in used)
                    Assert.IsFalse(cardNames.Contains(u.FullName ?? ""), t.Name + " must not depend on " + u.Name + " (Card -> Gameplay is forbidden)");
            }
        }

        [Test]
        public void BuildingAndReadingACard_NeverChangesThePlayerOrItsProfile()
        {
            var attrs = sak.Attributes;
            var zones = profile.SecondaryZones.ToArray();
            var affinities = profile.ArchetypeAffinities.ToArray();
            string name = sak.Name; int number = sak.Number;

            foreach (CardType t in Enum.GetValues(typeof(CardType)))
            {
                cardTypes.Set("fs27-p-sak", t);
                var c = builder.Build(sak);
                _ = c.Overall; c.GetPolyvalence(); c.GetRoles(); _ = c.StyleLabel; _ = c.TeamName;
            }
            Assert.AreEqual(attrs, sak.Attributes);
            CollectionAssert.AreEqual(zones, profile.SecondaryZones);
            CollectionAssert.AreEqual(affinities, profile.ArchetypeAffinities);
            Assert.AreEqual(name, sak.Name); Assert.AreEqual(number, sak.Number);
        }

        [Test]
        public void TheBuilderAndTheCard_RequireTheirInputs()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerCardBuilder(null));
            Assert.Throws<ArgumentNullException>(() => builder.Build(null));
            Assert.Throws<ArgumentNullException>(() => new PlayerCardData(null, null, null, CardType.Standard, calc));
            Assert.Throws<ArgumentNullException>(() => new PlayerCardData(sak, null, null, CardType.Standard, null));
        }

        [Test]
        public void TheCard_UsesTheCalculatorItWasBuiltWith_SoTuningChangesShowUp()
        {
            var card = builder.Build(sak);
            int before = card.Overall;
            calc.Tuning.OverallZoneBlend = 1f;
            Assert.AreNotEqual(before, card.Overall);
            Assert.AreEqual(calc.GetZoneRating(sak, PitchZone.Wing), card.Overall);
        }
    }
}
