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
using UnityEngine.Pool;
using IdleDefenseSurvival.Mission;
using IdleDefenseSurvival.Stats;

/// <summary>
/// Handles basic enemy AI for the auto‑shooter game.
/// - Moves towards the player while outside the attack range.
/// - Stops moving when within the attack range.
/// - Can be knocked back by the player; movement resumes after the knockback duration.
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

        // Slow effect tracking
        private readonly Dictionary<SlowSource, SlowEffect> _slowEffects = new();
        private float _originalMoveSpeed;

        // Optimasi: Buffer dan Contact Filter untuk menghindari GC Alloc setiap frame
        private Collider2D[] _neighborBuffer = new Collider2D[16];
        private ContactFilter2D _enemyContactFilter;

        public float EnemyAttackDamage => _damage;
        public float Evasion => _evasion;
        public Vector3 HealthBarWorldPosition
        {
            get
            {
                if (_spriteRenderer == null) return transform.position;
                Bounds bounds = _spriteRenderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + 0.15f, transform.position.z);
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

            // Setup ContactFilter2D untuk deteksi enemy saja
            _enemyContactFilter = new ContactFilter2D();
            _enemyContactFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _enemyContactFilter.useTriggers = false;
            
            _saveManager = SaveManager.Instance;
            _waveManager = WaveManager.Instance;
            _economyManager = EconomyManager.Instance;
            _ultimateManager = UltimateManager.Instance;
            _damagePopUpManager = DamagePopupManager.Instance;
            _enemyHealthBarManager = EnemyHealthBarManager.Instance;
        }

        private void Update()
        {
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

            UpdateDefenseBreaks();
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            if (Time.time < _stuntEndTime) return;

            ApplyMovement();
            UpdateFacing();
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
        /// Menghasilkan gaya tolak dari enemy terdekat menggunakan linear falloff.
        /// Recalculate setiap frame agar force selalu akurat (no caching).
        /// </summary>
        private Vector2 CalculateSeparation()
        {
            // Safety: if move speed is 0 or negative, no separation needed
            if (_moveSpeed <= 0f) return Vector2.zero;

            // Gunakan API baru Unity 6 untuk menghindari GC Allocation
            int count = Physics2D.OverlapCircle(transform.position, _separationRadius, _enemyContactFilter, _neighborBuffer);

            Vector2 separationSum = Vector2.zero;
            int neighborsFound = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _neighborBuffer[i];
                if (col.gameObject == gameObject) continue;

                Vector2 diff = (Vector2)transform.position - (Vector2)col.transform.position;
                float distance = diff.magnitude;

                if (distance > 0.01f && distance < _separationRadius)
                {
                    // SIMPLE LINEAR FALLOFF (lebih stabil dari inverse square)
                    // Strength = (1 - distance/radius) * weight
                    // Ketika distance = 0 → strength = weight (max push)
                    // Ketika distance = radius → strength = 0 (no push)
                    float strength = (1f - distance / _separationRadius) * _separationWeight;
                    separationSum += diff.normalized * strength;
                    neighborsFound++;
                }
            }

            // Tidak perlu normalize, biarkan magnitude alami dari sum
            // Cukup clamp agar tidak lebih besar dari move speed
            if (separationSum.magnitude > _moveSpeed)
                separationSum = separationSum.normalized * _moveSpeed;

            return separationSum;
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
            _originalMaxHealth = data.health; // Track original for HeartBreak detection
            _currentHealth  = _maxHealth;
            _defenseAmount  = Utilityku.FinalDefense(_role, _maxHealth);
            _originalDefenseAmount = _defenseAmount;
            _moveSpeed      = data.moveSpeed;
            _originalMoveSpeed = _moveSpeed;
            _knockbackDuration = data.knockback;
            _evasion  = data.evasion;
            _element        = data.element;

            _goldReward = goldReward;
            _gemReward  = gemReward;
            _meatReward = meatReward;
            _expReward  = data.exp;

            // Register dengan global health bar manager
            _enemyHealthBarManager.RegisterEnemy(this, _maxHealth);
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
            float finalDamage = Utilityku.FinalDamage(rawDamage, _defenseAmount, penetration);
            finalDamage = Mathf.Min(_currentHealth, finalDamage);
            _currentHealth -= finalDamage;

            // Apply Defense Break after hit enemy if damage data has it
            if (damageData.DefenseBreak > 0f)
                ApplyDefenseBreak(damageData.DefenseBreakSource, damageData.DefenseBreakType, damageData.DefenseBreak, damageData.DefenseBreakDuration);

            // Record damage taken
            RecordDamage(_lastDamageSource, finalDamage);

            // Show damage popup
            ShowDamagePopup(finalDamage, damageData.Type, damageData.Critical);

            // Update health bar melalui manager
            _enemyHealthBarManager.UpdateEnemyHealth(this, _currentHealth);

            // Mark statistics dirty
            EnemyStatisticsManager.Instance?.MarkDirty();

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
        }

        /// <summary>
        /// Applies a slow effect to the enemy.
        /// </summary>
        /// <param name="percent">Speed percent (e.g., 0.51 mean "slow sebesar 51%")</param>
        public void ApplySlow(SlowSource source, SlowType type, float percent)
        {
            percent = Mathf.Clamp01(percent);
            if (!_slowEffects.TryGetValue(source, out var effect))
            {
                effect = new SlowEffect { Source = source };
                _slowEffects[source] = effect;
            }
            effect.Type = type;
            effect.Percent = 1f - percent;
            RecalculateMoveSpeed();
        }

        /// <summary>
        /// Removes the slow effect from the enemy.
        /// </summary>
        public void RemoveSlow(SlowSource source)
        {
            _slowEffects.Remove(source);
            RecalculateMoveSpeed();
        }

        private void RecalculateMoveSpeed()
        {
            float multiplier = 1f;
            foreach (var effect in _slowEffects.Values)
                multiplier *= effect.Percent; // Perm/Aura/Temporary all multiply identically

            _moveSpeed = _originalMoveSpeed * multiplier;
            EnemyStatisticsManager.Instance?.MarkDirty();
        }

        // Defense Break effect tracking
        private const int MAX_DEFENSE_BREAK_STACKS = 5;
        private readonly Dictionary<DefenseBreakSource, DefenseBreakEffect> _defenseBreakEffects = new();
        private float _originalDefenseAmount;

        public void ApplyDefenseBreak(
            DefenseBreakSource source, DefenseBreakType type, float percent, float duration = 0f)
        {
            percent = Mathf.Clamp01(percent);
            if (type == DefenseBreakType.Temporary && duration <= 0f)
            {
                Debug.LogWarning(
                    $"[EnemyAi] Temporary Defense Break from {source} " +
                    $"requires duration > 0."
                );
                return;
            }
            if (!_defenseBreakEffects.TryGetValue(source, out var effect))
            {
                effect = new DefenseBreakEffect
                {
                    Source = source,
                    Type = type,
                    Percent = percent,
                    StackCount = 1,
                    ExpireTime = type == DefenseBreakType.Temporary ? Time.time + duration : 0f
                };
                _defenseBreakEffects[source] = effect;
            }
            else
            {
                if (effect.Type != type)
                {
                    Debug.LogWarning(
                        $"[EnemyAi] Defense Break source {source} " +
                        $"already has type {effect.Type}, cannot apply {type}."
                    );
                    return;
                }
                // Source sama → stack sampai maksimum 5.
                effect.StackCount = Mathf.Min(effect.StackCount + 1, MAX_DEFENSE_BREAK_STACKS);
                // Gunakan nilai effect pertama sebagai nilai per-stack.
                effect.Percent = percent;
                // Temporary → refresh duration setiap kali terkena lagi.
                if (effect.Type == DefenseBreakType.Temporary)
                    effect.ExpireTime = Time.time + duration;
            }
            RecalculateDefense();
        }

        public void RemoveDefenseBreak(DefenseBreakSource source, DefenseBreakType type)
        {
            if (!_defenseBreakEffects.TryGetValue(source, out var effect)) return;
            if (effect.Type != type) return;
            _defenseBreakEffects.Remove(source);
            RecalculateDefense();
        }

        private void RecalculateDefense()
        {
            float totalBreak = 0f;
            foreach (var effect in _defenseBreakEffects.Values)
                totalBreak += effect.Percent * effect.StackCount;
            float calculatedDefense = _originalDefenseAmount * (1f - totalBreak);
            _defenseAmount = Mathf.Max(-_originalDefenseAmount, calculatedDefense);
            EnemyStatisticsManager.Instance?.MarkDirty();
        }

        private void UpdateDefenseBreaks()
        {
            if (_defenseBreakEffects.Count == 0) return;
            var expiredSources = ListPool<DefenseBreakSource>.Get();
            foreach (var pair in _defenseBreakEffects)
            {
                var effect = pair.Value;
                if (effect.Type == DefenseBreakType.Temporary && Time.time >= effect.ExpireTime)
                    expiredSources.Add(pair.Key);
            }
            bool changed = expiredSources.Count > 0;
            foreach (var source in expiredSources)
                _defenseBreakEffects.Remove(source);
            ListPool<DefenseBreakSource>.Release(expiredSources);
            if (changed) RecalculateDefense();
        }

        /// <summary>
        /// Reduce max health by percentage (e.g., 0.1f = 10%).
        /// If current health exceeds new max health, clamp it.
        /// </summary>
        public void ReduceMaxHealth(float percent)
        {
            percent = Mathf.Clamp01(percent * 0.01f);
            _maxHealth *= 1f - percent;
            if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;
            _enemyHealthBarManager.UpdateEnemyHealth(this, _currentHealth);
            EnemyStatisticsManager.Instance?.MarkDirty();
        }

        /// <summary>
        /// Check if enemy has any active Defense Break effect.
        /// Used by health bar UI to show DefenseBreak indicator.
        /// </summary>
        public bool HasActiveDefenseBreak()
        {
            return _defenseBreakEffects.Count > 0;
        }

        /// <summary>
        /// Check if enemy's max health has been reduced from original.
        /// Used by health bar UI to show HeartBreak indicator.
        /// </summary>
        public bool HasReducedMaxHealth()
        {
            // We need to track original max health to compare
            // Since we don't store original max health, we'll use a simple heuristic:
            // if defense break effects include Permanent type, it likely reduced max health
            // Actually, ReduceMaxHealth directly reduces _maxHealth, so we need to track original
            return _maxHealth < _originalMaxHealth;
        }

        // Store original max health for HeartBreak detection
        private float _originalMaxHealth;

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
            if (EnemyData.isBoss)
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
        /// </summary>
        private void DropItemDrops()
        {
            if (EnemyData?.dropItems == null || EnemyData.dropItems.Length == 0) return;
            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            int currentTier = WaveManager.Instance?.CurrentTier ?? 1;
            foreach (var entry in EnemyData.dropItems)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;
                // Tier gate: material rarity tier must already be reachable (T1=rare1, T2=rare2, ...)
                if (entry.MinTier > currentTier) continue;
                if (!Utilityku.Chance(entry.Weight)) continue;

                int min = Mathf.Max(1, entry.MinCount);
                int max = Mathf.Max(min, entry.MaxCount);
                int quantity = Random.Range(min, max + 1);

                if (ItemDatabase.Instance != null && ItemDatabase.Instance.GetItem(entry.ItemId) == null)
                {
                    Debug.LogWarning($"[EnemyAi] Drop item not found in dataItems.json: {entry.ItemId}");
                    continue;
                }

                inventory.AddItem(entry.ItemId, quantity);

                // Drop Bag: record ONLY after the drop truly succeeded (Chance + MinTier passed,
                // AddItem executed). Single authoritative point — no duplicate events.
                if (DropBagManager.Instance != null)
                    DropBagManager.Instance.AddDrop(entry.ItemId, quantity);
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

        /// <summary>
        /// Clear all aura effects when enemy is disabled (for object pooling).
        /// Ensures effects don't carry over to the next reuse.
        /// </summary>
        private void OnDisable() => ClearAllEffects();

        /// <summary>
        /// Clears all slow and defense break effects from this enemy.
        /// Called on disable to prevent pooling carry-over.
        /// </summary>
        public void ClearAllEffects()
        {
            _slowEffects.Clear();
            RecalculateMoveSpeed();
            _defenseBreakEffects.Clear();
            RecalculateDefense();
        }

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
