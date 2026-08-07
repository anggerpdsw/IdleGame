
namespace IdleDefenseSurvival
{
    public enum CardRarity { Common, Rare, Epic, Legendary, Mythic }
    public enum CriticalType { None, Critical, SuperCritical, UltraCritical,
        DoubleResult,
        BonusQuality,
        FreeExtraItem,
        Masterpiece
    }
    public enum CurrencyType { Gold, Gem, Meat }
    public enum DailyRewardState { Locked, Waiting, Claimable, Claimed, CompletedToday }
    public enum DamageType { Normal, Critical, Heal, Poison, Burn, Ice, TrueDamage, Miss }
    public enum ProjectileOwner { Player, Tank, Enemy }
    public enum RewardType { Gold, Gem, Meat, Exp, Card, Ticket, Energy, Item, Equipment, Hero }
    public enum Role { Fighter, Tank, Golem, Caster, Ranger, Agile, Beast, BOSS }
    public enum SceneState { CardCollection, Game, Inventory, MainMenu }
    public enum SpawnMode { Circle, FourSides }
    public enum UltimateDMG { Player, Void, Tank, Root, Bomb, Fountain, Cloud, Lightning, Shockwave }
    public enum WaveState { ActiveWave, Defeat, InterWave, Victory }
    public enum MainAttribute { Constitution, Strength, Intelligence, Dexterity }
    public enum ModifierMode { Flat, Percent }
    public enum ModifierSource { AccountLevel, Buff, Card, Equipment, Event, Pet, Quest, Skin, Upgrade, Ultimate }

    public enum DefenseBreakSource { Card, Void, Lightning }
    public enum DefenseBreakType { Permanent, Temporary, Aura }

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

        // Survival
        HealthPoint = 16,
        HealthRegen = 17,
        DefenseAmount = 18,
        LifeSteal = 19,
        DeathDefy = 20,
        Evasion = 21,

        // Magic
        SkillDamage = 22,
        ElementDamage = 23,
        UltimateAttack = 24,

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
    }

    
    /// <summary>
    /// Item rarity tiers - each with associated visual properties and gameplay stats.
    /// </summary>
    public enum ItemRarity
    {
        None = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6,
        Ancient = 7,
        Divine = 8,
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
    }

}