using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items.Random;
using UnityEngine;

namespace IdleDefenseSurvival.Items
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
            if (ctx.Recipe == null)
            {
                ctx.Success = false;
                ctx.FailureReason = "Recipe is null";
                return;
            }

            if (!ctx.Recipe.IsValid())
            {
                ctx.Success = false;
                ctx.FailureReason = "Recipe validation failed";
                return;
            }

            if (ctx.Context == null)
            {
                ctx.Success = false;
                ctx.FailureReason = "CraftContext is null";
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
            successRate += ctx.SuccessRateBonus;
            successRate *= ctx.SuccessRateMultiplier;
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
            rate += ctx.Context.Luck * _config.LuckToSuccessRate;
            return rate;
        }
    }

    /// <summary>
    /// Stage 3: Roll base results from recipe.
    /// </summary>
    public class BaseRewardStage : CraftPipelineStageBase
    {
        public override string StageName => "BaseReward";
        public override int Order => 100;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;
            if (ctx.Recipe.PossibleResults == null || ctx.Recipe.PossibleResults.Length == 0) return;

            var result = RollBaseResult(ctx);
            if (result != null)
            {
                ctx.Entries.Add(result);
            }
        }

        private CraftResultEntry RollBaseResult(CraftPipelineContext ctx)
        {
            var results = ctx.Recipe.PossibleResults;
            float totalWeight = results.Sum(r => r.Weight);
            float roll = ctx.Rng.Range(0f, totalWeight);
            float accumulated = 0f;

            foreach (var recipeResult in results)
            {
                accumulated += recipeResult.Weight;
                if (roll <= accumulated)
                {
                    int count = ctx.Rng.Range(recipeResult.MinCount, recipeResult.MaxCount + 1);
                    int quality = ctx.Rng.Range(recipeResult.MinQuality, recipeResult.MaxQuality + 1);

                    // Only create ONE entry with the total count (not multiple entries)
                    return new CraftResultEntry
                    {
                        ItemId = recipeResult.ItemId,
                        Count = count,
                        Quality = quality,
                        Source = CraftRewardSource.Normal.ToString(),
                        IsCritical = false,
                        FixedLevel = recipeResult.FixedLevel,
                        FixedEnhance = recipeResult.FixedEnhance
                    };
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Stage 4: Apply guaranteed results.
    /// </summary>
    public class GuaranteedStage : CraftPipelineStageBase
    {
        public override string StageName => "GuaranteedReward";
        public override int Order => 150;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;
            if (ctx.Recipe.GuaranteedResult == null) return;

            var gr = ctx.Recipe.GuaranteedResult;
            int count = ctx.Rng.Range(gr.MinCount, gr.MaxCount + 1);
            int quality = ctx.Rng.Range(gr.MinQuality, gr.MaxQuality + 1);

            ctx.Entries.Add(new CraftResultEntry
            {
                ItemId = gr.ItemId,
                Count = count,
                Quality = quality,
                Source = CraftRewardSource.Guaranteed.ToString(),
                IsCritical = false,
                FixedLevel = gr.FixedLevel,
                FixedEnhance = gr.FixedEnhance
            });
        }
    }

    /// <summary>
    /// Stage 5: Apply mastery bonuses.
    /// </summary>
    public class MasteryStage : CraftPipelineStageBase
    {
        private readonly CraftFormulasConfig _config;

        public MasteryStage(CraftFormulasConfig config = null)
        {
            _config = config ?? new CraftFormulasConfig();
        }

        public override string StageName => "MasteryBonus";
        public override int Order => 200;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;

            int masteryLevel = ctx.Context.GetMasteryLevel(ctx.Recipe.RecipeId);
            if (masteryLevel <= 0) return;

            int effectiveLevel = Mathf.Min(masteryLevel, _config.MasteryMaxBonusLevel);
            float bonusChance = effectiveLevel * _config.MasteryBonusChancePerLevel;

            if (ctx.Rng.ChancePercent(bonusChance))
            {
                // Grant extra item of same type as main result
                var mainResult = ctx.Recipe.PossibleResults?.FirstOrDefault(r => r.IsMainResult)
                              ?? ctx.Recipe.PossibleResults?.FirstOrDefault();
                if (mainResult != null)
                {
                    int quality = ctx.Rng.Range(mainResult.MinQuality, mainResult.MaxQuality + 1);
                    ctx.Entries.Add(new CraftResultEntry
                    {
                        ItemId = mainResult.ItemId,
                        Count = 1,
                        Quality = quality,
                        Source = CraftRewardSource.Mastery.ToString(),
                        IsCritical = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// Stage 6: Apply event/seasonal modifiers.
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
    /// Stage 7: Apply critical craft effects.
    /// Runs LAST so it can multiply ALL accumulated results (base + guaranteed + mastery + event).
    /// </summary>
    public class CriticalStage : CraftPipelineStageBase
    {
        private readonly CraftFormulasConfig _config;

        public CriticalStage(CraftFormulasConfig config = null)
        {
            _config = config ?? new CraftFormulasConfig();
        }

        public override string StageName => "CriticalCraft";
        public override int Order => 300;

        public override void Execute(CraftPipelineContext ctx)
        {
            if (!ctx.Success) return;

            float criticalChance = CalculateCriticalChance(ctx);
            criticalChance += ctx.CriticalChanceBonus;
            criticalChance *= ctx.CriticalChanceMultiplier;
            criticalChance = Mathf.Clamp(criticalChance, 0f, _config.MaxCriticalChance);

            if (!ctx.Rng.ChancePercent(criticalChance))
                return;

            // Critical triggered! Apply effect to ALL current entries
            var criticalType = DetermineCriticalType(ctx);
            ApplyCriticalEffect(ctx, criticalType);
        }

        private float CalculateCriticalChance(CraftPipelineContext ctx)
        {
            float chance = ctx.Context.BaseCriticalChance;
            chance += ctx.Context.CraftingLevel * _config.LevelToCriticalChance;
            chance += ctx.Context.Luck * _config.LuckToCriticalChance;
            return chance;
        }

        private CriticalType DetermineCriticalType(CraftPipelineContext ctx)
        {
            float roll = ctx.Rng.NextFloat();
            float cumulative = 0f;

            cumulative += ctx.Context.MasterpieceChance + _config.MasterpieceWeight;
            if (roll < cumulative) return CriticalType.Masterpiece;

            cumulative += ctx.Context.QualityBonusChance + _config.QualityBonusWeight;
            if (roll < cumulative) return CriticalType.BonusQuality;

            cumulative += ctx.Context.ExtraItemChance + _config.ExtraItemWeight;
            if (roll < cumulative) return CriticalType.FreeExtraItem;

            return CriticalType.DoubleResult;
        }

        private void ApplyCriticalEffect(CraftPipelineContext ctx, CriticalType type)
        {
            // Snapshot current entries to apply critical effect to ALL of them
            var currentEntries = new List<CraftResultEntry>(ctx.Entries);

            foreach (var entry in currentEntries)
            {
                CraftResultEntry criticalEntry = null;

                switch (type)
                {
                    case CriticalType.DoubleResult:
                        criticalEntry = new CraftResultEntry
                        {
                            ItemId = entry.ItemId,
                            Count = entry.Count,
                            Quality = entry.Quality,
                            Source = CraftRewardSource.Critical.ToString() + "_Double",
                            IsCritical = true,
                            FixedLevel = entry.FixedLevel,
                            FixedEnhance = entry.FixedEnhance,
                            SocketCount = entry.SocketCount
                        };
                        break;

                    case CriticalType.BonusQuality:
                        criticalEntry = new CraftResultEntry
                        {
                            ItemId = entry.ItemId,
                            Count = entry.Count,
                            Quality = Mathf.Min(entry.Quality + 1, _config.MaxQualityTier),
                            Source = CraftRewardSource.Critical.ToString() + "_Quality",
                            IsCritical = true,
                            FixedLevel = entry.FixedLevel,
                            FixedEnhance = entry.FixedEnhance,
                            SocketCount = entry.SocketCount
                        };
                        break;

                    case CriticalType.FreeExtraItem:
                        if (ctx.Recipe.PossibleResults != null && ctx.Recipe.PossibleResults.Length > 0)
                        {
                            var extraResult = ctx.Recipe.PossibleResults[ctx.Rng.Range(0, ctx.Recipe.PossibleResults.Length)];
                            criticalEntry = new CraftResultEntry
                            {
                                ItemId = extraResult.ItemId,
                                Count = ctx.Rng.Range(extraResult.MinCount, extraResult.MaxCount + 1),
                                Quality = ctx.Rng.Range(extraResult.MinQuality, extraResult.MaxQuality + 1),
                                Source = CraftRewardSource.Critical.ToString() + "_Extra",
                                IsCritical = true
                            };
                        }
                        break;

                    case CriticalType.Masterpiece:
                        criticalEntry = new CraftResultEntry
                        {
                            ItemId = entry.ItemId,
                            Count = entry.Count * 2,
                            Quality = _config.MaxQualityTier,
                            Source = CraftRewardSource.Critical.ToString() + "_Masterpiece",
                            IsCritical = true,
                            FixedLevel = entry.FixedLevel,
                            FixedEnhance = entry.FixedEnhance,
                            SocketCount = entry.SocketCount
                        };
                        break;
                }

                if (criticalEntry != null)
                {
                    ctx.Entries.Add(criticalEntry);
                }
            }
        }
    }

    /// <summary>
    /// Stage 8: Validate final results (quality bounds, count bounds, valid item IDs).
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

            if (_itemDatabase != null && !_itemDatabase.IsValidItemId(entry.ItemId))
            {
                Debug.LogWarning($"[CraftPipeline] ItemId not found in database: {entry.ItemId}");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Stage 9: Calculate final EXP reward.
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
            exp += (long)ctx.ExpBonus;

            ctx.ExpReward = Math.Max(0, exp);
        }
    }

    /// <summary>
    /// Stage 10: Finalize - apply failure behavior if needed.
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
            RegisterStage(new BaseRewardStage());
            RegisterStage(new GuaranteedStage());
            RegisterStage(new MasteryStage(_config));
            RegisterStage(new EventStage());
            RegisterStage(new CriticalStage(_config));
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
                if (!pipelineCtx.Success && stage.Order >= 10)
                {
                    break;
                }
            }

            return new CraftRollResult
            {
                Success = pipelineCtx.Success,
                FailureReason = pipelineCtx.FailureReason,
                Entries = pipelineCtx.Entries,
                ExpReward = pipelineCtx.ExpReward
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