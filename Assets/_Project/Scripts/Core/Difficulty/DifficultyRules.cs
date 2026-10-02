namespace FS27.Core
{
    /// <summary>
    /// Hard limits no difficulty may cross, so that no setting can produce superhuman or "psychic" AI.
    /// Validation rejects any <see cref="DifficultyDefinition"/> outside them.
    /// </summary>
    public static class DifficultyRules
    {
        /// <summary>Nobody reacts faster than this to something they just perceived (seconds).</summary>
        public const float AbsoluteMinReactionSeconds = 0.10f;
        public const float MaxReactionSeconds = 2.0f;

        /// <summary>The AI never re-decides more often than this (seconds).</summary>
        public const float AbsoluteMinDecisionInterval = 0.10f;
        public const float MaxDecisionInterval = 2.0f;

        /// <summary>The AI never anticipates further ahead than this (seconds): no knowledge of the future.</summary>
        public const float AbsoluteMaxLookaheadSeconds = 1.0f;

        public const float MinVisionAngleDegrees = 60f;
        public const float MaxVisionAngleDegrees = 220f;

        public const float MaxDistanceMeters = 40f;

        public const int MinOptionsConsidered = 1;
        public const int MaxOptionsConsidered = 8;
        public const int MaxPressers = 4;

        public const int MaxIdLength = 64;
        public const int MaxNameLength = 32;
    }
}
