using System;

namespace FS27.Core
{
    public enum TeamSide
    {
        Home = 0,
        Away = 1
    }

    public static class TeamSideExtensions
    {
        public static TeamSide Opponent(this TeamSide side)
        {
            return side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        }
    }

    public enum MatchPhase
    {
        NotStarted,
        /// <summary>Waiting for the presentation to place players and ball for a kick-off.</summary>
        WaitingForKickoff,
        Playing,
        /// <summary>A goal was just scored: celebration time before the next kick-off.</summary>
        GoalCelebration,
        /// <summary>Ball left the pitch: waiting for the presentation to set up a throw-in / corner / goal kick.</summary>
        WaitingForRestart,
        Paused,
        Finished
    }

    public enum RestartKind
    {
        KickOff,
        ThrowIn,
        GoalKick,
        Corner
    }

    public enum BallOutcome
    {
        InPlay,
        Goal,
        OutOfBounds
    }

    /// <summary>The ball as the rules see it. Filled by the game from the real ball every tick.</summary>
    public struct BallObservation
    {
        public Vec3 Position;
        public float Radius;

        public BallObservation(Vec3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>What the rules say about the ball right now.</summary>
    public struct BallJudgement
    {
        public BallOutcome Outcome;
        public TeamSide ScoringTeam;
        public RestartKind Restart;
        public TeamSide RestartTeam;
        /// <summary>Where the restart is taken, in field space (X length, Y width).</summary>
        public Vec2 RestartPosition;
    }

    /// <summary>"Please set up this restart": raised by the controller, handled by the presentation.</summary>
    public struct RestartRequest
    {
        public RestartKind Kind;
        public TeamSide Team;
        public Vec2 Position;

        public RestartRequest(RestartKind kind, TeamSide team, Vec2 position)
        {
            Kind = kind;
            Team = team;
            Position = position;
        }
    }

    public struct MatchResult
    {
        public int HomeGoals;
        public int AwayGoals;
        public bool IsDraw;
        /// <summary>Only meaningful when <see cref="IsDraw"/> is false.</summary>
        public TeamSide Winner;
    }

    /// <summary>Pitch geometry in field space: centre at (0,0), X = length, Y (world Z) = width.</summary>
    [Serializable]
    public class FieldDimensions
    {
        public float Length = 40f;
        public float Width = 25f;
        public float GoalWidth = 5f;
        /// <summary>Crossbar height.</summary>
        public float GoalHeight = 2f;
        /// <summary>Corner kicks are taken this far inside the corner.</summary>
        public float CornerInset = 0.5f;
        /// <summary>Goal kicks are taken this far in front of the end line.</summary>
        public float GoalKickDistance = 2f;
        /// <summary>Throw-ins keep at least this distance from the end lines.</summary>
        public float ThrowInEndMargin = 1f;
    }

    [Serializable]
    public class MatchSettings
    {
        public float DurationSeconds = 180f;
        public float GoalCelebrationSeconds = 3f;
        public TeamSide KickoffTeam = TeamSide.Home;
        /// <summary>If true Home attacks the +X end (and defends -X).</summary>
        public bool HomeAttacksPositiveX = true;

        /// <summary>The team that attacks the end line on the given side (+1 = +X end, -1 = -X end).</summary>
        public TeamSide AttackerAtEnd(int endSign)
        {
            bool homeAttacksThatEnd = HomeAttacksPositiveX == (endSign > 0);
            return homeAttacksThatEnd ? TeamSide.Home : TeamSide.Away;
        }
    }

    public sealed class MatchScore
    {
        public int Home { get; private set; }
        public int Away { get; private set; }

        public int Get(TeamSide side)
        {
            return side == TeamSide.Home ? Home : Away;
        }

        public void Add(TeamSide side)
        {
            if (side == TeamSide.Home) Home++;
            else Away++;
        }
    }

    public sealed class MatchClock
    {
        public float Duration { get; set; }
        public float Elapsed { get; private set; }
        public float Remaining => Math.Max(0f, Duration - Elapsed);
        public bool IsFinished => Elapsed >= Duration;

        public MatchClock(float duration)
        {
            Duration = duration;
        }

        public void Advance(float dt)
        {
            if (dt <= 0f) return;
            Elapsed = Math.Min(Duration, Elapsed + dt);
        }
    }
}
