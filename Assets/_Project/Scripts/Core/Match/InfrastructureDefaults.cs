using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-7 AI infrastructure, in one place (the same rule as <see cref="ActionDefaults"/>): starting values copied into an editable <see cref="InfrastructureTuning"/>.
    /// DECISION MADE DURING PHASE 7: all of them (the specification fixes the contracts, not the values). Metres and seconds; the pitch is 40 x 25 by default.
    /// </summary>
    public static class InfrastructureDefaults
    {
        // ---- pitch and players inside it
        /// <summary>Depth of a team's goal zone in front of its goal line.</summary>
        public const float GoalZoneDepth = 4.0f;
        /// <summary>Half width of the goal zone.</summary>
        public const float GoalZoneHalfWidth = 5.0f;
        /// <summary>A player may be this far beyond a line (a throw-in is taken on the line) but never further.</summary>
        public const float PlayerBoundsMargin = 1.0f;

        // ---- separation between players
        /// <summary>Two players never end a step closer than this (no physics: a stable push apart).</summary>
        public const float MinSeparation = 0.4f;
        public const int SeparationIterations = 3;

        // ---- formations and restarts
        /// <summary>At a kick-off every player stays this far inside his own half.</summary>
        public const float KickoffHalfMargin = 0.5f;
        /// <summary>Players that do not take a restart stay at least this far from the ball.</summary>
        public const float RestartClearRadius = 2.5f;
        /// <summary>The kick-off taker stands this far behind the ball (towards his own goal).</summary>
        public const float TakerOffset = 0.8f;

        // ---- world queries
        /// <summary>A teammate is available for a pass when no opponent is closer than this to the line of the pass.</summary>
        public const float PassLaneClearance = 1.2f;
        public const float MaxPassDistance = 30.0f;
        /// <summary>The radius in which opponents are counted as "close" (the basic free-space measure).</summary>
        public const float OpenSpaceRadius = 3.5f;
        /// <summary>The ball's horizontal speed below this: it has no direction.</summary>
        public const float BallDirectionMinSpeed = 0.05f;
    }

    /// <summary>The editable copy of <see cref="InfrastructureDefaults"/>.</summary>
    public sealed class InfrastructureTuning
    {
        /// <summary>
        /// Players keep inside the pitch and apart (see <see cref="PlayerSeparation"/>). Off for a match nobody has set up (the older hosts and tests put every player at the origin and expect the Phase-3/4 behaviour);
        /// switched on automatically by the Phase-7 set-up (<see cref="MatchController.PlaceFormation"/>, <see cref="MatchController.PrepareRestart"/>) or by hand.
        /// </summary>
        public bool EnforcePlayerSpace;
        public float GoalZoneDepth = InfrastructureDefaults.GoalZoneDepth;
        public float GoalZoneHalfWidth = InfrastructureDefaults.GoalZoneHalfWidth;
        public float PlayerBoundsMargin = InfrastructureDefaults.PlayerBoundsMargin;
        public float MinSeparation = InfrastructureDefaults.MinSeparation;
        public int SeparationIterations = InfrastructureDefaults.SeparationIterations;
        public float KickoffHalfMargin = InfrastructureDefaults.KickoffHalfMargin;
        public float RestartClearRadius = InfrastructureDefaults.RestartClearRadius;
        public float TakerOffset = InfrastructureDefaults.TakerOffset;
        public float PassLaneClearance = InfrastructureDefaults.PassLaneClearance;
        public float MaxPassDistance = InfrastructureDefaults.MaxPassDistance;
        public float OpenSpaceRadius = InfrastructureDefaults.OpenSpaceRadius;
        public float BallDirectionMinSpeed = InfrastructureDefaults.BallDirectionMinSpeed;
    }
}
