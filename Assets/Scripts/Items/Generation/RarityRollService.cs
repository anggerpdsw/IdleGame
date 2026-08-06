using System;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Service for determining item rarity based on various factors.
    /// Data-driven: uses weights from configuration, modified by luck, tier, wave, events.
    /// </summary>
    public sealed class RarityRollService
    {
        private readonly IRandomProvider _rng;
        private readonly RarityWeightConfig _config;

        public RarityRollService(IRandomProvider rng, RarityWeightConfig config = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _config = config ?? RarityWeightConfig.Default;
        }

        /// <summary>
        /// Rolls rarity based on context.
        /// </summary>
        public ItemRarity RollRarity(ItemGenerationContext context)
        {
            // If forced quality, use that
            if (context.ForcedQuality.HasValue)
            {
                return (ItemRarity)Math.Clamp(context.ForcedQuality.Value, 1, 8);
            }

            // Get base weights for this tier/wave
            var weights = _config.GetWeightsForTier(context.Tier, context.Wave);

            // Apply modifiers
            weights = ApplyModifiers(weights, context);

            // Roll
            return RollFromWeights(weights);
        }

        /// <summary>
        /// Rolls rarity for equipment type.
        /// </summary>
        public ItemRarity RollRarityForEquipment(EquipmentType type, int tier, int wave, long luck = 0, float rarityBoost = 0f, int? seed = null)
        {
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, luck, seed)
                .With(equipmentType: type, category: ItemCategory.Equipment);
            return RollRarity(context);
        }

        /// <summary>
        /// Rolls rarity for gem.
        /// </summary>
        public ItemRarity RollRarityForGem(GemType type, int tier, int wave, long luck = 0, float rarityBoost = 0f, int? seed = null)
        {
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, luck, seed)
                .With(gemType: type, category: ItemCategory.Gem);
            return RollRarity(context);
        }

        private RarityWeightArray ApplyModifiers(RarityWeightArray weights, ItemGenerationContext context)
        {
            var modified = new float[weights.Weights.Length];
            Array.Copy(weights.Weights, modified, weights.Weights.Length);

            // Luck modifier: shifts weights toward higher rarities
            if (context.Luck > 0)
            {
                float luckFactor = Math.Min(context.Luck * 0.001f, 0.5f); // Cap at 50% shift
                for (int i = 1; i < modified.Length; i++)
                {
                    modified[i] *= 1f + luckFactor * (i * 0.1f);
                }
            }

            // ItemRarity boost (from events, consumables, etc.)
            if (context.RarityBoost > 0)
            {
                for (int i = 1; i < modified.Length; i++)
                {
                    modified[i] *= 1f + context.RarityBoost * (i * 0.05f);
                }
            }

            // Tier/wave already baked into base weights

            // Event modifiers
            if (context.EventModifiers != null)
            {
                foreach (var modifier in context.EventModifiers)
                {
                    if (modifier is IRarityModifier rarityMod)
                    {
                        rarityMod.ModifyRarityWeights(modified, context);
                    }
                }
            }

            return new RarityWeightArray { Weights = modified };
        }

        private ItemRarity RollFromWeights(RarityWeightArray weights)
        {
            float total = 0f;
            foreach (var w in weights.Weights) total += w;

            float roll = _rng.NextFloat() * total;
            float accum = 0f;

            for (int i = 0; i < weights.Weights.Length; i++)
            {
                accum += weights.Weights[i];
                if (roll <= accum)
                    return (ItemRarity)i;
            }

            return ItemRarity.Common;
        }
    }

    /// <summary>
    /// Configuration for rarity weights by tier/wave.
    /// </summary>
    [Serializable]
    public class RarityWeightConfig
    {
        public RarityWeightEntry[] Entries;

        public static RarityWeightConfig Default => new()
        {
            Entries = new[]
            {
                new RarityWeightEntry { MinTier = 1, MaxTier = 5, Weights = new float[] { 100, 50, 20, 5, 1, 0.2f, 0.05f, 0.01f, 0.001f } },
                new RarityWeightEntry { MinTier = 6, MaxTier = 10, Weights = new float[] { 80, 40, 25, 10, 3, 1, 0.2f, 0.05f, 0.005f } },
                new RarityWeightEntry { MinTier = 11, MaxTier = 20, Weights = new float[] { 50, 30, 25, 15, 5, 2, 0.5f, 0.1f, 0.01f } },
                new RarityWeightEntry { MinTier = 21, MaxTier = 50, Weights = new float[] { 30, 25, 20, 15, 8, 3, 1, 0.3f, 0.05f } },
                new RarityWeightEntry { MinTier = 51, MaxTier = int.MaxValue, Weights = new float[] { 10, 20, 20, 15, 10, 5, 2, 1, 0.2f } },
            }
        };

        public RarityWeightArray GetWeightsForTier(int tier, int wave)
        {
            var entry = Array.Find(Entries, e => tier >= e.MinTier && tier <= e.MaxTier);
            if (entry == null) entry = Entries[^1];

            var weights = new float[entry.Weights.Length];
            Array.Copy(entry.Weights, weights, entry.Weights.Length);

            // Wave modifier within tier
            float waveFactor = Math.Min(wave * 0.01f, 0.5f);
            for (int i = 1; i < weights.Length; i++)
            {
                weights[i] *= 1f + waveFactor * (i * 0.05f);
            }

            return new RarityWeightArray { Weights = weights };
        }
    }

    [Serializable]
    public class RarityWeightEntry
    {
        public int MinTier;
        public int MaxTier;
        public float[] Weights; // Index = ItemRarity enum value
    }

    public struct RarityWeightArray
    {
        public float[] Weights;
    }

    /// <summary>
    /// Interface for modifiers that affect rarity weights.
    /// </summary>
    public interface IRarityModifier
    {
        void ModifyRarityWeights(float[] weights, ItemGenerationContext context);
    }
}