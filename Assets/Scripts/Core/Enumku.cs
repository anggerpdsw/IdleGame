
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
	
}