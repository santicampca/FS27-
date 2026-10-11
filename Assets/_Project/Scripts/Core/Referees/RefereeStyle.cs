namespace FS27.Core
{
    /// <summary>
    /// How a referee judges (0..1 each). A strict referee (<see cref="Strictness"/> high) calls a foul for less and a card-happy one (<see cref="Cards"/> high) shows the yellow for less. Built from the descriptive
    /// <see cref="RefereeProfile"/> of the data (the other values of the profile are for later phases); <see cref="Neutral"/> is a referee with no profile.
    /// </summary>
    public struct RefereeStyle
    {
        public float Strictness;
        public float Cards;

        public static RefereeStyle Neutral { get { return new RefereeStyle { Strictness = RefereeDefaults.NeutralStyle, Cards = RefereeDefaults.NeutralStyle }; } }

        public static RefereeStyle From(RefereeProfile profile)
        {
            if (profile == null) return Neutral;
            return new RefereeStyle { Strictness = MathUtil.Clamp01(profile.Strictness / RefereeDefaults.ProfileScale), Cards = MathUtil.Clamp01(profile.Cards / RefereeDefaults.ProfileScale) };
        }
    }
}
