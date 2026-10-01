namespace FS27.Core
{
    /// <summary>The numbers that define what a valid 5v5 team is. One place, so rules are easy to find and change.</summary>
    public static class DataRules
    {
        /// <summary>Players on the pitch per team (1 goalkeeper + 4 outfield).</summary>
        public const int PlayersPerTeam = 5;
        public const int GoalkeepersPerTeam = 1;

        public const int MinShirtNumber = 1;
        public const int MaxShirtNumber = 99;

        public const int MaxIdLength = 64;
        public const int MaxNameLength = 32;

        /// <summary>Formation coordinates are relative: 0..1 on both axes.</summary>
        public const float MinRelative = 0f;
        public const float MaxRelative = 1f;

        /// <summary>A stable id is non-empty, has no whitespace and is not longer than <see cref="MaxIdLength"/>.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength) return false;
            for (int i = 0; i < id.Length; i++)
                if (char.IsWhiteSpace(id[i])) return false;
            return true;
        }

        public static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength;
        }
    }
}
