using System;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class StatModifier
    {
        public string Id;
        public ModifierSource Source;
        public SkillType Stat; // Legacy - for old system compatibility
        public SecondaryStat SecondaryStat = SecondaryStat.None; // New system
        public ModifierMode Mode;
        public float Value;
        public bool Permanent;
        public DateTime? ExpireUtc;

        // Helper to check which stat system is being used
        public bool UsesSecondaryStat => SecondaryStat != SecondaryStat.None;
        public bool UsesSkillType => Stat != SkillType.None;
    }
}
