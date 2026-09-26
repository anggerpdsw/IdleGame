namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Canonical enumeration of all card effect types.
    /// Used by CardDefinition.Effects[].Target for special mechanics.
    /// For stat modifiers, use SkillType enum instead.
    /// </summary>
    public enum CardEffectType
    {
        None = 0,

        // Economy / Resource
        Gold = 1,
        Meat = 2,

        // Aura / Zone Effects
        FrostAura = 10,
        Shield = 11,

        // Triggered Effects
        HealOnKill = 20,
        DeathChain = 21,
        BulletStorm = 22,
        ExecutionProtocol = 23,
        Overkill = 24,
        VampiricFrenzy = 25,
        GuardianInstinct = 26,
        CriticalCascade = 27,
        WarMachine = 28,
        ApocalypseEngine = 29,
        InfiniteArsenal = 30,
        SoulHarvester = 31,
        DeathReversal = 32,
        VoidOverlord = 33,
        ChainReaction = 34,

        // Wave / Time Effects
        TimeFast = 40,
        TimeWarp = 41,

        // Utility / Special
        AddTank = 50,
        Execution = 51,
        LastStand = 52,
        EnemyBalance = 53,
        CrazyGambler = 54,
        Desperados = 55,
        BatStalker = 56,

        // Immortality / Revival
        Immortal = 60,

        // Stat Modifiers (handled via SkillType in Effects, not via this enum)
        // These are NOT used as EffectType - they use SkillType directly
    }
}