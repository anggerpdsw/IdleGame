using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using UnityEngine;

namespace IdleDefenseSurvival.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _debug;

        [Header("Configuration")]
        [Tooltip("Base spawn interval (seconds between spawns).")]
        [SerializeField] private float _spawnInterval = 1.47f;
        [SerializeField] private Player.Player _player;
        [SerializeField] private SpawnMode _spawnMode = SpawnMode.Circle;

        [Header("TEST")]
        [SerializeField] private bool _testMode;
        [SerializeField] private string _testSpecificID = "Vampire";
        [SerializeField] private Role _testRole = Role.Fighter;

        private EnemyDatabase EnemyDatabase => DatabaseJSONCache.DatabaseEnemy;
        private readonly Dictionary<Role, Transform> _roleParents = new();
        private float _timer;

        // Fractional reward accumulator.
        // Pecahan reward tidak hilang dan dibawa ke enemy berikutnya.
        private float _goldFraction;
        private float _meatFraction;

        // =========================================================
        // PUBLIC
        // =========================================================
        public float SpawnInterval
        {
            get => _spawnInterval;
            set => _spawnInterval = Mathf.Max(0.01f, value);
        }

        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================
        private void Awake()
        {
            CreateRoleParents();
        }

        private void Start()
        {
            WaveManager.Instance.RegisterSpawner(this);
            WaveManager.Instance.ApplySpawnData();
        }

        private void Update()
        {
            if (!CanSpawn()) return;
            _timer += Time.deltaTime;
            if (_timer < _spawnInterval) return;
            SpawnEnemy();
            _timer = 0f;
        }

        private void OnEnable()
        {
            WaveManager.OnRunCompleted += StopSpawn;
        }

        private void OnDisable()
        {
            WaveManager.OnRunCompleted -= StopSpawn;
        }

        // =========================================================
        // SPAWN
        // =========================================================
        public void SpawnEnemy()
        {
            EnemyData rawData = GetRandomEnemy();
            if (rawData == null) return;
            EnemyData spawnedEnemy = Utilityku.CreateScaledEnemy(rawData);
            SpawnEnemyInternal(spawnedEnemy, GetSpawnPosition());
        }

        /// <summary>
        /// Spawns a specific enemy with pre-scaled stats.
        /// Used by WaveManager for special spawns.
        /// Bypasses normal weight-based selection and wave restrictions.
        /// </summary>
        public void SpawnSpecificEnemy(EnemyData spawnedEnemy)
            => SpawnSpecificEnemy(spawnedEnemy, GetSpawnPosition());

        /// <summary>
        /// Spawns a specific enemy at a custom position.
        /// Used by Necromancer periodic spawn to summon Undeath.
        /// </summary>
        public void SpawnSpecificEnemy(EnemyData spawnedEnemy, Vector2 spawnPos)
        {
            if (spawnedEnemy == null || _player == null) return;
            SpawnEnemyInternal(spawnedEnemy, spawnPos);
        }

        private void SpawnEnemyInternal(EnemyData spawnedEnemy, Vector2 spawnPos)
        {
            if (spawnedEnemy == null) return;

            long goldReward = CalculateGoldReward(spawnedEnemy.health);
            long gemReward = CalculateGemReward();
            long meatReward = CalculateMeatReward(spawnedEnemy.health);

            GameObject prefab = EnemyResources.GetEnemyPrefab(spawnedEnemy.prefabName);
            if (prefab == null)
            {
                LogMissingPrefab(spawnedEnemy.prefabName);
                return;
            }

            GameObject enemy = InstantiateEnemy(prefab, spawnedEnemy, spawnPos);
            if (enemy == null) return;
            if (!TryInitializeEnemy(
                    enemy, spawnedEnemy,
                    goldReward, gemReward, meatReward,
                    spawnPos))
                return;

            AddNecroBehavior(spawnedEnemy, enemy);
            RegisterEnemy(enemy);
        }

        private GameObject InstantiateEnemy(
            GameObject prefab, EnemyData enemyData, Vector2 spawnPos)
        {
            Transform parent = _roleParents[enemyData.role];
            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            enemy.name = $"{enemyData.id}_{enemy.GetInstanceID()}";
            return enemy;
        }

        private bool TryInitializeEnemy(
            GameObject enemy, EnemyData enemyData,
            long goldReward, long gemReward, long meatReward,
            Vector2 spawnPos)
        {
            if (!enemy.TryGetComponent(out EnemyAi enemyAi))
            {
                if (_debug)
                    Debug.LogWarning("Enemy prefab Basic is missing EnemyAi component.");
                return false;
            }

            SetupEnemySprite(enemyAi, enemyData.id);
            SetupEnemyFacing(enemyAi, spawnPos);

            enemyAi.Initialize(enemyData, goldReward, gemReward, meatReward);
            return true;
        }

        private void SetupEnemySprite(EnemyAi enemyAi, string enemyId)
        {
            Sprite sprite = EnemyResources.GetEnemySprite(enemyId);
            if (sprite != null)
            {
                enemyAi.SetSprite(sprite);
                return;
            }
            if (_debug) Debug.LogWarning($"[EnemySpawner] Sprite '{enemyId}' not found.");
        }

        private void SetupEnemyFacing(EnemyAi enemyAi, Vector2 spawnPos)
            => enemyAi.SetFacing(spawnPos.x > _player.transform.position.x);

        private void AddNecroBehavior(EnemyData spawnedEnemy, GameObject enemy)
        {
            if (spawnedEnemy.id != Behavior.Necromancer.ToString()) return;
            if (enemy.TryGetComponent<NecromancerBehavior>(out var necroBehavior))
                necroBehavior.Initialize(spawnedEnemy, this);
        }

        private void RegisterEnemy(GameObject enemy)
        {
            if (enemy.TryGetComponent(out EnemyAi enemyAi))
                EnemyStatisticsManager.Instance?.Register(enemyAi);
        }

        // =========================================================
        // RANDOM ENEMY SELECTION
        // =========================================================
        private EnemyData GetRandomEnemy()
        {
            if (EnemyDatabase?.enemies == null || EnemyDatabase.enemies.Length == 0)
                return null;
            if (!_testMode) return GetRandomEnemyByWeight();
            return GetTestEnemy();
        }

        private EnemyData GetTestEnemy()
        {
            if (!string.IsNullOrEmpty(_testSpecificID))
                return GetEnemyById(_testSpecificID);
            return GetRandomEnemyByRole(_testRole);
        }

        private EnemyData GetRandomEnemyByRole(Role role)
        {
            float totalWeight = 0f;
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null || enemy.role != role) continue;
                totalWeight += enemy.spawnWeight;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null || enemy.role != role) continue;
                cumulativeWeight += enemy.spawnWeight;
                if (randomValue <= cumulativeWeight) return enemy;
            }

            return null;
        }

        private EnemyData GetRandomEnemyByWeight()
        {
            int currentWave = WaveManager.Instance.CurrentWave;
            int currentTier = WaveManager.Instance.CurrentTier;

            float totalWeight = CalculateEligibleWeight(currentWave, currentTier);
            if (totalWeight <= 0f) return null;
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);

            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (!IsEnemyEligible(enemy, currentWave, currentTier)) continue;
                randomValue -= enemy.spawnWeight;
                if (randomValue <= 0f) return enemy;
            }

            return null;
        }

        private float CalculateEligibleWeight(int currentWave, int currentTier)
        {
            float totalWeight = 0f;
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (!IsEnemyEligible(enemy, currentWave, currentTier)) continue;
                totalWeight += enemy.spawnWeight;
            }
            return totalWeight;
        }

        private bool IsEnemyEligible(EnemyData enemy, int currentWave, int currentTier)
        {
            if (enemy == null || enemy.spawnWeight <= 0f) return false;
            // Undeath hanya bisa muncul melalui summon Necromancer.
            if (enemy.role == Role.Undeath) return false;
            // Enemy baru aktif setelah tier requirement terpenuhi.
            if (currentTier <= enemy.minTier) return false;
            // Caster, Ranger, dan Boss belum boleh muncul pada early wave.
            if (currentWave <= 15 &&
                (enemy.role == Role.Caster ||
                 enemy.role == Role.Ranger ||
                 enemy.role == Role.BOSS))
                return false;
            return true;
        }

        private EnemyData GetEnemyById(string enemyId)
        {
            if (EnemyDatabase?.enemies == null) return null;
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null) continue;
                if (string.Equals(enemy.id, enemyId, StringComparison.OrdinalIgnoreCase))
                    return enemy;
            }
            return null;
        }

        // =========================================================
        // SPAWN POSITION
        // =========================================================
        private Vector2 GetSpawnPosition()
        {
            float radius =
                PlayerStatsManager.Instance.GetStat(SkillType.AttackRange)
                + WaveManager.Instance.SpawnBuffer;
            return _spawnMode switch
            {
                SpawnMode.Circle => GetCircleSpawn(radius),
                SpawnMode.FourSides => GetFourSideSpawn(radius),
                _ => GetCircleSpawn(radius)
            };
        }

        private Vector2 GetCircleSpawn(float radius)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            return (Vector2)_player.transform.position + direction * radius;
        }

        private Vector2 GetFourSideSpawn(float radius)
        {
            Vector2 center = _player.transform.position;
            float offset = UnityEngine.Random.Range(-radius, radius);
            return UnityEngine.Random.Range(0, 4) switch
            {
                // Top
                0 => center + new Vector2(offset, radius),
                // Bottom
                1 => center + new Vector2(offset, -radius),
                // Left
                2 => center + new Vector2(-radius, offset),
                // Right
                _ => center + new Vector2(radius, offset)
            };
        }

        // =========================================================
        // REWARDS
        // =========================================================
        /// <summary>
        /// Calculate gold reward based on tier and enemy health.
        /// </summary>
        private long CalculateGoldReward(float enemyHealth)
        {
            int tier = WaveManager.Instance.CurrentTier;
            float baseGold = 0.5f + tier * 2.5f;
            float hpBonus = Mathf.Pow(enemyHealth, 0.35f);
            float tierMultiplier = 1f + (tier - 1) * 0.15f;
            float rawGold = (baseGold + hpBonus) * tierMultiplier;
            float cardMultiplier = CardModifierService.GetEffectResult(CardEffectType.Gold, 1f);
            rawGold *= cardMultiplier;
            float equipGoldGain = PlayerStatsManager.Instance.GetStat(SkillType.GoldGain);
            rawGold *= 1f + equipGoldGain / 100f;
            rawGold *= Utilityku.DropRateIncrease(GameConstants.DROP_CHANCE_GOLD);
            // Tambahkan pecahan dari enemy sebelumnya.
            rawGold += _goldFraction;
            long gold = (long)Mathf.Floor(rawGold);
            // Simpan kembali bagian pecahannya.
            _goldFraction = rawGold - gold;
            return Math.Max(1, gold);
        }

        private long CalculateGemReward()
        {
            float dropChance = Utilityku.DropRateIncrease(GameConstants.DROP_CHANCE_GEM);
            if (Utilityku.Chance01(dropChance)) return 0;
            if (!CanEarnGem()) return 0;
            return 1;
        }

        private bool CanEarnGem()
        {
            var saveManager = ServiceLocator.SaveService as SaveManager;
            if (saveManager == null) return false;
            if (saveManager.HasReachedDailyGemLimit())
            {
                if (_debug) Debug.Log("[EnemySpawner] Daily gem limit reached.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Calculate meat reward.
        /// 1% chance to drop 1-2 meat, scaled by tier.
        /// </summary>
        private long CalculateMeatReward(float enemyHealth)
        {
            float dropChance = Utilityku.DropRateIncrease(GameConstants.DROP_CHANCE_MEAT);
            if (Utilityku.Chance01(dropChance)) return 0;
            int tier = WaveManager.Instance.CurrentTier;
            float hpBonus = Mathf.Pow(enemyHealth, 0.25f) * 0.08f;
            float rawMeat = 1f + tier * 0.35f + hpBonus;
            float meatDropMultiplier = CardModifierService.GetEffectResult(CardEffectType.Meat, 1f);
            rawMeat *= meatDropMultiplier;
            // Tambahkan pecahan dari enemy sebelumnya.
            rawMeat += _meatFraction;
            long meat = (long)Mathf.Floor(rawMeat);
            // Simpan kembali bagian pecahannya.
            _meatFraction = rawMeat - meat;
            return Math.Max(1, meat);
        }

        // =========================================================
        // ROLE PARENTS
        // =========================================================
        private void CreateRoleParents()
        {
            foreach (Role role in Enum.GetValues(typeof(Role)))
            {
                GameObject roleObject = new(role.ToString());
                roleObject.transform.SetParent(transform);
                _roleParents.Add(role, roleObject.transform);
            }
        }

        // =========================================================
        // VALIDATION / STATE
        // =========================================================
        private bool CanSpawn()
        {
            if (_player == null || EnemyDatabase == null) return false;
            return WaveManager.Instance.State == WaveState.ActiveWave;
        }

        private void LogMissingPrefab(string prefabName)
        {
            if (_debug) Debug.LogError($"Enemy prefab not found: Enemies/{prefabName}");
        }

        private void StopSpawn(VictoryData data) => enabled = false;
    }
}