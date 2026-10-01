using System;

namespace FS27.Core
{
    /// <summary>
    /// Raw player ratings, 1..99, and the single source of truth for what a player can do. Pure data: it never decides
    /// behaviour by itself. <see cref="PlayerStats.Resolve"/> converts them to game values through <see cref="MovementTuning"/>.
    ///
    /// Player System V2 core set (12): Speed, Acceleration, Agility, Strength, Stamina, Shooting, Finishing, Passing,
    /// Control (stored in <see cref="BallControl"/>), Dribbling, Technique, Defense.
    /// <see cref="Reaction"/> is kept from the original set because the AI/difficulty infrastructure reads it;
    /// it is not part of the V2 ratings (overall, role and zone ratings).
    /// </summary>
    [Serializable]
    public struct PlayerAttributes
    {
        public const int Min = 1;
        public const int Max = 99;

        public int Speed;
        public int Acceleration;
        public int Stamina;
        public int BallControl;
        public int Passing;
        public int Shooting;
        public int Defense;
        public int Strength;
        /// <summary>Legacy attribute kept for the AI/difficulty infrastructure. Not used by V2 ratings.</summary>
        public int Reaction;

        // Added by Player System V2 (appended, so earlier fields keep their meaning).
        public int Agility;
        public int Finishing;
        public int Dribbling;
        public int Technique;

        /// <summary>The "Control" attribute of the V2 set. It is an alias of <see cref="BallControl"/>: one value, one storage.</summary>
        public int Control
        {
            get { return BallControl; }
            set { BallControl = value; }
        }

        public static PlayerAttributes CreateDefault()
        {
            return new PlayerAttributes
            {
                Speed = 70, Acceleration = 70, Stamina = 70,
                BallControl = 70, Passing = 70, Shooting = 70,
                Defense = 70, Strength = 70, Reaction = 70,
                Agility = 70, Finishing = 70, Dribbling = 70, Technique = 70
            };
        }

        /// <summary>Reads one of the 12 core attributes by id.</summary>
        public int GetValue(PlayerAttributeId id)
        {
            switch (id)
            {
                case PlayerAttributeId.Speed: return Speed;
                case PlayerAttributeId.Acceleration: return Acceleration;
                case PlayerAttributeId.Agility: return Agility;
                case PlayerAttributeId.Strength: return Strength;
                case PlayerAttributeId.Stamina: return Stamina;
                case PlayerAttributeId.Shooting: return Shooting;
                case PlayerAttributeId.Finishing: return Finishing;
                case PlayerAttributeId.Passing: return Passing;
                case PlayerAttributeId.Control: return BallControl;
                case PlayerAttributeId.Dribbling: return Dribbling;
                case PlayerAttributeId.Technique: return Technique;
                default: return Defense;
            }
        }

        /// <summary>A copy with one core attribute changed (this struct is not modified).</summary>
        public PlayerAttributes With(PlayerAttributeId id, int value)
        {
            PlayerAttributes a = this;
            switch (id)
            {
                case PlayerAttributeId.Speed: a.Speed = value; break;
                case PlayerAttributeId.Acceleration: a.Acceleration = value; break;
                case PlayerAttributeId.Agility: a.Agility = value; break;
                case PlayerAttributeId.Strength: a.Strength = value; break;
                case PlayerAttributeId.Stamina: a.Stamina = value; break;
                case PlayerAttributeId.Shooting: a.Shooting = value; break;
                case PlayerAttributeId.Finishing: a.Finishing = value; break;
                case PlayerAttributeId.Passing: a.Passing = value; break;
                case PlayerAttributeId.Control: a.BallControl = value; break;
                case PlayerAttributeId.Dribbling: a.Dribbling = value; break;
                case PlayerAttributeId.Technique: a.Technique = value; break;
                default: a.Defense = value; break;
            }
            return a;
        }

        /// <summary>Rating 1..99 mapped to 0..1.</summary>
        public static float Normalize(int rating)
        {
            if (rating < Min) rating = Min;
            if (rating > Max) rating = Max;
            return (rating - Min) / (float)(Max - Min);
        }
    }
}
