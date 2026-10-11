using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 6: REAL SKILLS. The catalogue stops being data and permission and becomes behaviour inside the action pipeline. These tests drive the match engine only through its public API
    /// (request -> resolver -> ball -> result) and prove that an owned skill is really used, resolves by attributes, stamina, foot and context, and is deterministic.
    /// </summary>
    public partial class SkillsRealTests
    {
        private const float Dt = LocomotionDefaults.FixedDeltaTime;
        private static readonly Vec2 East = new Vec2(1f, 0f);
        private static readonly Vec2 West = new Vec2(-1f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static string root;
        private static SkillCatalog catalog;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            Assert.IsTrue(SkillCatalogJson.TryFromJson(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_SKILLS_V1.json")), out catalog, out string error), error);
        }

        // ================= scenery =================

        private sealed class Players : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Defs = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Defs.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private sealed class SkillsOf : IPlayerSkillLookup
        {
            public readonly Dictionary<string, PlayerSkillSet> Sets = new Dictionary<string, PlayerSkillSet>();
            public bool TryGetSkills(string id, out PlayerSkillSet s) { return Sets.TryGetValue(id, out s); }
        }

        private sealed class Cast
        {
            public readonly Players Players = new Players();
            public readonly SkillsOf Skills = new SkillsOf();

            /// <summary>A player with every attribute at <paramref name="v"/> (then edited), his feet and the skills he owns.</summary>
            public Cast Add(string id, int v, Func<PlayerAttributes, PlayerAttributes> edit = null, PreferredFoot foot = PreferredFoot.Right, int weakFoot = 50, params string[] skills)
            {
                PlayerAttributes a = All(v);
                if (edit != null) a = edit(a);
                Players.Defs[id] = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, a) { PreferredFoot = foot, WeakFootQuality = weakFoot };
                Skills.Sets[id] = new PlayerSkillSet { PlayerId = id, SkillIds = skills.ToList() };
                return this;
            }
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

        private static MatchController Match(Cast cast, int seed = 1, MatchSettings settings = null, SkillTuning tuning = null, bool knownSkills = true)
        {
            settings = settings ?? MatchSettings.CreateClassic();
            settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, cast.Players, null, knownSkills ? cast.Skills : null, knownSkills ? catalog : null, tuning);
            c.Start();
            Park(c);
            Assert.IsTrue(c.ConfirmRestartReady());
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
        private static int Steps(float seconds) { return (int)Math.Ceiling(seconds / Dt) + 1; }

        private static ActionResult Do(MatchController c, string id, FootballActionKind kind, Vec2 dir, float power = 1f, float curve = 0f, string skillId = null, bool grant = false, TeamSide side = TeamSide.Home)
        {
            return c.Perform(new ActionRequest(side, id, kind, dir, power) { Curve = curve, SkillId = skillId, SkillGranted = grant });
        }

        private static void Tire(MatchController c, string id, float stamina, TeamSide side = TeamSide.Home) { c.State.Team(side).Find(id).Locomotion.CurrentStamina = stamina; }

        private const string Curve = "curve_shot", Close = "close_control_dribble", Elusive = "elusive_dribble", Flair = "flair_dribble", Switch = "long_switch", Creative = "creative_pass";

        // ================= the catalogue and the behaviour table =================

        [Test]
        public void TheBehaviourTable_HasExactlyOneRowPerCatalogueSkill_AndNoOrphan()
        {
            Assert.AreEqual(37, catalog.Count);
            Assert.AreEqual(37, SkillBindings.All.Count);
            foreach (SkillDefinition s in catalog.All) { Assert.IsTrue(SkillBindings.TryGet(s.SkillId, out SkillBinding b), s.SkillId + " has no behaviour row"); Assert.AreEqual(s.SkillId, b.SkillId); }
            foreach (SkillBinding b in SkillBindings.All) Assert.IsTrue(catalog.Contains(b.SkillId), b.SkillId + " is not in the catalogue");
            Assert.AreEqual(37, SkillBindings.All.Select(b => b.SkillId).Distinct().Count());
        }

        [Test]
        public void EverySkill_IsExactlyOneOf_Real_PermissionOnly_Deferred_WithTheClosedCounts()
        {
            int real = SkillBindings.All.Count(b => b.Implementation == SkillImplementation.Real);
            int permission = SkillBindings.All.Count(b => b.Implementation == SkillImplementation.PermissionOnly);
            int deferred = SkillBindings.All.Count(b => b.Implementation == SkillImplementation.Deferred);
            Assert.AreEqual(25, real); Assert.AreEqual(0, permission, "no skill only unlocks: every unlocking skill changes the action too"); Assert.AreEqual(12, deferred);
            Assert.AreEqual(37, real + permission + deferred);
        }

        [Test]
        public void EveryDeferredSkill_SaysWhy_AndHasNoEffect_EveryRealSkillHasItsOwnEffect()
        {
            foreach (SkillBinding b in SkillBindings.All.Where(x => x.Implementation == SkillImplementation.Deferred)) { Assert.IsFalse(string.IsNullOrWhiteSpace(b.Reason), b.SkillId); Assert.AreEqual(SkillEffect.None, b.Effect, b.SkillId); }
            var real = SkillBindings.All.Where(x => x.Implementation == SkillImplementation.Real).ToList();
            foreach (SkillBinding b in real) { Assert.AreNotEqual(SkillEffect.None, b.Effect, b.SkillId); Assert.AreEqual(b.SkillId, SkillBindings.SkillIdOf(b.Effect)); Assert.IsTrue(SkillBindings.TryGetByEffect(b.Effect, out SkillBinding again) && again == b); }
            Assert.AreEqual(real.Count, real.Select(b => b.Effect).Distinct().Count(), "one effect per real skill");
            Assert.AreEqual(SkillBindings.EffectCount, Enum.GetValues(typeof(SkillEffect)).Length);
            Assert.AreEqual(SkillBindings.EffectCount - 1, (int)Enum.GetValues(typeof(SkillEffect)).Cast<SkillEffect>().Max());
        }

        [Test]
        public void TheEnums_HaveExplicitAppendOnlyNumbers()
        {
            Assert.AreEqual(0, (int)SkillImplementation.Real); Assert.AreEqual(1, (int)SkillImplementation.PermissionOnly); Assert.AreEqual(2, (int)SkillImplementation.Deferred);
            Assert.AreEqual(1, (int)SkillEffect.CurvedShot); Assert.AreEqual(2, (int)SkillEffect.CloseControlMove); Assert.AreEqual(5, (int)SkillEffect.SwitchLob); Assert.AreEqual(25, (int)SkillEffect.BallRecovery);
            Assert.AreEqual(3, (int)ActionButton.C);
            Assert.AreEqual(0, (int)SkillKind.Dribble); Assert.AreEqual(16, (int)SkillKind.Aerial);
            Assert.AreEqual(20, (int)FootballActionKind.SkillMove); Assert.AreEqual(21, (int)FootballActionKind.SkillLobPass); Assert.AreEqual(16, (int)FootballActionKind.CurvedShot);
        }

        [Test]
        public void TheMasteryOfEverySkill_IsMadeOfOfficialAttributesOnly_AddingUpToOne()
        {
            foreach (SkillBinding b in SkillBindings.All.Where(x => x.Implementation == SkillImplementation.Real))
            {
                Assert.AreEqual(1f, b.Mastery.Sum(w => w.Weight), 0.0001f, b.SkillId);
                foreach (SkillWeight w in b.Mastery) { Assert.IsTrue(PlayerAttributeInfo.All.Contains(w.Attribute), b.SkillId); Assert.Greater(w.Weight, 0f); }
                Assert.AreEqual(b.Mastery.Length, b.Mastery.Select(w => w.Attribute).Distinct().Count(), b.SkillId + " repeats an attribute");
            }
            Assert.AreEqual(12, Enum.GetValues(typeof(PlayerAttributeId)).Length, "no attribute was invented");
        }

        [Test]
        public void TheSixUnlockingSkills_AreReal_AndTheirEffectBelongsToTheActionTheyUnlock()
        {
            var unlockers = catalog.All.Where(s => s.UnlocksAction != FootballActionKind.None).ToList();
            Assert.AreEqual(6, unlockers.Count);
            foreach (SkillDefinition s in unlockers)
            {
                SkillBindings.TryGet(s.SkillId, out SkillBinding b);
                Assert.AreEqual(SkillImplementation.Real, b.Implementation, s.SkillId);
                CollectionAssert.Contains(SkillBindings.EffectsUnlocking(s.UnlocksAction).ToList(), b.Effect, s.SkillId);
            }
            foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
                foreach (SkillEffect e in SkillBindings.EffectsUnlocking(k))
                    Assert.AreEqual(k, catalog.TryGet(SkillBindings.SkillIdOf(e), out SkillDefinition d) ? d.UnlocksAction : FootballActionKind.None, e.ToString());
            Assert.AreEqual(0, SkillBindings.EffectsUnlocking(FootballActionKind.Shot).Count);
        }

        [Test]
        public void TheSignatureActionAudit_258To160To37_LosesNothing_AndRepeatsNothing()
        {
            var tags = catalog.All.SelectMany(s => s.SourceTags).ToList();
            Assert.AreEqual(158, tags.Count); Assert.AreEqual(158, tags.Distinct().Count(), "a tag belongs to exactly one skill");
            Assert.AreEqual(2, catalog.DeferredTags.Count);
            foreach (string t in tags) Assert.IsTrue(catalog.TryResolveTag(t, out SkillDefinition s) && s.SourceTags.Contains(t), t);
            foreach (DeferredTag d in catalog.DeferredTags) { Assert.IsTrue(catalog.IsDeferred(d.Tag)); Assert.IsFalse(catalog.TryResolveTag(d.Tag, out _), d.Tag + " must not be a skill"); Assert.IsFalse(string.IsNullOrWhiteSpace(d.Reason)); }
            Assert.AreEqual(160, tags.Count + catalog.DeferredTags.Count, "158 tags with a skill + 2 deferred = the 160 concepts");
            Assert.IsTrue(catalog.IsDeferred("OffsideTrapExploitation")); Assert.IsTrue(catalog.IsDeferred("TacticalFoul"));
        }

        // ================= resolving the skills of a player =================

        [Test]
        public void ARequirementIsAGate_NotABonus_AMissingFloorMakesTheSkillInactive()
        {
            PlayerAttributes weak = All(60); // curve_shot needs Shooting 74 and Technique 76
            ResolvedSkills r = SkillResolver.Resolve(new PlayerSkillSet { PlayerId = "x", SkillIds = { Curve } }, catalog, weak);
            Assert.IsFalse(r.Has(SkillEffect.CurvedShot)); Assert.AreEqual(0f, r.Strength(SkillEffect.CurvedShot)); Assert.IsFalse(r.HasSkill(Curve));
            Assert.AreEqual(SkillIgnoredReason.RequirementNotMet, r.Ignored.Single().Reason);
            PlayerAttributes strong = All(80);
            ResolvedSkills ok = SkillResolver.Resolve(new PlayerSkillSet { PlayerId = "x", SkillIds = { Curve } }, catalog, strong);
            Assert.IsTrue(ok.Has(SkillEffect.CurvedShot)); Assert.IsTrue(ok.HasSkill(Curve)); Assert.AreEqual(0, ok.Ignored.Count);
        }

        [Test]
        public void TheMastery_ComesFromTheAttributes_AndAHigherAttributeNeverGivesLess()
        {
            float low = SkillResolver.Resolve(new PlayerSkillSet { SkillIds = { Curve } }, catalog, All(76)).Strength(SkillEffect.CurvedShot);
            float high = SkillResolver.Resolve(new PlayerSkillSet { SkillIds = { Curve } }, catalog, All(99)).Strength(SkillEffect.CurvedShot);
            Assert.Less(low, high); Assert.AreEqual(1f, high, 0.0001f); Assert.Greater(low, 0.7f);
            foreach (SkillBinding b in SkillBindings.All.Where(x => x.Implementation == SkillImplementation.Real))
            {
                float prev = -1f;
                for (int v = 1; v <= 99; v += 7) { float m = SkillResolver.MasteryOf(b, All(v)); Assert.GreaterOrEqual(m, prev, b.SkillId); Assert.GreaterOrEqual(m, 0f); Assert.LessOrEqual(m, 1f); prev = m; }
            }
        }

        [Test]
        public void UnknownDeferredAndUnmetSkills_AreListedNeverSilentlyDropped_AndNeverActive()
        {
            var set = new PlayerSkillSet { PlayerId = "x", SkillIds = { "no_such_skill", "support_run", "aerial_duel", Curve, Close } };
            ResolvedSkills r = SkillResolver.Resolve(set, catalog, All(60));
            Assert.AreEqual(0, r.ActiveSkillIds.Count);
            Assert.AreEqual(5, r.Ignored.Count);
            Assert.AreEqual(SkillIgnoredReason.UnknownSkill, r.Ignored.Single(i => i.SkillId == "no_such_skill").Reason);
            Assert.AreEqual(SkillIgnoredReason.Deferred, r.Ignored.Single(i => i.SkillId == "support_run").Reason);
            Assert.AreEqual(SkillIgnoredReason.Deferred, r.Ignored.Single(i => i.SkillId == "aerial_duel").Reason);
            Assert.AreEqual(SkillIgnoredReason.RequirementNotMet, r.Ignored.Single(i => i.SkillId == Curve).Reason);
            Assert.IsFalse(r.OwnsAny);
        }

        [Test]
        public void AnEmptyOrMissingSkillSet_OwnsNothing_ButStillHasAnAttributeMastery()
        {
            ResolvedSkills none = SkillResolver.Resolve(null, catalog, All(90));
            Assert.IsFalse(none.OwnsAny); Assert.AreEqual(0f, none.Strength(SkillEffect.CurvedShot)); Assert.Greater(none.Potential(SkillEffect.CurvedShot), 0f);
            Assert.IsTrue(none.IsAuthoritative);
            Assert.IsFalse(SkillResolver.FromAttributesOnly(All(90)).IsAuthoritative);
            Assert.IsFalse(ResolvedSkills.None.OwnsAny);
        }

        [Test]
        public void TheResolution_IsDeterministic_SameInputsSameResult()
        {
            var set = new PlayerSkillSet { PlayerId = "x", SkillIds = { Curve, Close, "high_press" } };
            ResolvedSkills a = SkillResolver.Resolve(set, catalog, All(88)), b = SkillResolver.Resolve(set, catalog, All(88));
            CollectionAssert.AreEqual(a.ActiveSkillIds, b.ActiveSkillIds);
            foreach (SkillEffect e in Enum.GetValues(typeof(SkillEffect))) Assert.AreEqual(a.Strength(e), b.Strength(e), e.ToString());
        }

        // ================= one permission, one source of truth =================

        [Test]
        public void OrdinaryActions_AreNeverGatedBySkills()
        {
            foreach (FootballActionKind k in Enum.GetValues(typeof(FootballActionKind)))
            {
                if (SkillBindings.IsSkillAction(k)) continue;
                Assert.IsTrue(SkillResolver.CanUseAction(ResolvedSkills.None, k, false).Allowed, k.ToString());
                Assert.IsTrue(SkillResolver.CanUseAction(null, k, true).Allowed, k.ToString());
            }
        }

        [Test]
        public void ASkillAction_NeedsItsSkill_AndTheOwnerIsAllowedWithTheRightEffect()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve, Close, Switch }).Add("hs2", 90);
            MatchController c = Match(cast);
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
            {
                SkillPermission yes = c.CanUseAction(TeamSide.Home, "hs1", k);
                Assert.IsTrue(yes.Allowed, k.ToString()); Assert.AreEqual(ActionRejection.None, yes.Rejection); Assert.IsNotNull(yes.SkillId);
                SkillPermission no = c.CanUseAction(TeamSide.Home, "hs2", k);
                Assert.IsFalse(no.Allowed, k.ToString()); Assert.AreEqual(ActionRejection.SkillNotGranted, no.Rejection);
            }
            Assert.AreEqual(SkillEffect.CurvedShot, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.CurvedShot).Effect);
            Assert.AreEqual(SkillEffect.CloseControlMove, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove).Effect);
            Assert.AreEqual(SkillEffect.SwitchLob, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillLobPass).Effect);
            Assert.AreEqual(ActionRejection.UnknownPlayer, c.CanUseAction(TeamSide.Home, "nobody", FootballActionKind.SkillMove).Rejection);
        }

        [Test]
        public void ASkillMove_NeedsTheBall_ThePermissionSaysSo()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Elusive });
            MatchController c = Match(cast);
            Put(c, TeamSide.Home, "hs1", 0f, 0f, East);
            PutBall(c, 10f, 10f);
            Assert.AreEqual(ActionRejection.NotInPossession, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove).Rejection);
            Assert.AreEqual(ActionRejection.NotInPossession, Do(c, "hs1", FootballActionKind.SkillMove, East).Rejection);
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.IsTrue(c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove).Allowed);
        }

        [Test]
        public void TheRequestedStyle_IsHonoured_AndAStyleHeDoesNotOwnIsRefused()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Close, Flair });
            MatchController c = Match(cast);
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(SkillEffect.FlairMove, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove, Flair).Effect);
            Assert.AreEqual(SkillEffect.CloseControlMove, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove, Close).Effect);
            Assert.AreEqual(ActionRejection.SkillNotGranted, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove, Elusive).Rejection);
            Assert.AreEqual(ActionRejection.SkillNotGranted, c.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillLobPass, Switch).Rejection, "a move skill is not a lob skill");
        }

        [Test]
        public void WithoutARequestedStyle_TheBestOwnedIsUsed_AndATieGoesToTheFirstInCatalogueOrder()
        {
            // flair is made of Technique, close control of Control: the better one wins; equal masteries -> the earlier effect of the list
            var cast = new Cast()
                .Add("hs1", 80, a => { a.Technique = 99; a.BallControl = 80; return a; }, skills: new[] { Close, Flair })
                .Add("hs2", 80, a => { a.Technique = 80; a.BallControl = 99; return a; }, skills: new[] { Close, Flair })
                .Add("hs3", 90, skills: new[] { Elusive, Close });
            MatchController c = Match(cast);
            Assert.AreEqual(SkillEffect.FlairMove, SkillResolver.CanUseAction(c.State.Home.Find("hs1").Skills, FootballActionKind.SkillMove, true).Effect);
            Assert.AreEqual(SkillEffect.CloseControlMove, SkillResolver.CanUseAction(c.State.Home.Find("hs2").Skills, FootballActionKind.SkillMove, true).Effect);
            ResolvedSkills r3 = c.State.Home.Find("hs3").Skills;
            SkillEffect first = SkillResolver.CanUseAction(r3, FootballActionKind.SkillMove, true).Effect;
            for (int i = 0; i < 5; i++) Assert.AreEqual(first, SkillResolver.CanUseAction(r3, FootballActionKind.SkillMove, true).Effect, "deterministic");
        }

        [Test]
        public void TheCallersGrant_IsHonouredOnlyByAMatchThatKnowsNoSkills()
        {
            var cast = new Cast().Add("hs1", 90);
            MatchController legacy = Match(cast, knownSkills: false);
            Carrier(legacy, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Do(legacy, "hs1", FootballActionKind.SkillMove, East).Rejection);
            Assert.AreNotEqual(ActionRejection.SkillNotGranted, Do(legacy, "hs1", FootballActionKind.SkillMove, East, grant: true).Rejection);
            MatchController known = Match(cast);
            Carrier(known, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Do(known, "hs1", FootballActionKind.SkillMove, East, grant: true).Rejection, "when the skills are known, the flag is ignored: the skills are the only permission");
            Assert.IsFalse(known.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove, callerGrant: true).Allowed);
            Assert.IsTrue(legacy.CanUseAction(TeamSide.Home, "hs1", FootballActionKind.SkillMove, callerGrant: true).Allowed);
        }

        [Test]
        public void CanUseAction_AgreesWithPerform_ForEveryPlayerAndSkillAction()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve, Elusive, Creative }).Add("hs2", 90, skills: new[] { Curve }).Add("hs3", 90);
            foreach (string id in new[] { "hs1", "hs2", "hs3" })
                foreach (FootballActionKind k in new[] { FootballActionKind.CurvedShot, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass })
                {
                    MatchController c = Match(cast);
                    Carrier(c, TeamSide.Home, id, -3f, 0f, East);
                    SkillPermission p = c.CanUseAction(TeamSide.Home, id, k);
                    ActionResult r = Do(c, id, k, East, curve: 0.5f);
                    Assert.AreEqual(p.Allowed, r.Rejection != ActionRejection.SkillNotGranted, id + " " + k);
                    if (!p.Allowed) Assert.AreEqual(p.Rejection, r.Rejection);
                }
        }

        [Test]
        public void TheSkillPermission_LivesInOnePlace_NoOtherCodeDecidesIt()
        {
            string coreDir = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string file in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file), text = File.ReadAllText(file);
                bool returnsIt = text.Contains("ActionRejection.SkillNotGranted");
                if (name == "SkillResolver.cs") continue;
                Assert.IsFalse(returnsIt, name + " decides the skill permission itself");
                if (text.Contains(".SkillGranted")) Assert.IsTrue(name == "MatchController.Actions.cs", name + " reads the caller's grant");
            }
            string actions = File.ReadAllText(Path.Combine(coreDir, "Match", "MatchController.Actions.cs"));
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(actions, @"\.SkillGranted").Count, "the engine passes the flag to the resolver exactly once");
        }

        [Test]
        public void TheSkillButton_IsMappedWithTheBall_AndDoesNothingWithoutIt()
        {
            FootballActionKind Map(ActionButton b, ActionGesture g, bool ball, bool keeper = false) { return ActionMapper.Map(new ActionInput(b, g, Vec2.Zero, 1f), new ActionContext { HasBall = ball, IsGoalkeeper = keeper }); }
            Assert.AreEqual(FootballActionKind.SkillMove, Map(ActionButton.C, ActionGesture.Tap, true));
            Assert.AreEqual(FootballActionKind.SkillLobPass, Map(ActionButton.C, ActionGesture.SwipeUp, true));
            Assert.AreEqual(FootballActionKind.CurvedShot, Map(ActionButton.C, ActionGesture.SwipeDown, true));
            foreach (ActionGesture g in Enum.GetValues(typeof(ActionGesture)))
            {
                Assert.AreEqual(FootballActionKind.None, Map(ActionButton.C, g, false), "no ball, no skill action");
                Assert.AreEqual(FootballActionKind.None, Map(ActionButton.C, g, true, true), "the keeper in goal has none");
            }
            Assert.AreEqual(FootballActionKind.Shot, Map(ActionButton.A, ActionGesture.SwipeDown, true), "A and B are as before");
        }

        [Test]
        public void ThroughTheInputPath_AnOwnerUsesTheSkill_AndAStrangerIsRefused()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve, Elusive, Switch }).Add("hs2", 90);
            MatchController c = Match(cast);
            Carrier(c, TeamSide.Home, "hs1", 5f, 0f, East);
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.C, ActionGesture.SwipeDown, East, 1f));
            Assert.AreEqual(FootballActionKind.CurvedShot, r.Kind); Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(Curve, r.SkillId);
            Assert.AreNotEqual(0f, c.State.Ball.Physics.Spin.Y, "the default curve of the skill was applied");
            MatchController c2 = Match(cast);
            Carrier(c2, TeamSide.Home, "hs2", 5f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, c2.PerformInput(TeamSide.Home, "hs2", new ActionInput(ActionButton.C, ActionGesture.Tap, East, 1f)).Rejection);
        }

        [Test]
        public void TheDefaultCurve_FollowsThePreferredFoot_AndAGivenCurveWins()
        {
            var cast = new Cast().Add("hs1", 90, foot: PreferredFoot.Right, skills: new[] { Curve }).Add("hs2", 90, foot: PreferredFoot.Left, skills: new[] { Curve });
            MatchController c = Match(cast);
            Carrier(c, TeamSide.Home, "hs1", 5f, 0f, East);
            c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.C, ActionGesture.SwipeDown, East, 1f));
            float right = c.State.Ball.Physics.Spin.Y;
            MatchController c2 = Match(cast);
            Carrier(c2, TeamSide.Home, "hs2", 5f, 0f, East);
            c2.PerformInput(TeamSide.Home, "hs2", new ActionInput(ActionButton.C, ActionGesture.SwipeDown, East, 1f));
            Assert.AreNotEqual(0f, right); Assert.AreNotEqual(Math.Sign(right), Math.Sign(c2.State.Ball.Physics.Spin.Y));
            MatchController c3 = Match(cast);
            Carrier(c3, TeamSide.Home, "hs1", 5f, 0f, East);
            var input = new ActionInput(ActionButton.C, ActionGesture.SwipeDown, East, 1f) { Curve = -1f };
            c3.PerformInput(TeamSide.Home, "hs1", input);
            Assert.AreEqual(-Math.Sign(right), Math.Sign(c3.State.Ball.Physics.Spin.Y));
        }
    }
}
