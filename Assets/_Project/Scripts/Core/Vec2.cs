using System;

namespace FS27.Core
{
    /// <summary>
    /// Minimal 2D vector for simulation code, so Core stays free of UnityEngine.
    /// Field space: X = right, Y = "up the screen" (world +Z in Unity).
    /// </summary>
    [Serializable]
    public struct Vec2
    {
        public float X;
        public float Y;

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        /// <summary>Unit vector for a heading in radians (0 = +Y, increasing clockwise toward +X).</summary>
        public static Vec2 FromHeading(float radians)
        {
            return new Vec2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }

        /// <summary>Heading in radians of this vector (inverse of FromHeading).</summary>
        public float ToHeading()
        {
            return (float)Math.Atan2(X, Y);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator *(float s, Vec2 a) { return new Vec2(a.X * s, a.Y * s); }
    }
}
