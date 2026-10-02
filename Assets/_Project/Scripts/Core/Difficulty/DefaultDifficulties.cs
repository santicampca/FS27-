using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The five initial difficulty levels, written as a table (one array per parameter, one value per level:
    /// Novice, Amateur, Professional, Expert, Elite). Every number here is a starting point meant to be tuned
    /// by playing; changing it never requires touching logic. Nothing in the table is an attribute or a stat bonus.
    /// </summary>
    public static class DefaultDifficulties
    {
        public const string NoviceId = "novice";
        public const string AmateurId = "amateur";
        public const string ProfessionalId = "professional";
        public const string ExpertId = "expert";
        public const string EliteId = "elite";

        private static readonly string[] Ids = { NoviceId, AmateurId, ProfessionalId, ExpertId, EliteId };
        private static readonly string[] Names = { "Novato", "Amateur", "Profesional", "Experto", "Élite" };

        // A) Decision
        private static readonly float[] DecisionQuality = { 0.35f, 0.50f, 0.65f, 0.80f, 0.92f };
        private static readonly int[] OptionsConsidered = { 2, 3, 4, 5, 6 };
        private static readonly float[] DecisionInterval = { 0.50f, 0.40f, 0.32f, 0.26f, 0.20f };
        private static readonly float[] PressureResistance = { 0.30f, 0.45f, 0.60f, 0.75f, 0.88f };
        private static readonly float[] RoleDiscipline = { 0.40f, 0.55f, 0.70f, 0.82f, 0.92f };

        // B) Reaction (seconds; lower = faster, never below the human limit)
        private static readonly float[] BaseReaction = { 0.50f, 0.40f, 0.32f, 0.26f, 0.21f };
        private static readonly float[] MinReaction = { 0.18f, 0.16f, 0.15f, 0.14f, 0.13f };
        private static readonly float[] ReactionVariance = { 0.30f, 0.25f, 0.20f, 0.15f, 0.10f };
        private static readonly float[] ReactionAttributeInfluence = { 0.40f, 0.40f, 0.40f, 0.40f, 0.40f };

        // C) Positioning
        private static readonly float[] PositioningQuality = { 0.30f, 0.50f, 0.65f, 0.80f, 0.92f };
        private static readonly float[] PositionError = { 3.0f, 2.0f, 1.3f, 0.8f, 0.4f };
        private static readonly float[] ZoneDiscipline = { 0.30f, 0.50f, 0.65f, 0.80f, 0.92f };
        private static readonly float[] PassLaneCover = { 0.20f, 0.40f, 0.60f, 0.78f, 0.90f };
        private static readonly float[] RoleAdaptability = { 0.25f, 0.40f, 0.55f, 0.70f, 0.85f };

        // D) Anticipation
        private static readonly float[] AnticipationQuality = { 0.20f, 0.40f, 0.60f, 0.78f, 0.90f };
        private static readonly float[] MaxLookahead = { 0.15f, 0.25f, 0.40f, 0.55f, 0.70f };
        private static readonly float[] VisionAngle = { 110f, 125f, 140f, 155f, 170f };
        private static readonly float[] PerceptionRange = { 12f, 15f, 18f, 21f, 24f };
        private static readonly float[] ReboundReading = { 0.20f, 0.40f, 0.60f, 0.78f, 0.90f };

        // E) Pressure. Intensity deliberately does NOT just grow with level: an elite team presses when it pays off.
        private static readonly float[] PressureIntensity = { 0.50f, 0.50f, 0.55f, 0.60f, 0.60f };
        private static readonly float[] PressureIntelligence = { 0.20f, 0.40f, 0.60f, 0.78f, 0.92f };
        private static readonly float[] MaxPressDistance = { 10f, 10f, 10f, 10f, 10f };
        private static readonly float[] RecoveryRun = { 0.30f, 0.50f, 0.65f, 0.80f, 0.90f };

        // F) Execution
        private static readonly float[] ExecutionQuality = { 0.35f, 0.50f, 0.65f, 0.80f, 0.90f };
        private static readonly float[] ErrorMagnitude = { 1.00f, 0.85f, 0.70f, 0.55f, 0.40f };
        private static readonly float[] PressureSensitivity = { 1.20f, 1.00f, 0.85f, 0.65f, 0.45f };
        private static readonly float[] FatigueSensitivity = { 1.00f, 0.90f, 0.80f, 0.70f, 0.60f };

        // G) Team coordination
        private static readonly float[] CoordinationQuality = { 0.20f, 0.40f, 0.60f, 0.78f, 0.92f };
        private static readonly int[] MaxPressers = { 3, 3, 2, 2, 2 };
        private static readonly float[] LineCompactness = { 0.30f, 0.45f, 0.60f, 0.75f, 0.88f };
        private static readonly float[] SupportRunTiming = { 0.25f, 0.45f, 0.62f, 0.78f, 0.90f };
        private static readonly float[] CommunicationDelay = { 0.90f, 0.70f, 0.50f, 0.35f, 0.25f };

        // H) Goalkeeper
        private static readonly float[] KeeperReaction = { 0.45f, 0.38f, 0.31f, 0.26f, 0.22f };
        private static readonly float[] KeeperPositioning = { 0.30f, 0.50f, 0.65f, 0.80f, 0.92f };
        private static readonly float[] KeeperAnticipation = { 0.20f, 0.40f, 0.60f, 0.78f, 0.90f };
        private static readonly float[] KeeperDecision = { 0.30f, 0.50f, 0.65f, 0.80f, 0.90f };
        private static readonly float[] KeeperSaveTiming = { 0.30f, 0.50f, 0.65f, 0.80f, 0.92f };
        private static readonly float[] KeeperShotReading = { 0.25f, 0.45f, 0.62f, 0.78f, 0.90f };
        private static readonly float[] KeeperRebound = { 0.20f, 0.40f, 0.60f, 0.78f, 0.90f };

        // Recommended human assists (pass, shot, player switch).
        private static readonly AssistLevel[][] Assists =
        {
            new[] { AssistLevel.High, AssistLevel.High, AssistLevel.High },
            new[] { AssistLevel.High, AssistLevel.Medium, AssistLevel.Medium },
            new[] { AssistLevel.Medium, AssistLevel.Low, AssistLevel.Manual },
            new[] { AssistLevel.Low, AssistLevel.Low, AssistLevel.Manual },
            new[] { AssistLevel.Low, AssistLevel.Manual, AssistLevel.Manual }
        };

        public static DifficultyDefinition Create(DifficultyLevel level)
        {
            int i = (int)level;
            return new DifficultyDefinition(Ids[i], Names[i], level)
            {
                Decision = new DecisionParameters
                {
                    DecisionQuality = DecisionQuality[i],
                    OptionsConsidered = OptionsConsidered[i],
                    DecisionIntervalSeconds = DecisionInterval[i],
                    PressureResistance = PressureResistance[i],
                    RoleDiscipline = RoleDiscipline[i]
                },
                Reaction = new ReactionParameters
                {
                    BaseReactionSeconds = BaseReaction[i],
                    MinReactionSeconds = MinReaction[i],
                    ReactionVariance = ReactionVariance[i],
                    AttributeInfluence = ReactionAttributeInfluence[i]
                },
                Positioning = new PositioningParameters
                {
                    PositioningQuality = PositioningQuality[i],
                    PositionErrorMeters = PositionError[i],
                    ZoneDiscipline = ZoneDiscipline[i],
                    PassLaneCover = PassLaneCover[i],
                    RoleAdaptability = RoleAdaptability[i]
                },
                Anticipation = new AnticipationParameters
                {
                    AnticipationQuality = AnticipationQuality[i],
                    MaxLookaheadSeconds = MaxLookahead[i],
                    VisionAngleDegrees = VisionAngle[i],
                    PerceptionRangeMeters = PerceptionRange[i],
                    ReboundReading = ReboundReading[i]
                },
                Pressure = new PressureParameters
                {
                    PressureIntensity = PressureIntensity[i],
                    PressureIntelligence = PressureIntelligence[i],
                    MaxPressDistanceMeters = MaxPressDistance[i],
                    RecoveryRunQuality = RecoveryRun[i]
                },
                Execution = new ExecutionParameters
                {
                    ExecutionQuality = ExecutionQuality[i],
                    ErrorMagnitude = ErrorMagnitude[i],
                    PressureSensitivity = PressureSensitivity[i],
                    FatigueSensitivity = FatigueSensitivity[i]
                },
                Coordination = new CoordinationParameters
                {
                    CoordinationQuality = CoordinationQuality[i],
                    MaxSimultaneousPressers = MaxPressers[i],
                    LineCompactness = LineCompactness[i],
                    SupportRunTiming = SupportRunTiming[i],
                    CommunicationDelaySeconds = CommunicationDelay[i]
                },
                Goalkeeper = new GoalkeeperParameters
                {
                    ReactionSeconds = KeeperReaction[i],
                    Positioning = KeeperPositioning[i],
                    Anticipation = KeeperAnticipation[i],
                    DecisionMaking = KeeperDecision[i],
                    SaveTiming = KeeperSaveTiming[i],
                    ShotReading = KeeperShotReading[i],
                    ReboundResponse = KeeperRebound[i]
                },
                RecommendedAssists = new PlayerAssistSettings(Assists[i][0], Assists[i][1], Assists[i][2])
            };
        }

        /// <summary>Fresh copies of the five levels, easiest first.</summary>
        public static List<DifficultyDefinition> CreateAll()
        {
            var all = new List<DifficultyDefinition>();
            for (int i = 0; i < Ids.Length; i++) all.Add(Create((DifficultyLevel)i));
            return all;
        }
    }

    /// <summary>Finds difficulty definitions by level or id. Keeps them ordered easiest to hardest.</summary>
    public sealed class DifficultyLibrary
    {
        private readonly List<DifficultyDefinition> ordered = new List<DifficultyDefinition>();

        public int Count => ordered.Count;
        /// <summary>Sorted by <see cref="DifficultyDefinition.Level"/>, easiest first.</summary>
        public IReadOnlyList<DifficultyDefinition> All => ordered;

        /// <summary>Adds a level. Returns false (and adds nothing) if it is null or its id/level is already taken.</summary>
        public bool TryAdd(DifficultyDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return false;
            foreach (DifficultyDefinition d in ordered)
                if (d.Id == definition.Id || d.Level == definition.Level) return false;

            ordered.Add(definition);
            ordered.Sort((a, b) => a.Level.CompareTo(b.Level));
            return true;
        }

        public bool TryGet(DifficultyLevel level, out DifficultyDefinition definition)
        {
            foreach (DifficultyDefinition d in ordered)
            {
                if (d.Level == level)
                {
                    definition = d;
                    return true;
                }
            }
            definition = null;
            return false;
        }

        public bool TryGet(string id, out DifficultyDefinition definition)
        {
            if (id != null)
            {
                foreach (DifficultyDefinition d in ordered)
                {
                    if (d.Id == id)
                    {
                        definition = d;
                        return true;
                    }
                }
            }
            definition = null;
            return false;
        }

        public static DifficultyLibrary CreateDefault()
        {
            var lib = new DifficultyLibrary();
            foreach (DifficultyDefinition d in DefaultDifficulties.CreateAll()) lib.TryAdd(d);
            return lib;
        }
    }
}
