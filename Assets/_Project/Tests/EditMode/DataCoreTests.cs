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
        /// <summary>All 12 core attributes plus the legacy Reaction set to the same value.</summary>
        public static PlayerAttributes Attrs(int value = 70)
        {
            return new PlayerAttributes
            {
                Speed = value, Acceleration = value, Stamina = value, BallControl = value, Passing = value,
                Shooting = value, Defense = value, Strength = value, Reaction = value,
                Agility = value, Finishing = value, Dribbling = value, Technique = value
            };
        }

        /// <summary>Attributes at 70 except the one at <paramref name="index"/> (order of <see cref="AttributeNames"/>).</summary>
        public static PlayerAttributes WithAttribute(int index, int value)
        {
            int[] v = Enumerable.Repeat(70, 13).ToArray();
            v[index] = value;
            return new PlayerAttributes
            {
                Speed = v[0], Acceleration = v[1], Stamina = v[2], BallControl = v[3], Passing = v[4],
                Shooting = v[5], Defense = v[6], Strength = v[7], Reaction = v[8],
                Agility = v[9], Finishing = v[10], Dribbling = v[11], Technique = v[12]
            };
        }

        public static readonly string[] AttributeNames =
        {
            "Speed", "Acceleration", "Stamina", "BallControl", "Passing", "Shooting", "Defense", "Strength", "Reaction",
            "Agility", "Finishing", "Dribbling", "Technique"
        };

        public static PlayerDefinition Player(string id, int number, PlayerRole role, int attr = 70)
        {
            return new PlayerDefinition(id, "Generic " + id, number, role, Attrs(attr));
        }

        /// <summary>
        /// A valid 6v6 team: goalkeeper first, as the default formations expect, then 2 defenders, 1 midfielder and 2 forwards.
        /// The 6 players are added to <paramref name="library"/>; the team itself holds only their ids.
        /// </summary>
        public static TeamDefinition Team(PlayerLibrary library, string prefix = "blue", string formationId = DefaultFormations.TwoOneTwo)
        {
            var team = new TeamDefinition(prefix, "Team " + prefix,
                new TeamColors(new ColorRgb(20, 80, 200), new ColorRgb(255, 255, 255)),
                formationId,
                prefix + "-gk", prefix + "-d1", prefix + "-d2", prefix + "-m1", prefix + "-f1", prefix + "-f2");
            library.TryAdd(Player(prefix + "-gk", 1, PlayerRole.Goalkeeper));
            library.TryAddGoalkeeperProfile(Keeper(70, prefix + "-gk"));
            library.TryAdd(Player(prefix + "-d1", 2, PlayerRole.Defender));
            library.TryAdd(Player(prefix + "-d2", 3, PlayerRole.Defender));
            library.TryAdd(Player(prefix + "-m1", 8, PlayerRole.Midfielder));
            library.TryAdd(Player(prefix + "-f1", 9, PlayerRole.Forward));
            library.TryAdd(Player(prefix + "-f2", 10, PlayerRole.Forward));
            return team;
        }

        /// <summary>A goalkeeper profile with every capability at <paramref name="value"/>.</summary>
        public static GoalkeeperProfile Keeper(int value, string playerId = "gk")
        {
            return new GoalkeeperProfile(playerId, GoalkeeperStyle.ShotStopper, value, value, value, value, value, value, value, value);
        }

        /// <summary>The library's player at a lineup index of the team.</summary>
        public static PlayerDefinition At(PlayerLibrary library, TeamDefinition team, int index)
        {
            library.TryGet(team.PlayerIds[index], out PlayerDefinition p);
            return p;
        }
    }

    public class DataCoreTests
    {
        private FormationLibrary library;
        private PlayerLibrary players;

        [SetUp]
        public void SetUp()
        {
            library = FormationLibrary.CreateDefault();
            players = new PlayerLibrary();
        }

        private TeamDefinition NewTeam(string prefix = "blue", string formationId = DefaultFormations.TwoOneTwo)
        {
            return TestData.Team(players, prefix, formationId);
        }

        private PlayerDefinition At(TeamDefinition team, int index)
        {
            return TestData.At(players, team, index);
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
        public void Player_EveryAttribute_IsRangeChecked([Range(0, 12)] int index, [Values(-1, 0, 100, 250)] int bad)
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
        public void Player_AttributeBoundaries_AreAccepted([Range(0, 12)] int index, [Values(1, 99)] int edge)
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
        public void DefaultFormations_AreAllValidFor6v6()
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
            CollectionAssert.AreEquivalent(new[] { "2-1-2", "1-2-2", "2-2-1" }, all.Select(f => f.Id).ToArray());
            Assert.AreEqual(3, all.Select(f => f.Id).Distinct().Count());
            Assert.AreEqual(3, all.Select(f => f.Name).Distinct().Count());
        }

        [Test]
        public void DefaultFormations_HaveOneKeeperAtSlotZero_AndFiveFieldPlayers()
        {
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
            {
                Assert.AreEqual(DataRules.PlayersPerTeam, f.Positions.Count, f.Id);
                Assert.AreEqual(6, f.Positions.Count, f.Id);
                Assert.AreEqual(5, f.Positions.Count(p => p.Role != PlayerRole.Goalkeeper), f.Id);
                Assert.AreEqual(1, f.Positions.Count(p => p.Role == PlayerRole.Goalkeeper), f.Id);
                Assert.AreEqual(0, f.GoalkeeperIndex, f.Id);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, f.Positions.Select(p => p.PlayerIndex).ToArray(), f.Id);
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
        public void DefaultFormations_ZonesMatchTheirNames()
        {
            var zones = DefaultFormations.CreateAll().ToDictionary(f => f.Id, f => f.Positions.Select(p => p.Zone).Where(z => z != PitchZone.Goal).ToArray());
            Assert.AreEqual(2, zones["2-1-2"].Count(z => z == PitchZone.Defense));
            Assert.AreEqual(1, zones["2-1-2"].Count(z => z == PitchZone.Midfield));
            Assert.AreEqual(1, zones["2-1-2"].Count(z => z == PitchZone.Wing));
            Assert.AreEqual(1, zones["2-1-2"].Count(z => z == PitchZone.Attack));
            Assert.AreEqual(1, zones["1-2-2"].Count(z => z == PitchZone.Defense));
            Assert.AreEqual(2, zones["1-2-2"].Count(z => z == PitchZone.Midfield));
            Assert.AreEqual(1, zones["1-2-2"].Count(z => z == PitchZone.Wing));
            Assert.AreEqual(1, zones["1-2-2"].Count(z => z == PitchZone.Attack));
            Assert.AreEqual(2, zones["2-2-1"].Count(z => z == PitchZone.Defense));
            Assert.AreEqual(2, zones["2-2-1"].Count(z => z == PitchZone.Midfield));
            Assert.AreEqual(1, zones["2-2-1"].Count(z => z == PitchZone.Attack));
        }

        [Test]
        public void DefaultFormations_EachSlotZoneAgreesWithWhereItSits()
        {
            foreach (FormationDefinition f in DefaultFormations.CreateAll())
                foreach (FormationPosition p in f.Positions)
                    Assert.AreEqual(PlayingProfileDefaults.ZoneOfRelativePosition(p.Relative), p.Zone, f.Id + " slot " + p.PlayerIndex);
        }

        [Test]
        public void DefaultFormations_AreFreshInstancesEachCall()
        {
            var a = DefaultFormations.CreateOneTwoTwo();
            a.Positions.Clear();
            Assert.AreEqual(6, DefaultFormations.CreateOneTwoTwo().Positions.Count);
        }

        private static FormationDefinition Good()
        {
            return DefaultFormations.CreateTwoOneTwo();
        }

        [Test]
        public void Formation_Null_IsReported()
        {
            Assert.IsTrue(DataValidator.ValidateFormation(null).Has(ValidationCode.FormationNull));
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(7)]
        public void Formation_WrongSlotCount_IsNot6v6(int count)
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
        [TestCase(6)]
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
        public void Formation_ExactlyFiveSlots_IsTheOldFiveAPlayerShapeAndIsRejected()
        {
            var f = Good();
            f.Positions.RemoveAt(5);
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationPositionCountInvalid));
        }

        [Test]
        public void Formation_UndefinedZone_IsReported()
        {
            var f = Good();
            var p = f.Positions[2]; p.Zone = (PitchZone)42; f.Positions[2] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationZoneInvalid));
        }

        [Test]
        public void Formation_FieldPlayerInTheGoalZone_AndKeeperOutsideIt_AreReported()
        {
            var f = Good();
            var p = f.Positions[2]; p.Zone = PitchZone.Goal; f.Positions[2] = p;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationZoneInvalid));

            f = Good();
            var k = f.Positions[0]; k.Zone = PitchZone.Defense; f.Positions[0] = k;
            Assert.IsTrue(DataValidator.ValidateFormation(f).Has(ValidationCode.FormationZoneInvalid));
        }

        [Test]
        public void FormationPosition_WithoutAZone_TakesItFromTheBroadRole()
        {
            Assert.AreEqual(PitchZone.Goal, new FormationPosition(0, PlayerRole.Goalkeeper, 0.1f, 0.5f).Zone);
            Assert.AreEqual(PitchZone.Defense, new FormationPosition(1, PlayerRole.Defender, 0.3f, 0.5f).Zone);
            Assert.AreEqual(PitchZone.Midfield, new FormationPosition(2, PlayerRole.Midfielder, 0.5f, 0.5f).Zone);
            Assert.AreEqual(PitchZone.Attack, new FormationPosition(3, PlayerRole.Forward, 0.8f, 0.5f).Zone);
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
            var f = DefaultFormations.CreateOneTwoTwo();
            Assert.IsTrue(f.TryGetByPlayerIndex(5, out var fwd));
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
            var keeper = DefaultFormations.CreateTwoOneTwo().Positions[0];
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
            Assert.IsTrue(library.Contains("2-1-2"));
            Assert.IsTrue(library.Contains("1-2-2"));
            Assert.IsTrue(library.Contains("2-2-1"));
            Assert.IsFalse(library.Contains("3-1"));
            Assert.IsFalse(library.Contains(null));
        }

        [Test]
        public void Library_TryGet_ReturnsTheFormation()
        {
            Assert.IsTrue(library.TryGet("1-2-2", out var f));
            Assert.AreEqual("1-2-2", f.Id);
            Assert.IsFalse(library.TryGet("nope", out var none));
            Assert.IsNull(none);
            Assert.IsFalse(library.TryGet(null, out _));
        }

        [Test]
        public void Library_RejectsDuplicatesNullAndBadIds_WithoutChangingItself()
        {
            Assert.IsFalse(library.TryAdd(DefaultFormations.CreateTwoOneTwo()));
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
            Assert.AreEqual("2-1-2", library.All[0].Id);
        }

        // ================= Team (6v6: 1 goalkeeper + 5 field players, referenced by id) =================

        [Test]
        public void Team_Valid_6Players_1Keeper_5FieldPlayers_Passes()
        {
            var t = NewTeam();
            var r = DataValidator.ValidateTeam(t, players, library);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(6, t.PlayerIds.Count);
            Assert.AreEqual(1, t.PlayerIds.Count(id => At(t, t.PlayerIds.IndexOf(id)).Role == PlayerRole.Goalkeeper));
            Assert.AreEqual(5, t.PlayerIds.Count(id => At(t, t.PlayerIds.IndexOf(id)).Role != PlayerRole.Goalkeeper));
        }

        [Test]
        public void Team_Valid_WithEachDefaultFormation()
        {
            // Roles of the sample players do not need to match slot zones; only the keeper alignment matters.
            foreach (string id in new[] { "2-1-2", "1-2-2", "2-2-1" })
            {
                var r = DataValidator.ValidateTeam(NewTeam("t" + id, id), players, library);
                Assert.IsTrue(r.IsValid, id + ": " + r);
            }
        }

        [Test]
        public void Team_Null_IsReported()
        {
            Assert.IsTrue(DataValidator.ValidateTeam(null, players).Has(ValidationCode.TeamNull));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(11)]
        public void Team_MustHaveExactlySixPlayers(int count)
        {
            var t = NewTeam();
            while (t.PlayerIds.Count > count) t.PlayerIds.RemoveAt(t.PlayerIds.Count - 1);
            for (int i = t.PlayerIds.Count; i < count; i++)
            {
                players.TryAdd(TestData.Player("extra" + i, 20 + i, PlayerRole.Midfielder));
                t.PlayerIds.Add("extra" + i);
            }
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid), "count " + count);
        }

        [Test]
        public void Team_FivePlayers_IsInvalid_TheOldRuleIsGone()
        {
            var t = NewTeam();
            t.PlayerIds.RemoveAt(5);
            Assert.AreEqual(5, t.PlayerIds.Count);
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [Test]
        public void Team_SevenPlayers_IsInvalid()
        {
            var t = NewTeam();
            players.TryAdd(TestData.Player("blue-extra", 11, PlayerRole.Forward));
            t.PlayerIds.Add("blue-extra");
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [Test]
        public void Team_ExactlySix_DoesNotRaiseTheCountError()
        {
            Assert.IsFalse(DataValidator.ValidateTeam(NewTeam(), players).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [Test]
        public void Team_NullPlayerIdList_IsReportedAsWrongCount()
        {
            var t = NewTeam();
            t.PlayerIds = null;
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamPlayerCountInvalid));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        public void Team_InvalidPlayerId_IsReported(string id)
        {
            var t = NewTeam();
            t.PlayerIds[2] = id;
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamPlayerIdInvalid));
        }

        [Test]
        public void Team_PlayerNotInTheLibrary_IsReportedWithTheId()
        {
            var t = NewTeam();
            t.PlayerIds[3] = "ghost";
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerNotFound));
            StringAssert.Contains("ghost", r.Issues.First(i => i.Code == ValidationCode.TeamPlayerNotFound).Subject);
        }

        [Test]
        public void Team_WithoutAPlayerLibrary_CannotBeChecked()
        {
            var r = DataValidator.ValidateTeam(NewTeam(), null);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerLookupMissing));
        }

        [Test]
        public void Team_WithoutGoalkeeper_ZeroKeepers_IsReported()
        {
            var t = NewTeam();
            At(t, 0).Role = PlayerRole.Defender;
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_WithTwoGoalkeepers_IsReported()
        {
            var t = NewTeam();
            At(t, 1).Role = PlayerRole.Goalkeeper;
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
        }

        [Test]
        public void Team_DuplicatePlayerId_IsReported_WithTheIdInTheMessage()
        {
            var t = NewTeam();
            t.PlayerIds[3] = t.PlayerIds[1];
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamDuplicatePlayerId));
            var issue = r.Issues.First(i => i.Code == ValidationCode.TeamDuplicatePlayerId);
            StringAssert.Contains("blue-d1", issue.Subject);
        }

        [Test]
        public void Team_DuplicateShirtNumber_IsReported()
        {
            var t = NewTeam();
            At(t, 4).Number = At(t, 0).Number;
            var r = DataValidator.ValidateTeam(t, players);
            Assert.AreEqual(1, r.CountOf(ValidationCode.TeamDuplicateShirtNumber));
        }

        [Test]
        public void Team_InvalidPlayer_IsReportedUnderTheTeam()
        {
            var t = NewTeam();
            At(t, 1).Attributes = TestData.WithAttribute(2, 150); // Stamina
            var r = DataValidator.ValidateTeam(t, players);
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
            var t = NewTeam();
            t.Id = "";
            t.Name = "  ";
            var r = DataValidator.ValidateTeam(t, players);
            Assert.IsTrue(r.Has(ValidationCode.TeamIdInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamNameInvalid));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("has space")]
        public void Team_MissingOrInvalidFormationId_IsReported(string formationId)
        {
            var t = NewTeam();
            t.FormationId = formationId;
            Assert.IsTrue(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamFormationIdInvalid));
        }

        [Test]
        public void Team_UnknownFormation_IsReportedOnlyWhenAFormationLibraryIsGiven()
        {
            var t = NewTeam("blue", "3-1");
            Assert.IsFalse(DataValidator.ValidateTeam(t, players).Has(ValidationCode.TeamFormationNotFound));
            Assert.IsTrue(DataValidator.ValidateTeam(t, players, library).Has(ValidationCode.TeamFormationNotFound));
        }

        [Test]
        public void Team_KeeperNotWhereTheFormationExpectsIt_IsReported()
        {
            var t = NewTeam();
            // Keeper moved to lineup index 2: the default formations put the keeper slot on index 0.
            (t.PlayerIds[0], t.PlayerIds[2]) = (t.PlayerIds[2], t.PlayerIds[0]);
            var r = DataValidator.ValidateTeam(t, players, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamFormationGoalkeeperMismatch), r.ToString());
            Assert.AreEqual(2, t.GoalkeeperIndex(players));
        }

        [Test]
        public void Team_KeeperAtAnotherIndex_IsFineWithAFormationThatExpectsIt()
        {
            var custom = new FormationDefinition("custom", "Custom",
                new FormationPosition(0, PlayerRole.Defender, 0.3f, 0.3f),
                new FormationPosition(1, PlayerRole.Defender, 0.3f, 0.7f),
                new FormationPosition(2, PlayerRole.Goalkeeper, 0.06f, 0.5f),
                new FormationPosition(3, PlayerRole.Midfielder, 0.5f, 0.5f),
                new FormationPosition(4, PlayerRole.Forward, 0.7f, 0.3f),
                new FormationPosition(5, PlayerRole.Forward, 0.7f, 0.7f));
            library.TryAdd(custom);

            var t = NewTeam("blue", "custom");
            (t.PlayerIds[0], t.PlayerIds[2]) = (t.PlayerIds[2], t.PlayerIds[0]);
            var r = DataValidator.ValidateTeam(t, players, library);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void Team_UsingABrokenFormation_SurfacesTheFormationErrors()
        {
            library.TryAdd(new FormationDefinition("broken", "Broken", new FormationPosition(0, PlayerRole.Goalkeeper, 0.1f, 0.5f)));
            var r = DataValidator.ValidateTeam(NewTeam("blue", "broken"), players, library);
            Assert.IsTrue(r.Has(ValidationCode.FormationPositionCountInvalid));
            StringAssert.Contains("blue", r.Issues.First(i => i.Code == ValidationCode.FormationPositionCountInvalid).Subject);
        }

        [Test]
        public void Team_AFiveSlotFormation_IsRejectedForATeam()
        {
            var five = DefaultFormations.CreateTwoOneTwo();
            five.Id = "old-five";
            five.Positions.RemoveAt(5);
            library.TryAdd(five);
            var r = DataValidator.ValidateTeam(NewTeam("blue", "old-five"), players, library);
            Assert.IsTrue(r.Has(ValidationCode.FormationPositionCountInvalid));
        }

        [Test]
        public void Team_KeeperIndexHelper_IsMinusOneWithoutAKeeper_OrWithoutALibrary()
        {
            var t = NewTeam();
            Assert.AreEqual(0, t.GoalkeeperIndex(players));
            Assert.AreEqual(-1, t.GoalkeeperIndex(null));
            At(t, 0).Role = PlayerRole.Midfielder;
            Assert.AreEqual(-1, t.GoalkeeperIndex(players));
        }

        [Test]
        public void Team_ManyProblems_AreAllCollected()
        {
            var t = NewTeam();
            t.PlayerIds.RemoveAt(5);                       // 5 players
            At(t, 0).Role = PlayerRole.Forward;            // no keeper
            At(t, 2).Number = At(t, 1).Number;             // duplicate number
            t.FormationId = "";                            // no formation
            var r = DataValidator.ValidateTeam(t, players, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamDuplicateShirtNumber));
            Assert.IsTrue(r.Has(ValidationCode.TeamFormationIdInvalid));
        }

        [Test]
        public void Team_HoldsOnlyIds_NoPlayerCopies()
        {
            var fields = typeof(TeamDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsFalse(fields.Any(f => typeof(PlayerDefinition).IsAssignableFrom(f.FieldType)
                                         || (f.FieldType.IsGenericType && f.FieldType.GetGenericArguments().Any(a => typeof(PlayerDefinition).IsAssignableFrom(a)))));
            Assert.AreEqual(typeof(System.Collections.Generic.List<string>), typeof(TeamDefinition).GetField("PlayerIds").FieldType);
        }

        // ================= Validation result =================

        [Test]
        public void Result_ReadsClearly_AndIsEmptyWhenValid()
        {
            Assert.AreEqual("valid", new ValidationResult().ToString());

            var t = NewTeam();
            At(t, 0).Role = PlayerRole.Defender;
            string text = DataValidator.ValidateTeam(t, players, library).ToString();
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
            var r = DataValidator.ValidateMatchTeams(NewTeam("blue"), NewTeam("red", DefaultFormations.OneTwoTwo), players, library);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void Match_SameTeamId_IsReported()
        {
            var home = NewTeam("blue");
            var away = NewTeam("blue2");
            away.Id = "blue";
            var r = DataValidator.ValidateMatchTeams(home, away, players, library);
            Assert.IsTrue(r.Has(ValidationCode.MatchSameTeam));
        }

        [Test]
        public void Match_SharedPlayerIdBetweenTeams_IsReported()
        {
            var home = NewTeam("blue");
            var away = NewTeam("red");
            away.PlayerIds[2] = home.PlayerIds[2];
            var r = DataValidator.ValidateMatchTeams(home, away, players, library);
            Assert.AreEqual(1, r.CountOf(ValidationCode.MatchDuplicatePlayerId));
        }

        [Test]
        public void Match_NullTeam_IsReportedWithoutCrashing()
        {
            var r = DataValidator.ValidateMatchTeams(NewTeam("blue"), null, players, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamNull));
            r = DataValidator.ValidateMatchTeams(null, null, players, library);
            Assert.AreEqual(2, r.CountOf(ValidationCode.TeamNull));
        }

        [Test]
        public void Match_ProblemsOfBothTeams_AreMerged()
        {
            var home = NewTeam("blue");
            var away = NewTeam("red");
            At(home, 0).Role = PlayerRole.Defender;
            away.PlayerIds.RemoveAt(0);
            var r = DataValidator.ValidateMatchTeams(home, away, players, library);
            Assert.IsTrue(r.Has(ValidationCode.TeamGoalkeeperCountInvalid));
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerCountInvalid));
        }

        // ================= Architecture guards =================

        private static readonly Type[] DataTypes =
        {
            typeof(PlayerDefinition), typeof(TeamDefinition), typeof(FormationDefinition), typeof(FormationPosition),
            typeof(TeamColors), typeof(ColorRgb), typeof(PlayerRole), typeof(FormationLibrary), typeof(DefaultFormations),
            typeof(DataValidator), typeof(DataRules), typeof(IPlayerLookup), typeof(ValidationResult), typeof(ValidationIssue), typeof(ValidationCode)
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
        public void PlayerAttributes_HasTheTwelveCoreAttributesPlusTheLegacyReaction_AndNoSecondStamina()
        {
            var names = typeof(PlayerAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(TestData.AttributeNames, names);
            Assert.AreEqual(1, names.Count(n => n.ToLowerInvariant().Contains("stamina")), "one stamina attribute only");
        }

        [Test]
        public void Rules_Constants_Describe6v6()
        {
            Assert.AreEqual(6, DataRules.PlayersPerTeam);
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
            Assert.AreEqual(DataRules.GoalkeepersPerTeam + DataRules.FieldPlayersPerTeam, DataRules.PlayersPerTeam);
            Assert.AreEqual(1, PlayerAttributes.Min);
            Assert.AreEqual(99, PlayerAttributes.Max);
        }
    }
}
