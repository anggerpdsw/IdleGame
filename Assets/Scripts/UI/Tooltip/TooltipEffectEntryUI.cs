using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipEffectEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectValueText;
        [SerializeField] private TextMeshProUGUI _effectChanceText;

        public void Initialize(SpecialEffectEntry effect)
        {
            if (_effectNameText != null)
                _effectNameText.text = effect.EffectType.GetDisplayName();

            if (_effectValueText != null)
            {
                _effectValueText.text = $"{effect.Value:F1}";
            }

            if (_effectChanceText != null && effect.Chance < 100)
            {
                _effectChanceText.text = $"{effect.Chance:F0}% chance";
            }
            else if (_effectChanceText != null)
            {
                _effectChanceText.text = "";
            }
        }
    }

}