using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Data;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for equipment items.
    /// Pipeline: Validate input → Resolve rarity → Load equip_base → Resolve rarity config →
    /// Generate Level → Generate durability config → Create InventoryItem →
    /// Generate MainAttribute → Generate SecondaryAttribute → Generate Sockets →
    /// Generate Affixes → Generate Enchantment → Apply Event Modifiers →
    /// Calculate final derived values → Validate → Return
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
            _attributeRoll = attributeRoll ?? new AttributeRollService(_rng);
        }

        /// <summary>
        /// Generates equipment from crafting recipe context.
        /// Uses equip_base as the single template source, with rarity from recipe.
        /// </summary>
        public InventoryItem Generate(EquipmentData baseEquipment, ItemGenerationContext context)
        {
            if (baseEquipment == null) return null;

            // 1. Resolve rarity
            // For crafting: rarity MUST come from recipe via context.ForcedQuality
            // For drops: rarity can be rolled if not forced
            Rarity rarity = context.ForcedQuality.HasValue
                ? (Rarity)Math.Clamp(context.ForcedQuality.Value, 1, 6)
                : _rarityRoll.RollRarity(context.With(category: ItemCategory.Equipment));

            // Validate rarity is in valid range for crafting
            if (context.Source == ItemSource.Craft && !context.ForcedQuality.HasValue)
            {
                UnityEngine.Debug.LogWarning($"[EquipmentGenerator] Craft source without forced rarity. Recipe should provide rarity via ForcedQuality.");
            }

            // 2. Load equip_base (single source of truth for all equipment)
            var baseConfig = EquipmentBaseDataRepository.Instance;
            if (baseConfig == null)
            {
                UnityEngine.Debug.LogError("[EquipmentGenerator] EquipmentBaseData not loaded.");
                return null;
            }

            // 3. Get rarity-specific configuration
            var rarityConfig = baseConfig.GetRarityConfig(rarity);
            if (!rarityConfig.IsValid)
            {
                UnityEngine.Debug.LogError($"[EquipmentGenerator] Invalid rarity config for {rarity}.");
                return null;
            }

            // 4. Determine level
            // For crafting: use BaseLevel from equip_base, then random within rarity range
            // For drops: can use FixedLevel from context or calculate from player/tier/wave
            int level = context.FixedLevel ?? GenerateLevel(rarityConfig, baseConfig.BaseLevel, context);

            // 5. Clamp level to rarity max
            level = Math.Clamp(level, 1, rarityConfig.MaxLevel);

            // 6. Create base item with all rarity-based configuration
            var item = CreateBaseItem(baseEquipment, rarity, level, rarityConfig, context);

            // 7. Generate Main Attributes (for crafting, rarity comes from recipe)
            if (context.Source == ItemSource.Craft)
            {
                GenerateMainAttributes(item, rarity, context);
            }

            // 8. Generate Secondary Stats (specialization stats like Crit, LifeSteal, etc.)
            var secondaryStats = _statRoll.RollSecondaryStats(baseEquipment, rarity, context);
            if (secondaryStats.Length > 0)
                ApplySecondaryStats(item, secondaryStats);

            // 9. Generate sockets using MaxSockets from rarity config
            item.Sockets = _socketGen.GenerateSockets(rarityConfig.MaxSockets, rarity, context);

            // 10. Generate affixes
            var affixes = _affixGen.GenerateAffixes(baseEquipment, rarity, context);
            if (affixes.Length > 0)
                ApplyAffixes(item, affixes);

            // 11. Generate enchantment
            item.Enchantment = _enchantGen.GenerateEnchantment(baseEquipment, rarity, level, context);

            // 12. Apply event modifiers
            ApplyEventModifiers(item, baseEquipment, rarity, level, context);

            // 13. Calculate final derived values (sell price, etc.)
            CalculateDerivedValues(item, baseConfig, rarityConfig, rarity, level);

            // 14. Validate
            var validation = _validator.Validate(item, baseEquipment);
            if (!validation.IsValid)
            {
                UnityEngine.Debug.LogWarning($"[EquipmentGenerator] Validation failed for {baseEquipment.Id}: {validation}");
            }

            return item;
        }

        /// <summary>
        /// Generates random equipment of a specific type (for drops, rewards).
        /// Uses equipment type to find appropriate base equipment template, but all
        /// rarity-based stats come from equip_base.
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

        private int GenerateLevel(EquipmentRarityConfig rarityConfig, int baseLevel, ItemGenerationContext context)
        {
            // For crafting: level is randomized within the rarity's level range
            // Common: 1-10, Rare: 10-15, Epic: 15-20, Legendary: 20-25, Mythic: 25-30, Divine: 30-50
            var (minLevel, maxLevel) = EquipmentBaseDataRepository.Instance.GetLevelRange(
                (Rarity)Math.Clamp(context.ForcedQuality ?? 1, 1, 6));

            if (context.Source == ItemSource.Craft)
            {
                // Crafting: random within rarity range, using deterministic RNG
                return _rng.Range(minLevel, maxLevel + 1); // inclusive max
            }

            // Drops/rewards: can use old calculation based on player/tier/wave
            int calculatedLevel = Math.Max(1, context.PlayerLevel + context.CraftingMastery / 5);
            calculatedLevel += context.Tier * 2;
            calculatedLevel += context.Wave / 5;

            return Math.Clamp(calculatedLevel, minLevel, maxLevel);
        }

        private InventoryItem CreateBaseItem(EquipmentData baseEquipment, Rarity rarity, int level, EquipmentRarityConfig rarityConfig, ItemGenerationContext context)
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
                ItemId = outputItemId,
                EquipmentTemplateId = "equip_base", // Always equip_base as the template source
                Quantity = 1,
                Level = level,
                MaxDurability = rarityConfig.MaxDurability,
                CurrentDurability = rarityConfig.MaxDurability,
                DurabilityLossPerUse = rarityConfig.DurabilityLossPerUse,
                RepairCostPerDurability = rarityConfig.RepairCostPerDurability,
                MaxSockets = rarityConfig.MaxSockets,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                EnhanceLevel = 0
            };

            return item;
        }

        private void GenerateMainAttributes(InventoryItem item, Rarity rarity, ItemGenerationContext context)
        {
            var tierConfig = CraftingConfig.Load()
                .GetAttributeTierConfig((int)rarity);

            var attributes = _attributeRoll
                .RollAttributes(rarity, tierConfig);

            if (attributes.Length > 0)
            {
                var mainAttrs = new List<EquipmentAttributeEntry>();
                var secondAttrs = new List<EquipmentAttributeEntry>();

                foreach (var attr in attributes)
                {
                    mainAttrs.Add(new EquipmentAttributeEntry(attr.Attribute, attr.BaseValue));
                }

                item.AttributeData = new EquipmentAttributeData(mainAttrs.ToArray(), secondAttrs.ToArray());
            }
        }

        private void ApplySecondaryStats(InventoryItem item, CombatStatEntry[] stats)
        {
            if (stats == null || stats.Length == 0) return;
            var secondAttrs = new List<EquipmentAttributeEntry>();
            foreach (var stat in stats)
            {
                var attrEntry = new EquipmentAttributeEntry((MainAttribute)(int)stat.Stat, stat.GetValue(item.Level, item.EnhanceLevel));
                secondAttrs.Add(attrEntry);
            }
            if (item.AttributeData == null)
                item.AttributeData = new EquipmentAttributeData(Array.Empty<EquipmentAttributeEntry>(), secondAttrs.ToArray());
            else
                item.AttributeData = new EquipmentAttributeData(item.AttributeData.MainAttribute, secondAttrs.ToArray());
        }

        private void ApplyAffixes(InventoryItem item, AffixInstanceData[] affixes)
        {
            if (affixes == null || affixes.Length == 0) return;
            var mainAttrs = new List<EquipmentAttributeEntry>();
            var secondAttrs = new List<EquipmentAttributeEntry>();

            foreach (var affix in affixes)
            {
                if (affix == null) continue;
                affix.ItemInstanceId = item.InstanceId;
                if (affix.AttributeValues != null)
                    foreach (var (attr, value) in affix.AttributeValues)
                        if (value != 0f) mainAttrs.Add(new EquipmentAttributeEntry(attr, value));
                if (affix.StatValues != null)
                    foreach (var (stat, value) in affix.StatValues)
                        if (stat != SecondaryStat.None && value != 0f)
                            secondAttrs.Add(new EquipmentAttributeEntry((MainAttribute)(int)stat, value));
            }

            if (mainAttrs.Count > 0 || secondAttrs.Count > 0)
            {
                var existingMain = item.AttributeData?.MainAttribute ?? Array.Empty<EquipmentAttributeEntry>();
                var existingSecond = item.AttributeData?.SecondAttribute ?? Array.Empty<EquipmentAttributeEntry>();
                item.AttributeData = new EquipmentAttributeData(
                    existingMain.Concat(mainAttrs).ToArray(),
                    existingSecond.Concat(secondAttrs).ToArray()
                );
            }
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

        private void CalculateDerivedValues(InventoryItem item, EquipmentBaseData baseConfig, EquipmentRarityConfig rarityConfig, Rarity rarity, int level)
        {
            // Sell price: base + rarity modifier + level modifier
            // Using existing economy formula if available, otherwise simple calculation
            long baseSellPrice = baseConfig.SellPrice;
            float rarityMultiplier = GetRaritySellMultiplier(rarity);
            float levelMultiplier = 1f + (level - 1) * 0.1f;

            // Store in CustomData for runtime access (not a persisted field on InventoryItem)
            if (item.CustomData == null) item.CustomData = new Dictionary<string, object>();
            item.CustomData["BaseSellPrice"] = baseSellPrice;
            item.CustomData["RaritySellMultiplier"] = rarityMultiplier;
            item.CustomData["LevelSellMultiplier"] = levelMultiplier;
            item.CustomData["FinalSellPrice"] = (long)(baseSellPrice * rarityMultiplier * levelMultiplier);
        }

        private float GetRaritySellMultiplier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => 1.0f,
                Rarity.Rare => 2.0f,
                Rarity.Epic => 4.0f,
                Rarity.Legendary => 8.0f,
                Rarity.Mythic => 16.0f,
                Rarity.Divine => 32.0f,
                _ => 1.0f
            };
        }
    }

    public interface IEquipmentModifier
    {
        void ModifyEquipment(InventoryItem item, EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context);
    }
}