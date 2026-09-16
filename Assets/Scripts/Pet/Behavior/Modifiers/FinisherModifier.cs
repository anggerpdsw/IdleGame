namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Finisher: Bonus damage when target HP is below threshold.
    /// Example: +50% damage when target HP < 30%
    /// </summary>
    public class FinisherModifier : IModifier
    {
        private readonly float _hpThreshold;
        private readonly float _damageBonus;

        public FinisherModifier(float hpThreshold, float damageBonus)
        {
            _hpThreshold = hpThreshold;
            _damageBonus = damageBonus;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            if (!context.HasValidTarget) return;
            if (!context.CurrentTarget.TryGetComponent<Enemy.EnemyAi>(out var enemy)) return;

            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            if (hpPercent <= _hpThreshold)
            {
                data.DamageMultiplier *= (1f + _damageBonus);
                data.EnableFinisher = true;
            }
        }
    }
}
