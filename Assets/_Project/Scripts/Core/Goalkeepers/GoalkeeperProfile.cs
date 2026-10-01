using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The 8 capabilities that only a goalkeeper has. They are NOT general player attributes: a field player simply has no
    /// <see cref="GoalkeeperProfile"/>. Scale 1..99, like the core attributes.
    /// </summary>
    public enum GoalkeeperCapability
    {
        /// <summary>How quickly and sharply the keeper responds to a shot (capacity to respond; not a save percentage).</summary>
        Reflexes = 0,
        /// <summary>Catching, securing and controlling the ball, and limiting rebounds.</summary>
        Handling = 1,
        /// <summary>Being in the right place: angle, distance from goal, reading the play.</summary>
        Positioning = 2,
        /// <summary>Physical and technical capacity for lateral interventions.</summary>
        Diving = 3,
        /// <summary>Long kicking (goal kicks, punts, volleys).</summary>
        Kicking = 4,
        /// <summary>Quality of decisions and delivery when the keeper starts an attack (not the same as Kicking).</summary>
        Distribution = 5,
        /// <summary>Organising and controlling the defensive area (line, pressing, calls, coming out).</summary>
        Command = 6,
        /// <summary>Getting up and getting ready again after an action.</summary>
        Recovery = 7
    }

    /// <summary>
    /// How a goalkeeper plays. This is a goalkeeper-only concept, NOT a <see cref="PlayerArchetype"/> (player roles stay the 12 official ones).
    /// A keeper has one primary style and may have secondary ones.
    /// </summary>
    public enum GoalkeeperStyle
    {
        ShotStopper = 0,
        Distributor = 1,
        Sweeper = 2,
        Commander = 3
    }

    /// <summary>
    /// Where a goalkeeper is acting, as a BEHAVIOUR concept (not a player zone: a keeper's zone stays <see cref="PitchZone.Goal"/>).
    /// Each area asks for a different mix of capabilities; see <see cref="GoalkeeperRatingCalculator.GetAreaRating"/>.
    /// </summary>
    public enum GoalkeeperActionArea
    {
        GoalLine = 0,
        Box = 1,
        Distribution = 2,
        SweeperArea = 3
    }

    public static class GoalkeeperInfo
    {
        public const int CapabilityCount = 8;

        public static readonly GoalkeeperCapability[] Capabilities =
        {
            GoalkeeperCapability.Reflexes, GoalkeeperCapability.Handling, GoalkeeperCapability.Positioning, GoalkeeperCapability.Diving,
            GoalkeeperCapability.Kicking, GoalkeeperCapability.Distribution, GoalkeeperCapability.Command, GoalkeeperCapability.Recovery
        };

        public static readonly GoalkeeperStyle[] Styles =
        {
            GoalkeeperStyle.ShotStopper, GoalkeeperStyle.Distributor, GoalkeeperStyle.Sweeper, GoalkeeperStyle.Commander
        };

        public static readonly GoalkeeperActionArea[] Areas =
        {
            GoalkeeperActionArea.GoalLine, GoalkeeperActionArea.Box, GoalkeeperActionArea.Distribution, GoalkeeperActionArea.SweeperArea
        };

        /// <summary>Spanish display names (UI only; data keeps the enum names).</summary>
        public static string SpanishName(GoalkeeperCapability c)
        {
            switch (c)
            {
                case GoalkeeperCapability.Reflexes: return "Reflejos";
                case GoalkeeperCapability.Handling: return "Manejo";
                case GoalkeeperCapability.Positioning: return "Colocación";
                case GoalkeeperCapability.Diving: return "Estirada";
                case GoalkeeperCapability.Kicking: return "Golpeo";
                case GoalkeeperCapability.Distribution: return "Distribución";
                case GoalkeeperCapability.Command: return "Mando";
                default: return "Recuperación";
            }
        }

        public static string SpanishName(GoalkeeperStyle s)
        {
            switch (s)
            {
                case GoalkeeperStyle.ShotStopper: return "Parador";
                case GoalkeeperStyle.Distributor: return "Distribuidor";
                case GoalkeeperStyle.Sweeper: return "Portero líbero";
                default: return "Comandante";
            }
        }
    }

    /// <summary>
    /// What makes a <see cref="PlayerDefinition"/> a goalkeeper, beyond what every player has: 8 goalkeeper capabilities and a
    /// goalkeeping style. Linked to its player by id (the player stays the single source of identity, team, number, physique, foot
    /// and ALL of the 12 core attributes including Stamina); it holds no copy of any of that. A field player has no profile.
    ///
    /// It is separate from the playing profile: goalkeeper styles are not player roles, and the capabilities do not feed the
    /// player's Overall (see <see cref="GoalkeeperRatingCalculator"/> for the separate goalkeeper rating). There is deliberately
    /// no Reaction here either: reaction time comes from the difficulty, and the keeper's Reflexes only modulate it.
    /// </summary>
    [Serializable]
    public class GoalkeeperProfile
    {
        public string PlayerId;

        public int Reflexes = 70;
        public int Handling = 70;
        public int Positioning = 70;
        public int Diving = 70;
        public int Kicking = 70;
        public int Distribution = 70;
        public int Command = 70;
        public int Recovery = 70;

        public GoalkeeperStyle PrimaryStyle = GoalkeeperStyle.ShotStopper;
        public List<GoalkeeperStyle> SecondaryStyles = new List<GoalkeeperStyle>();

        public GoalkeeperProfile()
        {
        }

        /// <summary>Capabilities in the order of <see cref="GoalkeeperCapability"/>.</summary>
        public GoalkeeperProfile(string playerId, GoalkeeperStyle primaryStyle, int reflexes, int handling, int positioning, int diving,
                                 int kicking, int distribution, int command, int recovery, params GoalkeeperStyle[] secondaryStyles)
        {
            PlayerId = playerId;
            PrimaryStyle = primaryStyle;
            Reflexes = reflexes;
            Handling = handling;
            Positioning = positioning;
            Diving = diving;
            Kicking = kicking;
            Distribution = distribution;
            Command = command;
            Recovery = recovery;
            SecondaryStyles = new List<GoalkeeperStyle>(secondaryStyles ?? new GoalkeeperStyle[0]);
        }

        public int GetValue(GoalkeeperCapability c)
        {
            switch (c)
            {
                case GoalkeeperCapability.Reflexes: return Reflexes;
                case GoalkeeperCapability.Handling: return Handling;
                case GoalkeeperCapability.Positioning: return Positioning;
                case GoalkeeperCapability.Diving: return Diving;
                case GoalkeeperCapability.Kicking: return Kicking;
                case GoalkeeperCapability.Distribution: return Distribution;
                case GoalkeeperCapability.Command: return Command;
                default: return Recovery;
            }
        }

        public void SetValue(GoalkeeperCapability c, int value)
        {
            switch (c)
            {
                case GoalkeeperCapability.Reflexes: Reflexes = value; break;
                case GoalkeeperCapability.Handling: Handling = value; break;
                case GoalkeeperCapability.Positioning: Positioning = value; break;
                case GoalkeeperCapability.Diving: Diving = value; break;
                case GoalkeeperCapability.Kicking: Kicking = value; break;
                case GoalkeeperCapability.Distribution: Distribution = value; break;
                case GoalkeeperCapability.Command: Command = value; break;
                default: Recovery = value; break;
            }
        }

        /// <summary>How the profile declares a style: primary, secondary or not at all.</summary>
        public GoalkeeperStyleDeclaration GetDeclaration(GoalkeeperStyle style)
        {
            if (style == PrimaryStyle) return GoalkeeperStyleDeclaration.Primary;
            return SecondaryStyles != null && SecondaryStyles.Contains(style) ? GoalkeeperStyleDeclaration.Secondary : GoalkeeperStyleDeclaration.None;
        }
    }

    public enum GoalkeeperStyleDeclaration
    {
        None = 0,
        Secondary = 1,
        Primary = 2
    }

    public static class GoalkeeperProfileValidator
    {
        /// <summary>
        /// Checks a goalkeeper profile (and, if the owner is given, that it belongs to a goalkeeper). Reports every problem found.
        /// </summary>
        public static ValidationResult Validate(GoalkeeperProfile p, PlayerDefinition owner = null)
        {
            var r = new ValidationResult();
            if (p == null)
            {
                r.Add(ValidationCode.GoalkeeperProfileNull, "goalkeeper profile", "Goalkeeper profile is null.");
                return r;
            }

            string who = "goalkeeper profile of '" + (p.PlayerId ?? "<no id>") + "'";
            if (!DataRules.IsValidId(p.PlayerId))
                r.Add(ValidationCode.GoalkeeperProfilePlayerIdInvalid, who, "Player id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");

            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                int v = p.GetValue(c);
                if (v < PlayerAttributes.Min || v > PlayerAttributes.Max)
                    r.Add(ValidationCode.GoalkeeperCapabilityOutOfRange, who, c + " = " + v + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
            }

            if (!Enum.IsDefined(typeof(GoalkeeperStyle), p.PrimaryStyle))
                r.Add(ValidationCode.GoalkeeperStyleInvalid, who, "Primary style " + (int)p.PrimaryStyle + " is not a goalkeeper style.");
            if (p.SecondaryStyles != null)
            {
                var seen = new HashSet<GoalkeeperStyle>();
                foreach (GoalkeeperStyle s in p.SecondaryStyles)
                {
                    if (!Enum.IsDefined(typeof(GoalkeeperStyle), s)) r.Add(ValidationCode.GoalkeeperStyleInvalid, who, "Secondary style " + (int)s + " is not a goalkeeper style.");
                    else if (s == p.PrimaryStyle) r.Add(ValidationCode.GoalkeeperStyleDuplicate, who, "Secondary style " + s + " is the primary style.");
                    else if (!seen.Add(s)) r.Add(ValidationCode.GoalkeeperStyleDuplicate, who, "Secondary style " + s + " is listed twice.");
                }
            }

            if (owner != null)
            {
                if (p.PlayerId != null && owner.Id != null && p.PlayerId != owner.Id)
                    r.Add(ValidationCode.GoalkeeperProfilePlayerIdMismatch, who, "The profile belongs to '" + p.PlayerId + "' but was checked against player '" + owner.Id + "'.");
                if (owner.Role != PlayerRole.Goalkeeper)
                    r.Add(ValidationCode.GoalkeeperProfileOnFieldPlayer, who, "Only a goalkeeper can have a goalkeeper profile; this player is not one.");
            }
            return r;
        }
    }
}
