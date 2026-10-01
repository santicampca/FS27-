using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PlayerLibraryAndTeamTests
    {
        private PlayerLibrary library;
        private readonly FormationLibrary formations = FormationLibrary.CreateDefault();

        [SetUp]
        public void SetUp()
        {
            library = new PlayerLibrary();
            foreach (string prefix in new[] { "blue", "red" })
            {
                library.TryAdd(TestData.Player(prefix + "-gk", 1, PlayerRole.Goalkeeper));
                library.TryAdd(TestData.Player(prefix + "-d1", 2, PlayerRole.Defender));
                library.TryAdd(TestData.Player(prefix + "-d2", 3, PlayerRole.Defender));
                library.TryAdd(TestData.Player(prefix + "-f1", 9, PlayerRole.Forward));
                library.TryAdd(TestData.Player(prefix + "-f2", 10, PlayerRole.Forward));
            }
            foreach (var p in library.All) library.TryAssignToTeam(p.Id, p.Id.StartsWith("blue") ? "blue" : "red");
        }

        private static string[] Ids(string prefix) { return new[] { prefix + "-gk", prefix + "-d1", prefix + "-d2", prefix + "-f1", prefix + "-f2" }; }

        private TeamDefinition Team(string id)
        {
            Assert.IsTrue(TeamRoster.TryBuild(id, "Team " + id, new TeamColors(), DefaultFormations.TwoTwo, Ids(id), library, out var team, out var problem), problem);
            return team;
        }

        // ================= Unique ids =================

        [Test]
        public void PlayerIds_AreUnique_TheLibraryRefusesRepeats()
        {
            Assert.AreEqual(10, library.Count);
            Assert.IsFalse(library.TryAdd(TestData.Player("blue-gk", 5, PlayerRole.Goalkeeper)), "same id again");
            Assert.AreEqual(10, library.Count);
            Assert.IsTrue(library.TryAdd(TestData.Player("brand-new", 5, PlayerRole.Midfielder)));
            Assert.AreEqual(11, library.Count);
        }

        [Test]
        public void TheLibrary_RejectsNullAndInvalidIds()
        {
            Assert.IsFalse(library.TryAdd(null));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = "has space", Name = "x" }));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = null, Name = "x" }));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = "", Name = "x" }));
            Assert.AreEqual(10, library.Count);
        }

        [Test]
        public void Lookup_ByIdWorks_AndReturnsTheSameInstance()
        {
            Assert.IsTrue(library.Contains("red-d1"));
            Assert.IsFalse(library.Contains("nobody"));
            Assert.IsFalse(library.Contains(null));
            Assert.IsTrue(library.TryGet("red-d1", out var p));
            Assert.AreSame(library.All.First(x => x.Id == "red-d1"), p);
            Assert.IsFalse(library.TryGet(null, out _));
        }

        // ================= Teams reference players =================

        [Test]
        public void ATeam_ReferencesTheLibrarysOwnPlayers_NotCopies()
        {
            var team = Team("blue");
            Assert.AreEqual(5, team.Players.Count);
            Assert.IsTrue(TeamRoster.UsesLibraryPlayers(team, library));
            for (int i = 0; i < 5; i++)
            {
                library.TryGet(Ids("blue")[i], out var inLibrary);
                Assert.AreSame(inLibrary, team.Players[i]);
            }
            CollectionAssert.AreEqual(Ids("blue"), TeamRoster.GetPlayerIds(team));
        }

        [Test]
        public void EditingAPlayerInTheLibrary_ChangesItInTheTeamToo()
        {
            var team = Team("blue");
            library.TryGet("blue-f1", out var f1);
            f1.Attributes = f1.Attributes.With(PlayerAttributeId.Finishing, 96);
            f1.Name = "Edited Name";
            Assert.AreEqual(96, team.Players[3].Attributes.Finishing);
            Assert.AreEqual("Edited Name", team.Players[3].Name);
        }

        [Test]
        public void TwoTeams_ShareNoPlayerInstances_AndTogetherUseEveryLibraryPlayerOnce()
        {
            var blue = Team("blue"); var red = Team("red");
            var all = blue.Players.Concat(red.Players).ToList();
            Assert.AreEqual(10, all.Count);
            Assert.AreEqual(10, all.Distinct().Count(), "no player appears twice");
            Assert.AreEqual(library.Count, all.Count);
            Assert.IsTrue(DataValidator.ValidateMatchTeams(blue, red, formations).IsValid, DataValidator.ValidateMatchTeams(blue, red, formations).ToString());
        }

        [Test]
        public void BuildingATeam_DoesNotModifyAnyPlayer()
        {
            var before = library.All.Select(p => p.TeamId + "|" + p.Name + "|" + p.Number).ToArray();
            Team("blue");
            CollectionAssert.AreEqual(before, library.All.Select(p => p.TeamId + "|" + p.Name + "|" + p.Number).ToArray());
        }

        [Test]
        public void BuildingATeam_FailsCleanly_ForUnknownOrRepeatedIds()
        {
            var ids = Ids("blue").ToList();
            ids[2] = "ghost";
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), "2-2", ids, library, out var team, out var problem));
            Assert.IsNull(team);
            StringAssert.Contains("ghost", problem);

            ids = Ids("blue").ToList();
            ids[1] = ids[0];
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), "2-2", ids, library, out team, out problem));
            StringAssert.Contains("twice", problem);

            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), "2-2", null, library, out _, out _));
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), "2-2", Ids("blue"), null, out _, out _));
        }

        [Test]
        public void UsesLibraryPlayers_DetectsACopy()
        {
            var team = Team("blue");
            Assert.IsTrue(TeamRoster.UsesLibraryPlayers(team, library));
            team.Players[1] = new PlayerDefinition(team.Players[1].Id, "Clone", 2, PlayerRole.Defender, team.Players[1].Attributes);
            Assert.IsFalse(TeamRoster.UsesLibraryPlayers(team, library), "an equal-looking copy is not the library's player");
            Assert.IsFalse(TeamRoster.UsesLibraryPlayers(null, library));
            Assert.IsFalse(TeamRoster.UsesLibraryPlayers(Team("red"), null));
        }

        [Test]
        public void TeamDefinition_StillHoldsPlayersAndFormationById_AndNothingElseWasAdded()
        {
            var fields = typeof(TeamDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Id", "Name", "Colors", "Players", "FormationId" }, fields);
        }

        // ================= Moving a player changes data, not copies =================

        [Test]
        public void MovingAPlayerToAnotherTeam_IsAChangeOfTeamId_AndTheRostersAreRebuilt()
        {
            Assert.IsTrue(library.TryAssignToTeam("blue-f2", "red"));
            library.TryGet("blue-f2", out var moved);
            Assert.AreEqual("red", moved.TeamId);
            CollectionAssert.DoesNotContain(library.GetByTeam("blue").Select(p => p.Id).ToArray(), "blue-f2");
            CollectionAssert.Contains(library.GetByTeam("red").Select(p => p.Id).ToArray(), "blue-f2");
            Assert.AreEqual(4, library.GetByTeam("blue").Count);
            Assert.AreEqual(6, library.GetByTeam("red").Count);
            Assert.AreEqual(10, library.Count, "nobody was duplicated or lost");
        }

        [Test]
        public void ATeamRebuiltAfterAMove_StaysConsistentWithThePlayersTeamIds()
        {
            library.TryAssignToTeam("red-f2", "blue");
            library.TryAssignToTeam("blue-f2", "red");   // swap two players
            var blueIds = new[] { "blue-gk", "blue-d1", "blue-d2", "blue-f1", "red-f2" };
            var redIds = new[] { "red-gk", "red-d1", "red-d2", "red-f1", "blue-f2" };
            Assert.IsTrue(TeamRoster.TryBuild("blue", "Blue", new TeamColors(), "2-2", blueIds, library, out var blue, out _));
            Assert.IsTrue(TeamRoster.TryBuild("red", "Red", new TeamColors(), "2-2", redIds, library, out var red, out _));
            // Shirt numbers collide only if two players in the SAME team share one; here f2 numbers are 10 and 10 but on different teams.
            Assert.IsTrue(DataValidator.ValidateMatchTeams(blue, red, formations).IsValid, DataValidator.ValidateMatchTeams(blue, red, formations).ToString());
            Assert.IsTrue(TeamRoster.UsesLibraryPlayers(blue, library));
        }

        [Test]
        public void AMismatchBetweenAPlayersTeamIdAndTheRoster_IsCaughtByTheValidator()
        {
            library.TryAssignToTeam("blue-f2", "red");
            var blueStillListingIt = Team("blue");
            var r = DataValidator.ValidateTeam(blueStillListingIt, formations);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerTeamIdMismatch));
        }

        [Test]
        public void AFreePlayer_HasNoTeam_AndTeamIdsAreValidated()
        {
            Assert.IsTrue(library.TryAssignToTeam("blue-d1", ""));
            library.TryGet("blue-d1", out var p);
            Assert.AreEqual("", p.TeamId);
            Assert.IsTrue(DataValidator.ValidatePlayer(p).IsValid);
            Assert.IsFalse(library.TryAssignToTeam("blue-d1", "has space"));
            Assert.IsFalse(library.TryAssignToTeam("ghost", "red"));
            Assert.IsTrue(library.TryAssignToTeam("blue-d1", null), "null means no team");
            Assert.AreEqual("", p.TeamId);
            Assert.AreEqual(0, library.GetByTeam(null).Count);
        }

        [Test]
        public void ATeamBuiltFromTheLibrary_IsAValidMatchTeam()
        {
            var team = Team("blue");
            var r = DataValidator.ValidateTeam(team, formations);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        // ================= Cross validation =================

        [Test]
        public void TheSampleExamplePlayerAndItsProfile_ValidateTogether()
        {
            var report = PlayerSystemValidator.Validate(SakData.Player(), SakData.Profile());
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual("valid", report.ToString());
        }

        [Test]
        public void TheReport_CollectsProblemsFromBothSides()
        {
            var p = SakData.Player(); p.Number = 0;
            var pr = SakData.Profile(); pr.RiskPreference = 500;
            var report = PlayerSystemValidator.Validate(p, pr);
            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(report.Player.Has(ValidationCode.PlayerNumberOutOfRange));
            Assert.IsTrue(report.Profile.Has(AiDataIssueCode.ProfileBehaviourOutOfRange));
        }

        [Test]
        public void ValidateAll_ChecksEveryPlayer_AndEveryProfileBelongsToSomeone()
        {
            library.TryAdd(SakData.Player());
            var good = SakData.Profile();
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(library, new[] { good }).IsValid);

            var orphan = SakData.Profile(); orphan.PlayerId = "nobody";
            var r = PlayerSystemValidator.ValidateAll(library, new[] { orphan });
            Assert.IsTrue(r.Profile.Has(AiDataIssueCode.ProfilePlayerIdInvalid));

            library.TryGet("blue-gk", out var gk);
            gk.WeakFootQuality = 150;
            Assert.IsTrue(PlayerSystemValidator.ValidateAll(library, new[] { good }).Player.Has(ValidationCode.PlayerWeakFootOutOfRange));
        }
    }
}
