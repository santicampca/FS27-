using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Starter 6v6 formations (1 goalkeeper + 5 field players). Examples only: they carry no behaviour. Each slot gives a
    /// starting spot, a zone and a broad role; the players placed there stay versatile.
    /// </summary>
    public static class DefaultFormations
    {
        public const string TwoOneTwo = "2-1-2";
        public const string OneTwoTwo = "1-2-2";
        public const string TwoTwoOne = "2-2-1";

        // Slot 0 is always the goalkeeper, a little off the goal line.
        private static FormationPosition Keeper() { return new FormationPosition(0, PlayerRole.Goalkeeper, PitchZone.Goal, 0.06f, 0.5f); }

        /// <summary>Two defenders, one midfielder, a winger and an attacker.</summary>
        public static FormationDefinition CreateTwoOneTwo()
        {
            return new FormationDefinition(TwoOneTwo, "2-1-2",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, PitchZone.Defense, 0.26f, 0.30f),
                new FormationPosition(2, PlayerRole.Defender, PitchZone.Defense, 0.26f, 0.70f),
                new FormationPosition(3, PlayerRole.Midfielder, PitchZone.Midfield, 0.48f, 0.50f),
                new FormationPosition(4, PlayerRole.Midfielder, PitchZone.Wing, 0.68f, 0.12f),
                new FormationPosition(5, PlayerRole.Forward, PitchZone.Attack, 0.78f, 0.60f));
        }

        /// <summary>One defender, two midfielders, a winger and an attacker.</summary>
        public static FormationDefinition CreateOneTwoTwo()
        {
            return new FormationDefinition(OneTwoTwo, "1-2-2",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, PitchZone.Defense, 0.25f, 0.50f),
                new FormationPosition(2, PlayerRole.Midfielder, PitchZone.Midfield, 0.48f, 0.30f),
                new FormationPosition(3, PlayerRole.Midfielder, PitchZone.Midfield, 0.48f, 0.70f),
                new FormationPosition(4, PlayerRole.Midfielder, PitchZone.Wing, 0.68f, 0.12f),
                new FormationPosition(5, PlayerRole.Forward, PitchZone.Attack, 0.78f, 0.60f));
        }

        /// <summary>Two defenders, two midfielders and a lone attacker.</summary>
        public static FormationDefinition CreateTwoTwoOne()
        {
            return new FormationDefinition(TwoTwoOne, "2-2-1",
                Keeper(),
                new FormationPosition(1, PlayerRole.Defender, PitchZone.Defense, 0.26f, 0.30f),
                new FormationPosition(2, PlayerRole.Defender, PitchZone.Defense, 0.26f, 0.70f),
                new FormationPosition(3, PlayerRole.Midfielder, PitchZone.Midfield, 0.50f, 0.30f),
                new FormationPosition(4, PlayerRole.Midfielder, PitchZone.Midfield, 0.50f, 0.70f),
                new FormationPosition(5, PlayerRole.Forward, PitchZone.Attack, 0.78f, 0.50f));
        }

        public static List<FormationDefinition> CreateAll()
        {
            return new List<FormationDefinition> { CreateTwoOneTwo(), CreateOneTwoTwo(), CreateTwoTwoOne() };
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
