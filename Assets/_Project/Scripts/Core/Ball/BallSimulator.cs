using System;

namespace FS27.Core
{
    /// <summary>
    /// Reference physics model of the ball, in pure C#: gravity, quadratic air drag, optional Magnus
    /// (curved flight from spin), ground bounce with friction, rolling resistance, extra forces and
    /// extra colliders. Deterministic and allocation-free.
    ///
    /// The Unity ball currently runs on PhysX (BallController/BallPhysics); this model is the testable
    /// reference used to predict trajectories (goalkeeper, AI, camera, replays) and to tune parameters.
    /// Improving the physics means changing this class + <see cref="BallParameters"/>, nothing else.
    /// </summary>
    public static class BallSimulator
    {
        public static void Step(ref BallState state, BallParameters p, float dt,
                                IBallCollider[] colliders = null, IBallForce extraForce = null)
        {
            if (dt <= 0f) return;

            int steps = Math.Max(1, (int)Math.Ceiling(dt / Math.Max(1e-4f, p.MaxSubstep)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++) Substep(ref state, p, h, colliders, extraForce);
        }

        private static void Substep(ref BallState s, BallParameters p, float h, IBallCollider[] colliders, IBallForce extraForce)
        {
            Vec3 v = s.Velocity;
            Vec3 a = new Vec3(0f, -p.Gravity, 0f);

            float speed = v.Magnitude;
            if (speed > 1e-6f && p.AirDragCoefficient > 0f)
                a -= v * (p.AirDragCoefficient * speed);

            if (p.MagnusCoefficient != 0f)
                a += Vec3.Cross(s.Spin, v) * p.MagnusCoefficient;

            if (extraForce != null)
                a += extraForce.Acceleration(s, p);

            s.Velocity = v + a * h;
            s.Position += s.Velocity * h;

            if (!s.IsGrounded)
                s.Spin *= Math.Max(0f, 1f - p.SpinDamping * h);

            ResolveGround(ref s, p, h);

            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].Resolve(ref s, p);
        }

        private static void ResolveGround(ref BallState s, BallParameters p, float h)
        {
            float r = p.Radius;
            if (s.Position.Y > r + 1e-4f)
            {
                s.IsGrounded = false;
                return;
            }

            Vec3 v = s.Velocity;
            Vec3 pos = s.Position;
            pos.Y = r;
            bool grounded;

            if (v.Y < 0f)
            {
                float impact = -v.Y;
                if (impact > p.BounceThreshold)
                {
                    v.Y = impact * p.Restitution;
                    float flat = (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
                    if (flat > 1e-5f)
                    {
                        float removed = Math.Min(flat, p.BounceFriction * (1f + p.Restitution) * impact);
                        float scale = (flat - removed) / flat;
                        v.X *= scale;
                        v.Z *= scale;
                    }
                    grounded = false;
                }
                else
                {
                    v.Y = 0f;
                    grounded = true;
                }
            }
            else
            {
                grounded = v.Y < 0.05f;
            }

            if (grounded)
            {
                float flat = (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
                if (flat <= p.RestSpeed)
                {
                    v.X = 0f;
                    v.Z = 0f;
                    s.Spin = Vec3.Zero;
                }
                else
                {
                    float slowed = Math.Max(0f, flat - p.RollingDeceleration * h);
                    float scale = slowed / flat;
                    v.X *= scale;
                    v.Z *= scale;
                    // Rolling without slipping: spin axis perpendicular to travel.
                    s.Spin = Vec3.Cross(Vec3.Up, new Vec3(v.X, 0f, v.Z)) * (1f / r);
                }
            }

            s.Position = pos;
            s.Velocity = v;
            s.IsGrounded = grounded;
        }

        /// <summary>
        /// Instantly changes the ball's velocity (a touch, kick or impact). Provided for gameplay layers;
        /// they decide the velocity, the simulator only applies it.
        /// </summary>
        public static void ApplyVelocityChange(ref BallState state, Vec3 deltaVelocity)
        {
            state.Velocity += deltaVelocity;
            if (deltaVelocity.Y > 0f) state.IsGrounded = false;
        }
    }
}
