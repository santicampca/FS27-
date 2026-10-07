using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 7: AI INFRASTRUCTURE. Nothing here decides anything: the tests prove that the world can be observed read-only, that formations place twelve players, that restarts have an explicit taker,
    /// and that a person's input and an AI intent both end in the one Core.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private const float Dt = LocomotionDefaults.FixedDeltaTime;
        private static readonly Vec2 East = new Vec2(1f, 0f);
        private static readonly Vec2 West = new Vec2(-1f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static GameDatabase realDb;
        private static GameDatabase Db { get { return realDb ?? (realDb = DataLockTests.LoadRealDatabase()); } }

        // ================= scenery =================

        private sealed class Players : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Defs = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Defs.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private static PlayerAttributes All(int v)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = a.Acceleration = a.Stamina = a.BallControl = a.Passing = a.Shooting = a.Defense = a.Strength = a.Agility = a.Finishing = a.Dribbling = a.Technique = v;
            return a;
        }

        private static SquadMember[] Starters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true, 100f) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false, 100f)); return l.ToArray(); }
        private static SquadMember[] Bench(string prefix) { return new[] { new SquadMember(prefix + "b0", true, 100f), new SquadMember(prefix + "b1", false, 100f), new SquadMember(prefix + "b2", false, 100f), new SquadMember(prefix + "b3", false, 100f) }; }
        private static MatchSquad Squad(string prefix) { return new MatchSquad(Starters(prefix), Bench(prefix)); }

        /// <summary>Synthetic players (ids hs0..hs5, hb0..hb3, as0.., ab0..), every attribute at 80, in a live match with the Phase-7 set-up NOT applied.</summary>
        private static MatchController Live(MatchSettings settings = null, int seed = 1, bool confirm = true)
        {
            var lookup = new Players();
            foreach (string prefix in new[] { "h", "a" })
                foreach (string id in Starters(prefix).Concat(Bench(prefix)).Select(m => m.PlayerId))
                    lookup.Defs[id] = new PlayerDefinition(id, id, 1, id.EndsWith("s0") || id.EndsWith("b0") ? PlayerRole.Goalkeeper : PlayerRole.Midfielder, All(80));
            settings = settings ?? MatchSettings.CreateClassic();
            settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, lookup);
            c.Start();
            if (confirm)
            {
                Park(c);
                Assert.IsTrue(c.ConfirmRestartReady());
            }
            return c;
        }

        private static void Park(MatchController c)
        {
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -16f, -10f + i * 4f), East.ToHeading());
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 16f, -10f + i * 4f), West.ToHeading());
            }
        }

        private static void Put(MatchController c, TeamSide side, string id, float x, float y, Vec2 facing) { Assert.IsTrue(c.PlacePlayer(side, id, new Vec2(x, y), facing.ToHeading())); }

        private static void PutBall(MatchController c, float x, float z, float vx = 0f, float vz = 0f)
        {
            c.State.Ball.Physics = new BallState(new Vec3(x, c.BallParameters.Radius, z), new Vec3(vx, 0f, vz)) { IsGrounded = true };
            c.ReleaseBall();
        }

        private static MatchPlayer Carrier(MatchController c, TeamSide side, string id, float x, float y, Vec2 facing)
        {
            Put(c, side, id, x, y, facing);
            PutBall(c, x + facing.X * 0.8f, y + facing.Y * 0.8f);
            Assert.IsTrue(c.GiveBallTo(side, id));
            return c.State.Team(side).Find(id);
        }

        private static void Step(MatchController c, int n) { for (int i = 0; i < n; i++) c.Advance(Dt); }

        private static RosterGeneration realRosters;
        /// <summary>The real rosters (the draw of the real teams with a fixed seed): each team's 6 starters and 4 substitutes.</summary>
        private static RosterGeneration Rosters { get { return realRosters ?? (realRosters = RosterGenerator.Generate(7, Db.Players, Db.Profiles, Db.Teams)); } }

        private static MatchController RealMatch(int homeTeam, int awayTeam, int seed = 1, MatchSettings settings = null)
        {
            TeamDefinition h = Db.Teams[homeTeam], a = Db.Teams[awayTeam];
            Assert.IsTrue(Db.TryGetFormation(h, out FormationDefinition fh), h.Id); Assert.IsTrue(Db.TryGetFormation(a, out FormationDefinition fa), a.Id);
            Assert.IsTrue(Rosters.TryGetRoster(h.Id, out TeamRoster rh)); Assert.IsTrue(Rosters.TryGetRoster(a.Id, out TeamRoster ra));
            Assert.IsTrue(rh.TryToMatchSquad(Db.Players, out MatchSquad sh, out string problem), problem);
            Assert.IsTrue(ra.TryToMatchSquad(Db.Players, out MatchSquad sa, out problem), problem);
            settings = settings ?? MatchSettings.CreateClassic();
            settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, sh, sa, null, "real", null, Db.Players, null, Db.PlayerSkills, Db.Skills);
            c.SetFormation(TeamSide.Home, fh); c.SetFormation(TeamSide.Away, fa);
            c.Start();
            return c;
        }

        // ================= WorldState =================

        [Test]
        public void WorldState_IsACorrectSnapshotOfTheMatch()
        {
            MatchController c = Live();
            Carrier(c, TeamSide.Home, "hs2", 3f, 1f, East);
            WorldState w = WorldState.Capture(c);
            Assert.AreEqual(GameMode.Classic, w.Match.Mode); Assert.AreEqual(CompetitionMatchType.Normal, w.Match.Competition);
            Assert.AreEqual(MatchPhase.Playing, w.Match.Phase); Assert.AreEqual(MatchPeriod.FirstHalf, w.Match.Period);
            Assert.IsTrue(w.Match.BallLive); Assert.IsTrue(w.Match.HasPossession); Assert.AreEqual(TeamSide.Home, w.Match.PossessingTeam);
            Assert.AreEqual(0, w.Match.HomeGoals); Assert.AreEqual(0, w.Match.AwayGoals); Assert.IsFalse(w.Match.RestartPending);
            Assert.AreEqual(20, w.PlayerCount); Assert.AreEqual(10, w.TeamCount(TeamSide.Home)); Assert.AreEqual(10, w.TeamCount(TeamSide.Away));
            Assert.IsTrue(w.TryGetPlayer("hs2", out WorldPlayer p));
            Assert.IsTrue(p.HasBall); Assert.AreEqual(new Vec2(3f, 1f), p.Position); Assert.AreEqual(TeamSide.Home, p.Team); Assert.AreEqual(PlayerAvailability.OnPitch, p.Availability);
            Assert.AreEqual(100f, p.Stamina); Assert.IsFalse(p.IsGoalkeeper); Assert.IsTrue(p.CanAct);
            Assert.IsTrue(w.TryGetPlayer("hs0", out WorldPlayer gk)); Assert.IsTrue(gk.IsGoalkeeper); Assert.AreEqual(PitchZone.Goal, gk.Zone); Assert.AreEqual(PlayerRole.Goalkeeper, gk.Role);
            Assert.IsTrue(w.TryGetPlayer("hb1", out WorldPlayer bench)); Assert.AreEqual(PlayerAvailability.Bench, bench.Availability); Assert.IsFalse(bench.IsOnPitch);
            Assert.AreEqual(w.Ball.Position, c.State.Ball.Physics.Position); Assert.AreEqual("hs2", w.Ball.PossessorId); Assert.AreEqual(BallMode.Controlled, w.Ball.Mode);
        }

        [Test]
        public void WorldState_CarriesTheBallDirectionHeightAndLastTouch()
        {
            MatchController c = Live();
            PutBall(c, 0f, 0f, 6f, 8f);
            c.State.Ball.Physics = new BallState(new Vec3(0f, 1.5f, 0f), new Vec3(6f, 2f, 8f)) { IsGrounded = false };
            WorldBall b = WorldState.Capture(c).Ball;
            Assert.AreEqual(10f, b.Speed, 0.001f); Assert.AreEqual(0.6f, b.Direction.X, 0.001f); Assert.AreEqual(0.8f, b.Direction.Y, 0.001f);
            Assert.AreEqual(1.5f - c.BallParameters.Radius, b.Height, 0.001f); Assert.IsFalse(b.IsGrounded); Assert.IsFalse(b.HasPossessor);
            c.State.Ball.Physics = new BallState(new Vec3(0f, c.BallParameters.Radius, 0f), Vec3.Zero) { IsGrounded = true };
            Assert.AreEqual(Vec2.Zero, WorldState.Capture(c).Ball.Direction, "a ball at rest has no direction");
        }

        [Test]
        public void WorldState_HoldsNoDefinitionsAndNoAttributes()
        {
            foreach (Type t in new[] { typeof(WorldPlayer), typeof(WorldBall), typeof(WorldMatch), typeof(WorldState), typeof(PlayerObservation), typeof(WorldView) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, t.Name + "." + f.Name);
                    Assert.AreNotEqual(typeof(PlayerDefinition), f.FieldType, t.Name + "." + f.Name);
                    Assert.AreNotEqual(typeof(MatchState), f.FieldType, t.Name + "." + f.Name + " must not point at the match");
                    Assert.AreNotEqual(typeof(MatchPlayer), f.FieldType, t.Name + "." + f.Name); Assert.AreNotEqual(typeof(MatchBall), f.FieldType, t.Name + "." + f.Name);
                    Assert.AreNotEqual(typeof(MatchController), f.FieldType, t.Name + "." + f.Name);
                }
        }

        [Test]
        public void WorldState_IsACopy_ChangingItNeverChangesTheMatch_AndCapturingNeverChangesTheMatch()
        {
            MatchController c = Live();
            Carrier(c, TeamSide.Home, "hs2", 3f, 1f, East);
            string before = Fingerprint(c);
            WorldState w = WorldState.Capture(c);
            WorldPlayer copy = w[w.IndexOf("hs2")];
            copy.Position = new Vec2(99f, 99f); copy.Stamina = 0f;
            WorldBall ball = w.Ball; ball.Position = new Vec3(50f, 50f, 50f);
            Assert.AreEqual(before, Fingerprint(c));
            Assert.AreEqual(new Vec2(3f, 1f), w[w.IndexOf("hs2")].Position, "the snapshot itself is untouched by edits of a copy");
            for (int i = 0; i < 20; i++) WorldState.Capture(c);
            Assert.AreEqual(before, Fingerprint(c), "capturing is a pure read");
        }

        private static string Fingerprint(MatchController c)
        {
            var parts = new List<string>();
            foreach (TeamMatchState t in new[] { c.State.Home, c.State.Away })
                foreach (MatchPlayer p in t.Players)
                    parts.Add(p.PlayerId + ":" + p.Locomotion.Position.X.ToString("R") + "," + p.Locomotion.Position.Y.ToString("R") + "," + p.Locomotion.CurrentStamina.ToString("R") + "," + p.Locomotion.Heading.ToString("R") + "," + p.Status);
            Vec3 b = c.State.Ball.Physics.Position;
            parts.Add(b.X.ToString("R") + "," + b.Y.ToString("R") + "," + b.Z.ToString("R") + "," + c.State.Ball.HasPossession + c.State.Ball.PossessingPlayerId + c.State.Phase + c.State.MatchTimeSeconds.ToString("R"));
            return string.Join("|", parts);
        }

        [Test]
        public void WorldState_RefreshFollowsTheMatch_AndACloneDoesNot()
        {
            MatchController c = Live();
            var w = new WorldState(); w.Refresh(c);
            WorldState frozen = w.Clone();
            Put(c, TeamSide.Home, "hs3", 5f, 5f, North); Tire(c, "hs3", 12f); c.IssueCard(TeamSide.Home, "hs4", CardKind.Red);
            w.Refresh(c);
            WorldPlayer p = w[w.IndexOf("hs3")];
            Assert.AreEqual(new Vec2(5f, 5f), p.Position); Assert.AreEqual(12f, p.Stamina, 0.001f); Assert.AreEqual(PlayerAvailability.Dismissed, w[w.IndexOf("hs4")].Availability);
            Assert.AreNotEqual(new Vec2(5f, 5f), frozen[frozen.IndexOf("hs3")].Position); Assert.AreEqual(PlayerAvailability.OnPitch, frozen[frozen.IndexOf("hs4")].Availability);
        }

        private static void Tire(MatchController c, string id, float stamina) { c.State.Home.Find(id).Locomotion.CurrentStamina = stamina; }

        [Test]
        public void WorldState_IsDeterministic_SameMatchSameSnapshot()
        {
            string Snap(int seed)
            {
                MatchController c = Live(seed: seed);
                Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
                c.SetPlayerInput(TeamSide.Home, "hs2", East);
                Step(c, 90);
                WorldState w = WorldState.Capture(c);
                return string.Join("|", Enumerable.Range(0, w.PlayerCount).Select(i => w[i].PlayerId + w[i].Position.X.ToString("R") + w[i].Position.Y.ToString("R") + w[i].Stamina.ToString("R") + w[i].Speed.ToString("R"))) + w.Ball.Position.X.ToString("R");
            }
            Assert.AreEqual(Snap(1), Snap(1)); Assert.AreEqual(Snap(1), Snap(2), "no random in an uneventful minute: the snapshot depends on the state only");
        }

        [Test]
        public void WorldPlayer_ReflectsActionStateAvailabilityAndControl()
        {
            MatchController c = Live();
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SelectPlayer(TeamSide.Home, "hs3");
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.Shield, East));
            Put(c, TeamSide.Away, "as2", 6f, 0f, West); c.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Pressure, West));
            WorldState w = WorldState.Capture(c);
            Assert.IsTrue(w[w.IndexOf("hs2")].IsShielding); Assert.IsTrue(w[w.IndexOf("as2")].IsPressing);
            Assert.IsTrue(w[w.IndexOf("hs3")].IsControlled); Assert.IsFalse(w[w.IndexOf("hs2")].IsControlled); Assert.AreEqual("hs3", w.ControlledPlayerOf(TeamSide.Home));
            c.SetControl(TeamSide.Home, ControlSource.Ai);
            Assert.IsFalse(WorldState.Capture(c).TryGetPlayer("hs3", out WorldPlayer ai) ? ai.IsControlled : true, "only a person-controlled side has a controlled player");
            c.IssueCard(TeamSide.Home, "hs5", CardKind.Red);
            WorldPlayer red = WorldState.Capture(c)[WorldState.Capture(c).IndexOf("hs5")];
            Assert.IsFalse(red.IsOnPitch); Assert.IsFalse(red.CanAct);
        }

        [Test]
        public void WorldMatch_CarriesTheClockTheScoreAndThePendingRestart()
        {
            MatchController c = Live(confirm: false);
            WorldState w = WorldState.Capture(c);
            Assert.IsTrue(w.Match.RestartPending); Assert.AreEqual(RestartKind.KickOff, w.Match.RestartKind); Assert.AreEqual(RestartStatus.Pending, w.Match.RestartStatus); Assert.IsFalse(w.Match.BallLive);
            Assert.AreEqual(MatchPhase.WaitingForKickoff, w.Match.Phase);
            Park(c); c.ConfirmRestartReady(); Step(c, 120);
            w = WorldState.Capture(c);
            Assert.AreEqual(2f, w.Match.PeriodElapsed, 0.05f); Assert.AreEqual(w.Match.PeriodElapsed, w.Match.MatchTime, 0.001f); Assert.Greater(w.Match.PeriodRemaining, 0f);
            Assert.IsFalse(w.Match.RestartPending); Assert.IsTrue(w.Match.HomeAttacksPositiveX == c.State.HomeAttacksPositiveX);
        }

        [Test]
        public void PlayerAvailability_MirrorsTheMatchPlayerStatus()
        {
            foreach (MatchPlayerStatus s in Enum.GetValues(typeof(MatchPlayerStatus))) Assert.AreEqual(s.ToString(), ((PlayerAvailability)(int)s).ToString());
            Assert.AreEqual(Enum.GetValues(typeof(MatchPlayerStatus)).Length, Enum.GetValues(typeof(PlayerAvailability)).Length);
        }

        // ================= WorldView =================

        [Test]
        public void WorldView_IsDerivedFromTheState_AndOffersNoWayToWrite()
        {
            MatchController c = Live();
            Carrier(c, TeamSide.Home, "hs2", 3f, 1f, East);
            WorldState w = WorldState.Capture(c); WorldView v = w.View;
            Assert.AreEqual(20, v.PlayerCount); Assert.AreEqual(6, v.OnPitchCount(TeamSide.Home)); Assert.AreEqual(w.Ball.Position, v.Ball.Position);
            Assert.IsTrue(v.TryGetBallCarrier(out WorldPlayer carrier)); Assert.AreEqual("hs2", carrier.PlayerId);
            Assert.AreEqual(0, v.IndexOf("hs0")); Assert.AreEqual(-1, v.IndexOf("nobody")); Assert.IsFalse(v.TryGetPlayer("nobody", out _));
            Assert.IsTrue(typeof(WorldView).IsValueType); Assert.IsTrue(typeof(WorldView).GetCustomAttributes(false).Any(a => a.GetType().Name == "IsReadOnlyAttribute"), "a readonly struct");
            foreach (PropertyInfo p in typeof(WorldView).GetProperties()) Assert.IsFalse(p.CanWrite, p.Name);
            foreach (FieldInfo f in typeof(WorldView).GetFields(BindingFlags.Public | BindingFlags.Instance)) Assert.IsTrue(f.IsInitOnly, f.Name);
            foreach (MethodInfo m in typeof(WorldView).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo pa in m.GetParameters()) Assert.IsFalse(typeof(MatchState).IsAssignableFrom(pa.ParameterType) || typeof(MatchController).IsAssignableFrom(pa.ParameterType), m.Name);
        }

        [Test]
        public void WorldView_PredictsTheBallOnACopy_WithoutTouchingTheRealOne()
        {
            MatchController c = Live();
            PutBall(c, 0f, 0f, 5f, 0f);
            WorldView v = WorldState.Capture(c).View;
            Vec3 ahead = v.PredictBallPosition(1f);
            Assert.Greater(ahead.X, 3f); Assert.Less(ahead.X, 5.01f); Assert.AreEqual(0f, c.State.Ball.Physics.Position.X, "the real ball did not move");
            Assert.AreEqual(ahead, v.PredictBallPosition(1f), "deterministic");
            c.Advance(1f);
            Assert.AreEqual(ahead.X, c.State.Ball.Physics.Position.X, 0.2f, "the prediction is the reference ball model");
        }

        [Test]
        public void WorldView_SkillsAndPermissions_AreTheEngines_NotACopyOfTheRules()
        {
            var cast = new Players();
            PlayerAttributes strong = All(90);
            cast.Defs["hs1"] = new PlayerDefinition("hs1", "hs1", 1, PlayerRole.Midfielder, strong);
            var skills = new Dictionary<string, PlayerSkillSet> { { "hs1", new PlayerSkillSet { PlayerId = "hs1", SkillIds = { "curve_shot", "support_run" } } } };
            var lookup = new SkillLookup(skills);
            var catalogJson = File.ReadAllText(Path.Combine(new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName, "Docs", "authoring", "FS27_SKILLS_V1.json"));
            Assert.IsTrue(SkillCatalogJson.TryFromJson(catalogJson, out SkillCatalog catalog, out string err), err);
            var settings = MatchSettings.CreateClassic();
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, cast, null, lookup, catalog);
            c.Start(); Park(c); c.ConfirmRestartReady();
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            WorldView v = WorldState.Capture(c).View;
            ResolvedSkills r = v.SkillsOf("hs1");
            Assert.IsTrue(r.HasSkill("curve_shot")); Assert.AreEqual(1, r.Ignored.Count); Assert.AreEqual(SkillIgnoredReason.Deferred, r.Ignored[0].Reason);
            Assert.IsTrue(v.CanUse("hs1", FootballActionKind.CurvedShot).Allowed); Assert.AreEqual(SkillEffect.CurvedShot, v.CanUse("hs1", FootballActionKind.CurvedShot).Effect);
            Assert.AreEqual(ActionRejection.SkillNotGranted, v.CanUse("hs1", FootballActionKind.SkillLobPass).Rejection);
            Assert.AreEqual(c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.CurvedShot).Allowed, v.CanUse("hs1", FootballActionKind.CurvedShot).Allowed);
            Assert.IsTrue(v.CanUse("hs1", FootballActionKind.Shot).Allowed);
            Assert.AreEqual(ActionRejection.PlayerNotActive, v.CanUse("hb1", FootballActionKind.Shot).Rejection);
            Assert.IsNull(v.SkillsOf("nobody"));
            WorldPlayer p = v[v.IndexOf("hs1")];
            Assert.Greater(p.Profile.ShotQualityFar, 0f); Assert.Greater(p.MaxSpeed, 0f); Assert.AreEqual(PreferredFoot.Right, p.PreferredFoot);
        }

        private sealed class SkillLookup : IPlayerSkillLookup
        {
            private readonly Dictionary<string, PlayerSkillSet> sets;
            public SkillLookup(Dictionary<string, PlayerSkillSet> sets) { this.sets = sets; }
            public bool TryGetSkills(string id, out PlayerSkillSet s) { return sets.TryGetValue(id, out s); }
        }
    }
}
