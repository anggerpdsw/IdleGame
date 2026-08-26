using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Ultimate
{
    public class FountainInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Fountain.ToString();
        [Header("Damage")]
        [Tooltip("Radius of the fountain effect")]
        [SerializeField] private float _fountainRadius = 0.915f;
        [Tooltip("Delay between damage ticks (in seconds)")]
        [SerializeField] private float _damageTickInterval = 0.71f;
        [Header("Visual/Audio")]
        [Tooltip("Particle effect prefab for the fountain")]
        [SerializeField] private GameObject _fountainEffect;
        [Tooltip("Audio clip for fountain damage")]
        [SerializeField] private AudioClip _fountainSoundClip;
        [SerializeField] private float _soundVolume = 1f;

        // Runtime references
        private Player.Player _player;
        private float _spawnTime;
        private int _enemyLayerMask;
        private bool _isActive = true;

        private DamageData damageData;
        private float _ultimateAttack;
        private float _fountainDamage;
        private float _fountainDuration;
        private float _stuntMultiplier;
        private float _stuntDuration;
        private float _nextDamageTime;

        // Track enemies currently stunted by this fountain
        private readonly HashSet<EnemyAi> _stuntedEnemies = new();

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _nextDamageTime = Time.time + _damageTickInterval;
            transform.localScale = new Vector3(3f, 3f, 1f);
        }

        public void Initialize(Player.Player player, UltimateData fountainData)
        {
            _player = player;
            _spawnTime = Time.time;
            _nextDamageTime = Time.time;
            
            // from fountainData → damageData
            _ultimateAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateAttack);
            _fountainDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * fountainData.GetDamageMultiplier() * _ultimateAttack;
            _fountainDuration = fountainData.GetDuration();
            _stuntMultiplier = fountainData.GetStuntMultiplier();
            _stuntDuration = PlayerStatsManager.Instance.GetStat(SkillType.StuntDuration) * _stuntMultiplier;

            damageData = new(_fountainDamage, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = fountainData.GetElement(),
                HasStunt = true,
                StuntMultiplier = _stuntMultiplier
            };

            SpawnFountainEffects();
        }

        private void Update()
        {
            if (!_isActive) return;

            // Check if duration has expired
            if (Time.time - _spawnTime >= _fountainDuration)
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
        /// Apply damage and stunt to all enemies in the fountain radius.
        /// </summary>
        private void ApplyEffectsToEnemies()
        {
            if (_player == null) return;

            // Find all enemies in radius
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, _fountainRadius, _enemyLayerMask);

            // Track which enemies are currently inside the fountain
            HashSet<EnemyAi> enemiesInFountain = new();

            foreach (Collider2D col in hitEnemies)
            {
                if (col.TryGetComponent(out EnemyAi enemy))
                {
                    enemiesInFountain.Add(enemy);

                    // Attack Enemies
                    enemy.TakeDamage(damageData, false);

                    // Apply stunt if not already stunted
                    if (!_stuntedEnemies.Contains(enemy))
                    {
                        enemy.ApplyStunt(_stuntDuration); 
                        _stuntedEnemies.Add(enemy);
                    }
                }
            }
        }

        /// <summary>
        /// Handle fountain expiry and cleanup all stunt effects.
        /// </summary>
        private void Expire()
        {
            if (!_isActive) return;
            _isActive = false;

            _stuntedEnemies.Clear();

            // Notify factory when ultimate is destroyed
            UltimateFactory.OnInstanceDestroyed(UltimateID);

            // Destroy the fountain
            Destroy(gameObject);
        }

        /// <summary>
        /// Spawn visual and audio effects at fountain location.
        /// </summary>
        private void SpawnFountainEffects()
        {
            // Show effect if assigned
            if (_fountainEffect != null) _fountainEffect.SetActive(true);

            // Play sound effect if assigned
            Utilityku.PlaySfx(_player.SfxSource, _fountainSoundClip, _soundVolume);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw fountain radius in editor
            Gizmos.color = GameColors.uncommonGreen.WithAlpha(0.3f); // Green with transparency
            Gizmos.DrawWireSphere(transform.position, _fountainRadius);
        }
#endif

    }
}
