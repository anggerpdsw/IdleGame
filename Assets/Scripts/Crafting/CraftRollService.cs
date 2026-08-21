using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Crafting
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

        /// <summary>Access to the default RNG provider for snapshot building (P0-C).</summary>
        public IRandomProvider RngProvider => _defaultRng;

        /// <summary>
        /// Rolls a craft with a specific seed for deterministic results (testing/replay).
        /// </summary>
        public CraftRollResult RollCraftSeeded(string recipeId, CraftContext context, int seed)
        {
            var seededRng = new SeedRandomProvider(seed);
            return RollCraft(recipeId, context, seededRng);
        }

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
        /// Gets the pipeline for advanced usage (adding custom stages, etc.)
        /// </summary>
        public CraftPipeline GetPipeline() => _pipeline;
    }

}