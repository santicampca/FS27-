using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// A <see cref="CharacterSpecificationPatch"/> as JSON: how a future visual editor (or a script, or a language model) hands a precise edit to the
    /// engine. Strict on read: unknown operation kinds, targets or fields are rejected, never guessed.
    /// </summary>
    public static class PatchJson
    {
        public static string ToJson(CharacterSpecificationPatch patch)
        {
            var ops = JsonValue.NewArray();
            foreach (PatchOperation o in patch.Operations)
            {
                var j = JsonValue.NewObject().Set("kind", JsonValue.Of(o.Kind.ToString())).Set("target", JsonValue.Of(o.Target.ToString())).Set("key", JsonValue.Of(o.Key));
                if (o.Kind == PatchOpKind.Modify || o.Kind == PatchOpKind.Add || (o.Kind == PatchOpKind.Replace && o.Level != 0f)) j.Set("level", JsonValue.Of(o.Level));
                if (o.Kind == PatchOpKind.Increment || o.Kind == PatchOpKind.Decrement) j.Set("delta", JsonValue.Of(o.Delta));
                if (o.Limit != PatchLimit.None) j.Set("limit", JsonValue.Of(o.Limit.ToString()));
                if (!string.IsNullOrEmpty(o.Value)) j.Set("value", JsonValue.Of(o.Value));
                if (!string.IsNullOrEmpty(o.Reason)) j.Set("reason", JsonValue.Of(o.Reason));
                if (o.Collateral) j.Set("collateral", JsonValue.Of(true));
                ops.Add(j);
            }
            return Json.Write(JsonValue.NewObject().Set("schemaVersion", JsonValue.Of(patch.SchemaVersion)).Set("label", JsonValue.Of(patch.Label)).Set("operations", ops));
        }

        public static bool TryFromJson(string text, out CharacterSpecificationPatch patch, CreatorValidationResult result)
        {
            patch = null;
            if (!Json.TryParse(text ?? "", out JsonValue root, out string error)) { result.Error(CreatorIssueCode.JsonInvalid, "patch", error); return false; }
            if (root.Kind != JsonKind.Object || root.GetString("schemaVersion", null) != CharacterSpecificationPatch.CurrentSchema)
            {
                result.Error(CreatorIssueCode.SchemaVersionUnsupported, "patch", "Expected schema '" + CharacterSpecificationPatch.CurrentSchema + "'.");
                return false;
            }
            if (!root.TryGet("operations", out JsonValue ops) || ops.Kind != JsonKind.Array) { result.Error(CreatorIssueCode.JsonShapeInvalid, "patch", "'operations' must be an array."); return false; }
            var p = new CharacterSpecificationPatch { Label = root.GetString("label", "") };
            int n = 0;
            foreach (JsonValue j in ops.Items)
            {
                string at = "operations[" + n++ + "]";
                if (j.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, at, "Must be an object."); continue; }
                var o = new PatchOperation { Key = j.GetString("key", ""), Value = j.GetString("value", ""), Reason = j.GetString("reason", ""), Level = (float)j.GetNumber("level", 0.0), Delta = (float)j.GetNumber("delta", 0.0) };
                if (!Enum.TryParse(j.GetString("kind", ""), false, out o.Kind) || !Enum.IsDefined(typeof(PatchOpKind), o.Kind)) result.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown kind '" + j.GetString("kind", "") + "'.");
                if (!Enum.TryParse(j.GetString("target", ""), false, out o.Target) || !Enum.IsDefined(typeof(PatchTarget), o.Target)) result.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown target '" + j.GetString("target", "") + "'.");
                if (j.TryGet("limit", out JsonValue lim) && (lim.Kind != JsonKind.String || !Enum.TryParse(lim.String, false, out o.Limit) || !Enum.IsDefined(typeof(PatchLimit), o.Limit))) result.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown limit.");
                o.Collateral = j.TryGet("collateral", out JsonValue c) && c.Kind == JsonKind.Bool && c.Bool;
                if (string.IsNullOrEmpty(o.Key) && o.Kind != PatchOpKind.Preserve) result.Error(CreatorIssueCode.JsonShapeInvalid, at, "A key is required.");
                p.Operations.Add(o);
            }
            if (!result.IsValid) return false;
            patch = p;
            return true;
        }
    }

    /// <summary>
    /// Lets the aim assist (see <see cref="PlayerAssistSettings"/> / <see cref="AssistParameters"/>) work on the aim carried by an intent. The DNA
    /// never touches the aim (it only flavours HOW an action is done), so assist and DNA cannot fight over the same number.
    /// </summary>
    public static class IntentAssist
    {
        /// <summary>
        /// If a candidate target lies within <paramref name="snapDegrees"/> of the aimed direction, returns the direction to that candidate (the
        /// nearest in angle wins; ties by distance, then by index); otherwise the aim unchanged. Pure; the Match decides who the candidates are.
        /// </summary>
        public static Vec2 SnapAim(Vec2 origin, Vec2 aim, IList<Vec2> candidates, float snapDegrees)
        {
            if (candidates == null || candidates.Count == 0 || snapDegrees <= 0f) return aim;
            Vec2 dir = (aim - origin).Normalized;
            if (dir.SqrMagnitude < 1e-8f) return aim;
            float best = float.MaxValue, bestDist = float.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vec2 d = candidates[i] - origin;
                float dist = d.Magnitude;
                if (dist < 1e-4f) continue;
                float cos = Math.Max(-1f, Math.Min(1f, (d.X * dir.X + d.Y * dir.Y) / dist));
                float angle = (float)Math.Acos(cos) * MathUtil.Rad2Deg;
                if (angle > snapDegrees) continue;
                if (angle < best - 1e-4f || (Math.Abs(angle - best) <= 1e-4f && dist < bestDist)) { best = angle; bestDist = dist; bestIndex = i; }
            }
            return bestIndex < 0 ? aim : candidates[bestIndex];
        }
    }
}
