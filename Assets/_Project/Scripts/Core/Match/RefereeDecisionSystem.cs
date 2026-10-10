using System;

namespace FS27.Core
{
    /// <summary>
    /// Every number of the referee's judgement of a contact (Phase 21), in one place. <see cref="RefereeTuning"/> is the editable copy. The weights add up to a SEVERITY (about 0..1.4); the thresholds say what the severity is worth.
    /// </summary>
    public static class RefereeDefaults
    {
        // ---- the evidence
        /// <summary>The two bodies meet when the players are this close (metres). Further apart there is no contact, however the duel ended.</summary>
        public const float ContactDistance = 1.3f;
        // ---- what makes a contact severe
        /// <summary>He did not play the ball first (he arrived late, or never touched it).</summary>
        public const float LateWeight = 0.45f;
        public const float BehindWeight = 0.20f;
        /// <summary>How fast he closed in on the carrier, as a share of his top speed (0..1.5).</summary>
        public const float ClosingWeight = 0.30f;
        public const float SlideWeight = 0.10f;
        /// <summary>The duel went so badly for him that the engine already counted it as a clumsy challenge (the margin under the foul margin of the action).</summary>
        public const float ClumsyWeight = 0.25f;
        // ---- what the severity is worth
        public const float FoulSeverity = 0.55f;
        public const float YellowSeverity = 0.95f;
        public const float RedSeverity = 1.30f;
        /// <summary>A slide at (or above) this closing speed, from behind or without the ball, is dangerous play: a foul whatever the severity says, and a yellow.</summary>
        public const float DangerousClosing = 0.60f;
        /// <summary>A dangerous slide from behind at (or above) this closing speed is a red.</summary>
        public const float ExtremeClosing = 0.85f;
        /// <summary>Denying a clear goal-scoring chance is a red from this severity: a challenge of no weight is not (the referee does not send a man off for a brush).</summary>
        public const float DogsoSeverity = 0.80f;
        /// <summary>This many fouls already committed by the same player: the next one is a yellow (persistent infringement).</summary>
        public const int PersistentFouls = 4;
        // ---- the situations
        /// <summary>A carrier this close to the goal he attacks, facing it, with no outfield defender between: a clear goal-scoring chance (metres).</summary>
        public const float GoalChanceDistance = 12.0f;
        public const float GoalChanceFacingCosine = 0.5f;
        /// <summary>A foul on a carrier whose team won the ball this recently and who is running at this share of his top speed is a tactical foul (stopping a counter-attack).</summary>
        public const float CounterPossessionSeconds = 4.0f;
        public const float CounterCarrierSpeed = 0.7f;
        /// <summary>... and he is running straight at the goal he attacks (cosine between his heading and the goal).</summary>
        public const float CounterFacingCosine = 0.7f;
        /// <summary>A carrier slower than this (m/s) is read by the way he faces, not by his velocity.</summary>
        public const float HeadingMinSpeed = 0.5f;
        // ---- the referee's style (0 = permissive .. 1 = strict): the thresholds are scaled by a number between these
        public const float StyleScaleLoose = 1.25f;
        public const float StyleScaleStrict = 0.75f;
        /// <summary>The neutral style (a referee with no profile) and the scale of the 0..100 referee profile values.</summary>
        public const float NeutralStyle = 0.5f;
        public const float ProfileScale = 100.0f;
    }

    /// <summary>The editable copy of <see cref="RefereeDefaults"/> (a copy per match: a test or an experiment can change it).</summary>
    [Serializable]
    public sealed class RefereeTuning
    {
        public float ContactDistance = RefereeDefaults.ContactDistance;
        public float LateWeight = RefereeDefaults.LateWeight;
        public float BehindWeight = RefereeDefaults.BehindWeight;
        public float ClosingWeight = RefereeDefaults.ClosingWeight;
        public float SlideWeight = RefereeDefaults.SlideWeight;
        public float ClumsyWeight = RefereeDefaults.ClumsyWeight;
        public float FoulSeverity = RefereeDefaults.FoulSeverity;
        public float YellowSeverity = RefereeDefaults.YellowSeverity;
        public float RedSeverity = RefereeDefaults.RedSeverity;
        public float DangerousClosing = RefereeDefaults.DangerousClosing;
        public float ExtremeClosing = RefereeDefaults.ExtremeClosing;
        public float DogsoSeverity = RefereeDefaults.DogsoSeverity;
        public int PersistentFouls = RefereeDefaults.PersistentFouls;
        public float GoalChanceDistance = RefereeDefaults.GoalChanceDistance;
        public float GoalChanceFacingCosine = RefereeDefaults.GoalChanceFacingCosine;
        public float CounterPossessionSeconds = RefereeDefaults.CounterPossessionSeconds;
        public float CounterCarrierSpeed = RefereeDefaults.CounterCarrierSpeed;
        public float CounterFacingCosine = RefereeDefaults.CounterFacingCosine;
        public float HeadingMinSpeed = RefereeDefaults.HeadingMinSpeed;
        public float StyleScaleLoose = RefereeDefaults.StyleScaleLoose;
        public float StyleScaleStrict = RefereeDefaults.StyleScaleStrict;
    }

    /// <summary>What the referee saw of a contact between a player who challenged and the carrier. Plain data built by the Core from the match; nothing random in it.</summary>
    public struct ContactEvidence
    {
        /// <summary>The challenger played the ball first (he won it, or knocked it away) before anything else.</summary>
        public bool TouchedBallFirst;
        /// <summary>The two bodies met (see <see cref="RefereeDefaults.ContactDistance"/>).</summary>
        public bool Contact;
        public bool IsSlide;
        public bool FromBehind;
        /// <summary>How fast he closed in on the carrier relative to his top speed (0..1.5).</summary>
        public float ClosingSpeed01;
        /// <summary>The engine already judged the challenge clumsy (the margin under the foul margin of the action).</summary>
        public bool Clumsy;
        /// <summary>Stopping a counter-attack: the carrier's team had just won the ball and he was running with it.</summary>
        public bool StoppedCounterAttack;
        /// <summary>A clear goal-scoring chance of the carrier: close to goal, facing it, no outfield defender between.</summary>
        public bool DeniedGoalChance;
        /// <summary>Fouls the challenger had already committed in this match.</summary>
        public int PriorFouls;
    }

    [Flags]
    public enum FoulReason
    {
        None = 0,
        /// <summary>He did not play the ball first.</summary>
        Late = 1,
        FromBehind = 2,
        /// <summary>Too fast or too hard for the situation.</summary>
        Reckless = 4,
        /// <summary>A slide at speed from behind or without the ball.</summary>
        Dangerous = 8,
        /// <summary>He stopped a counter-attack.</summary>
        Tactical = 16,
        Persistent = 32,
        /// <summary>He denied a clear goal-scoring chance.</summary>
        DeniedGoalChance = 64
    }

    public enum CardDecision
    {
        None = 0,
        Yellow,
        Red
    }

    /// <summary>The referee's call on a contact: foul or play on, the card, and why.</summary>
    public struct RefereeDecision
    {
        public bool IsFoul;
        public CardDecision Card;
        public FoulReason Reasons;
        /// <summary>The severity the call came from (for the record and for the tests).</summary>
        public float Severity;

        public override string ToString() { return (IsFoul ? "Foul" : "PlayOn") + (Card == CardDecision.None ? "" : " " + Card) + (Reasons == FoulReason.None ? "" : " (" + Reasons + ")"); }
    }

    /// <summary>
    /// The referee's judgement of a contact (Phase 21). A pure function of the evidence, the style and the tuning: the same action is the same decision, whatever the random numbers of the match did (the random numbers decide HOW the
    /// duel went, never what the referee calls). A clean challenge is not a foul: he touched the ball first and was not reckless. A late one is; a dangerous slide is. A yellow needs something on top of a foul, and a red is
    /// kept for a clear chance denied or a challenge of extreme danger.
    /// </summary>
    public static class RefereeDecisionSystem
    {
        public static RefereeDecision Decide(in ContactEvidence e, in RefereeStyle style, RefereeTuning t)
        {
            float closing = Math.Max(0f, e.ClosingSpeed01);
            float severity = (e.TouchedBallFirst ? 0f : t.LateWeight) + (e.FromBehind ? t.BehindWeight : 0f) + t.ClosingWeight * closing + (e.IsSlide ? t.SlideWeight : 0f) + (e.Clumsy ? t.ClumsyWeight : 0f);
            bool dangerous = e.IsSlide && closing >= t.DangerousClosing && (e.FromBehind || !e.TouchedBallFirst);

            var d = new RefereeDecision { Severity = severity };
            // no contact, no foul; a challenge that played the ball first is clean unless it was dangerous
            if (!e.Contact) return d;
            float foulAt = t.FoulSeverity * MathUtil.Lerp(t.StyleScaleLoose, t.StyleScaleStrict, style.Strictness);
            if (!dangerous && (e.TouchedBallFirst || severity < foulAt)) return d;

            d.IsFoul = true;
            if (!e.TouchedBallFirst) d.Reasons |= FoulReason.Late;
            if (e.FromBehind) d.Reasons |= FoulReason.FromBehind;
            if (closing >= t.DangerousClosing || e.Clumsy) d.Reasons |= FoulReason.Reckless;
            if (dangerous) d.Reasons |= FoulReason.Dangerous;
            if (e.StoppedCounterAttack) d.Reasons |= FoulReason.Tactical;
            if (e.PriorFouls >= t.PersistentFouls) d.Reasons |= FoulReason.Persistent;
            if (e.DeniedGoalChance) d.Reasons |= FoulReason.DeniedGoalChance;

            float yellowAt = t.YellowSeverity * MathUtil.Lerp(t.StyleScaleLoose, t.StyleScaleStrict, style.Cards);
            bool extreme = dangerous && e.FromBehind && closing >= t.ExtremeClosing;
            if ((e.DeniedGoalChance && severity >= t.DogsoSeverity) || severity >= t.RedSeverity || extreme) d.Card = CardDecision.Red;
            else if (severity >= yellowAt || dangerous || e.StoppedCounterAttack || e.PriorFouls >= t.PersistentFouls) d.Card = CardDecision.Yellow;
            return d;
        }
    }
}
