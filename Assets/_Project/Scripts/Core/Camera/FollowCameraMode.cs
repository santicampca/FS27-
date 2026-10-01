using System;

namespace FS27.Core
{
    /// <summary>
    /// Smoothed follow camera driven by a <see cref="CameraProfileData"/>: TV, TV Close and Wide are
    /// all this class with different data. Edit the profile at any time: distance, height and FOV blend
    /// toward the new values instead of jumping.
    /// </summary>
    public sealed class FollowCameraMode : ICameraMode
    {
        public CameraModeId Id { get; }
        public CameraProfileData Profile { get; set; }

        private bool initialised;
        private Vec3 focus;
        private Vec3 focusVelocity;
        private float distance, height, fov;
        private float distanceVelocity, heightVelocity, fovVelocity;
        private CameraPose pose;

        public FollowCameraMode(CameraModeId id, CameraProfileData profile)
        {
            Id = id;
            Profile = profile;
        }

        public void Snap(in CameraContext context)
        {
            focus = Anchor(context);
            focusVelocity = Vec3.Zero;
            distance = Profile.Distance;
            height = Profile.Height;
            fov = Profile.FieldOfView;
            distanceVelocity = heightVelocity = fovVelocity = 0f;
            initialised = true;
            pose = BuildPose();
        }

        public CameraPose Evaluate(in CameraContext context, float dt)
        {
            if (!initialised) Snap(context);
            if (dt <= 0f) return pose;

            CameraProfileData p = Profile;
            focus = SmoothMath.SmoothDamp(focus, Anchor(context), ref focusVelocity, p.FollowSmoothTime, dt);

            float blend = Math.Max(0.0001f, p.TransitionSmoothTime);
            distance = SmoothMath.SmoothDamp(distance, p.Distance, ref distanceVelocity, blend, dt);
            height = SmoothMath.SmoothDamp(height, p.Height, ref heightVelocity, blend, dt);
            fov = SmoothMath.SmoothDamp(fov, p.FieldOfView, ref fovVelocity, blend, dt);

            pose = BuildPose();
            return pose;
        }

        /// <summary>Ground point the camera wants to look at.</summary>
        private Vec3 Anchor(in CameraContext c)
        {
            Vec3 anchor = c.TargetPosition + new Vec3(c.TargetVelocity.X, 0f, c.TargetVelocity.Z) * Profile.LookAheadSeconds;
            if (c.HasBall && Profile.BallBias > 0f)
                anchor = Vec3.Lerp(anchor, c.BallPosition, Profile.BallBias);
            anchor.Y = 0f;
            return anchor;
        }

        private CameraPose BuildPose()
        {
            return new CameraPose(focus + new Vec3(0f, height, -distance), focus, fov);
        }
    }
}
