using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using System.Linq;

namespace IdleDefenseSurvival.Save
{
    /// <summary>
    /// Inventory serializer - handles serialization/deserialization of inventory data.
    /// v4: Config not saved, SlotIndex not saved (array index is the index), derived fields not serialized.
    /// </summary>
    public static class InventorySerializer
    {
        /// <summary>
        /// Serializes inventory to categorized save data (v5+).
        /// Each tab keeps only the fields that tab needs (see InventoryItem.TrimForSave).
        /// </summary>
        public static InventorySaveData Serialize(IInventoryService inventory)
        {
            return inventory?.GetSaveData() ?? InventorySaveData.CreateEmpty();
        }

        /// <summary>
        /// Deserializes inventory from save data.
        /// </summary>
        public static void Deserialize(IInventoryService inventory, InventorySaveData data)
        {
            if (inventory == null || data == null) return;

            inventory.LoadFromSaveData(data);
        }

        /// <summary>
        /// Validates inventory save data integrity across every category.
        /// </summary>
        public static bool ValidateSaveData(InventorySaveData data, out string error)
        {
            error = string.Empty;

            if (data == null)
            {
                error = "Save data is null";
                return false;
            }

            if (!data.IsCategorized)
            {
                // Legacy flat save - acceptable during migration.
                return true;
            }

            var seenInstanceIds = new HashSet<string>();
            foreach (var slotData in data.AllSlotsFlattened)
            {
                if (slotData?.Item == null) continue;

                if (string.IsNullOrEmpty(slotData.Item.InstanceId))
                {
                    error = "Item has empty InstanceId";
                    return false;
                }

                if (seenInstanceIds.Contains(slotData.Item.InstanceId))
                {
                    error = $"Duplicate InstanceId: {slotData.Item.InstanceId}";
                    return false;
                }
                seenInstanceIds.Add(slotData.Item.InstanceId);

                if (slotData.Item.Quantity <= 0)
                {
                    error = $"Item {slotData.Item.InstanceId} has invalid quantity: {slotData.Item.Quantity}";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Repairs common save data issues. Rebuilds categorized layout from valid slots.
        /// </summary>
        public static InventorySaveData RepairSaveData(InventorySaveData data)
        {
            if (data == null) return InventorySaveData.CreateEmpty();

            var categorized = InventoryCategorizedSlots.CreateEmpty();
            var seenIds = new HashSet<string>();

            // Iterate existing groups in place so items keep their tab assignment.
            foreach (var tab in new[] { TabType.Equipment, TabType.Consumables, TabType.Materials, TabType.Gems, TabType.Other })
            {
                var valid = new List<InventorySlotData>();
                var group = data.CategorizedSlots?.GetSlots(tab);
                if (group == null) continue;

                foreach (var slotData in group)
                {
                    if (slotData?.Item == null) continue;

                    if (string.IsNullOrEmpty(slotData.Item.InstanceId) || seenIds.Contains(slotData.Item.InstanceId))
                        slotData.Item.InstanceId = Guid.NewGuid().ToString();
                    seenIds.Add(slotData.Item.InstanceId);

                    if (slotData.Item.Quantity <= 0)
                        slotData.Item.Quantity = 1;

                    valid.Add(slotData);
                }
                categorized.SetSlots(tab, valid.ToArray());
            }

            return new InventorySaveData
            {
                CurrentCapacity = data.CurrentCapacity,
                CategorizedSlots = categorized,
                LastModifiedTimestamp = data.LastModifiedTimestamp
            };
        }
    }
}
