namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Maps MainStat (equipment) to SkillType (player stat system).
    /// Enum member names differ, so a plain cast would silently mis-wire.
    /// Stats without a SkillType counterpart map to None (skipped by builders).
    /// </summary>
    public static class MainStatMappingExtensions
    {
        public static SkillType ToSkillType(this MainStat stat) => stat switch
        {
            MainStat.HP => SkillType.HealthPoint,
            MainStat.Attack => SkillType.AttackDamage,
            MainStat.Defense => SkillType.DefenseAmount,
            MainStat.AttackSpeed => SkillType.AttackSpeed,
            MainStat.CriticalRate => SkillType.CriticalChance,
            MainStat.CriticalDamage => SkillType.CriticalFactor,
            MainStat.Range => SkillType.AttackRange,
            MainStat.LifeSteal => SkillType.LifeSteal,
            MainStat.Evasion => SkillType.EvasionChance,
            MainStat.Dodge => SkillType.EvasionChance,
            MainStat.HealthRegen => SkillType.HealthRegen,
            // MoveSpeed, Accuracy, ArmorPenetration, Magic*, CooldownReduction,
            // Mana, Projectile*, Luck, GoldGain, ExpGain, DropRate, damage-specific,
            // block/thorns/shield: no SkillType counterpart yet -> None (skipped).
            _ => SkillType.None
        };
    }
}