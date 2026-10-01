using System;

namespace FS27.Core
{
    /// <summary>
    /// Every number describing one follow-style camera. The Unity CameraProfile asset will hold one of these.
    /// The camera looks along the pitch from the -Z side: view angle = atan(Height / Distance).
    /// </summary>
    [Serializable]
    public class CameraProfileData
    {
        /// <summary>Horizontal distance behind the focus point, metres.</summary>
        public float Distance = 15f;
        /// <summary>Height above the ground, metres.</summary>
        public float Height = 12f;
        /// <summary>Vertical field of view, degrees.</summary>
        public float FieldOfView = 40f;
        /// <summary>Seconds to catch up with the focus point. Smaller = tighter.</summary>
        public float FollowSmoothTime = 0.25f;
        /// <summary>Aim this many seconds ahead of where the target is heading.</summary>
        public float LookAheadSeconds = 0.3f;
        /// <summary>Seconds to blend when the profile values change.</summary>
        public float TransitionSmoothTime = 0.35f;
        /// <summary>0 = follow only the target, 1 = follow the ball. In between frames both.</summary>
        public float BallBias = 0f;

        public static CameraProfileData CreateTv()
        {
            return new CameraProfileData();
        }

        public static CameraProfileData CreateTvClose()
        {
            return new CameraProfileData { Distance = 10f, Height = 8f, FollowSmoothTime = 0.2f, LookAheadSeconds = 0.25f };
        }

        public static CameraProfileData CreateWide()
        {
            return new CameraProfileData { Distance = 22f, Height = 18f, FieldOfView = 45f, FollowSmoothTime = 0.35f, LookAheadSeconds = 0.2f, BallBias = 0.3f };
        }
    }
}
