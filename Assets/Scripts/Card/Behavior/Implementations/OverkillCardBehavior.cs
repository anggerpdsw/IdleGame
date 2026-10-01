
namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class OverkillCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.Overkill;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnPlayerAttack()
        {
            // Overkill logic is handled in Projectile/Player on kill
            // This behavior provides the transfer percentage via GetCurrentValue()
        }

        public float GetTransferPercent() => GetCurrentValue();
        public float GetMaxTransferMultiplier() => GetParameter("MaximumTransferMultiplier");
    }
}