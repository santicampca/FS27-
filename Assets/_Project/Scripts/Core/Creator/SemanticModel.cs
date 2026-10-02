using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>What the person wants to happen. Provider-neutral: a parser, an editor or a language model all produce these.</summary>
    public enum SemanticIntent
    {
        Unknown = 0,
        Create,
        Modify,
        Remove,
        Increase,
        Decrease,
        Set,
        Prefer,
        Avoid,
        Add,
        Replace,
        Compare,
        Constrain,
        Preserve,
        Reset,
        Undo
    }

    /// <summary>
    /// How much, as a level of a documented scale (see <see cref="MagnitudeEngine"/>). Words are mapped to levels by language packs;
    /// levels are mapped to numbers in ONE place.
    /// </summary>
    public enum MagnitudeLevel
    {
        Unspecified = 0,
        Minimal,
        Slight,
        Little,
        Moderate,
        Quite,
        Much,
        VeryMuch,
        Extreme,
        Maximum
    }

    /// <summary>Which part of the game a request is about. "Fast" or "strong" mean different things in each.</summary>
    public enum SemanticDomain
    {
        Unspecified = 0,
        /// <summary>How the character looks.</summary>
        Visual,
        /// <summary>What the character can do / how it plays.</summary>
        Gameplay,
        /// <summary>How movement is shown (stride, playback speed).</summary>
        Animation
    }

    /// <summary>When in the game a gameplay request applies. "Strong in duels" is not "strong all the time".</summary>
    public enum SemanticPhase
    {
        Any = 0,
        WithBall,
        WithoutBall,
        Duels,
        Defending,
        Attacking,
        Pressure,
        Sprint
    }

    /// <summary>Which character a command refers to.</summary>
    public enum EntityReference
    {
        /// <summary>The character being authored right now (default).</summary>
        Current = 0,
        /// <summary>The character created before the current one ("como el anterior").</summary>
        Previous,
        /// <summary>A catalog character / archetype by name (resolved by the host).</summary>
        Named
    }

    public enum RelationKind
    {
        /// <summary>"A but B": both are wanted, B limits A.</summary>
        Contrast,
        /// <summary>"A without losing B".</summary>
        TradeOff,
        /// <summary>"A when B".</summary>
        Condition,
        /// <summary>"A like B" (reference to another character's trait).</summary>
        Like
    }

    /// <summary>
    /// One statement of the person, normalised. Examples: INCREASE speed (gameplay) Much; AVOID dribbling Little; SET height Extreme;
    /// REMOVE facialHair.
    /// </summary>
    [Serializable]
    public sealed class SemanticCommand
    {
        public int Id;
        public SemanticIntent Intent;
        /// <summary>A concept id from the <see cref="ConceptCatalog"/> (height, speed, hair, dribbling...). A concept, not a parameter id.</summary>
        public string Target = "";
        public SemanticDomain Domain;
        public SemanticPhase Phase;
        /// <summary>The direction of the change: Increase/Decrease/Set/Add/Remove/Replace/Reset/Preserve. Usually equals the intent.</summary>
        public SemanticIntent Operation;
        /// <summary>For Set: which end of the concept is meant (+1 tall, -1 short). Increase/Decrease already say it, so it stays +1 there.</summary>
        public int Direction = 1;
        public MagnitudeLevel Magnitude;
        /// <summary>True when the person negated it ("no demasiado musculoso"). The compiler applies the negation rule; the parser does not.</summary>
        public bool Negated;
        /// <summary>An explicit choice (hair style id/word, colour word, role). Empty when the command is only about direction/amount.</summary>
        public string Value = "";
        /// <summary>A finer reading of the concept ("tendency": plays it often; "ability": is good at it). Empty = the default one.</summary>
        public string Sense = "";
        public EntityReference Reference;
        public string ReferenceName = "";
        /// <summary>0..1.</summary>
        public float Confidence = 1f;
        /// <summary>The words this command came from.</summary>
        public string Source = "";

        public SemanticCommand Clone()
        {
            return (SemanticCommand)MemberwiseClone();
        }
    }

    /// <summary>A link between two commands (by <see cref="SemanticCommand.Id"/>).</summary>
    [Serializable]
    public sealed class SemanticRelation
    {
        public RelationKind Kind;
        public int From;
        public int To;
    }

    /// <summary>"Keep this" / "only change this" / "never exceed this".</summary>
    [Serializable]
    public sealed class SemanticConstraint
    {
        public SemanticIntent Intent;
        public string Target = "";
        /// <summary>Preserve everything not named by a command.</summary>
        public bool AllElse;
        /// <summary>Only the named targets may change.</summary>
        public bool OnlyThese;
        public string Source = "";
    }

    /// <summary>
    /// The meaning of a request, as data. The one thing every interpreter (deterministic parser, language-model provider, editor) must
    /// produce. Everything after it (compiling, patching, conflicts, validation) is shared and deterministic.
    /// </summary>
    [Serializable]
    public sealed class SemanticProgram
    {
        public const string CurrentSchema = "FS27.SemanticProgram.v1";

        public string SchemaVersion = CurrentSchema;
        public string Language = "";
        public string Text = "";
        public List<SemanticCommand> Commands = new List<SemanticCommand>();
        public List<SemanticRelation> Relations = new List<SemanticRelation>();
        public List<SemanticConstraint> Constraints = new List<SemanticConstraint>();
        /// <summary>The parts of the text no rule explained.</summary>
        public List<string> Unparsed = new List<string>();
        /// <summary>Things the words allowed more than one reading of, that the parser could not settle ("largo": hair or legs?).</summary>
        public List<string> Ambiguities = new List<string>();
        public string ParserName = "";

        public bool IsEmpty => Commands.Count == 0 && Constraints.Count == 0;

        public SemanticCommand FindCommand(int id)
        {
            foreach (SemanticCommand c in Commands) if (c.Id == id) return c;
            return null;
        }
    }

    /// <summary>Checks a program is well formed: known enums, known targets, sensible numbers. Does not judge whether it is wise.</summary>
    public static class SemanticProgramValidator
    {
        public static CreatorValidationResult Validate(SemanticProgram p, ConceptCatalog concepts)
        {
            var r = new CreatorValidationResult();
            if (p == null) { r.Error(CreatorIssueCode.SemanticFieldInvalid, "program", "The program is missing."); return r; }
            if (p.SchemaVersion != SemanticProgram.CurrentSchema) r.Error(CreatorIssueCode.SemanticSchemaUnknown, "schemaVersion", "Unknown program schema '" + p.SchemaVersion + "'.");
            var ids = new HashSet<int>();
            foreach (SemanticCommand c in p.Commands)
            {
                string at = "commands[" + c.Id + "]";
                if (!ids.Add(c.Id)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Duplicate command id.");
                if (!Enum.IsDefined(typeof(SemanticIntent), c.Intent) || c.Intent == SemanticIntent.Unknown) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown intent.");
                if (!Enum.IsDefined(typeof(SemanticIntent), c.Operation)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown operation.");
                if (!Enum.IsDefined(typeof(MagnitudeLevel), c.Magnitude)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown magnitude.");
                if (!Enum.IsDefined(typeof(SemanticDomain), c.Domain)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown domain.");
                if (!Enum.IsDefined(typeof(SemanticPhase), c.Phase)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "Unknown phase.");
                if (float.IsNaN(c.Confidence) || c.Confidence < 0f || c.Confidence > 1f) r.Error(CreatorIssueCode.SemanticValueOutOfRange, at, "Confidence must be 0..1.");
                bool needsTarget = c.Intent != SemanticIntent.Undo && c.Intent != SemanticIntent.Create && !(c.Intent == SemanticIntent.Preserve && string.IsNullOrEmpty(c.Target));
                if (needsTarget && string.IsNullOrEmpty(c.Target)) r.Error(CreatorIssueCode.SemanticFieldInvalid, at, "A target is required.");
                else if (!string.IsNullOrEmpty(c.Target) && concepts != null && !concepts.Contains(c.Target))
                    r.Error(CreatorIssueCode.SemanticTargetUnknown, at, "Unknown target concept '" + c.Target + "'.");
            }
            foreach (SemanticRelation rel in p.Relations)
                if (!ids.Contains(rel.From) || !ids.Contains(rel.To)) r.Error(CreatorIssueCode.SemanticRelationInvalid, "relations", "A relation points to a command that does not exist.");
            foreach (SemanticConstraint k in p.Constraints)
                if (!string.IsNullOrEmpty(k.Target) && concepts != null && !concepts.Contains(k.Target))
                    r.Error(CreatorIssueCode.SemanticTargetUnknown, "constraints", "Unknown constraint target '" + k.Target + "'.");
            return r;
        }
    }
}

namespace FS27.Core
{
    /// <summary>A compact one-line-per-command description of a program, for debug reports and test failures.</summary>
    public static class SemanticProgramPrinter
    {
        public static string Describe(SemanticProgram p)
        {
            var sb = new System.Text.StringBuilder();
            foreach (SemanticCommand c in p.Commands)
            {
                sb.Append('#').Append(c.Id).Append(' ').Append(c.Intent);
                if (c.Operation != c.Intent) sb.Append('/').Append(c.Operation);
                if (!string.IsNullOrEmpty(c.Target)) sb.Append(' ').Append(c.Target);
                if (c.Operation == SemanticIntent.Set && c.Direction < 0) sb.Append(" (-)");
                if (c.Magnitude != MagnitudeLevel.Unspecified) sb.Append(' ').Append(c.Magnitude);
                if (c.Negated) sb.Append(" NEG");
                if (c.Domain != SemanticDomain.Unspecified) sb.Append(" [").Append(c.Domain).Append(']');
                if (c.Phase != SemanticPhase.Any) sb.Append(" @").Append(c.Phase);
                if (!string.IsNullOrEmpty(c.Sense)) sb.Append(" sense=").Append(c.Sense);
                if (!string.IsNullOrEmpty(c.Value)) sb.Append(" =").Append(c.Value);
                if (c.Reference != EntityReference.Current) sb.Append(" ref=").Append(c.Reference);
                sb.Append('\n');
            }
            foreach (SemanticRelation r in p.Relations) sb.Append(r.Kind).Append(' ').Append(r.From).Append("->").Append(r.To).Append('\n');
            foreach (SemanticConstraint k in p.Constraints) sb.Append("constraint ").Append(k.Intent).Append(' ').Append(k.Target).Append(k.AllElse ? " ALLELSE" : "").Append(k.OnlyThese ? " ONLY" : "").Append('\n');
            foreach (string u in p.Unparsed) sb.Append("unparsed: ").Append(u).Append('\n');
            foreach (string a in p.Ambiguities) sb.Append("ambiguous: ").Append(a).Append('\n');
            return sb.ToString();
        }
    }
}
