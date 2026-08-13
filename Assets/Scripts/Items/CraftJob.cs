using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Represents a single crafting job in the queue.
    /// Uses unique JobId (Guid) to support multiple concurrent crafts of same recipe.
    /// </summary>
    [Serializable]
    public class CraftJob
    {
        // ============ Identity ============
        public string JobId;                 // Unique GUID for this craft job
        public string RecipeId;              // Reference to recipe

        // ============ Execution Snapshot (P0-A) ============
        // Immutable root containing RecipeSnapshot, CostSnapshot, Context, CraftCount, CompletionSeed.
        // Built at StartCraft; RecipeVersion is derived via ExecutionSnapshot.Recipe.RecipeVersion (§16.3).
        public CraftExecutionSnapshot ExecutionSnapshot;
        public long? CompletionSeed;         // mirrors ExecutionSnapshot.CompletionSeed for legacy lookup (I-21, I-20)

        // ============ Timing (UTC ticks for persistence) ============
        public long StartTimeUtc;            // DateTime.UtcNow.Ticks when started
        public long EndTimeUtc;              // StartTimeUtc + DurationTicks
        public long DurationTicks;           // Total craft time in ticks (100ns units)

        // ============ Quantity & Status ============
        public int Count = 1;                // Number of items to craft (batch support)
        public int CompletedCount = 0;       // How many have completed (for batch)
        public CraftJobStatus Status = CraftJobStatus.Queued;

        // ============ Progress ============
        // Computed property - not serialized
        public float Progress => Status == CraftJobStatus.Complete ? 1f :
                                Status == CraftJobStatus.Cancelled ? 0f :
                                (float)Math.Max(0, Math.Min(1, (DateTime.UtcNow.Ticks - StartTimeUtc) / (double)DurationTicks));

        public bool IsComplete => Status == CraftJobStatus.Complete;
        public bool IsActive => Status == CraftJobStatus.Crafting;
        public bool IsPending => Status == CraftJobStatus.Queued;

        // ============ Result ============
        public CraftResultData[] Results;    // Generated results when complete
        public string FailureReason;         // If failed

        // ============ Snapshot (immutable at creation) ============
        // Frozen copy of recipe ingredients scaled by job.Count, captured when the job was created.
        // Survives recipe/database mutations so refund and audit paths see what was actually consumed.
        // DecomposedRequirementsSnapshot[] is intentionally absent — no runtime resolver/aggregator exists.
        public CraftIngredientSnapshot[] IngredientsSnapshot;

        // ============ Helper Methods ============
        public static CraftJob Create(string recipeId, int count, long durationTicks)
        {
            var now = DateTime.UtcNow.Ticks;
            return new CraftJob
            {
                JobId = Guid.NewGuid().ToString(),
                RecipeId = recipeId,
                StartTimeUtc = now,
                DurationTicks = durationTicks,
                EndTimeUtc = now + durationTicks,
                Count = count,
                Status = CraftJobStatus.Queued
            };
        }

        /// <summary>
        /// Overload that seeds the job with a pre-built immutable <see cref="CraftExecutionSnapshot"/>
        /// and an ingredients snapshot. Used by <see cref="CraftSnapshotBuilder"/> flow to ensure
        /// the same snapshot object is shared between journal and job — single source of truth (P0-C).
        /// JobId is generated here; no queue/journal logic added.
        ///</summary>
        public static CraftJob Create(
            string recipeId,
            int count,
            long durationTicks,
            CraftExecutionSnapshot snapshot,
            CraftIngredientSnapshot[] ingredientsSnapshot)
        {
            var now = DateTime.UtcNow.Ticks;
            return new CraftJob
            {
                JobId = Guid.NewGuid().ToString(),
                RecipeId = recipeId,
                StartTimeUtc = now,
                DurationTicks = durationTicks,
                EndTimeUtc = now + durationTicks,
                Count = count,
                Status = CraftJobStatus.Queued,
                ExecutionSnapshot = snapshot,
                CompletionSeed = snapshot?.CompletionSeed,
                IngredientsSnapshot = ingredientsSnapshot
            };
        }

        public TimeSpan GetTimeRemaining()
        {
            if (IsComplete || Status == CraftJobStatus.Cancelled) return TimeSpan.Zero;
            var remaining = EndTimeUtc - DateTime.UtcNow.Ticks;
            return remaining > 0 ? TimeSpan.FromTicks(remaining) : TimeSpan.Zero;
        }

        public void MarkCrafting()
        {
            Status = CraftJobStatus.Crafting;
        }

        public void MarkComplete(CraftResultData[] results)
        {
            Status = CraftJobStatus.Complete;
            Results = results;
            CompletedCount = Count;
        }

        public void MarkCancelled(string reason)
        {
            Status = CraftJobStatus.Cancelled;
            FailureReason = reason;
        }

        public void MarkFailed(string reason)
        {
            Status = CraftJobStatus.Failed;
            FailureReason = reason;
        }
    }

    /// <summary>
    /// Status of a craft job.
    /// </summary>
    public enum CraftJobStatus
    {
        Queued = 0,               // Waiting to start (for future queue priority system)
        Crafting = 1,             // Currently in progress
        Complete = 2,             // Finished successfully
        Cancelled = 3,            // Cancelled by player
        Failed = 4,               // Failed (insufficient resources, etc.)
        RewardPendingCommit = 5   // Two-phase completion: Results+Seed durable, Phase B in flight (I-20, §13.2)
    }

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
}