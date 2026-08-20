using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for equipment items.
    /// Pipeline: Clone Template → Roll Level → Roll ItemRarity → Generate Sockets → Generate Affix → Generate Secondary → Generate Enchant → Apply Event Modifier → Validate → Return
    /// </summary>
    public sealed class EquipmentGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly RarityRollService _rarityRoll;
        private readonly StatRollService _statRoll;
        private readonly SocketGenerator _socketGen;
        private readonly EnchantmentGenerator _enchantGen;
        private readonly AffixGenerator _affixGen;
        private readonly ItemValidator _validator;
        private readonly AttributeRollService _attributeRoll;

        public EquipmentGenerator(
            IRandomProvider rng,
            RarityRollService rarityRoll = null,
            StatRollService statRoll = null,
            SocketGenerator socketGen = null,
            EnchantmentGenerator enchantGen = null,
            AffixGenerator affixGen = null,
            ItemValidator validator = null,
            AttributeRollService attributeRoll = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _rarityRoll = rarityRoll ?? new RarityRollService(_rng);
            _statRoll = statRoll ?? new StatRollService(_rng);
            _socketGen = socketGen ?? new SocketGenerator(_rng);
            _enchantGen = enchantGen ?? new EnchantmentGenerator(_rng);
            _affixGen = affixGen ?? new AffixGenerator(_rng);
            _validator = validator ?? new ItemValidator();
            // Same RNG as the other roll services: attribute rolls are deterministic under a seeded provider (I-11).
            _attributeRoll = attributeRoll ?? new AttributeRollService(_rng);
        }

        /// <summary>
        /// Generates equipment from a specific base template with context.
        /// </summary>
        public InventoryItem Generate(EquipmentData baseEquipment, ItemGenerationContext context)
        {
            if (baseEquipment == null) return null;

            // 1. Determine rarity
            Rarity rarity = context.ForcedQuality.HasValue
                ? (Rarity)Math.Clamp(context.ForcedQuality.Value, 1, 8)
                : _rarityRoll.RollRarity(context.With(category: ItemCategory.Equipment));

            // 2. Determine level
            int level = context.FixedLevel ?? CalculateLevel(baseEquipment, context);

            // 3. Create base item
            var item = CreateBaseItem(baseEquipment, rarity, level, context);

            // 4. Generate secondary stats
            var secondaryStats = _statRoll.RollSecondaryStats(baseEquipment, rarity, context);
            if (secondaryStats.Length > 0)
                ApplySecondaryStats(item, secondaryStats);

            // 4b. Roll main attributes (v3.8 §20) — craft-sourced only; rarity here is
            // recipe.Rarity via context.ForcedQuality (set by CraftRewardService).
            if (context.Source == ItemSource.Craft)
            {
                var tierConfig = CraftingConfig.Load().GetAttributeTierConfig((int)rarity);
                var attributes = _attributeRoll.RollAttributes(rarity, tierConfig);
                if (attributes.Length > 0)
                {
                    item.CustomData ??= new Dictionary<string, object>();
                    item.CustomData["AttributeStats"] = attributes;
                }
            }

            // 5. Generate sockets
            item.Sockets = _socketGen.GenerateSockets(baseEquipment, rarity, context);

            // 6. Generate affixes
            var affixes = _affixGen.GenerateAffixes(baseEquipment, rarity, context);
            if (affixes.Length > 0)
                ApplyAffixes(item, affixes);

            // 7. Generate enchantment
            item.Enchantment = _enchantGen.GenerateEnchantment(baseEquipment, rarity, level, context);

            // 8. Apply event modifiers
            ApplyEventModifiers(item, baseEquipment, rarity, level, context);

            // 9. Validate
            var validation = _validator.Validate(item, baseEquipment);
            if (!validation.IsValid)
            {
                // Log warning but return item anyway
                UnityEngine.Debug.LogWarning($"[EquipmentGenerator] Validation failed for {baseEquipment.Id}: {validation}");
            }

            return item;
        }

        /// <summary>
        /// Generates random equipment of a specific type.
        /// </summary>
        public InventoryItem GenerateRandom(EquipmentType type, int tier, int wave, long luck = 0, float rarityBoost = 0f, int? seed = null)
        {
            var baseEquipments = ItemDatabase.Instance?.GetEquipmentByType(type)?.ToList();
            if (baseEquipments == null || baseEquipments.Count == 0) return null;

            var baseEquipment = _rng.Choice(baseEquipments);
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, luck, seed)
                .With(equipmentType: type, category: ItemCategory.Equipment);

            return Generate(baseEquipment, context);
        }

        private InventoryItem CreateBaseItem(EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context)
        {
            string outputItemId = baseEquipment.Id;
            if (context?.CustomData != null &&
                context.CustomData.TryGetValue("OverrideItemId", out var outputId) &&
                outputId != null)
            {
                outputItemId = outputId.ToString();
            }

            var item = new InventoryItem
            {
                InstanceId = Guid.NewGuid().ToString(),

                // ID item konkret hasil crafting. Contoh: cotton_hat
                ItemId = outputItemId,

                // ID template equipment yang menjadi sumber data. Contoh: equip_hat_base
                EquipmentTemplateId = baseEquipment.Id,

                Quantity = 1,
                Level = Math.Clamp(level, 1, baseEquipment.MaxLevel),
                MaxDurability = baseEquipment.MaxDurability,
                CurrentDurability = baseEquipment.MaxDurability,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                EnhanceLevel = 0
            };

            if (context?.CustomData != null && context.CustomData.Count > 0)
                item.CustomData = new Dictionary<string, object>(context.CustomData);

            return item;
        }

        private int CalculateLevel(EquipmentData baseEquipment, ItemGenerationContext context)
        {
            int baseLevel = Math.Max(1, context.PlayerLevel + context.CraftingMastery / 5);
            int tierLevel = context.Tier * 2;
            int waveLevel = context.Wave / 5;

            int level = baseLevel + tierLevel + waveLevel;
            return Math.Clamp(level, 1, baseEquipment.MaxLevel);
        }

        private void ApplySecondaryStats(InventoryItem item, CombatStatEntry[] stats)
        {
            // Store in CustomData for now - actual stat application happens at runtime
            item.CustomData ??= new Dictionary<string, object>();
            item.CustomData["SecondaryStats"] = stats;
        }

        private void ApplyAffixes(InventoryItem item, AffixInstanceData[] affixes)
        {
            item.CustomData ??= new Dictionary<string, object>();
            foreach (var affix in affixes)
                if (affix != null) affix.ItemInstanceId = item.InstanceId;
            item.CustomData["Affixes"] = affixes;
        }

        private void ApplyEventModifiers(InventoryItem item, EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context)
        {
            if (context.EventModifiers == null) return;

            foreach (var modifier in context.EventModifiers)
            {
                if (modifier is IEquipmentModifier equipMod)
                {
                    equipMod.ModifyEquipment(item, baseEquipment, rarity, level, context);
                }
            }
        }
    }

    public interface IEquipmentModifier
    {
        void ModifyEquipment(InventoryItem item, EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context);
    }
}