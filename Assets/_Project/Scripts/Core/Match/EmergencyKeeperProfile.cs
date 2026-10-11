using System;

namespace FS27.Core
{
    /// <summary>Tuning of the emergency keeper.</summary>
    [Serializable]
    public sealed class EmergencyKeeperTuning
    {
        /// <summary>A field player in goal is clearly worse than a specialist: every derived capability is multiplied by this (0..1). Calibrated against the keeper database (see <see cref="MatchDefaults.EmergencyKeeperFactor"/>).</summary>
        public float UnspecializedFactor = MatchDefaults.EmergencyKeeperFactor;
    }

    /// <summary>
    /// The goalkeeper capabilities of a FIELD player who has to stand in goal (a red card with no substitution left). Derived at runtime from the 12 official attributes;
    /// NEVER stored, never added to the player definition, and it does not make the player a real keeper (it has no GRL).
    /// Command is not derived: it is contextual behaviour of the goalkeeper AI.
    /// </summary>
    public struct EmergencyKeeperProfile
    {
        public int Reflexes, Handling, Positioning, Diving, Kicking, Distribution, Recovery;

        /// <summary>Always true: Command is decided by the goalkeeper AI in context, it has no number.</summary>
        public bool CommandIsContextual => true;

        public int Get(GoalkeeperCapability capability)
        {
            switch (capability)
            {
                case GoalkeeperCapability.Reflexes: return Reflexes;
                case GoalkeeperCapability.Handling: return Handling;
                case GoalkeeperCapability.Positioning: return Positioning;
                case GoalkeeperCapability.Diving: return Diving;
                case GoalkeeperCapability.Kicking: return Kicking;
                case GoalkeeperCapability.Distribution: return Distribution;
                case GoalkeeperCapability.Recovery: return Recovery;
                default: return 0; // Command: contextual
            }
        }

        public static EmergencyKeeperProfile Derive(in PlayerAttributes a, EmergencyKeeperTuning tuning = null)
        {
            float factor = (tuning ?? new EmergencyKeeperTuning()).UnspecializedFactor;
            return new EmergencyKeeperProfile
            {
                Reflexes = Mix(a.Agility, a.Control, factor),
                Handling = Mix(a.Control, a.Technique, factor),
                Positioning = Mix(a.Defense, a.Agility, factor),
                Diving = Mix(a.Agility, a.Speed, factor),
                Kicking = Mix(a.Passing, a.Technique, factor),
                Distribution = Mix(a.Passing, a.Control, factor),
                Recovery = Mix(a.Stamina, a.Agility, factor)
            };
        }

        /// <summary>The mean of the two attributes, scaled by the specialisation factor, rounded half up.</summary>
        private static int Mix(int x, int y, float factor)
        {
            return (int)Math.Floor((x + y) * 0.5f * factor + 0.5f);
        }
    }
}
