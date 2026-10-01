using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BatStalkerCardBehavior : HealOnKillCardBehavior
    {
        public override CardEffectType EffectType => CardEffectType.BatStalker;
    }
}