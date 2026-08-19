using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Crafting;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Craft service orchestrator - coordinates all crafting subsystems.
    /// Uses: CraftRecipeRepository, CraftValidator, CraftTransactionService,
    /// CraftQueueService, CraftRollService, CraftRewardService, CraftPersistenceService
    /// </summary>
    public sealed class CraftingManager : MonoBehaviour
    {
        #region Singleton
        private static CraftingManager _instance;
        public static CraftingManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        #endregion

        #region Events
        public event Action<string> OnJobStarted;              // jobId (crafting began)
        public event Action<string, float> OnJobProgress;      // jobId, progress (0-1)
        public event Action<string> OnJobReadyToClaim;         // jobId (timer finished)
        public event Action<string, InventoryItem[]> OnJobClaimed; // jobId, result items
        public event Action<string> OnJobCancelled;            // jobId
        public event Action<string, string> OnCraftFailed;     // jobId, reason
        #endregion

        #region Services
        private CraftRecipeRepository _repository;
        private CraftValidator _validator;
        private CraftQueueService _queueService;
        private CraftRollService _rollService;
        private CraftRewardService _rewardService;
        private CraftPersistenceService _persistenceService;
        private CraftFormulasConfig _formulasConfig;
        private CraftContextBuilder _contextBuilder;
        private CraftCompletionService _completionService;
        private CraftRefundService _refundService;
        private SaveManager _saveManager;
        #endregion

        #region Initialization
        private void Initialize()
        {
            // Ensure dependencies exist
            var inventory = InventoryService.Instance;
            var economy = EconomyManager.Instance;
            _saveManager = SaveManager.Instance;

            if (inventory == null || economy == null)
            {
                Debug.LogError("[CraftService] Required services not initialized!");
                return;
            }

            // Create config (could load from Resources/JSON in production)
            _formulasConfig = new CraftFormulasConfig();

            // Create all sub-services
            _repository = new CraftRecipeRepository();
            _validator = new CraftValidator(_repository, inventory, economy, _saveManager);
            _queueService = new CraftQueueService();
            _rollService = new CraftRollService(_repository, new UnityRandomProvider(), _formulasConfig);
            _rewardService = new CraftRewardService(ItemGenerator.Instance);
            _persistenceService = new CraftPersistenceService(_queueService);
            _contextBuilder = new CraftContextBuilder(_saveManager);
            _refundService = new CraftRefundService(_repository, inventory, economy);
            _completionService = new CraftCompletionService(
                _queueService, _repository, _contextBuilder, _rollService, _rewardService, inventory, _saveManager);

            // Subscribe to queue events
            _queueService.OnJobStarted += id => OnJobStarted?.Invoke(id);
            _queueService.OnJobProgress += (id, p) => OnJobProgress?.Invoke(id, p);
            _queueService.OnJobReadyToClaim += id => OnJobReadyToClaim?.Invoke(id);
            _queueService.OnJobCancelled += id => OnJobCancelled?.Invoke(id);

            // Forward completion service events
            _completionService.Claimed += (jobId, success) =>
            {
                if (success) OnJobClaimed?.Invoke(jobId, Array.Empty<InventoryItem>()); // items in Result event
            };
            _completionService.Failed += (jobId, reason) => OnCraftFailed?.Invoke(jobId, reason);
            _completionService.Result += (jobId, recipeId, items) => OnJobClaimed?.Invoke(jobId, items);

            // Load recipes from equipment data
            _repository.Initialize();
        }
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            // Update craft queue (handles progress, ready-to-claim, starting queued jobs)
            _queueService?.Update();
        }
        #endregion

        // ============ Persistence ============
        /// <summary>
        /// Serializes the craft queue for saving.
        /// </summary>
        public CraftQueueSaveData GetQueueSaveData()
        {
            return _persistenceService?.CreateSaveData();
        }

        /// <summary>
        /// Restores the craft queue from saved data.
        /// </summary>
        public void LoadQueueSaveData(CraftQueueSaveData data)
        {
            _persistenceService?.RestoreSaveData(data);
        }

        #region Public API - Craft Operations
        /// <summary>
        /// Validates if a recipe can be crafted (without starting).
        /// </summary>
        public ValidationResult CanCraft(string recipeId, int count = 1)
        {
            return _validator?.CanCraft(recipeId, count) ?? ValidationResult.Fail("Service not initialized");
        }

        /// <summary>
        /// Starts a single craft job. Returns JobId or null if failed.
        /// Materials/currency reserved atomically via transaction.
        /// </summary>
        public string StartCraft(string recipeId, int count = 1)
        {
            // 1. Validate
            var validation = _validator.CanCraft(recipeId, count);
            if (!validation.IsSuccess)
            {
                Debug.Log($"[CraftService] Cannot craft {recipeId}: {validation.Reason}");
                return null;
            }

            if (!_repository.TryGetRecipe(recipeId, out var recipe)) return null;

            // 2. Generate completion seed for deterministic results
            long completionSeed = (long)_rollService.RngProvider.NextInt(1, int.MaxValue);

            // 3. Create Job (queued - EndTimeUtc = 0)
            long baseTicks = (long)(recipe.BaseCraftTime * TimeSpan.TicksPerSecond);
            long additionalTicks = (long)(recipe.TimePerAdditionalUnit * TimeSpan.TicksPerSecond * (count - 1));
            long totalDurationTicks = baseTicks + additionalTicks;
            var job = CraftJob.Create(recipeId, count, totalDurationTicks, recipe.RecipeVersion, completionSeed);

            // 4. Begin atomic transaction (reserves materials/currency)
            var transaction = new CraftTransactionService(InventoryService.Instance, EconomyManager.Instance, _saveManager);
            var transactionResult = transaction.BeginTransaction(recipe, count);

            if (!transactionResult.IsSuccess)
            {
                Debug.Log($"[CraftService] Transaction failed for {recipeId}: {transactionResult.Reason}");
                return null;
            }

            // 5. Enqueue job
            if (!_queueService.EnqueueJob(job))
            {
                transaction.Rollback();
                Debug.LogError($"[CraftService] Failed to enqueue job {job.JobId}. Transaction rolled back.");
                return null;
            }

            // 6. Commit transaction (consumes resources)
            try
            {
                var commitResult = transaction.Commit();
                if (!commitResult.IsSuccess)
                {
                    transaction.Rollback();
                    _queueService.CancelJob(job.JobId, RefundPolicy.None);
                    OnCraftFailed?.Invoke(job.JobId, commitResult.Reason);
                    Debug.LogError($"[CraftService] Commit failed post-enqueue: {commitResult.Reason}");
                    return null;
                }
            }
            catch (Exception e)
            {
                transaction.Rollback();
                _queueService.CancelJob(job.JobId, RefundPolicy.None);
                OnCraftFailed?.Invoke(job.JobId, e.Message);
                Debug.LogError($"[CraftService] Exception during commit for job {job.JobId}: {e.Message}");
                return null;
            }

            // 7. Try to start job from queue (if slot available)
            _queueService.TryStartNextJob();

            return job.JobId;
        }

        /// <summary>
        /// Starts multiple crafts of the same recipe (batch).
        /// Each gets its own JobId for independent completion.
        /// </summary>
        public IReadOnlyList<string> StartBatchCraft(string recipeId, int count)
        {
            var jobIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var id = StartCraft(recipeId, 1);
                if (id != null) jobIds.Add(id);
            }
            return jobIds;
        }

        /// <summary>
        /// Cancels a craft job with configurable refund policy.
        /// </summary>
        public bool CancelCraft(string jobId, RefundPolicy policy = RefundPolicy.ProgressBased)
        {
            var job = _queueService.GetJob(jobId);
            if (job == null) return false;

            bool wasActive = job.IsCrafting;
            bool success = _queueService.CancelJob(jobId, policy);

            if (success && wasActive)
            {
                // Handle refund based on policy
                _refundService.Refund(job, policy);
            }

            return success;
        }

        /// <summary>
        /// Gets the current progress of a craft job (0-1).
        /// </summary>
        public float GetProgress(string jobId)
        {
            return _queueService?.GetJobProgress(jobId) ?? 0f;
        }

        /// <summary>
        /// Gets time remaining for a craft job.
        /// </summary>
        public TimeSpan GetTimeRemaining(string jobId)
        {
            return _queueService?.GetJobTimeRemaining(jobId) ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Clears all ready-to-claim jobs from the queue.
        /// </summary>
        public void ClearCompletedJobs()
        {
            _queueService?.ClearCompletedJobs();
        }

        /// <summary>
        /// Claims a ready-to-claim job: generates deterministic reward, adds to inventory, removes job.
        /// </summary>
        public void ClaimJob(string jobId)
        {
            _completionService?.ClaimJob(jobId);
        }
        #endregion

        #region Public API - Recipe Queries
        public IReadOnlyList<CraftRecipeData> GetAllRecipes() => _repository?.GetAllRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetUnlockedRecipes() => _repository?.GetUnlockedRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetKnownRecipes() => _repository?.GetKnownRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetRecipesByCategory(ItemCategory category) => _repository?.GetRecipesByCategory(category) ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetRecipesForItem(string itemId) => _repository?.GetRecipesForItem(itemId) ?? Array.Empty<CraftRecipeData>();

        /// <summary>
        /// Checks if a recipe is unlocked.
        /// </summary>
        public bool IsRecipeUnlocked(string recipeId) => _repository?.IsUnlocked(recipeId) ?? false;

        /// <summary>
        /// Gets a recipe by ID.
        /// </summary>
        public bool TryGetRecipe(string recipeId, out CraftRecipeData recipe) => _repository?.TryGetRecipe(recipeId, out recipe) ?? (recipe = null) is null;

        /// <summary>
        /// Read-only preview of currency cost for a recipe (no transaction, no side effects).
        /// Returns null if recipe doesn't exist or count < 1.
        /// </summary>
        public CurrencySnapshot? GetRecipeCostPreview(string recipeId, int count = 1)
        {
            if (string.IsNullOrEmpty(recipeId) || count < 1)
                return null;

            if (!_repository.TryGetRecipe(recipeId, out var recipe))
                return null;

            return CraftCostResolver.ComputeCurrencyCost(recipe, count);
        }

        /// <summary>
        /// Read-only preview of material cost for a recipe (no transaction).
        /// Returns empty array if recipe doesn't exist or has no ingredients.
        /// </summary>
        public IngredientCost[] GetRecipeMaterialPreview(string recipeId, int count = 1)
        {
            if (string.IsNullOrEmpty(recipeId) || count < 1)
                return Array.Empty<IngredientCost>();

            if (!_repository.TryGetRecipe(recipeId, out var recipe))
                return Array.Empty<IngredientCost>();

            return recipe.Ingredients == null || recipe.Ingredients.Length == 0
                ? Array.Empty<IngredientCost>()
                : Array.ConvertAll(recipe.Ingredients, ing => new IngredientCost
                {
                    ItemId = ing.ItemId,
                    Count = ing.Count * count
                });
        }

        /// <summary>
        /// Gets the current queue state.
        /// </summary>
        public IReadOnlyList<CraftJob> GetActiveJobs() => _queueService?.GetActiveJobs() ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetPendingJobs() => _queueService?.GetPendingJobs() ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetReadyToClaimJobs() => _queueService?.GetReadyToClaimJobs() ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetAllJobs() => _queueService?.GetAllJobs() ?? Array.Empty<CraftJob>();
        #endregion

        #region Queue Management
        /// <summary>
        /// Sets maximum concurrent crafting jobs (upgradable).
        /// </summary>
        public void SetMaxConcurrentJobs(int max)
        {
            _queueService?.SetMaxConcurrentJobs(max);
        }

        /// <summary>
        /// Gets maximum concurrent jobs.
        /// </summary>
        public int GetMaxConcurrentJobs() => _queueService?.MaxConcurrentJobs ?? 1;
        #endregion

        #region Unlock System
        /// <summary>
        /// Attempts to unlock a recipe (by tier, level, quest, or event).
        /// </summary>
        public bool TryUnlockRecipe(string recipeId, bool notify = true)
        {
            return _repository?.UnlockRecipe(recipeId, notify) ?? false;
        }

        /// <summary>
        /// Discovers a hidden recipe.
        /// </summary>
        public bool DiscoverRecipe(string recipeId)
        {
            return _repository?.DiscoverRecipe(recipeId) ?? false;
        }

        /// <summary>
        /// Checks and unlocks all recipes that meet current requirements.
        /// </summary>
        public void CheckAutoUnlocks()
        {
            if (_repository == null || _saveManager == null) return;
            var account = _saveManager.GetAccountData();
            if (account != null)
            {
                _repository.UnlockRecipesByCraftingLevel(account.craftingLevel);
                _repository.UnlockRecipesByTier(_saveManager.GetHighestUnlockedTier());
            }
        }
        #endregion

        /// <summary>
        /// Gets the roll service for preview/simulation.
        /// </summary>
        public CraftRollService GetRollService() => _rollService;

        /// <summary>
        /// Gets the formulas config for tuning.
        /// </summary>
        public CraftFormulasConfig GetFormulasConfig() => _formulasConfig;

        /// <summary>Gets the queue service for direct job queries (UI access).</summary>
        public CraftQueueService GetQueueService() => _queueService;

        public void OpenCrafting() => SceneLoader.Instance.LoadCrafting();
    }
}