using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Tab button for equipment types (Hat, Gloves, Cape, etc.).
    /// </summary>
    public class EquipmentTabButton : MonoBehaviour
    {
        [SerializeField] private EquipmentType _type;
        [SerializeField] private Toggle _toggle;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private GameObject _activeIndicator;

        public EquipmentType Type => _type;
        public Toggle Toggle => _toggle;

        private UpgradeUI _parentUI;

        public void Initialize(UpgradeUI parentUI)
        {
            _parentUI = parentUI;
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleChanged);
            }
            if (_label != null)
                _label.text = _type.ToString();
        }

        private void OnToggleChanged(bool isOn)
        {
            if (isOn && _parentUI != null)
            {
                _parentUI.SetTab(_type);
            }
            UpdateVisual(isOn);
        }

        public void SetActive(bool active)
        {
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(active);
            UpdateVisual(active);
        }

        private void UpdateVisual(bool active)
        {
            if (_activeIndicator != null)
                _activeIndicator.SetActive(active);
        }
    }
}