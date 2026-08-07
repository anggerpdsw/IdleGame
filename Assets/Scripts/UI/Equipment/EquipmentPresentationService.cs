using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Builds UI view-data for equipment slots.
    /// All presentation decisions (icon, rarity color, durability, enhance text,
    /// set-bonus glow) live here so every equipment UI shares one representation.
    /// </summary>
    public static class EquipmentPresentationService
    {
        /// <summary>
        /// Builds view data for a slot from the current equip state.
        /// </summary>
        /// <param name="events">Latest equip state/event info. Device-independent: presenter never queries services.</param>
        public static EquipmentSlotViewData BuildSlot(EquipmentSlotViewSource source)
        {
            var data = new EquipmentSlotViewData();

            if (source.IsLocked)
            {
                data.State = EquipmentSlotState.Locked;
                data.UnlockState = source.UnlockState;
                data.ShowUnlockButton = source.UnlockState is EquipmentSlotUnlockState.LockedByGold or EquipmentSlotUnlockState.LockedByLevel;
                data.UnlockCost = source.UnlockCost;
                data.UnlockLabel = source.UnlockState switch
                {
                    EquipmentSlotUnlockState.LockedByLevel => $"Requires Lv.{source.RequiredLevel}",
                    EquipmentSlotUnlockState.LockedByQuest => "Requires Quest",
                    EquipmentSlotUnlockState.LockedByGold => $"Unlock {source.UnlockCost:N0}",
                    _ => string.Empty
                };
                return data;
            }

            if (source.Item == null || !source.Item.IsEquippable())
            {
                data.State = EquipmentSlotState.Empty;
                return data;
            }

            data.State = EquipmentSlotState.Occupied;

            data.ReferenceItem = source.Item;
            var def = CachedItemDefinition.Get(source.Item.ItemId);

            // Icon
            data.ShowIcon = def?.Icon != null;
            data.Icon = def?.Icon;

            // Rarity border
            data.ShowBorder = true;
            data.BorderColor = (def?.Rarity ?? source.Item.GetRarity()).GetDefaultColor();

            // Durability (data-driven color table)
            data.ShowDurability = true;
            data.Durability = source.Item.GetDurabilityPercent();
            data.DurabilityColor = DurabilityService.Instance != null
                ? DurabilityService.Instance.GetDurabilityColor(source.Item)
                : DurabilityColorTable.GetColor(data.Durability);

            // Enhance badge
            data.ShowEnhance = source.Item.EnhanceLevel > 0;
            data.EnhanceText = data.ShowEnhance ? $"+{source.Item.EnhanceLevel}" : string.Empty;

            // Set bonus glow
            data.ShowSetBonusGlow = source.SetBonusActive;

            return data;
        }
    }

    /// <summary>
    /// Services-free snapshot the presenter consumes. Producing it stays the caller's
    /// job so the presenter has zero service dependency.
    /// </summary>
    public struct EquipmentSlotViewSource
    {
        public bool IsLocked;
        public InventoryItem Item;
        public bool SetBonusActive;
        public EquipmentSlotUnlockState UnlockState;
        public long UnlockCost;
        public int RequiredLevel;
    }
}