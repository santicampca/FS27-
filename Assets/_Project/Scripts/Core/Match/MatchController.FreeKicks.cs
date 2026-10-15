using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// PHASE 22 — the free kick of Classic. The referee's call (<see cref="CallFoul"/>) stops play, the ball goes to the spot, the fouled team takes it behind a wall, the whistle resumes play exactly once.
    /// Everything here is Core: no screen, no Unity, no clock but the simulation's. Arena never gets here (<see cref="IGameModeRules.HasFouls"/> is false there, so no foul is called and nothing is stopped).
    /// The pieces: <see cref="BeginFreeKick"/> (stop play), <see cref="PrepareFreeKick"/> (taker, wall, clearance: reached through <see cref="PrepareRestart"/>), the whistle (<see cref="ConfirmRestart"/> / <see cref="ExecuteFreeKick"/> /
    /// the Core's own timer), and <see cref="StepFreeKickProtection"/> (the opponents keep their distance until the ball is played).
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly FreeKickTuning freeKick = new FreeKickTuning();

        /// <summary>Every number of the free kick (a copy per match: a test or an experiment may change it). See <see cref="FreeKickDefaults"/>.</summary>
        public FreeKickTuning FreeKick { get { return freeKick; } }

        // ------------------------------------------------------------------ stopping play

        /// <summary>
        /// The foul stops play: the fouled team keeps the ball (it is the beneficiary), the ball goes to the spot, the restart is requested. Only in live play and only once: a second foul in the same stoppage
        /// is still recorded by <see cref="CallFoul"/> but starts nothing. Returns false when play was not stopped.
        /// </summary>
        private bool BeginFreeKick(TeamSide offenderTeam, string offenderId, string victimId, FoulReason reasons, Vec2 where, int disciplineIndex)
        {
            if (!freeKick.Enabled || State.Phase != MatchPhase.Playing) return false;
            TeamSide beneficiary = offenderTeam.Opponent();
            Vec2 spot = FreeKickSpot(beneficiary, where);

            State.FreeKicksAwarded++;
            var fk = new FreeKickState
            {
                Stage = FreeKickStage.Preparing,
                Number = State.FreeKicksAwarded,
                Beneficiary = beneficiary,
                OffenderTeam = offenderTeam,
                OffenderId = offenderId,
                VictimId = victimId,
                Reasons = reasons,
                DisciplineIndex = disciplineIndex,
                Spot = spot
            };
            State.FreeKick = fk;

            var request = new RestartRequest(RestartKind.FreeKick, beneficiary, spot);
            MatchPlayer taker = SelectFreeKickTaker(beneficiary, spot);
            request.TakerId = taker != null ? taker.PlayerId : null;
            fk.TakerId = request.TakerId;
            fk.SuggestedTakerId = request.TakerId;
            // the AI never waits to choose; a person has the choice time
            fk.TakerConfirmed = GetControl(beneficiary) != ControlSource.Human;
            fk.TakerChoiceRemaining = fk.TakerConfirmed ? 0f : freeKick.TakerChoiceSeconds;
            RequestRestart(request);
            ClearPossession();
            return true;
        }

        /// <summary>The spot of the free kick: where the foul was, inside the lines (a margin from them) and never in the goal zone of the team that fouled (that would be a penalty, a later phase).</summary>
        private Vec2 FreeKickSpot(TeamSide beneficiary, Vec2 where)
        {
            float halfLength = field.Length * 0.5f, halfWidth = field.Width * 0.5f, m = freeKick.SpotFieldMargin;
            float x = Math.Max(-halfLength + m, Math.Min(halfLength - m, where.X));
            float y = Math.Max(-halfWidth + m, Math.Min(halfWidth - m, where.Y));
            var spot = new Vec2(x, y);
            TeamSide defender = beneficiary.Opponent();
            if (Pitch.IsInGoalZone(spot, State.HomeAttacksPositiveX, defender))
            {
                int sign = PitchBounds.OwnGoalSign(State.HomeAttacksPositiveX, defender);
                float depth = Math.Max(halfLength - sign * spot.X, freeKick.SpotMinGoalLineDistance);
                spot = new Vec2(sign * (halfLength - depth), spot.Y);
            }
            return spot;
        }

        // ------------------------------------------------------------------ the taker

        /// <summary>
        /// The best taker of the fouled team (deterministic, ties to the earlier in the squad): near the goal the best shot (the curve counts), further away the best passer; every metre between the player and the spot
        /// costs a little. A field player always; the goalkeeper only when there is no other.
        /// </summary>
        public MatchPlayer SelectFreeKickTaker(TeamSide team, Vec2 spot)
        {
            TeamMatchState t = State.Team(team);
            MatchPlayer best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < t.Players.Count; i++)
            {
                MatchPlayer p = t.Players[i];
                if (!p.IsActive || p.InGoal) continue;
                float score = TakerScore(p, team, spot);
                if (score > bestScore + 1e-6f) { best = p; bestScore = score; }
            }
            return best ?? t.GoalkeeperInGoal;
        }

        /// <summary>How good a taker a player is for a free kick at that spot (the rule of <see cref="SelectFreeKickTaker"/>).</summary>
        private float TakerScore(MatchPlayer p, TeamSide team, Vec2 spot)
        {
            float goalDistance = (GoalCentreOf(team) - spot).Magnitude;
            float quality;
            if (goalDistance <= freeKick.TakerShotDistance)
            {
                float far = goalDistance / Math.Max(1e-3f, freeKick.TakerShotDistance);
                quality = p.ActionProfile.ShotQualityClose * (1f - far) + p.ActionProfile.ShotQualityFar * far;
                if (SkillResolver.CanUseAction(p.Skills, FootballActionKind.CurvedShot, true).Allowed) quality += freeKick.TakerCurveBonus;
            }
            else quality = p.ActionProfile.PassQuality;
            return quality - freeKick.TakerDistancePenalty * (p.Locomotion.Position - spot).Magnitude / Math.Max(1e-3f, field.Length);
        }

        /// <summary>
        /// The players who may take the free kick that is waiting, for the screen that lets a person choose (Phase 22): the active field players of the team that takes it, in squad order. Empty when there is none.
        /// The goalkeeper, the sent-off, the substituted-off, the bench and the other team are never listed. Pass the player lookup to also get names, shirt numbers and the raw attributes.
        /// </summary>
        public List<FreeKickTakerCandidate> FreeKickTakerCandidates(IPlayerLookup players = null)
        {
            var list = new List<FreeKickTakerCandidate>();
            FreeKickState fk = State.FreeKick;
            if (fk == null || !fk.Active) return list;
            TeamMatchState team = State.Team(fk.Beneficiary);
            for (int i = 0; i < team.Players.Count; i++)
            {
                MatchPlayer p = team.Players[i];
                if (!p.IsActive || p.InGoal) continue;
                var c = new FreeKickTakerCandidate
                {
                    PlayerId = p.PlayerId, Role = p.Role, IsCurrent = p.PlayerId == fk.TakerId, IsSuggested = p.PlayerId == fk.SuggestedTakerId,
                    Score = TakerScore(p, fk.Beneficiary, fk.Spot), ShotClose = p.ActionProfile.ShotQualityClose, ShotFar = p.ActionProfile.ShotQualityFar, Pass = p.ActionProfile.PassQuality,
                    PreferredFoot = p.ActionProfile.PreferredFoot, CanCurve = SkillResolver.CanUseAction(p.Skills, FootballActionKind.CurvedShot, true).Allowed,
                    DistanceToBall = (p.Locomotion.Position - fk.Spot).Magnitude
                };
                PlayerDefinition def;
                if (players != null && players.TryGet(p.PlayerId, out def) && def != null)
                {
                    c.HasDefinition = true; c.Name = def.DisplayName; c.ShirtNumber = def.Number;
                    c.ShootingValue = def.Attributes.Shooting; c.TechniqueValue = def.Attributes.Technique; c.PassingValue = def.Attributes.Passing; c.FinishingValue = def.Attributes.Finishing;
                }
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// A person chooses who takes the free kick (Phase 22). Only for the team that takes it, when a person controls it, while the choice is open (<see cref="FreeKickTuning.TakerChoiceSeconds"/> from the stoppage,
        /// until <see cref="ConfirmFreeKickTaker"/>), and only for an active field player of that team: not an opponent, not on the bench or substituted off, not sent off, not the goalkeeper. The choice can change as often
        /// as the person wants. It is the SAME restart machinery as the automatic choice: the wall, the clearance and the whistle are set up again around the new taker.
        /// </summary>
        public FreeKickTakerResult ChooseFreeKickTaker(TeamSide side, string playerId)
        {
            FreeKickState fk = State.FreeKick;
            FreeKickTakerResult gate = TakerChoiceGate(side, fk);
            if (gate != FreeKickTakerResult.Accepted) return gate;
            MatchPlayer p = State.Team(fk.Beneficiary).Find(playerId);
            if (p == null)
                return playerId != null && State.Team(fk.Beneficiary.Opponent()).Find(playerId) != null ? FreeKickTakerResult.WrongTeam : FreeKickTakerResult.UnknownPlayer;
            if (p.Status == MatchPlayerStatus.Dismissed) return FreeKickTakerResult.Dismissed;
            if (!p.IsActive) return FreeKickTakerResult.NotAvailable;
            if (p.InGoal) return FreeKickTakerResult.Goalkeeper;

            RestartRequest r = State.PendingRestart;
            r.TakerId = p.PlayerId;
            State.PendingRestart = r;
            fk.TakerId = p.PlayerId;
            fk.TakerChosenManually = true;
            fk.TakerReplaced = false;
            if (r.Status == RestartStatus.Placed) PrepareRestart();
            return FreeKickTakerResult.Accepted;
        }

        /// <summary>The person confirms the taker (the current one: the automatic one if he chose nobody): the choice is closed and the aiming begins. Accepted again is harmless.</summary>
        public FreeKickTakerResult ConfirmFreeKickTaker(TeamSide side)
        {
            FreeKickState fk = State.FreeKick;
            if (fk != null && fk.TakerConfirmed && fk.Active && side == fk.Beneficiary) return FreeKickTakerResult.Accepted;
            FreeKickTakerResult gate = TakerChoiceGate(side, fk);
            if (gate != FreeKickTakerResult.Accepted) return gate;
            PrepareRestart();   // a taker who is no longer valid is replaced before he is locked (the set-up is idempotent)
            fk.TakerConfirmed = true;
            fk.TakerChoiceRemaining = 0f;
            return FreeKickTakerResult.Accepted;
        }

        private FreeKickTakerResult TakerChoiceGate(TeamSide side, FreeKickState fk)
        {
            if (fk == null || !fk.Active || State.Phase != MatchPhase.WaitingForRestart || State.PendingRestart.Kind != RestartKind.FreeKick) return FreeKickTakerResult.NoFreeKick;
            if (side != fk.Beneficiary || GetControl(side) != ControlSource.Human) return FreeKickTakerResult.NotUserTeam;
            if (fk.TakerConfirmed) return FreeKickTakerResult.Locked;
            return FreeKickTakerResult.Accepted;
        }

        // ------------------------------------------------------------------ the set-up

        /// <summary>
        /// Sets up the free kick (called by <see cref="PrepareRestart"/>, and again by <see cref="ConfirmRestart"/> so a card, a formation change or a substitution during the wait never leaves a stale set-up): the taker
        /// behind the ball looking at the goal, the wall of the defending team, everybody else out of the circles. Idempotent and deterministic. Publishes <see cref="GameplayEventKind.FreeKickPrepared"/> the first time.
        /// </summary>
        private bool PrepareFreeKick()
        {
            RestartRequest r = State.PendingRestart;
            FreeKickState fk = State.FreeKick;
            if (fk == null || fk.Stage == FreeKickStage.None || fk.Stage == FreeKickStage.Done) return false;
            TeamSide team = r.Team, other = team.Opponent();

            MatchPlayer taker = r.TakerId != null ? State.Team(team).Find(r.TakerId) : null;
            // a taker who is no longer available (sent off, substituted off, put in goal) is replaced by the Core's own choice, so the free kick is never stuck
            if (taker == null || !taker.IsActive || (taker.InGoal && fk.TakerChosenManually))
            {
                if (fk.TakerChosenManually) fk.TakerReplaced = true;
                fk.TakerChosenManually = false;
                taker = SelectFreeKickTaker(team, r.Position);
            }
            if (taker == null) return false;
            r.TakerId = taker.PlayerId;
            fk.TakerId = taker.PlayerId;

            Vec2 spot = r.Position;
            Vec2 goal = GoalCentreOf(team);
            Vec2 face = ActionModel.DirectionOr(goal - spot, AttackDirectionOf(team));
            PlacePlayer(team, taker.PlayerId, Pitch.ClampPlayer(spot - face * freeKick.TakerOffsetBehind), face.ToHeading());

            BuildWall(fk, other, taker, spot, goal);
            ClearFreeKickCircles(fk, taker, spot);
            ParkKeepers();
            SeparateActivePlayers(false);

            r.Status = RestartStatus.Placed;
            State.PendingRestart = r;
            infrastructure.EnforcePlayerSpace = true;
            if (fk.Stage == FreeKickStage.Preparing)
            {
                fk.Stage = FreeKickStage.Ready;
                Emit(GameplayEventKind.FreeKickPrepared, team, taker.PlayerId, Vec3.FromXZ(spot, ballParameters.Radius), fk.Reasons.ToString(), fk.OffenderId);
            }
            return true;
        }

        /// <summary>How many defenders stand in the wall for a ball at that distance from the goal, never leaving the defending team without field players outside it.</summary>
        private int WallSize(float goalDistance, int fieldDefenders)
        {
            int size;
            if (goalDistance > freeKick.WallMaxGoalDistance) size = 0;
            else if (goalDistance <= freeKick.WallThreeDistance) size = 3;
            else if (goalDistance <= freeKick.WallTwoDistance) size = 2;
            else size = 1;
            size = Math.Min(size, freeKick.WallMaxPlayers);
            return Math.Max(0, Math.Min(size, fieldDefenders - freeKick.WallMinFieldDefendersOutside));
        }

        /// <summary>
        /// The wall of the defending team: the nearest field players, shoulder to shoulder, <see cref="FreeKickTuning.WallDistance"/> from the ball on the line to the NEAR post (the goalkeeper covers the rest), facing the ball.
        /// Few players and never in front of the middle of the goal when the ball is on a side, so the wall never closes the goal.
        /// </summary>
        private void BuildWall(FreeKickState fk, TeamSide defending, MatchPlayer taker, Vec2 spot, Vec2 goal)
        {
            fk.Wall.Clear();
            TeamMatchState team = State.Team(defending);
            var candidates = new List<MatchPlayer>(team.Players.Count);
            for (int i = 0; i < team.Players.Count; i++) if (team.Players[i].IsActive && !team.Players[i].InGoal) candidates.Add(team.Players[i]);

            int size = WallSize((goal - spot).Magnitude, candidates.Count);
            if (size == 0) return;

            float side = Math.Abs(spot.Y) < freeKick.WallCentreBand ? 0f : (spot.Y > 0f ? 1f : -1f);
            Vec2 aimAt = new Vec2(goal.X, side * freeKick.WallPostCoverage * field.GoalWidth * 0.5f);
            Vec2 dir = ActionModel.DirectionOr(aimAt - spot, AttackDirectionOf(taker.Team));
            Vec2 centre = spot + dir * freeKick.WallDistance;
            Vec2 across = new Vec2(-dir.Y, dir.X);

            // the nearest to the middle of the wall (ties to the earlier in the squad)
            var chosen = new List<MatchPlayer>(size);
            for (int n = 0; n < size; n++)
            {
                MatchPlayer best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float d = (candidates[i].Locomotion.Position - centre).Magnitude;
                    if (d < bestDistance - 1e-6f) { best = candidates[i]; bestDistance = d; }
                }
                chosen.Add(best);
                candidates.Remove(best);
            }

            // left to right along the wall by where each one stands now, so nobody crosses another
            for (int i = 1; i < chosen.Count; i++)
            {
                MatchPlayer p = chosen[i];
                float key = Dot(p.Locomotion.Position - centre, across);
                int j = i - 1;
                while (j >= 0 && Dot(chosen[j].Locomotion.Position - centre, across) > key + 1e-6f) { chosen[j + 1] = chosen[j]; j--; }
                chosen[j + 1] = p;
            }

            for (int k = 0; k < chosen.Count; k++)
            {
                Vec2 at = centre + across * ((k - (chosen.Count - 1) * 0.5f) * freeKick.WallSpacing);
                at = InsideLines(Pitch.ClampPlayer(at));
                PlacePlayer(defending, chosen[k].PlayerId, at, (spot - at).ToHeading());
                fk.Wall.Add(chosen[k].PlayerId);
            }
        }

        /// <summary>Everybody who is not the taker, the wall or a goalkeeper stays out of the circle of the ball: the opponents of the wall distance, the taker's team-mates of the usual clearance.</summary>
        private void ClearFreeKickCircles(FreeKickState fk, MatchPlayer taker, Vec2 spot)
        {
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
            {
                bool takers = team.Side == fk.Beneficiary;
                float radius = takers ? infrastructure.RestartClearRadius : freeKick.WallDistance;
                foreach (MatchPlayer p in team.Players)
                {
                    if (!p.IsActive || p == taker || p.InGoal || fk.Wall.Contains(p.PlayerId)) continue;
                    Vec2 d = p.Locomotion.Position - spot;
                    float dist = d.Magnitude;
                    if (dist >= radius) continue;
                    Vec2 axis = dist > 1e-4f ? d * (1f / dist) : -AttackDirectionOf(p.Team);
                    PlacePlayer(team.Side, p.PlayerId, InsideLines(Pitch.ClampPlayer(spot + axis * radius)), p.Locomotion.Heading);
                }
            }
        }

        /// <summary>A goalkeeper (a substitute who has just come on, an emergency keeper) is put in his goal zone; the others stay where they are.</summary>
        private void ParkKeepers()
        {
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
            {
                MatchPlayer keeper = team.GoalkeeperInGoal;
                if (keeper == null) continue;
                Vec2 at = keeper.Locomotion.Position;
                if (!Pitch.IsInGoalZone(at, State.HomeAttacksPositiveX, team.Side))
                    PlacePlayer(team.Side, keeper.PlayerId, Pitch.ClampToGoalZone(at, State.HomeAttacksPositiveX, team.Side), keeper.Locomotion.Heading);
            }
        }

        private Vec2 InsideLines(Vec2 p)
        {
            float halfLength = field.Length * 0.5f, halfWidth = field.Width * 0.5f;
            return new Vec2(Math.Max(-halfLength, Math.Min(halfLength, p.X)), Math.Max(-halfWidth, Math.Min(halfWidth, p.Y)));
        }

        private static float Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        // ------------------------------------------------------------------ waiting

        /// <summary>
        /// The wait for a restart (Core time): a free kick is set up by itself, a team without a goalkeeper gets one when nobody has done it in <see cref="FreeKickTuning.KeeperVacancySeconds"/>, and the whistle goes by itself
        /// after <see cref="FreeKickTuning.ReadySeconds"/> (a person who is taking the kick has <see cref="FreeKickTuning.HumanMaxWaitSeconds"/>), so no match is ever blocked.
        /// The presentation may whistle sooner with <see cref="ConfirmRestart"/>, and a person with <see cref="ExecuteFreeKick"/>.
        /// </summary>
        private void TickRestartWait(float dt)
        {
            if (State.PendingRestart.Kind == RestartKind.Penalty) { TickPenaltyWait(dt); return; }
            FreeKickState fk = State.FreeKick;
            if (State.PendingRestart.Kind != RestartKind.FreeKick || fk == null || !fk.Active) return;

            fk.WaitSeconds += dt;
            if (State.PendingRestart.Status == RestartStatus.Pending) PrepareRestart();
            if (!fk.TakerConfirmed)
            {
                fk.TakerChoiceRemaining = Math.Max(0f, freeKick.TakerChoiceSeconds - fk.WaitSeconds);
                if (fk.TakerChoiceRemaining <= 0f) { fk.TakerConfirmed = true; PrepareRestart(); }   // nobody chose in time: the current (automatic) taker stays
            }

            if (State.Home.NeedsGoalkeeper || State.Away.NeedsGoalkeeper)
            {
                fk.KeeperWaitSeconds += dt;
                if (fk.KeeperWaitSeconds >= freeKick.KeeperVacancySeconds) FillVacantGoals(fk.Spot, fk.TakerId);
                return;
            }
            fk.KeeperWaitSeconds = 0f;

            if (fk.Stage != FreeKickStage.Ready) return;
            float limit = GetControl(fk.Beneficiary) == ControlSource.Human ? freeKick.HumanMaxWaitSeconds : freeKick.ReadySeconds;
            if (fk.WaitSeconds >= limit) ConfirmRestart();
        }

        /// <summary>
        /// Safety net: a team that still has nobody in goal gets a keeper. A substitute keeper comes on when the team still has a substitution (for the field player farthest from the ball, never the taker);
        /// otherwise the field player with the best reading of the game puts the gloves on. Deterministic. The normal way is the coach calling <see cref="TryReplaceDismissedKeeper"/> / <see cref="TryAssignEmergencyKeeper"/> first.
        /// </summary>
        private void FillVacantGoals(Vec2 spot, string takerId)
        {
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
            {
                if (!team.NeedsGoalkeeper) continue;
                MatchPlayer bench = team.AvailableBenchKeeper;
                if (bench != null && team.SubstitutionsUsed < settings.EffectiveSubstitutionLimit)
                {
                    MatchPlayer leaving = null;
                    float farthest = -1f;
                    for (int i = 0; i < team.Players.Count; i++)
                    {
                        MatchPlayer p = team.Players[i];
                        if (!p.IsActive || p.InGoal || p.PlayerId == takerId) continue;
                        float d = (p.Locomotion.Position - spot).Magnitude;
                        if (d > farthest + 1e-6f) { leaving = p; farthest = d; }
                    }
                    if (leaving != null && TryReplaceDismissedKeeper(team.Side, bench.PlayerId, leaving.PlayerId) == SubstitutionResult.Accepted) continue;
                }
                MatchPlayer reader = null;
                float best = float.NegativeInfinity;
                for (int i = 0; i < team.Players.Count; i++)
                {
                    MatchPlayer p = team.Players[i];
                    if (!p.IsActive || p.InGoal || p.PlayerId == takerId) continue;
                    if (p.ActionProfile.InterceptQuality > best + 1e-6f) { reader = p; best = p.ActionProfile.InterceptQuality; }
                }
                if (reader != null) TryAssignEmergencyKeeper(team.Side, reader.PlayerId);
            }
            // the set-up is done again with the new keeper in goal
            if (State.PendingRestart.Status == RestartStatus.Placed && State.Phase == MatchPhase.WaitingForRestart) PrepareRestart();
        }

        // ------------------------------------------------------------------ the whistle

        /// <summary>The whistle of a free kick (called by <see cref="ConfirmRestartReady"/> when play resumes): the opponents are protected from the ball for a moment, <see cref="GameplayEventKind.FreeKickTaken"/> once.</summary>
        private void OnRestartWhistle()
        {
            OnPenaltyWhistle();
            FreeKickState fk = State.FreeKick;
            if (fk == null || !fk.Active || State.PendingRestart.Kind != RestartKind.FreeKick) return;
            fk.Stage = FreeKickStage.Protected;
            fk.ProtectedSeconds = 0f;
            Emit(GameplayEventKind.FreeKickTaken, fk.Beneficiary, fk.TakerId, Vec3.FromXZ(fk.Spot, ballParameters.Radius), fk.Reasons.ToString(), fk.OffenderId);
        }

        /// <summary>
        /// After the whistle, until the ball is played: the opponents (goalkeeper excepted) do not enter the wall distance. The free kick is over when the ball leaves the taker (he kicked it, or lost it) or has moved,
        /// or after <see cref="FreeKickTuning.ProtectionSeconds"/> (a taker who keeps dribbling). Runs in live play only.
        /// </summary>
        private void StepFreeKickProtection(float dt)
        {
            FreeKickState fk = State.FreeKick;
            if (fk == null || fk.Stage != FreeKickStage.Protected) return;
            fk.ProtectedSeconds += dt;

            MatchBall b = State.Ball;
            bool carried = b.HasPossession && b.PossessingPlayerId == fk.TakerId && b.PossessingTeam == fk.Beneficiary;
            bool moved = (Flat(b.Physics.Position) - fk.Spot).Magnitude > freeKick.PlayedDistance;
            if (!carried || moved || fk.ProtectedSeconds >= freeKick.ProtectionSeconds)
            {
                fk.Stage = FreeKickStage.Done;
                fk.KickedByTaker = !carried && b.LastTouchPlayerId == fk.TakerId;
                return;
            }

            float radius = freeKick.WallDistance - freeKick.ProtectionSlack;
            TeamMatchState defenders = State.Team(fk.Beneficiary.Opponent());
            for (int i = 0; i < defenders.Players.Count; i++)
            {
                MatchPlayer p = defenders.Players[i];
                if (!p.IsActive || p.InGoal) continue;
                Vec2 d = p.Locomotion.Position - fk.Spot;
                float dist = d.Magnitude;
                if (dist >= radius) continue;
                Vec2 axis = dist > 1e-4f ? d * (1f / dist) : -AttackDirectionOf(p.Team);
                p.Locomotion.Position = InsideLines(Pitch.ClampPlayer(fk.Spot + axis * radius));
                p.Locomotion.CurrentSpeed = 0f;
                p.Locomotion.Velocity = Vec2.Zero;
            }
        }

        // ------------------------------------------------------------------ taking it (a person)

        /// <summary>
        /// Takes the free kick with the person's aim: the release of the shot button. Whistles (<see cref="ConfirmRestart"/>) and performs the kick through the SAME action system as any other kick
        /// (<see cref="Perform"/>): a shot (a curved one when the aim asks and the taker owns the skill, otherwise straight) or a pass to a team-mate (the one named, or the one the drag points at). The keeper and the wall
        /// react to the ball, never to this call. Refused when no free kick is waiting.
        /// </summary>
        public FreeKickExecuteResult ExecuteFreeKick(FreeKickAim aim)
        {
            FreeKickState fk = State.FreeKick;
            if (fk == null || !fk.Active || State.Phase != MatchPhase.WaitingForRestart || State.PendingRestart.Kind != RestartKind.FreeKick) return FreeKickExecuteResult.NoFreeKick;
            if (!ValidAim(aim, fk)) return FreeKickExecuteResult.InvalidAim;
            fk.TakerConfirmed = true;
            fk.TakerChoiceRemaining = 0f;
            if (!ConfirmRestart()) return FreeKickExecuteResult.NotReady;

            MatchPlayer taker = State.Team(fk.Beneficiary).Find(fk.TakerId);
            if (taker == null) return FreeKickExecuteResult.NotReady;
            Vec2 spot = fk.Spot;
            Vec2 goal = GoalCentreOf(fk.Beneficiary);
            float power = aim.Power > 0f ? ActionModel.Clamp01(aim.Power) : FreeKickAiming.PowerFromHold(aim.HoldSeconds, freeKick);

            // the destination: a goal zone, the dragged point (inside the lines), or the drag direction; with nothing, the middle of the goal
            Vec2 destination = FreeKickAiming.ResolveDestination(aim, spot, goal, AttackDirectionOf(fk.Beneficiary), field.GoalWidth, freeKick, InsideLines, goal, out bool hasDestination);
            Vec2 dir = ActionModel.DirectionOr(destination - spot, ActionModel.DirectionOr(goal - spot, AttackDirectionOf(fk.Beneficiary)));

            var request = new ActionRequest(fk.Beneficiary, taker.PlayerId, FootballActionKind.Shot, dir, power) { Source = ControlSource.Human };
            if (aim.Intent == FreeKickIntent.Shot || aim.Intent == FreeKickIntent.Lob)
            {
                if (aim.Intent == FreeKickIntent.Lob) request.Kind = FootballActionKind.Chip;
                if (hasDestination) request.Target = PassTarget.ToPosition(destination);
                float curve = Math.Max(-1f, Math.Min(1f, aim.Curve));
                if (aim.Intent == FreeKickIntent.Shot && Math.Abs(curve) > 1e-4f && SkillResolver.CanUseAction(taker.Skills, FootballActionKind.CurvedShot, true).Allowed)
                {
                    request.Kind = FootballActionKind.CurvedShot;
                    request.Curve = curve;
                }
            }
            else
            {
                MatchPlayer target = aim.TargetPlayerId != null ? State.Team(fk.Beneficiary).Find(aim.TargetPlayerId) : (hasDestination && !aim.HasDestination ? null : PassTargetByDrag(fk, taker, spot, dir));
                request.Kind = power >= freeKick.PassLongPower ? FootballActionKind.LongPass : FootballActionKind.ShortPass;
                if (target != null && target.IsActive && target != taker)
                {
                    request.Target = PassTarget.ToPlayer(target.PlayerId);
                    if ((target.Locomotion.Position - spot).Magnitude >= freeKick.PassLongDistance) request.Kind = FootballActionKind.LongPass;
                }
                else if (aim.HasDestination) request.Target = PassTarget.ToPosition(destination);
            }

            ActionResult result = Perform(request);
            if (result.Outcome == ActionOutcome.Rejected && request.Kind == FootballActionKind.CurvedShot)
            {
                request.Kind = FootballActionKind.Shot;
                request.Curve = 0f;
                result = Perform(request);
            }
            return result.Outcome == ActionOutcome.Rejected ? FreeKickExecuteResult.ActionRefused : FreeKickExecuteResult.Executed;
        }

        /// <summary>An aim is valid when its numbers are numbers, its intent is known and the team-mate it names (if any) plays for the taker's team and is on the pitch. Out-of-field destinations are NOT invalid: they are brought inside the lines.</summary>
        private bool ValidAim(FreeKickAim aim, FreeKickState fk)
        {
            if (!Enum.IsDefined(typeof(FreeKickIntent), aim.Intent)) return false;
            if (!ActionModel.IsFinite(aim.Direction) || float.IsNaN(aim.Power) || float.IsNaN(aim.HoldSeconds) || float.IsNaN(aim.Curve)) return false;
            if (aim.HasDestination && !ActionModel.IsFinite(aim.Destination)) return false;
            if (aim.HasZone && !Enum.IsDefined(typeof(GoalZone), aim.Zone)) return false;
            if (aim.TargetPlayerId != null)
            {
                MatchPlayer t = State.Team(fk.Beneficiary).Find(aim.TargetPlayerId);
                if (t == null || !t.IsActive || t.PlayerId == fk.TakerId) return false;
            }
            return true;
        }

        /// <summary>The team-mate a drag points at (see <see cref="FreeKickAiming.FindPassTarget"/>): the field players of the taker's team on the pitch, in squad order.</summary>
        private MatchPlayer PassTargetByDrag(FreeKickState fk, MatchPlayer taker, Vec2 spot, Vec2 dir)
        {
            TeamMatchState team = State.Team(fk.Beneficiary);
            var mates = new List<MatchPlayer>(team.Players.Count);
            var points = new List<Vec2>(team.Players.Count);
            for (int i = 0; i < team.Players.Count; i++)
            {
                MatchPlayer p = team.Players[i];
                if (!p.IsActive || p == taker || p.InGoal) continue;
                mates.Add(p);
                points.Add(p.Locomotion.Position);
            }
            int index = FreeKickAiming.FindPassTarget(spot, dir, points, freeKick);
            return index >= 0 ? mates[index] : null;
        }
    }
}
