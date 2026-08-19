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

        /// <summary>jobId, success</summary>
        public event Action<string, bool> Claimed;
        /// <summary>jobId, reason</summary>
        public event Action<string, string> Failed;
        /// <summary>jobId, recipeId, result items</summary>
        public event Action<string, string, InventoryItem[]> Result;

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
            var job = _queueService.GetJob(jobId);
            if (job == null)
            {
                Failed?.Invoke(jobId, "Job not found");
                Claimed?.Invoke(jobId, false);
                return;
            }

            if (!job.IsReadyToClaim)
            {
                Failed?.Invoke(jobId, "Job not ready to claim");
                Claimed?.Invoke(jobId, false);
                return;
            }

            if (!_repository.TryGetRecipe(job.RecipeId, out var recipe))
            {
                Failed?.Invoke(jobId, "Recipe not found");
                Claimed?.Invoke(jobId, false);
                return;
            }

            // Use stored CompletionSeed for deterministic reward generation
            long completionSeed = job.CompletionSeed;
            if (completionSeed == 0)
            {
                Failed?.Invoke(jobId, "Invalid completion seed");
                Claimed?.Invoke(jobId, false);
                return;
            }

            // Roll and generate rewards using the stored seed
            var context = _contextBuilder.Build();
            var rollResult = _rollService.RollCraft(job.RecipeId, context, new SeedRandomProvider((int)completionSeed));

            if (!rollResult.Success || rollResult.Entries.Count == 0)
            {
                Failed?.Invoke(jobId, rollResult.FailureReason ?? "Craft failed");
                Claimed?.Invoke(jobId, false);
                return;
            }

            var items = _rewardService.GenerateRewards(rollResult, recipe, context, completionSeed);

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
            if (removed)
            {
                _saveManager.PersistCurrentStateDurably();
            }

            Claimed?.Invoke(jobId, true);
            if (appliedItems.Count > 0)
            {
                Result?.Invoke(jobId, job.RecipeId, appliedItems.ToArray());
            }
        }
    }
}