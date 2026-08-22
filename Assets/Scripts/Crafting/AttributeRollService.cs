using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Rolls the four main attributes (CON/STR/INT/DEX) on crafted equipment (v3.8 §20).
    /// Roll count and value range come from CraftingConfig "AttributeRolls" keyed by rarity
    /// (recipe.Rarity is the source of truth — no random rarity here).
    /// Roll count = number of rolls, NOT number of unique attributes: duplicates are allowed
    /// on every rarity and aggregated into a single entry per attribute.
    /// This service is pure generation — it touches no inventory or save state.
    ///</summary>
    public sealed class AttributeRollService
    {
        private readonly IRandomProvider _rng;

        public AttributeRollService(IRandomProvider rng)
        {
            _rng = rng ?? new UnityRandomProvider();
        }

        /// <summary>
        /// Rolls attributes for the given rarity. Returns an entry per aggregated
        /// attribute (no duplicates); empty when the rarity has no tier config.
        ///</summary>
        public AttributeStatEntry[] RollAttributes(Rarity rarity, EquipmentAttributeTierConfig tierConfig)
        {
            if (tierConfig == null || tierConfig.MaxRolls <= 0)
                return Array.Empty<AttributeStatEntry>();

            var attributeValues = new Dictionary<MainAttribute, int>();

            for (int i = 0; i < tierConfig.MaxRolls; i++)
            {
                var attribute = _rng.Choice(AttributeChoices);
                int value = _rng.Range(tierConfig.MinValue, tierConfig.MaxValue + 1);

                if (attributeValues.TryGetValue(attribute, out int existing))
                    attributeValues[attribute] = existing + value;
                else
                    attributeValues[attribute] = value;
            }

            var entries = new AttributeStatEntry[attributeValues.Count];
            int index = 0;
            foreach (var kvp in attributeValues)
            {
                entries[index++] = new AttributeStatEntry
                {
                    Attribute = kvp.Key,
                    BaseValue = kvp.Value,
                    ValuePerLevel = 0f
                };
            }
            return entries;
        }

        private static readonly MainAttribute[] AttributeChoices =
        {
            MainAttribute.Constitution,
            MainAttribute.Strength,
            MainAttribute.Intelligence,
            MainAttribute.Dexterity
        };
    }
}
