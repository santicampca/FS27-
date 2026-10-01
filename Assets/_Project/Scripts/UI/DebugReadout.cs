using FS27.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FS27.UI
{
    /// <summary>
    /// Development readout (speed, mode, stamina) to judge and tune the feel, also on a phone.
    /// Refreshes a few times per second. Delete the object from the scene to hide it.
    /// </summary>
    public class DebugReadout : MonoBehaviour
    {
        [SerializeField] private PlayerEntity player;
        [SerializeField] private Text label;
        [SerializeField] private float refreshInterval = 0.2f;

        private float nextRefresh;

        private void Update()
        {
            if (player == null || label == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;

            var s = player.State;
            float top = player.Stats.TopSpeed;
            float jogSpeed = top * player.Tuning.JogSpeedFraction;
            string mode = s.IsExhausted ? "EXHAUSTED" : s.IsSprinting ? "SPRINT" : s.Speed > jogSpeed * 1.1f ? "RUN" : s.Speed > 0.3f ? "JOG" : "IDLE";
            label.text = string.Format("{0:0.0} / {1:0.0} m/s  {2}\nstamina {3:0}%", s.Speed, top, mode, s.Stamina01 * 100f);
        }
    }
}
