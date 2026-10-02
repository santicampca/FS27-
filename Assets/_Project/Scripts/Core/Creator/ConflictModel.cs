using System;
using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    public enum ConflictSeverity
    {
        Info = 0,
        Warning,
        Error,
        Critical
    }

    public enum SemanticConflictKind
    {
        /// <summary>Two requests push the same thing in opposite directions ("muy alto y extremadamente bajo").</summary>
        Contradiction = 0,
        /// <summary>Not impossible together, but pulling against each other (a side effect against a request).</summary>
        Tension,
        /// <summary>A request touches something the person asked to keep.</summary>
        PreserveViolation,
        /// <summary>The same thing said twice.</summary>
        Redundancy,
        /// <summary>A request removes something and also asks for it ("sin pelo pero con pelo largo").</summary>
        RemoveAndAdd
    }

    public enum ConflictResolution
    {
        Unresolved = 0,
        /// <summary>A documented rule settled it (explicit beats side effect; Preserve wins).</summary>
        AutoResolved,
        /// <summary>The engine will not guess: it asks.</summary>
        NeedsUserInput,
        Ignored
    }

    /// <summary>A real, structured conflict: what, how bad, between which things, and what happened to it. Never just text.</summary>
    [Serializable]
    public sealed class SemanticConflict
    {
        public int Id;
        public SemanticConflictKind Kind;
        public ConflictSeverity Severity;
        /// <summary>The concepts involved.</summary>
        public List<string> Targets = new List<string>();
        public List<int> CommandIds = new List<int>();
        public List<string> Messages = new List<string>();
        public ConflictResolution Resolution;
        public string ResolutionNote = "";

        /// <summary>True when the conflict stops the commands involved from being applied.</summary>
        public bool Blocks => Resolution == ConflictResolution.Unresolved || Resolution == ConflictResolution.NeedsUserInput;
    }

    /// <summary>Something that was asked and that this build cannot do yet, and what it would take.</summary>
    [Serializable]
    public sealed class UnsupportedCapability
    {
        public string Text = "";
        public UnsupportedReason Reason;
        /// <summary>The system that would have to exist.</summary>
        public string RequiredSystem = "";
        public string SuggestedFutureImplementation = "";
        public int CommandId;
    }

    /// <summary>
    /// A readable account of what was understood, what was not, what was assumed, what was applied and what was refused. Built so a person
    /// (or a future editor) can see WHY the character changed.
    /// </summary>
    [Serializable]
    public sealed class InterpretationReport
    {
        public string Language = "";
        public string Text = "";
        public string Interpreter = "";
        public List<string> Understood = new List<string>();
        public List<string> NotUnderstood = new List<string>();
        public List<string> Assumptions = new List<string>();
        public List<string> Applied = new List<string>();
        public List<string> Blocked = new List<string>();
        public List<SemanticConflict> Conflicts = new List<SemanticConflict>();
        public List<UnsupportedCapability> Unsupported = new List<UnsupportedCapability>();
        public List<string> Clarifications = new List<string>();
        /// <summary>0..1: the lowest confidence of what was applied, lowered by what was not understood.</summary>
        public float Confidence;

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("Text: ").Append(Text).Append("  [").Append(Language).Append(", ").Append(Interpreter).Append(", confidence ")
              .Append(Confidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("]\n");
            Section(sb, "Understood", Understood);
            Section(sb, "Applied", Applied);
            Section(sb, "Assumed", Assumptions);
            Section(sb, "Not understood", NotUnderstood);
            Section(sb, "Blocked", Blocked);
            if (Conflicts.Count > 0)
            {
                sb.Append("Conflicts:\n");
                foreach (SemanticConflict c in Conflicts)
                    sb.Append("  - [").Append(c.Severity).Append(' ').Append(c.Kind).Append(", ").Append(c.Resolution).Append("] ").Append(string.Join("; ", c.Messages)).Append('\n');
            }
            if (Unsupported.Count > 0)
            {
                sb.Append("Not possible yet:\n");
                foreach (UnsupportedCapability u in Unsupported) sb.Append("  - ").Append(u.Text).Append(" (needs: ").Append(u.RequiredSystem).Append(")\n");
            }
            Section(sb, "Questions", Clarifications);
            return sb.ToString();
        }

        private static void Section(StringBuilder sb, string title, List<string> items)
        {
            if (items.Count == 0) return;
            sb.Append(title).Append(":\n");
            foreach (string s in items) sb.Append("  - ").Append(s).Append('\n');
        }
    }
}
