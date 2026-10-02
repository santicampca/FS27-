using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>How well established an authoring note about a referee is: checked against reliable sources, not found, inferred by the authors, or a design value.</summary>
    public enum RefereeNoteKind
    {
        Verified = 0,
        Unverified = 1,
        AuthoringInference = 2,
        Authoring = 3
    }

    /// <summary>A line of authoring documentation about a referee. Never read by gameplay.</summary>
    [Serializable]
    public sealed class RefereeNote
    {
        public RefereeNoteKind Kind;
        public string Text = "";
        public RefereeNote() { }
        public RefereeNote(RefereeNoteKind kind, string text) { Kind = kind; Text = text; }
    }

    /// <summary>
    /// A referee style, as DATA (a new style is a new entry in the data file, not a code change). It is only a label with a description: nothing reads it yet.
    /// </summary>
    [Serializable]
    public sealed class RefereeStyleDefinition
    {
        public string StyleId = "";
        public string Name = "";
        public string Description = "";
        public RefereeStyleDefinition() { }
        public RefereeStyleDefinition(string styleId, string name, string description) { StyleId = styleId; Name = name; Description = description; }
    }

    /// <summary>
    /// The descriptive profile of a referee, all on the same 0..100 scale. AUTHORING VALUES prepared for a later gameplay phase: nothing reads them today, they feed no
    /// formula and no decision. Not a grade, not a rating: referees have no coach-style grade, formations or boosts.
    /// </summary>
    [Serializable]
    public sealed class RefereeProfile
    {
        public const int Min = 0;
        public const int Max = 100;

        public int Strictness;
        public int Cards;
        public int Advantage;
        public int GameControl;
        public int PenaltySensitivity;
        public int Consistency;
        public int Communication;

        public IEnumerable<KeyValuePair<string, int>> Values
        {
            get
            {
                yield return new KeyValuePair<string, int>("Strictness", Strictness);
                yield return new KeyValuePair<string, int>("Cards", Cards);
                yield return new KeyValuePair<string, int>("Advantage", Advantage);
                yield return new KeyValuePair<string, int>("GameControl", GameControl);
                yield return new KeyValuePair<string, int>("PenaltySensitivity", PenaltySensitivity);
                yield return new KeyValuePair<string, int>("Consistency", Consistency);
                yield return new KeyValuePair<string, int>("Communication", Communication);
            }
        }
    }

    /// <summary>
    /// A referee: identity, a style (by id, resolved in the <see cref="RefereeDatabase"/>) and a descriptive profile. Data only, independent of players and coaches;
    /// it contains no behaviour and no gameplay rule.
    /// </summary>
    [Serializable]
    public sealed class RefereeDefinition
    {
        public const int MaxFullNameLength = 64;

        public string RefereeId = "";
        public string Name = "";
        public string FullName = "";
        public string ShortName = "";
        public string NationalityCode = "";
        public string StyleId = "";
        public RefereeProfile Profile = new RefereeProfile();
        public string Description = "";
        /// <summary>Authoring documentation (verified facts, what could not be verified, inferences). Never read by gameplay.</summary>
        public List<RefereeNote> Notes = new List<RefereeNote>();
    }
}
