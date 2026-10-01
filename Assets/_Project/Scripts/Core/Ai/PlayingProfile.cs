using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Where on the pitch a player plays. "Wing" is the flank (a lateral zone, not a depth line).</summary>
    public enum PitchZone
    {
        Goal = 0,
        Defense = 1,
        Midfield = 2,
        Attack = 3,
        Wing = 4
    }

    /// <summary>
    /// What a player is like in play: the "roles" of a versatile player (an explosive winger who also creates and finishes).
    /// The AI interprets these tags, the difficulty never changes them.
    ///
    /// The 12 official FS27 roles (see <see cref="RoleInfo"/>), by group: Defense — Wall, Guardian, Anchor; Creation — Builder,
    /// Creator, Architect; Mobility — Engine, Winger, Explosive; Attack — Finisher, GoalHunter, Target.
    /// The numbers are fixed once assigned (they may be stored in data). 3, 6 and 7 belonged to roles that duplicated official
    /// ones (or were goalkeeper-specific, which waits for a goalkeeper system); they are retired and must not be reused.
    /// </summary>
    public enum PlayerArchetype
    {
        Explosive = 0,
        Creator = 1,
        Finisher = 2,
        Anchor = 4,
        Engine = 5,

        Guardian = 8,
        Wall = 9,
        Builder = 10,
        Architect = 11,
        Winger = 12,
        GoalHunter = 13,
        Target = 14
    }

    /// <summary>A role together with how strongly it defines the player (0..100).</summary>
    [Serializable]
    public struct RoleAffinity
    {
        public PlayerArchetype Role;
        public int Affinity;

        public RoleAffinity(PlayerArchetype role, int affinity)
        {
            Role = role;
            Affinity = affinity;
        }
    }

    /// <summary>
    /// A player is not "a position". A primary zone, optional secondary zones, up to three roles (each with an affinity 0..100)
    /// and a behaviour profile describe how versatile they are and how they tend to play. It lives apart from
    /// <see cref="PlayerDefinition"/> (identity and attributes), linked by player id. It holds NO attributes.
    /// </summary>
    [Serializable]
    public class PlayerPlayingProfile
    {
        public const int MaxArchetypes = 3;

        public string PlayerId;
        public PitchZone PrimaryZone;
        public List<PitchZone> SecondaryZones = new List<PitchZone>();
        /// <summary>The player's roles with their affinity (0..100): up to <see cref="MaxArchetypes"/>, most defining first. The only place roles live.</summary>
        public List<RoleAffinity> Roles = new List<RoleAffinity>();

        // ---- Behaviour profile: how the player TENDS to play. Not attributes: nothing here changes what the player can do. ----

        /// <summary>0 = conservative, 100 = takes risks.</summary>
        public int RiskPreference = 50;
        /// <summary>0 = direct and simple, 100 = creative and unpredictable.</summary>
        public int Creativity = 50;
        /// <summary>0 = passive, 100 = aggressive.</summary>
        public int Aggression = 50;

        /// <summary>Affinity given to the 1st, 2nd and 3rd role by the convenience constructor that lists roles without affinities.</summary>
        public static readonly int[] DefaultAffinityByRank = { 90, 75, 60 };

        public PlayerPlayingProfile()
        {
        }

        public PlayerPlayingProfile(string playerId, PitchZone primary, PitchZone[] secondary, RoleAffinity[] roles)
        {
            PlayerId = playerId;
            PrimaryZone = primary;
            SecondaryZones = new List<PitchZone>(secondary ?? new PitchZone[0]);
            Roles = new List<RoleAffinity>(roles ?? new RoleAffinity[0]);
        }

        public PlayerPlayingProfile(string playerId, PitchZone primary, PitchZone[] secondary, params PlayerArchetype[] archetypes)
        {
            PlayerId = playerId;
            PrimaryZone = primary;
            SecondaryZones = new List<PitchZone>(secondary ?? new PitchZone[0]);
            for (int i = 0; i < archetypes.Length; i++) Roles.Add(new RoleAffinity(archetypes[i], RankAffinity(i)));
        }

        public bool HasArchetype(PlayerArchetype a)
        {
            return IndexOfRole(a) >= 0;
        }

        public bool PlaysIn(PitchZone zone)
        {
            return zone == PrimaryZone || (SecondaryZones != null && SecondaryZones.Contains(zone));
        }

        /// <summary>Adds a role with its affinity. Returns this.</summary>
        public PlayerPlayingProfile AddRole(PlayerArchetype role, int affinity)
        {
            Roles.Add(new RoleAffinity(role, affinity));
            return this;
        }

        public PlayerPlayingProfile WithBehaviour(int riskPreference, int creativity, int aggression)
        {
            RiskPreference = riskPreference;
            Creativity = creativity;
            Aggression = aggression;
            return this;
        }

        /// <summary>The affinity (0..100) of a role this player has, or 0 if it is not one of their roles.</summary>
        public int GetAffinity(PlayerArchetype role)
        {
            int i = IndexOfRole(role);
            return i < 0 ? 0 : Roles[i].Affinity;
        }

        /// <summary>The role with the highest affinity (the first one on a tie). False if the player has no roles.</summary>
        public bool TryGetPrimaryRole(out PlayerArchetype role)
        {
            role = default;
            if (Roles == null || Roles.Count == 0) return false;
            int best = -1;
            for (int i = 0; i < Roles.Count; i++)
            {
                if (Roles[i].Affinity > best)
                {
                    best = Roles[i].Affinity;
                    role = Roles[i].Role;
                }
            }
            return true;
        }

        /// <summary>A copy of the roles with their affinities, in order.</summary>
        public RoleAffinity[] GetRoleAffinities()
        {
            return Roles == null ? new RoleAffinity[0] : Roles.ToArray();
        }

        private int IndexOfRole(PlayerArchetype role)
        {
            if (Roles == null) return -1;
            for (int i = 0; i < Roles.Count; i++)
                if (Roles[i].Role == role) return i;
            return -1;
        }

        private static int RankAffinity(int rank)
        {
            return DefaultAffinityByRank[Math.Min(rank, DefaultAffinityByRank.Length - 1)];
        }
    }

    /// <summary>Profiles for players that have no explicit one: derived from the existing single <see cref="PlayerRole"/>.</summary>
    public static class PlayingProfileDefaults
    {
        public static PitchZone ZoneForRole(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper: return PitchZone.Goal;
                case PlayerRole.Defender: return PitchZone.Defense;
                case PlayerRole.Midfielder: return PitchZone.Midfield;
                default: return PitchZone.Attack;
            }
        }

        public static PlayerArchetype ArchetypeForRole(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper: return PlayerArchetype.Guardian; // placeholder until the goalkeeper system defines goalkeeper roles
                case PlayerRole.Defender: return PlayerArchetype.Wall;
                case PlayerRole.Midfielder: return PlayerArchetype.Engine;
                default: return PlayerArchetype.Finisher;
            }
        }

        public static PlayerPlayingProfile FromPlayer(PlayerDefinition player)
        {
            return new PlayerPlayingProfile(player.Id, ZoneForRole(player.Role), null, ArchetypeForRole(player.Role));
        }

        /// <summary>The zone of the pitch a relative formation position (depth, width) lies in.</summary>
        public static PitchZone ZoneOfRelativePosition(Vec2 relative)
        {
            if (relative.X < 0.15f) return PitchZone.Goal;
            if (relative.Y < 0.2f || relative.Y > 0.8f) return PitchZone.Wing;
            if (relative.X < 0.40f) return PitchZone.Defense;
            if (relative.X < 0.65f) return PitchZone.Midfield;
            return PitchZone.Attack;
        }
    }

    /// <summary>Finds the playing profile of a player by id; falls back to the default derived from the player's role.</summary>
    public sealed class PlayingProfileCatalog
    {
        private readonly Dictionary<string, PlayerPlayingProfile> byPlayer = new Dictionary<string, PlayerPlayingProfile>();

        public int Count => byPlayer.Count;

        public bool TryAdd(PlayerPlayingProfile profile)
        {
            if (profile == null || !DataRules.IsValidId(profile.PlayerId) || byPlayer.ContainsKey(profile.PlayerId)) return false;
            byPlayer.Add(profile.PlayerId, profile);
            return true;
        }

        public bool TryGet(string playerId, out PlayerPlayingProfile profile)
        {
            if (playerId == null)
            {
                profile = null;
                return false;
            }
            return byPlayer.TryGetValue(playerId, out profile);
        }

        public PlayerPlayingProfile GetOrDefault(PlayerDefinition player)
        {
            return TryGet(player.Id, out PlayerPlayingProfile p) ? p : PlayingProfileDefaults.FromPlayer(player);
        }
    }

    public static class PlayingProfileValidator
    {
        public static AiDataValidationResult Validate(PlayerPlayingProfile p, PlayerDefinition player = null)
        {
            var r = new AiDataValidationResult();
            if (p == null)
            {
                r.Add(AiDataIssueCode.ProfileNull, "profile", "Profile is null.");
                return r;
            }

            string who = "profile of '" + (p.PlayerId ?? "<no id>") + "'";
            if (!DataRules.IsValidId(p.PlayerId))
                r.Add(AiDataIssueCode.ProfilePlayerIdInvalid, who, "Player id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!Enum.IsDefined(typeof(PitchZone), p.PrimaryZone))
                r.Add(AiDataIssueCode.ProfileZoneInvalid, who, "Primary zone " + (int)p.PrimaryZone + " is not valid.");

            var seenZones = new HashSet<PitchZone>();
            if (p.SecondaryZones != null)
            {
                foreach (PitchZone z in p.SecondaryZones)
                {
                    if (!Enum.IsDefined(typeof(PitchZone), z)) r.Add(AiDataIssueCode.ProfileZoneInvalid, who, "Secondary zone " + (int)z + " is not valid.");
                    else if (z == p.PrimaryZone) r.Add(AiDataIssueCode.ProfileSecondaryEqualsPrimary, who, "Secondary zone " + z + " is the primary zone.");
                    else if (!seenZones.Add(z)) r.Add(AiDataIssueCode.ProfileSecondaryZoneDuplicate, who, "Secondary zone " + z + " is listed twice.");
                }
            }

            int count = p.Roles == null ? 0 : p.Roles.Count;
            if (count < 1 || count > PlayerPlayingProfile.MaxArchetypes)
                r.Add(AiDataIssueCode.ProfileArchetypeCountInvalid, who, "A player needs 1-" + PlayerPlayingProfile.MaxArchetypes + " roles, found " + count + ".");
            if (p.Roles != null)
            {
                var seen = new HashSet<PlayerArchetype>();
                foreach (RoleAffinity ra in p.Roles)
                {
                    if (!Enum.IsDefined(typeof(PlayerArchetype), ra.Role)) r.Add(AiDataIssueCode.ProfileArchetypeInvalid, who, "Role " + (int)ra.Role + " is not valid.");
                    else if (!seen.Add(ra.Role)) r.Add(AiDataIssueCode.ProfileArchetypeDuplicate, who, "Role " + ra.Role + " is listed twice.");
                    if (ra.Affinity < 0 || ra.Affinity > 100) r.Add(AiDataIssueCode.ProfileAffinityOutOfRange, who, "Affinity " + ra.Affinity + " of role " + ra.Role + " is outside 0..100.");
                }
            }

            Behaviour(r, who, "RiskPreference", p.RiskPreference);
            Behaviour(r, who, "Creativity", p.Creativity);
            Behaviour(r, who, "Aggression", p.Aggression);

            if (player != null)
            {
                if (p.PlayerId != null && player.Id != null && p.PlayerId != player.Id)
                    r.Add(AiDataIssueCode.ProfilePlayerIdMismatch, who, "The profile belongs to '" + p.PlayerId + "' but was checked against player '" + player.Id + "'.");
                bool keeper = player.Role == PlayerRole.Goalkeeper;
                bool usesGoal = p.PrimaryZone == PitchZone.Goal || (p.SecondaryZones != null && p.SecondaryZones.Contains(PitchZone.Goal));
                if (keeper && p.PrimaryZone != PitchZone.Goal)
                    r.Add(AiDataIssueCode.ProfileGoalkeeperMismatch, who, "The goalkeeper's primary zone must be Goal.");
                if (!keeper && usesGoal)
                    r.Add(AiDataIssueCode.ProfileGoalkeeperMismatch, who, "Only the goalkeeper can use the Goal zone.");
            }
            return r;
        }

        private static void Behaviour(AiDataValidationResult r, string who, string name, int value)
        {
            if (value < 0 || value > 100)
                r.Add(AiDataIssueCode.ProfileBehaviourOutOfRange, who, name + " = " + value + " is outside 0..100.");
        }
    }

    /// <summary>How well a player fits a zone and how well the AI executes a role there.</summary>
    [Serializable]
    public class ZoneFitnessTuning
    {
        public float PrimaryFitness = 1.0f;
        public float SecondaryFitness = 0.75f;
        public float OtherFitness = 0.40f;
    }

    public static class RoleExecutionModel
    {
        public static float Fitness(PlayerPlayingProfile profile, PitchZone zone, ZoneFitnessTuning t)
        {
            if (zone == profile.PrimaryZone) return t.PrimaryFitness;
            if (profile.SecondaryZones != null && profile.SecondaryZones.Contains(zone)) return t.SecondaryFitness;
            return t.OtherFitness;
        }

        /// <summary>
        /// 0..1: how well the AI plays this player's role in this zone. In the player's primary zone it is the level's
        /// role discipline. Outside it, the shortfall in fitness costs less on levels with higher role adaptability.
        /// The profile (zones, archetypes) is only READ: difficulty changes how well the role is played, never the role.
        /// </summary>
        public static float Quality(DifficultyDefinition d, PlayerPlayingProfile profile, PitchZone zone, ZoneFitnessTuning t)
        {
            float shortfall = 1f - MathUtil.Clamp01(Fitness(profile, zone, t));
            float penalty = shortfall * (1f - d.Positioning.RoleAdaptability);
            return MathUtil.Clamp01(d.Decision.RoleDiscipline * (1f - penalty));
        }
    }
}
