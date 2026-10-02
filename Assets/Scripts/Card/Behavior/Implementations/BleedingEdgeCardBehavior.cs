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

        public void TryApplyBleed(EnemyAi enemy, float attackDamage, bool isCriticalHit)
        {
            if (!isCriticalHit || enemy == null) return;
            if (!Utilityku.Chance(_bleedChancePercent)) return;
            ApplyBleed(enemy, attackDamage);
        }

        private void ApplyBleed(EnemyAi enemy, float baseDamage)
        {
            if (!enemy.TryGetComponent<EnemyStatusEffectController>(out var statusController)) return;

            float dps = baseDamage * _damagePerSecondMultiplier;
            int currentStacks = statusController.GetBleedStacks();

            if (currentStacks >= _maximumStacks)
            {
                // Refresh duration instead of adding new stack
                statusController.RefreshBleed(_durationSeconds, dps);
            }
            else
            {
                statusController.AddBleed(_durationSeconds, dps);
            }
        }
    }
}