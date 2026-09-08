using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IdleDefenseSurvival.UI.Upgrade
{
    /// <summary>
    /// Tab button for equipment types (Hat, Gloves, Cape, etc.).
    /// </summary>
    public class EquipmentTabButton : MonoBehaviour
    {
        [Header("Tab Configuration")]
        [Tooltip("Equipment type this tab filters to. None = all equipment.")]
        [SerializeField] private EquipmentType _type = EquipmentType.None;

        [Header("References")]
        [Tooltip("UGUI Toggle backing this tab button.")]
        [SerializeField] private Toggle _toggle;
        [Tooltip("Label showing the equipment type name.")]
        [SerializeField] private TextMeshProUGUI _label;
        [Tooltip("Indicator shown while this tab is active.")]
        [SerializeField] private GameObject _activeIndicator;

        public EquipmentType Type => _type;

        private UpgradePanelUI _parentUI;

        /// <summary>
        /// Initializes the tab button, wiring the toggle and setting the label text.
        /// </summary>
        public void Initialize(UpgradePanelUI parentUI)
        {
            _parentUI = parentUI;

            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
                _toggle.onValueChanged.AddListener(OnToggleChanged);
            }

            if (_label != null)
                _label.text = _type.ToString();
        }

        private void OnToggleChanged(bool isOn)
        {
            if (isOn && _parentUI != null)
                _parentUI.SetTab(_type);

            UpdateVisual(isOn);
        }

        /// <summary>
        /// Sets the active visual state without firing the toggle event.
        /// </summary>
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