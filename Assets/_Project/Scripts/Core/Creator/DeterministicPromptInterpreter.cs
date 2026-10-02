using System;
using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// An <see cref="IAICharacterInterpreter"/> that needs no network and no model: it reads Spanish (and a little English) with the
    /// <see cref="PromptLexicon"/>, understands "how much" words, comparatives ("more", "a bit less"), negation ("not so muscular"), several
    /// requests in one sentence, requests about a character that already exists, and the context a word is used in. It is the reference
    /// implementation that lets the whole pipeline be built and tested for free.
    ///
    /// It is honest about its limits: anything it does not recognise is returned as unresolved, contradictions are returned as conflicts
    /// instead of being resolved silently, and requests the engine cannot fulfil are returned as unsupported. It is NOT semantic
    /// understanding: a language-model interpreter replaces it behind the same interface.
    /// </summary>
    public sealed class DeterministicPromptInterpreter : IAICharacterInterpreter
    {
        private readonly CreatorCatalogs catalogs;
        private readonly MagnitudeScale scale;

        public string Name => "FS27.DeterministicPromptInterpreter.v1";

        public DeterministicPromptInterpreter(CreatorCatalogs catalogs, MagnitudeScale scale = null)
        {
            this.catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            this.scale = scale ?? new MagnitudeScale();
        }

        // ------------------------------------------------------------------ text helpers

        internal static string Normalize(string text)
        {
            var sb = new StringBuilder();
            foreach (char raw in text.ToLowerInvariant())
            {
                char c = raw;
                switch (c)
                {
                    case 'á': case 'à': case 'ä': c = 'a'; break;
                    case 'é': case 'è': case 'ë': c = 'e'; break;
                    case 'í': case 'ì': case 'ï': c = 'i'; break;
                    case 'ó': case 'ò': case 'ö': c = 'o'; break;
                    case 'ú': case 'ù': case 'ü': c = 'u'; break;
                    case 'ñ': c = 'n'; break;
                }
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c == ',' || c == '.' || c == ';' || c == '!' || c == '?') sb.Append(" | ");
                else sb.Append(' ');
            }
            return sb.ToString();
        }

        private static readonly HashSet<string> ClauseBreakers = new HashSet<string> { "pero", "aunque", "despues", "luego", "ademas", "mientras" };
        private static readonly HashSet<string> Connectors = new HashSet<string> { "y", "e", "pero", "que", "con", "aunque", "|" };
        private static readonly HashSet<string> CreateVerbs = new HashSet<string> { "crea", "creame", "crear", "genera", "generame", "hazme", "dame" };
        private static readonly HashSet<string> NewCharacterMarkers = new HashSet<string> { "nuevo", "otro" };
        private static readonly HashSet<string> AnimationWords = new HashSet<string> { "animacion", "animaciones", "zancada", "zancadas" };
        private static readonly HashSet<string> VisualWords = new HashSet<string> { "parezca", "parezcan", "aspecto", "apariencia", "luzca", "look", "vea" };

        private static List<List<string>> SplitClauses(string normalized)
        {
            var clauses = new List<List<string>>();
            var current = new List<string>();
            foreach (string tok in normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tok == "|" || ClauseBreakers.Contains(tok))
                {
                    if (current.Count > 0) clauses.Add(current);
                    current = new List<string>();
                    continue;
                }
                current.Add(tok);
            }
            if (current.Count > 0) clauses.Add(current);
            return clauses;
        }

        // ------------------------------------------------------------------ modifiers

        private struct Modifiers
        {
            public int Comparative;      // +1 more, -1 less, 0 none
            public bool Negated;         // "not" / "without"
            public MagnitudeWord Word;
            public bool Any => Comparative != 0 || Negated || Word != MagnitudeWord.None;
        }

        private static MagnitudeWord WordOf(string t)
        {
            switch (t)
            {
                case "ligeramente": case "levemente": return MagnitudeWord.Slight;
                case "poco": case "poquito": return MagnitudeWord.Little;
                case "bastante": return MagnitudeWord.Quite;
                case "mucho": case "muy": case "super": case "realmente": return MagnitudeWord.Much;
                case "extremadamente": case "increiblemente": case "totalmente": case "absolutamente": return MagnitudeWord.Extreme;
                default: return MagnitudeWord.None;
            }
        }

        /// <summary>Reads the modifiers in the (up to) 3 words before <paramref name="start"/>, stopping at a connector or at an earlier match.</summary>
        private static Modifiers ScanBack(List<string> t, bool[] consumed, int start, int floor)
        {
            var m = new Modifiers();
            bool sawTan = false;
            for (int i = start - 1; i >= Math.Max(floor, start - 3); i--)
            {
                string w = t[i];
                if (consumed[i] || Connectors.Contains(w)) break;
                if (w == "mas") m.Comparative = 1;
                else if (w == "menos") m.Comparative = -1;
                else if (w == "tan") sawTan = true;
                else if (w == "no" || w == "sin" || w == "nada")
                {
                    if (sawTan) { m.Comparative = -1; if (m.Word == MagnitudeWord.None) m.Word = MagnitudeWord.Little; }
                    else m.Negated = true;
                }
                else
                {
                    MagnitudeWord mw = WordOf(w);
                    if (mw != MagnitudeWord.None && m.Word == MagnitudeWord.None) m.Word = mw;
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ matching

        private sealed class Match
        {
            public int Start;
            public int End; // inclusive
            public string Text;
            public LexiconEffect[] Effects;
            public bool Ambiguous;
            public Modifiers Mods;
            public bool Bound; // subject-bound (does not inherit modifiers)
        }

        private static readonly List<KeyValuePair<string[], LexiconEntry>> PhraseIndex = BuildIndex();

        private static List<KeyValuePair<string[], LexiconEntry>> BuildIndex()
        {
            var list = new List<KeyValuePair<string[], LexiconEntry>>();
            foreach (LexiconEntry e in PromptLexicon.Phrases)
                foreach (string p in e.Phrases)
                    list.Add(new KeyValuePair<string[], LexiconEntry>(p.Split(' '), e));
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list;
        }

        private List<Match> FindMatches(List<string> t, bool[] consumed, PromptContext context)
        {
            var matches = new List<Match>();

            // 1. Subject-bound descriptors ("pelo corto rizado", "ojos grandes", "camiseta roja").
            for (int i = 0; i < t.Count; i++)
            {
                if (consumed[i] || !PromptLexicon.Subjects.TryGetValue(t[i], out string subject)) continue;
                Dictionary<string, LexiconEffect[]> table;
                PromptLexicon.Descriptors.TryGetValue(subject, out table);
                bool canColor = PromptLexicon.ColorSlotOfSubject.ContainsKey(subject);
                int scanned = 0;
                int pendingStart = i + 1;
                bool any = false;
                for (int j = i + 1; j < t.Count && scanned < 6; j++, scanned++)
                {
                    string w = t[j];
                    if (PromptLexicon.Subjects.ContainsKey(w) && !consumed[j]) break;
                    LexiconEffect[] fx = null;
                    if (table != null && table.TryGetValue(w, out fx)) { }
                    else if (canColor && PromptLexicon.Colors.TryGetValue(w, out string hex))
                        fx = new[] { LexiconEffect.Col(PromptLexicon.ColorSlotOfSubject[subject], hex) };
                    if (fx == null) continue;

                    var m = new Match { Start = i, End = j, Text = t[i] + " " + w, Effects = fx, Bound = true };
                    // Modifiers sit between the subject and the descriptor ("pelo mas corto"), or just before the subject ("mas pelo").
                    m.Mods = ScanBack(t, consumed, j, i + 1);
                    if (!m.Mods.Any) m.Mods = ScanBack(t, consumed, i, 0);
                    matches.Add(m);
                    for (int k = i; k <= j; k++) consumed[k] = true;
                    any = true;
                    pendingStart = j + 1;
                    scanned = 0;
                }
                if (!any) pendingStart = i + 1;
            }

            // 2. Standalone phrases, longest first.
            for (int i = 0; i < t.Count; i++)
            {
                if (consumed[i]) continue;
                foreach (KeyValuePair<string[], LexiconEntry> kv in PhraseIndex)
                {
                    string[] p = kv.Key;
                    if (i + p.Length > t.Count) continue;
                    bool ok = true;
                    for (int k = 0; k < p.Length && ok; k++) ok = !consumed[i + k] && t[i + k] == p[k];
                    if (!ok) continue;

                    LexiconEntry e = kv.Value;
                    LexiconEffect[] fx = e.Default;
                    bool ambiguous = e.Ambiguous && context == PromptContext.Unspecified;
                    if (context == PromptContext.Visual && e.Visual != null) fx = e.Visual;
                    else if (context == PromptContext.Animation && e.Animation != null) fx = e.Animation;
                    var m = new Match { Start = i, End = i + p.Length - 1, Text = string.Join(" ", p), Effects = fx, Ambiguous = ambiguous };
                    for (int k = i; k < i + p.Length; k++) consumed[k] = true;
                    matches.Add(m);
                    i += p.Length - 1;
                    break;
                }
            }
            matches.Sort((a, b) => a.Start.CompareTo(b.Start));

            // 3. Modifiers of standalone matches (they were consumed in the order above, so scan with a fresh view of what is consumed).
            var owned = new bool[t.Count];
            foreach (Match m in matches) for (int k = m.Start; k <= m.End; k++) owned[k] = true;
            for (int idx = 0; idx < matches.Count; idx++)
            {
                Match m = matches[idx];
                if (m.Bound) continue;
                // Words that belong to an EARLIER match stop the scan; this match's own words are not scanned (they start at m.Start).
                var blocked = (bool[])owned.Clone();
                m.Mods = ScanBack(t, blocked, m.Start, 0);
                if (!m.Mods.Any && idx > 0) m.Mods = Inherit(t, matches[idx - 1], m);
            }
            return matches;
        }

        /// <summary>"mas creativo y arriesgado": the second one keeps the first one's comparative, when only "y"/"," lies between them.</summary>
        private static Modifiers Inherit(List<string> t, Match prev, Match m)
        {
            if (prev.Bound || prev.Mods.Comparative == 0) return default;
            for (int i = prev.End + 1; i < m.Start; i++)
                if (t[i] != "y" && t[i] != "e") return default;
            return new Modifiers { Comparative = prev.Mods.Comparative, Word = prev.Mods.Word };
        }

        // ------------------------------------------------------------------ the interpretation

        public PromptResult Interpret(PromptRequest request)
        {
            var result = new PromptResult { InterpreterName = Name };
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                result.Intent = PromptIntentKind.Unknown;
                result.Warnings.Add("The request is empty.");
                return result;
            }

            string normalized = Normalize(request.Text);
            string[] allTokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            PromptContext context = DetectContext(allTokens);
            result.Context = context;

            bool creating = request.Existing == null || WantsNewCharacter(allTokens);
            CharacterSpecification baseSpec = creating ? catalogs.NewSpecification(request.NewCharacterId ?? "character-new") : request.Existing;

            var raw = new List<SpecChange>();
            bool guard = false;
            int unresolvedClauses = 0;

            foreach (List<string> clause in SplitClauses(normalized))
            {
                var consumed = new bool[clause.Count];
                List<Match> matches = FindMatches(clause, consumed, context);
                if (matches.Count == 0)
                {
                    if (HasContent(clause)) { result.Unresolved.Add(string.Join(" ", clause)); unresolvedClauses++; }
                    continue;
                }
                foreach (Match m in matches)
                {
                    if (m.Ambiguous) result.Warnings.Add("'" + m.Text + "' can mean something about gameplay or about looks or animation; interpreted as gameplay. Say which if you meant another.");
                    foreach (LexiconEffect fx in m.Effects)
                    {
                        if (fx.Type == EffectType.GuardChildlike) { guard = true; continue; }
                        if (fx.Type == EffectType.Unsupported)
                        {
                            result.Unsupported.Add(new UnsupportedRequest { Text = m.Text, Reason = fx.Reason, Explanation = fx.Explanation });
                            continue;
                        }
                        Translate(fx, m, context, baseSpec, creating, raw, result);
                    }
                }
            }

            if (guard) foreach (SpecChange c in raw) if (c.Kind == SpecChangeKind.StyleShift) c.GuardChildlike = true;
            if (guard && !raw.Exists(c => c.Kind == SpecChangeKind.StyleShift))
                result.Warnings.Add("\"not childish\" was said but nothing in this request makes the character more cartoon, so it only limits future style shifts in this request (none).");

            List<SpecChange> merged = MergeAndDetectConflicts(raw, result);
            result.Changes = merged;
            AttachProfileCombinationRules(result, baseSpec, creating);

            bool understood = result.Changes.Count > 0 || result.AttributeHints.Count > 0 || result.ProfileHints.Count > 0;
            if (!understood)
            {
                // Nothing usable was found (only unsupported or unknown requests): no draft is invented.
                result.Intent = PromptIntentKind.Unknown;
                result.Confidence = 0f;
                return result;
            }
            result.Intent = DetermineIntent(creating, result);
            ModificationReport applied = ModificationApplier.Apply(baseSpec, result.Changes, catalogs);
            result.Draft = applied.Result;
            foreach (string s in applied.Skipped) result.Warnings.Add("Skipped: " + s);
            foreach (string s in applied.Clamped) result.Warnings.Add("Limited: " + s);
            result.Draft.Authoring = new AuthoringData { Source = "prompt", Generator = Name, Prompt = request.Text };

            result.Confidence = ComputeConfidence(result, unresolvedClauses);
            return result;
        }

        private static bool WantsNewCharacter(string[] tokens)
        {
            foreach (string t in tokens)
                if (CreateVerbs.Contains(t)) return true;
            for (int i = 0; i + 1 < tokens.Length; i++)
                if (NewCharacterMarkers.Contains(tokens[i]) && tokens[i + 1] == "jugador") return true;
            return false;
        }

        private static PromptContext DetectContext(string[] tokens)
        {
            foreach (string t in tokens) if (AnimationWords.Contains(t)) return PromptContext.Animation;
            foreach (string t in tokens) if (VisualWords.Contains(t)) return PromptContext.Visual;
            return PromptContext.Unspecified;
        }

        private static readonly HashSet<string> Filler = new HashSet<string>
        {
            "quiero", "un", "una", "el", "la", "los", "las", "que", "sea", "tenga", "de", "en", "y", "e", "hazlo", "hazle", "ponle", "pon", "cambia",
            "jugador", "personaje", "a", "con", "lo", "le", "se", "mas", "menos", "muy", "poco", "mucho", "crea", "creame", "dame", "hazme", "genera"
        };

        private static bool HasContent(List<string> clause)
        {
            foreach (string t in clause) if (!Filler.Contains(t)) return true;
            return false;
        }

        // ------------------------------------------------------------------ effect -> change

        private void Translate(LexiconEffect fx, Match m, PromptContext context, CharacterSpecification baseSpec, bool creating, List<SpecChange> changes, PromptResult result)
        {
            Modifiers mods = m.Mods;
            bool relative = mods.Comparative != 0;
            float conf = relative ? 0.8f : 0.9f;
            if (m.Ambiguous) conf = Math.Min(conf, 0.7f);
            float intensity = scale.IntensityOf(mods.Word);

            switch (fx.Type)
            {
                case EffectType.Param:
                {
                    var c = new SpecChange { Target = fx.Target, Confidence = conf, Context = ContextOf(fx.Target), Source = m.Text };
                    if (relative)
                    {
                        float mag = scale.DeltaOf(mods.Word == MagnitudeWord.None ? MagnitudeWord.Normal : mods.Word);
                        c.Kind = SpecChangeKind.ScalarDelta;
                        c.Direction = mods.Comparative * (fx.Level >= 0.5f ? 1 : -1);
                        c.Magnitude = Math.Min(1f, mag);
                    }
                    else
                    {
                        float level = mods.Negated ? 1f - fx.Level : fx.Level;
                        c.Kind = SpecChangeKind.ScalarSet;
                        c.Level = MathUtil.Clamp01(0.5f + (level - 0.5f) * intensity);
                    }
                    changes.Add(c);
                    break;
                }
                case EffectType.Choice:
                    if (relative || mods.Negated) { if (mods.Negated) result.Warnings.Add("Negating a choice ('" + m.Text + "') is not supported; ignored."); break; }
                    changes.Add(new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = fx.Target, Value = fx.Value, Confidence = conf, Context = PromptContext.Visual, Source = m.Text });
                    break;
                case EffectType.Color:
                    if (relative || mods.Negated) break;
                    changes.Add(new SpecChange { Kind = SpecChangeKind.ColorSet, Target = fx.Target, Value = fx.Value, Confidence = conf, Context = PromptContext.Visual, Source = m.Text });
                    break;
                case EffectType.Style:
                {
                    float mag = scale.DeltaOf(mods.Word == MagnitudeWord.None ? MagnitudeWord.Normal : mods.Word);
                    int dir = (fx.Level >= 0.5f ? 1 : -1) * (mods.Negated ? -1 : 1) * (mods.Comparative == -1 ? -1 : 1);
                    changes.Add(new SpecChange { Kind = SpecChangeKind.StyleShift, Target = "cartoon", Direction = dir, Magnitude = mag, Confidence = conf, Context = PromptContext.Visual, Source = m.Text });
                    break;
                }
                case EffectType.Behavior:
                {
                    float weight = MathUtil.Clamp01(0.5f + (fx.Level - 0.5f) * intensity);
                    bool have = baseSpec.Dna.TryGetBehavior(fx.Target, out BehaviorEntry existing);
                    if (relative)
                    {
                        float delta = mods.Comparative * scale.DeltaOf(mods.Word == MagnitudeWord.None ? MagnitudeWord.Normal : mods.Word);
                        float w = MathUtil.Clamp01((have ? existing.Weight : (mods.Comparative > 0 ? 0.4f : 0f)) + delta);
                        if (mods.Comparative < 0 && w <= 0.05f) changes.Add(new SpecChange { Kind = SpecChangeKind.BehaviorRemove, Target = fx.Target, Confidence = conf, Context = PromptContext.Gameplay, Source = m.Text });
                        else if (mods.Comparative > 0 || have) changes.Add(new SpecChange { Kind = SpecChangeKind.BehaviorAdd, Target = fx.Target, Level = w, Confidence = conf, Context = PromptContext.Gameplay, Source = m.Text });
                    }
                    else if (mods.Negated) changes.Add(new SpecChange { Kind = SpecChangeKind.BehaviorRemove, Target = fx.Target, Confidence = conf, Context = PromptContext.Gameplay, Source = m.Text });
                    else changes.Add(new SpecChange { Kind = SpecChangeKind.BehaviorAdd, Target = fx.Target, Level = weight, Confidence = conf, Context = PromptContext.Gameplay, Source = m.Text });
                    break;
                }
                case EffectType.Attribute:
                {
                    var h = new AttributeHint { Attribute = fx.Attribute, Confidence = conf, Source = m.Text };
                    if (relative) { h.Relative = true; h.Delta = mods.Comparative * (fx.Level >= 0.5f ? 1 : -1) * scale.DeltaOf(mods.Word == MagnitudeWord.None ? MagnitudeWord.Normal : mods.Word); }
                    else h.Level = MathUtil.Clamp01(0.5f + ((mods.Negated ? 1f - fx.Level : fx.Level) - 0.5f) * intensity);
                    result.AttributeHints.Add(h);
                    break;
                }
                case EffectType.Profile:
                {
                    var h = new ProfileHint { Kind = fx.ProfileKind, Zone = fx.Zone, Role = fx.Role, Confidence = conf, Source = m.Text };
                    bool numeric = fx.ProfileKind == ProfileHintKind.Risk || fx.ProfileKind == ProfileHintKind.Creativity || fx.ProfileKind == ProfileHintKind.Aggression;
                    if (numeric && relative) { h.Relative = true; h.Delta = mods.Comparative * (fx.Level >= 0.5f ? 1 : -1) * scale.DeltaOf(mods.Word == MagnitudeWord.None ? MagnitudeWord.Normal : mods.Word); }
                    else if (numeric) h.Level = MathUtil.Clamp01(0.5f + ((mods.Negated ? 1f - fx.Level : fx.Level) - 0.5f) * intensity);
                    else if (mods.Negated) break;
                    else h.Level = fx.Level;
                    result.ProfileHints.Add(h);
                    break;
                }
            }
        }

        private PromptContext ContextOf(string parameterId)
        {
            return catalogs.Parameters.TryGet(parameterId, out ParameterDefinition p) && p.Domain == ParameterDomain.FootballDna ? PromptContext.Gameplay : PromptContext.Visual;
        }

        // ------------------------------------------------------------------ merging and conflicts

        private List<SpecChange> MergeAndDetectConflicts(List<SpecChange> raw, PromptResult result)
        {
            var conflictedTargets = new HashSet<string>();

            // Contradictions between scalar sets (one clearly high, one clearly low), opposite deltas, and add/remove of the same behaviour.
            for (int i = 0; i < raw.Count; i++)
            {
                for (int j = i + 1; j < raw.Count; j++)
                {
                    SpecChange a = raw[i], b = raw[j];
                    if (a.Target != b.Target) continue;
                    bool bad =
                        (a.Kind == SpecChangeKind.ScalarSet && b.Kind == SpecChangeKind.ScalarSet && ((a.Level > 0.6f && b.Level < 0.4f) || (a.Level < 0.4f && b.Level > 0.6f))) ||
                        (a.Kind == SpecChangeKind.ScalarDelta && b.Kind == SpecChangeKind.ScalarDelta && a.Direction == -b.Direction) ||
                        (a.Kind == SpecChangeKind.StyleShift && b.Kind == SpecChangeKind.StyleShift && a.Direction == -b.Direction) ||
                        ((a.Kind == SpecChangeKind.BehaviorAdd && b.Kind == SpecChangeKind.BehaviorRemove) || (a.Kind == SpecChangeKind.BehaviorRemove && b.Kind == SpecChangeKind.BehaviorAdd));
                    if (!bad || !conflictedTargets.Add(a.Target)) continue;
                    result.Conflicts.Add(new PromptConflict
                    {
                        Kind = ConflictKind.Contradiction, Target = a.Target, PhraseA = a.Source, PhraseB = b.Source,
                        Description = "'" + a.Source + "' and '" + b.Source + "' ask for opposite things for " + a.Target + ". Nothing was applied to it; say which you want."
                    });
                }
            }

            // Whatever came from a contradicted phrase is dropped entirely: a half-applied "tall" next to a refused height would be misleading.
            var contradictedPhrases = new HashSet<string>();
            foreach (PromptConflict pc in result.Conflicts) { contradictedPhrases.Add(pc.PhraseA); contradictedPhrases.Add(pc.PhraseB); }

            // Two values for the same part slot: keep the first mentioned, and say so.
            var chosen = new Dictionary<string, SpecChange>();
            var merged = new List<SpecChange>();
            foreach (SpecChange c in raw)
            {
                if (contradictedPhrases.Contains(c.Source)) continue;
                if (conflictedTargets.Contains(c.Target) && c.Kind != SpecChangeKind.ChoiceSet && c.Kind != SpecChangeKind.ColorSet) continue;
                if (c.Kind == SpecChangeKind.ChoiceSet || c.Kind == SpecChangeKind.ColorSet)
                {
                    string key = c.Kind + ":" + c.Target;
                    if (chosen.TryGetValue(key, out SpecChange first))
                    {
                        if (first.Value != c.Value) result.Warnings.Add("Two requests for " + c.Target + " ('" + first.Source + "' and '" + c.Source + "'); kept the first.");
                        continue;
                    }
                    chosen[key] = c;
                    merged.Add(c);
                    continue;
                }
                // Same direction on the same target: keep the stronger request.
                SpecChange same = merged.Find(x => x.Kind == c.Kind && x.Target == c.Target);
                if (same == null) { merged.Add(c); continue; }
                switch (c.Kind)
                {
                    case SpecChangeKind.ScalarSet: if (Math.Abs(c.Level - 0.5f) > Math.Abs(same.Level - 0.5f)) { merged.Remove(same); merged.Add(c); } break;
                    case SpecChangeKind.ScalarDelta: case SpecChangeKind.StyleShift: if (c.Magnitude > same.Magnitude) { merged.Remove(same); merged.Add(c); } break;
                    case SpecChangeKind.BehaviorAdd: if (c.Level > same.Level) { merged.Remove(same); merged.Add(c); } break;
                }
            }

            // Hints: contradictory attribute wishes are reported and dropped, the rest are merged by strength.
            var attrs = new List<AttributeHint>();
            var droppedAttrs = new HashSet<PlayerAttributeId>();
            foreach (AttributeHint h in result.AttributeHints)
            {
                if (droppedAttrs.Contains(h.Attribute)) continue;
                int idx = attrs.FindIndex(x => x.Attribute == h.Attribute && x.Relative == h.Relative);
                if (idx < 0) { attrs.Add(h); continue; }
                AttributeHint prev = attrs[idx];
                bool opposite = !h.Relative && ((prev.Level > 0.6f && h.Level < 0.4f) || (prev.Level < 0.4f && h.Level > 0.6f));
                if (opposite)
                {
                    attrs.RemoveAt(idx);
                    droppedAttrs.Add(h.Attribute);
                    result.Conflicts.Add(new PromptConflict
                    {
                        Kind = ConflictKind.Contradiction, Target = "attribute." + h.Attribute, PhraseA = prev.Source, PhraseB = h.Source,
                        Description = "'" + prev.Source + "' and '" + h.Source + "' ask for opposite " + h.Attribute + " values. Nothing was suggested for it."
                    });
                }
                else if (!h.Relative ? Math.Abs(h.Level - 0.5f) > Math.Abs(prev.Level - 0.5f) : Math.Abs(h.Delta) > Math.Abs(prev.Delta)) attrs[idx] = h;
            }
            result.AttributeHints = attrs;

            var profiles = new List<ProfileHint>();
            foreach (ProfileHint h in result.ProfileHints)
            {
                int idx = profiles.FindIndex(x => x.Kind == h.Kind && x.Role == h.Role && x.Zone == h.Zone);
                if (idx < 0) profiles.Add(h);
                else if (!h.Relative && Math.Abs(h.Level - 0.5f) > Math.Abs(profiles[idx].Level - 0.5f)) profiles[idx] = h;
            }
            result.ProfileHints = profiles;

            // Tensions: not contradictory, but pulling against each other.
            SpecChange muscle = merged.Find(x => x.Kind == SpecChangeKind.ScalarSet && x.Target == "body.muscularity");
            SpecChange mass = merged.Find(x => x.Kind == SpecChangeKind.ScalarSet && x.Target == "body.mass");
            if (muscle != null && mass != null && muscle.Level >= 0.75f && mass.Level <= 0.3f)
            {
                result.Conflicts.Add(new PromptConflict
                {
                    Kind = ConflictKind.Tension, Target = "body.muscularity/body.mass", PhraseA = muscle.Source, PhraseB = mass.Source,
                    Description = "Very muscular and very light pull against each other. Both were applied; the result may look odd. Consider choosing."
                });
            }
            return merged;
        }

        /// <summary>Combination rules that need two pieces of the request together (not one word).</summary>
        private void AttachProfileCombinationRules(PromptResult result, CharacterSpecification baseSpec, bool creating)
        {
            SpecChange texture = result.Changes.Find(c => c.Kind == SpecChangeKind.ChoiceSet && c.Target == "hair.texture");
            SpecChange style = result.Changes.Find(c => c.Kind == SpecChangeKind.ChoiceSet && c.Target == "hair.style");
            SpecChange length = result.Changes.Find(c => c.Kind == SpecChangeKind.ScalarSet && c.Target == "hair.length");
            if (style == null && texture != null && texture.Value == "curly" && length != null && length.Level <= 0.3f)
                result.Changes.Add(new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = "hair.style", Value = "short_curly_07", Confidence = 0.85f, Context = PromptContext.Visual, Source = texture.Source + " + " + length.Source });
            else if (style == null && texture == null && length != null && length.Level <= 0.2f && !creating)
                result.Changes.Add(new SpecChange { Kind = SpecChangeKind.ChoiceSet, Target = "hair.style", Value = "short_crop_01", Confidence = 0.8f, Context = PromptContext.Visual, Source = length.Source });
        }

        private static PromptIntentKind DetermineIntent(bool creating, PromptResult r)
        {
            if (creating) return PromptIntentKind.CreateCharacter;
            bool body = false, face = false, hair = false, cloth = false, style = false, dna = false, add = false, remove = false, any = false;
            foreach (SpecChange c in r.Changes)
            {
                any = true;
                string t = c.Target ?? "";
                if (c.Kind == SpecChangeKind.StyleShift || t.StartsWith("style.")) style = true;
                else if (c.Kind == SpecChangeKind.BehaviorAdd) { add = true; dna = true; }
                else if (c.Kind == SpecChangeKind.BehaviorRemove) { remove = true; dna = true; }
                else if (t.StartsWith("body.")) body = true;
                else if (t.StartsWith("head.") || t.StartsWith("face.") || t.StartsWith("skin.")) face = true;
                else if (t.StartsWith("hair.")) hair = true;
                else if (t.StartsWith("kit.")) cloth = true;
                else dna = true;
            }
            bool playStyle = r.ProfileHints.Count > 0 || r.AttributeHints.Count > 0;
            int kinds = (body ? 1 : 0) + (face ? 1 : 0) + (hair ? 1 : 0) + (cloth ? 1 : 0) + (style ? 1 : 0) + (dna ? 1 : 0);
            if (!any && playStyle) return PromptIntentKind.ChangePlayStyle;
            if (kinds > 1 || (kinds == 1 && playStyle && !dna)) return PromptIntentKind.ModifyCharacter;
            if (body) return PromptIntentKind.ModifyBody;
            if (face) return PromptIntentKind.ModifyFace;
            if (hair) return PromptIntentKind.ModifyHair;
            if (cloth) return PromptIntentKind.ModifyClothing;
            if (style) return PromptIntentKind.ModifyStyle;
            if (add && !remove && !r.Changes.Exists(c => c.Kind == SpecChangeKind.ScalarSet || c.Kind == SpecChangeKind.ScalarDelta)) return PromptIntentKind.AddBehavior;
            if (remove && !add && !r.Changes.Exists(c => c.Kind == SpecChangeKind.ScalarSet || c.Kind == SpecChangeKind.ScalarDelta)) return PromptIntentKind.RemoveBehavior;
            if (dna && playStyle) return PromptIntentKind.ChangePlayStyle;
            if (dna) return PromptIntentKind.ModifyFootballDna;
            return any ? PromptIntentKind.ModifyCharacter : PromptIntentKind.Unknown;
        }

        private static float ComputeConfidence(PromptResult r, int unresolvedClauses)
        {
            float min = 1f;
            bool any = false;
            foreach (SpecChange c in r.Changes) { min = Math.Min(min, c.Confidence); any = true; }
            foreach (AttributeHint h in r.AttributeHints) { min = Math.Min(min, h.Confidence); any = true; }
            foreach (ProfileHint h in r.ProfileHints) { min = Math.Min(min, h.Confidence); any = true; }
            if (!any) return 0f;
            float c2 = min * (1f - 0.12f * unresolvedClauses) * (r.Conflicts.Count > 0 ? 0.8f : 1f);
            return MathUtil.Clamp01(c2);
        }
    }
}
