using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Where an observation came from. It decides how much it is trusted when observations disagree.</summary>
    public enum ObservationSourceType
    {
        Manual = 0,
        /// <summary>Counted data (per-90 statistics...).</summary>
        Statistics,
        /// <summary>Someone (or a tool) watched play and noted patterns.</summary>
        VideoAnalysis,
        Scouting,
        /// <summary>Said in a prompt.</summary>
        Prompt,
        /// <summary>Inferred by a language model: useful, never fully trusted.</summary>
        Model
    }

    public enum ObservationKind
    {
        /// <summary>How much the subject does something: Pattern is a football-DNA parameter id ("dribbling.takeOn").</summary>
        Tendency = 0,
        /// <summary>Whether the subject has a signature behaviour: Pattern is a behaviour id.</summary>
        Behavior
    }

    /// <summary>
    /// One thing seen about how a (reference) subject plays. AUTHORING data only: it never ships, and it names no real person: the subject is an
    /// opaque reference id. A pattern, how strongly, in which situation, how sure we are, from which kind of source, when, and how many
    /// events stand behind it.
    /// </summary>
    [Serializable]
    public sealed class FootballObservation
    {
        public string Id = "";
        /// <summary>An opaque id of the reference profile this is about (never a real name).</summary>
        public string Subject = "";
        public ObservationKind Kind;
        /// <summary>A football-DNA parameter id (Tendency) or a behaviour id (Behavior).</summary>
        public string Pattern = "";
        /// <summary>0..1: how strongly (Tendency: low..high; Behavior: how typical of the subject).</summary>
        public float Value;
        /// <summary>0..1: how sure we are this observation is right.</summary>
        public float Confidence;
        public ObservationSourceType SourceType;
        /// <summary>The situation it was seen in (None = in general).</summary>
        public BehaviorContext Context;
        /// <summary>When it was recorded (ISO-8601 text; this build never reads a clock).</summary>
        public string Timestamp = "";
        /// <summary>How many events it is based on (0 = unknown).</summary>
        public int EvidenceCount;
        public string Note = "";
    }

    /// <summary>The things that are true of a pattern after combining everything seen about it.</summary>
    [Serializable]
    public sealed class AggregatedPattern
    {
        public ObservationKind Kind;
        public string Pattern = "";
        /// <summary>0..1: the trust-weighted value.</summary>
        public float Value;
        /// <summary>0..1: more and agreeing evidence raises it, disagreement lowers it.</summary>
        public float Confidence;
        public int EvidenceCount;
        public int ObservationCount;
        public int SourceTypes;
        /// <summary>Spread of the values seen (standard deviation); 0 when they all agree.</summary>
        public float Disagreement;
        /// <summary>Situations seen in more than half of the weight.</summary>
        public BehaviorContext Context;
    }

    /// <summary>Checks observations are well formed and refer to things that exist. Anything from a model or a file is untrusted until it passes.</summary>
    public static class ObservationValidator
    {
        public static CreatorValidationResult Validate(FootballObservation o, CreatorCatalogs catalogs)
        {
            var r = new CreatorValidationResult();
            string who = "observation '" + o?.Id + "'";
            if (o == null) { r.Error(CreatorIssueCode.ObservationInvalid, "observation", "Missing."); return r; }
            if (string.IsNullOrEmpty(o.Subject)) r.Error(CreatorIssueCode.ObservationInvalid, who, "A subject (opaque reference id) is required.");
            if (o.Kind == ObservationKind.Tendency)
            {
                if (!catalogs.Parameters.TryGet(o.Pattern, out ParameterDefinition p) || p.Domain != ParameterDomain.FootballDna)
                    r.Error(CreatorIssueCode.ParameterUnknown, who, "'" + o.Pattern + "' is not a football DNA tendency.");
            }
            else if (o.Kind == ObservationKind.Behavior)
            {
                if (!catalogs.Behaviors.Contains(o.Pattern)) r.Error(CreatorIssueCode.BehaviorUnknown, who, "'" + o.Pattern + "' is not in the behaviour catalog.");
            }
            else r.Error(CreatorIssueCode.ObservationInvalid, who, "Unknown kind.");
            if (float.IsNaN(o.Value) || o.Value < 0f || o.Value > 1f) r.Error(CreatorIssueCode.ConfidenceOutOfRange, who, "Value must be 0..1.");
            if (float.IsNaN(o.Confidence) || o.Confidence < 0f || o.Confidence > 1f) r.Error(CreatorIssueCode.ConfidenceOutOfRange, who, "Confidence must be 0..1.");
            if (o.EvidenceCount < 0) r.Error(CreatorIssueCode.ObservationInvalid, who, "EvidenceCount cannot be negative.");
            if (!Enum.IsDefined(typeof(ObservationSourceType), o.SourceType)) r.Error(CreatorIssueCode.ObservationInvalid, who, "Unknown source type.");
            if (o.Confidence == 0f) r.Warning(CreatorIssueCode.ObservationInvalid, who, "Zero confidence: it will carry no weight.");
            return r;
        }
    }

    /// <summary>Combines many observations of the same patterns into one deterministic summary per pattern.</summary>
    public static class ObservationAggregator
    {
        /// <summary>How much each kind of source is trusted. Starting values meant to be tuned.</summary>
        public static float Trust(ObservationSourceType s)
        {
            switch (s)
            {
                case ObservationSourceType.Manual: return 1.0f;
                case ObservationSourceType.Statistics: return 1.0f;
                case ObservationSourceType.VideoAnalysis: return 0.9f;
                case ObservationSourceType.Scouting: return 0.7f;
                case ObservationSourceType.Prompt: return 0.6f;
                default: return 0.5f;
            }
        }

        public static List<AggregatedPattern> Aggregate(IEnumerable<FootballObservation> observations)
        {
            var groups = new SortedDictionary<string, List<FootballObservation>>(StringComparer.Ordinal);
            foreach (FootballObservation o in observations)
            {
                string key = (int)o.Kind + "|" + o.Pattern;
                if (!groups.TryGetValue(key, out List<FootballObservation> list)) groups[key] = list = new List<FootballObservation>();
                list.Add(o);
            }

            var result = new List<AggregatedPattern>();
            foreach (KeyValuePair<string, List<FootballObservation>> g in groups)
            {
                List<FootballObservation> list = g.Value;
                // stable order, so the same set gives the same floating-point result whatever order it arrived in
                list.Sort((a, b) =>
                {
                    int c = string.CompareOrdinal(a.Id, b.Id);
                    if (c != 0) return c;
                    c = a.Value.CompareTo(b.Value);
                    return c != 0 ? c : a.Confidence.CompareTo(b.Confidence);
                });

                float wSum = 0f, vSum = 0f, noMiss = 1f;
                int evidence = 0;
                var sources = new HashSet<ObservationSourceType>();
                foreach (FootballObservation o in list)
                {
                    float w = o.Confidence * Trust(o.SourceType) * (float)Math.Sqrt(Math.Max(1, o.EvidenceCount));
                    wSum += w;
                    vSum += w * o.Value;
                    noMiss *= 1f - MathUtil.Clamp01(o.Confidence * Trust(o.SourceType));
                    evidence += o.EvidenceCount;
                    sources.Add(o.SourceType);
                }
                float mean = wSum > 0f ? vSum / wSum : 0.5f;
                float var = 0f;
                if (wSum > 0f)
                    foreach (FootballObservation o in list)
                    {
                        float w = o.Confidence * Trust(o.SourceType) * (float)Math.Sqrt(Math.Max(1, o.EvidenceCount));
                        var += w * (o.Value - mean) * (o.Value - mean);
                    }
                float sd = wSum > 0f ? (float)Math.Sqrt(var / wSum) : 0f;

                // independent agreeing sources raise confidence (1 - product of doubts); spread between them lowers it
                float confidence = MathUtil.Clamp01((1f - noMiss) * (1f - Math.Min(0.6f, sd * 1.5f)));
                if (wSum <= 0f) confidence = 0f;

                BehaviorContext context = BehaviorContext.None;
                for (int bit = 0; bit < 31; bit++)
                {
                    var flag = (BehaviorContext)(1 << bit);
                    float with = 0f;
                    foreach (FootballObservation o in list)
                        if ((o.Context & flag) != 0) with += o.Confidence * Trust(o.SourceType) * (float)Math.Sqrt(Math.Max(1, o.EvidenceCount));
                    if (wSum > 0f && with * 2f > wSum) context |= flag;
                }

                result.Add(new AggregatedPattern
                {
                    Kind = list[0].Kind, Pattern = list[0].Pattern, Value = MathUtil.Clamp01(mean), Confidence = confidence, EvidenceCount = evidence,
                    ObservationCount = list.Count, SourceTypes = sources.Count, Disagreement = sd, Context = context
                });
            }
            return result;
        }
    }
}
