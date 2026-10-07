using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// What a skill is about. The first three (<see cref="Dribble"/>, <see cref="SpecialPass"/>, <see cref="SpecialShot"/>) are the SPECIAL TECHNIQUES of the Phase-2 contract (a feint,
    /// a flourish pass, a special shot); the others (Phase 5) are the everyday capabilities of the catalogue. Numbers are explicit and append-only.
    /// </summary>
    public enum SkillKind
    {
        Dribble = 0,
        SpecialPass = 1,
        SpecialShot = 2,
        Passing = 3,
        Shooting = 4,
        FirstTouch = 5,
        Technique = 6,
        Acceleration = 7,
        Agility = 8,
        BallCarrying = 9,
        OffBall = 10,
        Interception = 11,
        Tackle = 12,
        Block = 13,
        Pressure = 14,
        Recovery = 15,
        Aerial = 16
    }

    /// <summary>The three families of field-player skills. Goalkeepers keep their own system (<see cref="GoalkeeperCapability"/>): none of it is a skill.</summary>
    public enum SkillCategory
    {
        Ball = 0,
        Movement = 1,
        Defensive = 2
    }

    /// <summary>One attribute floor a player must reach to own a skill (a sanity filter, not a bonus: it never changes any attribute).</summary>
    [Serializable]
    public struct SkillRequirement
    {
        public PlayerAttributeId Attribute;
        public int Minimum;
        public SkillRequirement(PlayerAttributeId attribute, int minimum) { Attribute = attribute; Minimum = minimum; }
    }

    /// <summary>
    /// A field-player skill, described as DATA (the catalogue lives in FS27_SKILLS_V1.json). It is a gameplay capability, not a label: the later AI and the actions read it.
    /// Behaviour is NOT implemented here. A skill may unlock one of the existing skill actions (<see cref="UnlocksAction"/>: SkillMove, SkillLobPass, CurvedShot); the
    /// others are passive capabilities the future AI and tactics will read. Named SkillDefinition (not "capability": that word is the goalkeeper's eight capabilities).
    /// </summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public string SkillId = "";
        public string Name = "";
        public SkillKind Kind;
        public string Description = "";

        // ---- Phase 5 (additive) ----
        public SkillCategory Category;
        /// <summary>The skill action this skill unlocks for its owner (the "SkillGranted" of an action request), or None for a passive skill.</summary>
        public FootballActionKind UnlocksAction = FootballActionKind.None;
        /// <summary>The official roles this skill fits. Empty = any. A player is only given the skill when his playing profile has at least one of them.</summary>
        public List<PlayerArchetype> Archetypes = new List<PlayerArchetype>();
        /// <summary>Attribute floors, ALL of which must hold. Only the 12 official attributes exist; <c>Reaction</c> is never a requirement.</summary>
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        /// <summary>The authoring tags (the normalised signature actions of the player databases) that mean this skill. Each tag belongs to exactly one skill.</summary>
        public List<string> SourceTags = new List<string>();
    }

    /// <summary>
    /// The skills one field player owns, by skill id. Built by <see cref="SkillAssigner"/> from the player's signature actions, attributes and roles; a goalkeeper has none
    /// (<see cref="PlayerDefinition"/> itself carries no skills: the set is looked up by player id through <see cref="IPlayerSkillLookup"/>).
    /// </summary>
    [Serializable]
    public sealed class PlayerSkillSet
    {
        public string PlayerId = "";
        public List<string> SkillIds = new List<string>();
    }
}
