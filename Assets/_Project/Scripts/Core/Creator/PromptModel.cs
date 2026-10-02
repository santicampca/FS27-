using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>What the person wants. The contract allows all of these; an interpreter may support only some (and says so).</summary>
    public enum PromptIntentKind
    {
        Unknown = 0,
        CreateCharacter,
        ModifyCharacter,
        ModifyAppearance,
        ModifyBody,
        ModifyFace,
        ModifyHair,
        ModifyClothing,
        ModifyStyle,
        ModifyFootballDna,
        AddBehavior,
        RemoveBehavior,
        ChangePlayStyle
    }

    /// <summary>The part of the game a request is about. "Faster" means different things in each; they are never mixed silently.</summary>
    public enum PromptContext
    {
        Unspecified = 0,
        /// <summary>What the player can do: attributes and movement tendencies.</summary>
        Gameplay,
        /// <summary>How the player looks (body, face, hair, kit, style).</summary>
        Visual,
        /// <summary>How the movement is shown (animation playback, stride, lean).</summary>
        Animation
    }

    public enum SpecChangeKind
    {
        /// <summary>Set a scalar parameter to a normalised level (0..1 of its range).</summary>
        ScalarSet,
        /// <summary>Move a scalar parameter up or down by a fraction of its range.</summary>
        ScalarDelta,
        ChoiceSet,
        ColorSet,
        BehaviorAdd,
        BehaviorRemove,
        /// <summary>Move along the style's cartoon axis (up = more cartoon, down = more realistic).</summary>
        StyleShift
    }

    /// <summary>
    /// One structured change to a specification: what (target), which way (direction), how much (magnitude) and how sure we are (confidence).
    /// Never a string like "taller": the numbers are explicit, so a change can be validated, shown, undone and applied the same way no matter
    /// which interpreter (a person, an editor, a deterministic parser or a language model) produced it.
    /// </summary>
    [Serializable]
    public sealed class SpecChange
    {
        public SpecChangeKind Kind;
        /// <summary>Parameter id, part slot, colour slot, behaviour id, or the style axis ("cartoon"), depending on <see cref="Kind"/>.</summary>
        public string Target;
        /// <summary>-1 decrease, +1 increase, 0 for kinds with no direction.</summary>
        public int Direction;
        /// <summary>0..1, size of a delta or shift (fraction of the parameter's range).</summary>
        public float Magnitude;
        /// <summary>0..1, for <see cref="SpecChangeKind.ScalarSet"/> (normalised level) and behaviour weights.</summary>
        public float Level;
        /// <summary>Part id (<see cref="SpecChangeKind.ChoiceSet"/>) or "#RRGGBB" (<see cref="SpecChangeKind.ColorSet"/>).</summary>
        public string Value;
        /// <summary>0..1.</summary>
        public float Confidence = 1f;
        public PromptContext Context;
        /// <summary>For a style shift: apply the style's "not childish" guard (head, eyes, hands and feet move less).</summary>
        public bool GuardChildlike;
        /// <summary>The words of the request this change came from (for explaining it).</summary>
        public string Source = "";
    }

    public enum AttributeHintKind
    {
        Increase,
        Decrease
    }

    /// <summary>A wish about one of the 12 attributes. A HINT only: attributes belong to the <see cref="PlayerDefinition"/>, never to the specification.</summary>
    [Serializable]
    public struct AttributeHint
    {
        public PlayerAttributeId Attribute;
        /// <summary>0..1: 0 very low, 0.5 average, 1 very high. Used when <see cref="Relative"/> is false.</summary>
        public float Level;
        /// <summary>True for "more/less of it": <see cref="Delta"/> is applied to the current value instead of setting a level.</summary>
        public bool Relative;
        /// <summary>Signed fraction of the attribute's scale, when <see cref="Relative"/>.</summary>
        public float Delta;
        public float Confidence;
        public string Source;
    }

    public enum ProfileHintKind
    {
        Risk,
        Creativity,
        Aggression,
        PrimaryZone,
        Role
    }

    /// <summary>A wish about the player's <see cref="PlayerPlayingProfile"/> (behaviour values, zone, roles). Also only a hint.</summary>
    [Serializable]
    public struct ProfileHint
    {
        public ProfileHintKind Kind;
        /// <summary>0..1 for Risk/Creativity/Aggression and for the affinity of a Role.</summary>
        public float Level;
        public PitchZone Zone;
        public PlayerArchetype Role;
        /// <summary>True for "more/less": <see cref="Delta"/> (signed, fraction of 0..100) is applied instead of setting <see cref="Level"/>.</summary>
        public bool Relative;
        public float Delta;
        public float Confidence;
        public string Source;
    }

    public enum ConflictKind
    {
        /// <summary>Two requests push the same thing in opposite directions.</summary>
        Contradiction,
        /// <summary>Two requests are not impossible together but pull against each other.</summary>
        Tension
    }

    public sealed class PromptConflict
    {
        public ConflictKind Kind;
        public string Target;
        public string Description;
        public string PhraseA;
        public string PhraseB;
    }

    public enum UnsupportedReason
    {
        /// <summary>Needs a 3D/animation asset that does not exist.</summary>
        RequiresAsset,
        /// <summary>Needs a runtime system that is not implemented yet.</summary>
        RequiresFutureRuntime,
        /// <summary>The interpreter does not understand it.</summary>
        NotUnderstood
    }

    public sealed class UnsupportedRequest
    {
        public string Text;
        public UnsupportedReason Reason;
        public string Explanation;
    }

    /// <summary>What a person asked, plus the existing character when they are changing one.</summary>
    public sealed class PromptRequest
    {
        public string Text;
        /// <summary>The character being modified; null to create one. The interpreter never changes it.</summary>
        public CharacterSpecification Existing;
        /// <summary>Id for a character the request creates.</summary>
        public string NewCharacterId = "character-new";
    }

    /// <summary>
    /// Everything an interpreter found out. It never invents silently: what it did not understand, could not do, was unsure of or found
    /// contradictory is listed. <see cref="Draft"/> is the existing specification with the changes applied (or a new one) and has NOT been
    /// validated against the catalogs yet.
    /// </summary>
    public sealed class PromptResult
    {
        public PromptIntentKind Intent;
        public PromptContext Context;
        public string InterpreterName = "";
        public CharacterSpecification Draft;
        public List<SpecChange> Changes = new List<SpecChange>();
        public List<AttributeHint> AttributeHints = new List<AttributeHint>();
        public List<ProfileHint> ProfileHints = new List<ProfileHint>();
        public List<string> Warnings = new List<string>();
        public List<PromptConflict> Conflicts = new List<PromptConflict>();
        public List<UnsupportedRequest> Unsupported = new List<UnsupportedRequest>();
        public List<string> Unresolved = new List<string>();
        /// <summary>0..1, overall (lowest of the changes' confidences, lowered by unresolved parts).</summary>
        public float Confidence;

        /// <summary>True when nothing blocks applying the result as it is: no contradiction. Unsupported parts do not block (they are skipped and listed).</summary>
        public bool CanApplyAutomatically
        {
            get
            {
                foreach (PromptConflict c in Conflicts)
                    if (c.Kind == ConflictKind.Contradiction) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Turns a request into structured changes. The Creator Engine depends ONLY on this interface: a deterministic parser, a language-model
    /// provider (Claude, Gemini, OpenAI, a local model...) or a manual editor are interchangeable behind it, and none of them is "the engine".
    /// An implementation produces data (a <see cref="PromptResult"/>), never code, and holds no secrets.
    /// </summary>
    public interface IAICharacterInterpreter
    {
        string Name { get; }
        PromptResult Interpret(PromptRequest request);
    }

    /// <summary>The words people use for "how much", as numbers (fraction of a parameter's range). Configurable.</summary>
    [Serializable]
    public sealed class MagnitudeScale
    {
        public float Slight = 0.05f;
        public float Little = 0.10f;
        public float Normal = 0.18f;
        public float Quite = 0.28f;
        public float Much = 0.40f;
        public float Extreme = 0.55f;

        /// <summary>Multiplier on how extreme an ABSOLUTE descriptor is ("very tall" is further from average than "tall").</summary>
        public float IntensityOf(MagnitudeWord w)
        {
            switch (w)
            {
                case MagnitudeWord.Slight: return 0.35f;
                case MagnitudeWord.Little: return 0.55f;
                case MagnitudeWord.Quite: return 1.15f;
                case MagnitudeWord.Much: return 1.3f;
                case MagnitudeWord.Extreme: return 1.5f;
                default: return 1f;
            }
        }

        public float DeltaOf(MagnitudeWord w)
        {
            switch (w)
            {
                case MagnitudeWord.Slight: return Slight;
                case MagnitudeWord.Little: return Little;
                case MagnitudeWord.Quite: return Quite;
                case MagnitudeWord.Much: return Much;
                case MagnitudeWord.Extreme: return Extreme;
                default: return Normal;
            }
        }
    }

    public enum MagnitudeWord
    {
        None = 0,
        Slight,
        Little,
        Normal,
        Quite,
        Much,
        Extreme
    }

    /// <summary>Checks a prompt result is coherent and only refers to things that exist. Does not judge whether the interpretation was wise.</summary>
    public static class PromptSpecificationValidator
    {
        public static CreatorValidationResult Validate(PromptResult result, CreatorCatalogs catalogs)
        {
            var r = new CreatorValidationResult();
            if (result == null)
            {
                r.Error(CreatorIssueCode.SpecificationNull, "prompt result", "Result is null.");
                return r;
            }
            if (result.Confidence < 0f || result.Confidence > 1f) r.Error(CreatorIssueCode.ChangeConfidenceOutOfRange, "result", "Confidence must be within 0..1.");
            if (result.Intent == PromptIntentKind.Unknown && result.Changes.Count > 0)
                r.Error(CreatorIssueCode.ResultInconsistent, "result", "It has changes but an unknown intent.");
            if (result.Draft == null && result.Changes.Count > 0) r.Error(CreatorIssueCode.ResultInconsistent, "result", "It has changes but no draft to apply them to.");

            for (int i = 0; i < result.Changes.Count; i++)
            {
                SpecChange c = result.Changes[i];
                string who = "change " + i + " (" + c.Kind + " " + c.Target + ")";
                if (c.Magnitude < 0f || c.Magnitude > 1f || float.IsNaN(c.Magnitude)) r.Error(CreatorIssueCode.ChangeMagnitudeOutOfRange, who, "Magnitude must be within 0..1.");
                if (c.Level < 0f || c.Level > 1f || float.IsNaN(c.Level)) r.Error(CreatorIssueCode.ChangeMagnitudeOutOfRange, who, "Level must be within 0..1.");
                if (c.Confidence < 0f || c.Confidence > 1f || float.IsNaN(c.Confidence)) r.Error(CreatorIssueCode.ChangeConfidenceOutOfRange, who, "Confidence must be within 0..1.");

                switch (c.Kind)
                {
                    case SpecChangeKind.ScalarSet:
                    case SpecChangeKind.ScalarDelta:
                        if (!catalogs.Parameters.Contains(c.Target)) r.Error(CreatorIssueCode.ChangeTargetUnknown, who, "Unknown parameter.");
                        if (c.Kind == SpecChangeKind.ScalarDelta && c.Direction == 0) r.Error(CreatorIssueCode.ChangeKindInvalid, who, "A delta needs a direction.");
                        break;
                    case SpecChangeKind.ChoiceSet:
                        if (!catalogs.Appearance.HasSlot(c.Target)) r.Error(CreatorIssueCode.ChangeTargetUnknown, who, "Unknown slot.");
                        else if (!catalogs.Appearance.TryGetPart(c.Target, c.Value, out PartDefinition _)) r.Error(CreatorIssueCode.ChoicePartUnknown, who, "Part '" + c.Value + "' does not exist.");
                        break;
                    case SpecChangeKind.ColorSet:
                        if (!catalogs.Appearance.HasColorSlot(c.Target)) r.Error(CreatorIssueCode.ColorSlotUnknown, who, "Unknown colour slot.");
                        if (!CharacterSpecificationValidator.IsHexColor(c.Value)) r.Error(CreatorIssueCode.ColorInvalid, who, "Not a #RRGGBB colour.");
                        break;
                    case SpecChangeKind.BehaviorAdd:
                    case SpecChangeKind.BehaviorRemove:
                        if (!catalogs.Behaviors.Contains(c.Target)) r.Error(CreatorIssueCode.BehaviorUnknown, who, "Not in the behaviour catalog.");
                        break;
                    case SpecChangeKind.StyleShift:
                        if (c.Target != "cartoon") r.Error(CreatorIssueCode.ChangeTargetUnknown, who, "Only the 'cartoon' style axis exists.");
                        if (c.Direction == 0) r.Error(CreatorIssueCode.ChangeKindInvalid, who, "A style shift needs a direction.");
                        break;
                    default:
                        r.Error(CreatorIssueCode.ChangeKindInvalid, who, "Unknown change kind.");
                        break;
                }
            }
            return r;
        }
    }
}
