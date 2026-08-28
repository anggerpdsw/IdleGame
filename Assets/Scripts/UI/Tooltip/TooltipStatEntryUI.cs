using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipStatEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;
        [SerializeField] private TextMeshProUGUI _comparisonText;

        public void Initialize(SecondaryStat stat, float value, float comparisonValue = 0)
            => Initialize(stat.GetSkillDisplayName(), value, comparisonValue);

        public void Initialize(string statName, float value, float comparisonValue = 0)
        {
            if (_statNameText != null)
                _statNameText.text = statName;

            if (_statValueText != null)
            {
                string sign = value >= 0 ? "+" : "";
                _statValueText.text = $"{sign}{value:F1}";
                _statValueText.color = value >= 0 ? Color.green : Color.red;
            }

            if (_comparisonText != null && comparisonValue != 0)
            {
                float diff = comparisonValue - value;
                string sign = diff >= 0 ? "+" : "";
                _comparisonText.text = $"({sign}{diff:F1})";
                _comparisonText.color = diff >= 0 ? Color.green : Color.red;
            }
            else if (_comparisonText != null)
            {
                _comparisonText.text = "";
            }
        }
    }

}