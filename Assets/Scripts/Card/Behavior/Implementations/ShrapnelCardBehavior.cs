using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ShrapnelCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.Shrapnel;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack, CardEventType.OnCriticalHit };

        private float _damageMultiplier;
        private float _radius;
        private int _maximumTargets;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _damageMultiplier = GetParameter("DamageMultiplier", 0.35f);
            _radius = GetParameter("Radius", 2.5f);
            _maximumTargets = Mathf.RoundToInt(GetParameter("MaximumTargets", 4f));
        }

        public override void OnCriticalHit(bool isCriticalHit, float damage, Vector2 position)
        {
            if (!isCriticalHit) return;
            if (!Utilityku.Chance(GetCurrentValue())) return;
            SpawnShrapnel(position, damage);
        }

        private void SpawnShrapnel(Vector2 center, float baseDamage)
        {
            int enemyLayer = LayerMask.GetMask("Enemy");
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, _radius, enemyLayer);
            int targetsHit = 0;
            float shrapnelDamage = baseDamage * _damageMultiplier;

            foreach (var hit in hits)
            {
                if (targetsHit >= _maximumTargets) break;
                if (hit.TryGetComponent<EnemyAi>(out var enemy))
                {
                    if(enemy.CurrentHealth <= 0) return;
                    var damageData = new DamageData(shrapnelDamage, DamageType.Normal, CriticalType.None, "Shrapnel")
                    {
                        Element = Element.None
                    };
                    enemy.TakeDamage(damageData);
                    targetsHit++;
                }
            }
        }
    }
}