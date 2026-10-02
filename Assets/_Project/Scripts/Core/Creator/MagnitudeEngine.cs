using System;

namespace FS27.Core
{
    /// <summary>
    /// The ONE place where words like "un poco", "mucho" or "extremadamente" become numbers. Two readings of the same level:
    ///   Delta    - a RELATIVE change ("más rápido", "un poco más alto"): fraction of the parameter's range to move.
    ///   Absolute - an ABSOLUTE description ("muy alto", "algo bajo"): how far from the neutral value, as a fraction of the way to the end of
    ///              the range in that direction (0 = neutral, 1 = the end of the range).
    /// Scale (default values, all tunable through <see cref="MagnitudeTable"/>):
    ///   level        delta   absolute   (es)                         (en)
    ///   Minimal      0.04    0.15       muy poco, un pelín           barely, a hair
    ///   Slight       0.08    0.25       ligeramente, poquito         slightly
    ///   Little       0.12    0.35       un poco                      a little
    ///   Moderate     0.20    0.50       (sin palabra), algo, más     (no word), somewhat
    ///   Quite        0.30    0.65       bastante                     quite, fairly
    ///   Much         0.40    0.75       mucho, muy                   a lot, very
    ///   VeryMuch     0.50    0.85       muchísimo                    a great deal
    ///   Extreme      0.65    0.95       extremadamente               extremely
    ///   Maximum      1.00    1.00       al máximo, lo máximo         to the max
    /// Unspecified behaves as Moderate. Changing a number here changes every language and every interpreter at once.
    /// </summary>
    [Serializable]
    public sealed class MagnitudeTable
    {
        public float[] Delta = { 0.20f, 0.04f, 0.08f, 0.12f, 0.20f, 0.30f, 0.40f, 0.50f, 0.65f, 1.00f };
        public float[] Absolute = { 0.50f, 0.15f, 0.25f, 0.35f, 0.50f, 0.65f, 0.75f, 0.85f, 0.95f, 1.00f };
    }

    public static class MagnitudeEngine
    {
        public static readonly MagnitudeTable Default = new MagnitudeTable();

        public static float Delta(MagnitudeLevel level, MagnitudeTable table = null)
        {
            return (table ?? Default).Delta[(int)level];
        }

        public static float Absolute(MagnitudeLevel level, MagnitudeTable table = null)
        {
            return (table ?? Default).Absolute[(int)level];
        }

        /// <summary>The level as one step stronger / weaker, kept inside the scale (Maximum stays Maximum).</summary>
        public static MagnitudeLevel Step(MagnitudeLevel level, int steps)
        {
            int v = (int)(level == MagnitudeLevel.Unspecified ? MagnitudeLevel.Moderate : level) + steps;
            if (v < (int)MagnitudeLevel.Minimal) v = (int)MagnitudeLevel.Minimal;
            if (v > (int)MagnitudeLevel.Maximum) v = (int)MagnitudeLevel.Maximum;
            return (MagnitudeLevel)v;
        }

        /// <summary>True for the levels that make a negation a SOFT limit ("no demasiado", "not too"), not a reversal.</summary>
        public static bool IsHigh(MagnitudeLevel level)
        {
            return level >= MagnitudeLevel.Quite;
        }

        /// <summary>
        /// What a negated description means, as a signed absolute intensity (+ toward the described direction):
        ///   "no muy alto"  / "not too tall"   -> a SOFT cap: still a little in that direction, but only (1 - level) of it  (+).
        ///   "no alto"      / "not tall"       -> the opposite direction at the same level (-), because the person wants the contrary.
        ///   "sin barba"    is a removal, not a magnitude, and never goes through here.
        /// </summary>
        public static float NegatedAbsolute(MagnitudeLevel level, MagnitudeTable table = null)
        {
            if (level == MagnitudeLevel.Unspecified || level == MagnitudeLevel.Moderate) return -Absolute(MagnitudeLevel.Moderate, table);
            if (IsHigh(level)) return Math.Max(0f, 1f - Absolute(level, table)) + 0.05f;
            return -Absolute(level, table);
        }

        /// <summary>The same for a relative change: "no quiero que driblee tanto" -> decrease by this fraction of the range.</summary>
        public static float NegatedDelta(MagnitudeLevel level, MagnitudeTable table = null)
        {
            return -Delta(level == MagnitudeLevel.Unspecified ? MagnitudeLevel.Moderate : level, table);
        }

        public static bool IsMaximum(MagnitudeLevel level)
        {
            return level == MagnitudeLevel.Maximum;
        }

        /// <summary>A readable sentence of the whole scale, used by the docs and the debug report.</summary>
        public static string DescribeScale(MagnitudeTable table = null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i < Enum.GetValues(typeof(MagnitudeLevel)).Length; i++)
                sb.Append(((MagnitudeLevel)i).ToString()).Append(": delta ").Append(Delta((MagnitudeLevel)i, table).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", absolute ").Append(Absolute((MagnitudeLevel)i, table).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }
    }
}
