using System;

namespace FS27.Core
{
    /// <summary>What went wrong on purpose in a decision (Phase 14): a controlled mistake the level allows itself. Counted by the diagnostics, never used by any rule.</summary>
    public enum DecisionErrorKind
    {
        None = 0,
        /// <summary>A pass chosen when a better option was available.</summary>
        BadPass,
        /// <summary>The right kind of pass, to a worse receiver.</summary>
        WrongReceiver,
        BadShot,
        /// <summary>He dribbled when a better option was available.</summary>
        UnnecessaryDribble,
        /// <summary>A forward pass or a pass into space, or the shot, when a safer option was close.</summary>
        ExcessiveRisk,
        /// <summary>He held or shielded the ball when a better option was available.</summary>
        Passive,
        /// <summary>He waited before committing a tackle, a slide or a challenge.</summary>
        LateContest,
        /// <summary>He noticed a change of possession late.</summary>
        LateReaction
    }

    public struct DecisionError
    {
        public DecisionErrorKind Kind;
        /// <summary>How much better the option he did not take was, in score points.</summary>
        public float Gap;
        /// <summary>The situation of the carrier when it happened (pressure 0..1, stamina 0..100, whether he had a free way to dribble): for the diagnostics only.</summary>
        public float Pressure, Stamina;
        public bool FreeWay;
        public bool Occurred { get { return Kind != DecisionErrorKind.None; } }
    }

    public sealed partial class DecisionProfile
    {
        /// <summary>The defence of a team with this profile: the common tuning changed only in HOW WELL it is used (who presses, how far it reads, how careful a challenge is). A copy; the argument is not changed.</summary>
        public DefenseTuning TuneDefense(DefenseTuning common, GameMode mode)
        {
            DefenseTuning d = common.Clone();
            DecisionProfileTuning t = Tuning;
            d.InterceptHorizonSeconds *= MathUtil.Lerp(t.AnticipationMinScale, t.AnticipationMaxScale, AnticipationQuality);
            d.ScoreGoalSideWeight *= MathUtil.Lerp(t.PressGoalSideAtWorst, t.PressGoalSideAtBest, PressureQuality);
            d.PressMinStamina *= MathUtil.Lerp(t.PressStaminaAtWorst, t.PressStaminaAtBest, PressureQuality);
            // the careful defender gives away fewer fouls where there are fouls: only where the mode already limits the challenge
            if (mode == GameMode.Classic) d.ChallengeMaxRisk += t.ChallengeRiskSpread * (t.HabitReference - PressureQuality) / t.HabitReference;
            return d;
        }

        /// <summary>The most effort (0..1 of the stick) a movement that nothing depends on may ask of a player with this stamina: full while he is fresh, down to a walk as he empties. The better the level manages its legs, the earlier it starts to save them.</summary>
        public float EffortCap(float stamina)
        {
            DecisionProfileTuning t = Tuning;
            float ceiling = MathUtil.Lerp(t.EffortCeilingAtWorst, t.EffortCeilingAtBest, StaminaManagement);
            float fresh = MathUtil.Clamp01((stamina - t.EffortFloor) / Math.Max(ceiling - t.EffortFloor, 1e-3f));
            return MathUtil.Lerp(t.MinEffort, 1f, fresh);
        }

        /// <summary>Where this player is a little off his ideal place (a slow drift): metres, in a direction that changes every <see cref="DecisionProfileTuning.PositionErrorPeriod"/> seconds.</summary>
        public Vec2 PositionDrift(int slot, float matchTime, out float meters)
        {
            DecisionProfileTuning t = Tuning;
            meters = PositionErrorMeters * t.PositionErrorShare * (1f - PositioningQuality);
            if (meters <= 0f) return Vec2.Zero;
            int period = (int)(matchTime / t.PositionErrorPeriod);
            float angle = DecisionDice.Roll(DecisionDice.DriftAngle, period, slot) * t.FullTurn;
            float scale = DecisionDice.Roll(DecisionDice.Drift, period, slot);
            meters *= scale;
            return new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * meters;
        }

        /// <summary>
        /// Would this player take the second-best option instead of the best? Only when the two are close (never more than <see cref="DecisionProfileTuning.ErrorMaxScoreGap"/> apart), more likely the closer
        /// they are, the weaker the level, the more pressure he is under, the more tired he is and the worse the player is at that action. The roll depends on the match clock and the player only.
        /// </summary>
        public bool ChoosesSecond(in ActionCandidate best, in ActionCandidate second, float pressure, float stamina01, float actionQuality, int tick, int slot)
        {
            DecisionProfileTuning t = Tuning;
            float gap = best.Score - second.Score;
            if (ErrorRate <= 0f || gap > t.ErrorMaxScoreGap) return false;
            float confusion = 1f - MathUtil.Clamp01(Math.Max(0f, gap) / MinimumDecisionMargin);
            float chance = ErrorRate * confusion
                           * (1f + PressureSensitivity * MathUtil.Clamp01(pressure) * t.ErrorPressureWeight)
                           * (1f + FatigueSensitivity * (1f - MathUtil.Clamp01(stamina01)) * t.ErrorFatigueWeight)
                           * MathUtil.Lerp(t.ErrorAttributeAtWorst, t.ErrorAttributeAtBest, MathUtil.Clamp01(actionQuality));
            return DecisionDice.Roll(DecisionDice.Choice, tick, slot) < chance;
        }

        /// <summary>The kind of mistake taking <paramref name="taken"/> instead of <paramref name="best"/> is.</summary>
        public static DecisionErrorKind Classify(in ActionCandidate best, in ActionCandidate taken)
        {
            switch (taken.Type)
            {
                case CandidateType.Pass:
                case CandidateType.ForwardPass:
                    return (best.Type == CandidateType.Pass || best.Type == CandidateType.ForwardPass) ? DecisionErrorKind.WrongReceiver : DecisionErrorKind.BadPass;
                case CandidateType.ThroughPass: return DecisionErrorKind.ExcessiveRisk;
                case CandidateType.Shoot: return DecisionErrorKind.BadShot;
                case CandidateType.Dribble: return DecisionErrorKind.UnnecessaryDribble;
                default: return DecisionErrorKind.Passive;
            }
        }

        /// <summary>The player reading the game: his own quality for each action, his habits and the score and the clock, as a bias on every candidate.</summary>
        public ICandidateBias BiasFor(in WorldPlayer me, in WorldMatch match, float shootRange, float goalDistance)
        {
            var b = new CarrierBias(this, me, match, shootRange);
            b.GoalDistance = goalDistance;
            return b;
        }

        private sealed class CarrierBias : ICandidateBias
        {
            private readonly DecisionProfile p;
            private readonly ActionProfile skill;
            private readonly float risk, shootRange;
            public float GoalDistance;

            public CarrierBias(DecisionProfile profile, in WorldPlayer me, in WorldMatch match, float range)
            {
                p = profile; skill = me.Profile; shootRange = range;
                float r = profile.Urgency(match, me.Team) + profile.RiskTolerance;
                risk = Math.Max(-1f, Math.Min(1f, r));
            }

            public float Bias(in ActionCandidate c, in CarrierSummary s)
            {
                DecisionProfileTuning t = p.Tuning;
                float use = t.SkillUseWeight * p.SkillUsageQuality;
                float safe = risk < 0f ? -risk * t.SafeBias : 0f;
                float bold = risk * t.RiskBias;
                switch (c.Type)
                {
                    case CandidateType.Shoot:
                    {
                        float far = MathUtil.Clamp01(GoalDistance / shootRange);
                        float q = MathUtil.Lerp(skill.ShotQualityClose, skill.ShotQualityFar, far);
                        return use * (q - t.SkillNeutral) + t.ShotHabit * (t.HabitReference - p.ShotSelectionQuality) / t.HabitReference + bold;
                    }
                    case CandidateType.Pass:
                        return use * (skill.PassQuality - t.SkillNeutral) + safe;
                    case CandidateType.ForwardPass:
                    case CandidateType.ThroughPass:
                        return use * (skill.PassQuality - t.SkillNeutral) + bold;
                    case CandidateType.Dribble:
                        return use * (skill.DribbleSkill - t.SkillNeutral) + t.DribbleHabit * (t.HabitReference - p.PassSelectionQuality) / t.HabitReference;
                    default:
                        return safe;
                }
            }
        }
    }
}
