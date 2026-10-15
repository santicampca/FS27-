using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-28 set pieces (throw-ins, goal kicks, corners), in one place, like <see cref="FreeKickDefaults"/>: starting values copied into an editable <see cref="SetPieceTuning"/>.
    /// Metres and seconds; the pitch is 40 x 25 by default. These restarts exist only where the mode has them (Classic); Arena never asks for one, so none of this runs there.
    /// </summary>
    public static class SetPieceDefaults
    {
        /// <summary>Off = the Phase-27 behaviour (the ball is carried like any other, the opponents are only kept out of the clearance circle at the set-up).</summary>
        public const bool Enabled = true;

        // ---- the procedure: after the whistle the taker plays the ball from the spot
        /// <summary>The ball stays on the spot, in the taker's possession, until he plays it; the Core gives up after this long (the referee's limit) and the taker is free.</summary>
        public const float HoldMaxSeconds = 6.0f;
        /// <summary>The taker loses the right to the held ball when he walks further than this from the spot with it.</summary>
        public const float TakerMaxDistance = 2.5f;
        /// <summary>Opponents keep this far from the ball of a corner kick until it is played.</summary>
        public const float CornerOpponentRadius = 3.5f;
        /// <summary>Opponents keep this far from the ball of a goal kick until it is played.</summary>
        public const float GoalKickOpponentRadius = 5.0f;
        /// <summary>Opponents keep this far from the ball of a throw-in until it is played (the common clearance of the infrastructure when this is not larger).</summary>
        public const float ThrowInOpponentRadius = 2.5f;

        // ---- where the team-mates stand to offer a pass (the Core places them when it sets the restart up)
        /// <summary>At most this many team-mates are placed as options; the deepest one always stays back to cover.</summary>
        public const int MaxOptions = 3;
        /// <summary>Throw-in: a safe option square or back, this far in from the line and this far behind the thrower (along the attack).</summary>
        public const float ThrowShortIn = 5.5f, ThrowShortBack = 2.0f;
        /// <summary>Throw-in: an option forward and in.</summary>
        public const float ThrowMidIn = 8.0f, ThrowMidAhead = 5.0f;
        /// <summary>Throw-in: a far option, well ahead and well in.</summary>
        public const float ThrowFarIn = 11.0f, ThrowFarAhead = 10.0f;
        /// <summary>Goal kick: the two short options stand this far ahead of the spot and this far to each side of the middle.</summary>
        public const float GoalKickShortAhead = 6.0f, GoalKickShortWide = 8.0f;
        /// <summary>Goal kick: the long option stands this far ahead of the spot, in the middle.</summary>
        public const float GoalKickLongAhead = 14.0f;
        /// <summary>Corner: the short option stands this far from the corner along the goal line (back) and the touchline (in).</summary>
        public const float CornerShortBack = 5.0f, CornerShortIn = 3.0f;
        /// <summary>Corner: the penalty-spot option, this far in front of the goal line.</summary>
        public const float CornerCentreDepth = 5.0f;
        /// <summary>Corner: the near-post and far-post options, in front of the goal line and off the middle line.</summary>
        public const float CornerNearDepth = 2.0f, CornerNearWide = 2.5f, CornerFarDepth = 2.5f, CornerFarWide = 3.5f;
        /// <summary>The option point may be moved this far (a grid of this step, this many rings) to be free of rivals.</summary>
        public const float OptionSearchStep = 1.5f;
        public const int OptionSearchRings = 2;
        /// <summary>Free space around an option counts up to this many metres.</summary>
        public const float OptionRivalCap = 6.0f;
        /// <summary>The room between the pass lane (spot to option) and the nearest rival counts this much against a metre of room around the option itself.</summary>
        public const float OptionLaneWeight = 1.5f;
        /// <summary>What every metre an option is moved from its ideal point costs, against a metre of free space.</summary>
        public const float OptionMoveCost = 0.4f;
        /// <summary>Options and the thrower keep at least this far from each other.</summary>
        public const float OptionSeparation = 2.5f;
        /// <summary>An option stays this far inside the lines.</summary>
        public const float OptionLineMargin = 1.0f;
        // ---- Phase 33: a person takes the throw-in or the corner (the aim is the free kick's: same drag, same zones, same charge)
        /// <summary>How long the game waits for a person to take a throw-in or a corner before the AI takes it (seconds).</summary>
        public const float HumanWaitSeconds = 12.0f;
        /// <summary>The power of a throw-in is the free kick's charge (a tap = its minimum, a full charge = 1) times this: the hands never throw as far as a boot kicks.</summary>
        public const float ThrowMaxPower = 0.62f;
        /// <summary>From this power (after the scaling above) a throw is a lofted long throw (a different flight from a ground pass); below it, a short throw along the ground.</summary>
        public const float ThrowLoftedPower = 0.42f;
    }

    /// <summary>The editable copy of <see cref="SetPieceDefaults"/>.</summary>
    public sealed class SetPieceTuning
    {
        public bool Enabled = SetPieceDefaults.Enabled;
        public float HoldMaxSeconds = SetPieceDefaults.HoldMaxSeconds;
        public float TakerMaxDistance = SetPieceDefaults.TakerMaxDistance;
        public float CornerOpponentRadius = SetPieceDefaults.CornerOpponentRadius;
        public float GoalKickOpponentRadius = SetPieceDefaults.GoalKickOpponentRadius;
        public float ThrowInOpponentRadius = SetPieceDefaults.ThrowInOpponentRadius;
        public int MaxOptions = SetPieceDefaults.MaxOptions;
        public float ThrowShortIn = SetPieceDefaults.ThrowShortIn, ThrowShortBack = SetPieceDefaults.ThrowShortBack;
        public float ThrowMidIn = SetPieceDefaults.ThrowMidIn, ThrowMidAhead = SetPieceDefaults.ThrowMidAhead;
        public float ThrowFarIn = SetPieceDefaults.ThrowFarIn, ThrowFarAhead = SetPieceDefaults.ThrowFarAhead;
        public float GoalKickShortAhead = SetPieceDefaults.GoalKickShortAhead, GoalKickShortWide = SetPieceDefaults.GoalKickShortWide;
        public float GoalKickLongAhead = SetPieceDefaults.GoalKickLongAhead;
        public float CornerShortBack = SetPieceDefaults.CornerShortBack, CornerShortIn = SetPieceDefaults.CornerShortIn;
        public float CornerCentreDepth = SetPieceDefaults.CornerCentreDepth;
        public float CornerNearDepth = SetPieceDefaults.CornerNearDepth, CornerNearWide = SetPieceDefaults.CornerNearWide, CornerFarDepth = SetPieceDefaults.CornerFarDepth, CornerFarWide = SetPieceDefaults.CornerFarWide;
        public float OptionSearchStep = SetPieceDefaults.OptionSearchStep;
        public int OptionSearchRings = SetPieceDefaults.OptionSearchRings;
        public float OptionRivalCap = SetPieceDefaults.OptionRivalCap;
        public float OptionLaneWeight = SetPieceDefaults.OptionLaneWeight;
        public float OptionMoveCost = SetPieceDefaults.OptionMoveCost;
        public float OptionSeparation = SetPieceDefaults.OptionSeparation;
        public float OptionLineMargin = SetPieceDefaults.OptionLineMargin;
        public float HumanWaitSeconds = SetPieceDefaults.HumanWaitSeconds;
        public float ThrowMaxPower = SetPieceDefaults.ThrowMaxPower;
        public float ThrowLoftedPower = SetPieceDefaults.ThrowLoftedPower;

        /// <summary>The distance the opponents keep from the ball of that restart: the common clearance, or more for the set pieces that have a procedure of their own.</summary>
        public float OpponentRadius(RestartKind kind, float common)
        {
            switch (kind)
            {
                case RestartKind.ThrowIn: return Math.Max(common, ThrowInOpponentRadius);
                case RestartKind.Corner: return Math.Max(common, CornerOpponentRadius);
                case RestartKind.GoalKick: return Math.Max(common, GoalKickOpponentRadius);
                default: return common;
            }
        }
    }

    /// <summary>
    /// The set piece the match is handling (Phase 28): from the whistle of a throw-in, goal kick or corner until the taker plays the ball. While it lasts the ball waits on the spot in the taker's possession
    /// (he cannot carry it away) and the opponents stay out of the circle. Kept after it ends (Active false) until the next one, so a test or a report can read it.
    /// </summary>
    public sealed class SetPieceState
    {
        public bool Active;
        public RestartKind Kind;
        public TeamSide Team;
        public string TakerId;
        public Vec2 Spot;
        /// <summary>Core seconds since the whistle.</summary>
        public float Seconds;
        /// <summary>How it ended: true when the taker played the ball, false when he lost it or the time ran out.</summary>
        public bool PlayedByTaker;
        /// <summary>How many set pieces the match has seen.</summary>
        public int Number;
    }
}
