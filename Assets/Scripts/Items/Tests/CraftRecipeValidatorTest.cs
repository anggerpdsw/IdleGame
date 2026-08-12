using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Items.Tests
{
    /// <summary>
    /// EditMode test: validate all 66 craft recipes across 4-layer validation.
    /// Bootstrap: ItemDatabase (MonoBehaviour) + CraftRecipeRepository + CraftingConfig.
    /// Service args (inventory, economy) are null — ValidateItems/Design/Economy are pure.
    ///</summary>
    public class CraftRecipeValidatorTest
    {
        private ItemDatabase _db;
        private CraftRecipeRepository _repo;
        private CraftValidator _validator;

        [SetUp]
        public void SetUp()
        {
            // ItemDatabase is MonoBehaviour singleton — must attach to GameObject for Awake() to run
            var go = new GameObject("ItemDatabase_TestHost");
            _db = go.AddComponent<ItemDatabase>();
            _db.Initialize();

            // CraftingConfig singleton — idempotent
            CraftingConfig.Load();

            // Repository loads from DB equipment + 11 recipe JSON files
            _repo = new CraftRecipeRepository();
            _repo.Initialize();

            // Validator with null services — pure validation paths don't touch them
            _validator = new CraftValidator(_repo, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_db != null) UnityEngine.Object.DestroyImmediate(_db.gameObject);
            _db = null;
            _repo = null;
            _validator = null;
        }

        [Test]
        public void AllRecipes_LoadedFromJson_AtLeast66()
        {
            int count = _repo.AllRecipes.Count;
            Assert.GreaterOrEqual(count, 66,
                $"Expected >= 66 recipes (11 slots x 6 rarities), got {count}. " +
                $"Check Resources/Data/Crafting/dataRecipe*.json files.");
        }

        [Test]
        public void AllRecipes_PassValidateItems()
        {
            var failures = new List<string>();
            foreach (var recipe in _repo.AllRecipes.Values)
            {
                var result = _validator.ValidateItems(recipe);
                if (!result.IsSuccess)
                    failures.Add($"[{recipe.RecipeId}] {result.Reason}");
            }

            Assert.That(failures, Is.Empty,
                $"ValidateItems failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        [Test]
        public void AllRecipes_PassValidateDesign()
        {
            var failures = new List<string>();
            foreach (var recipe in _repo.AllRecipes.Values)
            {
                var result = _validator.ValidateDesign(recipe);
                if (!result.IsSuccess)
                    failures.Add($"[{recipe.RecipeId}] {result.Reason}");
            }

            Assert.That(failures, Is.Empty,
                $"ValidateDesign failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        [Test]
        public void AllRecipes_PassValidateEconomy()
        {
            var failures = new List<string>();
            foreach (var recipe in _repo.AllRecipes.Values)
            {
                var result = _validator.ValidateEconomy(recipe);
                if (!result.IsSuccess)
                    failures.Add($"[{recipe.RecipeId}] {result.Reason}");
            }

            Assert.That(failures, Is.Empty,
                $"ValidateEconomy failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        [Test]
        public void AllRecipes_HaveR6SpecialIngredient()
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

                if (!hasSpecial)
                    failures.Add($"[{recipe.RecipeId}] R6 missing Special family ingredient");
            }

            Assert.That(failures, Is.Empty,
                $"R6 Special requirement failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        [Test]
        public void AllRecipes_HaveWaterCatalyst()
        {
            var failures = new List<string>();
            foreach (var recipe in _repo.AllRecipes.Values)
            {
                var ingredients = recipe.Ingredients ?? Array.Empty<CraftIngredient>();
                bool hasWater = ingredients.Any(ing => ing.ItemId == "water");

                if (!hasWater)
                    failures.Add($"[{recipe.RecipeId}] missing water catalyst");
            }

            Assert.That(failures, Is.Empty,
                $"Water catalyst failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        [Test]
        public void AllRecipes_MonotonicWeightedCost_PerSlot()
        {
            var groupedBySlot = _repo.AllRecipes.Values
                .GroupBy(r => r.EquipmentType)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Rarity).ToList());

            var failures = new List<string>();
            var config = CraftingConfig.Load();

            foreach (var kvp in groupedBySlot)
            {
                var recipes = kvp.Value;
                for (int i = 1; i < recipes.Count; i++)
                {
                    var prev = ComputeCost(recipes[i - 1], config);
                    var curr = ComputeCost(recipes[i], config);

                    if (curr <= prev)
                        failures.Add($"[{kvp.Key}] {recipes[i].RecipeId} cost {curr:F2} <= prev {recipes[i - 1].RecipeId} cost {prev:F2}");
                }
            }

            Assert.That(failures, Is.Empty,
                $"Monotonic weighted cost failures ({failures.Count}):\n" + string.Join("\n", failures));
        }

        private double ComputeCost(CraftRecipeData recipe, CraftingConfig config)
        {
            if (recipe.Ingredients == null) return 0;
            double total = 0;
            foreach (var ing in recipe.Ingredients)
            {
                var item = _db.GetItem(ing.ItemId);
                if (item == null) continue;

                double weight = item.Role switch
                {
                    ItemRole.Material => config.GetWeight(item.CraftingFamily),
                    ItemRole.Catalyst => config.GetWeight(CraftingFamily.Water),
                    ItemRole.Progression => config.ProgressionWeight,
                    _ => 0
                };
                total += ing.Count * weight;
            }
            return total;
        }
    }
}
