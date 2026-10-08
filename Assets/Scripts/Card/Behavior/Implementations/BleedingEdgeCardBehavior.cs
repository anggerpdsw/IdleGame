using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BleedingEdgeCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.BleedingEdge;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnCriticalHit };

        private float _bleedChancePercent;
        private float _durationSeconds;
        private float _damagePerSecondMultiplier;
        private int _maximumStacks;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _bleedChancePercent = GetParameter("BleedChancePercent", 25f);
            _durationSeconds = GetParameter("DurationSeconds", 4f);
            _damagePerSecondMultiplier = GetParameter("DamagePerSecondMultiplier", 0.12f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 3f));
        }

        public override void OnCriticalHit(bool isCriticalHit, float damage, Vector2 position)
        {
            if (!isCriticalHit) return;
            if (!Utilityku.Chance(_bleedChancePercent)) return;
            
            float finalDamage = damage * GetCurrentValue() * 0.01f;
            // Find enemy at hit position
            var colliders = Physics2D.OverlapCircleAll(position, 0.5f, LayerMask.GetMask("Enemy"));
            foreach (var col in colliders)
            {
                if (col == null) continue;
                if (col.TryGetComponent<EnemyAi>(out var enemy)) 
                    ApplyBleed(enemy, finalDamage);
            }
        }

        private void ApplyBleed(EnemyAi enemy, float finalDamage)
        {
            if (!enemy.TryGetComponent<EnemyStatusEffectController>(out var statusController)) return;

            float dps = finalDamage * _damagePerSecondMultiplier;
            int currentStacks = statusController.GetBleedStacks();

            if (currentStacks >= _maximumStacks)
            {
                statusController.RefreshBleed(_durationSeconds, dps);
            }
            else
            {
                statusController.AddBleed(_durationSeconds, dps);
            }
        }
    }
}
