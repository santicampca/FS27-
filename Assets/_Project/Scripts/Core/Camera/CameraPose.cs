namespace FS27.Core
{
    /// <summary>Where the camera is, what it looks at and its vertical field of view (degrees).</summary>
    public struct CameraPose
    {
        public Vec3 Position;
        public Vec3 LookAt;
        public float FieldOfView;

        public CameraPose(Vec3 position, Vec3 lookAt, float fieldOfView)
        {
            Position = position;
            LookAt = lookAt;
            FieldOfView = fieldOfView;
        }

        public static CameraPose Lerp(CameraPose a, CameraPose b, float t)
        {
            return new CameraPose(
                Vec3.Lerp(a.Position, b.Position, t),
                Vec3.Lerp(a.LookAt, b.LookAt, t),
                MathUtil.Lerp(a.FieldOfView, b.FieldOfView, t));
        }
    }
}
