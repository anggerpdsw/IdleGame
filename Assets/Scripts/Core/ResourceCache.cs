using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Global cache for Resources assets.
    /// Loads each asset only once.
    /// </summary>
    public static class ResourceCache
    {
        private static readonly Dictionary<string, Object> Cache = new();
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static T Load<T>(string path) where T : Object
        {
            if (Cache.TryGetValue(path, out var asset))
                return asset as T;

            T loaded = Resources.Load<T>(path);

            if (loaded != null)
                Cache[path] = loaded;

            return loaded;
        }

        public static Sprite LoadSpriteFromSheet(string sheetPath, string spriteName)
        {
            string key = $"{sheetPath}:{spriteName}";

            if (SpriteCache.TryGetValue(key, out var sprite))
                return sprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(sheetPath);

            foreach (var s in sprites)
            {
                SpriteCache[$"{sheetPath}:{s.name}"] = s;
            }

            SpriteCache.TryGetValue(key, out sprite);
            return sprite;
        }

        public static void Clear()
        {
            Cache.Clear();
            SpriteCache.Clear();
        }
    }

    public static class ButtonResources
    {
        public static Sprite GetColor(string color)
            => ResourceCache.LoadSpriteFromSheet($"Art/Button/Color", color);
    }

    public static class CardResources
    {
        public static Sprite GetFrame(string rarity)
            => ResourceCache.LoadSpriteFromSheet($"Art/Card/Frame/CardFrame", rarity);

        public static Sprite GetIcon(string id)
            => ResourceCache.Load<Sprite>($"Art/Card/Icon/{id}");
    }
    
    public static class ItemResources
    {
        /// <summary>
        /// Resolves an item icon. IconKey formats:
        ///   "UltimateStone"            → single sprite Resources/Art/Item/UltimateStone
        ///   "UltimateStone/None"       → sprite named "None" from sheet Resources/Art/Item/UltimateStone
        /// </summary>
        public static Sprite GetItemSource(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            int sep = key.IndexOf('/');
            if (sep < 0)
                return ResourceCache.Load<Sprite>($"Art/Item/{key}");

            string sheet = key[..sep];
            string spriteName = key[(sep + 1)..];
            return ResourceCache.LoadSpriteFromSheet($"Art/Item/{sheet}", spriteName);
        }
    }
    
    public static class PlayerResources
    {
        public static Sprite GetDamageSource(string id)
            => ResourceCache.Load<Sprite>($"Art/Player/{id}");
    }
    
    public static class RewardResources
    {
        public static Sprite GetRewardType(string type)
            => ResourceCache.Load<Sprite>($"Art/Item/{type}");
    }
}