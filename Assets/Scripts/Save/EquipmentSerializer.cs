using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Save
{
    /// <summary>
    /// Equipment serializer - handles serialization/deserialization of equipment data.
    /// </summary>
    public static class EquipmentSerializer
    {
        /// <summary>
        /// Serializes equipment to save data.
        /// </summary>
        public static EquipmentSaveData Serialize(IEquipmentService equipment)
        {
            if (equipment == null) return EquipmentSaveData.CreateEmpty();

            var equippedItems = new List<EquippedItemData>();

            foreach (var kvp in equipment.EquippedItems)
            {
                equippedItems.Add(new EquippedItemData
                {
                    Slot = kvp.Key,
                    Item = SerializeItem(kvp.Value)
                });
            }

            var unlockedSlots = new List<UnlockedSlotData>();
            foreach (var slot in equipment.SlotData)
            {
                if (slot.IsUnlocked)
                {
                    unlockedSlots.Add(new UnlockedSlotData
                    {
                        Slot = slot.Slot,
                        IsUnlocked = true
                    });
                }
            }

            return new EquipmentSaveData
            {
                EquippedItems = equippedItems.ToArray(),
                UnlockedSlots = unlockedSlots.ToArray(),
                LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>
        /// Deserializes equipment from save data.
        /// </summary>
        public static void Deserialize(IEquipmentService equipment, EquipmentSaveData data)
        {
            if (equipment == null || data == null) return;

            equipment.LoadFromSaveData(data);
        }

        /// <summary>
        /// Serializes a single inventory item for equipment save.
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
                IsEquipped = item.IsEquipped,
                EquippedSlot = item.EquippedSlot,
                IsNew = item.IsNew,
                AcquiredTimestamp = item.AcquiredTimestamp,
                CustomData = item.CustomData != null ? new Dictionary<string, object>(item.CustomData) : null
            };

            return serialized;
        }

        /// <summary>
        /// Validates equipment save data integrity.
        /// </summary>
        public static bool ValidateSaveData(EquipmentSaveData data, out string error)
        {
            error = string.Empty;

            if (data == null)
            {
                error = "Save data is null";
                return false;
            }

            if (data.EquippedItems == null)
            {
                error = "EquippedItems array is null";
                return false;
            }

            var seenInstanceIds = new HashSet<string>();
            var seenSlots = new HashSet<EquipmentSlot>();

            foreach (var equipData in data.EquippedItems)
            {
                if (equipData?.Item == null) continue;

                if (string.IsNullOrEmpty(equipData.Item.InstanceId))
                {
                    error = $"Equipped item in slot {equipData.Slot} has empty InstanceId";
                    return false;
                }

                if (seenInstanceIds.Contains(equipData.Item.InstanceId))
                {
                    error = $"Duplicate InstanceId: {equipData.Item.InstanceId}";
                    return false;
                }
                seenInstanceIds.Add(equipData.Item.InstanceId);

                if (seenSlots.Contains(equipData.Slot))
                {
                    error = $"Duplicate slot: {equipData.Slot}";
                    return false;
                }
                seenSlots.Add(equipData.Slot);

                if (equipData.Item.Quantity != 1)
                {
                    error = $"Equipment item {equipData.Item.InstanceId} should have quantity 1";
                    return false;
                }

                // Validate item matches slot
                if (equipData.Item.GetEquipmentType() != equipData.Slot.ToType())
                {
                    error = $"Item {equipData.Item.ItemId} type {equipData.Item.GetEquipmentType()} doesn't match slot {equipData.Slot}";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Repairs common equipment save data issues.
        /// </summary>
        public static EquipmentSaveData RepairSaveData(EquipmentSaveData data)
        {
            if (data == null) return EquipmentSaveData.CreateEmpty();

            var repaired = new EquipmentSaveData
            {
                EquippedItems = data.EquippedItems ?? Array.Empty<EquippedItemData>(),
                UnlockedSlots = data.UnlockedSlots ?? Array.Empty<UnlockedSlotData>(),
                LastModifiedTimestamp = data.LastModifiedTimestamp
            };

            var validEquipped = new List<EquippedItemData>();
            var seenIds = new HashSet<string>();
            var seenSlots = new HashSet<EquipmentSlot>();

            foreach (var equipData in repaired.EquippedItems)
            {
                if (equipData?.Item == null) continue;

                // Generate new InstanceId if empty or duplicate
                if (string.IsNullOrEmpty(equipData.Item.InstanceId) || seenIds.Contains(equipData.Item.InstanceId))
                {
                    equipData.Item.InstanceId = Guid.NewGuid().ToString();
                }
                seenIds.Add(equipData.Item.InstanceId);

                // Fix quantity
                equipData.Item.Quantity = 1;

                // Fix slot conflicts - move to first available compatible slot
                EquipmentSlot targetSlot = equipData.Slot;
                if (seenSlots.Contains(targetSlot) || equipData.Item.GetEquipmentType() != targetSlot.ToType())
                {
                    // Find first available compatible slot
                    foreach (EquipmentSlot slot in EquipmentSlotExtensions.GetAllSlots())
                    {
                        if (!seenSlots.Contains(slot) && equipData.Item.GetEquipmentType() == slot.ToType())
                        {
                            targetSlot = slot;
                            break;
                        }
                    }
                }

                equipData.Slot = targetSlot;
                seenSlots.Add(targetSlot);
                validEquipped.Add(equipData);
            }

            repaired.EquippedItems = validEquipped.ToArray();
            return repaired;
        }
    }
}