using System;
using System.Collections.Generic;
using System.Linq; // Added this
using IdleDefenseSurvival.Crafting;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Base interface for craft modifiers.
    /// Each modifier type handles a specific aspect of crafting.
    /// </summary>
    public interface ICraftModifier
    {
        string ModifierId { get; }
        string DisplayName { get; }
        bool AppliesToRecipe(CraftRecipeData recipe);
        void Apply(ItemGenerationContext context, ref CraftRollResult result);
    }

    /// <summary>
    /// Result of craft rolling (from CraftRollService).
    /// </summary>
    public class CraftRollResult
    {
        public bool Success = true;
        public string FailureReason;
        public List<CraftResultEntry> Entries = new();
        public long ExpReward = 0;

        public static CraftRollResult Fail(string reason)
        {
            return new CraftRollResult { Success = false, FailureReason = reason };
        }
    }

    public class CraftResultEntry
    {
        public string ItemId;
        public int Count = 1;
        public int Quality = 0;
        public string Source;
        public bool IsCritical = false;
        public int FixedLevel = 0;
    }

    public enum CraftCategory
    {
        Equipment = 0,
        Gem = 1,
        Consumable = 2,
        Material = 3,
        Enchantment = 4,
        Upgrade = 5
    }

    public class RecipeResult
    {
        public string ItemId;
        public int MinCount = 1;
        public int MaxCount = 1;
        public int MinQuality = 0;
        public int MaxQuality = 0;
        public int FixedLevel = 0;
        public bool IsMainResult = false;
    }

    public class GuaranteedResult
    {
        public string ItemId;
        public int MinCount = 1;
        public int MaxCount = 1;
        public int MinQuality = 0;
        public int MaxQuality = 0;
    }

    public class RecipeIngredient
    {
        public string ItemId;
        public int Count = 1;
        public bool Consumed = true;
    }

    // ============ Concrete Modifiers ============

    /// <summary>
    /// Grants extra items on craft.
    /// </summary>
    public sealed class ExtraItemModifier : ICraftModifier
    {
        public string ModifierId => "ExtraItem";
        public string DisplayName => "Extra Item";
        public string ExtraItemId { get; set; }
        public int ExtraItemCount { get; set; } = 1;
        public int ExtraItemQuality { get; set; } = 0;
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            if (!string.IsNullOrEmpty(ExtraItemId))
            {
                result.Entries.Add(new CraftResultEntry
                {
                    ItemId = ExtraItemId,
                    Count = ExtraItemCount,
                    Quality = ExtraItemQuality,
                    Source = $"Modifier_{ModifierId}",
                    IsCritical = false
                });
            }
        }
    }

    /// <summary>
    /// Doubles craft output.
    /// </summary>
    public sealed class DoubleOutputModifier : ICraftModifier
    {
        public string ModifierId => "DoubleOutput";
        public string DisplayName => "Double Output";
        public float Chance { get; set; } = 1f; // 100% by default
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            if (UnityEngine.Random.value > Chance) return;

            var originalEntries = result.Entries.ToArray();
            foreach (var entry in originalEntries)
            {
                result.Entries.Add(new CraftResultEntry
                {
                    ItemId = entry.ItemId,
                    Count = entry.Count,
                    Quality = entry.Quality,
                    Source = $"Modifier_{ModifierId}",
                    IsCritical = true
                });
            }
        }
    }

    /// <summary>
    /// Reduces crafting time.
    /// </summary>
    public sealed class ReduceTimeModifier : ICraftModifier
    {
        public string ModifierId => "ReduceTime";
        public string DisplayName => "Reduced Time";
        public float TimeReductionPercent { get; set; } = 0.25f; // 25%
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            // Time reduction is handled by the queue service, not the roll
            // This modifier marks the context for the queue to pick up
            context = context.With(customData: new System.Collections.Generic.Dictionary<string, object>
            {
                ["TimeReductionPercent"] = TimeReductionPercent
            });
        }
    }

    /// <summary>
    /// Increases quality of crafted items.
    /// </summary>
    public sealed class QualityModifier : ICraftModifier
    {
        public string ModifierId => "QualityBoost";
        public string DisplayName => "Quality Boost";
        public int QualityBonus { get; set; } = 1;
        public float Chance { get; set; } = 1f;
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            if (UnityEngine.Random.value > Chance) return;

            foreach (var entry in result.Entries)
            {
                entry.Quality = Math.Min(entry.Quality + QualityBonus, 8); // Cap at Divine
            }
        }
    }

    /// <summary>
    /// Grants bonus socket on crafted equipment.
    /// </summary>
    public sealed class SocketModifier : ICraftModifier
    {
        public string ModifierId => "BonusSocket";
        public string DisplayName => "Bonus Socket";
        public int ExtraSockets { get; set; } = 1;
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            // Mark context for EquipmentGenerator to pick up
            context = context.With(customData: new System.Collections.Generic.Dictionary<string, object>
            {
                ["ExtraSockets"] = ExtraSockets
            });
        }
    }

    /// <summary>
    /// Guarantees minimum rarity on craft.
    /// </summary>
    public sealed class GuaranteedRarityModifier : ICraftModifier
    {
        public string ModifierId => "GuaranteedRarity";
        public string DisplayName => "Guaranteed ItemRarity";
        public Rarity MinimumRarity { get; set; } = Rarity.Rare;
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            context = context.With(forcedQuality: (int)MinimumRarity);
        }
    }

    /// <summary>
    /// Grants critical craft chance bonus.
    /// </summary>
    public sealed class CriticalCraftModifier : ICraftModifier
    {
        public string ModifierId => "CriticalCraft";
        public string DisplayName => "Critical Craft Chance";
        public float CriticalChanceBonus { get; set; } = 0.1f; // +10%
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            context = context.With(customData: new Dictionary<string, object>
            {
                ["CriticalChanceBonus"] = CriticalChanceBonus
            });
        }
    }

    /// <summary>
    /// Reduces material cost.
    /// </summary>
    public sealed class ReduceCostModifier : ICraftModifier
    {
        public string ModifierId => "ReduceCost";
        public string DisplayName => "Reduced Cost";
        public float CostReductionPercent { get; set; } = 0.2f; // 20%
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            context = context.With(customData: new System.Collections.Generic.Dictionary<string, object>
            {
                ["CostReductionPercent"] = CostReductionPercent
            });
        }
    }

    /// <summary>
    /// Double EXP from crafting.
    /// </summary>
    public sealed class DoubleExpModifier : ICraftModifier
    {
        public string ModifierId => "DoubleExp";
        public string DisplayName => "Double Experience";
        public float ExpMultiplier { get; set; } = 2f;
        public string[] ApplicableRecipeIds { get; set; }
        public string[] ApplicableCategories { get; set; }

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Any(r => r == recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Any(c => c == recipe.Category.ToString()))
                return true;
            return false;
        }

        public void Apply(ItemGenerationContext context, ref CraftRollResult result)
        {
            result.ExpReward = (long)(result.ExpReward * ExpMultiplier);
        }
    }
}