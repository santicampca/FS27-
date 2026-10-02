using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// What can be wrong with Creator Engine data. Append new codes at the END, so numbers stay stable.
    /// </summary>
    public enum CreatorIssueCode
    {
        // ---- general
        SpecificationNull,
        SchemaVersionMissing,
        SchemaVersionUnsupported,
        SchemaVersionNewer,
        CharacterIdInvalid,
        // ---- parameters
        ParameterUnknown,
        ParameterOutOfRange,
        ParameterNotANumber,
        ParameterWrongDomain,
        // ---- choices / parts / colours / style
        ChoiceSlotUnknown,
        ChoicePartUnknown,
        ChoicePartIncompatibleWithBase,
        ColorSlotUnknown,
        ColorInvalid,
        StyleUnknown,
        BaseModelUnknown,
        // ---- football DNA
        BehaviorUnknown,
        BehaviorDuplicate,
        BehaviorWeightOutOfRange,
        BehaviorNotExecutableYet,
        BehaviorRequirementNotMet,
        // ---- player link
        PlayerNotFound,
        PlayerAlreadyHasSpecification,
        CharacterIdDuplicate,
        // ---- prompt results
        ChangeTargetUnknown,
        ChangeMagnitudeOutOfRange,
        ChangeConfidenceOutOfRange,
        ChangeKindInvalid,
        ResultInconsistent,
        // ---- json
        JsonInvalid,
        JsonShapeInvalid,
        // ---- semantic layer (appended in the intelligence phase)
        SemanticSchemaUnknown,
        SemanticFieldInvalid,
        SemanticTargetUnknown,
        SemanticValueOutOfRange,
        SemanticRelationInvalid,
        // ---- football DNA 2.0
        BehaviorSettingOutOfRange,
        BehaviorConditionContradictory,
        SequenceInvalid,
        ConfidenceOutOfRange,
        // ---- observations and research
        ObservationInvalid,
        ResearchFailed
    }

    public enum CreatorSeverity
    {
        Error,
        Warning
    }

    public struct CreatorIssue
    {
        public CreatorIssueCode Code;
        public CreatorSeverity Severity;
        public string Subject;
        public string Message;

        public override string ToString()
        {
            return "[" + Code + (Severity == CreatorSeverity.Warning ? ", warning" : "") + "] " + Subject + ": " + Message;
        }
    }

    /// <summary>Everything found wrong with something, all together (never just the first problem).</summary>
    public sealed class CreatorValidationResult
    {
        private readonly List<CreatorIssue> issues = new List<CreatorIssue>();

        public IReadOnlyList<CreatorIssue> Issues => issues;
        public int Count => issues.Count;

        /// <summary>True when there are no ERRORS (warnings do not make data invalid).</summary>
        public bool IsValid
        {
            get
            {
                foreach (CreatorIssue i in issues)
                    if (i.Severity == CreatorSeverity.Error) return false;
                return true;
            }
        }

        public void Error(CreatorIssueCode code, string subject, string message)
        {
            issues.Add(new CreatorIssue { Code = code, Severity = CreatorSeverity.Error, Subject = subject, Message = message });
        }

        public void Warning(CreatorIssueCode code, string subject, string message)
        {
            issues.Add(new CreatorIssue { Code = code, Severity = CreatorSeverity.Warning, Subject = subject, Message = message });
        }

        public void Merge(CreatorValidationResult other)
        {
            if (other != null) issues.AddRange(other.issues);
        }

        public bool Has(CreatorIssueCode code)
        {
            foreach (CreatorIssue i in issues)
                if (i.Code == code) return true;
            return false;
        }

        public int CountOf(CreatorIssueCode code)
        {
            int n = 0;
            foreach (CreatorIssue i in issues)
                if (i.Code == code) n++;
            return n;
        }

        public override string ToString()
        {
            if (issues.Count == 0) return "valid";
            var sb = new StringBuilder();
            for (int i = 0; i < issues.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(issues[i]);
            }
            return sb.ToString();
        }
    }
}
