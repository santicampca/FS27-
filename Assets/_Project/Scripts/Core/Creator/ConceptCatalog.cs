using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum ConceptKind
    {
        /// <summary>Has a direction (more/less): height, speed, strength...</summary>
        Scalar = 0,
        /// <summary>A pick from a catalog slot: hair style, beard...</summary>
        Choice,
        /// <summary>A colour slot.</summary>
        Color,
        /// <summary>A football role: winger, striker...</summary>
        Role,
        /// <summary>A named bundle of other things (face, body, hair): used to preserve, replace or remove a whole area.</summary>
        Group
    }

    public enum EffectKind
    {
        Param = 0,
        Attribute,
        Profile,
        Choice,
        Color,
        Behavior,
        StyleAxis,
        Unsupported
    }

    /// <summary>
    /// One thing a concept changes when it moves. <see cref="Weight"/> is signed and relative to the concept's direction: increasing the concept
    /// by X moves this target by X * Weight. A concept has ONE primary effect (what the words say) and collateral effects (what they usually
    /// bring along); collateral effects are weaker and are reported as such.
    /// </summary>
    [Serializable]
    public sealed class ConceptEffect
    {
        public EffectKind Kind;
        /// <summary>Parameter id, choice slot, colour slot, behaviour id or style axis, by <see cref="Kind"/>.</summary>
        public string Key = "";
        public PlayerAttributeId Attribute;
        public ProfileHintKind ProfileKind;
        public PitchZone Zone;
        public PlayerArchetype Role;
        public float Weight = 1f;
        public bool Primary = true;
        /// <summary>A fixed part id or colour for Choice/Color effects.</summary>
        public string Value = "";
        public UnsupportedReason Reason;
        public string Explanation = "";

        public static ConceptEffect P(string param, float w = 1f, bool primary = true) { return new ConceptEffect { Kind = EffectKind.Param, Key = param, Weight = w, Primary = primary }; }
        public static ConceptEffect A(PlayerAttributeId a, float w = 1f, bool primary = true) { return new ConceptEffect { Kind = EffectKind.Attribute, Attribute = a, Key = a.ToString(), Weight = w, Primary = primary }; }
        public static ConceptEffect Prof(ProfileHintKind k, float w = 1f, bool primary = true) { return new ConceptEffect { Kind = EffectKind.Profile, ProfileKind = k, Key = k.ToString(), Weight = w, Primary = primary }; }
        public static ConceptEffect C(string slot, string part, float w = 1f, bool primary = true) { return new ConceptEffect { Kind = EffectKind.Choice, Key = slot, Value = part, Weight = w, Primary = primary }; }
        public static ConceptEffect Beh(string id, float w = 1f, bool primary = true) { return new ConceptEffect { Kind = EffectKind.Behavior, Key = id, Weight = w, Primary = primary }; }
        public static ConceptEffect Axis(string axis, float w = 1f) { return new ConceptEffect { Kind = EffectKind.StyleAxis, Key = axis, Weight = w, Primary = true }; }
        public static ConceptEffect RoleOf(PlayerArchetype r, PitchZone z) { return new ConceptEffect { Kind = EffectKind.Profile, ProfileKind = ProfileHintKind.Role, Role = r, Zone = z, Key = "Role", Weight = 1f, Primary = true }; }
        public static ConceptEffect U(UnsupportedReason why, string text) { return new ConceptEffect { Kind = EffectKind.Unsupported, Reason = why, Explanation = text, Key = "unsupported" }; }
    }

    /// <summary>What a concept means in one context: which domain/phase/sense it is about, and what it changes there.</summary>
    [Serializable]
    public sealed class ConceptSense
    {
        public SemanticDomain Domain;
        public SemanticPhase Phase;
        /// <summary>A finer reading chosen by the word ("tendency": plays it often; "ability": is good at it). Empty = the default reading.</summary>
        public string Key = "";
        public List<ConceptEffect> Effects = new List<ConceptEffect>();
    }

    [Serializable]
    public sealed class ConceptDefinition
    {
        public string Id = "";
        public ConceptKind Kind;
        public string Description = "";
        public SemanticDomain DefaultDomain;
        public List<ConceptSense> Senses = new List<ConceptSense>();
        /// <summary>What "remove it" does (beard -> no beard; hair -> shaved).</summary>
        public List<ConceptEffect> RemoveEffects = new List<ConceptEffect>();
        /// <summary>Choice concepts: the catalog slot. Replace picks another part of this slot.</summary>
        public string Slot = "";
        public string ColorSlot = "";
        /// <summary>Group concepts (and any concept): key prefixes (parameter ids, slots, colour slots) it covers. "Keep the face" protects them.</summary>
        public string[] Covers = new string[0];

        public ConceptSense AddSense(SemanticDomain domain, SemanticPhase phase, string key, params ConceptEffect[] effects)
        {
            var s = new ConceptSense { Domain = domain, Phase = phase, Key = key ?? "" };
            s.Effects.AddRange(effects);
            Senses.Add(s);
            return s;
        }

        /// <summary>
        /// The sense that fits the context best. Exact domain beats an unspecified one, exact phase beats "any phase", an exact sense key beats the
        /// default. A sense that contradicts the request (another domain, another phase, another key) is never chosen. Null if none fits.
        /// </summary>
        public ConceptSense FindSense(SemanticDomain domain, SemanticPhase phase, string key, out bool exactDomain)
        {
            ConceptSense best = null;
            int bestScore = -1;
            exactDomain = false;
            foreach (ConceptSense s in Senses)
            {
                int score = 0;
                if (domain != SemanticDomain.Unspecified)
                {
                    if (s.Domain == domain) score += 8;
                    else if (s.Domain != SemanticDomain.Unspecified) continue;
                }
                else if (s.Domain == DefaultDomain) score += 4;
                if (phase != SemanticPhase.Any)
                {
                    if (s.Phase == phase) score += 4;
                    else if (s.Phase != SemanticPhase.Any) continue;
                }
                else if (s.Phase != SemanticPhase.Any) continue;
                if (!string.IsNullOrEmpty(key))
                {
                    if (s.Key == key) score += 2;
                    else if (!string.IsNullOrEmpty(s.Key)) continue;
                }
                else if (!string.IsNullOrEmpty(s.Key)) score -= 1;
                if (score > bestScore) { best = s; bestScore = score; exactDomain = domain != SemanticDomain.Unspecified && s.Domain == domain; }
            }
            return best;
        }

        public bool HasDomain(SemanticDomain domain)
        {
            foreach (ConceptSense s in Senses) if (s.Domain == domain) return true;
            return false;
        }

        public bool Covers_(string key)
        {
            foreach (string p in Covers) if (key == p || key.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>Everything the semantic layer can talk about. Data: a new concept is a new row, in no language.</summary>
    public sealed class ConceptCatalog
    {
        private readonly Dictionary<string, ConceptDefinition> byId = new Dictionary<string, ConceptDefinition>(StringComparer.Ordinal);
        private readonly List<ConceptDefinition> ordered = new List<ConceptDefinition>();

        public IReadOnlyList<ConceptDefinition> All => ordered;
        public int Count => ordered.Count;

        public bool TryAdd(ConceptDefinition c)
        {
            if (c == null || string.IsNullOrEmpty(c.Id) || byId.ContainsKey(c.Id)) return false;
            byId.Add(c.Id, c);
            ordered.Add(c);
            return true;
        }

        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }

        public bool TryGet(string id, out ConceptDefinition c)
        {
            if (id == null) { c = null; return false; }
            return byId.TryGetValue(id, out c);
        }

        /// <summary>Checks every effect names something that exists: no concept points at a missing parameter, slot, behaviour or part.</summary>
        public CreatorValidationResult Validate(CreatorCatalogs catalogs)
        {
            var r = new CreatorValidationResult();
            foreach (ConceptDefinition c in ordered)
            {
                foreach (ConceptSense s in c.Senses) CheckEffects(c, s.Effects, catalogs, r);
                CheckEffects(c, c.RemoveEffects, catalogs, r);
                if (c.Kind == ConceptKind.Choice && !catalogs.Appearance.HasSlot(c.Slot)) r.Error(CreatorIssueCode.ChoiceSlotUnknown, c.Id, "Unknown slot '" + c.Slot + "'.");
                if (c.Kind == ConceptKind.Color && !catalogs.Appearance.HasColorSlot(c.ColorSlot)) r.Error(CreatorIssueCode.ColorSlotUnknown, c.Id, "Unknown colour slot '" + c.ColorSlot + "'.");
            }
            return r;
        }

        private static void CheckEffects(ConceptDefinition c, List<ConceptEffect> effects, CreatorCatalogs catalogs, CreatorValidationResult r)
        {
            foreach (ConceptEffect e in effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Param:
                        if (!catalogs.Parameters.Contains(e.Key)) r.Error(CreatorIssueCode.ParameterUnknown, c.Id, "Unknown parameter '" + e.Key + "'.");
                        break;
                    case EffectKind.Choice:
                        if (!catalogs.Appearance.HasSlot(e.Key)) r.Error(CreatorIssueCode.ChoiceSlotUnknown, c.Id, "Unknown slot '" + e.Key + "'.");
                        else if (!string.IsNullOrEmpty(e.Value) && !catalogs.Appearance.TryGetPart(e.Key, e.Value, out PartDefinition _)) r.Error(CreatorIssueCode.ChoicePartUnknown, c.Id, "Unknown part '" + e.Value + "'.");
                        break;
                    case EffectKind.Color:
                        if (!catalogs.Appearance.HasColorSlot(e.Key)) r.Error(CreatorIssueCode.ColorSlotUnknown, c.Id, "Unknown colour slot '" + e.Key + "'.");
                        break;
                    case EffectKind.Behavior:
                        if (!catalogs.Behaviors.Contains(e.Key)) r.Error(CreatorIssueCode.BehaviorUnknown, c.Id, "Unknown behaviour '" + e.Key + "'.");
                        break;
                }
            }
        }
    }

    public static class DefaultConcepts
    {
        private const SemanticDomain Vis = SemanticDomain.Visual;
        private const SemanticDomain Gam = SemanticDomain.Gameplay;
        private const SemanticDomain Ani = SemanticDomain.Animation;
        private const SemanticPhase Any = SemanticPhase.Any;

        private static ConceptDefinition Add(ConceptCatalog c, string id, ConceptKind kind, SemanticDomain domain, string text, params string[] covers)
        {
            var d = new ConceptDefinition { Id = id, Kind = kind, DefaultDomain = domain, Description = text, Covers = covers };
            c.TryAdd(d);
            return d;
        }

        private static ConceptDefinition Vs(ConceptCatalog c, string id, string text, string param, params string[] covers)
        {
            ConceptDefinition d = Add(c, id, ConceptKind.Scalar, Vis, text, covers.Length > 0 ? covers : new[] { param });
            d.AddSense(Vis, Any, "", ConceptEffect.P(param));
            return d;
        }

        public static ConceptCatalog Create()
        {
            var c = new ConceptCatalog();
            ConceptDefinition d;

            // ---------------- visual scalars ----------------
            d = Add(c, "height", ConceptKind.Scalar, Vis, "How tall the character is.", "body.height");
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.height"), ConceptEffect.P("body.legLength", 0.4f, false));
            d.AddSense(Gam, Any, "", ConceptEffect.U(UnsupportedReason.RequiresFutureRuntime, "Height is only a look; it changes no attribute. Use strength or reach words for gameplay."));
            d = Add(c, "mass", ConceptKind.Scalar, Vis, "How heavy / thick the body is.", "body.mass");
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.mass"), ConceptEffect.P("body.torsoWidth", 0.5f, false));
            d = Add(c, "overallSize", ConceptKind.Scalar, Vis, "Overall size of the character (taller and bigger, or shorter and smaller).", "body.height", "body.mass");
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.height"), ConceptEffect.P("body.mass", 0.5f, false), ConceptEffect.P("body.shoulderWidth", 0.3f, false));
            d = Add(c, "build", ConceptKind.Scalar, Vis, "Slim vs broad: mass and muscle together.", "body.mass", "body.muscularity");
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.mass"), ConceptEffect.P("body.muscularity", 0.6f, false), ConceptEffect.P("body.shoulderWidth", 0.3f, false));
            d = Add(c, "muscularity", ConceptKind.Scalar, Vis, "How muscular the body looks.", "body.muscularity");
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.muscularity"), ConceptEffect.P("body.shoulderWidth", 0.5f, false));
            Vs(c, "shoulders", "Shoulder width.", "body.shoulderWidth");
            Vs(c, "legLength", "Leg length.", "body.legLength");
            Vs(c, "armLength", "Arm length.", "body.armLength");
            Vs(c, "handSize", "Hand size.", "body.handScale");
            Vs(c, "footSize", "Foot / boot size.", "body.footScale");
            d = Add(c, "headSize", ConceptKind.Scalar, Vis, "Head size.", "head.scale");
            d.AddSense(Vis, Any, "", ConceptEffect.P("head.scale"));
            Vs(c, "jaw", "Jaw strength.", "head.jaw");
            Vs(c, "chin", "Chin prominence.", "head.chin");
            Vs(c, "cheekbones", "Cheekbones.", "head.cheekbones");
            Vs(c, "forehead", "Forehead height.", "head.forehead");
            Vs(c, "eyeSize", "Eye size.", "face.eyeSize");
            Vs(c, "eyebrowThickness", "Eyebrow thickness.", "face.eyebrowThickness");
            Vs(c, "noseSize", "Nose size.", "face.noseSize");
            Vs(c, "mouthWidth", "Mouth width.", "face.mouthWidth");
            Vs(c, "earSize", "Ear size.", "face.earSize");
            Vs(c, "expressiveness", "How expressive the face is.", "face.expressiveness");
            d = Add(c, "hairLength", ConceptKind.Scalar, Vis, "Hair length.", "hair.length");
            d.AddSense(Vis, Any, "", ConceptEffect.P("hair.length"));
            Vs(c, "hairVolume", "Hair volume.", "hair.volume");
            d = Add(c, "skinTone", ConceptKind.Scalar, Vis, "Skin tone (light to dark).", "skin.tone", "skin.undertone");
            d.AddSense(Vis, Any, "", ConceptEffect.P("skin.tone"));
            d = Add(c, "age", ConceptKind.Scalar, Vis, "Apparent age.");
            d.AddSense(Vis, Any, "", ConceptEffect.U(UnsupportedReason.RequiresAsset, "There is no age parameter or age-specific face assets yet."));

            // ---------------- style ----------------
            d = Add(c, "cartoon", ConceptKind.Scalar, Vis, "Cartoon-ness of the whole look (the style's cartoon axis).", "style.", "head.scale", "face.eyeSize");
            d.AddSense(Vis, Any, "", ConceptEffect.Axis("cartoon"));
            d = Add(c, "realism", ConceptKind.Scalar, Vis, "Realism of the whole look; the opposite end of the cartoon axis.", "style.", "head.scale", "face.eyeSize");
            d.AddSense(Vis, Any, "", ConceptEffect.Axis("cartoon", -1f));
            d = Add(c, "athleticLook", ConceptKind.Scalar, Vis, "Athletic proportions.", "style.athleticity");
            d.AddSense(Vis, Any, "", ConceptEffect.P("style.athleticity"), ConceptEffect.P("body.muscularity", 0.3f, false));
            Vs(c, "exaggeration", "Exaggeration of shapes.", "style.exaggeration");

            // ---------------- choices and colours ----------------
            d = Add(c, "hairStyle", ConceptKind.Choice, Vis, "Which hairstyle.", "hair.style", "hair.texture", "hair.length", "hair.volume");
            d.Slot = "hair.style";
            d.RemoveEffects.Add(ConceptEffect.C("hair.style", "buzz_02"));
            d.RemoveEffects.Add(ConceptEffect.P("hair.length", -1f));
            d = Add(c, "hairTexture", ConceptKind.Choice, Vis, "Straight, wavy, curly...", "hair.texture");
            d.Slot = "hair.texture";
            d = Add(c, "beard", ConceptKind.Choice, Vis, "Facial hair.", "face.beard", "facialHair.");
            d.Slot = "face.beard";
            d.RemoveEffects.Add(ConceptEffect.C("face.beard", "none"));
            d = Add(c, "expression", ConceptKind.Choice, Vis, "Default facial expression.", "face.expression");
            d.Slot = "face.expression";
            d = Add(c, "bodyPreset", ConceptKind.Choice, Vis, "Body preset.", "body.preset");
            d.Slot = "body.preset";
            d = Add(c, "boots", ConceptKind.Choice, Vis, "Boots.", "kit.boots");
            d.Slot = "kit.boots";
            d = Add(c, "hairColor", ConceptKind.Color, Vis, "Hair colour.", "hair.color");
            d.ColorSlot = "hair.color";
            d = Add(c, "eyeColor", ConceptKind.Color, Vis, "Eye colour.", "eyes.color");
            d.ColorSlot = "eyes.color";
            d = Add(c, "kitColor", ConceptKind.Color, Vis, "Main kit colour.", "kit.primary");
            d.ColorSlot = "kit.primary";

            // ---------------- groups (only for keep / replace / remove-whole-area) ----------------
            Add(c, "face", ConceptKind.Group, Vis, "The whole face (head, eyes, brows, nose, mouth, ears).", "head.", "face.", "eyes.", "brows.", "nose.", "mouth.", "ears.", "facialHair.");
            Add(c, "body", ConceptKind.Group, Vis, "The whole body.", "body.");
            Add(c, "hair", ConceptKind.Group, Vis, "The hair (style, length, colour, texture).", "hair.");
            Add(c, "style", ConceptKind.Group, Vis, "The visual style.", "style.");
            Add(c, "kit", ConceptKind.Group, Vis, "The kit (shirt, shorts, socks, boots).", "kit.");
            Add(c, "skin", ConceptKind.Group, Vis, "Skin.", "skin.");
            Add(c, "everything", ConceptKind.Group, SemanticDomain.Unspecified, "Everything.", "");
            ConceptDefinition play = Add(c, "playStyle", ConceptKind.Group, Gam, "How the character plays (every football tendency).", "movement.", "dribbling.", "possession.", "passing.", "shooting.", "positioning.", "decision.", "defending.");
            play.Covers = new[] { "movement.", "dribbling.", "possession.", "passing.", "shooting.", "positioning.", "decision.", "defending." };

            // ---------------- gameplay: abilities (attributes) with their usual companions ----------------
            d = Add(c, "speed", ConceptKind.Scalar, Gam, "Pace.", "movement.accelerationTendency");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Speed), ConceptEffect.A(PlayerAttributeId.Acceleration, 0.5f, false), ConceptEffect.P("movement.accelerationTendency", 0.3f, false));
            d.AddSense(Gam, SemanticPhase.WithBall, "", ConceptEffect.A(PlayerAttributeId.Speed), ConceptEffect.P("dribbling.changeOfPace", 0.3f, false));
            d.AddSense(Gam, SemanticPhase.Sprint, "", ConceptEffect.A(PlayerAttributeId.Speed), ConceptEffect.A(PlayerAttributeId.Stamina, 0.3f, false));
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.legLength", 0.7f), ConceptEffect.P("body.mass", -0.4f, false), ConceptEffect.P("style.athleticity", 0.4f, false));
            d.AddSense(Ani, Any, "", ConceptEffect.U(UnsupportedReason.RequiresFutureRuntime, "Playback speed of animations is not exposed yet (Animation 2.0 contract only)."));
            d = Add(c, "acceleration", ConceptKind.Scalar, Gam, "Burst of speed.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Acceleration), ConceptEffect.P("movement.accelerationTendency", 0.5f, false));
            d = Add(c, "agility", ConceptKind.Scalar, Gam, "Quickness of turning.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Agility), ConceptEffect.P("movement.turningTendency", 0.4f, false));
            d = Add(c, "strength", ConceptKind.Scalar, Gam, "Physical strength.", "body.muscularity");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Strength));
            d.AddSense(Gam, SemanticPhase.Duels, "", ConceptEffect.A(PlayerAttributeId.Strength), ConceptEffect.P("possession.shielding", 0.4f, false), ConceptEffect.P("defending.aggression", 0.3f, false));
            d.AddSense(Vis, Any, "", ConceptEffect.P("body.muscularity", 0.9f), ConceptEffect.P("body.mass", 0.5f, false), ConceptEffect.P("body.shoulderWidth", 0.6f, false), ConceptEffect.C("body.preset", "strong", 1f, false));
            d = Add(c, "stamina", ConceptKind.Scalar, Gam, "Endurance.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Stamina));
            d = Add(c, "shootingSkill", ConceptKind.Scalar, Gam, "How well the character shoots.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Shooting), ConceptEffect.A(PlayerAttributeId.Finishing, 0.4f, false));
            d = Add(c, "finishing", ConceptKind.Scalar, Gam, "Finishing quality.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Finishing));
            d = Add(c, "passingSkill", ConceptKind.Scalar, Gam, "Passing quality.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Passing));
            d = Add(c, "control", ConceptKind.Scalar, Gam, "Ball control.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Control), ConceptEffect.P("dribbling.closeControl", 0.3f, false));
            d = Add(c, "technique", ConceptKind.Scalar, Gam, "Technical quality.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Technique));
            d = Add(c, "defense", ConceptKind.Scalar, Gam, "Defensive ability.");
            d.AddSense(Gam, Any, "", ConceptEffect.A(PlayerAttributeId.Defense), ConceptEffect.P("defending.marking", 0.3f, false));

            // dribbling has two readings: HOW OFTEN it dribbles (tendency) and HOW GOOD it is (ability)
            d = Add(c, "dribbling", ConceptKind.Scalar, Gam, "Dribbling: tendency or ability.", "dribbling.");
            d.AddSense(Gam, Any, "", ConceptEffect.P("dribbling.takeOn"), ConceptEffect.P("dribbling.takeOnRisk", 0.3f, false));
            d.AddSense(Gam, Any, "tendency", ConceptEffect.P("dribbling.takeOn"), ConceptEffect.P("dribbling.takeOnRisk", 0.3f, false));
            d.AddSense(Gam, Any, "ability", ConceptEffect.A(PlayerAttributeId.Dribbling), ConceptEffect.P("dribbling.closeControl", 0.3f, false));

            // ---------------- gameplay: tendencies ----------------
            d = Add(c, "shooting", ConceptKind.Scalar, Gam, "How often it shoots.", "shooting.");
            d.AddSense(Gam, Any, "", ConceptEffect.P("shooting.frequency"), ConceptEffect.P("shooting.longShot", 0.3f, false));
            d = Add(c, "passing", ConceptKind.Scalar, Gam, "How much it passes.", "passing.");
            d.AddSense(Gam, Any, "", ConceptEffect.P("passing.short"), ConceptEffect.P("passing.safe", 0.4f, false));
            d = Add(c, "risk", ConceptKind.Scalar, Gam, "Taking risks.", "passing.risky");
            d.AddSense(Gam, Any, "", ConceptEffect.Prof(ProfileHintKind.Risk), ConceptEffect.P("passing.risky", 0.5f, false), ConceptEffect.P("dribbling.takeOnRisk", 0.5f, false));
            d = Add(c, "creativity", ConceptKind.Scalar, Gam, "Creative play.", "passing.throughBall");
            d.AddSense(Gam, Any, "", ConceptEffect.Prof(ProfileHintKind.Creativity), ConceptEffect.P("passing.throughBall", 0.4f, false), ConceptEffect.P("dribbling.bodyFeint", 0.3f, false));
            d = Add(c, "aggression", ConceptKind.Scalar, Gam, "Aggression.", "defending.aggression");
            d.AddSense(Gam, Any, "", ConceptEffect.Prof(ProfileHintKind.Aggression), ConceptEffect.P("defending.aggression", 0.5f, false), ConceptEffect.P("movement.aggression", 0.4f, false));
            d = Add(c, "pressing", ConceptKind.Scalar, Gam, "Pressing the ball.", "defending.pressing");
            d.AddSense(Gam, Any, "", ConceptEffect.P("defending.pressing"), ConceptEffect.P("defending.aggression", 0.3f, false));
            d = Add(c, "patience", ConceptKind.Scalar, Gam, "Waiting for the moment.", "decision.patience");
            d.AddSense(Gam, Any, "", ConceptEffect.P("decision.patience"));
            d = Add(c, "directness", ConceptKind.Scalar, Gam, "Playing direct.", "decision.directness");
            d.AddSense(Gam, Any, "", ConceptEffect.P("decision.directness"));
            d = Add(c, "width", ConceptKind.Scalar, Gam, "Staying wide.", "positioning.width");
            d.AddSense(Gam, Any, "", ConceptEffect.P("positioning.width"));
            d = Add(c, "crossing", ConceptKind.Scalar, Gam, "Crossing.", "passing.cross");
            d.AddSense(Gam, Any, "", ConceptEffect.P("passing.cross"));
            d = Add(c, "supportRuns", ConceptKind.Scalar, Gam, "Moving to support.", "movement.supportMovement");
            d.AddSense(Gam, Any, "", ConceptEffect.P("movement.supportMovement"), ConceptEffect.P("positioning.attackingRuns", 0.3f, false));

            // ---------------- signature behaviours (add / remove) ----------------
            Behavior(c, "behavior.stopAndGo", DefaultBehaviors.StopAndGo);
            Behavior(c, "behavior.bodyFeint", DefaultBehaviors.BodyFeint);
            Behavior(c, "behavior.explosiveExit", DefaultBehaviors.ExplosiveExit);
            Behavior(c, "behavior.delayedRun", DefaultBehaviors.DelayedRun);
            Behavior(c, "behavior.blindSideRun", DefaultBehaviors.BlindSideRun);
            Behavior(c, "behavior.insideCut", DefaultBehaviors.InsideCut);
            Behavior(c, "behavior.outsideCut", DefaultBehaviors.OutsideCut);
            Behavior(c, "behavior.longShot", DefaultBehaviors.LongRangeShot);
            Behavior(c, "behavior.firstTimeFinish", DefaultBehaviors.FirstTimeFinish);
            Behavior(c, "behavior.holdUp", DefaultBehaviors.HoldUpPlay);
            Behavior(c, "behavior.oneTouch", DefaultBehaviors.OneTouchCombination);
            Behavior(c, "behavior.creativePass", DefaultBehaviors.CreativePass);
            Behavior(c, "behavior.throughBall", DefaultBehaviors.RiskyThroughBall);
            Behavior(c, "behavior.press", DefaultBehaviors.AggressivePress);
            Behavior(c, "behavior.lateBoxArrival", DefaultBehaviors.LateBoxArrival);

            // ---------------- roles ----------------
            Role(c, "role.winger", PlayerArchetype.Winger, PitchZone.Wing, "positioning.width", "dribbling.outsideCut");
            Role(c, "role.striker", PlayerArchetype.GoalHunter, PitchZone.Attack, "shooting.frequency", "positioning.boxPresence");
            Role(c, "role.playmaker", PlayerArchetype.Architect, PitchZone.Midfield, "passing.progressive", "passing.throughBall");
            Role(c, "role.midfielder", PlayerArchetype.Engine, PitchZone.Midfield, "movement.supportMovement", "defending.pressing");
            Role(c, "role.defender", PlayerArchetype.Wall, PitchZone.Defense, "positioning.defensive", "defending.marking");
            Role(c, "role.goalkeeper", PlayerArchetype.Guardian, PitchZone.Goal);
            Role(c, "role.targetMan", PlayerArchetype.Target, PitchZone.Attack, "possession.holdUp", "positioning.boxPresence");
            return c;
        }

        private static void Behavior(ConceptCatalog c, string id, string behaviorId)
        {
            ConceptDefinition d = Add(c, id, ConceptKind.Scalar, Gam, "Signature behaviour " + behaviorId + ".");
            d.AddSense(Gam, Any, "", ConceptEffect.Beh(behaviorId));
            d.RemoveEffects.Add(ConceptEffect.Beh(behaviorId));
        }

        private static void Role(ConceptCatalog c, string id, PlayerArchetype role, PitchZone zone, params string[] tendencies)
        {
            ConceptDefinition d = Add(c, id, ConceptKind.Role, Gam, "The " + role + " role.");
            var effects = new List<ConceptEffect> { ConceptEffect.RoleOf(role, zone) };
            foreach (string t in tendencies) effects.Add(ConceptEffect.P(t, 0.6f, false));
            d.AddSense(Gam, Any, "", effects.ToArray());
        }
    }
}
