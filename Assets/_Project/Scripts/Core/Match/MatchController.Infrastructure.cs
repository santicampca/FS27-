using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>What <see cref="MatchController.SubmitHumanInput"/> did with a person's input.</summary>
    public struct HumanInputResult
    {
        /// <summary>The player the input went to (the controlled player of that side), or null when there was none.</summary>
        public string PlayerId;
        public bool Moved;
        public bool ActionAttempted;
        public ActionResult Action;
    }

    /// <summary>Where a player is to be placed (<see cref="MatchController.PlacePlayer(in PlayerPlacement)"/>).</summary>
    public struct PlayerPlacement
    {
        public TeamSide Side;
        public string PlayerId;
        public Vec2 Position;
        public float Heading;
        /// <summary>Also set his stamina (0..100, clamped).</summary>
        public bool SetStamina;
        public float Stamina;
        /// <summary>He is the one who takes the pending restart of his team (the explicit taker).</summary>
        public bool TakesRestart;
    }

    /// <summary>
    /// Phase 7 of the match engine: the INFRASTRUCTURE the future AI will stand on. Nothing here decides anything: the formations place the players, restarts have an explicit taker, a person's input
    /// and an AI intent end in the same <see cref="Perform"/>, players cannot overlap or leave the pitch, and one player per side can be the controlled one.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly InfrastructureTuning infrastructure = new InfrastructureTuning();
        private PitchBounds pitch;
        private readonly FormationDefinition[] formations = new FormationDefinition[2];
        private readonly ControlSource[] control = new ControlSource[2];
        private readonly string[] selected = new string[2];
        private readonly Vec2[] spaceBuffer = new Vec2[24];
        private readonly MatchPlayer[] spacePlayers = new MatchPlayer[24];
        private readonly List<PlacedPlayer> placement = new List<PlacedPlayer>(8);

        public InfrastructureTuning Infrastructure => infrastructure;
        public FieldDimensions Field => field;
        /// <summary>The geometry of the pitch (and what the ball does at its lines in this mode).</summary>
        public PitchBounds Pitch => pitch ?? (pitch = new PitchBounds(field, rules.UsesWalls, infrastructure));

        // ------------------------------------------------------------------ formations and placing

        /// <summary>The formation a team lines up in (null = none: its players stay where they are put).</summary>
        public void SetFormation(TeamSide side, FormationDefinition formation) { formations[(int)side] = formation; }
        public FormationDefinition GetFormation(TeamSide side) { return formations[(int)side]; }

        private readonly RoleAssignment[] assignments = new RoleAssignment[2];

        /// <summary>
        /// Who stands in which slot (Phase 9, see <see cref="RolePositionAssigner"/>). Without one the field players take the slots in squad order. A player who left the pitch hands his slot to the next
        /// field player who has none, in squad order (a substitute inherits the slot); calling this again reassigns explicitly. Null clears it.
        /// </summary>
        public void SetRoleAssignment(TeamSide side, RoleAssignment assignment) { assignments[(int)side] = assignment; }
        public RoleAssignment GetRoleAssignment(TeamSide side) { return assignments[(int)side]; }

        private PlayingProfileCatalog playingProfiles;

        /// <summary>
        /// The playing profiles (roles with affinity) of the players, for the AI to read through <see cref="WorldPlayer.Roles"/> (Phase 10). Optional: without them a player simply has no role, and the AI
        /// treats every player alike. They are read, never changed; the match does not use them for anything else.
        /// </summary>
        public void SetPlayingProfiles(PlayingProfileCatalog profiles) { playingProfiles = profiles; }
        internal bool TryGetPlayingProfile(string playerId, out PlayerPlayingProfile profile)
        {
            profile = null;
            return playingProfiles != null && playingProfiles.TryGet(playerId, out profile);
        }

        /// <summary>The field players on the pitch in the order of the field slots of the formation: the assigned ones where they can play, the others in squad order.</summary>
        private List<string> FieldIdsInSlotOrder(TeamMatchState team, RoleAssignment a)
        {
            var onPitch = new List<string>(5);
            foreach (MatchPlayer p in team.Players) if (p.IsActive && !p.InGoal) onPitch.Add(p.PlayerId);
            if (a == null) return onPitch;
            var ids = new List<string>(a.FieldPlayerIds.Count);
            var taken = new HashSet<string>();
            for (int k = 0; k < a.FieldPlayerIds.Count; k++)
            {
                string id = a.FieldPlayerIds[k];
                if (onPitch.Contains(id) && taken.Add(id)) ids.Add(id); else ids.Add(null);
            }
            int next = 0;
            for (int k = 0; k < ids.Count; k++)
            {
                if (ids[k] != null) continue;
                while (next < onPitch.Count && taken.Contains(onPitch[next])) next++;
                if (next >= onPitch.Count) break;
                ids[k] = onPitch[next]; taken.Add(onPitch[next]);
            }
            return ids;
        }

        /// <summary>
        /// Puts a team's players on their starting positions from its formation (turned to the way it attacks NOW, so it is right after a side switch). The goalkeeper in goal takes the goal slot and the
        /// field players take the field slots in squad order. Returns false when the team has no formation, nobody in goal or fewer field players than slots.
        /// </summary>
        public bool PlaceFormation(TeamSide side, bool kickoff = true)
        {
            FormationDefinition f = formations[(int)side];
            TeamMatchState team = State.Team(side);
            MatchPlayer keeper = team.GoalkeeperInGoal;
            if (f == null || keeper == null) return false;
            List<string> ids = FieldIdsInSlotOrder(team, assignments[(int)side]);
            if (!FormationPlacement.Compute(f, Pitch, State.HomeAttacksPositiveX, side, keeper.PlayerId, ids, kickoff, placement)) return false;
            foreach (PlacedPlayer pp in placement) PlacePlayer(side, pp.PlayerId, pp.Position, pp.Heading);
            infrastructure.EnforcePlayerSpace = true;
            return true;
        }

        /// <summary>Places a player and, optionally, sets his stamina and makes him the taker of his team's pending restart. Only active players can be placed.</summary>
        public bool PlacePlayer(in PlayerPlacement placementRequest)
        {
            if (!PlacePlayer(placementRequest.Side, placementRequest.PlayerId, placementRequest.Position, placementRequest.Heading)) return false;
            MatchPlayer p = State.Team(placementRequest.Side).Find(placementRequest.PlayerId);
            if (placementRequest.SetStamina) p.Locomotion.CurrentStamina = LocomotionStamina.Clamp(placementRequest.Stamina, locomotion);
            if (placementRequest.TakesRestart) AssignRestartTaker(placementRequest.PlayerId);
            return true;
        }

        private readonly int[] homeSignature = new int[2] { -1, -1 };
        private readonly FormationDefinition[] homeFormation = new FormationDefinition[2];
        private readonly bool[] homeAttack = new bool[2];
        private readonly RoleAssignment[] homeAssignment = new RoleAssignment[2];

        /// <summary>
        /// Brings the open-play positions of the formations up to date (Phase 8: a player's "home"). It is a DERIVED cache: it is recomputed only when the formation, the attacking direction or who is on the
        /// pitch changes, so calling it every step costs a few integer comparisons. Teams without a formation have no home.
        /// </summary>
        public void RefreshHomePositions()
        {
            for (int side = 0; side < 2; side++)
            {
                TeamMatchState team = side == 0 ? State.Home : State.Away;
                int signature = 17;
                for (int i = 0; i < team.Players.Count; i++) { MatchPlayer p = team.Players[i]; signature = signature * 31 + (p.IsActive ? (p.InGoal ? 2 : 1) : 0); }
                bool attack = State.HomeAttacksPositiveX;
                if (signature == homeSignature[side] && ReferenceEquals(formations[side], homeFormation[side]) && ReferenceEquals(assignments[side], homeAssignment[side]) && attack == homeAttack[side]) continue;
                homeSignature[side] = signature; homeFormation[side] = formations[side]; homeAssignment[side] = assignments[side]; homeAttack[side] = attack;
                foreach (MatchPlayer p in team.Players) p.HasHome = false;
                FormationDefinition f = formations[side];
                MatchPlayer keeper = team.GoalkeeperInGoal;
                if (f == null || keeper == null) continue;
                List<string> ids = FieldIdsInSlotOrder(team, assignments[side]);
                if (!FormationPlacement.Compute(f, Pitch, attack, team.Side, keeper.PlayerId, ids, false, placement)) continue;
                foreach (PlacedPlayer pp in placement) { MatchPlayer p = team.Find(pp.PlayerId); p.Home = pp.Position; p.HasHome = true; }
            }
        }

        // ------------------------------------------------------------------ restarts

        /// <summary>Chooses the taker of the pending restart explicitly. Only an active player of the team that takes it.</summary>
        public bool AssignRestartTaker(string playerId)
        {
            if (State.Phase != MatchPhase.WaitingForKickoff && State.Phase != MatchPhase.WaitingForRestart) return false;
            RestartRequest r = State.PendingRestart;
            MatchPlayer p = State.Team(r.Team).Find(playerId);
            if (p == null || !p.IsActive || r.Status == RestartStatus.Confirmed) return false;
            r.TakerId = playerId;
            State.PendingRestart = r;
            return true;
        }

        /// <summary>
        /// The default taker (used only when nobody was assigned): the goalkeeper for a goal kick; otherwise the field player of the team nearest to the spot (ties: the earlier in the squad) and only a
        /// goalkeeper when there is no field player. Deterministic, and never "the first of the list" unless he really is the nearest.
        /// </summary>
        public MatchPlayer DefaultTaker(RestartRequest request)
        {
            TeamMatchState team = State.Team(request.Team);
            if (request.Kind == RestartKind.GoalKick && team.GoalkeeperInGoal != null) return team.GoalkeeperInGoal;
            MatchPlayer best = null;
            float bestDistance = float.MaxValue;
            foreach (MatchPlayer p in team.Players)
            {
                if (!p.IsActive || p.InGoal) continue;
                float d = (p.Locomotion.Position - request.Position).Magnitude;
                if (d < bestDistance) { best = p; bestDistance = d; }
            }
            return best ?? team.GoalkeeperInGoal;
        }

        /// <summary>
        /// Sets up the pending restart: for a kick-off, both teams on their formations (when they have one); the taker (assigned, or the default) next to the ball; everybody else out of the clearance
        /// circle. The ball is already at the spot. Nothing is given to anyone yet. Returns false when there is no restart to set up or no taker.
        /// </summary>
        public bool PrepareRestart()
        {
            if (State.Phase != MatchPhase.WaitingForKickoff && State.Phase != MatchPhase.WaitingForRestart) return false;
            RestartRequest r = State.PendingRestart;
            if (r.Status == RestartStatus.Confirmed) return false;
            if (r.Kind == RestartKind.FreeKick) return PrepareFreeKick();
            if (r.Kind == RestartKind.Penalty) return PreparePenaltyInMatch();
            if (r.Kind == RestartKind.KickOff) { PlaceFormation(TeamSide.Home, true); PlaceFormation(TeamSide.Away, true); }

            MatchPlayer taker = r.TakerId != null ? State.Team(r.Team).Find(r.TakerId) : null;
            if (taker == null || !taker.IsActive) taker = DefaultTaker(r);
            if (taker == null) return false;
            r.TakerId = taker.PlayerId;

            Vec2 spot = r.Position;
            Vec2 face = TakerFacing(r, taker);
            Vec2 standAt = spot - face * infrastructure.TakerOffset;
            PlacePlayer(r.Team, taker.PlayerId, Pitch.ClampPlayer(standAt), face.ToHeading());
            ClearAroundSpot(r, taker);
            SeparateActivePlayers(r.Kind == RestartKind.KickOff);
            r.Status = RestartStatus.Placed;
            State.PendingRestart = r;
            infrastructure.EnforcePlayerSpace = true;
            return true;
        }

        /// <summary>Which way the taker faces: towards the goal he attacks for a kick-off; otherwise towards the middle of the pitch.</summary>
        private Vec2 TakerFacing(RestartRequest r, MatchPlayer taker)
        {
            Vec2 toCentre = ActionModel.DirectionOr(-r.Position, AttackDirectionOf(taker.Team));
            if (r.Kind == RestartKind.KickOff) return AttackDirectionOf(taker.Team);
            return toCentre;
        }

        /// <summary>The last touch of a set-up: nobody of the twelve overlaps. Pushes apart and puts everybody back inside the pitch (and, for a kick-off, in his own half) until it settles.</summary>
        private void SeparateActivePlayers(bool kickoff)
        {
            var list = new List<MatchPlayer>(12);
            foreach (TeamMatchState team in new[] { State.Home, State.Away }) foreach (MatchPlayer p in team.Players) if (p.IsActive) list.Add(p);
            var points = new Vec2[list.Count];
            for (int i = 0; i < points.Length; i++) points[i] = list[i].Locomotion.Position;
            for (int pass = 0; pass < 40; pass++)
            {
                bool settled = PlayerSeparation.Resolve(points, points.Length, infrastructure.MinSeparation, 1);
                for (int i = 0; i < points.Length; i++)
                {
                    Vec2 q = Pitch.ClampPlayer(points[i]);
                    if (list[i].InGoal) q = Pitch.ClampToGoalZone(q, State.HomeAttacksPositiveX, list[i].Team);
                    else if (kickoff && !PitchBounds.InOwnHalf(q, State.HomeAttacksPositiveX, list[i].Team)) q = new Vec2(PitchBounds.OwnGoalSign(State.HomeAttacksPositiveX, list[i].Team) * infrastructure.KickoffHalfMargin, q.Y);
                    points[i] = q;
                }
                if (settled && pass > 0) break;
            }
            for (int i = 0; i < points.Length; i++) if (!points[i].Equals(list[i].Locomotion.Position)) PlacePlayer(list[i].Team, list[i].PlayerId, points[i], list[i].Locomotion.Heading);
        }

        private void ClearAroundSpot(RestartRequest r, MatchPlayer taker)
        {
            float radius = infrastructure.RestartClearRadius;
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
                foreach (MatchPlayer p in team.Players)
                {
                    if (!p.IsActive || p == taker) continue;
                    bool kickoff = r.Kind == RestartKind.KickOff;
                    Vec2 pos = p.Locomotion.Position;
                    Vec2 d = pos - r.Position;
                    float dist = d.Magnitude;
                    bool moved = false;
                    if (dist < radius && (kickoff || p.Team != r.Team || !p.InGoal))
                    {
                        // out of the circle, along the line from the spot (or along the pitch when he stands on it)
                        Vec2 axis = dist > 1e-4f ? d * (1f / dist) : -AttackDirectionOf(p.Team);
                        pos = r.Position + axis * radius;
                        moved = true;
                    }
                    if (kickoff && !PitchBounds.InOwnHalf(pos, State.HomeAttacksPositiveX, p.Team))
                    {
                        pos = new Vec2(PitchBounds.OwnGoalSign(State.HomeAttacksPositiveX, p.Team) * infrastructure.KickoffHalfMargin, pos.Y);
                        moved = true;
                    }
                    if (moved) PlacePlayer(p.Team, p.PlayerId, Pitch.ClampPlayer(pos), p.Locomotion.Heading);
                }
        }

        /// <summary>
        /// The restart is ready: sets it up when that has not been done, resumes play (<see cref="ConfirmRestartReady"/>) and gives the ball to the TAKER (the assigned player, never the first of the
        /// list by default). Everybody else is outside the clearance circle, so nobody can pick the ball up by proximity. Returns false when the restart cannot start (a team still needs a goalkeeper).
        /// </summary>
        public bool ConfirmRestart()
        {
            if (State.Phase != MatchPhase.WaitingForKickoff && State.Phase != MatchPhase.WaitingForRestart) return false;
            // a free kick is set up again at the whistle (a card, a formation change or a substitution during the wait must not leave a stale wall)
            bool setUpAgain = State.PendingRestart.Kind == RestartKind.FreeKick || State.PendingRestart.Kind == RestartKind.Penalty || State.PendingRestart.Status == RestartStatus.Pending;
            if (setUpAgain && !PrepareRestart()) return false;
            RestartRequest r = State.PendingRestart;
            if (State.Home.NeedsGoalkeeper || State.Away.NeedsGoalkeeper) return false;
            if (!ConfirmRestartReady()) return false;
            r.Status = RestartStatus.Confirmed;
            State.PendingRestart = r;
            return GiveBallTo(r.Team, r.TakerId);
        }

        // ------------------------------------------------------------------ control, selection

        public void SetControl(TeamSide side, ControlSource source) { control[(int)side] = source; }
        public ControlSource GetControl(TeamSide side) { return control[(int)side]; }

        /// <summary>Selects (manually) the player a person controls on that side. Only an active player.</summary>
        public bool SelectPlayer(TeamSide side, string playerId)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            if (p == null || !p.IsActive) return false;
            selected[(int)side] = playerId;
            return true;
        }

        public void ClearSelection(TeamSide side) { selected[(int)side] = null; }

        /// <summary>The stored selection of a side, with no resolution and no side effect (what a snapshot reads).</summary>
        public string StoredSelection(TeamSide side) { return selected[(int)side]; }

        /// <summary>
        /// The player a person controls on that side. The selected one while he is on the pitch; when he is not (sent off, substituted, never chosen) the fallback: the ball carrier of the team, otherwise
        /// the first field player on the pitch, otherwise the goalkeeper in goal. The fallback is stored, so a later call gives the same player. (A smarter automatic selection is a later phase.)
        /// </summary>
        public string ControlledPlayerId(TeamSide side)
        {
            TeamMatchState team = State.Team(side);
            string current = selected[(int)side];
            MatchPlayer p = current != null ? team.Find(current) : null;
            if (p != null && p.IsActive) return current;
            MatchPlayer carrier = BallCarrier;
            if (carrier != null && carrier.Team == side && carrier.IsActive) p = carrier;
            else
            {
                p = null;
                foreach (MatchPlayer q in team.Players) if (q.IsActive && !q.InGoal) { p = q; break; }
                if (p == null) p = team.GoalkeeperInGoal;
            }
            selected[(int)side] = p != null ? p.PlayerId : null;
            return selected[(int)side];
        }

        /// <summary>Moves the selection to the next (+1) or previous (-1) active player of the side, in squad order, goalkeeper included. Returns the new player or null.</summary>
        public string CycleSelection(TeamSide side, int step = 1)
        {
            TeamMatchState team = State.Team(side);
            string current = ControlledPlayerId(side);
            int n = team.Players.Count;
            if (n == 0 || current == null) return null;
            int index = 0;
            for (int i = 0; i < n; i++) if (team.Players[i].PlayerId == current) { index = i; break; }
            int dir = step >= 0 ? 1 : -1;
            for (int k = 1; k <= n; k++)
            {
                MatchPlayer c = team.Players[((index + dir * k) % n + n) % n];
                if (c.IsActive) { selected[(int)side] = c.PlayerId; return c.PlayerId; }
            }
            return current;
        }

        // ------------------------------------------------------------------ the two ways in: a person and an intent (same Core)

        /// <summary>
        /// A person's input (already normalised) goes to the controlled player of that side: the stick drives his locomotion; a button becomes an <see cref="ActionInput"/> and goes through
        /// <see cref="PerformInput"/>, i.e. the mapper, a request and <see cref="Perform"/>.
        /// </summary>
        public HumanInputResult SubmitHumanInput(TeamSide side, NormalizedInput input)
        {
            var result = new HumanInputResult { PlayerId = ControlledPlayerId(side) };
            if (result.PlayerId == null) return result;
            NormalizedInput n = input.Sanitized();
            Vec2 stick = n.Move;
            result.Moved = ApplyMovementIntent(side, result.PlayerId, MovementIntent.Move(stick, stick.Magnitude, ControlSource.Human));
            if (n.HasAction)
            {
                result.ActionAttempted = true;
                result.Action = PerformInput(side, result.PlayerId, n.ToActionInput());
            }
            return result;
        }

        /// <summary>
        /// A movement intent (a person's stick or, later, the AI) becomes the player's locomotion input. When two intents reach the same player in the same step, the higher priority wins (equal: the last).
        /// Returns false when the player is unknown or not on the pitch, or a higher priority already took the step.
        /// </summary>
        public bool ApplyMovementIntent(TeamSide side, string playerId, MovementIntent intent)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            if (p == null || !p.IsActive || intent.Priority < p.IntentPriority) return false;
            p.IntentPriority = intent.Priority;
            p.PendingInput = intent.ToLocomotion().Stick;
            return true;
        }

        /// <summary>An action intent becomes the one <see cref="ActionRequest"/> and goes through <see cref="Perform"/>: no other route into the resolver exists.</summary>
        public ActionResult PerformIntent(ActionIntent intent) { return Perform(intent.ToRequest()); }

        // ------------------------------------------------------------------ targets

        private static bool IsKickAction(FootballActionKind kind)
        {
            switch (kind)
            {
                case FootballActionKind.ShortPass: case FootballActionKind.WallPass: case FootballActionKind.ThroughPass: case FootballActionKind.LongPass:
                case FootballActionKind.Chip: case FootballActionKind.Shot: case FootballActionKind.CurvedShot: case FootballActionKind.SkillLobPass:
                case FootballActionKind.Clearance: case FootballActionKind.GoalkeeperDistribution:
                    return true;
                default: return false;
            }
        }

        /// <summary>Resolves the target of a kick into a direction (from the kicker to the team-mate or the point). A player who cannot be resolved leaves the direction as it was and is reported as Unknown.</summary>
        private Vec2 ApplyTarget(in ActionRequest r, MatchPlayer p, Vec2 direction, ref ActionResult res)
        {
            res.TargetKind = r.Target.Kind;
            if (r.Target.Kind == PassTargetKind.Position)
            {
                if (!ActionModel.IsFinite(r.Target.Position)) { res.TargetKind = PassTargetKind.Unknown; return direction; }
                return ActionModel.DirectionOr(r.Target.Position - p.Locomotion.Position, direction);
            }
            if (r.Target.Kind == PassTargetKind.Player)
            {
                MatchPlayer t = r.Target.PlayerId != null ? State.Team(p.Team).Find(r.Target.PlayerId) : null;
                if (t == null || !t.IsActive || t == p) { res.TargetKind = PassTargetKind.Unknown; return direction; }
                res.TargetPlayerId = t.PlayerId;
                return ActionModel.DirectionOr(t.Locomotion.Position - p.Locomotion.Position, direction);
            }
            res.TargetKind = PassTargetKind.Unknown;
            return direction;
        }

        private ActionResult AfterKick(ActionResult res)
        {
            if (res.Outcome == ActionOutcome.Completed && res.TargetKind == PassTargetKind.Player && res.TargetPlayerId != null) State.Ball.IntendedReceiverId = res.TargetPlayerId;
            return res;
        }

        // ------------------------------------------------------------------ every step: players keep inside the pitch and apart

        private void StepPlayerSpace()
        {
            int n = 0;
            for (int side = 0; side < 2; side++)
            {
                TeamMatchState team = side == 0 ? State.Home : State.Away;
                for (int i = 0; i < team.Players.Count && n < spacePlayers.Length; i++)
                {
                    MatchPlayer p = team.Players[i];
                    p.IntentPriority = int.MinValue;
                    if (!p.IsActive) continue;
                    spacePlayers[n] = p;
                    spaceBuffer[n] = p.Locomotion.Position;
                    n++;
                }
            }
            if (!infrastructure.EnforcePlayerSpace) { for (int i = 0; i < n; i++) spacePlayers[i] = null; return; }
            if (n < 2 || !(infrastructure.MinSeparation > 0f)) { ClampSpace(n); return; }
            PlayerSeparation.Resolve(spaceBuffer, n, infrastructure.MinSeparation, infrastructure.SeparationIterations);
            ClampSpace(n);
        }

        private void ClampSpace(int n)
        {
            PitchBounds bounds = Pitch;
            for (int i = 0; i < n; i++)
            {
                spacePlayers[i].Locomotion.Position = bounds.ClampPlayer(spaceBuffer[i]);
                spacePlayers[i] = null;
            }
        }
    }
}
