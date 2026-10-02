using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Starting appearance values taken from what the PlayerDefinition already says (body type, height, weight). The appearance
    /// REUSES those; it never stores them a second time, and the shirt number stays the player's.
    /// </summary>
    public static class AppearanceDefaults
    {
        public const int ReferenceHeightCm = 178;
        public const int ReferenceWeightKg = 72;

        public static CharacterSpecification FromPlayer(PlayerDefinition player, CreatorCatalogs catalogs, string characterId = null)
        {
            CharacterSpecification spec = catalogs.NewSpecification(characterId ?? ("char-" + player.Id));
            spec.PlayerId = player.Id;

            ParameterSet a = spec.Appearance.Params;
            ParameterCatalog pc = catalogs.Parameters;
            a.Set(pc, "body.height", player.HeightCm / (float)ReferenceHeightCm);
            // Weight relative to what a body of that height weighs on average: only the surplus/deficit shows as thickness.
            float expected = ReferenceWeightKg * (player.HeightCm / (float)ReferenceHeightCm) * (player.HeightCm / (float)ReferenceHeightCm);
            a.Set(pc, "body.mass", player.WeightKg / expected);

            string preset;
            switch (player.BodyType)
            {
                case BodyType.Light: preset = "light"; a.Set(pc, "body.muscularity", 0.35f); break;
                case BodyType.Strong: preset = "strong"; a.Set(pc, "body.muscularity", 0.8f); a.Set(pc, "body.shoulderWidth", 1.08f); break;
                case BodyType.Tall: preset = "tall"; a.Set(pc, "body.legLength", 1.04f); break;
                case BodyType.Compact: preset = "compact"; a.Set(pc, "body.muscularity", 0.6f); break;
                default: preset = "athletic"; a.Set(pc, "body.muscularity", 0.6f); break;
            }
            spec.Appearance.Choices[DefaultAppearanceCatalog.BodyPresetSlot] = preset;
            return spec;
        }
    }

    /// <summary>
    /// Turns the attribute wishes of a prompt result into suggested attribute VALUES. It only suggests: the caller decides whether to write
    /// them into the <see cref="PlayerDefinition"/> (the only owner of attributes). Hinted attributes move towards the wish; the rest are untouched.
    /// </summary>
    public static class AttributeHintApplier
    {
        public const int LowestSuggested = 35;
        public const int HighestSuggested = 95;

        public static PlayerAttributes Suggest(in PlayerAttributes baseline, IEnumerable<AttributeHint> hints)
        {
            PlayerAttributes result = baseline;
            foreach (AttributeHint h in hints)
            {
                int current = result.GetValue(h.Attribute);
                int value = h.Relative
                    ? current + (int)Math.Round(h.Delta * (PlayerAttributes.Max - PlayerAttributes.Min))
                    : (int)Math.Round(LowestSuggested + h.Level * (HighestSuggested - LowestSuggested));
                if (value < PlayerAttributes.Min) value = PlayerAttributes.Min;
                if (value > PlayerAttributes.Max) value = PlayerAttributes.Max;
                result = result.With(h.Attribute, value);
            }
            return result;
        }
    }

    /// <summary>Applies the playing-profile wishes of a prompt result to an existing <see cref="PlayerPlayingProfile"/> (the single place those values live).</summary>
    public static class ProfileHintApplier
    {
        public static void Apply(PlayerPlayingProfile profile, IEnumerable<ProfileHint> hints)
        {
            foreach (ProfileHint h in hints)
            {
                switch (h.Kind)
                {
                    case ProfileHintKind.Risk: profile.RiskPreference = Value(profile.RiskPreference, h); break;
                    case ProfileHintKind.Creativity: profile.Creativity = Value(profile.Creativity, h); break;
                    case ProfileHintKind.Aggression: profile.Aggression = Value(profile.Aggression, h); break;
                    case ProfileHintKind.PrimaryZone:
                        if (profile.PrimaryZone != h.Zone)
                        {
                            profile.SecondaryZones.Remove(h.Zone);
                            profile.PrimaryZone = h.Zone;
                        }
                        break;
                    case ProfileHintKind.Role:
                        SetRole(profile, h.Role, (int)Math.Round(h.Level * 100f));
                        break;
                }
            }
        }

        private static int Value(int current, ProfileHint h)
        {
            float v = h.Relative ? current + h.Delta * 100f : h.Level * 100f;
            return (int)Math.Max(0, Math.Min(100, Math.Round(v)));
        }

        private static void SetRole(PlayerPlayingProfile profile, PlayerArchetype role, int affinity)
        {
            affinity = Math.Max(0, Math.Min(100, affinity));
            for (int i = 0; i < profile.Roles.Count; i++)
            {
                if (profile.Roles[i].Role == role)
                {
                    profile.Roles[i] = new RoleAffinity(role, Math.Max(profile.Roles[i].Affinity, affinity));
                    return;
                }
            }
            if (profile.Roles.Count >= PlayerPlayingProfile.MaxArchetypes)
            {
                int weakest = 0;
                for (int i = 1; i < profile.Roles.Count; i++)
                    if (profile.Roles[i].Affinity < profile.Roles[weakest].Affinity) weakest = i;
                if (profile.Roles[weakest].Affinity >= affinity) return; // the new role would be the weakest: not worth a slot
                profile.Roles.RemoveAt(weakest);
            }
            profile.Roles.Add(new RoleAffinity(role, affinity));
        }
    }

    /// <summary>
    /// Which specification belongs to which player. It is NOT a second player library: players stay in the <see cref="PlayerLibrary"/>; this only
    /// links a player id to its character data (one per player, ids unique) and refuses anything that does not validate.
    /// </summary>
    public sealed class CharacterRegistry
    {
        private readonly CreatorCatalogs catalogs;
        private readonly Dictionary<string, CharacterSpecification> byCharacter = new Dictionary<string, CharacterSpecification>();
        private readonly Dictionary<string, string> characterOfPlayer = new Dictionary<string, string>();
        private readonly List<CharacterSpecification> ordered = new List<CharacterSpecification>();

        public int Count => ordered.Count;
        public IReadOnlyList<CharacterSpecification> All => ordered;

        public CharacterRegistry(CreatorCatalogs catalogs)
        {
            this.catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        }

        /// <summary>Adds a specification. Nothing is added if it does not validate, its id is taken, its player does not exist, or the player already has one.</summary>
        public CreatorValidationResult TryAdd(CharacterSpecification spec, IPlayerLookup players)
        {
            CreatorValidationResult r = CharacterSpecificationValidator.Validate(spec, catalogs);
            if (spec == null) return r;
            if (byCharacter.ContainsKey(spec.CharacterId ?? "")) r.Error(CreatorIssueCode.CharacterIdDuplicate, "character '" + spec.CharacterId + "'", "A character with this id is already registered.");
            if (!string.IsNullOrEmpty(spec.PlayerId))
            {
                if (players == null || !players.TryGet(spec.PlayerId, out PlayerDefinition _))
                    r.Error(CreatorIssueCode.PlayerNotFound, "character '" + spec.CharacterId + "'", "Player '" + spec.PlayerId + "' does not exist in the player library.");
                else if (characterOfPlayer.ContainsKey(spec.PlayerId))
                    r.Error(CreatorIssueCode.PlayerAlreadyHasSpecification, "character '" + spec.CharacterId + "'", "Player '" + spec.PlayerId + "' already has character '" + characterOfPlayer[spec.PlayerId] + "'.");
            }
            if (!r.IsValid) return r;

            byCharacter.Add(spec.CharacterId, spec);
            ordered.Add(spec);
            if (!string.IsNullOrEmpty(spec.PlayerId)) characterOfPlayer.Add(spec.PlayerId, spec.CharacterId);
            return r;
        }

        public bool TryGet(string characterId, out CharacterSpecification spec)
        {
            if (characterId == null)
            {
                spec = null;
                return false;
            }
            return byCharacter.TryGetValue(characterId, out spec);
        }

        public bool TryGetByPlayer(string playerId, out CharacterSpecification spec)
        {
            spec = null;
            return playerId != null && characterOfPlayer.TryGetValue(playerId, out string id) && byCharacter.TryGetValue(id, out spec);
        }
    }
}
