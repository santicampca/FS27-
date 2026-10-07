using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 6 of the match engine: SKILLS inside the actions. Nothing here is a second system: a skill is the catalogue's <see cref="SkillDefinition"/>, owned through <see cref="PlayerSkillSet"/>,
    /// resolved ONCE per player into <see cref="ResolvedSkills"/> by <see cref="SkillResolver"/>, and applied here, inside the action that is being resolved (request -> resolver -> ball -> result).
    /// A skill never changes an attribute or any rating: it changes how one action resolves, by a bounded amount scaled by the owner's mastery, on top of the stamina, foot and noise the
    /// action already has. Every number is in <see cref="SkillTuning"/>; every chance comes from the match's seeded action generator, in the order the actions happen.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly SkillTuning skillTuning;
        public SkillTuning SkillTuning => skillTuning;

        private void ResolvePlayerSkills(IPlayerLookup players, IPlayerSkillLookup skills, SkillCatalog catalog, PlayerAttributes neutral)
        {
            bool known = skills != null && catalog != null;
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
                foreach (MatchPlayer p in team.Players)
                {
                    PlayerAttributes attributes = neutral;
                    if (players != null && players.TryGet(p.PlayerId, out PlayerDefinition def) && def != null) attributes = def.Attributes;
                    PlayerSkillSet set = null;
                    if (known) skills.TryGetSkills(p.PlayerId, out set);
                    p.Skills = known ? SkillResolver.Resolve(set, catalog, attributes, true) : SkillResolver.FromAttributesOnly(attributes);
                    p.LocomotionModifiers = SkillResolver.LocomotionOf(p.Skills, skillTuning);
                }
        }

        /// <summary>
        /// May this player perform this action? THE permission check of the engine (the same one <see cref="Perform"/> uses): ordinary actions are always allowed here, a skill action needs a skill
        /// (see <see cref="SkillResolver.CanUseAction"/>). <paramref name="skillId"/> asks for one style; <paramref name="callerGrant"/> is the Phase-4 grant, honoured only by a match that knows no skills.
        /// </summary>
        public SkillPermission CanUseAction(TeamSide side, string playerId, FootballActionKind action, string skillId = null, bool callerGrant = false)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            if (p == null) return SkillPermission.Refuse(ActionRejection.UnknownPlayer);
            return SkillResolver.CanUseAction(p.Skills, action, IsCarrier(p), callerGrant, skillId);
        }

        /// <summary>The foot is the weak one of this player (his preferred foot is the other).</summary>
        private static bool IsWeakFoot(in ActionProfile prof, ActionFoot foot)
        {
            if (foot != ActionFoot.Left && foot != ActionFoot.Right) return false;
            ActionFoot dominant = prof.PreferredFoot == PreferredFoot.Right ? ActionFoot.Right : ActionFoot.Left;
            return foot != dominant;
        }

        private float Mastery(MatchPlayer p, SkillEffect effect) { return p.Skills.Strength(effect); }

        // ------------------------------------------------------------------ kicks

        /// <summary>What the skills of a kicker change in one kick. The identity (no skill) is the Phase-4 kick, number for number.</summary>
        private struct KickSkillAdjust
        {
            public float QualityBonus;
            public float ErrorFactor;
            public float ExtraErrorFactor;
            public float SpeedFactor;
            public float LoftFactor;
            public float ExtraCost;
            public float AimDegrees;
            /// <summary>Error (degrees) no quality removes; it follows the same draw as the kick's error.</summary>
            public float ResidualErrorDegrees;
            public string SkillId;

            public static KickSkillAdjust Identity { get { return new KickSkillAdjust { ErrorFactor = 1f, ExtraErrorFactor = 1f, SpeedFactor = 1f, LoftFactor = 1f }; } }
        }

        private static void Note(ref KickSkillAdjust a, SkillEffect effect)
        {
            if (a.SkillId == null) a.SkillId = SkillBindings.SkillIdOf(effect);
        }

        private void Bonus(ref KickSkillAdjust a, MatchPlayer p, SkillEffect effect, float bonus)
        {
            float m = Mastery(p, effect);
            if (!(m > 0f)) return;
            a.QualityBonus += bonus * m;
            Note(ref a, effect);
        }

        private KickSkillAdjust KickSkillsOf(MatchPlayer p, ActionRequest r, SkillEffect style, bool carrier, Vec2 dir, float curve)
        {
            KickSkillAdjust a = KickSkillAdjust.Identity;
            SkillTuning t = skillTuning;
            Vec2 attack = AttackDirectionOf(p.Team);
            bool forward = dir.X * attack.X + dir.Y * attack.Y >= t.ForwardCosine;

            switch (r.Kind)
            {
                case FootballActionKind.ShortPass:
                    Bonus(ref a, p, SkillEffect.BuildUpPass, t.BuildUpBonus);
                    if (forward) Bonus(ref a, p, SkillEffect.ProgressivePass, t.ProgressivePassBonus);
                    break;
                case FootballActionKind.WallPass:
                    Bonus(ref a, p, SkillEffect.BuildUpPass, t.BuildUpBonus);
                    break;
                case FootballActionKind.LongPass:
                    if (forward) Bonus(ref a, p, SkillEffect.ProgressivePass, t.ProgressivePassBonus);
                    break;
                case FootballActionKind.ThroughPass:
                    {
                        float line = Mastery(p, SkillEffect.LineBreakingPass);
                        if (line > 0f) { a.QualityBonus += t.LineBreakingBonus * line; a.ExtraErrorFactor *= 1f - t.LineBreakingErrorRelief * line; Note(ref a, SkillEffect.LineBreakingPass); }
                        if (forward) Bonus(ref a, p, SkillEffect.ProgressivePass, t.ProgressivePassBonus);
                        break;
                    }
                case FootballActionKind.Shot:
                case FootballActionKind.CurvedShot:
                    {
                        Vec2 goal = GoalCentreOf(p.Team);
                        float distance = (goal - Flat(State.Ball.Physics.Position)).Magnitude;
                        if (distance <= actions.ShotCloseRange) Bonus(ref a, p, SkillEffect.ClinicalFinish, t.ClinicalFinishBonus);
                        else Bonus(ref a, p, SkillEffect.LongRangeStrike, t.LongRangeBonus);
                        if (!carrier) Bonus(ref a, p, SkillEffect.FirstTimeFinish, t.FirstTimeFinishBonus);
                        break;
                    }
            }

            switch (style)
            {
                case SkillEffect.CurvedShot:
                    {
                        float m = p.Skills.Potential(SkillEffect.CurvedShot);
                        a.QualityBonus += t.CurveMasteryBonus * m;
                        a.ExtraErrorFactor *= 1f - t.CurveErrorRelief * m;
                        a.AimDegrees = -t.CurveAimCompensationDegrees * curve * m;
                        a.ResidualErrorDegrees = t.CurveResidualErrorDegrees * Math.Abs(curve);
                        a.ExtraCost += t.CurvedShotExtraStamina;
                        a.SkillId = SkillBindings.SkillIdOf(style);
                        break;
                    }
                case SkillEffect.SwitchLob:
                    {
                        float m = p.Skills.Potential(SkillEffect.SwitchLob);
                        a.QualityBonus += t.LobMasteryBonus * m;
                        a.SpeedFactor *= t.SwitchSpeedFactor;
                        a.LoftFactor *= t.SwitchLoftFactor;
                        a.ExtraCost += t.LobExtraStamina;
                        a.SkillId = SkillBindings.SkillIdOf(style);
                        break;
                    }
                case SkillEffect.CreativeLob:
                    {
                        float m = p.Skills.Potential(SkillEffect.CreativeLob);
                        a.QualityBonus += t.LobMasteryBonus * m;
                        a.LoftFactor *= t.CreativeLoftFactor;
                        a.ErrorFactor *= t.CreativeErrorFactor;
                        a.ExtraCost += t.LobExtraStamina;
                        a.SkillId = SkillBindings.SkillIdOf(style);
                        break;
                    }
            }
            return a;
        }

        /// <summary>The quality of a kick with the skills' bonus: it never goes past the ceiling (so no skill makes a perfect kick), and never below what the attributes alone give.</summary>
        private float SkillQuality(float plain, float boosted)
        {
            boosted = ActionModel.Clamp01(boosted);
            if (!(boosted > plain)) return plain;
            return Math.Max(plain, Math.Min(boosted, skillTuning.QualityCeiling));
        }

        // ------------------------------------------------------------------ the skill move

        private int OpponentsNear(MatchPlayer p, float radius, int most)
        {
            int count = 0;
            TeamMatchState others = State.Team(p.Team.Opponent());
            for (int i = 0; i < others.Players.Count && count < most; i++)
            {
                MatchPlayer o = others.Players[i];
                if (o.IsActive && (o.Locomotion.Position - p.Locomotion.Position).Magnitude <= radius) count++;
            }
            return count;
        }

        private void MoveStyle(SkillEffect style, out float bias, out float distance, out float evade, out float cost)
        {
            SkillTuning t = skillTuning;
            switch (style)
            {
                case SkillEffect.CloseControlMove: bias = t.CloseMoveBias; distance = t.CloseMoveDistance; evade = t.CloseMoveEvade; cost = t.CloseMoveCost; break;
                case SkillEffect.FlairMove: bias = t.FlairMoveBias; distance = t.FlairMoveDistance; evade = t.FlairMoveEvade; cost = t.FlairMoveCost; break;
                default: bias = t.ElusiveMoveBias; distance = t.ElusiveMoveDistance; evade = t.ElusiveMoveEvade; cost = t.ElusiveMoveCost; break;
            }
        }

        /// <summary>
        /// A skill move: the carrier tries to beat the man in front of him. The score is his mastery of the style (from his attributes) x the foot he must use x his stamina, plus the bias
        /// of the style, minus the pressure of the opponents close to him, his speed and the angle he asks for, plus a little noise. Clean: the ball is pushed past the defender and the carrier is
        /// protected for a moment. Reduced: less ground and less protection. Failed: the ball runs away and he is busy for a moment. It costs stamina, and he cannot repeat it at once.
        /// </summary>
        private ActionResult ResolveSkillMove(ActionResult res, MatchPlayer p, Vec2 dir, float power, SkillEffect style, string skillId)
        {
            if (!IsCarrier(p)) return Reject(res, ActionRejection.NotInPossession);
            if (p.SkillMoveCooldownRemaining > 0f) return Reject(res, ActionRejection.ActionLocked);

            SkillTuning t = skillTuning;
            MoveStyle(style, out float bias, out float distanceFactor, out float evadeFactor, out float costFactor);
            ActionProfile prof = p.ActionProfile;
            Vec2 facing = p.Locomotion.FacingDirection;
            ActionFoot foot = ActionModel.ChooseFoot(prof, facing, dir, actions, out float footFactor);
            float angle = Math.Abs(ActionModel.SignedAngle(facing, dir));
            int pressing = OpponentsNear(p, actions.PressureRadius, 2);
            float topSpeed = p.LocomotionProfile.MaxSpeed;
            float speedShare = topSpeed > 0f ? ActionModel.Clamp01(p.Locomotion.CurrentSpeed / topSpeed) : 0f;

            float mastery = p.Skills.Potential(style);
            float score = mastery * footFactor * StaminaFactorOf(p) + bias - pressing * t.MovePressureWeight - speedShare * t.MoveSpeedWeight - angle / t.MoveAngleScaleDegrees * t.MoveAngleWeight + Noise() * t.MoveNoise;

            MatchBall b = State.Ball;
            float share = score >= t.MoveCleanThreshold ? 1f : (score >= t.MoveReducedThreshold ? t.MoveReducedShare : 0f);
            Vec3 current = b.Physics.Velocity;

            res.Foot = foot;
            res.WeakFoot = IsWeakFoot(prof, foot);
            res.Quality = ActionModel.Clamp01(score);
            res.SkillId = skillId;
            res.StaminaCost = SpendStamina(p, t.MoveStaminaCost * costFactor);
            p.SkillMoveCooldownRemaining = t.MoveCooldownSeconds;

            if (share > 0f)
            {
                float push = t.MoveBallSpeed * distanceFactor * share * power;
                SetBallVelocity(new Vec3(current.X + dir.X * push, 0f, current.Z + dir.Y * push));
                p.EvadeRemaining = t.MoveEvadeSeconds;
                p.EvadeStrength = evadeFactor * share;
                TouchBall(p, FootballActionKind.SkillMove, false);
                res.BallVelocity = b.Physics.Velocity;
                return Complete(res, share >= 1f ? ActionDetail.Evaded : ActionDetail.PartlyEvaded);
            }

            // the move failed: the ball runs away from his feet and he is busy for a moment
            ClearPossession();
            SetBallVelocity(new Vec3(current.X + dir.X * t.MoveFailBallSpeed, 0f, current.Z + dir.Y * t.MoveFailBallSpeed));
            b.Mode = BallMode.Free;
            TouchBall(p, FootballActionKind.SkillMove);
            p.ActionLockRemaining = Math.Max(p.ActionLockRemaining, t.MoveFailLockSeconds);
            res.BallVelocity = b.Physics.Velocity;
            res.Outcome = ActionOutcome.Failed;
            res.Detail = ActionDetail.Fumbled;
            return res;
        }

        private void StepSkillTimers(MatchPlayer p, float dt)
        {
            if (p.SkillMoveCooldownRemaining > 0f) p.SkillMoveCooldownRemaining = Math.Max(0f, p.SkillMoveCooldownRemaining - dt);
            if (p.EvadeRemaining > 0f)
            {
                p.EvadeRemaining = Math.Max(0f, p.EvadeRemaining - dt);
                if (p.EvadeRemaining <= 0f) p.EvadeStrength = 0f;
            }
        }

        // ------------------------------------------------------------------ defending and carrying

        private bool FacesTowards(MatchPlayer carrier, MatchPlayer defender)
        {
            Vec2 to = ActionModel.DirectionOr(defender.Locomotion.Position - carrier.Locomotion.Position, Vec2.Zero);
            Vec2 f = carrier.Locomotion.FacingDirection;
            return f.X * to.X + f.Y * to.Y > skillTuning.FacingCosine;
        }

        /// <summary>What the tackler's skills add to his attack in a standing or sliding tackle.</summary>
        private float SkillTackleBonus(MatchPlayer tackler, MatchPlayer carrier, bool slide)
        {
            float bonus = skillTuning.AggressiveTackleBonus * Mastery(tackler, SkillEffect.AggressiveTackle);
            if (!slide && FacesTowards(carrier, tackler)) bonus += skillTuning.OneVOneBonus * Mastery(tackler, SkillEffect.OneVOneDefending);
            return bonus;
        }

        /// <summary>What the carrier's skills add to his resistance in a duel: a skill move that has just worked, and holding the ball up when he shields.</summary>
        private float SkillResistBonus(MatchPlayer carrier)
        {
            float bonus = 0f;
            if (carrier.IsEvading) bonus += skillTuning.MoveEvadeDefenceBonus * carrier.EvadeStrength;
            if (carrier.IsShielding) bonus += skillTuning.HoldUpBonus * Mastery(carrier, SkillEffect.HoldUp);
            return bonus;
        }

        private float SkillFoulShift(MatchPlayer tackler) { return skillTuning.AggressiveFoulShift * Mastery(tackler, SkillEffect.AggressiveTackle); }

        /// <summary>1 = the whole recovery after a lost tackle; a one-on-one defender is back sooner.</summary>
        private float SkillFailLockFactor(MatchPlayer tackler) { return 1f - skillTuning.OneVOneLockRelief * Mastery(tackler, SkillEffect.OneVOneDefending); }

        private float SkillInterceptReachFactor(MatchPlayer p) { return 1f + skillTuning.InterceptReadingReach * Mastery(p, SkillEffect.InterceptionReading); }

        private float SkillInterceptBonus(MatchPlayer p, bool block)
        {
            float bonus = skillTuning.InterceptReadingBonus * Mastery(p, SkillEffect.InterceptionReading);
            if (block) bonus += skillTuning.DefensiveBlockBonus * Mastery(p, SkillEffect.DefensiveBlock);
            return bonus;
        }

        private float SkillPressureSecondsFactor(MatchPlayer p) { return 1f + skillTuning.HighPressSecondsGain * Mastery(p, SkillEffect.HighPress); }
        private float SkillPressureDrainFactor(MatchPlayer p) { return 1f - skillTuning.HighPressDrainRelief * Mastery(p, SkillEffect.HighPress); }
        private float SkillPressureRadiusFactor(MatchPlayer presser) { return 1f + skillTuning.HighPressRadiusGain * Mastery(presser, SkillEffect.HighPress); }
        /// <summary>How much this presser counts in the pressure on the carrier (1 for an ordinary presser).</summary>
        private float SkillPresserWeight(MatchPlayer presser) { return 1f + skillTuning.ScreenPressureGain * Mastery(presser, SkillEffect.DefensiveScreen); }

        /// <summary>The share of the pressure penalty that still reaches the carrier (1 = all of it).</summary>
        private float SkillPressureLeft(MatchPlayer carrier)
        {
            float left = 1f - skillTuning.PressResistanceRelief * Mastery(carrier, SkillEffect.PressResistance);
            if (carrier.IsEvading) left *= 1f - skillTuning.MoveEvadePressureRelief * ActionModel.Clamp01(carrier.EvadeStrength);
            return left;
        }

        /// <summary>What his skills add to the control of the ball he runs with: carrying it at pace, and holding it up when he shields.</summary>
        private float SkillCarryBonus(MatchPlayer carrier)
        {
            float top = carrier.LocomotionProfile.MaxSpeed;
            float pace = top > 0f ? ActionModel.Clamp01(carrier.Locomotion.CurrentSpeed / top) : 0f;
            float bonus = skillTuning.ProgressiveCarryBonus * Mastery(carrier, SkillEffect.ProgressiveCarry) * pace;
            if (carrier.IsShielding) bonus += skillTuning.HoldUpBonus * Mastery(carrier, SkillEffect.HoldUp);
            return bonus;
        }

        /// <summary>What his skills add to the first touch: receiving with an opponent on top of him, and recovering a loose ball.</summary>
        private float SkillFirstTouchBonus(MatchPlayer p)
        {
            if (p.InGoal) return 0f;
            float bonus = 0f;
            float pocket = Mastery(p, SkillEffect.PocketReceive);
            if (pocket > 0f && OpponentsNear(p, actions.PressureRadius, 1) > 0) bonus += skillTuning.PocketReceiveBonus * pocket;
            float recovery = Mastery(p, SkillEffect.BallRecovery);
            if (recovery > 0f && !State.Ball.HasPossession && (State.Ball.Mode == BallMode.Free || State.Ball.Mode == BallMode.Deflected || State.Ball.Mode == BallMode.Rebound)) bonus += skillTuning.BallRecoveryBonus * recovery;
            return bonus;
        }
    }
}
