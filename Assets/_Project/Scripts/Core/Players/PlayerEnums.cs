using System;

namespace FS27.Core
{
    public enum PreferredFoot
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Coarse body build. Pure data: a future presentation system picks a character variant from it.
    /// Kept short on purpose; height and weight carry the finer detail.
    /// </summary>
    public enum BodyType
    {
        Light = 0,
        Athletic = 1,
        Strong = 2,
        Tall = 3,
        Compact = 4
    }

    /// <summary>
    /// Presentation-only label of a player card. It has no effect on gameplay and no economy attached
    /// (no market, packs or currency exist in FS27).
    /// </summary>
    public enum CardType
    {
        Standard = 0,
        Rare = 1,
        Special = 2,
        Legend = 3,
        Event = 4,
        Custom = 5
    }

    /// <summary>Limits for the player identity and physical data.</summary>
    public static class PlayerRules
    {
        public const int MinAge = 15;
        public const int MaxAge = 45;
        public const int MinHeightCm = 140;
        public const int MaxHeightCm = 220;
        public const int MinWeightKg = 40;
        public const int MaxWeightKg = 140;
        public const int MinWeakFoot = 0;
        public const int MaxWeakFoot = 100;
        public const int MaxShortNameLength = 12;

        /// <summary>A nationality code is 2-3 capital letters (e.g. "AR"); fictional codes are allowed. Empty means "not set".</summary>
        public static bool IsValidNationalityCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return true;
            if (code.Length < 2 || code.Length > 3) return false;
            foreach (char c in code)
                if (c < 'A' || c > 'Z') return false;
            return true;
        }
    }
}
