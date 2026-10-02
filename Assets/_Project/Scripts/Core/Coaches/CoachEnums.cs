using System;

namespace FS27.Core
{
    /// <summary>
    /// The coach's global grade, official scale A+ A A- B+ B B- C+ C (no S, no D). It is an overall judgement of the coach's career AS A COACH, not an
    /// average of the eight supporting ratings, and it says nothing about style: two A+ coaches can play completely differently.
    /// Higher value = better grade.
    /// </summary>
    public enum CoachGrade
    {
        C = 0,
        CPlus = 1,
        BMinus = 2,
        B = 3,
        BPlus = 4,
        AMinus = 5,
        A = 6,
        APlus = 7
    }

    public static class CoachGradeInfo
    {
        private static readonly string[] Labels = { "C", "C+", "B-", "B", "B+", "A-", "A", "A+" };

        public static bool IsValid(CoachGrade grade) { return (int)grade >= 0 && (int)grade < Labels.Length; }

        public static string ToLabel(CoachGrade grade) { return IsValid(grade) ? Labels[(int)grade] : "?"; }

        /// <summary>Reads "A+", "B-"... (exactly the official labels; nothing else is accepted).</summary>
        public static bool TryParse(string label, out CoachGrade grade)
        {
            for (int i = 0; i < Labels.Length; i++)
                if (string.Equals(Labels[i], label, StringComparison.Ordinal)) { grade = (CoachGrade)i; return true; }
            grade = CoachGrade.C;
            return false;
        }

        /// <summary>How many formations a grade has: A+ / A / A- = 3, B+ / B / B- = 2, C+ / C = 1.</summary>
        public static int FormationCount(CoachGrade grade)
        {
            if (grade >= CoachGrade.AMinus) return 3;
            if (grade >= CoachGrade.BMinus) return 2;
            return 1;
        }
    }

    /// <summary>How many defenders stand in the back line of a formation variant. A tactical structure, NOT a new zone or position: every slot still uses the existing zones.</summary>
    public enum BackLineType
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    public enum Mentality
    {
        Defensive = 0,
        Balanced = 1,
        Attacking = 2,
        VeryAttacking = 3
    }

    public enum BuildUpStyle
    {
        Direct = 0,
        Balanced = 1,
        Short = 2,
        Positional = 3,
        FastVertical = 4
    }

    public enum PressingStyle
    {
        LowBlock = 0,
        MidBlock = 1,
        Selective = 2,
        HighPress = 3,
        AggressivePress = 4
    }

    public enum TransitionStyle
    {
        Counter = 0,
        Controlled = 1,
        Fast = 2,
        PossessionRecovery = 3,
        Direct = 4
    }

    /// <summary>How well established an authoring note is: documented fact, translation of facts into FS27 (inference) or a design decision (authoring).</summary>
    public enum CoachNoteKind
    {
        Fact = 0,
        Inference = 1,
        Authoring = 2
    }
}
