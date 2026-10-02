using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class FootballDna2Tests
    {
        private CreatorCatalogs catalogs;
        private FieldDimensions field;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            field = new FieldDimensions();
        }

        // ---------------- schema v2 and migration ----------------

        private const string V1Json = "{\"schemaVersion\":\"FS27.CharacterSpecification.v1\",\"characterId\":\"old-1\",\"baseModelId\":\"fs27_base_a\","
            + "\"appearance\":{\"styleId\":\"FS27_CARTOON_SPORTS\",\"params\":{\"body.height\":1.05},\"choices\":{},\"colors\":{}},"
            + "\"footballDna\":{\"schemaVersion\":\"FS27.FootballDNA.v1\",\"params\":{\"dribbling.takeOn\":0.9},\"behaviors\":[{\"id\":\"StopAndGo\",\"weight\":0.8}]}}";

        [Test]
        public void AVersion1File_LoadsAndIsMigratedToVersion2()
        {
            var r = new CreatorValidationResult();
            Assert.IsTrue(CharacterSpecificationJson.TryFromJson(V1Json, new SchemaMigrator(), out CharacterSpecification spec, r), r.ToString());
            Assert.AreEqual(CharacterSpecification.CurrentSchema, spec.SchemaVersion);
            Assert.AreEqual(FootballDNA.CurrentSchema, spec.Dna.SchemaVersion);
            Assert.AreEqual(0.8f, spec.Dna.Behaviors.Single().Weight, 1e-4f);
            Assert.AreEqual(BehaviorEntry.Unset, spec.Dna.Behaviors.Single().Priority, "an old entry has no overrides");
            Assert.IsTrue(CharacterSpecificationValidator.Validate(spec, catalogs).IsValid);
        }

        [Test]
        public void ARichVersion2Character_RoundTripsExactly()
        {
            CharacterSpecification s = catalogs.NewSpecification("rich-1");
            s.GenerationSeed = 4000000001u; s.AppearanceSeed = 7; s.BehaviorSeed = 99;
            s.Dna.Params.Set(catalogs.Parameters, "dribbling.takeOn", 0.9f);
            s.Dna.ParamConfidence["dribbling.takeOn"] = 0.6f;
            s.Dna.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.8f)
            {
                Priority = 8, Risk = 0.55f, CooldownSeconds = 4.5f, Confidence = 0.7f, Origin = "observed",
                Condition = new BehaviorCondition { Requires = BehaviorContext.OpenSpaceAhead, Forbids = BehaviorContext.UnderPressure, Prefers = BehaviorContext.WideArea, Threshold = 0.3f }
            });
            s.Dna.Behaviors.Add(new BehaviorEntry("ExplosiveExit", 0.6f));
            s.Dna.Sequences.Add(new BehaviorSequence { Id = "mine", Steps = { "StopAndGo", "ExplosiveExit" }, MaxGapSeconds = 1.1f, Weight = 0.7f });
            Assert.IsTrue(CharacterSpecificationValidator.Validate(s, catalogs).IsValid, CharacterSpecificationValidator.Validate(s, catalogs).ToString());

            string json = CharacterSpecificationJson.ToJson(s);
            var r = new CreatorValidationResult();
            Assert.IsTrue(CharacterSpecificationJson.TryFromJson(json, new SchemaMigrator(), out CharacterSpecification back, r), r.ToString());
            Assert.AreEqual(json, CharacterSpecificationJson.ToJson(back), "round trip must be byte-identical");
            Assert.AreEqual(4000000001u, back.GenerationSeed);
            BehaviorEntry e = back.Dna.Behaviors[0];
            Assert.AreEqual(8f, e.Priority);
            Assert.AreEqual(BehaviorContext.OpenSpaceAhead, e.Condition.Requires);
            Assert.AreEqual(0.6f, back.Dna.ParamConfidence["dribbling.takeOn"], 1e-4f);
            Assert.AreEqual(1, back.Dna.Sequences.Count);
        }

        [Test]
        public void APlainBehaviour_StaysTiny_InTheJson()
        {
            CharacterSpecification s = catalogs.NewSpecification("plain-1");
            s.Dna.SetBehavior("StopAndGo", 0.8f);
            string json = CharacterSpecificationJson.ToJson(s);
            StringAssert.DoesNotContain("priority", json);
            StringAssert.DoesNotContain("condition", json);
            StringAssert.DoesNotContain("sequences", json);
            Assert.Less(json.Length, 700);
        }

        [Test]
        public void TheRuntimeVersion_KeepsTheDnaSettingsAndTheSeeds_ButNoAuthoring()
        {
            CharacterSpecification s = catalogs.NewSpecification("rt-1");
            s.GenerationSeed = 5;
            s.Dna.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.8f) { Priority = 8 });
            s.Authoring = new AuthoringData { Prompt = "secret prompt" };
            CharacterSpecification rt = s.ForRuntime();
            Assert.IsNull(rt.Authoring);
            Assert.AreEqual(8f, rt.Dna.Behaviors[0].Priority);
            Assert.AreEqual(5u, rt.GenerationSeed);
            Assert.AreNotSame(s.Dna.Behaviors[0], rt.Dna.Behaviors[0], "a runtime copy must not share entries");
        }

        // ---------------- validation of the new fields ----------------

        [Test]
        public void BadBehaviourSettings_AreReported()
        {
            CharacterSpecification s = catalogs.NewSpecification("bad-1");
            s.Dna.Behaviors.Add(new BehaviorEntry("StopAndGo", 0.8f) { Priority = 12, Risk = 2f, CooldownSeconds = 500f, Confidence = 2f });
            s.Dna.Behaviors.Add(new BehaviorEntry("BodyFeint", 0.5f) { Condition = new BehaviorCondition { Requires = BehaviorContext.WideArea, Forbids = BehaviorContext.WideArea, Threshold = 3f } });
            CreatorValidationResult r = CharacterSpecificationValidator.Validate(s, catalogs);
            Assert.IsFalse(r.IsValid);
            Assert.IsTrue(r.Has(CreatorIssueCode.BehaviorSettingOutOfRange));
            Assert.IsTrue(r.Has(CreatorIssueCode.ConfidenceOutOfRange));
            Assert.IsTrue(r.Has(CreatorIssueCode.BehaviorConditionContradictory));
        }

        [Test]
        public void BadSequences_AreReported()
        {
            CharacterSpecification s = catalogs.NewSpecification("bad-2");
            s.Dna.Sequences.Add(new BehaviorSequence { Id = "a", Steps = { "StopAndGo" } });
            s.Dna.Sequences.Add(new BehaviorSequence { Id = "a", Steps = { "StopAndGo", "Nope" } });
            CreatorValidationResult r = CharacterSpecificationValidator.Validate(s, catalogs);
            Assert.IsTrue(r.Has(CreatorIssueCode.SequenceInvalid));
            Assert.IsTrue(r.Has(CreatorIssueCode.BehaviorUnknown));
        }

        // ---------------- context analysis ----------------

        private FootballContext Ctx(float x, float y, params (float, float)[] opponents)
        {
            var c = new FootballContext { PlayerPosition = new Vec2(x, y), PlayerHasBall = true, Possession = PossessionState.Own, AttackSign = 1, BallPosition = new Vec2(x, y) };
            foreach ((float ox, float oy) in opponents) c.Opponents.Add(new Vec2(ox, oy));
            return c;
        }

        [Test]
        public void TheAnalyzer_FindsBoxWideAndGoalDistance()
        {
            ContextAnalysis inside = FootballContextAnalyzer.Analyze(Ctx(17f, 0f), field);
            Assert.IsTrue((inside.Flags & BehaviorContext.InsideBox) != 0);
            Assert.IsTrue((inside.Flags & BehaviorContext.HasBall) != 0);
            Assert.AreEqual(3f, inside.DistanceToGoal, 1e-3f);
            ContextAnalysis wide = FootballContextAnalyzer.Analyze(Ctx(5f, 11f), field);
            Assert.IsTrue((wide.Flags & BehaviorContext.WideArea) != 0);
            Assert.IsTrue((wide.Flags & BehaviorContext.InsideBox) == 0);
            Assert.AreEqual(PitchZone.Wing, wide.Zone);
        }

        [Test]
        public void TheAnalyzer_UnderstandsDefendersInFrontAndBehind()
        {
            ContextAnalysis faced = FootballContextAnalyzer.Analyze(Ctx(0f, 0f, (1.5f, 0f)), field);
            Assert.IsTrue((faced.Flags & BehaviorContext.FacingDefender) != 0);
            Assert.IsTrue((faced.Flags & BehaviorContext.UnderPressure) != 0);
            Assert.IsTrue((faced.Flags & BehaviorContext.OpenSpaceAhead) == 0);

            ContextAnalysis free = FootballContextAnalyzer.Analyze(Ctx(0f, 0f, (-8f, 0f)), field);
            Assert.IsTrue((free.Flags & BehaviorContext.OpenSpaceAhead) != 0);
            Assert.IsTrue((free.Flags & BehaviorContext.FacingDefender) == 0);

            // two defenders, the player is beyond the second-last one (the last one is the keeper)
            ContextAnalysis behind = FootballContextAnalyzer.Analyze(Ctx(10f, 0f, (6f, 3f), (19f, 0f)), field);
            Assert.IsTrue((behind.Flags & BehaviorContext.BehindDefenderLine) != 0);
        }

        [Test]
        public void TheAnalyzer_WorksForATeamAttackingTheOtherWay()
        {
            FootballContext c = Ctx(-17f, 0f);
            c.AttackSign = -1;
            Assert.IsTrue((FootballContextAnalyzer.Analyze(c, field).Flags & BehaviorContext.InsideBox) != 0);
        }

        [Test]
        public void ReceivingTheBall_NeedsTheBallComing_AndOurTeamHavingIt()
        {
            FootballContext c = Ctx(0f, 0f);
            c.PlayerHasBall = false; c.BallIncoming = true;
            ContextAnalysis a = FootballContextAnalyzer.Analyze(c, field);
            Assert.IsTrue((a.Flags & BehaviorContext.ReceivingBall) != 0);
            Assert.IsTrue((a.Flags & BehaviorContext.TeammateHasBall) != 0);
        }

        // ---------------- decisions ----------------

        private FootballDNA Dribbler()
        {
            var dna = new FootballDNA();
            dna.Params.Set(catalogs.Parameters, "dribbling.stopAndGo", 0.95f);
            dna.Params.Set(catalogs.Parameters, "dribbling.takeOn", 0.9f);
            dna.Params.Set(catalogs.Parameters, "dribbling.takeOnRisk", 0.9f);
            dna.SetBehavior("StopAndGo", 0.9f);
            return dna;
        }

        private static ContextAnalysis Duel(float pressure = 0.2f)
        {
            return new ContextAnalysis { Flags = BehaviorContext.HasBall | BehaviorContext.FacingDefender | BehaviorContext.OpenSpaceAhead, Pressure01 = pressure };
        }

        private static PlayerAttributes Good()
        {
            return PlayerAttributes.CreateDefault().With(PlayerAttributeId.Agility, 90).With(PlayerAttributeId.Control, 85);
        }

        [Test]
        public void ADribbler_FacingADefender_ChoosesStopAndGo_AndSaysWhy()
        {
            BehaviorDecision d = BehaviorDecisionEngine.Decide(Dribbler(), Good(), Duel(), new BehaviorMemory(), catalogs);
            Assert.IsNotNull(d.Chosen);
            Assert.AreEqual("StopAndGo", d.Chosen.BehaviorId);
            Assert.Greater(d.Chosen.PlayerAffinity, 0.5f);
            Assert.Greater(d.Chosen.ContextMatch, 0f);
            Assert.AreEqual(FootballActionKind.Dribble, d.Chosen.Action);
            StringAssert.Contains("StopAndGo", d.Explanation);
        }

        [Test]
        public void TheSameInputs_AlwaysGiveTheSameDecision()
        {
            string Run()
            {
                BehaviorDecision d = BehaviorDecisionEngine.Decide(Dribbler(), Good(), Duel(), new BehaviorMemory(), catalogs);
                return string.Join(",", d.Candidates.Select(c => c.BehaviorId + ":" + c.Utility.ToString("0.0000")));
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void ARuledOutSituation_IsExplained_NotSilentlyDropped()
        {
            BehaviorDecision d = BehaviorDecisionEngine.Decide(Dribbler(), Good(), new ContextAnalysis { Flags = BehaviorContext.None }, new BehaviorMemory(), catalogs);
            Assert.IsNull(d.Chosen);
            Assert.IsTrue(d.Rejected.Any(r => r.BehaviorId == "StopAndGo" && r.Reason.Contains("situation")));
            StringAssert.Contains("play normally", d.Explanation);
        }

        [Test]
        public void ACooldown_RulesTheBehaviourOut_UntilItEnds()
        {
            var memory = new BehaviorMemory();
            memory.Record("StopAndGo", 3f);
            BehaviorDecision d = BehaviorDecisionEngine.Decide(Dribbler(), Good(), Duel(), memory, catalogs);
            Assert.IsTrue(d.Rejected.Any(r => r.BehaviorId == "StopAndGo" && r.Reason == "cooling down"));
            memory.Tick(3.1f);
            Assert.AreEqual("StopAndGo", BehaviorDecisionEngine.Decide(Dribbler(), Good(), Duel(), memory, catalogs).Chosen.BehaviorId);
        }

        [Test]
        public void APlayersOwnForbiddenSituation_AndThreshold_AreHonoured()
        {
            FootballDNA dna = Dribbler();
            dna.TryGetBehavior("StopAndGo", out BehaviorEntry e);
            e.Condition = new BehaviorCondition { Forbids = BehaviorContext.OpenSpaceAhead };
            BehaviorDecision d = BehaviorDecisionEngine.Decide(dna, Good(), Duel(), new BehaviorMemory(), catalogs);
            Assert.IsTrue(d.Rejected.Any(r => r.BehaviorId == "StopAndGo" && r.Reason.Contains("forbidden")));

            e.Condition = new BehaviorCondition { Threshold = 0.99f };
            d = BehaviorDecisionEngine.Decide(dna, Good(), Duel(), new BehaviorMemory(), catalogs);
            Assert.IsTrue(d.Rejected.Any(r => r.BehaviorId == "StopAndGo" && r.Reason.Contains("threshold")));
        }

        [Test]
        public void ABehaviourForbiddenByItsDefinition_IsNotChosen()
        {
            var dna = new FootballDNA();
            dna.Params.Set(catalogs.Parameters, "shooting.longShot", 1f);
            dna.SetBehavior("LongRangeShot", 1f);
            var inBox = new ContextAnalysis { Flags = BehaviorContext.HasBall | BehaviorContext.ShootingRange | BehaviorContext.LongRange | BehaviorContext.InsideBox };
            BehaviorDecision d = BehaviorDecisionEngine.Decide(dna, PlayerAttributes.CreateDefault().With(PlayerAttributeId.Shooting, 90), inBox, new BehaviorMemory(), catalogs);
            Assert.IsFalse(d.Candidates.Any(c => c.BehaviorId == "LongRangeShot"));
        }

        [Test]
        public void ASequence_MakesTheFollowUpMoreLikely_OnlyInsideItsTimeWindow()
        {
            FootballDNA dna = Dribbler();
            dna.Params.Set(catalogs.Parameters, "dribbling.changeOfPace", 0.6f);
            dna.Params.Set(catalogs.Parameters, "movement.accelerationTendency", 0.6f);
            dna.SetBehavior("ExplosiveExit", 0.6f);
            dna.Sequences.Add(new BehaviorSequence { Id = "s", Steps = { "StopAndGo", "ExplosiveExit" }, MaxGapSeconds = 1.2f, Weight = 0.8f });
            var open = new ContextAnalysis { Flags = BehaviorContext.HasBall | BehaviorContext.OpenSpaceAhead };

            float none = BehaviorDecisionEngine.Decide(dna, Good(), open, new BehaviorMemory(), catalogs).Candidates.First(c => c.BehaviorId == "ExplosiveExit").Score;
            var recent = new BehaviorMemory();
            recent.Record("StopAndGo", 0f);
            recent.Tick(0.5f);
            BehaviorDecision after = BehaviorDecisionEngine.Decide(dna, Good(), open, recent, catalogs);
            float boosted = after.Candidates.First(c => c.BehaviorId == "ExplosiveExit").Score;
            Assert.Greater(boosted, none);
            Assert.Greater(after.Candidates.First(c => c.BehaviorId == "ExplosiveExit").SequenceBoost, 0f);

            recent.Tick(2f);
            float late = BehaviorDecisionEngine.Decide(dna, Good(), open, recent, catalogs).Candidates.First(c => c.BehaviorId == "ExplosiveExit").Score;
            Assert.AreEqual(none, late, 1e-5f, "too late: no boost");
        }

        [Test]
        public void RiskUnderPressure_HoldsBackACautiousPlayer_NotARecklessOne()
        {
            FootballDNA Pass(float appetite)
            {
                var dna = new FootballDNA();
                dna.Params.Set(catalogs.Parameters, "passing.throughBall", 0.9f);
                dna.Params.Set(catalogs.Parameters, "passing.risky", appetite);
                dna.Params.Set(catalogs.Parameters, "dribbling.takeOnRisk", appetite);
                dna.SetBehavior("RiskyThroughBall", 0.9f);
                return dna;
            }
            var ctx = new ContextAnalysis { Flags = BehaviorContext.HasBall | BehaviorContext.BehindDefenderLine, Pressure01 = 0.9f };
            var attrs = PlayerAttributes.CreateDefault().With(PlayerAttributeId.Passing, 90);
            float cautious = BehaviorDecisionEngine.Decide(Pass(0.1f), attrs, ctx, new BehaviorMemory(), catalogs).Candidates.First(c => c.BehaviorId == "RiskyThroughBall").Score;
            float reckless = BehaviorDecisionEngine.Decide(Pass(1f), attrs, ctx, new BehaviorMemory(), catalogs).Candidates.First(c => c.BehaviorId == "RiskyThroughBall").Score;
            Assert.Greater(reckless, cautious);
        }

        [Test]
        public void WithARandomSource_TheChoiceVaries_ButIsReproducibleBySeed()
        {
            FootballDNA dna = Dribbler();
            dna.Params.Set(catalogs.Parameters, "dribbling.bodyFeint", 0.95f);
            dna.SetBehavior("BodyFeint", 0.9f);
            PlayerAttributes skilled = Good().With(PlayerAttributeId.Technique, 90);
            string Pick(uint seed) => BehaviorDecisionEngine.Decide(dna, skilled, Duel(), new BehaviorMemory(), catalogs, new SeededRandom(StableHash.Of("seed" + seed))).Chosen.BehaviorId;
            Assert.AreEqual(Pick(5), Pick(5));
            var picks = Enumerable.Range(1, 40).Select(i => Pick((uint)i)).Distinct().ToList();
            Assert.GreaterOrEqual(picks.Count, 2, "near-equal candidates should both appear across seeds");
        }

        // ---------------- from decision to intent ----------------

        [Test]
        public void ThePlayerIntent_StaysBackwardCompatible()
        {
            var i = new PlayerIntent(new Vec2(3f, 0f));
            Assert.AreEqual(1f, i.MoveMagnitude, 1e-5f);
            Assert.AreEqual(FootballActionKind.None, i.Action);
            Assert.IsFalse(i.HasAction);
            Assert.IsNull(i.BehaviorId);
            Assert.IsFalse(PlayerIntent.None.HasAction);
        }

        [Test]
        public void ForAnAiPlayer_TheChosenBehaviour_BecomesTheAction_AndTheMovementIsUntouched()
        {
            FootballDNA dna = Dribbler();
            BehaviorDecision d = BehaviorDecisionEngine.Decide(dna, Good(), Duel(), new BehaviorMemory(), catalogs);
            var move = new PlayerIntent(new Vec2(0.6f, 0.2f));
            ActionSelection sel = FootballActionResolver.Resolve(move, dna, catalogs.Parameters, d, Duel(), humanControlled: false);
            PlayerIntent result = FootballActionResolver.ApplyTo(move, sel);
            Assert.AreEqual(FootballActionKind.Dribble, result.Action);
            Assert.AreEqual(MovementStyle.Stop, result.Style);
            Assert.AreEqual("StopAndGo", result.BehaviorId);
            Assert.AreEqual(move.Move.X, result.Move.X);
            Assert.AreEqual(move.Move.Y, result.Move.Y);
            Assert.IsTrue(sel.FromBehavior);
        }

        [Test]
        public void ForAPerson_TheRequestedAction_IsNeverReplaced()
        {
            FootballDNA dna = Dribbler();
            BehaviorDecision d = BehaviorDecisionEngine.Decide(dna, Good(), Duel(), new BehaviorMemory(), catalogs);
            PlayerIntent pass = new PlayerIntent(new Vec2(1f, 0f)).WithAction(FootballActionKind.ShortPass);
            ActionSelection sel = FootballActionResolver.Resolve(pass, dna, catalogs.Parameters, d, Duel(), humanControlled: true);
            Assert.AreEqual(FootballActionKind.ShortPass, sel.Action);
            Assert.IsTrue(sel.KeptRequestedAction);
            Assert.IsNull(sel.BehaviorId, "a dribbling behaviour must not flavour a pass");

            ActionSelection none = FootballActionResolver.Resolve(new PlayerIntent(new Vec2(1f, 0f)), dna, catalogs.Parameters, d, Duel(), humanControlled: true);
            Assert.AreEqual(FootballActionKind.None, none.Action, "no action is invented for a person");
        }

        [Test]
        public void ForAPerson_ADribbleIsFlavouredByTheMatchingBehaviour()
        {
            FootballDNA dna = Dribbler();
            BehaviorDecision d = BehaviorDecisionEngine.Decide(dna, Good(), Duel(), new BehaviorMemory(), catalogs);
            PlayerIntent dribble = new PlayerIntent(new Vec2(1f, 0f)).WithAction(FootballActionKind.Dribble);
            ActionSelection sel = FootballActionResolver.Resolve(dribble, dna, catalogs.Parameters, d, Duel(), humanControlled: true);
            Assert.AreEqual(FootballActionKind.Dribble, sel.Action);
            Assert.AreEqual(MovementStyle.Stop, sel.Style);
            Assert.AreEqual(0.9f, sel.Dribble.Aggressiveness, 1e-4f);
        }

        [Test]
        public void DnaFlavoursShotsAndPasses_WithoutChangingTheirKind()
        {
            var dna = new FootballDNA();
            dna.Params.Set(catalogs.Parameters, "shooting.finesse", 0.95f);
            dna.Params.Set(catalogs.Parameters, "shooting.power", 0.2f);
            dna.Params.Set(catalogs.Parameters, "passing.throughBall", 0.95f);
            var none = new BehaviorDecision();
            ActionSelection shot = FootballActionResolver.Resolve(new PlayerIntent(Vec2.Zero).WithAction(FootballActionKind.Shot), dna, catalogs.Parameters, none, Duel(), true);
            Assert.IsTrue(shot.Shot.Placed);
            Assert.AreEqual(FootballActionKind.Shot, shot.Action);
            ActionSelection pass = FootballActionResolver.Resolve(new PlayerIntent(Vec2.Zero).WithAction(FootballActionKind.ShortPass), dna, catalogs.Parameters, none, Duel(), true);
            Assert.IsTrue(pass.Pass.Through);
        }

        [Test]
        public void EveryBehaviourDefinition_HasAnActionOrIsMovementOnly_AndSaneDefaults()
        {
            foreach (SignatureBehaviorDefinition b in catalogs.Behaviors.All)
            {
                Assert.That(b.DefaultPriority, Is.InRange(0f, 10f), b.Id);
                Assert.That(b.DefaultRisk, Is.InRange(0f, 1f), b.Id);
                Assert.That(b.DefaultCooldownSeconds, Is.InRange(0f, 60f), b.Id);
                Assert.AreEqual(BehaviorContext.None, b.Requires & b.Forbids, b.Id + " requires and forbids the same situation");
            }
            Assert.IsTrue(catalogs.Behaviors.SequenceTemplates.All(q => q.Steps.All(catalogs.Behaviors.Contains)));
        }
    }
}
