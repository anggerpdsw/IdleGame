using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class PlayerData
    {
        public PlayerSkills skills;
    }

    [Serializable]
    public class PlayerSkills
    {
        public AttackGroup attack;
        public DefenseGroup defense;
        public CurrencyGroup currency;
    }

    [Serializable]
    public class AttackGroup
    {
        public SkillData attackDamage;
        public SkillData attackSpeed;
        public SkillData attackRange;
        public SkillData bounceChance;
        public SkillData bounceCount;
        public SkillData criticalChance;
        public SkillData criticalFactor;
        public SkillData bounceSearchRadius;
        public SkillData damagePerRange;
        public SkillData knockbackChance;
        public SkillData knockbackForce;
        public SkillData multiShootChance;
        public SkillData multiShootCount;
        public SkillData stuntChance;
        public SkillData stuntDuration;
        public SkillData superCriticalChance;
        public SkillData superCriticalFactor;
        public SkillData ultimateWeaponAttack;
        public SkillData ultraCriticalChance;
        public SkillData ultraCriticalFactor;
    }

    [Serializable]
    public class DefenseGroup
    {
        public SkillData deathDefy;
        public SkillData defenseAmount;
        public SkillData evasionChance;
        public SkillData healthPoint;
        public SkillData healthRegen;
        public SkillData lifeSteal;
    }

    [Serializable]
    public class CurrencyGroup
    {
        public SkillData interestWave;
    }

    /// <summary>
    /// Defines the cost structure for upgrading a skill.
    /// Supports multiple currencies with different scaling formulas.
    /// Lives in Data namespace so SkillData can reference it directly.
    /// </summary>
    [Serializable]
    public class UpgradeCost
    {
        public CurrencyType currencyType = CurrencyType.Gold;
        [Tooltip("Base cost at level 1")]
        public long baseCost = 100;
        [Tooltip("Exponential growth factor (1.15 = 15% increase per level)")]
        public float growthFactor = 1.15f;
        [Tooltip("If true, cost rounds to nearest 10/100/1000 for cleaner UI")]
        public bool roundToNearest = true;

        public long CalculateCost(int currentLevel)
        {
            if (currentLevel < 0) return 0;

            float rawCost = baseCost * Mathf.Pow(growthFactor, currentLevel);

            if (roundToNearest)
            {
                if (rawCost < 100)
                    return Mathf.RoundToInt(rawCost / 10f) * 10;
                if (rawCost < 1000)
                    return Mathf.RoundToInt(rawCost / 50f) * 50;
                if (rawCost < 10000)
                    return Mathf.RoundToInt(rawCost / 100f) * 100;
                return Mathf.RoundToInt(rawCost / 1000f) * 1000;
            }

            return Mathf.RoundToInt(rawCost);
        }
    }

    [Serializable]
    public class SkillData
    {
        public int level;
        public int maxLevel;
        public float min;
        public float max;
        public bool isFloat = true;
        public bool locked;
        public string description;
        public string displayName;

        // ── Upgrade cost configuration (merged from dataUpgradeCosts.json) ──
        [Tooltip("Currency costs for upgrading this skill")]
        public List<UpgradeCost> costs = new();
        [Tooltip("Gem cost to unlock this skill. 0 = already unlocked")]
        public long unlockCost;
        [Tooltip("Currency type for unlock cost")]
        public CurrencyType unlockCurrency = CurrencyType.Gem;

        // ── Computed helpers ──
        public bool RequiresUnlock => unlockCost > 0;

        public Dictionary<CurrencyType, long> GetUpgradeCost(int currentLevel)
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
    }
}