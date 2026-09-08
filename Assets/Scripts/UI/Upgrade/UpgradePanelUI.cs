using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Upgrade;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.UI.Inventory;
using IdleDefenseSurvival.Equipment;
using System.Collections.Generic;

namespace IdleDefenseSurvival.UI.Upgrade
{
    /// <summary>
    /// Upgrade/Combine panel UI - two slots (Main + Material), preview, and confirm.
    /// Reuses InventorySlotUI for consistent look. Wires to UpgradeManager.
    /// </summary>
    public class UpgradePanelUI : MonoBehaviour
    {
        [Header("Slot UI")]
        [SerializeField] private InventorySlotUI _mainSlot;
        // assign 4 slot di Inspector
        [SerializeField] private InventorySlotUI[] _materialSlots = new InventorySlotUI[4]; 
        [SerializeField] private InventorySlotUI _previewSlot;

        [Header("Cost")]
        [SerializeField] private TextMeshProUGUI _goldCostText;
        [SerializeField] private Image _goldIcon;

        [Header("Action")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TextMeshProUGUI _upgradeButtonText;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Filters")]
        [SerializeField] private EquipmentTabButton[] _equipmentTabs;
        [SerializeField] private EquipmentType _currentTab = EquipmentType.None;

        [Header("Item List (for picking)")]
        [SerializeField] private RectTransform _itemListContainer;
        [SerializeField] private GameObject _itemListSlotPrefab;
        [SerializeField] private ScrollRect _itemListScroll;

        // State
        private InventoryItem _mainItem;
        private readonly List<InventoryItem> _materialItems = new();
        private int _mainInventoryIndex = -1;
        private readonly int[] _materialInventoryIndices = new int[4] { -1, -1, -1, -1 };
        private InventorySlotUI[] _itemListSlots;
        private List<InventoryItem> _filteredItems = new();

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            ClearSelection();
            RefreshItemList();
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Initialize()
        {
            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);

            foreach (var tab in _equipmentTabs)
            {
                tab.Initialize(this);
            }
            SetTab(_currentTab);

            if (_statusText != null) _statusText.text = "Pilih equipment utama";
        }

        private void SubscribeEvents()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnEquipmentUpgraded += OnEquipmentUpgraded;
                UpgradeManager.Instance.OnUpgradeFailed += OnUpgradeFailed;
            }
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
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnEquipmentUpgraded -= OnEquipmentUpgraded;
                UpgradeManager.Instance.OnUpgradeFailed -= OnUpgradeFailed;
            }
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

        private void OnInventoryChanged(InventoryChangedEventArgs args)
        {
            RefreshItemList();
            RefreshSlots();
        }
        private void OnEquipmentChanged(EquipmentType slot, InventoryItem item)
        {
            RefreshItemList();
        }

        private void OnEquipmentUpgraded(InventoryItem main, InventoryItem material, int oldLevel, int newLevel)
        {
            _statusText.text = $"Berhasil! Level {oldLevel} → {newLevel}";

            // Keep upgraded main item visible in main slot
            _mainItem = main;
            _mainInventoryIndex = -1; // will be resolved in RefreshSlots()

            // Clear only material slots
            ClearMaterialSlots();

            // Refresh UI: main slot (new level) + item list (materials consumed)
            RefreshSlots();
            RefreshItemList();

            _upgradeButton.interactable = false;
            _goldCostText.text = "-";
        }

        // Helper – clear material UI but preserve main slot
        private void ClearMaterialSlots()
        {
            _materialItems.Clear();
            for (int i = 0; i < _materialSlots.Length; i++)
            {
                _materialSlots[i].Clear();
                _materialInventoryIndices[i] = -1;
            }
        }

        private void OnUpgradeFailed(InventoryItem main, InventoryItem material, string reason)
        {
            _statusText.text = $"Gagal: {reason}";
        }

        // Tab filtering by EquipmentType
        public void SetTab(EquipmentType tab)
        {
            _currentTab = tab;
            foreach (var t in _equipmentTabs)
            {
                t.SetActive(t.Type == tab);
            }
            RefreshItemList();
        }

        // Called by InventorySlotUI when clicked (single click selects)
        public void OnMainSlotClicked(InventoryItem item, int inventoryIndex)
        {
            if (item == null || !item.IsEquippable()) return;

            // Block max-level equipment from being selected as main
            if (item.IsMaxLevel)
            {
                _statusText.text = $"Equipment {item.ItemId} sudah level maksimum.";
                return;
            }

            // Click same main → clear selection (toggle)
            if (_mainItem != null && item.InstanceId == _mainItem.InstanceId)
            {
                ClearSelection();
                return;
            }

            // New main selected → clear materials (different type may have been added)
            if (_mainItem != null)
            {
                ClearMaterialSlots();
            }

            _mainItem = item;
            _mainInventoryIndex = inventoryIndex;
            _mainSlot.SetItem(item, inventoryIndex);
            UpdatePreview();
            _statusText.text = "Pilih equipment material";
        }

        /// <summary>
        /// Clears the main equipment selection (called when clicking main slot directly).
        /// </summary>
        public void ClearMainSlot()
        {
            if (_mainItem == null) return;

            ClearMaterialSlots();
            _mainItem = null;
            _mainInventoryIndex = -1;
            _mainSlot.Clear();
            if (_previewSlot != null) _previewSlot.Clear();
            _upgradeButton.interactable = false;
            _goldCostText.text = "-";
            _statusText.text = "Pilih equipment utama";
        }

        public void OnMaterialSlotClicked(InventoryItem item, int inventoryIndex)
        {
            if (item == null || !item.IsEquippable()) return;
            if (item.InstanceId == _mainItem?.InstanceId) return;

            // Material type must match main type
            if (_mainItem != null && item.GetEquipmentType() != _mainItem.GetEquipmentType())
            {
                _statusText.text = $"Material tipe {item.GetEquipmentType()} tidak cocok dengan main {_mainItem.GetEquipmentType()}";
                return;
            }

            // Prevent selecting same material twice
            if (_materialItems.Exists(m => m.InstanceId == item.InstanceId))
            {
                _statusText.text = $"Material {item.ItemId} sudah dipilih.";
                return;
            }

            // Cek batas max level
            int maxMaterials = _mainItem != null ? Mathf.Max(0, _mainItem.MaxLevel - _mainItem.Level) : 0;
            if (_materialItems.Count >= maxMaterials && maxMaterials > 0)
            {
                _statusText.text = $"Maksimal {maxMaterials} material (level {_mainItem.Level}/{_mainItem.MaxLevel})";
                return;
            }

            // Cari slot kosong
            for (int i = 0; i < _materialSlots.Length; i++)
            {
                if (_materialSlots[i].CurrentItem == null)
                {
                    _materialItems.Add(item);
                    _materialInventoryIndices[i] = inventoryIndex;
                    _materialSlots[i].SetItem(item, inventoryIndex);
                    break;
                }
            }
            UpdatePreview();
        }


        private void UpdatePreview()
        {
            if (_mainItem != null && _materialItems.Count > 0)
            {
                bool canUpgrade = UpgradeManager.Instance.CanUpgradeMultiple(_mainItem, _materialItems, out string reason);
                int resultLevel = Mathf.Min(_mainItem.Level + _materialItems.Count, _mainItem.MaxLevel);
                long goldCost = GetUpgradeGoldCost(_mainItem) * _materialItems.Count;

                _upgradeButton.interactable = canUpgrade && EconomyManager.Instance.HasEnoughCurrency(CurrencyType.Gold, goldCost);
                _goldCostText.text = goldCost.ToString("N0");

                if (_previewSlot != null)
                {
                    var preview = CreatePreviewItem(_mainItem, resultLevel);
                    _previewSlot.SetItem(preview, -1);
                }

                _statusText.text = canUpgrade
                    ? $"Level {_mainItem.Level} → {resultLevel} (Biaya: {goldCost:N0} Gold)"
                    : $"Tidak cocok: {reason}";
            }
            else if (_mainItem != null)
            {
                _upgradeButton.interactable = false;
                _goldCostText.text = "-";
                _previewSlot?.Clear();
                _statusText.text = "Pilih equipment material";
            }
            else
            {
                _upgradeButton.interactable = false;
                _goldCostText.text = "-";
                _previewSlot?.Clear();
                _statusText.text = "Pilih equipment utama";
            }
        }

        private void OnUpgradeClicked()
        {
            if (_mainItem == null || _materialItems.Count == 0) return;
            UpgradeManager.Instance.UpgradeMultiple(_mainItem, _materialItems);
        }

        private void ClearSelection()
        {
            _mainItem = null;
            _materialItems.Clear();
            _mainInventoryIndex = -1;
            for (int i = 0; i < _materialSlots.Length; i++)
            {
                _materialSlots[i].Clear();
                _materialInventoryIndices[i] = -1;
            }
            _mainSlot.Clear();
            if (_previewSlot != null) _previewSlot.Clear();
            _upgradeButton.interactable = false;
            _goldCostText.text = "-";
            _statusText.text = "Pilih equipment utama";
        }

        private void RefreshSlots()
        {
            // Main
            if (_mainItem != null)
            {
                var refreshed = InventoryService.Instance.GetItem(_mainItem.InstanceId);
                if (refreshed != null)
                {
                    _mainItem = refreshed;
                    _mainSlot.SetItem(refreshed, _mainInventoryIndex);
                }
                else ClearSelection();
            }

            // Materials
            for (int i = 0; i < _materialSlots.Length; i++)
            {
                var mat = i < _materialItems.Count ? _materialItems[i] : null;
                if (mat != null)
                {
                    var refreshed = InventoryService.Instance.GetItem(mat.InstanceId);
                    if (refreshed != null)
                    {
                        _materialItems[i] = refreshed;
                        _materialSlots[i].SetItem(refreshed, _materialInventoryIndices[i]);
                    }
                    else
                    {
                        _materialItems.RemoveAt(i);
                        _materialSlots[i].Clear();
                        _materialInventoryIndices[i] = -1;
                    }
                }
                else
                {
                    _materialSlots[i].Clear();
                }
            }
            UpdatePreview();
        }


        private void RefreshItemList()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            _filteredItems.Clear();
            foreach (var slot in inventory.Slots)
            {
                if (!slot.IsEmpty && slot.Item.IsEquippable())
                {
                    if (_currentTab == EquipmentType.None || slot.Item.GetEquipmentType() == _currentTab)
                    {
                        _filteredItems.Add(slot.Item);
                    }
                }
            }

            // Build item list UI
            if (_itemListContainer != null && _itemListSlotPrefab != null)
            {
                // Simple pool
                _itemListSlots ??= new InventorySlotUI[0];
                int needed = _filteredItems.Count;
                if (_itemListSlots.Length < needed)
                {
                    Array.Resize(ref _itemListSlots, needed);
                    for (int i = 0; i < needed; i++)
                    {
                        if (_itemListSlots[i] == null)
                        {
                            var obj = Instantiate(_itemListSlotPrefab, _itemListContainer);
                            _itemListSlots[i] = obj.GetComponent<InventorySlotUI>();
                            _itemListSlots[i].Initialize(i, null); // No parent UI for list
                        }
                    }
                }

                for (int i = 0; i < _itemListSlots.Length; i++)
                {
                    if (i < _filteredItems.Count)
                    {
                        var item = _filteredItems[i];
                        int invIndex = -1;
                        var slots = inventory.Slots;
                        for (int j = 0; j < slots.Count; j++)
                        {
                            if (slots[j].Item == item)
                            {
                                invIndex = j;
                                break;
                            }
                        }
                        _itemListSlots[i].SetItem(item, invIndex);

                        // Wire callbacks for Upgrade scene (no parent InventoryUI)
                        _itemListSlots[i].OnSingleClickCallback = OnItemListSingleClick;
                        _itemListSlots[i].OnDoubleClickCallback = OnItemListDoubleClick;

                        _itemListSlots[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        _itemListSlots[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private long GetUpgradeGoldCost(InventoryItem item)
        {
            // Base cost scales with level and rarity
            int rarityMultiplier = (int)item.GetRarity() + 1; // Common=1, Rare=2, etc.
            return 10000L * item.Level * rarityMultiplier;
        }

        private InventoryItem CreatePreviewItem(InventoryItem source, int targetLevel)
        {
            var preview = new InventoryItem
            {
                ItemId = source.ItemId,
                Quantity = 1,
                Level = targetLevel,
                MaxLevel = source.MaxLevel,
                InstanceId = "PREVIEW_" + source.InstanceId,
                EquipmentType = source.EquipmentType,
                MaxDurability = source.MaxDurability,
                CurrentDurability = source.CurrentDurability,
                DurabilityLossPerUse = source.DurabilityLossPerUse,
                RepairCostPerDurability = source.RepairCostPerDurability,
                MaxSockets = source.MaxSockets,
                Sockets = source.Sockets,
                AttributeData = source.AttributeData,
                IsEquipped = false,
                IsLocked = false,
                IsFavorite = false,
                IsNew = false
            };
            return preview;
        }

        // Upgrade scene item-list callbacks
        private void OnItemListSingleClick(InventoryItem item, int inventoryIndex)
        {
            if (item == null || !item.IsEquippable()) return;

            // =========================================================
            // 1. Belum ada MAIN → klik pertama menjadi MAIN
            // =========================================================
            if (_mainItem == null)
            {
                OnMainSlotClicked(item, inventoryIndex);
                return;
            }

            // =========================================================
            // 2. Klik MAIN yang sedang terpilih → clear semua selection
            // =========================================================
            if (item.InstanceId == _mainItem.InstanceId)
            {
                ClearSelection();
                return;
            }

            // =========================================================
            // 3. MAIN sudah ada dan sejenis → klik item menjadi MATERIAL
            // =========================================================
            if (item.GetEquipmentType() == _mainItem.GetEquipmentType())
            {
                OnMaterialSlotClicked(item, inventoryIndex);
                return;
            }

            // =========================================================
            // 4. MAIN sudah ada namun beda jenis → replace main (swap)
            // =========================================================
            ClearMaterialSlots();
            _mainItem = item;
            _mainInventoryIndex = inventoryIndex;
            _mainSlot.SetItem(item, inventoryIndex);
            UpdatePreview();
            _statusText.text = "Pilih equipment material";
        }

        private void OnItemListDoubleClick(InventoryItem item, int inventoryIndex)
        {
            // Double-click = quick equip (like Inventory scene), then refresh list for equipped indicator
            if (item != null && item.IsEquippable())
                EquipmentService.Instance?.Equip(item);
            RefreshItemList();
        }
    }
}