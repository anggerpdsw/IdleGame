using UnityEngine;
using System.Collections;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Ultimate;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using System;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    public class Player : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static Player _instance;
        public static Player Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }
                
        public event Action OnHealthChanged;
        public event Action OnManaChanged;

        [Header("Visualization")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image fillHealth;
        [SerializeField] private Slider manaBar;
        [SerializeField] private Image fillMana;
        [SerializeField] private SpriteRenderer _attackRangeRenderer;
        // Barrier visual – child GameObject with SpriteRenderer
        [SerializeField] private SpriteRenderer _barrierRenderer;
        // Shield visual - separate from barrier
        [SerializeField] private SpriteRenderer _shieldRenderer;
        [SerializeField] private Image _shieldCooldownImage; // Radial fill image for cooldown UI
        private float _attackRangeSpeedRotate = 2f;
        // Immunity flag for DeathDefy
        private bool _isImmune;

        // Runtime state
        private List<TankInstance> _activeTanks;
        private float _attackTimer;
        private float _regenTimer;
        private float _currentHealth;
        private float _currentMana;
        private int _enemyLayerMask;
        private Transform _currentTarget;
        private UltimateManager _ultimateManager;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint)
            : 0f;
        public float CurrentMana => _currentMana;
        public float MaxMana => PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint)
            : 0f;

        // Shield system
        [SerializeField] private float _currentShield = 0f;
        private float _maxShield = 0f;
        private bool _shieldGranted;
        private float _shieldCooldownTimer = 0f;
        private const float ShieldCooldownDuration = 30f;
        private bool _isShieldOnCooldown = false;

        [SerializeField] private AudioSource _sfxSource;
        public AudioSource SfxSource => _sfxSource;

        // Movement
        [SerializeField] private Joystick _joyStick;
        private Rigidbody2D rb;

        public float AttackRange;

        private void Awake()
        {
            // Initialize singleton
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // Removed DontDestroyOnLoad - Player will be in Game scene

            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _activeTanks = new List<TankInstance>();

            // Ensure barrier renderer starts disabled
            if (_barrierRenderer != null)
                _barrierRenderer.enabled = false;
        }

        /// <summary>
        /// Reload player base stats from dataPlayer.json.
        /// Skills have no levels — modifiers (Constitution/Strength/etc.) are
        /// applied by ModifierManager on top of these base values.
        /// </summary>
        public void ReloadStats()
        {
            BaseStatLoader.Instance.LoadBaseStats();

            // Update visuals
            DrawAttackRange();

            // Always start at full health and mana on game start
            float health = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            _currentHealth = health;
            float mana = PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint);
            _currentMana = mana;

            UpdateHealthUI();
            UpdateManaUI();
            UpdateShieldVisual();
            AttackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
        }

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            // Use a coroutine to wait until essential singletons are initialized.
            StartCoroutine(InitializePlayer());
        }

        private IEnumerator InitializePlayer()
        {
            yield return new WaitUntil(() => BootstrapController.IsInitialized);
            yield return new WaitUntil(() =>
                PlayerStatsManager.Instance != null &&
                BaseStatLoader.Instance != null
            );

            if (SaveManager.Instance != null)
                yield return new WaitUntil(() => SaveManager.Instance.IsSaveLoaded);

            ReloadStats();

            UpdateHealthUI();
            UpdateShieldVisual();
            UpdateShieldCooldownUI();

            DrawAttackRange();

            _ultimateManager = UltimateManager.Instance;
        }

        private void Update()
        {
            FaceTarget(_currentTarget);
            TryAttack();
            TryTriggerUltimateWithCooldown();
            TryRegeneration();
            UpdateShield(); // Shield system update
            // Aura effects now handled by AuraCollider trigger system

            _attackRangeRenderer.transform.Rotate(0, 0, _attackRangeSpeedRotate * Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (_joyStick.joyStickVec.y != 0)
                MoveTo();
            else
                rb.linearVelocity = Vector2.zero;
        }

        private void MoveTo()
        {
            if (!UltimateManager.Instance.TryGetUltimate("Movement", out var ultimateData)) return;
            if(!CanAfford(ultimateData.manaCost)) return;
            float playerspeed = PlayerStatsManager.Instance.GetStat(SkillType.MoveSpeed) * 0.03f;
            rb.linearVelocity = new Vector2(_joyStick.joyStickVec.x * playerspeed, _joyStick.joyStickVec.y * playerspeed);
            SpendMana(ultimateData.manaCost);
        }

        private void FaceTarget(Transform target)
        {
            if (target == null) return;
            Vector3 scale = _visual.localScale;
            scale.x = target.position.x < transform.position.x ? -1f : 1f;
            _visual.localScale = scale;
        }

        private void TryAttack()
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0)
            {
                Attack();
                _attackTimer = 1f / PlayerStatsManager.Instance.GetStat(SkillType.AttackSpeed);
            }
        }

        private void Attack()
        {
            // Find all enemies within attack range
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, PlayerStatsManager.Instance.GetStat(SkillType.AttackRange), _enemyLayerMask);
            if (hits.Length == 0) return;

            // Filter valid enemies and sort by distance (closest first)
            List<Transform> targets = hits
                .Where(hit => hit.TryGetComponent<EnemyAi>(out _))
                .OrderBy(hit => Vector2.Distance(transform.position, hit.transform.position))
                .Select(hit => hit.transform)
                .ToList();

            // Determine how many distinct targets to fire at based on multi‑shoot chance
            bool multiShoot = Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.MultiShootChance));
            int maxTargets = multiShoot ? Mathf.Min(PlayerStatsManager.Instance.GetStatInt(SkillType.MultiShootCount), targets.Count) : 1;

            // Get projectile from pool for each distinct target (no duplicate targeting)
            for (int i = 0; i < maxTargets; i++)
            {
                Transform target = targets[i];
                _currentTarget = target;

                Projectile projectile = ProjectilePool.Instance.Get();
                if (projectile != null)
                {
                    projectile.transform.SetPositionAndRotation(transform.position, Quaternion.identity);

                    // Projectile pertama = damage penuh
                    float damageMultiplier = (i == 0) ? 1f : 0.75f;
                    projectile.Initialize(target, this, damageMultiplier);
                }
            }
        }

        /// <summary>
        /// Try to trigger ultimates using the new modular system.
        /// Delegates to UltimateManager.TrySpawn() which handles cooldown and chance.
        /// Only auto-casts when AutoCastUltimate setting is enabled.
        /// </summary>
        private void TryTriggerUltimateWithCooldown()
        {
            if (_ultimateManager == null) return;

            // TrySpawn handles cooldown, active checks, chance, and mana cost
            var pos = transform.position;
            _ultimateManager.TrySpawn(UltimateDMG.Void.ToString(), pos, this);
            _ultimateManager.TrySpawn(UltimateDMG.Root.ToString(), pos, this);
            _ultimateManager.TrySpawn(UltimateDMG.Fountain.ToString(), pos, this);
            _ultimateManager.TrySpawn(UltimateDMG.Shockwave.ToString(), pos, this);

            // Lightning is triggered by kill count in EnemyAi, not cooldown/chance
            // Do not call TrySpawn here - it's handled when enemies die
        }

        private void TryRegeneration()
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer < 1f) return;

            float maxHealth = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            float regen = PlayerStatsManager.Instance.GetStat(SkillType.HealthRegen);
            float maxMana = PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint);
            float manaRegen = PlayerStatsManager.Instance.GetStat(SkillType.ManaRegen);

            while (_regenTimer >= 1f)
            {
                _regenTimer -= 1f;
                if (_currentHealth < maxHealth)
                    Heal(Mathf.Min(regen, maxHealth - _currentHealth));
                if (_currentMana < maxMana)
                    GainMana(Mathf.Min(manaRegen, maxMana - _currentMana));
            }
        }

        /// <summary>Checks bump; le than cost, false. Mana system gate.</summary>
        public bool CanAfford(float manaCost) => _currentMana >= manaCost;

        /// <summary>Consumes mana for an ultimate or mana-cost skill. Returns true when spent.</summary>
        public bool SpendMana(float manaCost)
        {
            if (!CanAfford(manaCost)) return false;
            _currentMana -= manaCost;
            UpdateManaUI();
            return true;
        }

        // ===== Heal / Mana Over Time (from potions) =====
        private readonly List<Coroutine> _activeHoTs = new();
        private readonly List<Coroutine> _activeMoTs = new();
        private static readonly WaitForSeconds _oneSecond = new(1f);

        /// <summary>
        /// Starts a Heal-over-Time effect. Ticks every second for <paramref name="duration"/> seconds.
        /// Each tick heals <paramref name="totalAmount"/> / <paramref name="duration"/>.
        /// </summary>
        public void StartHealOverTime(float totalAmount, float duration = 10f)
        {
            if (totalAmount <= 0f || duration <= 0f) return;
            float tickAmount = totalAmount / duration;
            var routine = StartCoroutine(HealOverTimeRoutine(tickAmount, duration));
            _activeHoTs.Add(routine);
        }

        /// <summary>
        /// Starts a Mana-over-Time effect. Ticks every second for <paramref name="duration"/> seconds.
        /// Each tick restores <paramref name="totalAmount"/> / <paramref name="duration"/> mana.
        /// </summary>
        public void StartManaOverTime(float totalAmount, float duration = 10f)
        {
            if (totalAmount <= 0f || duration <= 0f) return;
            float tickAmount = totalAmount / duration;
            var routine = StartCoroutine(ManaOverTimeRoutine(tickAmount, duration));
            _activeMoTs.Add(routine);
        }

        private IEnumerator HealOverTimeRoutine(float tickAmount, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return _oneSecond;
                elapsed += 1f;

                float maxHealth = PlayerStatsManager.Instance?.GetStat(SkillType.HealthPoint) ?? 0f;
                if (_currentHealth < maxHealth)
                {
                    float before = _currentHealth;
                    _currentHealth = Mathf.Min(_currentHealth + tickAmount, maxHealth);
                    float actual = _currentHealth - before;
                    if (actual >= 1f)
                        UpdateHealthUI();
                }
                // Show heal tick popup every second regardless of actual heal (visual feedback)
                ShowDamagePopup(tickAmount, DamageType.Heal, CriticalType.None, "+");
            }
        }

        private IEnumerator ManaOverTimeRoutine(float tickAmount, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return _oneSecond;
                elapsed += 1f;

                float maxMana = PlayerStatsManager.Instance?.GetStat(SkillType.ManaPoint) ?? 0f;
                if (_currentMana < maxMana)
                {
                    float before = _currentMana;
                    _currentMana = Mathf.Min(_currentMana + tickAmount, maxMana);
                    float actual = _currentMana - before;
                    if (actual >= 1f)
                        UpdateManaUI();
                }
                // Show mana tick popup every second regardless of actual gain (visual feedback)
                ShowDamagePopup(tickAmount, DamageType.Mana, CriticalType.None, "+");
            }
        }

        /// <summary>
        /// Update shield system - regenerate shield when at full HP.
        /// </summary>
        private void UpdateShield()
        {
            float maxHealth = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);

            // Handle shield cooldown
            if (_isShieldOnCooldown)
            {
                _shieldCooldownTimer -= Time.deltaTime;
                UpdateShieldCooldownUI(); // Update radial fill

                if (_shieldCooldownTimer <= 0f)
                {
                    _isShieldOnCooldown = false;
                    UpdateShieldCooldownUI(); // Reset UI
                }
                return; // Don't grant shield while on cooldown
            }

            if (_currentHealth >= maxHealth)
            {
                if (!_shieldGranted)
                {
                    float shieldPercent = CardModifierService.GetEffectResult(CardEffectType.Shield, 0f);
                    _maxShield = maxHealth * shieldPercent;

                    _currentShield = _maxShield;
                    _shieldGranted = true;
                    UpdateShieldVisual();
                }
            }
            else
            {
                _shieldGranted = false;
            }
        }

        /// <summary>
        /// Update the radial cooldown UI fill amount.
        /// </summary>
        private void UpdateShieldCooldownUI()
        {
            if (_shieldCooldownImage == null) return;

            if (_isShieldOnCooldown)
            {
                // Fill amount: 0 = empty (cooldown done), 1 = full (cooldown just started)
                // We want it to fill up as cooldown progresses, so invert
                float fillAmount = 1f - (_shieldCooldownTimer / ShieldCooldownDuration);
                _shieldCooldownImage.fillAmount = fillAmount;
                _shieldCooldownImage.enabled = true;
            }
            else
            {
                _shieldCooldownImage.enabled = false;
                _shieldCooldownImage.fillAmount = 0f;
            }
        }

        /// <summary>
        /// Update shield visual effect.
        /// </summary>
        private void UpdateShieldVisual()
        {
            if (_shieldRenderer == null) return;
            bool hasShield = _currentShield > 0;
            _shieldRenderer.enabled = hasShield;
            if (!hasShield) return;

            float shieldPercent = _maxShield > 0 ? _currentShield / _maxShield : 0f;

            // Height (0 - 0.5)
            float yScale = Mathf.Lerp(0f, 0.5f, shieldPercent);
            _shieldRenderer.transform.localScale = new Vector3(1f, yScale, 1f);

            Color color = GameColors.red;

            if (shieldPercent > 0.75f)
            {
                // Yellow -> Green
                float t = Mathf.InverseLerp(0.75f, 1f, shieldPercent);
                color = Color.Lerp(GameColors.yellow, GameColors.green, t);
            }
            else if (shieldPercent > 0.3f)
            {
                // Red -> Yellow
                float t = Mathf.InverseLerp(0.3f, 0.75f, shieldPercent);
                color = Color.Lerp(GameColors.red, GameColors.yellow, t);
            }

            color.a = Mathf.Lerp(0.3f, 0.7f, shieldPercent);
            _shieldRenderer.color = color;
        }

        /// <summary>
        /// Manually casts an ultimate by ID.
        /// Manual casting bypasses chance checks but still respects
        /// cooldown and mana requirements through UltimateManager.
        /// Called by UltimatePanelController when the user presses
        /// an ultimate button.
        /// </summary>
        public bool ManualCastUltimate(string ultimateId)
        {
            if (_ultimateManager == null || string.IsNullOrEmpty(ultimateId)) 
                return false;
            // Tank has special spawn position (attack range boundary)
            // if (ultimateId == UltimateDMG.Tank.ToString())
            // {
            //     if (!TryGetTankSpawnPosition(out Vector3 spawnPos)) return false;
            //     return _ultimateManager.TrySpawnManual(ultimateId, spawnPos, this);
            // }
            // Stack-based ultimates (Bomb, Cloud, Lightning): cast all ready stacks
            return _ultimateManager.TryCastAllReadyStacks(ultimateId, this);
        }

        /// <summary>
        /// Attempts to generate one Tank Ultimate stack via chance roll.
        /// Tank's chance determines whether a new stack is generated.
        /// Auto Cast only determines whether the generated stack is
        /// immediately consumed and spawned.
        /// </summary>
        public void SpawnTank()
        {
            if (_ultimateManager == null) return;
            string ultimateId = UltimateDMG.Tank.ToString();

            if (!_ultimateManager.TryGetUltimate(ultimateId, out _))
                return;

            // Remove destroyed Tank references before checking positions.
            _activeTanks.RemoveAll(tank => tank == null);

            // Try to generate a stack via chance roll
            if (!TryGetTankSpawnPosition(out Vector3 spawnPos)) return;
            _ultimateManager.TryGenerateStack(ultimateId, this, spawnPos);

            // Auto Cast: if enabled and mana permits, TryGenerateStack already handles auto-cast
            // No additional logic needed here - UltimateManager handles it internally
        }

        /// <summary>
        /// Attempts to find a valid spawn position for a Tank.
        /// The player's AttackRange determines the distance of the Tank
        /// from the player. The Tank's own AttackRange is only used to
        /// determine the minimum spacing from existing Tanks.
        /// </summary>
        public bool TryGetTankSpawnPosition(out Vector3 spawnPos)
        {
            spawnPos = default;
            if (PlayerStatsManager.Instance == null) return false;
            float playerAR = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            if (playerAR <= 0f) return false;
            // Tank AttackRange is used only for spacing between Tanks.
            float tankAR = playerAR * 0.75f;
            if (!TryFindValidSpawnPosition(playerAR, tankAR, out Vector2 position))
                return false;
            spawnPos = position;
            return true;
        }

        /// <summary>
        /// Attempts to find a valid Tank spawn position.
        /// playerAR:
        ///     Determines where the Tank is spawned around the player.
        /// tankAR:
        ///     Determines the minimum spacing between the new Tank
        ///     and existing Tanks.
        /// </summary>
        private bool TryFindValidSpawnPosition(float playerAR, float tankAR, out Vector2 spawnPos)
        {
            const int MaxAttempts = 20;
            spawnPos = default;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Generate a random direction around the player.
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                // Spawn exactly on the player's AttackRange boundary.
                Vector2 candidatePosition = (Vector2)transform.position + randomDir * playerAR;
                if (!IsValidTankSpawnPosition(candidatePosition, tankAR)) continue;
                spawnPos = candidatePosition;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the specified position maintains sufficient
        /// spacing from all active Tanks.
        /// The minimum distance is the sum of the new Tank's AttackRange
        /// and the existing Tank's AttackRange.
        /// </summary>
        private bool IsValidTankSpawnPosition(Vector2 candidatePosition, float tankAR)
        {
            foreach (TankInstance existingTank in _activeTanks)
            {
                if (existingTank == null) continue;
                float distanceToExistingTank = Vector2.Distance(candidatePosition, (Vector2)existingTank.transform.position);
                float minimumDistance = tankAR + existingTank.TankAttackRange;
                // Attack ranges must not overlap.
                if (distanceToExistingTank < minimumDistance) return false;
            }
            return true;
        }

        /// <summary>
        /// Apply damage to player.
        /// Called by Enemy when it hits this player.
        /// </summary>
        public float TakeDamage(DamageData damageData, bool canEvade = true)
        {
            // Immunity & Evasion check – if player is currently immune, ignore damage.
            if (_isImmune || (canEvade && Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.Evasion))))
            {
                ShowDamagePopup(1f, DamageType.Miss, CriticalType.None);
                return 1f;
            }

            float rawDamage = damageData.Damage;

            // Apply shield first
            if (_currentShield > 0)
            {
                float shieldAbsorb = Mathf.Min(_currentShield, rawDamage);
                _currentShield -= shieldAbsorb;
                rawDamage -= shieldAbsorb;

                if (shieldAbsorb > 0)
                    ShowDamagePopup(shieldAbsorb, DamageType.Miss, CriticalType.None, "⛨ ");

                // Shield depleted - start cooldown
                if (_currentShield <= 0)
                {
                    _isShieldOnCooldown = true;
                    _shieldCooldownTimer = ShieldCooldownDuration;
                    _shieldGranted = false;
                }
            }

            float finalDamage = Utilityku.FinalDamage(rawDamage, PlayerStatsManager.Instance.GetStat(SkillType.DefenseAmount));
            _currentHealth -= finalDamage;
            _currentHealth = Mathf.Clamp(_currentHealth, 0, PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint));

            UpdateHealthUI();
            UpdateShieldVisual();

            // Check if dead
            if (_currentHealth <= 0) Die();

            if (finalDamage > 0)
                ShowDamagePopup(finalDamage, DamageType.Normal, CriticalType.None);

            return finalDamage;
        }

        public void GainMana(float gainmana)
        {
            float mana = PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint);
            if (_currentMana >= mana) return;

            float oldMana = _currentMana;
            _currentMana = Mathf.Min(_currentMana + gainmana, mana);

            // Calculate actual gainmana received (clamped by max mana)
            float actualGainMana = _currentMana - oldMana;

            if (!Mathf.Approximately(oldMana, _currentMana))
            {
                UpdateManaUI();
                // Show gainmana popup
                if (actualGainMana >= 1f) ShowDamagePopup(actualGainMana, DamageType.Mana, CriticalType.None, "+");
            }
        }

        private void UpdateManaUI()
        {
            if (manaBar == null) return;
            float maxMana = PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint);
            manaBar.maxValue = maxMana;
            manaBar.value = _currentMana;
            
            float percent = Mathf.Clamp01(_currentMana / maxMana);
            fillMana.color = Color.Lerp(GameColors.empty, GameColors.blue, percent);
            OnManaChanged?.Invoke();
        }

        public void Heal(float heal)
        {
            float health = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            if (_currentHealth >= health) return;

            float oldHealth = _currentHealth;
            _currentHealth = Mathf.Min(_currentHealth + heal, health);

            // Calculate actual heal received (clamped by max health)
            float actualHeal = _currentHealth - oldHealth;

            if (!Mathf.Approximately(oldHealth, _currentHealth))
            {
                UpdateHealthUI();

                // Show heal popup
                if (actualHeal >= 1f) ShowDamagePopup(actualHeal, DamageType.Heal, CriticalType.None, "+");
            }
        }

        private void UpdateHealthUI()
        {
            if (healthBar == null) return;
            float maxHealth = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            healthBar.maxValue = maxHealth;
            healthBar.value = _currentHealth;
            
            float percent = Mathf.Clamp01(_currentHealth / maxHealth);
            Color color;
            if (percent > 0.75f)
            {
                float t = Mathf.InverseLerp(0.75f, 1f, percent);
                color = Color.Lerp(GameColors.yellow, GameColors.green, t);
            }
            else
            {
                float t = Mathf.InverseLerp(0f, 0.75f, percent);
                color = Color.Lerp(GameColors.red, GameColors.yellow, t);
            }

            fillHealth.color = color;
            OnHealthChanged?.Invoke();
        }

        /// <summary>
        /// Show damage popup at player position.
        /// </summary>
        private void ShowDamagePopup(float damage, DamageType type, CriticalType criticalType, string prefix = "")
        {
            if (DamagePopupManager.Instance == null) return;

            // Position popup slightly above player center
            Vector3 popupPosition = transform.position + Vector3.up * 0.5f;

            DamagePopupData popupData = new(
                damage,
                type,
                criticalType, // Langsung kirim CriticalType, bukan bool
                prefix // Prefix with + for heal
            );

            DamagePopupManager.Instance.ShowDamage(popupPosition, popupData, transform);
        }

        private void Die()
        {
            if (Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.DeathDefy)))
            {
                // Heal a small amount and grant temporary immunity with visual barrier.
                float heal = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint) * 0.05f;
                Heal(heal);

                // Start immunity coroutine (5 seconds) and enable barrier visual.
                if (gameObject.activeInHierarchy) // ensure we can start coroutine
                {
                    StartCoroutine(ImmunityRoutine(5f));
                }
                return;
            }

            // Handle player death (e.g., trigger game over, respawn, etc.)
            WaveManager.Instance.Defeat();
        }

        // Coroutine that grants immunity and shows the barrier for the given duration.
        private IEnumerator ImmunityRoutine(float duration)
        {
            _isImmune = true;
            if (_barrierRenderer != null)
                _barrierRenderer.enabled = true;

            yield return new WaitForSeconds(duration);

            _isImmune = false;
            if (_barrierRenderer != null)
                _barrierRenderer.enabled = false;
        }

        private void DrawAttackRange()
        {
            float diameter = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) * 2f;
            _attackRangeRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
            _attackRangeRenderer.color = GameColors.debugAtkRangeCyan.WithAlpha(0.09f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw attack range as subtle cyan dashed circle in Scene view
            Gizmos.color = GameColors.debugCyanGizmo.WithAlpha(0.5f); // Cyan with transparency
            DrawCircleGizmo(transform.position, AttackRange, 64);
            
            if (Application.isPlaying)
            {
                float attackDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage);
                float attackSpeed= PlayerStatsManager.Instance.GetStat(SkillType.AttackSpeed);
                float attackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);

                Vector3 labelPos = transform.position + Vector3.up * 2f;
                UnityEditor.Handles.Label(labelPos, $"AttackDamage {attackDamage}, AttackSpeed {attackSpeed}, AttackRange {attackRange}, ");
            }
        }

        private static void DrawCircleGizmo(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments * Mathf.Deg2Rad;
            Vector3 prevPos = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep;
                Vector3 newPos = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0
                );
                Gizmos.DrawLine(prevPos, newPos);
                prevPos = newPos;
            }
            // Close the circle
            Gizmos.DrawLine(prevPos, center + new Vector3(radius, 0, 0));
        }

#endif
    }
}
