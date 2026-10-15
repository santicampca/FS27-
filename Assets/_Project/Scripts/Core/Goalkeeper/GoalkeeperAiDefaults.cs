using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the goalkeeper AI (Phase 16), in one place. They say how a keeper stands, when he can start to move and how he decides to come out; WHO he is (his reflexes, his positioning, his command)
    /// comes from his own profile and HOW WELL a level uses it comes from the difficulty (<see cref="DecisionProfile"/>). Nothing here changes how far a keeper can reach or how well he catches: that stays in the Core
    /// (<see cref="ActionTuning"/>). DECISIÓN FASE 16: all of them are starting values, tuned by simulation only.
    /// </summary>
    public static class GoalkeeperAiDefaults
    {
        /// <summary>The goalkeeper AI decides for the keeper in goal. Off, he plays the Phase 15 rule (stand on the ball line; chase the ball inside seven metres): used to compare, never to play.</summary>
        public const bool Enabled = true;

        // ---- standing (GoalkeeperPositioning)
        /// <summary>Distance from the goal line when the ball is far, and the furthest he advances when the danger is at its highest (metres; the goal zone is 4 m deep).</summary>
        public const float LineDepthMin = 1.0f;
        public const float LineDepthMax = 3.4f;
        /// <summary>However far off his place he is, he never stands further than this from the goal line (the depth of the goal zone, m).</summary>
        public const float StandMaxDepth = 4.0f;
        /// <summary>The danger grows from zero at this distance of the ball from the goal to its maximum at <see cref="DangerNearDistance"/>.</summary>
        public const float DangerFarDistance = 16.0f;
        public const float DangerNearDistance = 5.0f;
        /// <summary>How much of the danger a free ball (nobody has it) carries, against a ball in the feet of an opponent.</summary>
        public const float LooseBallDanger = 0.6f;
        /// <summary>A ball that team-mates are carrying is not a danger: share of the danger kept.</summary>
        public const float OwnBallDanger = 0.0f;
        /// <summary>Players in the corridor between the ball and the goal (either team): each one cuts this much of the danger (up to all of it), because the shot is harder.</summary>
        public const float BlockedDangerCut = 0.35f;
        /// <summary>Half width (m) of the corridor between the ball and the goal centre in which a player blocks the view and the shot.</summary>
        public const float CorridorHalfWidth = 1.4f;
        /// <summary>He moves this far (m) towards the side where the corridor is open, per blocker (the blockers are covering the other).</summary>
        public const float BlockedLateralShift = 0.5f;
        /// <summary>Extra metres towards the ball's side, at the widest angle: covering the near post.</summary>
        public const float NearPostShift = 0.45f;
        /// <summary>Stays this far inside the posts at most (negative = may stand outside them).</summary>
        public const float PostMargin = 0.15f;
        public const float PositionMagnitude = 0.7f;
        /// <summary>On top of the common error, a keeper of positioning quality 0 stands up to this far (m) more from where he should, drifting slowly: the period of the drift in seconds.</summary>
        public const float PositionErrorMax = 1.6f;
        /// <summary>Nobody stands exactly where he should: the part of the position error (m) that every keeper has, whatever his quality.</summary>
        public const float PositionErrorBase = 1.5f;
        public const float PositionErrorPeriod = 2.0f;

        // ---- reading a shot (GoalkeeperReaction)
        /// <summary>A ball that nobody has, coming at his goal faster than this (m/s) and in the direction of it (cosine of the angle to the goal's normal at least this), is read as a shot.</summary>
        public const float ShotMinSpeed = 7.0f;
        public const float ShotApproachCosine = 0.35f;
        /// <summary>Only shots that will cross the goal line within this many seconds count, and inside the goal mouth plus this margin (m), and under the bar plus this margin (m).</summary>
        public const float ShotHorizonSeconds = 1.8f;
        public const float CrossingMargin = 0.8f;
        public const float HeightMargin = 0.6f;
        /// <summary>The view of a shot is blocked by the players standing between the shooter and the keeper: half width of that corridor (m), and what each one takes from the visibility (0..1).</summary>
        public const float VisibilityCorridor = 1.0f;
        public const float VisibilityPerBlocker = 0.25f;
        /// <summary>Extra reaction time at zero visibility (a fraction), and for a keeper whose save timing is zero (a fraction).</summary>
        public const float VisibilityDelayPenalty = 0.6f;
        public const float SaveTimingPenalty = 0.5f;
        /// <summary>A keeper of anticipation 1 starts this much sooner (a fraction of his reaction time).</summary>
        public const float AnticipationRelief = 0.25f;
        /// <summary>Largest error (m) in where he thinks the ball will cross, for a keeper who reads shots at 0, at the start of the flight; it shrinks to nothing as the ball arrives (reference: seconds of flight).</summary>
        public const float ReadErrorMax = 1.5f;
        public const float ReadErrorReferenceSeconds = 0.6f;
        public const float DiveMagnitude = 1.0f;
        /// <summary>He goes to a point this far in front of the goal line, where the ball is read to cross (not to the line itself).</summary>
        public const float ShotStandOff = 0.4f;
        /// <summary>A read error of at least this many metres counts as a mistake of reading.</summary>
        public const float MisreadThreshold = 0.5f;
        /// <summary>After a rebound or a parry, extra waiting before he moves for the loose ball, for a keeper whose rebound response is zero (a fraction of his reaction time).</summary>
        public const float ReboundPenalty = 0.8f;

        // ---- coming out (the decision to leave or stay)
        /// <summary>He considers going for a ball nobody has when it is within this distance of the goal line (m), and goes no further from it than <see cref="ExitMaxDepth"/>.</summary>
        public const float ExitMaxDistance = 11.0f;
        public const float ExitMaxDepth = 9.0f;
        /// <summary>Share of his top speed he counts on when judging who gets to the ball first, and the margin in seconds by which he must be earlier.</summary>
        public const float ExitSpeedShare = 0.85f;
        public const float ExitMarginSeconds = 0.35f;
        /// <summary>A keeper of decision making 0 misjudges the opponents' arrival time by up to this fraction, and re-judges every this many seconds.</summary>
        public const float ExitJudgementError = 0.5f;
        public const float ExitJudgementPeriod = 0.6f;
        public const float ExitMagnitude = 1.0f;

        // ---- how the attributes weigh (the keeper's own capabilities, 0..1)
        /// <summary>How much the keeper's command (his experience of the game) sharpens his judgement of who gets to a loose ball first (0 = not at all).</summary>
        public const float CommandExitInfluence = 0.5f;

        // ---- Phase 29, stage 1: what he does with the ball in his hands (wait / short / long / clear)
        /// <summary>Whether he chooses what to do with the ball he holds (off: he plays it at once to the best-placed team-mate, as before Phase 29).</summary>
        public const bool DistributionEnabled = true;
        /// <summary>A rival this close (m) to the keeper holding the ball hurries him: he lowers his demand and does not wait.</summary>
        public const float DistributionRushRadius = 5.0f;
        /// <summary>The chance of arriving at which he plays at once, when hurried, and when he has waited long enough (a fallback).</summary>
        public const float DistributionReleaseChance = 0.75f;
        public const float DistributionRushedChance = 0.45f;
        public const float DistributionFallbackChance = 0.55f;
        /// <summary>The longest he waits for a lane to open (s), for a keeper of decision making 1; one of 0 waits this share of it.</summary>
        public const float DistributionPatienceSeconds = 2.5f;
        public const float DistributionPatienceFloor = 0.4f;
        /// <summary>The worth of an option: a base, plus the ground it gains (share of the pitch length, not below minus the floor) and the space around the receiver (m, capped).</summary>
        public const float DistributionBaseValue = 0.4f;
        public const float DistributionGainWeight = 1.0f;
        public const float DistributionBackFloor = 0.1f;
        public const float DistributionSpaceWeight = 0.3f;
        public const float DistributionSpaceCap = 8.0f;
        /// <summary>How much of the chance of a pass survives a keeper with no kicking quality (1 = none lost).</summary>
        public const float DistributionQualityFloor = 0.6f;
    }

    /// <summary>The editable copy of <see cref="GoalkeeperAiDefaults"/>. Arena and Classic differ in numbers only: <see cref="ArenaGoalkeeperDefaults"/>, <see cref="ClassicGoalkeeperDefaults"/>.</summary>
    [Serializable]
    public sealed partial class GoalkeeperAiTuning
    {
        public bool Enabled = GoalkeeperAiDefaults.Enabled;
        public float LineDepthMin = GoalkeeperAiDefaults.LineDepthMin;
        public float LineDepthMax = GoalkeeperAiDefaults.LineDepthMax;
        public float StandMaxDepth = GoalkeeperAiDefaults.StandMaxDepth;
        public float DangerFarDistance = GoalkeeperAiDefaults.DangerFarDistance;
        public float DangerNearDistance = GoalkeeperAiDefaults.DangerNearDistance;
        public float LooseBallDanger = GoalkeeperAiDefaults.LooseBallDanger;
        public float OwnBallDanger = GoalkeeperAiDefaults.OwnBallDanger;
        public float BlockedDangerCut = GoalkeeperAiDefaults.BlockedDangerCut;
        public float CorridorHalfWidth = GoalkeeperAiDefaults.CorridorHalfWidth;
        public float BlockedLateralShift = GoalkeeperAiDefaults.BlockedLateralShift;
        public float NearPostShift = GoalkeeperAiDefaults.NearPostShift;
        public float PostMargin = GoalkeeperAiDefaults.PostMargin;
        public float PositionMagnitude = GoalkeeperAiDefaults.PositionMagnitude;
        public float PositionErrorMax = GoalkeeperAiDefaults.PositionErrorMax;
        public float PositionErrorBase = GoalkeeperAiDefaults.PositionErrorBase;
        public float PositionErrorPeriod = GoalkeeperAiDefaults.PositionErrorPeriod;
        public float ShotMinSpeed = GoalkeeperAiDefaults.ShotMinSpeed;
        public float ShotApproachCosine = GoalkeeperAiDefaults.ShotApproachCosine;
        public float ShotHorizonSeconds = GoalkeeperAiDefaults.ShotHorizonSeconds;
        public float CrossingMargin = GoalkeeperAiDefaults.CrossingMargin;
        public float HeightMargin = GoalkeeperAiDefaults.HeightMargin;
        public float VisibilityCorridor = GoalkeeperAiDefaults.VisibilityCorridor;
        public float VisibilityPerBlocker = GoalkeeperAiDefaults.VisibilityPerBlocker;
        public float VisibilityDelayPenalty = GoalkeeperAiDefaults.VisibilityDelayPenalty;
        public float SaveTimingPenalty = GoalkeeperAiDefaults.SaveTimingPenalty;
        public float AnticipationRelief = GoalkeeperAiDefaults.AnticipationRelief;
        public float ReadErrorMax = GoalkeeperAiDefaults.ReadErrorMax;
        public float ReadErrorReferenceSeconds = GoalkeeperAiDefaults.ReadErrorReferenceSeconds;
        public float DiveMagnitude = GoalkeeperAiDefaults.DiveMagnitude;
        public float ShotStandOff = GoalkeeperAiDefaults.ShotStandOff;
        public float MisreadThreshold = GoalkeeperAiDefaults.MisreadThreshold;
        public float ReboundPenalty = GoalkeeperAiDefaults.ReboundPenalty;
        public float ExitMaxDistance = GoalkeeperAiDefaults.ExitMaxDistance;
        public float ExitMaxDepth = GoalkeeperAiDefaults.ExitMaxDepth;
        public float ExitSpeedShare = GoalkeeperAiDefaults.ExitSpeedShare;
        public float ExitMarginSeconds = GoalkeeperAiDefaults.ExitMarginSeconds;
        public float ExitJudgementError = GoalkeeperAiDefaults.ExitJudgementError;
        public float ExitJudgementPeriod = GoalkeeperAiDefaults.ExitJudgementPeriod;
        public float ExitMagnitude = GoalkeeperAiDefaults.ExitMagnitude;
        public float CommandExitInfluence = GoalkeeperAiDefaults.CommandExitInfluence;
        public bool DistributionEnabled = GoalkeeperAiDefaults.DistributionEnabled;
        public float DistributionRushRadius = GoalkeeperAiDefaults.DistributionRushRadius;
        public float DistributionReleaseChance = GoalkeeperAiDefaults.DistributionReleaseChance;
        public float DistributionRushedChance = GoalkeeperAiDefaults.DistributionRushedChance;
        public float DistributionFallbackChance = GoalkeeperAiDefaults.DistributionFallbackChance;
        public float DistributionPatienceSeconds = GoalkeeperAiDefaults.DistributionPatienceSeconds;
        public float DistributionPatienceFloor = GoalkeeperAiDefaults.DistributionPatienceFloor;
        public float DistributionBaseValue = GoalkeeperAiDefaults.DistributionBaseValue;
        public float DistributionGainWeight = GoalkeeperAiDefaults.DistributionGainWeight;
        public float DistributionBackFloor = GoalkeeperAiDefaults.DistributionBackFloor;
        public float DistributionSpaceWeight = GoalkeeperAiDefaults.DistributionSpaceWeight;
        public float DistributionSpaceCap = GoalkeeperAiDefaults.DistributionSpaceCap;
        public float DistributionQualityFloor = GoalkeeperAiDefaults.DistributionQualityFloor;
        /// <summary>Multipliers that only the modes use (1 = the common number): how late he starts, how wrong he reads, how far off his place he stands.</summary>
        public float ReactionScale = 1f;
        public float ReadErrorScale = 1f;
        public float PositionErrorScale = 1f;

        public GoalkeeperTuning Skill = new GoalkeeperTuning();

        /// <summary>The tuning of a mode: the common numbers with that mode's own (never a second engine).</summary>
        public static GoalkeeperAiTuning ForMode(GameMode mode)
        {
            var t = new GoalkeeperAiTuning();
            if (mode == GameMode.Arena) ArenaGoalkeeperDefaults.Apply(t); else ClassicGoalkeeperDefaults.Apply(t);
            return t;
        }
    }

    /// <summary>Arena: faster, more open; its keepers are a little more permissive (they start later, read worse and stand looser).</summary>
    public static class ArenaGoalkeeperDefaults
    {
        public const float ReactionScale = 1.1f;
        public const float ReadErrorScale = 1.25f;
        public const float PositionErrorScale = 0.85f;

        public static void Apply(GoalkeeperAiTuning t)
        {
            t.ReactionScale = ReactionScale; t.ReadErrorScale = ReadErrorScale; t.PositionErrorScale = PositionErrorScale;
        }
    }

    /// <summary>Classic: more football; its keepers are better placed, start sooner and read better, so they matter more.</summary>
    public static class ClassicGoalkeeperDefaults
    {
        public const float ReactionScale = 0.95f;
        public const float ReadErrorScale = 0.8f;
        public const float PositionErrorScale = 0.6f;

        public static void Apply(GoalkeeperAiTuning t)
        {
            t.ReactionScale = ReactionScale; t.ReadErrorScale = ReadErrorScale; t.PositionErrorScale = PositionErrorScale;
        }
    }
}
