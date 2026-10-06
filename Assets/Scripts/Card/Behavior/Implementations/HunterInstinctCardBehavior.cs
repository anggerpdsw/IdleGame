using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class HunterInstinctCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.HunterInstinct;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        public float GetDamageMultiplier(EnemyAi target)
        {
            if (target == null) return 1f;

            float hpPercent = target.CurrentHealth / target.MaxHealth;
            float threshold = GetParameter("HealthThresholdPercent", 35f) * 0.01f;

            if (hpPercent <= threshold)
                return 1f + (GetCurrentValue() * 0.01f);

            return 1f;
        }
    }
}
