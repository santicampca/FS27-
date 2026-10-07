using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 9: who stands where. The assignment of the starting players to the slots of a formation by role and zone, and the home positions it feeds.</summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController RealRoles(int home, int away, int seed = 1, MatchSettings settings = null)
        {
            MatchController c = RealMatch(home, away, seed, settings);
            c.SetPlayingProfiles(Db.Profiles);
            var assigner = new RolePositionAssigner(Db.Profiles);
            foreach (var pair in new[] { Tuple.Create(TeamSide.Home, home), Tuple.Create(TeamSide.Away, away) })
            {
                TeamDefinition team = Db.Teams[pair.Item2];
                Assert.IsTrue(Db.TryGetFormation(team, out FormationDefinition f)); Assert.IsTrue(Rosters.TryGetRoster(team.Id, out TeamRoster roster));
                Assert.IsTrue(assigner.TryAssign(roster, f, Db.Players, out RoleAssignment a, out string problem), problem);
                c.SetRoleAssignment(pair.Item1, a);
            }
            return c;
        }

        // ---- a small synthetic world: a six-slot formation and players with the roles we choose

        private static FormationDefinition TestFormation()
        {
            return new FormationDefinition("test", "test",
                new FormationPosition(0, PlayerRole.Goalkeeper, 0.06f, 0.5f),
                new FormationPosition(1, PlayerRole.Defender, 0.25f, 0.3f),
                new FormationPosition(2, PlayerRole.Defender, 0.25f, 0.7f),
                new FormationPosition(3, PlayerRole.Midfielder, 0.5f, 0.5f),
                new FormationPosition(4, PlayerRole.Midfielder, 0.6f, 0.08f),
                new FormationPosition(5, PlayerRole.Forward, 0.8f, 0.5f));
        }

        private sealed class Squad6
        {
            public readonly Players Players = new Players();
            public readonly PlayingProfileCatalog Profiles = new PlayingProfileCatalog();
            public readonly List<string> Ids = new List<string>();

            public Squad6 Add(string id, PlayerRole role, PitchZone primary, int attributes, params PlayerArchetype[] roles)
            {
                Players.Defs[id] = new PlayerDefinition(id, id, 1, role, All(attributes));
                if (role != PlayerRole.Goalkeeper) Profiles.TryAdd(new PlayerPlayingProfile(id, primary, null, roles));
                Ids.Add(id);
                return this;
            }
        }

        private static Squad6 Natural()
        {
            return new Squad6().Add("k1", PlayerRole.Goalkeeper, PitchZone.Goal, 70)
                .Add("wall", PlayerRole.Defender, PitchZone.Defense, 70, PlayerArchetype.Wall)
                .Add("guard", PlayerRole.Defender, PitchZone.Defense, 70, PlayerArchetype.Guardian)
                .Add("engine", PlayerRole.Midfielder, PitchZone.Midfield, 70, PlayerArchetype.Engine)
                .Add("winger", PlayerRole.Midfielder, PitchZone.Wing, 70, PlayerArchetype.Winger)
                .Add("finisher", PlayerRole.Forward, PitchZone.Attack, 70, PlayerArchetype.Finisher);
        }

        private static RoleAssignment Assign(Squad6 s, IList<string> order = null, PositioningTuning unused = null)
        {
            var assigner = new RolePositionAssigner(s.Profiles);
            Assert.IsTrue(assigner.TryAssign((IReadOnlyList<string>)(order ?? s.Ids).ToList(), TestFormation(), s.Players, out RoleAssignment a, out string problem), problem);
            return a;
        }

        [Test]
        public void TheKeeperGoesToTheGoalSlot_AndNoFieldPlayerIsEverAKeeper()
        {
            RoleAssignment a = Assign(Natural());
            Assert.AreEqual("k1", a.GoalkeeperId); CollectionAssert.DoesNotContain(a.FieldPlayerIds, "k1"); Assert.AreEqual(5, a.FieldPlayerIds.Count);
            Assert.IsTrue(a.Contains("k1")); Assert.IsFalse(a.Contains("nobody"));
            foreach (TeamDefinition team in Db.Teams)
            {
                Db.TryGetFormation(team, out FormationDefinition f); Rosters.TryGetRoster(team.Id, out TeamRoster roster);
                Assert.IsTrue(new RolePositionAssigner(Db.Profiles).TryAssign(roster, f, Db.Players, out RoleAssignment real, out string problem), problem);
                Assert.AreEqual(roster.ActiveGoalkeeperId, real.GoalkeeperId, team.Id); Assert.AreEqual(PlayerRole.Goalkeeper, Db.Players.TryGet(real.GoalkeeperId, out PlayerDefinition k) ? k.Role : PlayerRole.Forward);
                foreach (string id in real.FieldPlayerIds) { Assert.IsTrue(Db.Players.TryGet(id, out PlayerDefinition d)); Assert.AreNotEqual(PlayerRole.Goalkeeper, d.Role, team.Id); }
            }
        }

        [Test]
        public void TheFiveFieldSlotsAreAllFilled_ByFiveDifferentStarters_ForEveryRealTeam()
        {
            foreach (TeamDefinition team in Db.Teams)
            {
                Db.TryGetFormation(team, out FormationDefinition f); Rosters.TryGetRoster(team.Id, out TeamRoster roster);
                new RolePositionAssigner(Db.Profiles).TryAssign(roster, f, Db.Players, out RoleAssignment a, out _);
                Assert.AreEqual(5, a.FieldPlayerIds.Count, team.Id); Assert.AreEqual(5, a.FieldPlayerIds.Distinct().Count(), team.Id + " a player in two slots"); Assert.AreEqual(5, a.FieldSlots.Select(s => s.PlayerIndex).Distinct().Count());
                CollectionAssert.IsSubsetOf(a.FieldPlayerIds, roster.ActiveIds); CollectionAssert.AreEquivalent(roster.ActiveIds.Where(id => id != a.GoalkeeperId), a.FieldPlayerIds);
                Assert.AreEqual(f.Id, a.FormationId); Assert.IsTrue(a.FieldSlots.All(s => s.Role != PlayerRole.Goalkeeper));
                for (int k = 1; k < a.FieldSlots.Count; k++) Assert.Less(a.FieldSlots[k - 1].PlayerIndex, a.FieldSlots[k].PlayerIndex, "slots in lineup order");
                Assert.IsTrue(a.TryGetSlot(a.FieldPlayerIds[2], out FormationPosition slot)); Assert.AreEqual(a.FieldSlots[2].PlayerIndex, slot.PlayerIndex); Assert.IsFalse(a.TryGetSlot(a.GoalkeeperId, out _));
            }
        }

        [Test]
        public void RolesFavourCompatiblePositions_TheDefendersDefend_TheWingerGoesWide_TheFinisherGoesUp()
        {
            RoleAssignment a = Assign(Natural());
            var at = a.FieldPlayerIds.Select((id, i) => new { id, slot = a.FieldSlots[i].PlayerIndex }).ToDictionary(x => x.id, x => x.slot);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, new[] { at["wall"], at["guard"] }, "the two defensive roles take the two defence slots");
            Assert.AreEqual(4, at["winger"], "the winger takes the wide slot"); Assert.AreEqual(5, at["finisher"]); Assert.AreEqual(3, at["engine"]);
        }

        [Test]
        public void TheOrderOfTheRosterDoesNotMatter_TheSameStartersGiveTheSameAssignment()
        {
            Squad6 s = Natural();
            RoleAssignment baseline = Assign(s);
            foreach (var order in new[] { new[] { "finisher", "k1", "winger", "engine", "guard", "wall" }, new[] { "wall", "guard", "engine", "winger", "finisher", "k1" }, new[] { "engine", "finisher", "wall", "k1", "guard", "winger" } })
            {
                RoleAssignment a = Assign(s, order);
                CollectionAssert.AreEqual(baseline.FieldPlayerIds, a.FieldPlayerIds); Assert.AreEqual(baseline.GoalkeeperId, a.GoalkeeperId);
            }
            RoleAssignment again = Assign(s);
            CollectionAssert.AreEqual(baseline.FieldPlayerIds, again.FieldPlayerIds);
        }

        [Test]
        public void TheZoneOfThePlayerInfluencesTheSlot_WhenTheRolesAreTheSame()
        {
            var s = new Squad6().Add("k1", PlayerRole.Goalkeeper, PitchZone.Goal, 70)
                .Add("a", PlayerRole.Midfielder, PitchZone.Defense, 70, PlayerArchetype.Engine)
                .Add("b", PlayerRole.Midfielder, PitchZone.Defense, 70, PlayerArchetype.Engine)
                .Add("c", PlayerRole.Midfielder, PitchZone.Midfield, 70, PlayerArchetype.Engine)
                .Add("d", PlayerRole.Midfielder, PitchZone.Wing, 70, PlayerArchetype.Engine)
                .Add("e", PlayerRole.Midfielder, PitchZone.Attack, 70, PlayerArchetype.Engine);
            RoleAssignment a = Assign(s);
            var at = a.FieldPlayerIds.Select((id, i) => new { id, slot = a.FieldSlots[i].PlayerIndex }).ToDictionary(x => x.id, x => x.slot);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, new[] { at["a"], at["b"] }, "the two who play in defence take the defence slots");
            Assert.AreEqual(5, at["e"]); Assert.AreEqual(4, at["d"]); Assert.AreEqual(3, at["c"]);
        }

        [Test]
        public void TheOverallIsNeverTheMainCriterion_APerfectFitWithLowOverallBeatsAStrongMisfit()
        {
            var s = new Squad6().Add("k1", PlayerRole.Goalkeeper, PitchZone.Goal, 70)
                .Add("weakwall", PlayerRole.Defender, PitchZone.Defense, 45, PlayerArchetype.Wall)
                .Add("weakguard", PlayerRole.Defender, PitchZone.Defense, 45, PlayerArchetype.Guardian)
                .Add("strongstriker", PlayerRole.Forward, PitchZone.Attack, 95, PlayerArchetype.Finisher)
                .Add("strongstriker2", PlayerRole.Forward, PitchZone.Attack, 95, PlayerArchetype.GoalHunter)
                .Add("mid", PlayerRole.Midfielder, PitchZone.Midfield, 60, PlayerArchetype.Engine);
            RoleAssignment a = Assign(s);
            var at = a.FieldPlayerIds.Select((id, i) => new { id, slot = a.FieldSlots[i].PlayerIndex }).ToDictionary(x => x.id, x => x.slot);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, new[] { at["weakwall"], at["weakguard"] });
            Assert.AreEqual(5, new[] { at["strongstriker"], at["strongstriker2"] }.Max(), "a striker is in the attack slot");
        }

        [Test]
        public void IdenticalPlayers_AreOrderedByTheirIdentifier_NeverByChance()
        {
            var s = new Squad6().Add("k1", PlayerRole.Goalkeeper, PitchZone.Goal, 70);
            foreach (string id in new[] { "zed", "amy", "mia", "bob", "eli" }) s.Add(id, PlayerRole.Midfielder, PitchZone.Midfield, 70, PlayerArchetype.Engine);
            RoleAssignment a = Assign(s);
            CollectionAssert.AreEqual(new[] { "amy", "bob", "eli", "mia", "zed" }, a.FieldPlayerIds, "everything equal: the earlier identifier takes the earlier slot");
            CollectionAssert.AreEqual(a.FieldPlayerIds, Assign(s, new[] { "k1", "zed", "mia", "eli", "bob", "amy" }).FieldPlayerIds);
        }

        [Test]
        public void TheAssignerRefusesWhatItCannotAssign_AndExplainsWhy()
        {
            var assigner = new RolePositionAssigner(null);
            Squad6 s = Natural();
            Assert.IsFalse(assigner.TryAssign(new List<string> { "wall", "guard", "engine", "winger", "finisher", "engine" }, TestFormation(), s.Players, out _, out string p1)); StringAssert.Contains("goalkeeper", p1);
            Assert.IsFalse(assigner.TryAssign(new List<string> { "k1", "wall", "guard" }, TestFormation(), s.Players, out _, out string p2)); StringAssert.Contains("field", p2);
            Assert.IsFalse(assigner.TryAssign(new List<string> { "k1", "ghost", "guard", "engine", "winger", "finisher" }, TestFormation(), s.Players, out _, out string p3)); StringAssert.Contains("ghost", p3);
            Assert.IsFalse(assigner.TryAssign((TeamRoster)null, TestFormation(), s.Players, out _, out string p4)); StringAssert.Contains("required", p4);
            var noKeeperSlot = new FormationDefinition("x", "x", new FormationPosition(1, PlayerRole.Defender, 0.2f, 0.5f));
            Assert.IsFalse(assigner.TryAssign(s.Ids, noKeeperSlot, s.Players, out _, out string p5)); StringAssert.Contains("goalkeeper slot", p5);
            Assert.IsTrue(assigner.TryAssign(s.Ids, TestFormation(), s.Players, out RoleAssignment ok, out _), "players with no profile use the default one derived from their role"); Assert.IsNotNull(ok);
        }

        [Test]
        public void ForTheRealTeams_TheRoleAssignmentFitsTheZonesAtLeastAsWellAsTheSquadOrder_AndTheDrawIsUntouched()
        {
            int assigned = 0, baseline = 0;
            foreach (TeamDefinition team in Db.Teams)
            {
                Db.TryGetFormation(team, out FormationDefinition f); Rosters.TryGetRoster(team.Id, out TeamRoster roster);
                var before = roster.ActiveIds.Concat(roster.BenchIds).ToList();
                new RolePositionAssigner(Db.Profiles).TryAssign(roster, f, Db.Players, out RoleAssignment a, out _);
                CollectionAssert.AreEqual(before, roster.ActiveIds.Concat(roster.BenchIds).ToList(), "the roster is not touched");
                var orderBaseline = roster.ActiveIds.Where(id => id != a.GoalkeeperId).ToList();
                for (int k = 0; k < 5; k++)
                {
                    PitchZone zone = PlayingProfileDefaults.ZoneOfRelativePosition(a.FieldSlots[k].Relative);
                    if (Primary(a.FieldPlayerIds[k]) == zone) assigned++;
                    if (Primary(orderBaseline[k]) == zone) baseline++;
                }
            }
            Assert.GreaterOrEqual(assigned, baseline, "assigned " + assigned + " vs squad order " + baseline);
            RosterGeneration again = RosterGenerator.Generate(7, Db.Players, Db.Profiles, Db.Teams);
            foreach (TeamDefinition team in Db.Teams) { Rosters.TryGetRoster(team.Id, out TeamRoster x); again.TryGetRoster(team.Id, out TeamRoster y); CollectionAssert.AreEqual(x.ActiveIds, y.ActiveIds); CollectionAssert.AreEqual(x.BenchIds, y.BenchIds); }
        }

        private static PitchZone Primary(string id)
        {
            if (Db.Profiles.TryGet(id, out PlayerPlayingProfile p)) return p.PrimaryZone;
            return Db.Players.TryGet(id, out PlayerDefinition d) ? PlayingProfileDefaults.ZoneForRole(d.Role) : PitchZone.Midfield;
        }

        // ================= home positions fed by the assignment =================

        [Test]
        public void TheAssignedPlayersHaveTheirSlotAsHome_AndHomeDoesNotChangeEveryTick()
        {
            MatchController c = RealRoles(0, 1);
            Assert.IsTrue(c.ConfirmRestart());
            WorldState w = WorldState.Capture(c);
            RoleAssignment a = c.GetRoleAssignment(TeamSide.Home);
            for (int i = 0; i < w.PlayerCount; i++) if (w[i].IsOnPitch) Assert.IsTrue(w[i].HasHome, w[i].PlayerId + " has no home");
            Assert.IsTrue(a.TryGetSlot(a.FieldPlayerIds[3], out FormationPosition slot));
            Vec2 expected = slot.ToFieldPosition(c.Field, c.State.HomeAttacksPositiveX);
            WorldPlayer p = w[w.IndexOf(a.FieldPlayerIds[3])];
            Assert.AreEqual(expected.X, p.Home.X, 1.5f); Assert.AreEqual(expected.Y, p.Home.Y, 1.5f, "the home is the slot (nudged only to keep the team apart and in its half)");
            Vec2 before = p.Home;
            for (int i = 0; i < 180; i++) c.Advance(Dt);
            WorldState later = WorldState.Capture(c);
            Assert.AreEqual(before, later[later.IndexOf(p.PlayerId)].Home, "no change without a reason");
            Assert.IsTrue(later[later.IndexOf(a.GoalkeeperId)].HasHome); Assert.IsTrue(later[later.IndexOf(a.GoalkeeperId)].IsGoalkeeper);
            Assert.IsTrue(c.Pitch.IsInGoalZone(later[later.IndexOf(a.GoalkeeperId)].Home, c.State.HomeAttacksPositiveX, TeamSide.Home), "the keeper's home is in his zone");
        }

        [Test]
        public void TheHomeChanges_OnlyWhenTheFormationTheAssignmentTheSidesOrThePlayersChange()
        {
            MatchController c = RealRoles(2, 3);
            Assert.IsTrue(c.ConfirmRestart());
            WorldState w0 = WorldState.Capture(c);
            RoleAssignment a = c.GetRoleAssignment(TeamSide.Home);
            string id = a.FieldPlayerIds[1];
            Vec2 home = w0[w0.IndexOf(id)].Home;
            // a different assignment: the first two field players swap slots
            var swapped = new List<string>(a.FieldPlayerIds); string t0 = swapped[0]; swapped[0] = swapped[1]; swapped[1] = t0;
            c.SetRoleAssignment(TeamSide.Home, new RoleAssignment(a.FormationId, a.GoalkeeperId, swapped, new List<FormationPosition>(a.FieldSlots)));
            WorldState w1 = WorldState.Capture(c);
            Assert.AreNotEqual(home, w1[w1.IndexOf(id)].Home, "reassigned: his home is the other slot");
            // the sides switch: the home turns round
            c.SetRoleAssignment(TeamSide.Home, a);
            Vec2 before = WorldState.Capture(c)[w0.IndexOf(id)].Home;
            c.State.HomeAttacksPositiveX = false;
            WorldState w2 = WorldState.Capture(c);
            Assert.AreEqual(-before.X, w2[w2.IndexOf(id)].Home.X, 1.5f); Assert.AreEqual(-before.Y, w2[w2.IndexOf(id)].Home.Y, 1.5f);
            // a different formation
            c.State.HomeAttacksPositiveX = true;
            Db.TryGetFormation(Db.Teams[3], out FormationDefinition other);
            c.SetFormation(TeamSide.Home, other); c.SetRoleAssignment(TeamSide.Home, null);
            Assert.IsTrue(WorldState.Capture(c)[0].HasHome);
        }

        [Test]
        public void ASubstituteInheritsTheSlotOfThePlayerHeReplaces_AndAReplacementKeeperTakesTheGoalSlot()
        {
            MatchController c = RealRoles(4, 5);
            RoleAssignment a = c.GetRoleAssignment(TeamSide.Home);
            string leaving = a.FieldPlayerIds[2];
            WorldState w0 = WorldState.Capture(c); Vec2 slotHome = w0[w0.IndexOf(leaving)].Home;
            Assert.IsTrue(w0[w0.IndexOf(leaving)].HasHome);
            string incoming = c.State.Home.Players.First(p => p.Status == MatchPlayerStatus.Bench && !p.IsGoalkeeper).PlayerId;
            Assert.AreEqual(SubstitutionResult.Accepted, c.TrySubstitute(TeamSide.Home, leaving, incoming));
            WorldState w1 = WorldState.Capture(c);
            Assert.IsTrue(w1[w1.IndexOf(incoming)].HasHome); Assert.AreEqual(slotHome, w1[w1.IndexOf(incoming)].Home, "he stands where the one he replaced stood");
            Assert.IsFalse(w1[w1.IndexOf(leaving)].HasHome && w1[w1.IndexOf(leaving)].IsOnPitch);
            Assert.IsTrue(c.ConfirmRestart());
            Assert.IsFalse(c.BallCarrier.PlayerId == leaving);

            // the keeper is sent off and a substitute keeper comes on: the goal slot is his
            MatchController d = RealRoles(4, 5, 2);
            string starter = d.State.Home.GoalkeeperInGoal.PlayerId;
            d.IssueCard(TeamSide.Home, starter, CardKind.Red);
            string bench = d.State.Home.AvailableBenchKeeper.PlayerId;
            Assert.AreEqual(SubstitutionResult.Accepted, d.TryReplaceDismissedKeeper(TeamSide.Home, bench, d.GetRoleAssignment(TeamSide.Home).FieldPlayerIds[0]));
            WorldState w2 = WorldState.Capture(d);
            Assert.AreEqual(GoalkeeperSlotMode.Substitute, w2.GoalkeeperSlotOf(TeamSide.Home).Mode);
            Assert.IsTrue(d.Pitch.IsInGoalZone(w2[w2.IndexOf(bench)].Home, d.State.HomeAttacksPositiveX, TeamSide.Home));
            Assert.AreEqual(1, Enumerable.Range(0, w2.PlayerCount).Count(i => w2[i].Team == TeamSide.Home && w2[i].IsOnPitch && w2[i].IsGoalkeeper), "one keeper on the pitch");
            Assert.AreEqual(4, Enumerable.Range(0, w2.PlayerCount).Count(i => w2[i].Team == TeamSide.Home && w2[i].IsOnPitch && !w2[i].IsGoalkeeper && w2[i].HasHome), "a field player made way for the keeper (a team of five): the four left all have a home");
        }

        [Test]
        public void TheHomeController_DrawsTheNonDefendersTowardsTheBall_ButNeverFurtherThanTheMaximumDeviation()
        {
            MatchController c = RealRoles(0, 1);
            Assert.IsTrue(c.ConfirmRestart());
            var tuning = new RolePositioningTuning();
            var engine = new BasicDecisionEngine(c.Pitch);
            foreach (Vec2 ball in new[] { new Vec2(18f, 10f), new Vec2(-18f, -10f), new Vec2(0f, 0f), new Vec2(10f, -8f) })
            {
                PutBall(c, ball.X, ball.Y);
                WorldState w = WorldState.Capture(c);
                int moved = 0;
                foreach (MatchPlayer p in c.State.Home.Players.Where(x => x.IsActive && !x.InGoal))
                {
                    PlayerDecision d = engine.Decide(w.View, p.PlayerId);
                    if (d.Kind != DecisionKind.MoveToPoint && d.Kind != DecisionKind.Idle) continue;
                    WorldPlayer wp = w[w.IndexOf(p.PlayerId)];
                    if (d.HasMovement && d.Movement.HasTarget) { Assert.LessOrEqual((d.Movement.Target - wp.Home).Magnitude, tuning.HomeMaxDeviation + 0.01f, p.PlayerId); moved++; }
                }
                Assert.Greater(moved, 0);
            }
        }
    }
}
