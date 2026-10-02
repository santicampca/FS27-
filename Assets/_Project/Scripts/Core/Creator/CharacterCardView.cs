using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// What a player card can show about the Creator side, as plain read-only data: a short appearance summary, how the player plays, their
    /// signature behaviours, their role and zones, and the overall rating. It is a VIEW: it is built from the card, the character's specification
    /// and the catalogs, owns nothing, has no setters, and nothing in gameplay reads it. <see cref="PlayerCardData"/> itself is not changed.
    /// </summary>
    public sealed class CharacterCardView
    {
        public string PlayerId { get; }
        public string StyleName { get; }
        public IReadOnlyList<string> AppearanceSummary { get; }
        /// <summary>The football tendencies that stand out (highest and lowest), as short labels.</summary>
        public IReadOnlyList<string> FootballStyle { get; }
        /// <summary>Signature behaviours, strongest first, as readable names.</summary>
        public IReadOnlyList<string> Behaviors { get; }
        public string Role { get; }
        public PitchZone PrimaryZone { get; }
        public IReadOnlyList<PitchZone> SecondaryZones { get; }
        public int Overall { get; }
        public int HeadlineRating { get; }

        private CharacterCardView(string playerId, string style, List<string> look, List<string> play, List<string> behaviors, string role, PitchZone zone, IReadOnlyList<PitchZone> secondary, int overall, int headline)
        {
            PlayerId = playerId; StyleName = style; AppearanceSummary = look; FootballStyle = play; Behaviors = behaviors; Role = role; PrimaryZone = zone; SecondaryZones = secondary;
            Overall = overall; HeadlineRating = headline;
        }

        public static CharacterCardView Build(PlayerCardData card, CharacterSpecification spec, CreatorCatalogs catalogs)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            string style = catalogs.Styles.TryGet(spec.Appearance.StyleId, out StylePreset s) ? s.Name : spec.Appearance.StyleId;

            var look = new List<string>();
            AddLevel(look, spec, catalogs, "body.height", "tall", "short", 0.12f);
            AddLevel(look, spec, catalogs, "body.mass", "heavy build", "slim build", 0.12f);
            AddLevel(look, spec, catalogs, "body.muscularity", "muscular", "lean", 0.15f);
            if (spec.Appearance.Choices.TryGetValue("hair.style", out string hair)) look.Add("hair: " + hair.Replace('_', ' '));
            if (spec.Appearance.Choices.TryGetValue("face.beard", out string beard) && beard != "none") look.Add("beard: " + beard.Replace('_', ' '));
            if (look.Count == 0) look.Add("average build");

            var tendencies = new List<KeyValuePair<string, float>>();
            foreach (ParameterDefinition p in catalogs.Parameters.InDomain(ParameterDomain.FootballDna))
            {
                float level = spec.Dna.Get(catalogs.Parameters, p.Id);
                level = p.ToLevel(level);
                if (Math.Abs(level - 0.5f) >= 0.2f) tendencies.Add(new KeyValuePair<string, float>(p.Id, level));
            }
            tendencies.Sort((a, b) => { int c = Math.Abs(b.Value - 0.5f).CompareTo(Math.Abs(a.Value - 0.5f)); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
            var play = new List<string>();
            for (int i = 0; i < tendencies.Count && i < 4; i++) play.Add((tendencies[i].Value > 0.5f ? "high " : "low ") + Readable(tendencies[i].Key));

            var behaviors = new List<BehaviorEntry>(spec.Dna.Behaviors);
            behaviors.Sort((a, b) => { int c = b.Weight.CompareTo(a.Weight); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
            var names = new List<string>();
            foreach (BehaviorEntry b in behaviors) names.Add(ContentRef.ToSnake(b.Id).Replace('_', ' '));

            string role = card.TryGetPrimaryRole(out PlayerArchetype r) ? r.ToString() : "";
            return new CharacterCardView(card.PlayerId, style, look, play, names, role, card.PrimaryZone, card.SecondaryZones, card.Overall, card.HeadlineRating);
        }

        /// <summary>Adds a label when the parameter is clearly away from ITS OWN neutral value (not from the middle of its range: neutral is not always the middle).</summary>
        private static void AddLevel(List<string> list, CharacterSpecification spec, CreatorCatalogs catalogs, string param, string high, string low, float margin)
        {
            if (!catalogs.Parameters.TryGet(param, out ParameterDefinition p)) return;
            float delta = spec.Appearance.Params.GetLevel(catalogs.Parameters, param) - p.ToLevel(p.Default);
            if (delta >= margin) list.Add(high); else if (delta <= -margin) list.Add(low);
        }

        private static string Readable(string parameterId)
        {
            int dot = parameterId.IndexOf('.');
            string name = dot >= 0 ? parameterId.Substring(dot + 1) : parameterId;
            var sb = new System.Text.StringBuilder();
            foreach (char c in name) { if (char.IsUpper(c) && sb.Length > 0) sb.Append(' '); sb.Append(char.ToLowerInvariant(c)); }
            return (dot >= 0 ? parameterId.Substring(0, dot) + " " : "") + sb;
        }
    }
}
