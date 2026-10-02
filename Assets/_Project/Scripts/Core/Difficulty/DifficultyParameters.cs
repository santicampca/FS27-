using System;

namespace FS27.Core
{
    // Parameters are grouped by WHAT they change, not by how hard the level is. Every group is plain data
    // with public fields (Unity can serialise them). Defaults equal the Professional level.
    // Unless a field says otherwise: 0..1, where 1 = best. None of them touches PlayerAttributes.

    /// <summary>A) Decision intelligence: how well the AI picks among the options it has.</summary>
    [Serializable]
    public class DecisionParameters
    {
        /// <summary>How close to the best option the AI tends to choose.</summary>
        public float DecisionQuality = 0.65f;
        /// <summary>How many candidate options it can weigh at once.</summary>
        public int OptionsConsidered = 4;
        /// <summary>Seconds between re-evaluations of the situation (lower = more alert).</summary>
        public float DecisionIntervalSeconds = 0.32f;
        /// <summary>How much decision quality is kept when pressured.</summary>
        public float PressureResistance = 0.60f;
        /// <summary>How faithfully it executes its assigned role/zone (the role itself is never changed).</summary>
        public float RoleDiscipline = 0.70f;
    }

    /// <summary>B) Reaction: how long between perceiving something and acting on it.</summary>
    [Serializable]
    public class ReactionParameters
    {
        /// <summary>Seconds to react at an average Reaction attribute.</summary>
        public float BaseReactionSeconds = 0.32f;
        /// <summary>This level never reacts faster than this (never below <see cref="DifficultyRules.AbsoluteMinReactionSeconds"/>).</summary>
        public float MinReactionSeconds = 0.15f;
        /// <summary>Fraction of random variation applied to each reaction (0 = always identical).</summary>
        public float ReactionVariance = 0.20f;
        /// <summary>How strongly the player's own Reaction attribute speeds up or slows down the delay.</summary>
        public float AttributeInfluence = 0.40f;
    }

    /// <summary>C) Positioning: how close the AI stays to the ideal spot and how well it holds its zone.</summary>
    [Serializable]
    public class PositioningParameters
    {
        public float PositioningQuality = 0.65f;
        /// <summary>Largest distance (m) it may end up from the ideal position.</summary>
        public float PositionErrorMeters = 1.3f;
        /// <summary>How well it stays in its zone instead of chasing the ball.</summary>
        public float ZoneDiscipline = 0.65f;
        /// <summary>How well it closes passing lanes.</summary>
        public float PassLaneCover = 0.60f;
        /// <summary>How well it plays outside its primary zone (a versatile player's secondary zones).</summary>
        public float RoleAdaptability = 0.55f;
    }

    /// <summary>D) Anticipation: how far ahead the AI can read play, limited by what it can perceive.</summary>
    [Serializable]
    public class AnticipationParameters
    {
        public float AnticipationQuality = 0.60f;
        /// <summary>Longest horizon (s) it can read ahead (never above <see cref="DifficultyRules.AbsoluteMaxLookaheadSeconds"/>).</summary>
        public float MaxLookaheadSeconds = 0.40f;
        /// <summary>Field of view in degrees: it cannot read what is outside it.</summary>
        public float VisionAngleDegrees = 140f;
        public float PerceptionRangeMeters = 18f;
        public float ReboundReading = 0.60f;
    }

    /// <summary>
    /// E) Pressing. INTENSITY (how much it is willing to press) and INTELLIGENCE (whether it presses at the
    /// right moments) are separate on purpose: a strong team does not press all the time, it presses well.
    /// </summary>
    [Serializable]
    public class PressureParameters
    {
        /// <summary>How readily it presses. Not required to rise with difficulty; style also shapes it.</summary>
        public float PressureIntensity = 0.55f;
        /// <summary>How often it chooses the right action: press, contain, cover or retreat.</summary>
        public float PressureIntelligence = 0.60f;
        public float MaxPressDistanceMeters = 10f;
        /// <summary>How well it recovers shape when it loses the ball.</summary>
        public float RecoveryRunQuality = 0.65f;
    }

    /// <summary>F) Execution: how often and how badly actions go wrong. Skill still comes from attributes.</summary>
    [Serializable]
    public class ExecutionParameters
    {
        /// <summary>1 = rarely makes execution errors, 0 = often.</summary>
        public float ExecutionQuality = 0.65f;
        /// <summary>Cap on how bad an error can be (0..1, lower = smaller slips).</summary>
        public float ErrorMagnitude = 0.70f;
        /// <summary>How much pressure raises error chance (lower = calmer under pressure).</summary>
        public float PressureSensitivity = 0.85f;
        /// <summary>How much low stamina raises error chance.</summary>
        public float FatigueSensitivity = 0.80f;
    }

    /// <summary>G) Team coordination: acting as a unit instead of as individuals.</summary>
    [Serializable]
    public class CoordinationParameters
    {
        public float CoordinationQuality = 0.60f;
        /// <summary>How many players may press the carrier at once (more = less organised).</summary>
        public int MaxSimultaneousPressers = 2;
        public float LineCompactness = 0.60f;
        public float SupportRunTiming = 0.62f;
        /// <summary>Seconds the team needs to reorganise after the possession changes.</summary>
        public float CommunicationDelaySeconds = 0.50f;
    }

    /// <summary>
    /// H) Goalkeeper behaviour (configuration only; there is no goalkeeper AI yet). The keeper's physical
    /// and technical limits still come from the keeper's own attributes.
    /// </summary>
    [Serializable]
    public class GoalkeeperParameters
    {
        /// <summary>Seconds between seeing the shot and starting to move.</summary>
        public float ReactionSeconds = 0.31f;
        public float Positioning = 0.65f;
        public float Anticipation = 0.60f;
        public float DecisionMaking = 0.65f;
        /// <summary>How well it times the dive/save.</summary>
        public float SaveTiming = 0.65f;
        /// <summary>How well it reads where the shot is going.</summary>
        public float ShotReading = 0.62f;
        public float ReboundResponse = 0.60f;
    }
}
