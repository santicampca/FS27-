using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CharacterCardViewTests
    {
        private CreatorPipeline pipeline;
        private PlayerCardData card;

        [SetUp]
        public void SetUp()
        {
            pipeline = CreatorPipeline.CreateDefault();
            var library = new PlayerLibrary();
            library.TryAdd(SakData.Player());
            var profiles = new PlayingProfileCatalog();
            profiles.TryAdd(SakData.Profile());
            card = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), profiles, null, null, library).Build(SakData.Player());
        }

        private CharacterSpecification Character()
        {
            CreatorResult r = pipeline.Run(new CreatorRequest { Text = "Crea un extremo muy alto, musculoso, con pelo rizado y barba corta, que regatee mucho", Seed = 4 });
            Assert.IsTrue(r.Success, r.DebugReport);
            return r.Specification;
        }

        [Test]
        public void TheView_SummarisesAppearanceStyleBehavioursRoleZonesAndOverall()
        {
            CharacterCardView v = CharacterCardView.Build(card, Character(), pipeline.Catalogs);
            Assert.AreEqual(SakData.Id, v.PlayerId);
            Assert.AreEqual("FS27 Cartoon Sports", v.StyleName);
            CollectionAssert.Contains(v.AppearanceSummary, "tall");
            CollectionAssert.Contains(v.AppearanceSummary, "muscular");
            Assert.IsTrue(v.AppearanceSummary.Any(a => a.StartsWith("hair:")));
            Assert.IsTrue(v.AppearanceSummary.Any(a => a.StartsWith("beard:")));
            Assert.IsNotEmpty(v.FootballStyle);
            Assert.IsTrue(v.FootballStyle.Any(s => s.Contains("takeOn".ToLowerInvariant()) || s.Contains("take on")));
            Assert.AreEqual(card.Overall, v.Overall);
            Assert.AreEqual(card.HeadlineRating, v.HeadlineRating);
            Assert.AreEqual(card.PrimaryZone, v.PrimaryZone);
            Assert.IsNotEmpty(v.Role);
        }

        [Test]
        public void TheCard_IsUnchanged_ByHavingAView()
        {
            int overall = card.Overall;
            CharacterCardView.Build(card, Character(), pipeline.Catalogs);
            Assert.AreEqual(89, overall);
            Assert.AreEqual(overall, card.Overall);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (FieldInfo f in typeof(PlayerCardData).GetFields(all))
                Assert.IsFalse(f.FieldType == typeof(CharacterSpecification) || f.FieldType == typeof(CharacterCardView), f.Name);
        }

        [Test]
        public void TheView_IsReadOnly_AndKeepsNoAttributesOrSpecification()
        {
            foreach (PropertyInfo p in typeof(CharacterCardView).GetProperties()) Assert.IsFalse(p.CanWrite && p.SetMethod.IsPublic, p.Name + " has a public setter");
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (FieldInfo f in typeof(CharacterCardView).GetFields(all))
                Assert.IsFalse(f.FieldType == typeof(CharacterSpecification) || f.FieldType == typeof(FootballDNA) || f.FieldType == typeof(PlayerAttributes), f.Name);
        }

        [Test]
        public void Behaviours_AreListedStrongestFirst_AsReadableNames()
        {
            CharacterSpecification s = Character();
            s.Dna.Behaviors.Clear();
            s.Dna.SetBehavior("StopAndGo", 0.4f);
            s.Dna.SetBehavior("FirstTimeFinish", 0.9f);
            CharacterCardView v = CharacterCardView.Build(card, s, pipeline.Catalogs);
            CollectionAssert.AreEqual(new[] { "first time finish", "stop and go" }, v.Behaviors);
        }

        [Test]
        public void ANeutralCharacter_HasAnAverageSummary_AndNothingInvented()
        {
            CharacterSpecification s = pipeline.Catalogs.NewSpecification("neutral");
            CharacterCardView v = CharacterCardView.Build(card, s, pipeline.Catalogs);
            CollectionAssert.AreEqual(new[] { "average build" }, v.AppearanceSummary);
            Assert.IsEmpty(v.Behaviors);
            Assert.IsEmpty(v.FootballStyle);
        }

        [Test]
        public void NullInputs_AreRefused()
        {
            Assert.Throws<System.ArgumentNullException>(() => CharacterCardView.Build(null, Character(), pipeline.Catalogs));
            Assert.Throws<System.ArgumentNullException>(() => CharacterCardView.Build(card, null, pipeline.Catalogs));
        }
    }
}
