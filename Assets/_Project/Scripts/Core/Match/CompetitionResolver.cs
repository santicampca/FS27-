namespace FS27.Core
{
    /// <summary>What happens when a stage of the match is over.</summary>
    public enum StageOutcome
    {
        /// <summary>The match is over (a winner, or a draw that stands).</summary>
        Finish = 0,
        ExtraTime,
        PenaltyShootout
    }

    /// <summary>
    /// How a draw is resolved. It depends ONLY on the competition (<see cref="CompetitionMatchType"/>) and the settings: it takes no
    /// <see cref="GameMode"/>, so Arena and Classic resolve a draw identically and the two concepts cannot get mixed up.
    /// </summary>
    public static class CompetitionResolver
    {
        /// <summary>The regulation time is over. A winner ends it; a draw in a Normal match ends it as a draw; a draw in a Knockout goes on.</summary>
        public static StageOutcome AfterRegulation(int home, int away, CompetitionMatchType type, MatchSettings settings)
        {
            if (home != away || type != CompetitionMatchType.Knockout) return StageOutcome.Finish;
            if (settings.ExtraTimeEnabled) return StageOutcome.ExtraTime;
            return settings.ShootoutEnabled ? StageOutcome.PenaltyShootout : StageOutcome.Finish;
        }

        /// <summary>Extra time is over (only reached in a Knockout). A winner ends it; a draw goes to the shoot-out.</summary>
        public static StageOutcome AfterExtraTime(int home, int away, MatchSettings settings)
        {
            if (home != away) return StageOutcome.Finish;
            return settings.ShootoutEnabled ? StageOutcome.PenaltyShootout : StageOutcome.Finish;
        }
    }
}
