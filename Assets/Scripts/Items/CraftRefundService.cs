using System;
using IdleDefenseSurvival.Inventory;
using UnityEngine;
using IdleDefenseSurvival.Core.Interfaces;

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

            // Refund ingredients from job snapshot (immutable-at-creation copy).
            // Falls back to live recipe only for legacy jobs predating the snapshot feature —

            // P0-B step 10: ExecutionSnapshot.Cost.Materials is the new source of truth (P0-A).
            // Priority: ExecutionSnapshot.Cost.Materials -> IngredientsSnapshot[] -> recipe.Ingredients.
            var ingredientSource = (CraftIngredient[])null;
            bool snapshotAvailable = false;
            if (job.ExecutionSnapshot != null &&
                job.ExecutionSnapshot.Cost.Materials != null &&
                job.ExecutionSnapshot.Cost.Materials.Length > 0)
            {
                ingredientSource = Array.ConvertAll(
                    job.ExecutionSnapshot.Cost.Materials,
                    c => new CraftIngredient
                    {
                        ItemId = c.ItemId,
                        Count = c.Count,
                        Consumed = true,
                        CanSubstitute = false,
                        SubstituteItemIds = null,
                        MinQuality = 0,
                        MinLevel = 0,
                        MinEnhance = 0,
                        ReturnOnFail = false
                    });

                snapshotAvailable = true;
                Debug.Log($"[CraftRefundService] Using ExecutionSnapshot.Cost.Materials for job {job.JobId}");
            }
            else if (job.IngredientsSnapshot != null && job.IngredientsSnapshot.Length > 0)
            {
                ingredientSource = Array.ConvertAll(
                    job.IngredientsSnapshot,
                    s => new CraftIngredient
                    {
                        ItemId = s.ItemId,
                        Count = s.Count,
                        Consumed = s.Consumed,
                        CanSubstitute = s.CanSubstitute,
                        SubstituteItemIds = s.SubstituteItemIds,
                        MinQuality = s.MinQuality,
                        MinLevel = s.MinLevel,
                        MinEnhance = s.MinEnhance,
                        ReturnOnFail = s.ReturnOnFail
                    });

                snapshotAvailable = true;
                Debug.Log($"[CraftRefundService] Using ingredient snapshot for job {job.JobId}");
            }
            else if (recipe.Ingredients != null)
            {
                ingredientSource = recipe.Ingredients;
                Debug.LogWarning(
                    $"[CraftRefundService] Job {job.JobId} missing IngredientsSnapshot — falling back to live recipe (legacy path)");
            }

            if (ingredientSource != null)
            {
                foreach (var ingredient in ingredientSource)
                {
                    if (!ingredient.Consumed) continue;

                    int refundCount = snapshotAvailable
                        ? Mathf.RoundToInt(ingredient.Count * refundRate)
                        : Mathf.RoundToInt(ingredient.Count * job.Count * refundRate);

                    if (refundCount > 0)
                        _inventory.AddItem(ingredient.ItemId, refundCount);
                }
            }

            // P0-B step 11: refund decomposed requirements (Cost.Progression) if ExecutionSnapshot exists.
            // Cost.Progression[i].Count is pre-scaled by job.Count via SumPerJob — no extra multiplier.
            // Null-guard handles legacy jobs (created before P0-B step 10) that lack ExecutionSnapshot.
            if (job.ExecutionSnapshot?.Cost.Progression != null)
            {
                foreach (var prog in job.ExecutionSnapshot.Cost.Progression)
                {
                    int refundCount = Mathf.RoundToInt(prog.Count * refundRate);
                    if (refundCount > 0)
                        _inventory.AddItem(prog.ItemId, refundCount);
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