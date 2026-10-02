using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class AnimationResolverTests
    {
        private CreatorCatalogs catalogs;
        private AnimationCatalog animations;
        private CatalogAnimationResolver resolver;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            animations = AnimationCatalog.CreateDefault();
            resolver = new CatalogAnimationResolver(animations, catalogs);
        }

        private string Best(float speed, FootballActionKind action = FootballActionKind.None, MovementStyle style = MovementStyle.Default, string behavior = null)
        {
            var ctx = new AnimationSelectionContext { Speed = speed, Action = action, Style = style, BehaviorId = behavior, RigId = "fs27_humanoid_v1" };
            return resolver.Select(ctx).First().Profile.Id;
        }

        [Test]
        public void Locomotion_IsChosenBySpeed()
        {
            Assert.AreEqual("idle", Best(0.05f));
            Assert.AreEqual("walk_loop", Best(1.0f));
            Assert.AreEqual("jog_loop", Best(2.5f));
            Assert.AreEqual("run_loop", Best(4.8f));
            Assert.AreEqual("sprint_loop", Best(7.5f));
        }

        [Test]
        public void Actions_PickTheirOwnAnimation_AndNeverAnotherActions()
        {
            Assert.AreEqual("shot_instep", Best(3f, FootballActionKind.Shot));
            Assert.AreEqual("shot_placed", Best(3f, FootballActionKind.PlacedShot));
            Assert.AreEqual("pass_short", Best(3f, FootballActionKind.ShortPass));
            Assert.AreEqual("tackle_stand", Best(3f, FootballActionKind.Tackle));
            Assert.IsTrue(resolver.Select(new AnimationSelectionContext { Speed = 3f, Action = FootballActionKind.Shot, RigId = "fs27_humanoid_v1" }).All(c => c.Profile.Action == FootballActionKind.Shot));
        }

        [Test]
        public void LocomotionContext_NeverReturnsAnActionClip()
        {
            Assert.IsTrue(resolver.Select(new AnimationSelectionContext { Speed = 3f, RigId = "fs27_humanoid_v1" }).All(c => c.Profile.Action == FootballActionKind.None));
        }

        [Test]
        public void ADribbleStyle_PicksTheMatchingDribbleClip()
        {
            Assert.AreEqual("dribble_feint", Best(2f, FootballActionKind.Dribble, MovementStyle.Feint));
            Assert.AreEqual("dribble_cut_inside", Best(4f, FootballActionKind.Dribble, MovementStyle.CutInside));
            Assert.AreEqual("dribble_carry", Best(4f, FootballActionKind.Dribble));
        }

        [Test]
        public void ASignatureBehaviour_ChoosesItsOwnClip()
        {
            FootballDNA dna = new FootballDNA();
            catalogs.Behaviors.TryGet("StopAndGo", out SignatureBehaviorDefinition def);
            Assert.AreEqual("dribble_stop_restart", Best(2f, def.Action, def.Style, "StopAndGo"));
            catalogs.Behaviors.TryGet("BodyFeint", out SignatureBehaviorDefinition feint);
            Assert.AreEqual("dribble_feint", Best(2f, feint.Action, feint.Style, "BodyFeint"));
            Assert.IsNotNull(dna);
        }

        [Test]
        public void AClipForAnotherRig_IsNotACandidate()
        {
            Assert.IsEmpty(resolver.Select(new AnimationSelectionContext { Speed = 3f, RigId = "some_other_rig" }));
        }

        [Test]
        public void EveryTagABehaviourAsksFor_IsOfferedByTheCatalog()
        {
            SortedSetAssert(animations.AllTags(), catalogs.Behaviors.All.SelectMany(b => b.AnimationTags));
        }

        private static void SortedSetAssert(System.Collections.Generic.SortedSet<string> offered, System.Collections.Generic.IEnumerable<string> asked)
        {
            foreach (string t in asked.Distinct()) CollectionAssert.Contains(offered, t, "no animation offers the tag '" + t + "'");
        }

        [Test]
        public void EveryActionTheGameHas_HasAtLeastOneAnimationProfile()
        {
            foreach (FootballActionKind k in System.Enum.GetValues(typeof(FootballActionKind)).Cast<FootballActionKind>().Where(k => k != FootballActionKind.None))
                Assert.IsTrue(animations.All.Any(a => a.Action == k), k + " has no animation profile");
        }

        [Test]
        public void TheAnimationCatalog_IsHonest_NoClipsExistYet_AndRootMotionIsOff()
        {
            Assert.IsTrue(animations.All.All(a => a.Status == AnimationStatus.Planned), "no clip exists in this build");
            Assert.IsTrue(animations.All.All(a => a.RootMotionWeight == 0f), "gameplay moves the player, never the animation");
            var plan = resolver.Plan(new AnimationSelectionContext { Speed = 3f, RigId = "fs27_humanoid_v1" }, default, default);
            Assert.IsTrue(plan.NoClipYet);
        }

        [Test]
        public void TheSharedSet_ListsTheTagsOfTheRig()
        {
            var rc = new CatalogAppearanceResolver(catalogs).Resolve(catalogs.NewSpecification("a"));
            Assert.IsTrue(resolver.TryResolve(rc, default, out AnimationSetReference set));
            CollectionAssert.Contains(set.Tags, "run");
            CollectionAssert.Contains(set.Tags, "shot");
            Assert.AreEqual("fs27_humanoid_v1", set.RigId);
            rc.RigId = null;
            Assert.IsFalse(resolver.TryResolve(rc, default, out _));
        }

        [Test]
        public void TheSameInputs_GiveTheSameRanking()
        {
            string Rank() => string.Join(",", resolver.Select(new AnimationSelectionContext { Speed = 3.7f, Action = FootballActionKind.Dribble, Style = MovementStyle.CutOutside, RigId = "fs27_humanoid_v1" }).Select(c => c.Profile.Id + c.Score.ToString("R")));
            Assert.AreEqual(Rank(), Rank());
        }

        // ---------------- personality ----------------

        private CharacterSpecification Body(float mass, float musc, float height)
        {
            CharacterSpecification s = catalogs.NewSpecification("pers-1");
            catalogs.Parameters.TryGet("body.mass", out ParameterDefinition pm); s.Appearance.Params.Set(catalogs.Parameters, "body.mass", pm.FromLevel(mass));
            catalogs.Parameters.TryGet("body.muscularity", out ParameterDefinition pu); s.Appearance.Params.Set(catalogs.Parameters, "body.muscularity", pu.FromLevel(musc));
            catalogs.Parameters.TryGet("body.height", out ParameterDefinition ph); s.Appearance.Params.Set(catalogs.Parameters, "body.height", ph.FromLevel(height));
            return s;
        }

        private MovementPersonality Personality(CharacterSpecification s)
        {
            MovementSignature sig = MovementSignatureResolver.Resolve(BodyType.Athletic, s, PlayerAttributes.CreateDefault(), catalogs);
            return MovementPersonalityResolver.Resolve(s, sig, catalogs);
        }

        [Test]
        public void ALightAgilePlayer_BouncesMore_ThanAHeavyMuscularOne()
        {
            Assert.Greater(Personality(Body(0f, 0.1f, 0.5f)).Bounce, Personality(Body(1f, 0.9f, 0.5f)).Bounce);
        }

        [Test]
        public void AllPersonalityValues_StayInTheirSmallRanges_ForEveryExtremeBody()
        {
            foreach (float m in new[] { 0f, 0.5f, 1f })
                foreach (float u in new[] { 0f, 1f })
                    foreach (float h in new[] { 0f, 1f })
                    {
                        MovementPersonality p = Personality(Body(m, u, h));
                        Assert.That(p.PlaybackSpeedScale, Is.InRange(0.9f, 1.1f));
                        Assert.That(p.StrideScale, Is.InRange(0.9f, 1.1f));
                        Assert.That(p.LeanDegrees, Is.InRange(0f, 12f));
                        foreach (float v in new[] { p.Bounce, p.ArmSwing, p.Anticipation, p.Posture }) Assert.That(v, Is.InRange(0f, 1f));
                        Assert.That(p.IdleVariant, Is.InRange(0, MovementPersonalityResolver.IdleVariants - 1));
                    }
        }

        [Test]
        public void ThePersonality_IsDeterministic_AndTheSeedPicksTheIdleVariant()
        {
            CharacterSpecification s = Body(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(Personality(s).IdleVariant, Personality(s).IdleVariant);
            var variants = new System.Collections.Generic.HashSet<int>();
            for (uint seed = 1; seed < 60; seed++) { CharacterSpecification c = s.Clone(); c.AppearanceSeed = seed; variants.Add(Personality(c).IdleVariant); }
            Assert.GreaterOrEqual(variants.Count, 3);
        }

        [Test]
        public void ThePlan_ModulatesPlaybackWithinLimits_AndLeansOnlyWhenMoving()
        {
            CharacterSpecification s = Body(0.5f, 0.5f, 0.5f);
            MovementSignature sig = MovementSignatureResolver.Resolve(BodyType.Athletic, s, PlayerAttributes.CreateDefault(), catalogs);
            MovementPersonality pers = MovementPersonalityResolver.Resolve(s, sig, catalogs);
            AnimationPlan still = resolver.Plan(new AnimationSelectionContext { Speed = 0f, RigId = "fs27_humanoid_v1" }, sig, pers);
            AnimationPlan sprint = resolver.Plan(new AnimationSelectionContext { Speed = 8f, RigId = "fs27_humanoid_v1" }, sig, pers);
            Assert.AreEqual(0f, still.LeanDegrees);
            Assert.Greater(sprint.LeanDegrees, 0f);
            Assert.That(sprint.PlaybackSpeed, Is.InRange(0.8f, 1.2f));
        }

        // ---------------- the rule that matters most ----------------

        [Test]
        public void AnimationAndPersonality_NeverReachMovementCode()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            foreach (string file in new[] { "PlayerLocomotion.cs", "PlayerStats.cs", "MovementTuning.cs", "StaminaSystem.cs", "PlayerRuntimeState.cs" })
            {
                string text = FootballDnaTests.CodeOnly(Path.Combine(core, file));
                foreach (string word in new[] { "MovementPersonality", "AnimationPlan", "CatalogAnimationResolver", "AnimationProfile", "MovementStyle", "FootballDNA", "CharacterSpecification" })
                    Assert.IsFalse(text.Contains(word), file + " must not know about " + word + ": appearance and animation never change how a player moves");
            }
        }

        [Test]
        public void MovementPersonality_HoldsOnlyPresentationNumbers()
        {
            foreach (System.Reflection.FieldInfo f in typeof(MovementPersonality).GetFields())
                Assert.IsTrue(f.FieldType == typeof(float) || f.FieldType == typeof(int), f.Name);
            Assert.IsFalse(typeof(MovementPersonality).GetFields().Any(f => f.Name.ToLowerInvariant().Contains("speed") && !f.Name.ToLowerInvariant().Contains("playback")), "no gameplay speed here");
        }
    }
}
