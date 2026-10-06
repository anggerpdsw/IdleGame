using UnityEngine;
using IdleDefenseSurvival.Enemy;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BatStalkerCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.BatStalker;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            if (enemy == null || damageSource != DamageSource.Player.ToString()) return;

            var player = PlayerClass.Instance;
            if (player == null) return;

            float healPercent = GetCurrentValue() * 0.01f;
            float healAmount = enemy.MaxHealth * healPercent;
            if (healAmount <= 0f) return;

            float missingHealth = player.MaxHealth - player.CurrentHealth;
            float overheal = Mathf.Max(0f, healAmount - missingHealth);

            // Apply heal
            player.Heal(healAmount);

            // Overheal grants temporary shield (50% of excess for 4s)
            if (overheal > 0f)
            {
                float shieldAmount = overheal * 0.5f;
                player.AddShield(shieldAmount, 4f);
            }
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
        }
    }
}
