using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Unified manager for player status effects (Slow, Stun, Burn).
    /// Uses source-based tracking so multiple enemies can apply effects independently.
    /// Stacking: Slow = strongest wins; Stun = any active stun disables movement; Burn = source-based, non-stacking, refreshes duration.
    /// </summary>
    public sealed class PlayerStatusEffectManager : MonoBehaviour
    {
        private static PlayerStatusEffectManager _instance;
        public static PlayerStatusEffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerStatusEffectManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PlayerStatusEffectManager");
                        _instance = go.AddComponent<PlayerStatusEffectManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // Active effects tracked by source ID (Slow, Stun)
        private readonly Dictionary<string, PlayerEffect> _activeEffects = new();

        // Burn effects tracked by source ID (source-based, non-stacking, duration refresh)
        private readonly Dictionary<string, BurnEffect> _activeBurnEffects = new();

        // Current strongest slow percent (0-1)
        private float _currentSlowPercent = 0f;

        // Whether any stun is active
        private bool _isStunned = false;

        // Joystick state preservation for stun
        private bool _wasJoystickEnabled = true;

        // Cached player reference
        private Player _player;

        // Burn tick timer
        private float _burnTickTimer = 0f;
        private const float BURN_TICK_INTERVAL = 1f; // Apply burn damage once per second

        // Movement speed multiplier from slow (1 = normal, 0.5 = 50% slow)
        public float CurrentSlowMultiplier => 1f - _currentSlowPercent;

        // Whether player is currently slowed
        public bool IsSlowed => _currentSlowPercent > 0f;

        // Whether player is currently stunned
        public bool IsStunned => _isStunned;

        // Number of active effect sources
        public int ActiveEffectCount => _activeEffects.Count + _activeBurnEffects.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            float currentTime = Time.time;
            bool changed = false;

            var keysToRemove = new List<string>();
            foreach (var kvp in _activeEffects)
            {
                if (kvp.Value.ExpireTime <= currentTime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _activeEffects.Remove(key);
                changed = true;
            }

            // Handle Burn effects expiration
            var burnKeysToRemove = new List<string>();
            foreach (var kvp in _activeBurnEffects)
            {
                if (kvp.Value.ExpireTime <= currentTime)
                {
                    burnKeysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in burnKeysToRemove)
            {
                _activeBurnEffects.Remove(key);
                changed = true;
            }

            // Burn tick for damage application
            _burnTickTimer += Time.deltaTime;
            if (_burnTickTimer >= BURN_TICK_INTERVAL)
            {
                _burnTickTimer = 0f;
                ApplyBurnDamage();
            }

            if (!changed) return;

            RecalculateThenApply();
        }

        /// <summary>
        /// Applies or refreshes a status effect from a specific source.
        /// </summary>
        /// <param name="sourceId">Unique identifier for the source (e.g., "EnemyEffect_FrostGuardian_12345_Slow")</param>
        /// <param name="effectType">Type of effect to apply</param>
        /// <param name="percent">Effect strength as 0-1 for Slow/Burn; ignored for Stun</param>
        /// <param name="duration">Duration in seconds</param>
        public void ApplyEffect(string sourceId, PlayerEffectType effectType, float percent, float duration)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (duration <= 0f) return;
            if (effectType == PlayerEffectType.Slow && percent <= 0f) return;
            if (effectType == PlayerEffectType.Burn && percent <= 0f) return;

            percent = Mathf.Clamp01(percent);
            float expireTime = Time.time + Mathf.Max(0.1f, duration);

            if (effectType == PlayerEffectType.Burn)
            {
                // Burn: source-based, non-stacking, refreshes duration
                if (_activeBurnEffects.TryGetValue(sourceId, out var existingBurn))
                {
                    // Refresh duration if new duration is longer
                    if (expireTime > existingBurn.ExpireTime)
                    {
                        existingBurn.ExpireTime = expireTime;
                        existingBurn.PercentMaxHealth = percent; // Update percent if stronger
                        existingBurn.MaxHealthAtApplication = Player.Instance?.MaxHealth ?? 0f; // Refresh max health reference
                    }
                }
                else
                {
                    _activeBurnEffects[sourceId] = new BurnEffect
                    {
                        SourceId = sourceId,
                        PercentMaxHealth = percent,
                        Duration = duration,
                        ExpireTime = expireTime,
                        MaxHealthAtApplication = Player.Instance?.MaxHealth ?? 0f
                    };
                }
            }
            else
            {
                // Slow/Stun: use existing logic
                if (_activeEffects.TryGetValue(sourceId, out var existing))
                {
                    // Refresh duration if new duration is longer
                    if (expireTime > existing.ExpireTime)
                    {
                        existing.ExpireTime = expireTime;
                        existing.Percent = percent;
                    }
                    // Update effect type if it changed (shouldn't normally happen)
                    existing.Type = effectType;
                }
                else
                {
                    _activeEffects[sourceId] = new PlayerEffect
                    {
                        SourceId = sourceId,
                        Type = effectType,
                        Percent = percent,
                        ExpireTime = expireTime
                    };
                }
            }

            RecalculateThenApply();
        }

        /// <summary>
        /// Removes a status effect by source ID.
        /// </summary>
        public void RemoveEffect(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;

            bool removed = false;
            if (_activeEffects.Remove(sourceId)) removed = true;
            if (_activeBurnEffects.Remove(sourceId)) removed = true;
            if (!removed) return;
        
            RecalculateThenApply();
        }

        /// <summary>
        /// Applies an aura effect (persistent while in range).
        /// Uses a source ID that includes the enemy instance ID.
        /// </summary>
        public void ApplyAuraEffect(string sourceId, PlayerEffectType effectType, float percent, float radius, Vector2 enemyPosition)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (percent <= 0f) return;

            // For aura, we use a very short duration and refresh it every frame while in range
            ApplyEffect(sourceId, effectType, percent, 1f);
        }

        /// <summary>
        /// Removes an aura effect when player leaves range.
        /// </summary>
        public void RemoveAuraEffect(string sourceId) => RemoveEffect(sourceId);

        /// <summary>
        /// Applies burn damage to player from all active burn sources.
        /// </summary>
        private void ApplyBurnDamage()
        {
            if (_activeBurnEffects.Count == 0) return;
            if (_player == null) _player = Player.Instance;
            if (_player == null) return;

            float totalBurnDamage = 0f;

            foreach (var burn in _activeBurnEffects.Values)
            {
                // Burn damage = maxHealthAtApplication * percentMaxHealth / duration * tickInterval
                // This spreads the total damage (maxHealth * percent) over the duration
                float damagePerSecond = burn.MaxHealthAtApplication * burn.PercentMaxHealth / Mathf.Max(0.1f, burn.Duration);
                float tickDamage = damagePerSecond * BURN_TICK_INTERVAL;
                totalBurnDamage += tickDamage;
            }

            if (totalBurnDamage > 0f)
            {
                var damageData = new DamageData(totalBurnDamage, DamageType.Burn, CriticalType.None, PlayerEffectType.Burn.ToString());
                _player.TakeDamage(damageData);
            }
        }

        private void RecalculateThenApply()
        {
            RecalculateAllEffects();
            ApplyToPlayer();
        }

        /// <summary>
        /// Recalculates the strongest slow and stun state from all active effects.
        /// Stacking: Slow = strongest wins; Stun = any active.
        /// </summary>
        private void RecalculateAllEffects()
        {
            float maxSlow = 0f;
            bool anyStun = false;

            foreach (var effect in _activeEffects.Values)
            {
                if (effect.Type == PlayerEffectType.Slow)
                {
                    if (effect.Percent > maxSlow)
                        maxSlow = effect.Percent;
                }
                else if (effect.Type == PlayerEffectType.Stun)
                {
                    anyStun = true;
                }
            }

            _currentSlowPercent = maxSlow;
            _isStunned = anyStun;
        }

        /// <summary>
        /// Applies the current effects to the player.
        /// </summary>
        private void ApplyToPlayer()
        {
            if (_player == null) _player = Player.Instance;
            if (_player == null) return;

            // --- Apply Slow via ModifierManager ---
            if (IsSlowed)
            {
                // Convert multiplier (0.9 = 10% slow) to percent value (90 = 90% speed = -10% modifier)
                float multiplierPercent = CurrentSlowMultiplier * 100f;

                // Find the longest remaining duration among active slows
                float maxRemainingDuration = 0f;
                float currentTime = Time.time;
                foreach (var effect in _activeEffects.Values)
                {
                    if (effect.Type != PlayerEffectType.Slow) continue;
                    float remaining = effect.ExpireTime - currentTime;
                    if (remaining > maxRemainingDuration)
                        maxRemainingDuration = remaining;
                }

                float duration = Mathf.Max(0.1f, maxRemainingDuration);
                PlayerStatsManager.Instance?.ApplyTemporaryModifier(
                    multiplierPercent, "EnemySlow", duration,
                    SkillType.MoveSpeed, SkillType.AttackSpeed);
                ModifierManager.Instance?.RebuildAllDirty();
            }
            else
            {
                // Remove slow modifier by applying 100% (no slow) with minimal duration
                PlayerStatsManager.Instance?.ApplyTemporaryModifier(
                    100f, "EnemySlow", 0.1f,
                    SkillType.MoveSpeed, SkillType.AttackSpeed);
                ModifierManager.Instance?.RebuildAllDirty();
            }

            // --- Apply Stun via Joystick ---
            var joystick = _player.GetComponentInChildren<Joystick>();
            if (joystick != null)
            {
                if (_isStunned)
                {
                    if (joystick.enabled)
                    {
                        _wasJoystickEnabled = true;
                        joystick.enabled = false;
                    }
                }
                else
                {
                    joystick.enabled = _wasJoystickEnabled;
                }
            }

            // Visual sync for ice effect
            _player.SetIceEffect(IsSlowed);
        }

        /// <summary>
        /// Gets the current slow percent (0-1).
        /// </summary>
        public float GetCurrentSlowPercent() => _currentSlowPercent;

        /// <summary>
        /// Gets the total burn damage per second.
        /// </summary>
        public float GetTotalBurnDamagePerSecond()
        {
            float total = 0f;
            foreach (var burn in _activeBurnEffects.Values)
            {
                total += burn.MaxHealthAtApplication * burn.PercentMaxHealth / Mathf.Max(0.1f, burn.Duration);
            }
            return total;
        }

        /// <summary>
        /// For debugging - logs all active effects.
        /// </summary>
        public void LogActiveEffects()
        {
            if (_activeEffects.Count == 0 && _activeBurnEffects.Count == 0)
            {
                Debug.Log("[PlayerStatusEffectManager] No active effects");
                return;
            }

            string log = $"[PlayerStatusEffectManager] Active Effects ({_activeEffects.Count + _activeBurnEffects.Count}):\n";
            foreach (var kvp in _activeEffects)
            {
                var e = kvp.Value;
                log += $"  - {kvp.Key}: {e.Type} {e.Percent * 100:F1}%, expires in {e.ExpireTime - Time.time:F1}s\n";
            }
            foreach (var kvp in _activeBurnEffects)
            {
                var e = kvp.Value;
                log += $"  - {kvp.Key}: Burn {e.PercentMaxHealth * 100:F1}% maxHP over {e.Duration}s, expires in {e.ExpireTime - Time.time:F1}s\n";
            }
            Debug.Log(log);
        }

        public enum PlayerEffectType
        {
            Slow,
            Stun,
            Burn
        }

        private struct PlayerEffect
        {
            public string SourceId;
            public PlayerEffectType Type;
            public float Percent;      // 0-1 for Slow, ignored for Stun
            public float ExpireTime;   // Time.time when effect expires
        }

        private struct BurnEffect
        {
            public string SourceId;
            public float PercentMaxHealth;      // 0-1, e.g., 0.1 = 10% of max health over duration
            public float Duration;               // Total duration in seconds
            public float ExpireTime;             // Time.time when effect expires
            public float MaxHealthAtApplication; // Max health at time of application (for consistent damage)
        }
    }
}