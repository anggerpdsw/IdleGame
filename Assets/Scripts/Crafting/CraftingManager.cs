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
        public event Action<string> OnJobStarted;                     // jobId (crafting began)
        public event Action<string, float> OnJobProgress;            // jobId, progress (0-1)
        public event Action<string> OnJobReadyToClaim;               // jobId (timer finished)
        public event Action<string, InventoryItem[]> OnJobClaimed;   // jobId, reward items
        public event Action<string> OnJobCancelled;                  // jobId
        public event Action<string, string> OnCraftFailed;           // jobId, reason
        public event Action<string, bool> OnCraftClaimed;            // jobId, success
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
            var inventory   = InventoryService.Instance;
            var economy     = EconomyManager.Instance;
            _saveManager    = SaveManager.Instance;

            if (inventory == null || economy == null)
            {
                Debug.LogError("[CraftService] Required services not initialized!");
                return;
            }

            _formulasConfig = new CraftFormulasConfig();

            _repository   = new CraftRecipeRepository();
            _validator    = new CraftValidator(_repository, inventory, economy, _saveManager);
            _queueService = new CraftQueueService();
            _rollService  = new CraftRollService(_repository, new UnityRandomProvider(), _formulasConfig, ItemDatabase.Instance);
            _rewardService= new CraftRewardService(ItemGenerator.Instance);
            _persistenceService = new CraftPersistenceService(_queueService);
            _contextBuilder = new CraftContextBuilder(_saveManager);
            _refundService   = new CraftRefundService(_repository, inventory, economy);
            _completionService = new CraftCompletionService(
                _queueService, _repository, _contextBuilder,
                _rollService, _rewardService, inventory, _saveManager);

            // Queue events → manager events
            _queueService.OnJobStarted      += id => OnJobStarted?.Invoke(id);
            _queueService.OnJobProgress     += (id, p) => OnJobProgress?.Invoke(id, p);
            _queueService.OnJobReadyToClaim += id => OnJobReadyToClaim?.Invoke(id);
            _queueService.OnJobCancelled   += id => OnJobCancelled?.Invoke(id);
            _queueService.OnJobStatusChanged+= (id, status) => { /* optional UI hook */ };

            // Completion service events → manager events
            _completionService.Claimed += (jobId, success) =>
            {
                OnCraftClaimed?.Invoke(jobId, success);
                // Do NOT fire OnJobClaimed here – result will be emitted via Result event.
            };
            _completionService.Failed  += (jobId, reason) => OnCraftFailed?.Invoke(jobId, reason);
            _completionService.Result  += (jobId, recipeId, items) => OnJobClaimed?.Invoke(jobId, items);

            _repository.Initialize();
        }
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            _queueService?.Update();
        }
        #endregion

        // ---------- Persistence ----------
        public CraftQueueSaveData GetQueueSaveData() => _persistenceService?.CreateSaveData();
        public void LoadQueueSaveData(CraftQueueSaveData data) => _persistenceService?.RestoreSaveData(data);

        // ---------- Public API ----------
        public ValidationResult CanCraft(string recipeId, int count = 1) =>
            _validator?.CanCraft(recipeId, count) ?? ValidationResult.Fail("Service not initialized");

        public string StartCraft(string recipeId, int count = 1)
        {
            var validation = _validator.CanCraft(recipeId, count);
            if (!validation.IsSuccess) return null;
            if (!_repository.TryGetRecipe(recipeId, out var recipe)) return null;

            long completionSeed = (long)_rollService.RngProvider.NextInt(1, int.MaxValue);
            long baseTicks = (long)(recipe.BaseCraftTime * TimeSpan.TicksPerSecond);
            long extraTicks = (long)(recipe.TimePerAdditionalUnit * TimeSpan.TicksPerSecond * (count - 1));
            var job = CraftJob.Create(recipeId, count, baseTicks + extraTicks,
                                      recipe.RecipeVersion, completionSeed);

            var transaction = new CraftTransactionService(
                InventoryService.Instance, EconomyManager.Instance, _saveManager);
            var trxResult = transaction.BeginTransaction(recipe, count);
            if (!trxResult.IsSuccess) return null;

            if (!_queueService.EnqueueJob(job))
            {
                transaction.Rollback();
                return null;
            }

            try
            {
                var commitResult = transaction.Commit();
                if (!commitResult.IsSuccess)
                {
                    transaction.Rollback();
                    _queueService.CancelJob(job.JobId, RefundPolicy.None);
                    OnCraftFailed?.Invoke(job.JobId, commitResult.Reason);
                    return null;
                }
            }
            catch (Exception e)
            {
                transaction.Rollback();
                _queueService.CancelJob(job.JobId, RefundPolicy.None);
                OnCraftFailed?.Invoke(job.JobId, e.Message);
                return null;
            }

            _queueService.TryStartNextJob();
            return job.JobId;
        }

        public IReadOnlyList<string> StartBatchCraft(string recipeId, int count)
        {
            var ids = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var id = StartCraft(recipeId, 1);
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return ids;
        }

        public bool CancelCraft(string jobId, RefundPolicy policy = RefundPolicy.ProgressBased)
        {
            var job = _queueService.GetJob(jobId);
            if (job == null) return false;

            bool wasActive = job.IsCrafting;
            bool success   = _queueService.CancelJob(jobId, policy);

            if (success && wasActive)
                _refundService.Refund(job, policy);
            return success;
        }

        public float GetProgress(string jobId) => _queueService?.GetJobProgress(jobId) ?? 0f;
        public TimeSpan GetTimeRemaining(string jobId) => _queueService?.GetJobTimeRemaining(jobId) ?? TimeSpan.Zero;
        public void ClearCompletedJobs() => _queueService?.ClearCompletedJobs();

        // Claim flow – *only* delegates to completion service.
        public void ClaimJob(string jobId)
        {
            if (_completionService == null)
            {
                Debug.LogError("[CraftingManager] CompletionService is NULL.");
                return;
            }
            Debug.Log($"[CraftingManager] ClaimJob -> CompletionService | JobId={jobId}");
            _completionService.ClaimJob(jobId);
        }

        // ---------- Recipe queries ----------
        public IReadOnlyList<CraftRecipeData> GetAllRecipes() => _repository?.GetAllRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetUnlockedRecipes() => _repository?.GetUnlockedRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetKnownRecipes() => _repository?.GetKnownRecipes() ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetRecipesByCategory(ItemCategory cat) => _repository?.GetRecipesByCategory(cat) ?? Array.Empty<CraftRecipeData>();
        public IReadOnlyList<CraftRecipeData> GetRecipesForItem(string itemId) => _repository?.GetRecipesForItem(itemId) ?? Array.Empty<CraftRecipeData>();
        public bool IsRecipeUnlocked(string recipeId) => _repository?.IsUnlocked(recipeId) ?? false;
        public bool TryGetRecipe(string recipeId, out CraftRecipeData recipe) => _repository?.TryGetRecipe(recipeId, out recipe) ?? (recipe = null) is null;
        public CurrencySnapshot? GetRecipeCostPreview(string recipeId, int count = 1)
        {
            if (string.IsNullOrEmpty(recipeId) || count < 1) return null;
            if (!_repository.TryGetRecipe(recipeId, out var recipe)) return null;
            return CraftCostResolver.ComputeCurrencyCost(recipe, count);
        }

        public IngredientCost[] GetRecipeMaterialPreview(string recipeId, int count = 1)
        {
            if (string.IsNullOrEmpty(recipeId) || count < 1) return Array.Empty<IngredientCost>();
            if (!_repository.TryGetRecipe(recipeId, out var recipe)) return Array.Empty<IngredientCost>();
            if (recipe.Ingredients == null || recipe.Ingredients.Length == 0) return Array.Empty<IngredientCost>();
            return Array.ConvertAll(recipe.Ingredients, ing => new IngredientCost { ItemId = ing.ItemId, Count = ing.Count * count });
        }

        // ---------- Job queries ----------
        public IReadOnlyList<CraftJob> GetActiveJobs()  => _queueService?.GetActiveJobs()  ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetPendingJobs() => _queueService?.GetPendingJobs() ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetReadyToClaimJobs() => _queueService?.GetReadyToClaimJobs() ?? Array.Empty<CraftJob>();
        public IReadOnlyList<CraftJob> GetAllJobs() => _queueService?.GetAllJobs() ?? Array.Empty<CraftJob>();

        // ---------- Queue management ----------
        public void SetMaxConcurrentJobs(int max) => _queueService?.SetMaxConcurrentJobs(max);
        public int GetMaxConcurrentJobs() => _queueService?.MaxConcurrentJobs ?? 1;

        // ---------- Unlock system ----------
        public bool TryUnlockRecipe(string recipeId, bool notify = true) => _repository?.UnlockRecipe(recipeId, notify) ?? false;
        public bool DiscoverRecipe(string recipeId) => _repository?.DiscoverRecipe(recipeId) ?? false;
        public void CheckAutoUnlocks()
        {
            if (_repository == null || _saveManager == null) return;
            var account = _saveManager.GetAccountData();
            if (account == null) return;

            _repository.UnlockRecipesByCraftingLevel(account.craftingLevel);
            _repository.UnlockRecipesByTier(_saveManager.GetHighestUnlockedTier());
        }

        // ---------- Misc ----------
        public CraftRollService GetRollService() => _rollService;
        public CraftFormulasConfig GetFormulasConfig() => _formulasConfig;
        public CraftQueueService GetQueueService() => _queueService;
        public void OpenCrafting() => SceneLoader.Instance.LoadCrafting();
    }
}