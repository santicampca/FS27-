using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>One part of the score: its name, the 0..100 value and how much it counts.</summary>
    public struct CompatibilityComponent
    {
        public string Name;
        public int Score;
        public int Importance;
    }

    /// <summary>What the calculator says about one coach and one player: the 0..100 score, the boost it earns and the criteria that matched (for debugging).</summary>
    public sealed class CoachCompatibilityResult
    {
        public string CoachId = "";
        public string PlayerId = "";
        /// <summary>0..100.</summary>
        public int CompatibilityScore;
        /// <summary>0 or more, never negative, never above the rules' maximum.</summary>
        public int CoachBoost;
        public readonly List<string> MatchedCriteria = new List<string>();
        public readonly List<CompatibilityComponent> Components = new List<CompatibilityComponent>();
    }

    /// <summary>
    /// The player's rating seen through a coach: base + boost = effective. A contextual layer, computed on request and never stored: the player, the profile and the
    /// card keep their own numbers, so removing or changing the coach gives the base rating back at once.
    /// </summary>
    public readonly struct EffectiveRating
    {
        public int BaseOverall { get; }
        public int CoachBoost { get; }
        public int EffectiveOverall { get; }

        public EffectiveRating(int baseOverall, int coachBoost)
        {
            BaseOverall = baseOverall;
            CoachBoost = Math.Max(0, coachBoost);
            EffectiveOverall = Math.Min(99, baseOverall + CoachBoost);
        }
    }

    /// <summary>
    /// A player card seen through a coach. Wraps the (unchanged) <see cref="PlayerCardData"/>: <see cref="BaseOverall"/> is the card's own overall, the boost is the coach's
    /// contextual layer. The card never learns about the coach. This type belongs on the card side (like the character card view): gameplay never reads it.
    /// </summary>
    public sealed class PlayerCardCoachView
    {
        private readonly PlayerCardData card;
        private readonly CoachCompatibilityResult compatibility;

        /// <summary>The card seen through a coach: the card is read-only, the compatibility is computed on the spot.</summary>
        public static PlayerCardCoachView Create(PlayerCardData card, CoachCompatibilityCalculator calculator, CoachDefinition coach, PlayerDefinition player, PlayerPlayingProfile profile)
        {
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));
            return new PlayerCardCoachView(card, calculator.Evaluate(coach, player, profile));
        }

        public PlayerCardCoachView(PlayerCardData card, CoachCompatibilityResult compatibility)
        {
            this.card = card ?? throw new ArgumentNullException(nameof(card));
            this.compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        }

        public PlayerCardData Card { get { return card; } }
        public string CoachId { get { return compatibility.CoachId; } }
        public int CompatibilityScore { get { return compatibility.CompatibilityScore; } }
        public int BaseOverall { get { return card.Overall; } }
        public int CoachBoost { get { return compatibility.CoachBoost; } }
        public int EffectiveOverall { get { return new EffectiveRating(card.Overall, compatibility.CoachBoost).EffectiveOverall; } }
    }

    /// <summary>
    /// Computes how well a player fits a coach, from DATA only: the roles (with their affinity), the zones, the 12 attributes, the behaviour profile and the body type of the
    /// player against the coach's <see cref="CoachCompatibilityProfile"/>. No name of any player or coach appears anywhere in the calculation.
    /// Each part is 0..100 and the score is their average weighted by the profile's importances; a part with no data is left out.
    /// </summary>
    public sealed class CoachCompatibilityCalculator
    {
        private const float SecondaryZoneFactor = 0.8f;
        private const int BodyMismatchScore = 55;
        private const int MatchedAttributeValue = 80;

        public CoachBoostRules Rules { get; }

        public CoachCompatibilityCalculator() : this(new CoachBoostRules()) { }

        public CoachCompatibilityCalculator(CoachBoostRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (!rules.IsValid) throw new ArgumentException("The coach boost rules are not valid.", nameof(rules));
        }

        public CoachCompatibilityResult Evaluate(CoachDefinition coach, PlayerDefinition player, PlayerPlayingProfile profile)
        {
            if (coach == null) throw new ArgumentNullException(nameof(coach));
            if (player == null) throw new ArgumentNullException(nameof(player));
            PlayerPlayingProfile p = profile ?? PlayingProfileDefaults.FromPlayer(player);
            CoachCompatibilityProfile cp = coach.CompatibilityProfile ?? new CoachCompatibilityProfile();
            var result = new CoachCompatibilityResult { CoachId = coach.CoachId, PlayerId = player.Id };

            float weighted = 0f, total = 0f;
            void Add(string name, int score, int importance)
            {
                result.Components.Add(new CompatibilityComponent { Name = name, Score = score, Importance = importance });
                weighted += score * importance; total += importance;
            }

            if (cp.RoleImportance > 0 && cp.Roles.Count > 0) Add("Roles", RoleScore(cp, p, result), cp.RoleImportance);
            if (cp.ZoneImportance > 0 && cp.Zones.Count > 0) Add("Zones", ZoneScore(cp, p, result), cp.ZoneImportance);
            if (cp.AttributeImportance > 0 && cp.HasAttributes) Add("Attributes", AttributeScore(cp, player, result), cp.AttributeImportance);
            if (cp.BehaviourImportance > 0 && BehaviourWeight(cp) > 0) Add("Behaviour", BehaviourScore(cp, p, result), cp.BehaviourImportance);
            if (cp.BodyImportance > 0 && cp.BodyTypes.Count > 0) Add("Body", BodyScore(cp, player, result), cp.BodyImportance);

            result.CompatibilityScore = total <= 0f ? 0 : Clamp100((int)Math.Round(weighted / total, MidpointRounding.AwayFromZero));
            result.CoachBoost = Rules.BoostFor(result.CompatibilityScore);
            return result;
        }

        /// <summary>The player's rating with the coach: the base overall from the existing calculator plus the boost. Nothing is stored anywhere.</summary>
        public EffectiveRating GetEffectiveRating(CoachDefinition coach, PlayerDefinition player, PlayerPlayingProfile profile, PlayerRatingCalculator calculator)
        {
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));
            CoachCompatibilityResult r = Evaluate(coach, player, profile);
            return new EffectiveRating(calculator.GetOverall(player, profile ?? PlayingProfileDefaults.FromPlayer(player)), r.CoachBoost);
        }

        /// <summary>The boost applied to a rating that is NOT the field overall (for example a goalkeeper's GRL computed by its own calculator, which this class never changes).</summary>
        public EffectiveRating ApplyToRating(int baseRating, CoachCompatibilityResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new EffectiveRating(baseRating, result.CoachBoost);
        }

        // ------------------------------------------------------------------ parts

        private int RoleScore(CoachCompatibilityProfile cp, PlayerPlayingProfile p, CoachCompatibilityResult result)
        {
            float best = 0f;
            foreach (RoleAffinity r in p.Roles)
            {
                float v = cp.RoleWeight(r.Role) / 100f * Math.Max(0, Math.Min(100, r.Affinity));
                if (v > best) best = v;
                if (v >= Rules.MatchedCriterionThreshold) result.MatchedCriteria.Add("Role:" + r.Role);
            }
            return Clamp100((int)Math.Round(best, MidpointRounding.AwayFromZero));
        }

        private int ZoneScoreCore(CoachCompatibilityProfile cp, PlayerPlayingProfile p, out PitchZone bestZone)
        {
            float best = cp.ZoneWeight(p.PrimaryZone);
            bestZone = p.PrimaryZone;
            foreach (PitchZone z in p.SecondaryZones)
            {
                float v = cp.ZoneWeight(z) * SecondaryZoneFactor;
                if (v > best) { best = v; bestZone = z; }
            }
            return Clamp100((int)Math.Round(best, MidpointRounding.AwayFromZero));
        }

        private int ZoneScore(CoachCompatibilityProfile cp, PlayerPlayingProfile p, CoachCompatibilityResult result)
        {
            int score = ZoneScoreCore(cp, p, out PitchZone zone);
            if (score >= Rules.MatchedCriterionThreshold) result.MatchedCriteria.Add("Zone:" + zone);
            return score;
        }

        private int AttributeScore(CoachCompatibilityProfile cp, PlayerDefinition player, CoachCompatibilityResult result)
        {
            AttributeWeights w = cp.Attributes.Normalized();
            float mean = w.Evaluate(player.Attributes);
            float t = (mean - Rules.AttributeFloor) / (float)(Rules.AttributeCeiling - Rules.AttributeFloor);
            int score = Clamp100((int)Math.Round(MathUtil.Clamp01(t) * 100f, MidpointRounding.AwayFromZero));
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                if (w.Get(id) >= 0.12f && player.Attributes.GetValue(id) >= MatchedAttributeValue) result.MatchedCriteria.Add("Attribute:" + id + "(" + player.Attributes.GetValue(id) + ")");
            return score;
        }

        private static int BehaviourWeight(CoachCompatibilityProfile cp) { return cp.Risk.Importance + cp.Creativity.Importance + cp.Aggression.Importance; }

        private int BehaviourScore(CoachCompatibilityProfile cp, PlayerPlayingProfile p, CoachCompatibilityResult result)
        {
            float sum = 0f; float weight = 0f;
            void One(string name, BehaviourPreference pref, int value)
            {
                if (pref.Importance <= 0) return;
                int closeness = Clamp100(100 - Math.Abs(value - pref.Target));
                sum += closeness * pref.Importance; weight += pref.Importance;
                if (closeness >= Rules.MatchedCriterionThreshold + 5) result.MatchedCriteria.Add("Behaviour:" + name);
            }
            One("Risk", cp.Risk, p.RiskPreference);
            One("Creativity", cp.Creativity, p.Creativity);
            One("Aggression", cp.Aggression, p.Aggression);
            return weight <= 0f ? 0 : Clamp100((int)Math.Round(sum / weight, MidpointRounding.AwayFromZero));
        }

        private static int BodyScore(CoachCompatibilityProfile cp, PlayerDefinition player, CoachCompatibilityResult result)
        {
            if (cp.BodyTypes.Contains(player.BodyType)) { result.MatchedCriteria.Add("Body:" + player.BodyType); return 100; }
            return BodyMismatchScore;
        }

        private static int Clamp100(int v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }
    }
}
