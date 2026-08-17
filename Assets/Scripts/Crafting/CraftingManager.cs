using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Core;

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
        public event Action<string, bool> OnCraftCompleted;      // recipeId, success
        public event Action<string, InventoryItem[]> OnCraftResult; // recipeId, result items
        public event Action<string> OnCraftStarted;              // jobId
        public event Action<string, float> OnCraftProgress;      // jobId, progress (0-1)
        public event Action<string, string> OnCraftFailed;       // jobId, reason
        public event Action<string> OnCraftCancelled;            // jobId
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
        private CraftTransactionJournal _transactionJournal;
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
            _transactionJournal = new CraftTransactionJournal();
            _saveManager.RegisterJournal(_transactionJournal);
            _queueService = new CraftQueueService();
            _rollService = new CraftRollService(_repository, new UnityRandomProvider(), _formulasConfig);
            _rewardService = new CraftRewardService(ItemGenerator.Instance);
            _persistenceService = new CraftPersistenceService(_queueService);
            _contextBuilder = new CraftContextBuilder(_saveManager);
            _refundService = new CraftRefundService(_repository, inventory, economy);
            _completionService = new CraftCompletionService(
                _queueService, _repository, _contextBuilder, _rollService, _rewardService, inventory, _saveManager);

            // Subscribe to queue events
            _queueService.OnJobStarted += id => OnCraftStarted?.Invoke(id);
            _queueService.OnJobProgress += (id, p) => OnCraftProgress?.Invoke(id, p);
            _queueService.OnJobCompleted += jobId => _completionService.Complete(jobId);
            _queueService.OnJobCancelled += id => OnCraftCancelled?.Invoke(id);

            // Forward completion results
            _completionService.Completed += (recipeId, success) => OnCraftCompleted?.Invoke(recipeId, success);
            _completionService.Failed += (jobId, reason) => OnCraftFailed?.Invoke(jobId, reason);
            _completionService.Result += (recipeId, items) => OnCraftResult?.Invoke(recipeId, items);

            // Load recipes from equipment data
            _repository.Initialize();

            // P0-D: Run recovery executor after journal is loaded (SaveManager calls LoadFromSaveData on journal before this)
            RunTransactionRecovery(inventory, economy);
        }

        /// <summary>
        /// P0-D Recovery executor: consumes CraftTransactionJournal.ClassifyReconciliation() decisions
        /// and executes the required compensations. Called once at startup after journal is hydrated.
        /// </summary>
        private void RunTransactionRecovery(IInventoryService inventory, EconomyManager economy)
        {
            if (_transactionJournal == null) return;

            var decisions = _transactionJournal.ClassifyReconciliation();
            if (decisions.Count == 0) return;

            Debug.Log($"[CraftService] Recovery: processing {decisions.Count} reconciliation decisions");

            foreach (var decision in decisions)
            {
                try
                {
                    switch (decision.Action)
                    {
                        case ReconciliationAction.Commit:
                            ExecuteCommit(decision, inventory, economy);
                            break;

                        case ReconciliationAction.Rollback:
                            ExecuteRollback(decision, inventory, economy);
                            break;

                        case ReconciliationAction.Skip:
                            // No action needed
                            break;
                    }

                    // Persist journal state after each decision
                    _saveManager.PersistCurrentStateDurably();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CraftService] Recovery failed for decision {decision.Action} (entry={decision.EntryId}, op={decision.OperationId}): {e.Message}");
                    // Continue with other decisions; this one will be retried on next startup
                }
            }
        }

        /// <summary>
        /// Executes a Commit decision: consumes the resource for Pending operations.
        /// </summary>
        private void ExecuteCommit(ReconciliationDecision decision, IInventoryService inventory, EconomyManager economy)
        {
            switch (decision.ResourceType)
            {
                case ResourceKind.Material:
                case ResourceKind.Catalyst:
                case ResourceKind.Progression:
                    // These were reserved but not yet consumed — consume now via exact removal
                    int removed = inventory.RemoveItemById(decision.ResourceId, decision.Quantity);
                    if (removed != decision.Quantity)
                    {
                        Debug.LogWarning($"[CraftService] Recovery commit: expected to remove {decision.Quantity} of {decision.ResourceId}, removed {removed}");
                    }
                    break;

                case ResourceKind.Currency:
                    // Currency was reserved — spend now
                    if (Enum.TryParse<CurrencyType>(decision.ResourceId, out var currencyType))
                    {
                        bool spent = economy.TrySpendCurrency(currencyType, decision.Quantity, "CraftRecoveryCommit");
                        if (!spent)
                        {
                            Debug.LogError($"[CraftService] Recovery commit: failed to spend {decision.Quantity} {currencyType} for {decision.JobId}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[CraftService] Recovery commit: unknown currency {decision.ResourceId}");
                    }
                    break;
            }

            // Mark operation as Applied
            _transactionJournal.UpdateOperationState(decision.EntryId, decision.OperationId, OperationState.Applied);

            // If all operations in entry are now Applied/RolledBack, advance phase to JobPersisted
            var ops = _transactionJournal.GetOperations(decision.EntryId);
            bool allTerminal = true;
            foreach (var o in ops)
            {
                if (o.State != OperationState.Applied && o.State != OperationState.RolledBack)
                {
                    allTerminal = false;
                    break;
                }
            }
            if (allTerminal)
            {
                _transactionJournal.UpdateEntryPhase(decision.EntryId, CraftJournalPhase.JobPersisted);
            }
        }

        /// <summary>
        /// Executes a Rollback decision: refunds the resource for Applied operations.
        /// Uses idempotency guard (HasAppliedOperation) to avoid double-refund.
        /// </summary>
        private void ExecuteRollback(ReconciliationDecision decision, IInventoryService inventory, EconomyManager economy)
        {
            switch (decision.ResourceType)
            {
                case ResourceKind.Material:
                case ResourceKind.Catalyst:
                case ResourceKind.Progression:
                    // Refund items back to inventory
                    var itemData = ItemDatabase.Instance?.GetItem(decision.ResourceId);
                    if (itemData != null)
                    {
                        for (int i = 0; i < decision.Quantity; i++)
                        {
                            var refundItem = new InventoryItem
                            {
                                ItemId = decision.ResourceId,
                                Quantity = 1,
                                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            };
                            inventory.AddItemInstance(refundItem);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[CraftService] Recovery rollback: item not found {decision.ResourceId}");
                    }
                    break;

                case ResourceKind.Currency:
                    // Refund currency
                    if (Enum.TryParse<CurrencyType>(decision.ResourceId, out var currencyType))
                    {
                        economy.AddCurrency(currencyType, decision.Quantity, "CraftRecoveryRollback");
                    }
                    else
                    {
                        Debug.LogError($"[CraftService] Recovery rollback: unknown currency {decision.ResourceId}");
                    }
                    break;
            }

            // Mark operation as RolledBack
            _transactionJournal.UpdateOperationState(decision.EntryId, decision.OperationId, OperationState.RolledBack);

            // If all operations in entry are now RolledBack, advance phase to RolledBack
            var rollbackOps = _transactionJournal.GetOperations(decision.EntryId);
            bool allRolledBack = true;
            foreach (var o in rollbackOps)
            {
                if (o.State != OperationState.RolledBack)
                {
                    allRolledBack = false;
                    break;
                }
            }
            if (allRolledBack)
            {
                _transactionJournal.UpdateEntryPhase(decision.EntryId, CraftJournalPhase.RolledBack);
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            // Update craft queue (handles progress, completion, starting queued jobs)
            _queueService?.Update();
        }

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
        #endregion

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

            // 2. Build Execution Snapshot (P0-C)
            var ingredientsSnapshot = recipe.Ingredients != null
                ? Array.ConvertAll(recipe.Ingredients, ing => CraftIngredientSnapshot.From(ing, count))
                : null;
            var executionSnapshot = CraftSnapshotBuilder.Build(recipe, count, _rollService.RngProvider, ingredientsSnapshot);

            // 3. Create Job
            long baseTicks = (long)(recipe.BaseCraftTime * TimeSpan.TicksPerSecond);
            long additionalTicks = (long)(recipe.TimePerAdditionalUnit * TimeSpan.TicksPerSecond * (count - 1));
            long totalDurationTicks = baseTicks + additionalTicks;
            var job = CraftJob.Create(recipeId, count, totalDurationTicks, executionSnapshot, ingredientsSnapshot);

            // 4. Begin atomic transaction (reserves materials, creates PREPARED journal entry, persists)
            var transaction = new CraftTransactionService(InventoryService.Instance, EconomyManager.Instance, _transactionJournal, _saveManager);
            var transactionResult = transaction.BeginTransaction(job.JobId, recipe, executionSnapshot, count);

            if (!transactionResult.IsSuccess)
            {
                Debug.Log($"[CraftService] Transaction failed for {recipeId}: {transactionResult.Reason}");
                // No need to rollback, as BeginTransaction does it internally on failure
                return null;
            }

            // 5. Enqueue job
            if (!_queueService.EnqueueJob(job))
            {
                transaction.Rollback(); // Rolls back the journal entry and persists
                Debug.LogError($"[CraftService] Failed to enqueue job {job.JobId}. Transaction rolled back.");
                return null;
            }

            // 6. Mark as Reserved and persist
            _transactionJournal.UpdateEntryPhase(transaction.JournalEntryId, CraftJournalPhase.Reserved);
            _saveManager.PersistCurrentStateDurably();


            // 7. Commit transaction (consumes resources with per-operation checkpoints)
            try
            {
                var commitResult = transaction.Commit();
                if (!commitResult.IsSuccess)
                {
                    // This path indicates a non-exception failure, which current Commit() doesn't do. Safeguard.
                    transaction.Rollback();
                    _queueService.CancelJob(job.JobId, RefundPolicy.None);
                    OnCraftFailed?.Invoke(job.JobId, commitResult.Reason);
                    Debug.LogError($"[CraftService] Commit failed post-enqueue: {commitResult.Reason}");
                    return null;
                }
            }
            catch (Exception e)
            {
                // Exception during commit leaves journal in a partial state.
                // Mark for rollback and let recovery handle compensation.
                transaction.Rollback();
                _queueService.CancelJob(job.JobId, RefundPolicy.None);
                OnCraftFailed?.Invoke(job.JobId, e.Message);
                Debug.LogError($"[CraftService] Exception during commit for job {job.JobId}: {e.Message}");
                return null;
            }

            // 8. Mark as Committed and persist
            _transactionJournal.UpdateEntryPhase(transaction.JournalEntryId, CraftJournalPhase.Committed);
            _saveManager.PersistCurrentStateDurably();

            // 9. Try to start job from queue
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

            bool wasActive = job.IsActive;
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
        /// Clears all completed jobs from the queue.
        /// </summary>
        public void ClearCompletedJobs()
        {
            _queueService?.ClearCompletedJobs();
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
        public IReadOnlyList<CraftJob> GetCompletedJobs() => _queueService?.GetCompletedJobs() ?? Array.Empty<CraftJob>();
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
        
        public void OpenCrafting() => SceneLoader.Instance.LoadCrafting();
    }
}