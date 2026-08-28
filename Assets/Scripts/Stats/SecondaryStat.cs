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
        /// Maps SecondaryStat to its corresponding SkillType for display name lookup.
        /// Single source of truth for stat display names.
        /// </summary>
        public static SkillType SecondaryStatToSkillType(SecondaryStat stat) => stat switch
        {
            SecondaryStat.BossDamage => SkillType.BossDamage,
            SecondaryStat.BounceChance => SkillType.BounceChance,
            SecondaryStat.BounceCount => SkillType.BounceCount,
            SecondaryStat.CriticalDamage => SkillType.CriticalDamage,
            SecondaryStat.CooldownReduction => SkillType.CooldownReduction,
            SecondaryStat.DefenseBreak => SkillType.DefenseBreak,
            SecondaryStat.DropRate => SkillType.DropRate,
            SecondaryStat.EarthDamageBonus => SkillType.EarthDamageBonus,
            SecondaryStat.EliteDamage => SkillType.EliteDamage,
            SecondaryStat.FireDamageBonus => SkillType.FireDamageBonus,
            SecondaryStat.GoldGain => SkillType.GoldGain,
            SecondaryStat.HitRate => SkillType.HitRate,
            SecondaryStat.InterestWave => SkillType.InterestWave,
            SecondaryStat.KnockbackForce => SkillType.KnockbackForce,
            SecondaryStat.LifeSteal => SkillType.LifeSteal,
            SecondaryStat.LightningDamageBonus => SkillType.LightningDamageBonus,
            SecondaryStat.MetalDamageBonus => SkillType.MetalDamageBonus,
            SecondaryStat.MoveSpeed => SkillType.MoveSpeed,
            SecondaryStat.MultiShootChance => SkillType.MultiShootChance,
            SecondaryStat.MultiShootCount => SkillType.MultiShootCount,
            SecondaryStat.StuntChance => SkillType.StuntChance,
            SecondaryStat.StuntDuration => SkillType.StuntDuration,
            SecondaryStat.WaterDamageBonus => SkillType.WaterDamageBonus,
            SecondaryStat.WindDamageBonus => SkillType.WindDamageBonus,
            SecondaryStat.WoodDamageBonus => SkillType.WoodDamageBonus,
            _ => SkillType.None,
        };

        public static SecondaryStat SkillTypeToSecondaryStat(SkillType skillType)
        {
            // Only specialization stats have a SecondaryStat counterpart. Derived
            // stats (AttackDamage, HealthPoint, CriticalDamage, ...) come from Main
            // Attribute and are not buffed via the SecondaryStat path.
            return skillType switch
            {
                // Physical
                SkillType.CriticalDamage => SecondaryStat.CriticalDamage,
                SkillType.BounceChance => SecondaryStat.BounceChance,
                SkillType.BounceCount => SecondaryStat.BounceCount,
                SkillType.DefenseBreak => SecondaryStat.DefenseBreak,
                SkillType.MultiShootChance => SecondaryStat.MultiShootChance,
                SkillType.MultiShootCount => SecondaryStat.MultiShootCount,
                SkillType.KnockbackForce => SecondaryStat.KnockbackForce,
                SkillType.StuntChance => SecondaryStat.StuntChance,
                SkillType.StuntDuration => SecondaryStat.StuntDuration,

                // Survival
                SkillType.LifeSteal => SecondaryStat.LifeSteal,

                // Element damage (Layer 3) — per-element bonus (percent, from equipment/card/buff)
                SkillType.MetalDamageBonus => SecondaryStat.MetalDamageBonus,
                SkillType.WoodDamageBonus => SecondaryStat.WoodDamageBonus,
                SkillType.FireDamageBonus => SecondaryStat.FireDamageBonus,
                SkillType.WaterDamageBonus => SecondaryStat.WaterDamageBonus,
                SkillType.EarthDamageBonus => SecondaryStat.EarthDamageBonus,
                SkillType.LightningDamageBonus => SecondaryStat.LightningDamageBonus,
                SkillType.WindDamageBonus => SecondaryStat.WindDamageBonus,

                // Economy
                SkillType.InterestWave => SecondaryStat.InterestWave,
                SkillType.GoldGain => SecondaryStat.GoldGain,
                SkillType.DropRate => SecondaryStat.DropRate,

                // Utility
                SkillType.MoveSpeed => SecondaryStat.MoveSpeed,
                SkillType.CooldownReduction => SecondaryStat.CooldownReduction,
                SkillType.BossDamage => SecondaryStat.BossDamage,
                SkillType.EliteDamage => SecondaryStat.EliteDamage,

                // Accuracy (specialization — from equipment/passive/buff/card, NOT main attributes)
                SkillType.HitRate => SecondaryStat.HitRate,

                // Derived from Main Attribute / no secondary equivalent
                _ => SecondaryStat.None
            };
        }

        /// <summary>
        /// Gets the display name for a SecondaryStat — delegates to SkillTypeExtensions.
        /// </summary>
        public static string GetSkillDisplayName(this SecondaryStat stat) =>
            SecondaryStatToSkillType(stat).GetSkillDisplayName();

        /// <summary>
        /// Gets the short display name for a SecondaryStat — delegates to SkillTypeExtensions.
        /// </summary>
        public static string GetSkillShortName(this SecondaryStat stat) =>
            SecondaryStatToSkillType(stat).GetSkillShortName();

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
        CriticalDamage = 5,
        BounceChance = 7,
        BounceCount = 8,
        DefenseBreak = 9,
        MultiShootChance = 10,
        MultiShootCount = 11,
        KnockbackForce = 13,
        StuntChance = 14,
        StuntDuration = 15,

        // Survival
        LifeSteal = 19,

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
    }

}