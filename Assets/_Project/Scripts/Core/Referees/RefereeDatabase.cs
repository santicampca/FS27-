using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>The referee styles and the referees, found by id. A referee can only be added when its style exists; a repeated or invalid id is refused and nothing is overwritten.</summary>
    public sealed class RefereeDatabase
    {
        private readonly Dictionary<string, RefereeStyleDefinition> stylesById = new Dictionary<string, RefereeStyleDefinition>();
        private readonly List<RefereeStyleDefinition> styles = new List<RefereeStyleDefinition>();
        private readonly Dictionary<string, RefereeDefinition> byId = new Dictionary<string, RefereeDefinition>();
        private readonly List<RefereeDefinition> referees = new List<RefereeDefinition>();

        public IReadOnlyList<RefereeStyleDefinition> Styles { get { return styles; } }
        public IReadOnlyList<RefereeDefinition> Referees { get { return referees; } }
        public int Count { get { return referees.Count; } }

        public bool TryAddStyle(RefereeStyleDefinition style)
        {
            if (style == null || !DataRules.IsValidId(style.StyleId) || stylesById.ContainsKey(style.StyleId)) return false;
            stylesById.Add(style.StyleId, style);
            styles.Add(style);
            return true;
        }

        public bool ContainsStyle(string styleId) { return styleId != null && stylesById.ContainsKey(styleId); }

        public bool TryGetStyle(string styleId, out RefereeStyleDefinition style)
        {
            style = null;
            return styleId != null && stylesById.TryGetValue(styleId, out style);
        }

        public bool TryAddReferee(RefereeDefinition referee)
        {
            if (referee == null || !DataRules.IsValidId(referee.RefereeId) || byId.ContainsKey(referee.RefereeId) || !ContainsStyle(referee.StyleId)) return false;
            byId.Add(referee.RefereeId, referee);
            referees.Add(referee);
            return true;
        }

        public bool Contains(string refereeId) { return refereeId != null && byId.ContainsKey(refereeId); }

        public bool TryGet(string refereeId, out RefereeDefinition referee)
        {
            referee = null;
            return refereeId != null && byId.TryGetValue(refereeId, out referee);
        }
    }
}
