using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Special effect types for equipment passive effects.
    /// Uses Strategy Pattern - each effect type has a corresponding IEquipmentEffect implementation.
    /// Add new effects by creating new IEquipmentEffect implementations and registering them.
    /// </summary>
    public enum SpecialEffectType
    {
        None = 0,

        // === Healing/Regeneration ===
        HealEverySecond = 1,
        AutoHeal = 2,
        CriticalHeal = 3,
        LifeConversion = 4,
        PhoenixRevival = 5,

        // === Elemental Damage Over Time ===
        BurnEnemy = 10,
        FreezeEnemy = 11,
        Poison = 12,
        Shock = 13,
        Bleed = 14,
        Curse = 15,

        // === Triggered Effects (On Kill / On Hit / On Crit) ===
        ExplosionOnKill = 20,
        ChainLightning = 21,
        Meteor = 22,
        IceSpike = 23,
        FireAura = 24,
        ChainExplosion = 25,
        Ricochet = 26,
        Pierce = 27,
        Bounce = 28,
        SplitProjectile = 29,
        MultiShot = 30,
        CriticalExplosion = 31,
        DoubleProjectile = 32,
        TripleProjectile = 33,

        // === Summoning Effects ===
        SummonSkeleton = 40,
        SummonWolf = 41,
        SummonDrone = 42,
        SummonTotem = 43,
        SummonTurret = 44,
        OrbitSword = 45,
        OrbitShield = 46,

        // === Defensive/Protection ===
        ReflectDamage = 50,
        ShieldEvery10Seconds = 51,
        ReflectProjectile = 52,
        Invincible = 53,
        DamageReductionAura = 54,
        AdaptiveDefense = 55,

        // === Mobility/Positioning ===
        DashAttack = 60,
        Teleport = 61,
        PullEnemy = 62,
        PushEnemy = 63,
        Knockback = 64,
        SlowAura = 65,
        TimeSlow = 66,
        TimeFast = 67,

        // === Crowd Control ===
        Fear = 70,
        Stealth = 71,
        Camouflage = 72,
        GhostForm = 73,

        // === Buff/Debuff ===
        RandomBuff = 80,
        RandomDebuff = 81,
        Blessing = 82,
        Berserk = 83,
        Rage = 84,
        BloodSacrifice = 85,

        // === Economy/Loot ===
        ExtraCoin = 90,
        ExtraEXP = 91,
        ExtraLoot = 92,
        LuckyDrop = 93,
        AutoCollect = 94,
        Magnet = 95,
        GoldPerKill = 96,
        DamagePerGold = 97,
        DamagePerKill = 98,
        DamagePerWave = 99,
        DamagePerTier = 100,
        DamagePerMissingHP = 101,
        AdaptiveDamage = 102,

        // === Ultimate/Skill Related ===
        UltimateDamage = 110,
        SkillCooldownReduction = 111,
        BossExecute = 112,
        ComboMultiplier = 113,
        PetDamageBoost = 114,

        // === Unique/Special ===
        InstantKillChance = 120,
        Revive = 121,
        AutoRepair = 122,
    }

    /// <summary>
    /// Extension methods for SpecialEffectType.
    /// </summary>
    public static class SpecialEffectTypeExtensions
    {
        /// <summary>
        /// Gets the display name for the effect.
        /// </summary>
        public static string GetDisplayName(this SpecialEffectType type) => type switch
        {
            SpecialEffectType.HealEverySecond => "Heal Every Second",
            SpecialEffectType.AutoHeal => "Auto Heal",
            SpecialEffectType.CriticalHeal => "Critical Heal",
            SpecialEffectType.LifeConversion => "Life Conversion",
            SpecialEffectType.PhoenixRevival => "Phoenix Revival",

            SpecialEffectType.BurnEnemy => "Burn",
            SpecialEffectType.FreezeEnemy => "Freeze",
            SpecialEffectType.Poison => "Poison",
            SpecialEffectType.Shock => "Shock",
            SpecialEffectType.Bleed => "Bleed",
            SpecialEffectType.Curse => "Curse",

            SpecialEffectType.ExplosionOnKill => "Explosion on Kill",
            SpecialEffectType.ChainLightning => "Chain Lightning",
            SpecialEffectType.Meteor => "Meteor",
            SpecialEffectType.IceSpike => "Ice Spike",
            SpecialEffectType.FireAura => "Fire Aura",
            SpecialEffectType.ChainExplosion => "Chain Explosion",
            SpecialEffectType.Ricochet => "Ricochet",
            SpecialEffectType.Pierce => "Pierce",
            SpecialEffectType.Bounce => "Bounce",
            SpecialEffectType.SplitProjectile => "Split Projectile",
            SpecialEffectType.MultiShot => "Multi Shot",
            SpecialEffectType.CriticalExplosion => "Critical Explosion",
            SpecialEffectType.DoubleProjectile => "Double Projectile",
            SpecialEffectType.TripleProjectile => "Triple Projectile",

            SpecialEffectType.SummonSkeleton => "Summon Skeleton",
            SpecialEffectType.SummonWolf => "Summon Wolf",
            SpecialEffectType.SummonDrone => "Summon Drone",
            SpecialEffectType.SummonTotem => "Summon Totem",
            SpecialEffectType.SummonTurret => "Summon Turret",
            SpecialEffectType.OrbitSword => "Orbit Sword",
            SpecialEffectType.OrbitShield => "Orbit Shield",

            SpecialEffectType.ReflectDamage => "Reflect Damage",
            SpecialEffectType.ShieldEvery10Seconds => "Shield Every 10s",
            SpecialEffectType.ReflectProjectile => "Reflect Projectile",
            SpecialEffectType.Invincible => "Invincible",
            SpecialEffectType.DamageReductionAura => "Damage Reduction Aura",
            SpecialEffectType.AdaptiveDefense => "Adaptive Defense",

            SpecialEffectType.DashAttack => "Dash Attack",
            SpecialEffectType.Teleport => "Teleport",
            SpecialEffectType.PullEnemy => "Pull Enemy",
            SpecialEffectType.PushEnemy => "Push Enemy",
            SpecialEffectType.Knockback => "Knockback",
            SpecialEffectType.SlowAura => "Slow Aura",
            SpecialEffectType.TimeSlow => "Time Slow",
            SpecialEffectType.TimeFast => "Time Fast",

            SpecialEffectType.Fear => "Fear",
            SpecialEffectType.Stealth => "Stealth",
            SpecialEffectType.Camouflage => "Camouflage",
            SpecialEffectType.GhostForm => "Ghost Form",

            SpecialEffectType.RandomBuff => "Random Buff",
            SpecialEffectType.RandomDebuff => "Random Debuff",
            SpecialEffectType.Blessing => "Blessing",
            SpecialEffectType.Berserk => "Berserk",
            SpecialEffectType.Rage => "Rage",
            SpecialEffectType.BloodSacrifice => "Blood Sacrifice",

            SpecialEffectType.ExtraCoin => "Extra Coin",
            SpecialEffectType.ExtraEXP => "Extra EXP",
            SpecialEffectType.ExtraLoot => "Extra Loot",
            SpecialEffectType.LuckyDrop => "Lucky Drop",
            SpecialEffectType.AutoCollect => "Auto Collect",
            SpecialEffectType.Magnet => "Magnet",
            SpecialEffectType.GoldPerKill => "Gold per Kill",
            SpecialEffectType.DamagePerGold => "Damage per Gold",
            SpecialEffectType.DamagePerKill => "Damage per Kill",
            SpecialEffectType.DamagePerWave => "Damage per Wave",
            SpecialEffectType.DamagePerTier => "Damage per Tier",
            SpecialEffectType.DamagePerMissingHP => "Damage per Missing HP",
            SpecialEffectType.AdaptiveDamage => "Adaptive Damage",

            SpecialEffectType.UltimateDamage => "Ultimate Damage",
            SpecialEffectType.SkillCooldownReduction => "Skill CDR",
            SpecialEffectType.BossExecute => "Boss Execute",
            SpecialEffectType.ComboMultiplier => "Combo Multiplier",
            SpecialEffectType.PetDamageBoost => "Pet Damage Boost",

            SpecialEffectType.InstantKillChance => "Instant Kill Chance",
            SpecialEffectType.Revive => "Revive",
            SpecialEffectType.AutoRepair => "Auto Repair",

            _ => "Unknown"
        };

        /// <summary>
        /// Gets the effect category for UI grouping.
        /// </summary>
        public static EffectCategory GetCategory(this SpecialEffectType type) => type switch
        {
            // Healing
            SpecialEffectType.HealEverySecond or SpecialEffectType.AutoHeal or
            SpecialEffectType.CriticalHeal or SpecialEffectType.LifeConversion or
            SpecialEffectType.PhoenixRevival or SpecialEffectType.Revive => EffectCategory.Healing,

            // DoT
            SpecialEffectType.BurnEnemy or SpecialEffectType.FreezeEnemy or
            SpecialEffectType.Poison or SpecialEffectType.Shock or
            SpecialEffectType.Bleed or SpecialEffectType.Curse => EffectCategory.DamageOverTime,

            // Trigger
            SpecialEffectType.ExplosionOnKill or SpecialEffectType.ChainLightning or
            SpecialEffectType.Meteor or SpecialEffectType.IceSpike or
            SpecialEffectType.FireAura or SpecialEffectType.ChainExplosion or
            SpecialEffectType.Ricochet or SpecialEffectType.Pierce or
            SpecialEffectType.Bounce or SpecialEffectType.SplitProjectile or
            SpecialEffectType.MultiShot or SpecialEffectType.CriticalExplosion or
            SpecialEffectType.DoubleProjectile or SpecialEffectType.TripleProjectile => EffectCategory.Triggered,

            // Summon
            SpecialEffectType.SummonSkeleton or SpecialEffectType.SummonWolf or
            SpecialEffectType.SummonDrone or SpecialEffectType.SummonTotem or
            SpecialEffectType.SummonTurret or SpecialEffectType.OrbitSword or
            SpecialEffectType.OrbitShield => EffectCategory.Summoning,

            // Defensive
            SpecialEffectType.ReflectDamage or SpecialEffectType.ShieldEvery10Seconds or
            SpecialEffectType.ReflectProjectile or SpecialEffectType.Invincible or
            SpecialEffectType.DamageReductionAura or SpecialEffectType.AdaptiveDefense or
            SpecialEffectType.AutoRepair => EffectCategory.Defensive,

            // Mobility
            SpecialEffectType.DashAttack or SpecialEffectType.Teleport or
            SpecialEffectType.PullEnemy or SpecialEffectType.PushEnemy or
            SpecialEffectType.Knockback or SpecialEffectType.SlowAura or
            SpecialEffectType.TimeSlow or SpecialEffectType.TimeFast => EffectCategory.Mobility,

            // CC
            SpecialEffectType.Fear or SpecialEffectType.Stealth or
            SpecialEffectType.Camouflage or SpecialEffectType.GhostForm => EffectCategory.CrowdControl,

            // Buff/Debuff
            SpecialEffectType.RandomBuff or SpecialEffectType.RandomDebuff or
            SpecialEffectType.Blessing or SpecialEffectType.Berserk or
            SpecialEffectType.Rage or SpecialEffectType.BloodSacrifice => EffectCategory.BuffDebuff,

            // Economy
            SpecialEffectType.ExtraCoin or SpecialEffectType.ExtraEXP or
            SpecialEffectType.ExtraLoot or SpecialEffectType.LuckyDrop or
            SpecialEffectType.AutoCollect or SpecialEffectType.Magnet or
            SpecialEffectType.GoldPerKill or SpecialEffectType.DamagePerGold or
            SpecialEffectType.DamagePerKill or SpecialEffectType.DamagePerWave or
            SpecialEffectType.DamagePerTier or SpecialEffectType.DamagePerMissingHP or
            SpecialEffectType.AdaptiveDamage => EffectCategory.Economy,

            // Ultimate/Skill
            SpecialEffectType.UltimateDamage or SpecialEffectType.SkillCooldownReduction or
            SpecialEffectType.BossExecute or SpecialEffectType.ComboMultiplier or
            SpecialEffectType.PetDamageBoost => EffectCategory.Ultimate,

            // Unique
            SpecialEffectType.InstantKillChance => EffectCategory.Unique,

            _ => EffectCategory.Other
        };

        /// <summary>
        /// Checks if the effect is valid (not None).
        /// </summary>
        public static bool IsValid(this SpecialEffectType type) => type != SpecialEffectType.None;

        /// <summary>
        /// Gets all valid effect types (excludes None).
        /// </summary>
        public static SpecialEffectType[] GetAllTypes() =>
            (SpecialEffectType[])Enum.GetValues(typeof(SpecialEffectType));
    }

    /// <summary>
    /// Effect categories for UI grouping and filtering.
    /// </summary>
    public enum EffectCategory
    {
        None = 0,
        Healing = 1,
        DamageOverTime = 2,
        Triggered = 3,
        Summoning = 4,
        Defensive = 5,
        Mobility = 6,
        CrowdControl = 7,
        BuffDebuff = 8,
        Economy = 9,
        Ultimate = 10,
        Unique = 11,
        Other = 12,
    }
}