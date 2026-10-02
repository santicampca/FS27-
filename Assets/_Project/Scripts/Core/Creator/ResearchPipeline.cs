using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>What to look into. The subject is an opaque reference id, never a real name; nothing here is shipped.</summary>
    [Serializable]
    public sealed class ResearchQuery
    {
        public string Subject = "";
        public List<string> Topics = new List<string>();
        public string Question = "";
    }

    /// <summary>One piece of raw material a research provider brought back. Authoring-only: it is analysed and then discarded, never shipped.</summary>
    [Serializable]
    public sealed class ResearchFinding
    {
        public string Id = "";
        public ObservationSourceType SourceType;
        public string Summary = "";
        /// <summary>An opaque citation id (not a URL that the game would ever open).</summary>
        public string Reference = "";
        /// <summary>0..1: how much the source itself can be trusted.</summary>
        public float Reliability = 1f;
        public string Timestamp = "";
        /// <summary>Numeric facts (per-90 statistics...), if the finding has any.</summary>
        public SortedDictionary<string, float> Stats = new SortedDictionary<string, float>(StringComparer.Ordinal);
    }

    public sealed class ResearchResult
    {
        public readonly List<ResearchFinding> Findings = new List<ResearchFinding>();
        public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Step 1-2 of the research pipeline: gets raw findings from outside (a spreadsheet, a web adapter, a person, a language model with a search
    /// tool...). A CONTRACT: this build ships no adapter that reaches the internet, and nothing in the engine requires one.
    /// </summary>
    public interface IResearchProvider
    {
        string Name { get; }
        ResearchResult Research(ResearchQuery query);
    }

    public sealed class AnalysisResult
    {
        public readonly List<FootballObservation> Observations = new List<FootballObservation>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Step 3 of the research pipeline: turns one finding into observations. Whatever it returns is UNTRUSTED (a language model may be behind
    /// it): the pipeline validates every observation against the catalogs and drops what does not pass.
    /// </summary>
    public interface IFootballAnalysisProvider
    {
        string Name { get; }
        AnalysisResult Analyze(ResearchQuery query, ResearchFinding finding);
    }

    /// <summary>Hands over findings you already have (typed in, loaded from a file, produced by a test). No network, no model.</summary>
    public sealed class InlineResearchProvider : IResearchProvider
    {
        private readonly List<ResearchFinding> findings = new List<ResearchFinding>();

        public string Name => "FS27.InlineResearchProvider.v1";

        public InlineResearchProvider(params ResearchFinding[] findings)
        {
            this.findings.AddRange(findings);
        }

        public ResearchResult Research(ResearchQuery query)
        {
            var r = new ResearchResult();
            r.Findings.AddRange(findings);
            return r;
        }
    }

    /// <summary>
    /// A REAL, offline analysis: per-90 style statistics become tendencies by comparing each one with a reference mean and spread (a z-score), so
    /// "takes 6 dribbles per 90 where the typical player takes 3" becomes a high dribbling tendency. Deterministic. The reference table is data
    /// you can change; the numbers shipped are starting values meant to be tuned, not claims about any real league.
    /// </summary>
    public sealed class StatLineAnalyzer : IFootballAnalysisProvider
    {
        private sealed class Rule
        {
            public string Stat, Tendency;
            public float Mean, StdDev;
            public string Behavior;
        }

        private static readonly Rule[] Rules =
        {
            new Rule { Stat = "dribbles_attempted_p90", Tendency = "dribbling.takeOn", Mean = 3.0f, StdDev = 1.5f },
            new Rule { Stat = "shots_p90", Tendency = "shooting.frequency", Mean = 2.0f, StdDev = 1.0f },
            new Rule { Stat = "shots_outside_box_share", Tendency = "shooting.longShot", Mean = 0.35f, StdDev = 0.15f, Behavior = DefaultBehaviors.LongRangeShot },
            new Rule { Stat = "first_time_shot_share", Tendency = "shooting.firstTime", Mean = 0.30f, StdDev = 0.15f, Behavior = DefaultBehaviors.FirstTimeFinish },
            new Rule { Stat = "through_balls_p90", Tendency = "passing.throughBall", Mean = 0.6f, StdDev = 0.4f, Behavior = DefaultBehaviors.RiskyThroughBall },
            new Rule { Stat = "crosses_p90", Tendency = "passing.cross", Mean = 2.0f, StdDev = 1.5f },
            new Rule { Stat = "progressive_passes_p90", Tendency = "passing.progressive", Mean = 5.0f, StdDev = 2.5f },
            new Rule { Stat = "short_pass_share", Tendency = "passing.short", Mean = 0.70f, StdDev = 0.10f },
            new Rule { Stat = "pressures_p90", Tendency = "defending.pressing", Mean = 14f, StdDev = 5f, Behavior = DefaultBehaviors.AggressivePress },
            new Rule { Stat = "tackles_p90", Tendency = "defending.aggression", Mean = 2.2f, StdDev = 1.0f },
            new Rule { Stat = "interceptions_p90", Tendency = "defending.interception", Mean = 1.4f, StdDev = 0.8f },
            new Rule { Stat = "touches_in_box_p90", Tendency = "positioning.boxPresence", Mean = 4.0f, StdDev = 2.5f },
            new Rule { Stat = "back_to_goal_touch_share", Tendency = "possession.holdUp", Mean = 0.10f, StdDev = 0.07f, Behavior = DefaultBehaviors.HoldUpPlay }
        };

        /// <summary>The statistic that says how many minutes the numbers are based on (it sets the evidence count and the confidence).</summary>
        public const string MinutesStat = "minutes";
        /// <summary>How much one standard deviation moves a tendency away from 0.5.</summary>
        public const float LevelPerStdDev = 0.18f;
        /// <summary>A behaviour is only reported when its statistic is at least this many standard deviations above the mean.</summary>
        public const float BehaviorZ = 1.0f;

        public string Name => "FS27.StatLineAnalyzer.v1";

        public AnalysisResult Analyze(ResearchQuery query, ResearchFinding finding)
        {
            var result = new AnalysisResult();
            float minutes = finding.Stats.TryGetValue(MinutesStat, out float m) ? m : 0f;
            float sample = MathUtil.Clamp01(minutes / 900f);
            if (minutes <= 0f) result.Warnings.Add("No '" + MinutesStat + "' in the statistics: confidence is low.");
            float confidence = MathUtil.Clamp01(finding.Reliability * (minutes > 0f ? 0.4f + 0.6f * sample : 0.3f));
            int evidence = (int)(minutes / 90f);

            foreach (Rule rule in Rules)
            {
                if (!finding.Stats.TryGetValue(rule.Stat, out float value)) continue;
                float z = (value - rule.Mean) / rule.StdDev;
                float level = MathUtil.Clamp01(0.5f + z * LevelPerStdDev);
                result.Observations.Add(new FootballObservation
                {
                    Id = finding.Id + ":" + rule.Stat, Subject = query.Subject, Kind = ObservationKind.Tendency, Pattern = rule.Tendency, Value = level, Confidence = confidence,
                    SourceType = ObservationSourceType.Statistics, Timestamp = finding.Timestamp, EvidenceCount = evidence,
                    Note = rule.Stat + " = " + value.ToString("0.###", CultureInfo.InvariantCulture) + " (z " + z.ToString("0.00", CultureInfo.InvariantCulture) + ")"
                });
                if (rule.Behavior != null && z >= BehaviorZ)
                {
                    result.Observations.Add(new FootballObservation
                    {
                        Id = finding.Id + ":" + rule.Stat + ":behavior", Subject = query.Subject, Kind = ObservationKind.Behavior, Pattern = rule.Behavior, Value = level,
                        Confidence = confidence * 0.8f, SourceType = ObservationSourceType.Statistics, Timestamp = finding.Timestamp, EvidenceCount = evidence,
                        Note = "inferred from " + rule.Stat
                    });
                }
            }
            return result;
        }
    }

    public sealed class ResearchPipelineResult
    {
        public readonly List<ResearchFinding> Findings = new List<ResearchFinding>();
        public readonly List<FootballObservation> Accepted = new List<FootballObservation>();
        public readonly List<KeyValuePair<FootballObservation, string>> Rejected = new List<KeyValuePair<FootballObservation, string>>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public ComposerResult Composed;

        public bool Success => Errors.Count == 0 && Composed != null;
    }

    /// <summary>
    /// External sources -> research provider -> observation extraction -> validation -> pattern analysis -> FootballDNA. Every step is a seam: a
    /// provider can be a spreadsheet reader today and a web or model adapter tomorrow, and nothing downstream changes. Offline by default; a
    /// provider that fails is reported, never fatal, and never makes up data.
    /// </summary>
    public static class ResearchPipeline
    {
        public static ResearchPipelineResult Run(ResearchQuery query, IResearchProvider research, IList<IFootballAnalysisProvider> analyzers, CreatorCatalogs catalogs,
                                                 ComposerInput composerBase = null)
        {
            var result = new ResearchPipelineResult();
            ResearchResult found;
            try { found = research.Research(query); }
            catch (Exception e)
            {
                result.Errors.Add("The research provider '" + research.Name + "' failed: " + e.Message);
                return result;
            }
            result.Findings.AddRange(found.Findings);
            foreach (string err in found.Errors) result.Warnings.Add("Research: " + err);

            foreach (ResearchFinding f in found.Findings)
            {
                foreach (IFootballAnalysisProvider a in analyzers)
                {
                    AnalysisResult ar;
                    try { ar = a.Analyze(query, f); }
                    catch (Exception e)
                    {
                        result.Warnings.Add("The analyzer '" + a.Name + "' failed on '" + f.Id + "': " + e.Message);
                        continue;
                    }
                    foreach (string w in ar.Warnings) result.Warnings.Add(a.Name + ": " + w);
                    foreach (FootballObservation o in ar.Observations)
                    {
                        // whatever an analyzer says is untrusted until it passes the validator
                        CreatorValidationResult v = ObservationValidator.Validate(o, catalogs);
                        if (v.IsValid) result.Accepted.Add(o);
                        else result.Rejected.Add(new KeyValuePair<FootballObservation, string>(o, v.ToString()));
                    }
                }
            }

            ComposerInput input = composerBase ?? new ComposerInput();
            var all = new List<FootballObservation>(result.Accepted);
            if (input.Observations != null) all.AddRange(input.Observations);
            input.Observations = all;
            result.Composed = FootballDnaComposer.Compose(input, catalogs);
            return result;
        }
    }

    /// <summary>Reads and writes observations as JSON: the structured form a model or a spreadsheet exporter would hand over. Untrusted input: use <see cref="ObservationValidator"/> after.</summary>
    public static class ObservationJson
    {
        public const string Schema = "FS27.Observations.v1";

        public static string ToJson(IEnumerable<FootballObservation> observations)
        {
            var arr = JsonValue.NewArray();
            foreach (FootballObservation o in observations)
            {
                arr.Add(JsonValue.NewObject()
                    .Set("id", JsonValue.Of(o.Id)).Set("subject", JsonValue.Of(o.Subject)).Set("kind", JsonValue.Of(o.Kind.ToString())).Set("pattern", JsonValue.Of(o.Pattern))
                    .Set("value", JsonValue.Of(o.Value)).Set("confidence", JsonValue.Of(o.Confidence)).Set("source", JsonValue.Of(o.SourceType.ToString()))
                    .Set("context", JsonValue.Of((double)(int)o.Context)).Set("timestamp", JsonValue.Of(o.Timestamp)).Set("evidence", JsonValue.Of((double)o.EvidenceCount))
                    .Set("note", JsonValue.Of(o.Note)));
            }
            return Json.Write(JsonValue.NewObject().Set("schemaVersion", JsonValue.Of(Schema)).Set("observations", arr));
        }

        public static bool TryFromJson(string text, out List<FootballObservation> observations, CreatorValidationResult result)
        {
            observations = new List<FootballObservation>();
            if (!Json.TryParse(text, out JsonValue root, out string error)) { result.Error(CreatorIssueCode.JsonInvalid, "observations", error); return false; }
            if (root.Kind != JsonKind.Object || root.GetString("schemaVersion", null) != Schema) { result.Error(CreatorIssueCode.SchemaVersionUnsupported, "observations", "Expected schema '" + Schema + "'."); return false; }
            if (!root.TryGet("observations", out JsonValue list) || list.Kind != JsonKind.Array) { result.Error(CreatorIssueCode.JsonShapeInvalid, "observations", "Must be an array."); return false; }
            foreach (JsonValue item in list.Items)
            {
                if (item.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "observations", "Every observation must be an object."); return false; }
                var o = new FootballObservation
                {
                    Id = item.GetString("id", ""), Subject = item.GetString("subject", ""), Pattern = item.GetString("pattern", ""), Value = (float)item.GetNumber("value", double.NaN),
                    Confidence = (float)item.GetNumber("confidence", 0.0), Timestamp = item.GetString("timestamp", ""), Note = item.GetString("note", ""),
                    Context = (BehaviorContext)(int)item.GetNumber("context", 0.0), EvidenceCount = (int)item.GetNumber("evidence", 0.0)
                };
                if (!Enum.TryParse(item.GetString("kind", ""), out o.Kind)) { result.Error(CreatorIssueCode.ObservationInvalid, o.Id, "Unknown kind."); continue; }
                if (!Enum.TryParse(item.GetString("source", ""), out o.SourceType)) { result.Error(CreatorIssueCode.ObservationInvalid, o.Id, "Unknown source type."); continue; }
                observations.Add(o);
            }
            return result.IsValid;
        }
    }
}
