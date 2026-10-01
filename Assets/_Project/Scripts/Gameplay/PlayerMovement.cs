using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Moves the player's kinematic body. All the "feel" lives in Core (PlayerLocomotion + tuning);
    /// this component only feeds it the intent and applies the resulting velocity/heading to Unity.
    /// The visual model is a separate child, so it can be replaced without touching this.
    /// Runs on the physics step so the ball (a Rigidbody) reacts consistently.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerEntity), typeof(PlayerIntentProvider), typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private FieldBounds bounds;
        [Tooltip("Distance kept from the field edge (roughly the body radius).")]
        [SerializeField] private float edgeMargin = 0.4f;

        private PlayerEntity entity;
        private PlayerIntentProvider intentProvider;
        private Rigidbody body;

        private void Awake()
        {
            entity = GetComponent<PlayerEntity>();
            intentProvider = GetComponent<PlayerIntentProvider>();
            body = GetComponent<Rigidbody>();

            // Players are driven by code, not by forces. A kinematic body still pushes the ball physically.
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            PlayerRuntimeState state = entity.State;

            entity.RefreshStats();
            PlayerLocomotion.Step(state, entity.Stats, entity.Tuning, intentProvider.Read(), dt);

            Vec2 v = state.Velocity;
            Vector3 current = body.position;
            Vector3 next = current + new Vector3(v.X, 0f, v.Y) * dt;

            if (bounds != null)
            {
                next = bounds.Clamp(next, edgeMargin);
                // If the edge cut the move short, lose that speed (no running in place against the wall).
                float moved = new Vector2(next.x - current.x, next.z - current.z).magnitude;
                float wanted = state.Speed * dt;
                if (wanted > 1e-5f && moved < wanted) state.Speed *= moved / wanted;
            }

            body.MovePosition(next);
            body.MoveRotation(Quaternion.Euler(0f, state.Heading * MathUtil.Rad2Deg, 0f));
        }
    }
}
