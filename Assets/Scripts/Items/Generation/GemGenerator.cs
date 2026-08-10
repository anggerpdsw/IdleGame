using System;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for gem items.
    /// </summary>
    public sealed class GemGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly RarityRollService _rarityRoll;
        private readonly ItemValidator _validator;

        public GemGenerator(
            IRandomProvider rng,
            RarityRollService rarityRoll = null,
            ItemValidator validator = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _rarityRoll = rarityRoll ?? new RarityRollService(_rng);
            _validator = validator ?? new ItemValidator();
        }

        /// <summary>
        /// Generates a gem from a specific base template with context.
        /// </summary>
        public InventoryItem Generate(GemData baseGem, ItemGenerationContext context)
        {
            if (baseGem == null) return null;

            // 1. Determine rarity
            Rarity rarity = context.ForcedQuality.HasValue
                ? (Rarity)Math.Clamp(context.ForcedQuality.Value, 1, 8)
                : _rarityRoll.RollRarity(context.With(category: ItemCategory.Gem));

            // 2. Determine level
            int level = context.FixedLevel ?? CalculateLevel(baseGem, context);

            // 3. Create base item
            var item = CreateBaseItem(baseGem, rarity, level);

            // 4. Apply event modifiers
            ApplyEventModifiers(item, baseGem, rarity, level, context);

            // 5. Validate
            var validation = _validator.Validate(item, baseGem);
            if (!validation.IsValid)
            {
                UnityEngine.Debug.LogWarning($"[GemGenerator] Validation failed for {baseGem.GemId}: {validation}");
            }

            return item;
        }

        /// <summary>
        /// Generates a random gem of a specific type.
        /// </summary>
        public InventoryItem GenerateRandom(GemType type, int tier, int wave, long luck = 0, float rarityBoost = 0f, int? seed = null)
        {
            var baseGems = ItemDatabase.Instance?.GetGemsByType(type)?.ToList();
            if (baseGems == null || baseGems.Count == 0) return null;

            var baseGem = _rng.Choice(baseGems);
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, luck, seed)
                .With(gemType: type, category: ItemCategory.Gem);

            return Generate(baseGem, context);
        }

        private InventoryItem CreateBaseItem(GemData baseGem, Rarity rarity, int level)
        {
            return new InventoryItem
            {
                ItemId = baseGem.GemId,
                Quantity = 1,
                Level = Math.Clamp(level, 1, baseGem.MaxLevel),
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private int CalculateLevel(GemData baseGem, ItemGenerationContext context)
        {
            int baseLevel = Math.Max(1, context.PlayerLevel);
            int tierLevel = context.Tier * 2;
            int waveLevel = context.Wave / 5;

            int level = baseLevel + tierLevel + waveLevel;
            return Math.Clamp(level, 1, baseGem.MaxLevel);
        }

        private void ApplyEventModifiers(InventoryItem item, GemData baseGem, Rarity rarity, int level, ItemGenerationContext context)
        {
            if (context.EventModifiers == null) return;

            foreach (var modifier in context.EventModifiers)
            {
                if (modifier is IGemModifier gemMod)
                {
                    gemMod.ModifyGem(item, baseGem, rarity, level, context);
                }
            }
        }
    }

    public interface IGemModifier
    {
        void ModifyGem(InventoryItem item, GemData baseGem, Rarity rarity, int level, ItemGenerationContext context);
    }
}