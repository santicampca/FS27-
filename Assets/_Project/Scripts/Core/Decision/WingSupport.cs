using System;

namespace FS27.Core
{
    /// <summary>Phase 39 (REQ-0177): every number of the full-back's outside support. One place; twins in <see cref="WingSupportTuning"/>. Metres, m/s.</summary>
    public static class WingSupportDefaults
    {
        /// <summary>A player (not the goalkeeper, not a forward) whose formation place is this far from the middle line or more is a wide player: the full-back of a 4-line, the wide midfielder of the 6-a-side shape.</summary>
        public const float WideHome = 6.0f;
        /// <summary>The winger counts as cutting inside when he moves towards the middle at this speed or more.</summary>
        public const float CutInSpeed = 0.8f;
        /// <summary>... and is at least this far from the middle and has crossed this share of the pitch towards the goal he attacks.</summary>
        public const float MinWingerOffset = 3.0f, MinAdvance = 0.45f;
        /// <summary>The full-back runs to a point this far outside the winger (sideways) and this far ahead of him (m).</summary>
        public const float OutsideOffset = 4.0f, AheadOffset = 3.0f;
        /// <summary>The point is kept this far inside the touchline (m); the full-back does not come if he is further than this from it (m).</summary>
        public const float TouchlineMargin = 1.5f, MaxRunDistance = 22.0f;
        public const float RunMagnitude = 0.95f;
    }

    [Serializable]
    public sealed class WingSupportTuning
    {
        public float WideHome = WingSupportDefaults.WideHome;
        public float CutInSpeed = WingSupportDefaults.CutInSpeed;
        public float MinWingerOffset = WingSupportDefaults.MinWingerOffset, MinAdvance = WingSupportDefaults.MinAdvance;
        public float OutsideOffset = WingSupportDefaults.OutsideOffset, AheadOffset = WingSupportDefaults.AheadOffset;
        public float TouchlineMargin = WingSupportDefaults.TouchlineMargin, MaxRunDistance = WingSupportDefaults.MaxRunDistance;
        public float RunMagnitude = WingSupportDefaults.RunMagnitude;
    }

    /// <summary>
    /// Phase 39 (REQ-0177): when the winger carries the ball INSIDE, the wide player of his flank who is behind him (the full-back) offers the outside: he runs past him on the touchline side, a little ahead (the overlap), so the winger has a pass out wide.
    /// Only the nearest wide player of that flank goes. Pure: reads the world, writes nothing.
    /// </summary>
    public sealed class WingSupport
    {
        public WingSupportTuning Tuning = new WingSupportTuning();

        /// <summary>Whether this player is the full-back who offers the outside, and where he runs.</summary>
        public bool TryTarget(WorldView view, int idx, in WorldPlayer me, in WorldPlayer carrier, Vec2 attack, out Vec2 target)
        {
            target = default(Vec2);
            WingSupportTuning t = Tuning;
            if (me.IsGoalkeeper || me.Role == PlayerRole.Forward || !me.HasHome || me.Team != carrier.Team || me.PlayerId == carrier.PlayerId) return false;
            float side = carrier.Position.Y >= 0f ? 1f : -1f;
            if (Math.Abs(carrier.Position.Y) < t.MinWingerOffset) return false;
            if (me.Home.Y * side < t.WideHome) return false;                       // not a wide player of the carrier's flank
            if (((me.Position - carrier.Position).X * attack.X + (me.Position - carrier.Position).Y * attack.Y) >= 0f) return false;   // he runs from behind the winger, not from in front
            if (carrier.Velocity.Y * side > -t.CutInSpeed) return false;           // he is not cutting inside
            float length = view.Field.Length;
            float advance = (carrier.Position.X * attack.X + length / 2f) / length;   // 0 at his own goal line, 1 at the one he attacks
            if (advance < t.MinAdvance) return false;
            float halfWidth = view.Field.Width / 2f;
            target = new Vec2(carrier.Position.X + attack.X * t.AheadOffset, side * Math.Min(Math.Abs(carrier.Position.Y) + t.OutsideOffset, halfWidth - t.TouchlineMargin));
            if ((target - me.Position).Magnitude > t.MaxRunDistance) return false;
            // only the nearest full-back of that flank goes
            float mine = (target - me.Position).Magnitude;
            int start = view.TeamStart(me.Team), end = start + view.TeamCount(me.Team);
            for (int i = start; i < end; i++)
            {
                if (i == idx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.IsGoalkeeper || o.Role == PlayerRole.Forward || !o.HasHome || o.Home.Y * side < t.WideHome) continue;
                float d = (target - o.Position).Magnitude;
                if (d < mine || (d == mine && i < idx)) return false;
            }
            return true;
        }
    }
}
