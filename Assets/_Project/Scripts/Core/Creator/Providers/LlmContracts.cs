using System;
using System.Collections.Generic;

namespace FS27.Core
{
    // =====================================================================================================================
    // LANGUAGE-MODEL SEAM. Nothing in this folder opens a connection, holds a key or runs what a model says.
    //   Prompt -> (template) -> LlmRequest -> (wire format) -> WireRequest -> (transport, supplied by the HOST) -> WireResponse
    //          -> (wire format) -> text -> (schema -> semantic -> domain checks) -> SemanticProgram (DATA) -> the shared compiler.
    // A model is an interpreter among others: its output is untrusted data, validated, and never code.
    // =====================================================================================================================

    public sealed class LlmMessage
    {
        /// <summary>"user" or "assistant".</summary>
        public string Role = "user";
        public string Text = "";

        public LlmMessage() { }
        public LlmMessage(string role, string text) { Role = role; Text = text; }
    }

    /// <summary>A provider-neutral request for ONE structured answer.</summary>
    public sealed class LlmRequest
    {
        public string Model = "";
        public string System = "";
        public readonly List<LlmMessage> Messages = new List<LlmMessage>();
        /// <summary>The JSON Schema the answer must follow (as JSON text).</summary>
        public string JsonSchema = "";
        /// <summary>A short name for the schema (some providers require one).</summary>
        public string SchemaName = "fs27_output";
        public int MaxTokens = 2048;
    }

    /// <summary>What goes on the wire (built by a wire format, sent by a transport). The credential is NOT here: the transport adds it from the host's own secret store.</summary>
    public sealed class WireRequest
    {
        public string Url = "";
        public string Method = "POST";
        /// <summary>Non-secret headers only (content type, API version).</summary>
        public readonly SortedDictionary<string, string> Headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public string Body = "";
    }

    public sealed class WireResponse
    {
        public int StatusCode;
        public string Body = "";
        /// <summary>Set by the transport when nothing came back (offline, timeout, refused).</summary>
        public string TransportError = "";

        public bool Ok => string.IsNullOrEmpty(TransportError) && StatusCode >= 200 && StatusCode < 300;
    }

    /// <summary>
    /// Sends a <see cref="WireRequest"/> and returns the answer. The ONLY seam that may touch a network, and it is implemented by the HOST (in the
    /// editor tooling or a service), never in Core and never shipped inside the game. No implementation is mandatory: offline use needs none.
    /// </summary>
    public interface ILlmTransport
    {
        string Name { get; }
        WireResponse Send(WireRequest request);
    }

    public sealed class LlmParsedResponse
    {
        /// <summary>The model's text (the JSON answer).</summary>
        public string Text = "";
        /// <summary>The model refused or was cut short; <see cref="Text"/> must not be trusted.</summary>
        public bool Unusable;
        public string Problem = "";
    }

    /// <summary>How one provider's request and response look. Pure string building and parsing: easy to test without any network.</summary>
    public interface ILlmWireFormat
    {
        string Name { get; }
        /// <summary>True only for formats checked against the provider's documentation AND exercised against the real service. None are, in this build.</summary>
        bool ExercisedAgainstRealService { get; }
        WireRequest Build(LlmRequest request);
        LlmParsedResponse Parse(WireResponse response);
    }

    /// <summary>The default transport: there is none. Everything keeps working offline; asking a model reports clearly why it cannot.</summary>
    public sealed class OfflineTransport : ILlmTransport
    {
        public string Name => "FS27.OfflineTransport";

        public WireResponse Send(WireRequest request)
        {
            return new WireResponse { TransportError = "No transport is configured: the engine is running offline." };
        }
    }

    /// <summary>A transport that answers from a script and remembers what it was asked. For tests and demos; it never touches a network.</summary>
    public sealed class ScriptedTransport : ILlmTransport
    {
        private readonly Queue<WireResponse> responses = new Queue<WireResponse>();
        public readonly List<WireRequest> Received = new List<WireRequest>();

        public string Name => "FS27.ScriptedTransport";

        public ScriptedTransport Enqueue(WireResponse r) { responses.Enqueue(r); return this; }
        public ScriptedTransport EnqueueBody(string body, int status = 200) { return Enqueue(new WireResponse { StatusCode = status, Body = body }); }

        public WireResponse Send(WireRequest request)
        {
            Received.Add(request);
            return responses.Count > 0 ? responses.Dequeue() : new WireResponse { TransportError = "The script has no more answers." };
        }
    }
}
