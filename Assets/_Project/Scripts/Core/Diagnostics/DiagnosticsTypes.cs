using System;

namespace FS27.Core
{
    /// <summary>
    /// What to observe (Phase 12). Diagnostics only ever READ the match: nothing here changes a decision, an action, a position, the ball, stamina or the random numbers. The windows below are measuring
    /// choices (how long to wait before calling an outcome), not gameplay values.
    /// </summary>
    public sealed class DiagnosticsConfig
    {
        public bool Enabled = true;
        /// <summary>Keep a trace of the carrier's decisions (with every term of the winning and the runner-up candidate) for one player and a window of steps. Off by default.</summary>
        public bool TraceEnabled;
        /// <summary>The player to trace, or null for every carrier.</summary>
        public string TracePlayerId;
        public int TraceStartStep;
        public int TraceEndStep = int.MaxValue;
        /// <summary>How long after a kick a pass is followed before it is called missed (seconds of match time).</summary>
        public float PassWindowSeconds = 6f;
        /// <summary>How long after a dribble, protect or hold begins possession is checked.</summary>
        public float PossessionWindowSeconds = 1f;
        /// <summary>How long after a shot its fate is followed.</summary>
        public float ShotWindowSeconds = 3f;
        /// <summary>Between two decisions of the same kind this many steps apart or fewer count as the same episode.</summary>
        public int EpisodeGapSteps = 30;
        /// <summary>A carrier slower than this (m/s) when a duel happens counts as standing still.</summary>
        public float StationarySpeed = 0.5f;
        /// <summary>Phase 14: count only what ONE team does (its decisions, actions, passes, shots, duels, stamina, possessions and goals), to compare the two sides of the same match.</summary>
        public bool OnlyOneTeam;
        public TeamSide Team;
        /// <summary>The attacking third, as a fraction of the pitch length.</summary>
        public float FinalThirdFraction = 0.3333f;
        /// <summary>A possession with the ball this close to the opposing goal (metres) is a dangerous one.</summary>
        public float DangerDistance = 9f;
        /// <summary>A shot candidate that scores at least this is a clear chance.</summary>
        public float ClearChanceScore = 2.5f;
        /// <summary>Phase 15: a carrier decision with at least this pressure counts as made under pressure, and one with at most this stamina as made tired.</summary>
        public float PressedLevel = 0.5f;
        public float TiredLevel = 30f;
    }

    /// <summary>How a pass ended, as far as the core lets us see (never guessed: <see cref="NotObserved"/> when it could not be told).</summary>
    public enum PassOutcome
    {
        /// <summary>Still being followed.</summary>
        Pending = 0,
        /// <summary>A team-mate other than the passer got the ball.</summary>
        Completed,
        /// <summary>An opponent got the ball and the core published an interception or block by him.</summary>
        Intercepted,
        /// <summary>Nobody got the ball within the window (it went loose or stopped).</summary>
        MissedTarget,
        /// <summary>The ball stopped being live (a goal, a whistle, a restart) before the pass was settled.</summary>
        Invalidated,
        /// <summary>An opponent got the ball without an interception: a loose ball, a tackle after the reception.</summary>
        PossessionLostOther,
        /// <summary>The ball left the pitch (Classic).</summary>
        OutOfPlay,
        /// <summary>The passer himself had the ball again (the ball never found a team-mate).</summary>
        ReturnedToPasser,
        /// <summary>The evidence was not enough to tell.</summary>
        Unknown,
        /// <summary>The simulation ended while it was still being followed.</summary>
        NotObserved
    }

    /// <summary>What became of possession after a dribble, a protect or a hold began.</summary>
    public enum WindowOutcome
    {
        Pending = 0,
        /// <summary>His team still had the ball at the end of the window.</summary>
        Retained,
        /// <summary>He kicked it (a pass or a shot) before the window ended: the episode ended by his own decision, so possession says nothing.</summary>
        Released,
        /// <summary>An opponent got the ball within the window.</summary>
        Lost,
        /// <summary>Nobody had the ball at the end of the window.</summary>
        Loose,
        /// <summary>The ball was not live, or the simulation ended.</summary>
        Unknown
    }

    /// <summary>What became of a shot.</summary>
    public enum ShotOutcome
    {
        Pending = 0,
        Goal,
        /// <summary>An opponent (the goalkeeper included) got the ball: a save, a block, a clearance.</summary>
        OpponentPossession,
        /// <summary>His own team got the ball back (a rebound).</summary>
        TeamPossession,
        OutOfPlay,
        /// <summary>Still rolling at the end of the window, or the match stopped for another reason.</summary>
        Unknown
    }

    /// <summary>One pass into space, from the request to the outcome.</summary>
    public struct SpaceAttempt
    {
        public int Step;
        public string PasserId;
        public string RunnerId;
        public TeamSide Team;
        public FootballActionKind Kind;
        public Vec2 Target;
        public float PasserToTarget, RunnerToTarget, BallToTarget;
        public float RunnerSpeed, BallSpeed, Power;
        /// <summary>Seconds the runner and the nearest opponent need to reach the target at their top speed (times the through-pass speed share), the distance of the opposing goalkeeper to it, and the distance of the target to the goal being attacked.</summary>
        public float RunnerArrival, RivalArrival, KeeperDistance, TargetToGoal;
        public string NearestOpponentId;
        public float OpponentToTarget;
        public string SkillId;
        public PassOutcome Outcome;
        public string ReceiverId;
        /// <summary>The receiver was the runner the pass was meant for.</summary>
        public bool ReceivedByRunner;
        public int ResolvedStep;
        /// <summary>How far the ball was from the target when the outcome was settled.</summary>
        public float FinalBallToTarget;
    }

    /// <summary>One duel for the ball (a tackle, a slide or a challenge on the carrier) and, if it was a foul, what led to it.</summary>
    public struct ContactRecord
    {
        public int Step;
        public FootballActionKind Kind;
        public string DefenderId, CarrierId;
        public TeamSide DefenderTeam;
        public ActionOutcome Outcome;
        public ActionDetail Detail;
        public bool Foul;
        public bool FromBehind;
        public bool DefenderAggressive;
        public bool CarrierShielding, CarrierEvading;
        public float Distance;
        public float RelativeSpeed;
        public float DefenderStamina, CarrierStamina;
        public float DefenderSpeed, CarrierSpeed;
        /// <summary>The report quality of the duel: 0..1 (the margin of the duel when it did not hit either end).</summary>
        public float Quality;
        public float DefenderToOwnGoal;
        public PitchZone Zone;
        public DecisionKind CarrierLastKind;
        public bool HasCarrierLastKind;
        public PlayerRole DefenderRole, CarrierRole;
    }

    /// <summary>One carrier decision in the trace: what won, what came second and what they were made of.</summary>
    public struct CarrierTrace
    {
        public int Step;
        public string PlayerId;
        public TeamSide Team;
        public CandidateType Best;
        public float BestScore;
        public CandidateType RunnerUp;
        public float RunnerUpScore;
        public float Margin;
        public float Pressure, Stamina, DistanceToGoal;
        public int AvailableMask;
        public CandidateParts BestParts;
        public CandidateParts RunnerUpParts;
    }
}
