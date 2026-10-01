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
    /// A team: identity, colours, its players and the formation it starts with.
    /// The order of <see cref="Players"/> is the lineup: the player at index i takes the formation slot with PlayerIndex i.
    /// The formation is referenced by id (resolved through a <see cref="FormationLibrary"/>), so teams and formations
    /// stay independent assets.
    /// </summary>
    [Serializable]
    public class TeamDefinition
    {
        public string Id;
        public string Name;
        public TeamColors Colors;
        public List<PlayerDefinition> Players = new List<PlayerDefinition>();
        public string FormationId;

        public TeamDefinition()
        {
        }

        public TeamDefinition(string id, string name, TeamColors colors, string formationId, params PlayerDefinition[] players)
        {
            Id = id;
            Name = name;
            Colors = colors;
            FormationId = formationId;
            Players = new List<PlayerDefinition>(players);
        }

        /// <summary>Lineup index of the first goalkeeper, or -1.</summary>
        public int GoalkeeperIndex
        {
            get
            {
                for (int i = 0; i < Players.Count; i++)
                    if (Players[i] != null && Players[i].Role == PlayerRole.Goalkeeper) return i;
                return -1;
            }
        }
    }
}
