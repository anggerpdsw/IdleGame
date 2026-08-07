using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Centralized durability management service.
    /// All durability changes (damage, repair, break, restore) go through this service.
    /// </summary>
    public sealed class DurabilityService : MonoBehaviour
    {
        #region Singleton
        private static DurabilityService _instance;
        public static DurabilityService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

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
        /// <summary>Fired when any item's durability changes.</summary>
        public event Action<InventoryItem, int, int, DurabilityChangeReason> OnDurabilityChanged; // item, oldValue, newValue, reason

        /// <summary>Fired when an item breaks (durability reaches 0).</summary>
        public event Action<InventoryItem> OnItemBroken;

        /// <summary>Fired when an item is fully repaired.</summary>
        public event Action<InventoryItem, int> OnItemFullyRepaired; // item, amountRepaired

        /// <summary>Fired when durability damage is taken.</summary>
        public event Action<InventoryItem, int, DurabilityDamageSource> OnDurabilityDamaged; // item, amount, source
        #endregion

        #region Enums
        public enum DurabilityChangeReason
        {
            CombatDamage = 0,
            Repair = 1,
            AutoRepair = 2,
            Restore = 3,       // Full restore (e.g., repair kit)
            Decay = 4,         // Natural decay over time
            Break = 5,         // Forced break
            DeathPenalty = 6,  // Death penalty
            EventReward = 7,   // Event reward restoration
        }

        public enum DurabilityDamageSource
        {
            EnemyAttack = 0,
            Environmental = 1,
            Trap = 2,
            SkillCost = 3,      // Skills that cost durability
            Overload = 4,       // Overload mechanics
            Corrosion = 5,      // Corrosion debuff
            Death = 6,
        }
        #endregion

        #region Public API
        /// <summary>
        /// Applies durability damage to an item.
        /// </summary>
        /// <param name="item">Target item</param>
        /// <param name="amount">Damage amount</param>
        /// <param name="source">Source of damage</param>
        /// <returns>Actual damage applied (may be less if item breaks)</returns>
        public int TakeDurabilityDamage(InventoryItem item, int amount, DurabilityDamageSource source = DurabilityDamageSource.EnemyAttack)
        {
            if (item == null || amount <= 0) return 0;
            if (!item.IsEquippable()) return 0;
            if (item.IsBroken) return 0;

            int oldDurability = item.CurrentDurability;
            int actualDamage = Math.Min(amount, item.CurrentDurability);
            item.DamageDurability(actualDamage);

            OnDurabilityChanged?.Invoke(item, oldDurability, item.CurrentDurability, DurabilityChangeReason.CombatDamage);
            OnDurabilityDamaged?.Invoke(item, actualDamage, source);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Durability);

            if (item.IsBroken)
            {
                OnItemBroken?.Invoke(item);
            }

            return actualDamage;
        }

        /// <summary>
        /// Repairs an item by a specific amount.
        /// </summary>
        /// <param name="item">Target item</param>
        /// <param name="amount">Repair amount</param>
        /// <param name="reason">Reason for repair</param>
        /// <returns>Actual amount repaired</returns>
        public int Repair(InventoryItem item, int amount, DurabilityChangeReason reason = DurabilityChangeReason.Repair)
        {
            if (item == null || amount <= 0) return 0;
            if (!item.IsEquippable()) return 0;
            if (item.CurrentDurability >= item.MaxDurability) return 0;

            int oldDurability = item.CurrentDurability;
            int actualAmount = Math.Min(amount, item.MaxDurability - item.CurrentDurability);
            item.Repair(actualAmount);

            OnDurabilityChanged?.Invoke(item, oldDurability, item.CurrentDurability, reason);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Durability);

            if (item.CurrentDurability >= item.MaxDurability)
            {
                OnItemFullyRepaired?.Invoke(item, actualAmount);
            }

            return actualAmount;
        }

        /// <summary>
        /// Fully restores an item to max durability (e.g., repair kit).
        /// </summary>
        /// <param name="item">Target item</param>
        /// <returns>Amount restored</returns>
        public int Restore(InventoryItem item)
        {
            if (item == null) return 0;
            if (!item.IsEquippable()) return 0;
            if (item.CurrentDurability >= item.MaxDurability) return 0;

            int amount = item.MaxDurability - item.CurrentDurability;
            return Repair(item, amount, DurabilityChangeReason.Restore);
        }

        /// <summary>
        /// Forces an item to break (0 durability).
        /// </summary>
        /// <param name="item">Target item</param>
        public void Break(InventoryItem item)
        {
            if (item == null) return;
            if (!item.IsEquippable()) return;
            if (item.IsBroken) return;

            int oldDurability = item.CurrentDurability;
            item.DamageDurability(item.CurrentDurability);

            OnDurabilityChanged?.Invoke(item, oldDurability, 0, DurabilityChangeReason.Break);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Durability);

            OnItemBroken?.Invoke(item);
        }

        /// <summary>
        /// Recovers a broken item to 1 durability (emergency repair).
        /// </summary>
        /// <param name="item">Target item</param>
        /// <returns>True if recovered</returns>
        public bool Recover(InventoryItem item)
        {
            if (item == null) return false;
            if (!item.IsEquippable()) return false;
            if (!item.IsBroken) return false;

            int oldDurability = item.CurrentDurability;
            item.CurrentDurability = 1;

            OnDurabilityChanged?.Invoke(item, oldDurability, 1, DurabilityChangeReason.Repair);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Durability);

            return true;
        }

        /// <summary>
        /// Applies natural decay to an item (e.g., over time).
        /// </summary>
        /// <param name="item">Target item</param>
        /// <param name="amount">Decay amount</param>
        /// <returns>Actual decay applied</returns>
        public int Decay(InventoryItem item, int amount)
        {
            if (item == null || amount <= 0) return 0;
            if (!item.IsEquippable()) return 0;
            if (item.IsBroken) return 0;

            int oldDurability = item.CurrentDurability;
            int actualDecay = Math.Min(amount, item.CurrentDurability);
            item.DamageDurability(actualDecay);

            OnDurabilityChanged?.Invoke(item, oldDurability, item.CurrentDurability, DurabilityChangeReason.Decay);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Durability);

            if (item.IsBroken)
            {
                OnItemBroken?.Invoke(item);
            }

            return actualDecay;
        }

        /// <summary>
        /// Checks if an item needs repair.
        /// </summary>
        public bool NeedsRepair(InventoryItem item) => item != null && item.IsEquippable() && item.CurrentDurability < item.MaxDurability;

        /// <summary>
        /// Checks if an item is broken.
        /// </summary>
        public bool IsBroken(InventoryItem item) => item != null && item.IsBroken;

        /// <summary>
        /// Gets durability percentage.
        /// </summary>
        public float GetDurabilityPercent(InventoryItem item) => item?.GetDurabilityPercent() ?? 0f;

        /// <summary>Durability bar color from the data-driven table.</summary>
        public Color GetDurabilityColor(InventoryItem item) => DurabilityColorTable.GetColor(GetDurabilityPercent(item));

        /// <summary>
        /// Gets missing durability points.
        /// </summary>
        public int GetMissingDurability(InventoryItem item) => item != null ? Math.Max(0, item.MaxDurability - item.CurrentDurability) : 0;
        #endregion
    }
}