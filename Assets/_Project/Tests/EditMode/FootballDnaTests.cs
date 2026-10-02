using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class FootballDnaTests
    {
        private CreatorCatalogs catalogs;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
        }

        private static readonly string[] BehaviorIds =
        {
            "StopAndGo", "BodyFeint", "ExplosiveExit", "DelayedRun", "BlindSideRun", "LateBoxArrival", "HoldUpPlay", "FirstTimeFinish",
            "LongRangeShot", "InsideCut", "OutsideCut", "CreativePass", "RiskyThroughBall", "OneTouchCombination", "AggressivePress"
        };

        // ================= Signature behaviour catalog =================

        [Test]
        public void TheCatalog_HasTheFifteenRequestedBehaviours_WithUniqueIds()
        {
            CollectionAssert.AreEquivalent(BehaviorIds, catalogs.Behaviors.All.Select(b => b.Id).ToArray());
            Assert.AreEqual(15, catalogs.Behaviors.Count);
        }

        [Test]
        public void EveryBehaviour_IsPlanned_NotPretendingToBeExecutable()
        {
            foreach (var b in catalogs.Behaviors.All)
                Assert.AreEqual(BehaviorRuntimeStatus.Planned, b.Status, b.Id + " cannot be executed yet");
        }

        [Test]
        public void EveryBehaviour_IsFullyDescribed_AndPointsOnlyAtRealThings()
        {
            foreach (var b in catalogs.Behaviors.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(b.Description), b.Id);
                Assert.IsNotEmpty(b.Drivers, b.Id);
                Assert.AreEqual(1f, b.Drivers.Sum(d => d.Weight), 1e-4f, b.Id + ": driver weights add up to 1");
                foreach (var d in b.Drivers)
                {
                    Assert.IsTrue(catalogs.Parameters.TryGet(d.ParameterId, out var p), b.Id + " -> " + d.ParameterId);
                    Assert.AreEqual(ParameterDomain.FootballDna, p.Domain, d.ParameterId);
                    Assert.Greater(d.Weight, 0f);
                }
                foreach (var n in b.Needs) Assert.That(n.Enough, Is.InRange(PlayerAttributes.Min, PlayerAttributes.Max), b.Id);
                Assert.IsNotEmpty(b.AnimationTags, b.Id + " names what animation it will need");
                Assert.AreNotEqual(BehaviorContext.None, b.Requires | b.Prefers, b.Id + " has a context");
            }
        }

        [Test]
        public void TheBehaviourCatalogRefusesDuplicates()
        {
            var c = new SignatureBehaviorCatalog();
            Assert.IsTrue(c.TryAdd(new SignatureBehaviorDefinition { Id = "X" }));
            Assert.IsFalse(c.TryAdd(new SignatureBehaviorDefinition { Id = "X" }));
            Assert.IsFalse(c.TryAdd(new SignatureBehaviorDefinition { Id = "" }));
            Assert.IsFalse(c.TryAdd(null));
        }

        [Test]
        public void ANewBehaviour_IsOneRowOfData_AndValidatesLikeTheOthers()
        {
            catalogs.Behaviors.TryAdd(new SignatureBehaviorDefinition
            {
                Id = "Nutmeg", Category = BehaviorCategory.Dribbling, Description = "Passes the ball between a defender's legs.", Status = BehaviorRuntimeStatus.Implemented,
                Requires = BehaviorContext.HasBall | BehaviorContext.FacingDefender, Drivers = new List<WeightedParameter> { new WeightedParameter("dribbling.takeOn", 1f) }
            });
            var spec = catalogs.NewSpecification("c"); spec.Dna.SetBehavior("Nutmeg", 0.9f);
            var r = CharacterSpecificationValidator.Validate(spec, catalogs);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.IsFalse(r.Has(CreatorIssueCode.BehaviorNotExecutableYet), "Implemented behaviours raise no warning");
        }

        // ================= FootballDNA data and validation =================

        [Test]
        public void SetBehavior_NeverListsABehaviourTwice_AndClampsTheWeight()
        {
            var dna = new FootballDNA();
            dna.SetBehavior("StopAndGo", 0.4f);
            dna.SetBehavior("StopAndGo", 2f);
            Assert.AreEqual(1, dna.Behaviors.Count);
            Assert.AreEqual(1f, dna.Behaviors[0].Weight);
            Assert.IsTrue(dna.HasBehavior("StopAndGo"));
            Assert.IsTrue(dna.RemoveBehavior("StopAndGo"));
            Assert.IsFalse(dna.RemoveBehavior("StopAndGo"));
        }

        [Test]
        public void ADefaultDna_IsValid_AndNeutral()
        {
            var r = FootballDNAValidator.Validate(new FootballDNA(), catalogs);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(0.5f, new FootballDNA().Get(catalogs.Parameters, "dribbling.takeOn"));
        }

        [Test]
        public void DnaValidation_FindsUnknownDuplicateAndOutOfRangeBehaviours()
        {
            var dna = new FootballDNA();
            dna.Behaviors.Add(new BehaviorEntry("Nope", 0.5f));
            dna.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.5f));
            dna.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.6f));
            dna.Behaviors.Add(new BehaviorEntry("BodyFeint", 1.5f));
            dna.Behaviors.Add(new BehaviorEntry("InsideCut", float.NaN));
            var r = FootballDNAValidator.Validate(dna, catalogs);
            Assert.AreEqual(1, r.CountOf(CreatorIssueCode.BehaviorUnknown));
            Assert.AreEqual(1, r.CountOf(CreatorIssueCode.BehaviorDuplicate));
            Assert.AreEqual(2, r.CountOf(CreatorIssueCode.BehaviorWeightOutOfRange));
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void ABehaviourThatCannotRunYet_IsAWarning_NotAnError()
        {
            var dna = new FootballDNA(); dna.SetBehavior("StopAndGo", 0.9f);
            var r = FootballDNAValidator.Validate(dna, catalogs);
            Assert.IsTrue(r.IsValid, "data is fine");
            Assert.IsTrue(r.Has(CreatorIssueCode.BehaviorNotExecutableYet));
            Assert.AreEqual(CreatorSeverity.Warning, r.Issues.First(i => i.Code == CreatorIssueCode.BehaviorNotExecutableYet).Severity);
        }

        [Test]
        public void DnaParameters_AreCheckedAgainstTheirDomain_AndTheSchema()
        {
            var dna = new FootballDNA();
            dna.Params.Values["dribbling.takeOn"] = 2f;
            dna.Params.Values["head.scale"] = 1f;
            dna.Params.Values["nope"] = 1f;
            dna.SchemaVersion = "FS27.FootballDNA.v7";
            var r = FootballDNAValidator.Validate(dna, catalogs);
            Assert.IsTrue(r.Has(CreatorIssueCode.ParameterOutOfRange));
            Assert.IsTrue(r.Has(CreatorIssueCode.ParameterWrongDomain));
            Assert.IsTrue(r.Has(CreatorIssueCode.ParameterUnknown));
            Assert.IsTrue(r.Has(CreatorIssueCode.SchemaVersionNewer));
        }

        [Test]
        public void ASignatureBehaviourTheAttributesCannotSupport_IsWarnedAbout()
        {
            var dna = new FootballDNA(); dna.SetBehavior("ExplosiveExit", 0.9f);
            var weak = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Acceleration, 30).With(PlayerAttributeId.Speed, 30);
            var strong = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Acceleration, 92).With(PlayerAttributeId.Speed, 88);
            Assert.IsTrue(FootballDNAValidator.CheckAgainstAttributes(dna, weak, catalogs.Behaviors).Has(CreatorIssueCode.BehaviorRequirementNotMet));
            Assert.AreEqual(0, FootballDNAValidator.CheckAgainstAttributes(dna, strong, catalogs.Behaviors).Count);
            dna.SetBehavior("ExplosiveExit", 0.2f);
            Assert.AreEqual(0, FootballDNAValidator.CheckAgainstAttributes(dna, weak, catalogs.Behaviors).Count, "a weak signature is not worth a warning");
        }

        // ================= No duplication of the Player System =================

        [Test]
        public void TheDna_DoesNotDuplicateTheTwelveAttributes()
        {
            var attributeNames = new HashSet<string>(PlayerAttributeInfo.All.Select(a => a.ToString().ToLowerInvariant()).Concat(new[] { "ballcontrol", "reaction" }));
            foreach (var p in catalogs.Parameters.InDomain(ParameterDomain.FootballDna))
            {
                string leaf = p.Id.Substring(p.Id.IndexOf('.') + 1).ToLowerInvariant();
                Assert.IsFalse(attributeNames.Contains(leaf), p.Id + " would be a second copy of an attribute");
            }
            Assert.IsFalse(typeof(FootballDNA).GetFields().Any(f => f.FieldType == typeof(PlayerAttributes)));
            Assert.IsFalse(typeof(FootballDNA).GetFields().Any(f => f.FieldType == typeof(int)));
        }

        [Test]
        public void TheDna_DoesNotDuplicateTheBehaviourValuesOfThePlayingProfile()
        {
            foreach (var p in catalogs.Parameters.InDomain(ParameterDomain.FootballDna))
            {
                string leaf = p.Id.Substring(p.Id.IndexOf('.') + 1).ToLowerInvariant();
                Assert.AreNotEqual("risk", leaf, p.Id);
                Assert.AreNotEqual("creativity", leaf, p.Id);
                Assert.AreNotEqual("riskpreference", leaf, p.Id);
            }
            Assert.IsFalse(typeof(FootballDNA).GetFields().Any(f => f.Name.IndexOf("risk", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("creativity", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // ================= Behaviour resolver =================

        private static readonly BehaviorContext Duel = BehaviorContext.HasBall | BehaviorContext.FacingDefender | BehaviorContext.OpenSpaceAhead;

        private FootballDNA Dribbler()
        {
            var dna = new FootballDNA();
            dna.Params.Set(catalogs.Parameters, "dribbling.stopAndGo", 0.95f);
            dna.Params.Set(catalogs.Parameters, "dribbling.takeOn", 0.9f);
            dna.SetBehavior("StopAndGo", 0.9f);
            return dna;
        }

        private List<BehaviorCandidate> Rank(FootballDNA dna, PlayerAttributes a, BehaviorContext ctx)
        {
            return BehaviorResolver.Resolve(dna, a, ctx, catalogs.Parameters, catalogs.Behaviors);
        }

        [Test]
        public void TheDribblerFacingADefender_PrefersStopAndGo()
        {
            var ranked = Rank(Dribbler(), PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 90).With(PlayerAttributeId.Control, 85), Duel);
            Assert.AreEqual("StopAndGo", ranked[0].BehaviorId);
            Assert.Greater(ranked[0].Score, 0.6f);
            Assert.That(ranked.Select(c => c.Score), Is.Ordered.Descending);
        }

        [Test]
        public void ABehaviourOutsideItsContext_IsNotACandidate()
        {
            var ranked = Rank(Dribbler(), PlayerAttributes.CreateDefault(), BehaviorContext.OpponentHasBall);
            Assert.IsFalse(ranked.Any(c => c.BehaviorId == "StopAndGo"), "needs the ball and a defender");
            Assert.IsTrue(ranked.Any(c => c.BehaviorId == "AggressivePress"), "defending is possible when the opponent has the ball");
            Assert.AreEqual(0, Rank(Dribbler(), PlayerAttributes.CreateDefault(), BehaviorContext.None).Count(c => c.BehaviorId == "StopAndGo"));
        }

        [Test]
        public void TheSameDnaGivesDifferentChoices_InDifferentSituations()
        {
            var dna = Dribbler(); dna.SetBehavior("LongRangeShot", 0.9f); dna.Params.Set(catalogs.Parameters, "shooting.longShot", 0.95f);
            var attrs = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Shooting, 85).With(PlayerAttributeId.Agility, 85).With(PlayerAttributeId.Control, 85);
            string inDuel = Rank(dna, attrs, Duel)[0].BehaviorId;
            string longRange = Rank(dna, attrs, BehaviorContext.HasBall | BehaviorContext.ShootingRange | BehaviorContext.LongRange)[0].BehaviorId;
            Assert.AreEqual("StopAndGo", inDuel);
            Assert.AreEqual("LongRangeShot", longRange);
        }

        [Test]
        public void TheAttributesDecideHowWellABehaviourFits_NotWhetherItIsWanted()
        {
            var dna = Dribbler();
            var great = Rank(dna, PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 95).With(PlayerAttributeId.Control, 95), Duel).First(c => c.BehaviorId == "StopAndGo").Score;
            var poor = Rank(dna, PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 25).With(PlayerAttributeId.Control, 25), Duel).First(c => c.BehaviorId == "StopAndGo").Score;
            Assert.Greater(great, poor);
            Assert.Greater(poor, 0f, "still a candidate: the player wants it, just cannot do it well");
        }

        [Test]
        public void TheDnaDecidesWhatIsWanted_WithTheSameAttributes()
        {
            var attrs = PlayerAttributes.CreateDefault();
            var feinter = new FootballDNA(); feinter.Params.Set(catalogs.Parameters, "dribbling.bodyFeint", 0.95f); feinter.SetBehavior("BodyFeint", 0.9f);
            var sprinter = new FootballDNA(); sprinter.Params.Set(catalogs.Parameters, "dribbling.changeOfPace", 0.95f); sprinter.Params.Set(catalogs.Parameters, "movement.accelerationTendency", 0.95f); sprinter.SetBehavior("ExplosiveExit", 0.9f);
            Assert.AreEqual("BodyFeint", Rank(feinter, attrs, Duel)[0].BehaviorId);
            Assert.AreEqual("ExplosiveExit", Rank(sprinter, attrs, Duel)[0].BehaviorId);
        }

        [Test]
        public void TheResolverIsDeterministic_AndNeverChangesItsInputs()
        {
            var dna = Dribbler(); var attrs = PlayerAttributes.CreateDefault();
            string before = CharacterSpecificationJson.ToJson(new CharacterSpecification { CharacterId = "c", Dna = dna });
            var a = Rank(dna, attrs, Duel); var b = Rank(dna, attrs, Duel);
            CollectionAssert.AreEqual(a.Select(x => x.BehaviorId + x.Score), b.Select(x => x.BehaviorId + x.Score));
            Assert.AreEqual(before, CharacterSpecificationJson.ToJson(new CharacterSpecification { CharacterId = "c", Dna = dna }));
        }

        [Test]
        public void ScoresStayInZeroToOne_ForAnyInput()
        {
            var dna = new FootballDNA();
            foreach (var p in catalogs.Parameters.InDomain(ParameterDomain.FootballDna)) dna.Params.Set(catalogs.Parameters, p.Id, 1f);
            foreach (var b in catalogs.Behaviors.All) dna.SetBehavior(b.Id, 1f);
            BehaviorContext everything = (BehaviorContext)((1 << 14) - 1);
            foreach (int v in new[] { 1, 50, 99 })
                foreach (var c in Rank(dna, PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, v), everything))
                    Assert.That(c.Score, Is.InRange(0f, 1f), c.BehaviorId);
        }

        [Test]
        public void ABehaviourListedByThePlayer_BeatsOneThatOnlyTheNeutralDnaSuggests()
        {
            var plain = new FootballDNA();
            var listed = new FootballDNA(); listed.SetBehavior("InsideCut", 0.9f);
            var ctx = BehaviorContext.HasBall | BehaviorContext.WideArea | BehaviorContext.FacingDefender;
            Assert.Greater(Rank(listed, PlayerAttributes.CreateDefault(), ctx).First(c => c.BehaviorId == "InsideCut").Score,
                           Rank(plain, PlayerAttributes.CreateDefault(), ctx).First(c => c.BehaviorId == "InsideCut").Score);
        }

        // ================= Architecture rules =================

        private static string CreatorSourceDirectory()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            return core == null ? null : Path.Combine(core, "Creator");
        }

        /// <summary>The source with comment lines removed: the guards are about what the code DOES, not about words in its documentation.</summary>
        internal static string CodeOnly(string path)
        {
            return string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));
        }

        [Test]
        public void NoPlayerNameOrIdIsHardcoded_AnywhereInTheCreatorEngine()
        {
            string dir = CreatorSourceDirectory();
            if (dir == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            string[] banned = { "playerName ==", "playerId ==", "player.Name ==", "player.Id ==", ".Name == \"", ".Id == \"", "Kane", "Yamal", "Messi", "Ronaldo", "Haaland" };
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = CodeOnly(file);
                foreach (string phrase in banned)
                    Assert.IsFalse(text.Contains(phrase), Path.GetFileName(file) + " contains '" + phrase + "'");
            }
        }

        [Test]
        public void TheCreatorEngineHasNoCodeExecution_NoNetwork_NoProviderDependency_AndNoSecrets()
        {
            string dir = CreatorSourceDirectory();
            if (dir == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            string[] banned = { "System.Reflection.Emit", "Assembly.Load", "CSharpScript", "Process.Start", "System.Net", "HttpClient", "WebRequest", "api_key", "apikey", "ApiKey",
                                "Anthropic", "OpenAI", "Gemini", "UnityEngine", "Activator.CreateInstance", "System.Diagnostics.Process" };
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = CodeOnly(file);
                foreach (string phrase in banned)
                    Assert.IsFalse(text.Contains(phrase), Path.GetFileName(file) + " contains '" + phrase + "'");
            }
        }

        [Test]
        public void NothingInTheCreatorEngine_TouchesTheBall_OrTheMatch()
        {
            var creatorTypes = typeof(CharacterSpecification).Assembly.GetTypes().Where(t => t.Namespace == "FS27.Core" && t.Module == typeof(CharacterSpecification).Module)
                .Where(t => new[] { "CharacterSpecification", "PlayerAppearance", "FootballDNA", "DeterministicPromptInterpreter", "ModificationApplier", "BehaviorResolver",
                                    "CharacterRegistry", "CatalogAppearanceResolver", "MovementSignatureResolver", "ActionContactEvent" }.Contains(t.Name)).ToArray();
            Assert.AreEqual(10, creatorTypes.Length);
            string[] forbidden = { "BallState", "BallSimulator", "BallParameters", "MatchController", "MatchState", "MatchRules" };
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type t in creatorTypes)
            {
                var used = new List<Type>();
                used.AddRange(t.GetFields(all).Select(f => f.FieldType));
                used.AddRange(t.GetProperties(all).Select(p => p.PropertyType));
                foreach (MethodInfo m in t.GetMethods(all)) { used.Add(m.ReturnType); used.AddRange(m.GetParameters().Select(p => p.ParameterType)); }
                foreach (Type u in used) Assert.IsFalse(forbidden.Contains(u.Name), t.Name + " uses " + u.Name);
            }
        }
    }
}
