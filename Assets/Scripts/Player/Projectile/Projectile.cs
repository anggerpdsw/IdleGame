using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Card.Behavior;
using IdleDefenseSurvival.Card.Behavior.Implementations;

namespace IdleDefenseSurvival.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        // Cached layer mask - computed in Awake (Unity disallows GetMask in static initializer)
        private int _enemyLayerMask;

        [Header("Movement")]
        [Tooltip("Speed of the projectile in units per second.")]
        [SerializeField] private float _speed = 23f;

        [Header("Lifetime")]
        [Tooltip("Maximum distance the projectile can travel before self-destructing.")]
        [SerializeField] private float _maxDistance = 25f;

        [Tooltip("Radius to detect hit on target using distance check (fallback).")]
        [SerializeField] private float _hitRadius = 0.5f;

        [Header("Bounce Settings")]
        [Tooltip("Radius untuk mencari enemy terdekat saat bounce")]
        [SerializeField] private float _bounceRadius = 8f;
                
        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite _playerBulletSprite;
        [SerializeField] private Sprite _tankBulletSprite;
        [SerializeField] private Sprite _enemyBulletSprite;
        [SerializeField] private Sprite _pierceBulletSprite;
        [SerializeField] private Sprite _returnBulletSprite;
        [Tooltip("Target visual diameter in world units.")]
        [SerializeField] private float _visualSize = 0.67f;

        private Transform _target;
        private ProjectileOwner _owner;
        public ProjectileOwner Owner => _owner;
        private Player _player;
        private TankInstance _tank;
        private EnemyAi _enemyShooter;
        private string _sourceName;
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
        private DefenseBreakSource _defenseBreakSource;
        private DefenseBreakType _defenseBreakType;
        private float _defenseBreak;
        private float _defenseBreakDuration;
        private float _healthBreak;
        private Vector3 _startPosition;
        private Rigidbody2D _rb;
        private bool _hasHit = false;
        private int _bounceIndex = 0;  // Track bounce keberapa (0 = first hit)
        private bool _isMultiShoot = false;  // Track apakah projectile ini dari multi-shoot
        private bool _isEnemyDied = false;  // Track apakah enemy sudah Die
        private bool _isInfiniteArsenal;

        // Track enemies yang sudah terkena oleh projectile ini (untuk bounce chain)
        private readonly HashSet<Transform> _hitEnemies = new();

        // Bounce approval: di-set true pada hit pertama jika bounce chance sukses,
        // lalu dipakai untuk semua bounce berikutnya tanpa re-roll
        private bool _bounceApproved = false;

        // Pierce tracking: remaining pierces consumed per kill.
        // Actual pierce count retrieved at kill time via GetAccumulatedCount.
        private int _pierceCount;
        private bool _isPiercing = false;

        // ReturningEcho tracking
        private bool _isReturning = false;
        private int _returnHitCount = 0;

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
            _enemyShooter = null;
            _bounceApproved = false;
            _hitEnemies.Clear();
            _target = null;
            _owner = ProjectileOwner.Player;
            _player = null;
            _tank = null;
            _baseDamage = 0f;
            _damageMultiplier = 1f;
            _baseKnockbackForce = 0f;
            _basePerRange = 0f;
            _baseStuntDuration = 0f;
            _bounceChance = 0f;
            _bounceCount = 0;
            _knockbackChance = 0f;
            _lifeSteal = 0f;
            _stuntChance = 0f;
            _defenseBreakSource = DefenseBreakSource.None;
            _defenseBreakType = DefenseBreakType.None;
            _defenseBreak = 0f;
            _defenseBreakDuration = 0f;
            _isMultiShoot = false;
            _healthBreak = 0f;
            _sourceName = null;

            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            transform.rotation = Quaternion.identity;

            _isEnemyDied = false;
            _isInfiniteArsenal = false;
            _pierceCount = 0; // reset pierce tracker
            _isPiercing = false;
            _isReturning = false;
            _returnHitCount = 0;
            EnemyDeathHandler.OnEnemyKilled -= OnEnemyKilledHandler;
        }

        /// <summary>
        /// Called when any enemy dies. Sets flag if killed enemy is this projectile's target.
        /// </summary>
        private void OnEnemyKilledHandler(EnemyAi enemy, string source)
        {
            if (_target == null || enemy == null) return;
            if (_target.TryGetComponent(out EnemyAi targetEnemy) && targetEnemy == enemy)
                _isEnemyDied = true;
        }

        private void GuardEventSub()
        {
            _isEnemyDied = false;
            EnemyDeathHandler.OnEnemyKilled -= OnEnemyKilledHandler;
            EnemyDeathHandler.OnEnemyKilled += OnEnemyKilledHandler;
        }

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");

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
            if (_pool == null) _pool = ProjectilePool.Instance;
            if (_pool != null) _pool.Return(this);
        } 

        /// <summary>
        /// Initialize the projectile with player stats.
        /// </summary>
        public void Initialize(
            Transform target,
            Player player,
            float damageMultiplier,
            bool isMultiShoot = false,
            bool isInfiniteArsenal = false)
        {
            _owner = ProjectileOwner.Player;
            SetProjectileSprite(_playerBulletSprite);

            _target = target;
            GuardEventSub();
            _player = player;
            _damageMultiplier = damageMultiplier;
            _isMultiShoot = isMultiShoot;
            _isInfiniteArsenal = isInfiniteArsenal;
            _startPosition = transform.position;
            _baseDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage);
            _baseKnockbackForce = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackForce);
            _basePerRange = PlayerStatsManager.Instance.GetStat(SkillType.DamagePerRange);
            _baseStuntDuration = PlayerStatsManager.Instance.GetStat(SkillType.StuntDuration);
            _bounceChance = PlayerStatsManager.Instance.GetStat(SkillType.BounceChance);
            _bounceCount = 0; // Set when bounce is approved (see HitTarget)
            _bounceRadius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            _knockbackChance = PlayerStatsManager.Instance.GetStat(SkillType.KnockbackChance);
            _lifeSteal = PlayerStatsManager.Instance.GetStat(SkillType.LifeSteal);
            _stuntChance = PlayerStatsManager.Instance.GetStat(SkillType.StuntChance);
            _defenseBreakSource = DefenseBreakSource.PlayerProjectile;
            _defenseBreakType = DefenseBreakType.Permanent;
            _defenseBreak = PlayerStatsManager.Instance.GetStat(SkillType.DefenseBreak);
            _defenseBreakDuration = 0f;
            _pierceCount = PlayerStatsManager.Instance.GetAccumulatedCount(PlayerStatsManager.Instance.GetStat(SkillType.PierceCount), AccumulatedCountType.Pierce);
        }

        public void InitializeFromTank(Transform target, TankInstance tank)
        {
            _owner = ProjectileOwner.Tank;
            SetProjectileSprite(_tankBulletSprite);

            _target = target;
            GuardEventSub();
            _tank = tank;
            _startPosition = transform.position;
            _baseDamage = tank.TankAttackDamage;
            _basePerRange = tank.TankDamagePerRange;
            _defenseBreakSource = DefenseBreakSource.TankProjectile;
            _defenseBreakType = DefenseBreakType.Temporary;
            _defenseBreak = PlayerStatsManager.Instance.GetStat(SkillType.DefenseBreak) * 0.5f;
            _defenseBreakDuration = 7f;
        }

        public void InitializeFromEnemy(Transform target, EnemyAi enemy)
        {
            _owner = ProjectileOwner.Enemy;
            SetProjectileSprite(_enemyBulletSprite);

            _target = target;
            _enemyShooter = enemy;
            GuardEventSub();
            _startPosition = transform.position;
            _baseDamage = enemy.EnemyAttackDamage;
            _sourceName = enemy?.EnemyId ?? _owner.ToString();
        }

        private void FixedUpdate()
        {
            if (_hasHit) return;

            if (_target == null && !_isReturning)
            {
                ReturnToPool();
                return;
            }

            // Kompensasi Time.timeScale: bagi speed dengan timeScale supaya
            // kecepatan visual tetap konsisten di semua game speed (0.5x-7.5x).
            // Physics engine sudah mengalikan timeScale, jadi kita perlu membagi.
            float timeScaleCompensation = Mathf.Max(0.1f, Time.timeScale);
            float effectiveSpeed = _speed / timeScaleCompensation;

            Vector2 direction;
            if (_isReturning && _player != null)
            {
                // Return to player
                direction = ((Vector2)_player.transform.position - _rb.position).normalized;
            }
            else if (_target != null)
            {
                // Move towards target
                direction = ((Vector2)_target.position - _rb.position).normalized;
            }
            else
            {
                ReturnToPool();
                return;
            }

            _rb.linearVelocity = direction * effectiveSpeed;

            // Rotate to face direction of movement
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (_hasHit) return;

            float distanceTraveled = Vector3.Distance(_startPosition, transform.position);

            // ReturningEcho: trigger return if distance exceeded and not already returning
            if (!_isReturning && _owner == ProjectileOwner.Player && _player != null)
            {
                if (CardRuntimeManager.Instance?.GetBehavior("returning_echo") is ReturningEchoCardBehavior behavior)
                {
                    float triggerDist = behavior.GetTriggerDistance();
                    if (distanceTraveled >= triggerDist && behavior.CanTriggerReturn())
                    {
                        _isReturning = true;
                        _returnHitCount = 0;
                        behavior.TriggerReturnCooldown();
                        _hitEnemies.Clear(); // Allow hitting same enemies on return

                        // Visual feedback: swap to return sprite
                        if (_returnBulletSprite != null)
                            SetProjectileSprite(_returnBulletSprite);
                    }
                }
            }

            // Check max distance (extended for returning projectiles)
            float maxDist = _isReturning ? _maxDistance * 2f : _maxDistance;
            if (distanceTraveled >= maxDist)
            {
                ReturnToPool();
                return;
            }

            // If returning and reached player, pool it
            if (_isReturning && _player != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, _player.transform.position);
                if (distToPlayer <= _hitRadius)
                {
                    ReturnToPool();
                }
            }

            // Hit detection handled by OnTriggerEnter2D with Continuous collision detection
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (_hasHit && !_isReturning && !_isPiercing) return;

            switch (_owner)
            {
                case ProjectileOwner.Player:
                    if (_isReturning)
                    {
                        // Return path: hit any enemy
                        if (collision.TryGetComponent(out EnemyAi returnEnemy))
                        {
                            HitTargetOnReturn(returnEnemy);
                        }
                    }
                    else if (_isPiercing)
                    {
                        // Pierce path: hit all enemies along the way
                        if (collision.TryGetComponent(out EnemyAi pierceEnemy))
                        {
                            // Skip if already hit this enemy in current pierce chain
                            if (!_hitEnemies.Contains(collision.transform))
                            {
                                _hitEnemies.Add(collision.transform);
                                HitEnemyWhilePiercing(pierceEnemy);
                            }

                            // Check if we reached the target
                            if (collision.transform == _target)
                            {
                                HitTarget();
                            }
                        }
                    }
                    else
                    {
                        // Normal path
                        // Check if we collided with the target
                        SimpleHitTarget(collision);
                    }
                    break;

                case ProjectileOwner.Tank:
                    // Tank projectiles: simple hit-only, no bounce/pierce/return
                    SimpleHitTarget(collision);
                    break;

                case ProjectileOwner.Enemy:
                    // Enemy projectiles must only hit their assigned player target.
                    // They spawn inside the enemy's own collider, so reacting to any trigger
                    // collision would destroy them immediately at the spawn position.
                    bool hitAssignedTarget = _target != null &&
                        (collision.transform == _target || collision.transform.IsChildOf(_target));
                    if (hitAssignedTarget && _target.TryGetComponent(out Player player))
                    {
                        EnemyHitPlayer(player);
                    }
                    break;
            }
        }

        private void SimpleHitTarget(Collider2D collision)
        {
            if (collision.transform == _target || 
                collision.TryGetComponent<EnemyAi>(out _))
            {
                _target = collision.transform;
                HitTarget();
            }
        }

        private void HitEnemyWhilePiercing(EnemyAi enemy)
        {
            if (enemy == null || _player == null) return;

            float pierceDamage = _baseDamage * Mathf.Pow(0.9f, _bounceIndex);
            pierceDamage *= DamagePerRange(_player.transform.position);

            DamageData damageData = new(
                damage: pierceDamage,
                type: DamageType.Normal,
                crit: CriticalType.None,
                source: DamageSource.PiercingBullet.ToString()
            )
            {
                Element = Utilityku.RandomElement(),
                DefenseBreakSource = _defenseBreakSource,
                DefenseBreakType = _defenseBreakType,
                DefenseBreak = _defenseBreak,
                DefenseBreakDuration = _defenseBreakDuration
            };

            enemy.TakeDamage(damageData);
        }

        private void HitTargetOnReturn(EnemyAi enemy)
        {
            if (enemy == null || _player == null) return;
            if (_hitEnemies.Contains(enemy.transform)) return;

            if (CardRuntimeManager.Instance?.GetBehavior("returning_echo") is not ReturningEchoCardBehavior behavior) return;

            int maxTargets = behavior.GetMaximumReturnTargets();
            if (_returnHitCount >= maxTargets)
            {
                ReturnToPool();
                return;
            }

            _hitEnemies.Add(enemy.transform);
            _returnHitCount++;

            float returnDamage = behavior.GetReturnDamageMultiplier(_baseDamage);

            DamageData damageData = new(
                damage: returnDamage,
                type: DamageType.Normal,
                crit: CriticalType.None,
                source: DamageSource.ReturningEcho.ToString()
            )
            {
                Element = Utilityku.RandomElement()
            };

            enemy.TakeDamage(damageData);

            if (_returnHitCount >= maxTargets)
            {
                ReturnToPool();
            }
        }

        private void EnemyHitPlayer(Player player)
        {
            if (_hasHit) return;
            _hasHit = true;

            DamageData damageData = new(
                damage: _baseDamage,
                type: DamageType.Normal,
                crit: CriticalType.None,
                source: _sourceName
            );

            float actualDamageDealt = player.TakeDamage(damageData);

            // Process Vampiric LifeSteal after actual damage is known
            if (_enemyShooter != null)
                EnemyEffectProcessor.ProcessVampiricLifeSteal(_enemyShooter, actualDamageDealt);

            ReturnToPool();
        }

        private void TriggerEquipmentHitEffects(EnemyAi enemy)
        {
            var effectService = EquipmentService.Instance?.Effects;
            if (effectService == null) return;

            var data = new TriggerData { Enemy = enemy };
            effectService.TriggerEffects(EffectTriggerType.OnHit, data);
        }

        private void HitTarget()
        {
            if (_hasHit) return;
            _hasHit = true;

            if (_target != null)
            {
                if (_target.TryGetComponent(out EnemyAi enemy))
                {
                    // Kalkulasi damage dengan geometric reduction: baseDamage * (0.9 ^ bounceIndex)
                    // Bounce 0 (first hit): 100%, Bounce 1: 90%, Bounce 2: 81%, etc.
                    float currentDamage = _baseDamage * Mathf.Pow(0.9f, _bounceIndex);
                    Vector3 lastEnemyPos = enemy.transform.position;
                    
                    // Tambahkan target ke hit history
                    _hitEnemies.Add(_target);
                    
                    if (_tank != null) {
                        currentDamage *= DamagePerRange(_tank.transform.position);
                        // Build DamageData if can evade
                        DamageData damageData = new(
                            damage: currentDamage,
                            type: DamageType.Normal,
                            crit: CriticalType.None,
                            source: ProjectileOwner.Tank.ToString()
                        ) {
                            Element = Element.Metal,
                            // Defense Break
                            DefenseBreakSource = _defenseBreakSource,
                            DefenseBreakType = _defenseBreakType,
                            DefenseBreak = _defenseBreak,
                            DefenseBreakDuration = _defenseBreakDuration
                        };
                        float tankDamage = enemy.TakeDamage(damageData);
                        if (tankDamage <= 0f) 
                        {
                            ReturnToPool();
                            return;
                        }
                    }

                    if (_player != null)  {
                        // Check if this is InfiniteArsenal special projectile
                        bool isInfiniteArsenal = _isInfiniteArsenal;
                        bool isVoidOverlord = CardModifierService.IsVoidOverlordActive();
                        float hitDamageMultiplier = _damageMultiplier;

                        // Fracture Mark
                        float fractureMult = 1f;
                        if (CardRuntimeManager.Instance?.GetBehavior("fracture_mark") is FractureMarkCardBehavior fracture)
                            fractureMult = fracture.GetBonusMultiplier(enemy);
                        hitDamageMultiplier *= fractureMult;

                        // Hunter Instinct
                        float hunterMult = 1f;
                        if (CardRuntimeManager.Instance?.GetBehavior("hunter_instinct") is HunterInstinctCardBehavior hunter)
                            hunterMult = hunter.GetDamageMultiplier(enemy);
                        hitDamageMultiplier *= hunterMult;

                        // --- Calculate critical tier (None, Critical, SuperCritical) ---
                        CriticalType critTier = CriticalType.None;
                        float crit = PlayerStatsManager.Instance.GetStat(SkillType.CriticalChance);
                        float critDMG = PlayerStatsManager.Instance.GetStat(SkillType.CriticalDamage);

                        // LastBullet override
                        if (_player.GetNextAttackGuaranteedHit())
                        {
                            hitDamageMultiplier *= _player.GetNextAttackDamageMultiplier();
                            crit += _player.GetNextAttackCriticalChanceBonus();
                        }

                        // InfiniteArsenal: force 100% crit
                        if (isInfiniteArsenal)
                        {
                            critTier = CriticalType.Arsenal;
                            hitDamageMultiplier += critDMG * CardModifierService.GetCardParameter(
                                "infinite_arsenal", "ProjectileDamageMultiplier");
                        }
                        else
                        {
                            // Normal critical chance roll
                            if (Utilityku.Chance(crit))
                            {
                                critTier = CriticalType.Critical;
                                hitDamageMultiplier += critDMG;

                                // SuperCritical roll - nested Chance as specified
                                if (Utilityku.Chance(crit * 0.5f))
                                {
                                    critTier = CriticalType.SuperCritical;
                                    hitDamageMultiplier += critDMG * 1.05f;

                                    // UltraCritical roll - nested Chance as specified
                                    if (Utilityku.Chance(crit * 0.125f))
                                    {
                                        critTier = CriticalType.UltraCritical;
                                        hitDamageMultiplier += critDMG * 1.35f;
                                    }
                                }
                            }
                        }

                        // Dispatch critical hit event for card behaviors (Shrapnel, BleedingEdge)
                        if (critTier != CriticalType.None)
                        {
                            CardRuntimeManager.Instance?.DispatchPlayerCriticalHit(true, currentDamage, transform.position);
                        }

                        // VoidOverlord bonus damage
                        if (isVoidOverlord)
                        {
                            float voidBonus = CardModifierService.GetEffectResult(CardEffectType.VoidOverlord, 0f);
                            hitDamageMultiplier += voidBonus;
                        }

                        currentDamage *= DamagePerRange(_player.transform.position);

                        // Bounce chance: roll sekali di hit pertama (_bounceIndex == 0),
                        // lalu gunakan hasilnya untuk semua bounce berikutnya
                        if (_bounceIndex == 0)
                        {
                            // InfiniteArsenal: force bounce with 3 bounces
                            if (isInfiniteArsenal)
                            {
                                _bounceApproved = true;
                                _bounceCount = Mathf.RoundToInt(CardModifierService.GetCardParameter(
                                    "infinite_arsenal", "BounceCount"));
                            }
                            else
                            {
                                _bounceApproved = Utilityku.Chance(_bounceChance);
                                if (_bounceApproved)
                                {
                                    float rawBounceCount = PlayerStatsManager.Instance.GetStat(SkillType.BounceCount);
                                    _bounceCount = PlayerStatsManager.Instance.GetAccumulatedCount(rawBounceCount, AccumulatedCountType.Bounce);
                                }
                            }
                        }

                        // Build DamageData with critical tier
                        string damageSource = _isPiercing ? DamageSource.PiercingBullet.ToString() : ProjectileOwner.Player.ToString();
                        DamageData damageData = new(
                            damage: currentDamage,
                            type: DamageType.Normal,
                            crit: critTier,
                            source: damageSource
                        )
                        {
                            DamageMultiplier = hitDamageMultiplier,
                            Element = Utilityku.RandomElement(),
                            HasKnockback = Utilityku.Chance(_knockbackChance),
                            KnockbackForce = _baseKnockbackForce * Mathf.Pow(0.9f, _bounceIndex),
                            HasStunt = Utilityku.Chance(_stuntChance),
                            HasBounce = _bounceApproved,

                            // Defense Break
                            DefenseBreakSource = _defenseBreakSource,
                            DefenseBreakType = _defenseBreakType,
                            DefenseBreak = _defenseBreak,
                            DefenseBreakDuration = _defenseBreakDuration
                        };
                        
                        float actualDamage = enemy.TakeDamage(damageData);
                        if (actualDamage <= 0f)
                        {
                            ReturnToPool();
                            return;
                        }

                        // _isEnemyDied set via OnEnemyKilled event

                        // Consume 1 mana per enemy hit only when multi-shoot is active
                        if (_isMultiShoot && _bounceIndex == 0)
                            _player?.SpendMana(1f);

                        // Pump equipped-item + affix passives (e.g. FreezeEnemy):
                        // every hit an armed passive gets its chance to fire.
                        TriggerEquipmentHitEffects(enemy);

                        // Block lifesteal if Necromancer Unregeneration aura is active
                        if (_lifeSteal > 0f && !_player.IsUnregenerationActive()) {
                            float heal = Mathf.Max(0.51f, actualDamage * _lifeSteal * 0.01f);
                            _player.Heal(heal, true);
                        }

                        // === Card Effects ===
                        // ExecutionProtocol: instant kill check
                        if (!_isEnemyDied && CardModifierService.HasEffect(CardEffectType.ExecutionProtocol))
                        {
                            bool isBossOrElite = enemy.EnemyData != null && (enemy.EnemyData.IsBoss || enemy.EnemyData.IsSpecial);
                            float threshold = isBossOrElite 
                                ? CardModifierService.GetBossExecutionThreshold()
                                : CardModifierService.GetNormalExecutionThreshold();
                            float chance = Mathf.Clamp01(
                                CardModifierService.GetExecutionChance()
                                * CardModifierService.GetProjectileEffectTriggerRateMultiplier());
                            if (enemy.CurrentHealth <= enemy.MaxHealth * threshold && Utilityku.Chance(chance * 100f))
                            {
                                enemy.Die();
                                _isEnemyDied = true;
                                EffectPool.Instance?.Spawn("SwordEffect", lastEnemyPos);
                            }
                        }

                        // Overkill: transfer excess damage
                        if (_isEnemyDied && CardModifierService.HasEffect(CardEffectType.Overkill))
                        {
                            float excessDamage = enemy.LastOverkillDamage
                                * CardModifierService.GetEffectResult(CardEffectType.Overkill);
                            if (excessDamage > 0f)
                            {
                                float cap = damageData.GetFinalDamage()
                                    * CardModifierService.GetCardParameter("overkill", "MaximumTransferMultiplier");
                                excessDamage = Mathf.Min(excessDamage, cap);

                                Transform nearest = FindNearestUnhitEnemy(transform.position);
                                if (nearest != null && nearest.TryGetComponent<EnemyAi>(out var nearestEnemy))
                                {
                                    nearestEnemy.TakeDamage(new DamageData(excessDamage, DamageType.Normal, CriticalType.None, "Overkill"));
                                }
                            }
                        }

                        // CriticalCascade: spawn extra projectile on crit
                        if (critTier != CriticalType.None && CardModifierService.HasEffect(CardEffectType.CriticalCascade))
                        {
                            float cascadeChance = Mathf.Clamp01(
                                CardModifierService.GetEffectResult(CardEffectType.CriticalCascade, 0f)
                                * CardModifierService.GetProjectileEffectTriggerRateMultiplier());
                            if (Utilityku.Chance(cascadeChance * 100f))
                            {
                                Transform cascadeTarget = FindNearestUnhitEnemy(transform.position);
                                if (cascadeTarget != null)
                                {
                                    Projectile cascade = ProjectilePool.Instance.Get();
                                    if (cascade != null)
                                    {
                                        cascade.transform.position = transform.position;
                                        cascade.Initialize(cascadeTarget, _player,
                                            CardModifierService.GetCardParameter(
                                                "critical_cascade", "ProjectileDamageMultiplier"), false);
                                    }
                                }
                            }
                        }

                        // --- Implementasi Knockback ---
                        if (!_isEnemyDied && damageData.HasKnockback)
                        {
                            Vector2 kbDirection = ((Vector2)_target.position - _rb.position).normalized;
                            enemy.ApplyKnockback(kbDirection, damageData.KnockbackForce);
                        }

                        // --- Implementasi Stunt ---
                        if (!_isEnemyDied && damageData.HasStunt)
                        {
                            float currentStuntDuration = _baseStuntDuration * Mathf.Pow(0.9f, _bounceIndex);
                            enemy.ApplyStunt(currentStuntDuration);
                        }

                        // --- Pierce Logic (Panic Fire max stacks) ---
                        // If enemy died and we have remaining pierces, find farthest target and pierce through
                        if (_isEnemyDied)
                        {
                            if (_pierceCount > 0)
                            {
                                Transform nextTarget = FindFarthestUnhitEnemy(transform.position);
                                if (nextTarget != null)
                                {
                                    // Clear hit history so projectile can hit enemies in the path
                                    _hitEnemies.Clear();

                                    _target = nextTarget;
                                    _pierceCount--;
                                    _bounceIndex++;
                                    _hasHit = false;
                                    _isPiercing = true;

                                    // Visual feedback: swap to pierce sprite
                                    if (_pierceBulletSprite != null)
                                        SetProjectileSprite(_pierceBulletSprite);

                                    return;
                                }
                            }
                        }

                        // --- Implementasi Bounce ---
                        if ((damageData.HasBounce && _bounceCount > 0) || isInfiniteArsenal || isVoidOverlord)
                        {
                            Transform nextTarget = FindNearestUnhitEnemy(transform.position);
                            if (nextTarget != null)
                            {
                                // OPTIMIZATION: Ubah target projectile yang sama (tidak instantiate baru)
                                _target = nextTarget;
                                if (damageData.HasBounce && _bounceCount > 0)
                                {
                                    _bounceCount--;
                                    _bounceIndex++;
                                }
                                else if (isInfiniteArsenal)
                                {
                                    _bounceIndex++;
                                }
                                _hasHit = false;  // Reset agar projectile terus bergerak
                                return;  // Jangan destroy!
                            }
                        }

                        _player.SpawnTank();
                        UltimateManager.Instance.TryGenerateStack(DamageSource.Bomb.ToString(), _player, lastEnemyPos);
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
        /// Cari enemy terjauh dari posisi yang diberikan yang belum terkena projectile ini (untuk pierce).
        /// </summary>
        private Transform FindFarthestUnhitEnemy(Vector2 fromPosition)
        {
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(fromPosition, _bounceRadius, _enemyLayerMask);

            Transform farthest = null;
            float maxDistance = 0f;

            foreach (Collider2D col in nearbyEnemies)
            {
                if (_hitEnemies.Contains(col.transform)) continue;
                if (!col.gameObject.activeInHierarchy) continue;

                float sqrDistance = ((Vector2)col.transform.position - fromPosition).sqrMagnitude;
                if (sqrDistance > maxDistance)
                {
                    maxDistance = sqrDistance;
                    farthest = col.transform;
                }
            }

            return farthest;
        }

        /// <summary>
        /// Cari enemy terdekat dari posisi yang diberikan yang belum terkena projectile ini.
        /// </summary>
        private Transform FindNearestUnhitEnemy(Vector2 fromPosition)
        {
            // Gunakan Physics2D untuk cari semua enemy dalam radius
            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(fromPosition, _bounceRadius, _enemyLayerMask);

            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (Collider2D col in nearbyEnemies)
            {
                // Skip jika enemy ini sudah terkena
                if (_hitEnemies.Contains(col.transform)) continue;

                // Skip jika enemy sudah mati (GameObject inactive)
                if (!col.gameObject.activeInHierarchy) continue;

                float sqrDistance = ((Vector2)col.transform.position - fromPosition).sqrMagnitude;
                if (sqrDistance < minDistance)
                {
                    minDistance = sqrDistance;
                    nearest = col.transform;
                }
            }

            return nearest;
        }

        private void SetProjectileSprite(Sprite sprite)
        {
            if (_spriteRenderer == null || sprite == null) return;
            _spriteRenderer.sprite = sprite;
            NormalizeVisualSize();
        }

        private void NormalizeVisualSize()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;
            Sprite sprite = _spriteRenderer.sprite;
            // Sprite size in world units before local scale.
            Vector2 spriteSize = sprite.bounds.size;
            float currentSize = Mathf.Max(spriteSize.x, spriteSize.y);
            if (currentSize <= 0f) return;
            float normalizedScale = _visualSize / currentSize;
            _spriteRenderer.transform.localScale = Vector3.one * normalizedScale;
        }

        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw explosion radius in editor
            Gizmos.color = GameColors.debugOrangeGizmo.WithAlpha(0.3f); // Orange with transparency
            Gizmos.DrawWireSphere(transform.position, _bounceRadius);
        }
#endif

    }
}
