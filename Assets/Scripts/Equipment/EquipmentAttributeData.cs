using System;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Single attribute entry for equipment - stores only instance BaseValue.
    /// ValuePerLevel is resolved from database at runtime.
    /// </summary>
    [Serializable]
    public sealed class EquipmentAttributeEntry
    {
        public MainAttribute Attribute = MainAttribute.Constitution;
        public float BaseValue = 0f;

        public EquipmentAttributeEntry() { }

        public EquipmentAttributeEntry(MainAttribute attribute, float baseValue)
        {
            Attribute = attribute;
            BaseValue = baseValue;
        }
    }

    /// <summary>
    /// Stat bonus entry from enchantment - stores stat and base value.
    /// ValuePerLevel is resolved from database at runtime.
    /// </summary>
    [Serializable]
    public sealed class EnchantmentStatBonusEntry
    {
        public SecondaryStat Stat = SecondaryStat.None;
        public float BaseValue = 0f;
        public float ValuePerLevel = 0f;
        public SecondaryStatMode Mode = SecondaryStatMode.Flat;
        public bool IsPercent = false;

        public EnchantmentStatBonusEntry() { }

        public EnchantmentStatBonusEntry(SecondaryStat stat, float baseValue, float valuePerLevel = 0f, SecondaryStatMode mode = SecondaryStatMode.Flat, bool isPercent = false)
        {
            Stat = stat;
            BaseValue = baseValue;
            ValuePerLevel = valuePerLevel;
            Mode = mode;
            IsPercent = isPercent;
        }
    }

    /// <summary>
    /// Container for all attribute data on an equipment instance.
    /// Separates main attributes (CON/STR/INT/DEX) from secondary specialization stats.
    /// Enchantment stat bonuses are stored separately and merged with SecondAttribute at runtime.
    /// </summary>
    [Serializable]
    public sealed class EquipmentAttributeData
    {
        public EquipmentAttributeEntry[] MainAttribute = Array.Empty<EquipmentAttributeEntry>();
        public EquipmentAttributeEntry[] SecondAttribute = Array.Empty<EquipmentAttributeEntry>();
        public EnchantmentStatBonusEntry[] StatBonuses = Array.Empty<EnchantmentStatBonusEntry>();

        public EquipmentAttributeData() { }

        public EquipmentAttributeData(EquipmentAttributeEntry[] main, EquipmentAttributeEntry[] second)
        {
            MainAttribute = main ?? Array.Empty<EquipmentAttributeEntry>();
            SecondAttribute = second ?? Array.Empty<EquipmentAttributeEntry>();
        }

        public EquipmentAttributeData(EquipmentAttributeEntry[] main, EquipmentAttributeEntry[] second, EnchantmentStatBonusEntry[] bonuses)
        {
            MainAttribute = main ?? Array.Empty<EquipmentAttributeEntry>();
            SecondAttribute = second ?? Array.Empty<EquipmentAttributeEntry>();
            StatBonuses = bonuses ?? Array.Empty<EnchantmentStatBonusEntry>();
        }

        public bool HasAttributes =>
            (MainAttribute != null && MainAttribute.Length > 0) ||
            (SecondAttribute != null && SecondAttribute.Length > 0) ||
            (StatBonuses != null && StatBonuses.Length > 0);
    }
}