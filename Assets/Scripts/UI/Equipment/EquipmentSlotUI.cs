using TMPro;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.UI.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Individual equipment slot UI.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler
    {
        [Header("Slot Config")]
        public EquipmentType Slot;

        [Header("Visuals")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _emptyIndicator;
        [SerializeField] private Slider _durabilityBar;
        [SerializeField] private GameObject _enhanceIndicator;
        [SerializeField] private TextMeshProUGUI _enhanceText;
        [SerializeField] private GameObject _setBonusGlow;

        // State
        private EquipmentUI _parentUI;
        private InventoryItem _currentItem;

        public InventoryItem CurrentItem => _currentItem;

        public void Initialize(EquipmentUI parentUI)
        {
            _parentUI = parentUI;
            Refresh();
        }

        public void Refresh()
        {
            var equipment = EquipmentService.Instance;
            if (equipment == null) return;

            _currentItem = equipment.EquippedItems.GetValueOrDefault(Slot);

            var slotData = equipment.SlotData.FirstOrDefault(s => s.Slot == Slot);

            // Locked overlay
            bool isLocked = slotData != null && !slotData.IsUnlocked;
            if (_lockedOverlay != null) _lockedOverlay.SetActive(isLocked);

            if (_currentItem == null)
            {
                // Empty slot
                if (_iconImage != null) { _iconImage.sprite = null; _iconImage.enabled = false; }
                if (_rarityBorder != null) _rarityBorder.enabled = false;
                if (_emptyIndicator != null) _emptyIndicator.SetActive(true);
                if (_durabilityBar != null) _durabilityBar.gameObject.SetActive(false);
                if (_enhanceIndicator != null) _enhanceIndicator.SetActive(false);
                if (_setBonusGlow != null) _setBonusGlow.SetActive(false);
                return;
            }

            // Has item
            if (_emptyIndicator != null) _emptyIndicator.SetActive(false);

            var itemData = ItemDatabase.Instance?.GetItem(_currentItem.ItemId);

            // Icon
            if (_iconImage != null && itemData?.Icon != null)
            {
                _iconImage.sprite = itemData.Icon;
                _iconImage.enabled = true;
            }

            // ItemRarity border
            if (_rarityBorder != null && itemData?.ItemRarity != ItemRarity.None)
            {
                _rarityBorder.color = itemData.ItemRarity.GetDefaultColor();
                _rarityBorder.enabled = true;
            }

            // Durability
            if (_durabilityBar != null)
            {
                _durabilityBar.gameObject.SetActive(true);
                float p = _currentItem.GetDurabilityPercent();
                _durabilityBar.value = p;
                var fill = _durabilityBar.fillRect != null ? _durabilityBar.fillRect.GetComponent<Image>() : null;
                if (fill != null) fill.color = _currentItem.GetDurabilityColor();
            }

            // Enhance level
            if (_enhanceIndicator != null && _currentItem.EnhanceLevel > 0)
            {
                _enhanceIndicator.SetActive(true);
                if (_enhanceText != null) _enhanceText.text = $"+{_currentItem.EnhanceLevel}";
            }
            else if (_enhanceIndicator != null)
            {
                _enhanceIndicator.SetActive(false);
            }

            // Set bonus glow
            if (_setBonusGlow != null)
            {
                string setId = _currentItem.GetSetId();
                bool hasActiveSetBonus = !string.IsNullOrEmpty(setId) && EquipmentService.Instance?.IsSetBonusActive(setId, 0) == true;
                _setBonusGlow.SetActive(hasActiveSetBonus);
            }

            gameObject.name = $"EquipSlot_{Slot}_{_currentItem.ItemId}";
        }

        #region Event Handlers
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _parentUI?.OnSlotRightClick(this);
            }
            else
            {
                _parentUI?.OnSlotClick(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_parentUI == null)
            {
                _parentUI._hoveredSlot = this;
            }
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_parentUI == null)
            {
                _parentUI._hoveredSlot = null;
            }
            HideTooltip();
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Handle drag-drop from inventory
            var dragItem = eventData.pointerDrag?.GetComponent<InventoryDragItem>();
            if (dragItem != null && dragItem.Item != null)
            {
                var item = dragItem.Item;
                if (item.GetEquipmentType() == Slot)
                {
                    EquipmentService.Instance?.Equip(item, Slot);
                }
            }
        }
        #endregion

        #region Tooltip
        private void ShowTooltip()
        {
            if (_currentItem == null) return;
            TooltipUI.Instance?.ShowEquipment(_currentItem, transform.position);
        }

        private void HideTooltip()
        {
            TooltipUI.Instance?.Hide();
        }
        #endregion
    }

}