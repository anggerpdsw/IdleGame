using System;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Represents a single crafting job in the queue.
    /// Minimal persistence: only JobId, RecipeId, RecipeVersion, CompletionSeed, EndTimeUtc, DurationTicks, Count.
    /// State (Queued/Crafting/ReadyToClaim) computed from EndTimeUtc and current time.
    /// No StartTimeUtc, no persisted Status, no Results.
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
        // EndTimeUtc == 0 means job is queued (not started)
        // EndTimeUtc > 0 means job started; EndTimeUtc = startTime + DurationTicks
        public long EndTimeUtc;              // 0 = queued; >0 = absolute end time in UTC ticks
        public long DurationTicks;           // Total craft time in ticks (100ns units)

        // ============ Quantity ============
        public int Count = 1;                // Number of items to craft (batch support)

        // ============ Computed State (not serialized) ============
        public bool IsQueued => EndTimeUtc == 0;
        public bool IsCrafting => EndTimeUtc > 0 && DateTime.UtcNow.Ticks < EndTimeUtc;
        public bool IsReadyToClaim => EndTimeUtc > 0 && DateTime.UtcNow.Ticks >= EndTimeUtc;

        public float Progress
        {
            get
            {
                if (IsQueued) return 0f;
                long now = DateTime.UtcNow.Ticks;
                long start = EndTimeUtc - DurationTicks;
                if (now <= start) return 0f;
                if (now >= EndTimeUtc) return 1f;
                return (float)(now - start) / DurationTicks;
            }
        }

        public TimeSpan GetTimeRemaining()
        {
            if (IsQueued) return TimeSpan.Zero;
            long remaining = EndTimeUtc - DateTime.UtcNow.Ticks;
            return TimeSpan.FromTicks(Math.Max(0, remaining));
        }

        // ============ Helper Methods ============
        public static CraftJob Create(string recipeId, int count, long durationTicks, int recipeVersion, long completionSeed)
        {
            return new CraftJob
            {
                JobId = Guid.NewGuid().ToString(),
                RecipeId = recipeId,
                RecipeVersion = recipeVersion,
                CompletionSeed = completionSeed,
                EndTimeUtc = 0, // Queued initially
                DurationTicks = durationTicks,
                Count = count
            };
        }

        /// <summary>
        /// Starts the job by setting EndTimeUtc to now + DurationTicks.
        /// Call only when a concurrent slot is available.
        /// </summary>
        public void Start()
        {
            if (EndTimeUtc != 0) return; // Already started
            EndTimeUtc = DateTime.UtcNow.Ticks + DurationTicks;
        }

        /// <summary>
        /// Compatibility property for UI - derives status from time-based state.
        /// Not serialized.
        /// </summary>
        public CraftJobStatus Status
        {
            get
            {
                if (IsQueued) return CraftJobStatus.Queued;
                if (IsReadyToClaim) return CraftJobStatus.Complete;
                return CraftJobStatus.Crafting;
            }
        }
    }

    /// <summary>
    /// Status of a craft job (computed, not persisted).
    /// </summary>
    public enum CraftJobStatus
    {
        Queued = 0,
        Crafting = 1,
        Complete = 2, // Means "Ready to Claim" in new lifecycle
        Cancelled = 3,
        Failed = 4
    }
}