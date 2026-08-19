using System;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    public enum CraftJobStatus { Queued, Crafting, Complete, Cancelled, Failed }

    public class CraftJob
    {
        public string JobId;
        public string RecipeId;
        public int    RecipeVersion;
        public int    Count;
        public long   CompletionSeed;
        public long   EndTimeUtc;      // 0 = queued, > now = crafting, <= now = ready-to-claim
        public long   DurationTicks;   // total duration in ticks

        // ---------- Factory ----------
        public static CraftJob Create(string recipeId, int count, long durationTicks,
                                      int recipeVersion, long completionSeed)
        {
            return new CraftJob
            {
                JobId          = Guid.NewGuid().ToString(),
                RecipeId       = recipeId,
                Count          = Math.Max(1, count),
                DurationTicks  = Math.Max(1, durationTicks),
                RecipeVersion  = Math.Max(1, recipeVersion),
                CompletionSeed = completionSeed,
                EndTimeUtc     = 0 // queued
            };
        }

        // ---------- Status helpers ----------
        public bool IsQueued        => EndTimeUtc == 0;
        public bool IsCrafting      => EndTimeUtc > DateTime.UtcNow.Ticks;
        public bool IsReadyToClaim  => EndTimeUtc > 0 && DateTime.UtcNow.Ticks >= EndTimeUtc;

        public CraftJobStatus Status
        {
            get
            {
                if (IsQueued)        return CraftJobStatus.Queued;
                if (IsCrafting)      return CraftJobStatus.Crafting;
                if (IsReadyToClaim)  return CraftJobStatus.Complete;
                return CraftJobStatus.Failed; // fallback – should not happen
            }
        }

        // ---------- Progress ----------
        public float Progress
        {
            get
            {
                if (IsQueued) return 0f;
                if (IsReadyToClaim) return 1f;
                var elapsed = DateTime.UtcNow.Ticks - (EndTimeUtc - DurationTicks);
                return Mathf.Clamp01((float)elapsed / DurationTicks);
            }
        }

        // ---------- Control ----------
        public void Start()
        {
            // Move from queued → crafting
            EndTimeUtc = DateTime.UtcNow.Ticks + DurationTicks;
        }

        public TimeSpan GetTimeRemaining()
        {
            if (IsReadyToClaim) return TimeSpan.Zero;
            var remainingTicks = Math.Max(0, EndTimeUtc - DateTime.UtcNow.Ticks);
            return TimeSpan.FromTicks(remainingTicks);
        }
    }
}