using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Visual threat gauge [0-100] using Slider.
    /// Color shifts: Green → Yellow → Orange → Red.
    /// </summary>
    public class EventThreatBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider _slider;

        [Header("Colors")]
        [SerializeField] private Color _colorLow = GameColors.green;
        [SerializeField] private Color _colorMedium = GameColors.yellow;
        [SerializeField] private Color _colorHigh = GameColors.orangered;
        [SerializeField] private Color _colorCritical = GameColors.red;

        public void SetThreat(int current, int max)
        {
            if (_slider == null) return;

            float fillAmount = max > 0 ? (float)current / max : 0f;
            _slider.value = Mathf.Clamp01(fillAmount);

            // Color gradient based on threat level
            Color targetColor;
            if (fillAmount < 0.25f)
                targetColor = Color.Lerp(_colorLow, _colorMedium, fillAmount / 0.25f);
            else if (fillAmount < 0.5f)
                targetColor = Color.Lerp(_colorMedium, _colorHigh, (fillAmount - 0.25f) / 0.25f);
            else if (fillAmount < 0.75f)
                targetColor = Color.Lerp(_colorHigh, _colorCritical, (fillAmount - 0.5f) / 0.25f);
            else
                targetColor = _colorCritical;

            // Apply color to slider fill image
            var fillImage = _slider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = targetColor;
        }
    }
}
