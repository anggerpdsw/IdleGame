using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipStatEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;
        [SerializeField] private TextMeshProUGUI _comparisonText;

        public void Initialize(SecondaryStat stat, float value, float comparisonValue = 0)
            => Initialize(stat.GetSkillDisplayName(), value, comparisonValue, stat.IsPercentage());

        public void Initialize(string statName, float value, float comparisonValue = 0, bool isPercent = false)
        {
            if (_statNameText != null) _statNameText.text = statName;

            if (_statValueText != null)
            {
                string sign = value >= 0  && !isPercent ? "+" : "";
                float displayValue = isPercent ? value * 100f : value;
                string suffix = isPercent ? "%" : "";
                _statValueText.text = $"{sign}{displayValue:F1}{suffix}";
                _statValueText.color = value >= 0 ? Color.green : Color.red;
            }

            if (_comparisonText != null && comparisonValue != 0)
            {
                float diff = comparisonValue - value;
                float displayDiff = isPercent ? diff * 100f : diff;
                string sign = displayDiff >= 0 && !isPercent ? "+" : "";
                string suffix = isPercent ? "%" : "";
                _comparisonText.text = $"({sign}{displayDiff:F1}{suffix})";
                _comparisonText.color = diff >= 0 ? Color.green : Color.red;
            }
            else if (_comparisonText != null)
            {
                _comparisonText.text = "";
            }
        }
    }

}