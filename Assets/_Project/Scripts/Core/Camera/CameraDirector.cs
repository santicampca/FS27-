using System;

namespace FS27.Core
{
    /// <summary>
    /// Owns the available camera modes, tracks which one is active and cross-fades between them.
    /// Pure logic: the Unity camera just asks for the pose each frame and applies it.
    /// To add a mode, implement <see cref="ICameraMode"/> and <see cref="Register"/> it.
    /// </summary>
    public sealed class CameraDirector
    {
        private readonly ICameraMode[] modes = new ICameraMode[(int)CameraModeId.Replay + 1];

        private CameraModeId active;
        private CameraModeId previous;
        private bool hasActive;
        private bool blending;
        private float blendElapsed;
        private float blendDuration;

        private CameraContext lastContext;
        private bool hasContext;
        private CameraPose lastPose;

        public CameraModeId ActiveMode => active;
        public bool IsTransitioning => blending;

        public void Register(ICameraMode mode)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            modes[(int)mode.Id] = mode;
            if (!hasActive)
            {
                active = mode.Id;
                hasActive = true;
            }
        }

        public bool IsRegistered(CameraModeId id)
        {
            return modes[(int)id] != null;
        }

        /// <summary>
        /// Switches to another mode, cross-fading over <paramref name="transitionSeconds"/> (0 = instant).
        /// Returns false if that mode is not registered (nothing changes).
        /// </summary>
        public bool SetMode(CameraModeId id, float transitionSeconds)
        {
            if (!IsRegistered(id)) return false;
            if (hasActive && id == active) return true;

            ICameraMode next = modes[(int)id];
            if (hasContext) next.Snap(lastContext); // never start from stale state

            previous = active;
            active = id;
            hasActive = true;
            blendElapsed = 0f;
            blendDuration = Math.Max(0f, transitionSeconds);
            blending = blendDuration > 0f && hasContext && IsRegistered(previous) && previous != active;
            return true;
        }

        public CameraPose Evaluate(in CameraContext context, float dt)
        {
            lastContext = context;
            hasContext = true;

            ICameraMode current = modes[(int)active];
            if (!hasActive || current == null) return lastPose;

            CameraPose pose = current.Evaluate(context, dt);

            if (blending)
            {
                ICameraMode old = modes[(int)previous];
                CameraPose oldPose = old.Evaluate(context, dt);
                blendElapsed += Math.Max(0f, dt);
                float t = blendDuration > 0f ? blendElapsed / blendDuration : 1f;
                if (t >= 1f) blending = false;
                pose = CameraPose.Lerp(oldPose, pose, SmoothMath.SmoothStep(t));
            }

            lastPose = pose;
            return pose;
        }

        /// <summary>Convenience: a director with Tv, TvClose and Wide already registered, starting on Tv.</summary>
        public static CameraDirector CreateDefault()
        {
            var d = new CameraDirector();
            d.Register(new FollowCameraMode(CameraModeId.Tv, CameraProfileData.CreateTv()));
            d.Register(new FollowCameraMode(CameraModeId.TvClose, CameraProfileData.CreateTvClose()));
            d.Register(new FollowCameraMode(CameraModeId.Wide, CameraProfileData.CreateWide()));
            return d;
        }
    }
}
