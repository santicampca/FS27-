using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    public sealed class CreatorRequest
    {
        public string Text = "";
        /// <summary>The character being edited; null creates a new one.</summary>
        public AuthoringDraft Current;
        /// <summary>The character before the current one (for "como el anterior").</summary>
        public AuthoringDraft Previous;
        /// <summary>Decides every "pick one" (a new hairstyle, a default pick). Same text + same draft + same seed = same result.</summary>
        public uint Seed = 1;
        public string CharacterId = "character-001";
        public string StyleId = DefaultStyles.CartoonSports;
        /// <summary>Fill in football tendencies and signature behaviours from the roles and profile the prompt implies (the draft itself is not changed).</summary>
        public bool EnrichDna = true;
        public bool WantPreviewPng;
        public bool WantGlb;
        /// <summary>When something unresolved blocks part of the request, apply NOTHING and ask (default). False applies the safe part.</summary>
        public bool Transactional = true;
    }

    /// <summary>What the character would do in a typical moment: the top behaviours for a named situation.</summary>
    public sealed class BehaviorPreviewEntry
    {
        public string Situation = "";
        public readonly List<string> Top = new List<string>();
    }

    /// <summary>Everything needed to BUILD and SHOW the character, as data (still no engine objects).</summary>
    public sealed class CreatorGenerationPlan
    {
        public CharacterAssemblyPlan Assembly;
        public MovementSignature Signature;
        public MovementPersonality Personality;
        public string AnimationLibrary = "";
        public readonly List<string> AnimationTags = new List<string>();
        /// <summary>The animation that would play in a few typical moments (idle, jog, run, shot...), each marked as having no clip yet when that is the case.</summary>
        public readonly List<string> SampleAnimations = new List<string>();
    }

    /// <summary>
    /// The outcome of one pass through the pipeline. <see cref="Success"/> is true ONLY when a valid specification came out and nothing
    /// unresolved is left: never when anything is invalid, contradictory or not understood.
    /// </summary>
    public sealed class CreatorResult
    {
        public bool Success;
        /// <summary>The character (with its prompt, for authoring). Null when nothing usable came out.</summary>
        public CharacterSpecification Specification;
        /// <summary>The authoring state after the request: pass it back as <see cref="CreatorRequest.Current"/> for the next turn.</summary>
        public AuthoringDraft Draft;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<SemanticConflict> Conflicts = new List<SemanticConflict>();
        public readonly List<string> Clarifications = new List<string>();
        public readonly List<UnsupportedCapability> Unsupported = new List<UnsupportedCapability>();
        public readonly List<AttributeHint> AttributeHints = new List<AttributeHint>();
        public readonly List<ProfileHint> ProfileHints = new List<ProfileHint>();
        public readonly List<BehaviorPreviewEntry> BehaviorPreview = new List<BehaviorPreviewEntry>();
        /// <summary>A suggested goalkeeper profile, when the character is a goalkeeper (null otherwise). Like attribute hints, only a suggestion for the player's owner to accept.</summary>
        public GoalkeeperProfile Goalkeeper;
        public readonly List<string> Stages = new List<string>();
        public SemanticProgram Program;
        public InterpretationReport Interpretation;
        public CharacterSpecificationPatch Patch;
        public CreatorGenerationPlan GenerationPlan;
        public CharacterPreviewData Preview;
        public RuntimeCharacterRecord RuntimeData;
        public AuthoringCharacterRecord AuthoringData;
        public string DebugReport = "";
        public bool NeedsClarification;
    }

    /// <summary>
    /// Prompt -> Interpreter -> Semantic model -> Patch / creation -> Specification -> Validation -> FootballDNA -> Behaviour resolution -> Assembly
    /// plan -> Preview -> Runtime export. One call, one result, every stage explained. The interpreter is a seam (a deterministic parser or a
    /// language-model provider): whatever it returns is DATA, validated before use, and never executed.
    /// </summary>
    public sealed class CreatorPipeline
    {
        private readonly CreatorCatalogs catalogs;
        private readonly ConceptCatalog concepts;
        private readonly ISemanticInterpreter interpreter;
        private readonly SemanticCompiler compiler;
        private readonly MaterialCatalog materials;
        private readonly AnimationCatalog animations;
        private readonly CompatibilityRules rules;
        private readonly ContentIndex index;
        private readonly ProceduralCharacterGenerator generator;
        private readonly CatalogAnimationResolver animationResolver;

        public CreatorPipeline(CreatorCatalogs catalogs, ConceptCatalog concepts, ISemanticInterpreter interpreter, MaterialCatalog materials = null, AnimationCatalog animations = null, CompatibilityRules rules = null)
        {
            this.catalogs = catalogs;
            this.concepts = concepts;
            this.interpreter = interpreter;
            this.materials = materials ?? MaterialCatalog.CreateDefault();
            this.animations = animations ?? AnimationCatalog.CreateDefault();
            this.rules = rules ?? CompatibilityRules.CreateDefault();
            compiler = new SemanticCompiler(catalogs, concepts);
            index = ContentIndex.Build(catalogs, this.materials, this.animations);
            generator = new ProceduralCharacterGenerator(catalogs, this.materials);
            animationResolver = new CatalogAnimationResolver(this.animations, catalogs);
        }

        public static CreatorPipeline CreateDefault()
        {
            CreatorCatalogs cat = CreatorCatalogs.CreateDefault();
            ConceptCatalog concepts = DefaultConcepts.Create();
            return new CreatorPipeline(cat, concepts, new SemanticParser(concepts, LanguagePackEs.Create(), LanguagePackEn.Create()));
        }

        public CreatorCatalogs Catalogs => catalogs;
        public ContentIndex Index => index;
        public ISemanticInterpreter Interpreter => interpreter;

        public CreatorResult Run(CreatorRequest req)
        {
            var res = new CreatorResult();
            Finish(res, RunStages(req, res), req);
            return res;
        }

        private bool RunStages(CreatorRequest req, CreatorResult res)
        {
            // ---- 1. interpret
            res.Stages.Add("interpret (" + interpreter.Name + ")");
            SemanticProgram program;
            try { program = interpreter.Interpret(req.Text); }
            catch (Exception e)
            {
                res.Errors.Add("The interpreter failed: " + e.Message);
                return false;
            }
            res.Program = program;

            // ---- 2. semantic model check
            res.Stages.Add("validate semantic model");
            CreatorValidationResult pv = SemanticProgramValidator.Validate(program, concepts);
            if (!pv.IsValid)
            {
                foreach (CreatorIssue i in pv.Issues) res.Errors.Add("Interpretation invalid: " + i);
                return false;
            }
            if (program.IsEmpty)
            {
                res.Errors.Add("Nothing in the request was understood.");
                foreach (string u in program.Unparsed) res.Clarifications.Add("I did not understand '" + u + "'.");
                foreach (string a in program.Ambiguities) res.Clarifications.Add(a);
                res.NeedsClarification = true;
                return false;
            }

            // ---- 3. the base to work on
            bool creates = false;
            bool wantsPrevious = false;
            foreach (SemanticCommand c in program.Commands)
            {
                if (c.Intent == SemanticIntent.Create) creates = true;
                if (c.Reference == EntityReference.Previous) wantsPrevious = true;
                if (c.Intent == SemanticIntent.Undo) res.Warnings.Add("'Undo' needs a conversation (an AuthoringSession); a single pipeline run cannot undo.");
            }
            AuthoringDraft baseDraft;
            if (req.Current != null && !creates) baseDraft = req.Current.Clone();
            else if (wantsPrevious && req.Previous != null) { baseDraft = req.Previous.Clone(); baseDraft.Spec.CharacterId = req.CharacterId; }
            else
            {
                if (wantsPrevious) res.Warnings.Add("There is no previous character; started a new one.");
                baseDraft = new AuthoringDraft(catalogs.NewSpecification(req.CharacterId, req.StyleId));
            }
            if (baseDraft.Spec.Authoring == null) baseDraft.Spec.Authoring = new AuthoringData { Source = "prompt" };

            // ---- 4. compile to a patch
            res.Stages.Add("compile to patch");
            CompileResult compiled = compiler.Compile(program, baseDraft, req.Seed);
            res.Interpretation = compiled.Report;
            res.Conflicts.AddRange(compiled.Conflicts);
            res.Unsupported.AddRange(compiled.Unsupported);
            res.Clarifications.AddRange(compiled.Clarifications);
            res.Warnings.AddRange(compiled.Warnings);
            res.Patch = compiled.Patch;
            if (compiled.HasBlockingConflict)
            {
                res.NeedsClarification = true;
                foreach (SemanticConflict c in compiled.Conflicts) if (c.Blocks) res.Errors.Add("Conflict: " + string.Join(" ", c.Messages));
                if (req.Transactional)
                {
                    res.Specification = req.Current?.Spec;
                    res.Draft = req.Current;
                    return false;
                }
            }
            bool onlyConstraints = program.Commands.Count == 0 && program.Constraints.Count > 0 && compiled.Unsupported.Count == 0 && program.Ambiguities.Count == 0;
            // "keep the face" / "keep everything else" on its own: understood, and honoured by changing nothing (the stages below still run, so the result is complete)
            bool keepAsIs = compiled.Patch.IsEmpty && !creates && !compiled.CreateRequested && onlyConstraints && req.Current != null;
            if (keepAsIs) res.Warnings.Add("Nothing to change: the character was kept as it is.");
            if (compiled.Patch.IsEmpty && !creates && !compiled.CreateRequested && !keepAsIs)
            {
                res.Errors.Add(compiled.Unsupported.Count > 0 ? "The request needs something this build cannot do yet (see 'Not possible yet')." : "The request did not change anything.");
                res.NeedsClarification = compiled.Unsupported.Count == 0;
                res.Specification = req.Current?.Spec;
                res.Draft = req.Current;
                return false;
            }

            // ---- 5. apply
            res.Stages.Add("apply patch");
            PatchResult applied = PatchApplier.Apply(baseDraft, compiled.Patch, catalogs);   // an empty patch changes nothing
            foreach (string s in applied.Skipped) res.Warnings.Add("Skipped: " + s);
            foreach (string s in applied.Blocked) res.Warnings.Add("Blocked: " + s);
            foreach (string s in applied.Clamped) res.Warnings.Add("Limited: " + s);
            AuthoringDraft draft = applied.Draft;
            CharacterSpecification spec = draft.Spec;
            spec.Authoring.Prompt = string.IsNullOrEmpty(spec.Authoring.Prompt) ? req.Text : spec.Authoring.Prompt + " | " + req.Text;
            spec.Authoring.Generator = interpreter.Name;
            spec.Authoring.Notes = "";

            // ---- 6. make it coherent
            res.Stages.Add("normalise appearance");
            NormalizationReport norm = AppearanceNormalizer.Normalize(spec, catalogs, rules);
            foreach (string c in norm.Changes) res.Warnings.Add("Adjusted for coherence: " + c);
            foreach (string r in norm.Rejections) res.Errors.Add("Incompatible appearance: " + r);
            spec.Appearance = norm.Result.Appearance;
            if (draft.Roles.ContainsKey(PlayerArchetype.Guardian.ToString()) && !spec.Appearance.Choices.ContainsKey("kit.gloves"))
            {
                spec.Appearance.Choices["kit.gloves"] = "gk_standard";
                res.Warnings.Add("Assumed: a goalkeeper wears goalkeeper gloves.");
            }
            string canonicalStyle = catalogs.Styles.Canonical(spec.Appearance.StyleId);
            if (canonicalStyle != null) spec.Appearance.StyleId = canonicalStyle;
            res.Draft = draft;
            res.Specification = spec;

            // ---- 7. validate
            res.Stages.Add("validate specification");
            CreatorValidationResult sv = CharacterSpecificationValidator.Validate(spec, catalogs);
            foreach (CreatorIssue i in sv.Issues)
            {
                if (i.Severity == CreatorSeverity.Error) res.Errors.Add(i.ToString());
                else if (i.Code != CreatorIssueCode.BehaviorNotExecutableYet) res.Warnings.Add(i.ToString());
            }
            if (sv.Has(CreatorIssueCode.BehaviorNotExecutableYet)) res.Warnings.Add("Signature behaviours are defined, but gameplay cannot execute them yet.");
            if (res.Errors.Count > 0) return false;

            // ---- 8. football DNA and behaviours
            res.Stages.Add("football DNA and behaviours");
            foreach (AttributeHint h in draft.ToAttributeHints()) res.AttributeHints.Add(h);
            foreach (ProfileHint h in draft.ToProfileHints()) res.ProfileHints.Add(h);
            PlayerAttributes attributes = AttributeHintApplier.Suggest(PlayerAttributes.CreateDefault(), res.AttributeHints);
            PlayerPlayingProfile profile = BuildProfile(draft);
            CharacterSpecification outSpec = spec.Clone();
            if (req.EnrichDna && (profile.Roles.Count > 0 || draft.ProfileLevels.Count > 0 || draft.Attributes.Count > 0))
            {
                var manual = new ManualPreferences();
                foreach (KeyValuePair<string, float> kv in spec.Dna.Params.Values)
                {
                    catalogs.Parameters.TryGet(kv.Key, out ParameterDefinition pd);
                    manual.Params[kv.Key] = pd.ToLevel(kv.Value);
                }
                foreach (BehaviorEntry b in spec.Dna.Behaviors) manual.Behaviors.Add(b.Clone());
                ComposerResult composed = FootballDnaComposer.Compose(new ComposerInput { Profile = profile, Manual = manual, VariationSeed = spec.BehaviorSeed }, catalogs, attributes);
                outSpec.Dna = composed.Dna;
                res.Warnings.AddRange(composed.Warnings);
                CreatorValidationResult dv = FootballDNAValidator.Validate(outSpec.Dna, catalogs);
                foreach (CreatorIssue i in dv.Issues) if (i.Severity == CreatorSeverity.Error) res.Errors.Add(i.ToString());
                if (res.Errors.Count > 0) return false;
            }
            res.Specification = outSpec;
            res.Goalkeeper = GoalkeeperProfileSuggester.Suggest(draft);
            foreach (BehaviorPreviewEntry e in PreviewBehaviors(outSpec, attributes)) res.BehaviorPreview.Add(e);

            // ---- 9. assembly plan, movement, animation, preview
            res.Stages.Add("assembly plan, movement and animation");
            var gp = new CreatorGenerationPlan { Assembly = CharacterAssemblyPlanner.Plan(outSpec, catalogs, materials) };
            res.Warnings.AddRange(gp.Assembly.Warnings);
            BodyType bodyType = BodyTypeOf(outSpec);
            gp.Signature = MovementSignatureResolver.Resolve(bodyType, outSpec, attributes, catalogs);
            gp.Personality = MovementPersonalityResolver.Resolve(outSpec, gp.Signature, catalogs);
            var rc = new CatalogAppearanceResolver(catalogs).Resolve(outSpec);
            if (animationResolver.TryResolve(rc, gp.Signature, out AnimationSetReference set)) { gp.AnimationLibrary = set.LibraryId; gp.AnimationTags.AddRange(set.Tags); }
            foreach (KeyValuePair<string, AnimationSelectionContext> sample in SampleMoments(rc.RigId))
            {
                AnimationPlan ap = animationResolver.Plan(sample.Value, gp.Signature, gp.Personality);
                gp.SampleAnimations.Add(sample.Key + ": " + (ap.AnimationId ?? "none") + (ap.NoClipYet ? " (no clip yet)" : ""));
            }
            res.GenerationPlan = gp;
            if (req.WantPreviewPng || req.WantGlb)
            {
                res.Stages.Add("preview");
                res.Preview = generator.Preview(gp.Assembly, req.WantPreviewPng, req.WantGlb);
            }

            // ---- 10. exports
            res.Stages.Add("export");
            var runtime = new RuntimeCharacterRecord { Json = RuntimeCharacterJson.ToJson(outSpec, index) };
            runtime.Bytes = Encoding.UTF8.GetByteCount(runtime.Json);
            foreach (KeyValuePair<string, string> kv in outSpec.Appearance.Choices) { string id = index.PartId(kv.Key, kv.Value); if (id != null) runtime.ContentIds.Add(id); }
            foreach (BehaviorEntry b in outSpec.Dna.Behaviors) { string id = index.BehaviorId(b.Id); if (id != null) runtime.ContentIds.Add(id); }
            string styleContent = index.StyleId(outSpec.Appearance.StyleId);
            if (styleContent != null) runtime.ContentIds.Add(styleContent);
            res.RuntimeData = runtime;
            var authoring = new AuthoringCharacterRecord { Json = CharacterSpecificationJson.ToJson(outSpec, true, true), InterpretationText = res.Interpretation != null ? res.Interpretation.ToText() : "" };
            authoring.Prompts.Add(req.Text);
            res.AuthoringData = authoring;
            return true;
        }

        private void Finish(CreatorResult res, bool ok, CreatorRequest req)
        {
            // never success when anything is invalid, contradictory, or still needs an answer
            res.Success = ok && res.Errors.Count == 0 && res.Specification != null && !res.NeedsClarificationBlocking();
            res.DebugReport = CreatorDebugReport.Build(req, res);
        }

        // ------------------------------------------------------------------ helpers

        private static BodyType BodyTypeOf(CharacterSpecification spec)
        {
            if (spec.Appearance.Choices.TryGetValue("body.preset", out string preset))
            {
                switch (preset)
                {
                    case "light": return BodyType.Light;
                    case "strong": return BodyType.Strong;
                    case "tall": return BodyType.Tall;
                    case "compact": return BodyType.Compact;
                }
            }
            return BodyType.Athletic;
        }

        private static PlayerPlayingProfile BuildProfile(AuthoringDraft draft)
        {
            var profile = new PlayerPlayingProfile { PlayerId = "draft", PrimaryZone = PitchZone.Midfield };
            var roles = new List<KeyValuePair<PlayerArchetype, float>>();
            foreach (KeyValuePair<string, float> kv in draft.Roles)
                if (Enum.TryParse(kv.Key, out PlayerArchetype a)) roles.Add(new KeyValuePair<PlayerArchetype, float>(a, kv.Value));
            roles.Sort((x, y) => { int c = y.Value.CompareTo(x.Value); return c != 0 ? c : ((int)x.Key).CompareTo((int)y.Key); });
            for (int i = 0; i < roles.Count && i < PlayerPlayingProfile.MaxArchetypes; i++) profile.AddRole(roles[i].Key, (int)Math.Round(roles[i].Value * 100f));
            if (Enum.TryParse(draft.PrimaryZone, out PitchZone zone)) profile.PrimaryZone = zone;
            if (draft.ProfileLevels.TryGetValue("Risk", out float risk)) profile.RiskPreference = (int)Math.Round(risk * 100f);
            if (draft.ProfileLevels.TryGetValue("Creativity", out float cr)) profile.Creativity = (int)Math.Round(cr * 100f);
            if (draft.ProfileLevels.TryGetValue("Aggression", out float ag)) profile.Aggression = (int)Math.Round(ag * 100f);
            return profile;
        }

        private static IEnumerable<KeyValuePair<string, AnimationSelectionContext>> SampleMoments(string rigId)
        {
            yield return new KeyValuePair<string, AnimationSelectionContext>("standing", new AnimationSelectionContext { Speed = 0f, RigId = rigId });
            yield return new KeyValuePair<string, AnimationSelectionContext>("jogging", new AnimationSelectionContext { Speed = 2.5f, RigId = rigId });
            yield return new KeyValuePair<string, AnimationSelectionContext>("sprinting", new AnimationSelectionContext { Speed = 7.5f, RigId = rigId });
            yield return new KeyValuePair<string, AnimationSelectionContext>("shooting", new AnimationSelectionContext { Speed = 3f, Action = FootballActionKind.Shot, RigId = rigId });
            yield return new KeyValuePair<string, AnimationSelectionContext>("feinting", new AnimationSelectionContext { Speed = 2f, Action = FootballActionKind.Dribble, Style = MovementStyle.Feint, RigId = rigId });
        }

        private List<BehaviorPreviewEntry> PreviewBehaviors(CharacterSpecification spec, PlayerAttributes attributes)
        {
            var situations = new[]
            {
                new KeyValuePair<string, BehaviorContext>("1v1 facing a defender in open space", BehaviorContext.HasBall | BehaviorContext.FacingDefender | BehaviorContext.OpenSpaceAhead),
                new KeyValuePair<string, BehaviorContext>("carrying the ball out wide", BehaviorContext.HasBall | BehaviorContext.WideArea | BehaviorContext.FacingDefender),
                new KeyValuePair<string, BehaviorContext>("receiving near the box", BehaviorContext.ReceivingBall | BehaviorContext.NearBox | BehaviorContext.ShootingRange | BehaviorContext.TeammateHasBall),
                new KeyValuePair<string, BehaviorContext>("teammate on the ball, space behind the line", BehaviorContext.TeammateHasBall | BehaviorContext.BehindDefenderLine | BehaviorContext.NearBox),
                new KeyValuePair<string, BehaviorContext>("defending against the ball carrier", BehaviorContext.OpponentHasBall)
            };
            var list = new List<BehaviorPreviewEntry>();
            foreach (KeyValuePair<string, BehaviorContext> s in situations)
            {
                var entry = new BehaviorPreviewEntry { Situation = s.Key };
                BehaviorDecision d = BehaviorDecisionEngine.Decide(spec.Dna, attributes, new ContextAnalysis { Flags = s.Value }, new BehaviorMemory(), catalogs);
                for (int i = 0; i < d.Candidates.Count && i < 3; i++)
                    entry.Top.Add(d.Candidates[i].BehaviorId + " " + d.Candidates[i].Utility.ToString("0.00", CultureInfo.InvariantCulture));
                list.Add(entry);
            }
            return list;
        }
    }

    internal static class CreatorResultExtensions
    {
        /// <summary>True when the result is waiting for an answer (an unresolved conflict) even if nothing is technically invalid.</summary>
        internal static bool NeedsClarificationBlocking(this CreatorResult r)
        {
            foreach (SemanticConflict c in r.Conflicts) if (c.Blocks) return true;
            return false;
        }
    }

    /// <summary>A readable trace of one pipeline run, for logs, tests and a future debug window. Deterministic: it prints no clock and no random value.</summary>
    public static class CreatorDebugReport
    {
        public static string Build(CreatorRequest req, CreatorResult res)
        {
            var sb = new StringBuilder();
            sb.AppendLine("== REQUEST ==");
            sb.AppendLine(req.Text + "   [seed " + req.Seed + ", " + (req.Current == null ? "new character" : "editing " + req.Current.Spec?.CharacterId) + "]");
            sb.AppendLine("== RESULT == " + (res.Success ? "SUCCESS" : "NOT SUCCESSFUL") + (res.NeedsClarification ? " (needs an answer)" : ""));
            sb.AppendLine("stages: " + string.Join(" > ", res.Stages));
            if (res.Program != null)
            {
                sb.AppendLine("== SEMANTIC MODEL (" + res.Program.Language + ") ==");
                sb.Append(SemanticProgramPrinter.Describe(res.Program));
            }
            if (res.Interpretation != null)
            {
                sb.AppendLine("== INTERPRETATION ==");
                sb.Append(res.Interpretation.ToText());
            }
            if (res.Patch != null && !res.Patch.IsEmpty)
            {
                sb.AppendLine("== PATCH ==");
                foreach (PatchOperation o in res.Patch.Operations) sb.AppendLine("  " + o + (o.Collateral ? "  (side effect)" : ""));
            }
            foreach (string w in res.Warnings) sb.AppendLine("warning: " + w);
            foreach (string e in res.Errors) sb.AppendLine("ERROR: " + e);
            foreach (string c in res.Clarifications) sb.AppendLine("question: " + c);
            if (res.Specification != null)
            {
                sb.AppendLine("== SPECIFICATION ==");
                sb.AppendLine(CharacterSpecificationJson.ToJson(res.Specification, false, false));
            }
            foreach (AttributeHint h in res.AttributeHints) sb.AppendLine("attribute wish " + h.Attribute + " " + h.Level.ToString("0.00", CultureInfo.InvariantCulture));
            foreach (ProfileHint h in res.ProfileHints) sb.AppendLine("profile wish " + h.Kind + (h.Kind == ProfileHintKind.Role ? " " + h.Role : h.Kind == ProfileHintKind.PrimaryZone ? " " + h.Zone : "") + " " + h.Level.ToString("0.00", CultureInfo.InvariantCulture));
            if (res.BehaviorPreview.Count > 0)
            {
                sb.AppendLine("== BEHAVIOUR CANDIDATES ==");
                foreach (BehaviorPreviewEntry b in res.BehaviorPreview) sb.AppendLine("  " + b.Situation + ": " + (b.Top.Count == 0 ? "(plays normally)" : string.Join(", ", b.Top)));
            }
            if (res.GenerationPlan != null)
            {
                CreatorGenerationPlan g = res.GenerationPlan;
                BodyProportions p = g.Assembly.Proportions;
                sb.AppendLine("== GENERATION PLAN ==");
                sb.AppendLine("height " + p.HeightMeters.ToString("0.00", CultureInfo.InvariantCulture) + " m, " + p.HeadsTall.ToString("0.0", CultureInfo.InvariantCulture) + " heads, shoulders "
                              + p.ShoulderWidth.ToString("0.00", CultureInfo.InvariantCulture) + " m, legs " + p.LegLength.ToString("0.00", CultureInfo.InvariantCulture) + " m; " + g.Assembly.Parts.Count + " procedural parts; style " + g.Assembly.StyleId);
                sb.AppendLine("movement signature: stride " + g.Signature.StrideLength.ToString("0.00", CultureInfo.InvariantCulture) + ", cadence " + g.Signature.Cadence.ToString("0.00", CultureInfo.InvariantCulture)
                              + ", lean " + g.Signature.Lean.ToString("0.00", CultureInfo.InvariantCulture));
                foreach (string a in g.SampleAnimations) sb.AppendLine("  animation " + a);
            }
            if (res.RuntimeData != null) sb.AppendLine("runtime record: " + res.RuntimeData.Bytes + " bytes, " + res.RuntimeData.ContentIds.Count + " content references");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Validates the CONTENT of the Creator Engine itself (catalogs, vocabularies, links between them), not a character. If a new part, concept,
    /// behaviour, style, material or animation breaks a rule, this is where it is caught, before it can reach a player.
    /// </summary>
    public static class CreatorContentValidator
    {
        public static CreatorValidationResult Validate(CreatorCatalogs catalogs, ConceptCatalog concepts, MaterialCatalog materials, AnimationCatalog animations, IEnumerable<LanguagePack> packs)
        {
            var r = new CreatorValidationResult();
            r.Merge(concepts.Validate(catalogs));

            // every id of every kind is a valid, unique content id
            ContentIndex index = ContentIndex.Build(catalogs, materials, animations);
            foreach (string p in index.Problems) r.Error(CreatorIssueCode.ContentIdInvalid, "content index", p);

            // every behaviour's animation tags and every style's keys exist
            SortedSet<string> offered = animations.AllTags();
            foreach (SignatureBehaviorDefinition b in catalogs.Behaviors.All)
            {
                foreach (string tag in b.AnimationTags)
                    if (!offered.Contains(tag)) r.Error(CreatorIssueCode.ContentUnresolved, "behaviour " + b.Id, "Asks for the animation tag '" + tag + "', which no animation offers.");
                foreach (WeightedParameter d in b.Drivers)
                    if (!catalogs.Parameters.TryGet(d.ParameterId, out ParameterDefinition pd) || pd.Domain != ParameterDomain.FootballDna) r.Error(CreatorIssueCode.ParameterUnknown, "behaviour " + b.Id, "Driver '" + d.ParameterId + "' is not a football DNA parameter.");
            }
            foreach (StylePreset s in catalogs.Styles.All)
            {
                foreach (KeyValuePair<string, float> kv in s.StartLevels)
                    if (!catalogs.Parameters.TryGet(kv.Key, out ParameterDefinition pd) || pd.Domain != ParameterDomain.Appearance) r.Error(CreatorIssueCode.ParameterUnknown, "style " + s.Id, "Start level for '" + kv.Key + "', which is not an appearance parameter.");
                foreach (StyleAxisEffect e in s.CartoonAxis)
                    if (!catalogs.Parameters.Contains(e.ParameterId)) r.Error(CreatorIssueCode.ParameterUnknown, "style " + s.Id, "Axis effect on unknown '" + e.ParameterId + "'.");
                if (s.HeadHeights < 4f || s.HeadHeights > 9f) r.Error(CreatorIssueCode.AppearanceIncoherent, "style " + s.Id, "HeadHeights " + s.HeadHeights + " is not a sensible body proportion.");
            }
            foreach (MaterialDefinition m in materials.All)
                if (m.ColorSource.StartsWith("slot:", StringComparison.Ordinal) && !catalogs.Appearance.HasColorSlot(m.ColorSource.Substring(5)))
                    r.Error(CreatorIssueCode.ColorSlotUnknown, m.Id, "Colour slot '" + m.ColorSource.Substring(5) + "' does not exist.");
            foreach (AnimationProfile a in animations.All)
                if (a.MaxSpeed < a.MinSpeed) r.Error(CreatorIssueCode.ContentUnresolved, "animation " + a.Id, "MaxSpeed is below MinSpeed.");

            // vocabularies: every word points at a concept that exists, and every choice value at a real part
            foreach (LanguagePack pack in packs)
            {
                foreach (ConceptWord w in pack.Words) if (!concepts.Contains(w.Concept)) r.Error(CreatorIssueCode.SemanticTargetUnknown, pack.Id + " word '" + w.Phrase.Text + "'", "Unknown concept '" + w.Concept + "'.");
                foreach (NounWord n in pack.Nouns)
                    foreach (string c in new[] { n.Concept, n.ScalarConcept, n.GroupConcept })
                        if (!string.IsNullOrEmpty(c) && !concepts.Contains(c)) r.Error(CreatorIssueCode.SemanticTargetUnknown, pack.Id + " noun '" + n.Phrase.Text + "'", "Unknown concept '" + c + "'.");
                foreach (ValueWord v in pack.Values)
                {
                    if (!concepts.TryGet(v.Concept, out ConceptDefinition def)) { r.Error(CreatorIssueCode.SemanticTargetUnknown, pack.Id + " value '" + v.Phrase.Text + "'", "Unknown concept '" + v.Concept + "'."); continue; }
                    if (def.Kind == ConceptKind.Choice && !(v.Operation == SemanticIntent.Remove && v.Value.Length == 0) && !catalogs.Appearance.TryGetPart(def.Slot, v.Value, out PartDefinition _)) r.Error(CreatorIssueCode.ChoicePartUnknown, pack.Id + " value '" + v.Phrase.Text + "'", "'" + v.Value + "' is not a part of '" + def.Slot + "'.");
                }
                foreach (ColorWord c in pack.Colors) if (!CharacterSpecificationValidator.IsHexColor(c.Hex)) r.Error(CreatorIssueCode.ColorInvalid, pack.Id + " colour '" + c.Phrase.Text + "'", "Invalid colour '" + c.Hex + "'.");
            }
            return r;
        }
    }
}

namespace FS27.Core
{
    /// <summary>Turns goalkeeper wishes into a suggested <see cref="GoalkeeperProfile"/>. A suggestion only: the profile belongs to the player.</summary>
    public static class GoalkeeperProfileSuggester
    {
        /// <summary>The profile the wishes describe, or null when the draft is not a goalkeeper.</summary>
        public static GoalkeeperProfile Suggest(AuthoringDraft draft)
        {
            if (draft == null || !draft.Roles.ContainsKey(PlayerArchetype.Guardian.ToString())) return null;
            var p = new GoalkeeperProfile { PlayerId = "draft" };
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
                if (draft.Goalkeeper.TryGetValue(c.ToString(), out float level))
                    p.SetValue(c, (int)Math.Round(AttributeHintApplier.LowestSuggested + level * (AttributeHintApplier.HighestSuggested - AttributeHintApplier.LowestSuggested)));

            float L(GoalkeeperCapability c) { return draft.Goalkeeper.TryGetValue(c.ToString(), out float v) ? v : 0.5f; }
            var scores = new List<KeyValuePair<GoalkeeperStyle, float>>
            {
                new KeyValuePair<GoalkeeperStyle, float>(GoalkeeperStyle.ShotStopper, (L(GoalkeeperCapability.Reflexes) + L(GoalkeeperCapability.Diving) + L(GoalkeeperCapability.Handling)) / 3f),
                new KeyValuePair<GoalkeeperStyle, float>(GoalkeeperStyle.Distributor, (L(GoalkeeperCapability.Distribution) + L(GoalkeeperCapability.Kicking)) / 2f),
                new KeyValuePair<GoalkeeperStyle, float>(GoalkeeperStyle.Sweeper, (L(GoalkeeperCapability.Command) + L(GoalkeeperCapability.Positioning) + L(GoalkeeperCapability.Recovery)) / 3f),
                new KeyValuePair<GoalkeeperStyle, float>(GoalkeeperStyle.Commander, L(GoalkeeperCapability.Command))
            };
            scores.Sort((a, b) => { int c = b.Value.CompareTo(a.Value); return c != 0 ? c : ((int)a.Key).CompareTo((int)b.Key); });
            p.PrimaryStyle = scores[0].Key;
            for (int i = 1; i < scores.Count; i++) if (scores[i].Value >= 0.65f) p.SecondaryStyles.Add(scores[i].Key);
            return p;
        }
    }
}
