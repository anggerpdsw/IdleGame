using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Applies status effect to target.
    /// Integrates with existing EnemyStatusEffectController.
    /// </summary>
    public class ApplyStatusAction : IAction
    {
        private readonly StatusEffectType _statusType;
        private readonly float _duration;
        private readonly float _value;

        public ApplyStatusAction(StatusEffectType statusType, float duration, float value)
        {
            _statusType = statusType;
            _duration = duration;
            _value = value;
        }

        public bool Execute(IBehaviorContext context, ActionModifierData modifiers)
        {
            if (!context.HasValidTarget) return false;
            if (!context.CurrentTarget.TryGetComponent<Enemy.EnemyAi>(out var enemy)) return false;

            var controller = enemy.GetComponent<Enemy.EnemyStatusEffectController>();
            if (controller == null) return false;

            // Apply duration/potency modifiers
            float finalDuration = _duration * modifiers.StatusDurationMultiplier;
            float finalValue = _value * modifiers.StatusPotencyMultiplier;

            // Create appropriate status effect based on type
            IStatusEffect effect = CreateStatusEffect(_statusType, finalDuration, finalValue);
            if (effect == null) return false;

            controller.AddEffect(effect);
            return true;
        }

        private IStatusEffect CreateStatusEffect(StatusEffectType type, float duration, float value)
        {
            // Factory method for status effects
            // Integrates with existing ConcreteStatusEffects classes
            return type switch
            {
                StatusEffectType.Slow => new SlowStatus(duration, value),
                StatusEffectType.Stun => new StunStatus(duration),
                StatusEffectType.DefenseBreak => new DefenseBreakStatus(duration, value),
                StatusEffectType.HeartBreak => new HeartBreakStatus(duration, value),
                // Add more as needed
                _ => null
            };
        }
    }
}
