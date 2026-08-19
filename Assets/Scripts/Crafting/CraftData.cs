
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Generation;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Serialized craft result for persistence.
    /// </summary>
    [Serializable]
    public class CraftResultData
    {
        public string ItemId;
        public int Count;
        public int Level;
        public int Quality;
        public bool IsCritical;
        public string Source; // "Normal", "Critical", "Bonus", "Mastery", "Event"
        public long AcquiredTimestamp;
        /// <summary>Rolled instance data (AttributeStats/secondaries/affixes). v3.8 §20.5 — must survive the rebuild chain.</summary>
        public Dictionary<string, object> CustomData;

        public static CraftResultData[] FromInventoryItems(InventoryItem[] items, long expReward = 0)
        {
            if (items == null || items.Length == 0) return Array.Empty<CraftResultData>();

            var results = new List<CraftResultData>();
            foreach (var item in items)
            {
                results.Add(new CraftResultData
                {
                    ItemId = item.ItemId,
                    Count = item.Quantity,
                    Level = item.Level,
                    Quality = (int)item.GetRarity(),
                    IsCritical = false, // Would need to track this from roll result
                    Source = "Normal",
                    AcquiredTimestamp = item.AcquiredTimestamp,
                    CustomData = item.CustomData != null ? new Dictionary<string, object>(item.CustomData) : null
                });
            }
            return results.ToArray();
        }
    }

    /// <summary>
    /// Per-resource scaled cost entry. Count = per-unit * jobCount.
    /// Used for read-only material cost previews.
    /// </summary>
    [Serializable]
    public struct IngredientCost
    {
        public string ItemId;
        public int Count;
    }


}