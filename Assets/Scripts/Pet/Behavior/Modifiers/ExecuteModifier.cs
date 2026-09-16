namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Execute: Instantly kill target if HP below threshold.
    /// Example: Execute if target HP < 10%
    /// </summary>
    public class ExecuteModifier : IModifier
    {
        private readonly float _threshold;

        public ExecuteModifier(float threshold)
        {
            _threshold = threshold;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            if (!context.HasValidTarget) return;
            if (!context.CurrentTarget.TryGetComponent<Enemy.EnemyAi>(out var enemy)) return;

            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            if (hpPercent <= _threshold)
            {
                data.ExecuteThreshold = _threshold;
                // Action implementation should check ExecuteThreshold and deal lethal damage
                data.DamageMultiplier *= 999f; // Ensure kill
            }
        }
    }
}
