using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-22 free kick, in one place (the same rule as <see cref="ActionDefaults"/>): starting values copied into an editable <see cref="FreeKickTuning"/>. Metres and seconds; the pitch
    /// is 40 x 25 by default, so the distances are those of a small pitch (a real wall stands 9.15 m from the ball on a 105 m pitch).
    /// DECISION MADE DURING PHASE 22: all of them (the specification fixes the contract, not the values).
    /// </summary>
    public static class FreeKickDefaults
    {
        /// <summary>A called foul stops the play and gives a free kick. Off = the Phase-21 behaviour (the foul is recorded and published, the fouled team just keeps the ball): for experiments and for the tests of the referee that are about the call, not the restart.</summary>
        public const bool Enabled = true;

        // ---- the spot
        /// <summary>The ball is never placed closer than this to a line: a foul on the line is taken just inside it.</summary>
        public const float SpotFieldMargin = 1.0f;
        /// <summary>A foul in the goal zone is not a penalty (a later phase): the free kick is taken this far from the goal line instead, so it is still a free kick and never a tap-in.</summary>
        public const float SpotMinGoalLineDistance = 4.5f;

        // ---- the taker
        /// <summary>The taker stands this far behind the ball, looking at the goal he attacks.</summary>
        public const float TakerOffsetBehind = 1.0f;
        /// <summary>Up to this distance from the goal the taker is chosen for his shooting; further away for his passing.</summary>
        public const float TakerShotDistance = 16.0f;
        /// <summary>A taker who may curve the ball (he owns the skill) is worth this much more in the choice.</summary>
        public const float TakerCurveBonus = 0.10f;
        /// <summary>What every metre between a player and the spot costs him in the choice (per metre of the pitch length).</summary>
        public const float TakerDistancePenalty = 0.20f;

        // ---- the wall
        /// <summary>The opponents stand at least this far from the ball (and the wall stands exactly there).</summary>
        public const float WallDistance = 4.0f;
        /// <summary>Distance between two players of the wall (shoulder to shoulder).</summary>
        public const float WallSpacing = 0.8f;
        /// <summary>No wall when the ball is further than this from the goal it attacks.</summary>
        public const float WallMaxGoalDistance = 18.0f;
        /// <summary>Up to this distance from the goal the wall has three players.</summary>
        public const float WallThreeDistance = 8.0f;
        /// <summary>Up to this distance from the goal the wall has two players (beyond it, one).</summary>
        public const float WallTwoDistance = 13.0f;
        public const int WallMaxPlayers = 3;
        /// <summary>A defending team always keeps at least this many field players OUT of the wall (they mark, they cover the rebound).</summary>
        public const int WallMinFieldDefendersOutside = 1;
        /// <summary>The wall covers the NEAR post: its middle looks at this fraction of the half goal, on the side of the ball (the goalkeeper covers the rest). 0 = the middle of the goal.</summary>
        public const float WallPostCoverage = 0.6f;
        /// <summary>A ball this close to the middle line of the pitch (|y|) is aimed to the middle of the goal.</summary>
        public const float WallCentreBand = 0.5f;

        // ---- time
        /// <summary>After the set-up the free kick is taken by the Core by itself after this long (the whistle) unless a person is taking it.</summary>
        public const float ReadySeconds = 2.0f;
        /// <summary>A free kick that a person has to take (aim, charge) waits this long; then the Core takes it for him with the AI's decision, so the match is never blocked.</summary>
        public const float HumanMaxWaitSeconds = 12.0f;
        /// <summary>A person has this long, from the stoppage, to choose who takes the free kick; then the choice (the automatic one if nobody chose) is locked and the aiming begins. The AI never waits: its taker is automatic.</summary>
        public const float TakerChoiceSeconds = 5.0f;
        /// <summary>The set-up waits this long for a team to put somebody in goal; then the Core puts the emergency keeper on (safety net: the match is never blocked).</summary>
        public const float KeeperVacancySeconds = 3.0f;
        /// <summary>After the whistle the opponents may not enter the circle of the ball until it is played; this long at most.</summary>
        public const float ProtectionSeconds = 2.5f;
        /// <summary>The ball is "played" when it leaves the taker's possession or moves further than this from the spot.</summary>
        public const float PlayedDistance = 0.5f;
        /// <summary>While the ball is protected an opponent may stand this much closer than the wall distance (the wall itself is there) before he is pushed back.</summary>
        public const float ProtectionSlack = 0.25f;

        // ---- aiming (the contract for a finger)
        /// <summary>Holding the shot button this long gives full power.</summary>
        public const float FullChargeSeconds = 1.2f;
        /// <summary>The minimum power of a released shot (a tap is not zero).</summary>
        public const float MinPower = 0.25f;
        /// <summary>A pass drag counts as aimed at a team-mate when he is inside this angle of the drag.</summary>
        public const float PassAimConeDegrees = 25.0f;
        /// <summary>The goal zones: the centre of a side zone is this fraction of the half goal from the middle.</summary>
        public const float GoalZoneSideFraction = 0.8f;
        /// <summary>A free-kick pass of at least this length is a long pass; shorter, a short pass.</summary>
        public const float PassLongDistance = 12.0f;
        /// <summary>A free-kick pass with no team-mate on it (a drag into space) is a long pass from this power on.</summary>
        public const float PassLongPower = 0.7f;
        /// <summary>Power of a free kick the AI shoots.</summary>
        public const float AiShotPower = 0.9f;
        /// <summary>The curve the AI asks for (the taker needs the skill for it to apply).</summary>
        public const float AiCurve = 0.6f;
    }

    /// <summary>The editable copy of <see cref="FreeKickDefaults"/>.</summary>
    public sealed class FreeKickTuning
    {
        public bool Enabled = FreeKickDefaults.Enabled;
        public float SpotFieldMargin = FreeKickDefaults.SpotFieldMargin;
        public float SpotMinGoalLineDistance = FreeKickDefaults.SpotMinGoalLineDistance;
        public float TakerOffsetBehind = FreeKickDefaults.TakerOffsetBehind;
        public float TakerShotDistance = FreeKickDefaults.TakerShotDistance;
        public float TakerCurveBonus = FreeKickDefaults.TakerCurveBonus;
        public float TakerDistancePenalty = FreeKickDefaults.TakerDistancePenalty;
        public float WallDistance = FreeKickDefaults.WallDistance;
        public float WallSpacing = FreeKickDefaults.WallSpacing;
        public float WallMaxGoalDistance = FreeKickDefaults.WallMaxGoalDistance;
        public float WallThreeDistance = FreeKickDefaults.WallThreeDistance;
        public float WallTwoDistance = FreeKickDefaults.WallTwoDistance;
        public int WallMaxPlayers = FreeKickDefaults.WallMaxPlayers;
        public int WallMinFieldDefendersOutside = FreeKickDefaults.WallMinFieldDefendersOutside;
        public float WallPostCoverage = FreeKickDefaults.WallPostCoverage;
        public float WallCentreBand = FreeKickDefaults.WallCentreBand;
        public float ReadySeconds = FreeKickDefaults.ReadySeconds;
        public float HumanMaxWaitSeconds = FreeKickDefaults.HumanMaxWaitSeconds;
        public float TakerChoiceSeconds = FreeKickDefaults.TakerChoiceSeconds;
        public float KeeperVacancySeconds = FreeKickDefaults.KeeperVacancySeconds;
        public float ProtectionSeconds = FreeKickDefaults.ProtectionSeconds;
        public float PlayedDistance = FreeKickDefaults.PlayedDistance;
        public float ProtectionSlack = FreeKickDefaults.ProtectionSlack;
        public float FullChargeSeconds = FreeKickDefaults.FullChargeSeconds;
        public float MinPower = FreeKickDefaults.MinPower;
        public float PassAimConeDegrees = FreeKickDefaults.PassAimConeDegrees;
        public float GoalZoneSideFraction = FreeKickDefaults.GoalZoneSideFraction;
        public float PassLongDistance = FreeKickDefaults.PassLongDistance;
        public float PassLongPower = FreeKickDefaults.PassLongPower;
        public float AiShotPower = FreeKickDefaults.AiShotPower;
        public float AiCurve = FreeKickDefaults.AiCurve;
    }

    /// <summary>Where a free kick is in its life (Phase 22). The restart request itself keeps the generic <see cref="RestartStatus"/>.</summary>
    public enum FreeKickStage
    {
        /// <summary>No free kick.</summary>
        None = 0,
        /// <summary>Play is stopped, the ball is at the spot; taker and wall are being set up.</summary>
        Preparing,
        /// <summary>Taker and wall are in place; the kick waits for the whistle (the Core, the presentation or the person who takes it).</summary>
        Ready,
        /// <summary>The whistle has gone: the ball is in play, but the opponents still keep their distance until it is played.</summary>
        Protected,
        /// <summary>The ball has been played; the free kick is over.</summary>
        Done
    }

    /// <summary>
    /// The free kick the match is handling (Phase 22): what the foul was, who benefits, who takes it, where the wall stands. Kept after the kick (Stage Done) until the next one, so a test or a report can read it.
    /// It is the Core's record: the referee's call (<see cref="DisciplineRecord"/>) is not changed by it.
    /// </summary>
    public sealed class FreeKickState
    {
        public FreeKickStage Stage;
        /// <summary>1, 2, 3... in the order the match awarded them.</summary>
        public int Number;
        /// <summary>The team that takes it (the fouled team).</summary>
        public TeamSide Beneficiary;
        public TeamSide OffenderTeam;
        public string OffenderId;
        public string VictimId;
        public FoulReason Reasons;
        /// <summary>Index of the foul in <see cref="MatchState.Discipline"/> (-1 when unknown).</summary>
        public int DisciplineIndex = -1;
        /// <summary>Where the ball is placed.</summary>
        public Vec2 Spot;
        public string TakerId;
        /// <summary>The taker the Core would choose by itself (the best one by its rule); a person may choose another one.</summary>
        public string SuggestedTakerId;
        /// <summary>A person chose the current taker (<see cref="MatchController.ChooseFreeKickTaker"/>) and he is still the one.</summary>
        public bool TakerChosenManually;
        /// <summary>The taker is final: a person confirmed him, the choice time ran out, the kick was aimed, or the beneficiary is not a person (the AI chooses by itself). No more choosing.</summary>
        public bool TakerConfirmed;
        /// <summary>The player a person had chosen became unavailable (sent off, substituted, in goal) and the Core chose a valid one in his place.</summary>
        public bool TakerReplaced;
        /// <summary>Seconds left to choose the taker (0 when it is locked).</summary>
        public float TakerChoiceRemaining;
        /// <summary>The players that form the wall (possibly none: far from the goal there is no wall).</summary>
        public readonly List<string> Wall = new List<string>();
        /// <summary>Core seconds since the stoppage.</summary>
        public float WaitSeconds;
        /// <summary>Core seconds since the whistle (while Protected).</summary>
        public float ProtectedSeconds;
        /// <summary>How the kick was taken (Done): true when the taker kicked it (a person or the AI), false when the ball was played otherwise (the taker lost it, protection ran out).</summary>
        public bool KickedByTaker;
        /// <summary>Seconds a team has been waiting for a goalkeeper in the set-up (safety net).</summary>
        internal float KeeperWaitSeconds;
        internal bool Whistled;

        public bool Active { get { return Stage == FreeKickStage.Preparing || Stage == FreeKickStage.Ready; } }
        public bool InProtection { get { return Stage == FreeKickStage.Protected; } }
    }

    /// <summary>The answer to a choice of taker (<see cref="MatchController.ChooseFreeKickTaker"/>, <see cref="MatchController.ConfirmFreeKickTaker"/>).</summary>
    public enum FreeKickTakerResult
    {
        Accepted = 0,
        /// <summary>There is no free kick waiting to be taken.</summary>
        NoFreeKick,
        /// <summary>The team asking is not the one that takes it, or it is not controlled by a person (the AI chooses its own taker).</summary>
        NotUserTeam,
        /// <summary>Nobody has that id in the match.</summary>
        UnknownPlayer,
        /// <summary>He plays for the other team.</summary>
        WrongTeam,
        /// <summary>He is not on the pitch (on the bench, substituted off).</summary>
        NotAvailable,
        /// <summary>He has been sent off.</summary>
        Dismissed,
        /// <summary>He is in goal (the goalkeeper does not take free kicks).</summary>
        Goalkeeper,
        /// <summary>The taker is already final (confirmed, the choice time ran out, or the kick was aimed).</summary>
        Locked
    }

    /// <summary>
    /// One player who may take the free kick, as the future screen lists him (<see cref="MatchController.FreeKickTakerCandidates"/>). What the Core knows is always filled (id, role, the action numbers it derived, the curve);
    /// the name, shirt number and raw attributes are filled when the host passes the player lookup (the Core never copies them: they stay in the player's definition).
    /// </summary>
    public struct FreeKickTakerCandidate
    {
        public string PlayerId;
        public PlayerRole Role;
        /// <summary>He is the taker right now.</summary>
        public bool IsCurrent;
        /// <summary>He is the one the Core would choose by itself.</summary>
        public bool IsSuggested;
        /// <summary>The Core's own score for this free kick (higher = better): the one the automatic choice uses.</summary>
        public float Score;
        /// <summary>0..1, derived (<see cref="ActionProfile"/>).</summary>
        public float ShotClose, ShotFar, Pass;
        public PreferredFoot PreferredFoot;
        /// <summary>He owns the skill that curves a shot (the effect of the aim is applied only for him).</summary>
        public bool CanCurve;
        public float DistanceToBall;
        /// <summary>True when a definition was found: the fields below are then valid.</summary>
        public bool HasDefinition;
        public string Name;
        public int ShirtNumber;
        /// <summary>The raw attributes (1..99) a screen shows for a free kick.</summary>
        public int ShootingValue, TechniqueValue, PassingValue, FinishingValue;
    }

    /// <summary>What the person aims at (or the AI decides): shoot at the goal, pass to a team-mate or play to a point.</summary>
    public enum FreeKickIntent
    {
        /// <summary>At goal: a shot (curved when the taker may and the aim asks).</summary>
        Shot = 0,
        /// <summary>To a team-mate (the one named, or the one the drag points at).</summary>
        Pass,
        /// <summary>To a point of the pitch (a drag with no team-mate on it).</summary>
        PassToPoint,
        /// <summary>At goal, lofted (the existing chip): the way over a wall that closes the lane of a ground shot.</summary>
        Lob
    }

    /// <summary>The horizontal zones of the goal, as the ATTACKER sees them (left / right of the goal he looks at).</summary>
    public enum GoalZone
    {
        Centre = 0,
        Left,
        Right
    }

    /// <summary>
    /// The neutral input of a free kick (Phase 22): what a finger gives a game without the Core knowing about screens. The person drags a direction (an indicator), HOLDS the shot button (the charge becomes power),
    /// and RELEASES: the release calls <see cref="MatchController.ExecuteFreeKick"/>. The direction is in field space (the presentation converts it); the Core never reads a touch.
    /// </summary>
    public struct FreeKickAim
    {
        public FreeKickIntent Intent;
        /// <summary>The aim, in field space. Zero = towards the centre of the goal the taker attacks.</summary>
        public Vec2 Direction;
        /// <summary>The draggable point (the indicator): a destination in field space. When set it wins over <see cref="Direction"/>; the Core brings it inside the lines and the ball goes towards it.</summary>
        public bool HasDestination;
        public Vec2 Destination;
        /// <summary>A zone of the goal the taker attacks (a shot). Wins over <see cref="Destination"/> and <see cref="Direction"/> when <see cref="HasZone"/>.</summary>
        public bool HasZone;
        public GoalZone Zone;
        /// <summary>How long the button was held; <see cref="FreeKickAiming.PowerFromHold"/> turns it into power. Used when <see cref="Power"/> is not set.</summary>
        public float HoldSeconds;
        /// <summary>0..1, explicit power (wins over <see cref="HoldSeconds"/> when positive).</summary>
        public float Power;
        /// <summary>-1..1: the curve asked (the existing action system applies it only when the taker owns the skill; otherwise the shot is straight).</summary>
        public float Curve;
        /// <summary>For <see cref="FreeKickIntent.Pass"/>: the team-mate. Null = the one nearest to the direction of the drag (see <see cref="FreeKickAiming.FindPassTarget"/>).</summary>
        public string TargetPlayerId;
    }

    /// <summary>Why <see cref="MatchController.ExecuteFreeKick"/> did not (or did) take the kick.</summary>
    public enum FreeKickExecuteResult
    {
        Executed = 0,
        /// <summary>There is no free kick waiting to be taken.</summary>
        NoFreeKick,
        /// <summary>The set-up cannot finish (a team still needs a goalkeeper, nobody can take it).</summary>
        NotReady,
        /// <summary>The aim is not valid (not a number, an unknown intent, a named team-mate who is not on the pitch): nothing is whistled and the free kick keeps waiting.</summary>
        InvalidAim,
        /// <summary>The restart was confirmed, but the action itself was refused (the reason is in <see cref="MatchController.LastActionResult"/>).</summary>
        ActionRefused
    }

    /// <summary>
    /// The pure maths of the aiming contract (Phase 22): from a drag and a charge to a direction, a power and, if wanted, a team-mate. No state, no physics: the kick itself goes through the existing action system
    /// (<see cref="MatchController.Perform"/>), so the ball physics are not duplicated and the keeper and the wall react to the ball, never to the decision.
    /// </summary>
    public static class FreeKickAiming
    {
        /// <summary>Power from the time the button was held: a tap gives <see cref="FreeKickTuning.MinPower"/>, <see cref="FreeKickTuning.FullChargeSeconds"/> or more gives 1.</summary>
        public static float PowerFromHold(float holdSeconds, FreeKickTuning t)
        {
            if (float.IsNaN(holdSeconds) || holdSeconds <= 0f) return t.MinPower;
            float k = t.FullChargeSeconds > 0f ? holdSeconds / t.FullChargeSeconds : 1f;
            return ActionModel.Clamp01(t.MinPower + (1f - t.MinPower) * k);
        }

        /// <summary>The point of the goal line a zone means, as the attacker sees it (left of his view = the side that, looking at the goal, is on his left).</summary>
        public static Vec2 GoalZonePoint(GoalZone zone, Vec2 goalCentre, Vec2 attackDirection, float goalWidth, FreeKickTuning t)
        {
            if (zone == GoalZone.Centre) return goalCentre;
            // looking along attackDirection, "left" is the vector rotated a quarter turn anticlockwise
            Vec2 left = new Vec2(-attackDirection.Y, attackDirection.X);
            float sign = zone == GoalZone.Left ? 1f : -1f;
            return goalCentre + left * (sign * goalWidth * 0.5f * t.GoalZoneSideFraction);
        }

        /// <summary>
        /// Phase 33: the ONE place that turns an aim into a destination, for free kicks AND corners (and throw-ins): a goal zone wins over the dragged point, which wins over the drag direction; with nothing, <paramref name="fallback"/>.
        /// <paramref name="hasDestination"/> is false when only a direction was given (the ball goes that way, to no particular point).
        /// </summary>
        public static Vec2 ResolveDestination(in FreeKickAim aim, Vec2 spot, Vec2 goalCentre, Vec2 attackDirection, float goalWidth, FreeKickTuning t, Func<Vec2, Vec2> insideLines, Vec2 fallback, out bool hasDestination)
        {
            hasDestination = true;
            if (aim.HasZone) return GoalZonePoint(aim.Zone, goalCentre, attackDirection, goalWidth, t);
            if (aim.HasDestination) return insideLines(aim.Destination);
            if (aim.Direction.Magnitude > 1e-4f) { hasDestination = false; return spot + aim.Direction.Normalized; }
            return fallback;
        }

        /// <summary>The team-mate the drag points at: the one with the smallest angle to the drag inside the cone, ties to the nearer, then to the earlier in the list. Returns -1 when nobody is inside.</summary>
        public static int FindPassTarget(Vec2 spot, Vec2 direction, IList<Vec2> teammates, FreeKickTuning t)
        {
            if (teammates == null || direction.Magnitude < 1e-4f) return -1;
            Vec2 d = direction.Normalized;
            float cone = (float)Math.Cos(t.PassAimConeDegrees * (float)Math.PI / 180f);
            int best = -1;
            float bestCos = -2f, bestDistance = float.MaxValue;
            for (int i = 0; i < teammates.Count; i++)
            {
                Vec2 v = teammates[i] - spot;
                float m = v.Magnitude;
                if (m < 1e-4f) continue;
                float c = (v.X * d.X + v.Y * d.Y) / m;
                if (c < cone) continue;
                if (c > bestCos + 1e-5f || (Math.Abs(c - bestCos) <= 1e-5f && m < bestDistance)) { best = i; bestCos = c; bestDistance = m; }
            }
            return best;
        }
    }
}
