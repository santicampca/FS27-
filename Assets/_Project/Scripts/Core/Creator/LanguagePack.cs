using System;
using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// One word or phrase of a language (already without accents, lowercase, tokens separated by one space). A token ending in '*' matches
    /// any word that starts with it ("corr*" matches corre, corra, corriendo). The longest matching phrase wins.
    /// </summary>
    [Serializable]
    public sealed class Phrase
    {
        public string Text = "";
        internal string[] Tokens;

        internal void Prepare()
        {
            Tokens = TextNormalizer.Normalize(Text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        internal bool MatchesAt(string[] words, int at)
        {
            if (at + Tokens.Length > words.Length) return false;
            for (int i = 0; i < Tokens.Length; i++)
            {
                string t = Tokens[i];
                string w = words[at + i];
                if (t.Length > 1 && t[t.Length - 1] == '*')
                {
                    if (!w.StartsWith(t.Substring(0, t.Length - 1), StringComparison.Ordinal)) return false;
                }
                else if (t != w) return false;
            }
            return true;
        }
    }

    /// <summary>"muy" -> Much. <see cref="Flip"/>: "poco" turns the direction around (poco agresivo = low aggression). <see cref="Excess"/>: "demasiado".</summary>
    [Serializable]
    public sealed class IntensifierEntry
    {
        public Phrase Phrase;
        public MagnitudeLevel Level;
        public bool Flip;
        public bool Excess;
    }

    [Serializable]
    public sealed class OperatorEntry
    {
        public Phrase Phrase;
        public SemanticIntent Operation;
        /// <summary>"hazlo", "make it": the following description is a relative change of the existing character ("hazlo rápido" = faster).</summary>
        public bool MakeIt;
    }

    /// <summary>An adjective, verb or noun that names a concept directly: "rápido" -> speed, +1.</summary>
    [Serializable]
    public sealed class ConceptWord
    {
        public Phrase Phrase;
        public string Concept = "";
        public int Polarity = 1;
        public SemanticDomain Domain;
        public SemanticPhase Phase;
        public string Sense = "";
        /// <summary>Applies to both the gameplay and the visual reading when nothing says which ("fuerte"). The visual one is a weaker guess.</summary>
        public bool Both;
        /// <summary>A verb of play ("corra", "driblee"): negating it means "play less like that", not "the opposite".</summary>
        public bool Verb;
        /// <summary>The word already carries an operation ("calvo" = remove the hair, "barbudo" = add a beard).</summary>
        public SemanticIntent Implied;
        public string Value = "";
    }

    /// <summary>"grande", "largo": needs a thing to be about. Bound to the noun in the same clause, or to <see cref="Standalone"/> when there is none.</summary>
    [Serializable]
    public sealed class ModifierWord
    {
        public Phrase Phrase;
        /// <summary>size, length, width, thickness, height, strength.</summary>
        public string Key = "";
        public int Polarity = 1;
        public string Standalone = "";
    }

    /// <summary>"pelo", "cabeza": a thing. Tells what each generic modifier means for it ("pelo largo" = hair length).</summary>
    [Serializable]
    public sealed class NounWord
    {
        public Phrase Phrase;
        /// <summary>What the thing is when it is added, replaced or removed (hairStyle, beard, headSize...).</summary>
        public string Concept = "";
        /// <summary>The numeric reading ("más pelo", "pelo largo").</summary>
        public string ScalarConcept = "";
        /// <summary>The whole area, for "keep the hair", "change the face".</summary>
        public string GroupConcept = "";
        public Dictionary<string, string> ByModifier = new Dictionary<string, string>();
    }

    /// <summary>A choice that names its own concept and value: "rizado" -> hairTexture = curly; "afro" -> hairStyle = afro_08.</summary>
    [Serializable]
    public sealed class ValueWord
    {
        public Phrase Phrase;
        public string Concept = "";
        public string Value = "";
        public SemanticIntent Operation = SemanticIntent.Set;
    }

    [Serializable]
    public sealed class ColorWord
    {
        public Phrase Phrase;
        public string Hex = "";
    }

    [Serializable]
    public sealed class CueEntry
    {
        public Phrase Phrase;
        public SemanticDomain Domain;
        public SemanticPhase Phase;
    }

    [Serializable]
    public sealed class ReferenceEntry
    {
        public Phrase Phrase;
        public EntityReference Reference;
    }

    /// <summary>
    /// A language as DATA. The parser has no language in it; adding a language is writing one more pack (or loading one), not changing code.
    /// A pack only gives WORDS; what they mean (which parameter, which attribute) lives in the <see cref="ConceptCatalog"/>, shared by every language.
    /// </summary>
    public sealed class LanguagePack
    {
        public string Id = "";
        public List<IntensifierEntry> Intensifiers = new List<IntensifierEntry>();
        public List<OperatorEntry> Operators = new List<OperatorEntry>();
        public List<Phrase> Negators = new List<Phrase>();
        /// <summary>Negators that keep the negation going ("ni", "nor"): "sin barba ni bigote".</summary>
        public List<Phrase> NegationContinuers = new List<Phrase>();
        public List<Phrase> Contrasts = new List<Phrase>();
        /// <summary>"sin perder", "without losing": the next thing named is kept, not changed.</summary>
        public List<Phrase> KeepWhile = new List<Phrase>();
        public List<Phrase> Conjunctions = new List<Phrase>();
        public List<Phrase> CreatePhrases = new List<Phrase>();
        public List<Phrase> PreservePhrases = new List<Phrase>();
        public List<Phrase> PreserveAllElse = new List<Phrase>();
        public List<Phrase> OnlyPhrases = new List<Phrase>();
        public List<Phrase> UndoPhrases = new List<Phrase>();
        public List<ReferenceEntry> References = new List<ReferenceEntry>();
        public List<CueEntry> Cues = new List<CueEntry>();
        public List<ConceptWord> Words = new List<ConceptWord>();
        public List<ModifierWord> Modifiers = new List<ModifierWord>();
        public List<NounWord> Nouns = new List<NounWord>();
        public List<ValueWord> Values = new List<ValueWord>();
        public List<ColorWord> Colors = new List<ColorWord>();
        /// <summary>Words that carry no meaning here (articles, "quiero que", "por favor"). They are not reported as not understood.</summary>
        public List<Phrase> Fillers = new List<Phrase>();

        private bool prepared;

        public static Phrase Ph(string text) { return new Phrase { Text = text }; }

        internal void Prepare()
        {
            if (prepared) return;
            foreach (IntensifierEntry e in Intensifiers) e.Phrase.Prepare();
            foreach (OperatorEntry e in Operators) e.Phrase.Prepare();
            foreach (ConceptWord e in Words) e.Phrase.Prepare();
            foreach (ModifierWord e in Modifiers) e.Phrase.Prepare();
            foreach (NounWord e in Nouns) e.Phrase.Prepare();
            foreach (ValueWord e in Values) e.Phrase.Prepare();
            foreach (ColorWord e in Colors) e.Phrase.Prepare();
            foreach (CueEntry e in Cues) e.Phrase.Prepare();
            foreach (ReferenceEntry e in References) e.Phrase.Prepare();
            foreach (List<Phrase> list in new[] { Negators, NegationContinuers, Contrasts, KeepWhile, Conjunctions, CreatePhrases, PreservePhrases, PreserveAllElse, OnlyPhrases, UndoPhrases, Fillers })
                foreach (Phrase p in list) p.Prepare();
            prepared = true;
        }

        // ---- builders (they keep the packs readable) ----

        /// <summary>Several spellings of one thing: "alto|alta|altos|altas".</summary>
        public static List<Phrase> Many(string forms)
        {
            var l = new List<Phrase>();
            foreach (string f in forms.Split('|')) l.Add(Ph(f.Trim()));
            return l;
        }

        public void Negator(string forms) { Negators.AddRange(Many(forms)); }
        public void NegationContinuer(string forms) { NegationContinuers.AddRange(Many(forms)); }
        public void Contrast(string forms) { Contrasts.AddRange(Many(forms)); }
        public void KeepWhileWords(string forms) { KeepWhile.AddRange(Many(forms)); }
        public void Conjunction(string forms) { Conjunctions.AddRange(Many(forms)); }
        public void Create(string forms) { CreatePhrases.AddRange(Many(forms)); }
        public void Preserve(string forms) { PreservePhrases.AddRange(Many(forms)); }
        public void AllElse(string forms) { PreserveAllElse.AddRange(Many(forms)); }
        public void Only(string forms) { OnlyPhrases.AddRange(Many(forms)); }
        public void Undo(string forms) { UndoPhrases.AddRange(Many(forms)); }
        public void Filler(string forms) { Fillers.AddRange(Many(forms)); }

        public void Intensifier(string forms, MagnitudeLevel level, bool flip = false, bool excess = false)
        {
            foreach (Phrase p in Many(forms)) Intensifiers.Add(new IntensifierEntry { Phrase = p, Level = level, Flip = flip, Excess = excess });
        }

        public void Operator(string forms, SemanticIntent op, bool makeIt = false)
        {
            foreach (Phrase p in Many(forms)) Operators.Add(new OperatorEntry { Phrase = p, Operation = op, MakeIt = makeIt });
        }

        public void Reference(string forms, EntityReference r)
        {
            foreach (Phrase p in Many(forms)) References.Add(new ReferenceEntry { Phrase = p, Reference = r });
        }

        public void Cue(string forms, SemanticDomain domain, SemanticPhase phase = SemanticPhase.Any)
        {
            foreach (Phrase p in Many(forms)) Cues.Add(new CueEntry { Phrase = p, Domain = domain, Phase = phase });
        }

        public ConceptWord Word(string forms, string concept, int polarity = 1, SemanticDomain domain = SemanticDomain.Unspecified, string sense = "", bool verb = false, bool both = false,
            SemanticIntent implied = SemanticIntent.Unknown, string value = "", SemanticPhase phase = SemanticPhase.Any)
        {
            ConceptWord last = null;
            foreach (Phrase p in Many(forms))
            {
                last = new ConceptWord { Phrase = p, Concept = concept, Polarity = polarity, Domain = domain, Sense = sense, Verb = verb, Both = both, Implied = implied, Value = value, Phase = phase };
                Words.Add(last);
            }
            return last;
        }

        public void Modifier(string forms, string key, int polarity, string standalone = "")
        {
            foreach (Phrase p in Many(forms)) Modifiers.Add(new ModifierWord { Phrase = p, Key = key, Polarity = polarity, Standalone = standalone });
        }

        public void Noun(string forms, string concept, string scalar, string group, params string[] byModifier)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i + 1 < byModifier.Length; i += 2) map[byModifier[i]] = byModifier[i + 1];
            foreach (Phrase p in Many(forms)) Nouns.Add(new NounWord { Phrase = p, Concept = concept, ScalarConcept = scalar, GroupConcept = group, ByModifier = map });
        }

        public void Value(string forms, string concept, string value, SemanticIntent op = SemanticIntent.Set)
        {
            foreach (Phrase p in Many(forms)) Values.Add(new ValueWord { Phrase = p, Concept = concept, Value = value, Operation = op });
        }

        public void Color(string forms, string hex)
        {
            foreach (Phrase p in Many(forms)) Colors.Add(new ColorWord { Phrase = p, Hex = hex });
        }
    }

    /// <summary>Normalises text for matching: lower case, no accents, no inverted marks. "Más RÁPIDO" -> "mas rapido".</summary>
    public static class TextNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string d = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char ch in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (ch == '¿' || ch == '¡') continue;
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>Words, with the hard boundaries of the sentence kept as the token "|" (commas, semicolons, full stops, dashes...).</summary>
        public static string[] Tokenize(string text)
        {
            string n = Normalize(text);
            var tokens = new List<string>();
            var cur = new StringBuilder();
            Action flush = () => { if (cur.Length > 0) { tokens.Add(cur.ToString()); cur.Clear(); } };
            foreach (char ch in n)
            {
                if (char.IsLetterOrDigit(ch) || ch == '%' || ch == '\'') cur.Append(ch);
                else if (ch == ',' || ch == ';' || ch == '.' || ch == '!' || ch == '?' || ch == ':' || ch == '\n' || ch == '(' || ch == ')') { flush(); tokens.Add("|"); }
                else flush();
            }
            flush();
            return tokens.ToArray();
        }
    }
}
