using System;

namespace FS27.Core
{
    /// <summary>
    /// Every number that defines how locomotion and stamina FEEL. Lives in a ScriptableObject
    /// (TuningProfile) so it can be edited in the Inspector, even while playing, without touching code.
    /// Units: metres, seconds, degrees. Attribute ratings go from 1 (worst) to 99 (best).
    /// </summary>
    [Serializable]
    public class MovementTuning
    {
        // ---- Attribute -> game value (lerped between the 1 and 99 values) ----
        /// <summary>Top sprint speed (m/s) for Speed 1 / Speed 99.</summary>
        public float MinTopSpeed = 6.0f;
        public float MaxTopSpeed = 9.5f;

        /// <summary>Acceleration (m/s^2) for Acceleration 1 / 99.</summary>
        public float MinAcceleration = 5.0f;
        public float MaxAcceleration = 11.0f;

        /// <summary>Seconds of FULL sprint a full tank lasts, for Stamina 1 / 99.</summary>
        public float MinSprintSeconds = 4.0f;
        public float MaxSprintSeconds = 10.0f;

        /// <summary>Seconds to refill an empty tank while standing/jogging, for Stamina 1 / 99.</summary>
        public float SlowestRecoverySeconds = 14.0f;
        public float FastestRecoverySeconds = 8.0f;

        // ---- Stick deflection -> target speed ----
        /// <summary>Stick deflection below this is ignored (0..1).</summary>
        public float InputDeadzone = 0.08f;
        /// <summary>Deflection where jogging ends (0..1). 0..JogEnd = walk/jog.</summary>
        public float JogEnd = 0.30f;
        /// <summary>Deflection where running ends and sprinting begins. JogEnd..RunEnd = run.</summary>
        public float RunEnd = 0.70f;
        /// <summary>Deflection at which sprint is at maximum (below 1 so thumbs need not hit the exact edge).</summary>
        public float SprintFullAt = 0.95f;
        /// <summary>Speed (fraction of top speed) reached at JogEnd.</summary>
        public float JogSpeedFraction = 0.25f;
        /// <summary>Speed (fraction of top speed) reached at RunEnd.</summary>
        public float RunSpeedFraction = 0.65f;

        // ---- Acceleration / braking feel ----
        /// <summary>Braking = acceleration * this. Higher = stops faster.</summary>
        public float BrakingMultiplier = 2.0f;
        /// <summary>0..1. Acceleration shrinks as speed nears the top (0 = constant acceleration).</summary>
        public float AccelerationFalloff = 0.5f;

        // ---- Turning ----
        /// <summary>Turn rate (deg/s) when standing still.</summary>
        public float TurnRateAtRest = 1080f;
        /// <summary>Turn rate (deg/s) at top speed. Lower = wider arcs, more inertia.</summary>
        public float TurnRateAtTopSpeed = 360f;
        /// <summary>Heading error (deg) below which turning costs no speed.</summary>
        public float TurnSlowdownStartDeg = 25f;
        /// <summary>Heading error (deg) at which the speed penalty is maximal.</summary>
        public float TurnSlowdownFullDeg = 150f;
        /// <summary>Target-speed multiplier at maximal heading error (e.g. reversing direction).</summary>
        public float MinTurnSpeedFactor = 0.4f;

        // ---- Stamina behaviour ----
        /// <summary>Recovery speed while running (not sprinting, above jog) relative to standing still.</summary>
        public float RunRecoveryFactor = 0.5f;
        /// <summary>Once exhausted, sprint stays locked until stamina is back above this fraction (0..1).</summary>
        public float ExhaustionRecoverFraction = 0.25f;
        /// <summary>Speed cap (fraction of top speed) while exhausted: no full sprint.</summary>
        public float ExhaustedSpeedFraction = 0.65f;
        /// <summary>Minimum speed (m/s) for a stick in the sprint zone to count as "sprinting" (drains stamina).</summary>
        public float MinSprintingSpeed = 1.0f;
    }
}
