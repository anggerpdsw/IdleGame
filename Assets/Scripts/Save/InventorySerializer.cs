using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using System.Linq;

namespace IdleDefenseSurvival.Save
{
    /// <summary>
    /// Inventory serializer - handles serialization/deserialization of inventory data.
    /// Flat items list, explicit SlotIndex, category derived from ItemId (never persisted).
    /// </summary>
    public static class InventorySerializer
    {
        /// <summary>
        /// Serializes inventory to save data.
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
        /// Validates inventory save data integrity.
        /// Identity rule: equipment entries must carry an InstanceId (unique); stackables must not
        /// (their key is KeyId = ItemId [+ StackId], instance identity is equipment-only).
        /// </summary>
        public static bool ValidateSaveData(InventorySaveData data, out string error)
        {
            error = string.Empty;

            if (data == null)
            {
                error = "Save data is null";
                return false;
            }

            if (data.Items == null)
            {
                error = "Items array is null";
                return false;
            }

            var seenEquipmentIds = new HashSet<string>();
            var seenStackKeys = new HashSet<string>();
            foreach (var item in data.Items)
            {
                if (item == null) continue;

                if (item.Quantity <= 0)
                {
                    error = $"Item {item.ItemId} has invalid quantity: {item.Quantity}";
                    return false;
                }

                if (string.IsNullOrEmpty(item.ItemId)) continue; // skipped on load

                if (string.IsNullOrEmpty(item.InstanceId))
                {
                    // Stackable entry: key must be present and unique.
                    string key = string.IsNullOrEmpty(item.KeyId) ? BuildStackKey(item) : item.KeyId;
                    if (string.IsNullOrEmpty(key))
                    {
                        error = $"Stackable item {item.ItemId} has no stack key";
                        return false;
                    }
                    if (!seenStackKeys.Add(key))
                    {
                        error = $"Duplicate stack key: {key}";
                        return false;
                    }
                }
                else
                {
                    // Equipment entry: unique instance identity.
                    if (!seenEquipmentIds.Add(item.InstanceId))
                    {
                        error = $"Duplicate InstanceId: {item.InstanceId}";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Repairs common save data issues (missing/duplicate instance ids, missing stack keys, bad quantities).
        /// </summary>
        public static InventorySaveData RepairSaveData(InventorySaveData data)
        {
            if (data == null) return InventorySaveData.CreateEmpty();

            var valid = new List<InventoryItemData>();
            var seenEquipmentIds = new HashSet<string>();
            var seenStackKeys = new HashSet<string>();

            foreach (var item in data.Items ?? Array.Empty<InventoryItemData>())
            {
                if (item == null) continue;

                if (item.Quantity <= 0)
                    item.Quantity = 1;

                if (string.IsNullOrEmpty(item.InstanceId))
                {
                    // Stackable: ensure a unique stack key exists.
                    string key = string.IsNullOrEmpty(item.KeyId) ? BuildStackKey(item) : item.KeyId;
                    if (string.IsNullOrEmpty(key) || !seenStackKeys.Add(key))
                    {
                        item.StackId = AllocStackId(seenStackKeys);
                        item.KeyId = BuildStackKey(item);
                        seenStackKeys.Add(item.KeyId);
                    }
                }
                else
                {
                    if (!seenEquipmentIds.Add(item.InstanceId))
                        item.InstanceId = Guid.NewGuid().ToString();
                }

                valid.Add(item);
            }

            return new InventorySaveData
            {
                Capacity = data.Capacity,
                LastModifiedTimestamp = data.LastModifiedTimestamp,
                Items = valid.ToArray()
            };
        }

        private static string BuildStackKey(InventoryItemData item) =>
            string.IsNullOrEmpty(item.StackId) ? item.ItemId : item.ItemId + "~" + item.StackId;

        private static string AllocStackId(HashSet<string> taken)
        {
            for (int i = 0; i < 26; i++)
            {
                string id = ((char)('a' + i)).ToString();
                if (!taken.Contains(id)) return id;
            }
            return Guid.NewGuid().ToString("N")[..4];
        }
    }
}
