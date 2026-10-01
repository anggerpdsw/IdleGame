using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class CelestialArsenalCardBehavior : CardBehaviorBase
    {
        private enum WeaponType
        {
            PiercingBeam,
            OrbitalStrike,
            ChainLightning
        }

        private readonly List<EnemyAi> _enemyBuffer = new();
        private readonly HashSet<EnemyAi> _hitEnemies = new();
        private readonly HashSet<WeaponType> _usedWeapons = new();
        private int _attackCount;
        private int _attacksPerSummon;

        public override CardEffectType EffectType => CardEffectType.CelestialArsenal;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attackCount = 0;
            _attacksPerSummon = Mathf.Max(1, Mathf.CeilToInt(GetParameter("AttacksPerSummon")));
            _usedWeapons.Clear();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attackCount = 0;
            _usedWeapons.Clear();
            _enemyBuffer.Clear();
            _hitEnemies.Clear();
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _attacksPerSummon = Mathf.Max(1, Mathf.CeilToInt(GetParameter("AttacksPerSummon")));
        }

        public override void OnPlayerAttack()
        {
            _attackCount++;
            float triggerRate = CardModifierService.GetProjectileEffectTriggerRateMultiplier();
            int interval = Mathf.Max(1, Mathf.CeilToInt(_attacksPerSummon / triggerRate));
            if (_attackCount < interval) return;

            _attackCount = 0;
            SummonWeapon((WeaponType)Random.Range(0, 3));
        }

        private void SummonWeapon(WeaponType weapon)
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            EnemySpatialGrid.CopyAllActiveEnemiesTo(_enemyBuffer);
            float range = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            Vector2 playerPosition = player.transform.position;
            if (!HasEnemyInRange(playerPosition, range))
            {
                _enemyBuffer.Clear();
                return;
            }

            _usedWeapons.Add(weapon);
            float damage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage)
                * (1f + _usedWeapons.Count * GetCurrentValue() * 0.01f);

            switch (weapon)
            {
                case WeaponType.PiercingBeam:
                    FireBeam(playerPosition, range, damage);
                    break;
                case WeaponType.OrbitalStrike:
                    FireOrbitalStrike(playerPosition, range, damage);
                    break;
                case WeaponType.ChainLightning:
                    FireChainLightning(playerPosition, range, damage);
                    break;
            }

            _enemyBuffer.Clear();
            _hitEnemies.Clear();
        }

        private bool HasEnemyInRange(Vector2 origin, float range)
        {
            float rangeSquared = range * range;
            foreach (var enemy in _enemyBuffer)
            {
                if (enemy == null) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (offset.sqrMagnitude <= rangeSquared) return true;
            }
            return false;
        }

        private void FireBeam(Vector2 origin, float range, float damage)
        {
            EnemyAi firstTarget = FindNearest(origin, range, null);
            if (firstTarget == null) return;

            Vector2 direction = ((Vector2)firstTarget.transform.position - origin).normalized;
            float beamWidth = GetParameter("BeamWidth");
            float rangeSquared = range * range;
            foreach (var enemy in _enemyBuffer)
            {
                if (enemy == null) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float alongBeam = Vector2.Dot(offset, direction);
                if (alongBeam <= 0f || alongBeam > range || offset.sqrMagnitude > rangeSquared) continue;
                float distanceFromBeam = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                if (distanceFromBeam <= beamWidth)
                    DealWeaponDamage(enemy, damage);
            }
        }

        private void FireOrbitalStrike(Vector2 origin, float range, float damage)
        {
            EnemyAi target = null;
            float highestHealth = 0f;
            float rangeSquared = range * range;
            foreach (var enemy in _enemyBuffer)
            {
                if (enemy == null || ((Vector2)enemy.transform.position - origin).sqrMagnitude > rangeSquared)
                    continue;
                if (enemy.CurrentHealth <= highestHealth) continue;
                highestHealth = enemy.CurrentHealth;
                target = enemy;
            }

            if (target == null) return;
            Vector2 strikePosition = target.transform.position;
            float radiusSquared = GetParameter("OrbitalStrikeRadius") * GetParameter("OrbitalStrikeRadius");
            foreach (var enemy in _enemyBuffer)
            {
                if (enemy != null && ((Vector2)enemy.transform.position - strikePosition).sqrMagnitude <= radiusSquared)
                    DealWeaponDamage(enemy, damage);
            }
        }

        private void FireChainLightning(Vector2 origin, float range, float damage)
        {
            int targetCount = Mathf.Max(1, Mathf.RoundToInt(GetParameter("ChainTargetCount")));
            float retention = Mathf.Clamp01(GetParameter("ChainDamageRetentionPercent") * 0.01f);
            EnemyAi target = FindNearest(origin, range, null);
            Vector2 previousPosition = origin;

            for (int chain = 0; chain < targetCount && target != null; chain++)
            {
                DealWeaponDamage(target, damage * Mathf.Pow(retention, chain));
                previousPosition = target.transform.position;
                target = FindNearest(previousPosition, range, _hitEnemies);
            }
        }

        private EnemyAi FindNearest(Vector2 origin, float range, HashSet<EnemyAi> excluded)
        {
            float bestDistanceSquared = range * range;
            EnemyAi closest = null;
            foreach (var enemy in _enemyBuffer)
            {
                if (enemy == null || enemy.CurrentHealth <= 0f || (excluded != null && excluded.Contains(enemy)))
                    continue;

                float distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                closest = enemy;
            }
            return closest;
        }

        private void DealWeaponDamage(EnemyAi enemy, float damage)
        {
            if (enemy == null || enemy.CurrentHealth <= 0f || !_hitEnemies.Add(enemy)) return;
            enemy.TakeDamage(new DamageData(damage, DamageType.Normal, CriticalType.None, DamageSource.Player.ToString()));
        }
    }
}
