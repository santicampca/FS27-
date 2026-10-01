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
                library.TryAdd(TestData.Player(prefix + "-m1", 8, PlayerRole.Midfielder));
                library.TryAdd(TestData.Player(prefix + "-f1", 9, PlayerRole.Forward));
                library.TryAdd(TestData.Player(prefix + "-f2", 10, PlayerRole.Forward));
            }
            foreach (var p in library.All) library.TryAssignToTeam(p.Id, p.Id.StartsWith("blue") ? "blue" : "red");
        }

        private static string[] Ids(string prefix) { return new[] { prefix + "-gk", prefix + "-d1", prefix + "-d2", prefix + "-m1", prefix + "-f1", prefix + "-f2" }; }

        private TeamDefinition Team(string id)
        {
            Assert.IsTrue(TeamRoster.TryBuild(id, "Team " + id, new TeamColors(), DefaultFormations.TwoOneTwo, Ids(id), library, out var team, out var problem), problem);
            return team;
        }

        // ================= Unique ids =================

        [Test]
        public void PlayerIds_AreUnique_TheLibraryRefusesRepeats()
        {
            Assert.AreEqual(12, library.Count);
            Assert.IsFalse(library.TryAdd(TestData.Player("blue-gk", 5, PlayerRole.Goalkeeper)), "same id again");
            Assert.AreEqual(12, library.Count);
            Assert.IsTrue(library.TryAdd(TestData.Player("brand-new", 5, PlayerRole.Midfielder)));
            Assert.AreEqual(13, library.Count);
        }

        [Test]
        public void TheLibrary_RejectsNullAndInvalidIds()
        {
            Assert.IsFalse(library.TryAdd(null));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = "has space", Name = "x" }));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = null, Name = "x" }));
            Assert.IsFalse(library.TryAdd(new PlayerDefinition { Id = "", Name = "x" }));
            Assert.AreEqual(12, library.Count);
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
        public void ATeam_HoldsSixIds_AndResolvesToTheLibrarysOwnPlayers_NotCopies()
        {
            var team = Team("blue");
            Assert.AreEqual(6, team.PlayerIds.Count);
            CollectionAssert.AreEqual(Ids("blue"), team.PlayerIds);
            Assert.IsTrue(TeamRoster.TryResolve(team, library, out var resolved));
            Assert.AreEqual(6, resolved.Count);
            for (int i = 0; i < 6; i++)
            {
                library.TryGet(Ids("blue")[i], out var inLibrary);
                Assert.AreSame(inLibrary, resolved[i]);
            }
        }

        [Test]
        public void EditingAPlayerInTheLibrary_ChangesItForTheTeamToo()
        {
            var team = Team("blue");
            library.TryGet("blue-f1", out var f1);
            f1.Attributes = f1.Attributes.With(PlayerAttributeId.Finishing, 96);
            f1.Name = "Edited Name";
            TeamRoster.TryResolve(team, library, out var resolved);
            Assert.AreEqual(96, resolved[4].Attributes.Finishing);
            Assert.AreEqual("Edited Name", resolved[4].Name);
        }

        [Test]
        public void TwoTeams_ShareNoPlayers_AndTogetherUseEveryLibraryPlayerOnce()
        {
            var blue = Team("blue"); var red = Team("red");
            var all = blue.PlayerIds.Concat(red.PlayerIds).ToList();
            Assert.AreEqual(12, all.Count);
            Assert.AreEqual(12, all.Distinct().Count(), "no player appears twice");
            Assert.AreEqual(library.Count, all.Count);
            var r = DataValidator.ValidateMatchTeams(blue, red, library, formations);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void ATeamCanBeBuiltWithAnyNumberOfPlayers_TheSixPlayerRuleIsTheValidatorsJob()
        {
            Assert.IsTrue(TeamRoster.TryBuild("five", "Five", new TeamColors(), DefaultFormations.TwoOneTwo, Ids("blue").Take(5).ToList(), library, out var five, out _));
            Assert.IsTrue(DataValidator.ValidateTeam(five, library, formations).Has(ValidationCode.TeamPlayerCountInvalid));
            Assert.IsTrue(DataValidator.ValidateTeam(Team("blue"), library, formations).IsValid);
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
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), DefaultFormations.TwoOneTwo, ids, library, out var team, out var problem));
            Assert.IsNull(team);
            StringAssert.Contains("ghost", problem);

            ids = Ids("blue").ToList();
            ids[1] = ids[0];
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), DefaultFormations.TwoOneTwo, ids, library, out team, out problem));
            StringAssert.Contains("twice", problem);

            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), DefaultFormations.TwoOneTwo, null, library, out _, out _));
            Assert.IsFalse(TeamRoster.TryBuild("t", "T", new TeamColors(), DefaultFormations.TwoOneTwo, Ids("blue"), null, out _, out _));
        }

        [Test]
        public void ResolvingATeam_FailsCleanly_WhenAnIdIsMissingFromTheLibrary()
        {
            var team = Team("blue");
            team.PlayerIds[1] = "ghost";
            Assert.IsFalse(TeamRoster.TryResolve(team, library, out var resolved));
            Assert.AreEqual(0, resolved.Count);
            Assert.IsFalse(TeamRoster.TryResolve(null, library, out _));
            Assert.IsFalse(TeamRoster.TryResolve(Team("red"), null, out _));
        }

        [Test]
        public void TeamDefinition_HoldsIdsAndFormationById_NoPlayerInstances()
        {
            var fields = typeof(TeamDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Id", "Name", "Colors", "PlayerIds", "FormationId" }, fields);
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
            Assert.AreEqual(5, library.GetByTeam("blue").Count);
            Assert.AreEqual(7, library.GetByTeam("red").Count);
            Assert.AreEqual(12, library.Count, "nobody was duplicated or lost");
        }

        [Test]
        public void ATeamRebuiltAfterAMove_StaysConsistentWithThePlayersTeamIds()
        {
            library.TryAssignToTeam("red-f2", "blue");
            library.TryAssignToTeam("blue-f2", "red");   // swap two players
            var blueIds = new[] { "blue-gk", "blue-d1", "blue-d2", "blue-m1", "blue-f1", "red-f2" };
            var redIds = new[] { "red-gk", "red-d1", "red-d2", "red-m1", "red-f1", "blue-f2" };
            Assert.IsTrue(TeamRoster.TryBuild("blue", "Blue", new TeamColors(), DefaultFormations.TwoOneTwo, blueIds, library, out var blue, out _));
            Assert.IsTrue(TeamRoster.TryBuild("red", "Red", new TeamColors(), DefaultFormations.TwoOneTwo, redIds, library, out var red, out _));
            // Shirt numbers collide only if two players in the SAME team share one; here f2 numbers are 10 and 10 but on different teams.
            var result = DataValidator.ValidateMatchTeams(blue, red, library, formations);
            Assert.IsTrue(result.IsValid, result.ToString());
        }

        [Test]
        public void AMismatchBetweenAPlayersTeamIdAndTheRoster_IsCaughtByTheValidator()
        {
            library.TryAssignToTeam("blue-f2", "red");
            var blueStillListingIt = Team("blue");
            var r = DataValidator.ValidateTeam(blueStillListingIt, library, formations);
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
            var r = DataValidator.ValidateTeam(team, library, formations);
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
