using System;

namespace FS27.Core
{
    /// <summary>Infinite plane (e.g. a boundary wall). The ball is kept on the side its normal points to.</summary>
    public sealed class PlaneCollider : IBallCollider
    {
        public readonly Vec3 Normal;
        public readonly float Offset;
        public readonly float Restitution;
        public readonly float Friction;

        /// <param name="normal">Direction the plane faces (need not be normalised).</param>
        /// <param name="offset">Signed distance from the origin along the normal.</param>
        public PlaneCollider(Vec3 normal, float offset, float restitution = 0.6f, float friction = 0.2f)
        {
            Normal = normal.Normalized;
            Offset = offset;
            Restitution = restitution;
            Friction = friction;
        }

        public bool Resolve(ref BallState state, BallParameters p)
        {
            float distance = Vec3.Dot(state.Position, Normal) - Offset;
            if (distance >= p.Radius) return false;

            state.Position += Normal * (p.Radius - distance);

            float normalSpeed = Vec3.Dot(state.Velocity, Normal);
            if (normalSpeed >= 0f) return true; // already moving away

            Vec3 normalPart = Normal * normalSpeed;
            Vec3 tangent = state.Velocity - normalPart;

            float tangentSpeed = tangent.Magnitude;
            if (tangentSpeed > 1e-5f)
            {
                float removed = Math.Min(tangentSpeed, Friction * (1f + Restitution) * -normalSpeed);
                tangent -= tangent * (removed / tangentSpeed);
            }

            state.Velocity = tangent - normalPart * Restitution;
            return true;
        }
    }
}
