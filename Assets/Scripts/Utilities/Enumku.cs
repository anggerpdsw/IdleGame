
namespace IdleDefenseSurvival
{
    public enum CriticalType { None, Critical, SuperCritical, UltraCritical,
        DoubleResult,
        BonusQuality,
        FreeExtraItem,
        Masterpiece
    }
    public enum CurrencyType { Gold, Gem, Meat }
    public enum DailyRewardState { Locked, Waiting, Claimable, Claimed, CompletedToday }
    public enum DamageType { Normal, Critical, Heal, Mana, Poison, Burn, Ice, TrueDamage, Miss }
    public enum ProjectileOwner { Player, Tank, Enemy }
    public enum RewardType { Gold, Gem, Meat, Exp, Card, Ticket, Energy, Item, Equipment, Hero }
    public enum Role { Fighter, Tank, Golem, Caster, Ranger, Agile, Beast, BOSS }
    public enum SceneState { CardCollection, Crafting, Game, Inventory, MainMenu }
    public enum SpawnMode { Circle, FourSides }
    public enum UltimateDMG { Player, Void, Tank, Root, Bomb, Fountain, Cloud, Lightning, Shockwave }
    public enum WaveState { ActiveWave, Defeat, InterWave, Victory }
    public enum MainAttribute { Constitution, Strength, Intelligence, Dexterity }
    /// <summary>
    /// Player build profiles. Steering auto-equip attribute weights (EquipmentAutoEquipService)
    /// and future per-build tuning. Compile-time-safe alternatives to a string.
    /// </summary>
    public enum BuildProfile { All, Tank, Warrior, Mage, Assassin }
    public enum ModifierMode { Flat, Percent }
    public enum ModifierSource { AccountLevel, Buff, Card, Equipment, Event, Pet, Quest, Skin, Upgrade, Ultimate }

    public enum DefenseBreakSource { None, Lightning, PlayerProjectile, TankProjectile }
    public enum DefenseBreakType { None, Aura, Permanent, Temporary}

    public enum SlowSource { Card, Cloud, Void, Lightning }
    public enum SlowType { Permanent, Temporary, Aura }

    /// <summary>
    /// Card effect types - separate from SkillType (player stats).
    /// Used for special card behaviors like auras, on-hit effects, etc.
    /// </summary>
    public enum CardEffectType
    {
        None,
        Gold, Meat,
        FrostAura,            // Slows enemies in attack range (aura)
        TimeFast,
        Shield                // Grants shield up to % of max HP when at full HP
    }
	
    public enum Element { None, Metal, Wood, Fire, Water, Earth, Lightning, Wind }
    /// <summary>
    /// Basic skills Player - all combat runtime stats.
    /// Derived from MainAttribute (80%) + SecondaryStat specialization (20%).
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
        MultiShootChance = 10,
        MultiShootCount = 11,
        KnockbackChance = 12,
        KnockbackForce = 13,
        StuntChance = 14,
        StuntDuration = 15,
        DefenseBreak = 34,

        // Survival
        HealthPoint = 16,
        HealthRegen = 17,
        DefenseAmount = 18,
        LifeSteal = 19,
        DeathDefy = 20,
        Evasion = 21,

        // Magic
        /// <summary>Universal elemental power (final = 1 + ElementMastery/1000), boosted by Intelligence.</summary>
        ElementMastery = 23,
        UltimateAttack = 24,
        /// <summary>Maximum mana pool. Ultimates and mana-cost skills draw from this.</summary>
        ManaPoint = 47,
        /// <summary>Mana regenerated per second.</summary>
        ManaRegen = 48,

        // Element damage (Layer 3) — per-element bonus (percent, from equipment/card/buff)
        MetalDamageBonus = 40,
        WoodDamageBonus = 41,
        FireDamageBonus = 42,
        WaterDamageBonus = 43,
        EarthDamageBonus = 44,
        LightningDamageBonus = 45,
        WindDamageBonus = 46,

        // Economy
        InterestWave = 25,
        GoldGain = 26,
        DropRate = 27,

        // Utility
        MoveSpeed = 28,
        CooldownReduction = 29,
        BossDamage = 30,
        EliteDamage = 31,

        // Accuracy (specialization — from equipment/passive/buff/card, NOT main attributes)
        HitRate = 32,
        Penetration = 33,

    }

    /// <summary>
    /// Display helpers for SkillType (player runtime stats).
    /// Lives in this same file so it is always compiled alongside the enum;
    /// its namespace (IdleDefenseSurvival) is an enclosing namespace of every caller.
    /// </summary>
    public static class SkillTypeExtensions
    {
        public static string GetDisplayName(this SkillType stat) => stat switch
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
            SkillType.DefenseBreak => "Defense Break",
            SkillType.DropRate => "Drop Rate",
            SkillType.ElementMastery => "Element Mastery",
            SkillType.EliteDamage => "Elite Damage",
            SkillType.GoldGain => "Gold Gain",
            SkillType.HealthPoint => "Health Points",
            SkillType.HealthRegen => "Health Regen",
            SkillType.HitRate => "Hit Rate",
            SkillType.InterestWave => "Interest Wave",
            SkillType.KnockbackChance => "Knockback Chance",
            SkillType.KnockbackForce => "Knockback Force",
            SkillType.LifeSteal => "Life Steal",
            SkillType.ManaPoint => "Mana Points",
            SkillType.ManaRegen => "Mana Regen",
            SkillType.MoveSpeed => "Move Speed",
            SkillType.MultiShootChance => "Multi-Shot Chance",
            SkillType.MultiShootCount => "Multi-Shot Count",
            SkillType.StuntChance => "Stun Chance",
            SkillType.StuntDuration => "Stun Duration",
            SkillType.DefenseAmount => "Defense",
            SkillType.UltimateAttack => "Ultimate Attack",
            SkillType.EarthDamageBonus => "Earth Damage",
            SkillType.FireDamageBonus => "Fire Damage",
            SkillType.LightningDamageBonus => "Lightning Damage",
            SkillType.MetalDamageBonus => "Metal Damage",
            SkillType.WaterDamageBonus => "Water Damage",
            SkillType.WindDamageBonus => "Wind Damage",
            SkillType.WoodDamageBonus => "Wood Damage",
            _ => stat.ToString(),
        };
    }

    
    /// <summary>
    /// Universal rarity tier shared by Cards and Equipment.
    /// </summary>
    public enum Rarity
    {
        None = 0,
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5,
        Divine = 6,
    }

    /// <summary>
    /// Category for item in this game.
    /// </summary>
    public enum TabType
    {
        All = 0,
        Equipment = 1,
        Consumables = 2,
        Materials = 3,
        Gems = 4,
        Other = 5
    }

    /// <summary>
    /// Each equipment item has one EquipmentType that determines which slot it fits in.
    /// </summary>
    public enum EquipmentType
    {
        None = 0,
        Hat = 1,
        Gloves = 2,
        Cape = 3,
        Armor = 4,
        Belt = 5,
        Pants = 6,
        Pendant = 7,
        Ring = 8,
        Earring = 9,
        Bracelet = 10,
        Shoes = 11,
    }

    /// <summary>
    /// Secondary stats - specialization layer from equipment (the ~20% build identity).
    /// Core power comes from MainAttribute (CON/STR/INT/DEX) via derived SkillTypes,
    /// and SkillType feeds combat. SecondaryStat is pure specialization (build identity).
    /// No stat here is derivable from attributes — that avoids double-dipping.
    /// </summary>
    public enum SecondaryStat
    {
        None = 0,

        // Projectile
        AttackRange = 1,
        BounceChance = 2,
        BounceCount = 3,
        DefenseBreak = 29,

        // Multi Shot
        MultiShootChance = 4,
        MultiShootCount = 5,

        // Crowd Control
        KnockbackChance = 6,
        StuntChance = 7,
        StuntDuration = 8,

        // Sustain
        Evasion = 9,
        LifeSteal = 10,

        // Utility
        DamagePerRange = 11,
        CooldownReduction = 12,
        MoveSpeed = 13,
        UltimateAttack = 14,

        // PvE
        BossDamage = 15,
        EliteDamage = 16,

        // Economy
        GoldGain = 17,
        DropRate = 18,
        InterestWave = 19,

        // Accuracy — counters enemy Evasion. Specialization-only (equipment/passive/buff/card).
        HitRate = 20,
        Penetration = 28,

        // Element damage bonus (Layer 3) — per-element percent from equipment (Roll → ModifierSource.Equipment → SkillType).
        MetalDamageBonus = 21,
        WoodDamageBonus = 22,
        FireDamageBonus = 23,
        WaterDamageBonus = 24,
        EarthDamageBonus = 25,
        LightningDamageBonus = 26,
        WindDamageBonus = 27,
    }

}