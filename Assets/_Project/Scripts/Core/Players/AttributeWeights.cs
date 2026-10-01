using System;

namespace FS27.Core
{
    /// <summary>
    /// How much each of the 12 core attributes matters for something (a role, a zone, an overall). Weights are plain
    /// non-negative numbers; only their proportions matter (they are normalised when used). Serializable, so they can be
    /// edited as data. The legacy Reaction attribute is intentionally not part of any rating.
    /// </summary>
    [Serializable]
    public class AttributeWeights
    {
        public float Speed;
        public float Acceleration;
        public float Agility;
        public float Strength;
        public float Stamina;
        public float Shooting;
        public float Finishing;
        public float Passing;
        public float Control;
        public float Dribbling;
        public float Technique;
        public float Defense;

        /// <summary>Weights in the order of <see cref="PlayerAttributeId"/>.</summary>
        public static AttributeWeights Of(float speed, float acceleration, float agility, float strength, float stamina,
                                          float shooting, float finishing, float passing, float control, float dribbling,
                                          float technique, float defense)
        {
            return new AttributeWeights
            {
                Speed = speed, Acceleration = acceleration, Agility = agility, Strength = strength, Stamina = stamina,
                Shooting = shooting, Finishing = finishing, Passing = passing, Control = control, Dribbling = dribbling,
                Technique = technique, Defense = defense
            };
        }

        public float Get(PlayerAttributeId id)
        {
            switch (id)
            {
                case PlayerAttributeId.Speed: return Speed;
                case PlayerAttributeId.Acceleration: return Acceleration;
                case PlayerAttributeId.Agility: return Agility;
                case PlayerAttributeId.Strength: return Strength;
                case PlayerAttributeId.Stamina: return Stamina;
                case PlayerAttributeId.Shooting: return Shooting;
                case PlayerAttributeId.Finishing: return Finishing;
                case PlayerAttributeId.Passing: return Passing;
                case PlayerAttributeId.Control: return Control;
                case PlayerAttributeId.Dribbling: return Dribbling;
                case PlayerAttributeId.Technique: return Technique;
                default: return Defense;
            }
        }

        public void Set(PlayerAttributeId id, float value)
        {
            switch (id)
            {
                case PlayerAttributeId.Speed: Speed = value; break;
                case PlayerAttributeId.Acceleration: Acceleration = value; break;
                case PlayerAttributeId.Agility: Agility = value; break;
                case PlayerAttributeId.Strength: Strength = value; break;
                case PlayerAttributeId.Stamina: Stamina = value; break;
                case PlayerAttributeId.Shooting: Shooting = value; break;
                case PlayerAttributeId.Finishing: Finishing = value; break;
                case PlayerAttributeId.Passing: Passing = value; break;
                case PlayerAttributeId.Control: Control = value; break;
                case PlayerAttributeId.Dribbling: Dribbling = value; break;
                case PlayerAttributeId.Technique: Technique = value; break;
                default: Defense = value; break;
            }
        }

        public float Sum
        {
            get
            {
                float s = 0f;
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) s += Get(id);
                return s;
            }
        }

        /// <summary>True if every weight is a finite number &gt;= 0 and at least one is &gt; 0.</summary>
        public bool IsUsable
        {
            get
            {
                float sum = 0f;
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                {
                    float w = Get(id);
                    if (float.IsNaN(w) || float.IsInfinity(w) || w < 0f) return false;
                    sum += w;
                }
                return sum > 0f;
            }
        }

        /// <summary>A copy whose weights add up to 1.</summary>
        public AttributeWeights Normalized()
        {
            float sum = Sum;
            var n = new AttributeWeights();
            if (sum <= 0f) return n;
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All) n.Set(id, Get(id) / sum);
            return n;
        }

        /// <summary>The weighted average (1..99) of the player's attributes. Values outside 1..99 are clamped first.</summary>
        public float Evaluate(in PlayerAttributes attributes)
        {
            float sum = Sum;
            if (sum <= 0f) return PlayerAttributes.Min;
            float total = 0f;
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
            {
                float w = Get(id);
                if (w <= 0f) continue;
                int v = attributes.GetValue(id);
                if (v < PlayerAttributes.Min) v = PlayerAttributes.Min;
                if (v > PlayerAttributes.Max) v = PlayerAttributes.Max;
                total += w * v;
            }
            return total / sum;
        }

        public AttributeWeights Clone()
        {
            return (AttributeWeights)MemberwiseClone();
        }
    }
}
