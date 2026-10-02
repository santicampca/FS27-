using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Shared helpers for building and reading provider JSON.</summary>
    internal static class WireJson
    {
        internal static JsonValue Schema(string schemaText, bool stripAdditionalProperties = false)
        {
            JsonValue v = Json.Parse(schemaText);
            if (stripAdditionalProperties) Strip(v);
            return v;
        }

        private static void Strip(JsonValue v)
        {
            if (v.Kind == JsonKind.Object)
            {
                v.Members.Remove("additionalProperties");
                foreach (JsonValue child in v.Members.Values) Strip(child);
            }
            else if (v.Kind == JsonKind.Array) foreach (JsonValue child in v.Items) Strip(child);
        }

        internal static bool TryParse(string body, out JsonValue root, out string problem)
        {
            problem = "";
            if (!Json.TryParse(body ?? "", out root, out string error) || root.Kind != JsonKind.Object)
            {
                problem = "The provider's answer is not a JSON object" + (string.IsNullOrEmpty(error) ? "." : ": " + error);
                root = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Claude (Messages API) with structured output: the answer is constrained by a JSON Schema, so it arrives as JSON text in a text block.
    /// ⚠️ The request shape follows the official structured-outputs documentation; it has NEVER been sent to the real service from this project.
    /// Nothing here holds a key: the host's transport adds the credential. The thinking option is left out on purpose.
    /// </summary>
    public sealed class ClaudeWireFormat : ILlmWireFormat
    {
        public const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";
        public const string ApiVersion = "2023-06-01";

        public string Endpoint = DefaultEndpoint;
        public string Name => "claude.messages.structured";
        public bool ExercisedAgainstRealService => false;

        public WireRequest Build(LlmRequest request)
        {
            var w = new WireRequest { Url = Endpoint };
            w.Headers["anthropic-version"] = ApiVersion;
            w.Headers["content-type"] = "application/json";
            var msgs = JsonValue.NewArray();
            foreach (LlmMessage m in request.Messages) msgs.Add(JsonValue.NewObject().Set("role", JsonValue.Of(m.Role)).Set("content", JsonValue.Of(m.Text)));
            var body = JsonValue.NewObject()
                .Set("model", JsonValue.Of(request.Model))
                .Set("max_tokens", JsonValue.Of((double)request.MaxTokens))
                .Set("messages", msgs);
            if (!string.IsNullOrEmpty(request.System)) body.Set("system", JsonValue.Of(request.System));
            if (!string.IsNullOrEmpty(request.JsonSchema))
                body.Set("output_config", JsonValue.NewObject().Set("format", JsonValue.NewObject().Set("type", JsonValue.Of("json_schema")).Set("schema", WireJson.Schema(request.JsonSchema))));
            w.Body = Json.Write(body);
            return w;
        }

        public LlmParsedResponse Parse(WireResponse response)
        {
            var r = new LlmParsedResponse { Unusable = true };
            if (!string.IsNullOrEmpty(response.TransportError)) { r.Problem = response.TransportError; return r; }
            if (!WireJson.TryParse(response.Body, out JsonValue root, out string problem)) { r.Problem = problem; return r; }
            if (root.GetString("type", "") == "error")
            {
                r.Problem = "The provider reported an error" + (root.TryGet("error", out JsonValue e) && e.Kind == JsonKind.Object ? ": " + e.GetString("message", "") : ".");
                return r;
            }
            if (!response.Ok) { r.Problem = "HTTP status " + response.StatusCode + "."; return r; }
            string stop = root.GetString("stop_reason", "");
            if (stop == "refusal") { r.Problem = "The model refused to answer."; return r; }
            if (stop == "max_tokens") { r.Problem = "The answer was cut short (max_tokens): it may be incomplete."; return r; }
            if (root.TryGet("content", out JsonValue content) && content.Kind == JsonKind.Array)
                foreach (JsonValue block in content.Items)
                    if (block.Kind == JsonKind.Object && block.GetString("type", "") == "text") { r.Text = block.GetString("text", ""); r.Unusable = false; return r; }
            r.Problem = "The answer has no text block.";
            return r;
        }
    }

    /// <summary>OpenAI chat completions with a strict JSON schema. ⚠️ NO VERIFICADO: written from memory of the public API, never run.</summary>
    public sealed class OpenAiWireFormat : ILlmWireFormat
    {
        public string Endpoint = "https://api.openai.com/v1/chat/completions";
        public string Name => "openai.chat.json_schema";
        public bool ExercisedAgainstRealService => false;

        public WireRequest Build(LlmRequest request)
        {
            var w = new WireRequest { Url = Endpoint };
            w.Headers["content-type"] = "application/json";
            var msgs = JsonValue.NewArray();
            if (!string.IsNullOrEmpty(request.System)) msgs.Add(JsonValue.NewObject().Set("role", JsonValue.Of("system")).Set("content", JsonValue.Of(request.System)));
            foreach (LlmMessage m in request.Messages) msgs.Add(JsonValue.NewObject().Set("role", JsonValue.Of(m.Role)).Set("content", JsonValue.Of(m.Text)));
            var body = JsonValue.NewObject().Set("model", JsonValue.Of(request.Model)).Set("messages", msgs).Set("max_completion_tokens", JsonValue.Of((double)request.MaxTokens));
            if (!string.IsNullOrEmpty(request.JsonSchema))
                body.Set("response_format", JsonValue.NewObject().Set("type", JsonValue.Of("json_schema")).Set("json_schema",
                    JsonValue.NewObject().Set("name", JsonValue.Of(request.SchemaName)).Set("strict", JsonValue.Of(true)).Set("schema", WireJson.Schema(request.JsonSchema))));
            w.Body = Json.Write(body);
            return w;
        }

        public LlmParsedResponse Parse(WireResponse response)
        {
            var r = new LlmParsedResponse { Unusable = true };
            if (!string.IsNullOrEmpty(response.TransportError)) { r.Problem = response.TransportError; return r; }
            if (!WireJson.TryParse(response.Body, out JsonValue root, out string problem)) { r.Problem = problem; return r; }
            if (!response.Ok) { r.Problem = "HTTP status " + response.StatusCode + "."; return r; }
            if (!root.TryGet("choices", out JsonValue choices) || choices.Kind != JsonKind.Array || choices.Items.Count == 0) { r.Problem = "The answer has no choices."; return r; }
            JsonValue choice = choices.Items[0];
            string finish = choice.GetString("finish_reason", "");
            if (finish == "length" || finish == "content_filter") { r.Problem = "The answer was not completed (" + finish + ")."; return r; }
            if (choice.TryGet("message", out JsonValue msg) && msg.Kind == JsonKind.Object)
            {
                if (msg.TryGet("refusal", out JsonValue refusal) && refusal.Kind == JsonKind.String && refusal.String.Length > 0) { r.Problem = "The model refused to answer."; return r; }
                string content = msg.GetString("content", null);
                if (content != null) { r.Text = content; r.Unusable = false; return r; }
            }
            r.Problem = "The answer has no content.";
            return r;
        }
    }

    /// <summary>Gemini generateContent with a response schema. ⚠️ NO VERIFICADO: written from memory of the public API, never run. Its schema dialect has no additionalProperties, so it is removed.</summary>
    public sealed class GeminiWireFormat : ILlmWireFormat
    {
        public string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        public string Name => "gemini.generateContent.schema";
        public bool ExercisedAgainstRealService => false;

        public WireRequest Build(LlmRequest request)
        {
            var w = new WireRequest { Url = EndpointTemplate.Replace("{model}", request.Model) };
            w.Headers["content-type"] = "application/json";
            var contents = JsonValue.NewArray();
            foreach (LlmMessage m in request.Messages)
                contents.Add(JsonValue.NewObject().Set("role", JsonValue.Of(m.Role == "assistant" ? "model" : "user")).Set("parts", JsonValue.NewArray().Add(JsonValue.NewObject().Set("text", JsonValue.Of(m.Text)))));
            var gen = JsonValue.NewObject().Set("responseMimeType", JsonValue.Of("application/json")).Set("maxOutputTokens", JsonValue.Of((double)request.MaxTokens));
            if (!string.IsNullOrEmpty(request.JsonSchema)) gen.Set("responseSchema", WireJson.Schema(request.JsonSchema, stripAdditionalProperties: true));
            var body = JsonValue.NewObject().Set("contents", contents).Set("generationConfig", gen);
            if (!string.IsNullOrEmpty(request.System)) body.Set("systemInstruction", JsonValue.NewObject().Set("parts", JsonValue.NewArray().Add(JsonValue.NewObject().Set("text", JsonValue.Of(request.System)))));
            w.Body = Json.Write(body);
            return w;
        }

        public LlmParsedResponse Parse(WireResponse response)
        {
            var r = new LlmParsedResponse { Unusable = true };
            if (!string.IsNullOrEmpty(response.TransportError)) { r.Problem = response.TransportError; return r; }
            if (!WireJson.TryParse(response.Body, out JsonValue root, out string problem)) { r.Problem = problem; return r; }
            if (!response.Ok) { r.Problem = "HTTP status " + response.StatusCode + "."; return r; }
            if (!root.TryGet("candidates", out JsonValue cands) || cands.Kind != JsonKind.Array || cands.Items.Count == 0) { r.Problem = "The answer has no candidates (possibly blocked)."; return r; }
            JsonValue cand = cands.Items[0];
            string finish = cand.GetString("finishReason", "");
            if (finish == "SAFETY" || finish == "MAX_TOKENS" || finish == "RECITATION") { r.Problem = "The answer was not completed (" + finish + ")."; return r; }
            if (cand.TryGet("content", out JsonValue content) && content.Kind == JsonKind.Object && content.TryGet("parts", out JsonValue parts) && parts.Kind == JsonKind.Array && parts.Items.Count > 0)
            {
                string text = parts.Items[0].GetString("text", null);
                if (text != null) { r.Text = text; r.Unusable = false; return r; }
            }
            r.Problem = "The answer has no text.";
            return r;
        }
    }

    /// <summary>A local model served over an Ollama-style chat endpoint with a JSON schema format. ⚠️ NO VERIFICADO: written from memory, never run.</summary>
    public sealed class LocalModelWireFormat : ILlmWireFormat
    {
        public string Endpoint = "http://localhost:11434/api/chat";
        public string Name => "local.chat.format";
        public bool ExercisedAgainstRealService => false;

        public WireRequest Build(LlmRequest request)
        {
            var w = new WireRequest { Url = Endpoint };
            w.Headers["content-type"] = "application/json";
            var msgs = JsonValue.NewArray();
            if (!string.IsNullOrEmpty(request.System)) msgs.Add(JsonValue.NewObject().Set("role", JsonValue.Of("system")).Set("content", JsonValue.Of(request.System)));
            foreach (LlmMessage m in request.Messages) msgs.Add(JsonValue.NewObject().Set("role", JsonValue.Of(m.Role)).Set("content", JsonValue.Of(m.Text)));
            var body = JsonValue.NewObject().Set("model", JsonValue.Of(request.Model)).Set("messages", msgs).Set("stream", JsonValue.Of(false))
                .Set("options", JsonValue.NewObject().Set("num_predict", JsonValue.Of((double)request.MaxTokens)));
            if (!string.IsNullOrEmpty(request.JsonSchema)) body.Set("format", WireJson.Schema(request.JsonSchema));
            w.Body = Json.Write(body);
            return w;
        }

        public LlmParsedResponse Parse(WireResponse response)
        {
            var r = new LlmParsedResponse { Unusable = true };
            if (!string.IsNullOrEmpty(response.TransportError)) { r.Problem = response.TransportError; return r; }
            if (!WireJson.TryParse(response.Body, out JsonValue root, out string problem)) { r.Problem = problem; return r; }
            if (!response.Ok) { r.Problem = "HTTP status " + response.StatusCode + "."; return r; }
            if (root.GetString("done_reason", "") == "length") { r.Problem = "The answer was cut short."; return r; }
            if (root.TryGet("message", out JsonValue msg) && msg.Kind == JsonKind.Object)
            {
                string content = msg.GetString("content", null);
                if (content != null) { r.Text = content; r.Unusable = false; return r; }
            }
            r.Problem = "The answer has no content.";
            return r;
        }
    }
}
