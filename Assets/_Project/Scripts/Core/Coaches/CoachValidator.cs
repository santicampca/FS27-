using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>Every kind of coach data problem the validator can report. Tests and tools match on these, never on message text.</summary>
    public enum CoachIssueCode
    {
        CoachNull,
        IdInvalid,
        NameInvalid,
        FullNameInvalid,
        ShortNameInvalid,
        NationalityInvalid,
        GradeInvalid,
        RatingOutOfRange,
        FormationCountWrong,
        FormationIdInvalid,
        FormationIdDuplicate,
        FormationCoachMismatch,
        BaseFormationUnknown,
        BackLineMismatch,
        SlotCountWrong,
        SlotZoneInvalid,
        SlotZoneNotAllowedForBase,
        SlotRoleInvalid,
        DialOutOfRange,
        StyleInvalid,
        FormationNotPlayable,
        CompatibilityProfileMissing,
        CompatibilityWeightOutOfRange,
        CompatibilityProfileEmpty
    }

    public sealed class CoachIssue
    {
        public CoachIssueCode Code;
        public string CoachId = "";
        public string Message = "";
        public override string ToString() { return Code + " [" + CoachId + "]: " + Message; }
    }

    public sealed class CoachValidationResult
    {
        public readonly List<CoachIssue> Issues = new List<CoachIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
        public bool Has(CoachIssueCode code) { foreach (CoachIssue i in Issues) if (i.Code == code) return true; return false; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (CoachIssue i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Checks a coach against the project rules: valid grade, the number of formations the grade allows (A+/A/A- = 3, B+/B/B- = 2, C+/C = 1), formations that exist and are
    /// playable (1 goalkeeper + 5 field players, no new zone, no new role), a back line that matches the base formation, dials inside 0..100 and a usable compatibility profile.
    /// </summary>
    public static class CoachValidator
    {
        public static CoachValidationResult Validate(CoachDefinition coach, FormationLibrary baseFormations)
        {
            var result = new CoachValidationResult();
            if (coach == null) { Add(result, CoachIssueCode.CoachNull, "", "Missing."); return result; }
            string id = coach.CoachId ?? "";
            if (!DataRules.IsValidId(coach.CoachId)) Add(result, CoachIssueCode.IdInvalid, id, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(coach.Name)) Add(result, CoachIssueCode.NameInvalid, id, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");
            if (string.IsNullOrWhiteSpace(coach.FullName) || coach.FullName.Length > CoachDefinition.MaxFullNameLength) Add(result, CoachIssueCode.FullNameInvalid, id, "Full name must be 1-" + CoachDefinition.MaxFullNameLength + " characters.");
            if (!string.IsNullOrEmpty(coach.ShortName) && coach.ShortName.Length > PlayerRules.MaxShortNameLength) Add(result, CoachIssueCode.ShortNameInvalid, id, "Short name is at most " + PlayerRules.MaxShortNameLength + " characters.");
            if (!PlayerRules.IsValidNationalityCode(coach.NationalityCode)) Add(result, CoachIssueCode.NationalityInvalid, id, "Nationality code is 2-3 capital letters.");
            if (!CoachGradeInfo.IsValid(coach.Grade)) Add(result, CoachIssueCode.GradeInvalid, id, "Grade is not one of A+ A A- B+ B B- C+ C.");

            CheckRating(result, id, "TacticalIQ", coach.TacticalIQ); CheckRating(result, id, "AttackRating", coach.AttackRating); CheckRating(result, id, "DefenseRating", coach.DefenseRating);
            CheckRating(result, id, "Transitions", coach.Transitions); CheckRating(result, id, "Adaptability", coach.Adaptability);
            CheckRating(result, id, "PlayerDevelopment", coach.PlayerDevelopment); CheckRating(result, id, "Management", coach.Management); CheckRating(result, id, "Identity", coach.Identity);

            if (CoachGradeInfo.IsValid(coach.Grade) && coach.AvailableFormations.Count != CoachGradeInfo.FormationCount(coach.Grade))
                Add(result, CoachIssueCode.FormationCountWrong, id, "Grade " + CoachGradeInfo.ToLabel(coach.Grade) + " has " + CoachGradeInfo.FormationCount(coach.Grade) + " formation(s), found " + coach.AvailableFormations.Count + ".");

            var seen = new HashSet<string>();
            foreach (CoachFormationProfile f in coach.AvailableFormations) CheckFormation(result, coach, f, baseFormations, seen);
            CheckProfile(result, id, coach.CompatibilityProfile);
            return result;
        }

        private static void Add(CoachValidationResult r, CoachIssueCode code, string coachId, string message)
        {
            r.Issues.Add(new CoachIssue { Code = code, CoachId = coachId, Message = message });
        }

        private static void CheckRating(CoachValidationResult r, string id, string name, int value)
        {
            if (value < CoachDefinition.MinRating || value > CoachDefinition.MaxRating) Add(r, CoachIssueCode.RatingOutOfRange, id, name + " " + value + " is outside 0..100.");
        }

        private static void CheckDial(CoachValidationResult r, string id, string name, int value)
        {
            if (value < 0 || value > 100) Add(r, CoachIssueCode.DialOutOfRange, id, name + " " + value + " is outside 0..100.");
        }

        private static void CheckFormation(CoachValidationResult r, CoachDefinition coach, CoachFormationProfile f, FormationLibrary baseFormations, HashSet<string> seen)
        {
            string id = coach.CoachId ?? "";
            if (f == null) { Add(r, CoachIssueCode.FormationIdInvalid, id, "A formation is missing."); return; }
            if (!DataRules.IsValidId(f.FormationId)) Add(r, CoachIssueCode.FormationIdInvalid, id, "Formation id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            else if (!seen.Add(f.FormationId)) Add(r, CoachIssueCode.FormationIdDuplicate, id, "Formation id '" + f.FormationId + "' is repeated.");
            if (f.CoachId != coach.CoachId) Add(r, CoachIssueCode.FormationCoachMismatch, id, "Formation '" + f.FormationId + "' belongs to '" + f.CoachId + "'.");
            if (!DataRules.IsValidName(f.DisplayName)) Add(r, CoachIssueCode.FormationIdInvalid, id, "Formation '" + f.FormationId + "' needs a display name (1-" + DataRules.MaxNameLength + " characters).");
            if (!System.Enum.IsDefined(typeof(Mentality), f.Mentality) || !System.Enum.IsDefined(typeof(BuildUpStyle), f.BuildUpStyle) || !System.Enum.IsDefined(typeof(PressingStyle), f.PressingStyle)
                || !System.Enum.IsDefined(typeof(TransitionStyle), f.TransitionStyle)) Add(r, CoachIssueCode.StyleInvalid, id, "Formation '" + f.FormationId + "' has an undefined style value.");
            CheckDial(r, id, "Width", f.Width); CheckDial(r, id, "Tempo", f.Tempo); CheckDial(r, id, "Risk", f.Risk); CheckDial(r, id, "DefensiveLine", f.DefensiveLine);

            FormationDefinition baseDef = null;
            if (baseFormations == null || !baseFormations.TryGet(f.BaseFormation, out baseDef)) { Add(r, CoachIssueCode.BaseFormationUnknown, id, "Base formation '" + f.BaseFormation + "' does not exist."); return; }

            if (f.Slots.Count != CoachFormationProfile.FieldSlots) { Add(r, CoachIssueCode.SlotCountWrong, id, "Formation '" + f.FormationId + "' needs exactly " + CoachFormationProfile.FieldSlots + " field slots, found " + f.Slots.Count + "."); return; }

            var baseField = new List<FormationPosition>();
            foreach (FormationPosition p in baseDef.Positions) if (p.Role != PlayerRole.Goalkeeper) baseField.Add(p);
            baseField.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

            int defenders = 0;
            for (int i = 0; i < f.Slots.Count; i++)
            {
                CoachFormationSlot s = f.Slots[i];
                if (!System.Enum.IsDefined(typeof(PitchZone), s.Zone) || s.Zone == PitchZone.Goal) { Add(r, CoachIssueCode.SlotZoneInvalid, id, "Slot " + (i + 1) + " of '" + f.FormationId + "' must use Defense, Midfield, Wing or Attack."); continue; }
                if (!System.Enum.IsDefined(typeof(PlayerArchetype), s.Role)) Add(r, CoachIssueCode.SlotRoleInvalid, id, "Slot " + (i + 1) + " of '" + f.FormationId + "' is not one of the 12 official roles.");
                if (s.Zone == PitchZone.Defense) defenders++;
                bool baseDefends = baseField[i].Zone == PitchZone.Defense;
                if (baseDefends != (s.Zone == PitchZone.Defense)) Add(r, CoachIssueCode.SlotZoneNotAllowedForBase, id, "Slot " + (i + 1) + " of '" + f.FormationId + "': defensive slots of the base formation stay Defense and the others stay Midfield, Wing or Attack.");
            }
            if ((int)f.BackLineType != defenders) Add(r, CoachIssueCode.BackLineMismatch, id, "Formation '" + f.FormationId + "' declares a back line of " + (int)f.BackLineType + " but has " + defenders + " defensive slots.");
            int baseDefenders = 0;
            foreach (FormationPosition p in baseField) if (p.Zone == PitchZone.Defense) baseDefenders++;
            if (baseDefenders != defenders) Add(r, CoachIssueCode.BackLineMismatch, id, "Formation '" + f.FormationId + "' has " + defenders + " defensive slots but its base formation has " + baseDefenders + ".");

            FormationDefinition built = f.ToFormationDefinition(baseDef);
            if (built == null || !DataValidator.ValidateFormation(built).IsValid) Add(r, CoachIssueCode.FormationNotPlayable, id, "Formation '" + f.FormationId + "' is not a valid 6v6 formation (1 goalkeeper + 5 field players).");
        }

        private static void CheckProfile(CoachValidationResult r, string id, CoachCompatibilityProfile cp)
        {
            if (cp == null) { Add(r, CoachIssueCode.CompatibilityProfileMissing, id, "A coach needs a compatibility profile."); return; }
            foreach (WeightedRole w in cp.Roles) if (w.Weight < 0 || w.Weight > 100 || !System.Enum.IsDefined(typeof(PlayerArchetype), w.Role)) Add(r, CoachIssueCode.CompatibilityWeightOutOfRange, id, "Role weight '" + w.Role + "' " + w.Weight + " is outside 0..100 or not an official role.");
            foreach (WeightedZone w in cp.Zones) if (w.Weight < 0 || w.Weight > 100 || !System.Enum.IsDefined(typeof(PitchZone), w.Zone)) Add(r, CoachIssueCode.CompatibilityWeightOutOfRange, id, "Zone weight '" + w.Zone + "' " + w.Weight + " is outside 0..100 or not a zone.");
            foreach (PlayerAttributeId a in PlayerAttributeInfo.All) if (cp.Attributes != null && cp.Attributes.Get(a) < 0f) Add(r, CoachIssueCode.CompatibilityWeightOutOfRange, id, "Attribute weight " + a + " is negative.");
            foreach (BehaviourPreference b in new[] { cp.Risk, cp.Creativity, cp.Aggression })
                if (b.Target < 0 || b.Target > 100 || b.Importance < 0 || b.Importance > 100) Add(r, CoachIssueCode.CompatibilityWeightOutOfRange, id, "A behaviour preference is outside 0..100.");
            foreach (int imp in new[] { cp.RoleImportance, cp.ZoneImportance, cp.AttributeImportance, cp.BehaviourImportance, cp.BodyImportance })
                if (imp < 0 || imp > 100) Add(r, CoachIssueCode.CompatibilityWeightOutOfRange, id, "A component importance is outside 0..100.");
            bool usable = (cp.RoleImportance > 0 && cp.Roles.Count > 0) || (cp.ZoneImportance > 0 && cp.Zones.Count > 0) || (cp.AttributeImportance > 0 && cp.HasAttributes)
                          || (cp.BehaviourImportance > 0 && cp.Risk.Importance + cp.Creativity.Importance + cp.Aggression.Importance > 0) || (cp.BodyImportance > 0 && cp.BodyTypes.Count > 0);
            if (!usable) Add(r, CoachIssueCode.CompatibilityProfileEmpty, id, "The compatibility profile has no usable part.");
        }
    }
}
