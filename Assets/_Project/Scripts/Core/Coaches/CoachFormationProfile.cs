using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>One of the five field slots of a coach's formation variant: the existing zone it starts in and the official role the coach wants there.</summary>
    [Serializable]
    public struct CoachFormationSlot
    {
        public PitchZone Zone;
        public PlayerArchetype Role;
        public CoachFormationSlot(PitchZone zone, PlayerArchetype role) { Zone = zone; Role = role; }
    }

    /// <summary>
    /// One tactical interpretation of a base formation by a coach: which of the five field slots plays where and with which official role, plus how the team
    /// plays (mentality, build-up, pressing, transitions and four 0..100 dials). Data only. The slots follow the order of the field positions of the base formation
    /// (<see cref="FormationDefinition.Positions"/> without the goalkeeper), so the geometry comes from the base formation; defensive slots stay defensive, the others may
    /// be reinterpreted among Midfield, Wing and Attack. It never adds a position or a role.
    /// </summary>
    [Serializable]
    public sealed class CoachFormationProfile
    {
        public const int FieldSlots = DataRules.FieldPlayersPerTeam;

        public string FormationId = "";
        public string CoachId = "";
        /// <summary>Id of the base formation in the <see cref="FormationLibrary"/> ("2-1-2", "1-2-2", "2-2-1", "3-1-1").</summary>
        public string BaseFormation = "";
        public string DisplayName = "";
        public string TacticalPurpose = "";
        public BackLineType BackLineType = BackLineType.Two;
        public List<CoachFormationSlot> Slots = new List<CoachFormationSlot>();
        public Mentality Mentality = Mentality.Balanced;
        public BuildUpStyle BuildUpStyle = BuildUpStyle.Balanced;
        public PressingStyle PressingStyle = PressingStyle.MidBlock;
        public TransitionStyle TransitionStyle = TransitionStyle.Controlled;
        /// <summary>0 = narrow, 100 = full width.</summary>
        public int Width = 50;
        /// <summary>0 = slow and patient, 100 = fast and vertical.</summary>
        public int Tempo = 50;
        /// <summary>0 = safe, 100 = takes risks.</summary>
        public int Risk = 50;
        /// <summary>0 = deep, 100 = high.</summary>
        public int DefensiveLine = 50;
        public List<string> CoachInstructions = new List<string>();
        /// <summary>Authoring note on how well established the real-world basis is (see <see cref="CoachNoteKind"/>).</summary>
        public CoachNoteKind Evidence = CoachNoteKind.Inference;

        /// <summary>The roles the variant asks for, without repeats, in slot order.</summary>
        public List<PlayerArchetype> PreferredRoles
        {
            get
            {
                var list = new List<PlayerArchetype>();
                foreach (CoachFormationSlot s in Slots) if (!list.Contains(s.Role)) list.Add(s.Role);
                return list;
            }
        }

        /// <summary>The zones the variant uses, without repeats, in slot order.</summary>
        public List<PitchZone> PreferredZones
        {
            get
            {
                var list = new List<PitchZone>();
                foreach (CoachFormationSlot s in Slots) if (!list.Contains(s.Zone)) list.Add(s.Zone);
                return list;
            }
        }

        public int DefenderSlots
        {
            get
            {
                int n = 0;
                foreach (CoachFormationSlot s in Slots) if (s.Zone == PitchZone.Defense) n++;
                return n;
            }
        }

        public static PlayerRole CoarseRoleOf(PitchZone zone)
        {
            switch (zone)
            {
                case PitchZone.Goal: return PlayerRole.Goalkeeper;
                case PitchZone.Defense: return PlayerRole.Defender;
                case PitchZone.Attack: return PlayerRole.Forward;
                default: return PlayerRole.Midfielder;
            }
        }

        /// <summary>
        /// The playable formation of this variant: the base formation's goalkeeper slot and geometry, with the zone and coarse role of each field slot taken from
        /// <see cref="Slots"/>. Returns null when the base formation is missing or the slot count does not match it.
        /// </summary>
        public FormationDefinition ToFormationDefinition(FormationDefinition baseFormation)
        {
            if (baseFormation == null || Slots == null) return null;
            var field = new List<FormationPosition>();
            FormationPosition keeper = default;
            bool hasKeeper = false;
            foreach (FormationPosition p in baseFormation.Positions)
            {
                if (p.Role == PlayerRole.Goalkeeper) { keeper = p; hasKeeper = true; }
                else field.Add(p);
            }
            if (!hasKeeper || field.Count != Slots.Count) return null;
            field.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            var result = new FormationDefinition(FormationId, DisplayName, keeper);
            for (int i = 0; i < field.Count; i++)
            {
                FormationPosition bp = field[i];
                result.Positions.Add(new FormationPosition(bp.PlayerIndex, CoarseRoleOf(Slots[i].Zone), Slots[i].Zone, bp.Relative.X, bp.Relative.Y));
            }
            return result;
        }

        /// <summary>The team style this variant asks for, in the existing <see cref="TeamStyleDefinition"/> terms (0..1 dials). Presentation of the same data, not a second source.</summary>
        public TeamStyleDefinition ToTeamStyle()
        {
            var style = new TeamStyleDefinition(FormationId, DisplayName)
            {
                PressTriggerScale = PressScale(PressingStyle),
                DefensiveLineHeight = DefensiveLine / 100f,
                AttackWidth = Width / 100f,
                Directness = Directness(BuildUpStyle),
                Tempo = Tempo / 100f,
                RiskTaking = Risk / 100f
            };
            return style;
        }

        private static float PressScale(PressingStyle style)
        {
            switch (style)
            {
                case PressingStyle.LowBlock: return 0.6f;
                case PressingStyle.MidBlock: return 0.9f;
                case PressingStyle.Selective: return 1.0f;
                case PressingStyle.HighPress: return 1.25f;
                default: return 1.4f;
            }
        }

        private static float Directness(BuildUpStyle style)
        {
            switch (style)
            {
                case BuildUpStyle.Positional: return 0.1f;
                case BuildUpStyle.Short: return 0.25f;
                case BuildUpStyle.Balanced: return 0.5f;
                case BuildUpStyle.FastVertical: return 0.8f;
                default: return 0.9f;
            }
        }
    }
}
