namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Berserker: Bonus damage when self HP is below threshold.
    /// Example: +100% damage when pet HP < 40%
    /// Note: Current implementation assumes pets have HP tracking (future feature)
    /// </summary>
    public class BerserkerModifier : IModifier
    {
        private readonly float _hpThreshold;
        private readonly float _damageBonus;

        public BerserkerModifier(float hpThreshold, float damageBonus)
        {
            _hpThreshold = hpThreshold;
            _damageBonus = damageBonus;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            // Pet HP system not yet implemented - use player HP as alternative
            float hpPercent = context.PlayerHealthPercent;

            if (hpPercent <= _hpThreshold)
            {
                data.DamageMultiplier *= (1f + _damageBonus);
                data.BerserkerHPThreshold = _hpThreshold;
                data.BerserkerDamageBonus = _damageBonus;
            }
        }
    }
}
