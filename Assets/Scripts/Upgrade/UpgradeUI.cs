using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Upgrade screen inventory UI - filters to equipment only, tabs by EquipmentType.
    /// Inherits from InventoryUI to reuse slot/drag/info panel logic.
    /// </summary>
    public class UpgradeUI : InventoryUI
    {
        [Header("Upgrade Tabs")]
        [SerializeField] private EquipmentTabButton[] _equipmentTabs;
        [SerializeField] private EquipmentType _currentEquipmentTab = EquipmentType.None;

        protected override void Awake()
        {
            Initialize();
        }

        public override void Initialize()
        {
            if (_isInitialized) return;

            // Call base initialization for slot creation, event subscription, info panel
            base.Initialize();

            // Override: setup equipment tabs instead of category tabs
            foreach (var tab in _equipmentTabs)
            {
                tab.Initialize(this);
            }
            SetTab(_currentEquipmentTab);
        }

        // Override filtering: only equipment, filtered by EquipmentType tab
        protected override List<(InventoryItem item, int inventoryIndex)> GetFilteredItems()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return new List<(InventoryItem, int)>();

            return inventory.Slots
                .Select((slot, index) => (slot, index))
                .Where(x => !x.slot.IsEmpty)
                .Where(x => x.slot.Item.IsEquippable())
                .Where(x => _currentEquipmentTab == EquipmentType.None || x.slot.Item.GetEquipmentType() == _currentEquipmentTab)
                .Select(x => (x.slot.Item, x.index))
                .ToList();
        }

        // Tab switching uses EquipmentType (different signature from base, so no override needed)
        public void SetTab(EquipmentType tab)
        {
            _currentEquipmentTab = tab;
            foreach (var t in _equipmentTabs)
            {
                t.SetActive(t.Type == tab);
            }
            RefreshUI();
        }
    }
}