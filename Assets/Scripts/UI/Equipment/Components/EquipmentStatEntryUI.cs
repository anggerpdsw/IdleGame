using TMPro;
using UnityEngine;

namespace IdleDefenseSurvival.UI.Equipment
{
    public class EquipmentStatEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;

        public void Initialize(string name, string value, Color color)
        {
            if (_statNameText != null) _statNameText.text = name;
            if (_statValueText != null)
            {
                _statValueText.text = value;
                _statValueText.color = color;
            }
        }
    }
}