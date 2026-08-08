using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Validates generated items for consistency and correctness.
    /// </summary>
    public sealed class ItemValidator
    {
        public ValidationResult Validate(InventoryItem item, ItemData baseData)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (item == null)
            {
                errors.Add("Item is null");
                return new ValidationResult { IsValid = false, Errors = errors };
            }

            if (baseData == null)
            {
                errors.Add("Base data is null");
            }

            // Validate InstanceId — equipment only (stackables have no instance identity)
            if (string.IsNullOrEmpty(item.InstanceId) && baseData?.Category == ItemCategory.Equipment)
            {
                errors.Add("InstanceId is empty");
            }

            // Validate ItemId matches base data
            if (baseData != null)
            {
                string baseId = baseData.Id;
                if (baseData.Category == ItemCategory.Gem)
                {
                    // Use lookup if needed, but ItemData is not GemData
                    var gemData = ItemDatabase.Instance?.GetGem(baseData.Id);
                    if (gemData != null) baseId = gemData.GemId;
                }

                if (item.ItemId != baseId)
                {
                    errors.Add($"ItemId {item.ItemId} doesn't match base data {baseId}");
                }
            }

            // Validate Quantity
            if (item.Quantity <= 0)
            {
                errors.Add("Quantity must be positive");
            }
            else if (baseData != null && item.Quantity > baseData.StackSize && baseData.StackSize > 0)
            {
                errors.Add($"Quantity {item.Quantity} exceeds max stack {baseData.StackSize}");
            }

            // Validate Level
            if (item.Level < 1)
            {
                errors.Add("Level must be >= 1");
            }
            else if (baseData is EquipmentData equip && item.Level > equip.MaxLevel)
            {
                errors.Add($"Level {item.Level} exceeds max level {equip.MaxLevel}");
            }
            else if (baseData != null && baseData.Category == ItemCategory.Gem)
            {
                var gemData = ItemDatabase.Instance?.GetGem(baseData.Id);
                if (gemData != null && item.Level > gemData.MaxLevel)
                {
                    errors.Add($"Level {item.Level} exceeds gem max level {gemData.MaxLevel}");
                }
            }

            // Validate Durability (equipment only)
            if (baseData is EquipmentData durData && durData.MaxDurability > 0)
            {
                if (item.MaxDurability != durData.MaxDurability)
                {
                    errors.Add($"MaxDurability {item.MaxDurability} doesn't match base {durData.MaxDurability}");
                }
                if (item.CurrentDurability > item.MaxDurability)
                {
                    errors.Add("CurrentDurability exceeds MaxDurability");
                }
                if (item.CurrentDurability < 0)
                {
                    errors.Add("CurrentDurability is negative");
                }
            }

            // Validate Sockets
            if (baseData is EquipmentData equipData && equipData.MaxSockets > 0)
            {
                if (item.Sockets == null)
                {
                    errors.Add("Sockets array is null but equipment has socket slots");
                }
                else if (item.Sockets.Length != equipData.MaxSockets)
                {
                    errors.Add($"Sockets count {item.Sockets.Length} doesn't match MaxSockets {equipData.MaxSockets}");
                }
                else
                {
                    for (int i = 0; i < item.Sockets.Length; i++)
                    {
                        var socket = item.Sockets[i];
                        if (socket == null)
                        {
                            errors.Add($"Socket {i} is null");
                        }
                        else if (socket.SocketIndex != i)
                        {
                            errors.Add($"Socket {i} has wrong index {socket.SocketIndex}");
                        }
                    }
                }
            }

            // Validate Enchantment
            if (item.Enchantment != null)
            {
                if (string.IsNullOrEmpty(item.Enchantment.EnchantmentId))
                {
                    errors.Add("Enchantment has empty ID");
                }
                if (item.Enchantment.Level < 1)
                {
                    errors.Add("Enchantment level must be >= 1");
                }
                if (item.Enchantment.StatBonuses != null)
                {
                    foreach (var stat in item.Enchantment.StatBonuses)
                    {
                        if (stat.Stat == SecondaryStat.None)
                        {
                            errors.Add("Enchantment has None stat");
                        }
                    }
                }
            }

            // Validate EnhanceLevel
            if (item.EnhanceLevel < 0)
            {
                errors.Add("EnhanceLevel is negative");
            }
            else if (baseData is EquipmentData eq && item.EnhanceLevel > 20) // Default max
            {
                errors.Add($"EnhanceLevel {item.EnhanceLevel} exceeds maximum");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        public ValidationResult Validate(InventoryItem item, GemData baseGem)
        {
            // Get the ItemData for base validation (if it exists in items database)
            var itemData = ItemDatabase.Instance?.GetItem(baseGem.GemId);
            var result = Validate(item, itemData);

            // Add GemData-specific validations
            if (baseGem != null && item.Level > baseGem.MaxLevel)
            {
                if (result.IsValid)
                {
                    result.IsValid = false;
                    result.Errors = new System.Collections.Generic.List<string>(result.Errors);
                }
                result.Errors.Add($"Level {item.Level} exceeds gem max level {baseGem.MaxLevel}");
            }

            return result;
        }

        public ValidationResult Validate(InventoryItem item, EquipmentData baseEquip)
        {
            return Validate(item, baseEquip as ItemData);
        }
    }

    /// <summary>
    /// Validation result.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public System.Collections.Generic.List<string> Errors { get; set; } = new();

        public override string ToString()
        {
            return IsValid ? "Valid" : $"Invalid: {string.Join(", ", Errors)}";
        }
    }
}