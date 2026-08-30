using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using Newtonsoft.Json;
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
            string key = $"{typeof(T).FullName}:{path}";
            if (Cache.TryGetValue(key, out var asset))
                return asset as T;
            T loaded = Resources.Load<T>(path);
            if (loaded != null) Cache[key] = loaded;
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
            // Equipment/Hat/leather_hat
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
        public static Sprite GetUltimateSource(string ultimateID)
            => ResourceCache.Load<Sprite>($"Art/Player/{ultimateID}");
    }
    
    public static class RewardResources
    {
        public static Sprite GetRewardType(string type)
            => ResourceCache.Load<Sprite>($"Art/Item/{type}");
    }

    public static class EnemyResources
    {
        public static Sprite GetEnemySprite(string id)
        {
            // First: individual enemy sprites
            Sprite sprite = ResourceCache.Load<Sprite>($"Art/Enemy/{id}");
            if (sprite != null) return sprite;
            // Fallback: Monsterpack spritesheet
            return ResourceCache.LoadSpriteFromSheet("Art/Enemy/Monsterpack", id);
        }

        public static GameObject GetEnemyPrefab(string prefabName)
        {
            return ResourceCache.Load<GameObject>($"Enemies/{prefabName}");
        }

    }

    public static class DatabaseJSONCache
    {
        private const string DATA_ENEMY = "Data/dataEnemy";
        private static EnemyDatabase _databaseEnemy;
        public static EnemyDatabase DatabaseEnemy
        { get { if (_databaseEnemy == null) LoadEnemy(); return _databaseEnemy; }}
        private static void LoadEnemy()
        {
            TextAsset jsonFile = ResourceCache.Load<TextAsset>(DATA_ENEMY);
            if (jsonFile == null)
            {
                Debug.LogError($"Failed to load Resources/{DATA_ENEMY}.json");
                return;
            }
            var database = JsonConvert.DeserializeObject<EnemyDatabase>(jsonFile.text);
            if (database == null || database.enemies == null || database.enemies.Length == 0)
            {
                Debug.LogError($"Enemy database in {DATA_ENEMY} is empty or invalid.");
                return;
            }
            _databaseEnemy = database;
        }

        private const string DATA_MAIN_ATTRIBUTE = "Data/Player/dataMainAttribute";
        private static AttributeConfig _databaseMainAttributeConfig;
        public static AttributeConfig DatabaseMainAttributeConfig
        { get { if (_databaseMainAttributeConfig == null) LoadMainAttribute(); return _databaseMainAttributeConfig; }}
        private static Dictionary<string, List<AttributeBonusEntry>> _databaseSecondaryStatAttribute;
        public static Dictionary<string, List<AttributeBonusEntry>> DatabaseSecondaryStatAttribute
        { get { if (_databaseSecondaryStatAttribute == null) LoadMainAttribute(); return _databaseSecondaryStatAttribute; }}
        private static void LoadMainAttribute()
        {
            TextAsset jsonFile = ResourceCache.Load<TextAsset>(DATA_MAIN_ATTRIBUTE);
            if (jsonFile == null)
            {
                Debug.LogError($"Failed to load Resources/{DATA_MAIN_ATTRIBUTE}.json");
                return;
            }
            try
            {
                var dataMain = JsonConvert.DeserializeObject<AttributeConfig>(jsonFile.text);
                if (dataMain == null)
                {
                    Debug.LogError($"Attribute database in {DATA_MAIN_ATTRIBUTE}.json is empty or invalid.");
                    return;
                }
                var dataSecondary = JsonConvert.DeserializeObject<Dictionary<string, List<AttributeBonusEntry>>>(jsonFile.text);
                if (dataSecondary == null)
                {
                    Debug.LogError($"Attribute bonus database in {DATA_MAIN_ATTRIBUTE}.json is empty or invalid.");
                    return;
                }
                _databaseMainAttributeConfig = dataMain;
                _databaseSecondaryStatAttribute = dataSecondary;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"Failed to deserialize attribute data from " +
                    $"Resources/{DATA_MAIN_ATTRIBUTE}.json: {ex.Message}");
            }
        }

        private const string DATA_ULTIMATE = "Data/Player/dataUltimate";
        private static UltimateDatabase _databaseUltimate;
        public static UltimateDatabase DatabaseUltimate
        { get { if (_databaseUltimate == null) LoadUltimate(); return _databaseUltimate; }}
        private static void LoadUltimate()
        {
            TextAsset jsonFile = ResourceCache.Load<TextAsset>(DATA_ULTIMATE);
            if (jsonFile == null)
            {
                Debug.LogError($"Failed to load Resources/{DATA_ULTIMATE}.json");
                return;
            }
            var database = JsonConvert.DeserializeObject<UltimateDatabase>(jsonFile.text);
            if (database == null || database.ultimate == null || database.ultimate.Count == 0)
            {
                Debug.LogError($"Ultimate database in {DATA_ULTIMATE} is empty or invalid.");
                return;
            }
            _databaseUltimate = database;
        }
        
        public static void ClearAll()
        {
            _databaseEnemy = null;
            _databaseMainAttributeConfig = null;
            _databaseSecondaryStatAttribute = null;
            _databaseUltimate = null;
        }
    }
    

    
}