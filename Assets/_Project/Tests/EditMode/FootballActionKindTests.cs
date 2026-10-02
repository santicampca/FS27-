using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>The action enum is a gameplay contract: its original numbers never move and it does not belong to the Creator.</summary>
    public class FootballActionKindTests
    {
        [Test]
        public void OriginalValues_KeepTheirNumbers()
        {
            string[] original =
            {
                "None", "Control", "Dribble", "ShortPass", "LongPass", "Cross", "Shot", "PlacedShot", "Header", "Tackle", "Intercept", "Block", "Shield",
                "GoalkeeperSave", "GoalkeeperCatch", "GoalkeeperDistribution"
            };
            for (int i = 0; i < original.Length; i++)
                Assert.AreEqual(i, (int)Enum.Parse(typeof(FootballActionKind), original[i]), original[i]);
        }

        [Test]
        public void NewActions_AreAppendedAfterTheOriginalSet()
        {
            string[] added = { "CurvedShot", "Chip", "WallPass", "ThroughPass", "SkillMove", "SkillLobPass", "GoalkeeperRush" };
            for (int i = 0; i < added.Length; i++)
                Assert.AreEqual(16 + i, (int)Enum.Parse(typeof(FootballActionKind), added[i]), added[i]);
            Assert.AreEqual(23, Enum.GetValues(typeof(FootballActionKind)).Length, "no filler values");
        }

        [Test]
        public void Values_AreUniqueAndContiguous()
        {
            int[] values = Enum.GetValues(typeof(FootballActionKind)).Cast<int>().OrderBy(v => v).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, values.Length).ToArray(), values);
        }

        [Test]
        public void ActionKind_IsNotDefinedInsideTheCreatorSources()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(core, "Creator"), "*.cs", System.IO.SearchOption.AllDirectories))
                Assert.IsFalse(System.IO.File.ReadAllText(file).Contains("enum FootballActionKind"), file);
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(core, "FootballActionKind.cs")));
        }

        [Test]
        public void EveryNewAction_HasAPlannedAnimationProfile()
        {
            var catalog = AnimationCatalog.CreateDefault();
            foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.Chip, FootballActionKind.WallPass, FootballActionKind.ThroughPass, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass, FootballActionKind.GoalkeeperRush })
                Assert.IsTrue(catalog.All.Any(a => a.Action == k && a.Status == AnimationStatus.Planned), k.ToString());
        }
    }
}
