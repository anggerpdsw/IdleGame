using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Generation;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Generates final crafted items with proper levels, stats, and quality.
    /// Uses ItemGenerator for shared item generation logic.
    /// Slot-fallback resolves craft_* recipe ids to base equipment templates.
    ///</summary>
    public sealed class CraftRewardService
    {
        private readonly ItemGenerator _itemGenerator;

        public CraftRewardService(ItemGenerator itemGenerator)
        {
            _itemGenerator = itemGenerator;
        }

        // ============ Public API ============
        public InventoryItem[] GenerateRewards(CraftRollResult rollResult, CraftRecipeData recipe, CraftContext context)
        {
            if (!rollResult.Success || rollResult.Entries.Count == 0)
                return Array.Empty<InventoryItem>();

            var items = new List<InventoryItem>();

            foreach (var entry in rollResult.Entries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    var item = GenerateSingleItem(entry, recipe, context);
                    if (item != null)
                        items.Add(item);
                }
            }

            return items.ToArray();
        }

        private InventoryItem GenerateSingleItem(CraftResultEntry entry, CraftRecipeData recipe, CraftContext context)
        {
            // Slot-based fallback: craft_* recipe ids don't directly resolve as EquipmentData.
            // Resolve via the 11 base templates (equip_<slot>_base) and scale by recipe.RequiredTier.
            var slot = InferSlotFromRecipe(recipe);
            if (slot.HasValue && recipe.Category == ItemCategory.Equipment)
            {
                return GenerateEquipmentFromBase(entry, recipe, context, slot.Value);
            }

            // Build generation context for non-equipment items
            var genContext = new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                RecipeId = recipe.RecipeId,
                PlayerLevel = context.CraftingLevel,
                CraftingMastery = context.GetMasteryLevel(recipe.RecipeId),
                BlacksmithLevel = context.BlacksmithLevel,
                EventModifiers = (IReadOnlyList<EventCraftModifier>)context.ActiveEventModifiers,
                Luck = context.Luck,
                ForcedQuality = entry.Quality > 0 ? entry.Quality : -1,
                FixedLevel = entry.FixedLevel > 0 ? entry.FixedLevel : -1,
                FixedEnhance = entry.FixedEnhance > 0 ? entry.FixedEnhance : -1
            };

            return _itemGenerator.GenerateItem(entry.ItemId, genContext);
        }

        // ============ Equipment Generation ============
        /// <summary>
        /// Generates equipment using base template + rarity scaling via ItemDatabase.GenerateEquipment.
        /// Overrides display name with recipe-specific identity (e.g., Vega Hat vs generic Hat).
        ///</summary>
        private InventoryItem GenerateEquipmentFromBase(
            CraftResultEntry entry,
            CraftRecipeData recipe,
            CraftContext context,
            EquipmentType slot)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return null;

            string baseId = $"equip_{slot.ToString().ToLower()}_base";
            var baseEquip = db.GetEquipment(baseId);
            if (baseEquip == null)
            {
                UnityEngine.Debug.LogError($"[CraftRewardService] Missing base template: {baseId}");
                return null;
            }

            int rarityLevel = recipe.RequiredTier > 0 ? recipe.RequiredTier : 1;
            var generated = db.GenerateEquipment(
                baseEquip.Id,
                (Rarity)rarityLevel,
                rarityLevel,
                slot);

            if (generated == null) return null;

            // Override with recipe-specific identity (rarity + slot-specific naming)
            if (!string.IsNullOrEmpty(recipe.DisplayName))
                generated.Name = recipe.DisplayName;

            return ToInventoryItem(generated);
        }

        /// <summary>
        /// Infers the equipment slot from the recipe id (e.g., craft_cotton_hat -> Hat).
        ///</summary>
        private static EquipmentType? InferSlotFromRecipe(CraftRecipeData recipe)
        {
            string id = recipe?.RecipeId?.ToLowerInvariant() ?? string.Empty;
            if (id.Contains("_hat")) return EquipmentType.Hat;
            if (id.Contains("_gloves")) return EquipmentType.Gloves;
            if (id.Contains("_cape")) return EquipmentType.Cape;
            if (id.Contains("_armor")) return EquipmentType.Armor;
            if (id.Contains("_belt")) return EquipmentType.Belt;
            if (id.Contains("_pants")) return EquipmentType.Pants;
            if (id.Contains("_pendant")) return EquipmentType.Pendant;
            if (id.Contains("_earring")) return EquipmentType.Earring;
            if (id.Contains("_bracelet")) return EquipmentType.Bracelet;
            if (id.Contains("_ring")) return EquipmentType.Ring;
            if (id.Contains("_shoes") || id.Contains("_boots")) return EquipmentType.Shoes;
            return null;
        }

        /// <summary>
        /// Converts EquipmentData -> InventoryItem with proper InstanceId.
        ///</summary>
        private static InventoryItem ToInventoryItem(EquipmentData equip)
        {
            return new InventoryItem
            {
                ItemId = equip.Id,
                Quantity = 1,
                Level = equip.BaseLevel,
                EnhanceLevel = 0,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
