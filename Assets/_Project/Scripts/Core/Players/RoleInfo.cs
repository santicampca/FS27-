namespace FS27.Core
{
    public enum RoleGroup
    {
        Defense,
        Creation,
        Mobility,
        Attack,
        Goalkeeping
    }

    /// <summary>
    /// Facts about each role: its group, its Spanish display name and whether it belongs to the official set.
    /// Display names are for UI only; ids in data stay the enum names. The enum keeps its existing English terminology.
    ///
    /// Official set (11): Defense — Guardian (Guardián), Wall (Muro); Creation — Builder (Constructor), Creator (Creador),
    /// Architect (Arquitecto); Mobility — Engine (Motor), Winger (Ala), Explosive (Explosivo); Attack — Finisher (Finalizador),
    /// GoalHunter (Cazagoles), Target (Objetivo).
    /// </summary>
    public static class RoleInfo
    {
        public static readonly PlayerArchetype[] OfficialRoles =
        {
            PlayerArchetype.Guardian, PlayerArchetype.Wall,
            PlayerArchetype.Builder, PlayerArchetype.Creator, PlayerArchetype.Architect,
            PlayerArchetype.Engine, PlayerArchetype.Winger, PlayerArchetype.Explosive,
            PlayerArchetype.Finisher, PlayerArchetype.GoalHunter, PlayerArchetype.Target
        };

        public static bool IsOfficial(PlayerArchetype role)
        {
            return System.Array.IndexOf(OfficialRoles, role) >= 0;
        }

        public static RoleGroup GroupOf(PlayerArchetype role)
        {
            switch (role)
            {
                case PlayerArchetype.Guardian:
                case PlayerArchetype.Wall:
                case PlayerArchetype.Destroyer:
                case PlayerArchetype.Anchor:
                    return RoleGroup.Defense;
                case PlayerArchetype.Builder:
                case PlayerArchetype.Creator:
                case PlayerArchetype.Architect:
                    return RoleGroup.Creation;
                case PlayerArchetype.Engine:
                case PlayerArchetype.Winger:
                case PlayerArchetype.Explosive:
                    return RoleGroup.Mobility;
                case PlayerArchetype.Finisher:
                case PlayerArchetype.GoalHunter:
                case PlayerArchetype.Target:
                    return RoleGroup.Attack;
                default:
                    return RoleGroup.Goalkeeping;
            }
        }

        public static string SpanishName(PlayerArchetype role)
        {
            switch (role)
            {
                case PlayerArchetype.Guardian: return "Guardián";
                case PlayerArchetype.Wall: return "Muro";
                case PlayerArchetype.Builder: return "Constructor";
                case PlayerArchetype.Creator: return "Creador";
                case PlayerArchetype.Architect: return "Arquitecto";
                case PlayerArchetype.Engine: return "Motor";
                case PlayerArchetype.Winger: return "Ala";
                case PlayerArchetype.Explosive: return "Explosivo";
                case PlayerArchetype.Finisher: return "Finalizador";
                case PlayerArchetype.GoalHunter: return "Cazagoles";
                case PlayerArchetype.Target: return "Objetivo";
                case PlayerArchetype.Destroyer: return "Recuperador";
                case PlayerArchetype.Anchor: return "Ancla";
                case PlayerArchetype.ShotStopper: return "Paradón";
                case PlayerArchetype.Sweeper: return "Líbero";
                default: return role.ToString();
            }
        }
    }
}
