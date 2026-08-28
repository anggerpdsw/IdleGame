using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using System.Linq;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Equipment UI controller - displays equipped items and allows management.
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlotUI[] _slotUis;

        [Header("Info Panel")]
        [SerializeField] private EquipmentInfoPanel _infoPanel;
        [SerializeField] private EquipmentComparePanel _comparePanel;

        [Header("Set Bonus Display")]
        [SerializeField] private Transform _setBonusContainer;
        [SerializeField] private GameObject _setBonusEntryPrefab;

        [Header("Stats Display")]
        [SerializeField] private TextMeshProUGUI _totalStatsText;
        [SerializeField] private Transform _statBreakdownContainer;
        [SerializeField] private GameObject _statEntryPrefab;

        [Header("Actions")]
        [SerializeField] private Button _autoEquipButton;
        [SerializeField] private Button _unequipAllButton;
        [SerializeField] private Button _repairAllButton;

        // State
        public EquipmentSlotUI _hoveredSlot;
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

            // Initialize slot UIs
            foreach (var slotUI in _slotUis)
            {
                slotUI.Initialize(this);
            }

            // Setup buttons
            if (_autoEquipButton != null)
                _autoEquipButton.onClick.AddListener(OnAutoEquip);

            if (_unequipAllButton != null)
                _unequipAllButton.onClick.AddListener(OnUnequipAll);

            if (_repairAllButton != null)
                _repairAllButton.onClick.AddListener(OnRepairAll);

            _isInitialized = true;
        }

        private void SubscribeEvents()
        {
            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnEquipmentChanged += OnEquipmentChanged;
                EquipmentService.Instance.OnItemEquipped += OnItemEquipped;
                EquipmentService.Instance.OnItemUnequipped += OnItemUnequipped;
                EquipmentService.Instance.OnSetBonusChanged += OnSetBonusChanged;
                EquipmentService.Instance.OnDurabilityChanged += OnDurabilityChanged;
                EquipmentService.Instance.OnSlotUnlocked += OnSlotUnlocked;
            }

            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged += OnInventoryChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentService.Instance.OnItemEquipped -= OnItemEquipped;
                EquipmentService.Instance.OnItemUnequipped -= OnItemUnequipped;
                EquipmentService.Instance.OnSetBonusChanged -= OnSetBonusChanged;
                EquipmentService.Instance.OnDurabilityChanged -= OnDurabilityChanged;
                EquipmentService.Instance.OnSlotUnlocked -= OnSlotUnlocked;
            }

            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged -= OnInventoryChanged;
            }
        }
        #endregion

        #region Event Handlers
        private void OnEquipmentChanged(EquipmentChangedEventArgs args)
        {
            if (args?.Slot != EquipmentType.None) RefreshSlot(args.Slot);
            else RefreshUI();
        }
        private void OnItemEquipped(EquipmentType slot, InventoryItem item) => RefreshSlot(slot);
        private void OnItemUnequipped(EquipmentType slot, InventoryItem item) => RefreshSlot(slot);
        private void OnSetBonusChanged() => RefreshAllSlots();
        private void OnDurabilityChanged(EquipmentType slot) => RefreshSlot(slot);
        private void OnSlotUnlocked(EquipmentType slot) => RefreshSlot(slot);
        private void OnInventoryChanged(InventoryChangedEventArgs args) => RefreshAllSlots();
        #endregion

        #region Public API
        public void RefreshUI()
        {
            if (!_isInitialized) return;

            RefreshAllSlots();
            RefreshSetBonuses();
            RefreshStats();
        }

        private void RefreshAllSlots()
        {
            if (_slotUis == null) return;
            foreach (var slotUI in _slotUis)
            {
                RefreshSlot(slotUI.Slot);
            }
        }

        private void RefreshSlot(EquipmentType slot)
        {
            var slotUI = Array.Find(_slotUis, s => s.Slot == slot);
            if (slotUI == null) return;

            slotUI.ApplyViewData(BuildSlotViewData(slot));
        }

        /// <summary>Builds presenter view-data for one slot (all service lookups happen here).</summary>
        public bool IsSlotLocked(EquipmentType slot)
        {
            var equipment = EquipmentService.Instance;
            if (equipment == null) return false;
            foreach (var sd in equipment.SlotData)
            {
                if (sd.Slot == slot) return !sd.IsUnlocked;
            }
            return false;
        }

        private EquipmentSlotViewData BuildSlotViewData(EquipmentType slot)
        {
            var equipment = EquipmentService.Instance;
            if (equipment == null) return new EquipmentSlotViewData();

            equipment.EquippedItems.TryGetValue(slot, out var item);

            EquipmentSlotData slotData = null;
            foreach (var sd in equipment.SlotData)
            {
                if (sd.Slot == slot) { slotData = sd; break; }
            }

            string setId = item?.GetSetId();
            bool setBonusActive = !string.IsNullOrEmpty(setId) && equipment.IsSetBonusActive(setId);

            bool isLocked = slotData != null && !slotData.IsUnlocked;

            return EquipmentPresentationService.BuildSlot(new EquipmentSlotViewSource
            {
                IsLocked = isLocked,
                Item = item,
                SetBonusActive = setBonusActive,
                UnlockState = slotData?.UnlockState ?? EquipmentSlotUnlockState.Unlocked,
                UnlockCost = isLocked ? equipment.GetSlotUnlockCost(slot) : 0,
                RequiredLevel = slotData?.RequiredLevel ?? 1
            });
        }

        private void RefreshSetBonuses()
        {
            if (_setBonusContainer == null || _setBonusEntryPrefab == null) return;

            // Clear existing
            foreach (Transform child in _setBonusContainer)
                Destroy(child.gameObject);

            var equipment = EquipmentService.Instance;
            if (equipment == null) return;

            var activeBonuses = equipment.GetAllActiveSetBonuses();
            foreach (var kvp in activeBonuses)
            {
                var setData = ItemDatabase.Instance?.GetSet(kvp.Key);
                if (setData == null) continue;

                foreach (var tier in kvp.Value)
                {
                    var entryObj = Instantiate(_setBonusEntryPrefab, _setBonusContainer);
                    var entryUI = entryObj.GetComponent<SetBonusEntryUI>();
                    entryUI?.Initialize(setData, tier, kvp.Value.Count);
                }
            }
        }

        private void RefreshStats()
        {
            var equipment = EquipmentService.Instance;
            if (equipment == null) return;

            var totalBonuses = equipment.GetTotalStatBonuses();

            // Update total stats text
            if (_totalStatsText != null)
            {
                var lines = new List<string>();
                foreach (var kvp in totalBonuses.OrderByDescending(k => Math.Abs(k.Value)))
                {
                    string sign = kvp.Value >= 0 ? "+" : "";
                    lines.Add($"{kvp.Key.GetSkillShortName()}: {sign}{kvp.Value:F1}");
                }
                _totalStatsText.text = string.Join("\n", lines);
            }

            // Update stat breakdown
            if (_statBreakdownContainer != null && _statEntryPrefab != null)
            {
                foreach (Transform child in _statBreakdownContainer)
                    Destroy(child.gameObject);

                foreach (var kvp in totalBonuses.OrderByDescending(k => Math.Abs(k.Value)))
                {
                    var entryObj = Instantiate(_statEntryPrefab, _statBreakdownContainer);
                    var entryUI = entryObj.GetComponent<StatBreakdownEntryUI>();
                    entryUI?.Initialize(kvp.Key, kvp.Value);
                }
            }
        }
        #endregion

        #region Actions
        private void OnAutoEquip()
        {
            int equipped = EquipmentService.Instance?.AutoEquipBest() ?? 0;
            // Show feedback
        }

        private void OnUnequipAll()
        {
            EquipmentService.Instance?.UnequipAll();
        }

        private void OnRepairAll()
        {
            var result = RepairService.Instance?.RepairAll(RepairMode.Equipped);
            long cost = result?.TotalCost ?? 0;
            // Show feedback
        }

        public void ShowComparison(InventoryItem newItem)
        {
            if (_comparePanel != null && newItem != null)
            {
                InventoryItem currentItem = null;
                EquipmentService.Instance?.EquippedItems.TryGetValue(newItem.GetEquipmentType(), out currentItem);
                _comparePanel.ShowComparison(currentItem, newItem);
            }
        }

        public void HideComparison()
        {
            _comparePanel?.Hide();
        }
        #endregion

        #region Slot Interaction
        public void OnSlotClick(EquipmentSlotUI slotUI)
        {
            if (slotUI == null) return;
            var item = slotUI.CurrentItem;
            if (item != null)
                // Show item detail / comparison
                ShowComparison(item);
        }

        public void OnSlotRightClick(EquipmentSlotUI slotUI)
        {
            if (slotUI == null) return;
            var item = slotUI.CurrentItem;
            if (item != null)
            {
                // Show context menu: Unequip, Info, Repair
            }
            else
            {
                // Slot empty - show compatible items from inventory
                ShowCompatibleItems(slotUI.Slot);
            }
        }

        private void ShowCompatibleItems(EquipmentType slot)
        {
            var inventory = InventoryService.Instance;
            var equipment = EquipmentService.Instance;
            if (inventory == null || equipment == null) return;

            var candidates = inventory.GetEquipmentsByType(slot)
                .Where(i => !i.IsEquipped && equipment.CanEquip(i, slot, out _))
                .ToList();

            // Show in a popup or highlight in inventory
        }
        #endregion
    }

    
    /// <summary>
    /// Set bonus entry UI.
    /// </summary>
    public class SetBonusEntryUI : MonoBehaviour
    {
        [SerializeField] private Image _setIcon;
        [SerializeField] private TextMeshProUGUI _setNameText;
        [SerializeField] private TextMeshProUGUI _tierText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        public void Initialize(SetBonusData setData, SetBonusTier tier, int currentPieces)
        {
            if (_setIcon != null && setData.SetIcon != null)
                _setIcon.sprite = setData.SetIcon;

            if (_setNameText != null)
                _setNameText.text = setData.SetName;

            if (_tierText != null)
                _tierText.text = tier.TierName;

            if (_descriptionText != null)
                _descriptionText.text = tier.Description;

            if (_progressBar != null)
            {
                _progressBar.fillAmount = (float)currentPieces / tier.RequiredPieces;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{currentPieces}/{tier.RequiredPieces}";
            }
        }
    }

    /// <summary>
    /// Stat breakdown entry UI.
    /// </summary>
    public class StatBreakdownEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;
        [SerializeField] private Image _statIcon;

        public void Initialize(SecondaryStat stat, float value)
        {
            if (_statNameText != null)
                _statNameText.text = stat.GetSkillDisplayName();

            if (_statValueText != null)
            {
                string sign = value >= 0 ? "+" : "";
                _statValueText.text = $"{sign}{value:F1}";
                _statValueText.color = value >= 0 ? Color.green : Color.red;
            }

            if (_statIcon != null)
            {
                // Could set stat-specific icon
            }
        }
    }
}