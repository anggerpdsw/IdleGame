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
            _ => "??"
        };

        /// <summary>
        /// Gets the default color for the stat in UI.
        /// </summary>
        public static Color GetStatColor(this SecondaryStat stat) => stat switch
        {
            SecondaryStat.LifeSteal => new Color(0.8f, 0.2f, 0.4f),

            // PvE / utility damage - Red/Orange
            SecondaryStat.BossDamage => new Color(1f, 0.1f, 0.1f),
            SecondaryStat.EliteDamage => new Color(1f, 0.3f, 0.2f),
            SecondaryStat.DamagePerRange => new Color(1f, 0.4f, 0.2f),

            // Utility stats - Yellow/Gold
            SecondaryStat.MoveSpeed => new Color(1f, 0.9f, 0.2f),
            SecondaryStat.CooldownReduction => new Color(0.9f, 0.7f, 0.2f),
            SecondaryStat.GoldGain => new Color(1f, 0.85f, 0f),
            SecondaryStat.DropRate => new Color(0.9f, 0.6f, 0.1f),
            SecondaryStat.InterestWave => new Color(0.5f, 0.8f, 0.5f),

            // Accuracy - Cyan/Blue white
            SecondaryStat.HitRate => new Color(0.4f, 0.9f, 1f),

            // Projectile/Crowd Control - Purple/Teal
            SecondaryStat.AttackRange => new Color(0.6f, 0.3f, 1f),
            SecondaryStat.BounceChance => new Color(0.5f, 0.8f, 1f),
            SecondaryStat.BounceCount => new Color(0.4f, 0.7f, 1f),
            SecondaryStat.MultiShootChance => new Color(0.8f, 0.3f, 1f),
            SecondaryStat.MultiShootCount => new Color(0.7f, 0.2f, 1f),
            SecondaryStat.KnockbackChance => new Color(0.9f, 0.4f, 0.6f),
            SecondaryStat.StuntChance => new Color(0.8f, 0.3f, 0.5f),
            SecondaryStat.StuntDuration => new Color(0.7f, 0.2f, 0.4f),

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
            SecondaryStat.EliteDamage or SecondaryStat.BossDamage or SecondaryStat.DamagePerRange => StatCategory.Offense,
            SecondaryStat.MoveSpeed or SecondaryStat.CooldownReduction or SecondaryStat.HitRate => StatCategory.Utility,
            SecondaryStat.GoldGain or SecondaryStat.DropRate or SecondaryStat.InterestWave => StatCategory.Economy,
            SecondaryStat.AttackRange or SecondaryStat.BounceChance or SecondaryStat.BounceCount or
            SecondaryStat.MultiShootChance or SecondaryStat.MultiShootCount or
            SecondaryStat.KnockbackChance or SecondaryStat.StuntChance or SecondaryStat.StuntDuration => StatCategory.Special,
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