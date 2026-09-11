using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Manages enemy aura effects on the player.
    /// Player-centric: checks which enemy auras the player is inside each frame.
    /// Efficient for large numbers of enemies (>1000).
    /// </summary>
    public sealed class EnemyAuraManager : MonoBehaviour
    {
        private static EnemyAuraManager _instance;
        public static EnemyAuraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<EnemyAuraManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("EnemyAuraManager");
                        _instance = go.AddComponent<EnemyAuraManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // Track which aura sources are currently affecting the player
        private readonly HashSet<string> _activeAuraSources = new();

        // Cache player transform
        private Transform _playerTransform;
        private Player _player;

        // Layer mask for enemy detection
        private int _enemyLayerMask;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _enemyLayerMask = LayerMask.GetMask("Enemy");
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                _player = Player.Instance;
                if (_player != null) _playerTransform = _player.transform;
            }
            if (_playerTransform == null) return;

            UpdateAuras();
        }

        /// <summary>
        /// Checks all enemies within max aura range and applies/removes aura effects.
        /// </summary>
        private void UpdateAuras()
        {
            // Find all enemies within a reasonable max aura radius
            // Max aura radius from data is typically around 4-5 units, so use 10 as safe bound
            const float maxAuraCheckRadius = 10f;

            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(_playerTransform.position, maxAuraCheckRadius, _enemyLayerMask);

            // Track which sources are still in range this frame
            var currentlyInRange = new HashSet<string>();

            foreach (var col in nearbyEnemies)
            {
                if (!col.TryGetComponent<EnemyAi>(out var enemy)) continue;
                if (enemy.EnemyData?.effects == null) continue;

                foreach (var effect in enemy.EnemyData.effects)
                {
                    if (effect?.aura == null) continue;
                    foreach (var action in effect.aura)
                    {
                        if (action == null || action.effect == StatusEffectType.None) continue;
                        if (action.radius <= 0f) continue;
                        if (action.value <= 0f) continue;

                        // Check if player is within aura radius
                        float distance = Vector2.Distance(_playerTransform.position, enemy.transform.position);
                        if (distance <= action.radius)
                        {
                            string sourceId =
                                $"Aura_{enemy.EnemyData.id}_" +
                                $"{enemy.GetInstanceID()}_{action.effect}";
                            currentlyInRange.Add(sourceId);

                            // Apply/refresh aura effect
                            ApplyAuraEffect(sourceId, action, enemy);
                        }
                    }
                }
            }

            // Remove aura effects for sources no longer in range
            var sourcesToRemove = new List<string>();
            foreach (var sourceId in _activeAuraSources)
            {
                if (!currentlyInRange.Contains(sourceId))
                    sourcesToRemove.Add(sourceId);
            }

            foreach (var sourceId in sourcesToRemove)
            {
                RemoveAuraEffect(sourceId);
                _activeAuraSources.Remove(sourceId);
            }

            // Update active sources
            _activeAuraSources.Clear();
            foreach (var sourceId in currentlyInRange)
            {
                _activeAuraSources.Add(sourceId);
            }
        }

        /// <summary>
        /// Applies an aura effect to the player.
        /// </summary>
        private void ApplyAuraEffect(string sourceId, EnemyEffectAction action, EnemyAi enemy)
        {
            switch (action.effect)
            {
                case StatusEffectType.Slow:
                    PlayerSlowManager.Instance.ApplyAuraSlow(sourceId, Mathf.Clamp01(action.value * 0.01f), action.radius, enemy.transform.position);
                    break;
                default:
                    Debug.LogWarning($"[EnemyAuraManager] Unknown aura effect type: {action.effect} on enemy {enemy.EnemyData.id}");
                    break;
            }
        }

        /// <summary>
        /// Removes an aura effect from the player.
        /// </summary>
        private void RemoveAuraEffect(string sourceId)
        {
            // Check what type of effect this was by parsing sourceId
            if (sourceId.StartsWith("Aura_"))
            {
                // Extract effect type from sourceId
                // Format: Aura_EnemyId_InstanceID_EffectType
                var parts = sourceId.Split('_');
                if (parts.Length >= 4)
                {
                    string effectType = parts[3];
                    switch (effectType)
                    {
                        case "Slow":
                            PlayerSlowManager.Instance.RemoveAuraSlow(sourceId);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Called when an enemy dies to immediately remove its aura.
        /// </summary>
        public void OnEnemyDeath(EnemyAi enemy)
        {
            if (enemy?.EnemyData?.effects == null) return;
            foreach (var effect in enemy.EnemyData.effects)
            {
                if (effect?.aura == null) continue;
                foreach (var action in effect.aura)
                {
                    if (action == null || action.effect == StatusEffectType.None) continue;
                    string sourceId = $"Aura_{enemy.EnemyData.id}_{enemy.GetInstanceID()}_{action.effect}";
                    if (_activeAuraSources.Contains(sourceId))
                    {
                        RemoveAuraEffect(sourceId);
                        _activeAuraSources.Remove(sourceId);
                    }
                }
            }
        }

        /// <summary>
        /// For debugging - logs all active auras.
        /// </summary>
        public void LogActiveAuras()
        {
            Debug.Log($"[EnemyAuraManager] Active Auras: {_activeAuraSources.Count}");
            foreach (var sourceId in _activeAuraSources)
            {
                Debug.Log($"  - {sourceId}");
            }
        }
    }
}