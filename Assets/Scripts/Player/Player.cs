using UnityEngine;
using System.Collections;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Ultimate;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using Newtonsoft.Json;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;

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

        [Header("Visualization")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image fillImage;
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
        private int _enemyLayerMask;
        private Transform _currentTarget;
        private UltimateManager _ultimateManager;

        public float CurrentHealth => _currentHealth;

        // Shield system
        [SerializeField] private float _currentShield = 0f;
        private float _maxShield = 0f;
        private bool _shieldGranted;
        private float _shieldCooldownTimer = 0f;
        private const float ShieldCooldownDuration = 30f;
        private bool _isShieldOnCooldown = false;

        [SerializeField] private AudioSource _sfxSource;
        public AudioSource SfxSource => _sfxSource;

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

            ReloadStats();
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _activeTanks = new List<TankInstance>();

            // Ensure barrier renderer starts disabled
            if (_barrierRenderer != null)
                _barrierRenderer.enabled = false;
        }

        /// <summary>
        /// Reload player stats based on current upgrade levels.
        /// Called by UpgradeManager after a skill is upgraded.
        /// </summary>
        public void ReloadStats()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null)
            {
                Debug.LogError("[Player] Cannot reload stats - dataPlayer.json not found!");
                return;
            }

            PlayerData baseData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
            var upgradeManager = ServiceLocator.UpgradeService;

            if (upgradeManager == null)
            {
                Debug.LogWarning("[Player] UpgradeManager not found, using base stats.");
                LoadPlayerData();
                return;
            }

            // Load stats with current upgrade levels using the grouped structure
            StatLoader.LoadWithUpgrades(baseData, (UpgradeManager)upgradeManager);

            // Update visuals
            DrawAttackRange();

            // Always start at full health on game start
            float health = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            _currentHealth = health;
            
            UpdateHealthUI();
            UpdateShieldVisual();
            AttackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
        }

        private void LoadPlayerData()
        {
            TextAsset playerJson = Resources.Load<TextAsset>("Data/dataPlayer");
            if (playerJson == null)
            {
                Debug.LogWarning("Player data file not found at Resources/Data/dataPlayer.json. Using default values.");
                return;
            }

            PlayerData pData = JsonConvert.DeserializeObject<PlayerData>(playerJson.text);
            StatLoader.LoadFromPlayerData(pData);
        }

        private void Start()
        {
            // Wait for SaveManager to load before applying saved upgrade levels
            if (SaveManager.Instance != null && SaveManager.Instance.IsSaveLoaded)
            {
                // Save already loaded (e.g., scene reload)
                ReloadStats();
            }
            else if (SaveManager.Instance != null)
            {
                // Subscribe to load event
                SaveManager.OnSaveLoaded += OnSaveLoaded_ReloadStats;
            }
            else
            {
                // Fallback - no SaveManager
                ReloadStats();
            }

            UpdateHealthUI();
            UpdateShieldVisual();
            UpdateShieldCooldownUI(); // Initialize cooldown UI

            DrawAttackRange();
            _ultimateManager = UltimateManager.Instance;
        }

        private void OnSaveLoaded_ReloadStats()
        {
            SaveManager.OnSaveLoaded -= OnSaveLoaded_ReloadStats;
            ReloadStats();
        }

        private void OnDisable()
        {
            // Clean up event subscription
            SaveManager.OnSaveLoaded -= OnSaveLoaded_ReloadStats;
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
        /// Try to trigger a shockwave using the new modular system.
        /// Delegates to UltimateManager.TrySpawn() which handles cooldown and chance.
        /// </summary>
        private void TryTriggerUltimateWithCooldown()
        {
            if (_ultimateManager == null) return;

            // TrySpawn handles cooldown and active checks
            _ultimateManager.TrySpawn(UltimateDMG.Void.ToString(), transform.position, this);
            _ultimateManager.TrySpawn(UltimateDMG.Root.ToString(), transform.position, this);
            _ultimateManager.TrySpawn(UltimateDMG.Fountain.ToString(), transform.position, this);
            _ultimateManager.TrySpawn(UltimateDMG.Shockwave.ToString(), transform.position, this);

            // Lightning is triggered by kill count in EnemyAi, not cooldown/chance
            // Do not call TrySpawn here - it's handled when enemies die
        }

        private void TryRegeneration()
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer < 1f) return;

            float maxHealth = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            float regen = PlayerStatsManager.Instance.GetStat(SkillType.HealthRegen);

            while (_regenTimer >= 1f)
            {
                _regenTimer -= 1f;
                if (_currentHealth >= maxHealth) break;
                Heal(Mathf.Min(regen, maxHealth - _currentHealth));
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

            if (hasShield)
            {
                float shieldPercent = _maxShield > 0 ? _currentShield / _maxShield : 0f;

                // Height (0 - 0.5)
                float yScale = Mathf.Lerp(0f, 0.5f, shieldPercent);
                _shieldRenderer.transform.localScale = new Vector3(1f, yScale, 1f);

                Color color;

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
                else
                {
                    color = GameColors.red;
                }

                color.a = Mathf.Lerp(0.3f, 0.7f, shieldPercent);
                _shieldRenderer.color = color;
            }
        }

        /// <summary>
        /// Spawn a single tank at a valid position using the new modular system.
        /// Tank spawning logic is delegated to TankHandler via UltimateManager.
        /// </summary>
        public void SpawnTank()
        {
            if (_ultimateManager == null) return;
            if (!_ultimateManager.TryGetUltimate(UltimateDMG.Tank.ToString(), out var tankData)) return;

            // Clean up destroyed tanks from the list
            _activeTanks.RemoveAll(t => t == null);

            // Calculate tank attack range for position validation
            float tankAttackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) * 0.75f;

            // Find a valid spawn position at the attack range boundary
            Vector2 spawnPos = FindValidSpawnPosition(tankAttackRange);
            if (spawnPos == Vector2.zero) return;

            // Delegate to UltimateManager which uses TankHandler to spawn
            // TankHandler handles: active count, chance, instantiation, initialization
            _ultimateManager.TrySpawn(UltimateDMG.Tank.ToString(), (Vector3)spawnPos, this);
        }

        /// <summary>
        /// Find a valid spawn position on the attack range boundary.
        /// Position must maintain minimum distance from other tanks (sum of attack ranges).
        /// </summary>
        private Vector2 FindValidSpawnPosition(float tankAttackRange)
        {
            const int maxAttempts = 10;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Random direction from player
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;

                // Random position on the attack range boundary
                Vector2 candidatePos = (Vector2)transform.position + randomDir * PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);

                // Check if position maintains minimum distance from all other active tanks
                bool isValid = true;
                foreach (TankInstance existingTank in _activeTanks)
                {
                    if (existingTank == null) continue;

                    float distanceToExistingTank = Vector2.Distance(candidatePos, (Vector2)existingTank.transform.position);

                    // Minimum distance = sum of both tank attack ranges
                    // This ensures attack range circles don't overlap
                    float minDistance = tankAttackRange + existingTank.TankAttackRange;

                    if (distanceToExistingTank < minDistance)
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid) return candidatePos;
            }

            // No valid position found after max attempts
            return Vector2.zero;
        }

        /// <summary>
        /// Apply damage to player.
        /// Called by Enemy when it hits this player.
        /// </summary>
        public float TakeDamage(DamageData damageData, bool canEvade = true)
        {
            // Immunity & Evasion check – if player is currently immune, ignore damage.
            if (_isImmune || (canEvade && Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.EvasionChance))))
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

            fillImage.color = color;
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
            _attackRangeRenderer.color = new Color(0.10f, 0.95f, 1.00f, 0.09f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Update the visualization when attack range changes in the Inspector
            if (Application.isPlaying) DrawAttackRange();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw attack range as subtle cyan dashed circle in Scene view
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan with transparency
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
