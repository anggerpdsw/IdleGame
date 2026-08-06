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
        /// Checks if the item is a gem.
        /// </summary>
        public static bool IsGem(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId);
            return itemData?.Category == ItemCategory.Gem;
        }

        /// <summary>
        /// Gets the set ID of the item (for equipment).
        /// </summary>
        public static string GetSetId(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId) as EquipmentData;
            return itemData?.SetId ?? string.Empty;
        }

        /// <summary>
        /// Gets the item category.
        /// </summary>
        public static ItemCategory GetItemCategory(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId);
            return itemData?.Category ?? ItemCategory.None;
        }

        /// <summary>
        /// Gets the item rarity.
        /// </summary>
        public static ItemRarity GetRarity(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId);
            return itemData?.ItemRarity ?? ItemRarity.Common;
        }

        /// <summary>
        /// Checks if the item is equippable.
        /// </summary>
        public static bool IsEquippable(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId);
            return itemData?.Category == ItemCategory.Equipment;
        }

        /// <summary>
        /// Gets the equipment type.
        /// </summary>
        public static EquipmentType GetEquipmentType(this InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item?.ItemId) as EquipmentData;
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