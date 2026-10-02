using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// HOW a team likes to play, independent of HOW WELL it plays (<see cref="DifficultyDefinition"/>).
    /// Two teams on the same difficulty with different styles play differently; the same style on different
    /// difficulties plays the same ideas better or worse. Nothing here is a skill, and nothing here touches attributes.
    /// Only the neutral "Balanced" style exists for now; High Press, Counter Attack, Possession, Deep Defence, Wing Play and
    /// Direct Play will be additional data entries, with no change to the system.
    /// </summary>
    [Serializable]
    public class TeamStyleDefinition
    {
        public string Id;
        public string Name;

        /// <summary>Scales the distance at which the team starts pressing (&gt;1 presses further up the pitch). 1 = neutral.</summary>
        public float PressTriggerScale = 1f;
        /// <summary>0 = defends deep, 1 = defends high. 0.5 = neutral.</summary>
        public float DefensiveLineHeight = 0.5f;
        /// <summary>0 = narrow attack, 1 = uses the full width.</summary>
        public float AttackWidth = 0.5f;
        /// <summary>0 = short passing build-up, 1 = long/direct balls.</summary>
        public float Directness = 0.5f;
        /// <summary>0 = slow, patient; 1 = fast, vertical.</summary>
        public float Tempo = 0.5f;
        /// <summary>0 = safe play, 1 = takes risks.</summary>
        public float RiskTaking = 0.5f;

        public TeamStyleDefinition()
        {
        }

        public TeamStyleDefinition(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public const float MinPressTriggerScale = 0.5f;
        public const float MaxPressTriggerScale = 1.5f;
    }

    public static class DefaultTeamStyles
    {
        public const string BalancedId = "balanced";

        public static TeamStyleDefinition CreateBalanced()
        {
            return new TeamStyleDefinition(BalancedId, "Equilibrado");
        }
    }

    public sealed class TeamStyleLibrary
    {
        private readonly Dictionary<string, TeamStyleDefinition> byId = new Dictionary<string, TeamStyleDefinition>();
        private readonly List<TeamStyleDefinition> ordered = new List<TeamStyleDefinition>();

        public int Count => ordered.Count;
        public IReadOnlyList<TeamStyleDefinition> All => ordered;

        public bool TryAdd(TeamStyleDefinition style)
        {
            if (style == null || !DataRules.IsValidId(style.Id) || byId.ContainsKey(style.Id)) return false;
            byId.Add(style.Id, style);
            ordered.Add(style);
            return true;
        }

        public bool TryGet(string id, out TeamStyleDefinition style)
        {
            if (id == null)
            {
                style = null;
                return false;
            }
            return byId.TryGetValue(id, out style);
        }

        public static TeamStyleLibrary CreateDefault()
        {
            var lib = new TeamStyleLibrary();
            lib.TryAdd(DefaultTeamStyles.CreateBalanced());
            return lib;
        }
    }

    public static class TeamStyleValidator
    {
        public static AiDataValidationResult Validate(TeamStyleDefinition s)
        {
            var r = new AiDataValidationResult();
            if (s == null)
            {
                r.Add(AiDataIssueCode.StyleNull, "style", "Style is null.");
                return r;
            }

            string who = "style '" + (s.Id ?? "<no id>") + "'";
            if (!DataRules.IsValidId(s.Id))
                r.Add(AiDataIssueCode.StyleIdInvalid, who, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(s.Name))
                r.Add(AiDataIssueCode.StyleNameInvalid, who, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            if (!(s.PressTriggerScale >= TeamStyleDefinition.MinPressTriggerScale && s.PressTriggerScale <= TeamStyleDefinition.MaxPressTriggerScale))
                r.Add(AiDataIssueCode.StyleValueOutOfRange, who, "PressTriggerScale = " + s.PressTriggerScale + " is outside " + TeamStyleDefinition.MinPressTriggerScale + ".." + TeamStyleDefinition.MaxPressTriggerScale + ".");
            Unit(r, who, "DefensiveLineHeight", s.DefensiveLineHeight);
            Unit(r, who, "AttackWidth", s.AttackWidth);
            Unit(r, who, "Directness", s.Directness);
            Unit(r, who, "Tempo", s.Tempo);
            Unit(r, who, "RiskTaking", s.RiskTaking);
            return r;
        }

        private static void Unit(AiDataValidationResult r, string who, string field, float v)
        {
            if (!(v >= 0f && v <= 1f))
                r.Add(AiDataIssueCode.StyleValueOutOfRange, who, field + " = " + v + " is outside 0..1.");
        }
    }

    /// <summary>
    /// A team's AI setup for one match: WHICH difficulty and WHICH style, by id. Kept apart from <see cref="TeamDefinition"/>
    /// (which is not modified) and from both definitions, so each can change independently.
    /// </summary>
    [Serializable]
    public class TeamAiProfile
    {
        public string TeamId;
        public string DifficultyId = DefaultDifficulties.ProfessionalId;
        public string StyleId = DefaultTeamStyles.BalancedId;

        public TeamAiProfile()
        {
        }

        public TeamAiProfile(string teamId, string difficultyId, string styleId)
        {
            TeamId = teamId;
            DifficultyId = difficultyId;
            StyleId = styleId;
        }
    }

    /// <summary>The two independent halves of a team's AI, resolved. They are shared references and are never modified here.</summary>
    public struct ResolvedTeamAi
    {
        public DifficultyDefinition Difficulty;
        public TeamStyleDefinition Style;
    }

    public static class AiProfileResolver
    {
        public static bool TryResolve(TeamAiProfile profile, DifficultyLibrary difficulties, TeamStyleLibrary styles, out ResolvedTeamAi resolved)
        {
            resolved = default;
            if (profile == null || difficulties == null || styles == null) return false;
            if (!difficulties.TryGet(profile.DifficultyId, out DifficultyDefinition d)) return false;
            if (!styles.TryGet(profile.StyleId, out TeamStyleDefinition s)) return false;
            resolved = new ResolvedTeamAi { Difficulty = d, Style = s };
            return true;
        }
    }
}
