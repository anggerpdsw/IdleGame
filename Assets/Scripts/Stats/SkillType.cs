namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Display helpers for SkillType (player runtime stats).
    /// Lives in this same file so it is always compiled alongside the enum;
    /// its namespace (IdleDefenseSurvival) is an enclosing namespace of every caller.
    /// </summary>
    public static class SkillTypeExtensions
    {
        public static string GetSkillDisplayName(this SkillType stat) => stat switch
        {
            SkillType.AttackDamage => "Attack Damage",
            SkillType.AttackRange => "Attack Range",
            SkillType.AttackSpeed => "Attack Speed",
            SkillType.BossDamage => "Boss Damage",
            SkillType.BounceChance => "Bounce Chance",
            SkillType.BounceCount => "Bounce Count",
            SkillType.CriticalChance => "Critical Chance",
            SkillType.CriticalDamage => "Critical Damage",
            SkillType.CooldownReduction => "Cooldown Reduction",
            SkillType.DamagePerRange => "Damage per Range",
            SkillType.DeathDefy => "Death Defy",
            SkillType.DefenseAmount => "Defense",
            SkillType.DefenseBreak => "Defense Break",
            SkillType.DropRate => "Drop Rate",
            SkillType.EarthDamageBonus => "Earth Damage",
            SkillType.ElementMastery => "Element Mastery",
            SkillType.EliteDamage => "Elite Damage",
            SkillType.FireDamageBonus => "Fire Damage",
            SkillType.GoldGain => "Gold Gain",
            SkillType.HealthPoint => "Health Points",
            SkillType.HealthRegen => "Health Regen",
            SkillType.HitRate => "Hit Rate",
            SkillType.InterestWave => "Interest Wave",
            SkillType.KnockbackChance => "Knockback Chance",
            SkillType.KnockbackForce => "Knockback Force",
            SkillType.LifeSteal => "Life Steal",
            SkillType.LightningDamageBonus => "Lightning Damage",
            SkillType.ManaPoint => "Mana Points",
            SkillType.ManaRegen => "Mana Regen",
            SkillType.MetalDamageBonus => "Metal Damage",
            SkillType.MoveSpeed => "Move Speed",
            SkillType.MultiShootChance => "Multi-Shot Chance",
            SkillType.MultiShootCount => "Multi-Shot Count",
            SkillType.StuntChance => "Stun Chance",
            SkillType.StuntDuration => "Stun Duration",
            SkillType.UltimateAttack => "Ultimate Attack",
            SkillType.WaterDamageBonus => "Water Damage",
            SkillType.WindDamageBonus => "Wind Damage",
            SkillType.WoodDamageBonus => "Wood Damage",
            _ => stat.ToString(),
        };
    
        /// <summary>
        /// Gets the short display name for compact UI.
        /// </summary>
        public static string GetSkillShortName(this SkillType stat) => stat switch
        {
            SkillType.AttackDamage => "ADG",
            SkillType.AttackRange => "ARG",
            SkillType.AttackSpeed => "ASP",
            SkillType.BossDamage => "BDMG",
            SkillType.BounceChance => "BCH",
            SkillType.BounceCount => "BCT",
            SkillType.CriticalChance => "CCH",
            SkillType.CriticalDamage => "CDG",
            SkillType.CooldownReduction => "CDR",
            SkillType.DamagePerRange => "DPR",
            SkillType.DeathDefy => "DDF",
            SkillType.DefenseAmount => "DEF",
            SkillType.DefenseBreak => "DBK",
            SkillType.DropRate => "DRT",
            SkillType.EarthDamageBonus => "EDB",
            SkillType.ElementMastery => "ELM",
            SkillType.Evasion => "EVA",
            SkillType.EliteDamage => "EDMG",
            SkillType.FireDamageBonus => "FDB",
            SkillType.GoldGain => "GOLD",
            SkillType.HealthPoint => "HP",
            SkillType.HealthRegen => "HRG",
            SkillType.HitRate => "HIT",
            SkillType.InterestWave => "INW",
            SkillType.KnockbackChance => "KCH",
            SkillType.KnockbackForce => "KFR",
            SkillType.LifeSteal => "LFS",
            SkillType.LightningDamageBonus => "LDB",
            SkillType.ManaPoint => "MP",
            SkillType.ManaRegen => "MRG",
            SkillType.MetalDamageBonus => "MDB",
            SkillType.MoveSpeed => "MSP",
            SkillType.MultiShootChance => "MCH",
            SkillType.MultiShootCount => "MCT",
            SkillType.Penetration => "PEN",
            SkillType.StuntChance => "SCH",
            SkillType.StuntDuration => "SDR",
            SkillType.UltimateAttack => "UAK",
            SkillType.WaterDamageBonus => "WADB",
            SkillType.WindDamageBonus => "WIDB",
            SkillType.WoodDamageBonus => "WDDB",
            _ => "??"
        };


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