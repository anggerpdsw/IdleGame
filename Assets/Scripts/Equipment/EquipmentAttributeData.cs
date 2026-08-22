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
    /// Container for all attribute data on an equipment instance.
    /// Separates main attributes (CON/STR/INT/DEX) from secondary specialization stats.
    /// </summary>
    [Serializable]
    public sealed class EquipmentAttributeData
    {
        public EquipmentAttributeEntry[] MainAttribute = Array.Empty<EquipmentAttributeEntry>();
        public EquipmentAttributeEntry[] SecondAttribute = Array.Empty<EquipmentAttributeEntry>();

        public EquipmentAttributeData() { }

        public EquipmentAttributeData(EquipmentAttributeEntry[] main, EquipmentAttributeEntry[] second)
        {
            MainAttribute = main ?? Array.Empty<EquipmentAttributeEntry>();
            SecondAttribute = second ?? Array.Empty<EquipmentAttributeEntry>();
        }

        public bool HasAttributes =>
            (MainAttribute != null && MainAttribute.Length > 0) ||
            (SecondAttribute != null && SecondAttribute.Length > 0);
    }
}