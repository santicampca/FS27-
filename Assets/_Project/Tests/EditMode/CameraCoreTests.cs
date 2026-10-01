using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class CameraCoreTests
    {
        private const float Dt = 1f / 60f;

        private static CameraContext Ctx(float x, float z, float vx = 0f, float vz = 0f)
        {
            return new CameraContext { TargetPosition = new Vec3(x, 1f, z), TargetVelocity = new Vec3(vx, 0f, vz) };
        }

        private static CameraPose Run(ICameraMode mode, CameraContext ctx, float seconds)
        {
            CameraPose pose = default;
            int steps = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < steps; i++) pose = mode.Evaluate(ctx, Dt);
            return pose;
        }

        // ---------- SmoothMath ----------

        [Test]
        public void SmoothDamp_ConvergesWithoutOvershoot()
        {
            float x = 0f, v = 0f;
            for (int i = 0; i < 300; i++)
            {
                x = SmoothMath.SmoothDamp(x, 10f, ref v, 0.25f, Dt);
                Assert.LessOrEqual(x, 10f + 1e-4f, "never passes the target");
            }
            Assert.AreEqual(10f, x, 0.01f);
        }

        [Test]
        public void SmoothDamp_StableWithHugeDt()
        {
            float v = 0f;
            float x = SmoothMath.SmoothDamp(0f, 5f, ref v, 0.25f, 5f);
            Assert.AreEqual(5f, x, 0.05f);
            Assert.IsFalse(float.IsNaN(x));
        }

        // ---------- Follow mode ----------

        [Test]
        public void Follow_SettlesOnTheProfileOffset()
        {
            var p = CameraProfileData.CreateTv();
            var cam = new FollowCameraMode(CameraModeId.Tv, p);
            CameraPose pose = Run(cam, Ctx(4f, 2f), 3f);
            Assert.AreEqual(4f, pose.LookAt.X, 0.01f);
            Assert.AreEqual(2f, pose.LookAt.Z, 0.01f);
            Assert.AreEqual(p.Height, pose.Position.Y, 0.01f);
            Assert.AreEqual(2f - p.Distance, pose.Position.Z, 0.01f);
            Assert.AreEqual(p.FieldOfView, pose.FieldOfView, 0.01f);
        }

        [Test]
        public void Follow_FirstFrame_SnapsToTarget_NoSwoopFromOrigin()
        {
            var cam = new FollowCameraMode(CameraModeId.Tv, CameraProfileData.CreateTv());
            CameraPose pose = cam.Evaluate(Ctx(30f, -8f), Dt);
            Assert.AreEqual(30f, pose.LookAt.X, 0.01f);
            Assert.AreEqual(-8f, pose.LookAt.Z, 0.01f);
        }

        [Test]
        public void Follow_Lags_ThenCatchesUp_WithoutOvershoot()
        {
            var p = CameraProfileData.CreateTv();
            p.LookAheadSeconds = 0f;
            var cam = new FollowCameraMode(CameraModeId.Tv, p);
            cam.Snap(Ctx(0f, 0f));

            CameraPose first = cam.Evaluate(Ctx(10f, 0f), Dt);
            Assert.Greater(first.LookAt.X, 0f);
            Assert.Less(first.LookAt.X, 1f, "smoothed: does not jump to the target");

            for (int i = 0; i < 300; i++)
            {
                CameraPose pose = cam.Evaluate(Ctx(10f, 0f), Dt);
                Assert.LessOrEqual(pose.LookAt.X, 10f + 1e-3f);
            }
        }

        [Test]
        public void Follow_LooksAheadOfAMovingTarget()
        {
            var cam = new FollowCameraMode(CameraModeId.Tv, CameraProfileData.CreateTv());
            CameraPose pose = Run(cam, Ctx(0f, 0f, 0f, 8f), 3f);
            Assert.Greater(pose.LookAt.Z, 1.5f);
        }

        [Test]
        public void Follow_BallBias_PullsTheFramingTowardTheBall()
        {
            var p = CameraProfileData.CreateWide();
            p.LookAheadSeconds = 0f;
            var cam = new FollowCameraMode(CameraModeId.Wide, p);
            var ctx = Ctx(0f, 0f);
            ctx.HasBall = true;
            ctx.BallPosition = new Vec3(10f, 0.2f, 0f);
            CameraPose pose = Run(cam, ctx, 4f);
            Assert.AreEqual(p.BallBias * 10f, pose.LookAt.X, 0.05f);
        }

        [Test]
        public void Follow_ProfileEdits_BlendInsteadOfJumping()
        {
            var p = CameraProfileData.CreateTv();
            var cam = new FollowCameraMode(CameraModeId.Tv, p);
            CameraPose before = Run(cam, Ctx(0f, 0f), 1f);

            p.Distance = 25f;
            CameraPose next = cam.Evaluate(Ctx(0f, 0f), Dt);
            Assert.Less(System.Math.Abs(next.Position.Z - before.Position.Z), 1f, "no jump on the first frame");

            CameraPose settled = Run(cam, Ctx(0f, 0f), 3f);
            Assert.AreEqual(-25f, settled.Position.Z, 0.05f);
        }

        [Test]
        public void Follow_ZeroDt_ReturnsLastPose_NoNaN()
        {
            var cam = new FollowCameraMode(CameraModeId.Tv, CameraProfileData.CreateTv());
            CameraPose a = Run(cam, Ctx(3f, 3f), 1f);
            CameraPose b = cam.Evaluate(Ctx(50f, 50f), 0f);
            Assert.AreEqual(a.Position.X, b.Position.X);
            Assert.IsFalse(float.IsNaN(b.Position.Z));
        }

        [Test]
        public void Presets_AreOrdered_Close_Tv_Wide()
        {
            Assert.Less(CameraProfileData.CreateTvClose().Distance, CameraProfileData.CreateTv().Distance);
            Assert.Less(CameraProfileData.CreateTv().Distance, CameraProfileData.CreateWide().Distance);
        }

        // ---------- Director ----------

        [Test]
        public void Director_StartsOnTheFirstRegisteredMode()
        {
            var d = CameraDirector.CreateDefault();
            Assert.AreEqual(CameraModeId.Tv, d.ActiveMode);
            Assert.IsTrue(d.IsRegistered(CameraModeId.Wide));
            Assert.IsFalse(d.IsRegistered(CameraModeId.Replay));
        }

        [Test]
        public void Director_RejectsUnregisteredModes_WithoutChangingState()
        {
            var d = CameraDirector.CreateDefault();
            Assert.IsFalse(d.SetMode(CameraModeId.Replay, 0.5f));
            Assert.AreEqual(CameraModeId.Tv, d.ActiveMode);
        }

        [Test]
        public void Director_InstantSwitch_UsesTheNewModeAtOnce()
        {
            var d = CameraDirector.CreateDefault();
            var ctx = Ctx(0f, 0f);
            for (int i = 0; i < 60; i++) d.Evaluate(ctx, Dt);

            Assert.IsTrue(d.SetMode(CameraModeId.TvClose, 0f));
            CameraPose pose = d.Evaluate(ctx, Dt);
            Assert.AreEqual(CameraProfileData.CreateTvClose().Height, pose.Position.Y, 0.2f);
            Assert.IsFalse(d.IsTransitioning);
        }

        [Test]
        public void Director_Transition_BlendsBetweenModes_ThenEndsOnTheNewOne()
        {
            var d = CameraDirector.CreateDefault();
            var ctx = Ctx(0f, 0f);
            for (int i = 0; i < 120; i++) d.Evaluate(ctx, Dt);

            float tvH = CameraProfileData.CreateTv().Height;
            float wideH = CameraProfileData.CreateWide().Height;

            d.SetMode(CameraModeId.Wide, 1f);
            Assert.IsTrue(d.IsTransitioning);

            CameraPose start = d.Evaluate(ctx, Dt);
            Assert.AreEqual(tvH, start.Position.Y, 0.3f, "starts near the old framing");

            CameraPose mid = default;
            for (int i = 0; i < 30; i++) mid = d.Evaluate(ctx, Dt);
            Assert.Greater(mid.Position.Y, tvH + 0.5f);
            Assert.Less(mid.Position.Y, wideH - 0.5f);

            CameraPose end = default;
            for (int i = 0; i < 240; i++) end = d.Evaluate(ctx, Dt);
            Assert.IsFalse(d.IsTransitioning);
            Assert.AreEqual(wideH, end.Position.Y, 0.1f);
        }

        [Test]
        public void Director_SwitchingAfterALongTime_DoesNotStartFromStaleState()
        {
            var d = CameraDirector.CreateDefault();
            for (int i = 0; i < 60; i++) d.Evaluate(Ctx(0f, 0f), Dt);
            for (int i = 0; i < 600; i++) d.Evaluate(Ctx(35f, 10f), Dt); // Wide never ran meanwhile

            d.SetMode(CameraModeId.Wide, 0f);
            CameraPose pose = d.Evaluate(Ctx(35f, 10f), Dt);
            Assert.AreEqual(35f, pose.LookAt.X, 0.3f);
        }

        [Test]
        public void Director_SettingTheSameMode_IsANoOp()
        {
            var d = CameraDirector.CreateDefault();
            d.Evaluate(Ctx(0f, 0f), Dt);
            Assert.IsTrue(d.SetMode(CameraModeId.Tv, 1f));
            Assert.IsFalse(d.IsTransitioning);
        }

        [Test]
        public void Director_CustomMode_CanBePluggedIn()
        {
            var d = CameraDirector.CreateDefault();
            d.Register(new FixedCameraMode());
            d.SetMode(CameraModeId.Broadcast, 0f);
            CameraPose pose = d.Evaluate(Ctx(5f, 5f), Dt);
            Assert.AreEqual(100f, pose.Position.X);
            Assert.AreEqual(5f, pose.LookAt.X, 1e-4f);
        }

        private class FixedCameraMode : ICameraMode
        {
            public CameraModeId Id => CameraModeId.Broadcast;
            public void Snap(in CameraContext context) { }
            public CameraPose Evaluate(in CameraContext context, float dt)
            {
                return new CameraPose(new Vec3(100f, 20f, 0f), context.TargetPosition, 30f);
            }
        }
    }
}
