using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class VolatileCoreCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.VolatileCore;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private int _killCount;
        private int _killThreshold;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _killCount = 0;
            _killThreshold = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _killCount = 0;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _killThreshold = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;

            _killCount += count;
            while (_killCount >= _killThreshold)
            {
                CreateVolatileCore();
                _killCount -= _killThreshold;
            }
        }

        private void CreateVolatileCore()
        {
            var player = Player.Player.Instance;
            if (player == null) return;

            var playerPos = player.transform.position;
            float radius = GetParameter("Radius", 2.5f);
            float damageMultiplier = GetParameter("DamagePerSecondMultiplier", 0.35f);

            var enemies = Physics2D.OverlapCircleAll(
                playerPos,
                radius,
                LayerMask.GetMask("Enemy"));

            float playerDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage);
            float damageAmount = playerDamage * damageMultiplier;

            foreach (var col in enemies)
            {
                if (col.TryGetComponent<EnemyAi>(out var enemy))
                {
                    var damageData = new DamageData(
                        damageAmount,
                        DamageType.Normal,
                        CriticalType.None,
                        DamageSource.Player.ToString());
                    enemy.TakeDamage(damageData);
                }
            }
        }
    }
}
