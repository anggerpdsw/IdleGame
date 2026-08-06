using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Core;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Calculates and applies refunds for cancelled craft jobs based on policy.
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
            if (!_repository.TryGetRecipe(job.RecipeId, out var recipe)) return;

            float refundRate = CalculateRefundRate(job, policy, recipe);
            if (refundRate <= 0f) return;

            // Refund ingredients
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!ingredient.Consumed) continue;
                    int refundCount = Mathf.RoundToInt(ingredient.Count * job.Count * refundRate);
                    if (refundCount > 0)
                    {
                        _inventory.AddItem(ingredient.ItemId, refundCount);
                    }
                }
            }

            // Refund currency
            long goldRefund = Mathf.RoundToInt(recipe.GoldCost * job.Count * refundRate);
            long gemRefund = Mathf.RoundToInt(recipe.GemCost * job.Count * refundRate);
            if (goldRefund > 0) _economy.AddCurrency(CurrencyType.Gold, goldRefund);
            if (gemRefund > 0) _economy.AddCurrency(CurrencyType.Gem, gemRefund);

            if (recipe.AdditionalCosts != null)
            {
                foreach (var cost in recipe.AdditionalCosts)
                {
                    long refund = Mathf.RoundToInt(cost.Amount * job.Count * refundRate);
                    if (refund > 0) _economy.AddCurrency(cost.Currency, refund);
                }
            }

            Debug.Log($"[CraftRefundService] Refunded {refundRate * 100:F0}% for job {job.JobId}");
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