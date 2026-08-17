using NUnit.Framework;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Controller.Tests
{
    /// <summary>
    /// EditMode tests for CraftingUIController's pure decision logic.
    /// These methods are extracted as static so they can be tested without Unity scene setup.
    /// </summary>
    [TestFixture]
    public class CraftingUIControllerTests
    {
        #region ClampQuantity

        [Test]
        public void ClampQuantity_BelowOne_ReturnsOne()
        {
            Assert.AreEqual(1, CraftingUIController.ClampQuantity(0));
            Assert.AreEqual(1, CraftingUIController.ClampQuantity(-5));
            Assert.AreEqual(1, CraftingUIController.ClampQuantity(int.MinValue));
        }

        [Test]
        public void ClampQuantity_AtOrAboveOne_ReturnsInput()
        {
            Assert.AreEqual(1, CraftingUIController.ClampQuantity(1));
            Assert.AreEqual(5, CraftingUIController.ClampQuantity(5));
            Assert.AreEqual(100, CraftingUIController.ClampQuantity(100));
        }

        #endregion

        #region CanAffordCurrency

        [Test]
        public void CanAffordCurrency_NullPreview_ReturnsFalse()
        {
            Assert.IsFalse(CraftingUIController.CanAffordCurrency(null, 1000, 1000));
        }

        [Test]
        public void CanAffordCurrency_ExactMatch_ReturnsTrue()
        {
            var cost = new CurrencySnapshot(100, 50);
            Assert.IsTrue(CraftingUIController.CanAffordCurrency(cost, 100, 50));
        }

        [Test]
        public void CanAffordCurrency_Surplus_ReturnsTrue()
        {
            var cost = new CurrencySnapshot(100, 50);
            Assert.IsTrue(CraftingUIController.CanAffordCurrency(cost, 1000, 1000));
        }

        [Test]
        public void CanAffordCurrency_ShortGold_ReturnsFalse()
        {
            var cost = new CurrencySnapshot(100, 50);
            Assert.IsFalse(CraftingUIController.CanAffordCurrency(cost, 99, 50));
        }

        [Test]
        public void CanAffordCurrency_ShortGem_ReturnsFalse()
        {
            var cost = new CurrencySnapshot(100, 50);
            Assert.IsFalse(CraftingUIController.CanAffordCurrency(cost, 100, 49));
        }

        [Test]
        public void CanAffordCurrency_ZeroCost_ReturnsTrue()
        {
            var cost = new CurrencySnapshot(0, 0);
            Assert.IsTrue(CraftingUIController.CanAffordCurrency(cost, 0, 0));
        }

        #endregion

        #region CanAffordMaterials

        [Test]
        public void CanAffordMaterials_NullReqs_ReturnsTrue()
        {
            Assert.IsTrue(CraftingUIController.CanAffordMaterials(null, _ => 0));
        }

        [Test]
        public void CanAffordMaterials_EmptyReqs_ReturnsTrue()
        {
            Assert.IsTrue(CraftingUIController.CanAffordMaterials(new IngredientCost[0], _ => 0));
        }

        [Test]
        public void CanAffordMaterials_ExactMatch_ReturnsTrue()
        {
            var reqs = new[] { new IngredientCost { ItemId = "mat_a", Count = 10 } };
            Assert.IsTrue(CraftingUIController.CanAffordMaterials(reqs, id => id == "mat_a" ? 10 : 0));
        }

        [Test]
        public void CanAffordMaterials_Surplus_ReturnsTrue()
        {
            var reqs = new[] { new IngredientCost { ItemId = "mat_a", Count = 10 } };
            Assert.IsTrue(CraftingUIController.CanAffordMaterials(reqs, id => id == "mat_a" ? 100 : 0));
        }

        [Test]
        public void CanAffordMaterials_Shortage_ReturnsFalse()
        {
            var reqs = new[] { new IngredientCost { ItemId = "mat_a", Count = 10 } };
            Assert.IsFalse(CraftingUIController.CanAffordMaterials(reqs, id => id == "mat_a" ? 9 : 0));
        }

        [Test]
        public void CanAffordMaterials_MultipleReqs_AllMustPass()
        {
            var reqs = new[]
            {
                new IngredientCost { ItemId = "mat_a", Count = 10 },
                new IngredientCost { ItemId = "mat_b", Count = 5 }
            };
            Assert.IsTrue(CraftingUIController.CanAffordMaterials(reqs, id => id == "mat_a" ? 10 : (id == "mat_b" ? 5 : 0)));
            Assert.IsFalse(CraftingUIController.CanAffordMaterials(reqs, id => id == "mat_a" ? 10 : (id == "mat_b" ? 4 : 0)));
        }

        #endregion
    }

    /// <summary>
    /// Minimal hand-rolled fake for CraftService (no inheritance — sealed class).
    /// Only implements the subset of API the controller actually calls.
    /// </summary>
    public sealed class FakeCraftService
    {
        private readonly Dictionary<string, CraftRecipeData> _recipes = new();
        private readonly Dictionary<string, CurrencySnapshot> _costs = new();
        private readonly Dictionary<string, IngredientCost[]> _materials = new();
        private readonly Dictionary<string, ValidationResult> _validations = new();
        private int _jobCounter = 1;

        public void AddRecipe(CraftRecipeData recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.RecipeId)) return;
            _recipes[recipe.RecipeId] = recipe;
        }

        public void SetCost(string recipeId, CurrencySnapshot cost) => _costs[recipeId] = cost;
        public void SetMaterials(string recipeId, IngredientCost[] mats) => _materials[recipeId] = mats;
        public void SetValidation(string recipeId, ValidationResult result) => _validations[recipeId] = result;

        public IReadOnlyList<CraftRecipeData> GetKnownRecipes() => new List<CraftRecipeData>(_recipes.Values).AsReadOnly();

        public bool TryGetRecipe(string recipeId, out CraftRecipeData recipe) => _recipes.TryGetValue(recipeId, out recipe);

        public CurrencySnapshot? GetRecipeCostPreview(string recipeId, int count = 1)
        {
            if (!_costs.TryGetValue(recipeId, out var cost)) return null;
            return new CurrencySnapshot(cost.GoldSnapshot * count, cost.GemSnapshot * count);
        }

        public IngredientCost[] GetRecipeMaterialPreview(string recipeId, int count = 1)
        {
            if (!_materials.TryGetValue(recipeId, out var mats)) return new IngredientCost[0];
            return System.Array.ConvertAll(mats, m => new IngredientCost { ItemId = m.ItemId, Count = m.Count * count });
        }

        public ValidationResult CanCraft(string recipeId, int count = 1)
        {
            if (_validations.TryGetValue(recipeId, out var v)) return v;
            return ValidationResult.Success();
        }

        public string StartCraft(string recipeId, int count = 1)
        {
            if (!_recipes.ContainsKey(recipeId)) return null;
            return "job_" + _jobCounter++;
        }
    }

    /// <summary>
    /// Minimal hand-rolled fake for InventoryService (no inheritance).
    /// </summary>
    public sealed class FakeInventoryService
    {
        private readonly Dictionary<string, int> _quantities = new();

        public void SetQuantity(string itemId, int qty) => _quantities[itemId] = qty;

        public int GetTotalQuantity(string itemId) => _quantities.TryGetValue(itemId, out var q) ? q : 0;
    }

    /// <summary>
    /// Minimal hand-rolled fake for EconomyManager (no inheritance).
    /// </summary>
    public sealed class FakeEconomyManager
    {
        private long _gold = 1000;
        private long _gem = 1000;

        public void SetCurrency(CurrencyType type, long amount)
        {
            if (type == CurrencyType.Gold) _gold = amount;
            else if (type == CurrencyType.Gem) _gem = amount;
        }

        public long GetCurrency(CurrencyType type)
        {
            if (type == CurrencyType.Gold) return _gold;
            if (type == CurrencyType.Gem) return _gem;
            return 0;
        }
    }
}