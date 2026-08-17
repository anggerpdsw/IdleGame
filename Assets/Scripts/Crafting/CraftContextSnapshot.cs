using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Frozen value-only player state read by roll/reward (I-19).
    /// MUST NOT contain runtime refs, service refs, or live repository refs.
    ///</summary>
    [Serializable]
    public struct CraftContextSnapshot
    {
        public int PlayerLevel;
        public int ModifierVersion;
        public ModifierValue[] CraftingModifierValues;
        public ModifierValue[] RelevantStatValues;
        public ModifierValue[] RelevantCardModifierValues;

        public CraftContextSnapshot(
            int playerLevel,
            int modifierVersion,
            ModifierValue[] craftingModifierValues = null,
            ModifierValue[] relevantStatValues = null,
            ModifierValue[] relevantCardModifierValues = null)
        {
            PlayerLevel = playerLevel;
            ModifierVersion = modifierVersion;
            CraftingModifierValues = craftingModifierValues ?? Array.Empty<ModifierValue>();
            RelevantStatValues = relevantStatValues ?? Array.Empty<ModifierValue>();
            RelevantCardModifierValues = relevantCardModifierValues ?? Array.Empty<ModifierValue>();
        }
    }

    /// <summary>
    /// Source-tagged scalar value. No object references.
    ///</summary>
    [Serializable]
    public struct ModifierValue
    {
        public string SourceTag;   // "card:fire_aura", "upgrade:damage+5", etc.
        public float Value;
    }
}
