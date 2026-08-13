using System;

namespace IdleDefenseSurvival.Items
{
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
