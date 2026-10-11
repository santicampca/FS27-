using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Why an owned skill is not active in a match (it is listed, never silently dropped).</summary>
    public enum SkillIgnoredReason
    {
        UnknownSkill = 0,
        RequirementNotMet = 1,
        Deferred = 2
    }

    public struct IgnoredSkill
    {
        public string SkillId;
        public SkillIgnoredReason Reason;
        public IgnoredSkill(string skillId, SkillIgnoredReason reason) { SkillId = skillId; Reason = reason; }
    }

    /// <summary>The answer of <see cref="SkillResolver.CanUseAction"/>: allowed (with the skill effect that will be used) or refused (with the reason).</summary>
    public struct SkillPermission
    {
        public bool Allowed;
        public ActionRejection Rejection;
        /// <summary>The effect that will shape the action (SkillMove style, lob style, curve), or None for an ordinary action.</summary>
        public SkillEffect Effect;
        /// <summary>The catalogue skill behind that effect, or null.</summary>
        public string SkillId;

        public static SkillPermission Allow(SkillEffect effect = SkillEffect.None, string skillId = null) { return new SkillPermission { Allowed = true, Effect = effect, SkillId = skillId }; }
        public static SkillPermission Refuse(ActionRejection why) { return new SkillPermission { Allowed = false, Rejection = why }; }
    }

    /// <summary>The movement modifiers a player's skills give his locomotion step (all zero = the Phase-3 step, unchanged). They change how he gets going and turns, never his top speed or any attribute.</summary>
    public struct LocomotionModifiers
    {
        /// <summary>Extra acceleration (share) while he is below <see cref="BurstSpeedFraction"/> of his top speed.</summary>
        public float BurstGain;
        public float BurstSpeedFraction;
        /// <summary>Extra acceleration (share) while he is turning by more than <see cref="TurnAngleThreshold"/> (radians).</summary>
        public float TurnGain;
        public float TurnAngleThreshold;
        /// <summary>Share of the speed-dependent turn-rate penalty that is removed.</summary>
        public float TurnPenaltyRelief;
        public bool IsNone { get { return BurstGain == 0f && TurnGain == 0f && TurnPenaltyRelief == 0f; } }
    }

    /// <summary>
    /// The skills of ONE player as they are active in a match: derived once from his skill set, the catalogue and his attributes (never stored on the player). For each effect:
    /// whether he owns it (<see cref="Has"/>) and how masterful he is (<see cref="Strength"/>, 0..1, from his attributes). A skill whose attribute requirement is not met, that is
    /// unknown, or that is deferred, is NOT active and is listed in <see cref="Ignored"/>. A match built without any skill data has <see cref="IsAuthoritative"/> = false: a caller may then
    /// grant a skill action by request (the Phase-4 contract), and the mastery of that action is derived from the attributes alone.
    /// </summary>
    public sealed class ResolvedSkills
    {
        private readonly bool[] owned = new bool[SkillBindings.EffectCount];
        private readonly float[] mastery = new float[SkillBindings.EffectCount];
        private readonly List<string> active = new List<string>();
        private readonly List<IgnoredSkill> ignored = new List<IgnoredSkill>();

        /// <summary>True when the match knew the skills of its players: then the skills are the only permission.</summary>
        public bool IsAuthoritative { get; internal set; }
        public IReadOnlyList<string> ActiveSkillIds => active;
        public IReadOnlyList<IgnoredSkill> Ignored => ignored;

        public bool Has(SkillEffect effect) { return owned[(int)effect]; }
        public bool HasSkill(string skillId) { return active.Contains(skillId); }
        /// <summary>The mastery (0..1) of the effect when he owns it; 0 otherwise.</summary>
        public float Strength(SkillEffect effect) { int i = (int)effect; return owned[i] ? mastery[i] : 0f; }
        /// <summary>The mastery his attributes would give him in the effect, owned or not (used when a caller grants the action).</summary>
        public float Potential(SkillEffect effect) { return mastery[(int)effect]; }
        public bool OwnsAny { get { return active.Count > 0; } }

        public static readonly ResolvedSkills None = new ResolvedSkills();

        internal void SetPotential(SkillEffect effect, float value) { mastery[(int)effect] = value; }
        internal void SetOwned(SkillEffect effect) { owned[(int)effect] = true; }
        internal void AddActive(string id) { active.Add(id); }
        internal void AddIgnored(string id, SkillIgnoredReason why) { ignored.Add(new IgnoredSkill(id, why)); }
    }

    /// <summary>
    /// The single place where skills are resolved (Phase 6): which skills of a player are active, how masterful he is, whether he may perform an action, and the pure formulas the
    /// match engine applies inside an action. No state, no randomness (the action's own seeded generator is the only source of chance), no Unity.
    /// </summary>
    public static class SkillResolver
    {
        /// <summary>Resolves a player's skills for a match. <paramref name="set"/> may be null (he owns none). Deterministic: same inputs, same result.</summary>
        public static ResolvedSkills Resolve(PlayerSkillSet set, SkillCatalog catalog, in PlayerAttributes attributes, bool authoritative = true)
        {
            var r = new ResolvedSkills { IsAuthoritative = authoritative };
            for (int i = 0; i < SkillBindings.All.Count; i++)
            {
                SkillBinding b = SkillBindings.All[i];
                if (b.Implementation == SkillImplementation.Real) r.SetPotential(b.Effect, MasteryOf(b, attributes));
            }
            if (set == null || catalog == null) return r;

            foreach (string id in set.SkillIds)
            {
                if (!catalog.TryGet(id, out SkillDefinition def) || !SkillBindings.TryGet(id, out SkillBinding binding)) { r.AddIgnored(id, SkillIgnoredReason.UnknownSkill); continue; }
                if (binding.Implementation == SkillImplementation.Deferred) { r.AddIgnored(id, SkillIgnoredReason.Deferred); continue; }
                if (!MeetsRequirements(def, attributes)) { r.AddIgnored(id, SkillIgnoredReason.RequirementNotMet); continue; }
                r.SetOwned(binding.Effect);
                r.AddActive(id);
            }
            return r;
        }

        /// <summary>The potential of a player nobody knows the skills of (a match built without skill data): no skill owned, mastery from the attributes.</summary>
        public static ResolvedSkills FromAttributesOnly(in PlayerAttributes attributes)
        {
            return Resolve(null, null, attributes, false);
        }

        /// <summary>All the attribute floors hold. A requirement is a GATE (the skill is only usable when it holds); it never adds anything.</summary>
        public static bool MeetsRequirements(SkillDefinition def, in PlayerAttributes attributes)
        {
            foreach (SkillRequirement q in def.Requirements) if (attributes.GetValue(q.Attribute) < q.Minimum) return false;
            return true;
        }

        /// <summary>0..1: the weighted share of his attributes that this skill is made of (the weights are in <see cref="SkillBindings"/>).</summary>
        public static float MasteryOf(SkillBinding binding, in PlayerAttributes attributes)
        {
            float sum = 0f;
            foreach (SkillWeight w in binding.Mastery) sum += w.Weight * Normalised(attributes.GetValue(w.Attribute));
            return sum < 0f ? 0f : (sum > 1f ? 1f : sum);
        }

        private static float Normalised(int attribute)
        {
            int v = Math.Max(PlayerAttributes.Min, Math.Min(PlayerAttributes.Max, attribute));
            return (v - PlayerAttributes.Min) / (float)(PlayerAttributes.Max - PlayerAttributes.Min);
        }

        /// <summary>
        /// THE permission check (the one source of truth: nothing else tests "is the skill granted"). Ordinary actions are always allowed here. A skill action needs an owned skill of its
        /// effect list; when the match knows no skills at all, the caller's grant stands in for it. <paramref name="skillId"/> asks for one style (a skill move, a lob); empty = the best owned.
        /// </summary>
        public static SkillPermission CanUseAction(ResolvedSkills skills, FootballActionKind action, bool hasBall, bool callerGrant = false, string skillId = null)
        {
            if (!SkillBindings.IsSkillAction(action)) return SkillPermission.Allow();
            if (skills == null) skills = ResolvedSkills.None;
            IReadOnlyList<SkillEffect> effects = SkillBindings.EffectsUnlocking(action);

            SkillEffect chosen = SkillEffect.None;
            float best = -1f;
            bool granted = false;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillEffect e = effects[i];
                bool has = skills.Has(e);
                if (!has && !(callerGrant && !skills.IsAuthoritative)) continue;
                if (!string.IsNullOrEmpty(skillId) && SkillBindings.SkillIdOf(e) != skillId) continue;
                granted = true;
                float s = skills.Potential(e);
                // ties go to the first effect of the list (catalogue order): deterministic
                if (s > best) { best = s; chosen = e; }
            }
            if (!granted) return SkillPermission.Refuse(ActionRejection.SkillNotGranted);
            if (action == FootballActionKind.SkillMove && !hasBall) return SkillPermission.Refuse(ActionRejection.NotInPossession);
            return SkillPermission.Allow(chosen, SkillBindings.SkillIdOf(chosen));
        }

        /// <summary>The movement modifiers of a player's skills (identity when he has none).</summary>
        public static LocomotionModifiers LocomotionOf(ResolvedSkills skills, SkillTuning t)
        {
            var m = new LocomotionModifiers();
            float burst = skills.Strength(SkillEffect.ExplosiveBurst);
            if (burst > 0f) { m.BurstGain = t.BurstAccelerationGain * burst; m.BurstSpeedFraction = t.BurstSpeedFraction; }
            float turn = skills.Strength(SkillEffect.TurnAndAccelerate);
            if (turn > 0f) { m.TurnGain = t.TurnAccelerationGain * turn; m.TurnAngleThreshold = t.TurnAngleThresholdDegrees * MathUtil.Deg2Rad; m.TurnPenaltyRelief = t.TurnPenaltyRelief * turn; }
            return m;
        }
    }
}
