using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Unified manager for player status effects (Slow, Stun, Burn).
    /// Uses source-based tracking so multiple enemies can apply effects independently.
    /// Stacking: Slow = strongest wins; Stun = any active stun disables movement; Burn = source-based, non-stacking, refreshes duration.
    /// Optimized for >5000 enemies: zero per-frame allocations, dirty-flag recalculation, cached refs, no redundant modifier rebuilds.
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

        // Active effects tracked by source ID (Slow, Stun, Burn) - unified struct
        private readonly Dictionary<EnemyAuraManager.StatusSourceId, EffectData> _activeEffects = new();
        private readonly Dictionary<EnemyAuraManager.StatusSourceId, EffectData> _activeBurnEffects = new();

        // Current strongest slow percent (0-1)
        private float _currentSlowPercent = 0f;

        // Any stun active flag
        private bool _isStunned = false;

        // Joystick enable cache for stun resume
        private bool _wasJoystickEnabled = true;

        // Cached player reference
        private Player _player;
        private Joystick _joystick;
        private PlayerStatsManager _playerStatsManager;

        // Burn tick timer
        private float _burnTickTimer = 0f;
        private const float BURN_TICK_INTERVAL = 1f;

        // Aura refresh interval - 10 checks/second instead of 60
        private const float AURA_REFRESH_INTERVAL = 0.1f;

        // Reuse removal lists to avoid per-frame allocations
        private readonly List<EnemyAuraManager.StatusSourceId> _effectsToRemove = new();
        private readonly List<EnemyAuraManager.StatusSourceId> _burnsToRemove = new();
        // Unregeneration aura sources (Necromancer) - binary effect (any source = blocked)
        private readonly HashSet<EnemyAuraManager.StatusSourceId> _unregenerationSources = new();

        // Dirty flags – only rebuild modifiers when needed
        private bool _slowDirty;
        private bool _stunDirty;

        // Track last applied slow multiplier to avoid redundant ModifierManager rebuilds
        private float _lastAppliedSlowMultiplier = 1f;
        private bool _hasAppliedSlow;

        // Stun counter for O(1) any-stun check
        private int _activeStunCount = 0;

        // Movement speed multiplier from slow (1 = normal, 0.5 = 50% slow)
        public float CurrentSlowMultiplier => 1f - _currentSlowPercent;

        // Whether player is currently slowed
        public bool IsSlowed => _currentSlowPercent > 0f;

        // Whether player is currently stunned
        public bool IsStunned => _isStunned;

        // Whether player regeneration is blocked by Necromancer aura
        public bool IsUnregenerationActive => _unregenerationSources.Count > 0;

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
            CachePlayerReferences();
        }

        private void CachePlayerReferences()
        {
            if (_player != null) return;
            _player = Player.Instance;
            if (_player == null) return;
            _joystick = _player.GetComponentInChildren<Joystick>();
            _playerStatsManager = PlayerStatsManager.Instance;
        }

        private void Update()
        {
            float currentTime = Time.time;

            // Process Slow/Stun expirations
            _effectsToRemove.Clear();
            foreach (var kvp in _activeEffects)
            {
                if (kvp.Value.ExpireTime <= currentTime)
                {
                    _effectsToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in _effectsToRemove)
            {
                if (_activeEffects.TryGetValue(key, out var expiredEffect))
                {
                    if (expiredEffect.Type == PlayerEffectType.Slow)
                    {
                        _slowDirty = true;
                    }
                    else if (expiredEffect.Type == PlayerEffectType.Stun)
                    {
                        _activeStunCount--;
                        if (_activeStunCount < 0) _activeStunCount = 0;
                        _stunDirty = true;
                    }
                }
                _activeEffects.Remove(key);
            }

            // Process Burn expirations
            _burnsToRemove.Clear();
            foreach (var kvp in _activeBurnEffects)
            {
                if (kvp.Value.ExpireTime <= currentTime)
                {
                    _burnsToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in _burnsToRemove)
            {
                _activeBurnEffects.Remove(key);
            }

            // Burn tick for damage application
            _burnTickTimer += Time.deltaTime;
            if (_burnTickTimer >= BURN_TICK_INTERVAL)
            {
                _burnTickTimer = 0f;
                ApplyBurnDamage();
            }

            // Recalculate only if dirty
            if (_slowDirty || _stunDirty)
            {
                RecalculateAllEffects();
                ApplyToPlayer();
                _slowDirty = _stunDirty = false;
            }
        }

        /// <summary>
        /// Applies or refreshes a status effect from a specific source.
        /// </summary>
        public void ApplyEffect(EnemyAuraManager.StatusSourceId sourceId, PlayerEffectType effectType, float percent, float duration)
        {
            if (duration <= 0f) return;
            if (effectType == PlayerEffectType.Slow && percent <= 0f) return;
            if (effectType == PlayerEffectType.Burn && percent <= 0f) return;

            percent = Mathf.Clamp01(percent);
            float expireTime = Time.time + Mathf.Max(0.1f, duration);
            bool stateChanged = false;

            if (effectType == PlayerEffectType.Burn)
            {
                // Burn: source-based, stackable (max 5), refreshes duration per hit
                if (_activeBurnEffects.TryGetValue(sourceId, out var existingBurn))
                {
                    bool updated = false;
                    if (expireTime > existingBurn.ExpireTime)
                    {
                        existingBurn.ExpireTime = expireTime;
                        updated = true;
                    }
                    // Increment stack (max 5)
                    if (existingBurn.StackCount < 5)
                    {
                        existingBurn.StackCount++;
                        existingBurn.Percent = existingBurn.BasePercent * existingBurn.StackCount;
                        updated = true;
                    }
                    if (updated)
                    {
                        existingBurn.MaxHealthAtApplication = _player?.MaxHealth ?? existingBurn.MaxHealthAtApplication;
                        _activeBurnEffects[sourceId] = existingBurn;
                        stateChanged = true;
                    }
                }
                else
                {
                    _activeBurnEffects[sourceId] = new EffectData
                    {
                        SourceId = sourceId,
                        Type = PlayerEffectType.Burn,
                        BasePercent = percent,
                        Percent = percent,
                        StackCount = 1,
                        Duration = duration,
                        ExpireTime = expireTime,
                        MaxHealthAtApplication = _player?.MaxHealth ?? 0f
                    };
                    stateChanged = true;
                }
            }
            else
            {
                // Slow / Stun
                if (_activeEffects.TryGetValue(sourceId, out var existing))
                {
                    bool changed = false;
                    if (expireTime > existing.ExpireTime)
                    {
                        existing.ExpireTime = expireTime;
                        existing.Duration = duration; // ✅ Sinkronkan durasi
                        changed = true;
                    }
                    if (effectType == PlayerEffectType.Slow && !Mathf.Approximately(existing.Percent, percent))
                    {
                        existing.Percent = percent;
                        changed = true;
                    }
                    // Handle type change
                    if (existing.Type != effectType)
                    {
                        // Type change: mark BOTH dirty — old effect's stat must be removed, new one applied
                        _slowDirty = true;
                        _stunDirty = true;
                        // Type change: handle stun counter
                        if (existing.Type == PlayerEffectType.Stun) _activeStunCount--;
                        if (effectType == PlayerEffectType.Stun) _activeStunCount++;
                        existing.Type = effectType;
                        changed = true;
                    }
                    if (changed)
                    {
                        _activeEffects[sourceId] = existing;
                        stateChanged = true;
                    }
                }
                else
                {
                    _activeEffects[sourceId] = new EffectData
                    {
                        SourceId = sourceId,
                        Type = effectType,
                        Percent = percent,
                        Duration = duration, // ✅ Wajib
                        ExpireTime = expireTime,
                        MaxHealthAtApplication = 0f
                    };
                    stateChanged = true;

                    if (effectType == PlayerEffectType.Stun) _activeStunCount++;
                }
            }

            // Mark dirty only for relevant effect types
            if (stateChanged)
            {
                if (effectType == PlayerEffectType.Slow)
                    _slowDirty = true;
                else if (effectType == PlayerEffectType.Stun)
                    _stunDirty = true;
            }
        }

        /// <summary>
        /// Removes a status effect by source ID.
        /// </summary>
        public void RemoveEffect(EnemyAuraManager.StatusSourceId sourceId)
        {
            bool slowRemoved = false;
            bool stunRemoved = false;

            if (_activeEffects.TryGetValue(sourceId, out var effect))
            {
                if (effect.Type == PlayerEffectType.Slow)
                    slowRemoved = true;
                else if (effect.Type == PlayerEffectType.Stun)
                {
                    stunRemoved = true;
                    _activeStunCount--;
                    if (_activeStunCount < 0) _activeStunCount = 0;
                }
            }

            if (_activeEffects.Remove(sourceId) || _activeBurnEffects.Remove(sourceId))
            {
                if (slowRemoved) _slowDirty = true;
                if (stunRemoved) _stunDirty = true;
            }
        }

        /// <summary>
        /// Applies an aura effect (persistent while in range).
        /// </summary>
        public void ApplyAuraEffect(EnemyAuraManager.StatusSourceId sourceId, PlayerEffectType effectType, float percent, float radius, Vector2 enemyPosition)
        {
            // Unregeneration is binary - track source separately (no percent/duration needed)
            if (effectType == PlayerEffectType.Unregeneration)
            {
                _unregenerationSources.Add(sourceId);
                return;
            }

            if (percent <= 0f) return;

            // Aura: duration = 5x refresh interval (0.5s) so effect persists between 10Hz refreshes
            // Caller should invoke at ~AURA_REFRESH_INTERVAL (0.1s) intervals, not every frame
            float auraDuration = AURA_REFRESH_INTERVAL * 5f;
            ApplyEffect(sourceId, effectType, percent, auraDuration);
        }

        /// <summary>
        /// Removes an aura effect when player leaves range.
        /// </summary>
        public void RemoveAuraEffect(EnemyAuraManager.StatusSourceId sourceId)
        {
            // Remove from Unregeneration sources if present
            _unregenerationSources.Remove(sourceId);
            // Also try regular removal (for Slow/Stun/Burn)
            RemoveEffect(sourceId);
        }

        /// <summary>
        /// Applies burn damage to player from all active burn sources.
        /// </summary>
        private void ApplyBurnDamage()
        {
            if (_activeBurnEffects.Count == 0) return;
            CachePlayerReferences();
            if (_player == null) return;

            float totalBurnDamage = 0f;

            foreach (var burn in _activeBurnEffects.Values)
            {
                float damagePerSecond = burn.MaxHealthAtApplication * burn.Percent / Mathf.Max(0.1f, burn.Duration);
                float tickDamage = damagePerSecond * BURN_TICK_INTERVAL;
                totalBurnDamage += tickDamage;
            }

            if (totalBurnDamage > 0f)
            {
                var damageData = new DamageData(totalBurnDamage, DamageType.Burn, CriticalType.None, PlayerEffectType.Burn.ToString());
                _player.TakeDamage(damageData);
            }
        }

        /// <summary>
        /// Recalculates the strongest slow and stun state from all active effects.
        /// Stacking: Slow = strongest wins; Stun = any active (via counter).
        /// </summary>
        private void RecalculateAllEffects()
        {
            if (_slowDirty)
            {
                float maxSlow = 0f;
                foreach (var effect in _activeEffects.Values)
                {
                    if (effect.Type == PlayerEffectType.Slow && effect.Percent > maxSlow)
                        maxSlow = effect.Percent;
                }
                _currentSlowPercent = maxSlow;
            }

            if (_stunDirty)
            {
                _isStunned = _activeStunCount > 0;
            }
        }

        /// <summary>
        /// Applies the current effects to the player.
        /// Only rebuilds modifiers when slow multiplier actually changes.
        /// </summary>
        private void ApplyToPlayer()
        {
            CachePlayerReferences();
            if (_player == null) return;

            // --- Apply Slow via ModifierManager ---
            if (IsSlowed)
            {
                float multiplierPercent = CurrentSlowMultiplier * 100f;

                // Only rebuild if multiplier changed
                if (!_hasAppliedSlow || !Mathf.Approximately(multiplierPercent, _lastAppliedSlowMultiplier * 100f))
                {
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
                    _playerStatsManager?.ApplyTemporaryModifier(
                        multiplierPercent, "EnemySlow", duration,
                        SkillType.MoveSpeed, SkillType.AttackSpeed);
                    ModifierManager.Instance?.RebuildAllDirty();

                    _lastAppliedSlowMultiplier = CurrentSlowMultiplier;
                    _hasAppliedSlow = true;
                }
            }
            else if (_hasAppliedSlow)
            {
                // Remove slow modifier by applying 100% (no slow) with minimal duration
                _playerStatsManager?.ApplyTemporaryModifier(
                    100f, "EnemySlow", 0.1f,
                    SkillType.MoveSpeed, SkillType.AttackSpeed);
                ModifierManager.Instance?.RebuildAllDirty();

                _hasAppliedSlow = false;
                _lastAppliedSlowMultiplier = 1f;
            }

            // --- Apply Stun via Joystick ---
            if (_joystick != null)
            {
                if (_isStunned)
                {
                    if (_joystick.enabled)
                    {
                        _wasJoystickEnabled = true;
                        _joystick.enabled = false;
                    }
                }
                else
                {
                    _joystick.enabled = _wasJoystickEnabled;
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
                total += burn.MaxHealthAtApplication * burn.Percent / Mathf.Max(0.1f, burn.Duration);
            }
            return total;
        }

        /// <summary>
        /// Gets ice (slow) effect cooldown fill amount (1 = just applied, 0 = expired).
        /// </summary>
        public float GetIceCooldownFill()
        {
            float maxExpireTime = 0f;
            float maxDuration = 0f;
            float currentTime = Time.time;

            foreach (var effect in _activeEffects.Values)
            {
                if (effect.Type != PlayerEffectType.Slow) continue;
                if (effect.ExpireTime > maxExpireTime)
                {
                    maxExpireTime = effect.ExpireTime;
                    maxDuration = effect.Duration;
                }
            }

            if (maxDuration <= 0f) return 0f;

            float remaining = maxExpireTime - currentTime;
            return Mathf.Clamp01(remaining / maxDuration);
        }

        /// <summary>
        /// Gets burn effect cooldown fill amount (1 = just applied, 0 = expired).
        /// </summary>
        public float GetBurnCooldownFill()
        {
            if (_activeBurnEffects.Count == 0) return 0f;

            float maxExpireTime = 0f;
            float maxDuration = 0f;
            float currentTime = Time.time;

            foreach (var burn in _activeBurnEffects.Values)
            {
                if (burn.ExpireTime > maxExpireTime)
                {
                    maxExpireTime = burn.ExpireTime;
                    maxDuration = burn.Duration;
                }
            }

            if (maxDuration <= 0f) return 0f;

            float elapsed = maxDuration - (maxExpireTime - currentTime);
            return Mathf.Clamp01(1f - (elapsed / maxDuration));
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
                log += $"  - {kvp.Key}: Burn {e.Percent * 100:F1}% maxHP over {e.Duration}s, expires in {e.ExpireTime - Time.time:F1}s\n";
            }
            Debug.Log(log);
        }

        public enum PlayerEffectType
        {
            Slow,
            Stun,
            Burn,
            Unregeneration
        }

        /// <summary>
        /// Unified status effect data structure.
        /// Slow: Percent (0-1), Duration unused.
        /// Stun: Percent unused (always 100% stun).
        /// Burn: Percent (total % = BasePercent × StackCount), StackCount (max 5), Duration, MaxHealthAtApplication (snapshot when applied).
        /// </summary>
        private struct EffectData
        {
            public EnemyAuraManager.StatusSourceId SourceId;
            public PlayerEffectType Type;
            public float Percent;
            public float BasePercent;
            public int StackCount;
            public float Duration;
            public float ExpireTime;
            public float MaxHealthAtApplication;
        }
    }
}