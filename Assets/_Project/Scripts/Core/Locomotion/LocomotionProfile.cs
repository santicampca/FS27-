using System;

namespace FS27.Core
{
    /// <summary>
    /// The movement abilities of a player, resolved ONCE from his official attributes (no new attribute, nothing stored on the player):
    /// Speed -> top speed, Acceleration -> acceleration, Agility -> braking and turning, Stamina -> efficiency and recovery.
    /// Strength, body type and the playing profile are NOT used here (they belong to contact and gameplay phases).
    /// </summary>
    public struct LocomotionProfile
    {
        /// <summary>Top speed in m/s with full stamina.</summary>
        public float MaxSpeed;
        /// <summary>m/s^2 with full stamina.</summary>
        public float MaxAcceleration;
        /// <summary>m/s^2.</summary>
        public float MaxDeceleration;
        /// <summary>0.70 .. 1.00: how well he changes direction.</summary>
        public float AgilityFactor;
        /// <summary>0.75 .. 1.25: scales recovery up and drain down.</summary>
        public float StaminaAttributeFactor;

        public static LocomotionProfile Resolve(in PlayerAttributes a, LocomotionTuning t)
        {
            float speed = Clamp(a.Speed), acceleration = Clamp(a.Acceleration), agility = Clamp(a.Agility), stamina = Clamp(a.Stamina);
            return new LocomotionProfile
            {
                MaxSpeed = t.BaseMaxSpeed + t.SpeedCoefficient * speed,
                MaxAcceleration = t.BaseAcceleration + t.AccelerationCoefficient * acceleration,
                MaxDeceleration = t.BaseDeceleration + t.AgilityDecelerationCoefficient * agility,
                AgilityFactor = t.AgilityTurnBase + t.AgilityTurnRange * (agility / PlayerAttributes.Max),
                StaminaAttributeFactor = t.AttributeStaminaBase + t.AttributeStaminaRange * (stamina / PlayerAttributes.Max)
            };
        }

        private static float Clamp(int attribute) { return Math.Max(PlayerAttributes.Min, Math.Min(PlayerAttributes.Max, attribute)); }
    }
}
