using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items.Random;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Handles CLAIM of a ready-to-claim craft job.
    /// Generates deterministic reward using CompletionSeed, adds to inventory, removes job on success.
    /// Called only when player clicks Claim (not automatically on timer completion).
    /// </summary>
    public sealed class CraftCompletionService
    {
        private readonly CraftQueueService _queueService;
        private readonly CraftRecipeRepository _repository;
        private readonly CraftContextBuilder _contextBuilder;
        private readonly CraftRollService _rollService;
        private readonly CraftRewardService _rewardService;
        private readonly IInventoryService _inventory;
        private readonly ISaveService _saveManager;

        /// <summary>jobId, success — canonical event</summary>
        public event Action<string, bool> Claimed;
        /// <summary>jobId, reason — canonical event</summary>
        public event Action<string, string> Failed;
        /// <summary>jobId, recipeId, result items — canonical event</summary>
        public event Action<string, string, InventoryItem[]> Result;

        // Legacy aliases for CraftingManager compatibility
        /// <summary>Legacy alias for Claimed</summary>
        public event Action<string, bool> Completed
        {
            add => Claimed += value;
            remove => Claimed -= value;
        }

        public CraftCompletionService(
            CraftQueueService queueService,
            CraftRecipeRepository repository,
            CraftContextBuilder contextBuilder,
            CraftRollService rollService,
            CraftRewardService rewardService,
            IInventoryService inventory,
            ISaveService saveManager)
        {
            _queueService = queueService;
            _repository = repository;
            _contextBuilder = contextBuilder;
            _rollService = rollService;
            _rewardService = rewardService;
            _inventory = inventory;
            _saveManager = saveManager;
        }

        /// <summary>
        /// Claims a ready-to-claim job: validates, generates reward deterministically, adds to inventory, removes job.
        /// </summary>
        public void ClaimJob(string jobId)
        {
            Debug.Log($"[CraftCompletionService] ClaimJob ENTRY | JobId={jobId}");
            try
            {
                if (_queueService == null)
                {
                    Debug.LogError($"[CraftCompletionService] _queueService is NULL!");
                    Failed?.Invoke(jobId, "Internal error: queue service missing");
                    Claimed?.Invoke(jobId, false);
                    return;
                }
                Debug.Log($"[CraftCompletionService] _queueService={_queueService.GetHashCode()}");
                var job = _queueService.GetJob(jobId);
                Debug.Log($"[CraftCompletionService] GetJob returned job={job != null} | job?.JobId={job?.JobId} status={job?.Status} ready={job?.IsReadyToClaim}");
                if (job == null)
                {
                    Debug.LogWarning($"[CraftCompletionService] Job not found | JobId={jobId}");
                    Failed?.Invoke(jobId, "Job not found!");
                    Claimed?.Invoke(jobId, false);
                    return;
                }

                if (!job.IsReadyToClaim)
                {
                    Debug.LogWarning($"[CraftCompletionService] Job not ready to claim | JobId={jobId}, Status={job.Status}, Progress={job.Progress}");
                    Failed?.Invoke(jobId, "Job not ready to claim");
                    Claimed?.Invoke(jobId, false);
                    return;
                }

                if (!_repository.TryGetRecipe(job.RecipeId, out var recipe))
                {
                    Debug.LogWarning($"[CraftCompletionService] Recipe not found | JobId={jobId}, RecipeId={job.RecipeId}");
                    Failed?.Invoke(jobId, "Recipe not found");
                    Claimed?.Invoke(jobId, false);
                    return;
                }
                Debug.Log($"[CraftCompletionService] Recipe found: {recipe.RecipeId} | DisplayName={recipe.DisplayName}");

                // Use stored CompletionSeed for deterministic reward generation
                long completionSeed = job.CompletionSeed;
                if (completionSeed == 0)
                {
                    Debug.LogWarning($"[CraftCompletionService] Invalid completion seed | JobId={jobId}");
                    Failed?.Invoke(jobId, "Invalid completion seed");
                    Claimed?.Invoke(jobId, false);
                    return;
                }
                Debug.Log($"[CraftCompletionService] CompletionSeed={completionSeed}");

                // Roll and generate rewards using the stored seed
                var context = _contextBuilder.Build();
                Debug.Log($"[CraftCompletionService] Context built: craftingLevel={context.PlayerStats?.CraftingLevel ?? 0} blacksmith={context.PlayerStats?.BlacksmithLevel ?? 0}");
                
                var rollResult = _rollService.RollCraftSeeded(job.RecipeId, context, (int)completionSeed);
                Debug.Log($"[CraftCompletionService] RollCraft: success={rollResult.Success} entries={rollResult.Entries?.Count ?? 0} reason={rollResult.FailureReason}");

                if (!rollResult.Success || rollResult.Entries.Count == 0)
                {
                    Failed?.Invoke(jobId, rollResult.FailureReason ?? "Craft failed");
                    Claimed?.Invoke(jobId, false);
                    return;
                }

                var items = _rewardService.GenerateRewards(rollResult, recipe, context, completionSeed);
                Debug.Log($"[CraftCompletionService] GenerateRewards returned items={items?.Length ?? 0}");

                // Apply each reward with idempotency guard
                bool allApplied = true;
                var appliedItems = new List<InventoryItem>();

                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    string rewardOperationId = $"{jobId}#{i}";

                    var applyResult = _inventory.ApplyReward(item, rewardOperationId);
                    if (applyResult == ApplyResult.Failure)
                    {
                        Debug.LogError($"[CraftCompletion] ApplyReward failed for {item.ItemId} (op={rewardOperationId})");
                        allApplied = false;
                    }
                    else
                    {
                        Debug.Log($"[CraftCompletionService] ApplyReward item moved to inventory");
                        appliedItems.Add(item);
                    }
                }

                if (!allApplied)
                {
                    // Some rewards failed to apply; job stays for retry
                    Failed?.Invoke(jobId, "Partial reward application failure");
                    Claimed?.Invoke(jobId, false);
                    return;
                }

                // All rewards applied successfully — remove job from queue
                bool removed = _queueService.RemoveJob(jobId);
                if (removed) _saveManager.PersistCurrentStateDurably();

                Claimed?.Invoke(jobId, true);
                if (appliedItems.Count > 0)
                    Result?.Invoke(jobId, job.RecipeId, appliedItems.ToArray());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CraftCompletionService] Unexpected exception | JobId={jobId}, ex={ex}");
                Failed?.Invoke(jobId, "Internal error");
                Claimed?.Invoke(jobId, false);
            }
        }
    }
}