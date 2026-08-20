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
    /// Derived fields (IsStackable, IsBroken, HasSockets, ...) are computed properties
    /// marked [JsonIgnore] so they are never serialized - they are derived from item data.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        // ============ Identity ============
        public string InstanceId; // Unique instance ID (GUID) — equipment (unique) only; null for stackables
        public string ItemId; // Reference to ItemData/EquipmentData (concrete output ID, e.g. "cotton_hat")
        public string EquipmentTemplateId; // Reference to base EquipmentData template (e.g. "equip_hat_base")

        // ============ Stack Identity ============
        /// <summary>'a'..'z' distinguishing stacks of the same item in different slots. Null = the canonical stack.</summary>
        public string StackId;

        // ============ Runtime State ============
        public int Quantity = 1; // For stackable items
        public int Level = 1; // Current level
        public int EnhanceLevel = 0; // Enhancement level (+0 to +20)

        // Advanced progression (runtime only — not persisted)
        [Newtonsoft.Json.JsonIgnore] public int LimitBreakCount = 0;
        [Newtonsoft.Json.JsonIgnore] public int RefineLevel = 0;
        [Newtonsoft.Json.JsonIgnore] public int TranscendLevel = 0;
        [Newtonsoft.Json.JsonIgnore] public int EvolutionStage = 0;
        [Newtonsoft.Json.JsonIgnore] public bool IsAwakened = false;
        [Newtonsoft.Json.JsonIgnore] public bool IsMasterwork = false;

        // ============ Durability ============
        public int CurrentDurability = 100;
        public int MaxDurability = 100;

        // ============ Sockets & Gems ============
        public SocketData[] Sockets; // Socket states (can be null/empty)

        // ============ Enchantment ============
        public EnchantmentInstanceData Enchantment; // Current enchantment data

        // ============ Attribute Data (New Equipment Attribute System) ============
        public EquipmentAttributeData AttributeData; // Main + Secondary attributes with BaseValue only

        // ============ Flags ============
        public bool IsFavorite = false; // Prevents accidental sell/destroy
        public bool IsLocked = false; // Prevents any modification
        public bool IsNew = true; // Newly acquired (for UI highlight)
        public long AcquiredTimestamp = 0; // When item was obtained (for Sort by Newest)

        // Runtime mirror of EquipmentService state - NOT saved (EquipmentService owns equip state)
        [Newtonsoft.Json.JsonIgnore]
        public bool IsEquipped = false;
        [Newtonsoft.Json.JsonIgnore]
        public EquipmentType EquippedSlot = EquipmentType.None;

        // ============ Custom Data ============
        public Dictionary<string, object> CustomData; // For modding/extensibility (no AttributeStats, OverrideItemId, ValuePerLevel, ValuePerEnhance)

        // ============ Computed Properties (NOT serialized - [JsonIgnore]) ============
        [Newtonsoft.Json.JsonIgnore] public bool IsStackable =>
            ItemDatabase.Instance != null && ItemDatabase.Instance.GetItem(ItemId)?.StackSize > 1;
        [Newtonsoft.Json.JsonIgnore] public bool IsMaxStack => Quantity >= GetMaxStackSize();
        [Newtonsoft.Json.JsonIgnore] public bool IsBroken => CurrentDurability <= 0;
        [Newtonsoft.Json.JsonIgnore] public bool CanEnhance => EnhanceLevel < GetMaxEnhanceLevel();
        [Newtonsoft.Json.JsonIgnore] public bool HasSockets => Sockets != null && Sockets.Length > 0;
        [Newtonsoft.Json.JsonIgnore] public int FilledSocketCount => Sockets?.Count(s => s?.GemId != null) ?? 0;
        [Newtonsoft.Json.JsonIgnore] public int EmptySocketCount => Sockets?.Count(s => s?.GemId == null) ?? 0;

        // ============ Methods ============
        public InventoryItem Clone()
        {
            var clone = (InventoryItem)MemberwiseClone();
            clone.InstanceId = Guid.NewGuid().ToString();
            clone.Quantity = 1; // Cloned items start as single
            clone.IsNew = true;
            clone.IsEquipped = false;
            clone.EquippedSlot = EquipmentType.None;
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
            splitItem.IsNew = false;

            // Stack identity: instance ids are equipment-only; a split stack gets a fresh StackId
            // ('a'..'z', canonical stack stays null) so both halves keep separate save keys.
            splitItem.InstanceId = null;
            splitItem.StackId = NextStackId(StackId);
            return splitItem;
        }

        /// <summary>Next free stack tag for a split: 'a'..'z', skipping the source stack's tag.</summary>
        private static string NextStackId(string source)
        {
            for (int i = 0; i < 26; i++)
            {
                string candidate = ((char)('a' + i)).ToString();
                if (candidate != source) return candidate;
            }
            return Guid.NewGuid().ToString("N")[..4];
        }
    }

    /// <summary>
    /// Socket data - represents a single socket on an equipment item.
    /// Persisted: IsUnlocked + GemInstanceId (socketed gem instance reference; null = empty).
    /// GemId/GemLevel/IsLocked/Experience live on the GemInstanceData (GemService owns them).
    /// </summary>
    [Serializable]
    public class SocketData
    {
        /// <summary>0-based index (runtime only — re-derived from array position, never saved).</summary>
        [Newtonsoft.Json.JsonIgnore] public int SocketIndex;

        /// <summary>ID of socketed gem (transient, restored from the persisted GemInstanceData via GemInstanceId).</summary>
        [Newtonsoft.Json.JsonIgnore] public string GemId;
        public bool IsUnlocked = true; // Socket unlocked (some only unlock at higher enhance)

        /// <summary>InstanceId of the GemInstanceData for this socket. Null = empty socket.</summary>
        public string GemInstanceId;

        /// <summary>StackId of the inventory stack the socketed gem came from — unsocket returns gems to their own (split) stack.</summary>
        [Newtonsoft.Json.JsonIgnore] public string StackId;

        [Newtonsoft.Json.JsonIgnore] public int GemLevel = 1; // runtime; from GemInstanceData
        [Newtonsoft.Json.JsonIgnore] public bool IsLocked = false; // runtime; anti-destroy guard

        [Newtonsoft.Json.JsonIgnore] // computed, not saved
        public bool IsEmpty => string.IsNullOrEmpty(GemInstanceId) && string.IsNullOrEmpty(GemId);

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
        public int MaxCapacity = 2000;
        public int ExpansionCostBase = 10; // Gem cost for first expansion
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

        /// <summary>Computed unlock gate for the UI (poin 10).</summary>
        public EquipmentSlotUnlockState UnlockState = EquipmentSlotUnlockState.Unlocked;

        public bool CanEquip(InventoryItem item, int playerLevel)
        {
            if (item == null || !IsUnlocked) return false;
            if (playerLevel < RequiredLevel) return false;
            if (!string.IsNullOrEmpty(RequiredQuest)) return false; // Quest check would go here
            if (item.GetEquipmentType() != Slot) return false;
            return true;
        }
    }

    /// <summary>
    /// Unlock gate for an equipment slot. UI reads this instead of branching.
    /// </summary>
    public enum EquipmentSlotUnlockState
    {
        Unlocked = 0,
        LockedByGold = 1,   // pay Gem/Gold to unlock (SlotUnlockCosts)
        LockedByLevel = 2,  // reach RequiredLevel
        LockedByQuest = 3   // complete RequiredQuest
    }
}