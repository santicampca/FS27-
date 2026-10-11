using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>A role the coach favours, 0..100.</summary>
    [Serializable]
    public struct WeightedRole
    {
        public PlayerArchetype Role;
        public int Weight;
        public WeightedRole(PlayerArchetype role, int weight) { Role = role; Weight = weight; }
    }

    /// <summary>A zone the coach favours, 0..100.</summary>
    [Serializable]
    public struct WeightedZone
    {
        public PitchZone Zone;
        public int Weight;
        public WeightedZone(PitchZone zone, int weight) { Zone = zone; Weight = weight; }
    }

    /// <summary>The value (0..100) the coach likes in a behaviour trait and how much it matters (0..100; 0 = ignored).</summary>
    [Serializable]
    public struct BehaviourPreference
    {
        public int Target;
        public int Importance;
        public BehaviourPreference(int target, int importance) { Target = target; Importance = importance; }
    }

    /// <summary>
    /// What kind of player fits a coach, as DATA. Every part is optional: a part with no data, or with importance 0, is simply left out of the score.
    /// The profile never names a player or a coach; it only talks about roles, zones, attributes, the behaviour profile and the body type.
    /// </summary>
    [Serializable]
    public sealed class CoachCompatibilityProfile
    {
        public List<WeightedRole> Roles = new List<WeightedRole>();
        public List<WeightedZone> Zones = new List<WeightedZone>();
        /// <summary>How much each of the 12 official attributes matters (any non-negative numbers; they are normalised). All zero = attributes ignored.</summary>
        public AttributeWeights Attributes = new AttributeWeights();
        public BehaviourPreference Risk;
        public BehaviourPreference Creativity;
        public BehaviourPreference Aggression;
        public List<BodyType> BodyTypes = new List<BodyType>();

        // How much each part counts in the final score (0..100; 0 = not used).
        public int RoleImportance = 40;
        public int ZoneImportance = 15;
        public int AttributeImportance = 25;
        public int BehaviourImportance = 15;
        public int BodyImportance = 5;

        public bool HasAttributes { get { return Attributes != null && Attributes.Sum > 0f; } }

        public int RoleWeight(PlayerArchetype role)
        {
            int best = 0;
            foreach (WeightedRole r in Roles) if (r.Role == role && r.Weight > best) best = r.Weight;
            return best;
        }

        public int ZoneWeight(PitchZone zone)
        {
            int best = 0;
            foreach (WeightedZone z in Zones) if (z.Zone == zone && z.Weight > best) best = z.Weight;
            return best;
        }
    }
}
