using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public sealed class LlmAttempt
    {
        public int Number;
        /// <summary>The model's text (kept truncated for the report).</summary>
        public string Raw = "";
        public string TransportProblem = "";
        public readonly List<string> Errors = new List<string>();
        public bool Accepted;
        /// <summary>Which stage of the validation chain stopped it: transport, schema, semantic, domain or apply (empty when accepted).</summary>
        public string FailedAt = "";
    }

    public sealed class LlmInterpretReport
    {
        public readonly List<LlmAttempt> Attempts = new List<LlmAttempt>();
        public bool UsedFallback;
        public bool Succeeded;
        public string FinalProblem = "";
    }

    /// <summary>
    /// A language model as an interpreter. It builds the request, sends it through a transport the HOST supplies, and then treats the answer as
    /// untrusted data: it must pass a four-stage chain (schema, semantic, domain, a dry-run application) before the engine will use it. A
    /// failed stage triggers a bounded REPAIR loop (the errors are shown to the model, which answers again). When the answer cannot be made
    /// valid, the interpreter either hands over to a fallback (the offline parser) or returns an empty program that asks the person
    /// to rephrase. It never executes anything and holds no key.
    /// </summary>
    public sealed class LlmSemanticInterpreter : ISemanticInterpreter
    {
        public const int MaxRawKept = 600;

        private readonly ILlmWireFormat wire;
        private readonly ILlmTransport transport;
        private readonly ConceptCatalog concepts;
        private readonly CreatorCatalogs catalogs;
        private readonly SemanticCompiler compiler;
        private readonly string model;
        private readonly ISemanticInterpreter fallback;
        private readonly string systemPrompt;
        private readonly string schema;

        /// <summary>How many times the model is asked to fix an invalid answer.</summary>
        public int MaxRepairs = 2;
        public int MaxTokens = 2048;
        /// <summary>The character being edited (for modification prompts and for the dry-run). Null = a new character.</summary>
        public AuthoringDraft Context;

        public LlmInterpretReport LastReport { get; private set; } = new LlmInterpretReport();
        public string Name => "FS27.LlmSemanticInterpreter(" + wire.Name + ")";

        public LlmSemanticInterpreter(ILlmWireFormat wire, ILlmTransport transport, ConceptCatalog concepts, CreatorCatalogs catalogs, string model, ISemanticInterpreter fallback = null, ISemanticInterpreter examples = null)
        {
            this.wire = wire;
            this.transport = transport;
            this.concepts = concepts;
            this.catalogs = catalogs;
            this.model = model;
            this.fallback = fallback;
            compiler = new SemanticCompiler(catalogs, concepts);
            systemPrompt = PromptTemplates.CharacterCreationSystem(concepts, examples ?? fallback);
            schema = SemanticProgramJson.Schema(concepts);
        }

        public SemanticProgram Interpret(string text)
        {
            var report = new LlmInterpretReport();
            LastReport = report;
            var request = new LlmRequest { Model = model, System = systemPrompt, JsonSchema = schema, SchemaName = "fs27_semantic_program", MaxTokens = MaxTokens };
            request.Messages.Add(new LlmMessage("user", Context != null ? PromptTemplates.CharacterModificationUser(text, Context, catalogs) : PromptTemplates.CharacterCreationUser(text)));

            for (int attemptNo = 1; attemptNo <= 1 + Math.Max(0, MaxRepairs); attemptNo++)
            {
                var attempt = new LlmAttempt { Number = attemptNo };
                report.Attempts.Add(attempt);

                WireResponse raw;
                try { raw = transport.Send(wire.Build(request)); }
                catch (Exception e) { raw = new WireResponse { TransportError = "The transport failed: " + e.Message }; }
                LlmParsedResponse parsed = wire.Parse(raw);
                attempt.Raw = Truncate(parsed.Text);
                if (parsed.Unusable)
                {
                    attempt.FailedAt = "transport";
                    attempt.TransportProblem = parsed.Problem;
                    report.FinalProblem = parsed.Problem;
                    break;   // asking again does not fix a refusal, an outage or a missing transport
                }

                SemanticProgram program = Check(parsed.Text, text, attempt);
                if (program != null)
                {
                    attempt.Accepted = true;
                    report.Succeeded = true;
                    program.Text = text;
                    program.ParserName = Name;
                    return program;
                }

                report.FinalProblem = attempt.FailedAt + ": " + string.Join(" | ", attempt.Errors);
                // repair: show the model its own answer and what is wrong with it
                request.Messages.Add(new LlmMessage("assistant", parsed.Text));
                request.Messages.Add(new LlmMessage("user", "Your answer was not valid (" + attempt.FailedAt + "):\n- " + string.Join("\n- ", Limit(attempt.Errors, 10)) + "\nReturn ONLY the corrected JSON."));
            }

            if (fallback != null)
            {
                report.UsedFallback = true;
                SemanticProgram p = fallback.Interpret(text);
                p.Ambiguities.Add("The language model could not be used (" + report.FinalProblem + "); the offline interpreter was used instead.");
                return p;
            }
            var empty = new SemanticProgram { Text = text, ParserName = Name };
            empty.Ambiguities.Add("The language model's answer could not be used (" + report.FinalProblem + "). Please try again or rephrase.");
            return empty;
        }

        /// <summary>The validation chain: schema, semantic, domain, apply. Null (with the reasons on the attempt) when any stage fails.</summary>
        private SemanticProgram Check(string rawText, string request, LlmAttempt attempt)
        {
            // 1. schema: shape, names, sizes
            var shape = new CreatorValidationResult();
            if (!SemanticProgramJson.TryFromJson(rawText, out SemanticProgram program, shape)) { Fail(attempt, "schema", shape); return null; }

            // 2. semantic: ids, targets, relations, ranges
            CreatorValidationResult sem = SemanticProgramValidator.Validate(program, concepts);
            if (!sem.IsValid) { Fail(attempt, "semantic", sem); return null; }

            // 3. domain: values refer to things that exist
            var dom = new CreatorValidationResult();
            DomainCheck(program, dom);
            if (!dom.IsValid) { Fail(attempt, "domain", dom); return null; }

            // 4. apply: a dry run must compile and apply cleanly
            AuthoringDraft draft = Context != null ? Context.Clone() : new AuthoringDraft(catalogs.NewSpecification("dry-run"));
            CompileResult compiled = compiler.Compile(program, draft, 1);
            PatchResult applied = PatchApplier.Apply(draft, compiled.Patch, catalogs);
            if (applied.Skipped.Count > 0)
            {
                attempt.FailedAt = "apply";
                attempt.Errors.AddRange(applied.Skipped);
                return null;
            }
            return program;
        }

        private void DomainCheck(SemanticProgram p, CreatorValidationResult r)
        {
            foreach (SemanticCommand c in p.Commands)
            {
                string at = "command " + c.Id;
                if (string.IsNullOrEmpty(c.Target)) continue;
                concepts.TryGet(c.Target, out ConceptDefinition def);
                if (!string.IsNullOrEmpty(c.Value))
                {
                    if (def.Kind == ConceptKind.Choice && !catalogs.Appearance.TryGetPart(def.Slot, c.Value, out PartDefinition _)) r.Error(CreatorIssueCode.ChoicePartUnknown, at, "'" + c.Value + "' is not a part of '" + def.Slot + "'.");
                    else if (def.Kind == ConceptKind.Color && !CharacterSpecificationValidator.IsHexColor(c.Value)) r.Error(CreatorIssueCode.ColorInvalid, at, "'" + c.Value + "' is not a #RRGGBB colour.");
                }
                if (def.Kind == ConceptKind.Color && (c.Operation == SemanticIntent.Set || c.Operation == SemanticIntent.Replace) && string.IsNullOrEmpty(c.Value)) r.Error(CreatorIssueCode.ColorInvalid, at, "A colour command needs a #RRGGBB value.");
                if (def.Kind == ConceptKind.Scalar && (c.Operation == SemanticIntent.Set || c.Operation == SemanticIntent.Increase || c.Operation == SemanticIntent.Decrease) && !string.IsNullOrEmpty(c.Value))
                    r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "A numeric concept takes no value text (use magnitude).");
            }
        }

        private static void Fail(LlmAttempt a, string stage, CreatorValidationResult r)
        {
            a.FailedAt = stage;
            foreach (CreatorIssue i in r.Issues) if (i.Severity == CreatorSeverity.Error) a.Errors.Add(i.Subject + ": " + i.Message);
        }

        private static string Truncate(string s)
        {
            return s != null && s.Length > MaxRawKept ? s.Substring(0, MaxRawKept) + "..." : s ?? "";
        }

        private static List<string> Limit(List<string> l, int n)
        {
            return l.Count <= n ? l : l.GetRange(0, n);
        }
    }

    /// <summary>
    /// Turns a finding into observations with a language model. Its output is untrusted: the research pipeline validates every observation
    /// against the catalogs and drops what does not pass. Offline, with no transport, it reports a warning and returns nothing.
    /// </summary>
    public sealed class LlmAnalysisProvider : IFootballAnalysisProvider
    {
        private readonly ILlmWireFormat wire;
        private readonly ILlmTransport transport;
        private readonly CreatorCatalogs catalogs;
        private readonly string model;
        private readonly string system;
        private readonly string schema;

        public string Name => "FS27.LlmAnalysisProvider(" + wire.Name + ")";

        public LlmAnalysisProvider(ILlmWireFormat wire, ILlmTransport transport, CreatorCatalogs catalogs, string model)
        {
            this.wire = wire;
            this.transport = transport;
            this.catalogs = catalogs;
            this.model = model;
            system = PromptTemplates.FootballDnaExtractionSystem(catalogs);
            schema = PromptTemplates.ObservationSchema(catalogs);
        }

        public AnalysisResult Analyze(ResearchQuery query, ResearchFinding finding)
        {
            var result = new AnalysisResult();
            var request = new LlmRequest { Model = model, System = system, JsonSchema = schema, SchemaName = "fs27_observations", MaxTokens = 4096 };
            request.Messages.Add(new LlmMessage("user", PromptTemplates.ResearchAnalysisUser(query, finding)));
            LlmParsedResponse parsed = wire.Parse(transport.Send(wire.Build(request)));
            if (parsed.Unusable) { result.Warnings.Add("No usable answer: " + parsed.Problem); return result; }
            var v = new CreatorValidationResult();
            if (!ObservationJson.TryFromJson(parsed.Text, out List<FootballObservation> list, v))
            {
                result.Warnings.Add("The answer was not valid observation data: " + v);
                return result;
            }
            foreach (FootballObservation o in list)
            {
                // a model is never a more trusted source than it says: its own inference is capped to the Model source type
                if (o.SourceType != ObservationSourceType.Model && finding.SourceType == ObservationSourceType.Model) o.SourceType = ObservationSourceType.Model;
                result.Observations.Add(o);
            }
            return result;
        }
    }
}
