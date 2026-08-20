using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Generation;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Generates final crafted items with proper levels, stats, and quality.
    /// Uses ItemGenerator for shared item generation logic.
    /// Slot-fallback resolves craft_* recipe ids to base equipment templates.
    /// Equipment rarity comes from recipe.Rarity; level comes from recipe.RequiredTier.
    /// AttributeStats are generated procedurally at craft time (not from base template).
    /// Uses deterministic seeded RNG via CompletionSeed for I-11 replay consistency.
    ///</summary>
    public sealed class CraftRewardService
    {
        private readonly ItemGenerator _itemGenerator;

        public CraftRewardService(ItemGenerator itemGenerator)
        {
            _itemGenerator = itemGenerator;
        }

        // ============ Public API ============
        public InventoryItem[] GenerateRewards(CraftRollResult rollResult, CraftRecipeData recipe, CraftContext context, long seed = 0)
        {
            if (!rollResult.Success || rollResult.Entries.Count == 0)
                return Array.Empty<InventoryItem>();

            var items = new List<InventoryItem>();

            foreach (var entry in rollResult.Entries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    var item = GenerateSingleItem(entry, recipe, context, seed + i);
                    if (item != null)
                        items.Add(item);
                }
            }

            return items.ToArray();
        }

        private InventoryItem GenerateSingleItem(CraftResultEntry entry, CraftRecipeData recipe, CraftContext context, long seed)
        {
            // Handle placeholder IDs for equipment (crafted_equipment, mastery_extra) without casting errors.
            // If entry uses placeholder, generate equipment via base template regardless of recipe.Category.
            bool isPlaceholder = entry.ItemId.StartsWith("crafted_") || entry.ItemId == "mastery_extra";
            if (isPlaceholder)
            {
                var slot = InferSlotFromRecipe(recipe);
                if (slot.HasValue)
                {
                    return GenerateEquipmentFromBase(recipe, context, slot.Value, seed);
                }
                // No slot inference → cannot generate equipment, skip.
                return null;
            }

            // Slot‑based fallback: craft_* recipe ids don't directly resolve as EquipmentData.
            // Resolve via the 11 base templates (equip_<slot>_base); rarity/level from the recipe.
            var slotFromRecipe = InferSlotFromRecipe(recipe);
            if (slotFromRecipe.HasValue && recipe.Category == ItemCategory.Equipment)
            {
                return GenerateEquipmentFromBase(recipe, context, slotFromRecipe.Value, seed);
            }

            // Convert active modifiers (ICraftModifier) to the expected EventCraftModifier list.
            // Only EventCraftModifier instances are relevant for item generation; other modifiers
            // (e.g., ExtraItemModifier) are not compatible with the ItemGenerationContext.EventModifiers type.
            var eventModifiers = new List<EventCraftModifier>();
            foreach (var mod in context.ActiveEventModifiers)
            {
                if (mod is EventCraftModifier ev)
                {
                    eventModifiers.Add(ev);
                }
            }

            // Build generation context for non‑equipment items
            var genContext = new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                RecipeId = recipe.RecipeId,
                PlayerLevel = context.CraftingLevel,
                CraftingMastery = context.GetMasteryLevel(recipe.RecipeId),
                BlacksmithLevel = context.BlacksmithLevel,
                // Safe: eventModifiers is List<EventCraftModifier>, implements IReadOnlyList<EventCraftModifier>
                EventModifiers = eventModifiers,
                Luck = context.Luck,
                ForcedQuality = entry.Quality > 0 ? entry.Quality : -1,
                FixedLevel = entry.FixedLevel > 0 ? entry.FixedLevel : -1,
                FixedEnhance = entry.FixedEnhance > 0 ? entry.FixedEnhance : -1
            };

            return _itemGenerator.GenerateItem(entry.ItemId, genContext);
        }

        // ============ Equipment Generation ============
        /// <summary>
        /// Generates equipment using base template + rarity from recipe.Rarity (v3.8 §20.1).
        /// Rarity is the sole output tier; RequiredTier remains a progression gate and
        /// only feeds the generated item's level. Routes through ItemGenerator so the full
        /// EquipmentGenerator pipeline runs (CustomData: AttributeStats/secondaries/affixes/sockets).
        /// Uses deterministic seeded RNG via CompletionSeed for I-11 replay consistency.
        ///</summary>
        private InventoryItem GenerateEquipmentFromBase(
            CraftRecipeData recipe,
            CraftContext context,
            EquipmentType slot,
            long seed)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return null;

            string baseId = $"equip_{slot.ToString().ToLower()}_base";
            string OutputItemId = recipe.DisplayName.ToLowerInvariant().Replace(" ", "_");  // "Cotton Hat" → "cotton_hat"

            var baseEquip = db.GetEquipment(baseId);
            if (baseEquip == null)
            {
                Debug.LogError($"[CraftRewardService] Missing base template: {baseId}");
                return null;
            }

            // v3.8 §20.1 — rarity source of truth: recipe.Rarity (1=Common..6=Divine).
            // EquipmentGenerator expects 0-based quality tier: 0=Common, 1=Rare, ..., 5=Divine.
            int qualityTier = Mathf.Max(0, recipe.Rarity - 1);
            int level = Mathf.Max(1, recipe.RequiredCraftingLevel);

            // Convert active modifiers (ICraftModifier) to the expected EventCraftModifier list.
            var eventModifiers = new List<EventCraftModifier>();
            foreach (var mod in context.ActiveEventModifiers)
            {
                if (mod is EventCraftModifier ev)
                    eventModifiers.Add(ev);
            }

            // Build context via helper — sets Source, EquipmentType, Category, PlayerLevel, ForcedQuality, FixedLevel correctly.
            var genContext = ItemGenerationContext.Equipment(
                                 equipmentType: slot,
                                 rarity: (Rarity)recipe.Rarity,
                                 level: level,
                                 tier: recipe.RequiredCraftingLevel)
                             .With(
                                 seed: (int)seed,
                                 forcedQuality: qualityTier,
                                 fixedLevel: level,
                                 eventModifiers: eventModifiers,
                                 customData: new Dictionary<string, object>
                                 {
                                     { "OverrideItemId", OutputItemId }
                                 });

            // Use injected generator (shares RNG with pipeline) for I-11 determinism.
            return _itemGenerator.GenerateEquipmentFromBase(baseEquip, genContext);
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
    }
}