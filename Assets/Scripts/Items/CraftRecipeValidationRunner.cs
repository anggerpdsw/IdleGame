using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Pure validation runner — no NUnit, no Editor dependency, no UnityEngine.Object.
    /// Reusable from runtime, Editor menu, and CLI -executeMethod.
    /// Validates all 66 recipes across 6 invariant layers.
    ///</summary>
    public sealed class CraftRecipeValidationRunner
    {
        public struct Report
        {
            public int TotalRecipes;
            public bool ItemsPass;
            public bool DesignPass;
            public bool EconomyPass;
            public bool R6SpecialPass;
            public bool WaterCatalystPass;
            public bool MonotonicCostPass;
            public List<string> Failures;

            public bool AllPass => ItemsPass && DesignPass && EconomyPass
                                   && R6SpecialPass && WaterCatalystPass && MonotonicCostPass
                                   && (Failures == null || Failures.Count == 0);

            public int FailedCount => Failures?.Count ?? 0;
        }

        private readonly CraftRecipeRepository _repo;
        private readonly ItemDatabase _db;
        private readonly CraftingConfig _config;
        private readonly CraftValidator _validator;

        public CraftRecipeValidationRunner(CraftRecipeRepository repo, ItemDatabase db, CraftingConfig config, CraftValidator validator)
        {
            _repo = repo;
            _db = db;
            _config = config;
            _validator = validator;
        }

        /// <summary>
        /// Run all 6 validation layers against all loaded recipes.
        /// Returns a Report. No Unity log calls — caller decides how to surface results.
        ///</summary>
        public Report RunAll()
        {
            var report = new Report
            {
                TotalRecipes = _repo.AllRecipes.Count,
                Failures = new List<string>()
            };

            if (report.TotalRecipes == 0)
            {
                report.Failures.Add("No recipes loaded — cannot validate.");
                return report;
            }

            // Layer 2: ValidateItems
            {
                var failures = new List<string>();
                foreach (var recipe in _repo.AllRecipes.Values)
                {
                    var result = _validator.ValidateItems(recipe);
                    if (!result.IsSuccess) failures.Add($"[Items][{recipe.RecipeId}] {result.Reason}");
                }
                report.ItemsPass = failures.Count == 0;
                if (!report.ItemsPass) report.Failures.AddRange(failures);
            }

            // Layer 3: ValidateDesign
            {
                var failures = new List<string>();
                foreach (var recipe in _repo.AllRecipes.Values)
                {
                    var result = _validator.ValidateDesign(recipe);
                    if (!result.IsSuccess) failures.Add($"[Design][{recipe.RecipeId}] {result.Reason}");
                }
                report.DesignPass = failures.Count == 0;
                if (!report.DesignPass) report.Failures.AddRange(failures);
            }

            // Layer 4: ValidateEconomy
            {
                var failures = new List<string>();
                foreach (var recipe in _repo.AllRecipes.Values)
                {
                    var result = _validator.ValidateEconomy(recipe);
                    if (!result.IsSuccess) failures.Add($"[Economy][{recipe.RecipeId}] {result.Reason}");
                }
                report.EconomyPass = failures.Count == 0;
                if (!report.EconomyPass) report.Failures.AddRange(failures);
            }

            // R6 Special requirement
            {
                var failures = new List<string>();
                foreach (var recipe in _repo.AllRecipes.Values.Where(r => r.Rarity == 6))
                {
                    var ingredients = recipe.Ingredients ?? Array.Empty<CraftIngredient>();
                    bool hasSpecial = ingredients.Any(ing =>
                    {
                        var item = _db.GetItem(ing.ItemId);
                        return item != null && item.CraftingFamily == CraftingFamily.Special;
                    });
                    if (!hasSpecial) failures.Add($"[R6Special][{recipe.RecipeId}] R6 missing Special family ingredient");
                }
                report.R6SpecialPass = failures.Count == 0;
                if (!report.R6SpecialPass) report.Failures.AddRange(failures);
            }

            // Water Catalyst requirement
            {
                var failures = new List<string>();
                foreach (var recipe in _repo.AllRecipes.Values)
                {
                    var ingredients = recipe.Ingredients ?? Array.Empty<CraftIngredient>();
                    bool hasWater = ingredients.Any(ing => ing.ItemId == "water");
                    if (!hasWater) failures.Add($"[WaterCatalyst][{recipe.RecipeId}] missing water catalyst");
                }
                report.WaterCatalystPass = failures.Count == 0;
                if (!report.WaterCatalystPass) report.Failures.AddRange(failures);
            }

            // Monotonic Weighted Cost per slot
            {
                var failures = new List<string>();
                var groupedBySlot = _repo.AllRecipes.Values
                    .GroupBy(r => r.EquipmentType)
                    .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Rarity).ToList());

                foreach (var kvp in groupedBySlot)
                {
                    var recipes = kvp.Value;
                    for (int i = 1; i < recipes.Count; i++)
                    {
                        var prev = ComputeCost(recipes[i - 1]);
                        var curr = ComputeCost(recipes[i]);
                        if (curr <= prev)
                            failures.Add($"[MonotonicCost][{kvp.Key}] {recipes[i].RecipeId} cost {curr:F2} <= prev {recipes[i - 1].RecipeId} cost {prev:F2}");
                    }
                }
                report.MonotonicCostPass = failures.Count == 0;
                if (!report.MonotonicCostPass) report.Failures.AddRange(failures);
            }

            return report;
        }

        private double ComputeCost(CraftRecipeData recipe)
        {
            if (recipe.Ingredients == null) return 0;
            double total = 0;
            foreach (var ing in recipe.Ingredients)
            {
                var item = _db.GetItem(ing.ItemId);
                if (item == null) continue;

                double weight = item.Role switch
                {
                    ItemRole.Material => _config.GetWeight(item.CraftingFamily),
                    ItemRole.Catalyst => _config.GetWeight(CraftingFamily.Water),
                    ItemRole.Progression => _config.ProgressionWeight,
                    _ => 0
                };
                total += ing.Count * weight;
            }
            return total;
        }
    }
}
