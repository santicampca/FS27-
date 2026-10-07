using System;

namespace FS27.Core
{
    /// <summary>
    /// The geometry of the pitch in the Core (field space: centre (0,0), X along the pitch, Y across it): its size, the goal zones and where a player may stand. It is pure geometry:
    /// what the BALL does at the lines is decided by the mode's rules (Arena: walls it rebounds off; Classic: it leaves play), see <see cref="BallLimit"/>. Nothing here is Unity.
    /// </summary>
    public sealed class PitchBounds
    {
        public FieldDimensions Field { get; }
        /// <summary>The mode's rules say the pitch is closed by walls (<see cref="IGameModeRules.UsesWalls"/>).</summary>
        public bool HasWalls { get; }
        public InfrastructureTuning Tuning { get; }
        public float HalfLength => Field.Length * 0.5f;
        public float HalfWidth => Field.Width * 0.5f;

        public PitchBounds(FieldDimensions field, bool hasWalls, InfrastructureTuning tuning = null)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            HasWalls = hasWalls;
            Tuning = tuning ?? new InfrastructureTuning();
        }

        /// <summary>What the ball does at the lines: Arena rebounds off the walls, Classic leaves play (a restart).</summary>
        public BallLimit BallLimit { get { return HasWalls ? BallLimit.Walls : BallLimit.LeavesPlay; } }

        /// <summary>Inside the lines (the ball's centre may touch the line).</summary>
        public bool Contains(Vec2 p) { return Math.Abs(p.X) <= HalfLength && Math.Abs(p.Y) <= HalfWidth; }

        /// <summary>The nearest point where a PLAYER may stand: inside the lines plus <see cref="InfrastructureTuning.PlayerBoundsMargin"/>.</summary>
        public Vec2 ClampPlayer(Vec2 p)
        {
            float x = Math.Max(-HalfLength - Tuning.PlayerBoundsMargin, Math.Min(HalfLength + Tuning.PlayerBoundsMargin, p.X));
            float y = Math.Max(-HalfWidth - Tuning.PlayerBoundsMargin, Math.Min(HalfWidth + Tuning.PlayerBoundsMargin, p.Y));
            return new Vec2(x, y);
        }

        /// <summary>The goal line a team defends: +1 = the +X end, -1 = the -X end.</summary>
        public static int OwnGoalSign(bool homeAttacksPositiveX, TeamSide team)
        {
            bool attacksPlusX = homeAttacksPositiveX == (team == TeamSide.Home);
            return attacksPlusX ? -1 : 1;
        }

        /// <summary>The centre of the goal a team defends.</summary>
        public Vec2 OwnGoalCentre(bool homeAttacksPositiveX, TeamSide team) { return new Vec2(OwnGoalSign(homeAttacksPositiveX, team) * HalfLength, 0f); }

        /// <summary>The centre of the goal a team attacks.</summary>
        public Vec2 OpponentGoalCentre(bool homeAttacksPositiveX, TeamSide team) { return new Vec2(-OwnGoalSign(homeAttacksPositiveX, team) * HalfLength, 0f); }

        /// <summary>The goal zone of a team (its goalkeeper's area): in front of the goal line it defends.</summary>
        public bool IsInGoalZone(Vec2 p, bool homeAttacksPositiveX, TeamSide team)
        {
            float sign = OwnGoalSign(homeAttacksPositiveX, team);
            float depth = (HalfLength - sign * p.X);
            return depth >= 0f && depth <= Tuning.GoalZoneDepth && Math.Abs(p.Y) <= Tuning.GoalZoneHalfWidth;
        }

        /// <summary>Keeps a point inside the goal zone of a team.</summary>
        public Vec2 ClampToGoalZone(Vec2 p, bool homeAttacksPositiveX, TeamSide team)
        {
            float sign = OwnGoalSign(homeAttacksPositiveX, team);
            float depth = Math.Max(0f, Math.Min(Tuning.GoalZoneDepth, HalfLength - sign * p.X));
            float y = Math.Max(-Tuning.GoalZoneHalfWidth, Math.Min(Tuning.GoalZoneHalfWidth, p.Y));
            return new Vec2(sign * (HalfLength - depth), y);
        }

        /// <summary>True when the point is in the half of the pitch a team defends.</summary>
        public static bool InOwnHalf(Vec2 p, bool homeAttacksPositiveX, TeamSide team) { return OwnGoalSign(homeAttacksPositiveX, team) * p.X >= 0f; }
    }

    /// <summary>What the ball does when it reaches the lines of a pitch.</summary>
    public enum BallLimit
    {
        /// <summary>Arena: closed pitch, the ball rebounds off the walls.</summary>
        Walls = 0,
        /// <summary>Classic: the ball leaves play and a restart follows.</summary>
        LeavesPlay = 1
    }
}
