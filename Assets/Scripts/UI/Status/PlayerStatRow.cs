using TMPro;
using UnityEngine;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// A single row in the Player Status panel.
    /// Displays a stat name and its formatted value.
    /// </summary>
    public class PlayerStatRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;

        /// <summary>
        /// Update the displayed stat name and value.
        /// </summary>
        public void SetValue(string statName, float value)
        {
            if (value < 0.5f) this.gameObject.SetActive(false);
            if (_statNameText != null) _statNameText.text = statName;
            if (_statValueText != null) _statValueText.text = FormatValue(value);
        }

        private static string FormatValue(float value)
        {
            // Percentage-based stats display as whole numbers
            if (Mathf.Approximately(value, Mathf.Floor(value)))
                return value.ToString("0");

            return value.ToString("F1");
        }
    }
}
