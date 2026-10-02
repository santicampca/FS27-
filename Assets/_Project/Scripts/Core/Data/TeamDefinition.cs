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
        /// <summary>
        /// LEGACY 6-player lineup of the original data core (kept so the older data, validators and the sandbox keep working). It is NOT where a drawn squad lives:
        /// a team's 10 players are its <see cref="TeamRoster"/> (the authority), generated for one concrete draw. Phase-2 teams leave this empty.
        /// </summary>
        public List<string> PlayerIds = new List<string>();
        public string FormationId;

        // ---- identity and configuration (Phase 2). Data only: no assets, no behaviour. ----
        /// <summary>Short name for tight spaces (e.g. a scoreboard).</summary>
        public string ShortName = "";
        /// <summary>An official FS27 team, or the user's team.</summary>
        public TeamKind Kind = TeamKind.Official;
        /// <summary>How strong the team is meant to be. It steers the draw (squad quality comes from WHICH players it gets, never from changing a player). Configurable; the user team is never Elite by default.</summary>
        public TeamLevel Level = TeamLevel.Balanced;
        /// <summary>The coach, by id in the coach library. Empty = none assigned (e.g. a user team that has not chosen one). A reference only: the coach boost is not applied to the simulation.</summary>
        public string CoachId = "";
        /// <summary>A reference to the team's badge (an id or a path; empty = none). No badge art exists yet.</summary>
        public string BadgeRef = "";
        /// <summary>The team's theme in a few words (identity text, not a rule).</summary>
        public string Theme = "";
        public string Description = "";

        public bool IsUserTeam => Kind == TeamKind.User;
        public bool IsElite => Level == TeamLevel.Elite;

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
