using System;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
using UnityEngine;

namespace IdleDefenseSurvival.Inventory
{
    /// <summary>
    /// Extension methods for InventoryItem.
    /// </summary>
    public static class InventoryItemExtensions
    {
        /// <summary>
        /// Durability bar color with smooth lerp between zones:
        /// &gt;75% green, 50-75% yellow, 30-50% lerp red→yellow, &lt;30% red.
        /// </summary>
        public static Color GetDurabilityColor(this InventoryItem item)
        {
            float p = Mathf.Clamp01(item.GetDurabilityPercent());
            if (p >= 0.75f) return Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp(0.75f, 1f, p));
            if (p >= 0.5f) return Color.yellow;
            if (p >= 0.3f) return Color.Lerp(Color.red, Color.yellow, Mathf.InverseLerp(0.3f, 0.5f, p));
            return Color.red;
        }
        /// <summary>
        /// True when a stackable entry of 'item' can grow by stacking with 'other' (same item id,
        /// any stackable kind — consumables, materials, gems, CardRoll, UltimateStone, SkinShard).
        /// Unique (equipment) never stacks.
        /// </summary>
        public static bool CanStackWith(this InventoryItem item, InventoryItem other)
        {
            if (item == null || other == null || item.ItemId != other.ItemId) return false;
            if (item.IsEquippable()) return false;
            var data = ItemDatabase.Instance?.GetItem(item.ItemId);
            return data?.StackSize > 1;
        }

        /// <summary>
        /// Stable stack key for stackables (ItemId + StackId); null for unique (equipment) items.
        /// StackId ('a'..'z') keeps stacks of the same item in different slots separate; missing/null
        /// StackId = the canonical stack. Equipment/leaf items must never use this as a key.
        /// </summary>
        public static string GetStackKey(this InventoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId)) return null;
            if (item.IsEquippable()) return null;
            return string.IsNullOrEmpty(item.StackId) ? item.ItemId : item.ItemId + "~" + item.StackId;
        }

        /// <summary>
        /// Checks if the item is a gem.
        /// </summary>
        public static bool IsGem(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId);
            return itemData?.Category == ItemCategory.Gem;
        }

        /// <summary>
        /// Gets the equipment data using EquipmentTemplateId when available, falling back to ItemId.
        /// </summary>
        public static EquipmentData GetEquipmentData(this InventoryItem item)
        {
            if (item == null) return null;
            var id = !string.IsNullOrEmpty(item.EquipmentTemplateId) ? item.EquipmentTemplateId : item.ItemId;
            return ItemDatabase.Instance?.GetItem(id) as EquipmentData;
        }

        /// <summary>
        /// Gets the set ID of the item (for equipment).
        /// </summary>
        public static string GetSetId(this InventoryItem item)
        {
            var itemData = item.GetEquipmentData();
            return itemData?.SetId ?? string.Empty;
        }

        /// <summary>
        /// Gets the item category.
        /// </summary>
        public static ItemCategory GetItemCategory(this InventoryItem item)
        {
            if (item == null) return ItemCategory.None;
            // Equipment identified by runtime state, not DB lookup
            if (item.IsEquippable()) return ItemCategory.Equipment;

            // Non-equipment: query ItemDatabase directly by ItemId (not EquipmentTemplateId)
            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            return itemData?.Category ?? ItemCategory.None;
        }

        /// <summary>
        /// Gets the item rarity.
        /// </summary>
        public static Rarity GetRarity(this InventoryItem item)
        {
            var itemData = item.GetEquipmentData();
            return itemData?.ItemRarity ?? Rarity.Common;
        }
                
        /// <summary>
        /// Checks if the item is an equipment instance.
        ///
        /// Equipment identity is determined from the runtime instance state,
        /// not from a concrete ItemDatabase lookup by ItemId.
        /// Crafted equipment may use a generated ItemId such as "leather_pants"
        /// while its shared template remains "equip_base".
        /// </summary>
        public static bool IsEquippable(this InventoryItem item)
        {
            if (item == null) return false;
            if (item.EquipmentType != EquipmentType.None) return true;
            if (!string.IsNullOrEmpty(item.EquipmentTemplateId)) return true;
            return item.AttributeData != null;
        }

        /// <summary>
        /// Gets the equipment type.
        /// </summary>
        public static EquipmentType GetEquipmentType(this InventoryItem item)
        {
            var itemData = item.GetEquipmentData();
            return itemData?.EquipmentType ?? EquipmentType.None;
        }

        /// <summary>
        /// Checks if the ItemData is equippable.
        /// </summary>
        public static bool IsEquippable(this ItemData itemData)
        {
            return itemData?.Category == ItemCategory.Equipment;
        }

        /// <summary>
        /// Checks if the ItemData is a consumable.
        /// </summary>
        public static bool IsConsumable(this ItemData itemData)
        {
            return itemData?.Category == ItemCategory.Consumable ||
                   itemData?.Category == ItemCategory.Chest ||
                   itemData?.Category == ItemCategory.SkillBook ||
                   itemData?.Category == ItemCategory.UpgradeStone;
        }
    }
}