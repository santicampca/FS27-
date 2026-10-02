namespace FS27.Core
{
    /// <summary>What the body is doing, for animation, HUD and rules. Descriptive only: the speed is always continuous and never depends on this label.</summary>
    public enum MovementState
    {
        Idle = 0,
        Walk,
        Run,
        Sprint,
        Exhausted
    }

    /// <summary>
    /// The movement state of ONE player in the Core: where he is, where he faces, how fast he moves and how much stamina he has. Field space (centre 0,0; X along the pitch).
    /// The Core owns it; Unity only reads it. CurrentStamina is the player's state (0..100); his Stamina ATTRIBUTE lives in <see cref="LocomotionProfile"/> and never changes.
    /// </summary>
    public struct LocomotionState
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>Radians; 0 = +Y of the field, growing towards +X (the heading convention of <see cref="Vec2.FromHeading"/>).</summary>
        public float Heading;
        /// <summary>The last input direction (unit length) or zero when there was no input.</summary>
        public Vec2 InputDirection;
        /// <summary>0..1.</summary>
        public float InputMagnitude;
        /// <summary>m/s along the facing direction.</summary>
        public float CurrentSpeed;
        /// <summary>0..100.</summary>
        public float CurrentStamina;
        public MovementState MovementState;

        public Vec2 FacingDirection { get { return Vec2.FromHeading(Heading); } }

        public static LocomotionState Create(Vec2 position, float heading = 0f, float stamina = LocomotionDefaults.MaxStamina)
        {
            return new LocomotionState { Position = position, Heading = heading, CurrentStamina = stamina, MovementState = MovementState.Idle };
        }
    }
}
