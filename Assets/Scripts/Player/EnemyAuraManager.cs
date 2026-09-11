using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Manages enemy aura effects:
    /// - Enemy auras affecting the player (Slow, etc.)
    /// - Enemy auras affecting other enemies (DamageReduction, etc.)
    /// Single entry point for all aura effects via ApplyAuraEffectToPlayer / ApplyAuraEffectToEnemy.
    /// </summary>
    public sealed class EnemyAuraManager : MonoBehaviour
    {
        private static EnemyAuraManager _instance;
        private static bool _isQuitting = false;
        public static EnemyAuraManager Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<EnemyAuraManager>();
                    if (_instance == null && Application.isPlaying)
                    {
                        var go = new GameObject("EnemyAuraManager");
                        _instance = go.AddComponent<EnemyAuraManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        // ===== Player-targeted auras =====
        private readonly HashSet<string> _activePlayerAuraSources = new();
        private Transform _playerTransform;
        private Player _player;

        // ===== Enemy-targeted auras =====
        // sourceId -> AuraSource
        private readonly Dictionary<string, AuraSource> _enemyAuraSources = new();
        // targetEnemy -> HashSet<sourceId>
        private readonly Dictionary<EnemyAi, HashSet<string>> _enemyAffectedBy = new();

        // Shared
        private int _enemyLayerMask;

        // Performance: update interval for enemy-to-enemy auras (5Hz)
        private float _enemyAuraUpdateTimer;
        private const float ENEMY_AURA_UPDATE_INTERVAL = 0.2f;

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

            UpdatePlayerAuras();
            UpdateEnemyAuras();
        }

        // ===================================================================
        // Player-targeted auras (existing logic)
        // ===================================================================
        private void UpdatePlayerAuras()
        {
            const float maxAuraCheckRadius = 10f;

            Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(_playerTransform.position, maxAuraCheckRadius, _enemyLayerMask);

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

                        float distance = Vector2.Distance(_playerTransform.position, enemy.transform.position);
                        if (distance <= action.radius)
                        {
                            string sourceId = $"Aura_{enemy.EnemyData.id}_{enemy.GetInstanceID()}_{action.effect}";
                            currentlyInRange.Add(sourceId);
                            ApplyAuraEffectToPlayer(sourceId, action, enemy);
                        }
                    }
                }
            }

            var sourcesToRemove = new List<string>();
            foreach (var sourceId in _activePlayerAuraSources)
            {
                if (!currentlyInRange.Contains(sourceId))
                    sourcesToRemove.Add(sourceId);
            }

            foreach (var sourceId in sourcesToRemove)
            {
                RemoveAuraEffectFromPlayer(sourceId);
                _activePlayerAuraSources.Remove(sourceId);
            }

            _activePlayerAuraSources.Clear();
            foreach (var sourceId in currentlyInRange)
            {
                _activePlayerAuraSources.Add(sourceId);
            }
        }

        private void ApplyAuraEffectToPlayer(string sourceId, EnemyEffectAction action, EnemyAi enemy)
        {
            switch (action.effect)
            {
                case StatusEffectType.Slow:
                    PlayerSlowManager.Instance.ApplyAuraSlow(sourceId, Mathf.Clamp01(action.value * 0.01f), action.radius, enemy.transform.position);
                    break;
                case StatusEffectType.DamageReduction: break;
                default:
                    Debug.LogWarning($"[EnemyAuraManager] Unknown player aura effect type: {action.effect} on enemy {enemy.EnemyData.id}");
                    break;
            }
        }

        private void RemoveAuraEffectFromPlayer(string sourceId)
        {
            if (sourceId.StartsWith("Aura_"))
            {
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

        // ===================================================================
        // Enemy-targeted auras (merged from EnemyDamageReductionAuraManager)
        // ===================================================================
        private void UpdateEnemyAuras()
        {
            // Cleanup phase: run EVERY frame for instant UI update when enemies leave range
            CleanupOutOfRangeAuras();

            // Apply phase: run at 5Hz to save physics queries
            _enemyAuraUpdateTimer += Time.deltaTime;
            if (_enemyAuraUpdateTimer < ENEMY_AURA_UPDATE_INTERVAL) return;
            _enemyAuraUpdateTimer = 0f;

            ApplyAurasToNearbyEnemies();
        }

        private void CleanupOutOfRangeAuras()
        {
            var enemiesToCheck = new List<EnemyAi>(_enemyAffectedBy.Keys);
            foreach (var targetEnemy in enemiesToCheck)
            {
                if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
                {
                    _enemyAffectedBy.Remove(targetEnemy);
                    continue;
                }

                var activeSourcesForTarget = _enemyAffectedBy[targetEnemy];
                var sourcesToRemoveFromTarget = new List<string>();

                foreach (var sourceId in activeSourcesForTarget)
                {
                    if (!_enemyAuraSources.TryGetValue(sourceId, out var source))
                    {
                        sourcesToRemoveFromTarget.Add(sourceId);
                        continue;
                    }

                    if (source.SourceEnemy == null)
                    {
                        sourcesToRemoveFromTarget.Add(sourceId);
                        continue;
                    }

                    float distance = Vector2.Distance(targetEnemy.transform.position, source.SourceEnemy.transform.position);
                    if (distance > source.Action.radius)
                    {
                        sourcesToRemoveFromTarget.Add(sourceId);
                    }
                }

                foreach (var sourceId in sourcesToRemoveFromTarget)
                {
                    RemoveAuraEffectFromEnemy(sourceId, targetEnemy);
                    activeSourcesForTarget.Remove(sourceId);
                }

                if (activeSourcesForTarget.Count == 0)
                {
                    _enemyAffectedBy.Remove(targetEnemy);
                }
            }
        }

        private void ApplyAurasToNearbyEnemies()
        {
            foreach (var kvp in _enemyAuraSources)
            {
                string sourceId = kvp.Key;
                var source = kvp.Value;

                if (source.SourceEnemy == null) continue;

                Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(
                    source.SourceEnemy.transform.position,
                    source.Action.radius,
                    _enemyLayerMask);

                foreach (var col in nearbyEnemies)
                {
                    if (!col.TryGetComponent<EnemyAi>(out var targetEnemy)) continue;
                    if (targetEnemy == source.SourceEnemy) continue;
                    ApplyAuraEffectToEnemy(sourceId, source, targetEnemy);
                }
            }
        }

        /// <summary>
        /// Registers an enemy as an aura source for enemy-to-enemy auras.
        /// Called from EnemyAi.Initialize when enemy has aura effects.
        /// </summary>
        public void RegisterEnemyAuraSource(EnemyAi sourceEnemy)
        {
            if (sourceEnemy == null || sourceEnemy.EnemyData == null || sourceEnemy.EnemyData.effects == null) return;

            foreach (var effect in sourceEnemy.EnemyData.effects)
            {
                if (effect?.aura == null) continue;
                foreach (var action in effect.aura)
                {
                    if (action == null || action.effect == StatusEffectType.None) continue;
                    if (action.radius <= 0f) continue;
                    if (action.value <= 0f) continue;

                    string sourceId = $"EnemyAura_{sourceEnemy.EnemyData.id}_{sourceEnemy.GetInstanceID()}_{action.effect}";
                    _enemyAuraSources[sourceId] = new AuraSource
                    {
                        SourceEnemy = sourceEnemy,
                        Action = action,
                        EffectType = action.effect
                    };
                }
            }
        }

        /// <summary>
        /// Unregisters an enemy as an aura source (when it dies or is disabled).
        /// Immediately removes aura effects from all affected enemies.
        /// </summary>
        public void UnregisterEnemyAuraSource(EnemyAi sourceEnemy)
        {
            if (sourceEnemy == null) return;

            var sourcesToRemove = new List<string>();
            foreach (var kvp in _enemyAuraSources)
            {
                if (kvp.Value.SourceEnemy == sourceEnemy)
                {
                    sourcesToRemove.Add(kvp.Key);
                }
            }

            foreach (var sourceId in sourcesToRemove)
            {
                // Remove aura effect from all enemies currently affected by this source
                var affectedEnemies = new List<EnemyAi>(_enemyAffectedBy.Keys);
                foreach (var targetEnemy in affectedEnemies)
                {
                    // Skip destroyed enemies
                    if (targetEnemy == null) continue;
                    if (_enemyAffectedBy.TryGetValue(targetEnemy, out var sources))
                    {
                        if (sources.Contains(sourceId))
                        {
                            RemoveAuraEffectFromEnemy(sourceId, targetEnemy);
                            sources.Remove(sourceId);
                        }

                        if (sources.Count == 0)
                        {
                            _enemyAffectedBy.Remove(targetEnemy);
                        }
                    }
                }

                _enemyAuraSources.Remove(sourceId);
            }
        }

        private void ApplyAuraEffectToEnemy(string sourceId, AuraSource source, EnemyAi targetEnemy)
        {
            if (!_enemyAffectedBy.TryGetValue(targetEnemy, out var sources))
            {
                sources = new HashSet<string>();
                _enemyAffectedBy[targetEnemy] = sources;
            }
            sources.Add(sourceId);

            if (!targetEnemy.TryGetComponent<EnemyStatusEffectController>(out var controller)) return;

            switch (source.EffectType)
            {
                case StatusEffectType.Slow: break;
                case StatusEffectType.DamageReduction:
                {
                    // Iron Guardian (or any enemy with DamageReduction aura) should not receive
                    // DamageReduction from other sources — they already have thick HP.
                    if (targetEnemy.EnemyData?.effects != null)
                    {
                        foreach (var effect in targetEnemy.EnemyData.effects)
                        {
                            if (effect?.aura != null)
                            {
                                foreach (var action in effect.aura)
                                {
                                    if (action?.effect == StatusEffectType.DamageReduction)
                                    {
                                        // Target is itself a DamageReduction aura source — skip
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    float reductionPercent = Mathf.Clamp01(source.Action.value * 0.01f);
                    var status = new DamageReductionStatus(reductionPercent, float.MaxValue);
                    controller.AddEffect(status);
                    break;
                }
                // Add other aura effect types here as needed
                default:
                    Debug.LogWarning($"[EnemyAuraManager] Unknown enemy aura effect type: {source.EffectType} on enemy {source.SourceEnemy.EnemyData.id}");
                    break;
            }

            targetEnemy.RefreshEnemyStatus();
        }

        private void RemoveAuraEffectFromEnemy(string sourceId, EnemyAi targetEnemy)
        {
            if (targetEnemy == null) return;
            if (!targetEnemy.TryGetComponent<EnemyStatusEffectController>(out var controller)) return;

            var parts = sourceId.Split('_');
            if (parts.Length >= 4)
            {
                string effectTypeStr = parts[3];
                if (System.Enum.TryParse<StatusEffectType>(effectTypeStr, out var effectType))
                {
                    controller.RemoveEffectImmediate(effectType, e => true);
                }
            }

            // Immediately refresh enemy status UI (health bar icons)
            targetEnemy.RefreshEnemyStatus();
        }

        private class AuraSource
        {
            public EnemyAi SourceEnemy;
            public EnemyEffectAction Action;
            public StatusEffectType EffectType;
        }

        /// <summary>
        /// Called when an enemy dies to immediately remove its auras (both player-targeted and enemy-targeted).
        /// </summary>
        public void OnEnemyDeath(EnemyAi enemy)
        {
            // Clean up player-targeted auras
            if (enemy?.EnemyData?.effects != null)
            {
                foreach (var effect in enemy.EnemyData.effects)
                {
                    if (effect?.aura == null) continue;
                    foreach (var action in effect.aura)
                    {
                        if (action == null || action.effect == StatusEffectType.None) continue;
                        string sourceId = $"Aura_{enemy.EnemyData.id}_{enemy.GetInstanceID()}_{action.effect}";
                        if (_activePlayerAuraSources.Contains(sourceId))
                        {
                            RemoveAuraEffectFromPlayer(sourceId);
                            _activePlayerAuraSources.Remove(sourceId);
                        }
                    }
                }
            }

            // Clean up enemy-targeted auras
            UnregisterEnemyAuraSource(enemy);
        }

        /// <summary>
        /// For debugging - logs all active auras.
        /// </summary>
        public void LogActiveAuras()
        {
            Debug.Log($"[EnemyAuraManager] Active Player Auras: {_activePlayerAuraSources.Count}");
            foreach (var sourceId in _activePlayerAuraSources)
            {
                Debug.Log($"  - {sourceId}");
            }
            Debug.Log($"[EnemyAuraManager] Active Enemy Aura Sources: {_enemyAuraSources.Count}");
            foreach (var kvp in _enemyAuraSources)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value.EffectType} radius={kvp.Value.Action.radius}");
            }
            Debug.Log($"[EnemyAuraManager] Affected Enemies: {_enemyAffectedBy.Count}");
        }
    }
}