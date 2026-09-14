using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Decomposition;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Decomposes max-level equipment into rarity-matched materials.
    /// Rule: equipment rarity X → decomposed_X material ×1.
    /// Note: DecomposedRequirementResolver is for CRAFT requirements, not decompose output.
    /// </summary>
    public static class EquipmentDecompositionService
    {
        /// <summary>
        /// Decompose multiple max-level equipment. Returns total items granted per ItemId.
        /// </summary>
        public static bool DecomposeMultiple(List<InventoryItem> items, out Dictionary<string, int> rewards, out string reason)
        {
            rewards = new Dictionary<string, int>();
            reason = string.Empty;

            if (items == null || items.Count == 0)
            {
                reason = "Tidak ada equipment";
                return false;
            }

            // Validate all items
            foreach (var item in items)
            {
                if (!item.IsMaxLevel)
                {
                    reason = $"{item.ItemId} belum level maksimal (Lv{item.Level}/{item.MaxLevel})";
                    return false;
                }
                if (!item.IsEquippable())
                {
                    reason = $"{item.ItemId} bukan equipment";
                    return false;
                }
            }

            // Aggregate rewards: rarity X → decomposed_X material
            foreach (var item in items)
            {
                int rarity = (int)item.GetRarity();

                if (rarity == 0)
                {
                    Debug.LogError($"[Decompose] Item {item.ItemId} has invalid rarity (0/None) - cannot decompose");
                    reason = $"Equipment {item.ItemId} tidak punya rarity";
                    return false;
                }

                string materialId = DecomposedRequirementResolver.GetDecomposeOutput(rarity);

                if (materialId == null)
                {
                    Debug.LogError($"[Decompose] Unknown rarity {rarity} for {item.ItemId}");
                    continue;
                }

                if (!rewards.ContainsKey(materialId))
                    rewards[materialId] = 0;
                rewards[materialId] += 1;
            }

            // Remove from inventory
            var inventory = InventoryService.Instance;
            if (inventory == null)
            {
                reason = "InventoryService not found";
                return false;
            }

            foreach (var item in items)
            {
                int removed = inventory.RemoveItem(item.InstanceId, 1);
                if (removed == 0)
                {
                    Debug.LogWarning($"[Decompose] Failed to remove {item.InstanceId}");
                }
            }

            // Add rewards
            foreach (var kvp in rewards)
            {
                Debug.Log($"[Decompose] Adding {kvp.Key} x{kvp.Value}");
                var itemData = ItemDatabase.Instance?.GetItem(kvp.Key);
                if (itemData == null)
                {
                    Debug.LogError($"[Decompose] ItemDatabase.GetItem({kvp.Key}) returned NULL");
                    continue;
                }
                Debug.Log($"[Decompose] ItemData found: {itemData.Name}, Category={itemData.Category}");

                string result = inventory.AddItem(kvp.Key, kvp.Value);
                if (string.IsNullOrEmpty(result))
                {
                    Debug.LogError($"[Decompose] AddItem returned empty for {kvp.Key}");
                }
                else
                {
                    Debug.Log($"[Decompose] AddItem success: {result}");
                }
            }

            return true;
        }

        /// <summary>
        /// Preview rewards without consuming items.
        /// </summary>
        public static Dictionary<string, int> PreviewRewards(List<InventoryItem> items)
        {
            var rewards = new Dictionary<string, int>();
            if (items == null) return rewards;

            foreach (var item in items)
            {
                if (!item.IsMaxLevel || !item.IsEquippable()) continue;

                int rarity = (int)item.GetRarity();
                if (rarity == 0) continue;

                string materialId = DecomposedRequirementResolver.GetDecomposeOutput(rarity);
                if (materialId == null) continue;

                if (!rewards.ContainsKey(materialId))
                    rewards[materialId] = 0;
                rewards[materialId] += 1;
            }
            return rewards;
        }
    }
}
