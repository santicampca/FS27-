using System;

namespace FS27.Core
{
    /// <summary>
    /// One difficulty level as a bundle of independent, editable parameter groups. This is the single place
    /// where "how well the AI plays" is configured; logic only reads from it.
    ///
    /// What a difficulty is NOT: it never contains attributes, stat bonuses or multipliers on speed, passing,
    /// shooting or anything a player is physically able to do. Those stay in <see cref="PlayerAttributes"/>.
    /// A harder level changes how well the AI USES what its players have.
    /// </summary>
    [Serializable]
    public class DifficultyDefinition
    {
        /// <summary>Stable id, e.g. "professional".</summary>
        public string Id;
        /// <summary>Display name (Spanish by default; localisation comes later).</summary>
        public string Name;
        public DifficultyLevel Level = DifficultyLevel.Professional;

        public DecisionParameters Decision = new DecisionParameters();
        public ReactionParameters Reaction = new ReactionParameters();
        public PositioningParameters Positioning = new PositioningParameters();
        public AnticipationParameters Anticipation = new AnticipationParameters();
        public PressureParameters Pressure = new PressureParameters();
        public ExecutionParameters Execution = new ExecutionParameters();
        public CoordinationParameters Coordination = new CoordinationParameters();
        public GoalkeeperParameters Goalkeeper = new GoalkeeperParameters();

        /// <summary>Suggested human assists for this level. Only a suggestion: the player can override each one.</summary>
        public PlayerAssistSettings RecommendedAssists = new PlayerAssistSettings();

        public DifficultyDefinition()
        {
        }

        public DifficultyDefinition(string id, string name, DifficultyLevel level)
        {
            Id = id;
            Name = name;
            Level = level;
        }
    }
}
