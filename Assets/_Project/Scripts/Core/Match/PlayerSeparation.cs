using System;

namespace FS27.Core
{
    /// <summary>
    /// A minimal, stable separation between players: no physics, no velocities, no collision response. Any two points closer than the minimum are pushed apart, half each, along the line
    /// between them; two identical points are pushed along a fixed axis that depends only on their indices. Deterministic: same points, same result, order of indices only.
    /// </summary>
    public static class PlayerSeparation
    {
        /// <summary>Moves <paramref name="positions"/> (the first <paramref name="count"/>) apart in place. Returns true when nothing had to move. <paramref name="axisSign"/> (+1 or -1) flips the axis used for two identical points, so a mirrored layout stays an exact mirror.</summary>
        public static bool Resolve(Vec2[] positions, int count, float minDistance, int iterations, float axisSign = 1f)
        {
            if (positions == null || !(minDistance > 0f)) return true;
            bool untouched = true;
            if (count > positions.Length) count = positions.Length;
            for (int pass = 0; pass < Math.Max(1, iterations); pass++)
            {
                bool moved = false;
                for (int i = 0; i < count; i++)
                    for (int j = i + 1; j < count; j++)
                    {
                        Vec2 d = positions[j] - positions[i];
                        float sqr = d.SqrMagnitude;
                        if (sqr >= minDistance * minDistance) continue;
                        float dist = (float)Math.Sqrt(sqr);
                        Vec2 axis = dist > 1e-4f ? d * (1f / dist) : FallbackAxis(i, j) * axisSign;
                        float push = (minDistance - dist) * 0.5f;
                        positions[i] = positions[i] - axis * push;
                        positions[j] = positions[j] + axis * push;
                        moved = true;
                    }
                if (moved) untouched = false; else break;
            }
            return untouched;
        }

        /// <summary>The smallest distance between any two of the first <paramref name="count"/> points (infinity for fewer than two).</summary>
        public static float Smallest(Vec2[] positions, int count)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++) best = Math.Min(best, (positions[j] - positions[i]).Magnitude);
            return best;
        }

        private static Vec2 FallbackAxis(int i, int j)
        {
            // eight fixed directions chosen by the pair of indices: no randomness, no state
            int k = (i * 3 + j) & 7;
            return ActionModel.Rotate(new Vec2(1f, 0f), k * 45f);
        }
    }
}
