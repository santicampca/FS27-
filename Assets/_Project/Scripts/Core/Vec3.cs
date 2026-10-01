using System;

namespace FS27.Core
{
    /// <summary>
    /// Minimal 3D vector for simulation code (Core has no UnityEngine). Same axes as Unity world space:
    /// X = right, Y = up, Z = up the screen / along the pitch width. <see cref="Vec2"/> field space maps to (X, Z).
    /// </summary>
    [Serializable]
    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Vec3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec3(X / m, Y / m, Z / m) : Zero;
            }
        }

        /// <summary>Field-space projection: (X, Z).</summary>
        public Vec2 XZ => new Vec2(X, Z);

        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            t = MathUtil.Clamp01(t);
            return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        public static Vec3 FromXZ(Vec2 v, float y)
        {
            return new Vec3(v.X, y, v.Y);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, float s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
        public static Vec3 operator *(float s, Vec3 a) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
    }
}
