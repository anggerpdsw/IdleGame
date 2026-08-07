using System;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Data-driven 11-slot identity. Each slot owns one/two focus attributes and a
    /// recommended secondary list. Source of truth for build guidance + rare-drops
    /// metagame. Rarity mechanics (secondary/socket/passive counts) come from
    /// RarityMechanicConfig, not here.
    ///
    /// Only specialization stats (SecondaryStat) appear here — derived stats like
    /// AttackDamage (from STR), HealthRegen (from CON), SkillDamage (from INT),
    /// CriticalDamage (from DEX), etc. are NOT listed — they come from Main Attributes.
    /// </summary>
    public static class SlotIdentityService
    {
        // Primary attributes per slot (per design: Main Attribute Weight per Slot)
        private static readonly MainAttribute[] HatAttrs      = { MainAttribute.Intelligence, MainAttribute.Constitution };
        private static readonly MainAttribute[] GlovesAttrs   = { MainAttribute.Strength, MainAttribute.Dexterity };
        private static readonly MainAttribute[] CapeAttrs     = { MainAttribute.Dexterity, MainAttribute.Intelligence };
        private static readonly MainAttribute[] ArmorAttrs    = { MainAttribute.Constitution, MainAttribute.Strength };
        private static readonly MainAttribute[] BeltAttrs     = { MainAttribute.Constitution };
        private static readonly MainAttribute[] PantsAttrs    = { MainAttribute.Constitution, MainAttribute.Dexterity };
        private static readonly MainAttribute[] ShoesAttrs    = { MainAttribute.Dexterity };
        private static readonly MainAttribute[] PendantAttrs  = { MainAttribute.Intelligence };
        private static readonly MainAttribute[] RingAttrs     = { MainAttribute.Strength };
        private static readonly MainAttribute[] EarringAttrs  = { MainAttribute.Intelligence, MainAttribute.Dexterity };
        private static readonly MainAttribute[] BraceletAttrs = { MainAttribute.Dexterity, MainAttribute.Intelligence };

        private static readonly SecondaryStat[] none = Array.Empty<SecondaryStat>();

        /// <summary>Focus attributes for a slot. Empty when slot is None/unknown.</summary>
        public static MainAttribute[] GetPrimaryAttributes(EquipmentType slot) => slot switch
        {
            EquipmentType.Hat       => HatAttrs,
            EquipmentType.Gloves    => GlovesAttrs,
            EquipmentType.Cape      => CapeAttrs,
            EquipmentType.Armor     => ArmorAttrs,
            EquipmentType.Belt      => BeltAttrs,
            EquipmentType.Pants     => PantsAttrs,
            EquipmentType.Shoes     => ShoesAttrs,
            EquipmentType.Pendant   => PendantAttrs,
            EquipmentType.Ring      => RingAttrs,
            EquipmentType.Earring   => EarringAttrs,
            EquipmentType.Bracelet  => BraceletAttrs,
            _ => Array.Empty<MainAttribute>()
        };

        /// <summary>Recommended secondary stats for a slot (specialization only, from SecondaryStat enum).</summary>
        public static SecondaryStat[] GetRecommendedSecondaries(EquipmentType slot) => slot switch
        {
            // Hat: INT/CON main → Specialization: CooldownReduction, GoldGain, DropRate
            EquipmentType.Hat => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.GoldGain,
                SecondaryStat.DropRate
            },

            // Gloves: STR/DEX main → Specialization: AttackRange, BounceChance, KnockbackChance, MultiShootChance
            EquipmentType.Gloves => new[]
            {
                SecondaryStat.AttackRange,
                SecondaryStat.BounceChance,
                SecondaryStat.KnockbackChance,
                SecondaryStat.MultiShootChance
            },

            // Cape: DEX/INT main → Specialization: MoveSpeed, CooldownReduction, DropRate
            EquipmentType.Cape => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.CooldownReduction,
                SecondaryStat.DropRate
            },

            // Armor: CON/STR main → Specialization: LifeSteal, BossDamage, EliteDamage, DamagePerRange
            // (Defense moved to Damage Reduction% / Armor Penetration Resistance — not in SecondaryStat)
            EquipmentType.Armor => new[]
            {
                SecondaryStat.LifeSteal,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage,
                SecondaryStat.DamagePerRange
            },

            // Belt: CON main → Specialization: LifeSteal, GoldGain, DropRate
            // (HealthRegen comes from CON, not SecondaryStat)
            EquipmentType.Belt => new[]
            {
                SecondaryStat.LifeSteal,
                SecondaryStat.GoldGain,
                SecondaryStat.DropRate
            },

            // Pants: CON/DEX main → Specialization: MoveSpeed, DamagePerRange, CooldownReduction
            // (Evasion comes from DEX → replace with Slow Resistance not in enum; HealthRegen from CON)
            EquipmentType.Pants => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.DamagePerRange,
                SecondaryStat.CooldownReduction
            },

            // Shoes: DEX main → Specialization: MoveSpeed, AttackRange, CooldownReduction
            EquipmentType.Shoes => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.AttackRange,
                SecondaryStat.CooldownReduction
            },

            // Pendant: INT main → Specialization: CooldownReduction, BossDamage, DropRate
            // (SkillDamage, ElementDamage, UltimateAttack come from INT — not secondary)
            EquipmentType.Pendant => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.BossDamage,
                SecondaryStat.DropRate
            },

            // Ring: STR main → Specialization: BossDamage, EliteDamage, BounceCount
            // (CriticalDamage from DEX → Critical Damage replaced with Armor Penetration / Execute Damage not in enum)
            EquipmentType.Ring => new[]
            {
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage,
                SecondaryStat.BounceCount
            },

            // Earring: INT/DEX main → Specialization: CooldownReduction, DropRate, MultiShootCount
            // (ElementDamage comes from INT)
            EquipmentType.Earring => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.DropRate,
                SecondaryStat.MultiShootCount
            },

            // Bracelet: DEX/INT main → Specialization: BounceCount, MultiShootCount
            EquipmentType.Bracelet => new[]
            {
                SecondaryStat.BounceCount,
                SecondaryStat.MultiShootCount
            },

            _ => none
        };

        /// <summary>True if the stat belongs to this slot's recommended set.</summary>
        public static bool IsRecommendedForSlot(SecondaryStat stat, EquipmentType slot) =>
            Array.IndexOf(GetRecommendedSecondaries(slot), stat) >= 0;

        /// <summary>Human-readable identity, e.g. "Magic ✦ INT/CON". Unknown → empty.</summary>
        public static string GetIdentityLabel(EquipmentType slot)
        {
            var primaries = GetPrimaryAttributes(slot);
            if (primaries.Length == 0) return "";

            var names = new string[primaries.Length];
            for (int i = 0; i < primaries.Length; i++) names[i] = primaries[i].GetShortName();
            return string.Join("/", names);
        }
    }
}