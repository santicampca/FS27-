using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>The global player library built from the authored databases, with the playing profile of each field player and anything that went wrong.</summary>
    public sealed class AuthoredPlayerLibraryResult
    {
        public PlayerLibrary Library = new PlayerLibrary();
        public readonly PlayingProfileCatalog Profiles = new PlayingProfileCatalog();
        /// <summary>Real failures: a player that could not be imported.</summary>
        public readonly List<string> Problems = new List<string>();
        /// <summary>Source inconsistencies the readers noticed (already handled by the documented corrections); informational.</summary>
        public readonly List<string> Notes = new List<string>();
        public bool IsValid { get { return Problems.Count == 0; } }

        // ---- Phase 5: what the importers knew about each field player, kept instead of thrown away (additive; nothing above changes) ----
        /// <summary>The normalised signature tags of each field player, in source order (the vocabularies of the importers; at most <see cref="FieldPlayerImportOptions.MaxSignatureActions"/>). Authoring information.</summary>
        public readonly Dictionary<string, List<string>> SignatureTags = new Dictionary<string, List<string>>();
        /// <summary>Raw signature labels the vocabularies do not know (so they were left out), as "playerId: label".</summary>
        public readonly List<string> UnknownSignatureLabels = new List<string>();
        /// <summary>Raw signature labels dropped because a player already had the maximum number of tags, as "playerId: label".</summary>
        public readonly List<string> TrimmedSignatureLabels = new List<string>();
        /// <summary>Every value the importers had to default (a value the source lacks), field players only.</summary>
        public readonly List<DefaultedValue> Defaulted = new List<DefaultedValue>();
    }

    /// <summary>
    /// Builds THE global <see cref="PlayerLibrary"/>: every authored player (the goalkeepers and all the field-player databases) in one library, through the same readers, corrections and importers
    /// the databases already use. Core does no file reading and knows no file names: the host passes a function that returns the authoring text for a LOGICAL name (see
    /// <see cref="Sources"/>). Players come out with NO team (the library is global; squads come from the draw).
    /// </summary>
    public static class AuthoredPlayerLibrary
    {
        /// <summary>The logical names the loader asks the host for, in the order it reads them. A "x.corrections" source may be null/empty when a database has no corrections.</summary>
        public static readonly string[] Sources =
        {
            "goalkeepers", "goalkeepers.additions", "defenders", "defenders.corrections", "leftDefenders", "leftDefenders.corrections", "fullbacks", "fullbacks.corrections", "fullbacks.additions",
            "hybridMidfielders", "hybridMidfielders.corrections", "defensiveMidfielders", "defensiveMidfielders.corrections", "centralMidfielders", "centralMidfielders.corrections",
            "offensiveMidfielders", "offensiveMidfielders.corrections", "wingers", "wingers.corrections", "secondStrikers", "secondStrikers.corrections", "centreForwards", "centreForwards.corrections"
        };

        public static AuthoredPlayerLibraryResult Load(Func<string, string> readFile)
        {
            var result = new AuthoredPlayerLibraryResult();
            PlayerLibrary all = result.Library;

            Keepers(result, "goalkeepers", readFile("goalkeepers"));
            Keepers(result, "goalkeeper additions", readFile("goalkeepers.additions"));

            Field(result, "defenders", FieldPlayerLibraryBuilder.ImportMarkdown(readFile("defenders"), "cb", null, all, readFile("defenders.corrections")));
            Field(result, "left defenders", FieldPlayerLibraryBuilder.ImportCompactMarkdown(readFile("leftDefenders"), "cb", null, all, readFile("leftDefenders.corrections")));
            Field(result, "full-backs", FieldPlayerLibraryBuilder.ImportSectionedMarkdown(readFile("fullbacks"), "fb", FieldPlayerImportOptions.Fullback(), all, readFile("fullbacks.corrections")));
            Field(result, "full-back additions", FieldPlayerLibraryBuilder.ImportSectionedMarkdown(readFile("fullbacks.additions"), "fb", FieldPlayerImportOptions.Fullback(), all));
            Field(result, "hybrid midfielders", FieldPlayerLibraryBuilder.ImportCardMarkdown(readFile("hybridMidfielders"), "md", FieldPlayerImportOptions.HybridMidfielder(), all, readFile("hybridMidfielders.corrections")));
            Field(result, "defensive midfielders", FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(readFile("defensiveMidfielders"), "mc", FieldPlayerImportOptions.DefensiveMidfielder(), all, readFile("defensiveMidfielders.corrections")));
            Field(result, "central midfielders", FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(readFile("centralMidfielders"), "mq", FieldPlayerImportOptions.CentralMidfielder(), all, readFile("centralMidfielders.corrections")));
            Field(result, "offensive midfielders", FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(readFile("offensiveMidfielders"), "mo", FieldPlayerImportOptions.OffensiveMidfielder(), all, readFile("offensiveMidfielders.corrections")));
            Field(result, "wingers", FieldPlayerLibraryBuilder.ImportListMarkdown(readFile("wingers"), "wg", FieldPlayerImportOptions.Winger(), all, readFile("wingers.corrections")));
            Field(result, "second strikers", FieldPlayerLibraryBuilder.ImportListMarkdown(readFile("secondStrikers"), "sd", FieldPlayerImportOptions.SecondStriker(), all, readFile("secondStrikers.corrections")));
            Field(result, "centre forwards", FieldPlayerLibraryBuilder.ImportListMarkdown(readFile("centreForwards"), "dc", FieldPlayerImportOptions.Striker(), all, readFile("centreForwards.corrections")));
            return result;
        }

        private static void Keepers(AuthoredPlayerLibraryResult result, string label, string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return;
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(markdown, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            if (keepers.Failed > 0) result.Problems.Add(label + ": " + keepers.Failed + " goalkeepers failed to import.");
            foreach (string p in keepers.ReaderProblems) result.Notes.Add(label + ": " + p);
            foreach (PlayerDefinition p in keepers.Library.All)
            {
                if (!result.Library.TryAdd(p)) { result.Problems.Add(label + ": player id '" + p.Id + "' is already in the library."); continue; }
                if (keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk)) result.Library.TryAddGoalkeeperProfile(gk);
            }
        }

        private static void Field(AuthoredPlayerLibraryResult result, string label, FieldPlayerLibraryBuilder.Batch batch)
        {
            if (batch.Failed > 0) result.Problems.Add(label + ": " + batch.Failed + " players failed to import.");
            foreach (string p in batch.ReaderProblems) result.Notes.Add(label + ": " + p);
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                if (r.Profile != null) result.Profiles.TryAdd(r.Profile);
                if (r.Player == null) continue;
                result.SignatureTags[r.PlayerId] = new List<string>(r.SignatureTags);
                result.Defaulted.AddRange(r.Defaulted);
                foreach (FieldPlayerImportIssue i in r.Issues)
                {
                    if (i.Code == FieldPlayerImportCode.SignatureActionUnknown) result.UnknownSignatureLabels.Add(r.PlayerId + ": " + i.Value);
                    else if (i.Code == FieldPlayerImportCode.SignatureActionsTrimmed) result.TrimmedSignatureLabels.Add(r.PlayerId + ": " + i.Value);
                }
            }
        }
    }
}
