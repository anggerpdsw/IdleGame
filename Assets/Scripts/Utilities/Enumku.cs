
namespace IdleDefenseSurvival
{
    public enum MainAttribute { Constitution, Strength, Intelligence, Dexterity }
    public enum SceneState { CardCollection, Crafting, Game, Inventory, MainMenu }
    public enum LevelType { Alchemist, Blacksmith, Level }
    public enum CraftType { Equipment, Potion }
    public enum CriticalType { None, Critical, SuperCritical, UltraCritical }
    public enum CurrencyType { Gold, Gem, Meat }
    public enum DailyRewardState { Locked, Waiting, Claimable, Claimed, CompletedToday }
    public enum DamageType { Normal, Critical, Heal, Mana, Poison, Burn, Ice, TrueDamage, Miss }
    public enum ProjectileOwner { Player, Tank, Enemy }
    public enum RewardType { Gold, Gem, Meat, Exp, Card, Ticket, Energy, Item, Equipment, Hero }
    public enum Role { Fighter, Tank, Golem, Caster, Ranger, Agile, Beast, BOSS }
    public enum SpawnMode { Circle, FourSides }
    public enum UltimateDMG { Player, Void, Tank, Root, Bomb, Fountain, Cloud, Lightning, Shockwave }
    public enum WaveState { ActiveWave, Defeat, InterWave, Victory }
    /// <summary>
    /// Player build profiles. Steering auto-equip attribute weights (EquipmentAutoEquipService)
    /// and future per-build tuning. Compile-time-safe alternatives to a string.
    /// </summary>
    public enum BuildProfile { All, Tank, Warrior, Mage, Assassin }
    public enum ModifierMode { Flat, Percent }
    public enum ModifierSource { AccountLevel, Buff, Card, Equipment, Event, Pet, Quest, Skin, Upgrade, Ultimate, SkillTreeBonus }

    public enum DefenseBreakSource { None, Lightning, PlayerProjectile, TankProjectile }
    public enum DefenseBreakType { None, Aura, Permanent, Temporary}

    public enum SlowSource { Card, Cloud, Void, Lightning }
    public enum SlowType { Permanent, Temporary, Aura }


    /// <summary>
    /// Runtime status of a mission instance
    /// </summary>
    public enum MissionStatus { Active, Completed, Claimed, Cancelled }
    /// <summary>
    /// Mission progress event types for the event system
    /// </summary>
    public enum MissionEventType
    {
        EnemyKilled = 0,
        SpecificEnemyKilled = 1,
        CurrencyEarned = 2,
        WaveCompleted = 3,
        BossKilled = 4,
        Blacksmithing = 5
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
	
    public enum Element { None, Metal, Wood, Fire, Water, Earth, Lightning, Wind }
        
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
    /// Each potion item has one PotionType that determines what it is.
    /// </summary>
    public enum PotionType
    {
        None = 0,
        Health = 1,
        Mana = 2,
        Stamina = 3,
        DebuffCleanse = 4
    }

}