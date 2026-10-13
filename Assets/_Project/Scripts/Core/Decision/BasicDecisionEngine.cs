using System;

namespace FS27.Core
{
    /// <summary>
    /// The FIRST level of decision (Phase 8): one player looks at the world through a <see cref="WorldView"/>, recognises his situation (has the ball or not, is he the nearest, is he under pressure, is there
    /// a team-mate to pass to, is there room to run, is there a shot) and answers with a <see cref="PlayerDecision"/> carrying a <see cref="MovementIntent"/> and/or an <see cref="ActionIntent"/>.
    /// It is a short, fixed hierarchy, NOT a tactical AI: no formations as tactics, no marking, no cover, no pressing, no transitions, no scoring of futures.
    /// It is a pure function of (view, player): it reads only the view (never a definition, an attribute or the match), changes nothing, uses no chance and no time. The same view and the same
    /// player always give the same decision. The Core stays the authority: the intents it returns go through locomotion and the action resolver like any other.
    /// </summary>
    public sealed class BasicDecisionEngine
    {
        private readonly PitchBounds bounds;
        private readonly PlayerObservation obs = new PlayerObservation();
        private readonly RolePositioningTuning pos;

        // The tunings of the mode being played (Phase 13): Arena and Classic share this engine and every rule; only their numbers differ. A tuning given to the constructor is used for BOTH modes.
        private sealed class ModeSet
        {
            public BasicDecisionTuning T; public DefenseTuning D; public AttackSupport Attack; public BallCarrierEvaluator Carrier; public AttackPositioningTuning AT; public BallCarrierTuning CT; public GoalkeeperAiTuning K;
        }

        private readonly ModeSet arenaSet, classicSet;
        // the sets of a team that plays with a difficulty profile (Phase 14): [mode, team]; null = the common set of the mode
        private readonly ModeSet[,] profiledSets = new ModeSet[2, 2];
        private readonly DecisionProfile[] profiles = new DecisionProfile[2];
        private DecisionProfile profile;
        private ModeSet current;
        private BasicDecisionTuning t;
        private DefenseTuning d;
        private AttackSupport attack;
        private BallCarrierEvaluator carrier;
        private GoalkeeperAiTuning keeperAi;

        private bool collectRejections;
        /// <summary>The mistake the last decision made on purpose, if any (Phase 14). Read by the diagnostics; no rule reads it.</summary>
        public DecisionError LastError;
        /// <summary>Metres the last decision was off the ideal place because of the level's positioning (0 when none).</summary>
        public float LastDrift;
        public BasicDecisionTuning Tuning => t;
        public DefenseTuning DefenseTuning => d;
        /// <summary>The attack without the ball (Phase 10): who does which job, and where each stands (of the mode last played).</summary>
        public AttackSupport Attack => attack;
        /// <summary>How the carrier compares what he could do (Phase 11) (of the mode last played).</summary>
        public BallCarrierEvaluator Carrier => carrier;

        /// <summary>Count the rejections of the carrier's candidates (diagnostics), for both modes.</summary>
        public bool CollectRejections
        {
            get { return arenaSet.Carrier.CollectRejections; }
            set
            {
                collectRejections = value; arenaSet.Carrier.CollectRejections = value; classicSet.Carrier.CollectRejections = value;
                for (int m = 0; m < 2; m++) for (int k = 0; k < 2; k++) if (profiledSets[m, k] != null) profiledSets[m, k].Carrier.CollectRejections = value;
            }
        }

        /// <summary>Which tunings the engine reads from now on (it does this by itself at every decision, from the match it is shown).</summary>
        public void UseMode(GameMode mode)
        {
            current = mode == GameMode.Arena ? arenaSet : classicSet;
            t = current.T; d = current.D; attack = current.Attack; carrier = current.Carrier; keeperAi = current.K; profile = null;
        }

        /// <summary>The tunings of the team of the player who decides: the common ones of the mode, or the ones its difficulty profile gives it (Phase 14).</summary>
        private void UseTeam(GameMode mode, TeamSide team)
        {
            DecisionProfile p = profiles[(int)team];
            if (p == null) return;
            current = profiledSets[mode == GameMode.Arena ? 0 : 1, (int)team];
            t = current.T; d = current.D; attack = current.Attack; carrier = current.Carrier; keeperAi = current.K; profile = p;
        }

        /// <summary>The difficulty profile of a team, or null (decides as in Phase 13). Changing it never changes the players, only how they choose.</summary>
        public DecisionProfile ProfileOf(TeamSide team) { return profiles[(int)team]; }

        public void SetProfile(TeamSide team, DecisionProfile p)
        {
            profiles[(int)team] = p;
            for (int m = 0; m < 2; m++)
            {
                profiledSets[m, (int)team] = p == null ? null : BuildProfiledSet(m == 0 ? arenaSet : classicSet, m == 0 ? GameMode.Arena : GameMode.Classic, p);
                if (profiledSets[m, (int)team] != null) profiledSets[m, (int)team].Carrier.CollectRejections = collectRejections;
            }
        }

        private static ModeSet BuildProfiledSet(ModeSet common, GameMode mode, DecisionProfile p)
        {
            return new ModeSet
            {
                T = common.T, AT = common.AT, CT = common.CT, K = common.K, D = p.TuneDefense(common.D, mode),
                Attack = new AttackSupport(common.T, common.AT), Carrier = new BallCarrierEvaluator(common.T, common.AT, common.CT)
            };
        }

        public BasicDecisionEngine(PitchBounds bounds, BasicDecisionTuning tuning = null, DefenseTuning defense = null, RolePositioningTuning positioning = null, AttackPositioningTuning attackTuning = null, BallCarrierTuning carrierTuning = null, GoalkeeperAiTuning keeperTuning = null)
        {
            this.bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
            pos = positioning ?? new RolePositioningTuning();
            arenaSet = BuildSet(GameMode.Arena, tuning, defense, attackTuning, carrierTuning, keeperTuning);
            classicSet = BuildSet(GameMode.Classic, tuning, defense, attackTuning, carrierTuning, keeperTuning);
            UseMode(GameMode.Classic);
        }

        private static ModeSet BuildSet(GameMode mode, BasicDecisionTuning tuning, DefenseTuning defense, AttackPositioningTuning attackTuning, BallCarrierTuning carrierTuning, GoalkeeperAiTuning keeperTuning)
        {
            BasicDecisionTuning bt = tuning ?? BasicDecisionTuning.ForMode(mode);
            AttackPositioningTuning at = attackTuning ?? AttackPositioningTuning.ForMode(mode);
            BallCarrierTuning ct = carrierTuning ?? BallCarrierTuning.ForMode(mode);
            return new ModeSet { T = bt, AT = at, CT = ct, K = keeperTuning ?? GoalkeeperAiTuning.ForMode(mode), D = defense ?? DefenseTuning.ForMode(mode), Attack = new AttackSupport(bt, at), Carrier = new BallCarrierEvaluator(bt, at, ct) };
        }

        public PlayerDecision Decide(WorldView view, string playerId)
        {
            UseMode(view.Match.Mode);
            int idx = view.IndexOf(playerId);
            if (idx < 0) return PlayerDecision.Idle(playerId, DecisionReason.NotOnPitch);
            WorldPlayer me = view[idx];
            LastError = default(DecisionError); LastDrift = 0f;
            UseTeam(view.Match.Mode, me.Team);
            stamina = me.Stamina;
            if (!me.IsOnPitch) return PlayerDecision.Idle(playerId, DecisionReason.NotOnPitch);
            if (!view.Match.BallLive) return PlayerDecision.Idle(playerId, DecisionReason.NotPlaying);
            if (view.Match.PenaltyKeeperId != null && view.Match.PenaltyKeeperId == playerId) return PlayerDecision.Idle(playerId, DecisionReason.PenaltyHold);
            if (me.ActionLocked) return PlayerDecision.Idle(playerId, DecisionReason.Locked);
            view.Observe(idx, obs);
            if (me.IsGoalkeeper) return Keeper(view, idx, me);
            return me.HasBall ? WithBall(view, idx, me) : WithoutBall(view, idx, me);
        }

        // ------------------------------------------------------------------ geometry shared by the rules

        private static Vec2 AttackDirection(WorldView view, TeamSide team)
        {
            Vec2 d = WorldGeometry.AttackGoal(view.Match, view.Field, team) - WorldGeometry.OwnGoal(view.Match, view.Field, team);
            return d.Normalized;
        }

        private static Vec2 Lerp(Vec2 a, Vec2 b, float k) { return a + (b - a) * k; }

        private float stamina;

        /// <summary>The effort of a movement nothing depends on (Phase 14): a level that spends its legs well does not sprint to reposition. Essential movements (going for the ball, closing the carrier) are not capped.</summary>
        private float Effort(float magnitude, bool essential)
        {
            return profile == null || essential ? magnitude : Math.Min(magnitude, profile.EffortCap(stamina));
        }

        private PlayerDecision MoveTo(string playerId, Vec2 me, Vec2 target, float magnitude, DecisionKind kind, DecisionReason reason)
        {
            magnitude = Effort(magnitude, kind == DecisionKind.MoveToBall);
            Vec2 goal = bounds.ClampPlayer(target);
            if ((goal - me).Magnitude <= t.IdleRadius) return new PlayerDecision { PlayerId = playerId, Kind = DecisionKind.Idle, Reason = DecisionReason.ArrivedAtPoint, HasMovement = true, Movement = MovementIntent.Stand };
            return new PlayerDecision { PlayerId = playerId, Kind = kind, Reason = reason, HasMovement = true, Movement = MovementIntent.TowardsTarget(me, goal, magnitude, t.ArriveRadius) };
        }

        /// <summary>Did this player play the pass that is still travelling? (the ball is in a pass and his was the last touch; once it is deflected, free or won, it no longer is.)</summary>
        private static bool JustPassed(in WorldBall ball, in WorldPlayer p)
        {
            return ball.Mode == BallMode.InPass && ball.HasLastTouch && ball.LastTouchTeam == p.Team && ball.LastTouchPlayerId == p.PlayerId;
        }

        /// <summary>Am I the field player of my team nearest to the ball? (the goalkeeper in goal does not count; ties go to the earlier in the squad)</summary>
        private bool IsNearestFieldPlayerToBall(WorldView view, int idx, in WorldPlayer me)
        {
            float mine = WorldGeometry.DistanceToBall(me, view.Ball);
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team != me.Team || o.IsGoalkeeper || (t.PasserStaysOffHisPass && JustPassed(view.Ball, o))) continue;
                float d = WorldGeometry.DistanceToBall(o, view.Ball);
                if (d < mine || (d == mine && i < idx)) return false;
            }
            return true;
        }

        /// <summary>How many team-mates (field players, the carrier and me excluded) are nearer to the carrier than I am.</summary>
        private static int TeammatesNearerToCarrier(WorldView view, int idx, in WorldPlayer me, in WorldPlayer carrier, int carrierIdx)
        {
            float mine = (me.Position - carrier.Position).Magnitude;
            int n = 0;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx || i == carrierIdx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team != me.Team || o.IsGoalkeeper) continue;
                float d = (o.Position - carrier.Position).Magnitude;
                if (d < mine || (d == mine && i < idx)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ without the ball

        private PlayerDecision WithoutBall(WorldView view, int idx, in WorldPlayer me)
        {
            WorldBall ball = view.Ball;
            Vec2 home = me.HasHome ? me.Home : me.Position;
            bool nearest = IsNearestFieldPlayerToBall(view, idx, me);

            if (!ball.HasPossessor)
            {
                if (TryIntercept(view, idx, me, out PlayerDecision intercept)) return intercept;
                if (ball.IntendedReceiverId == me.PlayerId) return MoveTo(me.PlayerId, me.Position, ball.Ground, t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.ReceiveBall);
                if (t.PasserStaysOffHisPass && JustPassed(ball, me)) return Position(view, me, home, t.PositionBallPull, DecisionReason.AfterPass);
                if (nearest) return MoveTo(me.PlayerId, me.Position, ball.Ground, t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.ClosestToLooseBall);
                return Position(view, me, home, t.PositionBallPull, DecisionReason.LooseBallNotClosest);
            }

            int carrierIdx = view.IndexOf(ball.PossessorId);
            WorldPlayer carrier = view[carrierIdx];
            if (carrier.Team != me.Team) return Defend(view, idx, me, carrierIdx, carrier, home);
            if (view.Match.SetPieceTakerId != null && view.Match.SetPieceTakerId == carrier.PlayerId) return SetPieceOffer(view, me);

            // a team-mate has the ball
            if ((me.Position - home).Magnitude > t.FarFromHomeDistance) return MoveTo(me.PlayerId, me.Position, home, t.ChaseMagnitude, DecisionKind.MoveToPoint, DecisionReason.FarFromHome);
            if (attack.Tuning.Enabled) return AttackWithoutBall(view, idx, me, carrierIdx, home);
            float toCarrier = (me.Position - carrier.Position).Magnitude;
            if (toCarrier <= t.SupportRadius && TeammatesNearerToCarrier(view, idx, me, carrier, carrierIdx) < t.SupportCount)
                return MoveTo(me.PlayerId, me.Position, SupportPoint(view, me, carrier, home), t.PositionMagnitude, DecisionKind.Support, DecisionReason.SupportRole);
            return Position(view, me, home, t.PositionBallPull, DecisionReason.TeammateHasBall);
        }

        /// <summary>
        /// A team-mate has the ball (Phase 10): the player does the job he is given (support, run forward, width) or gets open near his place, always at the point the open-point search finds. The decision keeps the
        /// job's kind even when he has arrived (he then stands), so what each player is doing can be read.
        /// </summary>
        private PlayerDecision AttackWithoutBall(WorldView view, int idx, in WorldPlayer me, int carrierIdx, Vec2 home)
        {
            attack.Assign(view, carrierIdx);
            AttackJob job = attack.JobOf(idx);
            if (job == AttackJob.None) return Position(view, me, home, t.PositionBallPull, DecisionReason.TeammateHasBall);
            AttackPlan plan = attack.Plan(view, idx, carrierIdx, job, bounds.ClampPlayer(HomeTarget(view, home, t.PositionBallPull)));
            DecisionKind kind; DecisionReason reason;
            switch (plan.Job)
            {
                case AttackJob.Support: kind = DecisionKind.Support; reason = DecisionReason.SupportRole; break;
                case AttackJob.Forward: kind = DecisionKind.ForwardRun; reason = DecisionReason.ForwardRunRole; break;
                case AttackJob.Width: kind = DecisionKind.HoldWidth; reason = DecisionReason.HoldWidthRole; break;
                default: kind = DecisionKind.GetOpen; reason = DecisionReason.GetOpenRole; break;
            }
            Vec2 goal = bounds.ClampPlayer(plan.Target);
            bool arrived = (goal - me.Position).Magnitude <= t.IdleRadius;
            float magnitude = Effort(plan.Job == AttackJob.Forward && view.Ball.SecondsSincePossessionChange < attack.Tuning.RecoverySeconds ? t.ChaseMagnitude : plan.Magnitude, false);
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = kind, Reason = reason, HasMovement = true,
                Movement = arrived ? MovementIntent.Stand : MovementIntent.TowardsTarget(me.Position, goal, magnitude, t.ArriveRadius)
            };
        }

        /// <summary>
        /// The home controller: a place near his home, drawn towards the ball but never further than <see cref="RolePositioningTuning.HomeMaxDeviation"/> from it (and drawn less when the ball is far),
        /// so he is neither frozen nor glued to the ball, and does not abandon his zone. A player far from home goes back.
        /// </summary>
        private PlayerDecision Position(WorldView view, in WorldPlayer me, Vec2 home, float pull, DecisionReason reason)
        {
            if ((me.Position - home).Magnitude > t.FarFromHomeDistance) return MoveTo(me.PlayerId, me.Position, home, t.ChaseMagnitude, DecisionKind.MoveToPoint, DecisionReason.FarFromHome);
            return MoveTo(me.PlayerId, me.Position, HomeTarget(view, home, pull) + Drift(me, view), t.PositionMagnitude, DecisionKind.MoveToPoint, reason);
        }

        private Vec2 HomeTarget(WorldView view, Vec2 home, float pull)
        {
            Vec2 toBall = view.Ball.Ground - home;
            float d = toBall.Magnitude;
            float far = Math.Min(1f, d / pos.HomeBallRange);
            float k = pull * (1f - far * (1f - pos.HomeFarPullShare));
            Vec2 dev = toBall * k;
            float len = dev.Magnitude;
            if (len > pos.HomeMaxDeviation) dev = dev * (pos.HomeMaxDeviation / len);
            return home + dev;
        }

        // ------------------------------------------------------------------ defending (the opponent has the ball)

        private PlayerDecision Defend(WorldView view, int idx, in WorldPlayer me, int carrierIdx, in WorldPlayer carrier, Vec2 home)
        {
            DefenseRoles.Rank(view, me.Team, carrier, d, out int primary, out int secondary);
            if (idx == primary) return Primary(view, me, carrier);
            if (idx == secondary) return Secondary(view, me, carrier, home);

            // the others: back to their place, at full speed during the first moments after losing the ball
            if (view.Ball.SecondsSincePossessionChange < d.TransitionSeconds && (me.Position - home).Magnitude > t.IdleRadius)
                return MoveTo(me.PlayerId, me.Position, HomeTarget(view, home, t.DefendBallPull), t.ChaseMagnitude, DecisionKind.MoveToPoint, DecisionReason.TransitionReturn);
            if (me.Stamina < d.PressMinStamina) return Position(view, me, home, t.DefendBallPull, DecisionReason.TiredReturnHome);
            return Position(view, me, home, t.DefendBallPull, DecisionReason.OpponentHasBall);
        }

        private PlayerDecision Primary(WorldView view, in WorldPlayer me, in WorldPlayer carrier)
        {
            Vec2 ball = view.Ball.Ground;
            float toCarrier = (me.Position - carrier.Position).Magnitude;
            float toBall = (ball - me.Position).Magnitude;
            Vec2 fromCarrier = toCarrier > 0f ? (me.Position - carrier.Position) * (1f / toCarrier) : Vec2.Zero;
            bool behind = carrier.Facing.X * fromCarrier.X + carrier.Facing.Y * fromCarrier.Y < d.BehindCosine;
            bool canAct = me.CanAct && !me.KickCooldown;
            Vec2 toBallDir = ActionModel.DirectionOr(ball - me.Position, me.Facing);

            if (canAct)
            {
                if (toBall <= d.TackleDistance && !behind)
                    return Contest(me, DecisionKind.Tackle, FootballActionKind.Tackle, toBallDir, DecisionReason.TackleInRange);
                if (SlideWindow(view, me, carrier, toBall, toCarrier, behind))
                    return Contest(me, DecisionKind.SlidingTackle, FootballActionKind.SlidingTackle, toBallDir, DecisionReason.SlideWindow);
                if (toCarrier <= d.ChallengeDistance && ChallengeRisk(me, carrier, fromCarrier) <= d.ChallengeMaxRisk * DisciplineRiskScale(me))
                    return Contest(me, DecisionKind.Challenge, FootballActionKind.PhysicalChallenge, ActionModel.DirectionOr(carrier.Position - me.Position, me.Facing), DecisionReason.ChallengeContact);
            }

            float magnitude = me.Stamina < d.TiredStamina ? d.TiredMagnitude : (toCarrier <= d.PressDistance ? d.PressMagnitude : d.CloseMagnitude);
            Vec2 target = ball;
            if (toCarrier > d.ApproachDistance)
            {
                Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
                target = carrier.Position + ActionModel.DirectionOr(own - carrier.Position, me.Facing) * d.GoalSideStandOff;
            }
            Vec2 goal = bounds.ClampPlayer(target);
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.CloseDown, Reason = DecisionReason.PrimaryDefender, HasMovement = true, Movement = MovementIntent.TowardsTarget(me.Position, goal, magnitude, t.ArriveRadius) };
        }

        /// <summary>
        /// Phase 21: how much of his usual risk a defender accepts given his cards: all of it with a clean sheet, <see cref="DefenseTuning.YellowRiskScale"/> of it with a yellow, and (almost) none when he has a yellow and has
        /// already committed <see cref="DefenseTuning.NearRedFouls"/> fouls: one more card and he is off. Where the mode has no cards nobody has any, so nothing changes.
        /// </summary>
        private float DisciplineRiskScale(in WorldPlayer me)
        {
            if (me.YellowCards <= 0) return 1f;
            return me.FoulsCommitted >= d.NearRedFouls ? d.NearRedRiskScale : d.YellowRiskScale;
        }

        /// <summary>
        /// How risky a physical challenge is for this defender against this carrier: 0 from the front or the side, up to 1 from straight behind, more when the carrier is shielding the ball and when the defender comes in fast.
        /// A challenge from behind is the kind that becomes a foul where the mode has fouls; <paramref name="fromCarrier"/> is the unit vector from the carrier to the defender.
        /// </summary>
        private float ChallengeRisk(in WorldPlayer me, in WorldPlayer carrier, Vec2 fromCarrier)
        {
            float behind = -(carrier.Facing.X * fromCarrier.X + carrier.Facing.Y * fromCarrier.Y);
            if (behind <= 0f) return 0f;
            Vec2 closingVelocity = me.Velocity - carrier.Velocity;
            float closing = me.MaxSpeed > 0f ? Math.Max(0f, -(closingVelocity.X * fromCarrier.X + closingVelocity.Y * fromCarrier.Y)) / me.MaxSpeed : 0f;
            return behind * (1f + (carrier.IsShielding ? d.ChallengeShieldRisk : 0f) + closing * d.ChallengeClosingRisk);
        }

        private PlayerDecision Contest(in WorldPlayer me, DecisionKind kind, FootballActionKind action, Vec2 dir, DecisionReason reason)
        {
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = kind, Reason = reason, HasAction = true, Action = new ActionIntent(me.Team, me.PlayerId, action, dir, ActionDefaults.DefaultPower),
                HasMovement = true, Movement = MovementIntent.Stand
            };
        }

        /// <summary>
        /// A sliding tackle is rare: the ball just out of standing reach, the carrier running at the defender's goal with some speed, the defender in the way (inside the cone of the carrier's path), enough stamina,
        /// and not from behind. The owner of the aggressive-tackle skill slides from a little further. Nothing else.
        /// </summary>
        private bool SlideWindow(WorldView view, in WorldPlayer me, in WorldPlayer carrier, float toBall, float toCarrier, bool behind)
        {
            if (behind || me.Stamina < d.SlideMinStamina || carrier.Speed < d.SlideMinCarrierSpeed) return false;
            if (d.CardedNeverSlide > 0f && me.YellowCards > 0) return false;   // Phase 21: a booked player does not slide
            float max = d.SlideMaxDistance + (me.Skills != null && me.Skills.Has(SkillEffect.AggressiveTackle) ? d.AggressiveSlideBonus : 0f);
            if (toBall < d.SlideMinDistance || toBall > max) return false;
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            Vec2 toGoal = ActionModel.DirectionOr(own - carrier.Position, Vec2.Zero);
            Vec2 heading = carrier.Velocity.Normalized;
            if (heading.X * toGoal.X + heading.Y * toGoal.Y < d.SlideClosingCosine) return false;
            return WorldGeometry.AngleBetween(heading, me.Position - carrier.Position) <= d.SlideMaxAngleDegrees;
        }

        private PlayerDecision Secondary(WorldView view, in WorldPlayer me, in WorldPlayer carrier, Vec2 home)
        {
            if (me.Stamina < d.TiredStamina) return Position(view, me, home, t.DefendBallPull, DecisionReason.TiredReturnHome);
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            Vec2 target = carrier.Position + ActionModel.DirectionOr(own - carrier.Position, me.Facing) * d.SecondaryStandOff;
            Vec2 goal = bounds.ClampPlayer(target + Drift(me, view));
            if ((goal - me.Position).Magnitude <= t.IdleRadius) return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Idle, Reason = DecisionReason.SecondaryDefender, HasMovement = true, Movement = MovementIntent.Stand };
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.CloseDown, Reason = DecisionReason.SecondaryDefender, HasMovement = true, Movement = MovementIntent.TowardsTarget(me.Position, goal, Effort(d.SecondaryMagnitude, false), t.ArriveRadius) };
        }

        // ------------------------------------------------------------------ interception of a ball travelling to an opponent

        private readonly Vec2[] samples = new Vec2[16];

        /// <summary>
        /// A ball an opponent has just played, still travelling: where will it be in the next moments (the reference ball model, on a copy)? The field player of the team who can get there first goes for
        /// it, and plays the interception when it is in reach; nobody else does. The owner of the interception-reading skill looks further ahead. Reachability = distance minus reach against speed x time.
        /// </summary>
        private bool TryIntercept(WorldView view, int idx, in WorldPlayer me, out PlayerDecision decision)
        {
            decision = default(PlayerDecision);
            WorldBall ball = view.Ball;
            if (me.IsGoalkeeper || !ball.HasLastTouch || ball.LastTouchTeam == me.Team || ball.Speed < d.InterceptMinBallSpeed) return false;
            if (ball.IntendedReceiverId != null && view.TryGetPlayer(ball.IntendedReceiverId, out WorldPlayer receiver) && receiver.Team == me.Team) return false;

            int count = FillSamples(view, me);
            int mine = Arrival(me, count);
            if (mine < 0) return false;
            // only the best placed field player of the team goes (earliest arrival; ties: the earlier in the squad)
            int start = view.TeamStart(me.Team), end = start + view.TeamCount(me.Team);
            for (int i = start; i < end; i++)
            {
                if (i == idx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.IsGoalkeeper) continue;
                int other = Arrival(o, FillSamples(view, o));
                if (other >= 0 && (other < mine || (other == mine && i < idx))) return false;
            }
            Vec2 target = samples[mine];
            bool inReach = (ball.Ground - me.Position).Magnitude <= d.InterceptActionDistance && ball.Height <= d.InterceptMaxHeight && me.CanAct && !me.KickCooldown;
            decision = new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Intercept, Reason = DecisionReason.InterceptReachable, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, bounds.ClampPlayer(target), t.ChaseMagnitude, 0f)
            };
            if (inReach) { decision.HasAction = true; decision.Action = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Intercept, ActionModel.DirectionOr(ball.Ground - me.Position, me.Facing), ActionDefaults.DefaultPower); }
            return true;
        }

        private int FillSamples(WorldView view, in WorldPlayer who)
        {
            float horizon = d.InterceptHorizonSeconds + (who.Skills != null && who.Skills.Has(SkillEffect.InterceptionReading) ? d.InterceptSkillHorizonBonus : 0f);
            int n = 0;
            for (float time = d.InterceptStepSeconds; time <= horizon && n < samples.Length; time += d.InterceptStepSeconds)
            {
                Vec3 p = view.PredictBallPosition(time);
                samples[n++] = new Vec2(p.X, p.Z);
            }
            return n;
        }

        /// <summary>The first sample (index) the player can reach, or -1: his distance to it, less his reach, is within what he can run in that time.</summary>
        private int Arrival(in WorldPlayer who, int count)
        {
            for (int k = 0; k < count; k++)
            {
                float time = (k + 1) * d.InterceptStepSeconds;
                if ((samples[k] - who.Position).Magnitude - d.InterceptReachDistance <= who.MaxSpeed * d.InterceptSpeedShare * time) return k;
            }
            return -1;
        }

        /// <summary>Where a supporting player stands: between his home and the carrier, a little further up the pitch, never too close to him nor too far.</summary>
        private Vec2 SupportPoint(WorldView view, in WorldPlayer me, in WorldPlayer carrier, Vec2 home)
        {
            Vec2 attack = AttackDirection(view, me.Team);
            Vec2 p = Lerp(home, carrier.Position, t.SupportBallPull) + attack * t.SupportAdvance;
            Vec2 off = p - carrier.Position;
            float d = off.Magnitude;
            Vec2 dir = d > 1e-4f ? off * (1f / d) : new Vec2(0f, home.Y >= carrier.Position.Y ? 1f : -1f);
            if (d < t.SupportMinDistance) p = carrier.Position + dir * t.SupportMinDistance;
            else if (d > t.SupportMaxDistance) p = carrier.Position + dir * t.SupportMaxDistance;
            return p;
        }

        // ------------------------------------------------------------------ with the ball

        private PlayerDecision WithBall(WorldView view, int idx, in WorldPlayer me)
        {
            if (view.Match.FreeKickTakerId != null && view.Match.FreeKickTakerId == me.PlayerId) return TakeFreeKick(view, idx, me);
            if (view.Match.SetPieceTakerId != null && view.Match.SetPieceTakerId == me.PlayerId) return TakeSetPiece(view, idx, me);
            if (carrier.Tuning.Enabled) return CarrierDecision(view, idx, me);
            bool canKick = !me.KickCooldown;
            float nearestOpponent = obs.NearestOpponentDistance;
            bool pressed = nearestOpponent < t.PressureRadius;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);

            if (canKick && TryShoot(view, me, goal, nearestOpponent, out PlayerDecision shot)) return shot;

            if (canKick)
            {
                int receiver = BestReceiver(view, idx, me, pressed, false, out float gain);
                if (receiver < 0 && pressed) receiver = BestReceiver(view, idx, me, true, true, out gain);
                if (receiver >= 0) return PassTo(view, me, view[receiver], pressed ? DecisionReason.UnderPressurePass : DecisionReason.FreePass);
            }

            if (TryDribble(view, me, goal, canKick, pressed, out PlayerDecision dribble)) return dribble;
            if (pressed) return Protect(view, me);

            // nothing applies: keep moving towards the goal
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.NoActionFallback, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, bounds.ClampPlayer(goal), t.DribbleMagnitude, 0f)
            };
        }

        /// <summary>
        /// Phase 22: the taker of a free kick plays it, he does not carry it: a shot when he is near enough (the wall and the keeper are part of the shot, so the lane is not asked for; a curve when he may), otherwise a pass
        /// to the best team-mate, otherwise the ball forward. The AI knows no more than any player: it looks at the same view.
        /// </summary>
        private PlayerDecision TakeFreeKick(WorldView view, int idx, in WorldPlayer me)
        {
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            float distance = (goal - me.Position).Magnitude;
            if (distance <= t.FreeKickShootRange)
            {
                PlayerDecision shot = BuildShot(view, me, goal, distance);
                shot.Reason = DecisionReason.FreeKickShot;
                // something (the wall) stands on the lane to goal: over it, not into it
                if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, goal, t.ShootLaneClearance, true))
                {
                    shot.Action.Kind = FootballActionKind.Chip;
                    shot.Action.Curve = 0f;
                    shot.Action.Power = t.FreeKickLobPower;
                }
                return shot;
            }
            int receiver = BestReceiver(view, idx, me, false, false, out float gain);
            if (receiver < 0) receiver = BestReceiver(view, idx, me, true, true, out gain);
            if (receiver >= 0) return PassTo(view, me, view[receiver], DecisionReason.FreeKickPass);
            var clear = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Clearance, AttackDirection(view, me.Team), t.ShotPower);
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = DecisionReason.FreeKickClear, HasAction = true, Action = clear, HasMovement = true, Movement = MovementIntent.Stand };
        }

        private bool TryShoot(WorldView view, in WorldPlayer me, Vec2 goal, float nearestOpponent, out PlayerDecision decision)
        {
            decision = default(PlayerDecision);
            float distance = (goal - me.Position).Magnitude;
            if (distance > t.ShootRange || nearestOpponent < t.ShootMinOpponentDistance) return false;
            if (Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, goal)) > t.ShootMaxAngleDegrees) return false;
            if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, goal, t.ShootLaneClearance, true)) return false;
            decision = BuildShot(view, me, goal, distance);
            return true;
        }

        /// <summary>The shot itself: aimed at the side of the goal the keeper is not on, curved when the player may and the distance allows.</summary>
        private PlayerDecision BuildShot(WorldView view, in WorldPlayer me, Vec2 goal, float distance)
        {
            // aim for the side of the goal the goalkeeper is not on
            float side = 1f;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (o.IsOnPitch && o.Team != me.Team && o.IsGoalkeeper) { side = o.Position.Y >= goal.Y ? -1f : 1f; break; }
            }
            Vec2 aim = new Vec2(goal.X, goal.Y + side * t.ShotAimOffset);
            Vec2 dir = ActionModel.DirectionOr(aim - me.Position, ActionModel.DirectionOr(goal - me.Position, me.Facing));
            var intent = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Shot, dir, t.ShotPower);
            DecisionReason reason = DecisionReason.ClearShot;
            if (distance >= t.CurveMinDistance && view.CanUse(me.PlayerId, FootballActionKind.CurvedShot).Allowed)
            {
                intent.Kind = FootballActionKind.CurvedShot;
                intent.Curve = (me.PreferredFoot == PreferredFoot.Right ? 1f : -1f) * t.CurveAmount;
                reason = DecisionReason.CurvedShotAvailable;
            }
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Shoot, Reason = reason, HasAction = true, Action = intent, HasMovement = true, Movement = MovementIntent.Stand };
        }

        /// <summary>
        /// The best team-mate to pass to, or -1: on the pitch, not too near nor too far, with a clear lane, with space around him and (unless the carrier is under pressure) further forward than the carrier.
        /// The score weighs how far forward he is, how much space he has, how close he is to a comfortable distance and how much the carrier has to turn. The goalkeeper is only considered when asked.
        /// </summary>
        private int BestReceiver(WorldView view, int idx, in WorldPlayer me, bool pressed, bool allowKeeper, out float bestGain)
        {
            bestGain = 0f;
            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                float score = ReceiverScore.Evaluate(view, idx, i, pressed, allowKeeper, t, attack.Tuning, out float gain);
                if (score > bestScore) { bestScore = score; best = i; bestGain = gain; }
            }
            return best;
        }

        private PlayerDecision PassTo(WorldView view, in WorldPlayer me, in WorldPlayer receiver, DecisionReason reason, bool lofted = false)
        {
            Vec2 dir = ActionModel.DirectionOr(receiver.Position - me.Position, me.Facing);
            float d = (receiver.Position - me.Position).Magnitude;
            FootballActionKind kind = FootballActionKind.ShortPass;
            if (d >= t.LobPassDistance && view.CanUse(me.PlayerId, FootballActionKind.SkillLobPass).Allowed) { kind = FootballActionKind.SkillLobPass; reason = DecisionReason.LobPassAvailable; }
            else if (lofted || d >= t.LongPassDistance) kind = FootballActionKind.LongPass;
            float power = Math.Min(1f, t.PassPowerBase + (1f - t.PassPowerBase) * d / t.PassPowerRefDistance);
            if (t.PassPhysicsEnabled)
            {
                float planned = me.Profile.PassQuality * t.PlannedQualityShare;
                bool closeRival = WorldGeometry.OpponentsWithin(view, me.Team, receiver.Position, attack.Tuning.ReceiverPressureRadius) > 0;
                power = kind == FootballActionKind.ShortPass ? PassPhysics.GroundPower(d, closeRival ? t.PressedArrivalSpeed : t.GroundArrivalSpeed, planned, 1f) : PassPhysics.LoftedPower(d * t.LandingShare, planned);
            }
            var intent = new ActionIntent(me.Team, me.PlayerId, kind, dir, power) { Target = PassTarget.ToPlayer(receiver.PlayerId) };
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = reason, HasAction = true, Action = intent, TargetPlayerId = receiver.PlayerId, HasMovement = true, Movement = MovementIntent.Stand };
        }

        private bool TryDribble(WorldView view, in WorldPlayer me, Vec2 goal, bool canKick, bool pressed, out PlayerDecision decision)
        {
            decision = default(PlayerDecision);
            DribbleAssessment d = DribbleSpace.Assess(view, me, goal, canKick, t);
            if (d.SkillMove)
            {
                var move = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.SkillMove, d.SkillDirection, ActionDefaults.DefaultPower);
                decision = new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.SkillMoveAvailable, HasAction = true, Action = move, HasMovement = true, Movement = MovementIntent.Move(d.SkillDirection, t.DribbleMagnitude) };
                return true;
            }
            if (!d.Free) return false;
            Vec2 ahead = bounds.ClampPlayer(me.Position + d.Direction * t.DribbleLookahead);
            decision = new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.SpaceToDribble, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, ahead, t.DribbleMagnitude, 0f)
            };
            return true;
        }

        // ------------------------------------------------------------------ Phase 11: the carrier compares what he could do

        /// <summary>
        /// The player with the ball scores his options on one scale (<see cref="BallCarrierEvaluator"/>) and does the best one, building the intent with the same builders as before (the shot, the pass, the dribble,
        /// the shield). The runs his team-mates make come from the attack layer through <see cref="ITeammateRuns"/>.
        /// </summary>
        private PlayerDecision CarrierDecision(WorldView view, int idx, in WorldPlayer me)
        {
            bool canKick = !me.KickCooldown;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            ITeammateRuns runs = NoRuns.Instance;
            if (attack.Tuning.Enabled) { attack.Assign(view, idx); runs = attack; }
            if (profile != null) carrier.Bias = profile.BiasFor(me, view.Match, t.ShootRange, (goal - me.Position).Magnitude);
            ActionCandidate best = carrier.Evaluate(view, idx, runs, canKick);
            if (profile != null && carrier.HasSecond) best = ChooseWithMistakes(view, me, best);
            bool pressed = obs.NearestOpponentDistance < t.PressureRadius;
            PlayerDecision decision;
            switch (best.Type)
            {
                case CandidateType.Shoot:
                    return BuildShot(view, me, goal, (goal - me.Position).Magnitude);
                case CandidateType.Pass:
                case CandidateType.ForwardPass:
                    return PassTo(view, me, view[best.ReceiverIndex], best.Reason, best.Reason == DecisionReason.CrossToBox);
                case CandidateType.ThroughPass:
                    return ThroughTo(view, me, view[best.ReceiverIndex], best.Target);
                case CandidateType.Dribble:
                    if (TryDribble(view, me, goal, canKick, pressed, out decision)) return decision;
                    break;
                case CandidateType.Hold:
                    return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Hold, Reason = DecisionReason.HoldForRun, HasMovement = true, Movement = MovementIntent.Stand };
            }
            if (pressed && best.Type != CandidateType.Dribble) return Protect(view, me);
            if (best.Type == CandidateType.Protect) return Protect(view, me);
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.NoActionFallback, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, bounds.ClampPlayer(goal), t.DribbleMagnitude, 0f)
            };
        }

        /// <summary>
        /// The player of a level that makes mistakes (Phase 14) takes the second best option now and then, when it is close to the best. The mistake is recorded in <see cref="LastError"/>; the evaluator is told,
        /// so that what it reports is what was done.
        /// </summary>
        private ActionCandidate ChooseWithMistakes(WorldView view, in WorldPlayer me, ActionCandidate best)
        {
            ActionCandidate second = carrier.Second;
            float quality = ActionQualityOf(best.Type, me);
            int tick = (int)Math.Round(view.Match.MatchTime / LocomotionDefaults.FixedDeltaTime);
            if (!profile.ChoosesSecond(best, second, carrier.Summary.Pressure, me.Stamina / profile.Tuning.StaminaScale, quality, tick, me.Slot)) return best;
            LastError = new DecisionError { Kind = DecisionProfile.Classify(best, second), Gap = best.Score - second.Score, Pressure = carrier.Summary.Pressure, Stamina = me.Stamina, FreeWay = carrier.Summary.DribbleFree };
            carrier.Choose(second);
            return second;
        }

        private float ActionQualityOf(CandidateType type, in WorldPlayer me)
        {
            switch (type)
            {
                case CandidateType.Shoot: return me.Profile.ShotQualityClose;
                case CandidateType.Pass:
                case CandidateType.ForwardPass:
                case CandidateType.ThroughPass: return me.Profile.PassQuality;
                case CandidateType.Dribble: return me.Profile.DribbleSkill;
                default: return me.Profile.ControlQuality;
            }
        }

        /// <summary>Where the level's positioning puts a player off the place he should be in (nothing for a team without a profile).</summary>
        private Vec2 Drift(in WorldPlayer me, WorldView view)
        {
            if (profile == null) return Vec2.Zero;
            Vec2 v = profile.PositionDrift(me.Slot, view.Match.MatchTime, out float meters);
            LastDrift = v.Magnitude;
            return v;
        }

        /// <summary>A pass into the space a runner is running to: the ball goes to the point, the runner goes with it.</summary>
        private PlayerDecision ThroughTo(WorldView view, in WorldPlayer me, in WorldPlayer runner, Vec2 point)
        {
            Vec2 dir = ActionModel.DirectionOr(point - me.Position, me.Facing);
            float d = (point - me.Position).Magnitude;
            float power = Math.Min(1f, t.PassPowerBase + (1f - t.PassPowerBase) * d / t.PassPowerRefDistance);
            if (t.PassPhysicsEnabled) power = PassPhysics.GroundPower(d, t.ThroughArrivalSpeed, me.Profile.PassQuality * t.PlannedQualityShare, ActionDefaults.ThroughSpeedBoost);
            var intent = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.ThroughPass, dir, power) { Target = PassTarget.ToPosition(point) };
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = DecisionReason.SpacePass, HasAction = true, Action = intent, TargetPlayerId = runner.PlayerId, HasMovement = true, Movement = MovementIntent.Stand };
        }

        private PlayerDecision Protect(WorldView view, in WorldPlayer me)
        {
            Vec2 away = me.Facing * -1f;
            if (obs.OpponentCount > 0) away = ActionModel.DirectionOr(me.Position - view[obs.OpponentIndex(0)].Position, away);
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Protect, Reason = DecisionReason.UnderPressureProtect,
                HasAction = true, Action = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Shield, me.Facing, ActionDefaults.DefaultPower),
                HasMovement = true, Movement = MovementIntent.Move(away, t.ProtectAwayMagnitude)
            };
        }

        // ------------------------------------------------------------------ the goalkeeper in goal (a minimal rule)

        private PlayerDecision Keeper(WorldView view, int idx, in WorldPlayer me)
        {
            bool attacksPlusX = view.Match.HomeAttacksPositiveX;
            if (me.HasBall)
            {
                if (view.Match.SetPieceTakerId != null && view.Match.SetPieceTakerId == me.PlayerId) return TakeSetPiece(view, idx, me);
                if (keeperAi.Enabled && keeperAi.DistributionEnabled) return KeeperPlayBall(view, idx, me);
                return KeeperKick(view, me, BestReceiver(view, idx, me, true, false, out _));
            }

            if (!keeperAi.Enabled) return LegacyKeeper(view, me, attacksPlusX);
            return KeeperAi(view, me);
        }

        /// <summary>Phase 29: the keeper with the ball in his hands waits for a lane, plays short or long to the best option, or clears (see <see cref="GoalkeeperDistribution"/>).</summary>
        private PlayerDecision KeeperPlayBall(WorldView view, int idx, in WorldPlayer me)
        {
            GoalkeeperParameters level = profile != null && profile.Goalkeeper != null ? profile.Goalkeeper : NeutralKeeper;
            GoalkeeperSkill skill = GoalkeeperSkillModel.ResolveNormalized(level, me.Profile.KeeperReflexes, me.Profile.KeeperPositioning, me.Profile.KeeperCommand, me.Profile.KeeperDiving, me.Profile.KeeperHandling, keeperAi.Skill);
            GoalkeeperPlayChoice choice = GoalkeeperDistribution.Choose(view, idx, me, t, keeperAi, view.Ball.SecondsSinceTouch, skill.DecisionMaking);
            if (choice.Play == GoalkeeperPlay.Wait)
                return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Idle, Reason = DecisionReason.KeeperWaitForLane, HasMovement = true, Movement = MovementIntent.Stand };
            return KeeperKick(view, me, choice.Receiver);
        }

        /// <summary>The goalkeeper plays the ball he holds: to the team-mate (or, with nobody, long up the pitch), short or long by the distance, with the power the ball needs to get there.</summary>
        private PlayerDecision KeeperKick(WorldView view, in WorldPlayer me, int receiver)
        {
            Vec2 dir; string targetId = null;
            if (receiver >= 0) { dir = ActionModel.DirectionOr(view[receiver].Position - me.Position, me.Facing); targetId = view[receiver].PlayerId; }
            else dir = AttackDirection(view, me.Team);
            float d = receiver >= 0 ? (view[receiver].Position - me.Position).Magnitude : t.LongPassDistance;
            bool longKick = d >= t.LongPassDistance;
            float kickPower = t.KeeperDistributePower;
            if (t.PassPhysicsEnabled)
            {
                float keeperQuality = (longKick ? ActionDefaults.KeeperLongKickShare * me.Profile.KeeperKicking + (1f - ActionDefaults.KeeperLongKickShare) * me.Profile.KeeperDistribution
                                                : ActionDefaults.KeeperShortDistributionShare * me.Profile.KeeperDistribution + (1f - ActionDefaults.KeeperShortDistributionShare) * me.Profile.KeeperKicking) * t.PlannedQualityShare;
                kickPower = longKick ? PassPhysics.LoftedPower(d * t.LandingShare, keeperQuality) : PassPhysics.GroundPower(d, t.GroundArrivalSpeed, keeperQuality, 1f);
            }
            var intent = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.GoalkeeperDistribution, dir, kickPower) { Long = longKick };
            if (targetId != null) intent.Target = PassTarget.ToPlayer(targetId);
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = DecisionReason.KeeperDistribute, HasAction = true, Action = intent, TargetPlayerId = targetId, HasMovement = true, Movement = MovementIntent.Stand };
        }

        // ------------------------------------------------------------------ Phase 28: throw-ins, goal kicks and corners

        /// <summary>
        /// The taker of a throw-in, goal kick or corner plays the ball from the spot (it waits there, the rivals are kept out of the circle): every team-mate is an option worth (value of the pass) times (the chance it arrives,
        /// <see cref="PassRisk"/>); he plays the best one at once when it is safe enough, waits a moment for his team-mates to open a lane, then settles for the best that is still reasonable, and otherwise clears.
        /// He never carries the ball away and never plays into a rival who stands on the lane.
        /// </summary>
        private PlayerDecision TakeSetPiece(WorldView view, int idx, in WorldPlayer me)
        {
            WorldMatch m = view.Match;
            bool corner = m.SetPieceKind == RestartKind.Corner;
            Vec2 goal = WorldGeometry.AttackGoal(m, view.Field, me.Team);
            float fromGoal = (goal - me.Position).Magnitude;
            int best = -1; float bestUtility = float.NegativeInfinity, bestChance = 0f; bool bestLofted = false;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer r = view[i];
                if (!r.IsOnPitch || r.Team != me.Team || r.IsGoalkeeper) continue;
                float dist = (r.Position - me.Position).Magnitude;
                if (dist < t.PassMinDistance || dist > t.PassMaxDistance) continue;
                float toGoal = (goal - r.Position).Magnitude;
                bool lofted = corner && toGoal <= t.SetPieceBoxDepth;
                float chance = PassRisk.SuccessChance(view, me, r, dist, t, lofted);
                float gain = (fromGoal - toGoal) / view.Field.Length;
                float space = Math.Min(WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position), t.SetPieceSpaceCap) / t.SetPieceSpaceCap;
                float value = t.SetPieceBaseValue + t.SetPieceGainWeight * Math.Max(-t.SetPieceBackFloor, gain) + t.SetPieceSpaceWeight * space + (lofted ? t.SetPieceBoxBonus : 0f);
                float utility = chance * value;
                if (utility > bestUtility) { bestUtility = utility; best = i; bestChance = chance; bestLofted = lofted; }
            }
            bool waited = m.SetPieceSeconds >= t.SetPieceWaitSeconds;
            if (best >= 0 && (bestChance >= t.SetPieceMinChance || (waited && bestChance >= t.SetPieceFallbackChance)))
            {
                if (me.IsGoalkeeper) return KeeperKick(view, me, best);
                return PassTo(view, me, view[best], DecisionReason.SetPiecePass, bestLofted);
            }
            if (!waited)
                return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Idle, Reason = DecisionReason.SetPieceWait, HasMovement = true, Movement = MovementIntent.Stand };
            if (me.IsGoalkeeper) return KeeperKick(view, me, -1);
            Vec2 attackDir = AttackDirection(view, me.Team);
            Vec2 inward = new Vec2(0f, me.Position.Y > 0f ? -1f : 1f);
            Vec2 clearDir = ActionModel.DirectionOr(attackDir + inward * t.SetPieceClearInward, attackDir);
            var clear = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Clearance, clearDir, t.SetPieceClearPower);
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = DecisionReason.SetPieceClear, HasAction = true, Action = clear, HasMovement = true, Movement = MovementIntent.Stand };
        }

        /// <summary>A team-mate of the taker of a set piece stays where the set-up put him (an open lane for the taker) and only steps away from a rival who comes too close.</summary>
        private PlayerDecision SetPieceOffer(WorldView view, in WorldPlayer me)
        {
            float nearest = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, me.Position);
            if (nearest >= t.SetPieceOfferRivalDistance || obs.OpponentCount == 0)
                return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Idle, Reason = DecisionReason.SetPieceOffer, HasMovement = true, Movement = MovementIntent.Stand };
            Vec2 away = ActionModel.DirectionOr(me.Position - view[obs.OpponentIndex(0)].Position, me.Facing * -1f);
            Vec2 target = bounds.ClampPlayer(me.Position + away * t.SetPieceOfferStep);
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.MoveToPoint, Reason = DecisionReason.SetPieceOffer, HasMovement = true, Movement = MovementIntent.TowardsTarget(me.Position, target, t.PositionMagnitude, t.ArriveRadius) };
        }

        /// <summary>The goalkeeper rule of Phase 15 (kept to compare): stand on the line from his goal to the ball; chase the ball inside seven metres.</summary>
        private PlayerDecision LegacyKeeper(WorldView view, in WorldPlayer me, bool attacksPlusX)
        {
            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            Vec2 ball = view.Ball.Ground;
            if ((ball - me.Position).Magnitude <= t.KeeperReactDistance)
                return MoveTo(me.PlayerId, me.Position, bounds.ClampToGoalZone(ball, attacksPlusX, me.Team), t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.KeeperReact);
            Vec2 toBall = ActionModel.DirectionOr(ball - own, AttackDirection(view, me.Team));
            return MoveTo(me.PlayerId, me.Position, bounds.ClampToGoalZone(own + toBall * t.KeeperLineDepth, attacksPlusX, me.Team), t.PositionMagnitude, DecisionKind.MoveToPoint, DecisionReason.KeeperHold);
        }

        /// <summary>A team without a difficulty profile: the defaults of the parameters, which are the reference level's.</summary>
        private static readonly GoalkeeperParameters NeutralKeeper = new GoalkeeperParameters();

        /// <summary>
        /// The goalkeeper in goal (Phase 16): a shot he can read makes him go to where he thinks it will cross, but only once his reaction time has passed; a free ball he can get to first makes him come out; otherwise he stands where
        /// <see cref="GoalkeeperPositioning"/> puts him. His own capabilities come from his profile (reflexes, positioning, command, diving, handling) and how well his level uses them from the difficulty.
        /// </summary>
        private PlayerDecision KeeperAi(WorldView view, in WorldPlayer me)
        {
            GoalkeeperParameters level = profile != null && profile.Goalkeeper != null ? profile.Goalkeeper : NeutralKeeper;
            GoalkeeperSkill skill = GoalkeeperSkillModel.ResolveNormalized(level, me.Profile.KeeperReflexes, me.Profile.KeeperPositioning, me.Profile.KeeperCommand, me.Profile.KeeperDiving, me.Profile.KeeperHandling, keeperAi.Skill);
            Vec2 goal = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            float inward = goal.X > 0f ? -1f : 1f;

            GoalkeeperRead read = GoalkeeperReaction.Read(view, me, keeperAi, skill);
            if (read.IsShot && read.Started)
            {
                float reach = view.Field.GoalWidth / 2f - keeperAi.PostMargin;
                float y = Math.Max(goal.Y - reach, Math.Min(goal.Y + reach, read.Believed.Y));
                Vec2 stand = new Vec2(goal.X + inward * keeperAi.ShotStandOff, y);
                if (read.ReadError >= keeperAi.MisreadThreshold) LastError = new DecisionError { Kind = DecisionErrorKind.KeeperMisread, Gap = read.ReadError };
                return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.MoveToPoint, Reason = DecisionReason.KeeperSave, HasMovement = true, Movement = MovementIntent.TowardsTarget(me.Position, stand, keeperAi.DiveMagnitude, 0f) };
            }

            if (!read.IsShot)
            {
                float reboundWait = skill.ReactionSeconds * keeperAi.ReactionScale * (1f + keeperAi.ReboundPenalty * (1f - MathUtil.Clamp01(skill.ReboundResponse)));
                GoalkeeperExitChoice exit = GoalkeeperExit.Choose(view, me, keeperAi, skill, me.Profile.KeeperCommand, reboundWait);
                if (exit.Considered && exit.Mistake) LastError = new DecisionError { Kind = DecisionErrorKind.KeeperBadExit, Gap = exit.TrueMargin };
                if (exit.Go)
                    return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.MoveToBall, Reason = DecisionReason.KeeperExit, HasMovement = true, Movement = MovementIntent.TowardsTarget(me.Position, bounds.ClampPlayer(exit.Target), keeperAi.ExitMagnitude, 0f) };
            }

            GoalkeeperStand place = GoalkeeperPositioning.Stand(view, me, keeperAi, skill);
            LastDrift = place.Drift;
            return MoveTo(me.PlayerId, me.Position, place.Target, keeperAi.PositionMagnitude, DecisionKind.MoveToPoint, DecisionReason.KeeperHold);
        }
    }
}
