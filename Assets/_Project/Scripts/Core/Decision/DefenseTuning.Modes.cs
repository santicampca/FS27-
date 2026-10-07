namespace FS27.Core
{
    public sealed partial class DefenseTuning
    {
        static partial void ApplyModeValues(DefenseTuning t, GameMode mode)
        {
            if (mode == GameMode.Classic) t.ChallengeMaxRisk = DefenseModeDefaults.ClassicChallengeMaxRisk;
        }
    }
}
