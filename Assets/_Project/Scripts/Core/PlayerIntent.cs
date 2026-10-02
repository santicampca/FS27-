namespace FS27.Core
{
    /// <summary>
    /// How the body moves while doing something. A style, not a speed: the speed (jog, run, sprint) still comes ONLY from the length of the move
    /// vector, i.e. the stick deflection. There is deliberately no sprint style, flag or button.
    /// </summary>
    public enum MovementStyle
    {
        Default = 0,
        Jog,
        Run,
        /// <summary>A short, sharp acceleration.</summary>
        Burst,
        /// <summary>Planting to stop the ball or the body.</summary>
        Stop,
        Feint,
        CutInside,
        CutOutside,
        Shield,
        Press,
        /// <summary>Waiting, then starting late.</summary>
        Delayed
    }

    /// <summary>Optional detail for a dribble.</summary>
    public struct DribbleIntent
    {
        /// <summary>Preferred direction to carry the ball (field space, 0..1 length); zero = follow the movement.</summary>
        public Vec2 Direction;
        /// <summary>0..1: how hard to try to beat the defender (0 = just carry).</summary>
        public float Aggressiveness;
    }

    /// <summary>Optional detail for a shot.</summary>
    public struct ShotIntent
    {
        /// <summary>Where to aim, in field space; zero = the goal chosen by gameplay.</summary>
        public Vec2 Aim;
        /// <summary>0..1.</summary>
        public float Power;
        /// <summary>True for a placed (finesse) shot.</summary>
        public bool Placed;
        public bool FirstTime;
    }

    /// <summary>Optional detail for a pass.</summary>
    public struct PassIntent
    {
        /// <summary>Where the pass should go, in field space; zero = the receiver chosen by gameplay.</summary>
        public Vec2 Target;
        /// <summary>0..1.</summary>
        public float Power;
        /// <summary>A through ball (into space behind the defence).</summary>
        public bool Through;
        public bool OneTouch;
    }

    /// <summary>
    /// What a player "wants to do" this frame. Produced by an <see cref="IIntentSource"/>
    /// (touch, keyboard, gamepad, AI, replay...) and consumed by player systems.
    /// The consumer never knows where the intent came from.
    /// Movement is the core. The optional fields describe an action and its flavour (behaviour, style, dribble/shot/pass details); a plain
    /// movement intent leaves them at their defaults, so every existing producer and consumer keeps working unchanged. There is ONE intent type:
    /// the Creator Engine does not add a second one.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>
        /// Desired movement in FIELD space (X = right, Y = up the screen), length 0..1.
        /// The length is the stick deflection: it selects jog / run / sprint.
        /// </summary>
        public Vec2 Move;

        /// <summary>The signature behaviour behind this intent (empty = none). Set by the AI's decision, never required.</summary>
        public string BehaviorId;
        /// <summary>The football action wanted (None = movement only).</summary>
        public FootballActionKind Action;
        public MovementStyle Style;
        public DribbleIntent Dribble;
        public ShotIntent Shot;
        public PassIntent Pass;

        public static readonly PlayerIntent None = new PlayerIntent();

        public PlayerIntent(Vec2 move)
        {
            float sqr = move.SqrMagnitude;
            Move = sqr > 1f ? move.Normalized : move;
            BehaviorId = null;
            Action = FootballActionKind.None;
            Style = MovementStyle.Default;
            Dribble = default;
            Shot = default;
            Pass = default;
        }

        public float MoveMagnitude => Move.Magnitude;

        public bool HasAction => Action != FootballActionKind.None;

        public PlayerIntent WithAction(FootballActionKind action, MovementStyle style = MovementStyle.Default, string behaviorId = null)
        {
            PlayerIntent c = this;
            c.Action = action;
            c.Style = style;
            c.BehaviorId = behaviorId;
            return c;
        }
    }
}
