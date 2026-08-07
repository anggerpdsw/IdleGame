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
        /// Serializes inventory to save data.
        /// </summary>
        public static InventorySaveData Serialize(IInventoryService inventory)
        {
            if (inventory == null) return InventorySaveData.CreateEmpty();

            var slots = new List<InventorySlotData>();

            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                if (!slot.IsEmpty)
                {
                    slots.Add(new InventorySlotData
                    {
                        Item = SerializeItem(slot.Item)
                    });
                }
            }

            return new InventorySaveData
            {
                CurrentCapacity = inventory.Capacity,
                Slots = slots.ToArray(),
                LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
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
        /// Serializes a single inventory item.
        /// Derived/computed fields are NOT serialized (stored in [NonSerialized] fields).
        /// </summary>
        private static InventoryItem SerializeItem(InventoryItem item)
        {
            if (item == null) return null;

            var serialized = new InventoryItem
            {
                InstanceId = item.InstanceId,
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                Level = item.Level,
                EnhanceLevel = item.EnhanceLevel,
                LimitBreakCount = item.LimitBreakCount,
                RefineLevel = item.RefineLevel,
                TranscendLevel = item.TranscendLevel,
                EvolutionStage = item.EvolutionStage,
                IsAwakened = item.IsAwakened,
                IsMasterwork = item.IsMasterwork,
                CurrentDurability = item.CurrentDurability,
                MaxDurability = item.MaxDurability,
                Sockets = item.Sockets?.Select(s => s?.Clone()).ToArray(),
                Enchantment = item.Enchantment?.Clone(),
                IsFavorite = item.IsFavorite,
                IsLocked = item.IsLocked,
                IsNew = item.IsNew,
                AcquiredTimestamp = item.AcquiredTimestamp,
                CustomData = item.CustomData != null ? new Dictionary<string, object>(item.CustomData) : null
            };

            return serialized;
        }

        /// <summary>
        /// Validates inventory save data integrity.
        /// </summary>
        public static bool ValidateSaveData(InventorySaveData data, out string error)
        {
            error = string.Empty;

            if (data == null)
            {
                error = "Save data is null";
                return false;
            }

            if (data.Slots == null)
            {
                error = "Slots array is null";
                return false;
            }

            var seenInstanceIds = new HashSet<string>();
            foreach (var slotData in data.Slots)
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
        /// Repairs common save data issues.
        /// </summary>
        public static InventorySaveData RepairSaveData(InventorySaveData data)
        {
            if (data == null) return InventorySaveData.CreateEmpty();

            var repaired = new InventorySaveData
            {
                CurrentCapacity = data.CurrentCapacity,
                Slots = data.Slots ?? Array.Empty<InventorySlotData>(),
                LastModifiedTimestamp = data.LastModifiedTimestamp
            };

            // Remove null items and fix indices
            var validSlots = new List<InventorySlotData>();
            var seenIds = new HashSet<string>();

            foreach (var slotData in repaired.Slots)
            {
                if (slotData?.Item == null) continue;

                // Generate new InstanceId if empty or duplicate
                if (string.IsNullOrEmpty(slotData.Item.InstanceId) || seenIds.Contains(slotData.Item.InstanceId))
                {
                    slotData.Item.InstanceId = Guid.NewGuid().ToString();
                }
                seenIds.Add(slotData.Item.InstanceId);

                // Fix quantity
                if (slotData.Item.Quantity <= 0)
                    slotData.Item.Quantity = 1;

                validSlots.Add(slotData);
            }

            repaired.Slots = validSlots.ToArray();
            return repaired;
        }
    }
}
