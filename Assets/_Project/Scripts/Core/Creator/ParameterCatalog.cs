using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Which part of a character a parameter belongs to. Appearance parameters never affect gameplay; DNA parameters never affect looks.</summary>
    public enum ParameterDomain
    {
        Appearance = 0,
        FootballDna = 1
    }

    /// <summary>
    /// One tunable number of the Creator Engine, described as DATA: its id, its range and its neutral value. Characters only store the
    /// values that differ from the neutral one. Adding a new tendency or proportion means adding one row to the catalog: no new field,
    /// no new serializer, no new validator.
    /// Normalised level: 0 = the parameter's minimum, 1 = its maximum, 0.5 = the middle of its range.
    /// </summary>
    [Serializable]
    public sealed class ParameterDefinition
    {
        public string Id;
        public ParameterDomain Domain;
        /// <summary>Group inside the domain (body, head, face, hair, skin, style / movement, dribbling, passing...).</summary>
        public string Group;
        public float Min;
        public float Max;
        public float Default;
        public string Description;

        public float FromLevel(float level01)
        {
            return Min + MathUtil.Clamp01(level01) * (Max - Min);
        }

        public float ToLevel(float value)
        {
            return Max > Min ? MathUtil.Clamp01((value - Min) / (Max - Min)) : 0f;
        }

        public float Clamp(float value)
        {
            return value < Min ? Min : (value > Max ? Max : value);
        }
    }

    public sealed class ParameterCatalog
    {
        private readonly Dictionary<string, ParameterDefinition> byId = new Dictionary<string, ParameterDefinition>();
        private readonly List<ParameterDefinition> ordered = new List<ParameterDefinition>();

        public int Count => ordered.Count;
        public IReadOnlyList<ParameterDefinition> All => ordered;

        /// <summary>Adds a parameter. False (adding nothing) if the id is taken, empty, or the range is not sensible.</summary>
        public bool TryAdd(ParameterDefinition p)
        {
            if (p == null || string.IsNullOrEmpty(p.Id) || byId.ContainsKey(p.Id)) return false;
            if (!(p.Min < p.Max) || p.Default < p.Min || p.Default > p.Max) return false;
            byId.Add(p.Id, p);
            ordered.Add(p);
            return true;
        }

        public bool TryGet(string id, out ParameterDefinition p)
        {
            if (id == null)
            {
                p = null;
                return false;
            }
            return byId.TryGetValue(id, out p);
        }

        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }

        public List<ParameterDefinition> InDomain(ParameterDomain domain)
        {
            var list = new List<ParameterDefinition>();
            foreach (ParameterDefinition p in ordered)
                if (p.Domain == domain) list.Add(p);
            return list;
        }
    }

    /// <summary>
    /// The values of a set of parameters for one character. Only values that DIFFER from the neutral default are stored, so a typical
    /// character costs a few hundred bytes. Reading an unset parameter gives its default.
    /// </summary>
    [Serializable]
    public sealed class ParameterSet
    {
        public SortedDictionary<string, float> Values = new SortedDictionary<string, float>(StringComparer.Ordinal);

        public float Get(ParameterCatalog catalog, string id)
        {
            if (Values.TryGetValue(id, out float v)) return v;
            return catalog.TryGet(id, out ParameterDefinition p) ? p.Default : 0f;
        }

        public float GetLevel(ParameterCatalog catalog, string id)
        {
            return catalog.TryGet(id, out ParameterDefinition p) ? p.ToLevel(Get(catalog, id)) : 0f;
        }

        /// <summary>Stores a value (clamped into the parameter's range). Equal-to-default values are not stored. False if the parameter is unknown.</summary>
        public bool Set(ParameterCatalog catalog, string id, float value)
        {
            if (!catalog.TryGet(id, out ParameterDefinition p)) return false;
            float v = p.Clamp(float.IsNaN(value) ? p.Default : value);
            if (Math.Abs(v - p.Default) < 1e-4f) Values.Remove(id);
            else Values[id] = v;
            return true;
        }

        public ParameterSet Clone()
        {
            var c = new ParameterSet();
            foreach (KeyValuePair<string, float> kv in Values) c.Values[kv.Key] = kv.Value;
            return c;
        }
    }

    /// <summary>The standard parameters of FS27. Starting values meant to be tuned; the catalog is the one place that lists them.</summary>
    public static class DefaultParameters
    {
        private static void A(ParameterCatalog c, string group, string id, float min, float max, float def, string text)
        {
            c.TryAdd(new ParameterDefinition { Id = id, Domain = ParameterDomain.Appearance, Group = group, Min = min, Max = max, Default = def, Description = text });
        }

        private static void D(ParameterCatalog c, string group, string id, string text)
        {
            c.TryAdd(new ParameterDefinition { Id = id, Domain = ParameterDomain.FootballDna, Group = group, Min = 0f, Max = 1f, Default = 0.5f, Description = text });
        }

        public static ParameterCatalog Create()
        {
            var c = new ParameterCatalog();

            // ---------------- Appearance: body (scales relative to the base model; 1 = the base) ----------------
            A(c, "body", "body.height", 0.85f, 1.15f, 1.0f, "Overall height scale.");
            A(c, "body", "body.mass", 0.85f, 1.25f, 1.0f, "Overall thickness (weight) scale.");
            A(c, "body", "body.shoulderWidth", 0.85f, 1.20f, 1.0f, "Shoulder width.");
            A(c, "body", "body.torsoWidth", 0.85f, 1.20f, 1.0f, "Torso width.");
            A(c, "body", "body.muscularity", 0f, 1f, 0.5f, "0 lean .. 1 very muscular.");
            A(c, "body", "body.armLength", 0.9f, 1.1f, 1.0f, "Arm length.");
            A(c, "body", "body.legLength", 0.9f, 1.1f, 1.0f, "Leg length.");
            A(c, "body", "body.handScale", 0.9f, 1.4f, 1.0f, "Hand emphasis (the style wants slightly big hands).");
            A(c, "body", "body.footScale", 0.9f, 1.4f, 1.0f, "Foot/boot emphasis.");

            // ---------------- Appearance: head and face ----------------
            A(c, "head", "head.scale", 0.9f, 1.35f, 1.0f, "Head size relative to the body (cartoon proportions).");
            A(c, "head", "head.width", 0.85f, 1.15f, 1.0f, "Head width.");
            A(c, "head", "head.height", 0.85f, 1.15f, 1.0f, "Head height.");
            A(c, "head", "head.jaw", 0f, 1f, 0.5f, "0 soft jaw .. 1 strong jaw.");
            A(c, "head", "head.chin", 0f, 1f, 0.5f, "0 small chin .. 1 prominent chin.");
            A(c, "head", "head.cheekbones", 0f, 1f, 0.5f, "0 flat .. 1 prominent cheekbones.");
            A(c, "head", "head.forehead", 0f, 1f, 0.5f, "0 low .. 1 high forehead.");
            A(c, "face", "face.eyeSize", 0.8f, 1.35f, 1.0f, "Eye size.");
            A(c, "face", "face.eyeSpacing", 0.85f, 1.15f, 1.0f, "Distance between the eyes.");
            A(c, "face", "face.eyeTilt", 0f, 1f, 0.5f, "0 downturned .. 1 upturned.");
            A(c, "face", "face.eyebrowThickness", 0f, 1f, 0.5f, "Eyebrow thickness.");
            A(c, "face", "face.noseSize", 0.8f, 1.2f, 1.0f, "Nose size.");
            A(c, "face", "face.noseWidth", 0.8f, 1.2f, 1.0f, "Nose width.");
            A(c, "face", "face.mouthWidth", 0.85f, 1.2f, 1.0f, "Mouth width.");
            A(c, "face", "face.mouthHeight", 0f, 1f, 0.5f, "Mouth vertical position.");
            A(c, "face", "face.earSize", 0.8f, 1.25f, 1.0f, "Ear size.");
            A(c, "face", "face.expressiveness", 0f, 1f, 0.5f, "How strongly the face reads expressions.");

            // ---------------- Appearance: hair and skin ----------------
            A(c, "hair", "hair.length", 0f, 1f, 0.3f, "0 shaved .. 1 long.");
            A(c, "hair", "hair.volume", 0.6f, 1.5f, 1.0f, "Hair volume.");
            A(c, "skin", "skin.tone", 0f, 1f, 0.5f, "Position on the skin tone ramp (light .. dark).");
            A(c, "skin", "skin.variation", 0f, 1f, 0f, "Subtle visual variation (freckles, blush...), 0 none.");

            // ---------------- Appearance: style sliders (see StyleCatalog for how they move together) ----------------
            A(c, "style", "style.realism", 0f, 1f, 0.25f, "0 not realistic .. 1 realistic.");
            A(c, "style", "style.stylization", 0f, 1f, 0.7f, "0 none .. 1 strongly stylised.");
            A(c, "style", "style.exaggeration", 0f, 1f, 0.35f, "Exaggeration of shapes.");
            A(c, "style", "style.expressiveness", 0f, 1f, 0.6f, "Facial and body expressiveness.");
            A(c, "style", "style.athleticity", 0f, 1f, 0.7f, "Athletic proportions.");

            // ---------------- Football DNA: tendencies (0 = never/low .. 1 = always/high; 0.5 = neutral) ----------------
            // They describe how the player TENDS to play. They never replace the 12 attributes, nor the behaviour values (risk, creativity,
            // aggression) that already live in PlayerPlayingProfile.
            D(c, "movement", "movement.accelerationTendency", "Likes to burst away.");
            D(c, "movement", "movement.decelerationTendency", "Likes to stop sharply.");
            D(c, "movement", "movement.turningTendency", "Likes to turn.");
            D(c, "movement", "movement.aggression", "Attacks space with intent.");
            D(c, "movement", "movement.runTiming", "Times runs carefully.");
            D(c, "movement", "movement.supportMovement", "Moves to support the ball carrier.");
            D(c, "movement", "movement.spaceSeeking", "Looks for open space.");
            D(c, "movement", "movement.diagonal", "Prefers diagonal runs.");
            D(c, "movement", "movement.delayedRuns", "Starts runs late.");
            D(c, "movement", "movement.blindSideRuns", "Runs behind defenders' view.");
            D(c, "dribbling", "dribbling.takeOn", "Tries to beat defenders.");
            D(c, "dribbling", "dribbling.closeControl", "Keeps the ball close.");
            D(c, "dribbling", "dribbling.changeOfPace", "Changes speed to beat a defender.");
            D(c, "dribbling", "dribbling.stopAndGo", "Stops and restarts.");
            D(c, "dribbling", "dribbling.bodyFeint", "Uses body feints.");
            D(c, "dribbling", "dribbling.directionChange", "Changes direction sharply.");
            D(c, "dribbling", "dribbling.insideCut", "Cuts inside.");
            D(c, "dribbling", "dribbling.outsideCut", "Goes outside.");
            D(c, "dribbling", "dribbling.takeOnRisk", "Accepts losing the ball when dribbling.");
            D(c, "possession", "possession.holdUp", "Holds the ball with back to goal.");
            D(c, "possession", "possession.shielding", "Protects the ball with the body.");
            D(c, "passing", "passing.short", "Short passes.");
            D(c, "passing", "passing.progressive", "Forward passes.");
            D(c, "passing", "passing.throughBall", "Through balls.");
            D(c, "passing", "passing.cross", "Crosses.");
            D(c, "passing", "passing.safe", "Safe passes.");
            D(c, "passing", "passing.risky", "Risky passes.");
            D(c, "passing", "passing.oneTouch", "One-touch passing.");
            D(c, "shooting", "shooting.frequency", "Shoots often.");
            D(c, "shooting", "shooting.longShot", "Shoots from distance.");
            D(c, "shooting", "shooting.finesse", "Placed shots.");
            D(c, "shooting", "shooting.power", "Powerful shots.");
            D(c, "shooting", "shooting.firstTime", "First-time shots.");
            D(c, "shooting", "shooting.weakFootUsage", "Uses the weaker foot.");
            D(c, "shooting", "shooting.insideBox", "Prefers finishing inside the box.");
            D(c, "positioning", "positioning.boxPresence", "Stays in the box.");
            D(c, "positioning", "positioning.halfSpace", "Occupies the half-spaces.");
            D(c, "positioning", "positioning.width", "Stays wide.");
            D(c, "positioning", "positioning.depth", "Stays high up the pitch.");
            D(c, "positioning", "positioning.dropping", "Drops to receive.");
            D(c, "positioning", "positioning.attackingRuns", "Makes attacking runs.");
            D(c, "positioning", "positioning.defensive", "Positions to defend.");
            D(c, "decision", "decision.patience", "Waits for the right moment.");
            D(c, "decision", "decision.directness", "Plays direct.");
            D(c, "decision", "decision.pressureResponse", "Stays composed under pressure.");
            D(c, "decision", "decision.scanning", "Checks surroundings before receiving.");
            D(c, "defending", "defending.pressing", "Presses the ball.");
            D(c, "defending", "defending.marking", "Marks opponents.");
            D(c, "defending", "defending.interception", "Intercepts passes.");
            D(c, "defending", "defending.aggression", "Goes into tackles.");
            D(c, "defending", "defending.retreat", "Drops back quickly.");
            D(c, "defending", "defending.laneBlocking", "Blocks passing lanes.");
            return c;
        }
    }
}
