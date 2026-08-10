using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipComparisonStatUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _currentValueText;
        [SerializeField] private TextMeshProUGUI _newValueText;
        [SerializeField] private TextMeshProUGUI _differenceText;

        public void Initialize(StatComparison comp)
        {
            if (_statNameText != null)
                _statNameText.text = comp.Stat.GetShortName();

            if (_currentValueText != null)
                _currentValueText.text = comp.CurrentValue.ToString("F1");

            if (_newValueText != null)
                _newValueText.text = comp.NewValue.ToString("F1");

            if (_differenceText != null)
            {
                string sign = comp.Difference >= 0 ? "+" : "";
                _differenceText.text = $"{sign}{comp.Difference:F1} ({comp.PercentChange:+0.0;-0.0}%)";
                _differenceText.color = comp.Difference >= 0 ? Color.green : Color.red;
            }
        }
    }
}