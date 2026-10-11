using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Imports many records into the ONE <see cref="PlayerLibrary"/>: each complete keeper becomes a normal player plus its profile (and a
    /// football DNA). A repeated player id is an error and the LATER record is rejected: the earlier one is never overwritten. Records with
    /// errors are not added. Nothing is hidden: every result and every reader problem is returned.
    /// </summary>
    public static class GoalkeeperLibraryBuilder
    {
        public sealed class Batch
        {
            public readonly List<GoalkeeperImportResult> Results = new List<GoalkeeperImportResult>();
            public readonly PlayerLibrary Library = new PlayerLibrary();
            /// <summary>Football DNA (signature behaviours) by player id, for the keepers that were added.</summary>
            public readonly Dictionary<string, FootballDNA> Dna = new Dictionary<string, FootballDNA>();
            /// <summary>Structural problems found by the file reader (only when imported through <see cref="ImportMarkdown"/>).</summary>
            public readonly List<string> ReaderProblems = new List<string>();
            public int Added => Library.Count;
            public int Failed { get { int n = 0; foreach (GoalkeeperImportResult r in Results) if (r.HasErrors) n++; return n; } }
            /// <summary>Authoring rating vs calculated rating of every keeper that was built.</summary>
            public IEnumerable<GoalkeeperRatingCheck> RatingChecks { get { foreach (GoalkeeperImportResult r in Results) if (r.Rating != null) yield return r.Rating; } }
        }

        public static Batch ImportMarkdown(string markdown, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null, GoalkeeperImportDefaults defaults = null)
        {
            GoalkeeperDatabaseParseResult parsed = GoalkeeperDatabaseMarkdown.Parse(markdown);
            Batch batch = Import(parsed.Records, behaviors, actionMap, defaults);
            batch.ReaderProblems.AddRange(parsed.Problems);
            return batch;
        }

        public static Batch Import(IEnumerable<GoalkeeperDataRecord> records, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null,
                                   GoalkeeperImportDefaults defaults = null)
        {
            var batch = new Batch();
            var seen = new HashSet<string>();
            foreach (GoalkeeperDataRecord rec in records)
            {
                GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(rec, behaviors, actionMap, defaults);
                if (rec != null && !string.IsNullOrEmpty(rec.PlayerId) && !seen.Add(rec.PlayerId))
                {
                    res.Issues.Add(new GoalkeeperImportIssue
                    {
                        Code = GoalkeeperImportCode.DuplicatePlayerId, IsError = true, PlayerId = rec.PlayerId, Field = "playerId", Value = rec.PlayerId,
                        Message = "Player id '" + rec.PlayerId + "' was already used by an earlier record; this one is rejected."
                    });
                    res.Profile = null; res.Player = null; res.Dna = null; res.Rating = null;
                }
                batch.Results.Add(res);
                if (!res.IsComplete) continue;
                if (batch.Library.TryAdd(res.Player) && batch.Library.TryAddGoalkeeperProfile(res.Profile)) batch.Dna[res.Player.Id] = res.Dna;
            }
            return batch;
        }
    }

    /// <summary>Behaviour scope rules: a goalkeeper may not have a field-player-only behaviour and a field player may not have a goalkeeper-only one.</summary>
    public static class BehaviorScopeRules
    {
        public static ValidationResult Validate(IEnumerable<string> behaviorIds, SignatureBehaviorCatalog catalog, bool isGoalkeeper, string who)
        {
            var r = new ValidationResult();
            if (behaviorIds == null || catalog == null) return r;
            foreach (string id in behaviorIds)
            {
                if (!catalog.TryGet(id, out SignatureBehaviorDefinition d)) continue;
                bool bad = isGoalkeeper ? d.Scope == BehaviorScope.FieldPlayer : d.Scope == BehaviorScope.Goalkeeper;
                if (bad) r.Add(ValidationCode.BehaviorScopeMismatch, who,
                      "Behaviour '" + id + "' is for " + (isGoalkeeper ? "field players" : "goalkeepers") + " only.");
            }
            return r;
        }
    }

}
