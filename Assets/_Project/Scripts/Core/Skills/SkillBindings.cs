using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>How far a catalogue skill is real in the Core (Phase 6). Numbers are explicit and append-only.</summary>
    public enum SkillImplementation
    {
        /// <summary>The skill changes how an action resolves (it has an <see cref="SkillEffect"/>).</summary>
        Real = 0,
        /// <summary>The skill only grants a permission (it unlocks an action that resolves the same for everybody). No catalogue skill is in this state: every unlocking skill changes its action.</summary>
        PermissionOnly = 1,
        /// <summary>The skill depends on something a later phase builds (see <see cref="SkillBinding.Reason"/>). It is owned, listed and ignored by the resolver: nothing is faked.</summary>
        Deferred = 2
    }

    /// <summary>
    /// What a real skill does, as a closed list the resolver understands. One effect per real skill. Numbers are explicit and append-only (they index <see cref="ResolvedSkills"/>).
    /// </summary>
    public enum SkillEffect
    {
        None = 0,
        CurvedShot = 1,
        CloseControlMove = 2,
        ElusiveMove = 3,
        FlairMove = 4,
        SwitchLob = 5,
        CreativeLob = 6,
        ProgressivePass = 7,
        LineBreakingPass = 8,
        BuildUpPass = 9,
        ClinicalFinish = 10,
        FirstTimeFinish = 11,
        LongRangeStrike = 12,
        PocketReceive = 13,
        HoldUp = 14,
        ExplosiveBurst = 15,
        TurnAndAccelerate = 16,
        ProgressiveCarry = 17,
        PressResistance = 18,
        InterceptionReading = 19,
        AggressiveTackle = 20,
        OneVOneDefending = 21,
        DefensiveBlock = 22,
        DefensiveScreen = 23,
        HighPress = 24,
        BallRecovery = 25
    }

    /// <summary>One attribute and its share of a skill's mastery (the shares of a skill add up to 1).</summary>
    public struct SkillWeight
    {
        public PlayerAttributeId Attribute;
        public float Weight;
        public SkillWeight(PlayerAttributeId attribute, float weight) { Attribute = attribute; Weight = weight; }
    }

    /// <summary>
    /// How the Core treats one catalogue skill: its implementation state, the effect it has, the actions it touches, what mastery of it is made of (attributes only) and,
    /// for a deferred skill, why. The CATALOGUE (id, requirements, roles, unlock) stays in the data file; this is the behaviour table that goes with it, and a test proves that
    /// every catalogue skill has exactly one row here.
    /// </summary>
    public sealed class SkillBinding
    {
        public string SkillId = "";
        public SkillImplementation Implementation;
        public SkillEffect Effect;
        public FootballActionKind[] Actions = new FootballActionKind[0];
        public SkillWeight[] Mastery = new SkillWeight[0];
        public string Reason = "";
    }

    public static class SkillBindings
    {
        public const int EffectCount = 26;

        private static readonly List<SkillBinding> all = Build();
        private static readonly Dictionary<string, SkillBinding> byId = Index(all);

        public static IReadOnlyList<SkillBinding> All => all;
        public static bool TryGet(string skillId, out SkillBinding binding) { binding = null; return skillId != null && byId.TryGetValue(skillId, out binding); }

        /// <summary>The effects whose skill unlocks this action (the single list the permission check reads).</summary>
        public static IReadOnlyList<SkillEffect> EffectsUnlocking(FootballActionKind action)
        {
            switch (action)
            {
                case FootballActionKind.CurvedShot: return CurveEffects;
                case FootballActionKind.SkillMove: return MoveEffects;
                case FootballActionKind.SkillLobPass: return LobEffects;
                default: return NoEffects;
            }
        }

        public static bool IsSkillAction(FootballActionKind action)
        {
            return action == FootballActionKind.CurvedShot || action == FootballActionKind.SkillMove || action == FootballActionKind.SkillLobPass;
        }

        private static readonly SkillEffect[] NoEffects = new SkillEffect[0];
        private static readonly SkillEffect[] CurveEffects = { SkillEffect.CurvedShot };
        private static readonly SkillEffect[] MoveEffects = { SkillEffect.CloseControlMove, SkillEffect.ElusiveMove, SkillEffect.FlairMove };
        private static readonly SkillEffect[] LobEffects = { SkillEffect.SwitchLob, SkillEffect.CreativeLob };

        /// <summary>The skill id behind an effect (the reverse of the table), or null.</summary>
        public static string SkillIdOf(SkillEffect effect)
        {
            for (int i = 0; i < all.Count; i++) if (all[i].Effect == effect && all[i].Implementation == SkillImplementation.Real) return all[i].SkillId;
            return null;
        }

        public static bool TryGetByEffect(SkillEffect effect, out SkillBinding binding)
        {
            binding = null;
            for (int i = 0; i < all.Count; i++) if (all[i].Effect == effect && all[i].Implementation == SkillImplementation.Real) { binding = all[i]; return true; }
            return false;
        }

        // ------------------------------------------------------------------ the table

        private static SkillWeight W(PlayerAttributeId a, float w) { return new SkillWeight(a, w); }
        private static FootballActionKind[] A(params FootballActionKind[] k) { return k; }

        private static SkillBinding Real(string id, SkillEffect effect, FootballActionKind[] actions, params SkillWeight[] mastery)
        {
            return new SkillBinding { SkillId = id, Implementation = SkillImplementation.Real, Effect = effect, Actions = actions, Mastery = mastery };
        }

        private static SkillBinding Deferred(string id, string reason)
        {
            return new SkillBinding { SkillId = id, Implementation = SkillImplementation.Deferred, Effect = SkillEffect.None, Reason = reason };
        }

        private static List<SkillBinding> Build()
        {
            const PlayerAttributeId Spd = PlayerAttributeId.Speed, Acc = PlayerAttributeId.Acceleration, Agi = PlayerAttributeId.Agility, Str = PlayerAttributeId.Strength, Sta = PlayerAttributeId.Stamina;
            const PlayerAttributeId Sho = PlayerAttributeId.Shooting, Fin = PlayerAttributeId.Finishing, Pas = PlayerAttributeId.Passing, Con = PlayerAttributeId.Control, Dri = PlayerAttributeId.Dribbling;
            const PlayerAttributeId Tec = PlayerAttributeId.Technique, Def = PlayerAttributeId.Defense;
            const string offBall = "Off-ball movement is decided by the AI, which does not exist yet: the Core has no player-controlled run for this skill to modify.";
            const string transition = "Needs the possession-transition context (a team that has just won or lost the ball), which belongs to the later transitions phase.";
            return new List<SkillBinding>
            {
                Real("close_control_dribble", SkillEffect.CloseControlMove, A(FootballActionKind.SkillMove), W(Con, 0.35f), W(Dri, 0.30f), W(Agi, 0.20f), W(Tec, 0.15f)),
                Real("elusive_dribble", SkillEffect.ElusiveMove, A(FootballActionKind.SkillMove), W(Agi, 0.35f), W(Dri, 0.30f), W(Spd, 0.20f), W(Con, 0.15f)),
                Real("flair_dribble", SkillEffect.FlairMove, A(FootballActionKind.SkillMove), W(Tec, 0.35f), W(Dri, 0.30f), W(Con, 0.20f), W(Agi, 0.15f)),
                Real("progressive_pass", SkillEffect.ProgressivePass, A(FootballActionKind.ShortPass, FootballActionKind.LongPass, FootballActionKind.ThroughPass), W(Pas, 0.70f), W(Tec, 0.15f), W(Con, 0.15f)),
                Real("line_breaking_pass", SkillEffect.LineBreakingPass, A(FootballActionKind.ThroughPass), W(Pas, 0.60f), W(Tec, 0.25f), W(Con, 0.15f)),
                Real("long_switch", SkillEffect.SwitchLob, A(FootballActionKind.SkillLobPass), W(Pas, 0.55f), W(Tec, 0.25f), W(Con, 0.10f), W(Str, 0.10f)),
                Real("creative_pass", SkillEffect.CreativeLob, A(FootballActionKind.SkillLobPass), W(Pas, 0.45f), W(Tec, 0.35f), W(Con, 0.20f)),
                Real("build_up_play", SkillEffect.BuildUpPass, A(FootballActionKind.ShortPass, FootballActionKind.WallPass), W(Pas, 0.55f), W(Con, 0.35f), W(Tec, 0.10f)),
                Deferred("precision_cross", "Crossing is not resolved by the Core yet (FootballActionKind.Cross is NotSupported since Phase 4)."),
                Real("clinical_finishing", SkillEffect.ClinicalFinish, A(FootballActionKind.Shot, FootballActionKind.CurvedShot), W(Fin, 0.70f), W(Tec, 0.15f), W(Sho, 0.15f)),
                Real("first_time_finish", SkillEffect.FirstTimeFinish, A(FootballActionKind.Shot, FootballActionKind.CurvedShot), W(Fin, 0.55f), W(Tec, 0.25f), W(Con, 0.20f)),
                Real("long_range_strike", SkillEffect.LongRangeStrike, A(FootballActionKind.Shot, FootballActionKind.CurvedShot), W(Sho, 0.70f), W(Tec, 0.20f), W(Str, 0.10f)),
                Real("curve_shot", SkillEffect.CurvedShot, A(FootballActionKind.CurvedShot), W(Tec, 0.40f), W(Sho, 0.25f), W(Fin, 0.20f), W(Con, 0.15f)),
                Deferred("header_finish", "Headers are NotSupported by the Core: aerial play is a later phase."),
                Deferred("dead_ball_specialist", "Set pieces (free kicks, corners) do not exist in the Core yet: restarts are a later phase."),
                Real("pocket_receive", SkillEffect.PocketReceive, A(FootballActionKind.Control), W(Con, 0.50f), W(Tec, 0.30f), W(Agi, 0.20f)),
                Real("hold_up_play", SkillEffect.HoldUp, A(FootballActionKind.Shield, FootballActionKind.Tackle, FootballActionKind.PhysicalChallenge), W(Str, 0.50f), W(Con, 0.35f), W(Tec, 0.15f)),
                Real("explosive_burst", SkillEffect.ExplosiveBurst, A(), W(Acc, 0.60f), W(Agi, 0.20f), W(Spd, 0.20f)),
                Deferred("counter_sprint", transition),
                Real("turn_and_accelerate", SkillEffect.TurnAndAccelerate, A(), W(Agi, 0.55f), W(Acc, 0.45f)),
                Real("progressive_carry", SkillEffect.ProgressiveCarry, A(FootballActionKind.Dribble), W(Dri, 0.45f), W(Con, 0.30f), W(Spd, 0.25f)),
                Real("press_resistance", SkillEffect.PressResistance, A(FootballActionKind.Dribble), W(Con, 0.40f), W(Agi, 0.25f), W(Str, 0.20f), W(Dri, 0.15f)),
                Deferred("cut_inside", "Needs the tactical lane context (what is 'inside' relative to the touchline and the play), which is positioning AI."),
                Deferred("support_run", offBall),
                Deferred("overlapping_run", offBall),
                Deferred("half_space_movement", offBall),
                Deferred("late_box_arrival", offBall),
                Real("interception_reading", SkillEffect.InterceptionReading, A(FootballActionKind.Intercept, FootballActionKind.Block), W(Def, 0.55f), W(Agi, 0.20f), W(Spd, 0.15f), W(Con, 0.10f)),
                Real("aggressive_tackle", SkillEffect.AggressiveTackle, A(FootballActionKind.Tackle, FootballActionKind.SlidingTackle), W(Def, 0.55f), W(Str, 0.30f), W(Spd, 0.15f)),
                Real("one_v_one_defending", SkillEffect.OneVOneDefending, A(FootballActionKind.Tackle), W(Def, 0.50f), W(Agi, 0.35f), W(Spd, 0.15f)),
                Real("defensive_block", SkillEffect.DefensiveBlock, A(FootballActionKind.Block), W(Def, 0.60f), W(Str, 0.20f), W(Agi, 0.20f)),
                Real("defensive_screen", SkillEffect.DefensiveScreen, A(FootballActionKind.Pressure), W(Def, 0.50f), W(Str, 0.30f), W(Agi, 0.20f)),
                Real("high_press", SkillEffect.HighPress, A(FootballActionKind.Pressure), W(Sta, 0.50f), W(Def, 0.30f), W(Spd, 0.20f)),
                Deferred("counter_press", transition),
                Real("ball_recovery", SkillEffect.BallRecovery, A(FootballActionKind.Control), W(Def, 0.40f), W(Sta, 0.25f), W(Agi, 0.20f), W(Spd, 0.15f)),
                Deferred("recovery_sprint", "Needs the defensive recovery context (running back to the goal side), which is defensive AI."),
                Deferred("aerial_duel", "Aerial duels need headers and ball-in-the-air contests, which the Core does not resolve yet (Header is NotSupported).")
            };
        }

        private static Dictionary<string, SkillBinding> Index(List<SkillBinding> list)
        {
            var d = new Dictionary<string, SkillBinding>();
            foreach (SkillBinding b in list) d.Add(b.SkillId, b);
            return d;
        }
    }
}
