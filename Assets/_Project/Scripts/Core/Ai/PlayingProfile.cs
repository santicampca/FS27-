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
    /// This is a list that can grow; the AI interprets these tags, the difficulty never changes them.
    /// </summary>
    public enum PlayerArchetype
    {
        Explosive = 0,
        Creator = 1,
        Finisher = 2,
        Destroyer = 3,
        Anchor = 4,
        Engine = 5,
        ShotStopper = 6,
        Sweeper = 7
    }

    /// <summary>
    /// A player is not "a position". A primary zone, optional secondary zones and up to three archetypes describe how
    /// versatile they are. It lives apart from <see cref="PlayerDefinition"/> (which is not modified), linked by player id.
    /// </summary>
    [Serializable]
    public class PlayerPlayingProfile
    {
        public const int MaxArchetypes = 3;

        public string PlayerId;
        public PitchZone PrimaryZone;
        public List<PitchZone> SecondaryZones = new List<PitchZone>();
        public List<PlayerArchetype> Archetypes = new List<PlayerArchetype>();

        public PlayerPlayingProfile()
        {
        }

        public PlayerPlayingProfile(string playerId, PitchZone primary, PitchZone[] secondary, params PlayerArchetype[] archetypes)
        {
            PlayerId = playerId;
            PrimaryZone = primary;
            SecondaryZones = new List<PitchZone>(secondary ?? new PitchZone[0]);
            Archetypes = new List<PlayerArchetype>(archetypes);
        }

        public bool HasArchetype(PlayerArchetype a)
        {
            return Archetypes != null && Archetypes.Contains(a);
        }

        public bool PlaysIn(PitchZone zone)
        {
            return zone == PrimaryZone || (SecondaryZones != null && SecondaryZones.Contains(zone));
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
                case PlayerRole.Goalkeeper: return PlayerArchetype.ShotStopper;
                case PlayerRole.Defender: return PlayerArchetype.Destroyer;
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

            int count = p.Archetypes == null ? 0 : p.Archetypes.Count;
            if (count < 1 || count > PlayerPlayingProfile.MaxArchetypes)
                r.Add(AiDataIssueCode.ProfileArchetypeCountInvalid, who, "A player needs 1-" + PlayerPlayingProfile.MaxArchetypes + " archetypes, found " + count + ".");
            if (p.Archetypes != null)
            {
                var seen = new HashSet<PlayerArchetype>();
                foreach (PlayerArchetype a in p.Archetypes)
                {
                    if (!Enum.IsDefined(typeof(PlayerArchetype), a)) r.Add(AiDataIssueCode.ProfileArchetypeInvalid, who, "Archetype " + (int)a + " is not valid.");
                    else if (!seen.Add(a)) r.Add(AiDataIssueCode.ProfileArchetypeDuplicate, who, "Archetype " + a + " is listed twice.");
                }
            }

            if (player != null)
            {
                bool keeper = player.Role == PlayerRole.Goalkeeper;
                bool usesGoal = p.PrimaryZone == PitchZone.Goal || (p.SecondaryZones != null && p.SecondaryZones.Contains(PitchZone.Goal));
                if (keeper && p.PrimaryZone != PitchZone.Goal)
                    r.Add(AiDataIssueCode.ProfileGoalkeeperMismatch, who, "The goalkeeper's primary zone must be Goal.");
                if (!keeper && usesGoal)
                    r.Add(AiDataIssueCode.ProfileGoalkeeperMismatch, who, "Only the goalkeeper can use the Goal zone.");
            }
            return r;
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
