using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 4 of the match engine: ACTIONS and BALL. The flow is always  input -> <see cref="Perform"/> -> player action -> ball (<see cref="BallSimulator"/>) -> <see cref="MatchState"/> -> Unity shows it.
    /// Nothing here is a second source of truth: possession lives only in <see cref="MatchBall"/>; the ball is never attached to a player (a controlled ball is a ball that is kept close by
    /// the dribble step, and that can be lost); every outcome that has randomness draws from the match's seeded generator, in the order the actions happen.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly ActionTuning actions;
        private readonly SeededRandom actionRandom;

        /// <summary>The last action answered, including the automatic touches of the ball.</summary>
        public ActionResult LastActionResult { get; private set; }
        /// <summary>Raised for every action that was answered (requests and automatic touches), in order.</summary>
        public event Action<ActionResult> ActionPerformed;
        public ActionTuning ActionTuning => actions;

        // ------------------------------------------------------------------ public API

        /// <summary>The player who controls the ball, or null when it is free (the single source: <see cref="MatchBall"/>).</summary>
        public MatchPlayer BallCarrier
        {
            get
            {
                MatchBall b = State.Ball;
                if (!b.HasPossession || b.PossessingPlayerId == null) return null;
                return State.Team(b.PossessingTeam).Find(b.PossessingPlayerId);
            }
        }

        public ActionContext GetActionContext(TeamSide side, string playerId)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            return new ActionContext { HasBall = p != null && IsCarrier(p), IsGoalkeeper = p != null && p.InGoal };
        }

        /// <summary>The neutral input of the player (button, gesture, direction, magnitude) is mapped to an action by the context and performed.</summary>
        public ActionResult PerformInput(TeamSide side, string playerId, ActionInput input)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            var request = new ActionRequest(side, playerId, FootballActionKind.None, input.Direction, input.Magnitude > 0f ? input.Magnitude : actions.DefaultPower) { Source = ControlSource.Human };
            if (p != null)
            {
                ActionContext context = new ActionContext { HasBall = IsCarrier(p), IsGoalkeeper = p.InGoal };
                request.Kind = ActionMapper.Map(input, context);
                request.Long = context.IsGoalkeeper && context.HasBall && input.Gesture == ActionGesture.SwipeUp;
                request.Curve = input.Curve;
                if (request.Kind == FootballActionKind.CurvedShot && request.Curve == 0f) request.Curve = (p.ActionProfile.PreferredFoot == PreferredFoot.Right ? 1f : -1f) * skillTuning.DefaultCurve;
            }
            return Perform(request);
        }

        /// <summary>Performs an action. Never throws for normal gameplay: a refusal is an <see cref="ActionResult"/> with its reason.</summary>
        public ActionResult Perform(ActionRequest request)
        {
            ActionResult result = Resolve(request);
            LastActionResult = result;
            ActionPerformed?.Invoke(result);
            return result;
        }

        // ------------------------------------------------------------------ resolution

        private ActionResult Resolve(ActionRequest r)
        {
            var res = new ActionResult { Kind = r.Kind, Team = r.Team, PlayerId = r.PlayerId, Outcome = ActionOutcome.Rejected, Source = r.Source };

            if (r.Kind == FootballActionKind.None) return Reject(res, ActionRejection.InvalidInput);
            if (!ActionsLive) return Reject(res, ActionRejection.MatchNotLive);
            MatchPlayer p = State.Team(r.Team).Find(r.PlayerId);
            if (p == null) return Reject(res, ActionRejection.UnknownPlayer);
            if (!p.IsActive) return Reject(res, ActionRejection.PlayerNotActive);
            if (!ActionModel.IsFinite(r.Direction) || float.IsNaN(r.Power) || float.IsNaN(r.Curve)) return Reject(res, ActionRejection.InvalidInput);
            if (p.ActionLockRemaining > 0f || p.KickCooldownRemaining > 0f) return Reject(res, ActionRejection.ActionLocked);

            Vec2 facing = p.Locomotion.FacingDirection;
            Vec2 dir = ActionModel.DirectionOr(r.Direction, facing);
            if (IsKickAction(r.Kind) && !r.Target.IsNone) dir = ApplyTarget(r, p, dir, ref res);
            float power = ActionModel.Clamp01(r.Power);
            res.Direction = dir;
            res.Intensity = power;

            switch (r.Kind)
            {
                case FootballActionKind.ShortPass:
                case FootballActionKind.WallPass:
                case FootballActionKind.ThroughPass:
                case FootballActionKind.LongPass:
                case FootballActionKind.Chip:
                case FootballActionKind.Shot:
                case FootballActionKind.Clearance:
                case FootballActionKind.GoalkeeperDistribution:
                    return AfterKick(ResolveKick(res, p, r, dir, power));

                case FootballActionKind.SkillLobPass:
                case FootballActionKind.CurvedShot:
                case FootballActionKind.SkillMove:
                    {
                        SkillPermission permission = SkillResolver.CanUseAction(p.Skills, r.Kind, IsCarrier(p), r.SkillGranted, r.SkillId);
                        if (!permission.Allowed) return Reject(res, permission.Rejection);
                        if (r.Kind == FootballActionKind.SkillMove) return ResolveSkillMove(res, p, dir, power, permission.Effect, permission.SkillId);
                        return AfterKick(ResolveKick(res, p, r, dir, power, permission.Effect));
                    }

                case FootballActionKind.Control: return ResolveControlAction(res, p);
                case FootballActionKind.Dribble: return IsCarrier(p) ? Complete(res, ActionDetail.Dribbling) : Reject(res, ActionRejection.NotInPossession);
                case FootballActionKind.Shield: return ResolveShield(res, p);
                case FootballActionKind.Pressure: return ResolvePressure(res, p);
                case FootballActionKind.Tackle: return ResolveTackle(res, p, dir, false);
                case FootballActionKind.SlidingTackle: return ResolveTackle(res, p, dir, true);
                case FootballActionKind.PhysicalChallenge: return ResolveChallenge(res, p);
                case FootballActionKind.Intercept: return ResolveIntercept(res, p, false);
                case FootballActionKind.Block: return ResolveIntercept(res, p, true);
                case FootballActionKind.GoalkeeperRush: return ResolveRush(res, p);

                default:
                    // Header, Cross, PlacedShot, GoalkeeperSave and GoalkeeperCatch (the last two happen on their own when the keeper touches the ball)
                    return Reject(res, ActionRejection.NotSupported);
            }
        }

        private static ActionResult Reject(ActionResult res, ActionRejection why)
        {
            res.Outcome = ActionOutcome.Rejected;
            res.Rejection = why;
            res.Detail = ActionDetail.None;
            res.Direction = Vec2.Zero;
            res.Intensity = 0f;
            return res;
        }

        private static ActionResult Complete(ActionResult res, ActionDetail detail)
        {
            res.Outcome = ActionOutcome.Completed;
            res.Detail = detail;
            return res;
        }

        // ------------------------------------------------------------------ small helpers

        private bool IsCarrier(MatchPlayer p)
        {
            MatchBall b = State.Ball;
            return b.HasPossession && b.PossessingTeam == p.Team && b.PossessingPlayerId == p.PlayerId;
        }

        private static Vec2 Flat(Vec3 v) { return new Vec2(v.X, v.Z); }

        private float Noise() { return actionRandom.NextFloat01() * 2f - 1f; }

        private float BallDistanceTo(MatchPlayer p) { return (Flat(State.Ball.Physics.Position) - p.Locomotion.Position).Magnitude; }

        private float BallHeight() { return State.Ball.Height(ballParameters.Radius); }

        private float StaminaFactorOf(MatchPlayer p) { return ActionModel.StaminaFactor(p.Locomotion.CurrentStamina, locomotion, actions); }

        private float PerformanceOf(MatchPlayer p) { return LocomotionStamina.Performance(p.Locomotion.CurrentStamina, locomotion); }

        private float SpendStamina(MatchPlayer p, float cost)
        {
            if (!(cost > 0f)) return 0f;
            float before = p.Locomotion.CurrentStamina;
            p.Locomotion.CurrentStamina = LocomotionStamina.Clamp(before - cost, locomotion);
            return before - p.Locomotion.CurrentStamina;
        }

        private Vec2 AttackDirectionOf(TeamSide side)
        {
            bool plusX = State.HomeAttacksPositiveX == (side == TeamSide.Home);
            return new Vec2(plusX ? 1f : -1f, 0f);
        }

        private Vec2 GoalCentreOf(TeamSide attacker)
        {
            return new Vec2(AttackDirectionOf(attacker).X * field.Length * 0.5f, 0f);
        }

        private void MarkBall(MatchPlayer p, FootballActionKind kind)
        {
            State.Ball.LastAction = kind;
            State.Ball.LastActionPlayerId = p.PlayerId;
        }

        /// <summary>A touch of the ball: the event, the last action and (unless the ball is about to be given to the player, which records it) the last-touch bookkeeping.</summary>
        private void TouchBall(MatchPlayer p, FootballActionKind kind, bool record = true)
        {
            if (record) State.Ball.RecordTouch(p.Team, p.PlayerId);
            MarkBall(p, kind);
            Emit(GameplayEventKind.BallTouched, p.Team, p.PlayerId, State.Ball.Physics.Position, kind.ToString());
        }

        private void SetBallVelocity(Vec3 velocity)
        {
            Vec3 delta = velocity - State.Ball.Physics.Velocity;
            BallSimulator.ApplyVelocityChange(ref State.Ball.Physics, delta);
            if (velocity.Y > 0f) State.Ball.Physics.IsGrounded = false;
        }

        // ------------------------------------------------------------------ kicks: passes, shots, chip, clearance, goalkeeper distribution

        private ActionResult ResolveKick(ActionResult res, MatchPlayer p, ActionRequest r, Vec2 dir, float power, SkillEffect style = SkillEffect.None)
        {
            MatchBall b = State.Ball;
            bool carrier = IsCarrier(p);

            if (r.Kind == FootballActionKind.GoalkeeperDistribution)
            {
                if (!p.InGoal) return Reject(res, ActionRejection.NotGoalkeeper);
                if (!carrier) return Reject(res, ActionRejection.NotInPossession);
            }
            if (!carrier)
            {
                if (b.Mode == BallMode.Dead || b.Mode == BallMode.Goal) return Reject(res, ActionRejection.MatchNotLive);
                if (b.HasPossession) return Reject(res, ActionRejection.BallNotAvailable);
                if (BallDistanceTo(p) > actions.KickReach || BallHeight() > actions.MaxTouchHeight) return Reject(res, ActionRejection.BallOutOfReach);
            }

            ActionProfile prof = p.ActionProfile;
            Vec2 facing = p.Locomotion.FacingDirection;
            ActionFoot foot = ActionModel.ChooseFoot(prof, facing, dir, actions, out float footFactor);
            float stamina = StaminaFactorOf(p);
            float baseQuality, minSpeed, maxSpeed, loft, maxError, extraError = 0f, cost, curve = 0f;
            ActionDetail detail = ActionDetail.Kicked;
            GameplayEventKind eventKind = GameplayEventKind.PassPerformed;
            string eventDetail = r.Kind.ToString();

            switch (r.Kind)
            {
                // Phase 20: the short pass is an ordinary pass. It had no case of its own and fell into the goalkeeper-distribution branch below, which rated it with the emergency-keeper profile (x0.75)
                case FootballActionKind.ShortPass:
                    baseQuality = prof.PassQuality; minSpeed = actions.PassMinSpeed; maxSpeed = actions.PassMaxSpeed; loft = 0f; maxError = actions.PassMaxError; cost = actions.PassStaminaCost;
                    break;
                case FootballActionKind.WallPass:
                    baseQuality = prof.PassQuality; minSpeed = actions.PassMinSpeed; maxSpeed = actions.PassMaxSpeed; loft = 0f; maxError = actions.PassMaxError; cost = actions.PassStaminaCost;
                    detail = ActionDetail.Wall;
                    break;
                case FootballActionKind.ThroughPass:
                    baseQuality = prof.PassQuality; minSpeed = actions.PassMinSpeed * actions.ThroughSpeedBoost; maxSpeed = actions.PassMaxSpeed * actions.ThroughSpeedBoost; loft = 0f;
                    maxError = actions.PassMaxError; extraError = actions.ThroughExtraError; cost = actions.PassStaminaCost;
                    detail = ActionDetail.Through;
                    break;
                case FootballActionKind.LongPass:
                case FootballActionKind.SkillLobPass:
                    baseQuality = prof.PassQuality; minSpeed = actions.LongPassMinSpeed; maxSpeed = actions.LongPassMaxSpeed; loft = actions.LongPassLoft; maxError = actions.LongPassMaxError; cost = actions.PassStaminaCost;
                    detail = ActionDetail.Lofted;
                    break;
                case FootballActionKind.Chip:
                    baseQuality = ShotQualityAt(prof, p); minSpeed = actions.ChipMinSpeed; maxSpeed = actions.ChipMaxSpeed; loft = actions.ChipVerticalSpeed / Math.Max(1f, ActionModel.Lerp(actions.ChipMinSpeed, actions.ChipMaxSpeed, power));
                    maxError = actions.ChipMaxError; cost = actions.ShotStaminaCost;
                    detail = ActionDetail.Lofted; eventKind = GameplayEventKind.ShotPerformed;
                    break;
                case FootballActionKind.Shot:
                case FootballActionKind.CurvedShot:
                    baseQuality = ShotQualityAt(prof, p); minSpeed = actions.ShotMinSpeed; maxSpeed = actions.ShotMaxSpeed; loft = actions.ShotLoft; maxError = actions.ShotMaxError; cost = actions.ShotStaminaCost;
                    eventKind = GameplayEventKind.ShotPerformed;
                    curve = ActionModel.Clamp01(Math.Abs(r.Curve)) * (r.Curve < 0f ? -1f : 1f);
                    float limit = r.Kind == FootballActionKind.CurvedShot ? 1f : actions.BasicCurveLimit;
                    curve = Math.Max(-limit, Math.Min(limit, curve));
                    if (curve != 0f) { extraError = actions.CurveExtraError * Math.Abs(curve); loft = Math.Max(loft, actions.CurveLift); detail = ActionDetail.Curved; }
                    break;
                case FootballActionKind.Clearance:
                    baseQuality = prof.ClearanceQuality; minSpeed = actions.ClearanceMinSpeed; maxSpeed = actions.ClearanceMaxSpeed; loft = actions.ClearanceLoft; maxError = actions.ClearanceMaxError; cost = actions.ClearanceStaminaCost;
                    detail = ActionDetail.Lofted; eventKind = GameplayEventKind.ClearancePerformed;
                    break;
                default: // goalkeeper distribution (only GoalkeeperDistribution reaches it)
                    baseQuality = r.Long ? actions.KeeperLongKickShare * prof.KeeperKicking + (1f - actions.KeeperLongKickShare) * prof.KeeperDistribution : actions.KeeperShortDistributionShare * prof.KeeperDistribution + (1f - actions.KeeperShortDistributionShare) * prof.KeeperKicking;
                    minSpeed = r.Long ? actions.LongPassMinSpeed : actions.PassMinSpeed; maxSpeed = r.Long ? actions.LongPassMaxSpeed : actions.PassMaxSpeed;
                    loft = r.Long ? actions.LongPassLoft : 0f; maxError = r.Long ? actions.LongPassMaxError : actions.PassMaxError; cost = actions.PassStaminaCost;
                    detail = r.Long ? ActionDetail.Lofted : ActionDetail.Kicked;
                    break;
            }

            // the skills of the kicker (none = the kick above, unchanged): quality, error, speed, loft, aim and cost
            KickSkillAdjust skill = KickSkillsOf(p, r, style, carrier, dir, curve);
            minSpeed *= skill.SpeedFactor; maxSpeed *= skill.SpeedFactor; loft *= skill.LoftFactor; maxError *= skill.ErrorFactor; extraError *= skill.ExtraErrorFactor; cost += skill.ExtraCost;

            float quality = SkillQuality(ActionModel.Clamp01(baseQuality * footFactor * stamina), (baseQuality + skill.QualityBonus) * footFactor * stamina);
            if (IsPassKick(r.Kind) && actions.PassPressureEffect > 0f) quality *= 1f - ActionModel.Clamp01(actions.PassPressureEffect * PassPressureOf(p) * SkillPressureLeft(p));
            float noise = Noise();
            float errorDegrees = ActionModel.KickError(maxError, quality, power, extraError, noise, actions) + noise * skill.ResidualErrorDegrees;
            Vec2 aimed = ActionModel.Rotate(dir, errorDegrees + skill.AimDegrees);
            float speed = ActionModel.Lerp(minSpeed, maxSpeed, power) * (actions.KickSpeedQualityBase + (1f - actions.KickSpeedQualityBase) * quality);
            Vec3 velocity = new Vec3(aimed.X * speed, speed * loft, aimed.Y * speed);

            if (carrier) ClearPossession();
            SetBallVelocity(velocity);
            b.Physics.Spin = curve != 0f ? new Vec3(0f, curve * actions.CurveSpinRate * (actions.CurveSpinQualityBase + (1f - actions.CurveSpinQualityBase) * quality), 0f) : Vec3.Zero;
            b.Mode = (eventKind == GameplayEventKind.PassPerformed) ? BallMode.InPass : (eventKind == GameplayEventKind.ClearancePerformed ? BallMode.Free : BallMode.InShot);
            if (b.Mode == BallMode.Free && velocity.Magnitude > actions.FreeBallSpeed) b.Mode = BallMode.InPass;

            TouchBall(p, r.Kind);
            Emit(eventKind, p.Team, p.PlayerId, b.Physics.Position, eventDetail);
            p.KickCooldownRemaining = actions.KickCooldownSeconds;

            res.Outcome = ActionOutcome.Completed;
            res.Detail = detail;
            res.Foot = foot;
            res.WeakFoot = IsWeakFoot(prof, foot);
            res.Quality = quality;
            res.SkillId = skill.SkillId;
            res.Direction = aimed;
            res.BallVelocity = velocity;
            res.StaminaCost = SpendStamina(p, cost * (actions.PowerCostBase + (1f - actions.PowerCostBase) * power));
            res.GainedPossession = false;
            return res;
        }

        private static bool IsPassKick(FootballActionKind kind)
        {
            return kind == FootballActionKind.ShortPass || kind == FootballActionKind.WallPass || kind == FootballActionKind.ThroughPass || kind == FootballActionKind.LongPass || kind == FootballActionKind.SkillLobPass;
        }

        /// <summary>
        /// 0..2: the rivals that stand close to a passer (Phase 20). The two nearest opponents inside <see cref="ActionTuning.PressureRadius"/> count, each 1 when he is at his feet and 0 at the radius. Plain proximity: it does not need the
        /// opponent to have asked for the Pressure action (the AI closes down by moving), so it is the same for a person's pass and for the AI's.
        /// </summary>
        private float PassPressureOf(MatchPlayer p)
        {
            TeamMatchState others = State.Team(p.Team.Opponent());
            float radius = actions.PressureRadius, first = 0f, second = 0f;
            for (int i = 0; i < others.Players.Count; i++)
            {
                MatchPlayer o = others.Players[i];
                if (!o.IsActive) continue;
                float near = 1f - (o.Locomotion.Position - p.Locomotion.Position).Magnitude / radius;
                if (near <= 0f) continue;
                if (near > first) { second = first; first = near; }
                else if (near > second) second = near;
            }
            return first + second;
        }

        private float ShotQualityAt(in ActionProfile prof, MatchPlayer p)
        {
            Vec2 goal = GoalCentreOf(p.Team);
            float distance = (goal - Flat(State.Ball.Physics.Position)).Magnitude;
            return distance <= actions.ShotCloseRange ? prof.ShotQualityClose : prof.ShotQualityFar;
        }

        // ------------------------------------------------------------------ control, shield, pressure

        private ActionResult ResolveControlAction(ActionResult res, MatchPlayer p)
        {
            MatchBall b = State.Ball;
            if (IsCarrier(p)) return Complete(res, ActionDetail.CleanControl);
            if (b.HasPossession) return Reject(res, ActionRejection.BallNotAvailable);
            if (BallDistanceTo(p) > actions.KickReach || BallHeight() > actions.MaxTouchHeight) return Reject(res, ActionRejection.BallOutOfReach);
            ActionResult touch = FirstTouch(p, actions.ControlActionBonus, true);
            touch.Direction = res.Direction;
            touch.Intensity = res.Intensity;
            return touch;
        }

        private ActionResult ResolveShield(ActionResult res, MatchPlayer p)
        {
            if (!IsCarrier(p)) return Reject(res, ActionRejection.NotInPossession);
            p.ShieldRemaining = actions.ShieldSeconds;
            res.Outcome = ActionOutcome.Accepted;
            res.Detail = ActionDetail.Shielding;
            return res;
        }

        private ActionResult ResolvePressure(ActionResult res, MatchPlayer p)
        {
            if (IsCarrier(p)) return Reject(res, ActionRejection.BallNotAvailable);
            p.PressureRemaining = actions.PressureSeconds * SkillPressureSecondsFactor(p);
            res.Outcome = ActionOutcome.Accepted;
            res.Detail = ActionDetail.Pressing;
            return res;
        }

        // ------------------------------------------------------------------ first touch (automatic and deliberate)

        /// <summary>
        /// A player touches the ball without kicking it. The result is decided by the first-touch score (<see cref="ActionModel.FirstTouchScore"/>): controlled, heavy touch (still his, but it runs away) or
        /// deflected. A goalkeeper in goal is judged on his handling and reflexes, and may catch or parry; a ball he cannot reach in time is simply not touched (returns a Rejected result).
        /// </summary>
        private ActionResult FirstTouch(MatchPlayer p, float bonus, bool deliberate)
        {
            NotePenaltyContact(p);
            MatchBall b = State.Ball;
            bool keeper = p.InGoal;
            ActionProfile prof = p.ActionProfile;
            Vec2 facing = p.Locomotion.FacingDirection;
            Vec2 ballXY = Flat(b.Physics.Position);
            Vec2 toBall = ballXY - p.Locomotion.Position;
            Vec2 toBallDir = ActionModel.DirectionOr(toBall, facing);
            float angle = ActionModel.SignedAngle(facing, toBallDir);
            float footFactor = 1f;
            ActionFoot foot = keeper ? ActionFoot.Body : ActionModel.ChooseFoot(prof, facing, toBallDir, actions, out footFactor);

            Vec2 relative = Flat(b.Physics.Velocity) - p.Locomotion.Velocity;
            float vy = b.Physics.Velocity.Y;
            float relSpeed = (float)Math.Sqrt(relative.X * relative.X + relative.Y * relative.Y + vy * vy) * PenaltyKeeperSpeedFactor(p);

            var res = new ActionResult { Team = p.Team, PlayerId = p.PlayerId, Foot = foot, Direction = toBallDir };
            float score;
            if (keeper)
            {
                float quality = actions.KeeperHandlingShare * prof.KeeperHandling + (1f - actions.KeeperHandlingShare) * prof.KeeperReflexes;
                float eased = relSpeed * (actions.KeeperReflexEaseBase - actions.KeeperReflexEaseRange * prof.KeeperReflexes);
                score = ActionModel.FirstTouchScore(quality, 1f, StaminaFactorOf(p), eased, 0f, bonus, Noise(), actions);
            }
            else
            {
                score = ActionModel.FirstTouchScore(prof.ControlQuality, footFactor, StaminaFactorOf(p), relSpeed, angle, bonus + SkillFirstTouchBonus(p), Noise(), actions);
            }
            res.Quality = ActionModel.Clamp01(score);
            res.Intensity = ActionModel.Clamp01(relSpeed / actions.FirstTouchMaxBallSpeed);

            if (score >= actions.CleanControlThreshold)
            {
                SettleBall(p);
                res.Kind = keeper ? FootballActionKind.GoalkeeperCatch : FootballActionKind.Control;
                res.Outcome = ActionOutcome.Completed;
                res.Detail = keeper ? ActionDetail.Caught : ActionDetail.CleanControl;
                res.GainedPossession = true;
                TouchBall(p, res.Kind, false);
                GiveBallTo(p.Team, p.PlayerId);
                return Finish(res);
            }

            if (keeper)
            {
                if (score >= actions.HeavyTouchThreshold)
                {
                    // parry: the ball is pushed away, slower and to a side
                    Vec2 away = ActionModel.Rotate(ActionModel.DirectionOr(-Flat(b.Physics.Velocity), -AttackDirectionOf(p.Team)), (Noise() >= 0f ? 1f : -1f) * (actions.ParryAngleMinDegrees + actions.ParryAngleRangeDegrees * Math.Abs(Noise())));
                    float speed = Math.Max(actions.DeflectSpeed, b.Physics.Speed * actions.KeeperParryFactor);
                    SetBallVelocity(new Vec3(away.X * speed, actions.ParryLift, away.Y * speed));
                    res.Kind = FootballActionKind.GoalkeeperSave;
                    res.Outcome = ActionOutcome.Completed;
                    res.Detail = ActionDetail.Parried;
                    res.Direction = away;
                    res.BallVelocity = b.Physics.Velocity;
                    ReleaseTouchedBall(p, res.Kind, true);
                    return Finish(res);
                }
                return default(ActionResult) /* beaten: no touch */;
            }

            if (score >= actions.HeavyTouchThreshold)
            {
                // heavy touch: the ball is his but it is pushed ahead, to roughly FirstTouchDistance in front of him
                float heavySpeed = Math.Min(actions.ControlSpeed, (float)Math.Sqrt(2f * ballParameters.RollingDeceleration * actions.FirstTouchDistance));
                Vec2 push = facing * heavySpeed + p.Locomotion.Velocity;
                SetBallVelocity(new Vec3(push.X, 0f, push.Y));
                State.Ball.Physics.Position = new Vec3(State.Ball.Physics.Position.X, ballParameters.Radius, State.Ball.Physics.Position.Z);
                res.Kind = FootballActionKind.Control;
                res.Outcome = ActionOutcome.Completed;
                res.Detail = ActionDetail.HeavyTouch;
                res.BallVelocity = b.Physics.Velocity;
                ReleaseTouchedBall(p, res.Kind, false);
                return Finish(res);
            }

            // bad touch: the ball bounces off him
            Vec2 off = ActionModel.DirectionOr(toBall, ActionModel.DirectionOr(-Flat(b.Physics.Velocity), facing));
            Vec2 carry = Flat(b.Physics.Velocity) * actions.DeflectCarryShare;
            SetBallVelocity(new Vec3(off.X * actions.DeflectSpeed + carry.X, Math.Max(0f, vy * actions.DeflectLiftShare), off.Y * actions.DeflectSpeed + carry.Y));
            res.Kind = FootballActionKind.Control;
            res.Outcome = ActionOutcome.Failed;
            res.Detail = ActionDetail.Deflected;
            res.Direction = off;
            res.BallVelocity = b.Physics.Velocity;
            ReleaseTouchedBall(p, res.Kind, true);
            return Finish(res);
        }

        private ActionResult Finish(ActionResult res)
        {
            LastActionResult = res;
            ActionPerformed?.Invoke(res);
            return res;
        }

        private void ReleaseTouchedBall(MatchPlayer p, FootballActionKind kind, bool deflected)
        {
            ClearPossession();
            State.Ball.Mode = deflected ? BallMode.Deflected : BallMode.Free;
            TouchBall(p, kind);
            if (deflected) Emit(GameplayEventKind.BallDeflected, p.Team, p.PlayerId, State.Ball.Physics.Position, kind.ToString());
        }

        /// <summary>The ball is brought down to the ground and takes the player's velocity: a cleanly controlled touch.</summary>
        private void SettleBall(MatchPlayer p)
        {
            Vec3 v = new Vec3(p.Locomotion.Velocity.X, 0f, p.Locomotion.Velocity.Y);
            State.Ball.Physics.Velocity = v;
            State.Ball.Physics.Position = new Vec3(State.Ball.Physics.Position.X, ballParameters.Radius, State.Ball.Physics.Position.Z);
            State.Ball.Physics.Spin = Vec3.Zero;
            State.Ball.Physics.IsGrounded = true;
        }

        // ------------------------------------------------------------------ defending

        private MatchPlayer OpponentCarrierOf(MatchPlayer p)
        {
            MatchPlayer c = BallCarrier;
            return c != null && c.Team != p.Team ? c : null;
        }

        private float DuelNoise() { return Noise() * actions.TackleNoise; }

        private static bool FromBehind(MatchPlayer attacker, MatchPlayer carrier)
        {
            Vec2 toAttacker = attacker.Locomotion.Position - carrier.Locomotion.Position;
            Vec2 f = carrier.Locomotion.FacingDirection;
            return f.X * toAttacker.X + f.Y * toAttacker.Y < 0f;
        }

        private ActionResult ResolveTackle(ActionResult res, MatchPlayer p, Vec2 dir, bool slide)
        {
            MatchPlayer target = OpponentCarrierOf(p);
            float reach = slide ? actions.SlideReach : actions.TackleReach;
            bool inReach = target != null && BallDistanceTo(p) <= reach;

            if (!inReach)
            {
                if (!slide) return Reject(res, ActionRejection.NoTarget);
                // a slide with nobody to tackle: it goes in anyway, and may poke a free ball
                float cost0 = SpendStamina(p, actions.SlideStaminaCost);
                StartSlide(p, dir);
                res.StaminaCost = cost0;
                if (target == null && !State.Ball.HasPossession && BallDistanceTo(p) <= reach && BallHeight() <= actions.MaxTouchHeight)
                {
                    SetBallVelocity(new Vec3(dir.X * actions.SlideSpeed, 0f, dir.Y * actions.SlideSpeed));
                    ReleaseTouchedBall(p, res.Kind, false);
                    State.Ball.Mode = BallMode.Deflected;
                    res.BallVelocity = State.Ball.Physics.Velocity;
                    return Complete(res, ActionDetail.Loose);
                }
                res.Outcome = ActionOutcome.Accepted;
                res.Detail = ActionDetail.Sliding;
                return res;
            }

            res.OtherPlayerId = target.PlayerId;
            float cost = SpendStamina(p, slide ? actions.SlideStaminaCost : actions.TackleStaminaCost);
            res.StaminaCost = cost;
            float attack = p.ActionProfile.TackleSkill * PerformanceOf(p) + SkillTackleBonus(p, target, slide);
            float defend = target.ActionProfile.DribbleSkill * PerformanceOf(target) + (target.IsShielding ? actions.ShieldBonus : 0f) + SkillResistBonus(target);
            float margin = attack - defend + DuelNoise() + (slide ? actions.SlideBonus : 0f);
            res.Quality = ActionModel.Clamp01(actions.QualityReportBase + margin);
            if (slide) StartSlide(p, dir);

            return ResolveDuel(res, p, target, margin, (slide ? actions.SlideFoulMargin : actions.StandingFoulMargin) + SkillFoulShift(p), GameplayEventKind.TacklePerformed);
        }

        private ActionResult ResolveChallenge(ActionResult res, MatchPlayer p)
        {
            MatchPlayer target = OpponentCarrierOf(p);
            if (target == null) return Reject(res, ActionRejection.NoTarget);
            if ((target.Locomotion.Position - p.Locomotion.Position).Magnitude > actions.ChallengeReach) return Reject(res, ActionRejection.NoTarget);

            res.OtherPlayerId = target.PlayerId;
            res.StaminaCost = SpendStamina(p, actions.ChallengeStaminaCost);
            float attack = p.ActionProfile.ChallengeStrength * PerformanceOf(p);
            float defend = target.ActionProfile.ResistStrength * PerformanceOf(target) + (target.IsShielding ? actions.ShieldBonus : 0f) + SkillResistBonus(target);
            float margin = attack - defend + DuelNoise();
            res.Quality = ActionModel.Clamp01(actions.QualityReportBase + margin);
            return ResolveDuel(res, p, target, margin, actions.StandingFoulMargin, GameplayEventKind.TacklePerformed);
        }

        /// <summary>
        /// The common end of a duel for the ball: won cleanly (the ball is his), won loose (the carrier loses it, nobody has it), lost (nothing, he is unbalanced) or a foul.
        /// Phase 21: where the mode has fouls (Classic) the REFEREE judges the contact first (<see cref="RefereeDecisionSystem"/>): a challenge that played the ball first is clean; a late or dangerous one is a foul, whatever the
        /// duel would otherwise have given him; a foul takes the ball from no one (the fouled team keeps it) and may carry a card.
        /// </summary>
        private ActionResult ResolveDuel(ActionResult res, MatchPlayer attacker, MatchPlayer carrier, float margin, float foulMargin, GameplayEventKind eventKind)
        {
            MatchBall b = State.Ball;
            bool won = margin >= actions.TackleCleanMargin, loose = !won && margin >= actions.TackleDeflectMargin;
            if (rules.HasFouls)
            {
                ContactEvidence seen = EvidenceOf(attacker, carrier, res.Kind == FootballActionKind.SlidingTackle, won || loose, margin <= foulMargin);
                RefereeDecision call = RefereeDecisionSystem.Decide(seen, RefereeStyle, RefereeTuning);
                if (call.IsFoul)
                {
                    assessmentHint = RefereeDecisionSystem.Decide(seen, RefereeStyle.Neutral, RefereeTuning); hasAssessmentHint = true;
                    attacker.ActionLockRemaining = Math.Max(attacker.ActionLockRemaining, actions.FailedTackleLockSeconds * SkillFailLockFactor(attacker));
                    res.Outcome = ActionOutcome.Failed;
                    res.Detail = ActionDetail.Foul;
                    CallFoul(attacker.Team, attacker.PlayerId, carrier.PlayerId, call, b.Physics.Position);
                    Emit(eventKind, attacker.Team, attacker.PlayerId, b.Physics.Position, res.Kind + ":Lost", carrier.PlayerId);
                    return res;
                }
                NoteUncalledContactForVar(attacker, carrier, seen, Flat(b.Physics.Position));
            }

            if (won)
            {
                TouchBall(attacker, res.Kind, false);
                GiveBallTo(attacker.Team, attacker.PlayerId);
                res.GainedPossession = true;
                Emit(eventKind, attacker.Team, attacker.PlayerId, b.Physics.Position, res.Kind + ":Won", carrier.PlayerId);
                return Complete(res, ActionDetail.Won);
            }
            if (loose)
            {
                Vec2 away = ActionModel.DirectionOr(Flat(b.Physics.Position) - attacker.Locomotion.Position, attacker.Locomotion.FacingDirection);
                SetBallVelocity(new Vec3(away.X * actions.ChallengeLooseSpeed, 0f, away.Y * actions.ChallengeLooseSpeed));
                res.BallVelocity = b.Physics.Velocity;
                ReleaseTouchedBall(attacker, res.Kind, false);
                b.Mode = BallMode.Deflected;
                Emit(eventKind, attacker.Team, attacker.PlayerId, b.Physics.Position, res.Kind + ":Loose", carrier.PlayerId);
                return Complete(res, ActionDetail.Loose);
            }

            attacker.ActionLockRemaining = Math.Max(attacker.ActionLockRemaining, actions.FailedTackleLockSeconds * SkillFailLockFactor(attacker));
            res.Outcome = ActionOutcome.Failed;
            Emit(eventKind, attacker.Team, attacker.PlayerId, b.Physics.Position, res.Kind + ":Lost", carrier.PlayerId);
            return res;
        }

        /// <summary>What the referee sees of a duel (Phase 21): plain geometry and state, nothing random.</summary>
        private ContactEvidence EvidenceOf(MatchPlayer attacker, MatchPlayer carrier, bool slide, bool touchedBallFirst, bool clumsy)
        {
            RefereeTuning t = RefereeTuning;
            Vec2 toCarrier = carrier.Locomotion.Position - attacker.Locomotion.Position;
            float distance = toCarrier.Magnitude;
            float top = attacker.LocomotionProfile.MaxSpeed;
            float closing = 0f;
            if (distance > 1e-4f && top > 0f)
            {
                Vec2 relative = attacker.Locomotion.Velocity - carrier.Locomotion.Velocity;
                closing = Math.Max(0f, (relative.X * toCarrier.X + relative.Y * toCarrier.Y) / distance) / top;
            }

            // the goal the carrier attacks, and the outfield defenders of the challenger's team that stand between
            Vec2 goal = GoalCentreOf(carrier.Team);
            Vec2 toGoal = goal - carrier.Locomotion.Position;
            float goalDistance = toGoal.Magnitude;
            bool chance = false, counter = false;
            if (goalDistance > 1e-4f)
            {
                Vec2 heading = carrier.Locomotion.Velocity.Magnitude > t.HeadingMinSpeed ? carrier.Locomotion.Velocity.Normalized : carrier.Locomotion.FacingDirection;
                float facingGoal = (heading.X * toGoal.X + heading.Y * toGoal.Y) / goalDistance;
                if (goalDistance <= t.GoalChanceDistance && facingGoal >= t.GoalChanceFacingCosine)
                {
                    chance = true;
                    TeamMatchState defenders = State.Team(attacker.Team);
                    for (int i = 0; i < defenders.Players.Count; i++)
                    {
                        MatchPlayer o = defenders.Players[i];
                        if (o == attacker || !o.IsActive || o.InGoal) continue;
                        if ((goal - o.Locomotion.Position).Magnitude < goalDistance) { chance = false; break; }
                    }
                }
                float carrierTop = carrier.LocomotionProfile.MaxSpeed;
                counter = State.Ball.SecondsSincePossessionChange <= t.CounterPossessionSeconds && carrierTop > 0f && carrier.Locomotion.CurrentSpeed >= t.CounterCarrierSpeed * carrierTop && facingGoal >= t.CounterFacingCosine;
            }

            return new ContactEvidence
            {
                TouchedBallFirst = touchedBallFirst, Contact = distance <= t.ContactDistance, IsSlide = slide, FromBehind = FromBehind(attacker, carrier), ClosingSpeed01 = closing,
                Clumsy = clumsy, StoppedCounterAttack = counter, DeniedGoalChance = chance, PriorFouls = attacker.FoulsCommitted
            };
        }

        private void StartSlide(MatchPlayer p, Vec2 dir)
        {
            p.SlideDirection = dir;
            p.SlideRemaining = actions.SlideLockSeconds;
            p.ActionLockRemaining = actions.SlideLockSeconds;
        }

        private ActionResult ResolveIntercept(ActionResult res, MatchPlayer p, bool block)
        {
            MatchBall b = State.Ball;
            if (b.Mode == BallMode.Dead || b.Mode == BallMode.Goal) return Reject(res, ActionRejection.MatchNotLive);
            if (b.HasPossession) return Reject(res, ActionRejection.BallNotAvailable);
            if (b.HasLastTouch && b.LastTouchTeam == p.Team && (b.Mode == BallMode.InPass || b.Mode == BallMode.InShot)) return Reject(res, ActionRejection.BallNotAvailable);
            if (BallDistanceTo(p) > actions.InterceptReach * SkillInterceptReachFactor(p) || BallHeight() > actions.MaxTouchHeight * actions.InterceptHeightFactor) return Reject(res, ActionRejection.BallOutOfReach);

            res.StaminaCost = SpendStamina(p, actions.InterceptStaminaCost);
            float speed = b.Physics.Speed;
            float difficulty = ActionModel.Clamp01(speed / actions.ShotSpeedForBlock) * actions.InterceptDifficultyWeight;
            float score = p.ActionProfile.InterceptQuality * PerformanceOf(p) + SkillInterceptBonus(p, block) - difficulty + Noise() * actions.InterceptNoise;
            res.Quality = ActionModel.Clamp01(score + actions.QualityReportBase);
            bool tooFastToTake = speed > actions.ShotSpeedForBlock;

            if (!block && !tooFastToTake && score >= actions.InterceptCleanThreshold)
            {
                SettleBall(p);
                res.GainedPossession = true;
                TouchBall(p, res.Kind, false);
                GiveBallTo(p.Team, p.PlayerId);
                Emit(GameplayEventKind.InterceptionPerformed, p.Team, p.PlayerId, b.Physics.Position, "Intercepted");
                return Complete(res, ActionDetail.Intercepted);
            }
            if (score >= actions.InterceptBlockThreshold)
            {
                Vec2 incoming = Flat(b.Physics.Velocity);
                Vec2 away = ActionModel.Rotate(ActionModel.DirectionOr(-incoming, -AttackDirectionOf(p.Team)), Noise() * actions.BlockAngleSpreadDegrees);
                float newSpeed = Math.Max(actions.DeflectSpeed, speed * actions.BlockSpeedFactor);
                SetBallVelocity(new Vec3(away.X * newSpeed, Math.Min(actions.BlockMaxLift, speed * actions.BlockLiftShare), away.Y * newSpeed));
                res.BallVelocity = b.Physics.Velocity;
                res.Direction = away;
                ReleaseTouchedBall(p, res.Kind, true);
                Emit(GameplayEventKind.InterceptionPerformed, p.Team, p.PlayerId, b.Physics.Position, "Blocked");
                return Complete(res, ActionDetail.Blocked);
            }
            res.Outcome = ActionOutcome.Failed;
            return res;
        }

        // ------------------------------------------------------------------ goalkeeper rush (contract only)

        private ActionResult ResolveRush(ActionResult res, MatchPlayer p)
        {
            if (!p.InGoal) return Reject(res, ActionRejection.NotGoalkeeper);
            MatchBall b = State.Ball;
            if (b.HasPossession && b.PossessingTeam == p.Team) return Reject(res, ActionRejection.BallNotAvailable);
            if (BallDistanceTo(p) > actions.RushMaxDistance) return Reject(res, ActionRejection.BallOutOfReach);
            p.RushRemaining = actions.RushSeconds;
            res.StaminaCost = SpendStamina(p, actions.RushStaminaCost);
            res.Outcome = ActionOutcome.Accepted;
            res.Detail = ActionDetail.Rushing;
            return res;
        }

        // ------------------------------------------------------------------ every fixed step

        /// <summary>The timers of the actions and the slide. Returns true when the player's movement was handled here (he is sliding or locked).</summary>
        private bool StepActionMovement(MatchPlayer p, float dt)
        {
            StepSkillTimers(p, dt);
            if (p.KickCooldownRemaining > 0f) p.KickCooldownRemaining = Math.Max(0f, p.KickCooldownRemaining - dt);
            if (p.ShieldRemaining > 0f) p.ShieldRemaining = Math.Max(0f, p.ShieldRemaining - dt);
            if (p.RushRemaining > 0f) p.RushRemaining = Math.Max(0f, p.RushRemaining - dt);
            if (p.PressureRemaining > 0f)
            {
                p.PressureRemaining = Math.Max(0f, p.PressureRemaining - dt);
                p.Locomotion.CurrentStamina = LocomotionStamina.Clamp(p.Locomotion.CurrentStamina - actions.PressureDrainPerSecond * SkillPressureDrainFactor(p) * dt, locomotion);
            }

            if (p.SlideRemaining > 0f)
            {
                float k = p.SlideRemaining / actions.SlideLockSeconds;
                float speed = actions.SlideSpeed * k;
                p.Locomotion.Heading = p.SlideDirection.ToHeading();
                p.Locomotion.CurrentSpeed = speed;
                p.Locomotion.Velocity = p.SlideDirection * speed;
                p.Locomotion.Position = p.Locomotion.Position + p.Locomotion.Velocity * dt;
                p.Locomotion.InputMagnitude = 0f;
                p.SlideRemaining = Math.Max(0f, p.SlideRemaining - dt);
                p.ActionLockRemaining = Math.Max(0f, p.ActionLockRemaining - dt);
                return true;
            }
            if (p.ActionLockRemaining > 0f)
            {
                p.ActionLockRemaining = Math.Max(0f, p.ActionLockRemaining - dt);
                LocomotionStepper.Step(ref p.Locomotion, p.LocomotionProfile, locomotion, Vec2.Zero, dt);
                return true;
            }
            return false;
        }

        /// <summary>The ball against the players, after everybody has moved: the carried ball is kept close (or lost); a loose ball is touched by whoever reaches it.</summary>
        private void StepBallInteractions(float dt)
        {
            MatchBall b = State.Ball;
            b.SecondsSinceTouch = Math.Min(999f, b.SecondsSinceTouch + dt);
            b.SecondsSincePossessionChange = Math.Min(999f, b.SecondsSincePossessionChange + dt);

            if (b.Mode == BallMode.Controlled && b.HasPossession)
            {
                StepDribble(dt);
                return;
            }

            if ((b.Mode == BallMode.InPass || b.Mode == BallMode.InShot || b.Mode == BallMode.Deflected || b.Mode == BallMode.Rebound) &&
                b.Physics.IsGrounded && b.Physics.HorizontalSpeed < actions.FreeBallSpeed)
                b.Mode = BallMode.Free;

            if (b.Mode == BallMode.Dead || b.Mode == BallMode.Goal) return;
            // Phase 18: a ball that has already crossed the goal line completely is a goal (the judgement comes right after this step): nobody, the keeper included, plays it any more
            if (actions.KeeperHoldsBallOnThePitch > 0f && MatchRules.HasCrossedGoalLine(field, new BallObservation(b.Physics.Position, ballParameters.Radius))) return;

            MatchPlayer best = null;
            float bestDistance = float.MaxValue;
            Vec2 ballXY = Flat(b.Physics.Position);
            float height = BallHeight();
            for (int side = 0; side < 2; side++)
            {
                TeamMatchState team = side == 0 ? State.Home : State.Away;
                for (int i = 0; i < team.Players.Count; i++)
                {
                    MatchPlayer p = team.Players[i];
                    if (!p.IsActive || ShootoutTakerExcluded(p)) continue;
                    if (b.HasLastTouch && b.LastTouchTeam == p.Team && b.LastTouchPlayerId == p.PlayerId && b.SecondsSinceTouch < actions.ReTouchCooldown) continue;
                    // Phase 14: the ball he has just kicked is not his again while his kick is still recovering, even if somebody else touched it in between (a team-mate's control, an opponent's block): before, the
                    // re-touch cooldown above followed the LAST toucher, so one touch by anyone else let the kicker take his own pass back on the very next step
                    if (actions.KickerKeepsOffHisKick > 0f && p.KickCooldownRemaining > 0f) continue;
                    bool keeper = p.InGoal;
                    float reach = keeper ? actions.TouchReach * (actions.KeeperReachBase + actions.KeeperReachRange * p.ActionProfile.KeeperDiving) : actions.TouchReach;
                    reach = PenaltyKeeperReach(p, reach);
                    float maxHeight = keeper ? field.GoalHeight : actions.MaxTouchHeight;
                    if (height > maxHeight) continue;
                    float d = (ballXY - p.Locomotion.Position).Magnitude;
                    if (d > reach || d >= bestDistance) continue;
                    best = p;
                    bestDistance = d;
                }
            }
            if (best == null || PenaltyBlocksTouch(best)) return;
            FirstTouch(best, 0f, false);
        }

        /// <summary>
        /// 0..1: how well this player keeps the ball while he runs with it: his control and dribbling, his tiredness, minus the pressure of up to two opponents who are pressing close,
        /// plus the shield. It scales how fast the ball can follow him (<see cref="ActionTuning.CatchUpAcceleration"/>); it never teleports the ball.
        /// </summary>
        public float DribbleControlQuality(MatchPlayer p)
        {
            int counted = 0;
            float pressing = 0f;
            TeamMatchState others = State.Team(p.Team.Opponent());
            for (int i = 0; i < others.Players.Count && counted < 2; i++)
            {
                MatchPlayer o = others.Players[i];
                if (o.IsActive && o.IsPressing && (o.Locomotion.Position - p.Locomotion.Position).Magnitude <= actions.PressureRadius * SkillPressureRadiusFactor(o)) { counted++; pressing += SkillPresserWeight(o); }
            }
            ActionProfile prof = p.ActionProfile;
            float q = actions.DribbleControlShare * prof.ControlQuality + (1f - actions.DribbleControlShare) * prof.DribbleSkill;
            return ActionModel.Clamp01(q * StaminaFactorOf(p) - actions.PressureEffect * pressing * SkillPressureLeft(p) + (p.IsShielding ? actions.ShieldBonus : 0f) + SkillCarryBonus(p));
        }

        private void StepDribble(float dt)
        {
            MatchBall b = State.Ball;
            MatchPlayer p = BallCarrier;
            if (p == null || !p.IsActive)
            {
                ReleaseBall();
                return;
            }

            if (HoldsSetPieceBall(p))
            {
                // Phase 28: a throw-in, goal kick or corner is played from the spot: the ball waits there until the taker kicks it
                SetPieceState sp = State.SetPiece;
                b.Physics.Position = new Vec3(sp.Spot.X, ballParameters.Radius, sp.Spot.Y);
                b.Physics.Velocity = Vec3.Zero;
                b.Physics.IsGrounded = true;
                return;
            }

            Vec2 ballXY = Flat(b.Physics.Position);
            Vec2 toBall = ballXY - p.Locomotion.Position;
            if (toBall.Magnitude > actions.LooseBallDistance)
            {
                // too far from his feet: the control is lost
                ReleaseBall();
                b.SecondsSinceTouch = 0f;
                return;
            }

            float q = DribbleControlQuality(p);
            float scale = actions.CatchUpFloor + (1f - actions.CatchUpFloor) * q;

            Vec2 facing = p.Locomotion.FacingDirection;
            Vec2 target = p.Locomotion.Position + facing * (actions.ControlDistance + actions.LeadPerSpeed * p.Locomotion.CurrentSpeed);
            Vec2 wanted = p.Locomotion.Velocity + (target - ballXY) * actions.PullGain;
            Vec2 current = new Vec2(b.Physics.Velocity.X, b.Physics.Velocity.Z);
            Vec2 delta = wanted - current;
            float limit = actions.CatchUpAcceleration * scale * dt;
            float mag = delta.Magnitude;
            if (mag > limit && mag > 0f) delta = delta * (limit / mag);
            b.Physics.Velocity = new Vec3(current.X + delta.X, 0f, current.Y + delta.Y);
            b.Physics.Position = new Vec3(b.Physics.Position.X, ballParameters.Radius, b.Physics.Position.Z);
            b.Physics.IsGrounded = true;
            if (p.InGoal && actions.KeeperHoldsBallOnThePitch > 0f) KeepHeldBallOnThePitch(b);
        }

        /// <summary>
        /// Phase 18: the ball in a goalkeeper's hands stays on the pitch. He may be carried back by the momentum of his dive, but the ball he holds does not cross his own goal line with him: its whole width stays at most
        /// touching the line, and what pushed it outwards is cancelled. (A ball that crosses the line completely while it is NOT held is judged as always.)
        /// </summary>
        private void KeepHeldBallOnThePitch(MatchBall b)
        {
            float limit = field.Length * 0.5f - ballParameters.Radius;
            Vec3 pos = b.Physics.Position;
            if (Math.Abs(pos.X) <= limit) return;
            float sign = pos.X > 0f ? 1f : -1f;
            Vec3 vel = b.Physics.Velocity;
            b.Physics.Position = new Vec3(sign * limit, pos.Y, pos.Z);
            if (vel.X * sign > 0f) b.Physics.Velocity = new Vec3(0f, vel.Y, vel.Z);
        }
    }
}
