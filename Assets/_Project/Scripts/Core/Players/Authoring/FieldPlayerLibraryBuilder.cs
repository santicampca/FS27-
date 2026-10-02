using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Imports many field-player records: each complete player is added to the ONE <see cref="PlayerLibrary"/> and its playing profile to a
    /// <see cref="PlayingProfileCatalog"/>. A repeated player id is an error and the LATER record is rejected (also against players already in the
    /// target library). Records with errors are not added. Every result is returned; nothing is hidden.
    /// </summary>
    public static class FieldPlayerLibraryBuilder
    {
        public sealed class Batch
        {
            public readonly List<FieldPlayerImportResult> Results = new List<FieldPlayerImportResult>();
            public PlayerLibrary Library;
            public readonly PlayingProfileCatalog Profiles = new PlayingProfileCatalog();
            /// <summary>Football DNA (references to existing behaviours) by player id, for the players that were added.</summary>
            public readonly Dictionary<string, FootballDNA> Dna = new Dictionary<string, FootballDNA>();
            public readonly List<string> ReaderProblems = new List<string>();
            /// <summary>The documented corrections applied to the authoring records before importing (and any that could not be applied).</summary>
            public CorrectionLog Corrections = new CorrectionLog();
            public int Added { get; internal set; }
            public int Failed { get { int n = 0; foreach (FieldPlayerImportResult r in Results) if (r.HasErrors) n++; return n; } }
            public IEnumerable<OverallCheck> OverallChecks { get { foreach (FieldPlayerImportResult r in Results) if (r.Overall != null) yield return r.Overall; } }
        }

        /// <param name="correctionsMarkdown">Optional table of documented corrections (see <see cref="AuthoringCorrections"/>), applied to the records before importing.</param>
        public static Batch ImportMarkdown(string markdown, string idPrefix, FieldPlayerImportOptions options = null, PlayerLibrary into = null, string correctionsMarkdown = null)
        {
            FieldPlayerParseResult parsed = FieldPlayerDatabaseMarkdown.Parse(markdown, idPrefix);
            var tableProblems = new List<string>();
            CorrectionLog log = AuthoringCorrections.Apply(parsed.Records, AuthoringCorrections.ParseMarkdown(correctionsMarkdown, tableProblems));
            Batch batch = Import(parsed.Records, options, into);
            batch.ReaderProblems.AddRange(parsed.Problems);
            batch.Corrections = log;
            log.Problems.AddRange(tableProblems);
            return batch;
        }

        public static Batch Import(IEnumerable<FieldPlayerDataRecord> records, FieldPlayerImportOptions options = null, PlayerLibrary into = null)
        {
            var batch = new Batch { Library = into ?? new PlayerLibrary() };
            var seen = new HashSet<string>();
            foreach (FieldPlayerDataRecord rec in records)
            {
                FieldPlayerImportResult res = FieldPlayerRecordImporter.Import(rec, options);
                bool taken = rec != null && !string.IsNullOrEmpty(rec.PlayerId) && (!seen.Add(rec.PlayerId) || batch.Library.Contains(rec.PlayerId));
                if (taken)
                {
                    res.Issues.Add(new FieldPlayerImportIssue
                    {
                        Code = FieldPlayerImportCode.DuplicatePlayerId, IsError = true, PlayerId = rec.PlayerId, Field = "playerId", Value = rec.PlayerId,
                        Message = "Player id '" + rec.PlayerId + "' was already used by an earlier record; this one is rejected."
                    });
                    res.Player = null; res.Profile = null; res.Dna = null; res.Overall = null;
                }
                batch.Results.Add(res);
                if (!res.IsComplete) continue;
                if (batch.Library.TryAdd(res.Player) && batch.Profiles.TryAdd(res.Profile)) { batch.Dna[res.Player.Id] = res.Dna; batch.Added++; }
            }
            return batch;
        }
    }
}
