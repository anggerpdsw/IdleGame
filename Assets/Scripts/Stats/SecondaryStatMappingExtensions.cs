namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Maps SecondaryStat (equipment) to SkillType (player stat system).
    /// Enum member names differ, so a plain cast would silently mis-wire.
    /// Stats without a SkillType counterpart map to None (skipped by builders).
    /// </summary>
    public static class SecondaryStatMappingExtensions
    {
    /// <summary>
    /// Maps SecondaryStat (equipment specialization) to SkillType (player stat system).
    /// The 80% derived layer comes from MainAttribute (see MainAttributeExtensions).
    /// Only specialization stats route through here; derived stats (AttackDamage,
    /// HealthPoint, CriticalDamage, ...) come from Main Attribute, not equipment.
    /// </summary>
    public static SkillType ToSkillType(this SecondaryStat stat) => stat switch
    {
        // Projectile / Multi / Crowd Control — map 1:1 to SkillType
        SecondaryStat.AttackRange => SkillType.AttackRange,
        SecondaryStat.BounceChance => SkillType.BounceChance,
        SecondaryStat.BounceCount => SkillType.BounceCount,
        SecondaryStat.MultiShootChance => SkillType.MultiShootChance,
        SecondaryStat.MultiShootCount => SkillType.MultiShootCount,
        SecondaryStat.KnockbackChance => SkillType.KnockbackChance,
        SecondaryStat.StuntChance => SkillType.StuntChance,
        SecondaryStat.StuntDuration => SkillType.StuntDuration,
        SecondaryStat.DefenseBreak => SkillType.DefenseBreak,

        // Sustain
        SecondaryStat.LifeSteal => SkillType.LifeSteal,

        // Utility / PvE / Economy
        SecondaryStat.DamagePerRange => SkillType.DamagePerRange,
        SecondaryStat.CooldownReduction => SkillType.CooldownReduction,
        SecondaryStat.MoveSpeed => SkillType.MoveSpeed,
        SecondaryStat.BossDamage => SkillType.BossDamage,
        SecondaryStat.EliteDamage => SkillType.EliteDamage,
        SecondaryStat.GoldGain => SkillType.GoldGain,
        SecondaryStat.DropRate => SkillType.DropRate,
        SecondaryStat.InterestWave => SkillType.InterestWave,

        // Accuracy (specialization-only, counters enemy Evasion)
        SecondaryStat.HitRate => SkillType.HitRate,
        SecondaryStat.Penetration => SkillType.Penetration,

        // Element damage bonus — per-element percent from equipment rolls
        SecondaryStat.MetalDamageBonus    => SkillType.MetalDamageBonus,
        SecondaryStat.WoodDamageBonus     => SkillType.WoodDamageBonus,
        SecondaryStat.FireDamageBonus     => SkillType.FireDamageBonus,
        SecondaryStat.WaterDamageBonus    => SkillType.WaterDamageBonus,
        SecondaryStat.EarthDamageBonus    => SkillType.EarthDamageBonus,
        SecondaryStat.LightningDamageBonus => SkillType.LightningDamageBonus,
        SecondaryStat.WindDamageBonus     => SkillType.WindDamageBonus,

        _ => SkillType.None
    };
    }
}