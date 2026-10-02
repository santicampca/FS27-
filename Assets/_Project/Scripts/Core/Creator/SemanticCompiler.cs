using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    public sealed class CompileResult
    {
        public SemanticProgram Program;
        public CharacterSpecificationPatch Patch = new CharacterSpecificationPatch();
        public readonly List<SemanticConflict> Conflicts = new List<SemanticConflict>();
        public readonly List<UnsupportedCapability> Unsupported = new List<UnsupportedCapability>();
        public readonly List<string> Clarifications = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Assumptions = new List<string>();
        public readonly List<string> Understood = new List<string>();
        public bool UndoRequested;
        public bool CreateRequested;
        public EntityReference BaseReference;
        public InterpretationReport Report;

        /// <summary>True when an unresolved conflict stopped part of the request. The rest of the patch is still safe to apply.</summary>
        public bool HasBlockingConflict
        {
            get
            {
                foreach (SemanticConflict c in Conflicts) if (c.Blocks) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Turns meaning (a <see cref="SemanticProgram"/>) into a precise <see cref="CharacterSpecificationPatch"/>. Deterministic and independent of
    /// how the program was made. This is where "fuerte" becomes gameplay strength or visual build depending on context, where negation
    /// becomes a limit or a reversal, where side effects are kept apart from what was asked, and where contradictions are found, never averaged.
    /// </summary>
    public sealed class SemanticCompiler
    {
        private sealed class Plan
        {
            public SemanticCommand Cmd;
            public ConceptDefinition Def;
            public List<PatchOperation> Ops = new List<PatchOperation>();
            public int Sign;
            public string Family = "";
            public bool Removes;
            public bool Adds;
            public string ValueKey = "";
            public bool Dropped;
        }

        private readonly CreatorCatalogs catalogs;
        private readonly ConceptCatalog concepts;
        private readonly MagnitudeTable table;

        /// <summary>The concept id of the visual style group (the one whose "change" swaps the style preset).</summary>
        public const string StyleConceptId = "style";

        public float ConfidenceThreshold = 0.5f;
        /// <summary>Default weight of a behaviour added without an amount.</summary>
        public float DefaultBehaviorWeight = 0.7f;

        public SemanticCompiler(CreatorCatalogs catalogs, ConceptCatalog concepts, MagnitudeTable table = null)
        {
            this.catalogs = catalogs;
            this.concepts = concepts;
            this.table = table ?? MagnitudeEngine.Default;
        }

        public CompileResult Compile(SemanticProgram program, AuthoringDraft current, uint seed = 0)
        {
            var res = new CompileResult { Program = program };
            CreatorValidationResult valid = SemanticProgramValidator.Validate(program, concepts);
            if (!valid.IsValid)
            {
                foreach (CreatorIssue i in valid.Issues) res.Warnings.Add("Invalid program: " + i);
                res.Clarifications.Add("The interpretation was not valid, nothing was changed.");
                return Finish(res, program, 0f);
            }

            // ---- scope and preservation
            bool allElse = false, onlyThese = false;
            var prot = new ProtectedSet();
            foreach (SemanticConstraint k in program.Constraints)
            {
                if (k.AllElse) allElse = true;
                if (k.OnlyThese) onlyThese = true;
                if (k.Intent == SemanticIntent.Preserve && !string.IsNullOrEmpty(k.Target) && concepts.TryGet(k.Target, out ConceptDefinition kd))
                    prot.AddConcept(kd, false);
            }
            bool suppressCollateral = allElse || onlyThese;
            if (allElse) res.Assumptions.Add("'Keep everything else': only what was asked changes; no side effects were added.");
            if (onlyThese) res.Assumptions.Add("'Only change...': only what was named changes; no side effects were added.");

            // ---- commands to plans
            var plans = new List<Plan>();
            float minConfidence = 1f;
            foreach (SemanticCommand cmd in program.Commands)
            {
                switch (cmd.Intent)
                {
                    case SemanticIntent.Undo: res.UndoRequested = true; res.Understood.Add("undo the last change"); continue;
                    case SemanticIntent.Create:
                        res.CreateRequested = true;
                        if (cmd.Reference != EntityReference.Current) res.BaseReference = cmd.Reference;
                        res.Understood.Add(cmd.Reference == EntityReference.Previous ? "start from the previous character" : "create a new character");
                        continue;
                }
                if (string.IsNullOrEmpty(cmd.Target) || !concepts.TryGet(cmd.Target, out ConceptDefinition def)) continue;
                if (cmd.Confidence < ConfidenceThreshold)
                {
                    res.Clarifications.Add("I am not sure about '" + cmd.Source + "' (" + Describe(cmd) + "). Could you say it differently?");
                    res.Warnings.Add("Skipped a low-confidence command: " + Describe(cmd));
                    continue;
                }
                minConfidence = Math.Min(minConfidence, cmd.Confidence);
                Plan plan = BuildPlan(cmd, def, current, suppressCollateral, prot, res, seed);
                if (plan != null) plans.Add(plan);
                res.Understood.Add(Describe(cmd));
            }

            DetectConflicts(plans, res);
            ApplyProtection(plans, prot, res);

            foreach (Plan p in plans)
            {
                if (p.Dropped) continue;
                res.Patch.Operations.AddRange(p.Ops);
            }
            foreach (string a in program.Ambiguities) res.Clarifications.Add(a);
            foreach (SemanticConflict c in res.Conflicts)
                if (c.Blocks) res.Clarifications.Add(string.Join(" ", c.Messages) + " Which one do you want?");

            float confidence = minConfidence;
            if (program.Commands.Count == 0 && program.Constraints.Count == 0) confidence = 0f;
            else confidence = Math.Max(0f, confidence - 0.1f * Math.Min(3, program.Unparsed.Count));
            return Finish(res, program, confidence);
        }

        private CompileResult Finish(CompileResult res, SemanticProgram program, float confidence)
        {
            var r = new InterpretationReport
            {
                Language = program.Language, Text = program.Text, Interpreter = program.ParserName, Confidence = confidence
            };
            r.Understood.AddRange(res.Understood);
            foreach (string u in program.Unparsed) r.NotUnderstood.Add(u);
            r.Assumptions.AddRange(res.Assumptions);
            foreach (PatchOperation o in res.Patch.Operations) r.Applied.Add(o.ToString() + (o.Collateral ? " (side effect)" : "") + (string.IsNullOrEmpty(o.Reason) ? "" : "  <- " + o.Reason));
            foreach (string w in res.Warnings) r.Blocked.Add(w);
            r.Conflicts.AddRange(res.Conflicts);
            r.Unsupported.AddRange(res.Unsupported);
            r.Clarifications.AddRange(res.Clarifications);
            res.Report = r;
            return res;
        }

        // ------------------------------------------------------------------ one command

        private static string Describe(SemanticCommand c)
        {
            string s = c.Operation + " " + c.Target;
            if (c.Operation == SemanticIntent.Set && c.Direction < 0) s += " (low)";
            if (c.Magnitude != MagnitudeLevel.Unspecified) s += " " + c.Magnitude;
            if (c.Negated) s += " (negated)";
            if (c.Domain != SemanticDomain.Unspecified) s += " [" + c.Domain + "]";
            if (c.Phase != SemanticPhase.Any) s += " @" + c.Phase;
            if (!string.IsNullOrEmpty(c.Value)) s += " = " + c.Value;
            return s;
        }

        private Plan BuildPlan(SemanticCommand cmd, ConceptDefinition def, AuthoringDraft cur, bool suppress, ProtectedSet prot, CompileResult res, uint seed)
        {
            var plan = new Plan { Cmd = cmd, Def = def };
            plan.Family = def.Id + "|" + cmd.Domain + "|" + cmd.Phase + "|" + cmd.Sense;
            SemanticIntent op = cmd.Operation;

            if (cmd.Intent == SemanticIntent.Preserve || op == SemanticIntent.Preserve)
            {
                prot.AddConcept(def, true);
                SemanticDomain pd = cmd.Domain != SemanticDomain.Unspecified ? cmd.Domain : def.DefaultDomain;
                ConceptSense ps = def.FindSense(pd, SemanticPhase.Any, cmd.Sense, out _) ?? FirstSense(def);
                if (ps != null)
                    foreach (ConceptEffect e in ps.Effects)
                        if (e.Primary && TryTarget(e, out PatchTarget t, out string k))
                            plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Preserve, Target = t, Key = k, CommandId = cmd.Id, Reason = "keep " + def.Id });
                return plan;
            }

            switch (def.Kind)
            {
                case ConceptKind.Choice: ChoiceOps(plan, cmd, def, cur, res, seed); break;
                case ConceptKind.Color: ColorOps(plan, cmd, def, res); break;
                case ConceptKind.Group: GroupOps(plan, cmd, def, cur, res, seed); break;
                default: ScalarOps(plan, cmd, def, cur, suppress, res); break;
            }
            return plan;
        }

        private static ConceptSense FirstSense(ConceptDefinition def)
        {
            return def.Senses.Count > 0 ? def.Senses[0] : null;
        }

        private ConceptSense ResolveSense(SemanticCommand cmd, ConceptDefinition def, CompileResult res)
        {
            SemanticDomain domain = cmd.Domain != SemanticDomain.Unspecified ? cmd.Domain : def.DefaultDomain;
            ConceptSense s = def.FindSense(domain, cmd.Phase, cmd.Sense, out _);
            if (s == null && cmd.Phase != SemanticPhase.Any)
            {
                s = def.FindSense(domain, SemanticPhase.Any, cmd.Sense, out _);
                if (s != null) res.Assumptions.Add("'" + def.Id + "' has no specific meaning during " + cmd.Phase + "; applied in general.");
            }
            if (s == null && !string.IsNullOrEmpty(cmd.Sense))
            {
                s = def.FindSense(domain, cmd.Phase, "", out _);
            }
            if (s == null && cmd.Domain == SemanticDomain.Unspecified)
            {
                s = FirstSense(def);
                if (s != null) res.Assumptions.Add("'" + def.Id + "': no context was given; used its " + s.Domain + " meaning.");
            }
            if (s == null)
            {
                res.Unsupported.Add(new UnsupportedCapability
                {
                    Text = def.Id + " in the " + domain + " sense", Reason = UnsupportedReason.NotUnderstood, CommandId = cmd.Id,
                    RequiredSystem = "A " + domain + " meaning for '" + def.Id + "'",
                    SuggestedFutureImplementation = "Add a " + domain + " sense to the concept '" + def.Id + "' in the concept catalog, if it makes sense."
                });
                res.Warnings.Add("'" + def.Id + "' has no " + domain + " meaning, so '" + cmd.Source + "' was not applied.");
            }
            return s;
        }

        // ---- scalar concepts (heights, attributes, tendencies, roles, behaviours)

        private void ScalarOps(Plan plan, SemanticCommand cmd, ConceptDefinition def, AuthoringDraft cur, bool suppress, CompileResult res)
        {
            SemanticIntent op = cmd.Operation;
            List<ConceptEffect> effects;
            if (op == SemanticIntent.Remove && def.RemoveEffects.Count > 0)
            {
                effects = def.RemoveEffects;
            }
            else
            {
                ConceptSense sense = ResolveSense(cmd, def, res);
                if (sense == null) { plan.Dropped = true; return; }
                effects = sense.Effects;
            }

            foreach (ConceptEffect e in effects)
            {
                if (e.Kind == EffectKind.Unsupported)
                {
                    res.Unsupported.Add(new UnsupportedCapability
                    {
                        Text = def.Id + ": " + cmd.Source, Reason = e.Reason, CommandId = cmd.Id,
                        RequiredSystem = e.Reason == UnsupportedReason.RequiresAsset ? "Character asset pipeline" : "A runtime system that is not implemented yet",
                        SuggestedFutureImplementation = e.Explanation
                    });
                    plan.Dropped = true;
                    return;
                }
                if (!e.Primary && suppress) continue;
                switch (e.Kind)
                {
                    case EffectKind.StyleAxis: StyleOps(plan, cmd, e, res); break;
                    case EffectKind.Behavior: BehaviorOps(plan, cmd, e, def); break;
                    case EffectKind.Choice: ChoiceEffectOps(plan, cmd, e); break;
                    case EffectKind.Profile when e.ProfileKind == ProfileHintKind.Role: RoleOps(plan, cmd, e, cur); break;
                    default: NumberOps(plan, cmd, e, cur, res); break;
                }
            }
        }

        private bool TryTarget(ConceptEffect e, out PatchTarget target, out string key)
        {
            key = e.Key;
            target = PatchTarget.AppearanceParam;
            switch (e.Kind)
            {
                case EffectKind.Param:
                    if (!catalogs.Parameters.TryGet(e.Key, out ParameterDefinition p)) return false;
                    target = p.Domain == ParameterDomain.Appearance ? PatchTarget.AppearanceParam : PatchTarget.DnaParam;
                    return true;
                case EffectKind.Attribute: target = PatchTarget.Attribute; key = e.Attribute.ToString(); return true;
                case EffectKind.Profile: target = PatchTarget.Profile; key = e.ProfileKind.ToString(); return true;
                case EffectKind.Behavior: target = PatchTarget.Behavior; return true;
                case EffectKind.Choice: target = PatchTarget.Choice; return true;
                case EffectKind.StyleAxis: target = PatchTarget.StyleAxis; return true;
            }
            return false;
        }

        private float NeutralLevel(PatchTarget t, string key)
        {
            if ((t == PatchTarget.AppearanceParam || t == PatchTarget.DnaParam) && catalogs.Parameters.TryGet(key, out ParameterDefinition p)) return p.ToLevel(p.Default);
            return 0.5f;
        }

        private void NumberOps(Plan plan, SemanticCommand cmd, ConceptEffect e, AuthoringDraft cur, CompileResult res)
        {
            if (!TryTarget(e, out PatchTarget target, out string key)) return;
            float w = e.Weight, aw = Math.Abs(w);
            int ws = w >= 0f ? 1 : -1;
            SemanticIntent op = cmd.Operation;
            string why = cmd.Source + " -> " + cmd.Target + (e.Primary ? "" : " (side effect)");
            float neutral = NeutralLevel(target, key);

            if (op == SemanticIntent.Increase || op == SemanticIntent.Decrease)
            {
                int dir = (op == SemanticIntent.Increase ? 1 : -1) * ws;
                if (cmd.Negated)
                {
                    // "no más rápido": do not go beyond where it is now
                    if (!e.Primary) return;
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = key, Level = CurrentLevel(cur, target, key), Limit = dir > 0 ? PatchLimit.AtMost : PatchLimit.AtLeast, CommandId = cmd.Id, Reason = why });
                    return;
                }
                float delta = MagnitudeEngine.Delta(cmd.Magnitude, table) * aw;
                if (cmd.Magnitude == MagnitudeLevel.Maximum && e.Primary)
                {
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = key, Level = dir > 0 ? 1f : 0f, CommandId = cmd.Id, Reason = why });
                }
                else
                {
                    plan.Ops.Add(new PatchOperation { Kind = dir > 0 ? PatchOpKind.Increment : PatchOpKind.Decrement, Target = target, Key = key, Delta = delta, CommandId = cmd.Id, Reason = why, Collateral = !e.Primary, Confidence = cmd.Confidence });
                }
                if (e.Primary) plan.Sign = (op == SemanticIntent.Increase ? 1 : -1);
                return;
            }

            if (op == SemanticIntent.Set || op == SemanticIntent.Add || op == SemanticIntent.Replace)
            {
                int dir = cmd.Direction * ws;
                MagnitudeLevel lvl = cmd.Magnitude;
                float abs;
                PatchLimit limit = PatchLimit.None;
                if (cmd.Negated)
                {
                    if (MagnitudeEngine.IsHigh(lvl))
                    {
                        // "no demasiado musculoso": a limit, not a reversal
                        if (!e.Primary) return;
                        abs = MagnitudeEngine.NegatedAbsolute(lvl, table);
                        limit = dir > 0 ? PatchLimit.AtMost : PatchLimit.AtLeast;
                        plan.Sign = 0;
                    }
                    else
                    {
                        dir = -dir;
                        abs = MagnitudeEngine.Absolute(lvl, table);
                        if (e.Primary) plan.Sign = -Math.Sign(cmd.Direction);
                    }
                }
                else
                {
                    abs = MagnitudeEngine.Absolute(lvl, table);
                    if (e.Primary) plan.Sign = Math.Sign(cmd.Direction);
                }
                if (!e.Primary) abs *= aw;
                float end = dir > 0 ? 1f : 0f;
                float level = neutral + (end - neutral) * MathUtil.Clamp01(abs);
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = key, Level = level, Limit = limit, CommandId = cmd.Id, Reason = why, Collateral = !e.Primary, Confidence = cmd.Confidence });
                return;
            }

            if (op == SemanticIntent.Reset)
            {
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Reset, Target = target, Key = key, CommandId = cmd.Id, Reason = why, Collateral = !e.Primary });
                return;
            }

            if (op == SemanticIntent.Remove)
            {
                // "remove" on a scalar with remove-effects: weight sign picks the end of the range
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = key, Level = w < 0f ? 0f : 1f, CommandId = cmd.Id, Reason = why });
                plan.Removes = true;
                return;
            }

            res.Warnings.Add("The operation " + op + " does not apply to '" + cmd.Target + "'.");
        }

        private float CurrentLevel(AuthoringDraft cur, PatchTarget t, string key)
        {
            if (cur == null) return NeutralLevel(t, key);
            switch (t)
            {
                case PatchTarget.AppearanceParam: return cur.Spec != null ? cur.Spec.Appearance.Params.GetLevel(catalogs.Parameters, key) : NeutralLevel(t, key);
                case PatchTarget.DnaParam: return cur.Spec != null ? cur.Spec.Dna.Params.GetLevel(catalogs.Parameters, key) : NeutralLevel(t, key);
                case PatchTarget.Attribute: return cur.Attributes.TryGetValue(key, out float a) ? a : 0.5f;
                case PatchTarget.Profile: return cur.ProfileLevels.TryGetValue(key, out float p) ? p : 0.5f;
            }
            return 0.5f;
        }

        private void StyleOps(Plan plan, SemanticCommand cmd, ConceptEffect e, CompileResult res)
        {
            int dir = (cmd.Operation == SemanticIntent.Decrease ? -1 : 1) * (e.Weight >= 0f ? 1 : -1);
            if (cmd.Negated) dir = -dir;
            float amount = MagnitudeEngine.Delta(cmd.Magnitude, table);
            if (cmd.Magnitude == MagnitudeLevel.Maximum) amount = 0.5f;
            plan.Ops.Add(new PatchOperation
            {
                Kind = dir > 0 ? PatchOpKind.Increment : PatchOpKind.Decrement, Target = PatchTarget.StyleAxis, Key = e.Key, Delta = amount, CommandId = cmd.Id,
                Reason = cmd.Source + " -> " + cmd.Target, Confidence = cmd.Confidence
            });
            plan.Sign = dir;
        }

        private void BehaviorOps(Plan plan, SemanticCommand cmd, ConceptEffect e, ConceptDefinition def)
        {
            SemanticIntent op = cmd.Operation;
            string why = cmd.Source + " -> " + def.Id;
            float add = cmd.Magnitude == MagnitudeLevel.Unspecified ? DefaultBehaviorWeight : Math.Max(0.3f, MagnitudeEngine.Absolute(cmd.Magnitude, table));
            switch (op)
            {
                case SemanticIntent.Remove:
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Behavior, Key = e.Key, CommandId = cmd.Id, Reason = why });
                    plan.Removes = true;
                    break;
                case SemanticIntent.Increase:
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Increment, Target = PatchTarget.Behavior, Key = e.Key, Delta = MagnitudeEngine.Delta(cmd.Magnitude, table), CommandId = cmd.Id, Reason = why });
                    plan.Sign = 1;
                    plan.Adds = true;
                    break;
                case SemanticIntent.Decrease:
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Decrement, Target = PatchTarget.Behavior, Key = e.Key, Delta = MagnitudeEngine.Delta(cmd.Magnitude, table), CommandId = cmd.Id, Reason = why });
                    plan.Sign = -1;
                    break;
                default:
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Add, Target = PatchTarget.Behavior, Key = e.Key, Level = add, CommandId = cmd.Id, Reason = why });
                    plan.Sign = 1;
                    plan.Adds = true;
                    break;
            }
        }

        private void ChoiceEffectOps(Plan plan, SemanticCommand cmd, ConceptEffect e)
        {
            // a side-effect choice ("strong" -> the strong body preset) only when the request is strong enough to justify it
            bool up = cmd.Operation == SemanticIntent.Increase || cmd.Operation == SemanticIntent.Set && cmd.Direction > 0;
            if (cmd.Negated || !up || cmd.Magnitude < MagnitudeLevel.Quite) return;
            plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Choice, Key = e.Key, Value = e.Value, CommandId = cmd.Id, Collateral = !e.Primary, Reason = cmd.Source + " -> " + cmd.Target + " (side effect)" });
        }

        private void RoleOps(Plan plan, SemanticCommand cmd, ConceptEffect e, AuthoringDraft cur)
        {
            string why = cmd.Source + " -> " + cmd.Target;
            if (cmd.Operation == SemanticIntent.Remove)
            {
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Profile, Key = "Role", Value = e.Role.ToString(), CommandId = cmd.Id, Reason = why });
                plan.Removes = true;
                return;
            }
            plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Add, Target = PatchTarget.Profile, Key = "Role", Value = e.Role.ToString(), Level = 0.8f, CommandId = cmd.Id, Reason = why });
            plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Profile, Key = "PrimaryZone", Value = e.Zone.ToString(), CommandId = cmd.Id, Reason = why });
            plan.Sign = 1;
            plan.Adds = true;
        }

        // ---- choices and colours

        private void ChoiceOps(Plan plan, SemanticCommand cmd, ConceptDefinition def, AuthoringDraft cur, CompileResult res, uint seed)
        {
            SemanticIntent op = cmd.Operation;
            string why = cmd.Source + " -> " + def.Id;
            plan.ValueKey = def.Slot + "=" + cmd.Value;
            if (op == SemanticIntent.Remove)
            {
                plan.Removes = true;
                if (!string.IsNullOrEmpty(cmd.Value))
                {
                    // "sin rizos": drop that choice only if it is the current one
                    plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Choice, Key = def.Slot, CommandId = cmd.Id, Reason = why });
                    return;
                }
                foreach (ConceptEffect e in def.RemoveEffects)
                {
                    if (e.Kind == EffectKind.Choice) plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Choice, Key = e.Key, Value = e.Value, CommandId = cmd.Id, Reason = why });
                    else if (e.Kind == EffectKind.Param && TryTarget(e, out PatchTarget t, out string k))
                        plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = t, Key = k, Level = e.Weight < 0f ? 0f : 1f, CommandId = cmd.Id, Reason = why });
                }
                return;
            }
            if (op == SemanticIntent.Reset)
            {
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Reset, Target = PatchTarget.Choice, Key = def.Slot, CommandId = cmd.Id, Reason = why });
                return;
            }
            if (!string.IsNullOrEmpty(cmd.Value))
            {
                if (!catalogs.Appearance.TryGetPart(def.Slot, cmd.Value, out PartDefinition _))
                {
                    res.Warnings.Add("The part '" + cmd.Value + "' does not exist in '" + def.Slot + "'.");
                    plan.Dropped = true;
                    return;
                }
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Choice, Key = def.Slot, Value = cmd.Value, CommandId = cmd.Id, Reason = why });
                plan.Adds = cmd.Value != "none";
                plan.Removes = cmd.Value == "none";
                return;
            }
            if (op == SemanticIntent.Replace || op == SemanticIntent.Add || op == SemanticIntent.Set)
            {
                string pick = PickPart(def.Slot, cur, StableHash.Combine(seed, StableHash.Of(def.Id + cmd.Id)), op == SemanticIntent.Replace);
                if (pick == null) { res.Warnings.Add("There is no other option for '" + def.Slot + "'."); plan.Dropped = true; return; }
                plan.ValueKey = def.Slot + "=" + pick;
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Choice, Key = def.Slot, Value = pick, CommandId = cmd.Id, Reason = why + (op == SemanticIntent.Replace ? " (variation)" : " (default pick)") });
                plan.Adds = true;
                res.Assumptions.Add("No specific " + def.Id + " was named; picked '" + pick + "' deterministically.");
                return;
            }
            res.Warnings.Add("'" + def.Id + "' is a choice, so '" + op + "' does not apply; name an option or say 'change it'.");
            plan.Dropped = true;
        }

        /// <summary>Another part of the slot than the current one (variation), chosen with the seed; or the first real option (not none/plain/neutral).</summary>
        private string PickPart(string slot, AuthoringDraft cur, uint seed, bool different)
        {
            var parts = new List<string>();
            foreach (string id in catalogs.Appearance.PartIds(slot)) parts.Add(id);
            parts.Sort(StringComparer.Ordinal);
            string current = null;
            cur?.Spec?.Appearance.Choices.TryGetValue(slot, out current);
            var options = new List<string>();
            foreach (string id in parts)
            {
                if (different && id == current) continue;
                if (!different && (id == "none" || id == "plain" || id == "neutral")) continue;
                options.Add(id);
            }
            if (options.Count == 0) return null;
            if (!different) return options[0];
            return options[(int)(new SeededRandom(seed).NextFloat01() * options.Count) % options.Count];
        }

        private void ColorOps(Plan plan, SemanticCommand cmd, ConceptDefinition def, CompileResult res)
        {
            string why = cmd.Source + " -> " + def.Id;
            if (cmd.Operation == SemanticIntent.Remove || cmd.Operation == SemanticIntent.Reset)
            {
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Reset, Target = PatchTarget.Color, Key = def.ColorSlot, CommandId = cmd.Id, Reason = why });
                return;
            }
            if (!CharacterSpecificationValidator.IsHexColor(cmd.Value))
            {
                res.Warnings.Add("'" + cmd.Value + "' is not a valid colour.");
                plan.Dropped = true;
                return;
            }
            plan.ValueKey = def.ColorSlot + "=" + cmd.Value;
            plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Color, Key = def.ColorSlot, Value = cmd.Value, CommandId = cmd.Id, Reason = why });
            plan.Adds = true;
        }

        private void GroupOps(Plan plan, SemanticCommand cmd, ConceptDefinition def, AuthoringDraft cur, CompileResult res, uint seed)
        {
            string why = cmd.Source + " -> " + def.Id;
            if (def.Id == StyleConceptId && (cmd.Operation == SemanticIntent.Replace || cmd.Operation == SemanticIntent.Set))
            {
                var styles = new List<string>();
                foreach (StylePreset s in catalogs.Styles.All) styles.Add(s.Id);
                styles.Sort(StringComparer.Ordinal);
                string current = cur?.Spec?.Appearance.StyleId;
                styles.Remove(current);
                if (styles.Count == 0)
                {
                    res.Unsupported.Add(new UnsupportedCapability
                    {
                        Text = "Change the style: " + cmd.Source, Reason = UnsupportedReason.RequiresAsset, CommandId = cmd.Id,
                        RequiredSystem = "More than one style preset in the style catalog",
                        SuggestedFutureImplementation = "Add style presets (CharacterStylePreset) so a style can be swapped."
                    });
                    plan.Dropped = true;
                    return;
                }
                string pick = styles[(int)(new SeededRandom(StableHash.Combine(seed, StableHash.Of("style" + cmd.Id))).NextFloat01() * styles.Count) % styles.Count];
                plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.StyleId, Key = "style", Value = pick, CommandId = cmd.Id, Reason = why });
                plan.Adds = true;
                return;
            }
            if (cmd.Operation == SemanticIntent.Reset)
            {
                foreach (ParameterDefinition p in catalogs.Parameters.All)
                    if (def.Covers_(p.Id))
                        plan.Ops.Add(new PatchOperation { Kind = PatchOpKind.Reset, Target = p.Domain == ParameterDomain.Appearance ? PatchTarget.AppearanceParam : PatchTarget.DnaParam, Key = p.Id, CommandId = cmd.Id, Reason = why });
                return;
            }
            res.Warnings.Add("'" + def.Id + "' names a whole area; '" + cmd.Operation + "' needs something more specific (a part of it).");
            res.Clarifications.Add("What exactly about the " + def.Id + "?");
            plan.Dropped = true;
        }

        // ------------------------------------------------------------------ conflicts

        private void DetectConflicts(List<Plan> plans, CompileResult res)
        {
            int nextId = 1;
            // 1. same thing, opposite directions / different values
            for (int i = 0; i < plans.Count; i++)
            {
                for (int j = i + 1; j < plans.Count; j++)
                {
                    Plan a = plans[i], b = plans[j];
                    if (a.Dropped || b.Dropped) continue;
                    if (a.Def.Id != b.Def.Id || a.Cmd.Domain != b.Cmd.Domain || a.Cmd.Phase != b.Cmd.Phase) continue;
                    bool opposite = a.Sign != 0 && b.Sign != 0 && a.Sign != b.Sign;
                    bool addRemove = (a.Adds && b.Removes) || (a.Removes && b.Adds);
                    bool differentValues = !string.IsNullOrEmpty(a.ValueKey) && !string.IsNullOrEmpty(b.ValueKey) && a.ValueKey != b.ValueKey && a.Def.Kind != ConceptKind.Scalar && !addRemove;
                    if (!opposite && !addRemove && !differentValues) continue;
                    var c = new SemanticConflict
                    {
                        Id = nextId++, Kind = addRemove ? SemanticConflictKind.RemoveAndAdd : SemanticConflictKind.Contradiction, Severity = ConflictSeverity.Error,
                        Resolution = ConflictResolution.NeedsUserInput
                    };
                    c.Targets.Add(a.Def.Id);
                    c.CommandIds.Add(a.Cmd.Id);
                    c.CommandIds.Add(b.Cmd.Id);
                    c.Messages.Add("'" + a.Cmd.Source + "' and '" + b.Cmd.Source + "' ask for opposite things for " + a.Def.Id + ".");
                    c.ResolutionNote = "Neither was applied.";
                    res.Conflicts.Add(c);
                    a.Dropped = b.Dropped = true;
                }
            }

            // 2. a removal and a request for something inside the removed area ("sin pelo pero con pelo largo")
            for (int i = 0; i < plans.Count; i++)
            {
                Plan r = plans[i];
                if (r.Dropped || !r.Removes || r.Def.Covers.Length == 0) continue;
                for (int j = 0; j < plans.Count; j++)
                {
                    Plan b = plans[j];
                    if (i == j || b.Dropped || b.Removes || b.Def.Id == r.Def.Id) continue;
                    bool inside = false;
                    foreach (PatchOperation o in b.Ops)
                        if (o.Kind != PatchOpKind.Preserve && !o.Collateral && r.Def.Covers_(o.Key)) inside = true;
                    if (!inside) continue;
                    var c = new SemanticConflict { Id = nextId++, Kind = SemanticConflictKind.RemoveAndAdd, Severity = ConflictSeverity.Error, Resolution = ConflictResolution.NeedsUserInput };
                    c.Targets.Add(r.Def.Id);
                    c.Targets.Add(b.Def.Id);
                    c.CommandIds.Add(r.Cmd.Id);
                    c.CommandIds.Add(b.Cmd.Id);
                    c.Messages.Add("'" + r.Cmd.Source + "' removes " + r.Def.Id + ", but '" + b.Cmd.Source + "' asks for something that needs it.");
                    c.ResolutionNote = "Neither was applied.";
                    res.Conflicts.Add(c);
                    r.Dropped = b.Dropped = true;
                    break;
                }
            }

            // 3. operation level: the same key pulled both ways by different commands
            var seen = new Dictionary<string, KeyValuePair<Plan, PatchOperation>>(StringComparer.Ordinal);
            foreach (Plan p in plans)
            {
                if (p.Dropped) continue;
                foreach (PatchOperation o in p.Ops.ToArray())
                {
                    int dir = DirectionOf(o);
                    if (dir == 0) continue;
                    string key = o.Target + "|" + o.Key;
                    if (!seen.TryGetValue(key, out KeyValuePair<Plan, PatchOperation> other)) { seen[key] = new KeyValuePair<Plan, PatchOperation>(p, o); continue; }
                    if (other.Key == p || DirectionOf(other.Value) == dir) continue;
                    PatchOperation primary = o.Collateral ? other.Value : o;
                    PatchOperation side = o.Collateral ? o : other.Value;
                    var c = new SemanticConflict { Id = nextId++, Kind = SemanticConflictKind.Tension, Severity = ConflictSeverity.Warning, Resolution = ConflictResolution.AutoResolved };
                    c.Targets.Add(o.Key);
                    c.CommandIds.Add(other.Value.CommandId);
                    c.CommandIds.Add(o.CommandId);
                    if (o.Collateral != other.Value.Collateral)
                    {
                        c.Severity = ConflictSeverity.Info;
                        c.Messages.Add("A side effect of one request pulls '" + o.Key + "' against what another request asked directly.");
                        c.ResolutionNote = "What was asked directly wins; the side effect was dropped.";
                        (side == o ? p : other.Key).Ops.Remove(side);
                    }
                    else
                    {
                        c.Messages.Add("Two requests pull '" + o.Key + "' in opposite directions; both were applied in order.");
                        c.ResolutionNote = "Applied in the order given.";
                    }
                    res.Conflicts.Add(c);
                }
            }
        }

        private static int DirectionOf(PatchOperation o)
        {
            switch (o.Kind)
            {
                case PatchOpKind.Increment: return 1;
                case PatchOpKind.Decrement: return -1;
                case PatchOpKind.Modify: return o.Limit != PatchLimit.None ? 0 : (o.Level > 0.5f ? 1 : o.Level < 0.5f ? -1 : 0);
            }
            return 0;
        }

        // ------------------------------------------------------------------ preservation

        private sealed class ProtectedSet
        {
            private readonly List<string> prefixes = new List<string>();
            private readonly HashSet<string> exact = new HashSet<string>(StringComparer.Ordinal);

            public void AddConcept(ConceptDefinition def, bool primaryKeysOnly)
            {
                foreach (string p in def.Covers)
                    if (p.Length > 0) prefixes.Add(p);
                foreach (ConceptSense s in def.Senses)
                    foreach (ConceptEffect e in s.Effects)
                    {
                        if (!e.Primary) continue;
                        if (e.Kind == EffectKind.Attribute) exact.Add(PatchTarget.Attribute + "|" + e.Attribute);
                        else if (e.Kind == EffectKind.Profile) exact.Add(PatchTarget.Profile + "|" + e.ProfileKind);
                        else if (e.Kind == EffectKind.Param || e.Kind == EffectKind.Choice) prefixes.Add(e.Key);
                    }
            }

            public bool Covers(PatchOperation o)
            {
                if (exact.Contains(o.Target + "|" + o.Key)) return true;
                if (o.Target == PatchTarget.Attribute || o.Target == PatchTarget.Profile || o.Target == PatchTarget.StyleAxis) return false;
                foreach (string p in prefixes)
                    if (o.Key == p || (p.EndsWith(".", StringComparison.Ordinal) && o.Key.StartsWith(p, StringComparison.Ordinal))) return true;
                return false;
            }

            public bool IsEmpty => prefixes.Count == 0 && exact.Count == 0;
        }

        private void ApplyProtection(List<Plan> plans, ProtectedSet prot, CompileResult res)
        {
            if (prot.IsEmpty) return;
            int nextId = res.Conflicts.Count + 1;
            foreach (Plan p in plans)
            {
                if (p.Dropped) continue;
                if (p.Cmd.Operation == SemanticIntent.Preserve || p.Cmd.Intent == SemanticIntent.Preserve) continue;
                var kept = new List<PatchOperation>();
                foreach (PatchOperation o in p.Ops)
                {
                    if (o.Kind == PatchOpKind.Preserve || !prot.Covers(o)) { kept.Add(o); continue; }
                    var c = new SemanticConflict
                    {
                        Id = nextId++, Kind = SemanticConflictKind.PreserveViolation, Severity = o.Collateral ? ConflictSeverity.Info : ConflictSeverity.Warning,
                        Resolution = ConflictResolution.AutoResolved, ResolutionNote = "What was asked to be kept was kept."
                    };
                    c.Targets.Add(o.Key);
                    c.CommandIds.Add(p.Cmd.Id);
                    c.Messages.Add("'" + p.Cmd.Source + "' would change '" + o.Key + "', which was to be kept.");
                    res.Conflicts.Add(c);
                    res.Warnings.Add("Not applied (kept): " + o);
                }
                p.Ops = kept;
            }
        }
    }
}
