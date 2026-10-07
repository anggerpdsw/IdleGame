using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Visual threat gauge [0-100].
    /// Color shifts: Green → Yellow → Orange → Red.
    /// </summary>
    public class EventThreatBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _backgroundImage;

        [Header("Colors")]
        [SerializeField] private Color _colorLow = Color.green;
        [SerializeField] private Color _colorMedium = Color.yellow;
        [SerializeField] private Color _colorHigh = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color _colorCritical = Color.red;

        public void SetThreat(int current, int max)
        {
            if (_fillImage == null) return;

            float fillAmount = max > 0 ? (float)current / max : 0f;
            _fillImage.fillAmount = Mathf.Clamp01(fillAmount);

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

            _fillImage.color = targetColor;
        }
    }
}
