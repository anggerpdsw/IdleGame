using System;
using UnityEngine;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Extension methods for SecondaryStat.
    /// </summary>
    public static class SecondaryStatExtensions
    {
        /// <summary>
        /// Gets the display name for the stat.
        /// </summary>
        public static string GetDisplayName(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.AttackRange => "Attack Range",
            SecondaryStat.BounceChance => "Bounce Chance",
            SecondaryStat.BounceCount => "Bounce Count",
            SecondaryStat.MultiShootChance => "Multi-Shot Chance",
            SecondaryStat.MultiShootCount => "Multi-Shot Count",
            SecondaryStat.KnockbackChance => "Knockback Chance",
            SecondaryStat.StuntChance => "Stun Chance",
            SecondaryStat.StuntDuration => "Stun Duration",
            SecondaryStat.LifeSteal => "Life Steal",
            SecondaryStat.DamagePerRange => "Damage per Range",
            SecondaryStat.CooldownReduction => "Cooldown Reduction",
            SecondaryStat.MoveSpeed => "Move Speed",
            SecondaryStat.BossDamage => "Boss Damage",
            SecondaryStat.EliteDamage => "Elite Damage",
            SecondaryStat.GoldGain => "Gold Gain",
            SecondaryStat.DropRate => "Drop Rate",
            SecondaryStat.InterestWave => "Interest Wave",
            SecondaryStat.HitRate => "Hit Rate",
            SecondaryStat.Penetration => "Penetration",
            SecondaryStat.MetalDamageBonus => "Metal Damage",
            SecondaryStat.WoodDamageBonus => "Wood Damage",
            SecondaryStat.FireDamageBonus => "Fire Damage",
            SecondaryStat.WaterDamageBonus => "Water Damage",
            SecondaryStat.EarthDamageBonus => "Earth Damage",
            SecondaryStat.LightningDamageBonus => "Lightning Damage",
            SecondaryStat.WindDamageBonus => "Wind Damage",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short display name for compact UI.
        /// </summary>
        public static string GetShortName(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.AttackRange => "ARNG",
            SecondaryStat.BounceChance => "BCH",
            SecondaryStat.BounceCount => "BCNT",
            SecondaryStat.MultiShootChance => "MSCH",
            SecondaryStat.MultiShootCount => "MCNT",
            SecondaryStat.KnockbackChance => "KBCH",
            SecondaryStat.StuntChance => "STCH",
            SecondaryStat.StuntDuration => "STDUR",
            SecondaryStat.LifeSteal => "LS",
            SecondaryStat.DamagePerRange => "DPR",
            SecondaryStat.CooldownReduction => "CDR",
            SecondaryStat.MoveSpeed => "SPD",
            SecondaryStat.BossDamage => "BDMG",
            SecondaryStat.EliteDamage => "EDMG",
            SecondaryStat.GoldGain => "GOLD",
            SecondaryStat.DropRate => "DR",
            SecondaryStat.InterestWave => "INTW",
            SecondaryStat.HitRate => "HIT",
            SecondaryStat.Penetration => "PEN",
            SecondaryStat.MetalDamageBonus => "MET",
            SecondaryStat.WoodDamageBonus => "WOD",
            SecondaryStat.FireDamageBonus => "FIR",
            SecondaryStat.WaterDamageBonus => "WAT",
            SecondaryStat.EarthDamageBonus => "ERT",
            SecondaryStat.LightningDamageBonus => "LTN",
            SecondaryStat.WindDamageBonus => "WND",
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
            SecondaryStat.DamagePerRange => GameColors.statDamagePerRange,

            // Utility stats - Yellow/Gold
            SecondaryStat.MoveSpeed => GameColors.statMoveSpeed,
            SecondaryStat.CooldownReduction => GameColors.statCooldownReduction,
            SecondaryStat.GoldGain => GameColors.statGoldGain,
            SecondaryStat.DropRate => GameColors.statDropRate,
            SecondaryStat.InterestWave => GameColors.statInterestWave,

            // Accuracy - Cyan/Blue white
            SecondaryStat.HitRate => GameColors.statHitRate,
            SecondaryStat.Penetration => GameColors.purple,

            // Element damage bonus - Arcane purple/teal family, one hue per element
            SecondaryStat.MetalDamageBonus => GameColors.statMetal,
            SecondaryStat.WoodDamageBonus => GameColors.statWood,
            SecondaryStat.FireDamageBonus => GameColors.statFire,
            SecondaryStat.WaterDamageBonus => GameColors.statWater,
            SecondaryStat.EarthDamageBonus => GameColors.statEarth,
            SecondaryStat.LightningDamageBonus => GameColors.statMoveSpeed,
            SecondaryStat.WindDamageBonus => GameColors.statWind,

            // Projectile/Crowd Control - Purple/Teal
            SecondaryStat.AttackRange => GameColors.statRange,
            SecondaryStat.BounceChance => GameColors.statBounceChance,
            SecondaryStat.BounceCount => GameColors.statBounceCount,
            SecondaryStat.MultiShootChance => GameColors.statMultiShootChance,
            SecondaryStat.MultiShootCount => GameColors.statMultiShootCount,
            SecondaryStat.KnockbackChance => GameColors.statKnockbackChance,
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
            SecondaryStat.KnockbackChance => true,
            SecondaryStat.StuntChance => true,
            SecondaryStat.InterestWave => true,
            SecondaryStat.HitRate => true,
            SecondaryStat.MetalDamageBonus => true,
            SecondaryStat.WoodDamageBonus => true,
            SecondaryStat.FireDamageBonus => true,
            SecondaryStat.WaterDamageBonus => true,
            SecondaryStat.EarthDamageBonus => true,
            SecondaryStat.LightningDamageBonus => true,
            SecondaryStat.WindDamageBonus => true,
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
            SecondaryStat.EliteDamage or SecondaryStat.BossDamage or SecondaryStat.DamagePerRange or SecondaryStat.Penetration => StatCategory.Offense,
            SecondaryStat.MoveSpeed or SecondaryStat.CooldownReduction or SecondaryStat.HitRate => StatCategory.Utility,
            SecondaryStat.GoldGain or SecondaryStat.DropRate or SecondaryStat.InterestWave => StatCategory.Economy,
            SecondaryStat.AttackRange or SecondaryStat.BounceChance or SecondaryStat.BounceCount or
            SecondaryStat.MultiShootChance or SecondaryStat.MultiShootCount or
            SecondaryStat.KnockbackChance or SecondaryStat.StuntChance or SecondaryStat.StuntDuration => StatCategory.Special,
            SecondaryStat.MetalDamageBonus or SecondaryStat.WoodDamageBonus or SecondaryStat.FireDamageBonus or
            SecondaryStat.WaterDamageBonus or SecondaryStat.EarthDamageBonus or
            SecondaryStat.LightningDamageBonus or SecondaryStat.WindDamageBonus => StatCategory.Magic,
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