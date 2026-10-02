using System;

namespace FS27.Core
{
    public enum AssistLevel
    {
        High = 0,
        Medium = 1,
        Low = 2,
        Manual = 3
    }

    /// <summary>
    /// How much help the HUMAN player gets. Entirely separate from AI difficulty: a difficulty may RECOMMEND
    /// values, but the player can set each one freely. Nothing here changes how the AI plays.
    /// </summary>
    [Serializable]
    public class PlayerAssistSettings
    {
        public AssistLevel PassAssist = AssistLevel.Medium;
        public AssistLevel ShotAssist = AssistLevel.Medium;
        public AssistLevel PlayerSwitchAssist = AssistLevel.Medium;

        public PlayerAssistSettings()
        {
        }

        public PlayerAssistSettings(AssistLevel pass, AssistLevel shot, AssistLevel playerSwitch)
        {
            PassAssist = pass;
            ShotAssist = shot;
            PlayerSwitchAssist = playerSwitch;
        }

        public PlayerAssistSettings Clone()
        {
            return new PlayerAssistSettings(PassAssist, ShotAssist, PlayerSwitchAssist);
        }
    }

    /// <summary>Values the player chose explicitly. Anything left null falls back to the difficulty's recommendation.</summary>
    public class PlayerAssistOverrides
    {
        public AssistLevel? PassAssist;
        public AssistLevel? ShotAssist;
        public AssistLevel? PlayerSwitchAssist;
    }

    /// <summary>What each assist level means in numbers (hooks for the future pass/shot/switch systems; unused today).</summary>
    [Serializable]
    public class AssistTuning
    {
        /// <summary>Indexed by <see cref="AssistLevel"/>: how far (degrees) a pass may be bent toward a teammate.</summary>
        public float[] PassAimSnapDegrees = { 25f, 15f, 7f, 0f };
        /// <summary>How far (degrees) a shot may be corrected toward the goal.</summary>
        public float[] ShotAimCorrectionDegrees = { 12f, 8f, 4f, 0f };
        /// <summary>Radius (m) in which the game may pick the player to control automatically.</summary>
        public float[] SwitchSearchRadiusMeters = { 10f, 7f, 4f, 0f };
    }

    public struct AssistParameters
    {
        public float PassAimSnapDegrees;
        public float ShotAimCorrectionDegrees;
        public float SwitchSearchRadiusMeters;
        public bool AutoSwitch;
    }

    public static class AssistResolver
    {
        /// <summary>The player's final settings: their own choice where given, otherwise the difficulty's recommendation.</summary>
        public static PlayerAssistSettings Resolve(DifficultyDefinition difficulty, PlayerAssistOverrides overrides)
        {
            PlayerAssistSettings rec = difficulty != null ? difficulty.RecommendedAssists : new PlayerAssistSettings();
            if (overrides == null) return rec.Clone();
            return new PlayerAssistSettings(
                overrides.PassAssist ?? rec.PassAssist,
                overrides.ShotAssist ?? rec.ShotAssist,
                overrides.PlayerSwitchAssist ?? rec.PlayerSwitchAssist);
        }

        public static AssistParameters ToParameters(PlayerAssistSettings settings, AssistTuning tuning)
        {
            return new AssistParameters
            {
                PassAimSnapDegrees = tuning.PassAimSnapDegrees[(int)settings.PassAssist],
                ShotAimCorrectionDegrees = tuning.ShotAimCorrectionDegrees[(int)settings.ShotAssist],
                SwitchSearchRadiusMeters = tuning.SwitchSearchRadiusMeters[(int)settings.PlayerSwitchAssist],
                AutoSwitch = settings.PlayerSwitchAssist != AssistLevel.Manual
            };
        }
    }
}
