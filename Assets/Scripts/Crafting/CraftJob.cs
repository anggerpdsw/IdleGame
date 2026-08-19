using System;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Represents a single crafting job in the queue.
    /// Minimal, recipe-driven design: only runtime state persisted.
    /// Recipe data (equipment type, rarity, ingredients, cost, duration) resolved from RecipeId at runtime.
    /// </summary>
    [Serializable]
    public class CraftJob
    {
        // ============ Identity ============
        public string JobId;                 // Unique GUID for this craft job
        public string RecipeId;              // Reference to recipe (single source of truth)
        public int RecipeVersion = 1;        // Recipe version at job creation (for migration/compatibility)

        // ============ Deterministic Completion ============
        public long CompletionSeed;          // Seed generated at job creation for deterministic results

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

        // ============ Helper Methods ============
        public static CraftJob Create(string recipeId, int count, long durationTicks, int recipeVersion, long completionSeed)
        {
            var now = DateTime.UtcNow.Ticks;
            return new CraftJob
            {
                JobId = Guid.NewGuid().ToString(),
                RecipeId = recipeId,
                RecipeVersion = recipeVersion,
                CompletionSeed = completionSeed,
                StartTimeUtc = now,
                DurationTicks = durationTicks,
                EndTimeUtc = now + durationTicks,
                Count = count,
                Status = CraftJobStatus.Queued
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

        public void MarkRewardPendingCommit()
        {
            Status = CraftJobStatus.RewardPendingCommit;
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

}