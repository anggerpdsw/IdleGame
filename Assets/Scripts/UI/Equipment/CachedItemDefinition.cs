using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Per-itemId cache of the visual fields used by UI.
    /// Avoids ItemDatabase.GetItem() lookups on every refresh.
    /// </summary>
    public sealed class CachedItemDefinition
    {
        public readonly Sprite Icon;
        public readonly ItemRarity Rarity;

        private CachedItemDefinition(ItemData data)
        {
            Icon = data?.Icon;
            Rarity = data?.ItemRarity ?? ItemRarity.None;
        }

        private static readonly Dictionary<string, CachedItemDefinition> Cache = new();

        /// <summary>Gets (or builds once) the cached definition for an itemId.</summary>
        public static CachedItemDefinition Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (!Cache.TryGetValue(itemId, out var def))
            {
                def = new CachedItemDefinition(ItemDatabase.Instance?.GetItem(itemId));
                Cache[itemId] = def;
            }
            return def;
        }
    }
}