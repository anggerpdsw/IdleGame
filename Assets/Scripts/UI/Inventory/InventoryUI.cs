using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
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

        [Header("Filter/Sort")]
        [SerializeField] private InventorySortUI _sortUI;

        [Header("Tabs")]
        [SerializeField] private InventoryTabButton[] _tabs;
        [SerializeField] private TabType _currentTab = TabType.All;

        [Header("Info Panel")]
        [SerializeField] private InventoryInfoPanel _infoPanel;

        [Header("Drag Drop")]
        [SerializeField] private Canvas _dragCanvas;
        [SerializeField] private InventoryDragItem _dragItemPrefab;

        [Header("Dev")]
        [Tooltip("Seed sample items when inventory is empty")]
        [SerializeField] private bool _seedSampleItems = true;

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

        private void Update()
        {
            // Handle drag visual
            if (_activeDragItem != null)
            {
                _activeDragItem.transform.position = Mouse.current.position.ReadValue();
            }
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

            // Setup sort
            if (_sortUI != null)
            {
                _sortUI.OnSortChanged += OnSortChanged;
            }

            // Setup info panel
            if (_infoPanel != null)
            {
                _infoPanel.Initialize(this);
            }

            // Ensure inventory has content for demo/debug (no-op if already populated)
            SeedSampleItemsIfEmpty();

            _isInitialized = true;
        }

        /// <summary>
        /// Dev convenience: sample items so the grid is never blank in a fresh save.
        /// Disable via flag in Inspector for a real empty inventory.
        /// </summary>
        private void SeedSampleItemsIfEmpty()
        {
            if (!_seedSampleItems) return;
            var inv = InventoryService.Instance;
            if (inv == null || ItemDatabase.Instance == null || inv.AllItems.Count > 0) return;

            inv.AddItem("potion_hp", 12);
            inv.AddItem("iron_ore", 40);
            inv.AddItem("magic_crystal", 9);
            inv.AddItem("upgrade_stone_basic", 5);
            inv.AddItem("gold_pouch", 3);
            inv.AddItem("equip_hat_leather");
            inv.AddItem("equip_gloves_fighter");
            inv.AddItem("equip_armor_iron");
            inv.AddItem("equip_ring_ruby");
        }

        private void SubscribeEvents()
        {
            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged += OnInventoryChanged;
                InventoryService.Instance.OnInventorySorted += OnInventorySorted;
                InventoryService.Instance.OnInventoryFiltered += OnInventoryFiltered;
            }
        }

        private void UnsubscribeEvents()
        {
            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged -= OnInventoryChanged;
                InventoryService.Instance.OnInventorySorted -= OnInventorySorted;
                InventoryService.Instance.OnInventoryFiltered -= OnInventoryFiltered;
            }
        }
        #endregion

        #region Event Handlers
        private void OnInventoryChanged(InventoryChangedEventArgs args)
        {
            RefreshUI();
        }

        private void OnInventorySorted()
        {
            RefreshUI();
        }

        private void OnInventoryFiltered()
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
            int index = 0;

            foreach (var item in items)
            {
                if (index < _slotUIs.Length)
                {
                    _slotUIs[index].SetItem(item);
                    index++;
                }
            }

            // Clear remaining slots
            for (int i = index; i < _slotUIs.Length; i++)
            {
                _slotUIs[i].Clear();
            }

            // Update capacity display
            UpdateCapacityDisplay();
        }

        private List<InventoryItem> GetFilteredItems()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return new List<InventoryItem>();

            var items = new List<InventoryItem>(inventory.AllItems);

            // Equipment currently worn is shown in the paper-doll slots, never in the list.
            items.RemoveAll(item => item.IsEquipped);

            if (_currentTab != TabType.All)
                items.RemoveAll(item => !TabMatches(item));

            return items;
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

        public void OnSortChanged(InventorySortType sortType, bool ascending)
        {
            InventoryService.Instance?.Sort(sortType, ascending);
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

        #region Tab Types
        public enum TabType
        {
            All = 0,
            Equipment = 1,
            Consumables = 2,
            Materials = 3,
            Gems = 4,
            Other = 5
        }
        #endregion
    }


}