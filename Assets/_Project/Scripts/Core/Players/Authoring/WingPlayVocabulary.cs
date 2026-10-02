using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Signature-action vocabulary for wide players: <see cref="DefenderVocabulary"/> (reused as it is) plus conceptual tags for wing play (overlapping runs, crosses, cut-backs...).
    /// Like the rest of the vocabulary these are AUTHORING-ONLY labels for future behaviours, not code, attributes or mechanics; only a tag that means exactly an existing
    /// football behaviour becomes a DNA reference (see <see cref="TagBehaviors"/>).
    /// </summary>
    public static class WingPlayVocabulary
    {
        private static readonly string[][] Rows =
        {
            new[] { "OverlappingRun", "Overlapping run", "Overlapping power run" },
            new[] { "Cutback", "Cutback", "Cutback assist" },
            new[] { "EarlyCross", "Early cross" },
            new[] { "PrecisionCross", "Precision cross", "Pinpoint cross", "Outside-of-boot cross" },
            new[] { "SwitchOfPlay", "Switch of play" },
            new[] { "LongRangeStrike", "Long-range strike", "Long-range screamer" },
            new[] { "LateBoxArrival", "Late box arrival" },
            new[] { "BackPostHeader", "Back-post header" },
            new[] { "ExplosiveDribble", "Explosive dribble" },
            new[] { "HalfSpacePass", "Inverted half-space pass" },
            new[] { "DragBack", "Technical drag-back" },
            new[] { "PassingLaneBlock", "Inverted passing lane block" }
        };

        // wide players' own spellings of tags the defender vocabulary already has
        private static readonly string[][] Aliases =
        {
            new[] { "RecoveryRun", "Recovery sprint", "Recovery defensive sprint", "Defensive recovery sprint" },
            new[] { "HighPressing", "High-press tackle", "High-press ball win" },
            new[] { "ProgressiveCarry", "Turbo-carry" },
            new[] { "OneVsOneContainment", "Aggressive 1v1 containment" }
        };

        public static IReadOnlyDictionary<string, string> Create()
        {
            var d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in DefenderVocabulary.Create()) d[kv.Key] = kv.Value;
            foreach (string[] row in Rows) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            foreach (string[] row in Aliases) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            return d;
        }

        public static IEnumerable<string> Tags
        {
            get
            {
                foreach (string t in DefenderVocabulary.Tags) yield return t;
                foreach (string[] row in Rows) yield return row[0];
            }
        }

        /// <summary>Tag -> id of an EXISTING football behaviour that means the same (the defender mapping plus "arrives late in the box").</summary>
        public static IReadOnlyDictionary<string, string> TagBehaviors()
        {
            var d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in DefenderVocabulary.TagBehaviors()) d[kv.Key] = kv.Value;
            d["LateBoxArrival"] = DefaultBehaviors.LateBoxArrival;
            return d;
        }
    }
}
