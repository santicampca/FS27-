using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum SkillKind
    {
        Dribble = 0,
        SpecialPass = 1,
        SpecialShot = 2
    }

    /// <summary>
    /// CONTRACT ONLY (Phase 2). A technical skill a player may own (a feint, a flourish pass, a special shot), described as data. There is no skill catalogue yet and no behaviour: the catalogue,
    /// the conditions and the assignment to players are a later, separate phase. Named SkillDefinition (not "capability": that word is the goalkeeper's eight capabilities).
    /// </summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public string SkillId = "";
        public string Name = "";
        public SkillKind Kind;
        public string Description = "";
    }

    /// <summary>
    /// CONTRACT ONLY (Phase 2). The skills one player owns, by skill id. Nothing creates one yet: no skill is assigned to any player (that is the later skills phase), and
    /// <see cref="PlayerDefinition"/> carries no skills.
    /// </summary>
    [Serializable]
    public sealed class PlayerSkillSet
    {
        public string PlayerId = "";
        public List<string> SkillIds = new List<string>();
    }
}
