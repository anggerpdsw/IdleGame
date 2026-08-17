using System.Collections.Generic;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Builds CraftContext from player progression data.
    /// Isolated so buff/stat source changes don't touch CraftService.
    /// </summary>
    public sealed class CraftContextBuilder
    {
        private readonly SaveManager _saveManager;

        public CraftContextBuilder(SaveManager saveManager)
        {
            _saveManager = saveManager;
        }

        /// <summary>
        /// Builds a fresh context for a craft job.
        /// </summary>
        public CraftContext Build()
        {
            return new CraftContext
            {
                PlayerStats = new PlayerCraftStats
                {
                    CraftingLevel = GetPlayerCraftingLevel(),
                    BlacksmithLevel = GetBlacksmithLevel(),
                    Luck = GetPlayerLuck()
                },
                Buffs = new CraftBuffContext
                {
                    SuccessRateBonus = GetSuccessRateBonus(),
                    CriticalChanceBonus = GetCriticalChanceBonus(),
                    ExpMultiplier = GetExpMultiplier()
                },
                Mastery = new CraftMasteryContext
                {
                    RecipeMasteryLevels = GetRecipeMasteryLevels()
                },
                Events = new CraftEventContext
                {
                    ActiveModifiers = ConvertToModifiers(GetActiveEventModifiers())
                }
            };
        }

        // ============ Player state sources ============

        private int GetPlayerCraftingLevel()
        {
            return _saveManager?.GetAccountData()?.craftingLevel ?? 1;
        }

        private int GetBlacksmithLevel()
        {
            // Building level that affects crafting
            return 0; // Simplified
        }

        private long GetPlayerLuck()
        {
            // From equipment, cards, buffs, etc.
            return 0; // Simplified
        }

        private float GetSuccessRateBonus()
        {
            // From equipment, buffs, cards, etc.
            return 0f;
        }

        private float GetCriticalChanceBonus()
        {
            // From equipment, mastery, buffs
            return 0f;
        }

        private float GetExpMultiplier()
        {
            // From VIP, events, buffs
            return 1f;
        }

        private Dictionary<string, int> GetRecipeMasteryLevels()
        {
            // From crafting mastery system
            return _saveManager?.GetAccountData()?.recipeMasteryLevels ?? new Dictionary<string, int>();
        }

        private List<EventCraftModifier> GetActiveEventModifiers()
        {
            // From event system
            return new List<EventCraftModifier>();
        }

        private static List<ICraftModifier> ConvertToModifiers(List<EventCraftModifier> legacyModifiers)
        {
            var modifiers = new List<ICraftModifier>();
            if (legacyModifiers == null) return modifiers;

            foreach (var legacy in legacyModifiers)
            {
                if (legacy.GrantExtraItem && !string.IsNullOrEmpty(legacy.ExtraItemId))
                {
                    modifiers.Add(new ExtraItemModifier
                    {
                        ItemId = legacy.ExtraItemId,
                        MinCount = legacy.ExtraItemCount,
                        MaxCount = legacy.ExtraItemCount,
                        MinQuality = legacy.ExtraItemQuality,
                        MaxQuality = legacy.ExtraItemQuality,
                        ApplicableRecipeIds = legacy.ApplicableRecipeIds,
                        ApplicableCategories = legacy.ApplicableCategories
                    });
                }
            }
            return modifiers;
        }
    }
}
