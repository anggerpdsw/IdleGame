using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Ultimate
{
    public class CloudInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Cloud.ToString();
        [Header("Damage")]
        [Tooltip("Radius of the cloud effect")]
        [SerializeField] private float _cloudRadius = 0.915f;
        [Tooltip("Delay between damage ticks (in seconds)")]
        [SerializeField] private float _damageTickInterval = 0.71f;

        [Header("Visual/Audio")]
        [Tooltip("Particle effect prefab for the cloud")]
        [SerializeField] private GameObject _cloudEffect;
        [Tooltip("Audio clip for cloud damage")]
        [SerializeField] private AudioClip _cloudSoundClip;
        [SerializeField] private float _soundVolume = 0.25f;

        // Runtime references
        private Player.Player _player;
        private float _spawnTime;
        private float _nextDamageTime;
        private int _enemyLayerMask;
        private bool _isActive = true;

        private DamageData damageData;
        private float _cloudDamage;
        private float _cloudDuration;
        private float _ultimateWeaponAttack;

        // Track enemies currently slowed by this cloud
        private readonly HashSet<EnemyAi> _slowedEnemies = new();

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _nextDamageTime = Time.time + _damageTickInterval;
        }

        public void Initialize(Player.Player player, UltimateData cloudData)
        {
            _player = player;
            _spawnTime = Time.time;
            _nextDamageTime = Time.time;
            
            // from cloudData → damageData
            _ultimateWeaponAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateWeaponAttack);
            _cloudDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * cloudData.GetDamageMultiplier() * _ultimateWeaponAttack;
            _cloudDuration = cloudData.GetDuration();
            
            damageData = new(_cloudDamage, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = cloudData.GetElement(),
                SlowPercent = cloudData.GetSlowPercent()
            };

            SpawnCloudEffects();
        }

        private void Update()
        {
            if (!_isActive) return;

            // Check if duration has expired
            if (Time.time - _spawnTime >= _cloudDuration)
            {
                Expire();
                return;
            }

            // Apply damage and slow on tick interval
            if (Time.time >= _nextDamageTime)
            {
                ApplyEffectsToEnemies();
                _nextDamageTime = Time.time + _damageTickInterval;
            }
        }

        /// <summary>
        /// Apply damage and slow to all enemies in the cloud radius.
        /// </summary>
        private void ApplyEffectsToEnemies()
        {
            if (_player == null) return;

            // Find all enemies in radius
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, _cloudRadius, _enemyLayerMask);

            // Track which enemies are currently inside the cloud
            HashSet<EnemyAi> enemiesInCloud = new();

            foreach (Collider2D col in hitEnemies)
            {
                if (col.TryGetComponent(out EnemyAi enemy))
                {
                    enemiesInCloud.Add(enemy);

                    // Attack Enemies
                    enemy.TakeDamage(damageData, false);

                    // Apply slow if not already slowed
                    if (!_slowedEnemies.Contains(enemy))
                    {
                        enemy.ApplySlow(SlowSource.Cloud, SlowType.Temporary, damageData.SlowPercent); 
                        _slowedEnemies.Add(enemy);
                    }
                }
            }

            // Remove slow from enemies that left the cloud
            _slowedEnemies.RemoveWhere(enemy =>
            {
                if (enemy == null || !enemiesInCloud.Contains(enemy))
                {
                    enemy?.RemoveSlow(SlowSource.Cloud);
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// Handle cloud expiry and cleanup all slow effects.
        /// </summary>
        private void Expire()
        {
            if (!_isActive) return;
            _isActive = false;

            // Remove slow from all affected enemies before destroying
            foreach (var enemy in _slowedEnemies)
                if (enemy != null) enemy.RemoveSlow(SlowSource.Cloud);
            _slowedEnemies.Clear();

            // Notify factory when cloud is destroyed
            UltimateFactory.OnInstanceDestroyed(UltimateID);

            // Destroy the cloud
            Destroy(gameObject);
        }

        /// <summary>
        /// Spawn visual and audio effects at cloud location.
        /// </summary>
        private void SpawnCloudEffects()
        {
            // Show effect if assigned
            if (_cloudEffect != null) _cloudEffect.SetActive(true);

            // Play sound effect if assigned
            Utilityku.PlaySfx(_player.SfxSource, _cloudSoundClip, _soundVolume);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw cloud radius in editor
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f); // Green with transparency
            Gizmos.DrawWireSphere(transform.position, _cloudRadius);
        }
#endif

    }
}
