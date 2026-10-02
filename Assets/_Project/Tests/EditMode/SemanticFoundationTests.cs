using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class SemanticFoundationTests
    {
        private CreatorCatalogs catalogs;
        private ConceptCatalog concepts;

        [SetUp]
        public void SetUp()
        {
            catalogs = CreatorCatalogs.CreateDefault();
            concepts = DefaultConcepts.Create();
        }

        // ---------------- magnitude ----------------

        [Test]
        public void TheMagnitudeScale_IsMonotonic_ForDeltaAndAbsolute()
        {
            for (int i = 2; i <= (int)MagnitudeLevel.Maximum; i++)
            {
                Assert.GreaterOrEqual(MagnitudeEngine.Delta((MagnitudeLevel)i), MagnitudeEngine.Delta((MagnitudeLevel)(i - 1)), "delta " + (MagnitudeLevel)i);
                Assert.GreaterOrEqual(MagnitudeEngine.Absolute((MagnitudeLevel)i), MagnitudeEngine.Absolute((MagnitudeLevel)(i - 1)), "absolute " + (MagnitudeLevel)i);
            }
            Assert.AreEqual(1f, MagnitudeEngine.Absolute(MagnitudeLevel.Maximum));
        }

        [Test]
        public void Unspecified_BehavesAsModerate()
        {
            Assert.AreEqual(MagnitudeEngine.Delta(MagnitudeLevel.Moderate), MagnitudeEngine.Delta(MagnitudeLevel.Unspecified));
            Assert.AreEqual(MagnitudeEngine.Absolute(MagnitudeLevel.Moderate), MagnitudeEngine.Absolute(MagnitudeLevel.Unspecified));
        }

        [Test]
        public void NegatingAHighLevel_IsASoftCap_ButNegatingAPlainOneIsAReversal()
        {
            float notTooMuch = MagnitudeEngine.NegatedAbsolute(MagnitudeLevel.Much);
            Assert.Greater(notTooMuch, 0f, "'not too tall' stays a little tall");
            Assert.Less(notTooMuch, MagnitudeEngine.Absolute(MagnitudeLevel.Moderate));
            Assert.Less(MagnitudeEngine.NegatedAbsolute(MagnitudeLevel.Unspecified), 0f, "'not tall' means the opposite");
            Assert.Less(MagnitudeEngine.NegatedDelta(MagnitudeLevel.Unspecified), 0f);
        }

        [Test]
        public void Step_StaysInsideTheScale()
        {
            Assert.AreEqual(MagnitudeLevel.Maximum, MagnitudeEngine.Step(MagnitudeLevel.Maximum, 3));
            Assert.AreEqual(MagnitudeLevel.Minimal, MagnitudeEngine.Step(MagnitudeLevel.Minimal, -3));
            Assert.AreEqual(MagnitudeLevel.Quite, MagnitudeEngine.Step(MagnitudeLevel.Unspecified, 1));
        }

        // ---------------- concept catalog ----------------

        [Test]
        public void EveryConceptEffect_PointsAtSomethingThatExists()
        {
            CreatorValidationResult r = concepts.Validate(catalogs);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void TheSameWord_MeansDifferentThings_InDifferentDomains()
        {
            Assert.IsTrue(concepts.TryGet("strength", out ConceptDefinition strength));
            ConceptSense gameplay = strength.FindSense(SemanticDomain.Gameplay, SemanticPhase.Any, "", out _);
            ConceptSense visual = strength.FindSense(SemanticDomain.Visual, SemanticPhase.Any, "", out _);
            ConceptSense duels = strength.FindSense(SemanticDomain.Gameplay, SemanticPhase.Duels, "", out _);
            Assert.IsTrue(gameplay.Effects.Any(e => e.Kind == EffectKind.Attribute));
            Assert.IsTrue(visual.Effects.Any(e => e.Key == "body.muscularity"));
            Assert.IsFalse(visual.Effects.Any(e => e.Kind == EffectKind.Attribute), "looking strong is not being strong");
            Assert.IsTrue(duels.Effects.Any(e => e.Key == "possession.shielding"));
        }

        [Test]
        public void ADomainTheConceptDoesNotCover_FindsNoSense()
        {
            concepts.TryGet("hairLength", out ConceptDefinition hair);
            Assert.IsNull(hair.FindSense(SemanticDomain.Gameplay, SemanticPhase.Any, "", out _));
        }

        [Test]
        public void Dribbling_HasATendencyReading_AndAnAbilityReading()
        {
            concepts.TryGet("dribbling", out ConceptDefinition d);
            ConceptSense tendency = d.FindSense(SemanticDomain.Gameplay, SemanticPhase.Any, "tendency", out _);
            ConceptSense ability = d.FindSense(SemanticDomain.Gameplay, SemanticPhase.Any, "ability", out _);
            Assert.IsTrue(tendency.Effects.Any(e => e.Key == "dribbling.takeOn"));
            Assert.IsTrue(ability.Effects.Any(e => e.Kind == EffectKind.Attribute && e.Attribute == PlayerAttributeId.Dribbling));
        }

        // ---------------- program validation ----------------

        [Test]
        public void AProgram_WithAnUnknownTarget_IsRejected()
        {
            var p = new SemanticProgram();
            p.Commands.Add(new SemanticCommand { Id = 1, Intent = SemanticIntent.Increase, Operation = SemanticIntent.Increase, Target = "flying" });
            CreatorValidationResult r = SemanticProgramValidator.Validate(p, concepts);
            Assert.IsFalse(r.IsValid);
            Assert.IsTrue(r.Has(CreatorIssueCode.SemanticTargetUnknown));
        }

        [Test]
        public void AWellFormedProgram_IsAccepted_AndABadRelationIsNot()
        {
            var p = new SemanticProgram();
            p.Commands.Add(new SemanticCommand { Id = 1, Intent = SemanticIntent.Increase, Operation = SemanticIntent.Increase, Target = "height", Magnitude = MagnitudeLevel.Much });
            Assert.IsTrue(SemanticProgramValidator.Validate(p, concepts).IsValid);
            p.Relations.Add(new SemanticRelation { Kind = RelationKind.Contrast, From = 1, To = 9 });
            Assert.IsTrue(SemanticProgramValidator.Validate(p, concepts).Has(CreatorIssueCode.SemanticRelationInvalid));
        }
    }
}
