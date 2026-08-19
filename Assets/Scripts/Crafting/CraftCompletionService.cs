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
        private readonly ISaveService _saveManager;

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
        /// Completes a queue job using two-phase commit (P0-D).
        /// </summary>
        public void Complete(string jobId)
        {
            var job = _queueService.GetJob(jobId);
            if (job == null) return;

            // Phase A: Roll, generate results, persist durable (RewardPendingCommit)
            if (job.Status != CraftJobStatus.RewardPendingCommit)
            {
                // v3.8 §20.7 — CompletionSeed resolves BEFORE any roll; it seeds both the
                // craft roll and equipment attribute generation (I-11 determinism).
                long completionSeed = job.CompletionSeed;
                if (completionSeed == 0)
                    completionSeed = (long)_rollService.RngProvider.NextInt(1, int.MaxValue);
                job.CompletionSeed = completionSeed;

                var context = _contextBuilder.Build();
                var rollResult = _rollService.RollCraft(job.RecipeId, context, new SeedRandomProvider((int)completionSeed));

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

                var items = _rewardService.GenerateRewards(rollResult, recipe, context, completionSeed);
                job.Results = CraftResultData.FromInventoryItems(items, rollResult.ExpReward);

                // Phase A: Mark RewardPendingCommit and persist durably (Results + Seed)
                job.MarkRewardPendingCommit();
                _saveManager.PersistCurrentStateDurably();
            }

            // Phase B: Apply each reward with idempotency guard
            var itemsToApply = job.Results.Select(r => new InventoryItem
            {
                ItemId = r.ItemId,
                Quantity = r.Count,
                Level = r.Level,
                AcquiredTimestamp = r.AcquiredTimestamp,
                CustomData = r.CustomData != null ? new Dictionary<string, object>(r.CustomData) : null
            }).ToArray();

            bool allApplied = true;
            for (int i = 0; i < itemsToApply.Length; i++)
            {
                var item = itemsToApply[i];
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
                    AcquiredTimestamp = r.AcquiredTimestamp,
                    CustomData = r.CustomData != null ? new Dictionary<string, object>(r.CustomData) : null
                }).ToArray();
                Result?.Invoke(job.RecipeId, resultItems);
            }
        }
    }
}