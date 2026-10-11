namespace FS27.Core
{
    /// <summary>
    /// The shape of a squad, in one place. A squad has 10 players: 6 ACTIVE (1 goalkeeper + 5 field players) and 4 SUBSTITUTES (1 goalkeeper + 3 field players), so every
    /// squad holds 2 goalkeepers and 8 field players. (The substitute keeper is what the match engine's red-card-to-the-keeper rule relies on.)
    /// The numbers agree with <see cref="MatchDefaults"/> (6 starters, 4 substitutes) and with the match squad's own validation.
    /// </summary>
    public static class RosterRules
    {
        public const int Size = MatchDefaults.StartersPerTeam + MatchDefaults.BenchPerTeam;      // 10
        public const int ActiveCount = MatchDefaults.StartersPerTeam;                              // 6
        public const int BenchCount = MatchDefaults.BenchPerTeam;                                  // 4

        public const int ActiveGoalkeepers = 1;
        public const int BenchGoalkeepers = 1;
        public const int GoalkeepersPerRoster = ActiveGoalkeepers + BenchGoalkeepers;              // 2
        public const int FieldPerRoster = Size - GoalkeepersPerRoster;                             // 8
        public const int ActiveField = ActiveCount - ActiveGoalkeepers;                            // 5
        public const int BenchField = BenchCount - BenchGoalkeepers;                               // 3

        // A plausible squad is not made of one kind of player: minimum field players per broad role among the 8 (and among the 5 active).
        public const int MinDefenders = 2;
        public const int MinMidfielders = 2;
        public const int MinForwards = 1;
        public const int MinActivePerRole = 1;

        /// <summary>Teams in the standard configuration: 11 official + 1 user team.</summary>
        public const int OfficialTeamCount = 11;
        public const int TeamCount = OfficialTeamCount + 1;
        public const int EliteTeamCount = 2;
    }
}
