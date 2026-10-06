using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ChainReactionCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.ChainReaction;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            if (enemy == null) return;

            if (enemy.TryGetComponent<EnemyStatusEffectController>(out var controller)
                && controller.GetEffect(StatusEffectType.Volatile) is VolatileStatus volatileStatus
                && !volatileStatus.HasExploded)
            {
                OnVolatileEnemyDeath(enemy);
                return;
            }

            if (damageSource == DamageSource.Player.ToString())
                OnEnemyKilledChainReaction(enemy);
        }

        public void OnEnemyKilledChainReaction(EnemyAi deadEnemy)
        {
            float chancePercent = GetCurrentValue()
                * CardModifierService.GetProjectileEffectTriggerRateMultiplier();
            if (!Utilityku.Chance(chancePercent)) return;

            float radius = GetParameter("MarkRadius");
            float duration = GetParameter("VolatileDurationSeconds");
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                deadEnemy.transform.position,
                radius,
                LayerMask.GetMask("Enemy")
            );

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<EnemyAi>(out var target)) continue;
                if (target == deadEnemy || target.CurrentHealth <= 0) continue;
                if (!target.TryGetComponent<EnemyStatusEffectController>(out var controller)) continue;
                controller.AddEffect(new VolatileStatus(duration));
            }
        }

        public void OnVolatileEnemyDeath(EnemyAi deadEnemy)
        {
            if (!deadEnemy.TryGetComponent<EnemyStatusEffectController>(out var controller)) return;
            if (controller.GetEffect(StatusEffectType.Volatile) is not VolatileStatus volatileEffect || volatileEffect.HasExploded) return;

            volatileEffect.HasExploded = true;

            float explosionRadius = GetParameter("ExplosionRadius");
            float explosionDamagePercent = GetParameter("ExplosionDamageMultiplier");
            float volatileDuration = GetParameter("VolatileDurationSeconds");

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                deadEnemy.transform.position,
                explosionRadius,
                LayerMask.GetMask("Enemy")
            );

            float explosionDamage = deadEnemy.MaxHealth * explosionDamagePercent;
            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<EnemyAi>(out var target)) continue;
                if (target == deadEnemy || target.CurrentHealth <= 0) continue;

                var damageData = new DamageData(
                    explosionDamage,
                    DamageType.Normal,
                    CriticalType.None,
                    "ChainReactionExplosion"
                )
                {
                    Element = Element.Fire
                };
                target.TakeDamage(damageData);

                // Spread Volatile
                if (!target.TryGetComponent<EnemyStatusEffectController>(out var targetController)) continue;
                if (targetController.GetEffect(StatusEffectType.Volatile) is not VolatileStatus existingVolatile || !existingVolatile.HasExploded)
                {
                    targetController.AddEffect(new VolatileStatus(volatileDuration));
                }
            }
        }
    }
}