using System;

namespace FS27.Core
{
    /// <summary>
    /// Static description of a player: who they are and how good they are. Pure data (fictional or, one day,
    /// licensed: the code does not care). Attributes reuse <see cref="PlayerAttributes"/>, so a definition can
    /// be handed straight to <see cref="PlayerStats.Resolve"/>. A Unity ScriptableObject will simply wrap one of these.
    /// </summary>
    [Serializable]
    public class PlayerDefinition
    {
        /// <summary>Stable identifier, unique across the whole game. Never reused, never shown to the player.</summary>
        public string Id;
        public string Name;
        public int Number;
        public PlayerRole Role;
        /// <summary>Speed, Acceleration, Stamina, BallControl, Passing, Shooting, Defense, Strength, Reaction (1..99).</summary>
        public PlayerAttributes Attributes = PlayerAttributes.CreateDefault();

        public PlayerDefinition()
        {
        }

        public PlayerDefinition(string id, string name, int number, PlayerRole role, PlayerAttributes attributes)
        {
            Id = id;
            Name = name;
            Number = number;
            Role = role;
            Attributes = attributes;
        }
    }
}
