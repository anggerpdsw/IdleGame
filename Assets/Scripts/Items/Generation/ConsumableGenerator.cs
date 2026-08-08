using System;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for consumable items (materials, potions, scrolls, etc.).
    /// </summary>
    public sealed class ConsumableGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly RarityRollService _rarityRoll;
        private readonly ItemValidator _validator;

        public ConsumableGenerator(
            IRandomProvider rng,
            RarityRollService rarityRoll = null,
            ItemValidator validator = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _rarityRoll = rarityRoll ?? new RarityRollService(_rng);
            _validator = validator ?? new ItemValidator();
        }

        /// <summary>
        /// Generates a consumable from a specific base template with context.
        /// </summary>
        public InventoryItem Generate(ItemData baseItem, ItemGenerationContext context)
        {
            if (baseItem == null) return null;

            // 1. Determine rarity (for consumables, usually fixed or Common)
            ItemRarity rarity = context.ForcedQuality.HasValue
                ? (ItemRarity)Math.Clamp(context.ForcedQuality.Value, 1, 8)
                : (baseItem.ItemRarity != ItemRarity.None ? baseItem.ItemRarity : _rarityRoll.RollRarity(context));

            // 2. Determine quantity
            int quantity = CalculateQuantity(baseItem, context);

            // 3. Create base item
            var item = CreateBaseItem(baseItem, quantity);

            // 4. Apply event modifiers
            ApplyEventModifiers(item, baseItem, context);

            // 5. Validate
            var validation = _validator.Validate(item, baseItem);
            if (!validation.IsValid)
            {
                UnityEngine.Debug.LogWarning($"[ConsumableGenerator] Validation failed for {baseItem.Id}: {validation}");
            }

            return item;
        }

        /// <summary>
        /// Generates a random consumable of a specific category.
        /// </summary>
        public InventoryItem GenerateRandom(ItemCategory category, int tier, int wave, long luck = 0, float rarityBoost = 0f, int? seed = null)
        {
            var items = ItemDatabase.Instance?.GetItemsByCategory(category)?.ToList();
            if (items == null || items.Count == 0) return null;

            var baseItem = _rng.Choice(items);
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, luck, seed)
                .With(category: category);

            return Generate(baseItem, context);
        }

        private InventoryItem CreateBaseItem(ItemData baseItem, int quantity)
        {
            return new InventoryItem
            {
                ItemId = baseItem.Id,
                Quantity = Math.Clamp(quantity, 1, baseItem.StackSize),
                Level = 1,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private int CalculateQuantity(ItemData baseItem, ItemGenerationContext context)
        {
            int baseQty = 1;

            // Stackable items can have more quantity
            if (baseItem.IsStackable)
            {
                baseQty = _rng.Range(1, Math.Max(2, baseItem.StackSize / 10));
            }

            // Tier/wave bonus for materials
            if (baseItem.Category == ItemCategory.Material)
            {
                baseQty += context.Tier / 5;
                baseQty += context.Wave / 50;
            }

            return Math.Clamp(baseQty, 1, baseItem.StackSize);
        }

        private void ApplyEventModifiers(InventoryItem item, ItemData baseItem, ItemGenerationContext context)
        {
            if (context.EventModifiers == null) return;

            foreach (var modifier in context.EventModifiers)
            {
                if (modifier is IConsumableModifier consumableMod)
                {
                    consumableMod.ModifyConsumable(item, baseItem, context);
                }
            }
        }
    }

    public interface IConsumableModifier
    {
        void ModifyConsumable(InventoryItem item, ItemData baseItem, ItemGenerationContext context);
    }
}