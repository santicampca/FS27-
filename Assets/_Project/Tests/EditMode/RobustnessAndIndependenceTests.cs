using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class RobustnessAndIndependenceTests
    {
        private CreatorPipeline pipeline;

        [SetUp]
        public void SetUp()
        {
            pipeline = CreatorPipeline.CreateDefault();
        }

        // ================= the vocabulary is data: a new language is a new pack =================

        [Test]
        public void ANewLanguagePack_IsEnoughToUnderstandAnotherLanguage()
        {
            var pt = new LanguagePack { Id = "pt" };
            pt.Operator("mais", SemanticIntent.Increase);
            pt.Operator("menos", SemanticIntent.Decrease);
            pt.Operator("deixa|faz", SemanticIntent.Modify, makeIt: true);
            pt.Negator("nao|sem");
            pt.Intensifier("muito", MagnitudeLevel.Much);
            pt.Word("rapido|veloz", "speed", 1);
            pt.Word("alto", "height", 1, SemanticDomain.Visual);
            pt.Filler("o|a|ele|ela|ficar");
            var parser = new SemanticParser(DefaultConcepts.Create(), LanguagePackEs.Create(), LanguagePackEn.Create(), pt);
            SemanticProgram p = parser.Parse("faz ele muito mais rápido");
            Assert.AreEqual("pt", p.Language);
            SemanticCommand c = p.Commands.Single();
            Assert.AreEqual("speed", c.Target);
            Assert.AreEqual(SemanticIntent.Increase, c.Operation);
            Assert.AreEqual(MagnitudeLevel.Much, c.Magnitude);
            // and the rest of the engine did not need to know
            var pipe = new CreatorPipeline(pipeline.Catalogs, DefaultConcepts.Create(), parser);
            CreatorResult r = pipe.Run(new CreatorRequest { Text = "faz ele mais alto" });
            Assert.IsTrue(r.Success, r.DebugReport);
            Assert.Greater(r.Specification.Appearance.Params.GetLevel(pipe.Catalogs.Parameters, "body.height"), 0.5f);
        }

        [Test]
        public void TheSameMeaning_InSpanishAndEnglish_CompilesToTheSamePatch()
        {
            var compiler = new SemanticCompiler(pipeline.Catalogs, DefaultConcepts.Create());
            var parser = SemanticParser.CreateDefault();
            var d = new AuthoringDraft(pipeline.Catalogs.NewSpecification("x"));
            string Ops(string text) => PatchJson.ToJson(compiler.Compile(parser.Parse(text), d).Patch);
            // reasons quote the words, so compare the operations without them
            string Strip(string json) { JsonValue v = Json.Parse(json); foreach (JsonValue o in v.Members["operations"].Items) o.Members.Remove("reason"); return Json.Write(v); }
            Assert.AreEqual(Strip(Ops("hazlo un poco más alto")), Strip(Ops("make him a little taller")));
            Assert.AreEqual(Strip(Ops("no demasiado musculoso")), Strip(Ops("not too muscular")));
            Assert.AreEqual(Strip(Ops("sin barba")), Strip(Ops("without a beard")));
        }

        // ================= patches are data a visual editor can send =================

        [Test]
        public void APatch_RoundTripsThroughJson_AndAppliesTheSame()
        {
            var session = AuthoringSession.CreateDefault(5);
            AuthoringTurn t = session.Submit("Crea un extremo rápido, alto, con pelo rizado");
            var compiler = new SemanticCompiler(session.Current.Spec == null ? null : CreatorCatalogs.CreateDefault(), DefaultConcepts.Create());
            CharacterSpecificationPatch patch = t.Compiled.Patch;
            string json = PatchJson.ToJson(patch);
            var v = new CreatorValidationResult();
            Assert.IsTrue(PatchJson.TryFromJson(json, out CharacterSpecificationPatch back, v), v.ToString());
            Assert.AreEqual(json, PatchJson.ToJson(back));
            CreatorCatalogs cat = CreatorCatalogs.CreateDefault();
            var fresh = new AuthoringDraft(cat.NewSpecification("c-1"));
            Assert.AreEqual(CharacterSpecificationJson.ToJson(PatchApplier.Apply(fresh, patch, cat).Draft.Spec), CharacterSpecificationJson.ToJson(PatchApplier.Apply(fresh, back, cat).Draft.Spec));
            Assert.IsNotNull(compiler);
        }

        [Test]
        public void ABadPatch_IsRefusedClearly()
        {
            foreach (string bad in new[] { "", "nope", "{}", "{\"schemaVersion\":\"FS27.CharacterSpecificationPatch.v1\"}",
                "{\"schemaVersion\":\"FS27.CharacterSpecificationPatch.v1\",\"operations\":[{\"kind\":\"Explode\",\"target\":\"Choice\",\"key\":\"x\"}]}",
                "{\"schemaVersion\":\"FS27.CharacterSpecificationPatch.v1\",\"operations\":[{\"kind\":\"Modify\",\"target\":\"Nope\",\"key\":\"x\"}]}",
                "{\"schemaVersion\":\"FS27.CharacterSpecificationPatch.v1\",\"operations\":[{\"kind\":\"Modify\",\"target\":\"AppearanceParam\",\"key\":\"\"}]}" })
            {
                var v = new CreatorValidationResult();
                Assert.IsFalse(PatchJson.TryFromJson(bad, out CharacterSpecificationPatch p, v), bad);
                Assert.IsNull(p);
            }
        }

        // ================= a hostile mix of words never breaks the engine, and never claims a false success =================

        [Test]
        public void TwoThousandRandomSentences_NeverCrash_NeverFalselySucceed_AndAreDeterministic()
        {
            string[] words = { "más", "menos", "muy", "poco", "no", "sin", "pero", "y", "alto", "bajo", "rápido", "lento", "fuerte", "débil", "pelo", "barba", "largo", "corto", "rizado", "negro", "rubio",
                "extremo", "portero", "delantero", "crea", "hazlo", "quita", "cambia", "mantén", "solo", "todo", "demás", "la", "cara", "estilo", "se", "vea", "en", "los", "duelos", "corra", "driblee", "tanto",
                "agresivo", "creativo", "arriesgado", "cabeza", "grande", "pequeño", "manos", "make", "him", "taller", "faster", "not", "too", "keep", "the", "face", "hair", "xyzzy", "qwerty", "123", "10%", "!!!", "" };
            var rnd = new SeededRandom(12345);
            var cat = pipeline.Catalogs;
            AuthoringDraft draft = pipeline.Run(new CreatorRequest { Text = "Crea un extremo" }).Draft;
            int successes = 0;
            var transcript = new List<string>();
            for (int i = 0; i < 2000; i++)
            {
                int n = 1 + (int)(rnd.NextFloat01() * 9);
                var sentence = string.Join(" ", Enumerable.Range(0, n).Select(_ => words[(int)(rnd.NextFloat01() * words.Length) % words.Length]));
                CreatorResult r = null;
                Assert.DoesNotThrow(() => r = pipeline.Run(new CreatorRequest { Text = sentence, Current = i % 3 == 0 ? null : draft, Seed = (uint)i + 1 }), sentence);
                if (r.Success)
                {
                    successes++;
                    Assert.IsNotNull(r.Specification, sentence);
                    CreatorValidationResult v = CharacterSpecificationValidator.Validate(r.Specification, cat);
                    Assert.IsTrue(v.IsValid, sentence + "\n" + v);
                    Assert.IsFalse(AppearanceNormalizer.Normalize(r.Specification, cat, CompatibilityRules.CreateDefault()).Rejected, sentence);
                    Assert.IsFalse(r.Conflicts.Any(c => c.Blocks), sentence);
                    var rv = new CreatorValidationResult();
                    Assert.IsTrue(RuntimeCharacterJson.TryFromJson(r.RuntimeData.Json, pipeline.Index, cat, out CharacterSpecification back, rv), sentence + "\n" + rv);
                    if (i % 3 != 0) draft = r.Draft;
                }
                else Assert.IsEmpty(r.Errors.Count == 0 ? new[] { "a result without success must say why: " + sentence } : new string[0]);
                transcript.Add(sentence + "=>" + r.Success);
            }
            Assert.Greater(successes, 100, "the vocabulary is rich enough that many random sentences do mean something");
            Assert.Less(successes, 2000, "and many do not");
            TestContext.WriteLine("random sentences: " + successes + " understood of 2000");
        }

        // ================= difficulty, assists and gameplay stay out of the Creator's way, and the other way round =================

        private static string CoreDir()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            return core;
        }

        [Test]
        public void Difficulty_NeverTouchesAttributesOverallOrDna()
        {
            string dir = Path.Combine(CoreDir(), "Difficulty");
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = FootballDnaTests.CodeOnly(file);
                foreach (string word in new[] { "FootballDNA", "CharacterSpecification", "BehaviorEntry", "SignatureBehavior", "BehaviorDecisionEngine", "SemanticProgram", "CreatorPipeline" })
                    Assert.IsFalse(text.Contains(word), Path.GetFileName(file) + " must not know about " + word);
            }
        }

        [Test]
        public void TheBehaviourEngine_TakesNoDifficulty_AndNoAssistSettings()
        {
            foreach (Type t in new[] { typeof(BehaviorDecisionEngine), typeof(FootballActionResolver), typeof(FootballContextAnalyzer) })
                foreach (var m in t.GetMethods().Where(m => m.DeclaringType == t))
                    foreach (var p in m.GetParameters())
                        Assert.IsFalse(p.ParameterType.Name.Contains("Difficulty") || p.ParameterType.Name.Contains("Assist"), t.Name + "." + m.Name + " takes " + p.ParameterType.Name);
        }

        [Test]
        public void TheActionResolver_NeverWritesTheAim_SoAssistAndDnaDoNotFight()
        {
            CreatorCatalogs cat = pipeline.Catalogs;
            var dna = new FootballDNA();
            dna.Params.Set(cat.Parameters, "shooting.finesse", 1f);
            dna.Params.Set(cat.Parameters, "passing.throughBall", 1f);
            var shot = new PlayerIntent(new Vec2(1f, 0f)).WithAction(FootballActionKind.Shot);
            shot.Shot.Aim = new Vec2(18f, 2f);
            var pass = new PlayerIntent(new Vec2(1f, 0f)).WithAction(FootballActionKind.ShortPass);
            pass.Pass.Target = new Vec2(9f, -3f);
            var ctx = new ContextAnalysis();
            ActionSelection s = FootballActionResolver.Resolve(shot, dna, cat.Parameters, new BehaviorDecision(), ctx, true);
            ActionSelection p = FootballActionResolver.Resolve(pass, dna, cat.Parameters, new BehaviorDecision(), ctx, true);
            Assert.AreEqual(shot.Shot.Aim.X, s.Shot.Aim.X); Assert.AreEqual(shot.Shot.Aim.Y, s.Shot.Aim.Y);
            Assert.AreEqual(pass.Pass.Target.X, p.Pass.Target.X); Assert.AreEqual(pass.Pass.Target.Y, p.Pass.Target.Y);
            Assert.IsTrue(s.Shot.Placed, "the DNA flavoured the shot");
        }

        [Test]
        public void TheAimAssist_SnapsToTheNearestCandidateInsideTheAngle_AndOtherwiseLeavesTheAimAlone()
        {
            Vec2 origin = Vec2.Zero;
            var mates = new List<Vec2> { new Vec2(10f, 3f), new Vec2(10f, 0.5f), new Vec2(-5f, 0f) };
            Vec2 snapped = IntentAssist.SnapAim(origin, new Vec2(10f, 1f), mates, 15f);
            Assert.AreEqual(10f, snapped.X); Assert.AreEqual(0.5f, snapped.Y);
            Vec2 far = IntentAssist.SnapAim(origin, new Vec2(0f, 10f), mates, 15f);
            Assert.AreEqual(0f, far.X); Assert.AreEqual(10f, far.Y);
            Assert.AreEqual(10f, IntentAssist.SnapAim(origin, new Vec2(10f, 1f), mates, 0f).X);
            Assert.AreEqual(10f, IntentAssist.SnapAim(origin, new Vec2(10f, 1f), null, 15f).X);
        }

        // ================= the AI flow, end to end: AI -> decision -> DNA influence -> PlayerIntent -> movement / action =================

        private sealed class DnaDrivenSource : IIntentSource
        {
            private readonly CreatorCatalogs catalogs;
            private readonly FootballDNA dna;
            private readonly PlayerAttributes attributes;
            private readonly BehaviorMemory memory = new BehaviorMemory();
            public FootballContext Context = new FootballContext();
            public Vec2 Move;
            public bool Human;
            public ActionSelection LastSelection;

            public DnaDrivenSource(CreatorCatalogs c, FootballDNA d, PlayerAttributes a) { catalogs = c; dna = d; attributes = a; }

            public PlayerIntent ReadIntent()
            {
                ContextAnalysis analysis = FootballContextAnalyzer.Analyze(Context, new FieldDimensions());
                BehaviorDecision decision = BehaviorDecisionEngine.Decide(dna, attributes, analysis, memory, catalogs);
                PlayerIntent intent = new PlayerIntent(Move);
                LastSelection = FootballActionResolver.Resolve(intent, dna, catalogs.Parameters, decision, analysis, Human);
                if (LastSelection.FromBehavior) memory.Record(LastSelection.BehaviorId, 3f);
                return FootballActionResolver.ApplyTo(intent, LastSelection);
            }
        }

        [Test]
        public void TheWholeAiFlow_ProducesAnIntentThatSurvivesTheInputPipeline_AndKeepsTheStickInChargeOfSpeed()
        {
            CreatorCatalogs cat = pipeline.Catalogs;
            var dna = new FootballDNA();
            dna.Params.Set(cat.Parameters, "dribbling.stopAndGo", 0.95f); dna.Params.Set(cat.Parameters, "dribbling.takeOn", 0.9f); dna.Params.Set(cat.Parameters, "dribbling.takeOnRisk", 0.9f);
            dna.SetBehavior("StopAndGo", 0.9f);
            var attrs = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 90).With(PlayerAttributeId.Control, 85);
            var src = new DnaDrivenSource(cat, dna, attrs) { Move = new Vec2(0.4f, 0f) };
            src.Context.PlayerPosition = new Vec2(0f, 0f); src.Context.PlayerHasBall = true; src.Context.Possession = PossessionState.Own;
            src.Context.Opponents.Add(new Vec2(3f, 0f));
            src.Context.BallPosition = src.Context.PlayerPosition;

            PlayerIntent raw = src.ReadIntent();
            Assert.AreEqual(FootballActionKind.Dribble, raw.Action);
            Assert.AreEqual("StopAndGo", raw.BehaviorId);

            // through the existing input pipeline: nothing about the action is lost, and the movement is still just the stick
            PlayerIntent clean = IntentMixer.Sanitize(raw);
            Assert.AreEqual(raw.Action, clean.Action);
            Assert.AreEqual(raw.Style, clean.Style);
            Assert.AreEqual(raw.BehaviorId, clean.BehaviorId);
            Assert.AreEqual(0.4f, clean.MoveMagnitude, 1e-5f);

            // the movement code sees the same speed with or without the action fields: the DNA never changes how fast the player goes
            var tuning = new MovementTuning();
            var stats = PlayerStats.Resolve(attrs, tuning);
            var a = new PlayerRuntimeState(); a.Reset(stats, 0f);
            var b = new PlayerRuntimeState(); b.Reset(stats, 0f);
            for (int i = 0; i < 60; i++) { PlayerLocomotion.Step(a, stats, tuning, clean, 1f / 60f); PlayerLocomotion.Step(b, stats, tuning, new PlayerIntent(new Vec2(0.4f, 0f)), 1f / 60f); }
            Assert.AreEqual(b.Speed, a.Speed, 1e-5f);
            Assert.AreEqual(b.Heading, a.Heading, 1e-5f);
            Assert.AreEqual(b.Stamina, a.Stamina, 1e-5f);

            // a cooldown follows: asked again straight away, the same behaviour is not repeated
            Assert.AreNotEqual("StopAndGo", src.ReadIntent().BehaviorId);
        }

        [Test]
        public void ForAHumanPlayer_TheSameFlowNeverInventsAnAction()
        {
            var src = new DnaDrivenSource(pipeline.Catalogs, new FootballDNA(), PlayerAttributes.CreateDefault()) { Human = true, Move = new Vec2(1f, 0f) };
            src.Context.PlayerHasBall = true;
            PlayerIntent i = src.ReadIntent();
            Assert.AreEqual(FootballActionKind.None, i.Action);
            Assert.AreEqual(1f, i.MoveMagnitude, 1e-5f);
        }

        [Test]
        public void SanitisingAnIntent_StillRemovesNonFiniteMovement()
        {
            var bad = new PlayerIntent { Move = new Vec2(float.NaN, 0f), Action = FootballActionKind.Shot };
            Assert.AreEqual(FootballActionKind.None, IntentMixer.Sanitize(bad).Action, "a corrupt intent becomes 'no input' entirely");
            Assert.AreEqual(0f, IntentMixer.Sanitize(bad).MoveMagnitude);
        }

        [Test]
        public void ANamedReference_IsNotSilentlyPretendedTo_BeSupported()
        {
            var p = new SemanticProgram();
            p.Commands.Add(new SemanticCommand { Id = 1, Intent = SemanticIntent.Increase, Operation = SemanticIntent.Increase, Target = "height", Reference = EntityReference.Named, ReferenceName = "the captain" });
            CompileResult r = new SemanticCompiler(pipeline.Catalogs, DefaultConcepts.Create()).Compile(p, new AuthoringDraft(pipeline.Catalogs.NewSpecification("x")));
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("Named references")));
        }

        [Test]
        public void TheCreatorEngine_NeverReadsTheClock_OrAnUnseededRandom()
        {
            string dir = Path.Combine(CoreDir(), "Creator");
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string text = FootballDnaTests.CodeOnly(file);
                foreach (string word in new[] { "DateTime.Now", "DateTime.UtcNow", "Stopwatch", "new Random(", "Guid.NewGuid", "Environment.TickCount", "System.Random" })
                    Assert.IsFalse(text.Contains(word), Path.GetFileName(file) + " uses '" + word + "': generation must be reproducible");
            }
        }
    }
}
