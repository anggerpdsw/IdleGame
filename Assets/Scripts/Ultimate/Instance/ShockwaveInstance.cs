using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// ShockwaveInstance handles the actual shockwave behavior.
    /// It's an instant radial effect emanating from the player.
    /// This is the actual GameObject component, separate from ShockwaveHandler (factory).
    /// </summary>
    public class ShockwaveInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Shockwave.ToString();
        [Header("Effect Radius")]
        [Tooltip("Buffer distance beyond player attack range")]
        [SerializeField] private float _radiusBuffer = 2.3f;
        [Tooltip("Duration of the visual/audio effect")]
        [SerializeField] private float _effectDuration = 3f;

        [Header("Effect Visuals")]
        [Tooltip("Rotation speed of the shockwave effect (degrees per second)")]
        [SerializeField] private float _rotationSpeed = 500f;
        [Tooltip("Initial scale of the shockwave")]
        [SerializeField] private Vector3 _initialScale = Vector3.zero;
        [Tooltip("Scale multiplier for calculating final scale from attack range")]
        [SerializeField] private float _scaleMultiplier = 1.06f;

        [Header("Visual/Audio")]
        [Tooltip("Particle effect prefab for the shockwave")]
        [SerializeField] private GameObject _shockwaveEffect;
        [Tooltip("Audio clip for shockwave")]
        [SerializeField] private AudioClip _shockwaveSoundClip;
        [SerializeField] private float _soundVolume = 1f;

        // Runtime references
        private Player.Player _player;
        private float _spawnTime;
        private float _maxRadius;
        private float _currentRadius;
        private int _enemyLayerMask;
        private Vector3 _calculatedFinalScale;
        private DamageData damageData;
        private float _shockwaveDamage;
        private float _knockbackForce;
        private float _ultimateAttack;

        // Track enemies already hit to prevent multiple hits
        private readonly HashSet<EnemyAi> _hitEnemies = new();

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            transform.localScale = _initialScale;
        }

        /// <summary>
        /// Initialize shockwave with player reference and max radius.
        /// Called by ShockwaveHandler after instantiation.
        /// </summary>
        public void Initialize(Player.Player player, UltimateData shockwaveData)
        {
            _player = player;
            _spawnTime = Time.time;
            _maxRadius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) + _radiusBuffer;
            _currentRadius = 0f;

            // from shockwaveData → damageData
            _ultimateAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateAttack);
            _shockwaveDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * shockwaveData.GetDamageMultiplier() * _ultimateAttack;
            _knockbackForce = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackForce) * shockwaveData.GetKnockbackMultiplier();

            damageData = new(_shockwaveDamage, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = shockwaveData.GetElement(),
                HasKnockback = true,
                KnockbackForce = _knockbackForce,
            };

            // Calculate final scale based on max radius (diameter * scale multiplier)
            float diameter = _maxRadius * 2f;
            _calculatedFinalScale = new Vector3(diameter * _scaleMultiplier, diameter * _scaleMultiplier, 1f);

            // Spawn visual effects
            SpawnShockwaveEffects();
        }

        private void Update()
        {
            // Expand the radius visually
            float elapsedTime = Time.time - _spawnTime;
            float progress = Mathf.Clamp01(elapsedTime / _effectDuration);
            _currentRadius = progress * _maxRadius;

            // Apply scaling and rotation
            transform.localScale = Vector3.Lerp(_initialScale, _calculatedFinalScale, progress);
            transform.Rotate(0, 0, _rotationSpeed * Time.deltaTime);

            // Apply damage to enemies as they are hit by the expanding radius
            ApplyDamageToNewEnemies();

            // Check if effect duration has expired
            if (elapsedTime >= _effectDuration) Destroy(gameObject);
        }

        /// <summary>
        /// Apply damage and knockback to new enemies in the current shockwave radius.
        /// </summary>
        private void ApplyDamageToNewEnemies()
        {
            if (_player == null) return;

            // Find all enemies in current radius
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, _currentRadius, _enemyLayerMask);

            foreach (Collider2D col in hitEnemies)
            {
                if (col.TryGetComponent(out EnemyAi enemy) && !_hitEnemies.Contains(enemy))
                {
                    // Attack Enemies
                    enemy.TakeDamage(damageData, false);

                    // Apply knockback
                    Vector2 knockbackDirection = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
                    enemy.ApplyKnockback(knockbackDirection, damageData.KnockbackForce);

                    _hitEnemies.Add(enemy);
                }
            }
        }

        /// <summary>
        /// Spawn visual and audio effects at shockwave center.
        /// </summary>
        private void SpawnShockwaveEffects()
        {
            // Show effect if assigned
            if (_shockwaveEffect != null) _shockwaveEffect.SetActive(true);

            // Play sound effect if assigned
            Utilityku.PlaySfx(_player.SfxSource, _shockwaveSoundClip, _soundVolume);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw current radius in editor
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Cyan with transparency
            Gizmos.DrawWireSphere(transform.position, _currentRadius);

            // Draw max radius as dashed reference
            Gizmos.color = new Color(0f, 0.7f, 0.7f, 0.2f); // Darker cyan
            Gizmos.DrawWireSphere(transform.position, _maxRadius);
        }
#endif
    }
}
