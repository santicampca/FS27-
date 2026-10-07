using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// How a team's initial tactical identity reads, in the terms the coach data already has (mentality, build-up, pressing, transition, back line and four 0..100 dials). It is a READ-ONLY
    /// view of data (the formation variant the team uses, or a neutral default): nothing here is behaviour. The future AI and tactics will read it.
    /// </summary>
    public struct TeamTacticalIdentity
    {
        /// <summary>The formation variant (coach) or base formation (no coach) this identity comes from.</summary>
        public string FormationId;
        public bool FromCoach;
        public BackLineType BackLineType;
        public Mentality Mentality;
        public BuildUpStyle BuildUpStyle;
        public PressingStyle PressingStyle;
        public TransitionStyle TransitionStyle;
        /// <summary>0 = narrow, 100 = full width.</summary>
        public int Width;
        /// <summary>0 = slow and patient, 100 = fast and vertical.</summary>
        public int Tempo;
        /// <summary>0 = safe, 100 = takes risks.</summary>
        public int Risk;
        /// <summary>0 = deep, 100 = high.</summary>
        public int DefensiveLine;

        /// <summary>The identity of a team with no coach: balanced everything, around a base formation.</summary>
        public static TeamTacticalIdentity Neutral(string baseFormationId, BackLineType backLine)
        {
            return new TeamTacticalIdentity
            {
                FormationId = baseFormationId, FromCoach = false, BackLineType = backLine, Mentality = Mentality.Balanced, BuildUpStyle = BuildUpStyle.Balanced,
                PressingStyle = PressingStyle.MidBlock, TransitionStyle = TransitionStyle.Controlled, Width = 50, Tempo = 50, Risk = 50, DefensiveLine = 50
            };
        }

        public static TeamTacticalIdentity From(CoachFormationProfile v)
        {
            return new TeamTacticalIdentity
            {
                FormationId = v.FormationId, FromCoach = true, BackLineType = v.BackLineType, Mentality = v.Mentality, BuildUpStyle = v.BuildUpStyle, PressingStyle = v.PressingStyle,
                TransitionStyle = v.TransitionStyle, Width = v.Width, Tempo = v.Tempo, Risk = v.Risk, DefensiveLine = v.DefensiveLine
            };
        }
    }

    /// <summary>The numbers and choices of how a team gets its formation. One place.</summary>
    public static class TeamFormationRules
    {
        /// <summary>The formation a team with no coach starts with (a base formation of the formation library). The user can change it later.</summary>
        public const string DefaultBaseFormationId = "2-1-2";
    }

    /// <summary>Finds the playable formation and the tactical identity of a team from its <see cref="TeamDefinition.FormationId"/>. The id is either a coach's formation variant or a base formation.</summary>
    public static class TeamFormationResolver
    {
        /// <summary>The coach variant a team's formation id points to, searching the team's own coach first and then every coach.</summary>
        public static bool TryFindVariant(TeamDefinition team, CoachLibrary coaches, out CoachFormationProfile variant)
        {
            variant = null;
            if (team == null || string.IsNullOrEmpty(team.FormationId) || coaches == null) return false;
            if (!string.IsNullOrEmpty(team.CoachId) && coaches.TryGet(team.CoachId, out CoachDefinition own))
                foreach (CoachFormationProfile v in own.AvailableFormations) if (v.FormationId == team.FormationId) { variant = v; return true; }
            foreach (CoachDefinition c in coaches.All)
                foreach (CoachFormationProfile v in c.AvailableFormations) if (v.FormationId == team.FormationId) { variant = v; return true; }
            return false;
        }

        /// <summary>The playable formation of the team: a coach variant built over its base formation, or a base formation as it is.</summary>
        public static bool TryResolve(TeamDefinition team, CoachLibrary coaches, FormationLibrary baseFormations, out FormationDefinition formation)
        {
            formation = null;
            if (team == null || string.IsNullOrEmpty(team.FormationId) || baseFormations == null) return false;
            if (TryFindVariant(team, coaches, out CoachFormationProfile v))
            {
                if (!baseFormations.TryGet(v.BaseFormation, out FormationDefinition baseF)) return false;
                formation = v.ToFormationDefinition(baseF);
                return formation != null;
            }
            return baseFormations.TryGet(team.FormationId, out formation);
        }

        public static bool TryIdentity(TeamDefinition team, CoachLibrary coaches, FormationLibrary baseFormations, out TeamTacticalIdentity identity)
        {
            identity = default(TeamTacticalIdentity);
            if (team == null || string.IsNullOrEmpty(team.FormationId)) return false;
            if (TryFindVariant(team, coaches, out CoachFormationProfile v)) { identity = TeamTacticalIdentity.From(v); return true; }
            if (baseFormations == null || !baseFormations.TryGet(team.FormationId, out FormationDefinition f)) return false;
            int defenders = 0;
            foreach (FormationPosition p in f.Positions) if (p.Role == PlayerRole.Defender) defenders++;
            identity = TeamTacticalIdentity.Neutral(f.Id, defenders >= 3 ? BackLineType.Three : BackLineType.Two);
            return true;
        }
    }

    /// <summary>
    /// Gives every team a formation, from data that already exists, with one visible rule (no names, only ids and levels):
    ///  - a team with a coach takes one of that coach's formation variants. How many variants a coach has is already settled by his grade in the data (A+/A/A- = 3, B+/B/B- = 2, C+/C = 1);
    ///  - an Elite or Strong team plays the coach's FIRST variant (his signature: the first one in his data);
    ///  - a Balanced or Modest team plays the variant with the LOWEST risk dial (ties: the lowest defensive line, then the first), a safer reading of the same coach;
    ///  - a team with no coach (the user team by default) takes <see cref="TeamFormationRules.DefaultBaseFormationId"/>.
    /// Nothing is random: the same data always gives the same formations.
    /// </summary>
    public static class TeamFormationAssigner
    {
        public static string Choose(TeamDefinition team, CoachLibrary coaches)
        {
            if (team != null && !string.IsNullOrEmpty(team.CoachId) && coaches != null && coaches.TryGet(team.CoachId, out CoachDefinition coach) && coach.AvailableFormations.Count > 0)
            {
                CoachFormationProfile pick = coach.AvailableFormations[0];
                if (team.Level == TeamLevel.Balanced || team.Level == TeamLevel.Modest)
                    foreach (CoachFormationProfile v in coach.AvailableFormations)
                        if (v.Risk < pick.Risk || (v.Risk == pick.Risk && v.DefensiveLine < pick.DefensiveLine)) pick = v;
                return pick.FormationId;
            }
            return TeamFormationRules.DefaultBaseFormationId;
        }

        /// <summary>Sets the formation of every team that has none. A formation already set is never overwritten (the user may have chosen it).</summary>
        public static void AssignMissing(IReadOnlyList<TeamDefinition> teams, CoachLibrary coaches)
        {
            foreach (TeamDefinition t in teams) if (string.IsNullOrEmpty(t.FormationId)) t.FormationId = Choose(t, coaches);
        }
    }
}
