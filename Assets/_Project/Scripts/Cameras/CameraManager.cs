using UnityEngine;

namespace FS27.Cameras
{
    /// <summary>
    /// Match camera. Follows a target with smoothing and a little look-ahead, using the active
    /// <see cref="CameraProfile"/>. Switching profile (<see cref="SetProfile"/>) blends smoothly, which is
    /// the hook for the future camera modes (TV Close, Wide, Player, Broadcast, Replay).
    /// It only needs a Transform to follow, so it does not depend on gameplay code.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private CameraProfile profile;

        private Camera cam;
        private bool initialised;

        private Vector3 focus;
        private Vector3 focusVelocity;
        private Vector3 lastTargetPosition;
        private Vector3 smoothedTargetVelocity;

        private float distance, height, fov;
        private float distanceVelocity, heightVelocity, fovVelocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            initialised = false;
        }

        /// <summary>Switch to another camera mode. Values blend over the profile's transition time.</summary>
        public void SetProfile(CameraProfile newProfile)
        {
            profile = newProfile;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null || profile == null) return;

            float dt = Time.deltaTime;
            Vector3 targetPosition = target.position;

            if (!initialised)
            {
                focus = new Vector3(targetPosition.x, 0f, targetPosition.z);
                focusVelocity = Vector3.zero;
                lastTargetPosition = targetPosition;
                smoothedTargetVelocity = Vector3.zero;
                distance = profile.Distance;
                height = profile.Height;
                fov = profile.FieldOfView;
                initialised = true;
            }

            if (dt > 1e-5f)
            {
                Vector3 velocity = (targetPosition - lastTargetPosition) / dt;
                velocity.y = 0f;
                smoothedTargetVelocity = Vector3.Lerp(smoothedTargetVelocity, velocity, 1f - Mathf.Exp(-6f * dt));
            }
            lastTargetPosition = targetPosition;

            Vector3 focusGoal = targetPosition + smoothedTargetVelocity * profile.LookAheadSeconds;
            focusGoal.y = 0f;
            focus = Vector3.SmoothDamp(focus, focusGoal, ref focusVelocity, Mathf.Max(0.0001f, profile.FollowSmoothTime));

            float blend = Mathf.Max(0.0001f, profile.TransitionSmoothTime);
            distance = Mathf.SmoothDamp(distance, profile.Distance, ref distanceVelocity, blend);
            height = Mathf.SmoothDamp(height, profile.Height, ref heightVelocity, blend);
            fov = Mathf.SmoothDamp(fov, profile.FieldOfView, ref fovVelocity, blend);

            Vector3 position = focus + new Vector3(0f, height, -distance);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
            cam.fieldOfView = fov;
        }
    }
}
