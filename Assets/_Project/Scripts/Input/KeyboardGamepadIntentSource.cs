using FS27.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FS27.Input
{
    /// <summary>
    /// Development / desktop input: WASD or arrow keys, plus gamepad left stick, turned into the same
    /// <see cref="PlayerIntent"/> the touch joystick produces.
    ///
    /// A gamepad stick is analogue, so it gives the full jog -> run -> sprint range like the touch joystick.
    /// Keys are digital, so they emulate stick deflection instead:
    ///   hold a direction = run, +Space = jog/walk, +Shift = sprint (ramps up smoothly, not instantly).
    /// </summary>
    public class KeyboardGamepadIntentSource : MonoBehaviour, IIntentSource
    {
        [Header("Keyboard stick emulation (0..1 deflection)")]
        [SerializeField] private float jogDeflection = 0.22f;
        [SerializeField] private float runDeflection = 0.60f;
        [SerializeField] private float sprintDeflection = 1.0f;
        [Tooltip("How fast the emulated stick moves toward its target, in deflection per second.")]
        [SerializeField] private float rampPerSecond = 1.5f;

        private PlayerIntent current;
        private float keyDeflection;

        public PlayerIntent ReadIntent()
        {
            return current;
        }

        private void OnDisable()
        {
            current = PlayerIntent.None;
            keyDeflection = 0f;
        }

        private void Update()
        {
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.0025f)
                {
                    current = new PlayerIntent(new Vec2(stick.x, stick.y));
                    keyDeflection = 0f;
                    return;
                }
            }

            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                current = PlayerIntent.None;
                return;
            }

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

            if (x == 0f && y == 0f)
            {
                keyDeflection = 0f;
                current = PlayerIntent.None;
                return;
            }

            float target = runDeflection;
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) target = sprintDeflection;
            else if (kb.spaceKey.isPressed) target = jogDeflection;

            keyDeflection = keyDeflection <= 0f
                ? Mathf.Min(target, runDeflection)
                : Mathf.MoveTowards(keyDeflection, target, rampPerSecond * Time.deltaTime);

            Vec2 dir = new Vec2(x, y).Normalized;
            current = new PlayerIntent(dir * keyDeflection);
        }
    }
}
