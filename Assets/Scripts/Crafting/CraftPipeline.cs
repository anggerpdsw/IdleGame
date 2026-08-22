using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Interface for a pipeline stage.
    /// Each stage has a single responsibility.
    /// </summary>
    public interface ICraftPipelineStage
    {
        string StageName { get; }
        int Order { get; }
        bool CanExecute(CraftPipelineContext ctx);
        void Execute(CraftPipelineContext ctx);
    }

    /// <summary>
    /// Base class for pipeline stages.
    /// </summary>
    public abstract class CraftPipelineStageBase : ICraftPipelineStage
    {
        public abstract string StageName { get; }
        public abstract int Order { get; }
        public virtual bool CanExecute(CraftPipelineContext ctx) => true;
        public abstract void Execute(CraftPipelineContext ctx);
    }

    // ============ Pipeline Stages ============

    /// <summary>
    /// Stage 1: Validate recipe and context.
    /// </summary>
    public class ValidationStage : CraftPipelineStageBase
    {
        public override string StageName => "Validation";
        public override int Order => 0;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;

            if (ctx.Recipe == null)
            {
                ctx.Success = false;
                ctx.FailureReason = "Recipe is null";
                return;
            }

            if (ctx.Context == null)
            {
                ctx.Success = false;
                ctx.FailureReason = "Context is null";
                return;
            }

            if (ctx.Rng == null)
            {
                ctx.Success = false;
                ctx.FailureReason = "RNG provider is null";
                return;
            }
        }
    }

    /// <summary>
    /// Stage 2: Calculate and check success rate.
    /// </summary>
    public class SuccessStage : CraftPipelineStageBase
    {
        private readonly CraftFormulasConfig _config;

        public SuccessStage(CraftFormulasConfig config = null)
        {
            _config = config ?? new CraftFormulasConfig();
        }

        public override string StageName => "SuccessCheck";
        public override int Order => 10;

        public override void Execute(CraftPipelineContext ctx)
        {
            float successRate = CalculateSuccessRate(ctx);
            successRate = Mathf.Clamp(successRate, 0f, 100f);

            if (ctx.Rng.ChancePercent(successRate))
            {
                ctx.Success = true;
            }
            else
            {
                ctx.Success = false;
                ctx.FailureReason = "Craft failed (success rate check)";
            }
        }

        private float CalculateSuccessRate(CraftPipelineContext ctx)
        {
            float rate = ctx.Recipe.BaseSuccessRate;
            rate += ctx.Context.CraftingLevel * ctx.Recipe.SuccessRatePerLevel;
            return rate;
        }
    }

    /// <summary>
    /// Stage 3: Add base equipment result (deterministic - no RNG).
    /// Equipment is generated from recipe metadata (Rarity, RequiredTier, EquipmentType).
    /// </summary>
    public class BaseEquipmentStage : CraftPipelineStageBase
    {
        public override string StageName => "BaseEquipment";
        public override int Order => 100;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;
            int count = Mathf.Max(1, ctx.Context?.PlayerStats?.JobCount ?? 1);
            ctx.Entries.Add(new CraftResultEntry
            {
                ItemId = "crafted_equipment", // Placeholder - resolved in CraftRewardService
                Count = count,
                Quality = ctx.Recipe.Rarity, // Recipe rarity is the quality tier
                Source = CraftRewardSource.Normal.ToString(),
                FixedLevel = 0 // Level determined by recipe progression
            });
        }
    }

    /// <summary>
    /// Stage 5: Apply event/seasonal modifiers.
    /// </summary>
    public class EventStage : CraftPipelineStageBase
    {
        public override string StageName => "EventModifiers";
        public override int Order => 250;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;

            // Apply all event modifiers from context
            var modifiers = ctx.Context.ActiveEventModifiers;
            if (modifiers == null || modifiers.Count == 0) return;

            // Sort by priority
            var sortedModifiers = modifiers.OrderBy(m => m.Priority).ToList();

            foreach (var modifier in sortedModifiers)
            {
                if (modifier.CanApply(ctx.Context, ctx.Recipe))
                {
                    modifier.Apply(ctx);
                }
            }
        }
    }

    /// <summary>
    /// Stage 7: Validate final results (quality bounds, count bounds, valid item IDs).
    /// </summary>
    public class ValidationFinalStage : CraftPipelineStageBase
    {
        private readonly IItemDatabase _itemDatabase;

        public ValidationFinalStage(IItemDatabase itemDatabase = null)
        {
            _itemDatabase = itemDatabase;
        }

        public override string StageName => "FinalValidation";
        public override int Order => 400;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;

            var validEntries = new List<CraftResultEntry>();

            foreach (var entry in ctx.Entries)
            {
                if (ValidateEntry(entry))
                {
                    // Clamp quality to valid range
                    entry.Quality = Mathf.Clamp(entry.Quality, 0, 5);
                    // Ensure count is positive
                    entry.Count = Mathf.Max(1, entry.Count);
                    validEntries.Add(entry);
                }
                else
                {
                    Debug.LogWarning($"[CraftPipeline] Invalid entry filtered out: {entry.ItemId}");
                }
            }

            ctx.Entries = validEntries;

            if (ctx.Entries.Count == 0 && ctx.Success)
            {
                ctx.Success = false;
                ctx.FailureReason = "No valid results after validation";
            }
        }

        private bool ValidateEntry(CraftResultEntry entry)
        {
            if (string.IsNullOrEmpty(entry.ItemId))
                return false;

            if (entry.Count <= 0)
                return false;

            // Allow placeholder IDs that will be resolved by CraftRewardService later
            if (entry.ItemId == "crafted_equipment" || entry.ItemId == "mastery_extra")
                return true;

            if (_itemDatabase != null && !_itemDatabase.IsValidItemId(entry.ItemId))
            {
                Debug.LogWarning($"[CraftPipeline] ItemId not found in database: {entry.ItemId}");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Stage 8: Calculate final EXP reward.
    /// </summary>
    public class ExperienceStage : CraftPipelineStageBase
    {
        public override string StageName => "ExperienceReward";
        public override int Order => 500;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;
            long exp = ctx.Recipe.BaseExpReward;
            exp += ctx.Context.CraftingLevel * ctx.Recipe.ExpPerAdditionalUnit;
            exp = (long)(exp * ctx.Context.ExpMultiplier);
            exp = (long)(exp * ctx.ExpMultiplier);
            ctx.ExpReward = Math.Max(0, exp);
        }
    }

    /// <summary>
    /// Stage 9: Finalize - apply failure behavior if needed.
    /// </summary>
    public class FinalizeStage : CraftPipelineStageBase
    {
        private readonly CraftFormulasConfig _config;

        public FinalizeStage(CraftFormulasConfig config = null)
        {
            _config = config ?? new CraftFormulasConfig();
        }

        public override string StageName => "Finalize";
        public override int Order => 600;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success)
            {
                // Apply failure behavior
                HandleFailure(ctx);
            }
        }

        private void HandleFailure(CraftPipelineContext ctx)
        {
            // Could add partial results on failure based on config
            // For now, just clear entries on failure
            ctx.Entries.Clear();
            ctx.ExpReward = 0;
        }
    }

    // ============ Pipeline Orchestrator ============

    /// <summary>
    /// Executes all craft pipeline stages in order.
    /// </summary>
    public sealed class CraftPipeline
    {
        private readonly List<ICraftPipelineStage> _stages = new();
        private readonly CraftFormulasConfig _config;
        private readonly IItemDatabase _itemDatabase;

        public CraftPipeline(CraftFormulasConfig config = null, IItemDatabase itemDatabase = null)
        {
            _config = config ?? new CraftFormulasConfig();
            _itemDatabase = itemDatabase;

            // Register stages in order
            RegisterStage(new ValidationStage());
            RegisterStage(new SuccessStage(_config));
            RegisterStage(new BaseEquipmentStage());
            RegisterStage(new EventStage());
            RegisterStage(new ValidationFinalStage(_itemDatabase));
            RegisterStage(new ExperienceStage());
            RegisterStage(new FinalizeStage(_config));

            // Sort by order
            _stages.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public void RegisterStage(ICraftPipelineStage stage)
        {
            _stages.Add(stage);
        }

        public void RemoveStage(string stageName)
        {
            _stages.RemoveAll(s => s.StageName == stageName);
        }

        public CraftRollResult Execute(CraftRecipeData recipe, CraftContext context, IRandomProvider rng)
        {
            var pipelineCtx = new CraftPipelineContext
            {
                Recipe = recipe,
                Context = context,
                Rng = rng ?? new UnityRandomProvider(),
                CraftTime = recipe.BaseCraftTime
            };

            foreach (var stage in _stages)
            {
                if (stage.CanExecute(pipelineCtx))
                {
                    try
                    {
                        stage.Execute(pipelineCtx);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[CraftPipeline] Stage {stage.StageName} failed: {e}");
                        pipelineCtx.Success = false;
                        pipelineCtx.FailureReason = $"Pipeline error in {stage.StageName}";
                        break;
                    }
                }

                // Early exit on failure
                if (!pipelineCtx.Success && stage.Order >= 10) break;
            }

            return new CraftRollResult
            {
                Success = pipelineCtx.Success,
                Entries = pipelineCtx.Entries,
                ExpReward = pipelineCtx.ExpReward, // → belum diapliaksikan saat ini
                FailureReason = pipelineCtx.FailureReason
            };
        }

        public IReadOnlyList<ICraftPipelineStage> GetStages() => _stages;
    }

    /// <summary>
    /// Result of a craft roll (matches existing interface for compatibility).
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
}