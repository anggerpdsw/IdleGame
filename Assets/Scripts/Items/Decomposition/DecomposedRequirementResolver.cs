using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Items.Decomposition
{
    /// <summary>
    /// Pure C# resolver for decomposed material logic:
    /// 1. ComputeCraftRequirements: materials needed to craft equipment of target rarity.
    /// 2. GetDecomposeOutput: material produced from decomposing equipment of given rarity.
    /// No Unity dependency, no IO.
    ///</summary>
    public static class DecomposedRequirementResolver
    {
        /// <summary>
        /// Get decompose output material for equipment of given rarity.
        /// Decompose: equipment rarity X → decomposed_X material ×1.
        ///</summary>
        /// <param name="rarity">Equipment rarity, 1..6</param>
        /// <returns>Material ItemId, or null if invalid rarity</returns>
        public static string GetDecomposeOutput(int rarity)
        {
            return rarity switch
            {
                1 => "decomposed_common",
                2 => "decomposed_rare",
                3 => "decomposed_epic",
                4 => "decomposed_legendary",
                5 => "decomposed_mythic",
                6 => "decomposed_divine",
                _ => null
            };
        }

        /// <summary>
        /// Compute per-unit decomposed requirements for crafting equipment of target rarity.
        /// R1 (common) = no gate. R2 (rare) needs rarity-1. R6 (divine) needs up to mythic (no decomposed_divine).
        /// Pattern: craft rarity N needs decomposed materials from rarity 1..(N-1).
        ///</summary>
        /// <param name="rarity">Equipment rarity, 1..6</param>
        /// <returns>Per-unit requirements. Never null</returns>
        public static IReadOnlyList<DecomposedRequirement> ComputeCraftRequirements(int rarity)
        {
            return rarity switch
            {
                // Common: no decomposed gate
                1 => Array.Empty<DecomposedRequirement>(),

                // Rare: common×1
                2 => new[]
                    {
                        new DecomposedRequirement("decomposed_common", 1)
                    },

                // Epic: common×2 + rare×1
                3 => new[]
                    {
                        new DecomposedRequirement("decomposed_common", 2),
                        new DecomposedRequirement("decomposed_rare", 1)
                    },

                // Legendary: common×3 + rare×2 + epic×1
                4 => new[]
                    {
                        new DecomposedRequirement("decomposed_common", 3),
                        new DecomposedRequirement("decomposed_rare", 2),
                        new DecomposedRequirement("decomposed_epic", 1)
                    },

                // Mythic: common×4 + rare×3 + epic×2 + legendary×1
                5 => new[]
                    {
                        new DecomposedRequirement("decomposed_common", 4),
                        new DecomposedRequirement("decomposed_rare", 3),
                        new DecomposedRequirement("decomposed_epic", 2),
                        new DecomposedRequirement("decomposed_legendary", 1)
                    },

                // Divine: common×5 + rare×3 + epic×3 + legendary×2 + mythic×1
                6 => new[]
                    {
                        new DecomposedRequirement("decomposed_common", 5),
                        new DecomposedRequirement("decomposed_rare", 3),
                        new DecomposedRequirement("decomposed_epic", 3),
                        new DecomposedRequirement("decomposed_legendary", 2),
                        new DecomposedRequirement("decomposed_mythic", 1)
                    },

                _ => Array.Empty<DecomposedRequirement>(),
            };
        }
    }
}
