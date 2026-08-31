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

    /// <summary>
    /// Root of dataAttribute.json — per-point bonus of each main attribute.
    /// </summary>
    [Serializable]
    public class AttributeConfig
    {
        public AttributeBonusEntry[] constitution;
        public AttributeBonusEntry[] strength;
        public AttributeBonusEntry[] intelligence;
        public AttributeBonusEntry[] dexterity;
    }

    /// <summary>
    /// One per-point bonus entry: stat name (parsed to <see cref="SkillType"/>), flat and percent value.
    /// </summary>
    [Serializable]
    public class AttributeBonusEntry
    {
        public string stat;
        public float flat;
        public float percent;
    }

    [Serializable]
    public class PlayerSkills
    {
        public AttackGroup attack;
        public DefenseGroup defense;
        public ElementGroup element;
        public EconomyGroup economy;
        public UtilityGroup utility;
    }

    [Serializable]
    public class AttackGroup
    {
        public SkillData attackDamage;
        public SkillData attackSpeed;
        public SkillData attackRange;
        public SkillData criticalChance;
        public SkillData criticalDamage;
        public SkillData damagePerRange;
        public SkillData bounceChance;
        public SkillData bounceCount;
        public SkillData defenseBreak;
        public SkillData multiShootChance;
        public SkillData multiShootCount;
        public SkillData knockbackChance;
        public SkillData knockbackForce;
        public SkillData stuntChance;
        public SkillData stuntDuration;
        public SkillData ultimateAttack;
    }

    [Serializable]
    public class DefenseGroup
    {
        public SkillData healthPoint;
        public SkillData healthRegen;
        public SkillData defenseAmount;
        public SkillData lifeSteal;
        public SkillData deathDefy;
        public SkillData evasion;
        public SkillData hitRate;
        public SkillData penetration;
        public SkillData manaPoint;
        public SkillData manaRegen;
        public SkillData moveSpeed;
    }

    [Serializable]
    public class ElementGroup
    {
        public SkillData elementMastery;
        public SkillData metalDamageBonus;
        public SkillData woodDamageBonus;
        public SkillData fireDamageBonus;
        public SkillData waterDamageBonus;
        public SkillData earthDamageBonus;
        public SkillData lightningDamageBonus;
        public SkillData windDamageBonus;
    }

    [Serializable]
    public class EconomyGroup
    {
        public SkillData interestWave;
        public SkillData goldGain;
        public SkillData dropRate;
    }

    [Serializable]
    public class UtilityGroup
    {
        public SkillData cooldownReduction;
        public SkillData bossDamage;
        public SkillData eliteDamage;
    }

    /// <summary>
    /// A single player skill. Has a fixed base value (no levels).
    /// Later influenced by Constitution/Strength/Intelligence/Dexterity stat modifiers.
    /// </summary>
    [Serializable]
    public class SkillData
    {
        public float baseValue;
        public int Mode;
        public float ValuePerLevel;
        public float ValuePerEnhance;
        public string description;
        public string displayName;
        public string shortName;
    }
}