using System;
using UnityEngine;

using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Upgrade
{
    /// <summary>
    /// Handles equipment upgrading by consuming another equipment
    /// of the same type.
    ///
    /// Upgrade rule:
    /// Main equipment level + 1
    /// Material equipment is consumed.
    ///
    /// Example:
    /// Main Lv.1 + Material Lv.3 = Main Lv.2
    /// Main Lv.2 + Material Lv.10 = Main Lv.3
    /// </summary>
    public sealed class UpgradeManager : MonoBehaviour
    {
        #region Singleton
        private static UpgradeManager _instance;
        public static UpgradeManager Instance => _instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        #endregion

        #region Events
        /// <summary>
        /// Fired after a successful equipment upgrade.
        /// </summary>
        public event Action<InventoryItem, InventoryItem, int, int> OnEquipmentUpgraded;
        /// <summary>
        /// Fired when an upgrade attempt fails.
        /// </summary>
        public event Action<InventoryItem, InventoryItem, string> OnUpgradeFailed;
        #endregion

        #region Public API
        /// <summary>
        /// Attempts to upgrade main equipment by consuming material equipment.
        /// </summary>
        /// <param name="main">Equipment that receives the level increase.</param>
        /// <param name="material">Equipment that will be consumed.</param>
        /// <returns>True if upgrade succeeds.</returns>
        public bool UpgradeEquipment(InventoryItem main, InventoryItem material)
        {
            if (!CanUpgradeEquipment(main, material, out string reason))
            {
                Debug.LogWarning($"[UpgradeManager] Upgrade failed: {reason}");
                OnUpgradeFailed?.Invoke(main, material, reason);
                return false;
            }

            var inventory = InventoryService.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[UpgradeManager] Upgrade failed: InventoryService is not available.");
                return false;
            }

            int oldLevel = main.Level;
            int newLevel = oldLevel + 1;

            /*
             * -------------------------------------------------------------
             * 1. Upgrade main equipment
             * -------------------------------------------------------------
             */

            main.Level = newLevel;

            /*
             * -------------------------------------------------------------
             * 2. Consume material
             * -------------------------------------------------------------
             */

            int removed = inventory.RemoveItem(material.InstanceId, 1);
            bool materialRemoved = removed > 0;
            if (!materialRemoved)
            {
                // Safety rollback.
                main.Level = oldLevel;
                Debug.LogWarning(
                    $"[UpgradeManager] Failed to consume material {material.InstanceId}. " +
                    "Upgrade has been rolled back.");
                return false;
            }

            /*
             * -------------------------------------------------------------
             * 3. Mark main item dirty
             * -------------------------------------------------------------
             */
            inventory.MarkItemDirty(main.InstanceId, DirtyType.Item | DirtyType.Tooltip);

            /*
             * -------------------------------------------------------------
             * 4. Refresh stats
             * -------------------------------------------------------------
             */
            RefreshEquipmentStats(main);

            /*
             * -------------------------------------------------------------
             * 5. Notify listeners
             * -------------------------------------------------------------
             */
            OnEquipmentUpgraded?.Invoke(main, material, oldLevel, newLevel);
            Debug.Log(
                $"[UpgradeManager] Equipment upgraded successfully: " +
                $"{main.ItemId} " +
                $"Lv.{oldLevel} -> Lv.{newLevel}. " +
                $"Material {material.InstanceId} consumed.");
            return true;
        }

        /// <summary>
        /// Checks whether two equipment items can be merged/upgraded.
        /// </summary>
        public bool CanUpgradeEquipment(InventoryItem main, InventoryItem material, out string reason)
        {
            reason = string.Empty;

            /*
             * -------------------------------------------------------------
             * Main validation
             * -------------------------------------------------------------
             */
            if (main == null)
            {
                reason = "Main equipment is null.";
                return false;
            }

            if (material == null)
            {
                reason = "Material equipment is null.";
                return false;
            }

            if (main == material)
            {
                reason = "Main and material cannot be the same instance.";
                return false;
            }

            if (!main.IsEquippable())
            {
                reason = "Main equipment is not equippable.";
                return false;
            }

            if (!material.IsEquippable())
            {
                reason = "Material equipment is not equippable.";
                return false;
            }

            /*
             * -------------------------------------------------------------
             * Equipment type validation
             * -------------------------------------------------------------
             */
            EquipmentType mainType = main.GetEquipmentType();
            EquipmentType materialType = material.GetEquipmentType();

            if (mainType == EquipmentType.None)
            {
                reason = "Main equipment has an invalid equipment type.";
                return false;
            }

            if (mainType != materialType)
            {
                reason = $"Equipment types differ: {mainType} vs {materialType}.";
                return false;
            }

            /*
             * -------------------------------------------------------------
             * Inventory validation
             * -------------------------------------------------------------
             */
            var inventory = InventoryService.Instance;
            if (inventory == null)
            {
                reason = "InventoryService is not available.";
                return false;
            }

            if (inventory.GetItem(main.InstanceId) == null)
            {
                reason = "Main equipment is not in inventory.";
                return false;
            }

            if (inventory.GetItem(material.InstanceId) == null)
            {
                reason = "Material equipment is not in inventory.";
                return false;
            }

            /*
             * -------------------------------------------------------------
             * Main level validation
             * -------------------------------------------------------------
             */
            if (main.IsMaxLevel)
            {
                reason = $"Main equipment is already at max level ({main.Level}).";
                return false;
            }

            /*
             * -------------------------------------------------------------
             * Material validation
             * -------------------------------------------------------------
             *
             * Material may NOT be:
             * - equipped
             * - locked
             * - favorite
             */
            if (material.IsEquipped)
            {
                reason = "Material equipment is currently equipped.";
                return false;
            }

            if (material.IsLocked)
            {
                reason = "Material equipment is locked.";
                return false;
            }

            if (material.IsFavorite)
            {
                reason = "Material equipment is favorited.";
                return false;
            }

            return true;
        }
        #endregion

        #region Preview
        /// <summary>
        /// Gets the level that the main equipment will have after upgrade.
        /// </summary>
        public int GetResultLevel(InventoryItem main)
        {
            if (main == null) return 0;
            if (main.IsMaxLevel) return main.Level;
            return main.Level + 1;
        }

        /// <summary>
        /// Checks whether main and material are compatible.
        /// </summary>
        public bool IsCompatible(InventoryItem main, InventoryItem material)
        {
            if (main == null || material == null) return false;
            if (!main.IsEquippable() || !material.IsEquippable()) return false;
            return main.GetEquipmentType() == material.GetEquipmentType();
        }
        #endregion

        #region Stat Refresh
        private void RefreshEquipmentStats(InventoryItem item)
        {
            if (item == null) return;

            /*
             * EquipmentStatCalculator reads the current item level
             * when creating its stat modifiers.
             *
             * Therefore the old modifier must be refreshed.
             */

            var equipmentService = Equipment.EquipmentService.Instance;

            if (equipmentService != null && item.IsEquipped)
            {
                EquipmentType slot = item.EquippedSlot;
                equipmentService.ApplyItemStatModifiers(item, slot, false);
                equipmentService.ApplyItemStatModifiers(item, slot, true);
            }

            ModifierManager.Instance?.CleanupExpired();
            PlayerStatsManager.Instance?.RefreshStats();
        }
        #endregion

        #region Legacy Compatibility
        /// <summary>
        /// Compatibility wrapper for the old MergeEquipment API.
        /// </summary>
        public bool MergeEquipment(InventoryItem main, InventoryItem material)
            => UpgradeEquipment(main, material);
        #endregion

        #region UI Navigation
        public void OpenUpgrade() => SceneLoader.Instance.LoadUpgrade();
        #endregion
    }
}