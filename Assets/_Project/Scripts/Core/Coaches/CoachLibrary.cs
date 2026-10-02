using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Finds coaches by id. Adding a coach never changes the others; a repeated or invalid id is refused.</summary>
    public sealed class CoachLibrary
    {
        private readonly Dictionary<string, CoachDefinition> byId = new Dictionary<string, CoachDefinition>();
        private readonly List<CoachDefinition> ordered = new List<CoachDefinition>();

        public int Count { get { return ordered.Count; } }
        public IReadOnlyList<CoachDefinition> All { get { return ordered; } }

        public bool TryAdd(CoachDefinition coach)
        {
            if (coach == null || !DataRules.IsValidId(coach.CoachId) || byId.ContainsKey(coach.CoachId)) return false;
            byId.Add(coach.CoachId, coach);
            ordered.Add(coach);
            return true;
        }

        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }

        public bool TryGet(string id, out CoachDefinition coach)
        {
            coach = null;
            return id != null && byId.TryGetValue(id, out coach);
        }
    }
}
