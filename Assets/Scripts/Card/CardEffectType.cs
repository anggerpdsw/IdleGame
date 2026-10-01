namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Canonical enumeration of all card effect types.
    /// Used by CardData.EffectType for special mechanics.
    /// Stat modifiers use CardData.SkillType instead.
    /// Card effect types - separate from SkillType (player stats).
    /// Used for special card behaviors like auras, on-hit effects, etc.
    /// </summary>
    public enum CardEffectType
    {
        None,
        Gold, Meat,
        FrostAura,            // Slows enemies in attack range (aura)
        TimeFast,
        Shield,               // Grants shield up to % of max HP when at full HP
        Berserker,            // Increase AttackDamage based on missing HP (1% missing = 1% bonus, capped by card level)
        HealOnKill, BatStalker, // Heal player by % of enemy MaxHP when player kills enemy
        Immortal,             // Revive on death (DeathDefy fails) + immune 1 wave, cooldown per card level
        EnemyBalance,         // Reduce min spawn interval: more enemies per wave
        AddTank,              // +1 tank count, increase tank duration by card value%
        CrazyGambler,         // After wave 150: chance to +25% ATK or -15% ATK each wave (max 20 stack)
        Desperados,           // After wave 150: chance to +25% HP or -15% HP each wave (max 20 stack)
        DeathChain,           // Each kill within 2s → ATK +4%, stack up to 15x
        BulletStorm,          // Every 8 attacks → burst 5 projectiles
        ExecutionProtocol,    // Execute enemies below HP threshold
        Overkill,             // Excess damage transfers to nearest enemy
        VampiricFrenzy,       // Lifesteal → AtkSpeed bonus stacking
        GuardianInstinct,     // HP < 30% → shield + evasion, 30s CD
        CriticalCascade,      // Crit → 25% spawn extra projectile
        WarMachine,           // Continuous attack 5s → ATK/AS/CC bonus
        ApocalypseEngine,     // Every 50 kills → +1% ATK, +0.5% AS, +2% CRIT DMG (current battle)
        InfiniteArsenal,      // Every 12th attack → special projectile (pierce all, bounce 3x, 100% crit)
        SoulHarvester,        // Each kill → +1 Soul, each Soul +0.5% DMG, every 100 Souls +50 max stack
        DeathReversal,        // First death → rewind 5s (HP/pos), restore 50% HP, reset projectiles (once/wave)
        VoidOverlord,         // Every 100s → 15s state (pierce, +100% DMG, no enemy heal)
        ChainReaction,        // Kill → 20% mark nearby Volatile 5s, Volatile death explodes + spreads (once per enemy)
        WorldBreaker,        // Persistent kill milestones: damage and defense ignore
        DivineRetribution,   // Rolling HP-loss threshold triggers an all-enemy blast
        CelestialArsenal,    // Attack milestones summon distinct stacking weapon types
        LawOfCollapse,       // Periodic current-HP loss on the strongest enemy
        ApexEvolution,       // Player chooses a unique mutation at kill milestones
        MomentumCore,        // Kill streak grants attack speed stacking
        FractureMark,        // Mark enemy, next 3 hits deal bonus damage
        StormCharger,        // Every N attacks charge, release lightning to 3 enemies
        BloodEngine,         // Life steal above 80% HP converts to attack damage
        GravityWell,         // Every 6s pull nearby enemies and slow them
        HunterInstinct,      // Bonus damage to enemies below 35% HP
        EchoShot,            // Every 6th attack creates echo projectile
        AdrenalineLoop,      // Below 40% HP gain attack speed, kill restores HP
        VolatileCore,        // Every 12th kill leaves damaging core at death location
        ArcaneResonance,     // After Ultimate, next 8 attacks gain bonus damage and crit
        LastBullet           // Every 7th attack is enhanced with bonus damage and guaranteed hit

        // Stat Modifiers (handled via SkillType in Effects, not via this enum)
        // These are NOT used as EffectType - they use SkillType directly
    }
}