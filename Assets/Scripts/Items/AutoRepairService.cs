using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Auto-repair logic: safe zone detection, cooldown, threshold trigger.
    /// Consumes RepairService for the actual repair transaction.
    /// </summary>
    public sealed class AutoRepairService
    {
        private readonly RepairConfig _config;
        private readonly RepairCostCalculator _costCalculator;
        private readonly RepairService _repairService;
        private float _lastAutoRepairTime = -999f;
        private bool _isInSafeZone = true;

        public AutoRepairService(RepairConfig config, RepairCostCalculator costCalculator, RepairService repairService)
        {
            _config = config;
            _costCalculator = costCalculator;
            _repairService = repairService;
        }

        /// <summary>
        /// Enables/disables auto-repair.
        /// </summary>
        public void SetEnabled(bool enabled) => _config.AutoRepairEnabled = enabled;

        /// <summary>
        /// Sets whether we're in a safe zone (for auto-repair gating).
        /// </summary>
        public void SetSafeZone(bool isSafe) => _isInSafeZone = isSafe;

        /// <summary>
        /// Updates safe zone state from a loaded scene name.
        /// </summary>
        public void OnSceneLoaded(string sceneName)
        {
            // Safe zones: MainMenu, Town, Inventory, CardCollection, Settings, Paused
            _isInSafeZone = sceneName == "MainMenu" ||
                            sceneName == "Town" ||
                            sceneName == "Inventory" ||
                            sceneName == "CardCollection" ||
                            sceneName == "Settings" ||
                            sceneName == "Bootstrap";
        }

        /// <summary>
        /// Tries to auto-repair an item when its durability drops.
        /// </summary>
        public void TryAutoRepair(InventoryItem item)
        {
            if (!_config.AutoRepairEnabled) return;
            if (_config.AutoRepairSafeZonesOnly && !_isInSafeZone) return;
            if (Time.time - _lastAutoRepairTime < _config.AutoRepairCooldown) return;

            // Only auto-repair equipped items by default
            if (!item.IsEquipped) return;

            float durabilityPercent = item.GetDurabilityPercent();
            if (durabilityPercent > _config.AutoRepairThreshold) return;

            // Check cost limit
            long estimatedCost = _costCalculator.GetRepairCost(item);
            if (estimatedCost > _config.AutoRepairMaxCost) return;

            // Attempt repair
            var result = _repairService.RepairItem(item);
            if (result.Success)
            {
                _lastAutoRepairTime = Time.time;
            }
        }

        /// <summary>
        /// Checks all equipped items for auto-repair (call periodically or on wave complete).
        /// </summary>
        public void CheckAllEquipped()
        {
            if (!_config.AutoRepairEnabled) return;
            if (_config.AutoRepairSafeZonesOnly && !_isInSafeZone) return;

            var equipped = EquipmentService.Instance?.EquippedItems.Values;
            if (equipped == null) return;

            foreach (var item in equipped)
            {
                if (item.GetDurabilityPercent() <= _config.AutoRepairThreshold)
                {
                    TryAutoRepair(item);
                }
            }
        }
    }
}
