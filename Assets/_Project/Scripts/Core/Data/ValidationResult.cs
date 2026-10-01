using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>Every kind of data problem the validator can report. Tests and tools match on these, never on message text.</summary>
    public enum ValidationCode
    {
        // Players
        PlayerNull,
        PlayerIdInvalid,
        PlayerNameInvalid,
        PlayerNumberOutOfRange,
        PlayerRoleInvalid,
        PlayerAttributeOutOfRange,

        // Formations
        FormationNull,
        FormationIdInvalid,
        FormationNameInvalid,
        FormationPositionCountInvalid,
        FormationPlayerIndexOutOfRange,
        FormationPlayerIndexDuplicate,
        FormationRoleInvalid,
        FormationPositionOutOfRange,
        FormationGoalkeeperCountInvalid,

        // Teams
        TeamNull,
        TeamIdInvalid,
        TeamNameInvalid,
        TeamPlayerCountInvalid,
        TeamGoalkeeperCountInvalid,
        TeamDuplicatePlayerId,
        TeamDuplicateShirtNumber,
        TeamFormationIdInvalid,
        TeamFormationNotFound,
        TeamFormationGoalkeeperMismatch,

        // A match between two teams
        MatchSameTeam,
        MatchDuplicatePlayerId,

        // Player System V2 (appended, so earlier codes keep their values)
        PlayerShortNameInvalid,
        PlayerNationalityInvalid,
        PlayerTeamIdInvalid,
        PlayerAgeOutOfRange,
        PlayerHeightOutOfRange,
        PlayerWeightOutOfRange,
        PlayerFootInvalid,
        PlayerWeakFootOutOfRange,
        PlayerBodyTypeInvalid,
        TeamPlayerTeamIdMismatch,
        FormationZoneInvalid,
        TeamPlayerNotFound,
        TeamPlayerIdInvalid,
        TeamPlayerLookupMissing
    }

    public sealed class ValidationIssue
    {
        public ValidationCode Code { get; }
        /// <summary>What it is about, e.g. "team 'blue' > player 'blue-gk'".</summary>
        public string Subject { get; }
        public string Message { get; }

        public ValidationIssue(ValidationCode code, string subject, string message)
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

    /// <summary>All problems found by a validation pass (empty = valid). Collects everything instead of stopping at the first error.</summary>
    public sealed class ValidationResult
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues => issues;
        public bool IsValid => issues.Count == 0;
        public int Count => issues.Count;

        public void Add(ValidationCode code, string subject, string message)
        {
            issues.Add(new ValidationIssue(code, subject, message));
        }

        public void Merge(ValidationResult other)
        {
            if (other != null) issues.AddRange(other.issues);
        }

        public bool Has(ValidationCode code)
        {
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Code == code) return true;
            return false;
        }

        public int CountOf(ValidationCode code)
        {
            int n = 0;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Code == code) n++;
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
