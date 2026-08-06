using System;
using System.Linq;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Handles a completed craft queue job: roll → reward → inventory → notify.
    /// Kept separate so CraftService stays a thin orchestrator.
    /// </summary>
    public sealed class CraftCompletionService
    {
        private readonly CraftQueueService _queueService;
        private readonly CraftRecipeRepository _repository;
        private readonly CraftContextBuilder _contextBuilder;
        private readonly CraftRollService _rollService;
        private readonly CraftRewardService _rewardService;
        private readonly IInventoryService _inventory;

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
            IInventoryService inventory)
        {
            _queueService = queueService;
            _repository = repository;
            _contextBuilder = contextBuilder;
            _rollService = rollService;
            _rewardService = rewardService;
            _inventory = inventory;
        }

        /// <summary>
        /// Completes a queue job: rolls, generates rewards, stores in inventory, notifies.
        /// </summary>
        public void Complete(string jobId)
        {
            var job = _queueService.GetJob(jobId);
            if (job == null) return;

            var context = _contextBuilder.Build();
            var rollResult = _rollService.RollCraft(job.RecipeId, context);

            if (rollResult.Success && rollResult.Entries.Count > 0)
            {
                if (_repository.TryGetRecipe(job.RecipeId, out var recipe))
                {
                    var items = _rewardService.GenerateRewards(rollResult, recipe, context);

                    foreach (var item in items)
                    {
                        _inventory.AddItemInstance(item);
                    }

                    job.Results = CraftResultData.FromInventoryItems(items, rollResult.ExpReward);
                }
            }
            else
            {
                _queueService.CancelJob(jobId, RefundPolicy.ProgressBased);
                Failed?.Invoke(jobId, rollResult.FailureReason ?? "Craft failed");
            }

            Completed?.Invoke(job.RecipeId, rollResult.Success);
            if (rollResult.Success && job.Results != null)
            {
                var items = job.Results.Select(r => new InventoryItem
                {
                    InstanceId = Guid.NewGuid().ToString(),
                    ItemId = r.ItemId,
                    Quantity = r.Count,
                    Level = r.Level,
                    AcquiredTimestamp = r.AcquiredTimestamp
                }).ToArray();
                Result?.Invoke(job.RecipeId, items);
            }
        }
    }
}