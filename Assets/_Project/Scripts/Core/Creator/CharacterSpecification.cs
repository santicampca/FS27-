using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Everything about how a player LOOKS, as data. Independent of football data and of any renderer: it names a style, a base model, scale
    /// parameters and parts (by id) and colours; it never holds a mesh or a texture. Shirt number is NOT here (it is the player's own
    /// <see cref="PlayerDefinition.ShirtNumber"/>), nor is body build (<see cref="PlayerDefinition.BodyType"/>), which only gives defaults.
    /// </summary>
    [Serializable]
    public sealed class PlayerAppearance
    {
        public string StyleId = DefaultStyles.CartoonSports;
        /// <summary>Scale / slider values (<see cref="ParameterDomain.Appearance"/>), only those that differ from neutral.</summary>
        public ParameterSet Params = new ParameterSet();
        /// <summary>Selected part per slot, by id (hair.style, face.expression, kit.shirt, kit.boots...).</summary>
        public SortedDictionary<string, string> Choices = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Colours per slot as "#RRGGBB" (hair.color, kit.primary, kit.secondary...).</summary>
        public SortedDictionary<string, string> Colors = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public PlayerAppearance Clone()
        {
            return new PlayerAppearance
            {
                StyleId = StyleId,
                Params = Params.Clone(),
                Choices = new SortedDictionary<string, string>(Choices, StringComparer.Ordinal),
                Colors = new SortedDictionary<string, string>(Colors, StringComparer.Ordinal)
            };
        }
    }

    /// <summary>
    /// A real-player-inspired reference, as AUTHORING data only: a short description of what was observed and how sure we are. It never
    /// contains videos, pages or images, and it is not shipped in the game (see <see cref="CharacterSpecification.ForRuntime"/>).
    /// </summary>
    [Serializable]
    public sealed class ReferenceProfile
    {
        public string SourceDescription = "";
        public string AnalysisNotes = "";
        public string Provenance = "";
        public float Confidence;
        public string GeneratedDate = "";
    }

    /// <summary>Where a specification came from. Authoring data: removed from the runtime package.</summary>
    [Serializable]
    public sealed class AuthoringData
    {
        /// <summary>manual | prompt | import | preset</summary>
        public string Source = "manual";
        /// <summary>Who produced it (an interpreter or provider name; never a key or secret).</summary>
        public string Generator = "";
        public string CreatedUtc = "";
        public string Prompt = "";
        public string Notes = "";
        public ReferenceProfile Reference;
    }

    /// <summary>
    /// The central contract of the Creator Engine: a complete, renderer-independent, serialisable description of a character. A prompt
    /// interpreter, an editor, an importer or a person can all produce one; the engine validates it and only then uses it.
    /// </summary>
    [Serializable]
    public sealed class CharacterSpecification
    {
        public const string CurrentSchema = "FS27.CharacterSpecification.v2";
        public const string SchemaV1 = "FS27.CharacterSpecification.v1";

        public string SchemaVersion = CurrentSchema;
        public string CharacterId;
        /// <summary>The <see cref="PlayerDefinition"/> this character is the look and style of (empty for a spec not yet assigned).</summary>
        public string PlayerId = "";
        public string BaseModelId = DefaultAppearanceCatalog.BaseA;
        public PlayerAppearance Appearance = new PlayerAppearance();
        public FootballDNA Dna = new FootballDNA();
        /// <summary>Seeds that make generation reproducible (0 = none was used). Same specification + same seeds = same character, bit for bit.</summary>
        public uint GenerationSeed;
        public uint AppearanceSeed;
        public uint BehaviorSeed;
        /// <summary>Authoring-only information; null in runtime data.</summary>
        public AuthoringData Authoring;

        public CharacterSpecification Clone()
        {
            return new CharacterSpecification
            {
                SchemaVersion = SchemaVersion,
                CharacterId = CharacterId,
                PlayerId = PlayerId,
                BaseModelId = BaseModelId,
                Appearance = Appearance.Clone(),
                Dna = Dna.Clone(),
                GenerationSeed = GenerationSeed, AppearanceSeed = AppearanceSeed, BehaviorSeed = BehaviorSeed,
                Authoring = Authoring == null ? null : new AuthoringData
                {
                    Source = Authoring.Source, Generator = Authoring.Generator, CreatedUtc = Authoring.CreatedUtc, Prompt = Authoring.Prompt, Notes = Authoring.Notes,
                    Reference = Authoring.Reference == null ? null : new ReferenceProfile
                    {
                        SourceDescription = Authoring.Reference.SourceDescription, AnalysisNotes = Authoring.Reference.AnalysisNotes,
                        Provenance = Authoring.Reference.Provenance, Confidence = Authoring.Reference.Confidence, GeneratedDate = Authoring.Reference.GeneratedDate
                    }
                }
            };
        }

        /// <summary>The runtime version: the same character without any authoring data (prompt, notes, reference profile).</summary>
        public CharacterSpecification ForRuntime()
        {
            CharacterSpecification c = Clone();
            c.Authoring = null;
            return c;
        }
    }

    /// <summary>All the catalogs the Creator Engine validates and interprets against. One bundle so they travel together.</summary>
    public sealed class CreatorCatalogs
    {
        public ParameterCatalog Parameters;
        public AppearanceCatalog Appearance;
        public StyleCatalog Styles;
        public SignatureBehaviorCatalog Behaviors;

        public static CreatorCatalogs CreateDefault()
        {
            return new CreatorCatalogs
            {
                Parameters = DefaultParameters.Create(),
                Appearance = DefaultAppearanceCatalog.Create(),
                Styles = DefaultStyles.Create(),
                Behaviors = DefaultBehaviors.Create()
            };
        }

        /// <summary>A new specification with the style's starting look and neutral football DNA.</summary>
        public CharacterSpecification NewSpecification(string characterId, string styleId = DefaultStyles.CartoonSports)
        {
            var spec = new CharacterSpecification { CharacterId = characterId };
            spec.Appearance.StyleId = styleId;
            if (Styles.TryGet(styleId, out StylePreset style))
            {
                foreach (KeyValuePair<string, float> kv in style.StartLevels)
                    if (Parameters.TryGet(kv.Key, out ParameterDefinition p)) spec.Appearance.Params.Set(Parameters, kv.Key, p.FromLevel(kv.Value));
            }
            spec.Appearance.Choices["body.preset"] = "athletic";
            spec.Appearance.Choices["face.expression"] = "determined";
            return spec;
        }
    }
}
