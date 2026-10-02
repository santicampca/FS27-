using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum EffectType
    {
        /// <summary>Move a scalar parameter towards <see cref="LexiconEffect.Level"/> (0..1 of its range).</summary>
        Param,
        Choice,
        Color,
        Behavior,
        Attribute,
        Profile,
        /// <summary>Move along the cartoon style axis (Level above 0.5 = more cartoon).</summary>
        Style,
        /// <summary>Not "not childish"-style guard: limits how far head/eyes/hands/feet grow in the same request.</summary>
        GuardChildlike,
        Unsupported
    }

    /// <summary>One thing a word or phrase means, written as DATA.</summary>
    public sealed class LexiconEffect
    {
        public EffectType Type;
        public string Target = "";
        public string Value = "";
        /// <summary>0..1: where on the parameter's range the word points (0.85 = high, 0.15 = low).</summary>
        public float Level = 0.5f;
        public PlayerAttributeId Attribute;
        public ProfileHintKind ProfileKind;
        public PitchZone Zone;
        public PlayerArchetype Role;
        public UnsupportedReason Reason;
        public string Explanation = "";

        public static LexiconEffect P(string target, float level) { return new LexiconEffect { Type = EffectType.Param, Target = target, Level = level }; }
        public static LexiconEffect C(string slot, string part) { return new LexiconEffect { Type = EffectType.Choice, Target = slot, Value = part }; }
        public static LexiconEffect Col(string slot, string hex) { return new LexiconEffect { Type = EffectType.Color, Target = slot, Value = hex }; }
        public static LexiconEffect B(string behaviorId, float weight) { return new LexiconEffect { Type = EffectType.Behavior, Target = behaviorId, Level = weight }; }
        public static LexiconEffect A(PlayerAttributeId a, float level) { return new LexiconEffect { Type = EffectType.Attribute, Attribute = a, Level = level }; }
        public static LexiconEffect Prof(ProfileHintKind k, float level) { return new LexiconEffect { Type = EffectType.Profile, ProfileKind = k, Level = level }; }
        public static LexiconEffect Zone_(PitchZone z) { return new LexiconEffect { Type = EffectType.Profile, ProfileKind = ProfileHintKind.PrimaryZone, Zone = z, Level = 1f }; }
        public static LexiconEffect Role_(PlayerArchetype r, float affinity) { return new LexiconEffect { Type = EffectType.Profile, ProfileKind = ProfileHintKind.Role, Role = r, Level = affinity }; }
        public static LexiconEffect S(float level) { return new LexiconEffect { Type = EffectType.Style, Target = "cartoon", Level = level }; }
        public static LexiconEffect U(UnsupportedReason reason, string why) { return new LexiconEffect { Type = EffectType.Unsupported, Reason = reason, Explanation = why }; }
    }

    /// <summary>A word or phrase and what it means in each context. <see cref="Visual"/> / <see cref="Animation"/> are used when the request is clearly about that.</summary>
    public sealed class LexiconEntry
    {
        public string[] Phrases;
        public LexiconEffect[] Default;
        public LexiconEffect[] Visual;
        public LexiconEffect[] Animation;
        public bool Ambiguous => Visual != null || Animation != null;
    }

    /// <summary>
    /// The vocabulary of the deterministic interpreter: Spanish and some English, as data. It is a lexicon, not language understanding:
    /// it recognises the words it lists and says so when it does not. A language-model interpreter would replace it behind
    /// <see cref="IAICharacterInterpreter"/> without anything else changing.
    /// </summary>
    public static class PromptLexicon
    {
        private static LexiconEffect[] E(params LexiconEffect[] e) { return e; }
        private static LexiconEntry L(string[] phrases, params LexiconEffect[] effects) { return new LexiconEntry { Phrases = phrases, Default = effects }; }
        private static string[] W(params string[] p) { return p; }

        // ---- Standalone phrases (match anywhere in a clause, longest first) ----
        public static readonly List<LexiconEntry> Phrases = BuildPhrases();

        private static List<LexiconEntry> BuildPhrases()
        {
            var l = new List<LexiconEntry>();

            // ----- body (visual) -----
            l.Add(L(W("pequeno", "bajito", "chaparro"), LexiconEffect.P("body.height", 0.12f), LexiconEffect.C("body.preset", "compact")));
            l.Add(L(W("alto", "altura", "tall"), LexiconEffect.P("body.height", 0.88f), LexiconEffect.C("body.preset", "tall")));
            l.Add(L(W("delgado", "flaco", "esbelto", "slim", "thin"), LexiconEffect.P("body.mass", 0.15f), LexiconEffect.P("body.muscularity", 0.30f)));
            l.Add(L(W("musculoso", "musculado", "muscular"), LexiconEffect.P("body.muscularity", 0.88f), LexiconEffect.P("body.shoulderWidth", 0.7f)));
            l.Add(L(W("ligero", "liviano", "light"), LexiconEffect.P("body.mass", 0.15f), LexiconEffect.C("body.preset", "light")));
            l.Add(L(W("atletico", "atletica", "atleta", "athletic"), LexiconEffect.P("style.athleticity", 0.92f), LexiconEffect.C("body.preset", "athletic")));
            l.Add(L(W("compacto"), LexiconEffect.C("body.preset", "compact"), LexiconEffect.P("body.height", 0.3f)));
            l.Add(new LexiconEntry
            {
                Phrases = W("fuerte", "robusto", "corpulento", "strong"),
                // gameplay: strong = Strength; visual: strong = a bulkier build
                Default = E(LexiconEffect.A(PlayerAttributeId.Strength, 0.88f), LexiconEffect.P("body.muscularity", 0.78f), LexiconEffect.P("body.mass", 0.68f), LexiconEffect.C("body.preset", "strong")),
                Visual = E(LexiconEffect.P("body.muscularity", 0.78f), LexiconEffect.P("body.mass", 0.68f), LexiconEffect.P("body.shoulderWidth", 0.72f), LexiconEffect.C("body.preset", "strong")),
                Animation = null
            });

            // ----- speed: three different meanings, never mixed -----
            l.Add(new LexiconEntry
            {
                Phrases = W("rapido", "rapida", "veloz", "fast", "quick"),
                Default = E(LexiconEffect.A(PlayerAttributeId.Speed, 0.88f), LexiconEffect.A(PlayerAttributeId.Acceleration, 0.72f)),
                Visual = E(LexiconEffect.P("body.legLength", 0.7f), LexiconEffect.P("body.mass", 0.4f)),
                Animation = E(LexiconEffect.U(UnsupportedReason.RequiresFutureRuntime,
                    "Animation speed comes from the player's movement (Speed / Acceleration) through the animation system, which is not implemented yet; it is not a setting of the character."))
            });
            l.Add(L(W("lento", "lenta", "slow"), LexiconEffect.A(PlayerAttributeId.Speed, 0.2f), LexiconEffect.A(PlayerAttributeId.Acceleration, 0.3f)));

            // ----- style -----
            l.Add(L(W("cartoon", "caricaturesco", "caricaturesca"), LexiconEffect.S(0.9f)));
            l.Add(L(W("realista", "realistico", "realistic"), LexiconEffect.S(0.1f)));
            l.Add(L(W("expresivo", "expresiva", "expresivos", "expresivas", "expressive"), LexiconEffect.P("face.expressiveness", 0.85f), LexiconEffect.P("style.expressiveness", 0.8f)));
            l.Add(L(W("no infantil", "sin parecer infantil", "nada infantil", "not childish"), new LexiconEffect { Type = EffectType.GuardChildlike }));

            // ----- playing character (gameplay) -----
            l.Add(L(W("explosivo", "explosiva", "explosive"),
                LexiconEffect.A(PlayerAttributeId.Acceleration, 0.92f), LexiconEffect.A(PlayerAttributeId.Agility, 0.72f),
                LexiconEffect.P("movement.accelerationTendency", 0.88f), LexiconEffect.P("dribbling.changeOfPace", 0.78f),
                LexiconEffect.B(DefaultBehaviors.ExplosiveExit, 0.78f), LexiconEffect.Role_(PlayerArchetype.Explosive, 0.9f)));
            l.Add(L(W("extremo", "extremos", "winger"), LexiconEffect.Zone_(PitchZone.Wing), LexiconEffect.Role_(PlayerArchetype.Winger, 0.85f), LexiconEffect.P("positioning.width", 0.82f)));
            l.Add(L(W("delantero", "delanteros", "striker", "forward"), LexiconEffect.Zone_(PitchZone.Attack), LexiconEffect.P("positioning.depth", 0.8f), LexiconEffect.P("positioning.boxPresence", 0.7f)));
            l.Add(L(W("uno contra uno", "1 contra 1", "1v1", "uno a uno", "one on one"),
                LexiconEffect.A(PlayerAttributeId.Dribbling, 0.88f), LexiconEffect.A(PlayerAttributeId.Agility, 0.75f),
                LexiconEffect.P("dribbling.takeOn", 0.92f), LexiconEffect.B(DefaultBehaviors.BodyFeint, 0.72f)));
            l.Add(L(W("cambios de ritmo", "cambio de ritmo", "change of pace"),
                LexiconEffect.P("dribbling.changeOfPace", 0.92f), LexiconEffect.B(DefaultBehaviors.StopAndGo, 0.78f), LexiconEffect.B(DefaultBehaviors.ExplosiveExit, 0.75f),
                LexiconEffect.A(PlayerAttributeId.Acceleration, 0.82f)));
            l.Add(L(W("regate", "regates", "regatear", "regateador", "dribbling"), LexiconEffect.A(PlayerAttributeId.Dribbling, 0.88f), LexiconEffect.P("dribbling.takeOn", 0.85f), LexiconEffect.A(PlayerAttributeId.Technique, 0.7f)));
            l.Add(L(W("encarar", "encare", "encara", "frente a un defensor", "frente al defensor", "take on"), LexiconEffect.P("dribbling.takeOn", 0.9f), LexiconEffect.B(DefaultBehaviors.BodyFeint, 0.6f)));
            l.Add(L(W("atacar hacia dentro", "ataque hacia dentro", "hacia dentro", "hacia adentro", "cortar hacia dentro", "inside cut"),
                LexiconEffect.P("dribbling.insideCut", 0.92f), LexiconEffect.B(DefaultBehaviors.InsideCut, 0.85f), LexiconEffect.P("positioning.halfSpace", 0.75f)));
            l.Add(L(W("recibir abierto", "recibir abierta", "recibe abierto", "abierto"), LexiconEffect.P("positioning.width", 0.88f)));
            l.Add(L(W("creativo", "creativa", "creatividad", "creative"), LexiconEffect.Prof(ProfileHintKind.Creativity, 0.88f), LexiconEffect.A(PlayerAttributeId.Technique, 0.75f)));
            l.Add(L(W("arriesgado", "arriesgada", "arriesgar", "riesgo", "risky"), LexiconEffect.Prof(ProfileHintKind.Risk, 0.88f), LexiconEffect.P("dribbling.takeOnRisk", 0.82f), LexiconEffect.P("passing.risky", 0.72f)));
            l.Add(L(W("atacar el espacio", "ataque el espacio", "ataca el espacio", "atacar espacios", "ataque mucho el espacio", "attack space"),
                LexiconEffect.P("movement.spaceSeeking", 0.92f), LexiconEffect.P("movement.aggression", 0.82f), LexiconEffect.P("positioning.attackingRuns", 0.88f)));
            l.Add(L(W("bajar a recibir", "baje a recibir", "baja a recibir", "bajando a recibir", "drop deep"), LexiconEffect.P("positioning.dropping", 0.88f), LexiconEffect.P("decision.scanning", 0.65f)));
            l.Add(L(W("atacar el area", "ataque el area", "ataca el area", "atacar el area rival"), LexiconEffect.P("positioning.boxPresence", 0.88f), LexiconEffect.P("positioning.attackingRuns", 0.82f)));
            l.Add(L(W("aparecer tarde en el area", "aparezca tarde en el area", "aparece tarde en el area", "llegar tarde al area", "llegada tarde"),
                LexiconEffect.P("movement.delayedRuns", 0.88f), LexiconEffect.B(DefaultBehaviors.LateBoxArrival, 0.82f), LexiconEffect.P("positioning.boxPresence", 0.8f)));
            l.Add(L(W("proteger la pelota", "proteja la pelota", "protege la pelota", "proteger el balon", "proteja el balon", "protege el balon", "shield the ball"),
                LexiconEffect.P("possession.shielding", 0.88f), LexiconEffect.P("possession.holdUp", 0.82f), LexiconEffect.B(DefaultBehaviors.HoldUpPlay, 0.78f), LexiconEffect.A(PlayerAttributeId.Strength, 0.8f)));
            l.Add(L(W("definiendo", "definir", "definicion", "definidor", "finalizando", "finalizar", "finishing"),
                LexiconEffect.A(PlayerAttributeId.Finishing, 0.92f), LexiconEffect.A(PlayerAttributeId.Shooting, 0.82f), LexiconEffect.P("shooting.insideBox", 0.82f),
                LexiconEffect.P("shooting.firstTime", 0.78f), LexiconEffect.B(DefaultBehaviors.FirstTimeFinish, 0.78f)));
            l.Add(L(W("tiro de larga distancia", "tiros de larga distancia", "disparo de media distancia", "long shots", "remates de lejos"),
                LexiconEffect.P("shooting.longShot", 0.88f), LexiconEffect.B(DefaultBehaviors.LongRangeShot, 0.8f), LexiconEffect.A(PlayerAttributeId.Shooting, 0.8f)));
            l.Add(L(W("tecnico", "tecnica", "technical"), LexiconEffect.A(PlayerAttributeId.Technique, 0.88f), LexiconEffect.A(PlayerAttributeId.Control, 0.8f)));
            l.Add(L(W("directo", "directa", "direct"), LexiconEffect.P("decision.directness", 0.85f)));
            l.Add(L(W("paciente", "patient"), LexiconEffect.P("decision.patience", 0.85f)));
            l.Add(L(W("a un toque", "al primer toque", "one touch"), LexiconEffect.P("passing.oneTouch", 0.88f), LexiconEffect.B(DefaultBehaviors.OneTouchCombination, 0.75f)));
            l.Add(L(W("pase filtrado", "pases filtrados", "pase entre lineas", "through ball"), LexiconEffect.P("passing.throughBall", 0.88f), LexiconEffect.B(DefaultBehaviors.RiskyThroughBall, 0.75f), LexiconEffect.A(PlayerAttributeId.Passing, 0.82f)));
            l.Add(new LexiconEntry
            {
                Phrases = W("agresivo", "agresiva", "agresivos", "aggressive"),
                Default = E(LexiconEffect.Prof(ProfileHintKind.Aggression, 0.88f), LexiconEffect.P("defending.aggression", 0.82f), LexiconEffect.P("defending.pressing", 0.72f), LexiconEffect.B(DefaultBehaviors.AggressivePress, 0.72f)),
                Visual = E(LexiconEffect.C("face.expression", "intense"), LexiconEffect.P("head.jaw", 0.78f), LexiconEffect.P("face.eyebrowThickness", 0.75f)),
                Animation = null
            });

            // ----- things the engine cannot do: say so, never pretend -----
            l.Add(L(W("chilena", "bicicleta", "rabona", "taco", "tacon"), LexiconEffect.U(UnsupportedReason.RequiresAsset, "A new football animation needs a new animation asset and a gameplay action that do not exist yet.")));
            l.Add(L(W("animacion nueva", "nueva animacion", "animacion completamente nueva", "celebracion nueva", "celebracion"), LexiconEffect.U(UnsupportedReason.RequiresAsset, "Creating a new animation is not something the Creator Engine can generate: it needs an animation asset.")));
            l.Add(L(W("como el de verdad", "identico a", "igual que el real", "clon de"), LexiconEffect.U(UnsupportedReason.NotUnderstood, "FS27 characters are original; copying a real person's likeness is not supported.")));
            return l;
        }

        // ---- Subject-bound descriptors: "pelo corto", "ojos grandes", "camiseta roja" ----
        public static readonly Dictionary<string, string> Subjects = new Dictionary<string, string>
        {
            { "cabeza", "head" }, { "ojos", "eyes" }, { "ojo", "eyes" }, { "manos", "hands" }, { "mano", "hands" },
            { "botas", "feet" }, { "pies", "feet" }, { "pelo", "hair" }, { "cabello", "hair" }, { "nariz", "nose" }, { "boca", "mouth" },
            { "piel", "skin" }, { "camiseta", "shirt" }, { "pantalon", "shorts" }, { "shorts", "shorts" }, { "medias", "socks" }, { "calcetines", "socks" },
            { "guantes", "gloves" }
        };

        /// <summary>Descriptors per subject. Colour words are handled separately (<see cref="Colors"/>).</summary>
        public static readonly Dictionary<string, Dictionary<string, LexiconEffect[]>> Descriptors = BuildDescriptors();

        private static Dictionary<string, Dictionary<string, LexiconEffect[]>> BuildDescriptors()
        {
            var d = new Dictionary<string, Dictionary<string, LexiconEffect[]>>();
            d["head"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grande", E(LexiconEffect.P("head.scale", 0.78f)) }, { "grandes", E(LexiconEffect.P("head.scale", 0.78f)) },
                { "pequena", E(LexiconEffect.P("head.scale", 0.18f)) }, { "pequeno", E(LexiconEffect.P("head.scale", 0.18f)) },
                { "ancha", E(LexiconEffect.P("head.width", 0.78f)) }, { "estrecha", E(LexiconEffect.P("head.width", 0.22f)) },
                { "redonda", E(LexiconEffect.C("head.shape", "round")) }, { "angulosa", E(LexiconEffect.C("head.shape", "angular")) }, { "ovalada", E(LexiconEffect.C("head.shape", "athletic_oval")) }
            };
            d["eyes"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grandes", E(LexiconEffect.P("face.eyeSize", 0.8f)) }, { "pequenos", E(LexiconEffect.P("face.eyeSize", 0.2f)) },
                { "expresivos", E(LexiconEffect.P("face.eyeSize", 0.65f), LexiconEffect.P("face.expressiveness", 0.85f), LexiconEffect.C("face.eyeShape", "sport_expressive")) },
                { "separados", E(LexiconEffect.P("face.eyeSpacing", 0.78f)) }, { "juntos", E(LexiconEffect.P("face.eyeSpacing", 0.22f)) },
                { "rasgados", E(LexiconEffect.C("face.eyeShape", "narrow_sharp"), LexiconEffect.P("face.eyeTilt", 0.75f)) }
            };
            d["hands"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grandes", E(LexiconEffect.P("body.handScale", 0.8f)) }, { "pequenas", E(LexiconEffect.P("body.handScale", 0.15f)) }
            };
            d["feet"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grandes", E(LexiconEffect.P("body.footScale", 0.8f)) }, { "pequenas", E(LexiconEffect.P("body.footScale", 0.15f)) }
            };
            d["hair"] = new Dictionary<string, LexiconEffect[]>
            {
                { "corto", E(LexiconEffect.P("hair.length", 0.12f)) }, { "largo", E(LexiconEffect.P("hair.length", 0.85f)) },
                { "rizado", E(LexiconEffect.C("hair.texture", "curly")) }, { "liso", E(LexiconEffect.C("hair.texture", "straight")) },
                { "ondulado", E(LexiconEffect.C("hair.texture", "wavy")) }, { "afro", E(LexiconEffect.C("hair.style", "afro_08"), LexiconEffect.C("hair.texture", "coily")) },
                { "voluminoso", E(LexiconEffect.P("hair.volume", 0.82f)) }, { "rapado", E(LexiconEffect.C("hair.style", "buzz_02"), LexiconEffect.P("hair.length", 0.0f)) },
                { "degradado", E(LexiconEffect.C("hair.style", "fade_06")) }
            };
            d["nose"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grande", E(LexiconEffect.P("face.noseSize", 0.8f)) }, { "pequena", E(LexiconEffect.P("face.noseSize", 0.2f)) },
                { "ancha", E(LexiconEffect.P("face.noseWidth", 0.8f)) }, { "fina", E(LexiconEffect.P("face.noseWidth", 0.2f)) }
            };
            d["mouth"] = new Dictionary<string, LexiconEffect[]>
            {
                { "grande", E(LexiconEffect.P("face.mouthWidth", 0.8f)) }, { "pequena", E(LexiconEffect.P("face.mouthWidth", 0.2f)) },
                { "ancha", E(LexiconEffect.P("face.mouthWidth", 0.8f)) }
            };
            d["skin"] = new Dictionary<string, LexiconEffect[]>
            {
                { "clara", E(LexiconEffect.P("skin.tone", 0.12f)) }, { "palida", E(LexiconEffect.P("skin.tone", 0.05f)) },
                { "morena", E(LexiconEffect.P("skin.tone", 0.62f)) }, { "oscura", E(LexiconEffect.P("skin.tone", 0.88f)) }
            };
            return d;
        }

        /// <summary>Colour words and the slot each subject paints. Hex values are starting colours, editable data.</summary>
        public static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        {
            { "rojo", "#D62828" }, { "roja", "#D62828" }, { "azul", "#1D4ED8" }, { "celeste", "#38BDF8" }, { "verde", "#15803D" },
            { "amarillo", "#FACC15" }, { "amarilla", "#FACC15" }, { "blanco", "#F5F5F5" }, { "blanca", "#F5F5F5" }, { "negro", "#1A1A1A" }, { "negra", "#1A1A1A" },
            { "naranja", "#F97316" }, { "morado", "#7E22CE" }, { "morada", "#7E22CE" }, { "rosa", "#EC4899" }, { "gris", "#6B7280" },
            { "castano", "#5A3825" }, { "rubio", "#D9B65C" }, { "rubia", "#D9B65C" }, { "pelirrojo", "#B7410E" }
        };

        public static readonly Dictionary<string, string> ColorSlotOfSubject = new Dictionary<string, string>
        {
            { "shirt", "kit.primary" }, { "shorts", "kit.secondary" }, { "socks", "kit.accent" }, { "feet", "kit.boots" }, { "gloves", "kit.gloves" }, { "hair", "hair.color" }
        };
    }
}
