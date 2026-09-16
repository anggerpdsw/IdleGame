using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.UI;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Player;
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

        [Header("Pickup Prefab")]
        [Tooltip("Single pickup prefab for all currency types (Gem, Meat)")]
        [SerializeField] private GameObject _itemPrefab;

        // Regeneration aura stop-move behavior
        private bool _hasRegenerationAura;
        private bool _isRegenerating;
        private const float REGEN_HP_THRESHOLD = 0.90f;

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

        // Spatial grid state
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
            // Unregister from enemy-to-enemy aura manager
            EnemyAuraManager.Instance?.UnregisterEnemyAuraSource(this);
        }

        private void Update()
        {
            // Update regeneration state
            if (_hasRegenerationAura)
            {
                float hpPercent = _currentHealth / Mathf.Max(1f, _maxHealth);
                bool wasRegenerating = _isRegenerating;
                _isRegenerating = hpPercent < REGEN_HP_THRESHOLD;

                // Refresh icon saat status berubah
                if (_isRegenerating != wasRegenerating) RefreshEnemyStatus();
            }

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
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            if (Time.time < _stuntEndTime) return;

            // Throttle FixedUpdate for distant enemies (same intervals as Update)
            if (!ShouldFixedUpdate()) return;

            // Update spatial grid cell
            UpdateCell();

            // Regeneration behavior:
            // - HP < 90% + player in range → flee (move away)
            // - HP < 90% + player out of range → stop
            // - HP ≥ 90% → normal (seek player)
            if (_hasRegenerationAura && _isRegenerating)
            {
                bool playerInRange = IsInAttackRange();
                if (playerInRange)
                {
                    // Flee from player while regenerating
                    ApplyFleeMovement();
                }
                else
                {
                    // Stop movement when player is far
                    if (_rb.linearVelocity != Vector2.zero)
                        _rb.linearVelocity = Vector2.zero;
                }
            }
            else
            {
                // Normal movement (seek player)
                ApplyMovement();
            }

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
            Vector2 seekForce = EnemyMovementCalculator.CalculateSeek(
                transform.position, _player.position, _attackRange, _moveSpeed);
            Vector2 separationForce = EnemyMovementCalculator.CalculateSeparation(
                this, transform.position, _currentGridCell, _separationRadius, _separationWeight, _moveSpeed);
            Vector2 finalVelocity = EnemyMovementCalculator.CalculateFinalVelocity(
                seekForce, separationForce, _moveSpeed);

            // Apply damping untuk smooth transition, hindari "snap" ke velocity baru
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, finalVelocity, _velocityDamping);
        }

        /// <summary>
        /// Flee movement - mundur menjauhi player sambil tetap menjaga separation.
        /// Dipakai saat enemy regenerasi dan player dalam attack range.
        /// </summary>
        private void ApplyFleeMovement()
        {
            Vector2 fleeForce = EnemyMovementCalculator.CalculateFlee(
                transform.position, _player.position, _moveSpeed);
            Vector2 separationForce = EnemyMovementCalculator.CalculateSeparation(
                this, transform.position, _currentGridCell, _separationRadius, _separationWeight, _moveSpeed);
            Vector2 finalVelocity = EnemyMovementCalculator.CalculateFinalVelocity(
                fleeForce, separationForce, _moveSpeed);

            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, finalVelocity, _velocityDamping);
        }

        /// <summary>
        /// Flip sprite supaya enemy selalu menghadap player.
        /// </summary>
        private void UpdateFacing()
        {
            if (_spriteRenderer == null || _player == null) return;
            bool shouldFaceLeft = EnemyMovementCalculator.ShouldFaceLeft(transform.position, _player.position);
            // Hanya flip kalau benar-benar berubah (hindari call berulang)
            if (_spriteRenderer.flipX != shouldFaceLeft)
                SetFacing(shouldFaceLeft);
        }

        private void UpdateCell()
        {
            Vector2Int newCell = new(
                Mathf.FloorToInt(transform.position.x / EnemySpatialGrid.CellSize),
                Mathf.FloorToInt(transform.position.y / EnemySpatialGrid.CellSize)
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
            EnemySpatialGrid.Register(this, _currentGridCell);
            _inGrid = true;
        }

        private void UnregisterFromGrid()
        {
            EnemySpatialGrid.Unregister(this, _currentGridCell);
            _inGrid = false;
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

            // Check if has Regeneration aura
            _hasRegenerationAura = false;
            if (data.effects != null)
            {
                foreach (var eff in data.effects)
                {
                    if (eff?.aura == null) continue;
                    foreach (var act in eff.aura)
                    {
                        if (act?.effect == StatusEffectType.Regeneration)
                        {
                            _hasRegenerationAura = true;
                            break;
                        }
                    }
                    if (_hasRegenerationAura) break;
                }
            }

            // Refresh aura visuals via component if exists
            if (TryGetComponent<EnemyAuraVisualController>(out var auraVisual))
                auraVisual.RefreshAuraVisual(data);

            // Register as aura source for enemy-to-enemy auras (e.g., Iron Guardian Damage Reduction)
            EnemyAuraManager.Instance?.RegisterEnemyAuraSource(this);
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

            // Apply Damage Reduction aura (from Iron Guardian etc.) AFTER defense
            // This is a final damage multiplier: damage * (1 - reductionPercent)
            float damageReductionMultiplier = _statusEffectController != null ? _statusEffectController.GetDamageReductionMultiplier() : 1f;
            // Additional 50% reduction when regeneration aura active
            if (_hasRegenerationAura && _isRegenerating)
                damageReductionMultiplier *= 0.5f;
            damageAfterDefense *= damageReductionMultiplier;

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
            // ponytail: Undeath enemies immune to knockback
            if (EnemyData != null && EnemyData.role == Role.Undeath) return;
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
        /// Heal enemy by amount. Clamps to MaxHealth. Used by Vampiric LifeSteal and Regeneration aura.
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            if (_currentHealth >= _maxHealth) return;

            _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
            RefreshHealthBarStatus();
            EnemyStatisticsManager.Instance?.MarkDirty();

            // Show heal popup
            if (amount >= 1f) ShowDamagePopup(amount, DamageType.Heal, CriticalType.None, "+");
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
        /// Check if enemy currently has an active DamageReduction aura effect.
        /// Used by health bar UI to show DamageReduction indicator icon.
        /// </summary>
        public bool HasActiveDamageReduction()
        {
            bool hasEffectReduction = _statusEffectController != null
                && _statusEffectController.GetTotalDamageReductionPercent() > 0f;
            bool hasRegenReduction = _hasRegenerationAura && _isRegenerating;
            return hasEffectReduction || hasRegenReduction;
        }

        /// <summary>
        /// Forces the enemy health bar UI to refresh its status indicators.
        /// </summary>
        public void RefreshHealthBarStatus()
            => _enemyHealthBarManager?.UpdateEnemyHealth(this, _currentHealth);
        /// <summary>
        /// Forces the enemy to refresh its status indicators.
        /// Used after applying effect to enemy.
        /// </summary>
        public void RefreshEnemyStatus()
            => _enemyHealthBarManager?.UpdateEnemyStatus(this);

        /// <summary>
        /// Handle enemy death.
        /// </summary>
        private void Die()
        {
            // Delegate to static death handler
            EnemyDeathHandler.ProcessDeath(this, _lastDamageSource);
        }

        /// <summary>
        /// Internal accessor for item prefab (used by EnemyRewardDistributor).
        /// </summary>
        internal GameObject ItemPrefab => _itemPrefab;

        /// <summary>
        /// Internal accessor for managers (used by death/reward handlers).
        /// </summary>
        internal SaveManager SaveMgr => _saveManager;
        internal WaveManager WaveMgr => _waveManager;
        internal EconomyManager EconomyMgr => _economyManager;
        internal UltimateManager UltimateMgr => _ultimateManager;

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