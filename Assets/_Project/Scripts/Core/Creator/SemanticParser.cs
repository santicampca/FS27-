using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Anything that turns text into a <see cref="SemanticProgram"/>: the deterministic parser below, or a language-model provider.
    /// The program is the ONLY thing they hand to the rest of the engine, and it is data.
    /// </summary>
    public interface ISemanticInterpreter
    {
        string Name { get; }
        SemanticProgram Interpret(string text);
    }

    /// <summary>
    /// A deterministic, offline semantic parser. It does not match phrases to effects: it builds MEANING (a <see cref="SemanticProgram"/>) from
    /// words, using language packs for vocabulary and understanding negation ("no demasiado musculoso"), amounts ("muy", "un poco"), comparison
    /// ("más", "menos"), contrast ("creativo pero no arriesgado"), scope ("en los duelos", "se vea"), preservation ("mantén la cara") and
    /// references ("como el anterior"). What it cannot place is reported in <see cref="SemanticProgram.Unparsed"/> and
    /// <see cref="SemanticProgram.Ambiguities"/>, never guessed silently. It is not a language model: it understands what its packs know.
    /// </summary>
    public sealed class SemanticParser : ISemanticInterpreter
    {
        public const string ParserName = "FS27.SemanticParser.v1";

        private enum Kind
        {
            Boundary, Conjunction, Contrast, KeepWhile, Negator, NegationContinuer, Intensifier, Operator, Create, Preserve, AllElse, Only, Undo,
            Reference, Cue, Concept, Filler, Percent, Unknown
        }

        private sealed class Item
        {
            public Kind Kind;
            public int Index;
            public int Length;
            public string Text = "";
            public IntensifierEntry Intensifier;
            public OperatorEntry Operator;
            public ReferenceEntry Reference;
            public CueEntry Cue;
            public List<ConceptWord> Words = new List<ConceptWord>();
            public List<ModifierWord> Modifiers = new List<ModifierWord>();
            public List<NounWord> Nouns = new List<NounWord>();
            public List<ValueWord> Values = new List<ValueWord>();
            public List<ColorWord> Colors = new List<ColorWord>();
            public float PercentDelta;
            public bool Consumed;
            public int Segment;
        }

        private static readonly Dictionary<string, string> ColorOfNoun = new Dictionary<string, string>
        {
            { "hairStyle", "hairColor" }, { "eyeSize", "eyeColor" }, { "kitColor", "kitColor" }
        };

        private readonly List<LanguagePack> packs = new List<LanguagePack>();
        private readonly ConceptCatalog concepts;

        public string Name => ParserName;

        public SemanticParser(ConceptCatalog concepts, params LanguagePack[] languagePacks)
        {
            this.concepts = concepts;
            foreach (LanguagePack p in languagePacks)
            {
                p.Prepare();
                packs.Add(p);
            }
        }

        public static SemanticParser CreateDefault()
        {
            return new SemanticParser(DefaultConcepts.Create(), LanguagePackEs.Create(), LanguagePackEn.Create());
        }

        public SemanticProgram Interpret(string text)
        {
            return Parse(text);
        }

        public SemanticProgram Parse(string text)
        {
            string[] words = TextNormalizer.Tokenize(text);
            LanguagePack best = packs[0];
            int bestScore = -1;
            foreach (LanguagePack p in packs)
            {
                int score = Score(p, words);
                if (score > bestScore) { best = p; bestScore = score; }
            }
            return ParseWith(best, words, text);
        }

        /// <summary>Parses with one specific language pack (no detection).</summary>
        public SemanticProgram ParseWith(string languageId, string text)
        {
            foreach (LanguagePack p in packs)
                if (p.Id == languageId) return ParseWith(p, TextNormalizer.Tokenize(text), text);
            return Parse(text);
        }

        // ------------------------------------------------------------------ tokens to items

        private static int Score(LanguagePack pack, string[] words)
        {
            int score = 0;
            int i = 0;
            while (i < words.Length)
            {
                Item it = MatchAt(pack, words, i);
                if (it.Kind != Kind.Unknown && it.Kind != Kind.Boundary && it.Kind != Kind.Filler) score += it.Length;
                i += Math.Max(1, it.Length);
            }
            return score;
        }

        private static int Specificity(Phrase p)
        {
            int exact = 0;
            foreach (string t in p.Tokens) if (t.Length == 0 || t[t.Length - 1] != '*') exact++;
            return exact;
        }

        private static Item MatchAt(LanguagePack pack, string[] words, int at)
        {
            var item = new Item { Index = at, Length = 1, Text = words[at] };
            if (words[at] == "|") { item.Kind = Kind.Boundary; return item; }

            string w = words[at];
            bool isPercent = w.Length > 1 && w[w.Length - 1] == '%' && char.IsDigit(w[0]);
            if (isPercent || (IsNumber(w) && at + 1 < words.Length && (words[at + 1] == "%" || words[at + 1] == "por" && at + 2 < words.Length && words[at + 2] == "ciento" || words[at + 1] == "percent")))
            {
                float pct = ParseNumber(isPercent ? w.Substring(0, w.Length - 1) : w);
                item.Kind = Kind.Percent;
                item.PercentDelta = pct / 100f;
                item.Length = isPercent ? 1 : (words[at + 1] == "%" || words[at + 1] == "percent" ? 2 : 3);
                return item;
            }

            // find the longest phrase of any category; exact tokens beat prefix tokens of the same length
            int bestLen = 0;
            int bestSpec = -1;
            Action<Phrase> consider = p =>
            {
                if (!p.MatchesAt(words, at)) return;
                int len = p.Tokens.Length, spec = Specificity(p);
                if (len > bestLen || (len == bestLen && spec > bestSpec)) { bestLen = len; bestSpec = spec; }
            };
            foreach (IntensifierEntry e in pack.Intensifiers) consider(e.Phrase);
            foreach (OperatorEntry e in pack.Operators) consider(e.Phrase);
            foreach (ConceptWord e in pack.Words) consider(e.Phrase);
            foreach (ModifierWord e in pack.Modifiers) consider(e.Phrase);
            foreach (NounWord e in pack.Nouns) consider(e.Phrase);
            foreach (ValueWord e in pack.Values) consider(e.Phrase);
            foreach (ColorWord e in pack.Colors) consider(e.Phrase);
            foreach (CueEntry e in pack.Cues) consider(e.Phrase);
            foreach (ReferenceEntry e in pack.References) consider(e.Phrase);
            foreach (List<Phrase> list in new[] { pack.Negators, pack.NegationContinuers, pack.Contrasts, pack.KeepWhile, pack.Conjunctions, pack.CreatePhrases, pack.PreservePhrases, pack.PreserveAllElse, pack.OnlyPhrases, pack.UndoPhrases, pack.Fillers })
                foreach (Phrase p in list) consider(p);

            if (bestLen == 0) { item.Kind = Kind.Unknown; return item; }
            item.Length = bestLen;
            item.Text = string.Join(" ", words, at, bestLen);

            bool Hit(Phrase p) { return p.Tokens.Length == bestLen && p.Tokens.Length > 0 && p.MatchesAt(words, at) && Specificity(p) == bestSpec; }
            bool Any(List<Phrase> list) { foreach (Phrase p in list) if (Hit(p)) return true; return false; }

            // structural categories first, in a fixed priority
            if (Any(pack.PreserveAllElse)) { item.Kind = Kind.AllElse; return item; }
            if (Any(pack.PreservePhrases)) { item.Kind = Kind.Preserve; return item; }
            if (Any(pack.UndoPhrases)) { item.Kind = Kind.Undo; return item; }
            foreach (ReferenceEntry e in pack.References) if (Hit(e.Phrase)) { item.Kind = Kind.Reference; item.Reference = e; return item; }
            foreach (CueEntry e in pack.Cues) if (Hit(e.Phrase)) { item.Kind = Kind.Cue; item.Cue = e; return item; }
            if (Any(pack.CreatePhrases)) { item.Kind = Kind.Create; return item; }
            if (Any(pack.KeepWhile)) { item.Kind = Kind.KeepWhile; return item; }
            if (Any(pack.Contrasts)) { item.Kind = Kind.Contrast; return item; }
            if (Any(pack.OnlyPhrases)) { item.Kind = Kind.Only; return item; }
            foreach (OperatorEntry e in pack.Operators) if (Hit(e.Phrase)) { item.Kind = Kind.Operator; item.Operator = e; return item; }
            foreach (IntensifierEntry e in pack.Intensifiers) if (Hit(e.Phrase)) { item.Kind = Kind.Intensifier; item.Intensifier = e; return item; }
            bool negator = Any(pack.Negators), continuer = Any(pack.NegationContinuers);

            foreach (ConceptWord e in pack.Words) if (Hit(e.Phrase)) item.Words.Add(e);
            foreach (ModifierWord e in pack.Modifiers) if (Hit(e.Phrase)) item.Modifiers.Add(e);
            foreach (NounWord e in pack.Nouns) if (Hit(e.Phrase)) item.Nouns.Add(e);
            foreach (ValueWord e in pack.Values) if (Hit(e.Phrase)) item.Values.Add(e);
            foreach (ColorWord e in pack.Colors) if (Hit(e.Phrase)) item.Colors.Add(e);
            if (item.Words.Count + item.Modifiers.Count + item.Nouns.Count + item.Values.Count + item.Colors.Count > 0) { item.Kind = Kind.Concept; return item; }

            if (continuer && !negator) { item.Kind = Kind.NegationContinuer; return item; }
            if (negator) { item.Kind = Kind.Negator; return item; }
            if (Any(pack.Conjunctions)) { item.Kind = Kind.Conjunction; return item; }
            item.Kind = Kind.Filler;
            return item;
        }

        private static bool IsNumber(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s) if (!char.IsDigit(c)) return false;
            return true;
        }

        private static float ParseNumber(string s)
        {
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        // ------------------------------------------------------------------ items to program

        private sealed class State
        {
            public bool Negated;
            public bool Flip;
            public MagnitudeLevel Level;
            public SemanticIntent Comparative;
            public SemanticIntent Explicit;
            public bool MakeIt;
            public bool PreserveMode;
            public bool KeepNext;
            public bool OnlyPending;
            public int LastCommandItem = -10;
            public SemanticCommand Last;
            public bool LastHadOperator;
            public bool Creating;
        }

        private SemanticProgram ParseWith(LanguagePack pack, string[] words, string text)
        {
            var program = new SemanticProgram { Language = pack.Id, Text = text ?? "", ParserName = ParserName };
            var items = new List<Item>();
            for (int i = 0; i < words.Length;)
            {
                Item it = MatchAt(pack, words, i);
                items.Add(it);
                i += Math.Max(1, it.Length);
            }

            // segments: hard segment (sentence part between , . pero) and soft segment (also split by "y")
            int hard = 0, soft = 0;
            var hardOf = new List<int>();
            var softOf = new List<int>();
            foreach (Item it in items)
            {
                if (it.Kind == Kind.Boundary || it.Kind == Kind.Contrast) { hard++; soft++; }
                else if (it.Kind == Kind.Conjunction) soft++;
                hardOf.Add(hard);
                softOf.Add(soft);
                it.Segment = soft;
            }
            var hardDomain = new Dictionary<int, SemanticDomain>();
            var softPhase = new Dictionary<int, SemanticPhase>();
            for (int k = 0; k < items.Count; k++)
            {
                Item it = items[k];
                if (it.Kind != Kind.Cue) continue;
                if (it.Cue.Domain != SemanticDomain.Unspecified) hardDomain[hardOf[k]] = it.Cue.Domain;
                if (it.Cue.Phase != SemanticPhase.Any) softPhase[softOf[k]] = it.Cue.Phase;
            }

            var st = new State();
            int nextId = 1;
            int lastBeforeContrast = -1;
            bool contrastPending = false;
            bool seenUnknown = false;
            var unknown = new HashSet<string>();
            var onlyTargets = false;
            var unknownSegments = new HashSet<int>();
            var commandSegment = new Dictionary<int, int>();

            for (int k = 0; k < items.Count; k++)
            {
                Item it = items[k];
                int hs = hardOf[k], ss = softOf[k];
                hardDomain.TryGetValue(hs, out SemanticDomain segDomain);
                softPhase.TryGetValue(ss, out SemanticPhase segPhase);

                switch (it.Kind)
                {
                    case Kind.Boundary:
                        ResetSoft(st);
                        st.Comparative = SemanticIntent.Unknown;
                        st.Explicit = SemanticIntent.Unknown;
                        st.MakeIt = false;
                        st.PreserveMode = false;
                        st.KeepNext = false;
                        continue;
                    case Kind.Conjunction:
                        ResetSoft(st);
                        continue;
                    case Kind.Contrast:
                        lastBeforeContrast = st.Last != null ? st.Last.Id : lastBeforeContrast;
                        contrastPending = lastBeforeContrast > 0;
                        ResetSoft(st);
                        st.Comparative = SemanticIntent.Unknown;
                        st.Explicit = SemanticIntent.Unknown;
                        st.PreserveMode = false;
                        continue;
                    case Kind.Filler:
                        continue;
                    case Kind.Cue:
                        continue;
                    case Kind.Unknown:
                        if (unknown.Add(it.Text)) program.Unparsed.Add(it.Text);
                        seenUnknown = true;
                        unknownSegments.Add(hs);
                        continue;
                    case Kind.Negator:
                    case Kind.NegationContinuer:
                        st.Negated = true;
                        continue;
                    case Kind.Only:
                        st.OnlyPending = true;
                        continue;
                    case Kind.Create:
                        if (!st.Creating)
                        {
                            program.Commands.Add(new SemanticCommand { Id = nextId++, Intent = SemanticIntent.Create, Operation = SemanticIntent.Create, Source = it.Text });
                            st.Creating = true;
                        }
                        continue;
                    case Kind.Undo:
                        program.Commands.Add(new SemanticCommand { Id = nextId++, Intent = SemanticIntent.Undo, Operation = SemanticIntent.Undo, Source = it.Text });
                        continue;
                    case Kind.Reference:
                        if (it.Reference.Reference != EntityReference.Current)
                            program.Commands.Add(new SemanticCommand { Id = nextId++, Intent = SemanticIntent.Create, Operation = SemanticIntent.Create, Reference = it.Reference.Reference, Source = it.Text });
                        continue;
                    case Kind.Preserve:
                        st.PreserveMode = true;
                        continue;
                    case Kind.AllElse:
                        program.Constraints.Add(new SemanticConstraint { Intent = SemanticIntent.Preserve, AllElse = true, Source = it.Text });
                        continue;
                    case Kind.KeepWhile:
                        st.KeepNext = true;
                        continue;
                    case Kind.Percent:
                        MagnitudeLevel pl = NearestLevel(it.PercentDelta);
                        if (!AttachPostfixLevel(st, pl, false, k, items)) { st.Level = pl; }
                        continue;
                    case Kind.Intensifier:
                        if (!AttachPostfixLevel(st, it.Intensifier.Level, it.Intensifier.Flip, k, items))
                        {
                            st.Level = it.Intensifier.Level;
                            if (it.Intensifier.Flip) st.Flip = true;
                        }
                        continue;
                    case Kind.Operator:
                        HandleOperator(st, it, k, items);
                        continue;
                }

                // ---- a concept item: words, modifiers, nouns, values, colours
                var emitted = new List<SemanticCommand>();
                Emit(it, k, items, st, segDomain, segPhase, emitted, ref nextId, program);
                foreach (SemanticCommand c in emitted)
                {
                    program.Commands.Add(c);
                    commandSegment[c.Id] = hs;
                    if (contrastPending) { program.Relations.Add(new SemanticRelation { Kind = RelationKind.Contrast, From = lastBeforeContrast, To = c.Id }); contrastPending = false; }
                    if (st.OnlyPending) { onlyTargets = true; st.OnlyPending = false; }
                }
                if (emitted.Count > 0)
                {
                    st.Last = emitted[0];
                    st.LastCommandItem = k;
                    st.LastHadOperator = st.Comparative != SemanticIntent.Unknown || st.MakeIt || st.Explicit != SemanticIntent.Unknown;
                    // an amount or a negation belongs to the thing it was said about, not to the next one
                    st.Level = MagnitudeLevel.Unspecified;
                    st.Flip = false;
                    st.Negated = false;
                    st.Explicit = SemanticIntent.Unknown;
                }
            }

            // words nobody understood in the same sentence part make what WAS understood less certain
            foreach (SemanticCommand c in program.Commands)
                if (commandSegment.TryGetValue(c.Id, out int seg) && unknownSegments.Contains(seg)) c.Confidence = Math.Max(0.4f, c.Confidence * 0.7f);
            MergeDuplicates(program);
            if (onlyTargets || st.OnlyPending) program.Constraints.Add(new SemanticConstraint { Intent = SemanticIntent.Constrain, OnlyThese = true, Source = "only" });
            if (seenUnknown && program.Commands.Count == 0 && program.Constraints.Count == 0) program.Ambiguities.Add("Nothing in the text was recognised.");
            return program;
        }

        /// <summary>"corra rápido" says speed twice (a verb and an adjective): one command, with the stronger amount.</summary>
        private static void MergeDuplicates(SemanticProgram program)
        {
            for (int i = 0; i < program.Commands.Count; i++)
            {
                SemanticCommand a = program.Commands[i];
                if (string.IsNullOrEmpty(a.Target) || a.Negated) continue;
                for (int j = i + 1; j < program.Commands.Count; j++)
                {
                    SemanticCommand b = program.Commands[j];
                    if (b.Target != a.Target || b.Negated || b.Domain != a.Domain || b.Phase != a.Phase || b.Operation != a.Operation || b.Direction != a.Direction || b.Value != a.Value) continue;
                    if (j != i + 1) continue;
                    if (b.Magnitude > a.Magnitude) a.Magnitude = b.Magnitude;
                    if (string.IsNullOrEmpty(a.Sense)) a.Sense = b.Sense;
                    a.Source = a.Source + " " + b.Source;
                    program.Commands.RemoveAt(j);
                    foreach (SemanticRelation r in program.Relations)
                    {
                        if (r.From == b.Id) r.From = a.Id;
                        if (r.To == b.Id) r.To = a.Id;
                    }
                    j--;
                }
            }
            program.Relations.RemoveAll(r => r.From == r.To);
        }

        private static void ResetSoft(State st)
        {
            st.Negated = false;
            st.Flip = false;
            st.Level = MagnitudeLevel.Unspecified;
        }

        private static MagnitudeLevel NearestLevel(float delta)
        {
            MagnitudeLevel best = MagnitudeLevel.Minimal;
            float bestDiff = float.MaxValue;
            for (int i = (int)MagnitudeLevel.Minimal; i <= (int)MagnitudeLevel.Maximum; i++)
            {
                float d = Math.Abs(MagnitudeEngine.Delta((MagnitudeLevel)i) - delta);
                if (d < bestDiff) { bestDiff = d; best = (MagnitudeLevel)i; }
            }
            return best;
        }

        /// <summary>"corre mucho", "driblea tanto": an amount said AFTER the thing it belongs to.</summary>
        private static bool AttachPostfixLevel(State st, MagnitudeLevel level, bool flip, int k, List<Item> items)
        {
            if (st.Last == null || st.LastHadOperator) return false;
            if (!ImmediatelyAfter(st.LastCommandItem, k, items)) return false;
            if (st.Last.Magnitude != MagnitudeLevel.Unspecified) return false;
            st.Last.Magnitude = level;
            if (flip) Flip(st.Last);
            return true;
        }

        private static bool ImmediatelyAfter(int lastItem, int k, List<Item> items)
        {
            for (int j = lastItem + 1; j < k; j++)
                if (items[j].Kind != Kind.Filler) return false;
            return lastItem >= 0;
        }

        private static void Flip(SemanticCommand c)
        {
            if (c.Operation == SemanticIntent.Increase) c.Operation = c.Intent = SemanticIntent.Decrease;
            else if (c.Operation == SemanticIntent.Decrease) c.Operation = c.Intent = SemanticIntent.Increase;
            else if (c.Operation == SemanticIntent.Set) c.Direction = -c.Direction;
        }

        private static void HandleOperator(State st, Item it, int k, List<Item> items)
        {
            SemanticIntent op = it.Operator.Operation;
            if (it.Operator.MakeIt) { st.MakeIt = true; return; }
            if (op == SemanticIntent.Increase || op == SemanticIntent.Decrease)
            {
                // "corra más": the comparative comes after what it modifies
                if (st.Last != null && !st.LastHadOperator && ImmediatelyAfter(st.LastCommandItem, k, items) && st.Last.Operation == SemanticIntent.Set && !st.Last.Negated)
                {
                    bool up = op == SemanticIntent.Increase;
                    st.Last.Operation = st.Last.Intent = (st.Last.Direction > 0) == up ? SemanticIntent.Increase : SemanticIntent.Decrease;
                    st.Last.Direction = 1;
                    st.LastHadOperator = true;
                    return;
                }
                st.Comparative = op;
                return;
            }
            st.Explicit = op;
        }

        private void Emit(Item it, int k, List<Item> items, State st, SemanticDomain segDomain, SemanticPhase segPhase, List<SemanticCommand> output, ref int nextId, SemanticProgram program)
        {
            // --- decide which reading of this word is meant
            Item nounItem = null;
            ConceptWord word = null;
            ModifierWord modifier = null;
            ValueWord value = null;
            ColorWord color = null;
            NounWord noun = null;

            // a modifier with a noun that supports it wins over a bare word ("mandíbula fuerte")
            foreach (ModifierWord m in it.Modifiers)
            {
                Item n = FindNoun(items, k, m.Key, out NounWord nw);
                if (n != null) { modifier = m; nounItem = n; noun = nw; break; }
            }
            if (modifier == null && it.Values.Count > 0) value = it.Values[0];
            if (modifier == null && value == null && it.Words.Count > 0) word = it.Words[0];
            if (modifier == null && value == null && word == null && it.Colors.Count > 0) color = it.Colors[0];
            if (modifier == null && value == null && word == null && color == null && it.Modifiers.Count > 0) modifier = it.Modifiers[0];
            if (modifier == null && value == null && word == null && color == null && it.Nouns.Count > 0) noun = it.Nouns[0];

            SemanticDomain domain = segDomain;
            SemanticPhase phase = segPhase;

            if (st.PreserveMode)
            {
                string target = PreserveTarget(it, word, modifier, noun);
                if (!string.IsNullOrEmpty(target))
                    program.Constraints.Add(new SemanticConstraint { Intent = SemanticIntent.Preserve, Target = target, Source = it.Text });
                return;
            }

            if (st.KeepNext)
            {
                string target = PreserveTarget(it, word, modifier, noun);
                st.KeepNext = false;
                if (!string.IsNullOrEmpty(target) && concepts.Contains(target))
                {
                    var keep = new SemanticCommand { Id = nextId++, Intent = SemanticIntent.Preserve, Operation = SemanticIntent.Preserve, Target = target, Domain = domain, Phase = phase, Source = it.Text };
                    output.Add(keep);
                    if (st.Last != null) program.Relations.Add(new SemanticRelation { Kind = RelationKind.TradeOff, From = st.Last.Id, To = keep.Id });
                }
                return;
            }

            if (color != null)
            {
                Item n = FindColorNoun(items, k, out NounWord cn);
                if (n == null || !ColorOfNoun.TryGetValue(cn.Concept, out string colorConcept))
                {
                    program.Ambiguities.Add("'" + it.Text + "' is a colour, but of what (hair, eyes, kit)?");
                    return;
                }
                n.Consumed = true;
                output.Add(new SemanticCommand
                {
                    Id = nextId++, Intent = SemanticIntent.Set, Operation = st.Negated ? SemanticIntent.Remove : SemanticIntent.Set, Target = colorConcept, Value = color.Hex, Domain = SemanticDomain.Visual,
                    Source = it.Text, Negated = false
                });
                return;
            }

            if (value != null)
            {
                SemanticIntent vop = st.Negated ? SemanticIntent.Remove : (st.Explicit == SemanticIntent.Replace ? SemanticIntent.Replace : value.Operation);
                output.Add(new SemanticCommand { Id = nextId++, Intent = vop, Operation = vop, Target = value.Concept, Value = value.Value, Domain = SemanticDomain.Visual, Source = it.Text });
                return;
            }

            string conceptId;
            int polarity = 1;
            string sense = "";
            bool verb = false, both = false;
            SemanticIntent implied = SemanticIntent.Unknown;
            string fixedValue = "";

            if (modifier != null)
            {
                polarity = modifier.Polarity;
                if (nounItem != null)
                {
                    nounItem.Consumed = true;
                    conceptId = noun.ByModifier[modifier.Key];
                }
                else if (!string.IsNullOrEmpty(modifier.Standalone)) conceptId = modifier.Standalone;
                else
                {
                    program.Ambiguities.Add("'" + it.Text + "' needs something to be about (hair, legs, hands...).");
                    return;
                }
            }
            else if (word != null)
            {
                conceptId = word.Concept;
                polarity = word.Polarity;
                sense = word.Sense;
                verb = word.Verb;
                both = word.Both;
                implied = word.Implied;
                fixedValue = word.Value;
                if (word.Domain != SemanticDomain.Unspecified && domain == SemanticDomain.Unspecified) domain = word.Domain;
                if (word.Phase != SemanticPhase.Any && phase == SemanticPhase.Any) phase = word.Phase;
            }
            else
            {
                // a noun by itself: only meaningful with an explicit operation
                conceptId = noun.Concept;
                SemanticIntent nop = st.Explicit;
                if (st.Negated && nop == SemanticIntent.Unknown) nop = SemanticIntent.Remove;
                if (nop == SemanticIntent.Unknown && st.Comparative != SemanticIntent.Unknown && !string.IsNullOrEmpty(noun.ScalarConcept)) { conceptId = noun.ScalarConcept; nop = st.Comparative; }
                if (nop == SemanticIntent.Unknown)
                {
                    if (!it.Consumed && IsOnlyMention(items, k)) program.Ambiguities.Add("'" + it.Text + "' was mentioned, but not what to do with it.");
                    return;
                }
                if (it.Consumed || NounHasModifier(items, k, noun)) return;
                if (nop == SemanticIntent.Replace && string.IsNullOrEmpty(conceptId)) return;
                output.Add(new SemanticCommand
                {
                    Id = nextId++, Intent = nop, Operation = nop, Target = conceptId, Domain = SemanticDomain.Visual, Phase = phase, Magnitude = st.Level, Source = it.Text,
                    Confidence = 1f
                });
                return;
            }

            if (it.Consumed) return;
            if (!concepts.Contains(conceptId))
            {
                program.Ambiguities.Add("'" + it.Text + "' points at an unknown concept '" + conceptId + "'.");
                return;
            }

            concepts.TryGet(conceptId, out ConceptDefinition def);
            var cmd = new SemanticCommand { Id = nextId++, Target = conceptId, Domain = domain, Phase = phase, Magnitude = st.Level, Source = it.Text, Value = fixedValue, Sense = sense };
            cmd.Confidence = 1f;
            cmd.Direction = 1;

            if (implied == SemanticIntent.Add || def.Kind == ConceptKind.Role)
            {
                SemanticIntent op = st.Explicit == SemanticIntent.Remove || st.Negated ? SemanticIntent.Remove : (st.Explicit == SemanticIntent.Unknown ? SemanticIntent.Add : st.Explicit);
                cmd.Operation = cmd.Intent = op;
                cmd.Domain = SemanticDomain.Gameplay;
                if (op == SemanticIntent.Add && st.Comparative == SemanticIntent.Increase) { /* "más fintas": more of it */ cmd.Operation = cmd.Intent = SemanticIntent.Increase; }
                if (st.Comparative == SemanticIntent.Decrease) { cmd.Operation = cmd.Intent = SemanticIntent.Decrease; }
                output.Add(cmd);
                return;
            }

            int pol = st.Flip ? -polarity : polarity;
            if (st.Negated && verb)
            {
                // "no quiero que driblee tanto": play less like that
                cmd.Intent = SemanticIntent.Avoid;
                cmd.Operation = SemanticIntent.Decrease;
                cmd.Magnitude = st.Level == MagnitudeLevel.Unspecified ? MagnitudeLevel.Moderate : st.Level;
                if (pol < 0) cmd.Operation = SemanticIntent.Increase;
                output.Add(cmd);
                AddCompanionDomain(output, cmd, both, domain, ref nextId);
                return;
            }

            if (st.Explicit == SemanticIntent.Remove || st.Explicit == SemanticIntent.Reset || st.Explicit == SemanticIntent.Replace)
            {
                cmd.Operation = cmd.Intent = st.Explicit;
            }
            else if (st.Comparative != SemanticIntent.Unknown)
            {
                SemanticIntent eff = st.Comparative;
                if (pol < 0) eff = eff == SemanticIntent.Increase ? SemanticIntent.Decrease : SemanticIntent.Increase;
                cmd.Operation = cmd.Intent = eff;
                cmd.Negated = st.Negated;
            }
            else if (st.MakeIt && !st.Creating)
            {
                cmd.Operation = pol > 0 ? SemanticIntent.Increase : SemanticIntent.Decrease;
                cmd.Intent = SemanticIntent.Modify;
                cmd.Negated = st.Negated;
            }
            else
            {
                cmd.Operation = cmd.Intent = SemanticIntent.Set;
                cmd.Direction = pol >= 0 ? 1 : -1;
                cmd.Negated = st.Negated;
            }
            output.Add(cmd);
            AddCompanionDomain(output, cmd, both, domain, ref nextId);
        }

        /// <summary>"fuerte" with no hint: it is mainly about play, and (as a weaker guess) about looks.</summary>
        private static void AddCompanionDomain(List<SemanticCommand> output, SemanticCommand cmd, bool both, SemanticDomain domain, ref int nextId)
        {
            if (!both || domain != SemanticDomain.Unspecified) return;
            cmd.Domain = SemanticDomain.Gameplay;
            SemanticCommand visual = cmd.Clone();
            visual.Id = nextId++;
            visual.Domain = SemanticDomain.Visual;
            visual.Confidence = 0.6f;
            output.Add(visual);
        }

        /// <summary>True when nothing else in the sentence part says anything about this noun.</summary>
        private static bool IsOnlyMention(List<Item> items, int k)
        {
            for (int j = 0; j < items.Count; j++)
            {
                if (j == k || !SameHardSegment(items, j, k)) continue;
                Kind kind = items[j].Kind;
                if (kind == Kind.Concept || kind == Kind.Operator || kind == Kind.Preserve) return false;
            }
            return true;
        }

        private static bool NounHasModifier(List<Item> items, int k, NounWord noun)
        {
            for (int j = 0; j < items.Count; j++)
            {
                if (j == k || items[j].Kind != Kind.Concept || !SameHardSegment(items, j, k)) continue;
                foreach (ModifierWord m in items[j].Modifiers)
                    if (noun.ByModifier.ContainsKey(m.Key)) return true;
                // a colour or a named choice in the same part of the sentence already says what to do with the noun
                if (items[j].Colors.Count > 0 && ColorOfNoun.ContainsKey(noun.Concept)) return true;
                if (items[j].Values.Count > 0) return true;
            }
            return false;
        }

        private static string PreserveTarget(Item it, ConceptWord word, ModifierWord modifier, NounWord noun)
        {
            if (noun != null) return !string.IsNullOrEmpty(noun.GroupConcept) ? noun.GroupConcept : noun.Concept;
            if (it.Nouns.Count > 0) return !string.IsNullOrEmpty(it.Nouns[0].GroupConcept) ? it.Nouns[0].GroupConcept : it.Nouns[0].Concept;
            if (word != null) return word.Concept;
            if (it.Values.Count > 0) return it.Values[0].Concept;
            return null;
        }

        /// <summary>The nearest noun in the same sentence part that gives this modifier a meaning (before it first, then after).</summary>
        private static Item FindNoun(List<Item> items, int k, string key, out NounWord found)
        {
            found = null;
            int seg = items[k].Segment;
            Item best = null;
            int bestDist = int.MaxValue;
            for (int j = 0; j < items.Count; j++)
            {
                Item n = items[j];
                if (j == k || n.Kind != Kind.Concept || n.Nouns.Count == 0) continue;
                if (Math.Abs(n.Segment - seg) > 0 && !SameHardSegment(items, j, k)) continue;
                foreach (NounWord nw in n.Nouns)
                {
                    if (!nw.ByModifier.ContainsKey(key)) continue;
                    int dist = Math.Abs(j - k) * 2 + (j > k ? 1 : 0);
                    if (dist < bestDist) { bestDist = dist; best = n; found = nw; }
                }
            }
            return best;
        }

        private static Item FindColorNoun(List<Item> items, int k, out NounWord found)
        {
            found = null;
            Item best = null;
            int bestDist = int.MaxValue;
            for (int j = 0; j < items.Count; j++)
            {
                Item n = items[j];
                if (j == k || n.Kind != Kind.Concept || n.Nouns.Count == 0) continue;
                if (!SameHardSegment(items, j, k)) continue;
                foreach (NounWord nw in n.Nouns)
                {
                    if (!ColorOfNoun.ContainsKey(nw.Concept)) continue;
                    int dist = Math.Abs(j - k) * 2 + (j > k ? 1 : 0);
                    if (dist < bestDist) { bestDist = dist; best = n; found = nw; }
                }
            }
            return best;
        }

        private static bool SameHardSegment(List<Item> items, int a, int b)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            for (int j = lo; j <= hi; j++)
                if (items[j].Kind == Kind.Boundary || items[j].Kind == Kind.Contrast) return false;
            return true;
        }
    }
}
