using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Tests
{
    /// <summary>
    /// Regression tests proving CraftCostResolver.ComputeCurrencyCost()
    /// produces identical output to the prior inline cost calculation
    /// in CraftSnapshotBuilder.Build().
    ///
    /// Invariant: resolver.Compute(recipe, count) == Build(recipe, count, rng, ingredients).Cost.Currency
    ///
    /// The RNG is only consumed for CompletionSeed generation (not cost),
    /// so any IRandomProvider is acceptable for these equality checks.
    /// </summary>
    [TestFixture]
    public class CraftCostResolverTests
    {
        private class DummyRngProvider : IRandomProvider
        {
            private int _counter = 1;

            public int NextInt(int minInclusive, int maxExclusive) => _counter++;
            public int NextInt(int maxExclusive) => _counter++;
            public float NextFloat() => 0.5f;
            public double NextDouble() => 0.5;
            public bool Chance(float probability) => probability > 0.5f;
            public bool ChancePercent(float percent) => percent > 50f;
            public float Range(float min, float max) => min;
            public int Range(int minInclusive, int maxExclusive) => _counter++;
            public T Choice<T>(T[] array) => array.Length > 0 ? array[0] : default;
            public T Choice<T>(IReadOnlyList<T> list) => list.Count > 0 ? list[0] : default;
            public void Shuffle<T>(T[] array) { }
            public void Shuffle<T>(IList<T> list) { }
        }

        private CraftRecipeData CreateRecipe(
            long goldCost = 100,
            long gemCost = 50,
            CurrencyCost[] additionalCosts = null)
        {
            return new CraftRecipeData
            {
                RecipeId = "test_recipe_cost",
                GoldCost = goldCost,
                GemCost = gemCost,
                AdditionalCosts = additionalCosts
            };
        }

        private CraftIngredientSnapshot[] EmptyIngredients() => Array.Empty<CraftIngredientSnapshot>();

        [Test]
        public void GoldOnly_Count1_Equality()
        {
            var recipe = CreateRecipe(goldCost: 200, gemCost: 0, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 1, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
            Assert.AreEqual(buildOutput.AdditionalCosts.Length, resolverOutput.AdditionalCosts.Length);
        }

        [Test]
        public void GoldOnly_Count3_Equality()
        {
            var recipe = CreateRecipe(goldCost: 150, gemCost: 0, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 3);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 3, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
        }

        [Test]
        public void GemOnly_Count1_Equality()
        {
            var recipe = CreateRecipe(goldCost: 0, gemCost: 75, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 1, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
        }

        [Test]
        public void GemOnly_Count5_Equality()
        {
            var recipe = CreateRecipe(goldCost: 0, gemCost: 40, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 5);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 5, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
        }

        [Test]
        public void GoldAndGem_Count1_Equality()
        {
            var recipe = CreateRecipe(goldCost: 100, gemCost: 30, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 1, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
        }

        [Test]
        public void GoldAndGem_Count3_Equality()
        {
            var recipe = CreateRecipe(goldCost: 80, gemCost: 20, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 3);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 3, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
        }

        [Test]
        public void AdditionalCosts_Count1_Equality()
        {
            var recipe = CreateRecipe(
                goldCost: 500,
                gemCost: 100,
                additionalCosts: new[]
                {
                    new CurrencyCost { Currency = CurrencyType.Gold, Amount = 5 },
                    new CurrencyCost { Currency = CurrencyType.Gem, Amount = 10 }
                });
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 1, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
            Assert.AreEqual(buildOutput.AdditionalCosts.Length, resolverOutput.AdditionalCosts.Length);

            for (int i = 0; i < buildOutput.AdditionalCosts.Length; i++)
            {
                Assert.AreEqual(buildOutput.AdditionalCosts[i].CurrencyId, resolverOutput.AdditionalCosts[i].CurrencyId);
                Assert.AreEqual(buildOutput.AdditionalCosts[i].Amount, resolverOutput.AdditionalCosts[i].Amount);
            }
        }

        [Test]
        public void AdditionalCosts_Count3_Equality()
        {
            var recipe = CreateRecipe(
                goldCost: 300,
                gemCost: 50,
                additionalCosts: new[]
                {
                    new CurrencyCost { Currency = CurrencyType.Gold, Amount = 3 },
                    new CurrencyCost { Currency = CurrencyType.Gem, Amount = 7 }
                });
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 3);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 3, rng, EmptyIngredients()).Cost.Currency;

            for (int i = 0; i < buildOutput.AdditionalCosts.Length; i++)
            {
                Assert.AreEqual(buildOutput.AdditionalCosts[i].CurrencyId, resolverOutput.AdditionalCosts[i].CurrencyId);
                Assert.AreEqual(buildOutput.AdditionalCosts[i].Amount, resolverOutput.AdditionalCosts[i].Amount);
            }
        }

        [Test]
        public void NoCurrency_Count1_Equality()
        {
            var recipe = CreateRecipe(goldCost: 0, gemCost: 0, additionalCosts: null);
            var rng = new DummyRngProvider();

            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);
            var buildOutput = CraftSnapshotBuilder.Build(recipe, 1, rng, EmptyIngredients()).Cost.Currency;

            Assert.AreEqual(buildOutput.GoldSnapshot, resolverOutput.GoldSnapshot);
            Assert.AreEqual(buildOutput.GemSnapshot, resolverOutput.GemSnapshot);
            Assert.AreEqual(buildOutput.AdditionalCosts.Length, resolverOutput.AdditionalCosts.Length);
        }

        [Test]
        public void ZeroGoldPreservesFormula_Count1()
        {
            var recipe = CreateRecipe(goldCost: 0, gemCost: 0, additionalCosts: null);
            var resolverOutput = CraftCostResolver.ComputeCurrencyCost(recipe, 1);

            Assert.AreEqual(0, resolverOutput.GoldSnapshot);
            Assert.AreEqual(0, resolverOutput.GemSnapshot);
        }
    }
}
