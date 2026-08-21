using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Data;
using IdleDefenseSurvival.Items.Generation;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Tests
{
    /// <summary>
    /// Deterministic equipment generation tests.
    /// Validates that crafting recipes produce consistent, spec-compliant equipment.
    /// </summary>
    [TestFixture]
    public class EquipmentGenerationTests
    {
        private ItemGenerator _generator;
        private SeedRandomProvider _rng;

        [SetUp]
        public void SetUp()
        {
            // Ensure all static repositories are initialized
            EquipmentBaseDataRepository.Reload();
            CraftingConfig.Load();
            ItemDatabase.Instance.Initialize(); // if needed

            _rng = new SeedRandomProvider(12345);
            _generator = ItemGenerator.CreateDeterministic(12345);
        }

        [TearDown]
        public void TearDown()
        {
            _generator = null;
            _rng = null;
        }

        // ============================================================
        // Test 1 — Common (Rarity 1)
        // ============================================================
        [Test]
        public void TestCommon_EquipmentFromRecipe_UsesCorrectRarityConfig()
        {
            // Arrange
            var recipe = CreateTestRecipe(EquipmentType.Hat, 1); // Common
            var context = CreateCraftContext(recipe, seed: 42);

            // Act
            var item = _generator.GenerateEquipmentFromBase(
                CreateBaseEquipment(EquipmentType.Hat),
                context);

            // Assert
            Assert.NotNull(item);
            Assert.AreEqual(1, item.Quantity, "Equipment must not be stackable");
            Assert.AreEqual("equip_base", item.EquipmentTemplateId, "Template must be equip_base");
            Assert.AreEqual(EquipmentType.Hat, item.GetEquipmentType(), "EquipmentType from recipe");

            // Level range: Common = 1-10
            Assert.GreaterOrEqual(item.Level, 1);
            Assert.LessOrEqual(item.Level, 10);

            // Durability from rarity config
            Assert.AreEqual(100, item.MaxDurability, "Common MaxDurability = 100");
            Assert.AreEqual(100, item.CurrentDurability, "Starts at max");
            Assert.AreEqual(1, item.DurabilityLossPerUse, "Common DurabilityLoss = 1");
            Assert.AreEqual(5, item.RepairCostPerDurability, "Common RepairCost = 5");

            // Sockets from rarity config
            Assert.AreEqual(1, item.MaxSockets, "Common MaxSockets = 1");
            Assert.IsNotNull(item.Sockets);
            Assert.LessOrEqual(item.Sockets.Length, 1, "Socket count <= MaxSockets");

            // MainAttribute generated
            Assert.NotNull(item.AttributeData, "AttributeData must exist");
            Assert.GreaterOrEqual(item.AttributeData.MainAttribute?.Length ?? 0, 1, "At least 1 MainAttribute");

            Debug.Log($"[TestCommon] Level={item.Level}, MaxDur={item.MaxDurability}, Sockets={item.Sockets?.Length ?? 0}");
        }

        // ============================================================
        // Test 2 — Divine (Rarity 6)
        // ============================================================
        [Test]
        public void TestDivine_EquipmentFromRecipe_UsesCorrectRarityConfig()
        {
            // Arrange
            var recipe = CreateTestRecipe(EquipmentType.Hat, 6); // Divine
            var context = CreateCraftContext(recipe, seed: 42);

            // Act
            var item = _generator.GenerateEquipmentFromBase(
                CreateBaseEquipment(EquipmentType.Hat),
                context);

            // Assert
            Assert.NotNull(item);

            // Level range: Divine = 30-50
            Assert.GreaterOrEqual(item.Level, 30);
            Assert.LessOrEqual(item.Level, 50);

            // Durability from rarity config
            Assert.AreEqual(1000, item.MaxDurability, "Divine MaxDurability = 1000");
            Assert.AreEqual(1000, item.CurrentDurability);
            Assert.AreEqual(13, item.DurabilityLossPerUse, "Divine DurabilityLoss = 13");
            Assert.AreEqual(10000, item.RepairCostPerDurability, "Divine RepairCost = 10000");

            // Sockets from rarity config
            Assert.AreEqual(6, item.MaxSockets, "Divine MaxSockets = 6");
            Assert.IsNotNull(item.Sockets);
            Assert.LessOrEqual(item.Sockets.Length, 6);

            // MainAttribute generated
            Assert.NotNull(item.AttributeData);
            Assert.GreaterOrEqual(item.AttributeData.MainAttribute?.Length ?? 0, 1);

            Debug.Log($"[TestDivine] Level={item.Level}, MaxDur={item.MaxDurability}, Sockets={item.Sockets?.Length ?? 0}");
        }

        // ============================================================
        // Test 3 — EquipmentType Independence
        // ============================================================
        [Test]
        public void TestEquipmentTypeIndependence_AllTypesUseEquipBase()
        {
            var equipmentTypes = new[]
            {
                EquipmentType.Hat,
                EquipmentType.Armor,
                EquipmentType.Gloves,
                EquipmentType.Cape,
                EquipmentType.Belt,
                EquipmentType.Pants,
                EquipmentType.Pendant,
                EquipmentType.Earring,
                EquipmentType.Bracelet,
                EquipmentType.Ring,
                EquipmentType.Shoes
            };

            foreach (var eqType in equipmentTypes)
            {
                var recipe = CreateTestRecipe(eqType, 3); // Epic
                var context = CreateCraftContext(recipe, seed: 999);

                var item = _generator.GenerateEquipmentFromBase(
                    CreateBaseEquipment(eqType),
                    context);

                Assert.NotNull(item, $"Failed for {eqType}");
                Assert.AreEqual("equip_base", item.EquipmentTemplateId, $"Template must be equip_base for {eqType}");
                Assert.AreEqual(eqType, item.GetEquipmentType(), $"EquipmentType preserved for {eqType}");
                Assert.AreEqual(1, item.Quantity, $"Quantity=1 for {eqType}");

                // All should use Epic rarity config
                Assert.AreEqual(300, item.MaxDurability, $"Epic MaxDurability for {eqType}");
                Assert.AreEqual(3, item.MaxSockets, $"Epic MaxSockets for {eqType}");
            }
        }

        // ============================================================
        // Test 4 — Determinism (Same Seed = Same Output)
        // ============================================================
        [Test]
        public void TestDeterminism_SameSeedProducesIdenticalEquipment()
        {
            const int seed = 55555;
            var recipe = CreateTestRecipe(EquipmentType.Armor, 4); // Legendary
            var context1 = CreateCraftContext(recipe, seed: seed);
            var context2 = CreateCraftContext(recipe, seed: seed);

            var gen1 = ItemGenerator.CreateDeterministic(seed);
            var gen2 = ItemGenerator.CreateDeterministic(seed);

            var item1 = gen1.GenerateEquipmentFromBase(CreateBaseEquipment(EquipmentType.Armor), context1);
            var item2 = gen2.GenerateEquipmentFromBase(CreateBaseEquipment(EquipmentType.Armor), context2);

            Assert.NotNull(item1);
            Assert.NotNull(item2);

            // All generated fields must match
            Assert.AreEqual(item1.Level, item2.Level, "Level must match");
            Assert.AreEqual(item1.MaxDurability, item2.MaxDurability, "MaxDurability must match");
            Assert.AreEqual(item1.CurrentDurability, item2.CurrentDurability, "CurrentDurability must match");
            Assert.AreEqual(item1.DurabilityLossPerUse, item2.DurabilityLossPerUse, "DurabilityLoss must match");
            Assert.AreEqual(item1.RepairCostPerDurability, item2.RepairCostPerDurability, "RepairCost must match");
            Assert.AreEqual(item1.MaxSockets, item2.MaxSockets, "MaxSockets must match");
            Assert.AreEqual(item1.Sockets?.Length ?? 0, item2.Sockets?.Length ?? 0, "Socket count must match");

            // MainAttribute must match
            var main1 = item1.AttributeData?.MainAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            var main2 = item2.AttributeData?.MainAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            Assert.AreEqual(main1.Length, main2.Length, "MainAttribute count must match");
            for (int i = 0; i < main1.Length; i++)
            {
                Assert.AreEqual(main1[i].Attribute, main2[i].Attribute, $"MainAttribute[{i}] type must match");
                Assert.AreEqual(main1[i].BaseValue, main2[i].BaseValue, $"MainAttribute[{i}] value must match");
            }

            // SecondaryAttribute must match
            var sec1 = item1.AttributeData?.SecondAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            var sec2 = item2.AttributeData?.SecondAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            Assert.AreEqual(sec1.Length, sec2.Length, "SecondaryAttribute count must match");
            for (int i = 0; i < sec1.Length; i++)
            {
                Assert.AreEqual(sec1[i].Attribute, sec2[i].Attribute, $"SecondaryAttribute[{i}] type must match");
                Assert.AreEqual(sec1[i].BaseValue, sec2[i].BaseValue, $"SecondaryAttribute[{i}] value must match");
            }

            // Sockets unlock state must match
            if (item1.Sockets != null && item2.Sockets != null)
            {
                for (int i = 0; i < item1.Sockets.Length; i++)
                {
                    Assert.AreEqual(item1.Sockets[i].IsUnlocked, item2.Sockets[i].IsUnlocked, $"Socket[{i}] unlocked must match");
                }
            }

            // Enchantment must match
            if (item1.Enchantment != null && item2.Enchantment != null)
            {
                Assert.AreEqual(item1.Enchantment.EnchantmentId, item2.Enchantment.EnchantmentId);
                Assert.AreEqual(item1.Enchantment.Level, item2.Enchantment.Level);
            }

            Debug.Log($"[TestDeterminism] All fields identical for seed {seed}");
        }

        // ============================================================
        // Test 5 — Save/Load Roundtrip
        // ============================================================
        [Test]
        public void TestSaveLoad_RoundtripPreservesAllGeneratedFields()
        {
            var recipe = CreateTestRecipe(EquipmentType.Armor, 5); // Mythic
            var context = CreateCraftContext(recipe, seed: 777);

            var item = _generator.GenerateEquipmentFromBase(
                CreateBaseEquipment(EquipmentType.Armor),
                context);

            Assert.NotNull(item);

            // Serialize to JSON (simulating SaveData)
            var json = JsonUtility.ToJson(item);
            Debug.Log($"[TestSaveLoad] Serialized: {json}");

            // Deserialize (simulating Load)
            var loaded = JsonUtility.FromJson<InventoryItem>(json);

            // Verify all critical fields preserved
            Assert.AreEqual(item.InstanceId, loaded.InstanceId, "InstanceId preserved");
            Assert.AreEqual(item.ItemId, loaded.ItemId, "ItemId preserved");
            Assert.AreEqual(item.EquipmentTemplateId, loaded.EquipmentTemplateId, "EquipmentTemplateId preserved");
            Assert.AreEqual(item.Quantity, loaded.Quantity, "Quantity preserved");
            Assert.AreEqual(item.Level, loaded.Level, "Level preserved");
            Assert.AreEqual(item.MaxDurability, loaded.MaxDurability, "MaxDurability preserved");
            Assert.AreEqual(item.CurrentDurability, loaded.CurrentDurability, "CurrentDurability preserved");
            Assert.AreEqual(item.DurabilityLossPerUse, loaded.DurabilityLossPerUse, "DurabilityLossPerUse preserved");
            Assert.AreEqual(item.RepairCostPerDurability, loaded.RepairCostPerDurability, "RepairCostPerDurability preserved");
            Assert.AreEqual(item.MaxSockets, loaded.MaxSockets, "MaxSockets preserved");
            Assert.AreEqual(item.EnhanceLevel, loaded.EnhanceLevel, "EnhanceLevel preserved");
            Assert.AreEqual(item.AcquiredTimestamp, loaded.AcquiredTimestamp, "AcquiredTimestamp preserved");
            Assert.AreEqual(item.IsFavorite, loaded.IsFavorite, "IsFavorite preserved");
            Assert.AreEqual(item.IsLocked, loaded.IsLocked, "IsLocked preserved");
            Assert.AreEqual(item.IsNew, loaded.IsNew, "IsNew preserved");

            // Sockets
            Assert.NotNull(loaded.Sockets, "Sockets must deserialize");
            Assert.AreEqual(item.Sockets?.Length ?? 0, loaded.Sockets?.Length ?? 0, "Socket count preserved");
            if (item.Sockets != null)
            {
                for (int i = 0; i < item.Sockets.Length; i++)
                {
                    Assert.AreEqual(item.Sockets[i].IsUnlocked, loaded.Sockets[i].IsUnlocked, $"Socket[{i}] IsUnlocked preserved");
                    Assert.AreEqual(item.Sockets[i].GemInstanceId, loaded.Sockets[i].GemInstanceId, $"Socket[{i}] GemInstanceId preserved");
                }
            }

            // AttributeData
            Assert.NotNull(loaded.AttributeData, "AttributeData must deserialize");
            var mainOrig = item.AttributeData?.MainAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            var mainLoad = loaded.AttributeData?.MainAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            Assert.AreEqual(mainOrig.Length, mainLoad.Length, "MainAttribute count preserved");
            for (int i = 0; i < mainOrig.Length; i++)
            {
                Assert.AreEqual(mainOrig[i].Attribute, mainLoad[i].Attribute, $"MainAttribute[{i}] type preserved");
                Assert.AreEqual(mainOrig[i].BaseValue, mainLoad[i].BaseValue, $"MainAttribute[{i}] value preserved");
            }

            var secOrig = item.AttributeData?.SecondAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            var secLoad = loaded.AttributeData?.SecondAttribute ?? Array.Empty<EquipmentAttributeEntry>();
            Assert.AreEqual(secOrig.Length, secLoad.Length, "SecondaryAttribute count preserved");

            // Enchantment
            if (item.Enchantment != null)
            {
                Assert.NotNull(loaded.Enchantment, "Enchantment must deserialize");
                Assert.AreEqual(item.Enchantment.EnchantmentId, loaded.Enchantment.EnchantmentId);
                Assert.AreEqual(item.Enchantment.Level, loaded.Enchantment.Level);
                Assert.AreEqual(item.Enchantment.Experience, loaded.Enchantment.Experience);
            }

            Debug.Log($"[TestSaveLoad] Roundtrip successful - all fields preserved");
        }

        // ============================================================
        // Helpers
        // ============================================================
        private CraftRecipeData CreateTestRecipe(EquipmentType type, int rarity)
        {
            return new CraftRecipeData
            {
                RecipeId = $"craft_{type.ToString().ToLower()}_r{rarity}",
                DisplayName = $"{type} R{rarity}",
                Category = ItemCategory.Equipment,
                EquipmentType = type,
                Rarity = rarity,
                RequiredCraftingLevel = 1,
                Ingredients = Array.Empty<CraftIngredient>(),
                BaseCraftTime = 0f
            };
        }

        private ItemGenerationContext CreateCraftContext(CraftRecipeData recipe, long seed = 0)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                RecipeId = recipe.RecipeId,
                PlayerLevel = 1,
                CraftingMastery = 0,
                BlacksmithLevel = 0,
                Luck = 0,
                ForcedQuality = recipe.Rarity, // Recipe.Rarity is source of truth
                FixedLevel = null, // Let generator randomize within rarity range
                FixedEnhance = 0,
                Seed = (int)seed,
                Tier = 1,
                Wave = 1,
                EquipmentType = recipe.EquipmentType,
                Category = ItemCategory.Equipment,
                EventModifiers = Array.Empty<EventCraftModifier>()
            };
        }

        private EquipmentData CreateBaseEquipment(EquipmentType type)
        {
            return new EquipmentData
            {
                Id = "equip_base",
                EquipmentType = type,
                Category = ItemCategory.Equipment,
                ItemRarity = Rarity.Common, // Will be overridden by context.ForcedQuality
                SellPrice = 20,
                StackSize = 1
            };
        }
    }
}