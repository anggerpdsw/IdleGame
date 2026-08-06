using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Craft roll service - orchestrates the craft pipeline.
    /// Uses IRandomProvider for deterministic RNG (testing, replay).
    /// </summary>
    public sealed class CraftRollService
    {
        private readonly CraftRecipeRepository _repository;
        private readonly CraftPipeline _pipeline;
        private readonly IRandomProvider _defaultRng;

        public CraftRollService(CraftRecipeRepository repository, IRandomProvider rng = null, CraftFormulasConfig config = null, IItemDatabase itemDatabase = null)
        {
            _repository = repository;
            _defaultRng = rng ?? new UnityRandomProvider();
            _pipeline = new CraftPipeline(config, itemDatabase);
        }

        // ============ Public API ============

        /// <summary>
        /// Rolls a craft using the pipeline architecture.
        /// </summary>
        public CraftRollResult RollCraft(string recipeId, CraftContext context, IRandomProvider rng = null)
        {
            if (!_repository.TryGetRecipe(recipeId, out var recipe))
            {
                return CraftRollResult.Fail($"Recipe not found: {recipeId}");
            }

            var provider = rng ?? _defaultRng;
            return _pipeline.Execute(recipe, context, provider);
        }

        /// <summary>
        /// Rolls a craft with a specific seed for deterministic results (testing/replay).
        /// </summary>
        public CraftRollResult RollCraftSeeded(string recipeId, CraftContext context, int seed)
        {
            var seededRng = new SeedRandomProvider(seed);
            return RollCraft(recipeId, context, seededRng);
        }

        /// <summary>
        /// Preview roll - shows what COULD happen without consuming resources.
        /// Runs multiple simulations and returns statistics.
        /// </summary>
        public CraftRollPreview PreviewCraft(string recipeId, CraftContext context, int simulations = 1000)
        {
            if (!_repository.TryGetRecipe(recipeId, out var recipe))
            {
                return new CraftRollPreview { Success = false, Error = $"Recipe not found: {recipeId}" };
            }

            var preview = new CraftRollPreview
            {
                RecipeId = recipeId,
                TotalSimulations = simulations,
                SuccessCount = 0,
                CriticalCount = 0,
                ItemDropCounts = new Dictionary<string, (int min, int max, float avgCount, float avgQuality)>(),
                ExpRewards = new List<long>()
            };

            for (int i = 0; i < simulations; i++)
            {
                var rng = new SeedRandomProvider(i); // Deterministic seeds for reproducibility
                var result = _pipeline.Execute(recipe, context, rng);

                if (result.Success)
                {
                    preview.SuccessCount++;
                    preview.ExpRewards.Add(result.ExpReward);

                    foreach (var entry in result.Entries)
                    {
                        if (!preview.ItemDropCounts.ContainsKey(entry.ItemId))
                        {
                            preview.ItemDropCounts[entry.ItemId] = (int.MaxValue, 0, 0f, 0f);
                        }

                        var current = preview.ItemDropCounts[entry.ItemId];
                        current.min = Math.Min(current.min, entry.Count);
                        current.max = Math.Max(current.max, entry.Count);
                        // We'll calculate avg after loop
                        preview.ItemDropCounts[entry.ItemId] = current;

                        if (entry.IsCritical) preview.CriticalCount++;
                    }
                }
            }

            // Calculate averages
            foreach (var kvp in preview.ItemDropCounts)
            {
                long totalCount = 0;
                long totalQuality = 0;
                int dropCount = 0;

                for (int i = 0; i < simulations; i++)
                {
                    var rng = new SeedRandomProvider(i);
                    var result = _pipeline.Execute(recipe, context, rng);
                    if (result.Success)
                    {
                        foreach (var entry in result.Entries)
                        {
                            if (entry.ItemId == kvp.Key)
                            {
                                totalCount += entry.Count;
                                totalQuality += entry.Quality;
                                dropCount++;
                            }
                        }
                    }
                }

                if (dropCount > 0)
                {
                    var current = preview.ItemDropCounts[kvp.Key];
                    current.avgCount = (float)totalCount / simulations; // Average per simulation
                    current.avgQuality = (float)totalQuality / dropCount; // Average quality when dropped
                    preview.ItemDropCounts[kvp.Key] = current;
                }
            }

            preview.SuccessRate = (float)preview.SuccessCount / simulations * 100f;
            preview.CriticalRate = preview.SuccessCount > 0 ? (float)preview.CriticalCount / preview.SuccessCount * 100f : 0f;

            if (preview.ExpRewards.Count > 0)
            {
                preview.AvgExpReward = preview.ExpRewards.Sum() / preview.ExpRewards.Count;
                preview.MinExpReward = preview.ExpRewards.Min();
                preview.MaxExpReward = preview.ExpRewards.Max();
            }

            return preview;
        }

        /// <summary>
        /// Gets the pipeline for advanced usage (adding custom stages, etc.)
        /// </summary>
        public CraftPipeline GetPipeline() => _pipeline;
    }

    /// <summary>
    /// Preview result for craft simulation.
    /// </summary>
    public class CraftRollPreview
    {
        public string RecipeId;
        public bool Success = true;
        public string Error;
        public int TotalSimulations;
        public int SuccessCount;
        public int CriticalCount;
        public float SuccessRate;
        public float CriticalRate;
        public Dictionary<string, (int min, int max, float avgCount, float avgQuality)> ItemDropCounts;
        public List<long> ExpRewards = new();
        public long AvgExpReward;
        public long MinExpReward;
        public long MaxExpReward;
    }
}