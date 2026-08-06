
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
    public enum Element { None, Metal, Wood, Fire, Water, Earth, Lightning, Wind }
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
    /// Basic skills Player
    /// </summary>
    public enum SkillType
    {
        None,
        AttackRange, AttackSpeed, AttackDamage, BounceChance, BounceCount,
        BounceSearchRadius, CriticalChance, CriticalFactor, DamagePerRange, KnockbackChance,
        KnockbackForce, LifeSteal, MultiShootChance, MultiShootCount, StuntChance,
        StuntDuration, SuperCriticalChance, SuperCriticalFactor, UltimateWeaponAttack,
        UltraCriticalChance, UltraCriticalFactor,
        SkillDamage, ElementDamage,   // main-attribute derived stats (Intelligence)

        DeathDefy, DefenseAmount, EvasionChance, HealthPoint, HealthRegen,

        InterestWave
    }

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
	
    
    /// <summary>
    /// Item rarity tiers - each with associated visual properties and gameplay multipliers.
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
    /// Equipment type - matches EquipmentType exactly.
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

}