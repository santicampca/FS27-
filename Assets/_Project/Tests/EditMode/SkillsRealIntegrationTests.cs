using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 6: the whole path with the REAL players (AuthoredPlayerLibrary -> database -> PlayerSkillSet -> action with skill -> resolver -> BallSimulator -> ActionResult), the guards of the model and the centralised numbers.</summary>
    public partial class SkillsRealTests
    {
        private static GameDatabase realDb;
        private static GameDatabase RealDb { get { return realDb ?? (realDb = DataLockTests.LoadRealDatabase()); } }

        private static IEnumerable<PlayerDefinition> RealField { get { return RealDb.Players.All.Where(p => p.Role != PlayerRole.Goalkeeper); } }
        private static IEnumerable<PlayerDefinition> RealKeepers { get { return RealDb.Players.All.Where(p => p.Role == PlayerRole.Goalkeeper); } }

        /// <summary>A match with the real players: <paramref name="home"/> are the home field players (up to 5 starters + 3 on the bench), the away side is made of the other real players.</summary>
        private static MatchController RealMatch(IList<string> home, int seed = 1, MatchSettings settings = null)
        {
            string[] keepers = RealKeepers.Take(4).Select(k => k.Id).ToArray();
            var used = new HashSet<string>(home);
            var away = RealField.Where(p => !used.Contains(p.Id)).Take(8).Select(p => p.Id).ToList();
            var homeField = home.Concat(RealField.Where(p => !used.Contains(p.Id) && !away.Contains(p.Id)).Select(p => p.Id)).Take(8).ToList();
            MatchSquad Squad(string gkA, string gkB, List<string> field) => new MatchSquad(
                new[] { new SquadMember(gkA, true, 100f) }.Concat(field.Take(5).Select(i => new SquadMember(i, false, 100f))),
                new[] { new SquadMember(gkB, true, 100f) }.Concat(field.Skip(5).Take(3).Select(i => new SquadMember(i, false, 100f))));
            settings = settings ?? MatchSettings.CreateClassic();
            settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, Squad(keepers[0], keepers[1], homeField), Squad(keepers[2], keepers[3], away), null, "real", null, RealDb.Players, null, RealDb.PlayerSkills, RealDb.Skills);
            c.Start();
            var h = c.State.Home.Players.ToList(); var a = c.State.Away.Players.ToList();
            for (int i = 0; i < 6; i++) { c.PlacePlayer(TeamSide.Home, h[i].PlayerId, new Vec2(i == 0 ? -19f : -16f, -10f + i * 4f), East.ToHeading()); c.PlacePlayer(TeamSide.Away, a[i].PlayerId, new Vec2(i == 0 ? 19f : 16f, -10f + i * 4f), West.ToHeading()); }
            Assert.IsTrue(c.ConfirmRestartReady());
            return c;
        }

        private static string V(Vec3 v) { return v.X.ToString("R") + "," + v.Y.ToString("R") + "," + v.Z.ToString("R"); }

        private static string OwnerOf(string skillId, int skip = 0) { return RealField.Where(p => RealDb.PlayerSkills.Has(p.Id, skillId)).Skip(skip).Select(p => p.Id).FirstOrDefault(); }

        private static void Give(MatchController c, string id, float x, float y, Vec2 facing)
        {
            Put(c, TeamSide.Home, id, x, y, facing);
            PutBall(c, x + facing.X * 0.8f, y + facing.Y * 0.8f);
            Assert.IsTrue(c.GiveBallTo(TeamSide.Home, id));
        }

        // ================= the real players, the whole path =================

        [Test]
        public void ARealCurveShotOwner_ShootsACurvedBall_ThroughTheWholePath()
        {
            string id = OwnerOf(Curve);
            Assert.IsNotNull(id, "the real database has owners of the curved shot");
            MatchController c = RealMatch(new[] { id });
            Assert.IsTrue(c.State.Home.Find(id).Skills.Has(SkillEffect.CurvedShot));
            Assert.IsTrue(c.CanUseAction(TeamSide.Home, id, FootballActionKind.CurvedShot).Allowed);
            Give(c, id, 4f, 0f, East);
            Vec3 start = c.State.Ball.Physics.Position;
            ActionResult r = Do(c, id, FootballActionKind.CurvedShot, East, 1f, 0.7f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Curved, r.Detail); Assert.AreEqual(Curve, r.SkillId);
            Assert.AreNotEqual(0f, c.State.Ball.Physics.Spin.Y);
            Step(c, 30);
            Assert.Greater(c.State.Ball.Physics.Position.X, start.X + 3f, "the BallSimulator carried the ball");
            Assert.AreEqual(FootballActionKind.CurvedShot, c.State.Ball.LastAction); Assert.AreEqual(id, c.State.Ball.LastActionPlayerId);
        }

        [Test]
        public void ARealSkillMoveOwner_DribblesPastAnOpponent_ThroughTheWholePath()
        {
            string id = OwnerOf(Close) ?? OwnerOf(Elusive) ?? OwnerOf(Flair);
            Assert.IsNotNull(id);
            MatchController c = RealMatch(new[] { id });
            Give(c, id, 0f, 0f, East);
            Assert.IsTrue(c.CanUseAction(TeamSide.Home, id, FootballActionKind.SkillMove).Allowed);
            Put(c, TeamSide.Away, c.State.Away.Players[1].PlayerId, 1.4f, 0.4f, West);
            ActionResult r = Do(c, id, FootballActionKind.SkillMove, North);
            Assert.AreEqual(FootballActionKind.SkillMove, r.Kind); Assert.AreNotEqual(ActionOutcome.Rejected, r.Outcome);
            Assert.IsTrue(r.Detail == ActionDetail.Evaded || r.Detail == ActionDetail.PartlyEvaded || r.Detail == ActionDetail.Fumbled);
            Assert.IsNotNull(r.SkillId); Assert.IsTrue(catalog.TryGet(r.SkillId, out SkillDefinition d) && d.UnlocksAction == FootballActionKind.SkillMove);
            Assert.IsTrue(c.State.Home.Find(id).Skills.HasSkill(r.SkillId));
        }

        [Test]
        public void ARealLobPassOwner_PlaysTheLob_ThroughTheWholePath()
        {
            string id = OwnerOf(Switch) ?? OwnerOf(Creative);
            Assert.IsNotNull(id);
            MatchController c = RealMatch(new[] { id });
            Give(c, id, -6f, 0f, East);
            ActionResult r = Do(c, id, FootballActionKind.SkillLobPass, East);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Lofted, r.Detail); Assert.Greater(r.BallVelocity.Y, 0f);
            Assert.IsTrue(r.SkillId == Switch || r.SkillId == Creative);
            Step(c, 20);
            Assert.Greater(c.State.Ball.Physics.Position.X, -3f);
        }

        [Test]
        public void ARealPlayerWithoutTheSkill_IsRefused_EvenIfAnotherPlayerOwnsIt()
        {
            string owner = OwnerOf(Curve);
            string stranger = RealField.First(p => !RealDb.PlayerSkills.Has(p.Id, Curve) && !RealDb.UnlockedActions(p.Id).Contains(FootballActionKind.CurvedShot)).Id;
            MatchController c = RealMatch(new[] { owner, stranger });
            Give(c, stranger, 4f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Do(c, stranger, FootballActionKind.CurvedShot, East, 1f, 0.5f).Rejection);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Do(c, stranger, FootballActionKind.CurvedShot, East, 1f, 0.5f, grant: true).Rejection, "a match that knows the skills ignores the caller's flag");
        }

        [Test]
        public void TheDatabasePermission_AndTheEnginePermission_AreTheSameForEveryRealPlayer()
        {
            foreach (PlayerDefinition p in RealField)
            {
                var set = RealDb.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet s) ? s : null;
                ResolvedSkills r = SkillResolver.Resolve(set, RealDb.Skills, p.Attributes);
                List<FootballActionKind> unlocked = RealDb.UnlockedActions(p.Id);
                foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
                    Assert.AreEqual(unlocked.Contains(k), SkillResolver.CanUseAction(r, k, true).Allowed, p.Id + " " + k);
            }
        }

        [Test]
        public void EveryRealFieldPlayer_ResolvesCoherently_EveryOwnedSkillIsActiveOrListedAsDeferred_NeverInvalid()
        {
            int withReal = 0, deferredIgnored = 0, total = 0;
            var owned = new Dictionary<string, int>();
            foreach (PlayerDefinition p in RealField)
            {
                total++;
                RealDb.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet set);
                ResolvedSkills r = SkillResolver.Resolve(set, RealDb.Skills, p.Attributes);
                int count = set == null ? 0 : set.SkillIds.Count;
                Assert.AreEqual(count, r.ActiveSkillIds.Count + r.Ignored.Count, p.Id);
                foreach (IgnoredSkill i in r.Ignored) { Assert.AreEqual(SkillIgnoredReason.Deferred, i.Reason, p.Id + " " + i.SkillId + " must only be ignored for being deferred"); deferredIgnored++; }
                foreach (string id in r.ActiveSkillIds) { owned[id] = (owned.ContainsKey(id) ? owned[id] : 0) + 1; Assert.AreEqual(SkillImplementation.Real, SkillBindings.All.First(b => b.SkillId == id).Implementation); }
                if (r.OwnsAny) withReal++;
            }
            Assert.AreEqual(136, total, "the 136 field players");
            Assert.Greater(withReal, 60, "most real players own at least one real skill");
            Assert.IsTrue(owned.ContainsKey(Curve) && (owned.ContainsKey(Close) || owned.ContainsKey(Elusive) || owned.ContainsKey(Flair)) && (owned.ContainsKey(Switch) || owned.ContainsKey(Creative)));
            Assert.Greater(deferredIgnored, 0, "real players do own deferred skills: they are ignored, not lost");
        }

        [Test]
        public void TheGoalkeepers_OwnNoSkill_AndUseNone()
        {
            foreach (PlayerDefinition k in RealKeepers) Assert.IsFalse(RealDb.PlayerSkills.TryGetSkills(k.Id, out PlayerSkillSet s) && s.SkillIds.Count > 0, k.Id);
            MatchController c = RealMatch(new[] { OwnerOf(Curve) });
            string gk = c.State.Home.Players[0].PlayerId;
            Give(c, gk, -19f, 0f, East);
            foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
                Assert.AreEqual(ActionRejection.SkillNotGranted, Do(c, gk, k, East, 1f, 0.5f).Rejection, k.ToString());
        }

        [Test]
        public void SkillsNeverChangeAttributesOrOverall_ARealMatchWithManySkillActionsLeavesThemUntouched()
        {
            string a = OwnerOf(Curve), b = OwnerOf(Close) ?? OwnerOf(Elusive) ?? OwnerOf(Flair), l = OwnerOf(Switch) ?? OwnerOf(Creative);
            MatchController c = RealMatch(new[] { a, b, l });
            var calculator = PlayerRatingCalculator.CreateDefault();
            string Snapshot(string id)
            {
                PlayerDefinition d = RealDb.Players.TryGet(id, out PlayerDefinition x) ? x : null;
                RealDb.Profiles.TryGet(id, out PlayerPlayingProfile profile);
                return string.Join(",", Enum.GetValues(typeof(PlayerAttributeId)).Cast<PlayerAttributeId>().Select(i => d.Attributes.GetValue(i))) + "|" + calculator.GetOverall(d, profile) + "|" + d.PreferredFoot + d.WeakFootQuality;
            }
            string[] ids = { a, b, l };
            string[] before = ids.Select(Snapshot).ToArray();
            for (int round = 0; round < 6; round++)
            {
                Give(c, a, 4f, 0f, East); Do(c, a, FootballActionKind.CurvedShot, East, 1f, 0.8f); Step(c, 20);
                Give(c, b, 0f, 0f, East); Do(c, b, FootballActionKind.SkillMove, North); Step(c, 40);
                Give(c, l, -6f, 0f, East); Do(c, l, FootballActionKind.SkillLobPass, East); Step(c, 20);
            }
            CollectionAssert.AreEqual(before, ids.Select(Snapshot).ToArray());
            foreach (Type t in new[] { typeof(ResolvedSkills), typeof(SkillBinding), typeof(SkillResolver), typeof(SkillDefinition), typeof(PlayerSkillSet) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, t.Name + "." + f.Name + " must not hold attributes");
            Assert.IsNull(typeof(MatchPlayer).GetProperty("Attributes")); Assert.IsNull(typeof(MatchPlayer).GetField("Attributes"));
        }

        [Test]
        public void ARealMatch_WithTheSameSeedAndTheSameActions_IsIdentical_ArenaAndClassic()
        {
            string a = OwnerOf(Curve), b = OwnerOf(Close) ?? OwnerOf(Elusive) ?? OwnerOf(Flair);
            foreach (MatchSettings mode in new[] { MatchSettings.CreateClassic(), MatchSettings.CreateArena() })
            {
                string Run(int seed)
                {
                    MatchController c = RealMatch(new[] { a, b }, seed, mode == null ? null : (mode.Mode == MatchSettings.CreateArena().Mode ? MatchSettings.CreateArena() : MatchSettings.CreateClassic()));
                    var log = new List<string>();
                    Give(c, b, 0f, 0f, East); log.Add(Do(c, b, FootballActionKind.SkillMove, North).ToString() + V(c.State.Ball.Physics.Velocity)); Step(c, 90);
                    Give(c, a, 4f, 0f, East); ActionResult r = Do(c, a, FootballActionKind.CurvedShot, East, 1f, 0.9f); log.Add(r.ToString() + V(r.BallVelocity) + r.Quality); Step(c, 120);
                    log.Add(V(c.State.Ball.Physics.Position)); log.Add(V(c.State.Ball.Physics.Spin));
                    return string.Join("|", log);
                }
                Assert.AreEqual(Run(11), Run(11)); Assert.AreNotEqual(Run(11), Run(12));
            }
        }

        // ================= the model's own guards =================

        [Test]
        public void TheSkillNumbers_LiveInOnePlace_AndTheTuningStartsFromThem()
        {
            var t = new SkillTuning();
            foreach (FieldInfo f in typeof(SkillDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                FieldInfo mirror = typeof(SkillTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual((float)f.GetValue(null), (float)mirror.GetValue(t), f.Name);
            }
            foreach (FieldInfo f in typeof(SkillTuning).GetFields()) Assert.IsNotNull(typeof(SkillDefaults).GetField(f.Name), f.Name + " has no default");
            Assert.Greater(typeof(SkillDefaults).GetFields().Length, 60);
        }

        [Test]
        public void EverySkillDefault_IsSane_AndTheThresholdsAreOrdered()
        {
            foreach (FieldInfo f in typeof(SkillDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                float v = (float)f.GetValue(null);
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), f.Name);
                if (f.Name == "FlairMoveBias" || f.Name == "ElusiveMoveBias" || f.Name == "FacingCosine") continue; // may be negative or zero
                Assert.Greater(v, 0f, f.Name);
            }
            Assert.Greater(SkillDefaults.MoveCleanThreshold, SkillDefaults.MoveReducedThreshold);
            Assert.Less(SkillDefaults.QualityCeiling, 1f); Assert.Less(SkillDefaults.MoveReducedShare, 1f);
            Assert.Less(SkillDefaults.FlairMoveBias, SkillDefaults.ElusiveMoveBias); Assert.Greater(SkillDefaults.CloseMoveBias, SkillDefaults.ElusiveMoveBias);
            Assert.Greater(SkillDefaults.AggressiveFoulShift, SkillDefaults.AggressiveTackleBonus, "an aggressive tackler fouls more on balance");
            Assert.Less(SkillDefaults.CurveErrorRelief, 1f, "no mastery removes the whole error");
            foreach (string name in new[] { "LineBreakingErrorRelief", "OneVOneLockRelief", "PressResistanceRelief", "HighPressDrainRelief", "TurnPenaltyRelief", "MoveEvadePressureRelief" })
                Assert.Less((float)typeof(SkillDefaults).GetField(name).GetValue(null), 1f, name);
        }

        [Test]
        public void TheSkillCode_HasNoLooseNumbers_AndNoForbiddenWords()
        {
            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f");
            foreach (string file in new[] { Path.Combine(dir, "Match", "MatchController.Skills.cs"), Path.Combine(dir, "Skills", "SkillResolver.cs") })
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(Path.GetFileName(file) + ": loose number " + m.Value + " in: " + line);
                }
            string[] files = { Path.Combine(dir, "Match", "MatchController.Skills.cs"), Path.Combine(dir, "Skills", "SkillResolver.cs"), Path.Combine(dir, "Skills", "SkillBindings.cs"), Path.Combine(dir, "Skills", "SkillDefaults.cs") };
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string banned in new[] { "UnityEngine", "DateTime", "System.Random", "Time.time", "EffectiveOverall", "BaseOverall", "CoachBoost", "GRL", "Difficulty", "UnityEngine.Random" })
                    StringAssert.DoesNotContain(banned, text, Path.GetFileName(file));
            }
        }

        [Test]
        public void TheSkillResolution_UsesOnlyTheSeededGenerator_NoSecondSystemExists()
        {
            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            string skills = File.ReadAllText(Path.Combine(dir, "Match", "MatchController.Skills.cs"));
            StringAssert.DoesNotContain("new SeededRandom", skills); StringAssert.DoesNotContain("SeedMixer", skills);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(skills, @"Noise\(\)").Count, "the skill move draws exactly once, through the action generator of the match");
            foreach (string typeName in new[] { "SkillSystem", "SkillManager", "SkillPermission2", "SkillCatalog2", "ActionModel2", "BallSimulator2" })
                Assert.IsNull(typeof(MatchController).Assembly.GetType("FS27.Core." + typeName), typeName);
            Assert.AreEqual(1, typeof(MatchController).Assembly.GetTypes().Count(t => t.Name == "SkillCatalog"));
            Assert.AreEqual(1, typeof(MatchController).Assembly.GetTypes().Count(t => t.Name == "BallSimulator"));
            Assert.AreEqual(1, typeof(MatchController).Assembly.GetTypes().Count(t => t.Name == "ActionModel"));
        }

        [Test]
        public void TheStamina_IsTheExistingOne_ASkillActionNeverTakesMoreThanHeHas_AndRecoversAsAlways()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve, Elusive, Switch, "aggressive_tackle", "high_press" });
            foreach (float stamina in new[] { 100f, 40f, 3f, 0.1f, 0f })
                foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
                {
                    MatchController c = Match(cast); Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East); Tire(c, "hs1", stamina);
                    ActionResult r = Do(c, "hs1", k, East, 1f, 0.5f);
                    float left = c.State.Home.Find("hs1").Locomotion.CurrentStamina;
                    Assert.GreaterOrEqual(left, 0f, k + " at " + stamina); Assert.LessOrEqual(r.StaminaCost, stamina + 0.0001f);
                    Assert.AreEqual(stamina - r.StaminaCost, left, 0.0001f);
                }
            MatchController rest = Match(cast); Carrier(rest, TeamSide.Home, "hs1", 0f, 0f, East); Tire(rest, "hs1", 50f);
            Do(rest, "hs1", FootballActionKind.SkillMove, North); float after = rest.State.Home.Find("hs1").Locomotion.CurrentStamina;
            rest.SetPlayerInput(TeamSide.Home, "hs1", Vec2.Zero); Step(rest, 300);
            Assert.Greater(rest.State.Home.Find("hs1").Locomotion.CurrentStamina, after, "it is not permanent: the player recovers");
        }

        [Test]
        public void TheSkillStaminaCost_IsPaidThroughOneEntryPoint()
        {
            string dir = DifficultyGuardTests.FindCoreSourceDirectory();
            string skills = File.ReadAllText(Path.Combine(dir, "Match", "MatchController.Skills.cs"));
            StringAssert.DoesNotContain("CurrentStamina =", skills, "no second stamina: the engine's SpendStamina is the only way");
            StringAssert.DoesNotContain("CurrentStamina -", skills);
            Assert.IsTrue(skills.Contains("SpendStamina("));
        }

        [Test]
        public void ThePhase4Contracts_AreIntact_NoSkillMeansTheExactPreviousKick()
        {
            var cast = new Cast().Add("hs1", 80).Add("hs2", 80, skills: new[] { "line_breaking_pass" });
            foreach (FootballActionKind k in new[] { FootballActionKind.ShortPass, FootballActionKind.LongPass, FootballActionKind.Shot, FootballActionKind.Chip, FootballActionKind.Clearance })
            {
                MatchController a = Match(cast, 5), b = Match(cast, 5, null, null, false);
                Carrier(a, TeamSide.Home, "hs1", 0f, 0f, East); Carrier(b, TeamSide.Home, "hs1", 0f, 0f, East);
                ActionResult ra = Do(a, "hs1", k, East), rb = Do(b, "hs1", k, East);
                Assert.AreEqual(rb.BallVelocity, ra.BallVelocity, k + ": a match that knows the skills resolves a skill-less player exactly like one that does not");
                Assert.AreEqual(rb.Quality, ra.Quality); Assert.AreEqual(rb.StaminaCost, ra.StaminaCost); Assert.IsNull(ra.SkillId);
            }
        }

        [Test]
        public void ThePlayerCounts_AndTheRosters_AreNotTouched()
        {
            Assert.AreEqual(136, RealField.Count()); Assert.AreEqual(24, RealKeepers.Count()); Assert.AreEqual(12, RealDb.Teams.Count());
            Assert.AreEqual(37, RealDb.Skills.Count); Assert.AreEqual(2, RealDb.Skills.DeferredTags.Count);
        }
    }
}
