using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Upgrade
{
    /// <summary>
    /// Configuration for a single skill's upgrade costs.
    /// </summary>
    [Serializable]
    public class SkillUpgradeConfig
    {
        public string skillId;
        [Tooltip("Display name for UI")]
        public string displayName;
        [Tooltip("Costs can require multiple currencies")]
        public List<UpgradeCost> costs = new();
        [Tooltip("Cost to unlock the skill (if locked). Uses Gem by default.")]
        public long unlockCost = 0;
        [Tooltip("Currency type for unlock cost. Defaults to Gem.")]
        public CurrencyType unlockCurrency = CurrencyType.Gem;

        public Dictionary<CurrencyType, long> GetTotalCost(int currentLevel)
        {
            var result = new Dictionary<CurrencyType, long>();
            foreach (var cost in costs)
            {
                if (result.ContainsKey(cost.currencyType))
                    result[cost.currencyType] += cost.CalculateCost(currentLevel);
                else
                    result[cost.currencyType] = cost.CalculateCost(currentLevel);
            }
            return result;
        }

        public Dictionary<CurrencyType, long> GetUnlockCost()
        {
            if (unlockCost <= 0) return null;
            return new Dictionary<CurrencyType, long> { { unlockCurrency, unlockCost } };
        }

        public bool RequiresUnlock => unlockCost > 0;
    }

    /// <summary>
    /// Centralized calculator for all skill upgrade costs.
    /// Loads everything from dataPlayer.json (which now contains cost and unlock data).
    /// </summary>
    public static class UpgradeCostCalculator
    {
        private static Dictionary<string, SkillUpgradeConfig> _configs;
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the calculator by loading skill data (including costs) from dataPlayer.json.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _configs = new Dictionary<string, SkillUpgradeConfig>();

            TextAsset playerJson = Resources.Load<TextAsset>("Data/dataPlayer");
            if (playerJson == null)
            {
                Debug.LogWarning("[UpgradeCostCalculator] dataPlayer.json not found, no skills registered.");
                _initialized = true;
                return;
            }

            try
            {
                var playerData = JsonConvert.DeserializeObject<PlayerData>(playerJson.text);
                if (playerData?.skills == null) return;

                // Reflect over grouped skill objects
                foreach (var groupField in playerData.skills.GetType().GetFields())
                {
                    var group = groupField.GetValue(playerData.skills);
                    if (group == null) continue;

                    foreach (var skillField in group.GetType().GetFields())
                    {
                        if (skillField.FieldType != typeof(SkillData)) continue;
                        var skillData = (SkillData)skillField.GetValue(group);
                        var config = new SkillUpgradeConfig
                        {
                            skillId = skillField.Name,
                            displayName = string.IsNullOrEmpty(skillData.displayName) ? skillField.Name : skillData.displayName,
                            costs = skillData.costs,
                            unlockCost = skillData.unlockCost,
                            unlockCurrency = skillData.unlockCurrency
                        };
                        _configs[skillField.Name] = config;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UpgradeCostCalculator] Failed to parse dataPlayer.json: {e.Message}");
            }

            _initialized = true;
        }

        public static SkillUpgradeConfig GetConfig(string skillId)
        {
            if (!_initialized) Initialize();
            return _configs.TryGetValue(skillId, out var cfg) ? cfg : null;
        }

        public static Dictionary<CurrencyType, long> CalculateUpgradeCost(string skillId, int currentLevel)
        {
            if (!_initialized) Initialize();
            if (!_configs.TryGetValue(skillId, out var cfg))
            {
                Debug.LogWarning($"[UpgradeCostCalculator] No config found for skill: {skillId}");
                return null;
            }
            return cfg.GetTotalCost(currentLevel);
        }

        public static Dictionary<CurrencyType, long> CalculateUnlockCost(string skillId)
        {
            if (!_initialized) Initialize();
            if (!_configs.TryGetValue(skillId, out var cfg))
            {
                Debug.LogWarning($"[UpgradeCostCalculator] No config found for skill: {skillId}");
                return null;
            }
            return cfg.GetUnlockCost();
        }

        public static bool RequiresUnlock(string skillId)
        {
            if (!_initialized) Initialize();
            return _configs.TryGetValue(skillId, out var cfg) && cfg.RequiresUnlock;
        }

        public static bool CanAffordUnlock(string skillId)
        {
            var costs = CalculateUnlockCost(skillId);
            if (costs == null || costs.Count == 0) return true;
            var economy = Economy.EconomyManager.Instance;
            if (economy == null) return false;
            foreach (var kvp in costs)
                if (!economy.HasEnoughCurrency(kvp.Key, kvp.Value)) return false;
            return true;
        }

        public static bool CanAfford(string skillId, int currentLevel)
        {
            var costs = CalculateUpgradeCost(skillId, currentLevel);
            if (costs == null) return false;
            var economy = Economy.EconomyManager.Instance;
            if (economy == null) return false;
            foreach (var kvp in costs)
                if (!economy.HasEnoughCurrency(kvp.Key, kvp.Value)) return false;
            return true;
        }

        public static string GetCostString(string skillId, int currentLevel)
        {
            var costs = CalculateUpgradeCost(skillId, currentLevel);
            if (costs == null || costs.Count == 0) return "Free";
            var parts = new List<string>();
            foreach (var kvp in costs)
                parts.Add($"{Utilityku.FormatNumber(kvp.Value)} {kvp.Key}");
            return string.Join(", ", parts);
        }

    }
}

