using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// First, very simple dribble: while the player runs, if the ball is near the feet and in front,
    /// the player gives it small taps that push it ahead of them. The ball is never attached to the
    /// player: it stays a free physics object that can be chased, lost, or bounced off the body.
    /// Passing/shooting will be built on top of BallController later, not here.
    /// </summary>
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(PlayerEntity), typeof(Rigidbody))]
    public class BallInteractor : MonoBehaviour
    {
        [SerializeField] private BallController ball;

        [Header("Touch zone")]
        [Tooltip("Distance of the 'foot point' in front of the player.")]
        [SerializeField] private float footOffset = 0.55f;
        [Tooltip("How close the ball must be to the foot point to be touched.")]
        [SerializeField] private float touchRadius = 0.7f;
        [Tooltip("Ball must be at least this 'in front' (dot product with heading).")]
        [Range(-1f, 1f)] [SerializeField] private float minFrontDot = 0.2f;
        [SerializeField] private float maxBallHeight = 0.6f;

        [Header("Touch strength")]
        [Tooltip("Player must be at least this fast (m/s) to push the ball.")]
        [SerializeField] private float minSpeed = 0.8f;
        [SerializeField] private float touchInterval = 0.12f;
        [Tooltip("Extra ball speed over the player's speed when walking.")]
        [SerializeField] private float leadAtWalk = 0.6f;
        [Tooltip("Extra ball speed over the player's speed at full sprint (bigger = ball runs further ahead).")]
        [SerializeField] private float leadAtSprint = 3.0f;
        [Range(0f, 1f)] [SerializeField] private float touchStrength = 0.8f;

        private PlayerEntity entity;
        private Rigidbody body;
        private float nextTouchTime;

        private void Awake()
        {
            entity = GetComponent<PlayerEntity>();
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (ball == null || Time.time < nextTouchTime) return;

            PlayerRuntimeState state = entity.State;
            if (state.Speed < minSpeed) return;

            Vec2 dir2 = Vec2.FromHeading(state.Heading);
            Vector3 forward = new Vector3(dir2.X, 0f, dir2.Y);
            Vector3 origin = body.position;
            Vector3 foot = origin + forward * footOffset;

            Vector3 ballPos = ball.Position;
            if (ballPos.y - ball.Radius > maxBallHeight) return;

            Vector3 toBall = ballPos - foot;
            toBall.y = 0f;
            if (toBall.sqrMagnitude > touchRadius * touchRadius) return;

            Vector3 fromPlayer = ballPos - origin;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > 1e-6f && Vector3.Dot(forward, fromPlayer.normalized) < minFrontDot) return;

            float speed01 = entity.Stats.TopSpeed > 0f ? Mathf.Clamp01(state.Speed / entity.Stats.TopSpeed) : 0f;
            float lead = Mathf.Lerp(leadAtWalk, leadAtSprint, speed01);
            ball.ApplyTouch(forward * (state.Speed + lead), touchStrength);
            nextTouchTime = Time.time + touchInterval;
        }

        private void OnDrawGizmosSelected()
        {
            if (entity == null) entity = GetComponent<PlayerEntity>();
            Vector3 forward = entity != null ? transform.forward : Vector3.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + forward * footOffset, touchRadius);
        }
    }
}
