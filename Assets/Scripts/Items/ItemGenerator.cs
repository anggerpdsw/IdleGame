using System;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Items.Generation;
using Gen = IdleDefenseSurvival.Items.Generation;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Source of item generation.
    /// </summary>
    public enum ItemSource
    {
        Craft = 0,
        Drop = 1,
        Reward = 2,
        Purchase = 3,
        Quest = 4,
        Event = 5
    }

    /// <summary>
    /// Item Generator Facade - Maintains backward compatibility with old API.
    /// Internally delegates to specialized generator components.
    /// </summary>
    public sealed class ItemGenerator
    {
        private static ItemGenerator _instance;
        public static ItemGenerator Instance
        {
            get
            {
                if (_instance == null) _instance = new ItemGenerator();
                return _instance;
            }
        }

        // Generator components
        private readonly IRandomProvider _rng;
        private readonly RarityRollService _rarityRoll;
        private readonly StatRollService _statRoll;
        private readonly SocketGenerator _socketGen;
        private readonly EnchantmentGenerator _enchantGen;
        private readonly AffixGenerator _affixGen;
        private readonly EquipmentGenerator _equipmentGen;
        private readonly GemGenerator _gemGen;
        private readonly ConsumableGenerator _consumableGen;
        private readonly Gen.LootGenerator _lootGen;
        private readonly ItemValidator _validator;

        private ItemGenerator()
        {
            // Use Unity RNG by default for backward compatibility
            _rng = new UnityRandomProvider();

            // Initialize services
            _rarityRoll = new RarityRollService(_rng);
            _statRoll = new StatRollService(_rng);
            _socketGen = new SocketGenerator(_rng);
            _enchantGen = new EnchantmentGenerator(_rng);
            _affixGen = new AffixGenerator(_rng);
            _validator = new ItemValidator();

            // Initialize generators with shared services
            _equipmentGen = new EquipmentGenerator(_rng, _rarityRoll, _statRoll, _socketGen, _enchantGen, _affixGen, _validator);
            _gemGen = new GemGenerator(_rng, _rarityRoll, _validator);
            _consumableGen = new ConsumableGenerator(_rng, _rarityRoll, _validator);
            _lootGen = new Gen.LootGenerator(_rng, _equipmentGen, _gemGen, _consumableGen, Gen.LootGeneratorConfig.Default);
        }

        // ============ Public API (Backward Compatible) ============

        /// <summary>
        /// Generates an item based on context (craft, drop, reward, etc.)
        /// </summary>
        public InventoryItem GenerateItem(string itemId, ItemGenerationContext context)
        {
            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            if (itemData == null) return null;

            return itemData.Category switch
            {
                ItemCategory.Equipment => _equipmentGen.Generate(itemData as EquipmentData, context),
                ItemCategory.Gem => _gemGen.Generate(ItemDatabase.Instance.GetGem(itemId), context),
                ItemCategory.Consumable or ItemCategory.Material or ItemCategory.Chest or ItemCategory.SkillBook or ItemCategory.UpgradeStone => _consumableGen.Generate(itemData, context),
                _ => GenerateGenericItem(itemData, context)
            };
        }

        /// <summary>
        /// Generates a random equipment item.
        /// </summary>
        public InventoryItem GenerateEquipment(EquipmentType type, ItemRarity rarity, int level, int playerTier = 1)
        {
            var baseEquipments = ItemDatabase.Instance?.GetEquipmentByType(type)?.ToList();
            if (baseEquipments == null || baseEquipments.Count == 0) return null;

            var baseEquipment = _rng.Choice(baseEquipments);
            var context = ItemGenerationContext.Equipment(type, rarity, level, playerTier);
            return _equipmentGen.Generate(baseEquipment, context);
        }

        /// <summary>
        /// Generates equipment from a specific base template.
        /// </summary>
        public InventoryItem GenerateEquipmentFromBase(EquipmentData baseEquipment, ItemRarity rarity, int level, int playerTier = 1)
        {
            if (baseEquipment == null) return null;

            var context = ItemGenerationContext.Equipment(baseEquipment.EquipmentType, rarity, level, playerTier);
            context = context.With(forcedQuality: (int)rarity, fixedLevel: level);
            return _equipmentGen.Generate(baseEquipment, context);
        }

        /// <summary>
        /// Generates equipment from base with context.
        /// </summary>
        public InventoryItem GenerateEquipmentFromBase(EquipmentData baseEquipment, ItemGenerationContext context)
        {
            return _equipmentGen.Generate(baseEquipment, context);
        }

        /// <summary>
        /// Generates a random gem.
        /// </summary>
        public InventoryItem GenerateGem(GemType type, ItemRarity rarity, int level)
        {
            var baseGems = ItemDatabase.Instance?.GetGemsByType(type)?.ToList();
            if (baseGems == null || baseGems.Count == 0) return null;

            var baseGem = _rng.Choice(baseGems);
            var context = ItemGenerationContext.Gem(type, rarity, level);
            return _gemGen.Generate(baseGem, context);
        }

        public InventoryItem GenerateGemFromBase(GemData baseGem, ItemRarity rarity, int level)
        {
            if (baseGem == null) return null;

            var context = ItemGenerationContext.Gem(baseGem.GemType, rarity, level);
            context = context.With(forcedQuality: (int)rarity, fixedLevel: level);
            return _gemGen.Generate(baseGem, context);
        }

        public InventoryItem GenerateGemFromBase(GemData baseGem, ItemGenerationContext context)
        {
            return _gemGen.Generate(baseGem, context);
        }

        /// <summary>
        /// Generates a random consumable item.
        /// </summary>
        public InventoryItem GenerateConsumable(ItemCategory category, ItemRarity rarity, int quantity = 1)
        {
            var items = ItemDatabase.Instance?.GetItemsByCategory(category)?
                .Where(i => i.ItemRarity == rarity)
                .ToList() ?? new System.Collections.Generic.List<ItemData>();

            if (items.Count == 0) return null;

            var baseItem = _rng.Choice(items);
            var context = ItemGenerationContext.Drop(1, 1, 0, 0).With(category: category, forcedQuality: (int)rarity);
            return _consumableGen.Generate(baseItem, context);
        }

        public InventoryItem GenerateConsumableFromBase(ItemData baseItem, ItemGenerationContext context)
        {
            return _consumableGen.Generate(baseItem, context);
        }

        public InventoryItem GenerateConsumableFromBase(ItemData baseItem, int quantity = 1)
        {
            var context = ItemGenerationContext.Drop(1, 1).With(category: baseItem.Category);
            var item = _consumableGen.Generate(baseItem, context);
            if (item != null) item.Quantity = Math.Clamp(quantity, 1, baseItem.StackSize);
            return item;
        }

        /// <summary>
        /// Generates equipment appropriate for a tier/wave.
        /// </summary>
        public InventoryItem GenerateTierAppropriateEquipment(EquipmentType type, int tier, int wave)
        {
            return _equipmentGen.GenerateRandom(type, tier, wave);
        }

        /// <summary>
        /// Generates multiple items for a loot drop.
        /// </summary>
        public InventoryItem[] GenerateLootDrop(int tier, int wave, int itemCount, float rarityBoost = 0f)
        {
            return _lootGen.GenerateLoot(tier, wave, itemCount, rarityBoost, 0, null);
        }

        private InventoryItem GenerateGenericItem(ItemData itemData, ItemGenerationContext context)
        {
            return new InventoryItem
            {
                ItemId = itemData.Id,
                Quantity = itemData.IsStackable ? 1 : 1,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        // ============ Advanced API (New) ============

        /// <summary>
        /// Gets the equipment generator for direct access.
        /// </summary>
        public EquipmentGenerator Equipment => _equipmentGen;

        /// <summary>
        /// Gets the gem generator for direct access.
        /// </summary>
        public GemGenerator Gem => _gemGen;

        /// <summary>
        /// Gets the consumable generator for direct access.
        /// </summary>
        public ConsumableGenerator Consumable => _consumableGen;

        /// <summary>
        /// Gets the loot generator for direct access.
        /// </summary>
        public Gen.LootGenerator Loot => _lootGen;

        /// <summary>
        /// Creates a deterministic generator with a specific seed.
        /// </summary>
        public static ItemGenerator CreateDeterministic(int seed)
        {
            var rng = new SeedRandomProvider(seed);
            var instance = new ItemGenerator(rng);
            return instance;
        }

        /// <summary>
        /// Creates a test generator with predefined values.
        /// </summary>
        public static ItemGenerator CreateForTesting(params float[] randomValues)
        {
            var rng = new TestRandomProvider(randomValues);
            var instance = new ItemGenerator(rng);
            return instance;
        }

        // Internal constructor for custom RNG
        private ItemGenerator(IRandomProvider rng)
        {
            _rng = rng ?? new UnityRandomProvider();
            _rarityRoll = new RarityRollService(_rng);
            _statRoll = new StatRollService(_rng);
            _socketGen = new SocketGenerator(_rng);
            _enchantGen = new EnchantmentGenerator(_rng);
            _affixGen = new AffixGenerator(_rng);
            _validator = new ItemValidator();

            _equipmentGen = new EquipmentGenerator(_rng, _rarityRoll, _statRoll, _socketGen, _enchantGen, _affixGen, _validator);
            _gemGen = new GemGenerator(_rng, _rarityRoll, _validator);
            _consumableGen = new ConsumableGenerator(_rng, _rarityRoll, _validator);
            _lootGen = new Gen.LootGenerator(_rng, _equipmentGen, _gemGen, _consumableGen, Gen.LootGeneratorConfig.Default);
        }
    }
}