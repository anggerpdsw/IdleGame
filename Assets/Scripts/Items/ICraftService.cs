using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Interface for craft service - handles crafting recipes, queue management, and progression.
    /// </summary>
    public interface ICraftService
    {
        // ============ Events ============
        event Action<string, bool> OnCraftCompleted;         // recipeId, success
        event Action<string, InventoryItem[]> OnCraftResult;  // recipeId, result items
        event Action<string> OnCraftStarted;                  // recipeId
        event Action<string, float> OnCraftProgress;          // recipeId, progress (0-1)
        event Action<int> OnQueueChanged;                     // queue count
        event Action<int> OnCraftingLevelChanged;             // new level
        event Action<long> OnCraftingExpChanged;              // new exp

        // ============ Properties ============
        IReadOnlyDictionary<string, CraftRecipeData> Recipes { get; }
        IReadOnlyList<CraftQueueSlot> CraftQueue { get; }
        int MaxQueueSlots { get; }
        int CurrentQueueCount { get; }
        bool IsCrafting { get; }
        int CraftingLevel { get; }
        long CraftingExp { get; }
        long CraftingExpToNextLevel { get; }
        float CraftingSpeedMultiplier { get; } // From upgrades, VIP, etc.

        // ============ Recipe Management ============
        /// <summary>Registers a craft recipe.</summary>
        void RegisterRecipe(CraftRecipeData recipe);

        /// <summary>Unregisters a craft recipe.</summary>
        bool UnregisterRecipe(string recipeId);

        /// <summary>Gets a recipe by ID.</summary>
        CraftRecipeData GetRecipe(string recipeId);

        /// <summary>Gets all recipes that can produce a specific item.</summary>
        IReadOnlyList<CraftRecipeData> GetRecipesForItem(string itemId);

        /// <summary>Gets all recipes for a category.</summary>
        IReadOnlyList<CraftRecipeData> GetRecipesByCategory(ItemCategory category);

        /// <summary>Gets all recipes the player has discovered/unlocked.</summary>
        IReadOnlyList<CraftRecipeData> GetUnlockedRecipes();

        /// <summary>Unlocks a recipe (e.g., from quest, tier, or discovery).</summary>
        bool UnlockRecipe(string recipeId);

        /// <summary>Checks if a recipe is unlocked.</summary>
        bool IsRecipeUnlocked(string recipeId);

        // ============ Crafting Operations ============
        /// <summary>Checks if a recipe can be crafted (materials, level, queue space).</summary>
        bool CanCraft(string recipeId, out string reason);

        /// <summary>Starts crafting a recipe (adds to queue if timed, executes immediately if instant).</summary>
        bool StartCraft(string recipeId, int quantity = 1);

        /// <summary>Starts crafting multiple recipes in batch.</summary>
        int StartCraftBatch(IEnumerable<(string recipeId, int quantity)> crafts);

        /// <summary>Completes a craft immediately by spending gems (instant finish).</summary>
        bool InstantComplete(string recipeId, long gemCost = 0);

        /// <summary>Cancels a craft in the queue (refunds materials based on progress).</summary>
        bool CancelCraft(string queueId, bool refund = true);

        /// <summary>Clears all completed crafts from queue.</summary>
        int ClearCompletedCrafts();

        /// <summary>Reorders craft queue (move to front/back).</summary>
        bool ReorderQueue(string queueId, int newIndex);

        // ============ Queue Management ============
        /// <summary>Expands craft queue capacity (costs currency).</summary>
        bool ExpandQueue(int slots = 1);

        /// <summary>Gets cost to expand queue.</summary>
        long GetQueueExpansionCost(int currentSlots);

        // ============ Progression ============
        /// <summary>Adds crafting experience (called on craft completion).</summary>
        void AddCraftingExp(long exp);

        /// <summary>Gets crafting level progress (0-1).</summary>
        float GetCraftingLevelProgress();

        /// <summary>Gets exp required for a specific crafting level.</summary>
        long GetExpForLevel(int level);

        // ============ Statistics ============
        /// <summary>Gets total crafts completed for a recipe.</summary>
        int GetRecipeCraftCount(string recipeId);

        /// <summary>Gets total crafts completed across all recipes.</summary>
        int GetTotalCraftCount();

        /// <summary>Resets crafting statistics (for prestige/rebirth).</summary>
        void ResetStatistics();

        // ============ Persistence ============
        CraftSaveData GetSaveData();
        void LoadFromSaveData(CraftSaveData data);
        void Reset();
    }

    /// <summary>
    /// Craft queue slot - represents a single crafting job in the queue.
    /// </summary>
    [Serializable]
    public class CraftQueueSlot
    {
        public string QueueId;           // Unique ID for this queue entry
        public string RecipeId;          // Recipe being crafted
        public int Quantity;             // Total quantity to craft
        public int CompletedQuantity;    // Quantity already completed
        public float StartTime;          // When crafting started (Time.time)
        public float Duration;           // Total duration for one craft
        public float Progress;           // Current progress (0-1) for current item
        public CraftQueueState State;    // Current state
        public long ExpReward;           // Exp awarded per craft
        public bool AutoCollect;         // Auto-collect results

        public bool IsComplete => State == CraftQueueState.Completed;
        public bool IsActive => State == CraftQueueState.Crafting;
        public bool IsQueued => State == CraftQueueState.Queued;
        public float TimeRemaining => IsActive ? Duration * (1f - Progress) : 0f;
        public int RemainingQuantity => Quantity - CompletedQuantity;
    }

    public enum CraftQueueState
    {
        Queued = 0,      // Waiting for queue slot
        Crafting = 1,    // Currently crafting
        Completed = 2,   // Finished, waiting for collection
        Cancelled = 3,   // Cancelled by player
        Failed = 4       // Failed (insufficient materials, etc.)
    }

    /// <summary>
    /// Save data for craft service.
    /// </summary>
    [Serializable]
    public class CraftSaveData
    {
        public int Version = 1;
        public long LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public int CraftingLevel = 1;
        public long CraftingExp = 0;
        public int MaxQueueSlots = 1;
        public HashSet<string> UnlockedRecipeIds = new();
        public CraftQueueSlotData[] QueueSlots = Array.Empty<CraftQueueSlotData>();
        public Dictionary<string, int> RecipeCraftCounts = new();
        public int TotalCraftsCompleted = 0;
    }

    [Serializable]
    public class CraftQueueSlotData
    {
        public string QueueId;
        public string RecipeId;
        public int Quantity;
        public int CompletedQuantity;
        public float StartTime;
        public float Duration;
        public float Progress;
        public CraftQueueState State;
        public long ExpReward;
        public bool AutoCollect;
    }
}