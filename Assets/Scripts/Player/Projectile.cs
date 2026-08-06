using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Speed of the projectile in units per second.")]
        [SerializeField] private float _speed = 20f;

        [Header("Lifetime")]
        [Tooltip("Maximum distance the projectile can travel before self-destructing.")]
        [SerializeField] private float _maxDistance = 20f;

        [Tooltip("Radius to detect hit on target using distance check (fallback).")]
        [SerializeField] private float _hitRadius = 0.5f;

        [Header("Bounce Settings")]
        [Tooltip("Radius untuk mencari enemy terdekat saat bounce")]
        [SerializeField] private float _bounceSearchRadius = 2f;
                
        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite _playerBulletSprite;
        [SerializeField] private Sprite _tankBulletSprite;
        [SerializeField] private Sprite _enemyBulletSprite;

        private Transform _target;
        private ProjectileOwner _owner;
        private Player _player;
        private TankInstance _tank;
        private float _baseDamage;  // Base stats untuk geometric reduction
        private float _damageMultiplier = 1f;
        private float _baseKnockbackForce;
        private float _basePerRange;
        private float _baseStuntDuration;
        private float _bounceChance;
        private int _bounceCount;
        private float _knockbackChance;
        private float _lifeSteal;
        private float _stuntChance;
        private Vector3 _startPosition;
        private Rigidbody2D _rb;
        private bool _hasHit = false;
        private int _bounceIndex = 0;  // Track bounce keberapa (0 = first hit)

        // Track enemies yang sudah terkena oleh projectile ini (untuk bounce chain)
        private readonly HashSet<Transform> _hitEnemies = new();

        // Reference to the pool for returning projectiles
        private ProjectilePool _pool;

        /// <summary>
        /// Reset projectile state for reuse from object pool.
        /// Called by ProjectilePool.Return() before the projectile is returned to the pool.
        /// </summary>
        public void ResetState()
        {
            _hasHit = false;
            _bounceIndex = 0;
            _hitEnemies.Clear();
            _target = null;
            _owner = ProjectileOwner.Player;
            _player = null;
            _tank = null;
            _baseDamage = 0f;
            _baseKnockbackForce = 0f;
            _basePerRange = 0f;
            _baseStuntDuration = 0f;
            _bounceChance = 0f;
            _bounceCount = 0;
            _knockbackChance = 0f;
            _lifeSteal = 0f;
            _stuntChance = 0f;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }

            transform.rotation = Quaternion.identity;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Set collider as trigger
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = _hitRadius * 0.6f;

            // Find the projectile pool in the scene
            _pool = ProjectilePool.Instance;
        }

        private void ReturnToPool()
        {
            // Check if we have a valid pool reference
            if (_pool == null)
            {
                _pool = ProjectilePool.Instance;
            }

            if (_pool != null)
            {
                _pool.Return(this);
            }
        } 

        /// <summary>
        /// Initialize the projectile with player stats.
        /// </summary>
        public void Initialize(Transform target, Player player, float damageMultiplier)
        {    
            _owner = ProjectileOwner.Player;
            _spriteRenderer.sprite = _playerBulletSprite;

            _target = target;
            _player = player;
            _damageMultiplier = damageMultiplier;
            _startPosition = transform.position;
            _baseDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage);
            _baseKnockbackForce = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackForce);
            _basePerRange = PlayerStatsManager.Instance.GetStat(SkillType.DamagePerRange);
            _baseStuntDuration = PlayerStatsManager.Instance.GetStat(SkillType.StuntDuration);
            _bounceChance = PlayerStatsManager.Instance.GetStat(SkillType.BounceChance);
            _bounceCount = PlayerStatsManager.Instance.GetStatInt(SkillType.BounceCount);
            _bounceSearchRadius = PlayerStatsManager.Instance.GetStat(SkillType.BounceSearchRadius);
            _knockbackChance = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackChance);
            _lifeSteal = PlayerStatsManager.Instance.GetStat(SkillType.LifeSteal);
            _stuntChance = PlayerStatsManager.Instance.GetStat(SkillType.StuntChance);
        }

        public void InitializeFromTank(Transform target, TankInstance tank)
        {
            _owner = ProjectileOwner.Tank;
            _spriteRenderer.sprite = _tankBulletSprite;

            _target = target;
            _startPosition = transform.position;
            _baseDamage = tank.TankAttackDamage;
            _basePerRange = tank.TankDamagePerRange;
            _tank = tank;
        }

        public void InitializeFromEnemy(Transform target, EnemyAi enemy)
        {
            _owner = ProjectileOwner.Enemy;
            _spriteRenderer.sprite = _enemyBulletSprite;

            _target = target;
            _startPosition = transform.position;
            _baseDamage = enemy.EnemyAttackDamage;
        }

        private void FixedUpdate()
        {
            if (_hasHit) return;

            if (_target == null)
            {
                ReturnToPool();
                return;
            }

            // Move towards target
            Vector2 direction = ((Vector2)_target.position - _rb.position).normalized;
            _rb.linearVelocity = direction * _speed;

            // Rotate to face direction of movement
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (_hasHit) return;

            // Check max distance
            if (Vector3.Distance(_startPosition, transform.position) >= _maxDistance)
            {
                ReturnToPool();
                return;
            }

            // Distance-based hit detection (fallback in case trigger doesn't fire)
            if (_target != null)
            {
                float dist = Vector2.Distance(transform.position, _target.position);
                if (dist <= _hitRadius)
                {
                    if (_owner == ProjectileOwner.Enemy && _target.TryGetComponent(out Player player))
                    {
                        HitPlayer(player);
                    }
                    else
                    {
                        HitTarget();
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (_hasHit) return;

            switch (_owner)
            {
                case ProjectileOwner.Player:
                case ProjectileOwner.Tank:

                    // Check if we collided with the target
                    if (collision.transform == _target)
                    {
                        HitTarget();
                    }
                    // Also hit any enemy (e.g. if target died and another is in the way)
                    else if (collision.TryGetComponent(out EnemyAi enemy))
                    {
                        _target = collision.transform;
                        HitTarget();
                    }
                    break;

                case ProjectileOwner.Enemy:
                    // Enemy projectiles must only hit their assigned player target.
                    // They spawn inside the enemy's own collider, so reacting to any trigger
                    // collision would destroy them immediately at the spawn position.
                    bool hitAssignedTarget = _target != null &&
                        (collision.transform == _target || collision.transform.IsChildOf(_target));
                    if (hitAssignedTarget && _target.TryGetComponent(out Player player))
                    {
                        HitPlayer(player);
                    }
                    break;
            }
        }

        private void HitPlayer(Player player)
        {
            if (_hasHit) return;
            _hasHit = true;

            DamageData damageData = new(
                damage: _baseDamage,
                type: DamageType.Normal,
                crit: CriticalType.None,
                source: "enemy"
            );

            player.TakeDamage(damageData);
            ReturnToPool();
        }

        private void HitTarget()
        {
            if (_hasHit) return;
            _hasHit = true;

            if (_target != null)
            {
                if (_target.TryGetComponent(out EnemyAi enemy))
                {
                    // Kalkulasi damage dengan geometric reduction: baseDamage * (0.5 ^ bounceIndex)
                    // Bounce 0 (first hit): 100%, Bounce 1: 50%, Bounce 2: 25%, etc.
                    float currentDamage = _baseDamage * Mathf.Pow(0.5f, _bounceIndex);

                    // Tambahkan target ke hit history
                    _hitEnemies.Add(_target);
                    
                    if (_tank != null) {
                        currentDamage *= DamagePerRange(_tank.transform.position);
                        // Build DamageData if can evade
                        DamageData damageData = new(
                            damage: currentDamage,
                            type: DamageType.Normal,
                            crit: CriticalType.None,
                            source: UltimateDMG.Tank.ToString()
                        ) {
                            Element = Element.Metal
                        };
                        float tankDamage = enemy.TakeDamage(damageData);
                        if (tankDamage <= 0f) 
                        {
                            ReturnToPool();
                            return;
                        }
                    }

                    if (_player != null)  {
                        // --- Calculate critical tier (None, Critical, SuperCritical) ---
                        CriticalType critTier = CriticalType.None;
                        // Normal critical chance roll
                        if (Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.CriticalChance)))
                        {
                            critTier = CriticalType.Critical;
                            _damageMultiplier += PlayerStatsManager.Instance.GetStat(SkillType.CriticalFactor);

                            // SuperCritical roll - nested Chance as specified
                            if (Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.SuperCriticalChance)))
                            {
                                critTier = CriticalType.SuperCritical;
                                _damageMultiplier += PlayerStatsManager.Instance.GetStat(SkillType.SuperCriticalFactor);

                                // UltraCritical roll - nested Chance as specified
                                if (Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.UltraCriticalChance)))
                                {
                                    critTier = CriticalType.UltraCritical;
                                    _damageMultiplier += PlayerStatsManager.Instance.GetStat(SkillType.UltraCriticalFactor);
                                }
                            }
                        }

                        currentDamage *= DamagePerRange(_player.transform.position);
                        
                        // Build DamageData with critical tier
                        DamageData damageData = new(
                            damage: currentDamage,
                            type: DamageType.Normal,
                            crit: critTier,
                            source: UltimateDMG.Player.ToString()
                        )
                        {
                            DamageMultiplier = _damageMultiplier,
                            Element = Utilityku.RandomElement(),
                            HasKnockback = Utilityku.Chance(_knockbackChance),
                            KnockbackForce = _baseKnockbackForce * Mathf.Pow(0.5f, _bounceIndex),
                            HasStunt = Utilityku.Chance(_stuntChance),
                            HasBounce = Utilityku.Chance(_bounceChance)
                        };

                        float actualDamage = enemy.TakeDamage(damageData);
                        if (actualDamage <= 0f)
                        {
                            ReturnToPool();
                            return;
                        }

                        float heal = actualDamage * _lifeSteal / 100f;
                        _player.Heal(heal);

                        // --- Implementasi Knockback ---
                        if (damageData.HasKnockback)
                        {
                            Vector2 kbDirection = ((Vector2)_target.position - _rb.position).normalized;
                            enemy.ApplyKnockback(kbDirection, damageData.KnockbackForce);
                        }

                        // --- Implementasi Stunt ---
                        if (damageData.HasStunt)
                        {
                            float currentStuntDuration = _baseStuntDuration * Mathf.Pow(0.5f, _bounceIndex);
                            enemy.ApplyStunt(currentStuntDuration);
                        }

                        // --- Implementasi Bounce ---
                        if (damageData.HasBounce && _bounceCount > 0)
                        {
                            Transform nextTarget = FindNearestUnhitEnemy(transform.position);
                            if (nextTarget != null)
                            {
                                // OPTIMIZATION: Ubah target projectile yang sama (tidak instantiate baru)
                                _target = nextTarget;
                                _bounceCount--;
                                _bounceIndex++;  // Increment bounce index untuk damage reduction
                                _hasHit = false;  // Reset agar projectile terus bergerak
                                return;  // Jangan destroy!
                            }
                        }

                        _player.SpawnTank();
                        UltimateManager.Instance.TrySpawn(UltimateDMG.Bomb.ToString(), transform.position, _player);
                    }
                }
            }

            ReturnToPool();
        }

        private float DamagePerRange(Vector3 frompos)
        {
            float distance = Vector2.Distance(frompos, _target.position);
            // contoh: +2% damage setiap 1 unit jarak
            float rangeMultiplier = 1f + (distance * _basePerRange / 100f);

            return rangeMultiplier;
        }

        /// <summary>
        /// Cari enemy terdekat dari posisi yang diberikan yang belum terkena projectile ini.
        /// </summary>
        private Transform FindNearestUnhitEnemy(Vector2 fromPosition)
        {
            // Gunakan Physics2D untuk cari semua enemy dalam radius
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(fromPosition, _bounceSearchRadius, LayerMask.GetMask("Enemy"));

            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (Collider2D col in nearbyEnemies)
            {
                // Skip jika enemy ini sudah terkena
                if (_hitEnemies.Contains(col.transform)) continue;

                // Skip jika enemy sudah mati (GameObject inactive)
                if (!col.gameObject.activeInHierarchy) continue;

                float distance = Vector2.Distance(fromPosition, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = col.transform;
                }
            }

            return nearest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw explosion radius in editor
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange with transparency
            Gizmos.DrawWireSphere(transform.position, _bounceSearchRadius);
        }
#endif

    }
}
