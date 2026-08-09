using TMPro;
using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Individual inventory slot UI.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private GameObject _equippedIndicator;
        [SerializeField] private GameObject _favoriteIndicator;
        [SerializeField] private GameObject _lockedIndicator;
        [SerializeField] private GameObject _newIndicator;
        [SerializeField] private Slider _durabilityBar;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private GameObject _enhanceIndicator;

        private InventoryUI _parentUI;
        private int _slotIndex;        // UI grid position (0..capacity-1, fixed)
        private int _inventoryIndex;    // Physical slot index in InventoryService
        private InventoryItem _currentItem;

        /// <summary>Physical index in InventoryService.Slots (-1 when empty).</summary>
        public int InventoryIndex => _inventoryIndex;

        public void Initialize(int slotIndex, InventoryUI parentUI)
        {
            _slotIndex = slotIndex;
            _parentUI = parentUI;
            Clear();
        }

        public void SetItem(InventoryItem item, int inventoryIndex)
        {
            _inventoryIndex = inventoryIndex;
            _currentItem = item;

            if (item == null)
            {
                Clear();
                return;
            }

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null)
            {
                Clear();
                return;
            }

            // Icon
            if (_iconImage != null && itemData.Icon != null)
            {
                _iconImage.sprite = itemData.Icon;
                _iconImage.enabled = true;
            }

            // Quantity
            if (_quantityText != null)
            {
                _quantityText.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
                _quantityText.enabled = item.Quantity > 1;
            }

            // ItemRarity border
            if (_rarityBorder != null && itemData.ItemRarity != ItemRarity.None)
            {
                _rarityBorder.color = itemData.ItemRarity.GetDefaultColor();
                _rarityBorder.enabled = true;
            }

            // Indicators
            if (_equippedIndicator != null) _equippedIndicator.SetActive(item.IsEquipped);
            if (_favoriteIndicator != null) _favoriteIndicator.SetActive(item.IsFavorite);
            if (_lockedIndicator != null) _lockedIndicator.SetActive(item.IsLocked);
            if (_newIndicator != null) _newIndicator.SetActive(item.IsNew);

            // Durability (for equipment)
            if (_durabilityBar != null)
            {
                bool isEquip = item.IsEquippable();
                _durabilityBar.gameObject.SetActive(isEquip);
                if (isEquip)
                {
                    float p = item.GetDurabilityPercent();
                    _durabilityBar.value = p;
                    var fill = _durabilityBar.fillRect != null ? _durabilityBar.fillRect.GetComponent<Image>() : null;
                    if (fill != null) fill.color = item.GetDurabilityColor();
                }
            }

            // Level/Enhance
            if (_levelText != null)
            {
                _levelText.text = item.Level > 1 ? $"Lv.{item.Level}" : "";
                _levelText.enabled = item.Level > 1;
            }

            if (_enhanceIndicator != null)
            {
                _enhanceIndicator.SetActive(item.EnhanceLevel > 0);
                // Could show +X text
            }

            gameObject.name = $"Slot_{_slotIndex}_{item.ItemId}";
        }

        public void Clear()
        {
            _inventoryIndex = -1;
            _currentItem = null;

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
            if (_quantityText != null) _quantityText.enabled = false;
            if (_rarityBorder != null) _rarityBorder.enabled = false;
            if (_equippedIndicator != null) _equippedIndicator.SetActive(false);
            if (_favoriteIndicator != null) _favoriteIndicator.SetActive(false);
            if (_lockedIndicator != null) _lockedIndicator.SetActive(false);
            if (_newIndicator != null) _newIndicator.SetActive(false);
            if (_durabilityBar != null) _durabilityBar.gameObject.SetActive(false);
            if (_levelText != null) _levelText.enabled = false;
            if (_enhanceIndicator != null) _enhanceIndicator.SetActive(false);

            gameObject.name = $"Slot_{_slotIndex}_Empty";
        }

        #region Drag & Drop Handlers
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_currentItem == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right click - context menu
                _parentUI?.ShowContextMenu(_currentItem, _slotIndex);
            }
            else if (eventData.clickCount == 2)
            {
                // Double click - quick action
                OnDoubleClick();
            }
            else
            {
                // Single click - show in info panel
                _parentUI?.SelectItem(_currentItem, _slotIndex);
            }
        }

        private void OnDoubleClick()
        {
            if (_currentItem == null) return;

            var itemData = ItemDatabase.Instance?.GetItem(_currentItem.ItemId);
            if (itemData != null && itemData.IsEquippable())
            {
                // Quick equip
                EquipmentService.Instance?.Equip(_currentItem);
            }
            else if (itemData != null && itemData.IsConsumable())
            {
                // Quick use
                InventoryService.Instance?.UseItem(_currentItem.GetStackKey() ?? _currentItem.InstanceId);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_currentItem == null || _currentItem.IsLocked) return;
            // Drag uses the PHYSICAL inventory index, not the UI grid position
            _parentUI?.BeginDrag(_currentItem, _inventoryIndex, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Handled by InventoryUI Update
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Check if dropped on valid slot
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            int targetSlot = -1;
            foreach (var result in results)
            {
                if (result.gameObject.TryGetComponent<InventorySlotUI>(out var slotUI))
                {
                    targetSlot = slotUI.InventoryIndex; // Physical slot, not UI position
                    break;
                }
            }

            _parentUI?.EndDrag(targetSlot);
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Handled by OnEndDrag
        }
        #endregion
    }
}