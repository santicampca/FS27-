using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 28: the chance that a pass reaches its receiver, from what the passer can see: can any rival get to the path of the ball before the ball does (the ball's real travel time, <see cref="PassPhysics"/>, against the time each rival
    /// needs to run to that point) and will the kick land close enough to the receiver given the passer's quality (the angular error of the action at that distance). A pure function of the view: no state, no chance. The decision
    /// layer subtracts the cost of losing the ball (more in front of one's own goal) weighted by the chance that it is lost, so that a pass is worth what it gains times how likely it is to arrive, not only what it gains.
    /// </summary>
    public static class PassRisk
    {
        private const float Epsilon = 1e-4f;

        /// <summary>0..1: the chance the pass from <paramref name="me"/> to <paramref name="r"/> arrives (1 = nothing can stop it).</summary>
        public static float SuccessChance(WorldView view, in WorldPlayer me, in WorldPlayer r, float distance, BasicDecisionTuning t, bool forceLofted = false)
        {
            if (distance <= Epsilon) return 1f;
            bool lofted = forceLofted || distance >= t.LongPassDistance;
            float minMargin = LaneMargin(view, me, r, distance, t, lofted);
            float lane = float.IsPositiveInfinity(minMargin) ? 1f : 1f / (1f + (float)Math.Exp(-(minMargin - t.PassMarginCenter) / t.PassMarginScale));
            float quality = me.Profile.PassQuality * t.PlannedQualityShare;
            float maxError = lofted ? ActionDefaults.LongPassMaxError : ActionDefaults.PassMaxError;
            float powerFactor = 1f - ActionDefaults.PowerErrorShare + ActionDefaults.PowerErrorShare * t.PassRiskPowerEstimate;
            float lateral = distance * (float)Math.Tan(maxError * (1f - ActionModel.Clamp01(quality)) * powerFactor * MathUtil.Deg2Rad);
            float accuracy = lateral <= Epsilon ? 1f : Math.Min(1f, t.PassAccuracyReach / lateral);
            return lane * accuracy;
        }

        /// <summary>The smallest margin (seconds) by which the ball beats every rival to the points of its path: negative = a rival gets to the path first. Positive infinity when there is no rival.</summary>
        public static float LaneMargin(WorldView view, in WorldPlayer me, in WorldPlayer r, float distance, BasicDecisionTuning t, bool lofted)
        {
            Vec2 dir = (r.Position - me.Position) * (1f / distance);
            float minMargin = float.PositiveInfinity;
            if (!lofted)
            {
                float arrival = WorldGeometry.OpponentsWithin(view, me.Team, r.Position, t.PassRiskPressureRadius) > 0 ? t.PressedArrivalSpeed : t.GroundArrivalSpeed;
                float launch = PassPhysics.GroundLaunchSpeed(distance, arrival);
                for (int j = 0; j <= t.PassRiskSamples; j++)
                {
                    // sample 0 is the passer's own feet: a rival already within reach of the ball when it is kicked gets a touch at once (no reaction time), whatever side of the passer he is on
                    float along = j == 0 ? t.PassRiskStartOffset : distance * j / t.PassRiskSamples;
                    float ballTime = PassPhysics.GroundTime(launch, along);
                    minMargin = Math.Min(minMargin, RivalMargin(view, me.Team, me.Position + dir * along, ballTime, t.PassReactionSeconds, t.PassInterceptorSpeedShare, t.PassInterceptReach));
                }
            }
            else
            {
                float landing = distance * t.LandingShare;
                float flight = PassPhysics.LoftedFlightTime(PassPhysics.LoftedLaunchSpeed(landing));
                minMargin = RivalMargin(view, me.Team, me.Position + dir * landing, flight, t.PassReactionSeconds, t.PassInterceptorSpeedShare, t.PassInterceptReach);
            }
            return minMargin;
        }


        /// <summary>
        /// The margin (seconds) by which a ground pass leaves the passer's feet before any rival can take it, over the first metres of its path: the rivals who press the passer are already running and need no time to see the
        /// ball go. Negative = a rival gets a touch on the ball soon after the kick. Positive infinity when no rival is near enough to matter or the pass is lofted (it flies over them).
        /// </summary>
        public static float ReleaseMargin(WorldView view, in WorldPlayer me, Vec2 target, BasicDecisionTuning t, BallCarrierTuning c)
        {
            Vec2 ab = target - me.Position; float distance = ab.Magnitude;
            if (distance <= Epsilon || distance >= t.LongPassDistance) return float.PositiveInfinity;
            Vec2 dir = ab * (1f / distance);
            float launch = PassPhysics.GroundLaunchSpeed(distance, t.GroundArrivalSpeed);
            float span = Math.Min(c.ReleaseSpan, distance);
            float best = float.PositiveInfinity;
            for (int j = 0; j <= c.ReleaseSamples; j++)
            {
                float along = j == 0 ? t.PassRiskStartOffset : span * j / c.ReleaseSamples;
                best = Math.Min(best, RivalMargin(view, me.Team, me.Position + dir * along, PassPhysics.GroundTime(launch, along), c.ReleaseReaction, c.ReleaseSpeedShare, c.ReleaseReach));
            }
            return best;
        }

        /// <summary>The smallest (rival's time to the point) minus (the ball's time to it) over the rivals: negative = a rival gets there first.</summary>
        private static float RivalMargin(WorldView view, TeamSide team, Vec2 point, float ballTime, float reaction, float speedShare, float reach)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == team) continue;
                float speed = o.MaxSpeed * speedShare;
                if (speed <= Epsilon) continue;
                float gap = (o.Position - point).Magnitude - reach;
                float run = gap <= 0f ? 0f : gap / speed + reaction;
                float margin = run - ballTime;
                if (margin < best) best = margin;
            }
            return best;
        }
    }
}
