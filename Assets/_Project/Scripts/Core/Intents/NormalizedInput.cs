namespace FS27.Core
{
    /// <summary>
    /// The input of a person, already abstract: what Unity's touch layer produces (Unity: Touch -> NormalizedInput) and the Core consumes (Core: NormalizedInput -> ActionInput -> ActionRequest).
    /// The stick (<see cref="Move"/>), and optionally one action: a button (A / B / C), how it was used (tap, swipe up, swipe down), the direction of the gesture and its strength. No pixels, no
    /// finger ids, no Unity types. <see cref="Sanitized"/> makes any value safe (NaN, out of range).
    /// </summary>
    public struct NormalizedInput
    {
        /// <summary>The movement stick, length 0..1 (field space).</summary>
        public Vec2 Move;
        public ActionButton Button;
        public ActionGesture Gesture;
        /// <summary>Direction of the swipe / aim (need not be unit length; zero = where the player faces).</summary>
        public Vec2 Aim;
        /// <summary>0..1: how hard (power).</summary>
        public float Power;
        /// <summary>-1..1: curve for a curved shot (0 = the skill's default).</summary>
        public float Curve;

        public bool HasAction { get { return Button != ActionButton.None; } }

        public static NormalizedInput Stick(Vec2 move) { return new NormalizedInput { Move = move }; }

        public static NormalizedInput Press(Vec2 move, ActionButton button, ActionGesture gesture, Vec2 aim, float power, float curve = 0f)
        {
            return new NormalizedInput { Move = move, Button = button, Gesture = gesture, Aim = aim, Power = power, Curve = curve };
        }

        /// <summary>A safe copy: non-finite vectors become zero, the stick is at most length 1, power and curve are clamped, an unknown button is none.</summary>
        public NormalizedInput Sanitized()
        {
            NormalizedInput n = this;
            if (!ActionModel.IsFinite(n.Move)) n.Move = Vec2.Zero;
            else if (n.Move.Magnitude > 1f) n.Move = n.Move.Normalized;
            if (!ActionModel.IsFinite(n.Aim)) n.Aim = Vec2.Zero;
            n.Power = ActionModel.Clamp01(n.Power);
            float c = float.IsNaN(n.Curve) ? 0f : n.Curve;
            n.Curve = c < -1f ? -1f : (c > 1f ? 1f : c);
            if (n.Button < ActionButton.None || n.Button > ActionButton.C) n.Button = ActionButton.None;
            if (n.Gesture < ActionGesture.Tap || n.Gesture > ActionGesture.SwipeDown) n.Gesture = ActionGesture.Tap;
            return n;
        }

        /// <summary>The Phase-4 action input (button, gesture, direction, magnitude, curve) the mapper reads.</summary>
        public ActionInput ToActionInput()
        {
            NormalizedInput n = Sanitized();
            return new ActionInput(n.Button, n.Gesture, n.Aim, n.Power) { Curve = n.Curve };
        }

        public LocomotionInput ToLocomotion() { return new LocomotionInput(Sanitized().Move); }
    }
}
