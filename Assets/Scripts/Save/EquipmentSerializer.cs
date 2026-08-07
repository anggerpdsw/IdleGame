using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Save
{
    /// <summary>
    /// Equipment serializer - handles serialization/deserialization of equipment data.
    /// v4: EquippedItems stores only (Slot, InstanceId) - items live in inventory.
    /// </summary>
    public static class EquipmentSerializer
    {
        /// <summary>
        /// Serializes equipment to save data.
        /// </summary>
        public static EquipmentSaveData Serialize(IEquipmentService equipment)
        {
            if (equipment == null) return EquipmentSaveData.CreateEmpty();

            var equippedItems = new List<EquipmentInstanceIdData>();

            foreach (var kvp in equipment.EquippedItems)
            {
                equippedItems.Add(new EquipmentInstanceIdData
                {
                    Slot = kvp.Key,
                    InstanceId = kvp.Value?.InstanceId
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
            var seenSlots = new HashSet<EquipmentType>();

            foreach (var equipData in data.EquippedItems)
            {
                if (equipData == null || string.IsNullOrEmpty(equipData.InstanceId)) continue;

                if (seenInstanceIds.Contains(equipData.InstanceId))
                {
                    error = $"Duplicate InstanceId: {equipData.InstanceId}";
                    return false;
                }
                seenInstanceIds.Add(equipData.InstanceId);

                if (seenSlots.Contains(equipData.Slot))
                {
                    error = $"Duplicate slot: {equipData.Slot}";
                    return false;
                }
                seenSlots.Add(equipData.Slot);
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
                EquippedItems = data.EquippedItems ?? Array.Empty<EquipmentInstanceIdData>(),
                UnlockedSlots = data.UnlockedSlots ?? Array.Empty<UnlockedSlotData>(),
                LastModifiedTimestamp = data.LastModifiedTimestamp
            };

            var validEquipped = new List<EquipmentInstanceIdData>();
            var seenIds = new HashSet<string>();
            var seenSlots = new HashSet<EquipmentType>();

            foreach (var equipData in repaired.EquippedItems)
            {
                if (equipData == null || string.IsNullOrEmpty(equipData.InstanceId)) continue;

                // Generate new InstanceId if empty or duplicate
                if (seenIds.Contains(equipData.InstanceId))
                {
                    equipData.InstanceId = Guid.NewGuid().ToString();
                }
                seenIds.Add(equipData.InstanceId);

                // Fix slot conflicts - move to first available compatible slot
                EquipmentType targetSlot = equipData.Slot;
                if (seenSlots.Contains(targetSlot))
                {
                    // Find first available slot
                    foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
                    {
                        if (!seenSlots.Contains(slot))
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
