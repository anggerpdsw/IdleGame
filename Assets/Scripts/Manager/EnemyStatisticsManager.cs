using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// High-performance statistics service for active enemies.
    /// Uses dirty-flag caching: recalculates only when enemy composition/stats change.
    /// Designed for 1000+ concurrent enemies.
    /// </summary>
    public class EnemyStatisticsManager : MonoBehaviour
    {
        private static EnemyStatisticsManager _instance;
        public static EnemyStatisticsManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------
        // Active enemy registry
        // -------------------------------------------------------------------
        private readonly HashSet<EnemyAi> _activeEnemies = new();
        private bool _isDirty = true;

        // -------------------------------------------------------------------
        // Cached statistics
        // -------------------------------------------------------------------
        private float _cachedAvgHealth;
        private float _cachedAvgAttack;
        private float _cachedAvgDefense;
        private float _cachedAvgSpeed;
        private float _cachedAvgEvasion;
        private EnemyAi _cachedStrongest;
        private EnemyAi _cachedWeakest;
        private int _cachedAliveCount;
        private int _cachedBossCount;

        // -------------------------------------------------------------------
        // Public API: Registration
        // -------------------------------------------------------------------
        public void Register(EnemyAi enemy)
        {
            if (enemy == null || _activeEnemies.Contains(enemy)) return;
            _activeEnemies.Add(enemy);
            _isDirty = true;
        }

        public void Unregister(EnemyAi enemy)
        {
            if (enemy == null) return;
            _activeEnemies.Remove(enemy);
            _isDirty = true;
        }

        // -------------------------------------------------------------------
        // Public API: Statistics queries (auto-recalculate if dirty)
        // -------------------------------------------------------------------
        public float GetAverageHealth() { EnsureCalculated(); return _cachedAvgHealth; }
        public float GetAverageAttack() { EnsureCalculated(); return _cachedAvgAttack; }
        public float GetAverageDefense() { EnsureCalculated(); return _cachedAvgDefense; }
        public float GetAverageSpeed() { EnsureCalculated(); return _cachedAvgSpeed; }
        public float GetAverageEvasion() { EnsureCalculated(); return _cachedAvgEvasion; }
        public EnemyAi GetStrongestEnemy() { EnsureCalculated(); return _cachedStrongest; }
        public EnemyAi GetWeakestEnemy() { EnsureCalculated(); return _cachedWeakest; }
        public int GetAliveCount() { EnsureCalculated(); return _cachedAliveCount; }
        public int GetBossCount() { EnsureCalculated(); return _cachedBossCount; }

        // -------------------------------------------------------------------
        // Optional: subscribe to enemy stat changes
        // Call this from EnemyAi when its stats are modified at runtime
        // (e.g., after slow, defense break, max health reduction, heal, buff)
        // -------------------------------------------------------------------
        public void MarkDirty() => _isDirty = true;

        // -------------------------------------------------------------------
        // Internal: calculation
        // -------------------------------------------------------------------
        private void EnsureCalculated()
        {
            if (!_isDirty) return;

            int count = _activeEnemies.Count;
            if (count == 0)
            {
                ResetCache();
                _isDirty = false;
                return;
            }

            float sumHealth = 0f, sumAttack = 0f, sumDefense = 0f, sumSpeed = 0f, sumEvasion = 0f;
            EnemyAi strongest = null;
            EnemyAi weakest = null;
            float maxHealth = float.MinValue;
            float minHealth = float.MaxValue;
            int bossCount = 0;

            // Single pass over active enemies — O(n), zero allocations
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;

                float health = enemy.CurrentHealth; // requires public property or method
                float attack = enemy.EnemyAttackDamage;
                float defense = enemy.DefenseAmount;
                float speed = enemy.MoveSpeed;
                float evasion = enemy.Evasion;

                sumHealth += health;
                sumAttack += attack;
                sumDefense += defense;
                sumSpeed += speed;
                sumEvasion += evasion;

                if (health > maxHealth) { maxHealth = health; strongest = enemy; }
                if (health < minHealth) { minHealth = health; weakest = enemy; }

                if (enemy.Role == Role.BOSS) bossCount++;
            }

            _cachedAvgHealth = sumHealth / count;
            _cachedAvgAttack = sumAttack / count;
            _cachedAvgDefense = sumDefense / count;
            _cachedAvgSpeed = sumSpeed / count;
            _cachedAvgEvasion = sumEvasion / count;
            _cachedStrongest = strongest;
            _cachedWeakest = weakest;
            _cachedAliveCount = count;
            _cachedBossCount = bossCount;

            _isDirty = false;
        }

        private void ResetCache()
        {
            _cachedAvgHealth = _cachedAvgAttack = _cachedAvgDefense = _cachedAvgSpeed = _cachedAvgEvasion = 0f;
            _cachedStrongest = _cachedWeakest = null;
            _cachedAliveCount = _cachedBossCount = 0;
        }

    }
}