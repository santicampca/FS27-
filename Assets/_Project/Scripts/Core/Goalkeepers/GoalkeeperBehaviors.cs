using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The goalkeeper's signature behaviours as DATA, in the same <see cref="SignatureBehaviorDefinition"/> form the football DNA already uses:
    /// generic, reusable by any goalkeeper, none per player. Every entry is <see cref="BehaviorRuntimeStatus.Planned"/> and
    /// <see cref="BehaviorScope.Goalkeeper"/>: nothing here can be executed yet, and the situations a keeper acts in (a shot coming, a cross,
    /// a back-pass) do not exist in <see cref="BehaviorContext"/> yet, so no context is claimed. Drivers are only listed where a football
    /// tendency really exists (the rest are driven by the keeper's capabilities, which the future goalkeeper AI reads from the profile).
    /// This catalog is NOT merged into the field-player catalog: that needs the goalkeeper contexts first (documented as pending).
    /// </summary>
    public static class GoalkeeperBehaviors
    {
        public const string ReflexSave = "GoalkeeperReflexSave";
        public const string DivingSave = "GoalkeeperDivingSave";
        public const string OneOnOneStopper = "GoalkeeperOneOnOneStopper";
        public const string PenaltyStopper = "GoalkeeperPenaltyStopper";
        public const string CloseShotReaction = "GoalkeeperCloseShotReaction";
        public const string AggressiveClaim = "GoalkeeperAggressiveClaim";
        public const string QuickDistribution = "GoalkeeperQuickDistribution";
        public const string PressureBreakPass = "GoalkeeperPressureBreakPass";
        public const string LongBallSpecialist = "GoalkeeperLongBallSpecialist";
        public const string SweeperRun = "GoalkeeperSweeperRun";
        public const string VocalLeader = "GoalkeeperVocalLeader";
        public const string CommandBackline = "GoalkeeperCommandBackline";

        /// <summary>The names a source uses for a signature action -> the behaviour id. A label that is not here is reported, never guessed.</summary>
        public static IReadOnlyDictionary<string, string> DefaultActionMap()
        {
            var m = new Dictionary<string, string>();
            Map(m, "Reflex Save", ReflexSave); Map(m, "Diving Save", DivingSave); Map(m, "One-on-One Stopper", OneOnOneStopper);
            Map(m, "Penalty Stopper", PenaltyStopper); Map(m, "Close Shot Reaction", CloseShotReaction); Map(m, "Aggressive Claim", AggressiveClaim);
            Map(m, "Quick Distribution", QuickDistribution); Map(m, "Pressure-Break Pass", PressureBreakPass); Map(m, "Long Ball Specialist", LongBallSpecialist);
            Map(m, "Sweeper Run", SweeperRun); Map(m, "Vocal Leader", VocalLeader); Map(m, "Command Backline", CommandBackline);
            return m;
        }

        private static void Map(Dictionary<string, string> m, string label, string id) { m[GoalkeeperRecordImporter.Normalize(label)] = id; }

        public static SignatureBehaviorCatalog CreateCatalog()
        {
            var c = new SignatureBehaviorCatalog();
            Add(c, ReflexSave, BehaviorCategory.Defending, "Saves a shot by pure reaction.", FootballActionKind.GoalkeeperSave, MovementStyle.Burst, 8f, 0.2f, 1f);
            Add(c, DivingSave, BehaviorCategory.Defending, "Dives to reach a shot away from the body.", FootballActionKind.GoalkeeperSave, MovementStyle.Burst, 7f, 0.3f, 1f);
            Add(c, OneOnOneStopper, BehaviorCategory.Defending, "Closes down a lone attacker and makes the save.", FootballActionKind.GoalkeeperSave, MovementStyle.Burst, 8f, 0.4f, 2f);
            Add(c, PenaltyStopper, BehaviorCategory.Defending, "Reads and saves a penalty.", FootballActionKind.GoalkeeperSave, MovementStyle.Default, 9f, 0.3f, 1f);
            Add(c, CloseShotReaction, BehaviorCategory.Defending, "Reacts to a shot from close range.", FootballActionKind.GoalkeeperSave, MovementStyle.Burst, 8f, 0.2f, 1f);
            Add(c, AggressiveClaim, BehaviorCategory.Defending, "Comes out to claim a ball in the air.", FootballActionKind.GoalkeeperCatch, MovementStyle.Run, 6f, 0.5f, 3f,
                W("defending.aggression", 0.5f), W("positioning.boxPresence", 0.5f));
            Add(c, QuickDistribution, BehaviorCategory.Passing, "Restarts play quickly after a save or a claim.", FootballActionKind.GoalkeeperDistribution, MovementStyle.Default, 5f, 0.3f, 2f,
                W("passing.short", 0.5f), W("passing.oneTouch", 0.5f));
            Add(c, PressureBreakPass, BehaviorCategory.Passing, "Finds a teammate to beat the press from the back.", FootballActionKind.GoalkeeperDistribution, MovementStyle.Default, 6f, 0.5f, 3f,
                W("decision.pressureResponse", 0.5f), W("passing.progressive", 0.5f));
            Add(c, LongBallSpecialist, BehaviorCategory.Passing, "Launches a long ball to start a quick attack.", FootballActionKind.GoalkeeperDistribution, MovementStyle.Default, 5f, 0.5f, 4f,
                W("passing.progressive", 0.5f), W("decision.directness", 0.5f));
            Add(c, SweeperRun, BehaviorCategory.Positioning, "Leaves the area to cut out a ball in behind.", FootballActionKind.None, MovementStyle.Run, 6f, 0.6f, 5f,
                W("positioning.depth", 0.5f), W("defending.interception", 0.5f));
            Add(c, VocalLeader, BehaviorCategory.Positioning, "Organises and encourages the defence by voice.", FootballActionKind.None, MovementStyle.Default, 3f, 0.0f, 0f);
            Add(c, CommandBackline, BehaviorCategory.Positioning, "Directs the defensive line's position.", FootballActionKind.None, MovementStyle.Default, 4f, 0.1f, 0f,
                W("positioning.defensive", 1f));
            return c;
        }

        private static WeightedParameter W(string id, float w) { return new WeightedParameter(id, w); }

        private static void Add(SignatureBehaviorCatalog c, string id, BehaviorCategory cat, string text, FootballActionKind action, MovementStyle style,
                                float priority, float risk, float cooldown, params WeightedParameter[] drivers)
        {
            c.TryAdd(new SignatureBehaviorDefinition
            {
                Id = id, Scope = BehaviorScope.Goalkeeper, Category = cat, Description = text, Status = BehaviorRuntimeStatus.Planned,
                Action = action, Style = style, DefaultPriority = priority, DefaultRisk = risk, DefaultCooldownSeconds = cooldown,
                Drivers = new List<WeightedParameter>(drivers)
            });
        }
    }
}
