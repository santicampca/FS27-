using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-4 actions-and-ball model, in one place (the same rule as <see cref="LocomotionDefaults"/>): starting values of tuning, copied into an editable
    /// <see cref="ActionTuning"/>. Nothing else repeats them. Units: metres, seconds, m/s, degrees only where named. Every one of them was chosen during Phase 4 (DECISION MADE DURING PHASE 4):
    /// the architecture fixed the controls and the attributes, not these values; they are meant to be re-tuned in a balance pass.
    /// </summary>
    public static class ActionDefaults
    {

        // ---- geometry (metres): who can touch the ball
        /// <summary>A player within this horizontal distance of the ball (and low enough) can touch it on his own: the automatic first touch.</summary>
        public const float TouchReach = 1.0f;
        /// <summary>Reach for kicking actions (pass, shot, chip, clearance): the ball must be this close to the player's feet.</summary>
        public const float KickReach = 1.2f;
        /// <summary>Reach of a standing tackle, measured to the ball.</summary>
        public const float TackleReach = 1.0f;
        /// <summary>Reach of a sliding tackle.</summary>
        public const float SlideReach = 1.9f;
        /// <summary>Reach of a physical challenge, measured to the ball carrier.</summary>
        public const float ChallengeReach = 1.6f;
        /// <summary>Reach of the intercept/block control, measured to the ball.</summary>
        public const float InterceptReach = 2.2f;
        /// <summary>Highest the lowest point of the ball may be for a foot/body touch.</summary>
        public const float MaxTouchHeight = 1.1f;
        // ---- Phase 33: the ball in the air is played in the air (head), instead of waiting for it to fall within reach of the feet
        /// <summary>Highest ball (m) a field player can head.</summary>
        public const float HeadMaxHeight = 2.2f;
        /// <summary>A header needs the ball this close horizontally (m): tighter than a foot's touch.</summary>
        public const float HeaderReach = 0.9f;
        /// <summary>Quality a header loses against a touch on the ground (added to the first-touch score as a negative bonus).</summary>
        public const float HeaderPenalty = 0.2f;
        /// <summary>A cushioned header leaves the ball at his feet-ish: this slow (m/s), along his facing.</summary>
        public const float HeaderCushionSpeed = 3.0f;
        /// <summary>A header away (clearance or pass on) leaves at this speed (m/s) and this lift (m/s), the direction off by up to HeaderErrorDegrees times (1 - quality).</summary>
        public const float HeaderAwaySpeed = 8.0f, HeaderLift = 2.5f, HeaderErrorDegrees = 40.0f;
        /// <summary>Within this distance (m) of the opponents' goal the header is at goal, at this speed (m/s).</summary>
        public const float HeaderShotRange = 8.0f, HeaderShotSpeed = 13.0f;
        /// <summary>After a header nobody heads the ball again for this long (seconds): the ball must travel before it is another header (no ping-pong between two jumping players).</summary>
        public const float HeaderLockSeconds = 0.4f;
        /// <summary>The aerial duel: how much the distance to the ball costs a player's score (share of his quality at full reach) and how much luck (seeded noise) can move it.</summary>
        public const float HeaderDuelDistanceWeight = 0.35f, HeaderDuelLuck = 0.1f;
        /// <summary>The last player to touch the ball cannot take it automatically again for this long (seconds): a kicker does not re-collect his own pass.</summary>
        public const float ReTouchCooldown = 0.3f;
        /// <summary>Phase 14: a player who has just kicked the ball does not touch it again while his kick recovers, even if somebody else touched it in between (before, one touch by anyone let him take his own pass back at once). 1 = on, 0 = off (the Phase 13 behaviour).</summary>
        public const float KickerKeepsOffHisKick = 1.0f;
        /// <summary>Phase 18: 1 = a goalkeeper who holds the ball cannot carry it over his own goal line (the held ball stays on the pitch, at most touching the line) and a ball that has already crossed the line completely can no longer be played. 0 = as before.</summary>
        public const float KeeperHoldsBallOnThePitch = 1.0f;
        // ---- control, first touch and dribbling
        /// <summary>How far in front of the player the controlled ball sits at rest.</summary>
        public const float ControlDistance = 0.8f;
        /// <summary>Extra distance of the ball in front of the player per m/s of his speed.</summary>
        public const float LeadPerSpeed = 0.1f;
        /// <summary>Beyond this distance from his feet a dribbler loses control of the ball.</summary>
        public const float LooseBallDistance = 2.2f;
        /// <summary>Largest speed (m/s) a first touch leaves the ball with relative to the player.</summary>
        public const float ControlSpeed = 6.0f;
        /// <summary>Largest acceleration (m/s^2) with which the ball can follow a dribbling player at control quality 1; scaled down for worse control.</summary>
        public const float CatchUpAcceleration = 18.0f;
        /// <summary>Share of the catch-up acceleration kept at control quality 0.</summary>
        public const float CatchUpFloor = 0.35f;
        /// <summary>How strongly the ball is pulled back towards its place in front of the player (1/s).</summary>
        public const float PullGain = 8.0f;
        /// <summary>Where a heavy (poorly controlled) first touch sends the ball, in metres in front of the player.</summary>
        public const float FirstTouchDistance = 1.0f;
        /// <summary>Beyond this angle between the player's facing and the ball a touch cannot be controlled (it deflects the ball).</summary>
        public const float MaxControlAngleDegrees = 130.0f;
        /// <summary>Ball speed (m/s) at which a first touch is hardest.</summary>
        public const float FirstTouchMaxBallSpeed = 20.0f;
        /// <summary>First-touch score at or above which the ball is controlled cleanly.</summary>
        public const float CleanControlThreshold = 0.2f;
        /// <summary>First-touch score at or above which the player still gets the ball, but heavily (it runs away from him).</summary>
        public const float HeavyTouchThreshold = -0.05f;
        /// <summary>Half-width of the seeded noise added to a first-touch score.</summary>
        public const float FirstTouchNoise = 0.1f;
        /// <summary>Speed (m/s) a mis-controlled ball bounces away with.</summary>
        public const float DeflectSpeed = 3.5f;
        /// <summary>A pressing opponent within this distance reduces a dribbler's control.</summary>
        public const float PressureRadius = 3.0f;
        /// <summary>Control quality lost per pressing opponent (at most two count).</summary>
        public const float PressureEffect = 0.25f;
        /// <summary>Phase 20: how much opponents close to the passer take from the quality of EVERY pass (short, wall, through, long, lob): quality x (1 - this x pressure), pressure 0..2 (the two nearest opponents within <see cref="PressureRadius"/>, each 1 at his feet and 0 at the radius). Half of the dribble's <see cref="PressureEffect"/>. 0 = passes ignore the rivals.</summary>
        public const float PassPressureEffect = 0.125f;
        /// <summary>How long the pressure/contain action lasts.</summary>
        public const float PressureSeconds = 1.5f;
        /// <summary>Extra stamina per second while pressing.</summary>
        public const float PressureDrainPerSecond = 0.6f;
        // ---- kicking: speeds (m/s), errors (degrees)
        /// <summary>Power used when an action input carries none.</summary>
        public const float DefaultPower = 0.6f;
        /// <summary>Short pass at power 0.</summary>
        public const float PassMinSpeed = 5.0f;
        /// <summary>Short pass at power 1 (with perfect passing).</summary>
        public const float PassMaxSpeed = 17.0f;
        /// <summary>A through ball is hit this much harder.</summary>
        public const float ThroughSpeedBoost = 1.15f;
        /// <summary>Lofted pass at power 0.</summary>
        public const float LongPassMinSpeed = 9.0f;
        /// <summary>Lofted pass at power 1.</summary>
        public const float LongPassMaxSpeed = 22.0f;
        /// <summary>Vertical speed of a lofted pass as a share of its horizontal speed.</summary>
        public const float LongPassLoft = 0.55f;
        // ---- Phase 35: the cross (a lofted ball from the flank to the box). A higher arc than a long pass: the speeds are scaled so that a power that lands a long pass N metres away lands a cross about N metres away too (sin 2a of the two lofts).
        public const float CrossMinSpeed = 8.4f;
        public const float CrossMaxSpeed = 20.5f;
        public const float CrossLoft = 0.8f;
        public const float CrossMaxError = 14.0f;
        /// <summary>Shot at power 0.</summary>
        public const float ShotMinSpeed = 12.0f;
        /// <summary>Shot at power 1 (with perfect shooting).</summary>
        public const float ShotMaxSpeed = 30.0f;
        /// <summary>Horizontal speed of a chip at power 0.</summary>
        public const float ChipMinSpeed = 5.0f;
        /// <summary>Horizontal speed of a chip at power 1.</summary>
        public const float ChipMaxSpeed = 13.0f;
        /// <summary>Vertical speed of a chip.</summary>
        public const float ChipVerticalSpeed = 5.5f;
        /// <summary>Clearance at power 0.</summary>
        public const float ClearanceMinSpeed = 12.0f;
        /// <summary>Clearance at power 1.</summary>
        public const float ClearanceMaxSpeed = 26.0f;
        /// <summary>Vertical speed of a clearance as a share of its horizontal speed.</summary>
        public const float ClearanceLoft = 0.45f;
        /// <summary>Largest angular error of a short pass (degrees) for the worst passer at full power.</summary>
        public const float PassMaxError = 12.0f;
        /// <summary>Extra error share of a through ball (riskier).</summary>
        public const float ThroughExtraError = 0.5f;
        /// <summary>Largest angular error of a lofted pass.</summary>
        public const float LongPassMaxError = 18.0f;
        /// <summary>Largest angular error of a shot.</summary>
        public const float ShotMaxError = 16.0f;
        /// <summary>Extra error share of a curved shot.</summary>
        public const float CurveExtraError = 0.25f;
        /// <summary>Largest angular error of a chip.</summary>
        public const float ChipMaxError = 14.0f;
        /// <summary>Largest angular error of a clearance.</summary>
        public const float ClearanceMaxError = 35.0f;
        /// <summary>How much of the error depends on power (error x (1 - share + share x power)).</summary>
        public const float PowerErrorShare = 0.6f;
        /// <summary>A kick this far towards the weak side of the body uses the weak foot.</summary>
        public const float WeakFootAngleDegrees = 45.0f;
        /// <summary>Weak-foot quality factor at weak foot 0 (it is 1 at weak foot 100).</summary>
        public const float WeakFootFloor = 0.6f;
        /// <summary>A tired player kicks worse: factor = Base + (1 - Base) x performance.</summary>
        public const float StaminaQualityBase = 0.85f;
        /// <summary>Spin (rad/s) of a curved shot at technique quality 1.</summary>
        public const float CurveSpinRate = 14.0f;
        /// <summary>Curve strength of the match ball (acceleration = coefficient x spin x velocity).</summary>
        public const float MagnusCoefficient = 0.012f;
        /// <summary>Within this distance of the goal Finishing counts more than Shooting.</summary>
        public const float ShotCloseRange = 12.0f;
        // ---- defending
        /// <summary>Half-width of the seeded noise added to a tackle margin.</summary>
        public const float TackleNoise = 0.12f;
        /// <summary>Margin at or above which a tackle wins the ball cleanly.</summary>
        public const float TackleCleanMargin = 0.05f;
        /// <summary>Margin at or above which a tackle at least knocks the ball loose.</summary>
        public const float TackleDeflectMargin = -0.08f;
        /// <summary>Sliding is a bigger commitment: margin modifier.</summary>
        public const float SlideBonus = -0.05f;
        /// <summary>A standing tackle failing by this much from behind is a foul.</summary>
        public const float StandingFoulMargin = -0.3f;
        /// <summary>A sliding tackle failing by this much is a foul.</summary>
        public const float SlideFoulMargin = -0.15f;
        /// <summary>Speed (m/s) a sliding player carries.</summary>
        public const float SlideSpeed = 6.0f;
        /// <summary>A slider cannot act or steer for this long.</summary>
        public const float SlideLockSeconds = 0.7f;
        /// <summary>A failed standing tackle leaves the player unbalanced for this long.</summary>
        public const float FailedTackleLockSeconds = 0.25f;
        /// <summary>Speed (m/s) the ball squirts out with after a won challenge.</summary>
        public const float ChallengeLooseSpeed = 2.5f;
        /// <summary>Intercept score at or above which the ball is taken.</summary>
        public const float InterceptCleanThreshold = 0.25f;
        /// <summary>Intercept score at or above which the ball is at least blocked/deflected.</summary>
        public const float InterceptBlockThreshold = 0.05f;
        /// <summary>Half-width of the seeded noise on an intercept score.</summary>
        public const float InterceptNoise = 0.1f;
        /// <summary>A blocked ball keeps this share of its speed.</summary>
        public const float BlockSpeedFactor = 0.3f;
        /// <summary>A ball faster than this is blocked rather than controlled.</summary>
        public const float ShotSpeedForBlock = 14.0f;
        // ---- goalkeeper rush
        /// <summary>A rush is only reasonable when the ball is this close to the goalkeeper.</summary>
        public const float RushMaxDistance = 12.0f;
        /// <summary>How long an accepted rush request stays active.</summary>
        public const float RushSeconds = 1.2f;
        /// <summary>After a kick the same player cannot start another action for this long.</summary>
        public const float KickCooldownSeconds = 0.25f;
        /// <summary>Vertical speed of a normal shot as a share of its horizontal speed (a driven shot).</summary>
        public const float ShotLoft = 0.06f;
        /// <summary>Vertical speed of a curved shot as a share of its horizontal speed (it must be in the air for the curve to act).</summary>
        public const float CurveLift = 0.14f;
        /// <summary>How long a shield (A tap with the ball) lasts.</summary>
        public const float ShieldSeconds = 1.0f;
        /// <summary>Dribble skill added to a carrier who is shielding the ball.</summary>
        public const float ShieldBonus = 0.15f;
        /// <summary>A deliberate control action adds this to the first-touch score.</summary>
        public const float ControlActionBonus = 0.1f;
        /// <summary>A goalkeeper in goal reaches this many times the normal touch reach with no diving ability...</summary>
        public const float KeeperReachBase = 1.2f;
        /// <summary>...and this many more with perfect diving.</summary>
        public const float KeeperReachRange = 0.8f;
        /// <summary>A parried ball keeps this share of its speed.</summary>
        public const float KeeperParryFactor = 0.5f;
        /// <summary>A ball in flight (pass, shot, deflection) slower than this is simply free.</summary>
        public const float FreeBallSpeed = 1.0f;
        /// <summary>How much the dribbler's own skill adds to the ball control quality while he runs with the ball.</summary>
        public const float DribbleGainRange = 0.5f;
        /// <summary>A plain shot may carry a curve up to this share of the full curve (the full curve needs the curved-shot skill).</summary>
        public const float BasicCurveLimit = 0.5f;
        // ---- formula weights (the shapes of the formulas; the weights of the attributes are in ActionProfile)
        /// <summary>Long distribution quality: this share Kicking, the rest Distribution.</summary>
        public const float KeeperLongKickShare = 0.6f;
        /// <summary>Short distribution quality: this share Distribution, the rest Kicking.</summary>
        public const float KeeperShortDistributionShare = 0.7f;
        /// <summary>A kick's speed is scaled by Base + (1 - Base) x quality.</summary>
        public const float KickSpeedQualityBase = 0.85f;
        /// <summary>A curve's spin is scaled by Base + (1 - Base) x quality.</summary>
        public const float CurveSpinQualityBase = 0.5f;
        /// <summary>A kick costs Base of its stamina at power 0 and all of it at power 1.</summary>
        public const float PowerCostBase = 0.5f;
        /// <summary>Duel margins are reported as a 0..1 quality around this value.</summary>
        public const float QualityReportBase = 0.5f;
        /// <summary>Goalkeeper touch quality: this share Handling, the rest Reflexes.</summary>
        public const float KeeperHandlingShare = 0.6f;
        /// <summary>A keeper's reflexes ease the ball: difficulty speed x (Base - Range x reflexes).</summary>
        public const float KeeperReflexEaseBase = 1.2f;
        /// <summary>See KeeperReflexEaseBase.</summary>
        public const float KeeperReflexEaseRange = 0.6f;
        /// <summary>A parry sends the ball at least this far off the line it came on.</summary>
        public const float ParryAngleMinDegrees = 35.0f;
        /// <summary>...and up to this much further.</summary>
        public const float ParryAngleRangeDegrees = 25.0f;
        /// <summary>Vertical speed (m/s) of a parried ball.</summary>
        public const float ParryLift = 2.0f;
        /// <summary>A bad first touch keeps this share of the ball's own horizontal velocity.</summary>
        public const float DeflectCarryShare = 0.25f;
        /// <summary>A bad first touch keeps this share of the ball's vertical velocity.</summary>
        public const float DeflectLiftShare = 0.2f;
        /// <summary>An intercept reaches this many times the normal touch height.</summary>
        public const float InterceptHeightFactor = 1.6f;
        /// <summary>Weight of the ball's speed in the intercept difficulty.</summary>
        public const float InterceptDifficultyWeight = 0.5f;
        /// <summary>A block sends the ball off up to this angle either side of straight back.</summary>
        public const float BlockAngleSpreadDegrees = 55.0f;
        /// <summary>Largest vertical speed (m/s) of a blocked ball.</summary>
        public const float BlockMaxLift = 2.0f;
        /// <summary>Share of the ball's speed that becomes lift when blocked.</summary>
        public const float BlockLiftShare = 0.1f;
        /// <summary>Dribbling control: this share first-touch control quality, the rest dribble skill.</summary>
        public const float DribbleControlShare = 0.5f;
        /// <summary>Weight of the relative ball speed in the first-touch difficulty.</summary>
        public const float FirstTouchSpeedWeight = 0.8f;
        /// <summary>Weight of the approach angle in the first-touch difficulty.</summary>
        public const float FirstTouchAngleWeight = 0.25f;
        /// <summary>Extra difficulty when the ball comes from beyond the maximum control angle.</summary>
        public const float FirstTouchBehindPenalty = 0.3f;
        // ---- stamina cost per action (points of 0..100)
        /// <summary>Pass at full power (half at zero power).</summary>
        public const float PassStaminaCost = 0.5f;
        /// <summary>Shot at full power.</summary>
        public const float ShotStaminaCost = 1.5f;
        /// <summary>Clearance at full power.</summary>
        public const float ClearanceStaminaCost = 1.5f;
        /// <summary>Standing tackle.</summary>
        public const float TackleStaminaCost = 2.5f;
        /// <summary>Sliding tackle.</summary>
        public const float SlideStaminaCost = 6.0f;
        /// <summary>Physical challenge.</summary>
        public const float ChallengeStaminaCost = 3.0f;
        /// <summary>Intercept/block.</summary>
        public const float InterceptStaminaCost = 1.5f;
        /// <summary>Goalkeeper rush.</summary>
        public const float RushStaminaCost = 1.0f;
    }

    /// <summary>The editable copy of <see cref="ActionDefaults"/>.</summary>
    [Serializable]
    public sealed class ActionTuning
    {

        // ---- geometry (metres): who can touch the ball
        public float TouchReach = ActionDefaults.TouchReach;
        public float KickReach = ActionDefaults.KickReach;
        public float TackleReach = ActionDefaults.TackleReach;
        public float SlideReach = ActionDefaults.SlideReach;
        public float ChallengeReach = ActionDefaults.ChallengeReach;
        public float InterceptReach = ActionDefaults.InterceptReach;
        public float MaxTouchHeight = ActionDefaults.MaxTouchHeight;
        public float HeadMaxHeight = ActionDefaults.HeadMaxHeight;
        public float HeaderReach = ActionDefaults.HeaderReach;
        public float HeaderPenalty = ActionDefaults.HeaderPenalty;
        public float HeaderCushionSpeed = ActionDefaults.HeaderCushionSpeed;
        public float HeaderAwaySpeed = ActionDefaults.HeaderAwaySpeed, HeaderLift = ActionDefaults.HeaderLift, HeaderErrorDegrees = ActionDefaults.HeaderErrorDegrees;
        public float HeaderShotRange = ActionDefaults.HeaderShotRange, HeaderShotSpeed = ActionDefaults.HeaderShotSpeed;
        public float HeaderLockSeconds = ActionDefaults.HeaderLockSeconds;
        public float HeaderDuelDistanceWeight = ActionDefaults.HeaderDuelDistanceWeight, HeaderDuelLuck = ActionDefaults.HeaderDuelLuck;
        public float ReTouchCooldown = ActionDefaults.ReTouchCooldown;
        public float KickerKeepsOffHisKick = ActionDefaults.KickerKeepsOffHisKick;
        public float KeeperHoldsBallOnThePitch = ActionDefaults.KeeperHoldsBallOnThePitch;
        // ---- control, first touch and dribbling
        public float ControlDistance = ActionDefaults.ControlDistance;
        public float LeadPerSpeed = ActionDefaults.LeadPerSpeed;
        public float LooseBallDistance = ActionDefaults.LooseBallDistance;
        public float ControlSpeed = ActionDefaults.ControlSpeed;
        public float CatchUpAcceleration = ActionDefaults.CatchUpAcceleration;
        public float CatchUpFloor = ActionDefaults.CatchUpFloor;
        public float PullGain = ActionDefaults.PullGain;
        public float FirstTouchDistance = ActionDefaults.FirstTouchDistance;
        public float MaxControlAngleDegrees = ActionDefaults.MaxControlAngleDegrees;
        public float FirstTouchMaxBallSpeed = ActionDefaults.FirstTouchMaxBallSpeed;
        public float CleanControlThreshold = ActionDefaults.CleanControlThreshold;
        public float HeavyTouchThreshold = ActionDefaults.HeavyTouchThreshold;
        public float FirstTouchNoise = ActionDefaults.FirstTouchNoise;
        public float DeflectSpeed = ActionDefaults.DeflectSpeed;
        public float PressureRadius = ActionDefaults.PressureRadius;
        public float PressureEffect = ActionDefaults.PressureEffect;
        public float PassPressureEffect = ActionDefaults.PassPressureEffect;
        public float PressureSeconds = ActionDefaults.PressureSeconds;
        public float PressureDrainPerSecond = ActionDefaults.PressureDrainPerSecond;
        // ---- kicking: speeds (m/s), errors (degrees)
        public float DefaultPower = ActionDefaults.DefaultPower;
        public float PassMinSpeed = ActionDefaults.PassMinSpeed;
        public float PassMaxSpeed = ActionDefaults.PassMaxSpeed;
        public float ThroughSpeedBoost = ActionDefaults.ThroughSpeedBoost;
        public float LongPassMinSpeed = ActionDefaults.LongPassMinSpeed;
        public float LongPassMaxSpeed = ActionDefaults.LongPassMaxSpeed;
        public float LongPassLoft = ActionDefaults.LongPassLoft;
        public float CrossMinSpeed = ActionDefaults.CrossMinSpeed;
        public float CrossMaxSpeed = ActionDefaults.CrossMaxSpeed;
        public float CrossLoft = ActionDefaults.CrossLoft;
        public float CrossMaxError = ActionDefaults.CrossMaxError;
        public float ShotMinSpeed = ActionDefaults.ShotMinSpeed;
        public float ShotMaxSpeed = ActionDefaults.ShotMaxSpeed;
        public float ChipMinSpeed = ActionDefaults.ChipMinSpeed;
        public float ChipMaxSpeed = ActionDefaults.ChipMaxSpeed;
        public float ChipVerticalSpeed = ActionDefaults.ChipVerticalSpeed;
        public float ClearanceMinSpeed = ActionDefaults.ClearanceMinSpeed;
        public float ClearanceMaxSpeed = ActionDefaults.ClearanceMaxSpeed;
        public float ClearanceLoft = ActionDefaults.ClearanceLoft;
        public float PassMaxError = ActionDefaults.PassMaxError;
        public float ThroughExtraError = ActionDefaults.ThroughExtraError;
        public float LongPassMaxError = ActionDefaults.LongPassMaxError;
        public float ShotMaxError = ActionDefaults.ShotMaxError;
        public float CurveExtraError = ActionDefaults.CurveExtraError;
        public float ChipMaxError = ActionDefaults.ChipMaxError;
        public float ClearanceMaxError = ActionDefaults.ClearanceMaxError;
        public float PowerErrorShare = ActionDefaults.PowerErrorShare;
        public float WeakFootAngleDegrees = ActionDefaults.WeakFootAngleDegrees;
        public float WeakFootFloor = ActionDefaults.WeakFootFloor;
        public float StaminaQualityBase = ActionDefaults.StaminaQualityBase;
        public float CurveSpinRate = ActionDefaults.CurveSpinRate;
        public float MagnusCoefficient = ActionDefaults.MagnusCoefficient;
        public float ShotCloseRange = ActionDefaults.ShotCloseRange;
        // ---- defending
        public float TackleNoise = ActionDefaults.TackleNoise;
        public float TackleCleanMargin = ActionDefaults.TackleCleanMargin;
        public float TackleDeflectMargin = ActionDefaults.TackleDeflectMargin;
        public float SlideBonus = ActionDefaults.SlideBonus;
        public float StandingFoulMargin = ActionDefaults.StandingFoulMargin;
        public float SlideFoulMargin = ActionDefaults.SlideFoulMargin;
        public float SlideSpeed = ActionDefaults.SlideSpeed;
        public float SlideLockSeconds = ActionDefaults.SlideLockSeconds;
        public float FailedTackleLockSeconds = ActionDefaults.FailedTackleLockSeconds;
        public float ChallengeLooseSpeed = ActionDefaults.ChallengeLooseSpeed;
        public float InterceptCleanThreshold = ActionDefaults.InterceptCleanThreshold;
        public float InterceptBlockThreshold = ActionDefaults.InterceptBlockThreshold;
        public float InterceptNoise = ActionDefaults.InterceptNoise;
        public float BlockSpeedFactor = ActionDefaults.BlockSpeedFactor;
        public float ShotSpeedForBlock = ActionDefaults.ShotSpeedForBlock;
        // ---- goalkeeper rush
        public float RushMaxDistance = ActionDefaults.RushMaxDistance;
        public float RushSeconds = ActionDefaults.RushSeconds;
        // ---- stamina cost per action (points of 0..100)
        public float PassStaminaCost = ActionDefaults.PassStaminaCost;
        public float ShotStaminaCost = ActionDefaults.ShotStaminaCost;
        public float ClearanceStaminaCost = ActionDefaults.ClearanceStaminaCost;
        public float TackleStaminaCost = ActionDefaults.TackleStaminaCost;
        public float SlideStaminaCost = ActionDefaults.SlideStaminaCost;
        public float ChallengeStaminaCost = ActionDefaults.ChallengeStaminaCost;
        public float InterceptStaminaCost = ActionDefaults.InterceptStaminaCost;
        public float RushStaminaCost = ActionDefaults.RushStaminaCost;
        public float KickCooldownSeconds = ActionDefaults.KickCooldownSeconds;
        public float ShotLoft = ActionDefaults.ShotLoft;
        public float CurveLift = ActionDefaults.CurveLift;
        public float ShieldSeconds = ActionDefaults.ShieldSeconds;
        public float ShieldBonus = ActionDefaults.ShieldBonus;
        public float ControlActionBonus = ActionDefaults.ControlActionBonus;
        public float KeeperReachBase = ActionDefaults.KeeperReachBase;
        public float KeeperReachRange = ActionDefaults.KeeperReachRange;
        public float KeeperParryFactor = ActionDefaults.KeeperParryFactor;
        public float FreeBallSpeed = ActionDefaults.FreeBallSpeed;
        public float DribbleGainRange = ActionDefaults.DribbleGainRange;
        public float BasicCurveLimit = ActionDefaults.BasicCurveLimit;
        public float KeeperLongKickShare = ActionDefaults.KeeperLongKickShare;
        public float KeeperShortDistributionShare = ActionDefaults.KeeperShortDistributionShare;
        public float KickSpeedQualityBase = ActionDefaults.KickSpeedQualityBase;
        public float CurveSpinQualityBase = ActionDefaults.CurveSpinQualityBase;
        public float PowerCostBase = ActionDefaults.PowerCostBase;
        public float QualityReportBase = ActionDefaults.QualityReportBase;
        public float KeeperHandlingShare = ActionDefaults.KeeperHandlingShare;
        public float KeeperReflexEaseBase = ActionDefaults.KeeperReflexEaseBase;
        public float KeeperReflexEaseRange = ActionDefaults.KeeperReflexEaseRange;
        public float ParryAngleMinDegrees = ActionDefaults.ParryAngleMinDegrees;
        public float ParryAngleRangeDegrees = ActionDefaults.ParryAngleRangeDegrees;
        public float ParryLift = ActionDefaults.ParryLift;
        public float DeflectCarryShare = ActionDefaults.DeflectCarryShare;
        public float DeflectLiftShare = ActionDefaults.DeflectLiftShare;
        public float InterceptHeightFactor = ActionDefaults.InterceptHeightFactor;
        public float InterceptDifficultyWeight = ActionDefaults.InterceptDifficultyWeight;
        public float BlockAngleSpreadDegrees = ActionDefaults.BlockAngleSpreadDegrees;
        public float BlockMaxLift = ActionDefaults.BlockMaxLift;
        public float BlockLiftShare = ActionDefaults.BlockLiftShare;
        public float DribbleControlShare = ActionDefaults.DribbleControlShare;
        public float FirstTouchSpeedWeight = ActionDefaults.FirstTouchSpeedWeight;
        public float FirstTouchAngleWeight = ActionDefaults.FirstTouchAngleWeight;
        public float FirstTouchBehindPenalty = ActionDefaults.FirstTouchBehindPenalty;
    }
}
