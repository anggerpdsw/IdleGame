using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using UnityEngine;
using Newtonsoft.Json;

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
        public string EquipmentTemplateId; // Reference to base EquipmentData template (e.g. "equip_base")
        public EquipmentType EquipmentType = EquipmentType.None; // Persisted equipment slot type (Hat, Armor, etc.)

        // ============ Stack Identity ============
        /// <summary>'a'..'z' distinguishing stacks of the same item in different slots. Null = the canonical stack.</summary>
        public string StackId;

        // ============ Runtime State ============
        public int Quantity = 1; // For stackable items
        public int Level = 1; // Current level
        public int EnhanceLevel = 0; // Enhancement level (+0 to +20)

        // ============ Durability ============
        public int CurrentDurability = 100;
        public int MaxDurability = 100;
        public int DurabilityLossPerUse = 1; // From rarity config
        public long RepairCostPerDurability = 5; // From rarity config

        // ============ Sockets & Gems ============
        public SocketData[] Sockets; // Socket states (can be null/empty)
        public int MaxSockets = 0; // Max sockets from rarity config (derived from Sockets.Length at generation)

        // ============ Custom Data (for derived values like sell price) ============
        [JsonIgnore]
        public Dictionary<string, object> CustomData;

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
        [JsonIgnore] public bool IsEquipped = false;
        [JsonIgnore] public EquipmentType EquippedSlot = EquipmentType.None;

        // ============ Computed Properties (NOT serialized - [JsonIgnore]) ============
        [JsonIgnore] public bool IsStackable =>
            ItemDatabase.Instance != null && ItemDatabase.Instance.GetItem(ItemId)?.StackSize > 1;
        [JsonIgnore] public bool IsMaxStack => Quantity >= GetMaxStackSize();
        [JsonIgnore] public bool IsBroken => CurrentDurability <= 0;
        [JsonIgnore] public bool CanEnhance => EnhanceLevel < GetMaxEnhanceLevel();
        [JsonIgnore] public bool HasSockets => Sockets != null && Sockets.Length > 0;
        [JsonIgnore] public int FilledSocketCount => Sockets?.Count(s => s?.GemId != null) ?? 0;
        [JsonIgnore] public int EmptySocketCount => Sockets?.Count(s => s?.GemId == null) ?? 0;

        /// <summary>
        /// Gets the equipment type from the base equipment template.
        /// For crafted equipment using equip_base, this falls back to equipment type stored in ItemId pattern or EquipmentTemplateId.
        /// </summary>
        public EquipmentType GetEquipmentType()
        {
            // Direct field value takes precedence – set during generation.
            if (EquipmentType != EquipmentType.None) return EquipmentType;

            if (ItemDatabase.Instance == null) return EquipmentType.None;

            // First try: lookup from ItemDatabase by ItemId
            var itemData = ItemDatabase.Instance.GetItem(ItemId);
            if (itemData is EquipmentData equip) return equip.EquipmentType;

            // Second try: infer from ItemId naming pattern (e.g., "cotton_hat" -> Hat)
            if (!string.IsNullOrEmpty(ItemId))
            {
                var lowerId = ItemId.ToLowerInvariant();
                if (lowerId.Contains("_hat")) return EquipmentType.Hat;
                if (lowerId.Contains("_gloves")) return EquipmentType.Gloves;
                if (lowerId.Contains("_cape")) return EquipmentType.Cape;
                if (lowerId.Contains("_armor")) return EquipmentType.Armor;
                if (lowerId.Contains("_belt")) return EquipmentType.Belt;
                if (lowerId.Contains("_pants")) return EquipmentType.Pants;
                if (lowerId.Contains("_pendant")) return EquipmentType.Pendant;
                if (lowerId.Contains("_earring")) return EquipmentType.Earring;
                if (lowerId.Contains("_bracelet")) return EquipmentType.Bracelet;
                if (lowerId.Contains("_ring")) return EquipmentType.Ring;
                if (lowerId.Contains("_shoes")) return EquipmentType.Shoes;
            }

            // Third try: check EquipmentTemplateId (for backward compatibility with old templates)
            if (!string.IsNullOrEmpty(EquipmentTemplateId))
            {
                var template = ItemDatabase.Instance.GetEquipment(EquipmentTemplateId);
                if (template != null) return template.EquipmentType;

                var lowerTemplate = EquipmentTemplateId.ToLowerInvariant();
                if (lowerTemplate.Contains("hat")) return EquipmentType.Hat;
                if (lowerTemplate.Contains("gloves")) return EquipmentType.Gloves;
                if (lowerTemplate.Contains("cape")) return EquipmentType.Cape;
                if (lowerTemplate.Contains("armor")) return EquipmentType.Armor;
                if (lowerTemplate.Contains("belt")) return EquipmentType.Belt;
                if (lowerTemplate.Contains("pants")) return EquipmentType.Pants;
                if (lowerTemplate.Contains("pendant")) return EquipmentType.Pendant;
                if (lowerTemplate.Contains("earring")) return EquipmentType.Earring;
                if (lowerTemplate.Contains("bracelet")) return EquipmentType.Bracelet;
                if (lowerTemplate.Contains("ring")) return EquipmentType.Ring;
                if (lowerTemplate.Contains("shoes")) return EquipmentType.Shoes;
            }

            return EquipmentType.None;
        }

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
                    clone.Sockets[i] = Sockets[i]?.Clone();
            }
            if (Enchantment != null)
                clone.Enchantment = Enchantment.Clone();
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
    public class SocketData : ISerializationCallbackReceiver
    {
        /// <summary>0-based index (runtime only — re-derived from array position, never saved).</summary>
        [JsonIgnore] public int SocketIndex;

        /// <summary>ID of socketed gem (transient, restored from the persisted GemInstanceData via GemInstanceId).</summary>
        [JsonIgnore] public string GemId { get; set; }

        public bool IsUnlocked = true; // Socket unlocked (some only unlock at higher enhance)

        /// <summary>InstanceId of the GemInstanceData for this socket. Null = empty socket.</summary>
        private string _gemInstanceId;
        public string GemInstanceId
        {
            get => _gemInstanceId;
            set => _gemInstanceId = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>StackId of the inventory stack the socketed gem came from — unsocket returns gems to their own (split) stack.</summary>
        [JsonIgnore] public string StackId;

        [JsonIgnore] public int GemLevel = 1; // runtime; from GemInstanceData
        [JsonIgnore] public bool IsLocked = false; // runtime; anti-destroy guard

        [JsonIgnore] // computed, not saved
        public bool IsEmpty => string.IsNullOrEmpty(GemInstanceId) && string.IsNullOrEmpty(GemId);

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(_gemInstanceId))
                _gemInstanceId = null;
        }

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