using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Centralized metadata for a SecondaryStat — single source of truth for
    /// display, categorization, and generation configuration.
    /// </summary>
    public readonly struct SecondaryStatMeta
    {
        public readonly SecondaryStat Stat;
        public readonly StatCategory Category;
        public readonly Color Color;
        public readonly float BaseValue;
        public readonly bool CanRollOnEquipment;
        // Fallback display names (used if BaseStatLoader not available or JSON missing)
        private readonly string _fallbackDisplayName;
        private readonly string _fallbackShortName;

        public SecondaryStatMeta(
            SecondaryStat stat,
            string displayName,
            string shortName,
            StatCategory category,
            Color color,
            float baseValue,
            bool canRollOnEquipment = true)
        {
            Stat = stat;
            Category = category;
            Color = color;
            BaseValue = baseValue;
            CanRollOnEquipment = canRollOnEquipment;
            _fallbackDisplayName = displayName;
            _fallbackShortName = shortName;
        }

        /// <summary>Gets the SkillType mapped from this SecondaryStat.</summary>
        public SkillType SkillType => SecondaryStatExtensions.SecondaryStatToSkillType(Stat);

        /// <summary>Gets display name from dataPlayer.json via BaseStatLoader (single source of truth).</summary>
        public string DisplayName
        {
            get
            {
                var loader = BaseStatLoader.Instance;
                if (loader != null)
                {
                    var skillData = loader.GetSecondarySkillData(Stat);
                    if (skillData != null && !string.IsNullOrEmpty(skillData.displayName))
                        return skillData.displayName;
                }
                return _fallbackDisplayName;
            }
        }

        /// <summary>Gets short name from dataPlayer.json via BaseStatLoader (single source of truth).</summary>
        public string ShortName
        {
            get
            {
                var loader = BaseStatLoader.Instance;
                if (loader != null)
                {
                    var skillData = loader.GetSecondarySkillData(Stat);
                    if (skillData != null && !string.IsNullOrEmpty(skillData.shortName))
                        return skillData.shortName;
                }
                return _fallbackShortName;
            }
        }

        /// <summary>Gets ValuePerLevel from dataPlayer.json via BaseStatLoader (single source of truth).</summary>
        public float ValuePerLevel
        {
            get
            {
                var loader = BaseStatLoader.Instance;
                return loader != null ? loader.GetSecondaryValuePerLevel(Stat) : 0f;
            }
        }

        /// <summary>Gets ValuePerEnhance from dataPlayer.json via BaseStatLoader (single source of truth).</summary>
        public float ValuePerEnhance
        {
            get
            {
                var loader = BaseStatLoader.Instance;
                return loader != null ? loader.GetSecondaryValuePerEnhance(Stat) : 0f;
            }
        }
    }

    /// <summary>
    /// Static registry of all SecondaryStat metadata.
    /// Single source of truth for IsPercentage, BaseValue, Category, DisplayName, etc.
    /// All systems (roll, modifier, UI) must read from here.
    /// </summary>
    public static class SecondaryStatRegistry
    {
        private static readonly SecondaryStatMeta[] _entries;
        private static readonly Dictionary<SecondaryStat, SecondaryStatMeta> _byStat;

        static SecondaryStatRegistry()
        {
            var list = new List<SecondaryStatMeta>
            {
                // Physical
                new(SecondaryStat.BounceChance, "Bounce Chance", "Bounce%", StatCategory.Special, GameColors.statBounceChance, 5f),
                new(SecondaryStat.MultiShootChance, "Multi-Shot Chance", "Multi%", StatCategory.Special, GameColors.statMultiShootChance, 5f),
                new(SecondaryStat.StuntChance, "Stun Chance", "Stun%", StatCategory.Special, GameColors.statStunChance, 3f),
                new(SecondaryStat.CriticalDamage, "Critical Damage", "Crit Dmg", StatCategory.Offense, GameColors.red, 1f, canRollOnEquipment: false),
                new(SecondaryStat.BounceCount, "Bounce Count", "Bounce #", StatCategory.Special, GameColors.statBounceCount, 1f),
                new(SecondaryStat.DefenseBreak, "Defense Break", "Def Break", StatCategory.Special, GameColors.blue, 1f, canRollOnEquipment: false),
                new(SecondaryStat.MultiShootCount, "Multi-Shot Count", "Multi #", StatCategory.Special, GameColors.statMultiShootCount, 1f),
                new(SecondaryStat.KnockbackForce, "Knockback Force", "KB Force", StatCategory.Special, GameColors.red, 1f, canRollOnEquipment: false),
                new(SecondaryStat.StuntDuration, "Stun Duration", "Stun Dur", StatCategory.Special, GameColors.statStunDuration, 0.5f),

                // Survival
                new(SecondaryStat.LifeSteal, "Life Steal", "Lifesteal%", StatCategory.Health, GameColors.statLifeSteal, 1f),

                // Element Damage (Layer 3) — all flat, from equipment/card/buff
                new(SecondaryStat.MetalDamageBonus, "Metal Damage", "Metal", StatCategory.Magic, GameColors.statMetal, 1f),
                new(SecondaryStat.WoodDamageBonus, "Wood Damage", "Wood", StatCategory.Magic, GameColors.statWood, 1f),
                new(SecondaryStat.FireDamageBonus, "Fire Damage", "Fire", StatCategory.Magic, GameColors.statFire, 1f),
                new(SecondaryStat.WaterDamageBonus, "Water Damage", "Water", StatCategory.Magic, GameColors.statWater, 1f),
                new(SecondaryStat.EarthDamageBonus, "Earth Damage", "Earth", StatCategory.Magic, GameColors.statEarth, 1f),
                new(SecondaryStat.LightningDamageBonus, "Lightning Damage", "Lightning", StatCategory.Magic, GameColors.gold, 1f),
                new(SecondaryStat.WindDamageBonus, "Wind Damage", "Wind", StatCategory.Magic, GameColors.statWind, 1f),

                // Economy
                new(SecondaryStat.InterestWave, "Interest per Wave", "Interest", StatCategory.Economy, GameColors.statInterestWave, 1f),
                new(SecondaryStat.GoldGain, "Gold Gain", "Gold%", StatCategory.Economy, GameColors.statGoldGain, 1f),
                new(SecondaryStat.DropRate, "Drop Rate", "Drop%", StatCategory.Economy, GameColors.statDropRate, 1f),

                // Utility
                new(SecondaryStat.MoveSpeed, "Move Speed", "Move%", StatCategory.Utility, GameColors.statMoveSpeed, 0.5f),
                new(SecondaryStat.CooldownReduction, "Cooldown Reduction", "CD%", StatCategory.Utility, GameColors.statCooldownReduction, 1f),
                new(SecondaryStat.BossDamage, "Boss Damage", "Boss%", StatCategory.Offense, GameColors.statBossDamage, 1f),
                new(SecondaryStat.EliteDamage, "Elite Damage", "Elite%", StatCategory.Offense, GameColors.statEliteDamage, 1f),

                // Accuracy
                new(SecondaryStat.HitRate, "Hit Rate", "Hit%", StatCategory.Utility, GameColors.statHitRate, 1f),
            };

            _entries = list.ToArray();
            _byStat = new Dictionary<SecondaryStat, SecondaryStatMeta>(_entries.Length);
            foreach (var e in _entries) _byStat[e.Stat] = e;
        }

        /// <summary>Gets metadata for a stat. Returns default if not found.</summary>
        public static SecondaryStatMeta Get(SecondaryStat stat) =>
            _byStat.TryGetValue(stat, out var meta) ? meta : default;

        /// <summary>Gets all registered stats (excludes None).</summary>
        public static IReadOnlyList<SecondaryStatMeta> All => _entries;

        /// <summary>Gets all stats that can roll on equipment.</summary>
        public static IReadOnlyList<SecondaryStatMeta> Rollable =>
            _entries.Where(e => e.CanRollOnEquipment).ToArray();

        /// <summary>Gets stats filtered by category.</summary>
        public static IReadOnlyList<SecondaryStatMeta> ByCategory(StatCategory category) =>
            _entries.Where(e => e.Category == category).ToArray();

        /// <summary>Gets a flat array of just the SecondaryStat enum values for random picks.</summary>
        public static SecondaryStat[] GetAllStats() => _entries.Select(e => e.Stat).ToArray();

        /// <summary>Gets a flat array of rollable SecondaryStat enum values.</summary>
        public static SecondaryStat[] GetRollableStats() =>
            _entries.Where(e => e.CanRollOnEquipment).Select(e => e.Stat).ToArray();
    }

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
            // Only specialization stats have a SecondaryStat counterpart. 
            // Derived stats (AttackDamage, HealthPoint, CriticalDamage, ...) come from Main
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
        /// Gets the display name for a SecondaryStat — delegates to metadata registry.
        /// </summary>
        public static string GetSkillDisplayName(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).DisplayName;

        /// <summary>
        /// Gets the short display name for a SecondaryStat.
        /// </summary>
        public static string GetSkillShortName(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).ShortName;

        /// <summary>
        /// Gets the default color for the stat in UI.
        /// </summary>
        public static Color GetStatColor(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).Color;

        /// <summary>
        /// Gets the base value used for stat generation (roll).
        /// </summary>
        public static float GetBaseValue(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).BaseValue;

        /// <summary>
        /// Checks if the stat is valid (not None).
        /// </summary>
        public static bool IsValid(this SecondaryStat stat) => stat != SecondaryStat.None;

        /// <summary>
        /// Gets the stat category for UI grouping.
        /// </summary>
        public static StatCategory GetCategory(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).Category;

        /// <summary>
        /// Checks if this stat can roll on equipment.
        /// </summary>
        public static bool CanRollOnEquipment(this SecondaryStat stat) =>
            SecondaryStatRegistry.Get(stat).CanRollOnEquipment;
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
    /// Secondary stats - specialization layer from equipment.
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