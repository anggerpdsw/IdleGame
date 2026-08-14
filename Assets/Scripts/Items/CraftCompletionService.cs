using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Manager;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Handles a completed craft queue job using two-phase completion (P0-D).
    /// Phase A: Mark job RewardPendingCommit, roll, persist Results+CompletionSeed durably.
    /// Phase B: Iterate rewards, apply each via InventoryService.ApplyReward with idempotency key.
    /// </summary>
    public sealed class CraftCompletionService
    {
        private readonly CraftQueueService _queueService;
        private readonly CraftRecipeRepository _repository;
        private readonly CraftContextBuilder _contextBuilder;
        private readonly CraftRollService _rollService;
        private readonly CraftRewardService _rewardService;
        private readonly IInventoryService _inventory;
        private readonly SaveManager _saveManager;

        /// <summary>recipeId, success</summary>
        public event Action<string, bool> Completed;
        /// <summary>jobId, reason</summary>
        public event Action<string, string> Failed;
        /// <summary>recipeId, result items</summary>
        public event Action<string, InventoryItem[]> Result;

        public CraftCompletionService(
            CraftQueueService queueService,
            CraftRecipeRepository repository,
            CraftContextBuilder contextBuilder,
            CraftRollService rollService,
            CraftRewardService rewardService,
            IInventoryService inventory,
            SaveManager saveManager)
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
        /// Completes a queue job using two-phase commit (P0-D).
        /// </summary>
        public void Complete(string jobId)
        {
            var job = _queueService.GetJob(jobId);
            if (job == null) return;

            // Phase A: Roll, generate results, persist durable (RewardPendingCommit)
            var context = _contextBuilder.Build();
            var rollResult = _rollService.RollCraft(job.RecipeId, context);

            if (!rollResult.Success || rollResult.Entries.Count == 0)
            {
                _queueService.CancelJob(jobId, RefundPolicy.ProgressBased);
                Failed?.Invoke(jobId, rollResult.FailureReason ?? "Craft failed");
                Completed?.Invoke(job.RecipeId, false);
                return;
            }

            if (!_repository.TryGetRecipe(job.RecipeId, out var recipe))
            {
                _queueService.CancelJob(jobId, RefundPolicy.ProgressBased);
                Failed?.Invoke(jobId, "Recipe not found after roll");
                Completed?.Invoke(job.RecipeId, false);
                return;
            }

            var items = _rewardService.GenerateRewards(rollResult, recipe, context);
            job.Results = CraftResultData.FromInventoryItems(items, rollResult.ExpReward);

            // Use snapshot's CompletionSeed if available, else derive from RNG for replayability
            long completionSeed = job.ExecutionSnapshot?.CompletionSeed ?? 0;
            if (completionSeed == 0 && job.CompletionSeed.HasValue)
                completionSeed = job.CompletionSeed.Value;
            if (completionSeed == 0)
                completionSeed = (long)_rollService.RngProvider.NextInt(1, int.MaxValue);
            job.CompletionSeed = completionSeed;

            // Phase A: Mark RewardPendingCommit and persist durably (Results + Seed)
            job.Status = CraftJobStatus.RewardPendingCommit;
            _saveManager.PersistCurrentStateDurably();

            // Phase B: Apply each reward with idempotency guard
            bool allApplied = true;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                string rewardOperationId = $"{jobId}#{i}";

                var applyResult = _inventory.ApplyReward(item, rewardOperationId);
                if (applyResult == ApplyResult.Failure)
                {
                    Debug.LogError($"[CraftCompletion] ApplyReward failed for {item.ItemId} (op={rewardOperationId})");
                    allApplied = false;
                    // Don't break — continue attempting remaining rewards; already-applied ones are idempotent
                }
                // Persist after each reward to make progress durable
                _saveManager.PersistCurrentStateDurably();
            }

            if (!allApplied)
            {
                // Some rewards failed to apply; job stays in RewardPendingCommit for recovery
                Failed?.Invoke(jobId, "Partial reward application failure");
                Completed?.Invoke(job.RecipeId, false);
                return;
            }

            // All rewards applied successfully — mark Complete
            job.Status = CraftJobStatus.Complete;
            job.CompletedCount = job.Count;
            _saveManager.PersistCurrentStateDurably();

            Completed?.Invoke(job.RecipeId, true);
            if (job.Results != null)
            {
                var resultItems = job.Results.Select(r => new InventoryItem
                {
                    ItemId = r.ItemId,
                    Quantity = r.Count,
                    Level = r.Level,
                    AcquiredTimestamp = r.AcquiredTimestamp
                }).ToArray();
                Result?.Invoke(job.RecipeId, resultItems);
            }
        }
    }
}