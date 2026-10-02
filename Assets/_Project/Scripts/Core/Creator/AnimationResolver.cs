using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>What is happening to the player right now, as far as animation is concerned. Built from the (already decided) intent and the movement state.</summary>
    public struct AnimationSelectionContext
    {
        /// <summary>Current ground speed in m/s (from Movement, never from animation).</summary>
        public float Speed;
        public FootballActionKind Action;
        public MovementStyle Style;
        public string BehaviorId;
        public string RigId;
    }

    /// <summary>One animation that could play, how well it fits, and why.</summary>
    public sealed class AnimationCandidate
    {
        public AnimationProfile Profile;
        public float Score;
        public readonly List<string> Reasons = new List<string>();
    }

    /// <summary>How the chosen animation should be shown for THIS body: playback speed and posture modulation. Presentation only.</summary>
    public struct AnimationPlan
    {
        public string AnimationId;
        public AnimationStatus Status;
        public float PlaybackSpeed;
        public float StrideScale;
        public float LeanDegrees;
        /// <summary>True when the animation has no clip yet: a runtime must fall back to its placeholder / idle pose.</summary>
        public bool NoClipYet;
    }

    /// <summary>
    /// Picks which shared animation fits the moment and how this particular body modulates it. It is pure selection over DATA: it plays nothing,
    /// owns no clips, and cannot change where the player goes (movement comes from the intent; root motion is never used).
    /// </summary>
    public sealed class CatalogAnimationResolver : IAnimationResolver
    {
        public const string SharedLibraryId = "fs27_shared_v1";

        private readonly AnimationCatalog catalog;
        private readonly CreatorCatalogs catalogs;

        public CatalogAnimationResolver(AnimationCatalog animations, CreatorCatalogs catalogs)
        {
            catalog = animations;
            this.catalogs = catalogs;
        }

        /// <summary>The shared set a rig offers (the tags of every animation that rig can play).</summary>
        public bool TryResolve(ResolvedCharacter character, MovementSignature signature, out AnimationSetReference animations)
        {
            animations = null;
            if (character == null || string.IsNullOrEmpty(character.RigId)) return false;
            var set = new AnimationSetReference { LibraryId = SharedLibraryId, RigId = character.RigId };
            var tags = new SortedSet<string>(StringComparer.Ordinal);
            foreach (AnimationProfile a in catalog.All)
                if (a.RigIds.Contains(character.RigId)) foreach (string t in a.Tags) tags.Add(t);
            set.Tags.AddRange(tags);
            animations = set;
            return set.Tags.Count > 0;
        }

        /// <summary>Candidates ranked best first (ties by id). Empty when nothing is compatible with the rig.</summary>
        public List<AnimationCandidate> Select(in AnimationSelectionContext ctx)
        {
            var list = new List<AnimationCandidate>();
            SignatureBehaviorDefinition behavior = null;
            if (!string.IsNullOrEmpty(ctx.BehaviorId)) catalogs.Behaviors.TryGet(ctx.BehaviorId, out behavior);

            foreach (AnimationProfile a in catalog.All)
            {
                if (!string.IsNullOrEmpty(ctx.RigId) && !a.RigIds.Contains(ctx.RigId)) continue;
                var c = new AnimationCandidate { Profile = a };
                float score = 0f;

                if (ctx.Action != FootballActionKind.None)
                {
                    if (a.Action == ctx.Action) { score += 0.5f; c.Reasons.Add("action " + ctx.Action); }
                    else continue;   // an action animation for another action never fits
                }
                else if (a.Action != FootballActionKind.None) continue; // locomotion context: action clips are not candidates

                if (a.Style == ctx.Style) { if (ctx.Style != MovementStyle.Default) { score += 0.25f; c.Reasons.Add("style " + ctx.Style); } }
                else if (a.Style != MovementStyle.Default && ctx.Style != MovementStyle.Default) continue;   // a different, specific style
                else if (a.Style != MovementStyle.Default) score -= 0.1f;   // a specific clip for a plain request: only if nothing better

                if (ctx.Speed >= a.MinSpeed && ctx.Speed <= a.MaxSpeed)
                {
                    float mid = (a.MinSpeed + a.MaxSpeed) * 0.5f, half = Math.Max(0.1f, (a.MaxSpeed - a.MinSpeed) * 0.5f);
                    score += 0.2f * (1f - Math.Abs(ctx.Speed - mid) / half * 0.5f);
                    c.Reasons.Add("speed in range");
                }
                else
                {
                    float gap = ctx.Speed < a.MinSpeed ? a.MinSpeed - ctx.Speed : ctx.Speed - a.MaxSpeed;
                    score -= Math.Min(0.4f, gap * 0.2f);
                    if (ctx.Action == FootballActionKind.None) score -= 0.3f;
                }

                if (behavior != null)
                {
                    foreach (string tag in behavior.AnimationTags)
                        if (a.HasTag(tag)) { score += 0.15f; c.Reasons.Add("tag " + tag); break; }
                    if (a.Behaviors.Contains(behavior.Id)) { score += 0.1f; c.Reasons.Add("made for " + behavior.Id); }
                }
                if (a.Status == AnimationStatus.Planned) score -= 0.01f;   // an existing clip wins a tie

                c.Score = score;
                list.Add(c);
            }
            list.Sort((x, y) =>
            {
                int r = y.Score.CompareTo(x.Score);
                return r != 0 ? r : string.CompareOrdinal(x.Profile.Id, y.Profile.Id);
            });
            return list;
        }

        /// <summary>The best animation and how to show it for this body. Null id when nothing fits.</summary>
        public AnimationPlan Plan(in AnimationSelectionContext ctx, MovementSignature signature, MovementPersonality personality)
        {
            List<AnimationCandidate> c = Select(ctx);
            if (c.Count == 0) return new AnimationPlan { AnimationId = null, NoClipYet = true, PlaybackSpeed = 1f, StrideScale = 1f };
            AnimationProfile a = c[0].Profile;
            float playback = MathUtil.Lerp(0.9f, 1.1f, 0.5f + (signature.Cadence - 1f) * 2.5f) * personality.PlaybackSpeedScale;
            return new AnimationPlan
            {
                AnimationId = a.Id, Status = a.Status, NoClipYet = a.Status != AnimationStatus.Available,
                PlaybackSpeed = Math.Max(0.8f, Math.Min(1.2f, playback)), StrideScale = signature.StrideLength * personality.StrideScale,
                LeanDegrees = personality.LeanDegrees * (ctx.Speed > 1.5f ? Math.Min(1f, ctx.Speed / 6f) : 0f)
            };
        }
    }

    /// <summary>
    /// How a character CARRIES itself, as presentation numbers: a heavy, muscular player plants their feet; a light, agile one bounces. It is
    /// derived from the body, the football tendencies and the movement signature, and it can NEVER change how fast or how far the player moves
    /// (that is Movement, attributes and stamina). Every field is clamped to a small range so shared animation stays believable.
    /// </summary>
    [Serializable]
    public struct MovementPersonality
    {
        /// <summary>0.9..1.1 multiplier on animation playback speed.</summary>
        public float PlaybackSpeedScale;
        /// <summary>0.9..1.1 multiplier on the visual stride.</summary>
        public float StrideScale;
        /// <summary>0..12 degrees of forward lean when running.</summary>
        public float LeanDegrees;
        /// <summary>0..1 how much the body bounces.</summary>
        public float Bounce;
        /// <summary>0..1 how wide the arms swing.</summary>
        public float ArmSwing;
        /// <summary>0..1 how readable a feint or a turn is before it happens.</summary>
        public float Anticipation;
        /// <summary>0..1 how upright the posture is (1 = very upright).</summary>
        public float Posture;
        /// <summary>Which of the idle variants this character uses (0..3), from the seed.</summary>
        public int IdleVariant;
    }

    public static class MovementPersonalityResolver
    {
        public const int IdleVariants = 4;

        public static MovementPersonality Resolve(CharacterSpecification spec, MovementSignature signature, CreatorCatalogs catalogs)
        {
            ParameterCatalog pc = catalogs.Parameters;
            float mass = spec.Appearance.Params.Get(pc, "body.mass");
            float musc = spec.Appearance.Params.GetLevel(pc, "body.muscularity");
            float height = spec.Appearance.Params.Get(pc, "body.height");
            float expressive = spec.Appearance.Params.GetLevel(pc, "face.expressiveness");
            float aggression = spec.Dna.Get(pc, "movement.aggression");
            float feint = spec.Dna.Get(pc, "dribbling.bodyFeint");
            float patience = spec.Dna.Get(pc, "decision.patience");

            float heavy = MathUtil.Clamp01((mass - 0.85f) / 0.4f);
            float light = 1f - heavy;
            uint seed = spec.AppearanceSeed != 0 ? spec.AppearanceSeed : StableHash.Of(spec.CharacterId ?? "");

            return new MovementPersonality
            {
                PlaybackSpeedScale = Clamp(0.9f, 1.1f, 1f + 0.08f * (signature.Cadence - 1f) / 0.2f - 0.05f * heavy),
                StrideScale = Clamp(0.9f, 1.1f, 1f + 0.5f * (height - 1f)),
                LeanDegrees = Clamp(0f, 12f, 12f * signature.Lean * (0.6f + 0.4f * aggression)),
                Bounce = MathUtil.Clamp01(0.35f + 0.4f * light - 0.2f * musc + 0.15f * expressive),
                ArmSwing = MathUtil.Clamp01(0.4f + 0.3f * signature.AccelerationAggression + 0.2f * musc),
                Anticipation = MathUtil.Clamp01(0.3f + 0.4f * feint + 0.2f * expressive - 0.1f * patience),
                Posture = MathUtil.Clamp01(0.55f + 0.25f * (1f - aggression) + 0.1f * musc),
                IdleVariant = (int)(StableHash.Combine(seed, 77u) % IdleVariants)
            };
        }

        private static float Clamp(float lo, float hi, float v) { return Math.Max(lo, Math.Min(hi, v)); }
    }
}
