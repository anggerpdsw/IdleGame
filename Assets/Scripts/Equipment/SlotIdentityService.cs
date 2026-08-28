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
    /// AttackDamage (from STR), HealthRegen (from CON), ManaPoint/ManaRegen (from INT),
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

        /// <summary>
        /// Recommended secondary stats for an equipment slot.
        /// Contains only stats defined by SecondaryStat and intended as equipment specialization.
        /// MainAttribute-derived stats should NOT be listed here.
        /// </summary>
        public static SecondaryStat[] GetRecommendedSecondaries(EquipmentType slot) => slot switch
        {
            // Hat: INT/CON specialization
            // Focus: cooldown, economy, boss/elite damage
            EquipmentType.Hat => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.GoldGain,
                SecondaryStat.DropRate,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage
            },

            // Gloves: STR/DEX specialization
            // Focus: physical attack mechanics
            EquipmentType.Gloves => new[]
            {
                SecondaryStat.CriticalDamage,
                SecondaryStat.BounceChance,
                SecondaryStat.BounceCount,
                SecondaryStat.MultiShootChance,
                SecondaryStat.MultiShootCount,
                SecondaryStat.KnockbackForce,
                SecondaryStat.StuntChance,
                SecondaryStat.StuntDuration,
                SecondaryStat.DefenseBreak
            },

            // Cape: DEX/INT specialization
            // Focus: mobility, cooldown, accuracy
            EquipmentType.Cape => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.CooldownReduction,
                SecondaryStat.HitRate,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage
            },

            // Armor: CON/STR specialization
            // Focus: sustain + offensive physical specialization
            EquipmentType.Armor => new[]
            {
                SecondaryStat.LifeSteal,
                SecondaryStat.DefenseBreak,
                SecondaryStat.KnockbackForce,
                SecondaryStat.StuntChance,
                SecondaryStat.StuntDuration,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage
            },

            // Belt: CON specialization
            // Focus: sustain + economy
            EquipmentType.Belt => new[]
            {
                SecondaryStat.LifeSteal,
                SecondaryStat.GoldGain,
                SecondaryStat.DropRate,
                SecondaryStat.InterestWave
            },

            // Pants: CON/DEX specialization
            // Focus: mobility + control/utility
            EquipmentType.Pants => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.HitRate,
                SecondaryStat.KnockbackForce,
                SecondaryStat.StuntChance,
                SecondaryStat.StuntDuration
            },

            // Shoes: DEX specialization
            // Focus: mobility + accuracy + attack utility
            EquipmentType.Shoes => new[]
            {
                SecondaryStat.MoveSpeed,
                SecondaryStat.HitRate,
                SecondaryStat.CooldownReduction,
                SecondaryStat.KnockbackForce,
                SecondaryStat.StuntChance
            },

            // Pendant: INT specialization
            // Focus: cooldown + elemental damage + boss/elite specialization
            EquipmentType.Pendant => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage,
                SecondaryStat.FireDamageBonus,
                SecondaryStat.WaterDamageBonus,
                SecondaryStat.LightningDamageBonus,
                SecondaryStat.MetalDamageBonus,
                SecondaryStat.WoodDamageBonus,
                SecondaryStat.EarthDamageBonus,
                SecondaryStat.WindDamageBonus
            },

            // Ring: STR specialization
            // Focus: direct physical damage
            EquipmentType.Ring => new[]
            {
                SecondaryStat.CriticalDamage,
                SecondaryStat.DefenseBreak,
                SecondaryStat.BossDamage,
                SecondaryStat.EliteDamage,
                SecondaryStat.LifeSteal
            },

            // Earring: INT/DEX specialization
            // Focus: cooldown + accuracy + multi-shot
            EquipmentType.Earring => new[]
            {
                SecondaryStat.CooldownReduction,
                SecondaryStat.HitRate,
                SecondaryStat.MultiShootChance,
                SecondaryStat.MultiShootCount,
                SecondaryStat.DropRate
            },

            // Bracelet: DEX/INT specialization
            // Focus: projectile mechanics + cooldown
            EquipmentType.Bracelet => new[]
            {
                SecondaryStat.BounceChance,
                SecondaryStat.BounceCount,
                SecondaryStat.MultiShootChance,
                SecondaryStat.MultiShootCount,
                SecondaryStat.CooldownReduction
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
            for (int i = 0; i < primaries.Length; i++) names[i] = primaries[i].GetMainShortName();
            return string.Join("/", names);
        }
    }
}