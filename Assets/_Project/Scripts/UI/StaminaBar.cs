using FS27.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FS27.UI
{
    /// <summary>
    /// Simple stamina bar. The fill is a child RectTransform (pivot on the left) scaled horizontally,
    /// so it needs no sprite and allocates nothing. Placeholder look: the final design comes later.
    /// </summary>
    public class StaminaBar : MonoBehaviour
    {
        [SerializeField] private PlayerEntity player;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Color fullColor = new Color(0.25f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color lowColor = new Color(0.95f, 0.75f, 0.2f, 1f);
        [SerializeField] private Color exhaustedColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        private float shown = -1f;

        private void Update()
        {
            if (player == null || fill == null) return;

            float v = player.State.Stamina01;
            if (!Mathf.Approximately(v, shown))
            {
                shown = v;
                fill.localScale = new Vector3(v, 1f, 1f);
            }

            if (fillImage != null)
                fillImage.color = player.State.IsExhausted ? exhaustedColor : Color.Lerp(lowColor, fullColor, v * 2f - 0.5f);
        }
    }
}
