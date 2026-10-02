using System;

namespace FS27.Core
{
    /// <summary>
    /// What a game mode HAS. The match engine coordinates; the rules say which things exist. The engine never asks "is it Arena?": it asks the rules.
    /// A rule set knows nothing about the competition (see <see cref="CompetitionResolver"/> for that).
    /// </summary>
    public interface IGameModeRules
    {
        GameMode Mode { get; }
        /// <summary>The pitch is closed by walls the ball rebounds off.</summary>
        bool UsesWalls { get; }
        bool HasFouls { get; }
        bool HasOffside { get; }
        bool HasCorners { get; }
        bool HasThrowIns { get; }
        bool HasGoalKicks { get; }
        bool HasCards { get; }
        bool HasPenalties { get; }
        bool HasReferee { get; }
        bool HasInjuries { get; }
        /// <summary>A real break (the Break phase) between periods. False = only a side switch, play goes straight on.</summary>
        bool HasHalfTimeBreak { get; }

        /// <summary>Can an event of this kind exist in this mode.</summary>
        bool Allows(GameplayEventKind kind);

        /// <summary>The ball against the pitch: goal, still in play, or out (with its restart).</summary>
        BallJudgement JudgeBall(FieldDimensions field, bool homeAttacksPositiveX, TeamSide kickoffTeam, in BallObservation ball, bool hasLastTouch, TeamSide lastTouch);

        /// <summary>Whether a substitution may be made in this match phase: a safe moment or valid interruption, never in the middle of live play.</summary>
        bool IsSafeMomentForSubstitution(MatchPhase phase);

        /// <summary>The surfaces the ball rebounds off (besides the ground). Empty when the mode has no walls.</summary>
        IBallCollider[] CreateBoundaryColliders(FieldDimensions field, MatchSettings settings);
    }

    public static class GameModeRules
    {
        private static readonly ArenaRules arena = new ArenaRules();
        private static readonly ClassicRules classic = new ClassicRules();

        public static IGameModeRules For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Arena: return arena;
                case GameMode.Classic: return classic;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }

    /// <summary>Arena: closed pitch, rebounds, continuous play. Nothing that interrupts exists.</summary>
    public sealed class ArenaRules : IGameModeRules
    {
        public GameMode Mode => GameMode.Arena;
        public bool UsesWalls => true;
        public bool HasFouls => false;
        public bool HasOffside => false;
        public bool HasCorners => false;
        public bool HasThrowIns => false;
        public bool HasGoalKicks => false;
        public bool HasCards => false;
        public bool HasPenalties => false;
        public bool HasReferee => false;
        public bool HasInjuries => false;
        public bool HasHalfTimeBreak => false;

        public bool Allows(GameplayEventKind kind)
        {
            switch (kind)
            {
                case GameplayEventKind.HalfTimeStarted:
                case GameplayEventKind.BallOutOfBounds:
                case GameplayEventKind.CornerAwarded:
                case GameplayEventKind.ThrowInAwarded:
                case GameplayEventKind.GoalKickAwarded:
                case GameplayEventKind.FoulCommitted:
                case GameplayEventKind.OffsideDetected:
                case GameplayEventKind.PenaltyAwarded:
                case GameplayEventKind.YellowCardIssued:
                case GameplayEventKind.RedCardIssued:
                case GameplayEventKind.PlayerDismissed:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Only a goal can happen: the walls keep the ball in, so "out" does not exist.</summary>
        public BallJudgement JudgeBall(FieldDimensions field, bool homeAttacksPositiveX, TeamSide kickoffTeam, in BallObservation ball, bool hasLastTouch, TeamSide lastTouch)
        {
            BallJudgement j = MatchRules.Evaluate(field, homeAttacksPositiveX, kickoffTeam, ball, hasLastTouch, lastTouch);
            return j.Outcome == BallOutcome.Goal ? j : new BallJudgement { Outcome = BallOutcome.InPlay };
        }

        /// <summary>Substitutions never stop an attack: only when the engine is not running live play (after a goal, at the side switch, waiting for a kick-off).</summary>
        public bool IsSafeMomentForSubstitution(MatchPhase phase)
        {
            return phase == MatchPhase.WaitingForKickoff || phase == MatchPhase.GoalCelebration || phase == MatchPhase.Break;
        }

        public IBallCollider[] CreateBoundaryColliders(FieldDimensions field, MatchSettings settings)
        {
            return new IBallCollider[] { new ArenaWalls(field, settings.ArenaWallRestitution, settings.ArenaWallFriction) };
        }
    }

    /// <summary>Classic: traditional football. Lines, restarts, fouls, offside, cards, referee.</summary>
    public sealed class ClassicRules : IGameModeRules
    {
        public GameMode Mode => GameMode.Classic;
        public bool UsesWalls => false;
        public bool HasFouls => true;
        public bool HasOffside => true;
        public bool HasCorners => true;
        public bool HasThrowIns => true;
        public bool HasGoalKicks => true;
        public bool HasCards => true;
        public bool HasPenalties => true;
        public bool HasReferee => true;
        public bool HasInjuries => false;
        public bool HasHalfTimeBreak => true;

        public bool Allows(GameplayEventKind kind)
        {
            return kind != GameplayEventKind.BallBouncedOffWall;
        }

        public BallJudgement JudgeBall(FieldDimensions field, bool homeAttacksPositiveX, TeamSide kickoffTeam, in BallObservation ball, bool hasLastTouch, TeamSide lastTouch)
        {
            return MatchRules.Evaluate(field, homeAttacksPositiveX, kickoffTeam, ball, hasLastTouch, lastTouch);
        }

        /// <summary>Valid interruptions only: restarts, kick-offs, goal celebrations and breaks. Never while the ball is live.</summary>
        public bool IsSafeMomentForSubstitution(MatchPhase phase)
        {
            return phase == MatchPhase.WaitingForKickoff || phase == MatchPhase.WaitingForRestart || phase == MatchPhase.GoalCelebration || phase == MatchPhase.Break;
        }

        public IBallCollider[] CreateBoundaryColliders(FieldDimensions field, MatchSettings settings)
        {
            return new IBallCollider[0];
        }
    }
}
