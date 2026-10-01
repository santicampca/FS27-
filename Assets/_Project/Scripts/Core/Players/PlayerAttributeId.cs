namespace FS27.Core
{
    /// <summary>
    /// The 12 core attributes of Player System V2, in display order. This enum only NAMES them (for weights, cards,
    /// tooling); the values live in <see cref="PlayerAttributes"/>, the single source of truth.
    /// "Control" is stored in the existing field <c>PlayerAttributes.BallControl</c> (same attribute, no second copy).
    /// </summary>
    public enum PlayerAttributeId
    {
        // Physical
        Speed = 0,
        Acceleration = 1,
        Agility = 2,
        Strength = 3,
        Stamina = 4,
        // Attack
        Shooting = 5,
        Finishing = 6,
        Passing = 7,
        // Control
        Control = 8,
        Dribbling = 9,
        Technique = 10,
        // Defense
        Defense = 11
    }

    public enum PlayerAttributeGroup
    {
        Physical,
        Attack,
        Control,
        Defense
    }

    public static class PlayerAttributeInfo
    {
        public const int Count = 12;

        /// <summary>All 12 core attributes, in order.</summary>
        public static readonly PlayerAttributeId[] All =
        {
            PlayerAttributeId.Speed, PlayerAttributeId.Acceleration, PlayerAttributeId.Agility, PlayerAttributeId.Strength, PlayerAttributeId.Stamina,
            PlayerAttributeId.Shooting, PlayerAttributeId.Finishing, PlayerAttributeId.Passing,
            PlayerAttributeId.Control, PlayerAttributeId.Dribbling, PlayerAttributeId.Technique,
            PlayerAttributeId.Defense
        };

        public static PlayerAttributeGroup GroupOf(PlayerAttributeId id)
        {
            switch (id)
            {
                case PlayerAttributeId.Speed:
                case PlayerAttributeId.Acceleration:
                case PlayerAttributeId.Agility:
                case PlayerAttributeId.Strength:
                case PlayerAttributeId.Stamina:
                    return PlayerAttributeGroup.Physical;
                case PlayerAttributeId.Shooting:
                case PlayerAttributeId.Finishing:
                case PlayerAttributeId.Passing:
                    return PlayerAttributeGroup.Attack;
                case PlayerAttributeId.Control:
                case PlayerAttributeId.Dribbling:
                case PlayerAttributeId.Technique:
                    return PlayerAttributeGroup.Control;
                default:
                    return PlayerAttributeGroup.Defense;
            }
        }
    }
}
