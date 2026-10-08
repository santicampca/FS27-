namespace FS27.Core
{
    public sealed partial class DefenseTuning
    {
        /// <summary>A copy that can be changed without changing this one.</summary>
        public DefenseTuning Clone() { return (DefenseTuning)MemberwiseClone(); }

        static partial void ApplyModeValues(DefenseTuning t, GameMode mode)
        {
            if (mode == GameMode.Classic) t.ChallengeMaxRisk = DefenseModeDefaults.ClassicChallengeMaxRisk;
        }
    }
}
