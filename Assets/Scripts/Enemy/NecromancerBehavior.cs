using UnityEngine;
using IdleDefenseSurvival.Data;
using System.Collections;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Specialized behavior for Necromancer enemy.
    /// Handles:
    /// - Periodic spawn of 6 random Undeath enemies every 40s
    /// - Death → Grave spawn
    /// - Revive tracking (health multiplier accumulation)
    /// Attached dynamically when Necromancer is spawned.
    /// </summary>
    public class NecromancerBehavior : MonoBehaviour
    {
        [SerializeField] private GameObject _gravePrefab;
        [SerializeField] private EnemyAi _enemyAi;
        private EnemySpawner _spawner;

        // Periodic spawn state
        private Coroutine _periodicSpawnCoroutine;
        private float _periodicInterval = 40f;
        private int _spawnCount = 6;

        // Revive state (parsed from onDeath effect)
        private int _reviveCount = 0;
        private float _reviveHealthIncrementPercent = 30f; // default 30%
        private float _reviveDuration = 165f; // default 165s
        private float _baseMaxHealth = 0f; // Original max health at first spawn
        private float _baseDamage = 0f; // Original damage at first spawn
        private EnemyData _originalData; // Original data snapshot for full restore

        // Grave reference (set by death handler)
        private NecromancerGrave _activeGrave;

        private WaitForSeconds _periodicWait;
        private static readonly WaitForSeconds StaggerWait = new(0.05f);

        public int ReviveCount => _reviveCount;
        public bool HasActiveGrave => _activeGrave != null;

        /// <summary>
        /// Initialize from EnemyAi after enemy data is loaded.
        /// Parses onPeriodic and onDeath effects from EnemyData.
        /// </summary>
        public void Initialize(EnemyData data, EnemySpawner spawner)
        {
            _spawner = spawner;
            _originalData = data; // Save original data for revive restore
            _baseMaxHealth = _enemyAi.MaxHealth;
            _baseDamage = _enemyAi.EnemyAttackDamage;

            if (data.effects != null)
            {
                foreach (var effect in data.effects)
                {
                    if (effect == null) continue;

                    if (effect.onPeriodic != null)
                    {
                        foreach (var action in effect.onPeriodic)
                        {
                            if (action == null || action.effect != StatusEffects.StatusEffectType.Spawn) continue;
                            if (action.value > 0) _spawnCount = Mathf.RoundToInt(action.value);
                            if (action.duration > 0) _periodicInterval = action.duration;
                        }
                    }

                    if (effect.onDeath != null)
                    {
                        foreach (var action in effect.onDeath)
                        {
                            if (action == null || action.effect != StatusEffects.StatusEffectType.Revive) continue;
                            if (action.value > 0) _reviveHealthIncrementPercent = action.value;
                            if (action.duration > 0) _reviveDuration = action.duration;
                        }
                    }
                }
            }

            _periodicWait = new WaitForSeconds(_periodicInterval);
            StartPeriodicSpawn();
        }

        /// <summary>
        /// Start periodic spawn coroutine.
        /// Called after Initialize and after Revive.
        /// </summary>
        public void StartPeriodicSpawn()
        {
            if (_periodicSpawnCoroutine != null)
                StopCoroutine(_periodicSpawnCoroutine);
            _periodicSpawnCoroutine = StartCoroutine(PeriodicSpawnRoutine());
        }

        /// <summary>
        /// Stop periodic spawn (called on death).
        /// </summary>
        public void StopPeriodicSpawn()
        {
            if (_periodicSpawnCoroutine != null)
            {
                StopCoroutine(_periodicSpawnCoroutine);
                _periodicSpawnCoroutine = null;
            }
        }

        private IEnumerator PeriodicSpawnRoutine()
        {
            SpawnUndeathWave();
            yield return _periodicWait;

            while (true)
            {
                SpawnUndeathWave();
                yield return _periodicWait;
            }
        }

        private void SpawnUndeathWave()
        {
            if (_enemyAi == null) return;

            var pool = DatabaseJSONCache.UndeathSummonableEnemies;
            if (pool == null || pool.Length == 0)
            {
                _enemyAi.enabled = true;
                return;
            }

            _enemyAi.enabled = false;
            StartCoroutine(SpawnStaggered(_spawnCount));
        }

        private IEnumerator SpawnStaggered(int count)
        {
            if (_spawner == null) yield break;

            var waveManager = WaveManager.Instance;
            if (waveManager == null) yield break;

            Vector2 necroPos = transform.position;
            float scatterRadius = 2f;

            var pool = DatabaseJSONCache.UndeathSummonableEnemies;

            for (int i = 0; i < count; i++)
            {
                var baseData = pool[Random.Range(0, pool.Length)];

                var scaledData = Utilityku.CreateScaledEnemy(baseData, true);
                
                Vector2 randomOffset = Random.insideUnitCircle * scatterRadius;
                Vector2 spawnPos = necroPos + randomOffset;

                _spawner.SpawnSpecificEnemy(scaledData, spawnPos);

                yield return StaggerWait;
            }

            if (_enemyAi != null) _enemyAi.enabled = true;
        }

        /// <summary>
        /// Called by Grave when revive completes.
        /// Restores Necromancer with increased health and damage (compound growth).
        /// </summary>
        public void OnReviveComplete(Vector3 revivePosition)
        {
            _reviveCount++;

            // Compound growth per revive:
            // Revive 1: 100 * (1.30)^1 = 130
            // Revive 2: 100 * (1.30)^2 = 169
            // Revive 3: 100 * (1.30)^3 = 219.7
            float multiplierH = 1f + _reviveHealthIncrementPercent * 0.01f;
            float multiplierD = 1f + _reviveHealthIncrementPercent * 0.001f;
            float newMaxHealth = _baseMaxHealth * Mathf.Pow(multiplierH, _reviveCount);
            float newDamage = _baseDamage * Mathf.Pow(multiplierD, _reviveCount);

            // Restore all original stats from data (attack speed, move speed, etc.)
            if (_originalData != null)
            {
                _enemyAi.SetAttackSpeed(_originalData.attackSpeed);
                // Other immutable stats (defense, evasion, element) remain from initial load
            }

            // Apply compound growth to health and damage only
            _enemyAi.SetMaxHealthTo(newMaxHealth);
            _enemyAi.SetDamage(newDamage);

            // Restore to full health
            _enemyAi.Heal(newMaxHealth);

            // Move to revive position
            transform.position = revivePosition;

            // Re-enable enemy
            gameObject.SetActive(true);

            // Re-register with managers
            EnemyStatisticsManager.Instance?.Register(_enemyAi);
            UI.EnemyHealthBarManager.Instance?.RegisterEnemy(_enemyAi, newMaxHealth);

            // Re-register aura (Unregeneration automatically parsed from EnemyData)
            EnemyAuraManager.Instance?.RegisterEnemyAuraSource(_enemyAi);

            // Restart periodic spawn
            StartPeriodicSpawn();

            // Clear grave reference (grave destroys itself)
            _activeGrave = null;
        }

        public NecromancerGrave SpawnGrave(Vector3 deathPosition)
        {
            // Prevent duplicate graves - reuse existing if already spawned
            if (_activeGrave != null) return _activeGrave;

            StopPeriodicSpawn();

            // ponytail: use BehaviorPool for grave - single world-space canvas managed by pool
            if (_gravePrefab == null)
            {
                Debug.LogError("[NecromancerBehavior] No grave prefab assigned");
                return null;
            }

            var pool = BehaviorPool.Instance;
            if (pool == null)
            {
                Debug.LogError("[NecromancerBehavior] BehaviorPool not found in scene");
                return null;
            }

            var grave = pool.Get<NecromancerGrave>(_gravePrefab);
            if (grave == null)
            {
                Debug.LogError("[NecromancerBehavior] Failed to get grave from pool");
                return null;
            }

            grave.transform.position = deathPosition;
            grave.Initialize(this, _reviveDuration, deathPosition);
            _activeGrave = grave;
            return grave;
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void OnDisable()
        {
            StopPeriodicSpawn();

            // ponytail: grave must persist after enemy deactivation for revive timer
            // Cleanup handled in OnReviveComplete or when owner destroyed
        }

        /// <summary>
        /// Reset revive count for fresh Necromancer instance.
        /// Called if enemy is pooled and reused.
        /// </summary>
        public void ResetReviveCount()
        {
            _reviveCount = 0;
        }
    }
}
