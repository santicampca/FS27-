using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CharacterVariationTests
    {
        private CreatorCatalogs catalogs;
        private CharacterSpecification winger;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            var s = AuthoringSession.CreateDefault(5);
            s.Submit("Crea un extremo rápido con pelo negro");
            winger = s.Current.Spec;
            winger.Dna.Params.Set(catalogs.Parameters, "positioning.width", 0.85f);
        }

        [Test]
        public void TheSameSeed_GivesTheSameCharacter_ADifferentSeed_ADifferentOne()
        {
            string A(uint seed) => CharacterSpecificationJson.ToJson(CharacterVariationGenerator.Generate(winger, seed, catalogs, "p"));
            Assert.AreEqual(A(11), A(11));
            Assert.AreNotEqual(A(11), A(12));
        }

        [Test]
        public void TheSeedsAreRecorded_SoACharacterCanBeRegenerated()
        {
            CharacterSpecification c = CharacterVariationGenerator.Generate(winger, 777, catalogs, "p");
            Assert.AreEqual(777u, c.GenerationSeed);
            Assert.AreNotEqual(0u, c.AppearanceSeed);
            Assert.AreNotEqual(0u, c.BehaviorSeed);
            Assert.AreNotEqual(c.AppearanceSeed, c.BehaviorSeed);
            CharacterSpecification again = CharacterVariationGenerator.Generate(winger, c.GenerationSeed, catalogs, "p");
            Assert.AreEqual(CharacterSpecificationJson.ToJson(c), CharacterSpecificationJson.ToJson(again));
        }

        [Test]
        public void EveryGeneratedCharacter_IsValid_Coherent_AndKeepsTheArchetypesIdentity()
        {
            foreach (uint seed in Enumerable.Range(1, 60).Select(i => (uint)i))
            {
                CharacterSpecification c = CharacterVariationGenerator.Generate(winger, seed, catalogs, "p-" + seed);
                CreatorValidationResult v = CharacterSpecificationValidator.Validate(c, catalogs);
                Assert.IsTrue(v.IsValid, seed + ": " + v);
                Assert.IsFalse(AppearanceNormalizer.Normalize(c, catalogs, CompatibilityRules.CreateDefault()).Rejected, "seed " + seed);
                Assert.AreEqual(0, AppearanceNormalizer.Normalize(c, catalogs, CompatibilityRules.CreateDefault()).Changes.Count, "already coherent, seed " + seed);
                Assert.AreEqual(winger.Appearance.Colors["hair.color"], c.Appearance.Colors["hair.color"], "an archetype colour stays");
                Assert.AreEqual(0.85f, c.Dna.Get(catalogs.Parameters, "positioning.width"), 0.07f, "the archetype's tendencies only move a little");
            }
        }

        [Test]
        public void AnArchetypeChoice_IsNeverOverridden()
        {
            winger.Appearance.Choices["hair.style"] = "afro_08";
            for (uint seed = 1; seed <= 25; seed++)
                Assert.AreEqual("afro_08", CharacterVariationGenerator.Generate(winger, seed, catalogs, "p").Appearance.Choices["hair.style"]);
        }

        [Test]
        public void ACrowd_IsDiverse()
        {
            List<CharacterSpecification> crowd = CharacterVariationGenerator.Crowd(winger, 200, 42, catalogs);
            Assert.AreEqual(200, crowd.Count);
            Assert.AreEqual(200, crowd.Select(c => c.CharacterId).Distinct().Count());
            Assert.AreEqual(200, crowd.Select(c => CharacterSpecificationJson.ToJson(c)).Distinct().Count(), "no two identical characters");
            Assert.GreaterOrEqual(crowd.Select(c => c.Appearance.Choices["hair.style"]).Distinct().Count(), 5);
            Assert.GreaterOrEqual(crowd.Select(c => c.Appearance.Params.GetLevel(catalogs.Parameters, "body.height")).Select(v => (int)(v * 10)).Distinct().Count(), 6);
            Assert.Greater(crowd.Count(c => c.Appearance.Choices.TryGetValue("face.beard", out string b) && b != "none"), 10);
        }

        [Test]
        public void FiveThousandPlayers_AreSmall_Fast_AndReproducible()
        {
            var sw = Stopwatch.StartNew();
            List<CharacterSpecification> crowd = CharacterVariationGenerator.Crowd(winger, 5000, 7, catalogs);
            long generate = sw.ElapsedMilliseconds;
            long bytes = 0;
            foreach (CharacterSpecification c in crowd) bytes += CharacterSpecificationJson.ToJson(c.ForRuntime()).Length;
            Assert.Less(generate, 5000, "generation of 5000 characters took " + generate + " ms");
            Assert.Less(bytes / 5000.0, 2500, "average runtime JSON per character: " + bytes / 5000.0 + " bytes");
            Assert.Less(bytes, 12_500_000, "5000 characters must stay a few MB of text");
            Assert.AreEqual(CharacterSpecificationJson.ToJson(crowd[4321]), CharacterSpecificationJson.ToJson(CharacterVariationGenerator.Crowd(winger, 4322, 7, catalogs)[4321]));
            TestContext.WriteLine("5000 characters: " + generate + " ms, " + (bytes / 5000) + " bytes of runtime JSON each, " + (bytes / 1024) + " KB total");
        }
    }
}
