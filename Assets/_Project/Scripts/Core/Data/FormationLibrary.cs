using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Starter 5v5 formations (1 goalkeeper + 4 outfield). Examples only: they carry no behaviour.</summary>
    public static class DefaultFormations
    {
        public const string TwoTwo = "2-2";
        public const string Diamond = "1-2-1";
        public const string TwoOneOne = "2-1-1";

        // Slot 0 is always the goalkeeper, a little off the goal line.
        private static FormationPosition Keeper() { return new FormationPosition(0, PlayerRole.Goalkeeper, 0.06f, 0.5f); }

        public static FormationDefinition CreateTwoTwo()
        {
            return new FormationDefinition(TwoTwo, "2-2",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, 0.28f, 0.28f),
                new FormationPosition(2, PlayerRole.Defender, 0.28f, 0.72f),
                new FormationPosition(3, PlayerRole.Forward, 0.70f, 0.28f),
                new FormationPosition(4, PlayerRole.Forward, 0.70f, 0.72f));
        }

        public static FormationDefinition CreateDiamond()
        {
            return new FormationDefinition(Diamond, "1-2-1 Diamond",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, 0.25f, 0.50f),
                new FormationPosition(2, PlayerRole.Midfielder, 0.50f, 0.20f),
                new FormationPosition(3, PlayerRole.Midfielder, 0.50f, 0.80f),
                new FormationPosition(4, PlayerRole.Forward, 0.78f, 0.50f));
        }

        public static FormationDefinition CreateTwoOneOne()
        {
            return new FormationDefinition(TwoOneOne, "2-1-1",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, 0.26f, 0.30f),
                new FormationPosition(2, PlayerRole.Defender, 0.26f, 0.70f),
                new FormationPosition(3, PlayerRole.Midfielder, 0.50f, 0.50f),
                new FormationPosition(4, PlayerRole.Forward, 0.78f, 0.50f));
        }

        public static List<FormationDefinition> CreateAll()
        {
            return new List<FormationDefinition> { CreateTwoTwo(), CreateDiamond(), CreateTwoOneOne() };
        }
    }

    /// <summary>Finds formations by id. Adding a formation never changes existing ones.</summary>
    public sealed class FormationLibrary
    {
        private readonly Dictionary<string, FormationDefinition> byId = new Dictionary<string, FormationDefinition>();
        private readonly List<FormationDefinition> ordered = new List<FormationDefinition>();

        public int Count => ordered.Count;
        public IReadOnlyList<FormationDefinition> All => ordered;

        /// <summary>Adds a formation. Returns false (and adds nothing) if it is null, has no id or the id is taken.</summary>
        public bool TryAdd(FormationDefinition formation)
        {
            if (formation == null || !DataRules.IsValidId(formation.Id) || byId.ContainsKey(formation.Id)) return false;
            byId.Add(formation.Id, formation);
            ordered.Add(formation);
            return true;
        }

        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }

        public bool TryGet(string id, out FormationDefinition formation)
        {
            if (id == null)
            {
                formation = null;
                return false;
            }
            return byId.TryGetValue(id, out formation);
        }

        public static FormationLibrary CreateDefault()
        {
            var lib = new FormationLibrary();
            foreach (FormationDefinition f in DefaultFormations.CreateAll()) lib.TryAdd(f);
            return lib;
        }
    }
}
