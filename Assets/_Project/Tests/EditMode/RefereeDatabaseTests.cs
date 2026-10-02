using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Referee DATA only: identity, style and profile. No behaviour is tested because there is none.</summary>
    public class RefereeDatabaseTests
    {
        private static readonly string[] ExpectedIds =
        {
            "referee-szymon-marciniak", "referee-michael-oliver", "referee-clement-turpin", "referee-danny-makkelie", "referee-istvan-kovacs"
        };

        private static readonly string[] ExpectedStyles = { "BalancedControl", "PermissiveFlow", "TechnicalBalanced", "DisciplinaryControl", "AuthorityIntensity" };

        private static string root;
        private static string text;
        private static RefereeImportResult result;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            text = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_REFEREES_V1.json"));
            result = RefereeJson.Load(text, out string error);
            Assert.IsNotNull(result, error);
        }

        [Test]
        public void Database_LoadsWithoutIssues() { Assert.IsTrue(result.IsValid, result.ToString()); }

        [Test]
        public void Database_HasExactlyTheFiveReferees()
        {
            Assert.AreEqual(5, result.Database.Count);
            CollectionAssert.AreEquivalent(ExpectedIds, result.Database.Referees.Select(r => r.RefereeId).ToArray());
        }

        [Test]
        public void Ids_AreUniqueAndValid()
        {
            var ids = result.Database.Referees.Select(r => r.RefereeId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            foreach (string id in ids) Assert.IsTrue(DataRules.IsValidId(id), id);
        }

        [Test]
        public void Names_AreValid()
        {
            foreach (RefereeDefinition r in result.Database.Referees)
            {
                Assert.IsTrue(DataRules.IsValidName(r.Name), r.RefereeId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.FullName), r.RefereeId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.ShortName), r.RefereeId);
            }
        }

        [Test]
        public void Nationalities_UseTheProjectCodes()
        {
            var expected = new Dictionary<string, string>
            {
                { "referee-szymon-marciniak", "POL" }, { "referee-michael-oliver", "ENG" }, { "referee-clement-turpin", "FRA" },
                { "referee-danny-makkelie", "NED" }, { "referee-istvan-kovacs", "ROU" }
            };
            foreach (RefereeDefinition r in result.Database.Referees)
            {
                Assert.IsTrue(PlayerRules.IsValidNationalityCode(r.NationalityCode), r.RefereeId);
                Assert.AreEqual(expected[r.RefereeId], r.NationalityCode, r.RefereeId);
            }
        }

        [Test]
        public void Styles_AreTheFiveInitialOnesAndEveryRefereeUsesOne()
        {
            CollectionAssert.AreEquivalent(ExpectedStyles, result.Database.Styles.Select(s => s.StyleId).ToArray());
            foreach (RefereeDefinition r in result.Database.Referees) Assert.IsTrue(result.Database.ContainsStyle(r.StyleId), r.RefereeId);
        }

        [Test]
        public void ProfileValues_AreInsideZeroToHundred()
        {
            foreach (RefereeDefinition r in result.Database.Referees)
            {
                Assert.AreEqual(7, r.Profile.Values.Count());
                foreach (KeyValuePair<string, int> v in r.Profile.Values) Assert.That(v.Value, Is.InRange(0, 100), r.RefereeId + " " + v.Key);
            }
        }

        [Test]
        public void ProfileValues_AreExactlyTheAuthoredOnes()
        {
            int[][] expected =
            {
                new[] { 55, 50, 80, 95, 55, 90, 90 }, new[] { 45, 45, 90, 88, 50, 88, 85 }, new[] { 55, 55, 75, 92, 55, 92, 88 },
                new[] { 70, 70, 60, 92, 55, 88, 85 }, new[] { 68, 65, 60, 92, 60, 88, 85 }
            };
            for (int i = 0; i < ExpectedIds.Length; i++)
            {
                result.Database.TryGet(ExpectedIds[i], out RefereeDefinition r);
                CollectionAssert.AreEqual(expected[i], r.Profile.Values.Select(v => v.Value).ToArray(), ExpectedIds[i]);
            }
        }

        [Test]
        public void EveryReferee_HasAuthoringNotes_AndUnverifiedOnesAreMarked()
        {
            foreach (RefereeDefinition r in result.Database.Referees)
            {
                Assert.IsTrue(r.Notes.Any(n => n.Kind == RefereeNoteKind.AuthoringInference), r.RefereeId);
                Assert.IsTrue(r.Notes.Any(n => n.Kind == RefereeNoteKind.Verified), r.RefereeId);
            }
        }

        [Test]
        public void Json_RoundTripsUnchanged()
        {
            Assert.IsTrue(RefereeJson.TryFromJson(text, out var styles, out var records, out string error), error);
            string again = RefereeJson.ToJson(styles, records);
            Assert.IsTrue(RefereeJson.TryFromJson(again, out var styles2, out var records2, out error), error);
            Assert.AreEqual(RefereeJson.ToJson(styles, records), RefereeJson.ToJson(styles2, records2));
        }

        [Test]
        public void Importer_RejectsBadRecords_WithoutAddingThem()
        {
            RefereeJson.TryFromJson(text, out var styles, out var records, out _);
            var bad = new List<RefereeDataRecord>(records);
            RefereeDataRecord dup = Clone(records[0]);
            bad.Add(dup);
            RefereeDataRecord badStyle = Clone(records[1]); badStyle.RefereeId = "referee-x1"; badStyle.Style = "NoSuchStyle";
            RefereeDataRecord badValue = Clone(records[1]); badValue.RefereeId = "referee-x2"; badValue.Cards = 101;
            RefereeDataRecord missing = Clone(records[1]); missing.RefereeId = "referee-x3"; missing.Advantage = null;
            RefereeDataRecord badNat = Clone(records[1]); badNat.RefereeId = "referee-x4"; badNat.Nationality = "Atlantis";
            bad.AddRange(new[] { badStyle, badValue, missing, badNat });
            RefereeImportResult r = RefereeRecordImporter.Import(styles, bad);
            Assert.IsTrue(r.Has(RefereeIssueCode.IdDuplicate));
            Assert.IsTrue(r.Has(RefereeIssueCode.StyleUnknown));
            Assert.IsTrue(r.Has(RefereeIssueCode.ValueOutOfRange));
            Assert.IsTrue(r.Has(RefereeIssueCode.ValueMissing));
            Assert.IsTrue(r.Has(RefereeIssueCode.NationalityUnknown));
            Assert.AreEqual(5, r.Database.Count);
        }

        [Test]
        public void NewStyle_IsDataNotCode()
        {
            RefereeJson.TryFromJson(text, out var styles, out var records, out _);
            styles.Add(new RefereeStyleDefinition("SomeNewStyle", "Some new style", "Added as data."));
            records[0].Style = "SomeNewStyle";
            Assert.IsTrue(RefereeRecordImporter.Import(styles, records).IsValid);
        }

        [Test]
        public void Referees_HaveNoCoachConcepts()
        {
            string[] forbidden = { "Grade", "Formation", "Boost", "Compatibility" };
            foreach (string member in typeof(RefereeDefinition).GetFields().Select(f => f.Name).Concat(typeof(RefereeProfile).GetFields().Select(f => f.Name)))
                foreach (string f in forbidden) Assert.IsFalse(member.Contains(f), member);
        }

        [Test]
        public void RefereeTypes_AreUsedByNoOtherCode()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            string[] names = { "RefereeDefinition", "RefereeProfile", "RefereeDatabase", "RefereeStyleDefinition", "RefereeDataRecord", "RefereeJson", "RefereeRecordImporter" };
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Referees/")) continue;
                string src = File.ReadAllText(file);
                foreach (string n in names) Assert.IsFalse(src.Contains(n), Path.GetFileName(file) + " must not depend on " + n);
            }
        }

        private static RefereeDataRecord Clone(RefereeDataRecord r)
        {
            return new RefereeDataRecord
            {
                RefereeId = r.RefereeId, Name = r.Name, FullName = r.FullName, ShortName = r.ShortName, Nationality = r.Nationality, Style = r.Style,
                Strictness = r.Strictness, Cards = r.Cards, Advantage = r.Advantage, GameControl = r.GameControl, PenaltySensitivity = r.PenaltySensitivity,
                Consistency = r.Consistency, Communication = r.Communication, Description = r.Description, Notes = new List<RefereeNote>(r.Notes)
            };
        }
    }
}
