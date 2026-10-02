using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class SemanticParserTests
    {
        private SemanticParser parser;
        private ConceptCatalog concepts;

        [SetUp]
        public void SetUp()
        {
            concepts = DefaultConcepts.Create();
            parser = new SemanticParser(concepts, LanguagePackEs.Create(), LanguagePackEn.Create());
        }

        private SemanticProgram P(string text)
        {
            SemanticProgram p = parser.Parse(text);
            CreatorValidationResult v = SemanticProgramValidator.Validate(p, concepts);
            Assert.IsTrue(v.IsValid, text + "\n" + v + "\n" + SemanticProgramPrinter.Describe(p));
            return p;
        }

        private static SemanticCommand Cmd(SemanticProgram p, string target)
        {
            SemanticCommand c = p.Commands.FirstOrDefault(x => x.Target == target);
            Assert.IsNotNull(c, "no command for '" + target + "'\n" + SemanticProgramPrinter.Describe(p));
            return c;
        }

        // ---------------- language detection ----------------

        [Test]
        public void TheLanguage_IsDetectedFromTheWords()
        {
            Assert.AreEqual("es", P("hazlo más rápido").Language);
            Assert.AreEqual("en", P("make him faster").Language);
        }

        // ---------------- negation ----------------

        [Test]
        public void NoDemasiadoMusculoso_IsANegatedHighLevel_NotAnIncrease()
        {
            SemanticCommand c = Cmd(P("no demasiado musculoso"), "muscularity");
            Assert.IsTrue(c.Negated);
            Assert.AreEqual(MagnitudeLevel.Much, c.Magnitude);
        }

        [Test]
        public void QueNoSeaMuyAlto_IsNegatedHeight()
        {
            SemanticCommand c = Cmd(P("que no sea muy alto"), "height");
            Assert.IsTrue(c.Negated);
            Assert.AreEqual(MagnitudeLevel.Much, c.Magnitude);
        }

        [Test]
        public void SinBarba_RemovesTheBeard()
        {
            SemanticCommand c = Cmd(P("sin barba"), "beard");
            Assert.AreEqual(SemanticIntent.Set, c.Operation);
            Assert.AreEqual("none", c.Value);
        }

        [Test]
        public void QuitaLaBarba_IsARemoval()
        {
            Assert.AreEqual(SemanticIntent.Remove, Cmd(P("quita la barba"), "beard").Operation);
        }

        [Test]
        public void NoQuieroQueDriblee_IsAvoidingATendency_NotTheOppositeAbility()
        {
            SemanticCommand c = Cmd(P("no quiero que driblee tanto"), "dribbling");
            Assert.AreEqual(SemanticIntent.Avoid, c.Intent);
            Assert.AreEqual(SemanticIntent.Decrease, c.Operation);
            Assert.AreEqual("tendency", c.Sense);
        }

        // ---------------- contrast and trade-off ----------------

        [Test]
        public void CreativoPeroNoArriesgado_IsTwoCommandsWithAContrast()
        {
            SemanticProgram p = P("creativo pero no arriesgado");
            Assert.IsFalse(Cmd(p, "creativity").Negated);
            Assert.IsTrue(Cmd(p, "risk").Negated);
            Assert.AreEqual(1, p.Relations.Count(r => r.Kind == RelationKind.Contrast));
        }

        [Test]
        public void SinPerderFuerza_KeepsStrength_AndLinksItToTheChange()
        {
            SemanticProgram p = P("hazlo mucho más rápido pero sin perder fuerza");
            Assert.AreEqual(SemanticIntent.Increase, Cmd(p, "speed").Operation);
            Assert.AreEqual(MagnitudeLevel.Much, Cmd(p, "speed").Magnitude);
            Assert.AreEqual(SemanticIntent.Preserve, Cmd(p, "strength").Operation);
            Assert.IsTrue(p.Relations.Any(r => r.Kind == RelationKind.TradeOff));
        }

        // ---------------- context ----------------

        [Test]
        public void SeVeaFuerte_IsVisual_AndFuerteEnLosDuelos_IsGameplayInDuels()
        {
            SemanticCommand look = Cmd(P("quiero que se vea fuerte"), "strength");
            Assert.AreEqual(SemanticDomain.Visual, look.Domain);
            Assert.AreEqual(1, P("quiero que se vea fuerte").Commands.Count);
            SemanticCommand duels = Cmd(P("fuerte en los duelos"), "strength");
            Assert.AreEqual(SemanticDomain.Gameplay, duels.Domain);
            Assert.AreEqual(SemanticPhase.Duels, duels.Phase);
        }

        [Test]
        public void FuerteWithNoHint_IsBothGameplayAndAWeakerVisualGuess()
        {
            SemanticProgram p = P("quiero un jugador fuerte");
            SemanticCommand[] strong = p.Commands.Where(c => c.Target == "strength").ToArray();
            Assert.AreEqual(2, strong.Length);
            Assert.IsTrue(strong.Any(c => c.Domain == SemanticDomain.Gameplay && c.Confidence >= 0.99f));
            Assert.IsTrue(strong.Any(c => c.Domain == SemanticDomain.Visual && c.Confidence < 0.99f));
        }

        [Test]
        public void CorraRapido_IsOneSpeedCommand_NotTwo()
        {
            Assert.AreEqual(1, P("corra rápido").Commands.Count(c => c.Target == "speed"));
        }

        [Test]
        public void ADriblerWord_IsAbility_AndADribbleVerb_IsTendency()
        {
            Assert.AreEqual("ability", Cmd(P("un buen driblador"), "dribbling").Sense);
            Assert.AreEqual("tendency", Cmd(P("que regatee mucho"), "dribbling").Sense);
        }

        // ---------------- references, edits, preservation ----------------

        [Test]
        public void HazloMasAlto_IsARelativeIncrease_OfTheCurrentCharacter()
        {
            SemanticCommand c = Cmd(P("hazlo más alto"), "height");
            Assert.AreEqual(SemanticIntent.Increase, c.Operation);
            Assert.AreEqual(EntityReference.Current, c.Reference);
        }

        [Test]
        public void CambialeElPelo_IsAReplace_OfTheHairStyle()
        {
            Assert.AreEqual(SemanticIntent.Replace, Cmd(P("cámbiale el pelo"), "hairStyle").Operation);
        }

        [Test]
        public void ComoElAnterior_RefersToThePreviousCharacter()
        {
            SemanticProgram p = P("como el anterior pero con pelo largo");
            Assert.IsTrue(p.Commands.Any(c => c.Reference == EntityReference.Previous));
            Assert.AreEqual(1, p.Commands.Count(c => c.Target == "hairLength"));
        }

        [Test]
        public void ManténLaCara_IsAPreserveConstraint_NotACommand()
        {
            SemanticProgram p = P("mantén la cara");
            Assert.AreEqual(0, p.Commands.Count);
            Assert.IsTrue(p.Constraints.Any(c => c.Intent == SemanticIntent.Preserve && c.Target == "face"));
        }

        [Test]
        public void MantenTodoLoDemas_IsAnAllElseConstraint()
        {
            Assert.IsTrue(P("mantén todo lo demás").Constraints.Any(c => c.AllElse));
            Assert.IsTrue(P("keep everything else").Constraints.Any(c => c.AllElse));
        }

        [Test]
        public void SoloCambiaSuEstilo_MeansOnlyThat()
        {
            SemanticProgram p = P("solo cambia su estilo");
            Assert.AreEqual(SemanticIntent.Replace, Cmd(p, "style").Operation);
            Assert.IsTrue(p.Constraints.Any(c => c.OnlyThese));
        }

        // ---------------- convergence: different sentences, one meaning ----------------

        [TestCase("más rápido")]
        [TestCase("quiero que sea más rápido")]
        [TestCase("hazlo rápido")]
        [TestCase("dale más velocidad")]
        [TestCase("quiero que corra más")]
        [TestCase("hazlo más veloz")]
        [TestCase("make him faster")]
        [TestCase("I want him faster")]
        [TestCase("give him more speed")]
        public void ManyWaysOfSayingFaster_AllMeanIncreaseSpeed(string text)
        {
            SemanticProgram p = P(text);
            SemanticCommand c = Cmd(p, "speed");
            Assert.AreEqual(SemanticIntent.Increase, c.Operation, text + "\n" + SemanticProgramPrinter.Describe(p));
            Assert.IsFalse(c.Negated);
            Assert.AreEqual(1, p.Commands.Count, text);
        }

        // ---------------- honesty ----------------

        [Test]
        public void WordsNobodyUnderstands_AreReported_AndLowerTheConfidence()
        {
            SemanticProgram p = P("hazlo más rápido y blorpificado");
            Assert.Contains("blorpificado", p.Unparsed);
            Assert.Less(Cmd(p, "speed").Confidence, 1f);
        }

        [Test]
        public void ALoneAdjective_ThatNeedsAThing_IsAmbiguous_NotGuessed()
        {
            SemanticProgram p = P("hazlo más largo");
            Assert.AreEqual(0, p.Commands.Count);
            Assert.AreEqual(1, p.Ambiguities.Count);
        }

        [Test]
        public void ANonsenseText_IsNotUnderstood_AndProducesNothing()
        {
            SemanticProgram p = P("xyzzy plugh");
            Assert.IsTrue(p.IsEmpty);
            Assert.IsNotEmpty(p.Unparsed);
        }

        [Test]
        public void Parsing_IsDeterministic()
        {
            string a = SemanticProgramPrinter.Describe(P("Crea un extremo rápido, no muy alto, con pelo negro rizado"));
            string b = SemanticProgramPrinter.Describe(P("Crea un extremo rápido, no muy alto, con pelo negro rizado"));
            Assert.AreEqual(a, b);
        }

        // ---------------- vocabulary is data ----------------

        [Test]
        public void EveryConceptNamedByEveryLanguagePack_ExistsInTheCatalog()
        {
            foreach (LanguagePack pack in new[] { LanguagePackEs.Create(), LanguagePackEn.Create() })
            {
                foreach (ConceptWord w in pack.Words) Assert.IsTrue(concepts.Contains(w.Concept), pack.Id + " word '" + w.Phrase.Text + "' -> " + w.Concept);
                foreach (ModifierWord m in pack.Modifiers) if (m.Standalone != "") Assert.IsTrue(concepts.Contains(m.Standalone), pack.Id + " modifier standalone " + m.Standalone);
                foreach (NounWord n in pack.Nouns)
                {
                    foreach (string c in new[] { n.Concept, n.ScalarConcept, n.GroupConcept }) if (!string.IsNullOrEmpty(c)) Assert.IsTrue(concepts.Contains(c), pack.Id + " noun '" + n.Phrase.Text + "' -> " + c);
                    foreach (string c in n.ByModifier.Values) Assert.IsTrue(concepts.Contains(c), pack.Id + " noun '" + n.Phrase.Text + "' modifier -> " + c);
                }
                foreach (ValueWord v in pack.Values) Assert.IsTrue(concepts.Contains(v.Concept), pack.Id + " value '" + v.Phrase.Text + "' -> " + v.Concept);
            }
        }

        [Test]
        public void EveryChoiceValueInTheLanguagePacks_IsARealCatalogPart()
        {
            CreatorCatalogs cat = CreatorCatalogs.CreateDefault();
            foreach (LanguagePack pack in new[] { LanguagePackEs.Create(), LanguagePackEn.Create() })
                foreach (ValueWord v in pack.Values)
                {
                    concepts.TryGet(v.Concept, out ConceptDefinition def);
                    Assert.IsTrue(cat.Appearance.TryGetPart(def.Slot, v.Value, out PartDefinition _), pack.Id + " '" + v.Phrase.Text + "' -> " + def.Slot + "/" + v.Value);
                }
        }
    }
}
