using UnityEngine;

namespace FS27.Infrastructure
{
    /// <summary>
    /// Applies the runtime settings the whole game relies on (frame rate, physics step, screen).
    /// Put one in every scene that needs them; later it will also create global services (save, audio...).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        [Tooltip("Physics runs at this rate. 60 Hz matches the frame rate target and keeps the ball smooth.")]
        [SerializeField] private float physicsHz = 60f;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Time.fixedDeltaTime = 1f / physicsHz;
            Physics.bounceThreshold = 0.5f;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
