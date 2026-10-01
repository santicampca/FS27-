using UnityEngine;

namespace FS27.Cameras
{
    /// <summary>
    /// Tunable description of one camera "mode". Phase 1 only has the TV profile; TV Close, Wide, etc.
    /// will simply be more profile assets. Edit values live in Play mode: the camera blends toward them.
    /// The camera looks along the pitch from the -Z side (field space: X right, Z up the screen), so the
    /// view angle is atan(Height / Distance).
    /// </summary>
    [CreateAssetMenu(fileName = "CameraProfile", menuName = "FS27/Camera Profile")]
    public class CameraProfile : ScriptableObject
    {
        [Tooltip("Horizontal distance behind the focus point, in metres.")]
        [SerializeField] private float distance = 15f;
        [Tooltip("Height above the ground, in metres.")]
        [SerializeField] private float height = 12f;
        [Tooltip("Vertical field of view, in degrees.")]
        [Range(20f, 90f)] [SerializeField] private float fieldOfView = 40f;
        [Tooltip("Seconds the camera takes to catch up with the player. Smaller = tighter.")]
        [SerializeField] private float followSmoothTime = 0.25f;
        [Tooltip("The camera aims this many seconds ahead of where the player is heading.")]
        [SerializeField] private float lookAheadSeconds = 0.3f;
        [Tooltip("Seconds to blend when this profile (or its values) changes.")]
        [SerializeField] private float transitionSmoothTime = 0.35f;

        public float Distance => distance;
        public float Height => height;
        public float FieldOfView => fieldOfView;
        public float FollowSmoothTime => followSmoothTime;
        public float LookAheadSeconds => lookAheadSeconds;
        public float TransitionSmoothTime => transitionSmoothTime;
    }
}
