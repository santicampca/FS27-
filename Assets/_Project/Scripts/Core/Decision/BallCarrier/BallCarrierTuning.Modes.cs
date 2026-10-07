namespace FS27.Core
{
    public sealed partial class BallCarrierTuning
    {
        static partial void ApplyModeValues(BallCarrierTuning t, GameMode mode)
        {
            if (mode == GameMode.Arena)
            {
                t.SafePassBaseWeight = BallCarrierModeDefaults.ArenaSafePassBaseWeight;
            }
            else
            {
                t.DribbleSpaceHorizon = BallCarrierModeDefaults.ClassicDribbleSpaceHorizon;
                t.SafePassBaseWeight = BallCarrierModeDefaults.ClassicSafePassBaseWeight;
                t.ShootMinScore = BallCarrierModeDefaults.ClassicShootMinScore;
            }
        }
    }
}
