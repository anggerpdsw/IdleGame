using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Manages slow effects applied to the player from various sources (enemy on-hit, on-take-damage, aura).
    /// Uses source-based tracking so multiple enemies can apply slow independently.
    /// Stacking rule: strongest slow wins (not additive).
    /// </summary>
    public sealed class PlayerSlowManager : MonoBehaviour
    {
        private static PlayerSlowManager _instance;
        public static PlayerSlowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerSlowManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PlayerSlowManager");
                        _instance = go.AddComponent<PlayerSlowManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // Active slow effects tracked by source ID
        private readonly Dictionary<string, SlowEffect> _activeSlows = new();

        // Current strongest slow percent (0-1)
        private float _currentSlowPercent = 0f;

        // Cached player reference
        private Player _player;

        // Movement speed multiplier from slow (1 = normal, 0.5 = 50% slow)
        public float CurrentSlowMultiplier => 1f - _currentSlowPercent;

        // Whether player is currently slowed
        public bool IsSlowed => _currentSlowPercent > 0f;

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
            // Clean up expired effects
            float currentTime = Time.time;
            bool changed = false;

            var keysToRemove = new List<string>();
            foreach (var kvp in _activeSlows)
            {
                if (kvp.Value.ExpireTime <= currentTime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _activeSlows.Remove(key);
                changed = true;
            }

            if (changed)
            {
                RecalculateStrongestSlow();
                ApplyToPlayer();
            }
        }

        /// <summary>
        /// Applies or refreshes a slow effect from a specific source.
        /// </summary>
        /// <param name="sourceId">Unique identifier for the source (e.g., "EnemyEffect_FrostGuardian_12345_Slow")</param>
        /// <param name="percent">Slow percentage as 0-1 (0.5 = 50% slow)</param>
        /// <param name="duration">Duration in seconds</param>
        public void ApplySlow(string sourceId, float percent, float duration)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (percent <= 0f) return;

            percent = Mathf.Clamp01(percent);
            float expireTime = Time.time + Mathf.Max(0.1f, duration);

            if (_activeSlows.TryGetValue(sourceId, out var existing))
            {
                // Refresh duration if new duration is longer
                if (expireTime > existing.ExpireTime)
                {
                    existing.ExpireTime = expireTime;
                    existing.Percent = percent; // Update percent in case it changed
                }
            }
            else
            {
                _activeSlows[sourceId] = new SlowEffect
                {
                    SourceId = sourceId,
                    Percent = percent,
                    ExpireTime = expireTime
                };
            }

            RecalculateStrongestSlow();
            ApplyToPlayer();
        }

        /// <summary>
        /// Removes a slow effect by source ID.
        /// </summary>
        public void RemoveSlow(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;

            if (_activeSlows.Remove(sourceId))
            {
                RecalculateStrongestSlow();
                ApplyToPlayer();
            }
        }

        /// <summary>
        /// Applies an aura slow effect (persistent while in range).
        /// Uses a source ID that includes the enemy instance ID.
        /// </summary>
        public void ApplyAuraSlow(string sourceId, float percent, float radius, Vector2 enemyPosition)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (percent <= 0f) return;

            // For aura, we use a very long duration and refresh it every frame while in range
            // The AuraManager will call this every frame, so we just keep refreshing
            ApplySlow(sourceId, percent, 1f); // 1 second duration, refreshed each frame
        }

        /// <summary>
        /// Removes an aura slow effect when player leaves range.
        /// </summary>
        public void RemoveAuraSlow(string sourceId) => RemoveSlow(sourceId);

        /// <summary>
        /// Recalculates the strongest slow from all active effects.
        /// Stacking rule: strongest slow wins (not additive).
        /// </summary>
        private void RecalculateStrongestSlow()
        {
            float maxSlow = 0f;
            foreach (var effect in _activeSlows.Values)
            {
                if (effect.Percent > maxSlow)
                {
                    maxSlow = effect.Percent;
                }
            }
            _currentSlowPercent = maxSlow;
        }

        /// <summary>
        /// Applies the current slow multiplier to the player's movement speed.
        /// Uses the remaining duration of the strongest slow so the modifier
        /// doesn't expire prematurely.
        /// </summary>
        private void ApplyToPlayer()
        {
            if (_player == null) _player = Player.Instance;
            if (_player != null)
            {
                // Convert multiplier (0.9 = 10% slow) to percent value (90 = 90% speed = -10% modifier)
                float multiplierPercent = CurrentSlowMultiplier * 100f;

                // Find the longest remaining duration among active slows
                // so the temporary modifier persists for the full effect duration.
                float maxRemainingDuration = 0f;
                float currentTime = Time.time;
                foreach (var effect in _activeSlows.Values)
                {
                    float remaining = effect.ExpireTime - currentTime;
                    if (remaining > maxRemainingDuration)
                        maxRemainingDuration = remaining;
                }

                // Apply as temporary percent modifier via ModifierManager
                // Use at least 0.1s to avoid zero-duration modifiers
                float duration = Mathf.Max(0.1f, maxRemainingDuration);
                PlayerStatsManager.Instance?.ApplyTemporaryModifier(
                    SkillType.MoveSpeed,
                    multiplierPercent,
                    "EnemySlow",
                    duration
                );
                // Force immediate stat recalculation after applying slow
                ModifierManager.Instance?.RebuildAllDirty();
                // === ICE VISUAL SYNC ===
                _player.SetIceEffect(_currentSlowPercent > 0f);
            }
        }

        /// <summary>
        /// Gets the current slow percent (0-1).
        /// </summary>
        public float GetCurrentSlowPercent() => _currentSlowPercent;

        /// <summary>
        /// Gets the number of active slow sources.
        /// </summary>
        public int ActiveSlowCount => _activeSlows.Count;

        /// <summary>
        /// For debugging - logs all active slows.
        /// </summary>
        public void LogActiveSlows()
        {
            if (_activeSlows.Count == 0)
            {
                Debug.Log("[PlayerSlowManager] No active slows");
                return;
            }

            string log = $"[PlayerSlowManager] Active Slows ({_activeSlows.Count}):\n";
            foreach (var kvp in _activeSlows)
            {
                log += $"  - {kvp.Key}: {kvp.Value.Percent * 100:F1}%, expires in {kvp.Value.ExpireTime - Time.time:F1}s\n";
            }
            Debug.Log(log);
        }

        private struct SlowEffect
        {
            public string SourceId;
            public float Percent;      // 0-1
            public float ExpireTime;   // Time.time when effect expires
        }
    }
}