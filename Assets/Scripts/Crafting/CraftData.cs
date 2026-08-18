
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
                    AcquiredTimestamp = item.AcquiredTimestamp
                });
            }
            return results.ToArray();
        }
    }
    
    /// <summary>
    /// Root container for craft transaction journal entries.
    /// Embedded in SaveData.craftJournal.
    ///</summary>
    [Serializable]
    public class CraftJournalSaveData
    {
        public List<CraftJournalEntry> Entries = new();

        public static CraftJournalSaveData Empty => new();
    }
    
    /// <summary>
    /// Persisted CraftJob representation. Survives save/load.
    /// §15.1 persistent state; §15.2 required fields for P0-A.
    /// Legacy fields preserved for migration per §15.3.
    ///</summary>
    [Serializable]
    public class CraftJobSaveData
    {
        // ============ Identity ============
        public string JobId;
        public string RecipeId;

        // ============ P0-A: Execution Snapshot ============
        public CraftExecutionSnapshot ExecutionSnapshot;   // root aggregate
        public long? CompletionSeed;                       // mirrors ExecutionSnapshot.CompletionSeed for legacy lookup

        // ============ Timing ============
        public long StartTimeUtc;
        public long EndTimeUtc;
        public long DurationTicks;

        // ============ Quantity & Status ============
        public int Count;
        public int CompletedCount;
        public CraftJobStatus Status;

        // ============ Result ============
        public CraftResultData[] Results;
        public string FailureReason;

        // ============ Legacy Migration Field (§15.3) ============
        // Preserved for backward compatibility with pre-v3.3 saves.
        // New code reads from ExecutionSnapshot.Cost.Materials instead.
        public CraftIngredientSnapshot[] IngredientsSnapshot;
    }

}