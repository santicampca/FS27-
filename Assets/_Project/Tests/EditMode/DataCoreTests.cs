using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Fictional, generic data only (no real players or teams).</summary>
    internal static class TestData
    {
        public static PlayerAttributes Attrs(int value = 70)
        {
            return new PlayerAttributes
            {
                Speed = value, Acceleration = value, Stamina = value, BallControl = value, Passing = value,
                Shooting = value, Defense = value, Strength = value, Reaction = value
            };
        }

        public static PlayerAttributes WithAttribute(int index, int value)
        {
            int[] v = Enumerable.Repeat(70, 9).ToArray();
            v[index] = value;
            return new PlayerAttributes
            {
                Speed = v[0], Acceleration = v[1], Stamina = v[2], BallControl = v[3], Passing = v[4],
                Shooting = v[5], Defense = v[6], Strength = v[7], Reaction = v[8]
            };
        }

        public static readonly string[] AttributeNames =
        {
            "Speed", "Acceleration", "Stamina", "BallControl", "Passing", "Shooting", "Defense", "Strength", "Reaction"
        };

        public static PlayerDefinition Player(string id, int number, PlayerRole role, int attr = 70)
        {
            return new PlayerDefinition(id, "Generic " + id, number, role, Attrs(attr));
        }

        /// <summary>A valid 5-player team: goalkeeper first, as the default formations expect.</summary>
        public static TeamDefinition Team(string prefix = "blue", string formationId = DefaultFormations.TwoTwo)
        {
            return new TeamDefinition(prefix, "Team " + prefix,
                new TeamColors(new ColorRgb(20, 80, 200), new ColorRgb(255, 255, 255)),
                formationId,
                Player(prefix + "-gk", 1, PlayerRole.Goalkeeper),
                Player(prefix + "-d1", 2, PlayerRole.Defender),
                Player(prefix + "-d2", 3, PlayerRole.Defender),
                Player(prefix + "-f1", 9, PlayerRole.Forward),
                Player(prefix + "-f2", 10, PlayerRole.Forward));
        }
    }

    public class DataCoreTests
    {
        private FormationLibrary library;

        [SetUp]
        public void SetUp()
        {
            library = FormationLibrary.CreateDefault();
        }

        // ================= Player =================

        [Test]
        public void Player_Valid_HasNoIssues()
        {
            var r = DataValidator.ValidatePlayer(TestData.Player("p1", 7, PlayerRole.Midfielder));
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void Player_Null_IsReported()
        {
            var r = DataValidator.ValidatePlayer(null);
            Assert.IsTrue(r.Has(ValidationCode.PlayerNull));
        }

        [Test]
        public void Player_DefaultConstructor_StartsWithDefaultAttributes()
        {
            var p = new PlayerDefinition();
            Assert.AreEqual(70, p.Attributes.Speed);
            Assert.AreEqual(70, p.Attributes.Reaction);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        [TestCase("tab\there")]
        public void Player_InvalidId_IsReported(string id)
        {
            var p = TestData.Player("x", 5, PlayerRole.Defender);
            p.Id = id;
            Assert.IsTrue(DataValidator.ValidatePlayer(p).Has(ValidationCode.PlayerIdInvalid), "id: " + (id ?? "null"));
        }

        [Test]
        public void Player_IdTooLong_IsReported_AndMaxLengthIsAccepted()
        {
            var p = TestData.Player("x", 5, PlayerRole.Defender);
            p.Id = new string('a', DataRules.MaxIdLength + 1);
            Assert.IsTrue(DataValidator.ValidatePlayer(p).Has(ValidationCode.PlayerIdInvalid));
            p.Id = new string('a', DataRules.MaxIdLength);
            Assert.IsTrue(DataValidator.ValidatePlayer(p).IsValid);
        }

        [TestCase("p-1")]
        [TestCase("player_1")]
        [TestCase("a")]
        [TestCase("blue.keeper.01")]
        public void Player_ReasonableIds_AreAccepted(string id)
        {
            Assert.IsTrue(DataValidator.ValidatePlayer(TestData.Player(id, 5, PlayerRole.Defender)).IsValid);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Player_BlankName_IsReported(string name)
        {
            var p = TestData.Player("p", 5, PlayerRole.Defender);
            p.Name = name;
            Assert.IsTrue(DataValidator.ValidatePlayer(p).Has(ValidationCode.PlayerNameInvalid));
        }

        [Test]
        public void Player_NameTooLong_IsReported()
        {
            var p = TestData.Player("p", 5, PlayerRole.Defender);
            p.Name = new string('n', DataRules.MaxNameLength + 1);
            Assert.IsTrue(DataValidator.ValidatePlayer(p).Has(ValidationCode.PlayerNameInvalid));
        }

        [TestCase(-5)]
        [TestCase(0)]
        [TestCase(100)]
        [TestCase(1000)]
        public void Player_NumberOutOfRange_IsReported(int number)
        {
            var r = DataValidator.ValidatePlayer(TestData.Player("p", number, PlayerRole.Defender));
            Assert.IsTrue(r.Has(ValidationCode.PlayerNumberOutOfRange));
            Assert.AreEqual(1, r.Count);
        }

        [TestCase(1)]
        [TestCase(50)]
        [TestCase(99)]
        public void Player_NumberInRange_IsAccepted(int number)
        {
            Assert.IsTrue(DataValidator.ValidatePlayer(TestData.Player("p", number, PlayerRole.Defender)).IsValid);
        }

        [Test]
        public void Player_UndefinedRole_IsReported()
        {
            var p = TestData.Player("p", 5, PlayerRole.Defender);
            p.Role = (PlayerRole)42;
            Assert.IsTrue(DataValidator.ValidatePlayer(p).Has(ValidationCode.PlayerRoleInvalid));
        }

        [Test]
        public void Player_EveryAttribute_IsRangeChecked([Range(0, 8)] int index, [Values(-1, 0, 100, 250)] int bad)
        {
            var p = TestData.Player("p", 5, PlayerRole.Defender);
            p.Attributes = TestData.WithAttribute(index, bad);
            var r = DataValidator.ValidatePlayer(p);
            Assert.AreEqual(1, r.Count, r.ToString());
            Assert.AreEqual(ValidationCode.PlayerAttributeOutOfRange, r.Issues[0].Code);
            StringAssert.Contains(TestData.AttributeNames[index], r.Issues[0].Message, "the message names the attribute");
            StringAssert.Contains("'p'", r.Issues[0].Subject, "the subject names the player");
        }

        [Test]
        public void Player_AttributeBoundaries_AreAccepted([Range(0, 8)] int index, [Values(1, 99)] int edge)
        {
            var p = TestData.Player("p", 5, PlayerRole.Defender);
            p.Attributes = TestData.WithAttribute(index, edge);
            Assert.IsTrue(DataValidator.ValidatePlayer(p).IsValid);
        }

        [Test]
        public void Player_AllProblemsAreReportedTogether()
        {
            var p = new PlayerDefinition("", " ", 0, (PlayerRole)9, TestData.WithAttribute(0, 0));
            var r = DataValidator.ValidatePlayer(p);
            Assert.IsTrue(r.Has(ValidationCode.PlayerIdInvalid));
            Assert.IsTrue(r.Has(ValidationCode.PlayerNameInvalid));
            Assert.IsTrue(r.Has(ValidationCode.PlayerNumberOutOfRange));
            Assert.IsTrue(r.Has(ValidationCode.PlayerRoleInvalid));
            Assert.IsTrue(r.Has(ValidationCode.PlayerAttributeOutOfRange));
            Assert.AreEqual(5, r.Count);
        }

        [Test]
        public void Player_IsCompatibleWithTheExistingStatsPipeline()
        {
            var tuning = new MovementTuning();
            var p = TestData.Player("p", 5, PlayerRole.Forward, 90);
            PlayerStats viaDefinition = PlayerStats.Resolve(p.Attributes, tuning);
            PlayerStats direct = PlayerStats.Resolve(TestData.Attrs(90), tuning);
            Assert.AreEqual(direct.TopSpeed, viaDefinition.TopSpeed, 1e-5f);
            Assert.AreEqual(direct.StaminaCapacity, viaDefinition.StaminaCapacity, 1e-5f);
        }

        // ================= Formations =================

        [Test]
        public void DefaultFormations_AreAllValidFor5v5()
        {
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
            {
                var r = DataValidator.ValidateFormation(f);
                Assert.IsTrue(r.IsValid, f.Id + ": " + r);
            }
        }

        [Test]
        public void DefaultFormations_AreThreeWithStableUniqueIds()
        {
            var all = DefaultFormations.CreateAll();
            Assert.AreEqual(3, all.Count);
            CollectionAssert.AreEquivalent(new[] { "2-2", "1-2-1", "2-1-1" }, all.Select(f => f.Id).ToArray());
            Assert.AreEqual(3, all.Select(f => f.Id).Distinct().Count());
            Assert.AreEqual(3, all.Select(f => f.Name).Distinct().Count());
        }

        [Test]
        public void DefaultFormations_HaveOneKeeperAtSlotZero_AndFourOutfielders()
        {
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
            {
                Assert.AreEqual(5, f.Positions.Count, f.Id);
                Assert.AreEqual(1, f.Positions.Count(p => p.Role == PlayerRole.Goalkeeper), f.Id);
                Assert.AreEqual(0, f.GoalkeeperIndex, f.Id);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, f.Positions.Select(p => p.PlayerIndex).ToArray(), f.Id);
            }
        }

        [Test]
        public void DefaultFormations_KeeperSitsDeepestAndSlotsDoNotOverlap()
        {
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
            {
                float keeperDepth = f.Positions.First(p => p.Role == PlayerRole.Goalkeeper).Relative.X;
                Assert.IsTrue(f.Positions.Where(p => p.Role != PlayerRole.Goalkeeper).All(p => p.Relative.X > keeperDepth), f.Id);

                for (int i = 0; i < f.Positions.Count; i++)
                    for (int j = i + 1; j < f.Positions.Count; j++)
                        Assert.Greater((f.Positions[i].Relative - f.Positions[j].Relative).Magnitude, 0.1f, f.Id + " slots " + i + "/" + j);
            }
        }

        [Test]
        public void DefaultFormations_RolesMatchTheirNames()
        {
            var roles = DefaultFormations.CreateAll().ToDictionary(f => f.Id, f => f.Positions.Select(p => p.Role).Where(r => r != PlayerRole.Goalkeeper).ToArray());
            Assert.AreEqual(2, roles["2-2"].Count(r => r == PlayerRole.Defender));
            Assert.AreEqual(2, roles["2-2"].Count(r => r == PlayerRole.Forward));
            Assert.AreEqual(1, roles["1-2-1"].Count(r => r == PlayerRole.Defender));
            Assert.AreEqual(2, roles["1-2-1"].Count(r => r == PlayerRole.Midfielder));
            Assert.AreEqual(1, roles["1-2-1"].Count(r => r == PlayerRole.Forward));
            Assert.AreEqual(2, roles["2-1-1"].Count(r => r == PlayerRole.Defender));
            Assert.AreEqual(1, roles["2-1-1"].Count(r => r == PlayerRole.Midfielder));
            Assert.AreEqual(1, roles["2-1-1"].Count(r => r == PlayerRole.Forward));
        }

        [Test]
        public void DefaultFormations_AreFreshInstancesEachCall()
        {
            var a = DefaultFormations.CreateDiamond();
            a.Positions.Clear();
            Assert.AreEqual(5, DefaultFormations.CreateDiamond().Positions.Count);
        }

        private static FormationDefinition Good()
        {
            return DefaultFormations.CreateTwoTwo();
        }

        [Test]
        public void Formation_Null_IsReported()
        {
            Assert.IsTrue(DataValidator.ValidateFormation(null).Has(ValidationCode.FormationNull));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(6)]
        public void Formation_WrongSlotCount_IsNot5v5(int count)
        {
            var f = Good();
            while (f.Positions.Count > count) f.Positions.RemoveAt(f.Positions.Count - 1);
            while (f.Positions.Count < count) f.Positions.Add(new FormationPosition(f.Positions.Count, PlayerRole.Forward, 0.5f, 0.5f));
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationPositionCountInvalid), "count " + count);
        }

        [Test]
        public void Formation_NullPositionList_IsReported()
        {
            var f = Good();
            f.Positions = null;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationPositionCountInvalid));
        }

        [Test]
        public void Formation_DuplicatePlayerIndex_IsReported()
        {
            var f = Good();
            var p = f.Positions[2]; p.PlayerIndex = 1; f.Positions[2] = p;
            var r = DataValidator.ValidateFormation(f);
            Assert.IsTrue(r.Has(ValidationCode.FormationPlayerIndexDuplicate));
        }

        [TestCase(-1)]
        [TestCase(5)]
        [TestCase(99)]
        public void Formation_PlayerIndexOutOfRange_IsReported(int index)
        {
            var f = Good();
            var p = f.Positions[3]; p.PlayerIndex = index; f.Positions[3] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationPlayerIndexOutOfRange));
        }

        [Test]
        public void Formation_WithoutKeeper_IsReported()
        {
            var f = Good();
            var p = f.Positions[0]; p.Role = PlayerRole.Defender; f.Positions[0] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationGoalkeeperCountInvalid));
        }

        [Test]
        public void Formation_WithTwoKeepers_IsReported()
        {
            var f = Good();
            var p = f.Positions[1]; p.Role = PlayerRole.Goalkeeper; f.Positions[1] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationGoalkeeperCountInvalid));
        }

        [TestCase(-0.01f, 0.5f)]
        [TestCase(1.01f, 0.5f)]
        [TestCase(0.5f, -0.2f)]
        [TestCase(0.5f, 1.5f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.PositiveInfinity)]
        public void Formation_PositionOutsideRelativeRange_IsReported(float depth, float width)
        {
            var f = Good();
            var p = f.Positions[2]; p.Relative = new Vec2(depth, width); f.Positions[2] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationPositionOutOfRange), depth + "," + width);
        }

        [Test]
        public void Formation_PositionsOnTheEdges_AreAccepted()
        {
            var f = Good();
            var p = f.Positions[3]; p.Relative = new Vec2(0f, 1f); f.Positions[3] = p;
            var q = f.Positions[4]; q.Relative = new Vec2(1f, 0f); f.Positions[4] = q;
            Assert.IsTrue(DataValidator.ValidateFormation(f).IsValid);
        }

        [Test]
        public void Formation_InvalidRole_IdAndName_AreReported()
        {
            var f = Good();
            f.Id = "bad id";
            f.Name = "";
            var p = f.Positions[1]; p.Role = (PlayerRole)77; f.Positions[1] = p;
            var r = DataValidator.ValidateFormation(f);
            Assert.IsTrue(r.Has(ValidationCode.FormationIdInvalid));
            Assert.IsTrue(r.Has(ValidationCode.FormationNameInvalid));
            Assert.IsTrue(r.Has(ValidationCode.FormationRoleInvalid));
        }

        [Test]
        public void Formation_Lookup_ByPlayerIndex_AndKeeperIndex()
        {
            var f = DefaultFormations.CreateDiamond();
            Assert.IsTrue(f.TryGetByPlayerIndex(4, out var fwd));
            Assert.AreEqual(PlayerRole.Forward, fwd.Role);
            Assert.IsFalse(f.TryGetByPlayerIndex(9, out _));
            Assert.AreEqual(0, f.GoalkeeperIndex);
            Assert.AreEqual(-1, new FormationDefinition().GoalkeeperIndex);
        }

        // ---- relative -> field space

        [Test]
        public void FieldPosition_CentreOfTheRelativeSpace_IsTheCentreOfThePitch()
        {
            var field = new FieldDimensions();
            var c = new FormationPosition(1, PlayerRole.Midfielder, 0.5f, 0.5f);
            Assert.AreEqual(0f, c.ToFieldPosition(field, true).Magnitude, 1e-5f);
            Assert.AreEqual(0f, c.ToFieldPosition(field, false).Magnitude, 1e-5f);
        }

        [Test]
        public void FieldPosition_Keeper_SitsNearItsOwnGoalLine()
        {
            var field = new FieldDimensions(); // 40 x 25
            var keeper = DefaultFormations.CreateTwoTwo().Positions[0];
            Vec2 attackingRight = keeper.ToFieldPosition(field, true);
            Vec2 attackingLeft = keeper.ToFieldPosition(field, false);
            Assert.AreEqual(-17.6f, attackingRight.X, 1e-3f);
            Assert.AreEqual(17.6f, attackingLeft.X, 1e-3f);
            Assert.AreEqual(0f, attackingRight.Y, 1e-4f);
        }

        [Test]
        public void FieldPosition_TheOtherTeamIsThe180DegreeRotation()
        {
            var field = new FieldDimensions();
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
                foreach (FormationPosition p in f.Positions)
                {
                    Vec2 a = p.ToFieldPosition(field, true);
                    Vec2 b = p.ToFieldPosition(field, false);
                    Assert.AreEqual(-a.X, b.X, 1e-4f);
                    Assert.AreEqual(-a.Y, b.Y, 1e-4f);
                }
        }

        [Test]
        public void FieldPosition_AllDefaultSlots_AreInsideThePitch_ForBothTeams()
        {
            var field = new FieldDimensions();
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
                foreach (FormationPosition p in f.Positions)
                    foreach (bool right in new[] { true, false })
                    {
                        Vec2 v = p.ToFieldPosition(field, right);
                        Assert.LessOrEqual(Math.Abs(v.X), field.Length / 2f);
                        Assert.LessOrEqual(Math.Abs(v.Y), field.Width / 2f);
                    }
        }

        [Test]
        public void FieldPosition_ScalesWithTheFieldSize()
        {
            var small = new FieldDimensions { Length = 20f, Width = 10f };
            var slot = new FormationPosition(3, PlayerRole.Forward, 0.75f, 0.25f);
            Vec2 v = slot.ToFieldPosition(small, true);
            Assert.AreEqual(5f, v.X, 1e-4f);
            Assert.AreEqual(-2.5f, v.Y, 1e-4f);
        }

        // ================= Formation library =================

        [Test]
        public void Library_Default_ContainsTheThreeFormations()
        {
            Assert.AreEqual(3, library.Count);
            Assert.IsTrue(library.Contains("2-2"));
            Assert.IsTrue(library.Contains("1-2-1"));
            Assert.IsTrue(library.Contains("2-1-1"));
            Assert.IsFalse(library.Contains("3-1"));
            Assert.IsFalse(library.Contains(null));
        }

        [Test]
        public void Library_TryGet_ReturnsTheFormation()
        {
            Assert.IsTrue(library.TryGet("1-2-1", out var f));
            Assert.AreEqual("1-2-1", f.Id);
            Assert.IsFalse(library.TryGet("nope", out var none));
            Assert.IsNull(none);
            Assert.IsFalse(library.TryGet(null, out _));
        }

        [Test]
        public void Library_RejectsDuplicatesNullAndBadIds_WithoutChangingItself()
        {
            Assert.IsFalse(library.TryAdd(DefaultFormations.CreateTwoTwo()));
            Assert.IsFalse(library.TryAdd(null));
            Assert.IsFalse(library.TryAdd(new FormationDefinition { Id = "with space", Name = "x" }));
            Assert.IsFalse(library.TryAdd(new FormationDefinition { Id = null, Name = "x" }));
            Assert.AreEqual(3, library.Count);
        }

        [Test]
        public void Library_AcceptsNewFormations_AndKeepsInsertionOrder()
        {
            var extra = new FormationDefinition("1-1-2", "1-1-2");
            Assert.IsTrue(library.TryAdd(extra));
            Assert.AreEqual(4, library.Count);
            Assert.AreEqual("1-1-2", library.All[3].Id);
            Assert.AreEqual("2-2", library.All[0].Id);
        }

        // ================= Team =================

        [Test]
        public void Team_Valid_PassesWithAndWithoutLibrary()
        {
            var t = TestData.Team();
            Assert.IsTrue(DataValidator.ValidateTeam(t).IsValid);
            var r = DataValidator.ValidateTeam(t, library);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void Team_Valid_WithEachDefaultFormation()
        {
            // Roles of the sample players do not need to match slot roles; only the keeper alignment matters.
            foreach (string id in new[] { "2-2", "1-2-1", "2-1-1" })
            {
                var r = DataValidator.ValidateTeam(TestData.Team("blue", id), library);
                Assert.IsTrue(r.IsValid, id + ": " + r);
            }
        }

        [Test]
        public void Team_Null_IsReported()
        {
            Assert.IsTrue(DataValidator.ValidateTeam(null).Has(ValidationCode.TeamNull));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(11)]
        public void Team_MustHaveExactlyFivePlayers(int count)
        {
            var t = TestData.Team();
            while (t.Players.Count > count) t.Players.RemoveAt(t.Players.Count - 1);
            for (int i = t.Players.Count; i < count; i++) t.Players.Add(TestData.Player("extra" + i, 20 + i, PlayerRole.Midfielder));
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid), "count " + count);
        }

        [Test]
        public void Team_ExactlyFive_DoesNotRaiseTheCountError()
        {
            Assert.IsFalse(DataValidator.ValidateTeam(TestData.Team()).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [Test]
        public void Team_NullPlayerList_IsReportedAsWrongCount()
        {
            var t = TestData.Team();
            t.Players = null;
            Assert.IsTrue(DataValidator.ValidateTeam(t).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [Test]
        public void Team_NullPlayerEntry_IsReported()
        {
            var t = TestData.Team();
            t.Players[2] = null;
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.PlayerNull));
        }

        [Test]
        public void Team_WithoutGoalkeeper_IsReported()
        {
            var t = TestData.Team();
            t.Players[0].Role = PlayerRole.Defender;
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_WithTwoGoalkeepers_IsReported()
        {
            var t = TestData.Team();
            t.Players[1].Role = PlayerRole.Goalkeeper;
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_DuplicatePlayerId_IsReported_WithTheIdInTheMessage()
        {
            var t = TestData.Team();
            t.Players[3].Id = t.Players[1].Id;
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.TeamDuplicatePlayerId));
            var issue = r.Issues.First(i => i.Code == ValidationCode.TeamDuplicatePlayerId);
            StringAssert.Contains("blue-d1", issue.Subject);
        }

        [Test]
        public void Team_DuplicateShirtNumber_IsReported()
        {
            var t = TestData.Team();
            t.Players[4].Number = t.Players[0].Number;
            var r = DataValidator.ValidateTeam(t);
            Assert.AreEqual(1, r.CountOf(ValidationCode.TeamDuplicateShirtNumber));
        }

        [Test]
        public void Team_InvalidPlayer_IsReportedUnderTheTeam()
        {
            var t = TestData.Team();
            t.Players[1].Attributes = TestData.WithAttribute(2, 150); // Stamina
            var r = DataValidator.ValidateTeam(t);
            Assert.AreEqual(1, r.Count, r.ToString());
            var issue = r.Issues[0];
            Assert.AreEqual(ValidationCode.PlayerAttributeOutOfRange, issue.Code);
            StringAssert.Contains("blue", issue.Subject);
            StringAssert.Contains("blue-d1", issue.Subject);
            StringAssert.Contains("Stamina", issue.Message);
        }

        [Test]
        public void Team_BadIdAndName_AreReported()
        {
            var t = TestData.Team();
            t.Id = "";
            t.Name = "  ";
            var r = DataValidator.ValidateTeam(t);
            Assert.IsTrue(r.Has(ValidationCode.TeamIdInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamNameInvalid));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        public void Team_MissingOrInvalidFormationId_IsReported(string formationId)
        {
            var t = TestData.Team();
            t.FormationId = formationId;
            Assert.IsTrue(DataValidator.ValidateTeam(t).Has(ValidationCode.TeamFormationIdInvalid));
        }

        [Test]
        public void Team_UnknownFormation_IsReportedOnlyWhenALibraryIsGiven()
        {
            var t = TestData.Team("blue", "3-1");
            Assert.IsFalse(DataValidator.ValidateTeam(t).Has(ValidationCode.TeamFormationNotFound));
            Assert.IsTrue(DataValidator.ValidateTeam(t, library).Has(ValidationCode.TeamFormationNotFound));
        }

        [Test]
        public void Team_KeeperNotWhereTheFormationExpectsIt_IsReported()
        {
            var t = TestData.Team();
            // Keeper moved to lineup index 2: the default formations put the keeper slot on index 0.
            var keeper = t.Players[0];
            t.Players[0] = t.Players[2];
            t.Players[2] = keeper;
            var r = DataValidator.ValidateTeam(t, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamFormationGoalkeeperMismatch), r.ToString());
            Assert.AreEqual(2, t.GoalkeeperIndex);
        }

        [Test]
        public void Team_KeeperAtAnotherIndex_IsFineWithAFormationThatExpectsIt()
        {
            var custom = new FormationDefinition("custom", "Custom",
                new FormationPosition(0, PlayerRole.Defender, 0.3f, 0.3f),
                new FormationPosition(1, PlayerRole.Defender, 0.3f, 0.7f),
                new FormationPosition(2, PlayerRole.Goalkeeper, 0.06f, 0.5f),
                new FormationPosition(3, PlayerRole.Forward, 0.7f, 0.3f),
                new FormationPosition(4, PlayerRole.Forward, 0.7f, 0.7f));
            library.TryAdd(custom);

            var t = TestData.Team("blue", "custom");
            var keeper = t.Players[0];
            t.Players[0] = t.Players[2];
            t.Players[2] = keeper;
            var r = DataValidator.ValidateTeam(t, library);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void Team_UsingABrokenFormation_SurfacesTheFormationErrors()
        {
            library.TryAdd(new FormationDefinition("broken", "Broken", new FormationPosition(0, PlayerRole.Goalkeeper, 0.1f, 0.5f)));
            var r = DataValidator.ValidateTeam(TestData.Team("blue", "broken"), library);
            Assert.IsTrue(r.Has(ValidationCode.FormationPositionCountInvalid));
            StringAssert.Contains("blue", r.Issues.First(i => i.Code == ValidationCode.FormationPositionCountInvalid).Subject);
        }

        [Test]
        public void Team_KeeperIndexHelper_IsMinusOneWithoutAKeeper()
        {
            var t = TestData.Team();
            t.Players[0].Role = PlayerRole.Midfielder;
            Assert.AreEqual(-1, t.GoalkeeperIndex);
        }

        [Test]
        public void Team_ManyProblems_AreAllCollected()
        {
            var t = TestData.Team();
            t.Players.RemoveAt(4);                         // 4 players
            t.Players[0].Role = PlayerRole.Forward;        // no keeper
            t.Players[2].Number = t.Players[1].Number;     // duplicate number
            t.FormationId = "";                            // no formation
            var r = DataValidator.ValidateTeam(t, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamDuplicateShirtNumber));
            Assert.IsTrue(r.Has(ValidationCode.TeamFormationIdInvalid));
        }

        // ================= Validation result =================

        [Test]
        public void Result_ReadsClearly_AndIsEmptyWhenValid()
        {
            Assert.AreEqual("valid", new ValidationResult().ToString());

            var t = TestData.Team();
            t.Players[0].Role = PlayerRole.Defender;
            string text = DataValidator.ValidateTeam(t, library).ToString();
            StringAssert.Contains("TeamGoalkeeperCountInvalid", text);
            StringAssert.Contains("team 'blue'", text);
        }

        [Test]
        public void Result_Merge_AndCountOf()
        {
            var a = new ValidationResult();
            a.Add(ValidationCode.TeamNull, "t", "m");
            var b = new ValidationResult();
            b.Add(ValidationCode.TeamNull, "t2", "m");
            b.Add(ValidationCode.PlayerNull, "p", "m");
            a.Merge(b);
            a.Merge(null);
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(2, a.CountOf(ValidationCode.TeamNull));
            Assert.IsFalse(a.IsValid);
        }

        // ================= Match pairing =================

        [Test]
        public void Match_TwoValidDistinctTeams_AreAccepted()
        {
            var r = DataValidator.ValidateMatchTeams(TestData.Team("blue"), TestData.Team("red", DefaultFormations.Diamond), library);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void Match_SameTeamId_IsReported()
        {
            var r = DataValidator.ValidateMatchTeams(TestData.Team("blue"), TestData.Team("blue"), library);
            Assert.IsTrue(r.Has(ValidationCode.MatchSameTeam));
        }

        [Test]
        public void Match_SharedPlayerIdBetweenTeams_IsReported()
        {
            var home = TestData.Team("blue");
            var away = TestData.Team("red");
            away.Players[2].Id = home.Players[2].Id;
            var r = DataValidator.ValidateMatchTeams(home, away, library);
            Assert.AreEqual(1, r.CountOf(ValidationCode.MatchDuplicatePlayerId));
        }

        [Test]
        public void Match_NullTeam_IsReportedWithoutCrashing()
        {
            var r = DataValidator.ValidateMatchTeams(TestData.Team("blue"), null, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamNull));
            r = DataValidator.ValidateMatchTeams(null, null, library);
            Assert.AreEqual(2, r.CountOf(ValidationCode.TeamNull));
        }

        [Test]
        public void Match_ProblemsOfBothTeams_AreMerged()
        {
            var home = TestData.Team("blue");
            var away = TestData.Team("red");
            home.Players[0].Role = PlayerRole.Defender;
            away.Players.RemoveAt(0);
            var r = DataValidator.ValidateMatchTeams(home, away, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid));
        }

        // ================= Architecture guards =================

        private static readonly Type[] DataTypes =
        {
            typeof(PlayerDefinition), typeof(TeamDefinition), typeof(FormationDefinition), typeof(FormationPosition),
            typeof(TeamColors), typeof(ColorRgb), typeof(PlayerRole), typeof(FormationLibrary), typeof(DefaultFormations),
            typeof(DataValidator), typeof(DataRules), typeof(ValidationResult), typeof(ValidationIssue), typeof(ValidationCode)
        };

        [Test]
        public void Core_DoesNotReferenceUnity()
        {
            var refs = typeof(PlayerDefinition).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(refs.Any(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor")), string.Join(", ", refs));
        }

        [Test]
        public void DataTypes_HaveNoSprintMember_SprintStaysAJoystickIntensityMatter()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type t in DataTypes)
            {
                Assert.IsFalse(t.Name.ToLowerInvariant().Contains("sprint"), t.Name);
                foreach (MemberInfo m in t.GetMembers(all))
                    Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name);
            }
        }

        [Test]
        public void AuthoredDataTypes_AreSerializable_SoUnityAssetsCanWrapThem()
        {
            foreach (Type t in new[] { typeof(PlayerDefinition), typeof(TeamDefinition), typeof(FormationDefinition), typeof(FormationPosition), typeof(TeamColors), typeof(ColorRgb) })
                Assert.IsTrue(t.IsSerializable, t.Name);
        }

        [Test]
        public void PlayerAttributes_StillHasTheNineExpectedAttributes()
        {
            var names = typeof(PlayerAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(TestData.AttributeNames, names);
        }

        [Test]
        public void Rules_Constants_Describe5v5()
        {
            Assert.AreEqual(5, DataRules.PlayersPerTeam);
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(1, PlayerAttributes.Min);
            Assert.AreEqual(99, PlayerAttributes.Max);
        }
    }
}
