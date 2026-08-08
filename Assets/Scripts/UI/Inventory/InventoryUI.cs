using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
using TMPro;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Main inventory UI controller.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _capacityText;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private GameObject _slotPrefab;

        [Header("Tabs")]
        [SerializeField] private InventoryTabButton[] _tabs;
        [SerializeField] private TabType _currentTab = TabType.All;

        [Header("Info Panel")]
        [SerializeField] private InventoryInfoPanel _infoPanel;

        [Header("Drag Drop")]
        [SerializeField] private Canvas _dragCanvas;
        [SerializeField] private InventoryDragItem _dragItemPrefab;

        // State
        private InventorySlotUI[] _slotUIs;
        private InventoryItem _draggedItem;
        private int _draggedFromSlot = -1;
        private InventoryDragItem _activeDragItem;
        private bool _isInitialized = false;

        #region Unity Lifecycle
        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            RefreshUI();
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }
        #endregion

        #region Initialization
        public void Initialize()
        {
            if (_isInitialized) return;

            // Create slot UIs
            int capacity = InventoryService.Instance?.Config.BaseCapacity ?? 48;
            _slotUIs = new InventorySlotUI[capacity];

            for (int i = 0; i < capacity; i++)
            {
                var slotObj = Instantiate(_slotPrefab, _gridContainer);
                var slotUI = slotObj.GetComponent<InventorySlotUI>();
                slotUI.Initialize(i, this);
                _slotUIs[i] = slotUI;
            }

            // Setup tabs
            foreach (var tab in _tabs)
            {
                tab.Initialize(this);
            }
            SetTab(_currentTab);

            // Setup info panel
            if (_infoPanel != null)
            {
                _infoPanel.Initialize(this);
            }

            _isInitialized = true;
        }

        private void SubscribeEvents()
        {
            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged += OnInventoryChanged;
            }

            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnItemEquipped += OnEquipmentChanged;
                EquipmentService.Instance.OnItemUnequipped += OnEquipmentChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged -= OnInventoryChanged;
            }

            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnItemEquipped -= OnEquipmentChanged;
                EquipmentService.Instance.OnItemUnequipped -= OnEquipmentChanged;
            }
        }
        #endregion

        #region Event Handlers
        private void OnInventoryChanged(InventoryChangedEventArgs args)
        {
            RefreshUI();
        }

        private void OnEquipmentChanged(EquipmentType slot, InventoryItem item)
        {
            RefreshUI();
        }

        #endregion

        #region Public API
        public void RefreshUI()
        {
            if (!_isInitialized) return;

            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            var items = GetFilteredItems();

            // UI slot N shows filtered entry N, but keeps its PHYSICAL inventory index.
            // Drag/drop and MoveItem operate on physical indices, never on UI positions.
            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (i < items.Count)
                {
                    var (item, inventoryIndex) = items[i];
                    _slotUIs[i].SetItem(item, inventoryIndex);
                }
                else
                {
                    _slotUIs[i].Clear();
                }
            }

            // Update capacity display
            UpdateCapacityDisplay();
        }

        private List<(InventoryItem item, int inventoryIndex)> GetFilteredItems()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return new List<(InventoryItem, int)>();

            // Slots preserve physical order; item + its real slot index travel together.
            return inventory.Slots
                .Select((slot, index) => (slot, index))
                .Where(x => !x.slot.IsEmpty)
                .Where(x => !x.slot.Item.IsEquipped)
                .Where(x => _currentTab == TabType.All || TabMatches(x.slot.Item))
                .Select(x => (x.slot.Item, x.index))
                .ToList();
        }

        private bool TabMatches(InventoryItem item) => _currentTab switch
        {
            TabType.Equipment => item.GetItemCategory() == ItemCategory.Equipment,
            TabType.Consumables => item.GetItemCategory() == ItemCategory.Consumable,
            TabType.Materials => item.GetItemCategory() == ItemCategory.Material,
            TabType.Gems => item.GetItemCategory() == ItemCategory.Gem,
            TabType.Other => OtherCategories.Contains(item.GetItemCategory()),
            _ => true
        };

        private static readonly ItemCategory[] OtherCategories =
        {
            ItemCategory.Quest, ItemCategory.Currency, ItemCategory.Key, ItemCategory.Chest,
            ItemCategory.UpgradeStone, ItemCategory.SkillBook, ItemCategory.Rune,
            ItemCategory.Skin, ItemCategory.Pet, ItemCategory.Artifact
        };

        public void SetTab(TabType tab)
        {
            _currentTab = tab;
            foreach (var t in _tabs)
            {
                t.SetActive(t.Type == tab);
            }
            RefreshUI();
        }

        /// <summary>
        /// Single click on a slot: show item details in the info panel.
        /// </summary>
        public void SelectItem(InventoryItem item, int slotIndex)
        {
            if (_infoPanel == null) return;
            _infoPanel.ShowItem(item);
        }

        private void UpdateCapacityDisplay()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            // Update capacity text if exists
            _capacityText.text = $"{inventory.UsedSlots}/{inventory.Capacity}";
        }
        #endregion

        #region Drag & Drop
        public void BeginDrag(InventoryItem item, int slotIndex, Vector3 screenPosition)
        {
            if (item == null || item.IsLocked) return;

            _draggedItem = item;
            _draggedFromSlot = slotIndex;

            if (_dragItemPrefab != null && _dragCanvas != null)
            {
                _activeDragItem = Instantiate(_dragItemPrefab, _dragCanvas.transform);
                _activeDragItem.Initialize(item);
                _activeDragItem.transform.position = screenPosition;
            }
            else
            {
                // No drag prefab wired yet; still allow drop via static DraggedItem.
                InventoryDragItem.DraggedItem = item;
            }
        }

        public void EndDrag(int targetSlotIndex)
        {
            if (_draggedItem == null) return;

            bool success = false;

            if (targetSlotIndex >= 0 && targetSlotIndex < _slotUIs.Length)
            {
                if (targetSlotIndex == _draggedFromSlot)
                {
                    // Clicked on same slot - show context menu
                    ShowContextMenu(_draggedItem, _draggedFromSlot);
                }
                else
                {
                    // Try move
                    success = InventoryService.Instance?.MoveItem(_draggedFromSlot, targetSlotIndex) ?? false;
                }
            }

            if (!success && _draggedFromSlot >= 0)
            {
                // Return to original position (visual only, logic handles it)
            }

            ClearDrag();
        }

        public void CancelDrag()
        {
            ClearDrag();
        }

        private void ClearDrag()
        {
            _draggedItem = null;
            _draggedFromSlot = -1;

            if (_activeDragItem != null)
            {
                Destroy(_activeDragItem.gameObject);
                _activeDragItem = null;
            }
            else
            {
                InventoryDragItem.DraggedItem = null;
            }
        }

        public void ShowContextMenu(InventoryItem item, int slotIndex)
        {
            // TODO: Show context menu with options:
            // Equip, Use, Split, Sell, Destroy, Lock/Unlock, Favorite/Unfavorite, Info
        }
        #endregion

    }


}