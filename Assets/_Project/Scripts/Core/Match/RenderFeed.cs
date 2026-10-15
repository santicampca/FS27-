using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Phase 40: the contract between the Core and ANY future renderer (a 3D client in any engine, the web client, a replay viewer). Version of the shape of <see cref="RenderFrame"/>: it changes when a field is removed or its meaning changes
    /// (adding a field does not change it).
    /// </summary>
    public static class RenderContract
    {
        public const int Version = 1;
    }

    /// <summary>What a player is doing, reduced to what an animation state machine needs. Derived from the Core state, never the other way round.</summary>
    public enum RenderPose { Idle = 0, Walk = 1, Jog = 2, Run = 3, Sprint = 4, Slide = 5, Shield = 6, Press = 7, Evade = 8, Down = 9 }

    /// <summary>One player of a frame. Positions in field space (X = length, Y = width, metres, centre of the pitch at 0,0); heading in radians (0 = +Y, growing towards +X).</summary>
    public struct RenderPlayer
    {
        public string PlayerId;
        public TeamSide Team;
        public bool OnPitch;
        public bool IsGoalkeeper;
        public bool HasBall;
        public bool Controlled;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Heading;
        public float Speed;
        /// <summary>0..1.</summary>
        public float Stamina01;
        public RenderPose Pose;
        /// <summary>The last action this player performed (a kick, a header, a save...), how long ago and how well; None when nothing yet. The renderer plays the matching animation once.</summary>
        public FootballActionKind LastAction;
        public ActionDetail LastDetail;
        public ActionOutcome LastOutcome;
        public float LastActionAge;
        public float LastActionQuality;
        public int YellowCards;
        public int RedCards;
    }

    public struct RenderBall
    {
        /// <summary>World space: X = length, Y = HEIGHT, Z = width (the Core's ball convention).</summary>
        public Vec3 Position;
        public Vec3 Velocity;
        public Vec3 Spin;
        public bool Grounded;
        public BallMode Mode;
        public float Radius;
        public bool HasPossession;
        public string PossessorId;
    }

    /// <summary>One thing that happened since the last frame, for sound, effects, camera cuts and replays.</summary>
    public struct RenderEvent
    {
        public int Sequence;
        public GameplayEventKind Kind;
        public float MatchTime;
        public bool HasTeam;
        public TeamSide Team;
        public string PlayerId;
        public string OtherPlayerId;
        public Vec3 Position;
        public string Detail;
    }

    /// <summary>
    /// A complete, read-only picture of the match at one instant. Plain data: no reference to the Core, no engine type. <see cref="Frame"/> grows by one per call to <see cref="RenderFeed.Fill"/>. The arrays are reused between
    /// frames (no garbage): copy what you need to keep.
    /// </summary>
    public sealed class RenderFrame
    {
        public int ContractVersion = RenderContract.Version;
        public int Frame;
        public float MatchTime;
        public MatchPhase Phase;
        public MatchPeriod Period;
        public GameMode Mode;
        public int ScoreHome, ScoreAway;
        public float ClockRemaining;
        public bool HomeAttacksPositiveX;
        public FieldDimensions Field;
        public RenderBall Ball = new RenderBall();
        public RenderPlayer[] Players = new RenderPlayer[0];
        public int PlayerCount;
        public readonly List<RenderEvent> Events = new List<RenderEvent>();
    }

    /// <summary>
    /// Phase 40: builds <see cref="RenderFrame"/>s from a match. It only READS the match (and listens to its events); it never writes to it, so attaching or not attaching it cannot change a result. Attach it once after the match is created,
    /// call <see cref="Fill"/> once per presented frame (not once per simulation step: events and last actions accumulate until the next fill).
    /// </summary>
    public sealed class RenderFeed
    {
        private struct Last { public FootballActionKind Kind; public ActionDetail Detail; public ActionOutcome Outcome; public float At; public float Quality; public bool Set; }

        private readonly MatchController match;
        private readonly Dictionary<string, Last> last = new Dictionary<string, Last>();
        private readonly List<RenderEvent> pending = new List<RenderEvent>();
        private int frame;

        /// <summary>Walking, jogging, running and sprinting from the speed (m/s): thresholds in one place.</summary>
        public float WalkBelow = 1.2f, JogBelow = 3.0f, RunBelow = 5.0f;

        public RenderFeed(MatchController match)
        {
            this.match = match;
            match.ActionPerformed += OnAction;
            match.Events.Published += OnEvent;
        }

        public void Detach()
        {
            match.ActionPerformed -= OnAction;
            match.Events.Published -= OnEvent;
        }

        private void OnAction(ActionResult r)
        {
            if (r.PlayerId == null) return;
            last[r.PlayerId] = new Last { Kind = r.Kind, Detail = r.Detail, Outcome = r.Outcome, At = match.State.MatchTimeSeconds, Quality = r.Quality, Set = true };
        }

        private void OnEvent(GameplayEvent e)
        {
            pending.Add(new RenderEvent { Sequence = e.Sequence, Kind = e.Kind, MatchTime = e.MatchTime, HasTeam = e.HasTeam, Team = e.Team, PlayerId = e.PlayerId, OtherPlayerId = e.OtherPlayerId, Position = e.Position, Detail = e.Detail });
        }

        /// <summary>Refills <paramref name="f"/> with the current state and hands over the events since the last fill.</summary>
        public void Fill(RenderFrame f)
        {
            MatchState s = match.State;
            int n = s.Home.Players.Count + s.Away.Players.Count;
            if (f.Players.Length < n) f.Players = new RenderPlayer[n];
            f.ContractVersion = RenderContract.Version;
            f.Frame = ++frame;
            f.MatchTime = s.MatchTimeSeconds; f.Phase = s.Phase; f.Period = s.Period; f.Mode = s.Mode;
            f.ScoreHome = s.Score.Home; f.ScoreAway = s.Score.Away; f.ClockRemaining = s.Clock.Remaining; f.HomeAttacksPositiveX = s.HomeAttacksPositiveX;
            f.Field = match.Pitch.Field;
            MatchBall b = s.Ball;
            f.Ball.Position = b.Physics.Position; f.Ball.Velocity = b.Physics.Velocity; f.Ball.Spin = b.Physics.Spin; f.Ball.Grounded = b.Physics.IsGrounded;
            f.Ball.Mode = b.Mode; f.Ball.Radius = match.BallParameters.Radius; f.Ball.HasPossession = b.HasPossession; f.Ball.PossessorId = b.PossessingPlayerId;
            string homeCtl = match.ControlledPlayerId(TeamSide.Home), awayCtl = match.GetControl(TeamSide.Away) == ControlSource.Human ? match.ControlledPlayerId(TeamSide.Away) : null;
            int i = 0;
            for (int side = 0; side < 2; side++)
            {
                TeamMatchState team = side == 0 ? s.Home : s.Away;
                for (int k = 0; k < team.Players.Count; k++)
                {
                    MatchPlayer p = team.Players[k];
                    RenderPlayer r = default(RenderPlayer);
                    r.PlayerId = p.PlayerId; r.Team = p.Team; r.OnPitch = p.IsActive; r.IsGoalkeeper = p.InGoal;
                    r.HasBall = b.HasPossession && b.PossessingPlayerId == p.PlayerId;
                    r.Controlled = p.PlayerId == homeCtl || p.PlayerId == awayCtl;
                    r.Position = p.Locomotion.Position; r.Velocity = p.Locomotion.Velocity; r.Heading = p.Locomotion.Heading;
                    r.Speed = p.Locomotion.Velocity.Magnitude;
                    r.Stamina01 = p.Runtime.Stamina01;
                    r.Pose = PoseOf(p, r.Speed);
                    if (last.TryGetValue(p.PlayerId, out Last l) && l.Set) { r.LastAction = l.Kind; r.LastDetail = l.Detail; r.LastOutcome = l.Outcome; r.LastActionAge = s.MatchTimeSeconds - l.At; r.LastActionQuality = l.Quality; }
                    else r.LastActionAge = float.PositiveInfinity;
                    r.YellowCards = p.YellowCards; r.RedCards = p.RedCards;
                    f.Players[i++] = r;
                }
            }
            f.PlayerCount = i;
            f.Events.Clear();
            f.Events.AddRange(pending);
            pending.Clear();
        }

        private RenderPose PoseOf(MatchPlayer p, float speed)
        {
            if (!p.IsActive) return RenderPose.Down;
            if (p.IsSliding) return RenderPose.Slide;
            if (p.IsEvading) return RenderPose.Evade;
            if (p.IsShielding) return RenderPose.Shield;
            if (p.IsPressing && speed < RunBelow) return RenderPose.Press;
            if (speed < 0.1f) return RenderPose.Idle;
            if (speed < WalkBelow) return RenderPose.Walk;
            if (speed < JogBelow) return RenderPose.Jog;
            if (speed < RunBelow) return RenderPose.Run;
            return RenderPose.Sprint;
        }
    }
}
