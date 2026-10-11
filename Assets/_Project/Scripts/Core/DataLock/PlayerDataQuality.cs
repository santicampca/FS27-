using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Where a value came from: written by the authoring, a neutral stand-in because the source has nothing, or a value that does not apply to that player.</summary>
    public enum DataStatus
    {
        Authored = 0,
        /// <summary>The source has no value; the neutral default stands in. Not verified: nothing should treat it as real precision.</summary>
        NeutralUnverified = 1,
        NotApplicable = 2
    }

    /// <summary>
    /// What is real and what is a stand-in in the player data. Built from what the importers had to default (a value the source lacks), so nothing is guessed afterwards:
    ///  - weak foot: Authored when the source writes it; NeutralUnverified (the neutral 50) when it does not; NotApplicable for goalkeepers (they keep their dominant-foot logic and the
    ///    value is never used to penalise them);
    ///  - shirt number, height, weight: Authored or NeutralUnverified in the same way.
    /// </summary>
    public sealed class PlayerDataProvenance
    {
        public const string WeakFootField = "weakFootQuality";
        public const string ShirtNumberField = "shirtNumber";
        public const string HeightField = "heightCm";
        public const string WeightField = "weightKg";

        private readonly Dictionary<string, HashSet<string>> defaultedByField = new Dictionary<string, HashSet<string>>();
        private readonly HashSet<string> goalkeepers = new HashSet<string>();

        public static PlayerDataProvenance Build(PlayerLibrary library, IEnumerable<DefaultedValue> defaulted)
        {
            var p = new PlayerDataProvenance();
            foreach (PlayerDefinition d in library.All) if (d.Role == PlayerRole.Goalkeeper) p.goalkeepers.Add(d.Id);
            foreach (DefaultedValue v in defaulted)
            {
                if (!p.defaultedByField.TryGetValue(v.Field, out HashSet<string> set)) { set = new HashSet<string>(); p.defaultedByField[v.Field] = set; }
                set.Add(v.PlayerId);
            }
            return p;
        }

        public DataStatus WeakFoot(string playerId) { return Of(WeakFootField, playerId, true); }
        public DataStatus ShirtNumber(string playerId) { return Of(ShirtNumberField, playerId, false); }
        public DataStatus Height(string playerId) { return Of(HeightField, playerId, false); }
        public DataStatus Weight(string playerId) { return Of(WeightField, playerId, false); }

        /// <summary>The ids whose value of this field is a neutral stand-in (goalkeepers excluded: their weak foot does not apply).</summary>
        public IReadOnlyCollection<string> Unverified(string field)
        {
            return defaultedByField.TryGetValue(field, out HashSet<string> set) ? (IReadOnlyCollection<string>)set : new string[0];
        }

        private DataStatus Of(string field, string playerId, bool notForKeepers)
        {
            if (notForKeepers && goalkeepers.Contains(playerId)) return DataStatus.NotApplicable;
            return defaultedByField.TryGetValue(field, out HashSet<string> set) && set.Contains(playerId) ? DataStatus.NeutralUnverified : DataStatus.Authored;
        }
    }

    public sealed class AttributeStats
    {
        public PlayerAttributeId Attribute;
        public int Min, Max;
        public double Mean, StdDev;
        public int Range => Max - Min;
    }

    /// <summary>The findings of the attribute audit of the FIELD players. Goalkeepers are left out on purpose: their 12 attributes are the neutral default and their quality is their <see cref="GoalkeeperProfile"/>.</summary>
    public sealed class AttributeAuditReport
    {
        public readonly List<AttributeStats> Stats = new List<AttributeStats>();
        /// <summary>Attributes outside 1..99 ("player: Speed = 120").</summary>
        public readonly List<string> OutOfRange = new List<string>();
        /// <summary>Field players whose 12 attributes are exactly the neutral default (a sign nobody authored them).</summary>
        public readonly List<string> NeutralDefaultVectors = new List<string>();
        /// <summary>Pairs of players with the same 12 attributes (suspicious: copied data).</summary>
        public readonly List<string> IdenticalVectors = new List<string>();
        /// <summary>Players with at least <see cref="AttributeAuditor.UniformThreshold"/> attributes sharing one value (suspicious).</summary>
        public readonly List<string> UniformPlayers = new List<string>();
        /// <summary>Attributes whose range across the field players is narrower than <see cref="AttributeAuditor.CompressedRange"/> (informational: a balance matter, not an error).</summary>
        public readonly List<string> CompressedAttributes = new List<string>();
        public int FieldPlayers;
        public bool HasErrors => OutOfRange.Count > 0 || NeutralDefaultVectors.Count > 0 || IdenticalVectors.Count > 0 || UniformPlayers.Count > 0;
    }

    public static class AttributeAuditor
    {
        public const int UniformThreshold = 7;
        public const int CompressedRange = 30;

        /// <summary>True for a player whose 12 official attributes are authored (every field player). A goalkeeper's are the neutral default: consumers must read his <see cref="GoalkeeperProfile"/>.</summary>
        public static bool HasAuthoredFieldAttributes(PlayerDefinition player) { return player != null && player.Role != PlayerRole.Goalkeeper; }

        /// <summary>True when the 12 official attributes are all the neutral default value.</summary>
        public static bool IsNeutralDefault(PlayerDefinition p)
        {
            PlayerAttributes n = PlayerAttributes.CreateDefault();
            foreach (PlayerAttributeId a in PlayerAttributeInfo.All) if (p.Attributes.GetValue(a) != n.GetValue(a)) return false;
            return true;
        }

        public static AttributeAuditReport Analyze(PlayerLibrary library)
        {
            var r = new AttributeAuditReport();
            var field = new List<PlayerDefinition>();
            foreach (PlayerDefinition p in library.All) if (HasAuthoredFieldAttributes(p)) field.Add(p);
            r.FieldPlayers = field.Count;
            if (field.Count == 0) return r;

            foreach (PlayerAttributeId a in PlayerAttributeInfo.All)
            {
                var s = new AttributeStats { Attribute = a, Min = int.MaxValue, Max = int.MinValue };
                double sum = 0;
                foreach (PlayerDefinition p in field)
                {
                    int v = p.Attributes.GetValue(a);
                    if (v < PlayerAttributes.Min || v > PlayerAttributes.Max) r.OutOfRange.Add(p.Id + ": " + a + " = " + v);
                    s.Min = Math.Min(s.Min, v); s.Max = Math.Max(s.Max, v); sum += v;
                }
                s.Mean = sum / field.Count;
                double sq = 0;
                foreach (PlayerDefinition p in field) { double d = p.Attributes.GetValue(a) - s.Mean; sq += d * d; }
                s.StdDev = Math.Sqrt(sq / field.Count);
                r.Stats.Add(s);
                if (s.Range < CompressedRange) r.CompressedAttributes.Add(a + " " + s.Min + ".." + s.Max);
            }

            var seen = new Dictionary<string, string>();
            foreach (PlayerDefinition p in field)
            {
                if (IsNeutralDefault(p)) r.NeutralDefaultVectors.Add(p.Id);
                var counts = new Dictionary<int, int>();
                var key = new System.Text.StringBuilder();
                foreach (PlayerAttributeId a in PlayerAttributeInfo.All)
                {
                    int v = p.Attributes.GetValue(a);
                    counts[v] = counts.TryGetValue(v, out int c) ? c + 1 : 1;
                    key.Append(v).Append(',');
                }
                foreach (int c in counts.Values) if (c >= UniformThreshold) { r.UniformPlayers.Add(p.Id); break; }
                if (seen.TryGetValue(key.ToString(), out string other)) r.IdenticalVectors.Add(other + " = " + p.Id); else seen[key.ToString()] = p.Id;
            }
            return r;
        }
    }
}
