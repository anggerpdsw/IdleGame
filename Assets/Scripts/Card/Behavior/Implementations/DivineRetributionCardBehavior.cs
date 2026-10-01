using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DivineRetributionCardBehavior : CardBehaviorBase
    {
        private readonly Queue<(float Time, float Damage)> _recentDamage = new();
        private readonly List<EnemyAi> _enemyBuffer = new();
        private float _windowDamage;
        private float _cooldownRemaining;

        public override CardEffectType EffectType => CardEffectType.DivineRetribution;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _recentDamage.Clear();
            _windowDamage = 0f;
            _cooldownRemaining = 0f;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _recentDamage.Clear();
            _windowDamage = 0f;
            _cooldownRemaining = 0f;
        }

        public override void OnPlayerDamaged(float damage, float currentHp, float maxHp)
        {
            if (damage <= 0f || maxHp <= 0f || _cooldownRemaining > 0f) return;

            float now = Time.time;
            float windowSeconds = GetParameter("DamageWindowSeconds");
            ExpireDamageBefore(now - windowSeconds);
            _recentDamage.Enqueue((now, damage));
            _windowDamage += damage;

            float threshold = maxHp * GetParameter("DamageThresholdPercent") * 0.01f;
            if (_windowDamage < threshold) return;

            _recentDamage.Clear();
            _windowDamage = 0f;
            _cooldownRemaining = GetParameter("CooldownSeconds");
            TriggerBlast();
        }

        public override void Update(float deltaTime)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            ExpireDamageBefore(Time.time - GetParameter("DamageWindowSeconds"));
        }

        private void ExpireDamageBefore(float earliestTime)
        {
            while (_recentDamage.Count > 0 && _recentDamage.Peek().Time < earliestTime)
                _windowDamage -= _recentDamage.Dequeue().Damage;
        }

        private void TriggerBlast()
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            float damage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage)
                * GetCurrentValue() * 0.01f;
            if (damage <= 0f) return;

            EnemySpatialGrid.CopyAllActiveEnemiesTo(_enemyBuffer);
            var damageData = new DamageData(
                damage,
                DamageType.Normal,
                CriticalType.None,
                DamageSource.Player.ToString());

            foreach (var enemy in _enemyBuffer)
            {
                if (enemy != null && enemy.CurrentHealth > 0f)
                    enemy.TakeDamage(damageData);
            }
            _enemyBuffer.Clear();
        }
    }
}
