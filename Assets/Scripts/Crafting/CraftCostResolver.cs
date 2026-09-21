using System;
using System.Linq;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Pure cost resolver for craft currency computation.
    /// Extracts the deterministic cost calculation from CraftSnapshotBuilder.Build()
    /// (lines 55-58) so it can be used by both the transaction path and a
    /// read-only preview API without invoking the impure snapshot builder
    /// (which generates a CompletionSeed via RNG).
    /// </summary>
    public static class CraftCostResolver
    {
        /// <summary>
        /// Compute the currency cost snapshot for a recipe and count.
        /// Base costs from GameConstants scaled by rarity multiplier:
        ///   Equipment: BASE_CRAFT_EQUIPMENT_GOLD_COST/MEAT_COST * rarity_mul * count
        ///   Potion: BASE_CRAFT_POTION_GOLD_COST/MEAT_COST * rarity_mul * count
        ///   Gem: BASE_CRAFT_*_GEM_COST * rarity_mul * count (only for highest rarity)
        ///   Gold = scaled base + recipe.GoldCost
        ///   Meat injected into AdditionalCosts
        /// Deterministic; no RNG, no singleton access, no side effects.
        /// </summary>
        public static CurrencySnapshot ComputeCurrencyCost(CraftRecipeData recipe, int count)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (count < 1) count = 1;

            // base gold/meat/gem per category
            long baseGold = 0;
            long baseMeat = 0;
            long baseGem = 0;
            if (recipe.IsEquipment)
            {
                baseGold = GameConstants.BASE_CRAFT_EQUIPMENT_GOLD_COST;
                baseMeat = GameConstants.BASE_CRAFT_EQUIPMENT_MEAT_COST;
                baseGem = GameConstants.BASE_CRAFT_EQUIPMENT_GEM_COST;
            }
            else if (recipe.PotionType != PotionType.None)
            {
                baseGold = GameConstants.BASE_CRAFT_POTION_GOLD_COST;
                baseMeat = GameConstants.BASE_CRAFT_POTION_MEAT_COST;
                baseGem = GameConstants.BASE_CRAFT_POTION_GEM_COST;
            }
            else if (recipe.IsEgg)
            {
                baseGold = GameConstants.BASE_CRAFT_PET_GOLD_COST;
                baseMeat = GameConstants.BASE_CRAFT_PET_MEAT_COST;
                baseGem = GameConstants.BASE_CRAFT_PET_GEM_COST;
            }

            // rarity multiplier from config (1.0..4.5)
            float rarityMul = CraftingConfig.Load().GetRarityMultiplier(recipe.Rarity);
            long scaledGold = (long)(baseGold * rarityMul * count);
            long scaledMeat = (long)(baseMeat * rarityMul * count);

            // gem cost only for highest rarity (Divine = 6)
            long scaledGem = 0;
            if (recipe.Rarity >= GameConstants.RARITY_COUNT)
            {
                scaledGem = (long)(baseGem * rarityMul * count);
            }

            // preserve existing additional costs
            var additionalCosts = recipe.AdditionalCosts != null
                ? Array.ConvertAll(recipe.AdditionalCosts, c => new CostEntry
                {
                    CurrencyId = c.Currency.ToString(),
                    Amount = c.Amount * count
                })
                : Array.Empty<CostEntry>();

            // inject meat if non-zero (merge, avoid duplicate)
            if (scaledMeat > 0)
            {
                var list = additionalCosts.ToList();
                var meatId = CurrencyType.Meat.ToString();
                var existing = list.FirstOrDefault(e => e.CurrencyId == meatId);
                if (existing.CurrencyId != null)
                {
                    existing.Amount += scaledMeat;
                }
                else
                {
                    list.Add(new CostEntry { CurrencyId = meatId, Amount = scaledMeat });
                }
                additionalCosts = list.ToArray();
            }

            // total gold = scaled base + recipe override
            // total gem = scaled base (rarity 6 only) + recipe override
            long totalGold = scaledGold + (recipe.GoldCost * count);
            long totalGem = scaledGem + (recipe.GemCost * count);

            return new CurrencySnapshot(totalGold, totalGem, additionalCosts);
        }
    }
}
