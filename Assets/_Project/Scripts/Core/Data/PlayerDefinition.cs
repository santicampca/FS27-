using System;

namespace FS27.Core
{
    /// <summary>
    /// Static description of a player: who they are and how good they are. It is the single source of truth for
    /// identity, basic data and attributes (behaviour and polyvalence live in <see cref="PlayerPlayingProfile"/>;
    /// a player card only READS from here). Pure data (fictional or, one day, licensed: the code does not care). Attributes reuse <see cref="PlayerAttributes"/>, so a definition can
    /// be handed straight to <see cref="PlayerStats.Resolve"/>. A Unity ScriptableObject will simply wrap one of these.
    /// </summary>
    [Serializable]
    public class PlayerDefinition
    {
        /// <summary>Stable identifier, unique across the whole game. Never reused, never shown to the player.</summary>
        public string Id;
        /// <summary>The display name (also exposed as <see cref="DisplayName"/>).</summary>
        public string Name;
        /// <summary>The shirt number (also exposed as <see cref="ShirtNumber"/>).</summary>
        public int Number;
        /// <summary>Coarse role (goalkeeper / defender / midfielder / forward), kept for formations and compatibility.
        /// Behaviour is described by zones and roles in <see cref="PlayerPlayingProfile"/>, not by this.</summary>
        public PlayerRole Role;
        /// <summary>The 12 core attributes (+ legacy Reaction), 1..99. See <see cref="PlayerAttributes"/>.</summary>
        public PlayerAttributes Attributes = PlayerAttributes.CreateDefault();

        // ---- Player System V2: identity and basic data (all editable, all optional/defaulted) ----

        /// <summary>Short name for tight spaces (max 12 chars). Empty = derived from <see cref="Name"/>.</summary>
        public string ShortName = "";
        /// <summary>Two or three capital letters, or empty when not set.</summary>
        public string NationalityCode = "";
        /// <summary>Id of the team the player belongs to (a <see cref="TeamDefinition"/>), or empty for a free player.</summary>
        public string TeamId = "";
        public int Age = 22;
        public int HeightCm = 178;
        public int WeightKg = 72;
        public PreferredFoot PreferredFoot = PreferredFoot.Right;
        /// <summary>0 (hopeless) .. 100 (as good as the preferred foot). Future pass/shot/cross systems will read it.</summary>
        public int WeakFootQuality = 50;
        /// <summary>Hint for the future presentation system (which character variant to use).</summary>
        public BodyType BodyType = BodyType.Athletic;

        /// <summary>Same value as <see cref="Name"/> (one storage, two names).</summary>
        public string DisplayName
        {
            get { return Name; }
            set { Name = value; }
        }

        /// <summary>Same value as <see cref="Number"/>.</summary>
        public int ShirtNumber
        {
            get { return Number; }
            set { Number = value; }
        }

        /// <summary>The short name, or one derived from the name (its last word) when none is set.</summary>
        public string ResolveShortName()
        {
            if (!string.IsNullOrWhiteSpace(ShortName)) return ShortName;
            if (string.IsNullOrWhiteSpace(Name)) return "";
            string[] words = Name.Trim().Split(' ');
            string last = words[words.Length - 1];
            return last.Length <= PlayerRules.MaxShortNameLength ? last : last.Substring(0, PlayerRules.MaxShortNameLength);
        }

        public PlayerDefinition()
        {
        }

        public PlayerDefinition(string id, string name, int number, PlayerRole role, PlayerAttributes attributes)
        {
            Id = id;
            Name = name;
            Number = number;
            Role = role;
            Attributes = attributes;
        }
    }
}
