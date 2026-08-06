using TMPro;
using UnityEngine;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// One row in the skills display panel: name, base value, description.
    /// </summary>
    public class SkillRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        public string SkillId { get; private set; }

        public void Initialize(string skillId, string displayName, float baseValue, string description)
        {
            SkillId = skillId;
            if (_nameText != null) _nameText.text = displayName;
            if (_valueText != null) _valueText.text = FormatValue(baseValue);
            if (_descriptionText != null) _descriptionText.text = description;
        }

        /// <summary>
        /// Refresh just the value display (base value may be modified by main stats later).
        /// </summary>
        public void RefreshValue(float value)
        {
            if (_valueText != null) _valueText.text = FormatValue(value);
        }

        private static string FormatValue(float value)
        {
            if (Mathf.Approximately(value, Mathf.Floor(value)))
                return value.ToString("0");
            return value.ToString("F1");
        }
    }
}