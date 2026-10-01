namespace FS27.Core
{
    /// <summary>Anything that can drive a player: touch joystick, keyboard, gamepad, AI...</summary>
    public interface IIntentSource
    {
        PlayerIntent ReadIntent();
    }
}
