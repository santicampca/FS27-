using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>A line of authoring documentation about a coach: what is documented (Fact), what it was translated to in FS27 (Inference) or decided (Authoring). Never read by gameplay.</summary>
    [Serializable]
    public sealed class CoachNote
    {
        public CoachNoteKind Kind;
        public string Text = "";
        public CoachNote() { }
        public CoachNote(CoachNoteKind kind, string text) { Kind = kind; Text = text; }
    }

    /// <summary>
    /// A coach: identity, a global grade, eight supporting ratings, a philosophy, a compatibility profile and the formation variants the grade allows.
    /// Independent of <see cref="PlayerDefinition"/>: it only READS a player (and its playing profile) to compute compatibility, and it never changes one.
    /// The grade is a global judgement, NOT the average of the supporting ratings, and says nothing about style.
    /// </summary>
    [Serializable]
    public sealed class CoachDefinition
    {
        public const int MinRating = 0;
        public const int MaxRating = 100;
        public const int MaxFullNameLength = 64;

        public string CoachId = "";
        public string Name = "";
        public string FullName = "";
        public string ShortName = "";
        public string NationalityCode = "";
        public CoachGrade Grade = CoachGrade.C;

        // Supporting ratings (0..100). They explain the grade, they do not compute it.
        public int TacticalIQ;
        /// <summary>Called AttackRating / DefenseRating (not Attack / Defense) because a field named like one of the 12 attributes would look like a second copy of it.</summary>
        public int AttackRating;
        public int DefenseRating;
        public int Transitions;
        public int Adaptability;
        public int PlayerDevelopment;
        public int Management;
        public int Identity;

        public string Philosophy = "";
        public string TacticalIdentity = "";
        public CoachCompatibilityProfile CompatibilityProfile = new CoachCompatibilityProfile();
        public List<CoachFormationProfile> AvailableFormations = new List<CoachFormationProfile>();
        /// <summary>Authoring documentation (facts, inferences, design decisions). Never read by gameplay.</summary>
        public List<CoachNote> Notes = new List<CoachNote>();

        /// <summary>The roles the coach clearly favours (compatibility weight 70 or more), strongest first.</summary>
        public List<PlayerArchetype> PreferredRoles
        {
            get
            {
                var list = new List<WeightedRole>();
                foreach (WeightedRole r in CompatibilityProfile.Roles) if (r.Weight >= 70) list.Add(r);
                list.Sort((a, b) => b.Weight.CompareTo(a.Weight));
                var roles = new List<PlayerArchetype>();
                foreach (WeightedRole r in list) roles.Add(r.Role);
                return roles;
            }
        }

        /// <summary>The zones the coach clearly favours (compatibility weight 70 or more), strongest first.</summary>
        public List<PitchZone> PreferredZones
        {
            get
            {
                var list = new List<WeightedZone>();
                foreach (WeightedZone z in CompatibilityProfile.Zones) if (z.Weight >= 70) list.Add(z);
                list.Sort((a, b) => b.Weight.CompareTo(a.Weight));
                var zones = new List<PitchZone>();
                foreach (WeightedZone z in list) zones.Add(z.Zone);
                return zones;
            }
        }

        /// <summary>True when at least one formation variant has a back line of three.</summary>
        public bool HasBackThree
        {
            get
            {
                foreach (CoachFormationProfile f in AvailableFormations) if (f.BackLineType == BackLineType.Three) return true;
                return false;
            }
        }

        public CoachFormationProfile FindFormation(string formationId)
        {
            foreach (CoachFormationProfile f in AvailableFormations) if (f.FormationId == formationId) return f;
            return null;
        }
    }
}
