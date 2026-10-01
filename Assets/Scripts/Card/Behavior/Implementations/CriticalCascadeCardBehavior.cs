
namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class CriticalCascadeCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.CriticalCascade;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnPlayerAttack()
        {
            // Critical cascade logic handled in Projectile on crit
            // This behavior provides the cascade chance via GetCurrentValue()
        }

        public float GetCascadeChance() => GetCurrentValue();
    }
}