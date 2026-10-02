using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// A readable trace of the whole pipeline for one request: Prompt -> Interpretation -> Specification -> Validation -> FootballDNA ->
    /// Behaviour candidates. Meant for logs, tests and a future debug window; it changes nothing.
    /// </summary>
    public static class PromptDebugReport
    {
        public static string Build(PromptRequest request, PromptResult result, CreatorCatalogs catalogs, in PlayerAttributes attributes, BehaviorContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("== PROMPT ==");
            sb.AppendLine(request.Text);

            sb.AppendLine("== INTERPRETATION ==");
            sb.AppendLine("interpreter: " + result.InterpreterName + " | intent: " + result.Intent + " | context: " + result.Context + " | confidence: " + F(result.Confidence));
            foreach (SpecChange c in result.Changes)
                sb.AppendLine("  change " + c.Kind + " " + c.Target + (c.Kind == SpecChangeKind.ScalarSet ? " level=" + F(c.Level) : "") +
                              (c.Kind == SpecChangeKind.ScalarDelta || c.Kind == SpecChangeKind.StyleShift ? " dir=" + c.Direction + " mag=" + F(c.Magnitude) : "") +
                              (c.Kind == SpecChangeKind.BehaviorAdd ? " weight=" + F(c.Level) : "") + (!string.IsNullOrEmpty(c.Value) ? " value=" + c.Value : "") +
                              " conf=" + F(c.Confidence) + "  <- \"" + c.Source + "\"");
            foreach (AttributeHint h in result.AttributeHints)
                sb.AppendLine("  attribute hint " + h.Attribute + (h.Relative ? " delta=" + F(h.Delta) : " level=" + F(h.Level)) + "  <- \"" + h.Source + "\"");
            foreach (ProfileHint h in result.ProfileHints)
                sb.AppendLine("  profile hint " + h.Kind + (h.Kind == ProfileHintKind.PrimaryZone ? " " + h.Zone : h.Kind == ProfileHintKind.Role ? " " + h.Role + " affinity=" + F(h.Level) : (h.Relative ? " delta=" + F(h.Delta) : " level=" + F(h.Level))) + "  <- \"" + h.Source + "\"");
            foreach (PromptConflict c in result.Conflicts) sb.AppendLine("  CONFLICT (" + c.Kind + ") " + c.Description);
            foreach (UnsupportedRequest u in result.Unsupported) sb.AppendLine("  UNSUPPORTED (" + u.Reason + ") \"" + u.Text + "\": " + u.Explanation);
            foreach (string u in result.Unresolved) sb.AppendLine("  UNRESOLVED \"" + u + "\"");
            foreach (string w in result.Warnings) sb.AppendLine("  warning: " + w);

            sb.AppendLine("== SPECIFICATION ==");
            if (result.Draft != null) sb.AppendLine(CharacterSpecificationJson.ToJson(result.Draft, false, false));

            sb.AppendLine("== VALIDATION ==");
            sb.AppendLine(result.Draft == null ? "no draft" : CharacterSpecificationValidator.Validate(result.Draft, catalogs).ToString());
            sb.AppendLine(PromptSpecificationValidator.Validate(result, catalogs).ToString());

            sb.AppendLine("== FOOTBALL DNA (non-neutral) ==");
            if (result.Draft != null)
            {
                foreach (KeyValuePair<string, float> kv in result.Draft.Dna.Params.Values) sb.AppendLine("  " + kv.Key + " = " + F(kv.Value));
                foreach (BehaviorEntry b in result.Draft.Dna.Behaviors) sb.AppendLine("  behaviour " + b.Id + " = " + F(b.Weight));
            }

            sb.AppendLine("== BEHAVIOUR CANDIDATES (context " + context + ") ==");
            if (result.Draft != null)
                foreach (BehaviorCandidate c in BehaviorResolver.Resolve(result.Draft.Dna, attributes, context, catalogs.Parameters, catalogs.Behaviors))
                    sb.AppendLine("  " + c.BehaviorId + " " + F(c.Score));
            return sb.ToString();
        }

        private static string F(float v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
