using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.Inventory
{
    /// <summary>
    /// Represents a single item instance in the inventory.
    /// Contains runtime state that varies per instance (level, durability, sockets, etc.)
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        // ============ Identity ============
        public string InstanceId; // Unique instance ID (GUID)
        public string ItemId; // Reference to ItemData/EquipmentData

        // ============ Runtime State ============
        public int Quantity = 1; // For stackable items
        public int Level = 1; // Current level
        public int EnhanceLevel = 0; // Enhancement level (+0 to +20)
        public int LimitBreakCount = 0; // Limit break count
        public int RefineLevel = 0; // Refinement level
        public int TranscendLevel = 0; // Transcendence level
        public int EvolutionStage = 0; // Evolution stage
        public bool IsAwakened = false; // Awakening state
        public bool IsMasterwork = false; // Masterwork state

        // ============ Durability ============
        public int CurrentDurability = 100;
        public int MaxDurability = 100;

        // ============ Sockets & Gems ============
        public SocketData[] Sockets; // Socket states (can be null/empty)

        // ============ Enchantment ============
        public EnchantmentInstanceData Enchantment; // Current enchantment data

        // ============ Flags ============
        public bool IsFavorite = false; // Prevents accidental sell/destroy
        public bool IsLocked = false; // Prevents any modification
        public bool IsEquipped = false; // Currently equipped
        public EquipmentType EquippedSlot = EquipmentType.None; // Which slot it's in
        public bool IsNew = true; // Newly acquired (for UI highlight)
        public long AcquiredTimestamp = 0; // When item was obtained

        // ============ Custom Data ============
        public Dictionary<string, object> CustomData; // For modding/extensibility

        // ============ Computed Properties ============
        public bool IsStackable => Quantity > 1;
        public bool IsMaxStack => Quantity >= GetMaxStackSize();
        public bool IsBroken => CurrentDurability <= 0;
        public bool CanEnhance => EnhanceLevel < GetMaxEnhanceLevel();
        public bool CanLimitBreak => LimitBreakCount < GetMaxLimitBreak();
        public bool HasSockets => Sockets != null && Sockets.Length > 0;
        public int FilledSocketCount => Sockets?.Count(s => s?.GemId != null) ?? 0;
        public int EmptySocketCount => Sockets?.Count(s => s?.GemId == null) ?? 0;

        // ============ Methods ============
        public InventoryItem Clone()
        {
            var clone = (InventoryItem)MemberwiseClone();
            clone.InstanceId = Guid.NewGuid().ToString();
            clone.Quantity = 1; // Cloned items start as single
            clone.IsNew = true;
            clone.AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Sockets != null)
            {
                clone.Sockets = new SocketData[Sockets.Length];
                for (int i = 0; i < Sockets.Length; i++)
                {
                    clone.Sockets[i] = Sockets[i]?.Clone();
                }
            }
            if (Enchantment != null)
                clone.Enchantment = Enchantment.Clone();
            if (CustomData != null)
                clone.CustomData = new Dictionary<string, object>(CustomData);
            return clone;
        }

        public int GetMaxStackSize()
        {
            var data = ItemDatabase.Instance?.GetItem(ItemId);
            return data?.StackSize > 0 ? data.StackSize : 999;
        }

        public int GetMaxEnhanceLevel()
        {
            // Will be filled by ItemDatabase lookup
            return 20; // Default
        }

        public int GetMaxLimitBreak()
        {
            // Will be filled by ItemDatabase lookup
            return 5; // Default
        }

        public float GetDurabilityPercent() => MaxDurability > 0 ? (float)CurrentDurability / MaxDurability : 0f;

        public void Repair(int amount)
        {
            CurrentDurability = Math.Min(MaxDurability, CurrentDurability + amount);
        }

        public void DamageDurability(int amount)
        {
            CurrentDurability = Math.Max(0, CurrentDurability - amount);
        }

        public bool TryAddToStack(int amount, int maxStack)
        {
            if (!IsStackable || Quantity >= maxStack) return false;
            int canAdd = Math.Min(amount, maxStack - Quantity);
            Quantity += canAdd;
            return canAdd > 0;
        }

        public InventoryItem SplitStack(int amount)
        {
            if (!IsStackable || amount >= Quantity || amount <= 0) return null;

            var splitItem = Clone();
            splitItem.Quantity = amount;
            Quantity -= amount;
            splitItem.InstanceId = Guid.NewGuid().ToString();
            splitItem.IsNew = false;
            return splitItem;
        }
    }

    /// <summary>
    /// Socket data - represents a single socket on an equipment item.
    /// Runtime state only - config is in SocketConfigData.SocketRules
    /// </summary>
    [Serializable]
    public class SocketData
    {
        public int SocketIndex; // 0-based index
        public string GemId; // ID of socketed gem (null = empty)
        public int GemLevel = 1; // Gem level
        public bool IsLocked = false; // Prevents gem removal
        public bool IsUnlocked = true; // Socket unlocked (some sockets unlock at higher enhance)
        public string GemInstanceId; // InstanceId of the GemInstanceData for this socket

        public bool IsEmpty => string.IsNullOrEmpty(GemId);
        public bool IsFilled => !IsEmpty;

        public SocketData Clone()
        {
            return (SocketData)MemberwiseClone();
        }
    }

    /// <summary>
    /// Gem instance data for socketed gems.
    /// </summary>
    [Serializable]
    public class GemInstanceData
    {
        public string InstanceId;
        public string GemId; // Reference to GemData
        public int Level = 1;
        public int Experience = 0;
        public CombatStatEntry[] Stats; // Generated stats
        public long AcquiredTimestamp;

        public GemInstanceData Clone()
        {
            var clone = (GemInstanceData)MemberwiseClone();
            clone.InstanceId = Guid.NewGuid().ToString();
            if (Stats != null)
            {
                clone.Stats = new CombatStatEntry[Stats.Length];
                Array.Copy(Stats, clone.Stats, Stats.Length);
            }
            return clone;
        }
    }

    /// <summary>
    /// Enchantment instance data for equipment enchantments.
    /// </summary>
    [Serializable]
    public class EnchantmentInstanceData
    {
        public string EnchantmentId;
        public int Level = 1;
        public int Experience = 0;
        public CombatStatEntry[] StatBonuses;
        public SpecialEffectEntry[] Effects;
        public long AcquiredTimestamp;

        public EnchantmentInstanceData Clone()
        {
            var clone = (EnchantmentInstanceData)MemberwiseClone();
            if (StatBonuses != null)
            {
                clone.StatBonuses = new CombatStatEntry[StatBonuses.Length];
                Array.Copy(StatBonuses, clone.StatBonuses, StatBonuses.Length);
            }
            if (Effects != null)
            {
                clone.Effects = new SpecialEffectEntry[Effects.Length];
                Array.Copy(Effects, clone.Effects, Effects.Length);
            }
            return clone;
        }
    }

    /// <summary>
    /// Inventory slot - a position in the inventory grid that can hold an item.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public int SlotIndex; // Position in grid
        public InventoryItem Item; // Item in this slot (null = empty)
        public bool IsLocked = false; // Slot locked (expansion required)
        public ItemCategory AllowedCategory = ItemCategory.None; // Category filter (None = any)

        public bool IsEmpty => Item == null;
        public bool IsFull => Item != null && Item.IsMaxStack;
        public bool CanAccept(InventoryItem item) =>
            !IsLocked &&
            (AllowedCategory == ItemCategory.None || item?.GetItemCategory() == AllowedCategory) &&
            (IsEmpty || (Item.ItemId == item.ItemId && Item.IsStackable && !Item.IsMaxStack));

        public InventoryItem GetItemCategory()
        {
            // Will be resolved via ItemDatabase
            return Item;
        }
    }

    /// <summary>
    /// Inventory grid configuration.
    /// </summary>
    [Serializable]
    public class InventoryConfig
    {
        public int Width = 8;
        public int Height = 6;
        public int BaseCapacity = 48; // Width * Height
        public int MaxCapacity = 200;
        public int ExpansionCostBase = 100; // Gold cost for first expansion
        public float ExpansionCostMultiplier = 1.5f;
        public int SlotsPerExpansion = 8;

        public int GetExpansionCost(int currentExpansions)
        {
            return Mathf.RoundToInt(ExpansionCostBase * Mathf.Pow(ExpansionCostMultiplier, currentExpansions));
        }
    }

    /// <summary>
    /// Equipment slot data for the equipment UI.
    /// </summary>
    [Serializable]
    public class EquipmentSlotData
    {
        public EquipmentType Slot;
        public InventoryItem EquippedItem;
        public bool IsUnlocked = true;
        public int RequiredLevel = 1;
        public string RequiredQuest;

        public bool CanEquip(InventoryItem item, int playerLevel)
        {
            if (item == null || !IsUnlocked) return false;
            if (playerLevel < RequiredLevel) return false;
            if (!string.IsNullOrEmpty(RequiredQuest)) return false; // Quest check would go here
            if (item.GetEquipmentType() != Slot) return false;
            return true;
        }
    }
}