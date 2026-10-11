using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-23 penalties, in one place (the same rule as <see cref="FreeKickDefaults"/>): starting values copied into an editable <see cref="PenaltyTuning"/>. Metres, seconds, fractions.
    /// The pitch is 40 x 25 with a 5 m goal, so the area and the spot are those of a small pitch. DECISION MADE DURING PHASE 23: all of them (the specification fixes the contract, not the values).
    /// </summary>
    public static class PenaltyDefaults
    {
        // ---- the area and the spot (Classic only: Arena has no penalties for fouls)
        /// <summary>A foul by the defending team that is committed less than this far from its goal line, and inside <see cref="AreaHalfWidth"/> of the middle, is a penalty.</summary>
        public const float AreaDepth = 7.0f;
        public const float AreaHalfWidth = 8.0f;
        /// <summary>The penalty spot is this far from the goal line, on the middle of the goal.</summary>
        public const float SpotDistance = 6.0f;
        /// <summary>The taker stands this far behind the ball, looking at the goal.</summary>
        public const float TakerOffsetBehind = 1.0f;
        /// <summary>The goalkeeper stands this far in front of the goal line.</summary>
        public const float KeeperLineOffset = 0.3f;
        /// <summary>Everybody else stays this far outside the area until the ball is kicked.</summary>
        public const float OthersMargin = 0.5f;

        // ---- time
        public const float ReadySeconds = 2.0f;
        public const float HumanMaxWaitSeconds = 12.0f;
        public const float TakerChoiceSeconds = 5.0f;
        /// <summary>After the whistle the taker kicks within this time; then the Core kicks for him with the AI's aim, so nothing is ever blocked.</summary>
        public const float KickWindowSeconds = 4.0f;
        /// <summary>A kick that has not been decided after this long is over (the ball has stopped somewhere harmless).</summary>
        public const float KickMaxSeconds = 6.0f;
        /// <summary>The shoot-out waits this long between two kicks.</summary>
        public const float BetweenKicksSeconds = 0.5f;
        public const float KeeperVacancySeconds = 3.0f;

        // ---- the aim (Classic physical kick)
        /// <summary>A lateral aim beyond this (fraction of the half goal width) is a miss on purpose; the aim is clamped to it.</summary>
        public const float MaxLateral = 1.5f;
        /// <summary>The height 1.0 means "just under the bar": the ball is aimed at this fraction of the goal height; above 1.0 goes over.</summary>
        public const float UnderBarFraction = 0.9f;
        public const float MaxHeight = 1.5f;
        public const float FullChargeSeconds = 1.2f;
        public const float MinPower = 0.35f;
        /// <summary>The curve of a penalty is passed to the existing curved shot only when the taker owns the skill.</summary>
        public const float AiPower = 0.8f;

        // ---- the goal frame (posts and crossbar the ball rebounds off during a penalty)
        public const float PostRadius = 0.1f;
        public const float FrameRestitution = 0.55f;

        // ---- the goalkeeper's dive (Classic physical kick)
        public const float DiveDelayBase = 0.20f;
        public const float DiveDelayRange = 0.10f;
        public const float DiveSpeedBase = 5.0f;
        public const float DiveSpeedRange = 4.5f;
        /// <summary>The farthest lateral distance of a dive, in fraction of the half goal width.</summary>
        public const float DiveMaxLateral = 1.0f;
        /// <summary>A goalkeeper who dives on a penalty is reading the shot: the speed of the ball he has to control counts this fraction of its real speed (1 = no help). A penalty is a duel of guesses, not of reflexes against a 25 m/s ball.</summary>
        public const float KeeperSpeedFactor = 0.45f;
        /// <summary>The reach of the goalkeeper's hands and body on a penalty, in metres: base + range x his diving (a penalty is a 6 m shot, not an open-play touch: the usual touch reach would cover most of the goal).</summary>
        public const float ReachBase = 0.55f;
        public const float ReachRange = 0.55f;
        /// <summary>A fast shot shortens that reach by up to this fraction (less with good reflexes); the speed is compared with <see cref="ReachReferenceSpeed"/>.</summary>
        public const float ReachSpeedLoss = 0.55f;
        public const float ReachReferenceSpeed = 26.0f;

        // ---- the arcade penalty (Arena): resolved at once from the aims and the attributes
        public const float ArenaErrorBase = 0.10f;
        public const float ArenaErrorPower = 0.22f;
        public const float ArenaAccuracyEffect = 0.65f;
        public const float ArenaHeightErrorFactor = 0.8f;
        /// <summary>Hitting the post or the bar: the aim is this much beyond the edge (fraction) and still touches the frame.</summary>
        public const float FrameMargin = 0.08f;
        public const float ArenaDiveReachBase = 0.50f;
        public const float ArenaDiveReachRange = 0.40f;
        public const float ArenaPowerBeatsKeeper = 0.55f;
        public const float ArenaWrongHeightFactor = 0.60f;
        public const float HighThreshold = 0.5f;
        /// <summary>Seconds the ball takes to go in (what a client animates).</summary>
        public const float ArenaFlightSeconds = 0.45f;

        // ---- the AI
        public const float AiSideLateral = 0.60f;
        public const float AiSideAccuracyBonus = 0.25f;
        public const float AiCentreChance = 0.12f;
        public const float AiHighChance = 0.45f;
        public const float AiKeeperCentreChance = 0.16f;
        public const float AiKeeperHighChance = 0.40f;

        // ---- taker choice
        /// <summary>A taker who may curve the ball (he owns the skill) is worth this much more.</summary>
        public const float TakerCurveBonus = 0.10f;
        /// <summary>In a shoot-out the goalkeeper is the last on the automatic order: his score is reduced by this.</summary>
        public const float KeeperLastPenalty = 10.0f;
    }

    /// <summary>The editable copy of <see cref="PenaltyDefaults"/>.</summary>
    public sealed class PenaltyTuning
    {
        /// <summary>A foul inside the area is a penalty (Classic). Off = the Phase-22 behaviour (an ordinary free kick), for experiments and for the free-kick tests.</summary>
        public bool Enabled = true;
        public float AreaDepth = PenaltyDefaults.AreaDepth;
        public float AreaHalfWidth = PenaltyDefaults.AreaHalfWidth;
        public float SpotDistance = PenaltyDefaults.SpotDistance;
        public float TakerOffsetBehind = PenaltyDefaults.TakerOffsetBehind;
        public float KeeperLineOffset = PenaltyDefaults.KeeperLineOffset;
        public float OthersMargin = PenaltyDefaults.OthersMargin;
        public float ReadySeconds = PenaltyDefaults.ReadySeconds;
        public float HumanMaxWaitSeconds = PenaltyDefaults.HumanMaxWaitSeconds;
        public float TakerChoiceSeconds = PenaltyDefaults.TakerChoiceSeconds;
        public float KickWindowSeconds = PenaltyDefaults.KickWindowSeconds;
        public float KickMaxSeconds = PenaltyDefaults.KickMaxSeconds;
        public float BetweenKicksSeconds = PenaltyDefaults.BetweenKicksSeconds;
        public float KeeperVacancySeconds = PenaltyDefaults.KeeperVacancySeconds;
        public float MaxLateral = PenaltyDefaults.MaxLateral;
        public float UnderBarFraction = PenaltyDefaults.UnderBarFraction;
        public float MaxHeight = PenaltyDefaults.MaxHeight;
        public float FullChargeSeconds = PenaltyDefaults.FullChargeSeconds;
        public float MinPower = PenaltyDefaults.MinPower;
        public float AiPower = PenaltyDefaults.AiPower;
        public float PostRadius = PenaltyDefaults.PostRadius;
        public float FrameRestitution = PenaltyDefaults.FrameRestitution;
        public float DiveDelayBase = PenaltyDefaults.DiveDelayBase;
        public float DiveDelayRange = PenaltyDefaults.DiveDelayRange;
        public float DiveSpeedBase = PenaltyDefaults.DiveSpeedBase;
        public float DiveSpeedRange = PenaltyDefaults.DiveSpeedRange;
        public float DiveMaxLateral = PenaltyDefaults.DiveMaxLateral;
        public float KeeperSpeedFactor = PenaltyDefaults.KeeperSpeedFactor;
        public float ReachBase = PenaltyDefaults.ReachBase;
        public float ReachRange = PenaltyDefaults.ReachRange;
        public float ReachSpeedLoss = PenaltyDefaults.ReachSpeedLoss;
        public float ReachReferenceSpeed = PenaltyDefaults.ReachReferenceSpeed;
        public float ArenaErrorBase = PenaltyDefaults.ArenaErrorBase;
        public float ArenaErrorPower = PenaltyDefaults.ArenaErrorPower;
        public float ArenaAccuracyEffect = PenaltyDefaults.ArenaAccuracyEffect;
        public float ArenaHeightErrorFactor = PenaltyDefaults.ArenaHeightErrorFactor;
        public float FrameMargin = PenaltyDefaults.FrameMargin;
        public float ArenaDiveReachBase = PenaltyDefaults.ArenaDiveReachBase;
        public float ArenaDiveReachRange = PenaltyDefaults.ArenaDiveReachRange;
        public float ArenaPowerBeatsKeeper = PenaltyDefaults.ArenaPowerBeatsKeeper;
        public float ArenaWrongHeightFactor = PenaltyDefaults.ArenaWrongHeightFactor;
        public float HighThreshold = PenaltyDefaults.HighThreshold;
        public float ArenaFlightSeconds = PenaltyDefaults.ArenaFlightSeconds;
        public float AiSideLateral = PenaltyDefaults.AiSideLateral;
        public float AiSideAccuracyBonus = PenaltyDefaults.AiSideAccuracyBonus;
        public float AiCentreChance = PenaltyDefaults.AiCentreChance;
        public float AiHighChance = PenaltyDefaults.AiHighChance;
        public float AiKeeperCentreChance = PenaltyDefaults.AiKeeperCentreChance;
        public float AiKeeperHighChance = PenaltyDefaults.AiKeeperHighChance;
        public float TakerCurveBonus = PenaltyDefaults.TakerCurveBonus;
        public float KeeperLastPenalty = PenaltyDefaults.KeeperLastPenalty;
    }

    /// <summary>A penalty during the match (the fouled team kicks it, play goes on afterwards) or one kick of a shoot-out (a single kick, nothing after it).</summary>
    public enum PenaltyKind
    {
        InMatch = 0,
        Shootout
    }

    public enum PenaltyStage
    {
        None = 0,
        /// <summary>Awarded: play is stopped, taker and players are being set up.</summary>
        Preparing,
        /// <summary>Everybody is in place; the whistle waits (the Core's timer, the presentation or the person who kicks).</summary>
        Ready,
        /// <summary>The whistle has gone; the taker has <see cref="PenaltyTuning.KickWindowSeconds"/> to kick.</summary>
        Whistled,
        /// <summary>The ball has been kicked; the goalkeeper dives and the ball flies.</summary>
        Kicked,
        /// <summary>The kick has its result (<see cref="PenaltyState.Outcome"/>). For a penalty in the match play goes on from there (the rebound can be played, with the second-touch rule).</summary>
        Resolved
    }

    public enum PenaltyOutcome
    {
        None = 0,
        Scored,
        /// <summary>The goalkeeper caught or parried it (in a shoot-out the kick ends there).</summary>
        Saved,
        HitPost,
        HitBar,
        /// <summary>Wide, over, or stopped without reaching the goal.</summary>
        Missed,
        /// <summary>The taker played the ball a second time before anybody else touched it (a penalty in the match).</summary>
        IllegalSecondTouch
    }

    /// <summary>
    /// What a finger gives for a penalty (the neutral Core contract, like <see cref="FreeKickAim"/>): where in the goal, how high, how hard and with what curve. The lateral aim is seen from the kicker (left positive); the Core never reads a touch.
    /// </summary>
    public struct PenaltyShotAim
    {
        /// <summary>-1..1 = from the right post to the left post as the kicker sees them (0 = the middle); beyond 1 is wide.</summary>
        public float Lateral;
        /// <summary>0 = along the ground, 1 = just under the bar, above 1 = over it.</summary>
        public float Height;
        /// <summary>0..1; wins over <see cref="HoldSeconds"/> when positive.</summary>
        public float Power;
        /// <summary>How long the shot button was held (<see cref="PenaltyAiming.PowerFromHold"/>).</summary>
        public float HoldSeconds;
        /// <summary>-1..1; applied only when the taker owns the curve skill (the existing curved shot).</summary>
        public float Curve;

        public static PenaltyShotAim At(GoalZone zone, float height, float power, PenaltyTuning t = null)
        {
            float side = t != null ? t.AiSideLateral : PenaltyDefaults.AiSideLateral;
            return new PenaltyShotAim { Lateral = zone == GoalZone.Centre ? 0f : (zone == GoalZone.Left ? side : -side), Height = height, Power = power };
        }
    }

    /// <summary>The goalkeeper's choice (a person's or the AI's): where to dive, seen from the kicker (left positive), and whether to go high.</summary>
    public struct PenaltyKeeperAim
    {
        public bool HasChoice;
        /// <summary>-1..1 (fraction of the half goal width, left positive as the kicker sees it); 0 = stay in the middle.</summary>
        public float Lateral;
        public bool High;

        public static PenaltyKeeperAim At(GoalZone zone, bool high, PenaltyTuning t = null)
        {
            float side = t != null ? t.AiSideLateral : PenaltyDefaults.AiSideLateral;
            return new PenaltyKeeperAim { HasChoice = true, Lateral = zone == GoalZone.Centre ? 0f : (zone == GoalZone.Left ? side : -side), High = high };
        }
    }

    public enum PenaltyExecuteResult
    {
        Executed = 0,
        /// <summary>There is no penalty waiting to be kicked.</summary>
        NoPenalty,
        /// <summary>The set-up cannot finish (a team has nobody in goal, no taker).</summary>
        NotReady,
        /// <summary>The aim is not valid (not a number...): nothing is whistled.</summary>
        InvalidAim,
        ActionRefused
    }

    public enum PenaltyTakerResult
    {
        Accepted = 0,
        NoPenalty,
        NotUserTeam,
        UnknownPlayer,
        WrongTeam,
        NotAvailable,
        Dismissed,
        Goalkeeper,
        Locked
    }

    /// <summary>The order of the takers of a shoot-out (<see cref="MatchController.SetShootoutOrder"/>).</summary>
    public enum ShootoutOrderResult
    {
        Accepted = 0,
        /// <summary>That team has already started kicking.</summary>
        AlreadyStarted,
        Empty,
        UnknownPlayer,
        WrongTeam,
        /// <summary>He is not on the pitch (bench, substituted off, sent off).</summary>
        NotEligible,
        Duplicate
    }

    /// <summary>The penalty the match is handling (a penalty in the match or a kick of the shoot-out), kept after the kick until the next one.</summary>
    public sealed class PenaltyState
    {
        public PenaltyKind Kind;
        public PenaltyStage Stage;
        /// <summary>1, 2, 3... in the order the match handled them.</summary>
        public int Number;
        /// <summary>The team that kicks.</summary>
        public TeamSide Team;
        public string TakerId;
        public string KeeperId;
        public string SuggestedTakerId;
        public bool TakerChosenManually;
        public bool TakerConfirmed;
        public bool TakerReplaced;
        public float TakerChoiceRemaining;
        /// <summary>Where the ball is placed.</summary>
        public Vec2 Spot;
        /// <summary>The goal the kick goes to (its centre).</summary>
        public Vec2 GoalCentre;
        // ---- the infraction (penalty in the match)
        public string OffenderId;
        public string VictimId;
        public FoulReason Reasons;
        public int DisciplineIndex = -1;
        // ---- time
        public float WaitSeconds, WhistleSeconds, KickSeconds;
        // ---- what happened
        public PenaltyShotAim Aim;
        public PenaltyKeeperAim KeeperAim;
        public bool KeeperAimGiven;
        public PenaltyOutcome Outcome;
        public bool HitFrame, HitPostFirst;
        /// <summary>The goalkeeper (or another player) touched the ball after the kick: from then on the taker may play it again.</summary>
        public bool ContactByOther;
        public bool KeeperTouched;
        /// <summary>The last valid contact with the ball (a player's touch, not a post or the bar).</summary>
        public string LastContactPlayerId;
        public bool Goal;
        /// <summary>Arena: seconds the ball takes to go in (what a client animates). Classic: 0.</summary>
        public float FlightSeconds;
        internal float KeeperDelay, KeeperSpeed;
        internal Vec2 KeeperStart, KeeperTarget;
        internal bool Counted;

        public bool Active { get { return Stage != PenaltyStage.None && Stage != PenaltyStage.Resolved; } }
        public bool Waiting { get { return Stage == PenaltyStage.Preparing || Stage == PenaltyStage.Ready; } }
    }

    /// <summary>The pure maths of the penalty aim, like <see cref="FreeKickAiming"/>: no state, no physics.</summary>
    public static class PenaltyAiming
    {
        public static float PowerFromHold(float holdSeconds, PenaltyTuning t)
        {
            if (float.IsNaN(holdSeconds) || holdSeconds <= 0f) return t.MinPower;
            float k = t.FullChargeSeconds > 0f ? holdSeconds / t.FullChargeSeconds : 1f;
            return ActionModel.Clamp01(t.MinPower + (1f - t.MinPower) * k);
        }

        public static bool IsValid(PenaltyShotAim a)
        {
            return !(float.IsNaN(a.Lateral) || float.IsNaN(a.Height) || float.IsNaN(a.Power) || float.IsNaN(a.HoldSeconds) || float.IsNaN(a.Curve)
                     || float.IsInfinity(a.Lateral) || float.IsInfinity(a.Height) || float.IsInfinity(a.Power) || float.IsInfinity(a.HoldSeconds) || float.IsInfinity(a.Curve));
        }

        public static bool IsValid(PenaltyKeeperAim a) { return !(float.IsNaN(a.Lateral) || float.IsInfinity(a.Lateral)); }

        public static float Power(PenaltyShotAim a, PenaltyTuning t) { return a.Power > 0f ? ActionModel.Clamp01(a.Power) : PowerFromHold(a.HoldSeconds, t); }

        /// <summary>The aim brought inside what is allowed (a lateral aim beyond <see cref="PenaltyTuning.MaxLateral"/> is clamped, the height too).</summary>
        public static PenaltyShotAim Clamp(PenaltyShotAim a, PenaltyTuning t)
        {
            a.Lateral = Math.Max(-t.MaxLateral, Math.Min(t.MaxLateral, a.Lateral));
            a.Height = Math.Max(0f, Math.Min(t.MaxHeight, a.Height));
            a.Curve = Math.Max(-1f, Math.Min(1f, a.Curve));
            return a;
        }

        /// <summary>The point of the goal line the aim means, in field space (<paramref name="attackDirection"/> is the unit vector the kicker attacks along; left positive as the kicker sees it).</summary>
        public static Vec2 GoalPoint(float lateral, Vec2 goalCentre, Vec2 attackDirection, float goalWidth)
        {
            Vec2 left = new Vec2(-attackDirection.Y, attackDirection.X);
            return goalCentre + left * (lateral * goalWidth * 0.5f);
        }
    }

    /// <summary>
    /// The arcade penalty of Arena (resolved at once, no ball flight): the kicker's aim and the goalkeeper's dive meet the attributes. The accuracy error grows with power and shrinks with the kicker's quality;
    /// the dive covers a part of the goal that grows with the goalkeeper's diving and shrinks with the power of the shot (less if his reflexes are good). Deterministic given the two random numbers; the noise is bounded,
    /// so the result is decided by the aims and the attributes, not by luck.
    /// </summary>
    public static class ArenaPenaltyResolver
    {
        public struct Result
        {
            public PenaltyOutcome Outcome;
            /// <summary>Where the ball actually went (lateral, same scale as the aim) and how high.</summary>
            public float ActualLateral, ActualHeight;
            public bool KeeperGuessedSide;
        }

        /// <param name="kickerQuality">0..1 (accuracy: finishing, shooting, technique).</param>
        /// <param name="keeperDiving">0..1</param>
        /// <param name="keeperReflexes">0..1</param>
        /// <param name="noiseLateral">-1..1</param>
        /// <param name="noiseHeight">-1..1</param>
        public static Result Resolve(PenaltyShotAim shot, PenaltyKeeperAim keeper, float kickerQuality, float keeperDiving, float keeperReflexes, float noiseLateral, float noiseHeight, PenaltyTuning t)
        {
            shot = PenaltyAiming.Clamp(shot, t);
            float power = PenaltyAiming.Power(shot, t);
            float error = (t.ArenaErrorBase + t.ArenaErrorPower * power) * (1f - ActionModel.Clamp01(kickerQuality) * t.ArenaAccuracyEffect);
            float lateral = shot.Lateral + noiseLateral * error;
            float height = shot.Height + noiseHeight * error * t.ArenaHeightErrorFactor;
            var r = new Result { ActualLateral = lateral, ActualHeight = height };

            float edge = 1f, frame = t.FrameMargin;
            if (Math.Abs(lateral) > edge + frame) { r.Outcome = PenaltyOutcome.Missed; return r; }
            if (Math.Abs(lateral) > edge) { r.Outcome = PenaltyOutcome.HitPost; return r; }
            if (height > 1f + frame) { r.Outcome = PenaltyOutcome.Missed; return r; }
            if (height > 1f) { r.Outcome = PenaltyOutcome.HitBar; return r; }

            float reach = t.ArenaDiveReachBase + t.ArenaDiveReachRange * ActionModel.Clamp01(keeperDiving);
            float cover = reach * (1f - t.ArenaPowerBeatsKeeper * power * (1f - ActionModel.Clamp01(keeperReflexes)));
            float keeperLateral = keeper.HasChoice ? Math.Max(-t.DiveMaxLateral, Math.Min(t.DiveMaxLateral, keeper.Lateral)) : 0f;
            bool shotHigh = height > t.HighThreshold;
            if (keeper.HasChoice && keeper.High != shotHigh) cover *= t.ArenaWrongHeightFactor;
            r.KeeperGuessedSide = Math.Abs(lateral - keeperLateral) <= cover;
            r.Outcome = r.KeeperGuessedSide ? PenaltyOutcome.Saved : PenaltyOutcome.Scored;
            return r;
        }
    }

    /// <summary>The AI's penalty decisions (kicker and goalkeeper): deterministic from the random numbers it is given.</summary>
    public static class PenaltyAi
    {
        /// <summary>A corner or the middle, high or low, power by quality; the better kicker aims closer to the post.</summary>
        public static PenaltyShotAim Kick(float kickerQuality, float r1, float r2, float r3, PenaltyTuning t)
        {
            float q = ActionModel.Clamp01(kickerQuality);
            var a = new PenaltyShotAim { Power = t.AiPower };
            if (r1 < t.AiCentreChance) a.Lateral = 0f;
            else a.Lateral = (r2 < 0.5f ? 1f : -1f) * (t.AiSideLateral + t.AiSideAccuracyBonus * q);
            a.Height = r3 < t.AiHighChance ? 0.8f : 0.2f;
            return a;
        }

        public static PenaltyKeeperAim Keeper(float r1, float r2, float r3, PenaltyTuning t)
        {
            var k = new PenaltyKeeperAim { HasChoice = true, High = r3 < t.AiKeeperHighChance };
            k.Lateral = r1 < t.AiKeeperCentreChance ? 0f : (r2 < 0.5f ? 1f : -1f) * t.AiSideLateral;
            return k;
        }
    }
}
