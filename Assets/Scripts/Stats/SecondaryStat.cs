using System;
using UnityEngine;

namespace IdleDefenseSurvival
{
    /// <summary>
    /// Extension methods for SecondaryStat.
    /// </summary>
    public static class SecondaryStatExtensions
    {
        /// <summary>
        /// Gets the display name for the stat.
        /// </summary>
        public static string GetSkillDisplayName(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.BossDamage => "Boss Damage",
            SecondaryStat.BounceChance => "Bounce Chance",
            SecondaryStat.BounceCount => "Bounce Count",
            SecondaryStat.CriticalDamage => "Critical Damage",
            SecondaryStat.CooldownReduction => "Cooldown Reduction",
            SecondaryStat.DefenseBreak => "Defense Break",
            SecondaryStat.DropRate => "Drop Rate",
            SecondaryStat.EarthDamageBonus => "Earth Damage",
            SecondaryStat.EliteDamage => "Elite Damage",
            SecondaryStat.FireDamageBonus => "Fire Damage",
            SecondaryStat.GoldGain => "Gold Gain",
            SecondaryStat.HitRate => "Hit Rate",
            SecondaryStat.InterestWave => "Interest Wave",
            SecondaryStat.KnockbackForce => "Knockback Force",
            SecondaryStat.LifeSteal => "Life Steal",
            SecondaryStat.LightningDamageBonus => "Lightning Damage",
            SecondaryStat.MetalDamageBonus => "Metal Damage",
            SecondaryStat.MoveSpeed => "Move Speed",
            SecondaryStat.MultiShootChance => "Multi-Shot Chance",
            SecondaryStat.MultiShootCount => "Multi-Shot Count",
            SecondaryStat.StuntChance => "Stun Chance",
            SecondaryStat.StuntDuration => "Stun Duration",
            SecondaryStat.WaterDamageBonus => "Water Damage",
            SecondaryStat.WindDamageBonus => "Wind Damage",
            SecondaryStat.WoodDamageBonus => "Wood Damage",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short display name for compact UI.
        /// </summary>
        public static string GetSkillShortName(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.BossDamage => "BDMG",
            SecondaryStat.BounceChance => "BCH",
            SecondaryStat.BounceCount => "BCT",
            SecondaryStat.CooldownReduction => "CDR",
            SecondaryStat.DefenseBreak => "DBK",
            SecondaryStat.DropRate => "DRT",
            SecondaryStat.EarthDamageBonus => "EDB",
            SecondaryStat.EliteDamage => "EDMG",
            SecondaryStat.FireDamageBonus => "FDB",
            SecondaryStat.GoldGain => "GOLD",
            SecondaryStat.HitRate => "HIT",
            SecondaryStat.InterestWave => "INW",
            SecondaryStat.KnockbackForce => "KFR",
            SecondaryStat.LifeSteal => "LFS",
            SecondaryStat.LightningDamageBonus => "LDB",
            SecondaryStat.MetalDamageBonus => "MDB",
            SecondaryStat.MoveSpeed => "MSP",
            SecondaryStat.MultiShootChance => "MCH",
            SecondaryStat.MultiShootCount => "MCT",
            SecondaryStat.StuntChance => "SCH",
            SecondaryStat.StuntDuration => "SDR",
            SecondaryStat.WaterDamageBonus => "WADB",
            SecondaryStat.WindDamageBonus => "WIDB",
            SecondaryStat.WoodDamageBonus => "WDDB",
            _ => "??"
        };

        /// <summary>
        /// Gets the default color for the stat in UI.
        /// </summary>
        public static Color GetStatColor(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.LifeSteal => GameColors.statLifeSteal,

            // PvE / utility damage - Red/Orange
            SecondaryStat.BossDamage => GameColors.statBossDamage,
            SecondaryStat.EliteDamage => GameColors.statEliteDamage,
            SecondaryStat.DefenseBreak => GameColors.blue,

            // Utility stats - Yellow/Gold
            SecondaryStat.MoveSpeed => GameColors.statMoveSpeed,
            SecondaryStat.CooldownReduction => GameColors.statCooldownReduction,
            SecondaryStat.GoldGain => GameColors.statGoldGain,
            SecondaryStat.DropRate => GameColors.statDropRate,
            SecondaryStat.InterestWave => GameColors.statInterestWave,

            // Accuracy - Cyan/Blue white
            SecondaryStat.HitRate => GameColors.statHitRate,

            // Element damage bonus - Arcane purple/teal family, one hue per element
            SecondaryStat.MetalDamageBonus => GameColors.statMetal,
            SecondaryStat.WoodDamageBonus => GameColors.statWood,
            SecondaryStat.FireDamageBonus => GameColors.statFire,
            SecondaryStat.WaterDamageBonus => GameColors.statWater,
            SecondaryStat.EarthDamageBonus => GameColors.statEarth,
            SecondaryStat.LightningDamageBonus => GameColors.statMoveSpeed,
            SecondaryStat.WindDamageBonus => GameColors.statWind,

            // Projectile/Crowd Control - Purple/Teal
            SecondaryStat.BounceChance => GameColors.statBounceChance,
            SecondaryStat.BounceCount => GameColors.statBounceCount,
            SecondaryStat.MultiShootChance => GameColors.statMultiShootChance,
            SecondaryStat.MultiShootCount => GameColors.statMultiShootCount,
            SecondaryStat.StuntChance => GameColors.statStunChance,
            SecondaryStat.StuntDuration => GameColors.statStunDuration,

            _ => Color.white
        };

        /// <summary>
        /// Checks if the stat is a percentage stat (displayed as %).
        /// </summary>
        public static bool IsPercentage(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.LifeSteal => true,
            SecondaryStat.CooldownReduction => true,
            SecondaryStat.BossDamage => true,
            SecondaryStat.EliteDamage => true,
            SecondaryStat.DropRate => true,
            SecondaryStat.GoldGain => true,
            SecondaryStat.BounceChance => true,
            SecondaryStat.MultiShootChance => true,
            SecondaryStat.StuntChance => true,
            SecondaryStat.InterestWave => true,
            SecondaryStat.HitRate => true,
            SecondaryStat.MetalDamageBonus  => true,
            SecondaryStat.WoodDamageBonus   => true,
            SecondaryStat.FireDamageBonus   => true,
            SecondaryStat.WaterDamageBonus  => true,
            SecondaryStat.EarthDamageBonus  => true,
            SecondaryStat.WindDamageBonus   => true,
            SecondaryStat.LightningDamageBonus => true,
            _ => false
        };

        /// <summary>
        /// Checks if the stat is valid (not None).
        /// </summary>
        public static bool IsValid(this SecondaryStat stat) => stat != SecondaryStat.None;

        /// <summary>
        /// Gets all valid secondary stats (excludes None).
        /// </summary>
        public static SecondaryStat[] GetAllStats() =>
            (SecondaryStat[])Enum.GetValues(typeof(SecondaryStat));

        /// <summary>
        /// Gets the stat category for UI grouping.
        /// </summary>
        public static StatCategory GetCategory(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.LifeSteal => StatCategory.Health,
            SecondaryStat.EliteDamage or 
            SecondaryStat.BossDamage => StatCategory.Offense,
            SecondaryStat.MoveSpeed or 
            SecondaryStat.CooldownReduction or 
            SecondaryStat.HitRate => StatCategory.Utility,
            SecondaryStat.GoldGain or 
            SecondaryStat.DropRate or 
            SecondaryStat.InterestWave => StatCategory.Economy,
            SecondaryStat.BounceChance or SecondaryStat.BounceCount or
            SecondaryStat.MultiShootChance or 
            SecondaryStat.MultiShootCount or
            SecondaryStat.StuntChance or 
            SecondaryStat.StuntDuration or
            SecondaryStat.DefenseBreak => StatCategory.Special,
            SecondaryStat.MetalDamageBonus or 
            SecondaryStat.WoodDamageBonus or 
            SecondaryStat.FireDamageBonus or
            SecondaryStat.WaterDamageBonus or 
            SecondaryStat.EarthDamageBonus or
            SecondaryStat.LightningDamageBonus or 
            SecondaryStat.WindDamageBonus => StatCategory.Magic,
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

    /// <summary>
    /// Secondary stats - specialization layer from equipment (the ~20% build identity).
    /// Core power comes from MainAttribute (CON/STR/INT/DEX) via derived SecondaryStats,
    /// and SecondaryStat feeds combat. SecondaryStat is pure specialization (build identity).
    /// No stat here is derivable from attributes — that avoids double-dipping.
    /// </summary>
    public enum SecondaryStat
    {
        None = 0,

        // Physical
        CriticalDamage = 1,
        BounceChance = 2,
        BounceCount = 3,
        DefenseBreak = 4,
        MultiShootChance = 5,
        MultiShootCount = 6,
        KnockbackForce = 7,
        StuntChance = 8,
        StuntDuration = 9,

        // Survival
        LifeSteal = 10,

        // Element damage (Layer 3) — per-element bonus (percent, from equipment/card/buff)
        MetalDamageBonus = 11,
        WoodDamageBonus = 12,
        FireDamageBonus = 13,
        WaterDamageBonus = 14,
        EarthDamageBonus = 15,
        LightningDamageBonus = 16,
        WindDamageBonus = 17,

        // Economy
        InterestWave = 18,
        GoldGain = 19,
        DropRate = 20,

        // Utility
        MoveSpeed = 21,
        CooldownReduction = 22,
        BossDamage = 23,
        EliteDamage = 24,

        // Accuracy (specialization — from equipment/passive/buff/card, NOT main attributes)
        HitRate = 25
    }

}