using System;
using System.Collections.Generic;

namespace FS27.Core
{
    // =====================================================================================================================
    // CONTRACTS ONLY. Nothing here builds a mesh, rig, animator or GameObject: that needs Unity and assets (Creator Engine roadmap
    // phases A-H). These types fix the shape of the pipeline so the second half can be added without changing the first.
    //   CharacterSpecification -> (appearance resolver) -> ResolvedCharacter -> (assembler, Unity) -> runtime character
    // Core never references Unity; the Unity side implements ICharacterAssembler<TRuntime> with TRuntime = a GameObject wrapper.
    // =====================================================================================================================

    /// <summary>A specification with every id resolved against the catalogs and everything left at neutral filled in. Still data: no assets.</summary>
    public sealed class ResolvedCharacter
    {
        public string CharacterId;
        public string PlayerId;
        public string BaseModelId;
        public string RigId;
        public string StyleId;
        /// <summary>Part per slot (every slot of the catalog that the character fills).</summary>
        public SortedDictionary<string, string> Parts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Every appearance parameter with its final value (defaults filled in).</summary>
        public SortedDictionary<string, float> Scales = new SortedDictionary<string, float>(StringComparer.Ordinal);
        public SortedDictionary<string, string> Colors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<string> Warnings = new List<string>();
    }

    /// <summary>Spec -> resolved data. The default implementation is pure (<see cref="CatalogAppearanceResolver"/>).</summary>
    public interface ICharacterAppearanceResolver
    {
        ResolvedCharacter Resolve(CharacterSpecification specification);
    }

    /// <summary>Which rig a base model uses. Many characters share one rig; this is how retargeting finds it.</summary>
    public interface ICharacterRigResolver
    {
        bool TryResolveRig(string baseModelId, out string rigId);
    }

    /// <summary>A named set of shared animations a rig can play, and the tags it offers (idle, run, feint, shot...). No animations exist yet.</summary>
    public sealed class AnimationSetReference
    {
        public string LibraryId;
        public string RigId;
        public List<string> Tags = new List<string>();
    }

    public interface IAnimationResolver
    {
        /// <summary>The shared animation set for a character, plus how this particular body should modulate it.</summary>
        bool TryResolve(ResolvedCharacter character, MovementSignature signature, out AnimationSetReference animations);
    }

    /// <summary>Procedural part generation (body, head, face, hair, clothing...). No implementation exists: it is Creator Engine roadmap phases B-E.</summary>
    public interface ICharacterGenerator
    {
        /// <summary>The slot this generator can produce (e.g. "hair.style"), or an empty string.</summary>
        string Slot { get; }
        /// <summary>Whether it can produce the part this character asks for.</summary>
        bool CanGenerate(ResolvedCharacter character);
    }

    /// <summary>
    /// Builds the visible character (Unity side). It must not know about the match, the ball, AI, difficulty or score: only the resolved data goes in.
    /// </summary>
    public interface ICharacterAssembler<TRuntime>
    {
        TRuntime Assemble(ResolvedCharacter character, AnimationSetReference animations);
    }

    /// <summary>What a base model's body tolerates, so shared animation can be adapted without a set per body (data only).</summary>
    [Serializable]
    public sealed class BodyCompatibility
    {
        public string BaseModelId;
        public float MinHeightScale = 0.85f;
        public float MaxHeightScale = 1.15f;
        public bool SupportsBlendShapes;
    }

    [Serializable]
    public sealed class AnimationCompatibility
    {
        public string RigId;
        public string LibraryId;
        public List<string> ProvidedTags = new List<string>();
    }

    public enum RetargetMode
    {
        /// <summary>Same rig: animation plays as authored.</summary>
        None,
        /// <summary>Humanoid-style retargeting between different rigs.</summary>
        Humanoid
    }

    [Serializable]
    public sealed class RetargetingProfile
    {
        public string SourceRigId;
        public string TargetRigId;
        public RetargetMode Mode = RetargetMode.None;
    }

    /// <summary>The football actions a character will perform. Gameplay requests one; animation shows it; the BALL stays independent.</summary>
    public enum FootballActionKind
    {
        /// <summary>No action requested (movement only).</summary>
        None = 0,
        Control,
        Dribble,
        ShortPass,
        LongPass,
        Cross,
        Shot,
        PlacedShot,
        Header,
        Tackle,
        Intercept,
        Block,
        Shield,
        GoalkeeperSave,
        GoalkeeperCatch,
        GoalkeeperDistribution
    }

    /// <summary>
    /// "The foot touches the ball now": sent by an animation to gameplay, which decides what really happens to the ball (Ball Core). An animation
    /// never moves the ball and the ball is never a child of a player.
    /// </summary>
    public struct ActionContactEvent
    {
        public FootballActionKind Action;
        /// <summary>0..1 point of the animation where contact happens.</summary>
        public float NormalizedTime;
        /// <summary>The body part that makes contact ("foot_r", "head"...).</summary>
        public string Contact;
    }

    /// <summary>The default, pure appearance resolver: ids checked against the catalog, neutral values filled in.</summary>
    public sealed class CatalogAppearanceResolver : ICharacterAppearanceResolver, ICharacterRigResolver
    {
        private readonly CreatorCatalogs catalogs;

        public CatalogAppearanceResolver(CreatorCatalogs catalogs)
        {
            this.catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        }

        public bool TryResolveRig(string baseModelId, out string rigId)
        {
            if (catalogs.Appearance.TryGetBaseModel(baseModelId, out BaseModelDefinition b)) { rigId = b.RigId; return true; }
            rigId = null;
            return false;
        }

        public ResolvedCharacter Resolve(CharacterSpecification spec)
        {
            var r = new ResolvedCharacter { CharacterId = spec.CharacterId, PlayerId = spec.PlayerId, BaseModelId = spec.BaseModelId, StyleId = spec.Appearance.StyleId };
            if (TryResolveRig(spec.BaseModelId, out string rig)) r.RigId = rig;
            else r.Warnings.Add("Base model '" + spec.BaseModelId + "' is not in the catalog.");

            foreach (ParameterDefinition p in catalogs.Parameters.InDomain(ParameterDomain.Appearance))
                r.Scales[p.Id] = spec.Appearance.Params.Get(catalogs.Parameters, p.Id);

            foreach (KeyValuePair<string, string> kv in spec.Appearance.Choices)
            {
                if (catalogs.Appearance.TryGetPart(kv.Key, kv.Value, out PartDefinition part) && part.FitsBase(spec.BaseModelId)) r.Parts[kv.Key] = kv.Value;
                else r.Warnings.Add("Part '" + kv.Value + "' for slot '" + kv.Key + "' is unknown or does not fit the base model; left empty.");
            }
            foreach (KeyValuePair<string, string> kv in spec.Appearance.Colors) r.Colors[kv.Key] = kv.Value;
            return r;
        }
    }

    /// <summary>
    /// How a particular body should MODULATE shared animation, so players move a little differently without any animation of their own. It
    /// is a presentation hint: it never changes how fast or how far the player really moves (that is gameplay: attributes and stamina).
    /// All values are around 1 (or 0..1) and clamped.
    /// </summary>
    public struct MovementSignature
    {
        /// <summary>1 = the base stride; longer for tall / long-legged players.</summary>
        public float StrideLength;
        /// <summary>1 = the base step rate; higher for short strides and agile players.</summary>
        public float Cadence;
        /// <summary>0..1 forward lean when accelerating.</summary>
        public float Lean;
        /// <summary>0..1 how sharp turns look.</summary>
        public float TurnSharpness;
        /// <summary>0..1 how abruptly the player starts moving.</summary>
        public float AccelerationAggression;
    }

    public static class MovementSignatureResolver
    {
        public static MovementSignature Resolve(BodyType bodyType, CharacterSpecification spec, in PlayerAttributes attributes, CreatorCatalogs catalogs)
        {
            float height = spec.Appearance.Params.Get(catalogs.Parameters, "body.height");
            float legs = spec.Appearance.Params.Get(catalogs.Parameters, "body.legLength");
            float mass = spec.Appearance.Params.Get(catalogs.Parameters, "body.mass");
            float accelTend = spec.Dna.Get(catalogs.Parameters, "movement.accelerationTendency");
            float turnTend = spec.Dna.Get(catalogs.Parameters, "movement.turningTendency");
            float dirChange = spec.Dna.Get(catalogs.Parameters, "dribbling.directionChange");

            float stride = 1f + 0.5f * (legs - 1f) + 0.4f * (height - 1f) + BodyStride(bodyType);
            float agility = PlayerAttributes.Normalize(attributes.Agility);
            float accel = PlayerAttributes.Normalize(attributes.Acceleration);
            float cadence = 1f - 0.8f * (stride - 1f) + 0.1f * (agility - 0.5f) - 0.15f * (mass - 1f);
            float lean = 0.3f + 0.35f * accelTend + 0.2f * accel - (bodyType == BodyType.Strong ? 0.05f : 0f);
            float turn = 0.4f * turnTend + 0.25f * dirChange + 0.35f * agility;
            float aggression = 0.5f * accelTend + 0.5f * accel;

            return new MovementSignature
            {
                StrideLength = System.Math.Max(0.8f, System.Math.Min(1.2f, stride)),
                Cadence = System.Math.Max(0.8f, System.Math.Min(1.2f, cadence)),
                Lean = MathUtil.Clamp01(lean),
                TurnSharpness = MathUtil.Clamp01(turn),
                AccelerationAggression = MathUtil.Clamp01(aggression)
            };
        }

        private static float BodyStride(BodyType t)
        {
            switch (t)
            {
                case BodyType.Tall: return 0.05f;
                case BodyType.Compact: return -0.05f;
                case BodyType.Strong: return -0.02f;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// Human + AI both go through PlayerIntent -> Movement -> MovementState -> Animation; the appearance and the DNA are not part of that chain.
    /// This is the one place the DNA reaches gameplay: it ranks behaviours for the AI (see <see cref="BehaviorResolver"/>), and the AI
    /// turns the chosen one into a PlayerIntent. Nothing else about a character may influence movement.
    /// </summary>
    public static class CreatorEngineRules
    {
        public const string MovementChain = "Human/AI -> PlayerIntent -> Movement -> MovementState -> Animation";
    }
}
