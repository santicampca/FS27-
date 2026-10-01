using System;

namespace FS27.Core
{
    /// <summary>Tuning for the simple dribble. Defaults mirror the Unity BallInteractor component.</summary>
    [Serializable]
    public class DribbleParameters
    {
        public float FootOffset = 0.55f;
        public float TouchRadius = 0.7f;
        /// <summary>Ball must be at least this "in front" (dot product with heading), -1..1.</summary>
        public float MinFrontDot = 0.2f;
        public float MaxBallHeight = 0.6f;
        public float MinSpeed = 0.8f;
        public float TouchInterval = 0.12f;
        public float LeadAtWalk = 0.6f;
        public float LeadAtSprint = 3.0f;
        public float TouchStrength = 0.8f;
    }

    /// <summary>Result of evaluating a dribble touch.</summary>
    public struct DribbleTouch
    {
        public bool Occurs;
        /// <summary>Horizontal velocity (field space, m/s) the ball should be nudged toward.</summary>
        public Vec2 DesiredVelocity;
        /// <summary>0..1: how much of the velocity difference to apply.</summary>
        public float Strength;
    }

    /// <summary>
    /// Pure version of the dribble rule: "while running with the ball near your feet and in front of you,
    /// tap it ahead". It only decides; applying the touch is the caller's job (<see cref="BallSimulator.ApplyVelocityChange"/>
    /// or PhysX). The ball is never attached to the player.
    /// </summary>
    public class DribbleTouchModel
    {
        public readonly DribbleParameters Parameters;
        private float nextTouchTime;

        public DribbleTouchModel(DribbleParameters parameters)
        {
            Parameters = parameters;
        }

        public void Reset()
        {
            nextTouchTime = 0f;
        }

        public DribbleTouch Evaluate(float time, Vec2 playerPosition, float heading, float speed, float topSpeed,
                                     in BallState ball, float ballRadius)
        {
            DribbleParameters d = Parameters;
            DribbleTouch none = new DribbleTouch();

            if (time < nextTouchTime || speed < d.MinSpeed) return none;
            if (ball.Position.Y - ballRadius > d.MaxBallHeight) return none;

            Vec2 forward = Vec2.FromHeading(heading);
            Vec2 foot = playerPosition + forward * d.FootOffset;
            Vec2 ballXZ = ball.Position.XZ;

            if ((ballXZ - foot).SqrMagnitude > d.TouchRadius * d.TouchRadius) return none;

            Vec2 fromPlayer = ballXZ - playerPosition;
            if (fromPlayer.SqrMagnitude > 1e-6f)
            {
                Vec2 n = fromPlayer.Normalized;
                if (forward.X * n.X + forward.Y * n.Y < d.MinFrontDot) return none;
            }

            float speed01 = topSpeed > 0f ? MathUtil.Clamp01(speed / topSpeed) : 0f;
            float lead = MathUtil.Lerp(d.LeadAtWalk, d.LeadAtSprint, speed01);
            nextTouchTime = time + d.TouchInterval;

            return new DribbleTouch
            {
                Occurs = true,
                DesiredVelocity = forward * (speed + lead),
                Strength = d.TouchStrength
            };
        }
    }
}
