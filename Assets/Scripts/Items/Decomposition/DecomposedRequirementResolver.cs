using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Items.Decomposition
{
    /// <summary>
    /// Pure C# resolver for decomposed material requirements by target equipment rarity.
    /// Implements v3.2 §5.1 mapping. No Unity dependency, no IO.
    ///</summary>
    public static class DecomposedRequirementResolver
    {
        /// <summary>
        /// Compute per-unit decomposed requirements for a target rarity.
        /// R1 returns empty (no gate). R6 uses decomposed_mythic (NOT decomposed_divine).
        ///</summary>
        /// <param name="rarity">Equipment rarity, 1..6</param>
        /// <returns>Per-unit requirements. Never null</returns>
        public static IReadOnlyList<DecomposedRequirement> Compute(int rarity)
        {
            switch (rarity)
            {
                case 1:
                    return Array.Empty<DecomposedRequirement>();

                case 2:
                    return new[]
                    {
                        new DecomposedRequirement("decomposed_common", 1)
                    };

                case 3:
                    return new[]
                    {
                        new DecomposedRequirement("decomposed_common", 2),
                        new DecomposedRequirement("decomposed_rare", 1)
                    };

                case 4:
                    return new[]
                    {
                        new DecomposedRequirement("decomposed_common", 3),
                        new DecomposedRequirement("decomposed_rare", 2),
                        new DecomposedRequirement("decomposed_epic", 1)
                    };

                case 5:
                    return new[]
                    {
                        new DecomposedRequirement("decomposed_common", 4),
                        new DecomposedRequirement("decomposed_rare", 3),
                        new DecomposedRequirement("decomposed_epic", 2),
                        new DecomposedRequirement("decomposed_legendary", 1)
                    };

                case 6:
                    return new[]
                    {
                        new DecomposedRequirement("decomposed_common", 5),
                        new DecomposedRequirement("decomposed_rare", 4),
                        new DecomposedRequirement("decomposed_epic", 3),
                        new DecomposedRequirement("decomposed_legendary", 2),
                        new DecomposedRequirement("decomposed_mythic", 1)
                    };

                default:
                    return Array.Empty<DecomposedRequirement>();
            }
        }
    }
}
