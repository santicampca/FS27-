using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-3 locomotion and stamina model, in one place. They are starting values of tuning, not constants of nature: <see cref="LocomotionTuning"/> copies them
    /// into an editable object, and no other file repeats them. Units: metres, seconds, radians (degrees only where named). Attributes are the official ones, 1..99.
    /// </summary>
    public static class LocomotionDefaults
    {
        // ---- simulation step ----
        public const int SimulationHz = 60;
        /// <summary>The fixed step of the deterministic Core (1/60 s). The match engine's step (<see cref="MatchDefaults.SimulationStepSeconds"/>) is this same value.</summary>
        public const float FixedDeltaTime = 1f / SimulationHz;

        // ---- Speed -> top speed ----
        public const float BaseMaxSpeed = 4.2f;
        public const float SpeedCoefficient = 0.0333f;

        // ---- Acceleration -> acceleration ----
        public const float BaseAcceleration = 4.0f;
        public const float AccelerationCoefficient = 0.08f;
        /// <summary>A tired player keeps part of his acceleration: effective = max x (Base + Range x performance).</summary>
        public const float FatigueAccelerationBase = 0.70f;
        public const float FatigueAccelerationRange = 0.30f;

        // ---- Agility -> braking and turning ----
        public const float BaseDeceleration = 6.0f;
        public const float AgilityDecelerationCoefficient = 0.05f;
        /// <summary>Turn rate of a player standing still with Agility 99 (degrees per second). Initial tuning value (the specification gives the factors, not the base).</summary>
        public const float BaseTurnRateDegrees = 900f;
        public const float AgilityTurnBase = 0.70f;
        public const float AgilityTurnRange = 0.30f;
        /// <summary>At top speed the turn rate is (1 - this) of the one at rest.</summary>
        public const float TurnSpeedPenalty = 0.55f;

        // ---- joystick ranges (descriptive: the speed itself is continuous) ----
        public const float WalkThreshold = 0.30f;
        public const float SprintThreshold = 0.70f;
        /// <summary>Input magnitude below this is "no input".</summary>
        public const float InputEpsilon = 1e-4f;

        // ---- stamina (0..100) ----
        public const float MaxStamina = 100f;
        public const float BaseStaminaDrain = 3.0f;
        public const float SprintMultiplier = 1.35f;
        public const float BaseStaminaRecovery = 2.0f;
        /// <summary>Recovery while walking, relative to standing still. Running and sprinting do not recover. Phase 17: 0.25 -> 0.6 (in a match the field players spent 33-43 % of their time under 5 stamina; see Docs/AI_BALANCE_PHASE17.md).</summary>
        public const float WalkRecoveryFactor = 0.6f;
        /// <summary>The Stamina attribute scales recovery (and, inversely, the drain): factor = Base + Range x (attribute / 99).</summary>
        public const float AttributeStaminaBase = 0.75f;
        public const float AttributeStaminaRange = 0.50f;
        /// <summary>Sprint availability: 0 at or below Low, 1 at or above Full, linear between.</summary>
        public const float SprintAvailabilityLow = 10f;
        public const float SprintAvailabilityFull = 40f;
        /// <summary>At or below this stamina the player is shown as Exhausted (he still moves).</summary>
        public const float ExhaustedThreshold = 0.5f;

        // ---- interval recovery: the SAME rules the match engine already closed (fractions of the maximum) ----
        public const float ClassicIntervalRecovery = MatchDefaults.ClassicHalfTimeRecovery;
        public const float ArenaIntervalRecovery = MatchDefaults.ArenaSideSwitchRecovery;

        // ---- performance curve: stamina -> performance factor (continuous, linear between points) ----
        public static readonly float[] PerformanceStamina = { 0f, 10f, 20f, 30f, 40f, 50f, 70f, 80f, 100f };
        public static readonly float[] PerformanceFactor = { 0.40f, 0.55f, 0.68f, 0.77f, 0.84f, 0.90f, 0.95f, 0.98f, 1.00f };
    }

    /// <summary>The editable copy of <see cref="LocomotionDefaults"/>. Every system takes one of these, so tuning never means editing code.</summary>
    [Serializable]
    public sealed class LocomotionTuning
    {
        public float BaseMaxSpeed = LocomotionDefaults.BaseMaxSpeed;
        public float SpeedCoefficient = LocomotionDefaults.SpeedCoefficient;
        public float BaseAcceleration = LocomotionDefaults.BaseAcceleration;
        public float AccelerationCoefficient = LocomotionDefaults.AccelerationCoefficient;
        public float FatigueAccelerationBase = LocomotionDefaults.FatigueAccelerationBase;
        public float FatigueAccelerationRange = LocomotionDefaults.FatigueAccelerationRange;
        public float BaseDeceleration = LocomotionDefaults.BaseDeceleration;
        public float AgilityDecelerationCoefficient = LocomotionDefaults.AgilityDecelerationCoefficient;
        public float BaseTurnRateDegrees = LocomotionDefaults.BaseTurnRateDegrees;
        public float AgilityTurnBase = LocomotionDefaults.AgilityTurnBase;
        public float AgilityTurnRange = LocomotionDefaults.AgilityTurnRange;
        public float TurnSpeedPenalty = LocomotionDefaults.TurnSpeedPenalty;
        public float WalkThreshold = LocomotionDefaults.WalkThreshold;
        public float SprintThreshold = LocomotionDefaults.SprintThreshold;
        public float MaxStamina = LocomotionDefaults.MaxStamina;
        public float BaseStaminaDrain = LocomotionDefaults.BaseStaminaDrain;
        public float SprintMultiplier = LocomotionDefaults.SprintMultiplier;
        public float BaseStaminaRecovery = LocomotionDefaults.BaseStaminaRecovery;
        public float WalkRecoveryFactor = LocomotionDefaults.WalkRecoveryFactor;
        public float AttributeStaminaBase = LocomotionDefaults.AttributeStaminaBase;
        public float AttributeStaminaRange = LocomotionDefaults.AttributeStaminaRange;
        public float SprintAvailabilityLow = LocomotionDefaults.SprintAvailabilityLow;
        public float SprintAvailabilityFull = LocomotionDefaults.SprintAvailabilityFull;
        public float ExhaustedThreshold = LocomotionDefaults.ExhaustedThreshold;
        public float[] PerformanceStamina = (float[])LocomotionDefaults.PerformanceStamina.Clone();
        public float[] PerformanceFactor = (float[])LocomotionDefaults.PerformanceFactor.Clone();
    }
}
