using IdleDefenseSurvival.Core;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IdleDefenseSurvival.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private bool _debug = false;

        [Header("Configuration")]
        [Tooltip("Base spawn interval (seconds between spawns).")]
        [SerializeField] private float _spawnInterval = 1.47f;
        [SerializeField] private Player.Player _player;
        [SerializeField] private SpawnMode _spawnMode = SpawnMode.Circle;

        [Header("TEST")]
        [SerializeField] private bool _testMode = false;
        [SerializeField] private Role _testRole = Role.Fighter;

        private EnemyDatabase EnemyDatabase => DatabaseJSONCache.DatabaseEnemy;
        private float _timer;

        // Public properties for WaveManager integration
        public float SpawnInterval
        {
            get => _spawnInterval;
            set => _spawnInterval = Mathf.Max(0.01f, value); // Min 0.01s
        }

        private readonly Dictionary<Role, Transform> _roleParents = new();
        private void Awake()
        {
            foreach (Role role in System.Enum.GetValues(typeof(Role)))
            {
                GameObject go = new(role.ToString());
                go.transform.SetParent(transform);
                _roleParents.Add(role, go.transform);
            }
        }
        
        private void Start()
        {
            WaveManager.Instance.RegisterSpawner(this);
            WaveManager.Instance.ApplySpawnData();
        }

        private void Update()
        {
            if (_player == null || EnemyDatabase == null) return;
            if (WaveManager.Instance.State != WaveState.ActiveWave) return;

            _timer += Time.deltaTime;
            if (_timer >= _spawnInterval)
            {
                SpawnEnemy();
                _timer = 0f;
            }
        }

        public void SpawnEnemy()
        {
            EnemyData rawData = GetRandomEnemy();
            if (rawData == null) return;

            EnemyData spawnedEnemy = new()
            {
                id   = rawData.id,
                role = rawData.role,
                prefabName  = rawData.prefabName,
                attackRange = rawData.attackRange,
                attackSpeed = rawData.attackSpeed,
                damage      = rawData.damage * WaveManager.Instance.DamageMult,
                health      = rawData.health * WaveManager.Instance.HealthMult,
                moveSpeed   = rawData.moveSpeed * WaveManager.Instance.SpeedMult,
                spawnWeight = rawData.spawnWeight,
                knockback   = rawData.knockback,
                evasion     = rawData.evasion,
                element     = rawData.element,
                exp         = rawData.exp,
                dropItems   = rawData.dropItems
            };

            // Calculate rewards based on enemy stats and wave
            long goldReward = CalculateGoldReward(spawnedEnemy.health);
            long gemReward  = CalculateGemReward();
            long meatReward = CalculateMeatReward(spawnedEnemy.health);

            Vector2 spawnPos = GetSpawnPosition();

            GameObject prefab = EnemyResources.GetEnemyPrefab(spawnedEnemy.prefabName);

            if (prefab == null)
            {
                if (_debug) Debug.LogError($"Enemy prefab not found: Enemies/{spawnedEnemy.prefabName}");
                return;
            }

            Transform parent = _roleParents[spawnedEnemy.role];
            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            enemy.name = $"{spawnedEnemy.id}_{enemy.GetInstanceID()}";

            if (!enemy.TryGetComponent(out EnemyAi enemyAi))
            {
                if (_debug) Debug.LogWarning("Enemy prefab Basic is missing EnemyAi component.");
                return;
            }

            Sprite sprite = EnemyResources.GetEnemySprite(spawnedEnemy.id);
            if (sprite != null)
            {
                enemyAi.SetSprite(sprite);
            }
            else if (_debug)
            {
                Debug.LogWarning($"[EnemySpawner] Sprite '{spawnedEnemy.id}' not found.");
            }
            
            // Flip berdasarkan posisi player
            enemyAi.SetFacing(spawnPos.x > _player.transform.position.x);

            // Inisialisasi data enemy dengan rewards
            enemyAi.Initialize(spawnedEnemy, goldReward, gemReward, meatReward);

            // Register with statistics service
            EnemyStatisticsManager.Instance?.Register(enemyAi);
        }

        private Vector2 GetSpawnPosition()
        {
            float radius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) + WaveManager.Instance.SpawnBuffer;
            return _spawnMode switch
            {
                SpawnMode.Circle => GetCircleSpawn(radius),
                SpawnMode.FourSides => GetFourSideSpawn(radius),
                _ => GetCircleSpawn(radius)
            };
        }

        private Vector2 GetCircleSpawn(float radius)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            return (Vector2)_player.transform.position + dir * radius;
        }

        private Vector2 GetFourSideSpawn(float radius)
        {
            Vector2 center = _player.transform.position;
            float offset = Random.Range(-radius, radius);
            return Random.Range(0, 4) switch
            {
                // Top
                0 => center + new Vector2(offset, radius),
                // Bottom
                1 => center + new Vector2(offset, -radius),
                // Left
                2 => center + new Vector2(-radius, offset),
                // Right
                _ => center + new Vector2(radius, offset),
            };
        }

        private EnemyData GetRandomEnemy()
        {
            if (EnemyDatabase == null || EnemyDatabase.enemies.Length == 0) return null;
            if (_testMode)
                return GetRandomEnemyByRole(_testRole);
            return GetRandomEnemyByWeight();
        }
                
        private EnemyData GetRandomEnemyByRole(Role role)
        {
            float totalWeight = 0f;
            // Hitung total weight hanya untuk role yang dipilih.
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null) continue;
                if (enemy.role != role) continue;
                totalWeight += enemy.spawnWeight;
            }

            // Weighted random hanya dari role yang dipilih.
            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;

            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null) continue;
                if (enemy.role != role) continue;
                cumulativeWeight += enemy.spawnWeight;
                if (randomValue <= cumulativeWeight)
                    return enemy;
            }

            return null;
        }

        private EnemyData GetRandomEnemyByWeight()
        {
            if (EnemyDatabase?.enemies == null) return null;
            int currentWave = WaveManager.Instance.CurrentWave;
            float totalWeight = 0f;
            // First pass: calculate weight of eligible enemies only.
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null || enemy.spawnWeight <= 0f) continue;
                if (currentWave <= 15 &&
                    (enemy.role == Role.Caster ||
                    enemy.role == Role.Ranger ||
                    enemy.role == Role.BOSS)) continue;
                totalWeight += enemy.spawnWeight;
            }
            if (totalWeight <= 0f) return null;
            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;
            // Second pass: weighted selection.
            foreach (EnemyData enemy in EnemyDatabase.enemies)
            {
                if (enemy == null || enemy.spawnWeight <= 0f) continue;
                if (currentWave <= 15 &&
                    (enemy.role == Role.Caster ||
                    enemy.role == Role.Ranger ||
                    enemy.role == Role.BOSS)) continue;
                cumulativeWeight += enemy.spawnWeight;
                if (randomValue <= cumulativeWeight) return enemy;
            }
            return null;
        }

        /// <summary>
        /// Calculate gold reward based on tier and enemy health.
        /// Tier-based system: 350 waves per tier, tier increases every tier completion.
        /// - Tier 1: ~1-2 gold (wave 1-350)
        /// - Tier 2: ~2-4 gold (wave 1-350, tier 2)
        /// - Tier 3: ~4-8 gold (wave 1-350, tier 3)
        /// Scaling uses tier as primary, with diminishing returns.
        /// </summary>
        private long CalculateGoldReward(float enemyHealth)
        {
            int tier = WaveManager.Instance.CurrentTier;
            // Base reward grows steadily with tier.
            float baseGold = 1f + tier * 2.5f;
            // Health contributes, but with diminishing returns.
            float hpBonus = Mathf.Pow(enemyHealth, 0.35f);
            // Additional tier scaling.
            float tierMultiplier = 1f + (tier - 1) * 0.15f;
            // Calculate final gold
            float rawGold = (baseGold + hpBonus) * tierMultiplier;
            float goldMultiplier = CardModifierService.GetEffectResult(CardEffectType.Gold, 1f);
            rawGold *= goldMultiplier;

            // Equipment GoldGain (percent from SecondaryStat.GoldGain -> SkillType.GoldGain)
            float equipGoldGain = PlayerStatsManager.Instance.GetStat(SkillType.GoldGain);
            rawGold *= 1f + equipGoldGain / 100f;

            rawGold *= 0.5f;
            return (long)System.Math.Max(1, Mathf.Floor(rawGold));
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

        private long CalculateGemReward()
        {
            if (Random.value > 0.0051f) return 0; // 0.51% drop chance
            if (!CanEarnGem()) return 0;
            return 1; // allocate 1 gem
        }

        /// <summary>
        /// Calculate meat reward (special resource).
        /// 2% chance to drop 1-2 meat, scaled by tier.
        /// </summary>
        private long CalculateMeatReward(float enemyHealth)
        {
            // Bonus kartu juga meningkatkan peluang drop.
            float dropChance = 0.02f;
            if (Random.value > dropChance) return 0;
            int tier = WaveManager.Instance.CurrentTier;
            float hpBonus = Mathf.Pow(enemyHealth, 0.25f) * 0.08f;
            float rawMeat = 1f + tier * 0.35f + hpBonus;
            long meat = (long)Mathf.Floor(rawMeat);
            float meatDropMultiplier = CardModifierService.GetEffectResult(CardEffectType.Meat, 1f);
            meat = (long)Mathf.Floor(meat * meatDropMultiplier);
            return System.Math.Max(1, meat);
        }

        private void StopSpawn(VictoryData data) => enabled = false;
        private void OnEnable() => WaveManager.OnRunCompleted += StopSpawn;
        private void OnDisable() => WaveManager.OnRunCompleted -= StopSpawn;

    }
}
