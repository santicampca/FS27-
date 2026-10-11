using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// PHASE 23 — penalties. Classic and Arena are different systems on purpose:
    /// * CLASSIC: a foul inside the area is a penalty (<see cref="BeginPenalty"/>, from <see cref="CallFoul"/>): play stops, the fouled team chooses the taker, taker and goalkeeper are placed, the whistle resumes play and the kick is
    ///   REAL: the existing shot action and ball physics, the goal frame (posts and crossbar rebound), the goalkeeper's dive and his existing touch model. A penalty in the match goes on after the kick (the rebound is played, the taker may not
    ///   touch it again until somebody else has: a second touch is an indirect free kick); one kick of a shoot-out ends at its first result (goal, save, miss) and nobody plays the rebound.
    /// * ARENA: no penalties for fouls. Only the shoot-out of a knockout match exists, and each kick is an arcade duel resolved at once from the two aims and the attributes (<see cref="ArenaPenaltyResolver"/>).
    /// Both modes share the order of the takers, the eligibility rules, the counting (<see cref="PenaltyShootoutState"/>) and the events.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly PenaltyTuning penalty = new PenaltyTuning();
        private IBallCollider[] baseColliders;
        private GoalFrameCollider[] goalFrames;
        private SeededRandom penaltyRandom;
        private readonly List<string>[] shootoutOrders = new List<string>[2];
        private float shootoutGap;

        /// <summary>Every number of the penalties (a copy per match). See <see cref="PenaltyDefaults"/>.</summary>
        public PenaltyTuning Penalties { get { return penalty; } }

        private SeededRandom PenaltyRandom { get { return penaltyRandom ?? (penaltyRandom = SeedMixer.Create(settings.Seed, 23)); } }

        // ------------------------------------------------------------------ geometry

        /// <summary>The goal line a team defends, signed X.</summary>
        private float OwnGoalLineX(TeamSide defending) { return PitchBounds.OwnGoalSign(State.HomeAttacksPositiveX, defending) * field.Length * 0.5f; }

        /// <summary>Is that point inside the penalty area of the team that defends that goal?</summary>
        public bool InPenaltyArea(Vec2 p, TeamSide defending)
        {
            float depth = Math.Abs(OwnGoalLineX(defending) - p.X);
            bool inFront = (OwnGoalLineX(defending) > 0f) ? p.X <= OwnGoalLineX(defending) : p.X >= OwnGoalLineX(defending);
            return inFront && depth <= penalty.AreaDepth && Math.Abs(p.Y) <= penalty.AreaHalfWidth;
        }

        /// <summary>The penalty spot in front of the goal a team defends.</summary>
        public Vec2 PenaltySpotOf(TeamSide defending)
        {
            float x = OwnGoalLineX(defending);
            return new Vec2(x - Math.Sign(x) * penalty.SpotDistance, 0f);
        }

        private GoalFrameCollider[] Frames
        {
            get
            {
                if (goalFrames == null)
                {
                    float hl = field.Length * 0.5f;
                    goalFrames = new[] { new GoalFrameCollider(hl, field.GoalWidth, field.GoalHeight, penalty.PostRadius, penalty.FrameRestitution), new GoalFrameCollider(-hl, field.GoalWidth, field.GoalHeight, penalty.PostRadius, penalty.FrameRestitution) };
                }
                return goalFrames;
            }
        }

        /// <summary>The goal frame (posts and crossbar) is part of the ball physics while a penalty is on (open play ignores it in this phase).</summary>
        private void SetGoalFrame(bool on)
        {
            if (baseColliders == null) baseColliders = colliders;
            if (!on) { colliders = baseColliders; return; }
            var all = new IBallCollider[baseColliders.Length + 2];
            Array.Copy(baseColliders, all, baseColliders.Length);
            foreach (GoalFrameCollider f in Frames) f.ResetCounters();
            all[baseColliders.Length] = Frames[0]; all[baseColliders.Length + 1] = Frames[1];
            colliders = all;
        }

        private int FrameHits { get { return goalFrames == null ? 0 : goalFrames[0].PostHits + goalFrames[0].BarHits + goalFrames[1].PostHits + goalFrames[1].BarHits; } }

        private bool PenaltyOn { get { return State.Penalty != null && State.Penalty.Active; } }

        /// <summary>Actions and the ball run: live play, or one live kick of a shoot-out.</summary>
        private bool ActionsLive
        {
            get
            {
                if (State.Phase == MatchPhase.Playing) return true;
                PenaltyState p = State.Penalty;
                return State.Phase == MatchPhase.Shootout && p != null && p.Kind == PenaltyKind.Shootout && (p.Stage == PenaltyStage.Whistled || p.Stage == PenaltyStage.Kicked);
            }
        }

        // ------------------------------------------------------------------ awarding a penalty in the match (Classic)

        /// <summary>
        /// The foul was inside the area of the team that committed it: play stops, the fouled team gets the penalty. Only in live play, where the mode has penalties, and once (a second foul during the stoppage is recorded but starts nothing).
        /// Returns false when this is not a penalty (it is then an ordinary free kick).
        /// </summary>
        private bool BeginPenalty(TeamSide offenderTeam, string offenderId, string victimId, FoulReason reasons, Vec2 where, int disciplineIndex)
        {
            if (!penalty.Enabled || !rules.HasPenalties || State.Phase != MatchPhase.Playing || !InPenaltyArea(where, offenderTeam)) return false;
            TeamSide beneficiary = offenderTeam.Opponent();
            Vec2 spot = PenaltySpotOf(offenderTeam);
            State.PenaltiesAwarded++;
            var pen = new PenaltyState
            {
                Kind = PenaltyKind.InMatch, Stage = PenaltyStage.Preparing, Number = State.PenaltiesAwarded, Team = beneficiary, Spot = spot,
                GoalCentre = new Vec2(OwnGoalLineX(offenderTeam), 0f), OffenderId = offenderId, VictimId = victimId, Reasons = reasons, DisciplineIndex = disciplineIndex
            };
            State.Penalty = pen;
            MatchPlayer taker = SelectPenaltyTaker(beneficiary);
            pen.TakerId = taker != null ? taker.PlayerId : null;
            pen.SuggestedTakerId = pen.TakerId;
            pen.TakerConfirmed = GetControl(beneficiary) != ControlSource.Human;
            pen.TakerChoiceRemaining = pen.TakerConfirmed ? 0f : penalty.TakerChoiceSeconds;
            MatchPlayer keeper = State.Team(offenderTeam).GoalkeeperInGoal;
            pen.KeeperId = keeper != null ? keeper.PlayerId : null;

            var request = new RestartRequest(RestartKind.Penalty, beneficiary, spot);
            request.TakerId = pen.TakerId;
            RequestRestart(request);
            ClearPossession();
            Emit(GameplayEventKind.PenaltyAwarded, beneficiary, victimId, Vec3.FromXZ(spot, ballParameters.Radius), reasons.ToString(), offenderId);
            return true;
        }

        /// <summary>The best finisher of the team (deterministic, ties to the earlier in the squad): the goalkeeper only when nobody else is on the pitch.</summary>
        public MatchPlayer SelectPenaltyTaker(TeamSide team)
        {
            TeamMatchState t = State.Team(team);
            MatchPlayer best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < t.Players.Count; i++)
            {
                MatchPlayer p = t.Players[i];
                if (!p.IsActive || p.InGoal) continue;
                float score = PenaltyScore(p);
                if (score > bestScore + 1e-6f) { best = p; bestScore = score; }
            }
            return best ?? t.GoalkeeperInGoal;
        }

        private float PenaltyScore(MatchPlayer p)
        {
            float s = p.ActionProfile.ShotQualityClose;
            if (SkillResolver.CanUseAction(p.Skills, FootballActionKind.CurvedShot, true).Allowed) s += penalty.TakerCurveBonus;
            return s;
        }

        // ------------------------------------------------------------------ the set-up

        /// <summary>
        /// Places the penalty (called by <see cref="PrepareRestart"/> and again at the whistle): the taker behind the ball looking at the goal, the goalkeeper on his line, everybody else outside the area. Idempotent.
        /// </summary>
        private bool PreparePenaltyInMatch()
        {
            PenaltyState pen = State.Penalty;
            RestartRequest r = State.PendingRestart;
            if (pen == null || pen.Kind != PenaltyKind.InMatch || pen.Stage == PenaltyStage.None || pen.Stage == PenaltyStage.Resolved) return false;

            MatchPlayer taker = r.TakerId != null ? State.Team(pen.Team).Find(r.TakerId) : null;
            if (taker == null || !taker.IsActive || (taker.InGoal && pen.TakerChosenManually))
            {
                if (pen.TakerChosenManually) pen.TakerReplaced = true;
                pen.TakerChosenManually = false;
                taker = SelectPenaltyTaker(pen.Team);
            }
            if (taker == null) return false;
            r.TakerId = taker.PlayerId;
            pen.TakerId = taker.PlayerId;

            PlacePenaltyPlayers(pen, taker);
            r.Status = RestartStatus.Placed;
            State.PendingRestart = r;
            infrastructure.EnforcePlayerSpace = true;
            if (pen.Stage == PenaltyStage.Preparing)
            {
                pen.Stage = PenaltyStage.Ready;
                SetGoalFrame(true);
                Emit(GameplayEventKind.PenaltyPrepared, pen.Team, taker.PlayerId, Vec3.FromXZ(pen.Spot, ballParameters.Radius), "InMatch", pen.OffenderId);
            }
            return true;
        }

        private void PlacePenaltyPlayers(PenaltyState pen, MatchPlayer taker)
        {
            Vec2 face = ActionModel.DirectionOr(pen.GoalCentre - pen.Spot, AttackDirectionOf(pen.Team));
            PlacePlayer(pen.Team, taker.PlayerId, Pitch.ClampPlayer(pen.Spot - face * penalty.TakerOffsetBehind), face.ToHeading());
            TeamSide defending = pen.Team.Opponent();
            MatchPlayer keeper = State.Team(defending).GoalkeeperInGoal;
            pen.KeeperId = keeper != null ? keeper.PlayerId : null;
            float goalX = pen.GoalCentre.X;
            if (keeper != null) PlacePlayer(defending, keeper.PlayerId, new Vec2(goalX - Math.Sign(goalX) * penalty.KeeperLineOffset, 0f), (-face).ToHeading());
            PushOutsideArea(pen, taker, keeper);
            SeparateActivePlayers(false);
        }

        /// <summary>Everybody who is not the taker or the goalkeeper stands outside the area (and its margin), on the side of the pitch he is.</summary>
        private void PushOutsideArea(PenaltyState pen, MatchPlayer taker, MatchPlayer keeper)
        {
            float goalX = pen.GoalCentre.X;
            float sign = Math.Sign(goalX);
            float edgeX = goalX - sign * (penalty.AreaDepth + penalty.OthersMargin);
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
                foreach (MatchPlayer p in team.Players)
                {
                    if (!p.IsActive || p == taker || p == keeper) continue;
                    Vec2 pos = p.Locomotion.Position;
                    bool inside = (sign > 0f ? pos.X > edgeX : pos.X < edgeX) && Math.Abs(pos.Y) <= penalty.AreaHalfWidth + penalty.OthersMargin;
                    if (!inside) continue;
                    PlacePlayer(team.Side, p.PlayerId, InsideLines(Pitch.ClampPlayer(new Vec2(edgeX, pos.Y))), p.Locomotion.Heading);
                }
        }

        // ------------------------------------------------------------------ choosing the taker (a person)

        /// <summary>The players who may take the penalty that is waiting (the same record as the free kick's): the active field players of the team that kicks, in squad order.</summary>
        public List<FreeKickTakerCandidate> PenaltyTakerCandidates(IPlayerLookup players = null)
        {
            var list = new List<FreeKickTakerCandidate>();
            PenaltyState pen = State.Penalty;
            if (pen == null || !pen.Waiting) return list;
            AddTakerCandidates(list, pen.Team, pen.TakerId, pen.SuggestedTakerId, pen.Spot, players, false);
            return list;
        }

        private void AddTakerCandidates(List<FreeKickTakerCandidate> list, TeamSide side, string currentId, string suggestedId, Vec2 spot, IPlayerLookup players, bool includeKeeper)
        {
            TeamMatchState team = State.Team(side);
            for (int i = 0; i < team.Players.Count; i++)
            {
                MatchPlayer p = team.Players[i];
                if (!p.IsActive || (p.InGoal && !includeKeeper)) continue;
                var c = new FreeKickTakerCandidate
                {
                    PlayerId = p.PlayerId, Role = p.Role, IsCurrent = p.PlayerId == currentId, IsSuggested = p.PlayerId == suggestedId, Score = PenaltyScore(p),
                    ShotClose = p.ActionProfile.ShotQualityClose, ShotFar = p.ActionProfile.ShotQualityFar, Pass = p.ActionProfile.PassQuality, PreferredFoot = p.ActionProfile.PreferredFoot,
                    CanCurve = SkillResolver.CanUseAction(p.Skills, FootballActionKind.CurvedShot, true).Allowed, DistanceToBall = (p.Locomotion.Position - spot).Magnitude
                };
                PlayerDefinition def;
                if (players != null && players.TryGet(p.PlayerId, out def) && def != null)
                {
                    c.HasDefinition = true; c.Name = def.DisplayName; c.ShirtNumber = def.Number;
                    c.ShootingValue = def.Attributes.Shooting; c.TechniqueValue = def.Attributes.Technique; c.PassingValue = def.Attributes.Passing; c.FinishingValue = def.Attributes.Finishing;
                }
                list.Add(c);
            }
        }

        /// <summary>A person chooses who takes the penalty (only the team that kicks, controlled by a person, while the choice is open; an active field player of that team: not an opponent, not on the bench, not sent off, not the goalkeeper).</summary>
        public PenaltyTakerResult ChoosePenaltyTaker(TeamSide side, string playerId)
        {
            PenaltyState pen = State.Penalty;
            PenaltyTakerResult gate = PenaltyChoiceGate(side, pen);
            if (gate != PenaltyTakerResult.Accepted) return gate;
            MatchPlayer p = State.Team(pen.Team).Find(playerId);
            if (p == null) return playerId != null && State.Team(pen.Team.Opponent()).Find(playerId) != null ? PenaltyTakerResult.WrongTeam : PenaltyTakerResult.UnknownPlayer;
            if (p.Status == MatchPlayerStatus.Dismissed) return PenaltyTakerResult.Dismissed;
            if (!p.IsActive) return PenaltyTakerResult.NotAvailable;
            if (p.InGoal) return PenaltyTakerResult.Goalkeeper;
            RestartRequest r = State.PendingRestart;
            r.TakerId = p.PlayerId;
            State.PendingRestart = r;
            pen.TakerId = p.PlayerId;
            pen.TakerChosenManually = true;
            pen.TakerReplaced = false;
            if (r.Status == RestartStatus.Placed) PrepareRestart();
            return PenaltyTakerResult.Accepted;
        }

        /// <summary>The person confirms the taker (the automatic one if he chose nobody): the choice closes and the aiming begins.</summary>
        public PenaltyTakerResult ConfirmPenaltyTaker(TeamSide side)
        {
            PenaltyState pen = State.Penalty;
            if (pen != null && pen.TakerConfirmed && pen.Waiting && side == pen.Team) return PenaltyTakerResult.Accepted;
            PenaltyTakerResult gate = PenaltyChoiceGate(side, pen);
            if (gate != PenaltyTakerResult.Accepted) return gate;
            PrepareRestart();
            pen.TakerConfirmed = true;
            pen.TakerChoiceRemaining = 0f;
            return PenaltyTakerResult.Accepted;
        }

        private PenaltyTakerResult PenaltyChoiceGate(TeamSide side, PenaltyState pen)
        {
            if (pen == null || pen.Kind != PenaltyKind.InMatch || !pen.Waiting || State.Phase != MatchPhase.WaitingForRestart || State.PendingRestart.Kind != RestartKind.Penalty) return PenaltyTakerResult.NoPenalty;
            if (side != pen.Team || GetControl(side) != ControlSource.Human) return PenaltyTakerResult.NotUserTeam;
            if (pen.TakerConfirmed) return PenaltyTakerResult.Locked;
            return PenaltyTakerResult.Accepted;
        }

        /// <summary>The goalkeeper's choice (a person's): where to dive. Only for the defending team, before the kick; without it the AI chooses at the kick.</summary>
        public bool SetPenaltyKeeperAim(TeamSide defendingSide, PenaltyKeeperAim aim)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || !(pen.Waiting || pen.Stage == PenaltyStage.Whistled) || defendingSide != pen.Team.Opponent() || !PenaltyAiming.IsValid(aim)) return false;
            aim.HasChoice = true;
            aim.Lateral = Math.Max(-penalty.DiveMaxLateral, Math.Min(penalty.DiveMaxLateral, aim.Lateral));
            pen.KeeperAim = aim;
            pen.KeeperAimGiven = true;
            return true;
        }

        // ------------------------------------------------------------------ waiting and the whistle

        private void TickPenaltyWait(float dt)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Kind != PenaltyKind.InMatch || !pen.Waiting || State.PendingRestart.Kind != RestartKind.Penalty) return;
            pen.WaitSeconds += dt;
            if (State.PendingRestart.Status == RestartStatus.Pending) PrepareRestart();
            if (!pen.TakerConfirmed)
            {
                pen.TakerChoiceRemaining = Math.Max(0f, penalty.TakerChoiceSeconds - pen.WaitSeconds);
                if (pen.TakerChoiceRemaining <= 0f) { pen.TakerConfirmed = true; PrepareRestart(); }
            }
            if (State.Home.NeedsGoalkeeper || State.Away.NeedsGoalkeeper)
            {
                pen.KickSeconds += dt;   // time spent waiting for a goalkeeper
                if (pen.KickSeconds >= penalty.KeeperVacancySeconds) FillVacantGoals(pen.Spot, pen.TakerId);
                return;
            }
            pen.KickSeconds = 0f;
            if (pen.Stage != PenaltyStage.Ready) return;
            float limit = GetControl(pen.Team) == ControlSource.Human ? penalty.HumanMaxWaitSeconds : penalty.ReadySeconds;
            if (pen.WaitSeconds >= limit) ConfirmRestart();
        }

        private void OnPenaltyWhistle()
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Kind != PenaltyKind.InMatch || pen.Stage != PenaltyStage.Ready || State.PendingRestart.Kind != RestartKind.Penalty) return;
            pen.Stage = PenaltyStage.Whistled;
            pen.WhistleSeconds = 0f;
            pen.KickSeconds = 0f;
            Emit(GameplayEventKind.PenaltyWhistled, pen.Team, pen.TakerId, Vec3.FromXZ(pen.Spot, ballParameters.Radius), "InMatch", pen.OffenderId);
        }

        // ------------------------------------------------------------------ taking it

        /// <summary>
        /// The release of the shot button for a penalty (Classic in the match, or a Classic shoot-out kick, or an Arena shoot-out kick): whistles when needed and takes the kick with the aim. The kick is the existing shot action
        /// (direction, power, accuracy error from the taker's attributes), then the height is set from the aim, so the ball physics, the goal frame and the goalkeeper's touch are the real ones. Refused when no penalty waits.
        /// </summary>
        public PenaltyExecuteResult ExecutePenalty(PenaltyShotAim aim)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || !(pen.Waiting || pen.Stage == PenaltyStage.Whistled)) return PenaltyExecuteResult.NoPenalty;
            if (!PenaltyAiming.IsValid(aim)) return PenaltyExecuteResult.InvalidAim;
            return TakePenalty(aim, ControlSource.Human);
        }

        private PenaltyExecuteResult TakePenalty(PenaltyShotAim aim, ControlSource source)
        {
            PenaltyState pen = State.Penalty;
            aim = PenaltyAiming.Clamp(aim, penalty);
            pen.TakerConfirmed = true;
            pen.TakerChoiceRemaining = 0f;

            if (pen.Kind == PenaltyKind.Shootout && !rules.PlaysPenaltiesPhysically) return TakeArenaShootoutKick(aim);

            if (pen.Waiting)
            {
                if (pen.Kind == PenaltyKind.InMatch)
                {
                    if (!ConfirmRestart()) return PenaltyExecuteResult.NotReady;
                }
                else
                {
                    pen.Stage = PenaltyStage.Whistled;
                    MatchPlayer t0 = State.Team(pen.Team).Find(pen.TakerId);
                    if (t0 == null || !GiveBallTo(pen.Team, pen.TakerId)) { pen.Stage = PenaltyStage.Ready; return PenaltyExecuteResult.NotReady; }
                    Emit(GameplayEventKind.PenaltyWhistled, pen.Team, pen.TakerId, Vec3.FromXZ(pen.Spot, ballParameters.Radius), "Shootout");
                }
            }
            MatchPlayer taker = State.Team(pen.Team).Find(pen.TakerId);
            if (taker == null || pen.Stage != PenaltyStage.Whistled) return PenaltyExecuteResult.NotReady;

            Vec2 attack = ActionModel.DirectionOr(pen.GoalCentre - pen.Spot, AttackDirectionOf(pen.Team));
            Vec2 point = PenaltyAiming.GoalPoint(aim.Lateral, pen.GoalCentre, attack, field.GoalWidth);
            float power = PenaltyAiming.Power(aim, penalty);
            var request = new ActionRequest(pen.Team, taker.PlayerId, FootballActionKind.Shot, ActionModel.DirectionOr(point - pen.Spot, attack), power) { Source = source, Target = PassTarget.ToPosition(point) };
            if (Math.Abs(aim.Curve) > 1e-4f && SkillResolver.CanUseAction(taker.Skills, FootballActionKind.CurvedShot, true).Allowed) { request.Kind = FootballActionKind.CurvedShot; request.Curve = aim.Curve; }
            ActionResult result = Perform(request);
            if (result.Outcome == ActionOutcome.Rejected && request.Kind == FootballActionKind.CurvedShot) { request.Kind = FootballActionKind.Shot; request.Curve = 0f; result = Perform(request); }
            if (result.Outcome == ActionOutcome.Rejected) return PenaltyExecuteResult.ActionRefused;

            SetShotHeight(pen, aim);
            pen.Aim = aim;
            pen.Stage = PenaltyStage.Kicked;
            pen.KickSeconds = 0f;
            pen.LastContactPlayerId = taker.PlayerId;
            StartKeeperDive(pen);
            return PenaltyExecuteResult.Executed;
        }

        /// <summary>The kick has left the taker's foot: the vertical part of its velocity is set so the ball reaches the aimed height at the goal line (the horizontal part, with the accuracy error, is the action's).</summary>
        private void SetShotHeight(PenaltyState pen, PenaltyShotAim aim)
        {
            Vec3 v = State.Ball.Physics.Velocity;
            float vh = (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
            if (vh < 0.5f) return;
            Vec3 pos = State.Ball.Physics.Position;
            float distance = Math.Abs(pen.GoalCentre.X - pos.X);
            float t = Math.Max(0.05f, distance / vh);
            float target = aim.Height * field.GoalHeight * penalty.UnderBarFraction;
            float vy = (target - pos.Y + 0.5f * ballParameters.Gravity * t * t) / t;
            SetBallVelocity(new Vec3(v.X, vy, v.Z));
        }

        private void StartKeeperDive(PenaltyState pen)
        {
            MatchPlayer keeper = pen.KeeperId != null ? State.Team(pen.Team.Opponent()).Find(pen.KeeperId) : null;
            if (keeper == null || !keeper.IsActive) return;
            if (!pen.KeeperAimGiven)
            {
                SeededRandom r = PenaltyRandom;
                pen.KeeperAim = PenaltyAi.Keeper(r.NextFloat01(), r.NextFloat01(), r.NextFloat01(), penalty);
            }
            ActionProfile prof = keeper.ActionProfile;
            pen.KeeperDelay = penalty.DiveDelayBase - penalty.DiveDelayRange * prof.KeeperReflexes;
            pen.KeeperSpeed = penalty.DiveSpeedBase + penalty.DiveSpeedRange * prof.KeeperDiving;
            Vec2 attack = ActionModel.DirectionOr(pen.GoalCentre - pen.Spot, AttackDirectionOf(pen.Team));
            Vec2 line = PenaltyAiming.GoalPoint(pen.KeeperAim.HasChoice ? pen.KeeperAim.Lateral : 0f, pen.GoalCentre, attack, field.GoalWidth);
            pen.KeeperStart = keeper.Locomotion.Position;
            pen.KeeperTarget = new Vec2(keeper.Locomotion.Position.X, line.Y);
        }

        // ------------------------------------------------------------------ stepping a penalty that is on

        private void StepPenalty(float dt)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null) return;
            if (pen.Stage == PenaltyStage.Whistled && pen.Kind == PenaltyKind.InMatch)
            {
                pen.WhistleSeconds += dt;
                MatchPlayer taker = State.Team(pen.Team).Find(pen.TakerId);
                MatchPlayer keeper = pen.KeeperId != null ? State.Team(pen.Team.Opponent()).Find(pen.KeeperId) : null;
                PushOutsideArea(pen, taker, keeper);
                bool person = GetControl(pen.Team) == ControlSource.Human;
                if (!person || pen.WhistleSeconds >= penalty.KickWindowSeconds) TakePenalty(AiShotAim(taker), ControlSource.Ai);
                return;
            }
            if (pen.Stage != PenaltyStage.Kicked) return;

            pen.KickSeconds += dt;
            DiveKeeper(pen);
            if (FrameHits > 0 && !pen.HitFrame)
            {
                pen.HitFrame = true;
                bool bar = goalFrames != null && (goalFrames[0].LastHitKind == 2 || goalFrames[1].LastHitKind == 2) && !(goalFrames[0].LastHitKind == 1 || goalFrames[1].LastHitKind == 1);
                pen.HitPostFirst = !bar;
                Emit(GameplayEventKind.PenaltyFrameHit, pen.Team, pen.TakerId, State.Ball.Physics.Position, bar ? "Bar" : "Post");
            }
            if (pen.KickSeconds >= penalty.KickMaxSeconds) FinishPenalty(null);
        }

        private void DiveKeeper(PenaltyState pen)
        {
            if (pen.KeeperTouched || pen.KeeperId == null) return;
            MatchPlayer keeper = State.Team(pen.Team.Opponent()).Find(pen.KeeperId);
            if (keeper == null || !keeper.IsActive) return;
            float t = pen.KickSeconds - pen.KeeperDelay;
            Vec2 pos = keeper.Locomotion.Position;
            Vec2 toTarget = new Vec2(0f, pen.KeeperTarget.Y - pos.Y);
            float distance = Math.Abs(toTarget.Y);
            if (t <= 0f || distance < 1e-4f) { keeper.Locomotion.CurrentSpeed = 0f; keeper.Locomotion.Velocity = Vec2.Zero; return; }
            float step = Math.Min(distance, pen.KeeperSpeed * Math.Min(t, 1f / 60f));
            keeper.Locomotion.Position = new Vec2(pos.X, pos.Y + Math.Sign(toTarget.Y) * step);
            keeper.Locomotion.CurrentSpeed = 0f;
            keeper.Locomotion.Velocity = Vec2.Zero;
        }

        private PenaltyShotAim AiShotAim(MatchPlayer taker)
        {
            SeededRandom r = PenaltyRandom;
            float quality = taker != null ? taker.ActionProfile.ShotQualityClose : 0.5f;
            return PenaltyAi.Kick(quality, r.NextFloat01(), r.NextFloat01(), r.NextFloat01(), penalty);
        }

        // ------------------------------------------------------------------ contacts and the second touch

        /// <summary>A player touched the ball (a first touch). From the kick on, a touch by anybody but the taker lets the taker play the ball again.</summary>
        /// <summary>1 normally; for the goalkeeper who is diving on a penalty, the fraction of the ball's speed he has to deal with (<see cref="PenaltyTuning.KeeperSpeedFactor"/>).</summary>
        private float PenaltyKeeperSpeedFactor(MatchPlayer p)
        {
            PenaltyState pen = State.Penalty;
            return pen != null && pen.Stage == PenaltyStage.Kicked && p.InGoal && p.PlayerId == pen.KeeperId ? penalty.KeeperSpeedFactor : 1f;
        }

        /// <summary>The reach of the goalkeeper's touch: the usual one, or on a penalty the dive reach (smaller, and shorter the faster the shot).</summary>
        private float PenaltyKeeperReach(MatchPlayer p, float usual)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Stage != PenaltyStage.Kicked || !p.InGoal || p.PlayerId != pen.KeeperId) return usual;
            ActionProfile prof = p.ActionProfile;
            float speed = State.Ball.Physics.HorizontalSpeed;
            float loss = penalty.ReachSpeedLoss * ActionModel.Clamp01(speed / Math.Max(1e-3f, penalty.ReachReferenceSpeed)) * (1f - prof.KeeperReflexes);
            return (penalty.ReachBase + penalty.ReachRange * prof.KeeperDiving) * (1f - loss);
        }

        private void NotePenaltyContact(MatchPlayer p)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Stage != PenaltyStage.Kicked) return;
            pen.LastContactPlayerId = p.PlayerId;
            if (p.PlayerId == pen.TakerId) return;
            pen.ContactByOther = true;
            if (p.InGoal && p.Team != pen.Team) pen.KeeperTouched = true;
        }

        /// <summary>
        /// Called when a player is about to take the ball: true when the touch must not happen. In a shoot-out the taker never plays the ball again (silently). In a penalty in the match a touch by the taker before anybody else
        /// has touched the ball is an illegal second touch: indirect free kick for the other team where it happened.
        /// </summary>
        private bool PenaltyBlocksTouch(MatchPlayer p)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || p.PlayerId != pen.TakerId || p.Team != pen.Team) return false;
            if (pen.Kind == PenaltyKind.Shootout) return false;
            if (pen.Stage != PenaltyStage.Kicked || pen.ContactByOther) return false;
            Vec2 where = Flat(State.Ball.Physics.Position);
            pen.Outcome = PenaltyOutcome.IllegalSecondTouch;
            Emit(GameplayEventKind.PenaltySecondTouch, pen.Team, pen.TakerId, State.Ball.Physics.Position, "SecondTouch");
            FinishPenalty(PenaltyOutcome.IllegalSecondTouch);
            BeginFreeKick(pen.Team, pen.TakerId, null, FoulReason.None, where, -1);
            return true;
        }

        /// <summary>In a shoot-out the taker never plays the ball again once he has kicked it: he is simply not a candidate to touch it.</summary>
        private bool ShootoutTakerExcluded(MatchPlayer p)
        {
            PenaltyState pen = State.Penalty;
            return pen != null && pen.Kind == PenaltyKind.Shootout && p.PlayerId == pen.TakerId && p.Team == pen.Team && (pen.Stage == PenaltyStage.Whistled || pen.Stage == PenaltyStage.Kicked);
        }

        private void OnPenaltyPossession(string playerId)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Stage != PenaltyStage.Kicked || pen.Kind != PenaltyKind.InMatch) return;
            if (playerId != pen.TakerId || pen.ContactByOther) FinishPenalty(null);
        }

        private void OnPenaltyGoal()
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Stage != PenaltyStage.Kicked) return;
            pen.Goal = true;
            FinishPenalty(PenaltyOutcome.Scored);
        }

        /// <summary>The kick is decided (once): the outcome is the forced one or the one the facts say, the goal frame is off, the shoot-out is told.</summary>
        private void FinishPenalty(PenaltyOutcome? forced)
        {
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Counted) return;
            pen.Counted = true;
            PenaltyOutcome o;
            if (forced.HasValue) o = forced.Value;
            else if (pen.Goal) o = PenaltyOutcome.Scored;
            else if (pen.KeeperTouched) o = PenaltyOutcome.Saved;
            else if (pen.HitFrame) o = pen.HitPostFirst ? PenaltyOutcome.HitPost : PenaltyOutcome.HitBar;
            else o = PenaltyOutcome.Missed;
            pen.Outcome = o;
            pen.Stage = PenaltyStage.Resolved;
            SetGoalFrame(false);
            Emit(GameplayEventKind.PenaltyKickTaken, pen.Team, pen.TakerId, State.Ball.Physics.Position, (pen.Kind == PenaltyKind.Shootout ? "Shootout:" : "InMatch:") + o, pen.KeeperId);
            if (pen.Kind == PenaltyKind.Shootout) { RecordShootoutResult(o == PenaltyOutcome.Scored); shootoutGap = 0f; }
        }

        // ------------------------------------------------------------------ the shoot-out

        /// <summary>
        /// Sets the order in which a team's players take the shoot-out kicks (before that team's first kick). Only players on the pitch (the goalkeeper included); no repeats. A partial list is completed automatically with the
        /// rest of the eligible players (best finisher first, the goalkeeper last). The order is kept and respected at every kick; <see cref="NextShootoutTaker"/> says who kicks next.
        /// </summary>
        public ShootoutOrderResult SetShootoutOrder(TeamSide side, IList<string> playerIds)
        {
            if (State.Shootout != null && State.Shootout.Taken(side) > 0) return ShootoutOrderResult.AlreadyStarted;
            if (playerIds == null || playerIds.Count == 0) return ShootoutOrderResult.Empty;
            TeamMatchState team = State.Team(side);
            var seen = new HashSet<string>();
            foreach (string id in playerIds)
            {
                MatchPlayer p = id != null ? team.Find(id) : null;
                if (p == null) return id != null && State.Team(side.Opponent()).Find(id) != null ? ShootoutOrderResult.WrongTeam : ShootoutOrderResult.UnknownPlayer;
                if (!p.IsActive) return ShootoutOrderResult.NotEligible;
                if (!seen.Add(id)) return ShootoutOrderResult.Duplicate;
            }
            shootoutOrders[(int)side] = new List<string>(playerIds);
            return ShootoutOrderResult.Accepted;
        }

        /// <summary>
        /// The full order of a team's takers as the Core will use it: the order the person set (entries that are no longer eligible are skipped), then the rest of the eligible players by the automatic rule. After the last one the cycle
        /// starts again, in the same order. This is the list a screen shows and the list the kicks follow.
        /// </summary>
        public List<string> ShootoutOrderFor(TeamSide side)
        {
            TeamMatchState team = State.Team(side);
            var order = new List<string>();
            List<string> explicitOrder = shootoutOrders[(int)side];
            if (explicitOrder != null)
                foreach (string id in explicitOrder) { MatchPlayer p = team.Find(id); if (p != null && p.IsActive && !order.Contains(id)) order.Add(id); }
            var rest = new List<MatchPlayer>();
            foreach (MatchPlayer p in team.Players) if (p.IsActive && !order.Contains(p.PlayerId)) rest.Add(p);
            while (rest.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < rest.Count; i++) if (ShootoutAutoScore(rest[i]) > ShootoutAutoScore(rest[best]) + 1e-6f) best = i;
                order.Add(rest[best].PlayerId);
                rest.RemoveAt(best);
            }
            return order;
        }

        private float ShootoutAutoScore(MatchPlayer p) { return PenaltyScore(p) - (p.InGoal ? penalty.KeeperLastPenalty : 0f); }

        /// <summary>The player who takes the next kick of that team (null when the team has nobody on the pitch).</summary>
        public string NextShootoutTaker(TeamSide side)
        {
            List<string> order = ShootoutOrderFor(side);
            if (order.Count == 0) return null;
            int taken = State.Shootout != null ? State.Shootout.Taken(side) : 0;
            return order[taken % order.Count];
        }

        /// <summary>The eligible players of a team for the order screen (everybody on the pitch, goalkeeper included), with the Core's own order marked: <see cref="FreeKickTakerCandidate.IsSuggested"/> on the automatic first one.</summary>
        public List<FreeKickTakerCandidate> ShootoutTakerCandidates(TeamSide side, IPlayerLookup players = null)
        {
            var list = new List<FreeKickTakerCandidate>();
            List<string> auto = AutomaticShootoutOrder(side);
            AddTakerCandidates(list, side, NextShootoutTaker(side), auto.Count > 0 ? auto[0] : null, Vec2.Zero, players, true);
            return list;
        }

        private List<string> AutomaticShootoutOrder(TeamSide side)
        {
            List<string> keep = shootoutOrders[(int)side];
            shootoutOrders[(int)side] = null;
            List<string> auto = ShootoutOrderFor(side);
            shootoutOrders[(int)side] = keep;
            return auto;
        }

        private bool ShootoutSimulated { get { return State.Shootout != null && settings.ShootoutExecution == ShootoutExecutionMode.Simulated; } }

        /// <summary>Called when the shoot-out starts: with the simulated execution the first kick is prepared.</summary>
        private void OnShootoutStarted()
        {
            if (!ShootoutSimulated) return;
            shootoutGap = penalty.BetweenKicksSeconds;
            State.Penalty = null;
        }

        /// <summary>Prepares the next kick of the shoot-out: taker (from the order), goalkeeper of the other team, ball on the spot, everybody else on the halfway line.</summary>
        private void PrepareShootoutKick()
        {
            PenaltyShootoutState so = State.Shootout;
            if (so == null || so.IsDecided) return;
            TeamSide side = so.NextTeam, defending = side.Opponent();
            if (State.Team(defending).NeedsGoalkeeper || State.Team(side).NeedsGoalkeeper)
            {
                // somebody has to keep goal: the Core's safety net (same as the free kick's)
                FillVacantGoals(Vec2.Zero, null);
            }
            string takerId = NextShootoutTaker(side);
            if (takerId == null) { RecordShootoutResult(false); return; }

            float hl = field.Length * 0.5f;
            float goalSign = State.HomeAttacksPositiveX ? 1f : -1f;   // both teams shoot at the goal the home team attacks
            Vec2 goal = new Vec2(goalSign * hl, 0f);
            Vec2 spot = new Vec2(goalSign * (hl - penalty.SpotDistance), 0f);
            State.PenaltiesAwarded++;
            var pen = new PenaltyState { Kind = PenaltyKind.Shootout, Stage = PenaltyStage.Ready, Number = State.PenaltiesAwarded, Team = side, TakerId = takerId, SuggestedTakerId = takerId, TakerConfirmed = true, Spot = spot, GoalCentre = goal };
            MatchPlayer keeper = State.Team(defending).GoalkeeperInGoal;
            pen.KeeperId = keeper != null ? keeper.PlayerId : null;
            State.Penalty = pen;
            State.Ball.Physics = new BallState(Vec3.FromXZ(spot, ballParameters.Radius), Vec3.Zero) { IsGrounded = true };
            ClearPossession();
            State.Ball.Mode = BallMode.Dead;

            Vec2 face = ActionModel.DirectionOr(goal - spot, new Vec2(goalSign, 0f));
            MatchPlayer taker = State.Team(side).Find(takerId);
            PlacePlayer(side, takerId, Pitch.ClampPlayer(spot - face * penalty.TakerOffsetBehind), face.ToHeading());
            if (keeper != null) PlacePlayer(defending, keeper.PlayerId, new Vec2(goal.X - goalSign * penalty.KeeperLineOffset, 0f), (-face).ToHeading());
            int n = 0;
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
                foreach (MatchPlayer p in team.Players)
                {
                    if (!p.IsActive || p == taker || p == keeper) continue;
                    PlacePlayer(team.Side, p.PlayerId, new Vec2(0f, -10f + 1.4f * (n++ % 15)), p.Locomotion.Heading);
                }
            infrastructure.EnforcePlayerSpace = true;
            if (rules.PlaysPenaltiesPhysically) SetGoalFrame(true);
            Emit(GameplayEventKind.PenaltyPrepared, side, takerId, Vec3.FromXZ(spot, ballParameters.Radius), "Shootout", pen.KeeperId);
        }

        private void TickShootout(float dt, in BallObservation ball)
        {
            if (!ShootoutSimulated || State.Shootout == null || State.Shootout.IsDecided) return;
            PenaltyState pen = State.Penalty;
            if (pen == null || pen.Stage == PenaltyStage.Resolved || pen.Stage == PenaltyStage.None)
            {
                shootoutGap += dt;
                if (shootoutGap >= penalty.BetweenKicksSeconds) { shootoutGap = 0f; PrepareShootoutKick(); }
                return;
            }
            if (pen.Stage == PenaltyStage.Ready)
            {
                pen.WaitSeconds += dt;
                float limit = GetControl(pen.Team) == ControlSource.Human ? penalty.HumanMaxWaitSeconds : penalty.ReadySeconds;
                if (pen.WaitSeconds >= limit) TakePenalty(AiShotAim(State.Team(pen.Team).Find(pen.TakerId)), ControlSource.Ai);
                return;
            }
            if (pen.Stage != PenaltyStage.Kicked) return;

            // a live kick of a Classic shoot-out: goal, out, stopped or held ends it
            BallJudgement j = rules.JudgeBall(field, State.HomeAttacksPositiveX, settings.KickoffTeam, ball, State.HasLastTouch, State.LastTouch);
            if (j.Outcome == BallOutcome.Goal) { pen.Goal = true; FinishPenalty(PenaltyOutcome.Scored); return; }
            if (j.Outcome == BallOutcome.OutOfBounds) { FinishPenalty(null); return; }
            MatchBall b = State.Ball;
            if (b.HasPossession && b.PossessingPlayerId != pen.TakerId) { FinishPenalty(null); return; }
            if (b.Physics.IsGrounded && b.Physics.HorizontalSpeed < 0.2f && pen.KickSeconds > 0.5f) FinishPenalty(null);
        }

        /// <summary>One kick of an Arena shoot-out: both aims and the attributes decide it at once (<see cref="ArenaPenaltyResolver"/>); nothing flies.</summary>
        private PenaltyExecuteResult TakeArenaShootoutKick(PenaltyShotAim aim)
        {
            PenaltyState pen = State.Penalty;
            MatchPlayer taker = State.Team(pen.Team).Find(pen.TakerId);
            MatchPlayer keeper = pen.KeeperId != null ? State.Team(pen.Team.Opponent()).Find(pen.KeeperId) : null;
            SeededRandom r = PenaltyRandom;
            PenaltyKeeperAim k;
            if (pen.KeeperAimGiven) k = pen.KeeperAim;
            else k = PenaltyAi.Keeper(r.NextFloat01(), r.NextFloat01(), r.NextFloat01(), penalty);
            pen.KeeperAim = k;
            float quality = taker != null ? taker.ActionProfile.ShotQualityClose : 0.5f;
            float diving = keeper != null ? keeper.ActionProfile.KeeperDiving : 0f, reflexes = keeper != null ? keeper.ActionProfile.KeeperReflexes : 0f;
            ArenaPenaltyResolver.Result res = ArenaPenaltyResolver.Resolve(aim, k, quality, diving, reflexes, r.NextFloat01() * 2f - 1f, r.NextFloat01() * 2f - 1f, penalty);
            pen.Aim = aim;
            pen.FlightSeconds = penalty.ArenaFlightSeconds;
            pen.Goal = res.Outcome == PenaltyOutcome.Scored;
            pen.KeeperTouched = res.Outcome == PenaltyOutcome.Saved;
            pen.Stage = PenaltyStage.Kicked;
            FinishPenalty(res.Outcome);
            return PenaltyExecuteResult.Executed;
        }

        /// <summary>Records the result of a kick of a simulated shoot-out (once) and ends the match when the shoot-out is decided.</summary>
        private void RecordShootoutResult(bool scored)
        {
            if (State.Phase != MatchPhase.Shootout || State.Shootout == null) return;
            TeamSide taker = State.Shootout.NextTeam;
            if (!State.Shootout.RecordKick(scored)) return;
            if (State.Shootout.IsDecided)
            {
                Emit(GameplayEventKind.PenaltyShootoutEnded, State.Shootout.HasWinner ? State.Shootout.Winner : (TeamSide?)null, null, Vec3.Zero, State.Shootout.HasWinner ? "Decided" : "Level");
                Finish(MatchTerminationReason.PenaltyShootout, null);
            }
        }
    }
}
