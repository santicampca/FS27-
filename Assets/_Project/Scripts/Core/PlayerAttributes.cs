using System;

namespace FS27.Core
{
    /// <summary>
    /// Raw player ratings, 1..99. Pure data: it never decides behaviour by itself.
    /// <see cref="PlayerStats.Resolve"/> converts them to game values through <see cref="MovementTuning"/>.
    /// Phase 1 only uses Speed, Acceleration and Stamina; the rest are reserved.
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
        public int Reaction;

        public static PlayerAttributes CreateDefault()
        {
            return new PlayerAttributes
            {
                Speed = 70, Acceleration = 70, Stamina = 70,
                BallControl = 70, Passing = 70, Shooting = 70,
                Defense = 70, Strength = 70, Reaction = 70
            };
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
