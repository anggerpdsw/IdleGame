using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using UnityEngine;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Interface for equipment service - manages equipped items and equipment logic.
    /// </summary>
    public interface IEquipmentService
    {
        // ============ Events ============
        event Action<EquipmentChangedEventArgs> OnEquipmentChanged;
        event Action<EquipmentType, InventoryItem> OnItemEquipped;
        event Action<EquipmentType, InventoryItem> OnItemUnequipped;
        event Action<EquipmentType> OnSlotUnlocked;
        event Action OnSetBonusChanged;
        event Action<EquipmentType> OnDurabilityChanged;

        // ============ Properties ============
        IReadOnlyDictionary<EquipmentType, InventoryItem> EquippedItems { get; }
        IReadOnlyList<EquipmentSlotData> SlotData { get; }
        int UnlockedSlotCount { get; }
        int TotalEquippedCount { get; }
        Dictionary<string, int> EquippedSetCounts { get; } // SetId -> piece count

        // ============ Core Operations ============
        /// <summary>Attempts to equip an item. Returns true on success.</summary>
        bool Equip(InventoryItem item, EquipmentType slot = EquipmentType.None);

        /// <summary>Attempts to equip an item by instance ID. Returns true on success.</summary>
        bool EquipByInstanceId(string instanceId, EquipmentType slot = EquipmentType.None);

        /// <summary>Unequips item from slot. Returns the unequipped item or null.</summary>
        InventoryItem Unequip(EquipmentType slot);

        /// <summary>Unequips item by instance ID. Returns the unequipped item or null.</summary>
        InventoryItem UnequipByInstanceId(string instanceId);

        /// <summary>Swaps equipment between two slots (if compatible).</summary>
        bool SwapEquipment(EquipmentType slotA, EquipmentType slotB);

        /// <summary>Auto-equips best items for all slots (based on simple stat comparison).</summary>
        int AutoEquipBest();

        /// <summary>Unequips all items.</summary>
        IReadOnlyList<InventoryItem> UnequipAll();

        // ============ Validation ============
        /// <summary>Checks if item can be equipped in slot.</summary>
        bool CanEquip(InventoryItem item, EquipmentType slot, out string reason);

        /// <summary>Checks if item meets requirements (level, tier, quests).</summary>
        bool MeetsRequirements(InventoryItem item, out string reason);

        /// <summary>Checks if slot is unlocked and available.</summary>
        bool IsSlotAvailable(EquipmentType slot);

        // ============ Slot Management ============
        /// <summary>Unlocks an equipment slot (costs currency).</summary>
        bool UnlockSlot(EquipmentType slot);

        /// <summary>Gets the cost to unlock a slot.</summary>
        long GetSlotUnlockCost(EquipmentType slot);

        /// <summary>Gets the next unlockable slot.</summary>
        EquipmentType? GetNextUnlockableSlot();

        // ============ Set Bonuses ============
        /// <summary>Gets active set bonuses for a set ID.</summary>
        IReadOnlyList<SetBonusTier> GetActiveSetBonuses(string setId);

        /// <summary>Gets all active set bonuses across all sets.</summary>
        IReadOnlyDictionary<string, IReadOnlyList<SetBonusTier>> GetAllActiveSetBonuses();

        /// <summary>Checks if a set bonus tier is active.</summary>
        bool IsSetBonusActive(string setId, int tierIndex);

        /// <summary>Gets the piece count for a set.</summary>
        int GetSetPieceCount(string setId);

        // ============ Stat Calculation ============
        /// <summary>Gets total stat bonuses from all equipped items (including set bonuses).</summary>
        Dictionary<SecondaryStat, float> GetTotalStatBonuses();

        /// <summary>Gets stat bonuses from a specific slot.</summary>
        Dictionary<SecondaryStat, float> GetSlotStatBonuses(EquipmentType slot);

        /// <summary>Gets all special effects from equipped items.</summary>
        IReadOnlyList<ActiveSpecialEffect> GetActiveSpecialEffects();

        // ============ Durability ============
        /// <summary>Damages durability on equipped items (called on hit, etc.).</summary>
        void DamageDurability(EquipmentType slot, int amount);

        /// <summary>Repairs all equipped items.</summary>
        long RepairAll();

        /// <summary>Repairs a specific slot.</summary>
        long RepairSlot(EquipmentType slot);

        /// <summary>Gets repair cost for all items.</summary>
        long GetTotalRepairCost();

        // ============ Visual ============
        /// <summary>Gets the equipped model prefab for a slot (for visual representation).</summary>
        GameObject GetEquippedModel(EquipmentType slot);

        // ============ Persistence ============
        EquipmentSaveData GetSaveData();
        void LoadFromSaveData(EquipmentSaveData data);
        void Reset();

        // ============ Comparison ============
        /// <summary>Compares an inventory item with currently equipped item in its slot.</summary>
        EquipmentComparison CompareWithEquipped(InventoryItem item);

        /// <summary>Gets the best item in inventory for a specific slot.</summary>
        InventoryItem GetBestItemForSlot(EquipmentType slot);
    }

    /// <summary>
    /// Event arguments for equipment changes.
    /// </summary>
    public class EquipmentChangedEventArgs : EventArgs
    {
        public EquipmentChangeType ChangeType;
        public EquipmentType Slot;
        public InventoryItem PreviousItem;
        public InventoryItem NewItem;
        public string SetId;
        public int PreviousSetCount;
        public int NewSetCount;

        public static EquipmentChangedEventArgs CreateEquipped(EquipmentType slot, InventoryItem item, string setId = null, int setCount = 0) =>
            new() { ChangeType = EquipmentChangeType.Equipped, Slot = slot, NewItem = item, SetId = setId, NewSetCount = setCount };

        public static EquipmentChangedEventArgs CreateUnequipped(EquipmentType slot, InventoryItem item, string setId = null, int setCount = 0) =>
            new() { ChangeType = EquipmentChangeType.Unequipped, Slot = slot, PreviousItem = item, SetId = setId, NewSetCount = setCount };

        public static EquipmentChangedEventArgs CreateSwapped(EquipmentType slotA, EquipmentType slotB, InventoryItem itemA, InventoryItem itemB) =>
            new() { ChangeType = EquipmentChangeType.Swapped, Slot = slotA, PreviousItem = itemA, NewItem = itemB };

        public static EquipmentChangedEventArgs CreateSetBonusChanged(string setId, int previousCount, int newCount) =>
            new() { ChangeType = EquipmentChangeType.SetBonusChanged, SetId = setId, PreviousSetCount = previousCount, NewSetCount = newCount };

        public static EquipmentChangedEventArgs CreateSlotUnlocked(EquipmentType slot) =>
            new() { ChangeType = EquipmentChangeType.SlotUnlocked, Slot = slot };
    }

    public enum EquipmentChangeType
    {
        Equipped = 0,
        Unequipped = 1,
        Swapped = 2,
        SetBonusChanged = 3,
        SlotUnlocked = 4,
        DurabilityChanged = 5,
        Broken = 6,
    }

    /// <summary>
    /// Active special effect with source tracking.
    /// </summary>
    [Serializable]
    public class ActiveSpecialEffect
    {
        public SpecialEffectType EffectType;
        public float Value;
        public float Chance;
        public float Cooldown;
        public EquipmentType SourceSlot;
        public string SourceItemId;
        public string SourceInstanceId;
        public bool IsActive;
        public float LastTriggerTime;

        public bool CanTrigger(float currentTime) => IsActive && (currentTime - LastTriggerTime) >= Cooldown;
    }

    /// <summary>
    /// Equipment comparison result.
    /// </summary>
    [Serializable]
    public class EquipmentComparison
    {
        public EquipmentType Slot;
        public InventoryItem CurrentItem;
        public InventoryItem NewItem;
        public Dictionary<SecondaryStat, StatComparison> StatComparisons;
        public int TotalStatImprovement; // Sum of all stat differences
        public bool IsUpgrade;
        public string[] GainedEffects;
        public string[] LostEffects;
        public string[] KeptEffects;
        public string[] GainedSetBonuses;
        public string[] LostSetBonuses;
        public float OverallScore;
        public float ScoreDifference;
        public int UpgradeStatCount;
        public int DowngradeStatCount;

        public float GetStatDifference(SecondaryStat stat)
        {
            return StatComparisons.TryGetValue(stat, out var comp) ? comp.Difference : 0f;
        }
    }

    [Serializable]
    public class StatComparison
    {
        public SecondaryStat Stat;
        public float CurrentValue;
        public float NewValue;
        public float Difference;
        public float PercentChange;
        public bool IsUpgrade => Difference > 0;
    }

    /// <summary>
    /// Save data for equipment.
    /// </summary>
    [Serializable]
    public class EquipmentSaveData
    {
        public EquippedItemData[] EquippedItems;
        public UnlockedSlotData[] UnlockedSlots;
        public long LastModifiedTimestamp;

        public static EquipmentSaveData CreateEmpty() => new()
        {
            EquippedItems = Array.Empty<EquippedItemData>(),
            UnlockedSlots = Array.Empty<UnlockedSlotData>(),
            LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    [Serializable]
    public class EquippedItemData
    {
        public EquipmentType Slot;
        public InventoryItem Item; // Full item data with instance ID
    }

    [Serializable]
    public class UnlockedSlotData
    {
        public EquipmentType Slot;
        public bool IsUnlocked;
    }
}