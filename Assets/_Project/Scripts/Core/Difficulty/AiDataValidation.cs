using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>Every problem the AI-configuration validators can report. Tests match on these, never on message text.</summary>
    public enum AiDataIssueCode
    {
        // Difficulty definitions
        DifficultyNull,
        DifficultyIdInvalid,
        DifficultyNameInvalid,
        DifficultyLevelInvalid,
        DifficultyValueOutOfRange,
        DifficultyBelowHumanLimit,
        DifficultyAssistInvalid,

        // Difficulty set
        DifficultySetDuplicateId,
        DifficultySetDuplicateLevel,
        DifficultySetNotOrdered,

        // Team style
        StyleNull,
        StyleIdInvalid,
        StyleNameInvalid,
        StyleValueOutOfRange,

        // Playing profile (zones / archetypes of a versatile player)
        ProfileNull,
        ProfilePlayerIdInvalid,
        ProfileZoneInvalid,
        ProfileSecondaryZoneDuplicate,
        ProfileSecondaryEqualsPrimary,
        ProfileArchetypeCountInvalid,
        ProfileArchetypeDuplicate,
        ProfileArchetypeInvalid,
        ProfileGoalkeeperMismatch,

        // Player System V2 (appended, so earlier codes keep their values)
        ProfileAffinityCountMismatch,
        ProfileAffinityOutOfRange,
        ProfileBehaviourOutOfRange,
        ProfilePlayerIdMismatch
    }

    public sealed class AiDataIssue
    {
        public AiDataIssueCode Code { get; }
        public string Subject { get; }
        public string Message { get; }

        public AiDataIssue(AiDataIssueCode code, string subject, string message)
        {
            Code = code;
            Subject = subject;
            Message = message;
        }

        public override string ToString()
        {
            return "[" + Code + "] " + Subject + ": " + Message;
        }
    }

    /// <summary>All problems found in a validation pass (empty = valid). Collects everything, not just the first error.</summary>
    public sealed class AiDataValidationResult
    {
        private readonly List<AiDataIssue> issues = new List<AiDataIssue>();

        public IReadOnlyList<AiDataIssue> Issues => issues;
        public bool IsValid => issues.Count == 0;
        public int Count => issues.Count;

        public void Add(AiDataIssueCode code, string subject, string message)
        {
            issues.Add(new AiDataIssue(code, subject, message));
        }

        public void Merge(AiDataValidationResult other)
        {
            if (other != null) issues.AddRange(other.issues);
        }

        public bool Has(AiDataIssueCode code)
        {
            foreach (AiDataIssue i in issues)
                if (i.Code == code) return true;
            return false;
        }

        public int CountOf(AiDataIssueCode code)
        {
            int n = 0;
            foreach (AiDataIssue i in issues)
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
