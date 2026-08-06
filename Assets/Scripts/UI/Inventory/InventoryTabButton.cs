using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleDefenseSurvival.UI.Inventory.InventoryUI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Tab button for inventory categories.
    /// </summary>
    public class InventoryTabButton : MonoBehaviour
    {
        [SerializeField] private TabType _type;
        [SerializeField] private Toggle _toggle;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private GameObject _activeIndicator;

        public TabType Type => _type;
        public Toggle Toggle => _toggle;

        private InventoryUI _parentUI;

        public void Initialize(InventoryUI parentUI)
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