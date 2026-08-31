using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Display helpers for SkillType (player runtime stats).
    /// Lives in this same file so it is always compiled alongside the enum;
    /// its namespace (IdleDefenseSurvival) is an enclosing namespace of every caller.
    /// </summary>
    public static class SkillTypeExtensions
    {
        public static string GetSkillDisplayName(this SkillType stat)
        {
            // Try to get displayName from dataPlayer.json via BaseStatLoader (single source of truth)
            var loader = BaseStatLoader.Instance;
            if (loader != null)
            {
                var skillData = loader.GetSkillData(stat);
                if (skillData != null && !string.IsNullOrEmpty(skillData.displayName))
                    return skillData.displayName;
            }
            // Fallback to hardcoded names for any missing entries
            return "???!";
        }
    
        /// <summary>
        /// Gets the short display name for compact UI.
        /// </summary>
        public static string GetSkillShortName(this SkillType stat)
        {
            // Try to get shortName from dataPlayer.json via BaseStatLoader (single source of truth)
            var loader = BaseStatLoader.Instance;
            if (loader != null)
            {
                var skillData = loader.GetSkillData(stat);
                if (skillData != null && !string.IsNullOrEmpty(skillData.shortName))
                    return skillData.shortName;
            }
            // Fallback to hardcoded names for any missing entries
            return "???!";
        }
    }

    /// <summary>
    /// Basic skills Player - all combat runtime stats.
    /// </summary>
    public enum SkillType
    {
        None,

        // Physical
        AttackDamage = 1,
        AttackSpeed = 2,
        AttackRange = 3,
        CriticalChance = 4,
        CriticalDamage = 5,
        DamagePerRange = 6,
        BounceChance = 7,
        BounceCount = 8,
        DefenseBreak = 9,
        MultiShootChance = 10,
        MultiShootCount = 11,
        KnockbackChance = 12,
        KnockbackForce = 13,
        StuntChance = 14,
        StuntDuration = 15,

        // Survival
        HealthPoint = 16,
        HealthRegen = 17,
        DefenseAmount = 18,
        LifeSteal = 19,
        DeathDefy = 20,
        Evasion = 21,

        // Magic
        /// <summary>Universal elemental power (final = 1 + ElementMastery/1000), boosted by Intelligence.</summary>
        ElementMastery = 22,
        UltimateAttack = 23,
        /// <summary>Maximum mana pool. Ultimates and mana-cost skills draw from this.</summary>
        ManaPoint = 24,
        /// <summary>Mana regenerated per second.</summary>
        ManaRegen = 25,

        // Element damage (Layer 3) — per-element bonus (percent, from equipment/card/buff)
        MetalDamageBonus = 26,
        WoodDamageBonus = 27,
        FireDamageBonus = 28,
        WaterDamageBonus = 29,
        EarthDamageBonus = 30,
        LightningDamageBonus = 31,
        WindDamageBonus = 32,

        // Economy
        InterestWave = 33,
        GoldGain = 34,
        DropRate = 35,

        // Utility
        MoveSpeed = 36,
        CooldownReduction = 37,
        BossDamage = 38,
        EliteDamage = 39,

        // Accuracy (specialization — from equipment/passive/buff/card, NOT main attributes)
        HitRate = 40,
        Penetration = 41,

    }


}