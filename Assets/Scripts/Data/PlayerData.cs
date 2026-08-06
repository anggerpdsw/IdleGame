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

    /// <summary>One stat bonus granted by an attribute. Parsed from dataAttribute.json.</summary>
    [Serializable]
    public class AttributeBonus
    {
        public string stat;          // SkillType name (AttackDamage, HealthPoint, ...)
        public float flat;           // +X per point (flat)
        public float percent;        // +X% per point (percent, 1 = +1%)
    }

    /// <summary>All bonuses one attribute grants. Parsed from dataAttribute.json.</summary>
    [Serializable]
    public class AttributeBonuses
    {
        public AttributeBonus[] constitution;
        public AttributeBonus[] strength;
        public AttributeBonus[] intelligence;
        public AttributeBonus[] dexterity;
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