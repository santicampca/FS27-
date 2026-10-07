using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The state of the match REDUCED to what gameplay and the AI need: the match, the ball and every player of both squads (on the pitch, on the bench, substituted, dismissed). It is a derived
    /// copy built by <see cref="Capture"/> from <see cref="MatchState"/>: it holds no reference to it (nothing in here can change the match) and no player definition. Its public API is read-only.
    /// To avoid garbage, <see cref="Refresh"/> refills the same object each step (its arrays are allocated once); <see cref="Clone"/> keeps a copy that never changes.
    /// </summary>
    public sealed class WorldState
    {
        private readonly WorldPlayer[] players = new WorldPlayer[MaxPlayers];
        private int count;
        private int homeStart, homeCount, awayStart, awayCount;
        private WorldMatch match;
        private WorldBall ball;

        /// <summary>A squad has 10 players (6 starters + 4 substitutes): two squads, 20.</summary>
        public const int MaxPlayers = 2 * (MatchDefaults.StartersPerTeam + MatchDefaults.BenchPerTeam);

        public WorldMatch Match => match;
        public WorldBall Ball => ball;
        public int PlayerCount => count;
        public FieldDimensions Field { get; private set; }
        /// <summary>The numbers of the queries (radii, clearances) the match was configured with.</summary>
        public InfrastructureTuning Tuning { get; private set; }
        public float BallRadius { get; private set; }
        public BallParameters BallParameters { get; private set; }
        /// <summary>Which side a person controls, if any, and who (the controlled player of each side).</summary>
        public string ControlledHome { get; private set; }
        public string ControlledAway { get; private set; }
        public GoalkeeperSlot HomeGoalkeeperSlot { get; private set; }
        public GoalkeeperSlot AwayGoalkeeperSlot { get; private set; }

        /// <summary>The player at an index (0..PlayerCount-1), a copy.</summary>
        public WorldPlayer this[int index] { get { return players[index]; } }
        public int TeamStart(TeamSide side) { return side == TeamSide.Home ? homeStart : awayStart; }
        public int TeamCount(TeamSide side) { return side == TeamSide.Home ? homeCount : awayCount; }
        public GoalkeeperSlot GoalkeeperSlotOf(TeamSide side) { return side == TeamSide.Home ? HomeGoalkeeperSlot : AwayGoalkeeperSlot; }
        public string ControlledPlayerOf(TeamSide side) { return side == TeamSide.Home ? ControlledHome : ControlledAway; }

        /// <summary>The index of a player, or -1.</summary>
        public int IndexOf(string playerId)
        {
            for (int i = 0; i < count; i++) if (players[i].PlayerId == playerId) return i;
            return -1;
        }

        public bool TryGetPlayer(string playerId, out WorldPlayer player)
        {
            int i = IndexOf(playerId);
            player = i >= 0 ? players[i] : default(WorldPlayer);
            return i >= 0;
        }

        /// <summary>The read-only view the AI observes.</summary>
        public WorldView View { get { return new WorldView(this, Tuning); } }

        public static WorldState Capture(MatchController controller)
        {
            var s = new WorldState();
            s.Refresh(controller);
            return s;
        }

        /// <summary>A copy that no later <see cref="Refresh"/> of this object can change.</summary>
        public WorldState Clone()
        {
            var c = new WorldState();
            Array.Copy(players, c.players, count);
            c.count = count; c.homeStart = homeStart; c.homeCount = homeCount; c.awayStart = awayStart; c.awayCount = awayCount;
            c.match = match; c.ball = ball; c.Field = Field; c.Tuning = Tuning; c.BallRadius = BallRadius; c.BallParameters = BallParameters;
            c.ControlledHome = ControlledHome; c.ControlledAway = ControlledAway; c.HomeGoalkeeperSlot = HomeGoalkeeperSlot; c.AwayGoalkeeperSlot = AwayGoalkeeperSlot;
            return c;
        }

        /// <summary>Refills this snapshot from the match (allocation-free after the first call). The match is only read, and the controlled-player resolution is NOT triggered: it reads what is stored.</summary>
        public void Refresh(MatchController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            MatchState st = controller.State;
            Field = controller.Field;
            Tuning = controller.Infrastructure;
            controller.RefreshHomePositions();
            BallParameters = controller.BallParameters;
            BallRadius = BallParameters.Radius;

            RefreshMatch(st);
            RefreshBall(st, controller);

            ControlledHome = controller.StoredSelection(TeamSide.Home);
            ControlledAway = controller.StoredSelection(TeamSide.Away);
            HomeGoalkeeperSlot = st.Home.GoalkeeperSlot;
            AwayGoalkeeperSlot = st.Away.GoalkeeperSlot;

            count = 0;
            homeStart = 0; homeCount = Fill(controller, st.Home, ControlledHome, controller.GetControl(TeamSide.Home));
            awayStart = count; awayCount = Fill(controller, st.Away, ControlledAway, controller.GetControl(TeamSide.Away));
        }

        private void RefreshMatch(MatchState st)
        {
            match.Mode = st.Mode; match.Competition = st.CompetitionType; match.Phase = st.Phase; match.Period = st.Period;
            match.MatchTime = st.MatchTimeSeconds; match.PeriodElapsed = st.Clock.Elapsed; match.PeriodRemaining = st.Clock.Remaining;
            match.HomeGoals = st.Score.Home; match.AwayGoals = st.Score.Away; match.HomeAttacksPositiveX = st.HomeAttacksPositiveX;
            match.BallLive = st.IsBallLive; match.HasPossession = st.Ball.HasPossession; match.PossessingTeam = st.Ball.PossessingTeam;
            bool pending = st.Phase == MatchPhase.WaitingForKickoff || st.Phase == MatchPhase.WaitingForRestart;
            match.RestartPending = pending;
            RestartRequest r = st.PendingRestart;
            match.RestartKind = pending ? r.Kind : RestartKind.KickOff;
            match.RestartTeam = r.Team; match.RestartTakerId = pending ? r.TakerId : null; match.RestartStatus = r.Status; match.RestartPosition = r.Position;
        }

        private void RefreshBall(MatchState st, MatchController controller)
        {
            MatchBall b = st.Ball;
            Vec3 v = b.Physics.Velocity;
            ball.Position = b.Physics.Position; ball.Velocity = v;
            ball.Ground = new Vec2(b.Physics.Position.X, b.Physics.Position.Z);
            float speed = (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
            ball.Speed = speed;
            ball.Direction = speed > controller.Infrastructure.BallDirectionMinSpeed ? new Vec2(v.X / speed, v.Z / speed) : Vec2.Zero;
            ball.Height = b.Height(BallRadius);
            ball.Mode = b.Mode; ball.IsGrounded = b.Physics.IsGrounded;
            ball.HasPossessor = b.HasPossession && b.PossessingPlayerId != null; ball.PossessingTeam = b.PossessingTeam; ball.PossessorId = ball.HasPossessor ? b.PossessingPlayerId : null;
            ball.HasLastTouch = b.HasLastTouch; ball.LastTouchTeam = b.LastTouchTeam; ball.LastTouchPlayerId = b.LastTouchPlayerId;
            ball.IntendedReceiverId = b.IntendedReceiverId;
            ball.SecondsSincePossessionChange = b.SecondsSincePossessionChange;
        }

        private int Fill(MatchController controller, TeamMatchState team, string controlled, ControlSource control)
        {
            int n = 0;
            IReadOnlyList<MatchPlayer> list = team.Players;
            for (int i = 0; i < list.Count && count < MaxPlayers; i++, n++)
            {
                MatchPlayer p = list[i];
                Vec2 facing = p.Locomotion.FacingDirection;
                float top = p.LocomotionProfile.MaxSpeed;
                Vec2 relative = RelativeToOwnGoal(p.Locomotion.Position, controller, team.Side);
                players[count++] = new WorldPlayer
                {
                    PlayerId = p.PlayerId, Team = team.Side, Slot = i,
                    Position = p.Locomotion.Position, Home = p.Home, HasHome = p.HasHome, Velocity = p.Locomotion.Velocity, Facing = facing, Heading = p.Locomotion.Heading, Speed = p.Locomotion.CurrentSpeed, MaxSpeed = top,
                    Stamina = p.Locomotion.CurrentStamina, Role = p.Role,
                    Zone = p.InGoal ? PitchZone.Goal : PlayingProfileDefaults.ZoneOfRelativePosition(relative),
                    Availability = (PlayerAvailability)(int)p.Status,
                    IsGoalkeeper = p.InGoal, IsEmergencyKeeper = p.IsEmergencyKeeper,
                    HasBall = controller.State.Ball.HasPossession && controller.State.Ball.PossessingTeam == team.Side && controller.State.Ball.PossessingPlayerId == p.PlayerId,
                    IsControlled = control == ControlSource.Human && controlled != null && controlled == p.PlayerId && p.IsActive,
                    ActionLocked = p.ActionLockRemaining > 0f, KickCooldown = p.KickCooldownRemaining > 0f, IsSliding = p.IsSliding, IsPressing = p.IsPressing, IsShielding = p.IsShielding, IsEvading = p.IsEvading,
                    PreferredFoot = p.ActionProfile.PreferredFoot, Profile = p.ActionProfile, Skills = p.Skills
                };
            }
            return n;
        }

        /// <summary>(depth, width) of a point as the formation reads it: depth 0 = own goal line, 1 = the opponent's; width 0..1.</summary>
        private static Vec2 RelativeToOwnGoal(Vec2 p, MatchController controller, TeamSide team)
        {
            FieldDimensions f = controller.Field;
            bool attacksPlusX = controller.State.HomeAttacksPositiveX == (team == TeamSide.Home);
            float x = attacksPlusX ? p.X : -p.X, y = attacksPlusX ? p.Y : -p.Y;
            return new Vec2(x / f.Length + 0.5f, y / f.Width + 0.5f);
        }
    }

    // PlayerAvailability mirrors MatchPlayerStatus (same names, same numbers): a test keeps them in step.
}
