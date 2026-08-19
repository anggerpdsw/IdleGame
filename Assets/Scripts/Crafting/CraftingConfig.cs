using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Crafting configuration loaded from Assets/Resources/Data/Crafting/dataConfigCrafting.json.
    /// Holds economic weights, rarity multipliers, profile baselines, water quantity reference table.
    ///</summary>
    [Serializable]
    public class CraftingConfig
    {
        // Custom JSON contract: enum keys deserialize from strings (e.g. "Stone" -> CraftingFamily.Stone)
        [JsonProperty(ItemConverterType = typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public int SchemaVersion = 1;
        public Dictionary<CraftingFamily, float> MaterialWeights = new();
        public float ProgressionWeight = 0f;
        public Dictionary<int, float> RarityMultipliers = new();
        public Dictionary<string, int> ProfileBaselines = new();
        public Dictionary<int, int> WaterBaselineTable = new();
        public Dictionary<int, EquipmentAttributeTierConfig> AttributeRolls = new();
        public float WaterTolerancePercent = 0.20f;
        public float ArmorMarginPercent = 0.20f;

        private static CraftingConfig _instance;
        private static bool _initialized = false;

        public static CraftingConfig Load()
        {
            if (_initialized) return _instance;

            var asset = Resources.Load<TextAsset>("Data/Crafting/dataConfigCrafting");
            if (asset == null)
            {
                Debug.LogError("[CraftingConfig] dataConfigCrafting.json not found in Resources/Data/Crafting/. Using empty defaults.");
                _instance = new CraftingConfig();
                _initialized = true;
                return _instance;
            }

            try
            {
                _instance = JsonConvert.DeserializeObject<CraftingConfig>(asset.text) ?? new CraftingConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CraftingConfig] Failed to deserialize: {e.Message}. Using empty defaults.");
                _instance = new CraftingConfig();
            }

            _initialized = true;
            return _instance;
        }

        /// <summary>
        /// Get economic weight for a crafting family.
        /// Returns 1.0 default for missing keys to avoid validator zero-cost bug.
        ///</summary>
        public float GetWeight(CraftingFamily family)
        {
            if (family == CraftingFamily.None) return ProgressionWeight;
            return MaterialWeights.TryGetValue(family, out float w) ? w : 1.0f;
        }

        /// <summary>
        /// Get rarity multiplier (1..6 rarity). Default 1.0 for missing.
        ///</summary>
        public float GetRarityMultiplier(int rarity)
        {
            return RarityMultipliers.TryGetValue(rarity, out float m) ? m : 1.0f;
        }

        /// <summary>
        /// Get profile baseline quantity (Small/Medium/Large/Heavy).
        ///</summary>
        public int GetProfileBaseline(string profile)
        {
            return ProfileBaselines.TryGetValue(profile, out int q) ? q : 4;
        }

        /// <summary>
        /// Get water quantity baseline for a rarity tier.
        /// </summary>
        public int GetWaterBaseline(int rarity)
        {
            return WaterBaselineTable.TryGetValue(rarity, out int q) ? q : 0;
        }

        /// <summary>
        /// Get attribute roll configuration for a rarity (1..6). v3.8 §20.3.
        /// Returns null when the rarity has no entry (data gap) — caller degrades safely.
        /// </summary>
        public EquipmentAttributeTierConfig GetAttributeTierConfig(int rarity)
        {
            return AttributeRolls.TryGetValue(rarity, out var cfg) ? cfg : null;
        }
    }

    /// <summary>
    /// Attribute roll tier configuration — how many times MainAttribute is rolled and
    /// the value range per roll, keyed by equipment rarity (1=Common..6=Divine).
    /// Loaded from dataConfigCrafting.json "AttributeRolls". v3.8 §20.3.
    /// </summary>
    [Serializable]
    public class EquipmentAttributeTierConfig
    {
        public int MaxRolls = 1;
        public int MinValue = 3;
        public int MaxValue = 6;
    }
}
