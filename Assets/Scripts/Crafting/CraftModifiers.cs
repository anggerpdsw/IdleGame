using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items.Random;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Source of a craft reward entry - strongly typed for compile-time safety.
    /// </summary>
    public enum CraftRewardSource
    {
        Normal = 0,
        Critical = 1,
        Guaranteed = 2,
        Mastery = 3,
        Event = 4
    }

    /// <summary>
    /// Base interface for all craft modifiers.
    /// Each modifier has a single responsibility and can be composed.
    /// </summary>
    public interface ICraftModifier
    {
        string ModifierId { get; }
        int Priority { get; }           // Execution order (lower = earlier)
        bool CanApply(CraftContext ctx, CraftRecipeData recipe);
        void Apply(CraftPipelineContext pipelineCtx);
    }

    /// <summary>
    /// Base class for craft modifiers with common functionality.
    /// </summary>
    public abstract class CraftModifierBase : ICraftModifier
    {
        public abstract string ModifierId { get; }
        public virtual int Priority => 100;

        public virtual bool CanApply(CraftContext ctx, CraftRecipeData recipe) => true;

        public abstract void Apply(CraftPipelineContext pipelineCtx);

        protected CraftResultEntry CreateEntry(string itemId, int count, int quality, CraftRewardSource source, int fixedLevel = 0)
        {
            return new CraftResultEntry
            {
                ItemId = itemId,
                Count = count,
                Quality = quality,
                Source = source.ToString(),
                FixedLevel = fixedLevel
            };
        }
    }

    // ============ Concrete Modifiers ============

    /// <summary>
    /// Adds extra items to the craft result.
    /// </summary>
    [Serializable]
    public class ExtraItemModifier : CraftModifierBase
    {
        public string ItemId;
        public int MinCount = 1;
        public int MaxCount = 1;
        public string[] ApplicableRecipeIds;
        public string[] ApplicableCategories;

        public override string ModifierId => "ExtraItem";
        public override int Priority => 200;

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe)
        {
            if (!string.IsNullOrEmpty(ItemId))
            {
                if (ApplicableRecipeIds?.Length > 0 && !Array.Exists(ApplicableRecipeIds, id => id == recipe.RecipeId))
                    return false;
                if (ApplicableCategories?.Length > 0 && !Array.Exists(ApplicableCategories, cat => cat == recipe.Category.ToString()))
                    return false;
                return true;
            }
            return false;
        }

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            int count = pipelineCtx.Rng.Range(MinCount, MaxCount + 1);
            int quality = pipelineCtx.Recipe.Rarity;
            for (int i = 0; i < count; i++)
            {
                pipelineCtx.Entries.Add(CreateEntry(ItemId, 1, quality, CraftRewardSource.Event));
            }
        }
    }

    /// <summary>
    /// Modifies critical chance.
    /// </summary>
    [Serializable]
    public class CriticalChanceModifier : CraftModifierBase
    {
        public float FlatBonus = 0f;
        public float PercentBonus = 0f;

        public override string ModifierId => "CriticalChance";
        public override int Priority => 20;

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => FlatBonus != 0f || PercentBonus != 0f;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            pipelineCtx.BonusCrafting += FlatBonus;
        }
    }

    /// <summary>
    /// Modifies quality of results.
    /// </summary>
    [Serializable]
    public class QualityModifier : CraftModifierBase
    {
        public int FlatQualityBonus = 0;       // +1 quality tier
        public float QualityUpgradeChance = 0f; // Chance to upgrade quality
        public int MaxQuality = 5;

        public override string ModifierId => "Quality";
        public override int Priority => 150; // After base results, before critical

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => FlatQualityBonus != 0 || QualityUpgradeChance > 0f;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            foreach (var entry in pipelineCtx.Entries)
            {
                entry.Quality = Mathf.Min(entry.Quality + FlatQualityBonus, MaxQuality);

                if (pipelineCtx.Rng.ChancePercent(QualityUpgradeChance * 100f))
                {
                    entry.Quality = Mathf.Min(entry.Quality + 1, MaxQuality);
                }
            }
        }
    }

    /// <summary>
    /// Modifies EXP reward.
    /// </summary>
    [Serializable]
    public class ExpModifier : CraftModifierBase
    {
        public float FlatBonus = 0f;
        public float PercentBonus = 0f;

        public override string ModifierId => "Exp";
        public override int Priority => 500; // Last, after everything

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => FlatBonus != 0f || PercentBonus != 0f;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            pipelineCtx.ExpMultiplier *= 1f + PercentBonus;
        }
    }

    /// <summary>
    /// Modifies craft time.
    /// </summary>
    [Serializable]
    public class TimeModifier : CraftModifierBase
    {
        public float FlatReduction = 0f;     // Seconds reduced
        public float PercentReduction = 0f;  // Percentage reduced (0-1)
        public bool InstantCraft = false;

        public override string ModifierId => "Time";
        public override int Priority => 5; // Very early

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => InstantCraft || FlatReduction != 0f || PercentReduction != 0f;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            if (InstantCraft)
            {
                pipelineCtx.CraftTime = 0f;
            }
            else
            {
                pipelineCtx.CraftTime = Mathf.Max(0f, pipelineCtx.CraftTime - FlatReduction);
                pipelineCtx.CraftTime *= (1f - PercentReduction);
            }
        }
    }

    /// <summary>
    /// Adds a socket to the crafted item.
    /// </summary>
    [Serializable]
    public class SocketModifier : CraftModifierBase
    {
        public int MinSockets = 1;
        public int MaxSockets = 1;
        public float SocketChance = 1f;

        public override string ModifierId => "Socket";
        public override int Priority => 250;

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => SocketChance > 0f;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            if (pipelineCtx.Rng.Chance(SocketChance))
            {
                int sockets = pipelineCtx.Rng.Range(MinSockets, MaxSockets + 1);
                foreach (var entry in pipelineCtx.Entries)
                {
                    entry.SocketCount = Mathf.Max(entry.SocketCount, sockets);
                }
            }
        }
    }

    /// <summary>
    /// Guarantees a specific rarity/quality.
    /// </summary>
    [Serializable]
    public class GuaranteedQualityModifier : CraftModifierBase
    {
        public int MinQuality = 4; // Legendary = 4, Mythic = 5

        public override string ModifierId => "GuaranteedQuality";
        public override int Priority => 300;

        public override bool CanApply(CraftContext ctx, CraftRecipeData recipe) => MinQuality > 0;

        public override void Apply(CraftPipelineContext pipelineCtx)
        {
            foreach (var entry in pipelineCtx.Entries)
            {
                if (entry.Quality < MinQuality)
                    entry.Quality = MinQuality;
            }
        }
    }

    /// <summary>
    /// Configuration for all craft formulas - data-driven balancing.
    /// </summary>
    [Serializable]
    public class CraftFormulasConfig
    {
        [Header("Quality")]
        public int MaxQualityTier = 5;
    }

    /// <summary>
    /// Extended result entry with socket support.
    /// </summary>
    [Serializable]
    public class CraftResultEntry
    {
        public string ItemId;
        public int Count = 1;
        public int Quality = 0; // 0 = base, 1-5 = quality tiers
        public string Source;   // CraftRewardSource enum as string for serialization
        public int FixedLevel = 0;
        public int SocketCount = 0; // Future: sockets on crafted items
    }

    /// <summary>
    /// Context passed through the pipeline - mutable state accumulated by stages.
    /// </summary>
    public class CraftPipelineContext
    {
        // Input (from CraftContext + Recipe)
        public CraftRecipeData Recipe;
        public CraftContext Context;
        public IRandomProvider Rng;

        // Mutable state accumulated by stages
        public List<CraftResultEntry> Entries = new();
        public long ExpReward = 0;
        public float CraftTime = 0f;

        // Modifiers applied by stages
        public float BonusCrafting = 0f;
        public float ExpMultiplier = 1f;

        // Output
        public bool Success = true;
        public string FailureReason;
    }

    /// <summary>
    /// Split CraftContext into focused sub-contexts for maintainability.
    /// </summary>
    [Serializable]
    public class PlayerCraftStats
    {
        public int CraftingLevel = 1;
        public int BlacksmithLevel = 1;
        public int JobCount = 1; 
    }

    [Serializable]
    public class CraftBuffContext
    {
        public float ExpMultiplier = 1f;
        public float ExtraItemChance = 0f;
    }

    [Serializable]
    public class CraftEventContext
    {
        public List<ICraftModifier> ActiveModifiers = new();
    }

    [Serializable]
    public class CraftRngContext
    {
        public int Seed = 0;
        public bool UseFixedSeed = false;
    }

    /// <summary>
    /// Composed context - replaces the old monolithic CraftContext.
    /// </summary>
    [Serializable]
    public class CraftContext
    {
        public PlayerCraftStats PlayerStats = new();
        public CraftBuffContext Buffs = new();
        public CraftEventContext Events = new();
        public CraftRngContext Rng = new();

        // Backward compatibility helpers
        public int CraftingLevel => PlayerStats.CraftingLevel;
        public int BlacksmithLevel => PlayerStats.BlacksmithLevel;
        public float ExpMultiplier => Buffs.ExpMultiplier;
        public float ExtraItemChance => Buffs.ExtraItemChance;
        public List<ICraftModifier> ActiveEventModifiers => Events.ActiveModifiers;

    }

    /// <summary>
    /// Legacy EventCraftModifier for backward compatibility.
    /// </summary>
    [Serializable]
    public class EventCraftModifier
    {
        public string EventId;
        public string[] ApplicableRecipeIds;
        public string[] ApplicableCategories;
        public bool GrantExtraItem = false;
        public string ExtraItemId;
        public int ExtraItemCount = 1;

        public bool AppliesToRecipe(CraftRecipeData recipe)
        {
            if (ApplicableRecipeIds != null && ApplicableRecipeIds.Contains(recipe.RecipeId))
                return true;
            if (ApplicableCategories != null && ApplicableCategories.Contains(recipe.Category.ToString()))
                return true;
            return false;
        }
    }
}