using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class LawOfCollapseCardBehavior : CardBehaviorBase
    {
        private readonly List<EnemyAi> _enemyBuffer = new();
        private float _timer;

        public override CardEffectType EffectType => CardEffectType.LawOfCollapse;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _timer = 0f;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _timer = 0f;
            _enemyBuffer.Clear();
        }

        public override void Update(float deltaTime)
        {
            float interval = GetParameter("IntervalSeconds");
            if (interval <= 0f) return;

            _timer += deltaTime;
            while (_timer >= interval)
            {
                _timer -= interval;
                ApplyCollapse();
            }
        }

        private void ApplyCollapse()
        {
            EnemySpatialGrid.CopyAllActiveEnemiesTo(_enemyBuffer);
            EnemyAi target = null;
            float highestCurrentHealth = 0f;

            foreach (var enemy in _enemyBuffer)
            {
                if (enemy == null || enemy.CurrentHealth <= highestCurrentHealth) continue;
                target = enemy;
                highestCurrentHealth = enemy.CurrentHealth;
            }

            if (target != null)
            {
                float lossPercent = target.EnemyData != null && target.EnemyData.IsBoss
                    ? GetParameter("BossCurrentHealthLossPercent")
                    : GetCurrentValue();

                if (target.EnemyStatusEffect != null
                    && target.EnemyStatusEffect.GetEffect(StatusEffectType.Volatile) is VolatileStatus)
                    lossPercent *= GetParameter("MarkedDamageMultiplier");

                target.RemoveCurrentHealthFraction(lossPercent * 0.01f, "LawOfCollapse");
            }

            _enemyBuffer.Clear();
        }
    }
}
