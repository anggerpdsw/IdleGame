using System;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using UnityEngine;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items.Decomposition;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Calculates and applies refunds for cancelled craft jobs based on policy.
    /// Recipe-driven: all refund data resolved from CraftRecipeData via job.RecipeId.
    /// </summary>
    public sealed class CraftRefundService
    {
        private readonly CraftRecipeRepository _repository;
        private readonly IInventoryService _inventory;
        private readonly IEconomyService _economy;

        public CraftRefundService(CraftRecipeRepository repository, IInventoryService inventory, IEconomyService economy)
        {
            _repository = repository;
            _inventory = inventory;
            _economy = economy;
        }

        /// <summary>
        /// Refunds materials/currency for a cancelled job according to policy.
        /// </summary>
        public void Refund(CraftJob job, RefundPolicy policy)
        {
            if (!_repository.TryGetRecipe(job.RecipeId, out var recipe))
            {
                Debug.LogWarning($"[CraftRefundService] Cannot refund job {job.JobId}: Recipe {job.RecipeId} not found.");
                return;
            }

            float refundRate = CalculateRefundRate(job, policy, recipe);
            if (refundRate <= 0f)
            {
                Debug.Log($"[CraftRefundService] No refund for job {job.JobId} (refund rate: {refundRate:F2})");
                return;
            }

            // Refund ingredients from the recipe, scaled by job.Count
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!ingredient.Consumed) continue;

                    int totalRequired = ingredient.Count * job.Count;
                    int refundCount = Mathf.RoundToInt(totalRequired * refundRate);

                    if (refundCount > 0)
                    {
                        _inventory.AddItem(ingredient.ItemId, refundCount);
                        Debug.Log($"[CraftRefundService] Refunded {refundCount} x {ingredient.ItemId} for job {job.JobId}");
                    }
                }
            }

            // Refund decomposed requirements (if any)
            var decomposedReqs = DecomposedRequirementResolver.Compute(recipe.Rarity);
            if (decomposedReqs != null && decomposedReqs.Count > 0)
            {
                var decomposedScaled = DecomposedRequirementAggregator.SumPerJob(decomposedReqs, job.Count);
                foreach (var prog in decomposedScaled)
                {
                    int refundCount = Mathf.RoundToInt(prog.Count * refundRate);
                    if (refundCount > 0)
                    {
                        _inventory.AddItem(prog.ItemId, refundCount);
                        Debug.Log($"[CraftRefundService] Refunded {refundCount} x {prog.ItemId} (decomposed) for job {job.JobId}");
                    }
                }
            }

            // Refund currency
            long goldRefund = Mathf.RoundToInt(recipe.GoldCost * job.Count * refundRate);
            long gemRefund = Mathf.RoundToInt(recipe.GemCost * job.Count * refundRate);

            if (goldRefund > 0)
            {
                _economy.AddCurrency(CurrencyType.Gold, goldRefund);
                Debug.Log($"[CraftRefundService] Refunded {goldRefund} Gold for job {job.JobId}");
            }
            if (gemRefund > 0)
            {
                _economy.AddCurrency(CurrencyType.Gem, gemRefund);
                Debug.Log($"[CraftRefundService] Refunded {gemRefund} Gems for job {job.JobId}");
            }

            if (recipe.AdditionalCosts != null)
            {
                foreach (var cost in recipe.AdditionalCosts)
                {
                    long refund = Mathf.RoundToInt(cost.Amount * job.Count * refundRate);
                    if (refund > 0)
                    {
                        _economy.AddCurrency(cost.Currency, refund);
                        Debug.Log($"[CraftRefundService] Refunded {refund} {cost.Currency} for job {job.JobId}");
                    }
                }
            }

            Debug.Log($"[CraftRefundService] Total refund for job {job.JobId} with rate {refundRate * 100:F0}% completed.");
        }

        private static float CalculateRefundRate(CraftJob job, RefundPolicy policy, CraftRecipeData recipe)
        {
            float progress = job.Progress;
            return policy switch
            {
                RefundPolicy.None => 0f,
                RefundPolicy.Full => 1f,
                RefundPolicy.ProgressBased => 1f - progress,
                RefundPolicy.HalfAfterHalf => progress < 0.5f ? 1f : (progress < 0.9f ? 0.5f : 0f),
                RefundPolicy.Custom => recipe.RefundPolicy switch
                {
                    RecipeRefundPolicy.None => 0f,
                    RecipeRefundPolicy.Full => 1f,
                    RecipeRefundPolicy.HalfAfterHalf => progress < 0.5f ? 1f : (progress < 0.9f ? 0.5f : 0f),
                    _ => 1f - progress
                },
                _ => 1f - progress
            };
        }
    }
}