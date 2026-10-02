using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Data for normalising the signature actions of the defender database: the source's many spellings ("Recovery Run", "Recovery Run Sprint",
    /// "Open-Field Recovery Sprint") become one conceptual tag. Tags are labels for FUTURE behaviours: nothing here is code, an attribute or a mechanic.
    /// Only a tag with a real behaviour in the catalog becomes a football-DNA reference (see <see cref="TagBehaviors"/>); the rest stay as authoring tags.
    /// </summary>
    public static class DefenderVocabulary
    {
        private static readonly string[][] Rows =
        {
            new[] { "AerialClearance", "Aerial Clearance", "Aerial Clearances", "Aerial Power Clearance", "Dominant Aerial Clearance" },
            new[] { "AerialDominance", "Aerial Dominance", "Vertical Aerial Leap" },
            new[] { "SetPieceHeader", "Set-Piece Header", "Power Header Goal" },
            new[] { "PhysicalBlock", "Defensive Block", "Physical Block", "Physical Blocking" },
            new[] { "PhysicalShielding", "Physical Shielding", "Fierce 1v1 Shielding" },
            new[] { "AggressiveTackle", "Aggressive Tackle", "Front-Foot Aggressive Tackle", "Aggressive 1v1 Duel", "Aggressive Forward Charge" },
            new[] { "AggressiveInterception", "Aggressive Interception", "Aggressive Forward Interception" },
            new[] { "CleanInterception", "Clean 1v1 Interception", "Clean Positional Interception", "Tactical Interception", "Clean Anticipation Tackle" },
            new[] { "Anticipation", "Clean Anticipation", "Front-Foot Anticipation" },
            new[] { "OneVsOneContainment", "Immaculate 1v1 Containment", "Masterclass 1v1 Containment", "Dominant 1v1 Lockdown" },
            new[] { "RecoveryRun", "Recovery Run", "Recovery Run Sprint", "Open-Field Recovery Sprint" },
            new[] { "WideRecovery", "Wide Recovery Run", "Wide Defensive Recovery" },
            new[] { "WideCover", "Wide Cover", "Wide Defensive Cover" },
            new[] { "FrontFootDefending", "Front-Foot Defending" },
            new[] { "HighPressing", "High Pressing" },
            new[] { "BackLineCommand", "Back-Line Command" },
            new[] { "ProgressivePass", "Calm Progressive Pass", "Clean Progressive Pass" },
            new[] { "LongDiagonalPass", "Accurate Long Diagonal", "Surgical Long Diagonal", "Surgery Long Pass" },
            new[] { "ProgressiveCarry", "Progressive Carry", "Progressive Carry / Pass", "Progressive Power Carry" },
            new[] { "InvertedProgression", "Inverted Play Progression" },
            new[] { "MidfieldDistribution", "Midfield Distribution (CDM)" },
            new[] { "PressureEscape", "Calm Pressure Escape", "Composed Pressure Escape" }
        };

        /// <summary>Source label (normalised) -> conceptual tag.</summary>
        public static IReadOnlyDictionary<string, string> Create()
        {
            var d = new Dictionary<string, string>();
            foreach (string[] row in Rows)
                for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            return d;
        }

        public static IEnumerable<string> Tags { get { foreach (string[] row in Rows) yield return row[0]; } }

        /// <summary>Tag -> id of an EXISTING football behaviour that means the same. Only exact meanings; everything else stays a pending tag.</summary>
        public static IReadOnlyDictionary<string, string> TagBehaviors()
        {
            return new Dictionary<string, string> { { "HighPressing", DefaultBehaviors.AggressivePress } };
        }
    }
}
