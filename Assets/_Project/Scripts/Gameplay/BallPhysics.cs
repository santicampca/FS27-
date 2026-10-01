using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Extra forces applied to the ball each physics step: quadratic air drag and rolling resistance.
    /// Kept separate from BallController so the physics model can be improved (Magnus/spin, wind,
    /// different surfaces) without touching anything else.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(BallController))]
    public class BallPhysics : MonoBehaviour
    {
        [Tooltip("Air drag: deceleration = coefficient * speed^2. 0 = no air drag.")]
        [SerializeField] private float airDragCoefficient = 0.015f;
        [Tooltip("Constant deceleration (m/s^2) while rolling on the ground. Higher = the ball stops sooner.")]
        [SerializeField] private float rollingDeceleration = 1.6f;
        [Tooltip("Below this horizontal speed (m/s) on the ground the ball comes to rest.")]
        [SerializeField] private float restSpeed = 0.12f;
        [SerializeField] private float groundCheckDistance = 0.06f;

        private BallController ball;
        private Rigidbody body;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            ball = GetComponent<BallController>();
            body = ball.Body != null ? ball.Body : GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Vector3 v = body.linearVelocity;

            // Raycasts that start inside the ball's own collider do not hit it, so this only sees what is below.
            IsGrounded = Physics.Raycast(body.position, Vector3.down, ball.Radius + groundCheckDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && v.y < 1.0f;

            float speed = v.magnitude;
            if (speed > 1e-4f && airDragCoefficient > 0f)
                body.AddForce(-(v / speed) * (airDragCoefficient * speed * speed), ForceMode.Acceleration);

            if (!IsGrounded) return;

            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float flatSpeed = flat.magnitude;
            if (flatSpeed <= restSpeed)
            {
                body.linearVelocity = new Vector3(0f, v.y, 0f);
                body.angularVelocity = Vector3.zero;
            }
            else if (rollingDeceleration > 0f)
            {
                float dv = Mathf.Min(rollingDeceleration * Time.fixedDeltaTime, flatSpeed);
                body.AddForce(-(flat / flatSpeed) * (dv / Time.fixedDeltaTime), ForceMode.Acceleration);
            }
        }
    }
}
