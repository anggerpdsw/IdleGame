using System;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Main stats that can appear on equipment and affect gameplay.
    /// Comprehensive list covering all RPG stat needs.
    /// </summary>
    public enum MainStat
    {
        None = 0,

        // Core Combat Stats
        HP = 1,
        Attack = 2,
        Defense = 3,
        AttackSpeed = 4,
        CriticalRate = 5,
        CriticalDamage = 6,
        MoveSpeed = 7,
        Range = 8,
        LifeSteal = 9,
        Dodge = 10,
        Accuracy = 11,
        ArmorPenetration = 12,
        MagicPower = 13,
        MagicResistance = 14,
        CooldownReduction = 15,
        Mana = 16,
        ManaRegen = 17,
        HealthRegen = 18,
        BlockChance = 19,
        Thorns = 20,
        Shield = 21,

        // Projectile/Skill Stats
        ProjectileSpeed = 22,
        ProjectileCount = 23,
        ExplosionRadius = 24,
        SummonPower = 25,

        // Economy/Progression Stats
        Luck = 26,
        GoldGain = 27,
        ExpGain = 28,
        DropRate = 29,
        GemDropRate = 30,

        // Damage Type Specific
        BossDamage = 31,
        EliteDamage = 32,
        NormalDamage = 33,

        // Damage/Defense Modifiers
        DamageReduction = 34,
        FinalDamage = 35,
        FinalDefense = 36,
        FinalHP = 37,
        Evasion = 38,

    }

    /// <summary>
    /// Extension methods for MainStat.
    /// </summary>
    public static class MainStatExtensions
    {
        /// <summary>
        /// Gets the display name for the stat.
        /// </summary>
        public static string GetDisplayName(this MainStat stat) => stat switch
        {
            MainStat.HP => "Max HP",
            MainStat.Attack => "Attack",
            MainStat.Defense => "Defense",
            MainStat.AttackSpeed => "Attack Speed",
            MainStat.CriticalRate => "Crit Rate",
            MainStat.CriticalDamage => "Crit Damage",
            MainStat.MoveSpeed => "Move Speed",
            MainStat.Range => "Attack Range",
            MainStat.LifeSteal => "Life Steal",
            MainStat.Dodge => "Dodge",
            MainStat.Accuracy => "Accuracy",
            MainStat.ArmorPenetration => "Armor Penetration",
            MainStat.MagicPower => "Magic Power",
            MainStat.MagicResistance => "Magic Resistance",
            MainStat.CooldownReduction => "Cooldown Reduction",
            MainStat.Mana => "Mana",
            MainStat.ManaRegen => "Mana Regen",
            MainStat.HealthRegen => "Health Regen",
            MainStat.BlockChance => "Block Chance",
            MainStat.Thorns => "Thorns",
            MainStat.Shield => "Shield",
            MainStat.ProjectileSpeed => "Projectile Speed",
            MainStat.ProjectileCount => "Projectile Count",
            MainStat.ExplosionRadius => "Explosion Radius",
            MainStat.SummonPower => "Summon Power",
            MainStat.Luck => "Luck",
            MainStat.GoldGain => "Gold Gain",
            MainStat.ExpGain => "EXP Gain",
            MainStat.DropRate => "Drop Rate",
            MainStat.GemDropRate => "Gem Drop Rate",
            MainStat.BossDamage => "Boss Damage",
            MainStat.EliteDamage => "Elite Damage",
            MainStat.NormalDamage => "Normal Damage",
            MainStat.DamageReduction => "Damage Reduction",
            MainStat.FinalDamage => "Final Damage",
            MainStat.FinalDefense => "Final Defense",
            MainStat.FinalHP => "Final HP",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short display name for compact UI.
        /// </summary>
        public static string GetShortName(this MainStat stat) => stat switch
        {
            MainStat.HP => "HP",
            MainStat.Attack => "ATK",
            MainStat.Defense => "DEF",
            MainStat.AttackSpeed => "ASPD",
            MainStat.CriticalRate => "CRIT%",
            MainStat.CriticalDamage => "CDMG",
            MainStat.MoveSpeed => "SPD",
            MainStat.Range => "RNG",
            MainStat.LifeSteal => "LS",
            MainStat.Dodge => "DODGE",
            MainStat.Accuracy => "ACC",
            MainStat.ArmorPenetration => "PEN",
            MainStat.MagicPower => "MPOW",
            MainStat.MagicResistance => "MRES",
            MainStat.CooldownReduction => "CDR",
            MainStat.Mana => "MP",
            MainStat.ManaRegen => "MP5",
            MainStat.HealthRegen => "HP5",
            MainStat.BlockChance => "BLOCK",
            MainStat.Thorns => "THORNS",
            MainStat.Shield => "SHLD",
            MainStat.ProjectileSpeed => "PSPD",
            MainStat.ProjectileCount => "PCNT",
            MainStat.ExplosionRadius => "EXPR",
            MainStat.SummonPower => "SUMM",
            MainStat.Luck => "LCK",
            MainStat.GoldGain => "GOLD",
            MainStat.ExpGain => "EXP",
            MainStat.DropRate => "DR",
            MainStat.GemDropRate => "GDR",
            MainStat.BossDamage => "BDMG",
            MainStat.EliteDamage => "EDMG",
            MainStat.NormalDamage => "NDMG",
            MainStat.DamageReduction => "DRD",
            MainStat.FinalDamage => "FD",
            MainStat.FinalDefense => "FDEF",
            MainStat.FinalHP => "FHP",
            _ => "??"
        };

        /// <summary>
        /// Gets the default color for the stat in UI.
        /// </summary>
        public static UnityEngine.Color GetStatColor(this MainStat stat) => stat switch
        {
            // HP/Health stats - Green/Red
            MainStat.HP => new UnityEngine.Color(0.2f, 0.8f, 0.2f),
            MainStat.HealthRegen => new UnityEngine.Color(0.2f, 0.8f, 0.4f),
            MainStat.LifeSteal => new UnityEngine.Color(0.8f, 0.2f, 0.4f),
            MainStat.FinalHP => new UnityEngine.Color(0.1f, 0.7f, 0.2f),

            // Attack/Damage stats - Red/Orange
            MainStat.Attack => new UnityEngine.Color(1f, 0.3f, 0.2f),
            MainStat.AttackSpeed => new UnityEngine.Color(1f, 0.5f, 0.2f),
            MainStat.CriticalRate => new UnityEngine.Color(1f, 0.3f, 0.5f),
            MainStat.CriticalDamage => new UnityEngine.Color(1f, 0.2f, 0.3f),
            MainStat.NormalDamage => new UnityEngine.Color(1f, 0.4f, 0.2f),
            MainStat.EliteDamage => new UnityEngine.Color(1f, 0.3f, 0.2f),
            MainStat.BossDamage => new UnityEngine.Color(1f, 0.1f, 0.1f),
            MainStat.FinalDamage => new UnityEngine.Color(0.9f, 0.1f, 0.1f),
            MainStat.ArmorPenetration => new UnityEngine.Color(0.8f, 0.3f, 0.2f),

            // Defense stats - Blue/Cyan
            MainStat.Defense => new UnityEngine.Color(0.2f, 0.5f, 1f),
            MainStat.MagicResistance => new UnityEngine.Color(0.2f, 0.6f, 1f),
            MainStat.DamageReduction => new UnityEngine.Color(0.2f, 0.7f, 0.8f),
            MainStat.BlockChance => new UnityEngine.Color(0.3f, 0.6f, 0.9f),
            MainStat.Dodge => new UnityEngine.Color(0.3f, 0.7f, 0.9f),
            MainStat.Accuracy => new UnityEngine.Color(0.4f, 0.7f, 1f),
            MainStat.Shield => new UnityEngine.Color(0.3f, 0.8f, 1f),
            MainStat.FinalDefense => new UnityEngine.Color(0.1f, 0.5f, 0.8f),

            // Utility stats - Yellow/Gold
            MainStat.MoveSpeed => new UnityEngine.Color(1f, 0.9f, 0.2f),
            MainStat.Range => new UnityEngine.Color(1f, 0.8f, 0.2f),
            MainStat.CooldownReduction => new UnityEngine.Color(0.9f, 0.7f, 0.2f),
            MainStat.Luck => new UnityEngine.Color(1f, 0.8f, 0.1f),
            MainStat.GoldGain => new UnityEngine.Color(1f, 0.85f, 0f),
            MainStat.ExpGain => new UnityEngine.Color(0.9f, 0.7f, 0.1f),
            MainStat.DropRate => new UnityEngine.Color(0.9f, 0.6f, 0.1f),
            MainStat.GemDropRate => new UnityEngine.Color(0.8f, 0.5f, 0.9f),

            // Magic stats - Purple/Magenta
            MainStat.MagicPower => new UnityEngine.Color(0.8f, 0.2f, 1f),
            MainStat.Mana => new UnityEngine.Color(0.3f, 0.4f, 1f),
            MainStat.ManaRegen => new UnityEngine.Color(0.4f, 0.5f, 1f),

            // Special stats - Unique colors
            MainStat.Thorns => new UnityEngine.Color(0.8f, 0.4f, 0.2f),
            MainStat.ProjectileSpeed => new UnityEngine.Color(0.6f, 0.8f, 0.3f),
            MainStat.ProjectileCount => new UnityEngine.Color(0.5f, 0.7f, 0.3f),
            MainStat.ExplosionRadius => new UnityEngine.Color(1f, 0.4f, 0.1f),
            MainStat.SummonPower => new UnityEngine.Color(0.5f, 0.3f, 0.8f),

            _ => UnityEngine.Color.white
        };

        /// <summary>
        /// Checks if the stat is a percentage stat (displayed as %).
        /// </summary>
        public static bool IsPercentage(this MainStat stat) => stat switch
        {
            MainStat.CriticalRate => true,
            MainStat.CriticalDamage => true,
            MainStat.LifeSteal => true,
            MainStat.Dodge => true,
            MainStat.Accuracy => true,
            MainStat.BlockChance => true,
            MainStat.CooldownReduction => true,
            MainStat.DamageReduction => true,
            MainStat.GoldGain => true,
            MainStat.ExpGain => true,
            MainStat.DropRate => true,
            MainStat.GemDropRate => true,
            MainStat.BossDamage => true,
            MainStat.EliteDamage => true,
            MainStat.NormalDamage => true,
            MainStat.FinalDamage => true,
            MainStat.FinalDefense => true,
            MainStat.FinalHP => true,
            _ => false
        };

        /// <summary>
        /// Checks if the stat is valid (not None).
        /// </summary>
        public static bool IsValid(this MainStat stat) => stat != MainStat.None;

        /// <summary>
        /// Gets all valid main stats (excludes None).
        /// </summary>
        public static MainStat[] GetAllStats() =>
            (MainStat[])Enum.GetValues(typeof(MainStat));

        /// <summary>
        /// Gets the stat category for UI grouping.
        /// </summary>
        public static StatCategory GetCategory(this MainStat stat) => stat switch
        {
            MainStat.HP or MainStat.HealthRegen or MainStat.LifeSteal or MainStat.FinalHP or MainStat.Shield => StatCategory.Health,
            MainStat.Attack or MainStat.AttackSpeed or MainStat.CriticalRate or MainStat.CriticalDamage or
            MainStat.ArmorPenetration or MainStat.NormalDamage or MainStat.EliteDamage or MainStat.BossDamage or MainStat.FinalDamage => StatCategory.Offense,
            MainStat.Defense or MainStat.MagicResistance or MainStat.DamageReduction or MainStat.BlockChance or MainStat.Dodge or MainStat.Accuracy or MainStat.FinalDefense => StatCategory.Defense,
            MainStat.MoveSpeed or MainStat.Range or MainStat.CooldownReduction or MainStat.Luck => StatCategory.Utility,
            MainStat.MagicPower or MainStat.Mana or MainStat.ManaRegen => StatCategory.Magic,
            MainStat.GoldGain or MainStat.ExpGain or MainStat.DropRate or MainStat.GemDropRate => StatCategory.Economy,
            MainStat.ProjectileSpeed or MainStat.ProjectileCount or MainStat.ExplosionRadius or MainStat.SummonPower or MainStat.Thorns => StatCategory.Special,
            _ => StatCategory.Other
        };
    }

    /// <summary>
    /// Stat categories for UI grouping and filtering.
    /// </summary>
    public enum StatCategory
    {
        None = 0,
        Health = 1,
        Offense = 2,
        Defense = 3,
        Utility = 4,
        Magic = 5,
        Economy = 6,
        Special = 7,
        Other = 8,
    }
}