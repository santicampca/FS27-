using System;

namespace FS27.Core
{
    /// <summary>Layout/behaviour of the on-screen stick, in "canvas units" (they scale with the screen).</summary>
    [Serializable]
    public class VirtualStickSettings
    {
        /// <summary>Thumb travel from the centre for full deflection.</summary>
        public float Radius = 150f;
        /// <summary>Where the stick rests when not touched, measured from the zone's bottom-left corner.</summary>
        public Vec2 RestPosition = new Vec2(280f, 260f);
        /// <summary>If true the stick appears where the thumb lands; if false it stays at the rest position.</summary>
        public bool Floating = true;
    }

    /// <summary>
    /// Pure model of a floating virtual joystick: pointer events in, deflection out. All positions are in
    /// the touch zone's own space (origin at its bottom-left corner, Y up). The Unity VirtualJoystick only
    /// forwards pointer events here and draws the result.
    ///
    /// The output is the stick DEFLECTION (0..1). It is the only thing that selects walk / run / sprint:
    /// there is deliberately no sprint button or flag.
    /// </summary>
    public sealed class VirtualStickModel
    {
        private const int NoPointer = int.MinValue;

        public VirtualStickSettings Settings { get; }

        /// <summary>Current deflection vector, length 0..1 (X right, Y up).</summary>
        public Vec2 Value { get; private set; }
        /// <summary>Centre of the stick background, in zone space.</summary>
        public Vec2 BaseCenter { get; private set; }
        public bool IsActive => activePointer != NoPointer;
        /// <summary>Knob offset from the base centre (for drawing).</summary>
        public Vec2 KnobOffset => Value * Settings.Radius;

        private int activePointer = NoPointer;

        public VirtualStickModel(VirtualStickSettings settings)
        {
            Settings = settings;
            BaseCenter = settings.RestPosition;
        }

        /// <summary>Returns false (and ignores the touch) if another finger already owns the stick.</summary>
        public bool PointerDown(int pointerId, Vec2 position, Vec2 zoneSize)
        {
            if (IsActive) return false;
            activePointer = pointerId;

            if (Settings.Floating)
            {
                float r = Settings.Radius;
                BaseCenter = new Vec2(
                    Clamp(position.X, r, Math.Max(r, zoneSize.X - r)),
                    Clamp(position.Y, r, Math.Max(r, zoneSize.Y - r)));
            }
            else
            {
                BaseCenter = Settings.RestPosition;
            }

            Update(position);
            return true;
        }

        public void PointerMove(int pointerId, Vec2 position)
        {
            if (pointerId != activePointer) return;
            Update(position);
        }

        public void PointerUp(int pointerId)
        {
            if (pointerId != activePointer) return;
            Reset();
        }

        public void Reset()
        {
            activePointer = NoPointer;
            Value = Vec2.Zero;
            BaseCenter = Settings.RestPosition;
        }

        public PlayerIntent ToIntent()
        {
            return new PlayerIntent(Value);
        }

        private void Update(Vec2 position)
        {
            float r = Math.Max(1e-3f, Settings.Radius);
            Vec2 v = (position - BaseCenter) * (1f / r);
            float len = v.Magnitude;
            Value = len > 1f ? v * (1f / len) : v;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
