using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>How the player's profile declares a zone.</summary>
    public enum ZoneDeclaration
    {
        None = 0,
        Secondary = 1,
        Primary = 2
    }

    /// <summary>One entry of a card's polyvalence panel.</summary>
    public struct ZoneSuitability
    {
        public PitchZone Zone;
        /// <summary>0..100.</summary>
        public int Suitability;
        public ZoneDeclaration Declaration;
    }

    /// <summary>
    /// What a player card shows, DERIVED from the player. It keeps REFERENCES to the player's definition and playing profile, never
    /// copies of any number: every attribute, rating or name is read from the source at the moment it is asked, so if an attribute
    /// changes from 82 to 86 the card reports 86 at once. Its only own datum is the <see cref="CardType"/>, a presentation label.
    ///
    /// A card is a representation, not gameplay: it has no setters, nothing in gameplay reads it, and the flow is one-way:
    /// PlayerDefinition / PlayerPlayingProfile -> PlayerCardData -> card view (UI, later).
    /// </summary>
    public sealed class PlayerCardData
    {
        private readonly PlayerDefinition player;
        private readonly PlayerPlayingProfile profile;
        private readonly TeamDefinition team;
        private readonly PlayerRatingCalculator calculator;
        private readonly GoalkeeperProfile goalkeeperProfile;
        private readonly GoalkeeperRatingCalculator goalkeeperCalculator;

        public CardType CardType { get; }

        public PlayerCardData(PlayerDefinition player, PlayerPlayingProfile profile, TeamDefinition team, CardType cardType, PlayerRatingCalculator calculator)
            : this(player, profile, team, cardType, calculator, null, null)
        {
        }

        /// <param name="goalkeeperProfile">The player's goalkeeper profile, or null for a field player. Only a reference: the card reads from it, never copies.</param>
        /// <param name="goalkeeperCalculator">Needed to show the goalkeeper rating; defaults to the standard weights when a goalkeeper profile is given.</param>
        public PlayerCardData(PlayerDefinition player, PlayerPlayingProfile profile, TeamDefinition team, CardType cardType, PlayerRatingCalculator calculator,
                              GoalkeeperProfile goalkeeperProfile, GoalkeeperRatingCalculator goalkeeperCalculator)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            this.profile = profile;
            this.team = team;
            this.goalkeeperProfile = goalkeeperProfile;
            this.goalkeeperCalculator = goalkeeperProfile == null ? null : (goalkeeperCalculator ?? GoalkeeperRatingCalculator.CreateDefault());
            CardType = cardType;
        }

        // The profile in use: the explicit one, or one derived from the coarse role if the player has none.
        private PlayerPlayingProfile Profile => profile ?? PlayingProfileDefaults.FromPlayer(player);

        // ---- identity and basic data (read through) ----
        public string PlayerId => player.Id;
        public string DisplayName => player.Name;
        public string ShortName => player.ResolveShortName();
        public int ShirtNumber => player.Number;
        public string NationalityCode => player.NationalityCode;
        public string TeamId => player.TeamId;
        /// <summary>The team's name when a team was supplied; empty otherwise.</summary>
        public string TeamName => team != null ? team.Name : "";
        public int Age => player.Age;
        public int HeightCm => player.HeightCm;
        public int WeightKg => player.WeightKg;
        public BodyType BodyType => player.BodyType;
        public PreferredFoot PreferredFoot => player.PreferredFoot;
        public int WeakFootQuality => player.WeakFootQuality;

        // ---- ratings (computed on request) ----
        public int Overall => calculator.GetOverall(player, Profile);

        // ---- goalkeeper data (only for a goalkeeper; read through from its GoalkeeperProfile) ----
        /// <summary>True if this card is a goalkeeper's (the player has a goalkeeper profile).</summary>
        public bool IsGoalkeeper => goalkeeperProfile != null;

        /// <summary>The goalkeeper rating (1..99), separate from <see cref="Overall"/>. 0 for a field player.</summary>
        public int GoalkeeperRating => goalkeeperProfile == null ? 0 : goalkeeperCalculator.GetRating(player, goalkeeperProfile);

        /// <summary>The number the card headlines: the goalkeeper rating for a goalkeeper, the Overall for everyone else.</summary>
        public int HeadlineRating => IsGoalkeeper ? GoalkeeperRating : Overall;

        /// <summary>The keeper's current value of one goalkeeper capability. 0 for a field player.</summary>
        public int GetGoalkeeperCapability(GoalkeeperCapability capability)
        {
            return goalkeeperProfile == null ? 0 : goalkeeperProfile.GetValue(capability);
        }

        /// <summary>The goalkeeper's primary style. False for a field player.</summary>
        public bool TryGetGoalkeeperStyle(out GoalkeeperStyle style)
        {
            style = default;
            if (goalkeeperProfile == null) return false;
            style = goalkeeperProfile.PrimaryStyle;
            return true;
        }

        /// <summary>The goalkeeper's secondary styles (empty for a field player).</summary>
        public IReadOnlyList<GoalkeeperStyle> GoalkeeperSecondaryStyles
        {
            get { return goalkeeperProfile != null ? (IReadOnlyList<GoalkeeperStyle>)goalkeeperProfile.SecondaryStyles : new GoalkeeperStyle[0]; }
        }

        // ---- attributes (always the player's current ones) ----
        /// <summary>The player's current value of one of the 12 core attributes.</summary>
        public int GetAttribute(PlayerAttributeId id)
        {
            return player.Attributes.GetValue(id);
        }

        /// <summary>The player's current attributes (a copy made at this moment, never stored by the card).</summary>
        public PlayerAttributes Attributes => player.Attributes;

        // ---- zones, roles, polyvalence ----
        public PitchZone PrimaryZone => Profile.PrimaryZone;
        public IReadOnlyList<PitchZone> SecondaryZones => Profile.SecondaryZones;

        /// <summary>The role with the highest affinity. False only if the player has no role at all.</summary>
        public bool TryGetPrimaryRole(out PlayerArchetype role)
        {
            return Profile.TryGetPrimaryRole(out role);
        }

        /// <summary>The player's roles with their affinity, most defining first.</summary>
        public RoleAffinity[] GetRoles()
        {
            return Profile.GetRoleAffinities();
        }

        /// <summary>The player's style label for display: the Spanish name of the primary role (empty if none).</summary>
        public string StyleLabel
        {
            get { return TryGetPrimaryRole(out PlayerArchetype role) ? RoleInfo.SpanishName(role) : ""; }
        }

        public int GetZoneSuitability(PitchZone zone)
        {
            return calculator.GetZoneSuitability(player, Profile, zone);
        }

        public int GetRoleSuitability(PlayerArchetype role)
        {
            return calculator.GetRoleSuitability(player, Profile, role);
        }

        /// <summary>Suitability in every zone (the polyvalence panel), best first.</summary>
        public ZoneSuitability[] GetPolyvalence()
        {
            PlayerPlayingProfile p = Profile;
            var zones = (PitchZone[])Enum.GetValues(typeof(PitchZone));
            var result = new ZoneSuitability[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                result[i] = new ZoneSuitability
                {
                    Zone = zones[i],
                    Suitability = calculator.GetZoneSuitability(player, p, zones[i]),
                    Declaration = zones[i] == p.PrimaryZone ? ZoneDeclaration.Primary : (p.PlaysIn(zones[i]) ? ZoneDeclaration.Secondary : ZoneDeclaration.None)
                };
            }
            Array.Sort(result, (a, b) => b.Suitability.CompareTo(a.Suitability));
            return result;
        }
    }

    /// <summary>Which card type each player has (presentation only). Players with no entry are Standard.</summary>
    public sealed class PlayerCardCatalog
    {
        private readonly Dictionary<string, CardType> types = new Dictionary<string, CardType>();

        public int Count => types.Count;

        /// <summary>Sets (or changes) a player's card type. Returns false if the id or the type is invalid.</summary>
        public bool Set(string playerId, CardType type)
        {
            if (!DataRules.IsValidId(playerId) || !Enum.IsDefined(typeof(CardType), type)) return false;
            types[playerId] = type;
            return true;
        }

        public CardType Get(string playerId)
        {
            return playerId != null && types.TryGetValue(playerId, out CardType t) ? t : CardType.Standard;
        }
    }

    /// <summary>
    /// Builds cards from the player data. It gathers the pieces (profile, card type, team) and hands over references;
    /// it never copies attributes. Card type and team are optional.
    /// </summary>
    public sealed class PlayerCardBuilder
    {
        private readonly PlayerRatingCalculator calculator;
        private readonly PlayingProfileCatalog profiles;
        private readonly PlayerCardCatalog cardTypes;
        private readonly Func<string, TeamDefinition> teamLookup;
        private readonly IPlayerLookup players;
        private readonly GoalkeeperRatingCalculator goalkeeperCalculator;

        /// <param name="players">Where goalkeeper profiles are found (normally the <see cref="PlayerLibrary"/>). Optional: without it every card is a field player's.</param>
        /// <param name="goalkeeperCalculator">The goalkeeper rating weights; the defaults if omitted.</param>
        public PlayerCardBuilder(PlayerRatingCalculator calculator, PlayingProfileCatalog profiles = null, PlayerCardCatalog cardTypes = null,
                                 Func<string, TeamDefinition> teamLookup = null, IPlayerLookup players = null, GoalkeeperRatingCalculator goalkeeperCalculator = null)
        {
            this.players = players;
            this.goalkeeperCalculator = goalkeeperCalculator;
            this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            this.profiles = profiles;
            this.cardTypes = cardTypes;
            this.teamLookup = teamLookup;
        }

        public PlayerCardData Build(PlayerDefinition player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            PlayerPlayingProfile profile = null;
            if (profiles != null) profiles.TryGet(player.Id, out profile);
            TeamDefinition team = teamLookup != null && !string.IsNullOrEmpty(player.TeamId) ? teamLookup(player.TeamId) : null;
            CardType type = cardTypes != null ? cardTypes.Get(player.Id) : CardType.Standard;
            GoalkeeperProfile keeperProfile = null;
            if (players != null) players.TryGetGoalkeeperProfile(player.Id, out keeperProfile);
            return new PlayerCardData(player, profile, team, type, calculator, keeperProfile, goalkeeperCalculator);
        }
    }
}
