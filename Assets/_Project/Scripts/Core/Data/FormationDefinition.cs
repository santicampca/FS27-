using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// One slot of a formation. <see cref="PlayerIndex"/> is the index of the player in the team's lineup
    /// (<c>TeamDefinition.PlayerIds</c>). <see cref="Relative"/> is where the slot sits, relative to the team's
    /// own half: X = depth (0 = own goal line, 1 = opponent's goal line), Y = width (0..1, 0.5 = centre).
    /// </summary>
    [Serializable]
    public struct FormationPosition
    {
        public int PlayerIndex;
        public PlayerRole Role;
        /// <summary>The zone this slot starts in. A starting context, not a fixed position: players stay versatile.</summary>
        public PitchZone Zone;
        public Vec2 Relative;

        public FormationPosition(int playerIndex, PlayerRole role, PitchZone zone, float depth, float width)
        {
            PlayerIndex = playerIndex;
            Role = role;
            Zone = zone;
            Relative = new Vec2(depth, width);
        }

        /// <summary>Same, with the zone taken from the broad role (Goalkeeper = Goal, Defender = Defense, Midfielder = Midfield, Forward = Attack).</summary>
        public FormationPosition(int playerIndex, PlayerRole role, float depth, float width)
            : this(playerIndex, role, PlayingProfileDefaults.ZoneForRole(role), depth, width)
        {
        }

        /// <summary>
        /// Converts the relative slot into field space (centre at 0,0; X = length, Y = width). A team attacking
        /// -X is the same layout turned 180 degrees, so left and right stay the same from the team's point of view.
        /// </summary>
        public Vec2 ToFieldPosition(FieldDimensions field, bool attacksPositiveX)
        {
            float x = (-0.5f + Relative.X) * field.Length;
            float y = (Relative.Y - 0.5f) * field.Width;
            return attacksPositiveX ? new Vec2(x, y) : new Vec2(-x, -y);
        }
    }

    /// <summary>A named layout for the 6 players of a team (1 goalkeeper + 5 field players). Data only: it describes where slots are, not how anyone behaves.</summary>
    [Serializable]
    public class FormationDefinition
    {
        public string Id;
        public string Name;
        public List<FormationPosition> Positions = new List<FormationPosition>();

        public FormationDefinition()
        {
        }

        public FormationDefinition(string id, string name, params FormationPosition[] positions)
        {
            Id = id;
            Name = name;
            Positions = new List<FormationPosition>(positions);
        }

        /// <summary>The slot for a lineup index, or false if the formation has none for it.</summary>
        public bool TryGetByPlayerIndex(int playerIndex, out FormationPosition position)
        {
            for (int i = 0; i < Positions.Count; i++)
            {
                if (Positions[i].PlayerIndex == playerIndex)
                {
                    position = Positions[i];
                    return true;
                }
            }
            position = default;
            return false;
        }

        /// <summary>Lineup index of the goalkeeper slot, or -1 if there is none.</summary>
        public int GoalkeeperIndex
        {
            get
            {
                for (int i = 0; i < Positions.Count; i++)
                    if (Positions[i].Role == PlayerRole.Goalkeeper) return Positions[i].PlayerIndex;
                return -1;
            }
        }
    }
}
