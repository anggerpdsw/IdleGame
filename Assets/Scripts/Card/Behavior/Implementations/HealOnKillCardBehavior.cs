using IdleDefenseSurvival.Enemy;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public class HealOnKillCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.HealOnKill;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            if (enemy == null || damageSource != DamageSource.Player.ToString()) return;

            var player = PlayerClass.Instance;
            if (player == null) return;

            float healAmount = enemy.MaxHealth * GetCurrentValue() * 0.01f;
            if (healAmount <= 0f) return;
            player.Heal(healAmount);
        }
    }
}