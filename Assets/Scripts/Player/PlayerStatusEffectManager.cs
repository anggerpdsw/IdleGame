using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Unified manager for player status effects (Slow, Stun).
    /// Uses source-based tracking so multiple enemies can apply effects independently.
    /// Stacking: Slow = strongest wins; Stun = any active stun disables movement.
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

        // Active effects tracked by source ID
        private readonly Dictionary<string, PlayerEffect> _activeEffects = new();

        // Current strongest slow percent (0-1)
        private float _currentSlowPercent = 0f;

        // Whether any stun is active
        private bool _isStunned = false;

        // Joystick state preservation for stun
        private bool _wasJoystickEnabled = true;

        // Cached player reference
        private Player _player;

        // Movement speed multiplier from slow (1 = normal, 0.5 = 50% slow)
        public float CurrentSlowMultiplier => 1f - _currentSlowPercent;

        // Whether player is currently slowed
        public bool IsSlowed => _currentSlowPercent > 0f;

        // Whether player is currently stunned
        public bool IsStunned => _isStunned;

        // Number of active effect sources
        public int ActiveEffectCount => _activeEffects.Count;

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

            if (changed)
            {
                RecalculateAllEffects();
                ApplyToPlayer();
            }
        }

        /// <summary>
        /// Applies or refreshes a status effect from a specific source.
        /// </summary>
        /// <param name="sourceId">Unique identifier for the source (e.g., "EnemyEffect_FrostGuardian_12345_Slow")</param>
        /// <param name="effectType">Type of effect to apply</param>
        /// <param name="percent">Effect strength as 0-1 for Slow; ignored for Stun</param>
        /// <param name="duration">Duration in seconds</param>
        public void ApplyEffect(string sourceId, PlayerEffectType effectType, float percent, float duration)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (duration <= 0f) return;
            if (effectType == PlayerEffectType.Slow && percent <= 0f) return;

            percent = Mathf.Clamp01(percent);
            float expireTime = Time.time + Mathf.Max(0.1f, duration);

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

            RecalculateAllEffects();
            ApplyToPlayer();
        }

        /// <summary>
        /// Removes a status effect by source ID.
        /// </summary>
        public void RemoveEffect(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;

            if (_activeEffects.Remove(sourceId))
            {
                RecalculateAllEffects();
                ApplyToPlayer();
            }
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
                // Actually, the modifier system should handle removal when duration expires
                // but we can explicitly remove by setting to 100% for a brief moment
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
        /// For debugging - logs all active effects.
        /// </summary>
        public void LogActiveEffects()
        {
            if (_activeEffects.Count == 0)
            {
                Debug.Log("[PlayerStatusEffectManager] No active effects");
                return;
            }

            string log = $"[PlayerStatusEffectManager] Active Effects ({_activeEffects.Count}):\n";
            foreach (var kvp in _activeEffects)
            {
                var e = kvp.Value;
                log += $"  - {kvp.Key}: {e.Type} {e.Percent * 100:F1}%, expires in {e.ExpireTime - Time.time:F1}s\n";
            }
            Debug.Log(log);
        }

        public enum PlayerEffectType
        {
            Slow,
            Stun
        }

        private struct PlayerEffect
        {
            public string SourceId;
            public PlayerEffectType Type;
            public float Percent;      // 0-1 for Slow, ignored for Stun
            public float ExpireTime;   // Time.time when effect expires
        }
    }
}