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
            if (loaded != null) Cache[path] = loaded;
            return loaded;
        }

        public static Sprite LoadSpriteFromSheet(string sheetPath, string spriteName)
        {
            string key = $"{sheetPath}:{spriteName}";
            if (SpriteCache.TryGetValue(key, out var sprite)) return sprite;
            Sprite[] sprites = Resources.LoadAll<Sprite>(sheetPath);
            foreach (var s in sprites)
                SpriteCache[$"{sheetPath}:{s.name}"] = s;
            SpriteCache.TryGetValue(key, out sprite);
            return sprite;
        }

        public static void Clear()
        {
            Cache.Clear();
            SpriteCache.Clear();
        }
    }

    public static class ItemResources
    {
        public static Sprite GetItemSource(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string[] parts = key.Split('/');
            // Single sprite → Art/Item/UltimateStone
            if (parts.Length == 1)
                return ResourceCache.Load<Sprite>($"Art/Item/{parts[0]}");

            // Sprite sheet:
            // Potion/hp
            // Potion/Potion/hp
            // Equipment/Hat/hat_leather
            // Equipment/Armor/Heavy/armor_iron
            //
            // Last part = sprite name
            // Everything before it = sheet path
            string spriteName = parts[^1];
            string sheetPath = string.Join("/", parts[..^1]);
            return ResourceCache.LoadSpriteFromSheet($"Art/Item/{sheetPath}", spriteName);
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