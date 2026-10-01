using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Plain 8-bit RGB colour (Core has no UnityEngine.Color).</summary>
    [Serializable]
    public struct ColorRgb
    {
        public byte R;
        public byte G;
        public byte B;

        public ColorRgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>Basic team look. Kits, crests and the like will extend this later.</summary>
    [Serializable]
    public struct TeamColors
    {
        public ColorRgb Primary;
        public ColorRgb Secondary;

        public TeamColors(ColorRgb primary, ColorRgb secondary)
        {
            Primary = primary;
            Secondary = secondary;
        }
    }

    /// <summary>
    /// A team: identity, colours, the ids of its players and the formation it starts with. FS27 is 6v6, so a playable team
    /// lists 6 ids (1 goalkeeper + 5 field players, see <see cref="DataRules"/>).
    /// The team holds only player IDS, never players: the single logical <see cref="PlayerDefinition"/> of each id lives in the
    /// <see cref="PlayerLibrary"/>, so editing a player, moving them to another team or reusing them needs no copies.
    /// The order of <see cref="PlayerIds"/> is the lineup: the player at index i takes the formation slot with PlayerIndex i.
    /// The formation is referenced by id (resolved through a <see cref="FormationLibrary"/>), so teams and formations
    /// stay independent assets.
    /// </summary>
    [Serializable]
    public class TeamDefinition
    {
        public string Id;
        public string Name;
        public TeamColors Colors;
        public List<string> PlayerIds = new List<string>();
        public string FormationId;

        public TeamDefinition()
        {
        }

        public TeamDefinition(string id, string name, TeamColors colors, string formationId, params string[] playerIds)
        {
            Id = id;
            Name = name;
            Colors = colors;
            FormationId = formationId;
            PlayerIds = new List<string>(playerIds);
        }

        /// <summary>Lineup index of the first goalkeeper (looking the players up), or -1.</summary>
        public int GoalkeeperIndex(IPlayerLookup players)
        {
            if (players == null || PlayerIds == null) return -1;
            for (int i = 0; i < PlayerIds.Count; i++)
                if (PlayerIds[i] != null && players.TryGet(PlayerIds[i], out PlayerDefinition p) && p != null && p.Role == PlayerRole.Goalkeeper) return i;
            return -1;
        }
    }
}
