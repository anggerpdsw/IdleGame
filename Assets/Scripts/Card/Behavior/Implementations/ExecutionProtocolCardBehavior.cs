
namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ExecutionProtocolCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.ExecutionProtocol;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnPlayerAttack()
        {
            // The actual execution logic is in Player/Projectile on hit
            // This behavior provides the chance value via GetCurrentValue()
        }

        public float GetExecutionChance() => GetCurrentValue();
        public float GetBossExecutionThreshold() => GetParameter("BossExecutionThreshold");
        public float GetNormalExecutionThreshold() => GetParameter("NormalExecutionThreshold");
    }
}