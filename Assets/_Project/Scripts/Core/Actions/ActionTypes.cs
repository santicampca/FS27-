using System;

namespace FS27.Core
{
    /// <summary>The action buttons of the game (A = individual / defensive, B = collective / physical, C = skill: Phase 6).</summary>
    public enum ActionButton
    {
        None = 0,
        A = 1,
        B = 2,
        C = 3
    }

    /// <summary>How the button was used: a tap, or a swipe up/down.</summary>
    public enum ActionGesture
    {
        Tap = 0,
        SwipeUp = 1,
        SwipeDown = 2
    }

    /// <summary>
    /// The neutral action input: a button, a gesture, the direction the player pushed and how hard (0..1). It says nothing about WHAT it does: the Core decides that from the
    /// context (does the player have the ball, is he the goalkeeper) through <see cref="ActionMapper"/>. Unity only translates touches into this.
    /// </summary>
    public struct ActionInput
    {
        public ActionButton Button;
        public ActionGesture Gesture;
        /// <summary>Wanted direction in field space (need not be unit length). Zero = "where I am facing".</summary>
        public Vec2 Direction;
        /// <summary>0..1: how hard (power of the kick). Values outside are clamped.</summary>
        public float Magnitude;
        /// <summary>-1..1: the curve of a curved shot (sign = side). 0 = the default curve of the skill.</summary>
        public float Curve;

        public ActionInput(ActionButton button, ActionGesture gesture, Vec2 direction, float magnitude)
        {
            Button = button;
            Gesture = gesture;
            Direction = direction;
            Magnitude = magnitude;
            Curve = 0f;
        }
    }

    /// <summary>What the player holds / is, as far as the mapping of the buttons is concerned.</summary>
    public struct ActionContext
    {
        /// <summary>The player controls the ball (he is the possessor).</summary>
        public bool HasBall;
        /// <summary>The player is standing in goal (the real keeper, or a field player who took the gloves).</summary>
        public bool IsGoalkeeper;
    }

    /// <summary>
    /// The control scheme, in one place (DECISION MADE DURING PHASE 4: the architecture fixed the gestures, the context rule is mine).
    ///  WITH the ball:    B tap = short pass, B swipe down = wall pass, B swipe up = through ball, A swipe down = shot, A swipe up = chip, A tap = shield.
    ///  WITHOUT the ball: A tap = pressure/contain, A swipe down = standing tackle, A swipe up = sliding tackle, B tap = physical challenge, B swipe down = intercept/block, B swipe up = clearance.
    ///  The goalkeeper in goal WITH the ball: B tap = short distribution (throw), B swipe up = long distribution (kick); the rest as any player.
    ///  The goalkeeper in goal WITHOUT the ball: A swipe up = rush out; the rest as any player.
    ///  The SKILL button C (Phase 6, DECISION MADE DURING PHASE 6) WITH the ball: C tap = skill move, C swipe up = special lob pass, C swipe down = curved shot. It does nothing without the ball and for the goalkeeper
    ///  in goal; whether the player may use the action is decided by his skills (<see cref="SkillResolver.CanUseAction"/>), never by the mapping.
    /// </summary>
    public static class ActionMapper
    {
        public static FootballActionKind Map(in ActionInput input, in ActionContext context)
        {
            if (input.Button == ActionButton.None) return FootballActionKind.None;
            ActionGesture g = input.Gesture;
            if (input.Button == ActionButton.C)
            {
                // the skill button only has meaning with the ball (no skill action exists without it)
                if (!context.HasBall || context.IsGoalkeeper) return FootballActionKind.None;
                if (g == ActionGesture.SwipeDown) return FootballActionKind.CurvedShot;
                if (g == ActionGesture.SwipeUp) return FootballActionKind.SkillLobPass;
                return FootballActionKind.SkillMove;
            }
            bool a = input.Button == ActionButton.A;

            if (context.HasBall)
            {
                if (context.IsGoalkeeper && !a && g != ActionGesture.SwipeDown) return FootballActionKind.GoalkeeperDistribution;
                if (a)
                {
                    if (g == ActionGesture.SwipeDown) return FootballActionKind.Shot;
                    if (g == ActionGesture.SwipeUp) return FootballActionKind.Chip;
                    return FootballActionKind.Shield;
                }
                if (g == ActionGesture.SwipeDown) return FootballActionKind.WallPass;
                if (g == ActionGesture.SwipeUp) return FootballActionKind.ThroughPass;
                return FootballActionKind.ShortPass;
            }

            if (a)
            {
                if (g == ActionGesture.SwipeDown) return FootballActionKind.Tackle;
                if (g == ActionGesture.SwipeUp) return context.IsGoalkeeper ? FootballActionKind.GoalkeeperRush : FootballActionKind.SlidingTackle;
                return FootballActionKind.Pressure;
            }
            if (g == ActionGesture.SwipeDown) return FootballActionKind.Intercept;
            if (g == ActionGesture.SwipeUp) return FootballActionKind.Clearance;
            return FootballActionKind.PhysicalChallenge;
        }
    }

    /// <summary>A request to perform an action. Plain data; <see cref="MatchController.Perform"/> answers with an <see cref="ActionResult"/>.</summary>
    public struct ActionRequest
    {
        public TeamSide Team;
        public string PlayerId;
        public FootballActionKind Kind;
        /// <summary>Wanted direction in field space. Zero = the player's facing.</summary>
        public Vec2 Direction;
        /// <summary>0..1.</summary>
        public float Power;
        /// <summary>-1..1: curve of a shot (sign = side, 0 = straight). Only shots use it.</summary>
        public float Curve;
        /// <summary>
        /// LEGACY (Phase 4) grant of a skill action (<see cref="FootballActionKind.SkillMove"/>, <see cref="FootballActionKind.SkillLobPass"/>, <see cref="FootballActionKind.CurvedShot"/>). Honoured ONLY by a match that knows
        /// no skills (built without skill data); a match that knows its players' skills ignores it: the skills are then the only permission (<see cref="SkillResolver.CanUseAction"/>).
        /// </summary>
        public bool SkillGranted;
        /// <summary>Optional: the catalogue skill (a style of skill move or lob pass) to use. Null or empty = the best one the player owns.</summary>
        public string SkillId;
        /// <summary>Goalkeeper distribution only: a long kick instead of a throw.</summary>
        public bool Long;

        public ActionRequest(TeamSide team, string playerId, FootballActionKind kind, Vec2 direction = default, float power = ActionDefaults.DefaultPower)
        {
            Team = team;
            PlayerId = playerId;
            Kind = kind;
            Direction = direction;
            Power = power;
            Curve = 0f;
            SkillGranted = false;
            SkillId = null;
            Long = false;
        }
    }

    /// <summary>What happened to a request. No exceptions for normal gameplay: a refusal is a result.</summary>
    public enum ActionOutcome
    {
        /// <summary>It was refused and nothing changed (see <see cref="ActionResult.Rejection"/>).</summary>
        Rejected = 0,
        /// <summary>It began and goes on over time (pressure, a slide with nothing in reach, a rush).</summary>
        Accepted,
        /// <summary>It was attempted and did not work (a lost tackle, an intercept that missed).</summary>
        Failed,
        /// <summary>It was done: the ball was kicked, won or touched.</summary>
        Completed
    }

    /// <summary>Why a request was refused.</summary>
    public enum ActionRejection
    {
        None = 0,
        MatchNotLive,
        UnknownPlayer,
        PlayerNotActive,
        /// <summary>The player is still busy (sliding, recovering from a tackle, between two kicks).</summary>
        ActionLocked,
        /// <summary>The ball is not close enough, or too high, for this action.</summary>
        BallOutOfReach,
        /// <summary>The ball is held by a player the action cannot be done to (an opponent for a kick, a team-mate...).</summary>
        BallNotAvailable,
        /// <summary>The action needs the player to control the ball and he does not.</summary>
        NotInPossession,
        /// <summary>The action needs an opponent who controls the ball (tackle, challenge) and there is none in reach.</summary>
        NoTarget,
        /// <summary>The action is only for the goalkeeper.</summary>
        NotGoalkeeper,
        /// <summary>A skill action whose skill is not unlocked.</summary>
        SkillNotGranted,
        /// <summary>The kind is part of the contract but Phase 4 does not resolve it yet.</summary>
        NotSupported,
        InvalidInput
    }

    /// <summary>The qualifier of a result: what exactly happened. Machine-readable; animation and sound read it.</summary>
    public enum ActionDetail
    {
        None = 0,
        // control and first touch
        CleanControl,
        HeavyTouch,
        Deflected,
        // kicks
        Kicked,
        Lofted,
        Curved,
        Wall,
        Through,
        // defence
        Won,
        Loose,
        Blocked,
        Intercepted,
        Foul,
        Pressing,
        Sliding,
        Shielding,
        // goalkeeper
        Caught,
        Parried,
        Rushing,
        Dribbling,
        // skill moves (Phase 6, appended)
        /// <summary>The skill move beat the man cleanly.</summary>
        Evaded,
        /// <summary>The skill move only partly worked: less ground gained, less protection.</summary>
        PartlyEvaded,
        /// <summary>The skill move failed: the ball ran away from the player.</summary>
        Fumbled
    }

    /// <summary>Which foot (or the body) played the ball.</summary>
    public enum ActionFoot
    {
        None = 0,
        Left,
        Right,
        Body
    }

    /// <summary>
    /// The full answer to an action: enough for the Core (ball, possession, stamina) AND for a future animation system (who, which action, which foot, how hard, which direction, what
    /// came of it). Plain data, never a reference to Unity.
    /// </summary>
    public struct ActionResult
    {
        public ActionOutcome Outcome;
        public ActionRejection Rejection;
        public FootballActionKind Kind;
        public TeamSide Team;
        public string PlayerId;
        public ActionFoot Foot;
        /// <summary>The kick was played with his weak foot (explicit, so nothing has to infer it from <see cref="Foot"/> and the player's preferred foot).</summary>
        public bool WeakFoot;
        /// <summary>0..1: how hard the action was (power of a kick, speed of the contact).</summary>
        public float Intensity;
        /// <summary>The direction the ball was sent or the action was aimed (unit length, field space). Zero when it has none.</summary>
        public Vec2 Direction;
        /// <summary>0..1: how well it was done (the quality that decided the error / the margin of a duel).</summary>
        public float Quality;
        public ActionDetail Detail;
        /// <summary>Velocity given to the ball (zero when the action does not move it).</summary>
        public Vec3 BallVelocity;
        /// <summary>Stamina points taken from the player (>= 0).</summary>
        public float StaminaCost;
        /// <summary>The player (or his team) ended up with the ball.</summary>
        public bool GainedPossession;
        /// <summary>The player whose ball was challenged, when there is one.</summary>
        public string OtherPlayerId;
        /// <summary>The catalogue skill that shaped the action (a skill action, or a passive skill that acted), or null.</summary>
        public string SkillId;

        public bool Succeeded { get { return Outcome == ActionOutcome.Completed || Outcome == ActionOutcome.Accepted; } }

        public override string ToString() { return Kind + " " + Outcome + (Rejection != ActionRejection.None ? " (" + Rejection + ")" : "") + (Detail != ActionDetail.None ? " " + Detail : ""); }
    }
}
