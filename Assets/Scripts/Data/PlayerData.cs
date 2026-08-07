using System;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class PlayerData
    {
        public MainAttributes mainAttributes;
        public PlayerSkills skills;
    }

    /// <summary>
    /// Base (level-1) values of the four main attributes.
    /// Player-allocated points are stored per-account in <see cref="AccountData"/>.
    /// </summary>
    [Serializable]
    public class MainAttributes
    {
        public float constitution;
        public float strength;
        public float intelligence;
        public float dexterity;
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
        public SkillData criticalDamage;
        public SkillData damagePerRange;
        public SkillData knockbackChance;
        public SkillData knockbackForce;
        public SkillData multiShootChance;
        public SkillData multiShootCount;
        public SkillData stuntChance;
        public SkillData stuntDuration;
        public SkillData superCriticalChance;
        public SkillData superCriticalFactor;
        public SkillData ultimateAttack;
        public SkillData skillDamage;
        public SkillData elementDamage;
        public SkillData ultraCriticalChance;
        public SkillData ultraCriticalFactor;
    }

    [Serializable]
    public class DefenseGroup
    {
        public SkillData deathDefy;
        public SkillData defenseAmount;
        public SkillData evasion;
        public SkillData hitRate;
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
    /// A single player skill. Has a fixed base value (no levels).
    /// Later influenced by Constitution/Strength/Intelligence/Dexterity stat modifiers.
    /// </summary>
    [Serializable]
    public class SkillData
    {
        public float baseValue;
        public string description;
        public string displayName;
    }
}