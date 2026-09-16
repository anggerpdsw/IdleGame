namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Modifies action execution parameters (damage, duration, range, etc.).
    /// Applied before action execution to build final modifier data.
    /// </summary>
    public interface IModifier
    {
        void Apply(IBehaviorContext context, ActionModifierData data);
    }

    /// <summary>
    /// Accumulated modifier data passed to action execution.
    /// Modifiers write to this struct, action reads from it.
    /// </summary>
    public class ActionModifierData
    {
        // Damage modifiers
        public float DamageMultiplier = 1f;
        public float CriticalChanceBonus = 0f;
        public float CriticalDamageBonus = 0f;
        public float ExecuteThreshold = 0f; // Kill target below this HP%

        // Status modifiers
        public float StatusDurationMultiplier = 1f;
        public float StatusPotencyMultiplier = 1f;

        // Range/AOE modifiers
        public float RangeMultiplier = 1f;
        public float SplashRadius = 0f;

        // Special mechanics
        public bool EnableChain = false;
        public int ChainCount = 0;
        public float ChainDamageReduction = 0.5f;
        public float LifeStealPercent = 0f;
        public bool EnableFinisher = false;
        public float BerserkerHPThreshold = 0f;
        public float BerserkerDamageBonus = 0f;

        public void Reset()
        {
            DamageMultiplier = 1f;
            CriticalChanceBonus = 0f;
            CriticalDamageBonus = 0f;
            ExecuteThreshold = 0f;
            StatusDurationMultiplier = 1f;
            StatusPotencyMultiplier = 1f;
            RangeMultiplier = 1f;
            SplashRadius = 0f;
            EnableChain = false;
            ChainCount = 0;
            ChainDamageReduction = 0.5f;
            LifeStealPercent = 0f;
            EnableFinisher = false;
            BerserkerHPThreshold = 0f;
            BerserkerDamageBonus = 0f;
        }
    }
}
