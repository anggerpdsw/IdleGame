using UnityEngine;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Bomb instance that handles the actual bomb behavior.
    /// Explodes on enemy collision or lifetime expiry.
    /// This is the actual GameObject component, separate from BombHandler (factory).
    /// </summary>
    public class BombInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Bomb.ToString();
        [Header("Lifetime")]
        [Tooltip("Time in seconds before bomb auto-explodes")]
        [SerializeField] private float _lifetime = 13f;

        [Header("Explosion")]
        [Tooltip("Radius of the explosion effect (in units)")]
        [SerializeField] private float _explosionRadius = 0.73f;
        [Tooltip("Delay before destroying bomb after explosion (allow effects to finish)")]
        [SerializeField] private float _destroyDelay = 0.5f;

        [Header("Visual/Audio")]
        [Tooltip("Particle effect prefab for explosion")]
        [SerializeField] private GameObject _explosionEffect;
        [Tooltip("Audio clip for explosion")]
        [SerializeField] private AudioClip _explosionSoundClip;
        [SerializeField] private float _soundVolume = 1f;
        [SerializeField] private float _zoomSize = 1f;

        // Runtime references
        private Player.Player _player;
        private float _spawnTime;
        private bool _hasExploded = false;
        private int _enemyLayerMask;
        private DamageData damageData;
        private float _explosionDamage;
        private float _knockbackForce;
        private float _ultimateAttack;

        private void Awake()
        {
            // Setup physics
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f; // Small trigger radius for collision detection

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;

            _enemyLayerMask = LayerMask.GetMask("Enemy");
            transform.localScale = new Vector3(_zoomSize, _zoomSize, 1f);
        }

        /// <summary>
        /// Initialize bomb with player reference and duration from data.
        /// Called by BombHandler after instantiation.
        /// </summary>
        public void Initialize(Player.Player player, UltimateData bombData)
        {
            _player = player;
            _spawnTime = Time.time;
            _lifetime = bombData.GetDuration();

            // from bombData → damageData
            _ultimateAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateAttack);
            _explosionDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * bombData.GetDamageMultiplier() * _ultimateAttack;
            _knockbackForce = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackForce) * bombData.GetKnockbackMultiplier();

            damageData = new(_explosionDamage, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = bombData.GetElement(),
                HasKnockback = true,
                KnockbackForce = _knockbackForce,
            };

            if (_explosionEffect != null) _explosionEffect.SetActive(false);
        }

        private void Update()
        {
            // Check if lifetime has expired
            if (Time.time - _spawnTime >= _lifetime) Explosion();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Check if enemy collided with bomb
            if (collision.TryGetComponent(out EnemyAi enemy))
            {
                Explosion();
            }
        }

        /// <summary>
        /// Handle bomb explosion.
        /// Called by both collision trigger and lifetime expiry.
        /// </summary>
        private void Explosion()
        {
            if (_hasExploded) return;
            _hasExploded = true;

            // Spawn visual/audio effects
            SpawnExplosionEffects();

            // Deal damage to all enemies in radius
            DealExplosionDamage();

            // Notify factory when bomb is destroyed
            UltimateFactory.OnInstanceDestroyed(UltimateID);

            // Start coroutine to destroy after effect delay
            StartCoroutine(DestroyAfterEffects());
        }

        private System.Collections.IEnumerator DestroyAfterEffects()
        {
            yield return new WaitForSeconds(_destroyDelay);
            Destroy(gameObject);
        }

        /// <summary>
        /// Deal damage and knockback to all enemies in explosion radius.
        /// </summary>
        private void DealExplosionDamage()
        {
            if (_player == null) return;

            // Find all enemies in radius
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, _explosionRadius, _enemyLayerMask);

            foreach (Collider2D col in hitEnemies)
            {
                if (col.TryGetComponent(out EnemyAi enemy))
                {
                    // Attack Enemies
                    enemy.TakeDamage(damageData, false);

                    // Apply knockback
                    Vector2 knockbackDirection = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
                    enemy.ApplyKnockback(knockbackDirection, damageData.KnockbackForce);
                }
            }
        }

        /// <summary>
        /// Spawn visual and audio effects at explosion point.
        /// </summary>
        private void SpawnExplosionEffects()
        {
            // Show effect if assigned
            if (_explosionEffect != null) _explosionEffect.SetActive(true);

            // Play sound effect if assigned
            Utilityku.PlaySfx(_player.SfxSource, _explosionSoundClip, _soundVolume);
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw explosion radius in editor
            Gizmos.color = GameColors.debugOrangeGizmo.WithAlpha(0.3f); // Orange with transparency
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
#endif

    }
}
