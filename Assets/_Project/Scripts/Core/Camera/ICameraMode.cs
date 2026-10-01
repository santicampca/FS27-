namespace FS27.Core
{
    /// <summary>
    /// One way of framing the match (TV, close, wide, player, broadcast, replay...). A mode is a small
    /// stateful object: it turns a <see cref="CameraContext"/> into a <see cref="CameraPose"/>.
    /// It knows nothing about Unity, transitions or other modes: the <see cref="CameraDirector"/> handles that.
    /// </summary>
    public interface ICameraMode
    {
        CameraModeId Id { get; }

        /// <summary>Jump straight to the framing for this context (no smoothing). Used at start and on mode switches.</summary>
        void Snap(in CameraContext context);

        /// <summary>Advance by dt and return the pose for this frame.</summary>
        CameraPose Evaluate(in CameraContext context, float dt);
    }
}
