using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Signature-action vocabulary for hybrid midfielders: <see cref="WingPlayVocabulary"/> (which already holds the defender tags) plus conceptual tags for midfield play
    /// (inside movement, support runs, press resistance...). AUTHORING-ONLY labels for future behaviours, not code, attributes or mechanics; a tag becomes a DNA reference only
    /// when it means exactly an existing football behaviour (none of the new ones does, so they stay pending).
    /// </summary>
    public static class MidfieldPlayVocabulary
    {
        private static readonly string[][] Rows =
        {
            new[] { "ProgressivePass", "Progressive Pass" },
            new[] { "InsideMovement", "Inside Movement" },
            new[] { "SupportRun", "Support Run" },
            new[] { "PressResistance", "Press Resistance" },
            new[] { "WideProgression", "Wide Progression" },
            new[] { "HalfSpaceMovement", "Half-Space Movement" },
            new[] { "Counterpressing", "Counterpressing" },
            new[] { "Pressing", "Pressing" },
            new[] { "DefensiveCover", "Defensive Cover" },
            new[] { "BoxToBoxRun", "Box-to-Box Run" },
            new[] { "DeepBuildUp", "DeepBuildUp", "Deep Build-Up" },
            new[] { "Interception", "Interception" },
            new[] { "DefensiveScreen", "DefensiveScreen", "Defensive Screen" },
            new[] { "LineBreakingPass", "LineBreakingPass", "Line-Breaking Pass" },
            new[] { "BallRecovery", "BallRecovery", "Ball Recovery" },
            new[] { "TacticalFoul", "TacticalFoul", "Tactical Foul" },
            new[] { "TurnAndAccelerate", "TurnAndAccelerate", "Turn And Accelerate" },
            new[] { "OutsideOfFootPass", "OutsideOfFootPass", "Outside-Of-Foot Pass" },
            new[] { "TempoControl", "TempoControl", "Tempo Control" },
            new[] { "AerialThreat", "AerialThreat", "Aerial Threat" }
        };

        // a hybrid midfielder's spelling of a tag the wing vocabulary already has
        private static readonly string[][] Aliases =
        {
            new[] { "OverlappingRun", "Overlap" },
            new[] { "Counterpressing", "CounterPress" },
            new[] { "SwitchOfPlay", "LongSwitch" }
        };

        public static IReadOnlyDictionary<string, string> Create()
        {
            var d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in WingPlayVocabulary.Create()) d[kv.Key] = kv.Value;
            foreach (string[] row in Rows) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            foreach (string[] row in Aliases) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            return d;
        }

        public static IEnumerable<string> Tags
        {
            get
            {
                foreach (string t in WingPlayVocabulary.Tags) yield return t;
                foreach (string[] row in Rows) yield return row[0];
            }
        }

        /// <summary>Tag -> existing football behaviour: the wing mapping, nothing new (a midfield tag is never forced onto a behaviour that merely resembles it).</summary>
        public static IReadOnlyDictionary<string, string> TagBehaviors() { return WingPlayVocabulary.TagBehaviors(); }
    }
}
