using TMPro;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.UI.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IdleDefenseSurvival.UI.Tooltip;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Pure view for one equipment slot (paper-doll).
    /// Only ApplyViewData from the parent UI; never queries any service or database.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler
    {
        [Header("Slot Config")]
        public EquipmentType Slot;

        [Header("Visuals")]
        [Tooltip("Icon of the equipped item.")]
        [SerializeField] private Image _iconImage;
        [Tooltip("Border colored by the equipped item's rarity.")]
        [SerializeField] private Image _rarityBorder;
        [Tooltip("Shown when this slot is not unlocked yet.")]
        [SerializeField] private GameObject _lockedOverlay;
        [Tooltip("Shown when no item is equipped in this slot.")]
        [SerializeField] private GameObject _emptyIndicator;
        [Tooltip("Durability bar of the equipped item.")]
        [SerializeField] private Slider _durabilityBar;
        [Tooltip("Shown when the equipped item has enhancement level > 0.")]
        [SerializeField] private GameObject _enhanceIndicator;
        [SerializeField] private TextMeshProUGUI _enhanceText;
        [Tooltip("Highlight when the equipped item's set bonus is active.")]
        [SerializeField] private GameObject _setBonusGlow;
        [Tooltip("Unlock button shown when the slot is locked and unlockable.")]
        [SerializeField] private Button _unlockButton;
        [Tooltip("Text on the unlock button (cost / requirement).")]
        [SerializeField] private TextMeshProUGUI _unlockButtonText;

        // State
        private EquipmentUI _parentUI;
        private InventoryItem _currentItem;
        private bool _isInitialized;
        private Vector3 _lastPointerPosition;

        public InventoryItem CurrentItem => _currentItem;
        public EquipmentType SlotType => Slot;

        public void Initialize(EquipmentUI parentUI)
        {
            _parentUI = parentUI;
            _isInitialized = true;

            if (_unlockButton != null)
                _unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        private void OnUnlockClicked()
        {
            EquipmentService.Instance?.UnlockSlot(Slot);
        }

        /// <summary>Applies presenter-built view data. Pure view step — no lookups.</summary>
        public void ApplyViewData(EquipmentSlotViewData data)
        {
            if (!_isInitialized || data == null) return;
            if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

            _currentItem = data.ReferenceItem;

            var state = data.State;
            var occupied = state == EquipmentSlotState.Occupied;

            if (_lockedOverlay != null) _lockedOverlay.SetActive(state == EquipmentSlotState.Locked);
            if (_emptyIndicator != null) _emptyIndicator.SetActive(state == EquipmentSlotState.Empty);
            if (_setBonusGlow != null) _setBonusGlow.SetActive(occupied && data.ShowSetBonusGlow);

            // Unlock button (poin 10)
            if (_unlockButton != null)
            {
                bool show = state == EquipmentSlotState.Locked && data.ShowUnlockButton;
                _unlockButton.gameObject.SetActive(show);
                if (_unlockButtonText != null && show) _unlockButtonText.text = data.UnlockLabel;
            }

            // Icon
            if (_iconImage != null)
            {
                _iconImage.sprite = data.Icon;
                _iconImage.enabled = occupied && data.ShowIcon;
            }

            // Rarity border
            if (_rarityBorder != null)
            {
                bool rarity = occupied && data.ShowBorder;
                _rarityBorder.color = rarity ? data.BorderColor : GameColors.white;
            }

            // Durability
            if (_durabilityBar != null)
            {
                bool show = occupied && data.ShowDurability;
                _durabilityBar.gameObject.SetActive(show);
                if (show)
                {
                    _durabilityBar.value = data.Durability;
                    var fill = _durabilityBar.fillRect != null ? _durabilityBar.fillRect.GetComponent<Image>() : null;
                    if (fill != null) fill.color = data.DurabilityColor;
                }
            }

            // Enhance badge
            if (_enhanceIndicator != null)
            {
                bool show = occupied && data.ShowEnhance;
                _enhanceIndicator.SetActive(show);
                if (_enhanceText != null && show) _enhanceText.text = data.EnhanceText;
            }

            gameObject.name = occupied
                ? $"Slot_{Slot}_{_currentItem?.ItemId}"
                : $"Slot_{Slot}_{state}";
        }

        #region Event Handlers
        public void OnPointerClick(PointerEventData eventData)
        {
            _lastPointerPosition = eventData.position;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _parentUI?.OnSlotRightClick(this);
                return;
            }

            // Locked slot: clicking unlocks (uses gold/level/quest gate via service).
            if (!_parentUI.IsSlotLocked(Slot))
            {
                EquipmentService.Instance?.UnlockSlot(Slot);
                return;
            }

            _parentUI?.OnSlotClick(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastPointerPosition = eventData.position;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_parentUI != null) _parentUI._hoveredSlot = this;
            _lastPointerPosition = eventData.position;
            ShowEquipmentInfo(_lastPointerPosition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_parentUI != null) _parentUI._hoveredSlot = null;
            HideEquipmentInfo();
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Fallback: eventData.pointerDrag is the grid slot, not the visual drag
            // component. Resolve from the drag's static current-drag instead.
            var item = eventData.pointerDrag?.GetComponent<InventoryDragItem>()?.Item
                       ?? InventoryDragItem.DraggedItem;

            if (item == null || item.GetEquipmentType() != Slot) return;

            if (EquipmentService.Instance?.CanEquip(item, Slot, out _) == true)
            {
                EquipmentService.Instance.Equip(item, Slot);
            }
        }
        #endregion

        #region Tooltip
        private void ShowEquipmentInfo(Vector3 screenPosition)
        {
            if (_currentItem == null) return;
            var tooltip = TooltipUI.Instance;
            if (tooltip == null) return;
            tooltip.ShowEquipment(_currentItem, screenPosition != Vector3.zero ? screenPosition : _lastPointerPosition);
        }
        private void HideEquipmentInfo() => TooltipUI.Instance?.Hide();
        #endregion
    }
}