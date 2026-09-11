using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.UI;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Item;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using System.Collections.Generic;
using IdleDefenseSurvival.Mission;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy.StatusEffects;

/// <summary>
/// Handles basic enemy AI for the auto‑shooter game.
/// - Moves towards the player while outside the attack range.
/// - Stops moving when within the attack range.
/// - Can be knocked back by the player; movement resumes after the knockback duration.
/// - Uses spatial grid for O(1) separation lookups.
/// - Throttles updates for distant enemies (50Hz near, 10Hz far).
/// - Status effects (slow, defense break, heart break, stun, freeze, etc.) are delegated to EnemyStatusEffectController.
/// </summary>
namespace IdleDefenseSurvival.Enemy
{
    public class EnemyAi : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Configurable fields (exposed in the Inspector)
        // -------------------------------------------------------------------
        [SerializeField] private Role _role = Role.Fighter;
        [SerializeField] private Element _element = Element.None;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackSpeed = 1f;
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _defenseAmount = 0f;
        [SerializeField] private float _maxHealth;
        [SerializeField] private float _moveSpeed = 1f;

        [Header("Steering Settings")]
        [Tooltip("Radius untuk mendeteksi tetangga guna menghindari tumpukan")]
        [SerializeField] private float _separationRadius = 0.16f;
        [Tooltip("Kekuatan dorongan antar enemy. Semakin tinggi, semakin renggang kerumunannya")]
        [SerializeField] private float _separationWeight = 0.4f;
        [Tooltip("Damping untuk mengurangi velocity secara bertahap (0-1)")]
        [SerializeField] private float _velocityDamping = 0.15f;

        [Header("UI & Effects")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private GameObject _dizzyEffect;

        // Status effect controller reference
        [SerializeField] private EnemyStatusEffectController _statusEffectController;

        [Header("Aura Visualization")]
        [Tooltip("SpriteRenderer for drawing enemy aura range (dashed circle).")]
        [SerializeField] private SpriteRenderer _auraRangeRenderer;
        [Tooltip("Rotation speed of aura range visual.")]
        [SerializeField] private float _auraRotationSpeed = 2f;
        [Tooltip("Scale multiplier for aura sprite size.")]
        [SerializeField] private float _auraScaleRange = 1f;

        [Header("Aura Pulse")]
        [Tooltip("Renderer untuk gelombang aura yang melebar dari pusat enemy.")]
        [SerializeField] private SpriteRenderer _auraPulseRenderer;
        [Tooltip("Durasi satu gelombang dari pusat sampai batas aura.")]
        [SerializeField] private float _auraPulseDuration = 1.2f;
        [Tooltip("Jeda antar gelombang.")]
        [SerializeField] private float _auraPulseInterval = 0.35f;
        [Tooltip("Alpha maksimum gelombang.")]
        [SerializeField, Range(0f, 1f)] private float _auraPulseMaxAlpha = 0.45f;
        [Tooltip("Warna gelombang aura Slow.")]
        [SerializeField] private Color _auraPulseColor = new(0.1f, 0.55f, 1f, 1f);

        private float _auraPulseTimer;
        private float _auraRadius;
        private bool _hasAura;

        private SaveManager _saveManager;
        private WaveManager _waveManager;
        private EconomyManager _economyManager;
        private UltimateManager _ultimateManager;
        private DamagePopupManager _damagePopUpManager;
        private EnemyHealthBarManager _enemyHealthBarManager;

        [Header("Rewards")]
        private long _goldReward = 0;
        private long _gemReward = 0;
        private long _meatReward = 0;
        private int _expReward = 0;

        public EnemyData EnemyData { get; private set; }

        [Header("Pickup Prefab")]
        [Tooltip("Single pickup prefab for all currency types (Gem, Meat)")]
        [SerializeField] private GameObject _itemPrefab;

        // -------------------------------------------------------------------
        // Runtime references
        // -------------------------------------------------------------------
        private string _enemyId; // Enemy type ID for tracking kills
        private float _currentHealth;
        private Transform _player;
        private Player.Player _playerComponent;
        private Rigidbody2D _rb;
        private float _stuntEndTime;
        private float _knockbackEndTime;
        private float _knockbackDuration;
        private float _evasion;
        private bool _isStunt = false;
        private float _attackTimer = 0f;
        private string _lastDamageSource = UltimateDMG.Player.ToString();

        // Performance: spatial grid untuk separation O(1) lookup
        private static readonly Dictionary<int, List<EnemyAi>> _spatialGrid = new();
        private static readonly int GridCellSize = 2; // world units per cell
        private Vector2Int _currentGridCell;
        private bool _inGrid = false;

        // Performance: update throttling untuk enemy jauh
        private float _lastUpdateTime;
        private const float UPDATE_INTERVAL_NEAR = 0.02f;  // 50Hz dekat player
        private const float UPDATE_INTERVAL_FAR = 0.1f;    // 10Hz jauh
        private const float FAR_DISTANCE_SQR = 400f;       // 20 units^2

        // Performance: FixedUpdate throttling (separate timer so Update/FixedUpdate don't interfere)
        private float _lastFixedUpdateTime;

        public float EnemyAttackDamage => _damage;
        public float Evasion => _evasion;
        public Vector3 HealthBarWorldPosition
        {
            get
            {
                if (_spriteRenderer == null) return transform.position;
                Bounds bounds = _spriteRenderer.bounds;
                float offset = _enemyHealthBarManager != null
                    ? _enemyHealthBarManager.HealthBarOffsetY
                    : 0.15f; // fallback
                return new Vector3(bounds.center.x, bounds.max.y + offset, transform.position.z);
            }
        }

        // -------------------------------------------------------------------
        // Unity callbacks
        // -------------------------------------------------------------------
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            var playerObj = GameObject.FindWithTag(UltimateDMG.Player.ToString());
            if (playerObj != null)
            {
                _player = playerObj.transform;
                _playerComponent = playerObj.GetComponent<Player.Player>();  // Cache Player component untuk attack
            }

            // Freeze rotation Z agar enemy tidak berputar saat terkena force (separation/knockback)
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            _saveManager = SaveManager.Instance;
            _waveManager = WaveManager.Instance;
            _economyManager = EconomyManager.Instance;
            _ultimateManager = UltimateManager.Instance;
            _damagePopUpManager = DamagePopupManager.Instance;
            _enemyHealthBarManager = EnemyHealthBarManager.Instance;
        }

        private void OnEnable()
        {
            RegisterWithGrid();
        }

        private void OnDisable()
        {
            UnregisterFromGrid();
            // Status effects are cleared by EnemyStatusEffectController.OnDisable
        }

        private void Update()
        {
            // Slow aura pulse tetap berjalan saat Time.timeScale = 0
            UpdateAuraPulse();

            // Skip movement if still in stun
            if (Time.time < _stuntEndTime)
            {
                // Only zero velocity if knockback has ended
                // This allows knockback to push enemy before freezing them
                if (Time.time >= _knockbackEndTime && _rb.linearVelocity != Vector2.zero)
                {
                    _rb.linearVelocity = Vector2.zero;
                }

                return;
            }
            SetStunt(false);

            // Throttle Update for distant enemies
            if (!ShouldUpdateThisFrame()) return;

            // Attack logic: jika dalam attack range, mulai attack dengan cooldown
            if (IsInAttackRange())
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0f)
                {
                    AttackPlayer();
                    _attackTimer = 1f / _attackSpeed;  // Reset cooldown
                }
            }
            else
            {
                // Reset timer ketika keluar dari attack range
                _attackTimer = 0f;
            }

            // Defense breaks are now handled by EnemyStatusEffectController

            // Rotate aura range visual
            if (_auraRangeRenderer != null && _auraRangeRenderer.enabled)
                _auraRangeRenderer.transform.Rotate(0, 0, _auraRotationSpeed * Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            if (Time.time < _stuntEndTime) return;

            // Throttle FixedUpdate for distant enemies (same intervals as Update)
            if (!ShouldFixedUpdate()) return;

            // Update spatial grid cell
            UpdateCell();

            ApplyMovement();
            UpdateFacing();
        }

        /// <summary>
        /// Throttle FixedUpdate using same distance-based intervals as Update.
        /// </summary>
        private bool ShouldFixedUpdate()
        {
            float currentTime = Time.time;
            float distSqr = (_player != null)
                ? (transform.position - _player.position).sqrMagnitude
                : FAR_DISTANCE_SQR;

            float interval = distSqr <= FAR_DISTANCE_SQR ? UPDATE_INTERVAL_NEAR : UPDATE_INTERVAL_FAR;

            if (currentTime - _lastFixedUpdateTime >= interval)
            {
                _lastFixedUpdateTime = currentTime;
                return true;
            }
            return false;
        }

        private bool ShouldUpdateThisFrame()
        {
            float currentTime = Time.time;
            float distSqr = (_player != null)
                ? (transform.position - _player.position).sqrMagnitude
                : FAR_DISTANCE_SQR;

            float interval = distSqr <= FAR_DISTANCE_SQR ? UPDATE_INTERVAL_NEAR : UPDATE_INTERVAL_FAR;

            if (currentTime - _lastUpdateTime >= interval)
            {
                _lastUpdateTime = currentTime;
                return true;
            }
            return false;
        }

        private void ApplyMovement()
        {
            Vector2 seekForce = CalculateSeek();
            Vector2 separationForce = CalculateSeparation();
            Vector2 finalVelocity = CalculateFinalVelocity(seekForce, separationForce);

            // Apply damping untuk smooth transition, hindari "snap" ke velocity baru
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, finalVelocity, _velocityDamping);
        }

        /// <summary>
        /// Flip sprite supaya enemy selalu menghadap player.
        /// </summary>
        private void UpdateFacing()
        {
            if (_spriteRenderer == null || _player == null) return;
            // Enemy di kiri player (dx < 0) → menghadap kanan (flipX = false)
            // Enemy di kanan player (dx > 0) → menghadap kiri (flipX = true)
            const float epsilon = 0.01f;
            bool shouldFaceLeft = transform.position.x > _player.position.x + epsilon;
            // Hanya flip kalau benar-benar berubah (hindari call berulang)
            if (_spriteRenderer.flipX != shouldFaceLeft)
                SetFacing(shouldFaceLeft);
        }

        /// <summary>
        /// Menghasilkan gaya tarik menuju player.
        /// </summary>
        private Vector2 CalculateSeek()
        {
            float distance = Vector2.Distance(transform.position, _player.position);

            // Jika sudah dalam attack range, gaya seek menjadi 0 (berhenti mengejar)
            // Namun separation tetap aktif agar mereka tidak tumpuk saat menyerang
            if (distance <= _attackRange) return Vector2.zero;

            return (_player.position - transform.position).normalized * _moveSpeed;
        }

        /// <summary>
        /// Menghasilkan gaya tolak dari enemy terdekat menggunakan spatial grid O(1) lookup.
        /// Hanya iterasi neighbor di cell saat ini dan 8 cell sekitarnya.
        /// </summary>
        private Vector2 CalculateSeparation()
        {
            // Safety: if move speed is 0 or negative, no separation needed
            if (_moveSpeed <= 0f) return Vector2.zero;

            Vector2 separationSum = Vector2.zero;
            Vector2 myPos = transform.position;

            // Cek 3x3 grid cells sekitar enemy
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int neighborCell = new(_currentGridCell.x + dx, _currentGridCell.y + dy);
                    int hash = GetGridHash(neighborCell);

                    if (!_spatialGrid.TryGetValue(hash, out var cellEnemies)) continue;

                    foreach (var other in cellEnemies)
                    {
                        if (other == this || other == null) continue;

                        Vector2 diff = myPos - (Vector2)other.transform.position;
                        float distance = diff.magnitude;

                        // Hanya apply separation jika dalam radius DAN di cell yang sama/berdekatan
                        if (distance > 0.01f && distance < _separationRadius)
                        {
                            // SIMPLE LINEAR FALLOFF (lebih stabil dari inverse square)
                            // Strength = (1 - distance/radius) * weight
                            // Ketika distance = 0 → strength = weight (max push)
                            // Ketika distance = radius → strength = 0 (no push)
                            float strength = (1f - distance / _separationRadius) * _separationWeight;
                            separationSum += diff.normalized * strength;
                        }
                    }
                }
            }

            // Tidak perlu normalize, biarkan magnitude alami dari sum
            // Cukup clamp agar tidak lebih besar dari move speed
            if (separationSum.magnitude > _moveSpeed)
                separationSum = separationSum.normalized * _moveSpeed;

            return separationSum;
        }

        private void UpdateCell()
        {
            Vector2Int newCell = new(
                Mathf.FloorToInt(transform.position.x / GridCellSize),
                Mathf.FloorToInt(transform.position.y / GridCellSize)
            );

            if (newCell != _currentGridCell)
            {
                if (_inGrid) UnregisterFromGrid();
                _currentGridCell = newCell;
                RegisterWithGrid();
            }
        }

        private void RegisterWithGrid()
        {
            int hash = GetGridHash(_currentGridCell);
            if (!_spatialGrid.TryGetValue(hash, out var list))
            {
                list = new List<EnemyAi>();
                _spatialGrid[hash] = list;
            }
            if (!list.Contains(this))
            {
                list.Add(this);
                _inGrid = true;
            }
        }

        private void UnregisterFromGrid()
        {
            int hash = GetGridHash(_currentGridCell);
            if (_spatialGrid.TryGetValue(hash, out var list))
            {
                list.Remove(this);
                if (list.Count == 0) _spatialGrid.Remove(hash);
            }
            _inGrid = false;
        }

        private static int GetGridHash(Vector2Int cell)
        {
            // Pairing function untuk hash unik dari 2D grid cell
            // Cantor pairing: (x + y) * (x + y + 1) / 2 + y
            // Diadaptasi untuk negative coords
            int x = cell.x >= 0 ? cell.x * 2 : -cell.x * 2 - 1;
            int y = cell.y >= 0 ? cell.y * 2 : -cell.y * 2 - 1;
            return (x + y) * (x + y + 1) / 2 + y;
        }

        private Vector2 CalculateFinalVelocity(Vector2 seek, Vector2 separation)
        {
            // Safety: if move speed is 0 or negative, return zero velocity
            if (_moveSpeed <= 0f) return Vector2.zero;

            // STRATEGI: Prioritaskan separation saat ada tabrakan
            // - Jika separation kuat (ada neighbor dekat), kurangi influence seek
            // - Jika tidak ada tabrakan, seek dominan

            float separationStrength = separation.magnitude / _moveSpeed; // 0-1 range
            separationStrength = Mathf.Clamp01(separationStrength);

            // Ketika separationStrength tinggi (neighbor dekat), seek dikurangi drastis
            // Gunakan exponential falloff: seek * (1 - strength²)
            Vector2 adjustedSeek = seek * (1f - separationStrength * separationStrength);

            Vector2 combined = adjustedSeek + separation;

            // Limit kecepatan maksimal
            if (combined.magnitude > _moveSpeed)
                combined = combined.normalized * _moveSpeed;

            return combined;
        }

        // -------------------------------------------------------------------
        // Public API - Statistics Service Access
        // -------------------------------------------------------------------
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float AttackSpeed => _attackSpeed;
        public float DefenseAmount => _defenseAmount;
        public float MoveSpeed => _moveSpeed;
        public Role Role => _role;

        public long GoldReward => _goldReward;
        public long GemReward => _gemReward;
        public long MeatReward => _meatReward;
        public int ExpReward => _expReward;
        public string DropTableId { get; private set; }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------
        /// <summary>
        /// Initialize enemy with data from JSON.
        /// Called by EnemySpawner right after instantiation.
        /// </summary>
        public void Initialize(EnemyData data, long goldReward = 0, long gemReward = 0, long meatReward = 0)
        {
            // --- Bagian baru untuk ganti sprite ---
            if (string.IsNullOrEmpty(data.id)) return;

            _enemyId = data.id;
            EnemyData = data;

            // Combat system
            _role           = data.role;
            _attackRange    = data.attackRange;
            _attackSpeed    = data.attackSpeed;
            _damage         = data.damage;
            _maxHealth      = data.health;
            _currentHealth  = _maxHealth;
            _defenseAmount  = Utilityku.FinalDefense(_role, _maxHealth);
            _moveSpeed      = data.moveSpeed;
            _knockbackDuration = data.knockback;
            _evasion  = data.evasion;
            _element        = data.element;

            _goldReward = goldReward;
            _gemReward  = gemReward;
            _meatReward = meatReward;
            _expReward  = data.exp;

            // Register dengan global health bar manager
            _enemyHealthBarManager.RegisterEnemy(this, _maxHealth);

            RefreshAuraVisual();
        }

        /// <summary>
        /// Updates the aura range visual based on EnemyData aura effects.
        /// </summary>
        private void RefreshAuraVisual()
        {
            if (_auraRangeRenderer == null) return;
            _auraRadius = 0f;
            if (EnemyData?.effects != null)
            {
                foreach (var ef in EnemyData.effects)
                {
                    if (ef?.aura == null) continue;
                    foreach (var act in ef.aura)
                    {
                        if (act == null) continue;
                        _auraRadius = Mathf.Max(_auraRadius, act.radius);

                        _auraPulseColor = act.effect switch
                        {
                            StatusEffectType.Slow => GameColors.rareBlue,
                            StatusEffectType.DamageReduction => GameColors.gemRuby,
                            _ => GameColors.empty,
                        };
                    }
                }
            }

            _hasAura = _auraRadius > 0f;

            // --------------------------------------------------
            // Aura boundary
            // --------------------------------------------------
            _auraRangeRenderer.enabled = _hasAura;
            if (_hasAura)
            {
                float diameter = _auraRadius * 2f;
                float scale = diameter * _auraScaleRange;
                _auraRangeRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                _auraRangeRenderer.color = GameColors.debugAtkRangeCyan.WithAlpha(0.09f);
            }
            
            if (_auraPulseRenderer != null)
            {
                _auraPulseRenderer.enabled = false;
                _auraPulseTimer = 0f;
            }
        }

        private void UpdateAuraPulse()
        {
            if (!_hasAura || _auraPulseRenderer == null) return;
            float deltaTime = Time.unscaledDeltaTime;
            _auraPulseTimer += deltaTime;

            float cycleDuration = _auraPulseDuration + _auraPulseInterval;
            float cycleTime = _auraPulseTimer % cycleDuration;

            // --------------------------------------------------
            // Jeda sebelum pulse berikutnya
            // --------------------------------------------------
            if (cycleTime >= _auraPulseDuration)
            {
                _auraPulseRenderer.enabled = false;
                return;
            }

            _auraPulseRenderer.enabled = true;

            // 0 → 1
            float t = cycleTime / _auraPulseDuration;

            // Smooth easing: mulai pelan, lalu melebar
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            // --------------------------------------------------
            // Scale dari pusat menuju batas aura
            // --------------------------------------------------
            float diameter = _auraRadius * 2f;
            float scale = diameter * easedT;
            _auraPulseRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            // --------------------------------------------------
            // Alpha: muncul → menghilang
            // --------------------------------------------------
            float alpha = Mathf.Sin(t * Mathf.PI) * _auraPulseMaxAlpha;
            Color color = _auraPulseColor;
            color.a = alpha;
            _auraPulseRenderer.color = color;
        }

        public float TakeDamage(DamageData damageData, bool canEvade = true)
        {
            float hitRate = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance.GetStat(SkillType.HitRate)
                : 100f;
            float hitChance = Mathf.Clamp(hitRate - _evasion, 5f, 100f);

            // Miss when HitRate (player, from equipment/passive/buff/card) fails to beat enemy evasion.
            if (canEvade && !Utilityku.Chance(hitChance))
            {
                ShowDamagePopup(1f, DamageType.Miss, CriticalType.None);
                return 1f;
            }

            _lastDamageSource = damageData.Source;

            // Calculate final damage value
            // 3-layer element pipeline:
            //   Layer 2: ElementMastery (universal, from Intelligence) → (1 + Mastery/1000)
            //   Layer 3: per-element bonus (equipment) → (1 + Bonus/100)
            //   Matchup: 1.5x counter / 0.75x same-or-unrelated / 0.5x weak
            // Element.None on both sides = 1x, no element bonus.
            float elementMultiplier = damageData.Element == Element.None || _element == Element.None
                ? 1f
                : (1f + PlayerStatsManager.Instance.GetStat(SkillType.ElementMastery) * 0.001f)
                    * Utilityku.ElementBonus(damageData.Element)
                    * Utilityku.ElementMultiplier(damageData.Element, _element);

            float penetration = PlayerStatsManager.Instance.GetStat(SkillType.Penetration);
            float rawDamage = damageData.GetFinalDamage(elementMultiplier);

            // Apply defense multiplier from status effects (DefenseBreak)
            float effectiveDefense = _defenseAmount * (_statusEffectController != null ? _statusEffectController.GetDefenseMultiplier() : 1f);
            float damageAfterDefense = Utilityku.FinalDamage(rawDamage, effectiveDefense, penetration);

            float damageBonus = EnemyData.IsBoss
                ? PlayerStatsManager.Instance.GetStat(SkillType.BossDamage)
                : EnemyData.IsElite
                    ? PlayerStatsManager.Instance.GetStat(SkillType.EliteDamage)
                    : 0f;
            damageAfterDefense *= 1f + damageBonus * 0.01f;
            float finalDamage = Mathf.Min(_currentHealth, damageAfterDefense);
            _currentHealth -= finalDamage;

            // Apply Defense Break after hit enemy if damage data has it
            if (damageData.DefenseBreak > 0f && _statusEffectController != null)
                ApplyDefenseBreak(damageData.DefenseBreakSource, damageData.DefenseBreakType, damageData.DefenseBreak, damageData.DefenseBreakDuration);

            // Apply Health Break (HeartBreak) if present
            if (damageData.HealthBreak > 0f && _statusEffectController != null)
                ReduceMaxHealth(damageData.HealthBreak);

            // Record damage taken
            RecordDamage(_lastDamageSource, finalDamage);

            // Show damage popup
            ShowDamagePopup(finalDamage, damageData.Type, damageData.Critical);

            // IMPORTANT:
            // Status effect sudah ditambahkan sebelum UI refresh.
            RefreshHealthBarStatus();

            // Mark statistics dirty
            EnemyStatisticsManager.Instance?.MarkDirty();

            // Process OnTakeDamage effects after damage is applied
            EnemyEffectProcessor.ProcessOnTakeDamage(this, damageData);

            // Check if dead
            if (_currentHealth <= 0) Die();

            return finalDamage;
        }

        /// <summary>
        /// Show damage popup at enemy position.
        /// </summary>
        private void ShowDamagePopup(float damage, DamageType type, CriticalType criticalType, string prefix = "")
        {
            if (_damagePopUpManager == null) return;

            // Position popup slightly above enemy center
            Vector3 popupPosition = transform.position + Vector3.up * 0.5f;

            DamagePopupData popupData = new(
                damage,
                type,
                criticalType, // Langsung kirim CriticalType, bukan bool
                prefix // No prefix for normal damage
            );

            // Pass transform sebagai target untuk tracking slot per-enemy
            _damagePopUpManager.ShowDamage(popupPosition, popupData, transform);
        }

        /// <summary>
        /// Record damage received from a specific source.
        /// </summary>
        public void RecordDamage(string damageSource, float amount)
        {
            // Call WaveManager to record total damage for the wave
            if (_waveManager != null && !string.IsNullOrEmpty(_enemyId))
                _waveManager.RecordDamage(_enemyId, damageSource, amount);
        }

        /// <summary>
        /// Apply knockback force to this enemy.
        /// Called by Projectile when knockback is triggered.
        /// Knockback always includes a short "soft stun" (0.2s) so that
        /// MoveTowardsPlayer() doesn't immediately override the knockback velocity.
        /// </summary>
        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (_rb == null) return;

            _rb.linearVelocity = direction * force;

            // Soft stun: extend stun end time so Update() skips MoveTowardsPlayer()
            // during the knockback window.
            float newStunEnd = Time.time + _knockbackDuration;
            if (newStunEnd > _stuntEndTime)
            {
                _stuntEndTime = newStunEnd;
                SetStunt(true);
            }
            _knockbackEndTime = Time.time + _knockbackDuration;
        }

        /// <summary>
        /// Apply stun effect to this enemy.
        /// </summary>
        public void ApplyStunt(float duration)
        {
            if (_isStunt) return; // Already stunned, ignore new stun

            _stuntEndTime = Time.time + duration;
            SetStunt(true);

            // Also add to status effect controller for consistency
            if (_statusEffectController != null)
                _statusEffectController.AddEffect(new StunStatus(duration));
        }

        /// <summary>
        /// Applies a slow effect to the enemy.
        /// </summary>
        /// <param name="percent">Speed percent (e.g., 0.51 mean "slow sebesar 51%")</param>
        public void ApplySlow(SlowSource source, SlowType type, float percent)
        {
            if (_statusEffectController == null) return;
            percent = Mathf.Clamp01(percent);
            _statusEffectController.AddEffect(new SlowStatus(percent, type == SlowType.Permanent ? float.MaxValue : 30f)
            {
                Source = source
            });
        }

        /// <summary>
        /// Removes the slow effect from the enemy for a specific source.
        /// </summary>
        public void RemoveSlow(SlowSource source)
        {
            if (_statusEffectController == null) return;
            _statusEffectController.RemoveEffect(StatusEffectType.Slow, e => e is SlowStatus s && s.Source == source);
        }

        /// <summary>
        /// Applies a defense break effect to the enemy.
        /// </summary>
        public void ApplyDefenseBreak(DefenseBreakSource source, DefenseBreakType type, float percent, float duration = 0f)
        {
            if (_statusEffectController == null) return;
            percent = Mathf.Clamp01(percent);
            if (type == DefenseBreakType.Temporary && duration <= 0f)
            {
                Debug.LogWarning(
                    $"[EnemyAi] Temporary Defense Break from {source} " +
                    $"requires duration > 0."
                );
                return;
            }
            _statusEffectController.AddEffectImmediate(new DefenseBreakStatus(
                percent,
                type == DefenseBreakType.Temporary ? duration : float.MaxValue,
                type
            )
            {
                Source = source
            });

            RefreshEnemyStatus();
        }

        /// <summary>
        /// Removes a defense break effect from the enemy.
        /// </summary>
        public void RemoveDefenseBreak(DefenseBreakSource source, DefenseBreakType type)
        {
            if (_statusEffectController == null) return;
            _statusEffectController.RemoveEffect(StatusEffectType.DefenseBreak,
                e => e is DefenseBreakStatus db && db.Source == source && db.BreakType == type);
                
            RefreshEnemyStatus();
        }

        /// <summary>
        /// Reduce max health by percentage (e.g., 0.1f = 10%).
        /// If current health exceeds new max health, clamp it.
        /// </summary>
        public void ReduceMaxHealth(float percent)
        {
            if (_statusEffectController == null)
            {
                // Fallback to direct modification
                percent = Mathf.Clamp01(percent * 0.01f);
                _maxHealth *= 1f - percent;
                if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;
                RefreshHealthBarStatus();
                EnemyStatisticsManager.Instance?.MarkDirty();
                return;
            }

            percent = Mathf.Clamp01(percent * 0.01f);
            _statusEffectController.AddEffect(new HeartBreakStatus(percent));

            // Immediately apply the max health reduction
            float newMaxHealth = _maxHealth * (1f - percent);
            ReduceMaxHealthTo(newMaxHealth);
        }

        /// <summary>
        /// Internal method to set max health to a specific value and clamp current health.
        /// </summary>
        public void ReduceMaxHealthTo(float newMaxHealth)
        {
            _maxHealth = Mathf.Max(1f, newMaxHealth);
            if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;
            RefreshHealthBarStatus();
            RefreshEnemyStatus();
            EnemyStatisticsManager.Instance?.MarkDirty();
        }

        /// <summary>
        /// Check if enemy has any active Defense Break effect.
        /// Used by health bar UI to show DefenseBreak indicator.
        /// </summary>
        public bool HasActiveDefenseBreak()
        {
            return _statusEffectController != null && _statusEffectController.HasEffect(StatusEffectType.DefenseBreak);
        }

        /// <summary>
        /// Check if enemy's max health has been reduced from original.
        /// Used by health bar UI to show HeartBreak indicator.
        /// </summary>
        public bool HasReducedMaxHealth()
        {
            return _statusEffectController != null && _statusEffectController.HasEffect(StatusEffectType.HeartBreak);
        }

        /// <summary>
        /// Forces the enemy health bar UI to refresh its status indicators.
        /// Used after applying Defense Break / Heart Break.
        /// </summary>
        public void RefreshHealthBarStatus()
            => _enemyHealthBarManager?.UpdateEnemyHealth(this, _currentHealth);
        /// <summary>
        /// Forces the enemy to refresh its status indicators.
        /// Used after applying Defense Break / Heart Break.
        /// </summary>
        public void RefreshEnemyStatus()
            => _enemyHealthBarManager?.UpdateEnemyStatus(this);

        /// <summary>
        /// Handle enemy death.
        /// </summary>
        private void Die()
        {
            // Record kill in save system with damage source
            RecordEnemyKill(_lastDamageSource);

            string player = UltimateDMG.Player.ToString();
            string lightning = UltimateDMG.Lightning.ToString();
            string cloud = UltimateDMG.Cloud.ToString();

            // Register kill for Lightning ultimate trigger (if killed by player or lightning)
            if (_lastDamageSource == player || _lastDamageSource == lightning)
            {
                if (LightningHandler.RegisterKill())
                {
                    // Lightning ready to trigger - spawn it at player position
                    _ultimateManager.TrySpawn(lightning, _playerComponent.transform.position, _playerComponent);
                }
            }

            // Try to spawn toxic death cloud at death position (if killed by player or cloud)
            if (_lastDamageSource == player || _lastDamageSource == cloud)
            {
                _ultimateManager.TryGenerateStack(cloud, _playerComponent, transform.position);
            }

            // Unregister dari manager
            _enemyHealthBarManager.UnregisterEnemy(this);

            // Unregister from statistics service
            EnemyStatisticsManager.Instance?.Unregister(this);

            // Notify aura manager to clean up any active auras from this enemy
            EnemyAuraManager.Instance?.OnEnemyDeath(this);

            DropRewards();
            DropItemDrops();

            EnemyKillMission();

            // TODO: Add death animation, particle effects, etc.
            Destroy(gameObject);
        }

        private void EnemyKillMission()
        {
            if (EnemyData == null) return;

            var missionService = MissionService.Instance;
            if (missionService == null) return;

            // Any enemy whose Role == BOSS
            // counts toward BossKilled missions.
            if (EnemyData.IsBoss)
            {
                missionService.UpdateProgress(MissionEventType.BossKilled, EnemyData.id, 1);
                return;
            }

            // Generic kill mission:
            // Any non-boss enemy counts toward generic EnemyKilled missions.
            missionService.UpdateProgress(MissionEventType.EnemyKilled, EnemyData.id, 1);

            // Specific enemy mission:
            // "Kill X Goblins", "Kill X Slimes", etc.
            missionService.UpdateProgress(MissionEventType.SpecificEnemyKilled, EnemyData.id, 1);
        }

        /// <summary>
        /// Record this enemy kill in the save system, grouped by role.
        /// </summary>
        /// <param name="damageSource">The source of damage that killed the enemy (e.g., UltimateDMG.Player.ToString(), "bomb", "tank").</param>
        private void RecordEnemyKill(string damageSource)
        {
            if (string.IsNullOrEmpty(_enemyId)) return;
            _saveManager.RecordEnemyKill(_enemyId, damageSource, _role.ToString());
            _saveManager.AddKills(_waveManager.CurrentTier, 1);
        }

        private void DropRewards()
        {
            if (_economyManager == null) return;

            // Gold & Exp: Instant add (no pickup)
            RewardManager.Instance.GiveEnemyReward(_goldReward, _expReward, gameObject.name);

            // Gem: Spawn physical pickup with re-check of daily limit
            if (_gemReward > 0)
            {
                // Re-check if daily limit reached at death time
                if (!_saveManager.HasReachedDailyGemLimit())
                {
                    // Record gem drop and spawn (re-enforce limit)
                    int actualGems = _saveManager.RecordGemDrop(1);
                    if (actualGems > 0) SpawnItem(CurrencyType.Gem, actualGems);
                }
            }

            // Meat: Spawn physical pickup
            if (_meatReward > 0) SpawnItem(CurrencyType.Meat, _meatReward);

        }

        /// <summary>
        /// Roll material drops defined in dataEnemy.json (dropItems) and grant them to inventory.
        /// Each entry rolls independently; Weight is a percent (0-100), consistent with
        /// Utilityku.Chance and DropEntry.Weight semantics elsewhere. Uses InventoryService.AddItem
        /// so stacking, capacity, events, and save-dirty flow stay centralized.
        /// Normal (non-boss) enemies are capped at 2 successful item drops to prevent inventory flooding.
        /// </summary>
        private void DropItemDrops()
        {
            if (EnemyData?.dropItems == null || EnemyData.dropItems.Length == 0) return;
            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            int currentTier = WaveManager.Instance?.CurrentTier ?? 1;
            int droppedCount = 0;

            foreach (var entry in EnemyData.dropItems)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;
                // Tier gate: material rarity tier must already be reachable (T1=rare1, T2=rare2, ...)
                if (entry.MinTier > currentTier) continue;

                // DropRate increases drop chance directly.
                float dropRate = PlayerStatsManager.Instance.GetStat(SkillType.DropRate);
                float finalWeight = entry.Weight * (1f + dropRate * 0.01f);
                if (!Utilityku.Chance(finalWeight)) continue;

                int min = Mathf.Max(1, entry.MinCount);
                int max = Mathf.Max(min, entry.MaxCount);
                int quantity = Random.Range(min, max + 1);

                if (ItemDatabase.Instance != null && ItemDatabase.Instance.GetItem(entry.ItemId) == null)
                {
                    Debug.LogWarning($"[EnemyAi] Drop item not found in dataItems.json: {entry.ItemId}");
                    continue;
                }

                inventory.AddItem(entry.ItemId, quantity);
                droppedCount++;

                // Drop Bag: record ONLY after the drop truly succeeded (Chance + MinTier passed,
                // AddItem executed). Single authoritative point — no duplicate events.
                if (DropBagManager.Instance != null)
                    DropBagManager.Instance.AddDrop(entry.ItemId, quantity);

                // Cap normal enemies at 2 item drops; bosses keep full drop potential.
                if (!EnemyData.IsBoss && droppedCount >= 2) break;
            }
        }

        /// <summary>
        /// Spawn item(s) at enemy death position with spread animation.
        /// Spawns 1 parent item that handles spreading additional items.
        /// Items.cs handles the spread animation and magnetic collection.
        /// </summary>
        private void SpawnItem(CurrencyType currencyType, long amount)
        {
            if (_economyManager == null) return;
            if (_itemPrefab == null)
            {
                Debug.LogWarning($"[EnemyAi] Currency item prefab not assigned! Adding {currencyType} directly.");
                _economyManager.AddCurrency(currencyType, amount, $"Kill {gameObject.name}");
                return;
            }

            // Spawn 1 item at enemy death position (center)
            // Items.cs will handle spawning additional items with spread effect
            GameObject itemObj = Instantiate(_itemPrefab, transform.position, Quaternion.identity, UIManager.Instance.DropRoot);
            if (itemObj.TryGetComponent<CurrencyPickup>(out var item))
            {
                item.Initialize(currencyType, amount);
            }
            else
            {
                Debug.LogError($"[EnemyAi] Item prefab missing CurrencyPickup component!");
                Destroy(itemObj);
            }
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        public void SetStunt(bool isStunt)
        {
            _isStunt = isStunt;
            _dizzyEffect.SetActive(isStunt);
        }

        public void SetAttackSpeed(float speed)
        {
            _attackSpeed = speed;
        }

        public void SetMoveSpeed(float speed)
        {
            _moveSpeed = speed;
        }

        public void SetDefenseAmount(float amount)
        {
            _defenseAmount = amount;
        }

        public Transform PlayerTransform => _player;

        public void SetSprite(Sprite sprite) => _spriteRenderer.sprite = sprite;
        public void SetFacing(bool faceLeft) => _spriteRenderer.flipX = faceLeft;

        /// <summary>
        /// Check if enemy is within attack range of the player.
        /// </summary>
        private bool IsInAttackRange()
        {
            if (_player == null) return false;
            float distance = Vector2.Distance(transform.position, _player.position);
            return distance <= _attackRange;
        }

        /// <summary>
        /// Attack the player by dealing damage.
        /// Called when attack cooldown expires and enemy is in range.
        /// </summary>
        private void AttackPlayer()
        {
            if (_playerComponent == null) return;

            // Get projectile from pool - don't parent to enemy since enemy moves
            Projectile projectile = ProjectilePool.Instance.Get();
            if (projectile != null)
            {
                projectile.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
                projectile.InitializeFromEnemy(_player.transform, this);
            }

            // Process OnHit effects after successful attack
            EnemyEffectProcessor.ProcessOnHit(this, _playerComponent);
        }

#if UNITY_EDITOR
        // Gizmo untuk visualisasi attack range dan separation radius
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            Gizmos.color = GameColors.debugBlueGizmo.WithAlpha(0.5f); // Blue dengan transparansi
            Gizmos.DrawWireSphere(transform.position, _separationRadius);
        }
#endif
    }
}