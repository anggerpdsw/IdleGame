using System.Collections.Generic;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Crafting
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
                    BlacksmithLevel = GetPlayerBlacksmithLevel(),
                    CraftingLevel = GetPlayerCraftingLevel(),
                },
                Buffs = new CraftBuffContext
                {
                    ExpMultiplier = GetExpMultiplier()
                },
                Events = new CraftEventContext
                {
                    ActiveModifiers = ConvertToModifiers(GetActiveEventModifiers())
                }
            };
        }

        // ============ Player state sources ============
        private int GetPlayerBlacksmithLevel()
        {
            return _saveManager?.GetAccountData()?.craftingLevel ?? 1;
        }

        private int GetPlayerCraftingLevel()
        {
            return 0; // Simplified
        }

        private float GetExpMultiplier()
        {
            // From VIP, events, buffs
            return 1f;
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
                        ApplicableRecipeIds = legacy.ApplicableRecipeIds,
                        ApplicableCategories = legacy.ApplicableCategories
                    });
                }
            }
            return modifiers;
        }
    }
}
