using TMPro;
using UnityEngine;

namespace IdleDefenseSurvival.UI.Equipment
{
    public class EquipmentEffectEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectValueText;
        [SerializeField] private TextMeshProUGUI _effectChanceText;

        public void Initialize(string name, float value, float chance)
        {
            if (_effectNameText != null) _effectNameText.text = name;
            if (_effectValueText != null) _effectValueText.text = value.ToString("F1");
            if (_effectChanceText != null)
            {
                if (chance < 100) _effectChanceText.text = $"{chance:F0}% chance";
                else _effectChanceText.text = "";
            }
        }
    }
}