using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using PlayerClass = IdleDefenseSurvival.Player.Player;
using IdleDefenseSurvival.Enemy.StatusEffects;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Enemy
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

        private void OnApplicationQuit() => _isQuitting = true;

        // ===== Player-targeted auras =====
        // sourceId -> EffectType (replaces HashSet, avoids string.Split on removal)
        private readonly Dictionary<StatusSourceId, StatusEffectType> _activePlayerAuraSources = new();
        private Transform _playerTransform;
        private PlayerClass _player;

        // ===== Enemy-targeted auras =====
        // sourceId -> AuraSource
        private readonly Dictionary<StatusSourceId, AuraSource> _enemyAuraSources = new();
        // targetEnemy -> HashSet<sourceId>
        private readonly Dictionary<EnemyAi, HashSet<StatusSourceId>> _enemyAffectedBy = new();

        // Shared
        private int _enemyLayerMask;

        // Performance: update interval for enemy-to-enemy auras (5Hz)
        private float _enemyAuraUpdateTimer;
        private const float ENEMY_AURA_UPDATE_INTERVAL = 0.2f;

        // Performance: update interval for player-targeted auras (10Hz)
        private float _playerAuraUpdateTimer;
        private const float PLAYER_AURA_UPDATE_INTERVAL = 0.1f;

        // ===== Allocation-free physics buffers & cached collections =====
        private readonly Collider2D[] _overlapBuffer = new Collider2D[1024];
        private readonly List<EnemyAi> _enemyCleanupList = new();
        private readonly List<StatusSourceId> _sourceCleanupList = new();
        private readonly List<StatusSourceId> _removeFromTargetList = new();
        private readonly List<StatusSourceId> _sourcesToRemove = new();
        private readonly List<EnemyAi> _affectedEnemiesCleanup = new();

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
            // ---- player auras -------------------------------------------------
            if (_playerTransform == null)
            {
                _player = PlayerClass.Instance;
                if (_player != null) _playerTransform = _player.transform;
            }
            if (_playerTransform == null) return;

            _playerAuraUpdateTimer += Time.deltaTime;
            if (_playerAuraUpdateTimer >= PLAYER_AURA_UPDATE_INTERVAL)
            {
                _playerAuraUpdateTimer = 0f;
                UpdatePlayerAuras();
            }

            // ---- enemy-to-enemy auras (cleanup + apply at same 5Hz interval) ---
            _enemyAuraUpdateTimer += Time.deltaTime;
            if (_enemyAuraUpdateTimer < ENEMY_AURA_UPDATE_INTERVAL) return;
            _enemyAuraUpdateTimer = 0f;

            CleanupOutOfRangeAuras();
            ApplyAurasToNearbyEnemies();
            ProcessRegenerationAuras();
        }

        // -----------------------------------------------------------------------
        // Player-targeted auras
        // -----------------------------------------------------------------------
        private void UpdatePlayerAuras()
        {
            const float maxAuraCheckRadius = 10f;
            int count = Physics2D.OverlapCircle(
                _playerTransform.position,
                maxAuraCheckRadius,
                new ContactFilter2D { useLayerMask = true, layerMask = _enemyLayerMask },
                _overlapBuffer);

            _sourceCleanupList.Clear();
            _sourceCleanupList.AddRange(_activePlayerAuraSources.Keys);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (!col.TryGetComponent<EnemyAi>(out var enemy)) continue;
                if (enemy.EnemyData?.effects == null) continue;

                foreach (var effect in enemy.EnemyData.effects)
                {
                    if (effect?.aura == null) continue;
                    foreach (var action in effect.aura)
                    {
                        if (action == null || action.effect == StatusEffectType.None) continue;
                        if (action.radius <= 0f) continue;

                        float distSq = (enemy.transform.position - _playerTransform.position).sqrMagnitude;
                        if (distSq > action.radius * action.radius) continue;

                        var sourceId = new StatusSourceId(enemy.GetInstanceID(), (int)action.effect);
                        _sourceCleanupList.Remove(sourceId);
                        _activePlayerAuraSources[sourceId] = action.effect;
                        ApplyAuraEffectToPlayer(sourceId, action, enemy);
                    }
                }
            }

            foreach (var sourceId in _sourceCleanupList)
            {
                if (_activePlayerAuraSources.TryGetValue(sourceId, out var effectType))
                {
                    RemoveAuraEffectFromPlayer(sourceId, effectType);
                    _activePlayerAuraSources.Remove(sourceId);
                }
            }
        }

        private void ApplyAuraEffectToPlayer(StatusSourceId sourceId, EnemyEffectAction action, EnemyAi enemy)
        {
            switch (action.effect)
            {
                case StatusEffectType.Slow:
                    PlayerStatusEffectManager.Instance.ApplyAuraEffect(sourceId,
                        PlayerStatusEffectManager.PlayerEffectType.Slow,
                        Mathf.Clamp01(action.value * 0.01f),
                        action.radius,
                        enemy.transform.position);
                    break;
                case StatusEffectType.Unregeneration:
                    PlayerStatusEffectManager.Instance.ApplyAuraEffect(sourceId,
                        PlayerStatusEffectManager.PlayerEffectType.Unregeneration,
                        1f, // Binary effect: 1 = blocked, 0 = not blocked
                        action.radius,
                        enemy.transform.position);
                    break;
                case StatusEffectType.DamageReduction:
                    break;
                case StatusEffectType.Regeneration:
                    // Regeneration is enemy-to-enemy only, does not affect player
                    break;
                default:
                    Debug.LogWarning($"[EnemyAuraManager] Unknown player aura: {action.effect}");
                    break;
            }
        }

        private void RemoveAuraEffectFromPlayer(StatusSourceId sourceId, StatusEffectType effectType)
        {
            if (effectType == StatusEffectType.Slow || effectType == StatusEffectType.Unregeneration)
                PlayerStatusEffectManager.Instance.RemoveAuraEffect(sourceId);
        }

        // -----------------------------------------------------------------------
        // Enemy-targeted auras
        // -----------------------------------------------------------------------
        private void CleanupOutOfRangeAuras()
        {
            _enemyCleanupList.Clear();
            _enemyCleanupList.AddRange(_enemyAffectedBy.Keys);

            foreach (var targetEnemy in _enemyCleanupList)
            {
                if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
                {
                    _enemyAffectedBy.Remove(targetEnemy);
                    continue;
                }

                var activeSources = _enemyAffectedBy[targetEnemy];
                _removeFromTargetList.Clear();

                foreach (var sourceId in activeSources)
                {
                    if (!_enemyAuraSources.TryGetValue(sourceId, out var src) ||
                        src.SourceEnemy == null ||
                        (targetEnemy.transform.position - src.SourceEnemy.transform.position).sqrMagnitude
                            > src.Action.radius * src.Action.radius)
                    {
                        _removeFromTargetList.Add(sourceId);
                    }
                }

                foreach (var srcId in _removeFromTargetList)
                {
                    RemoveAuraEffectFromEnemy(srcId, targetEnemy);
                    activeSources.Remove(srcId);
                }

                if (activeSources.Count == 0) _enemyAffectedBy.Remove(targetEnemy);
            }
        }

        private void ApplyAurasToNearbyEnemies()
        {
            foreach (var kvp in _enemyAuraSources)
            {
                var source = kvp.Value;
                if (source.SourceEnemy == null) continue;

                int count = Physics2D.OverlapCircle(
                    source.SourceEnemy.transform.position,
                    source.Action.radius,
                    new ContactFilter2D { useLayerMask = true, layerMask = _enemyLayerMask },
                    _overlapBuffer);

                for (int i = 0; i < count; i++)
                {
                    var col = _overlapBuffer[i];
                    if (!col.TryGetComponent<EnemyAi>(out var targetEnemy)) continue;
                    if (targetEnemy == source.SourceEnemy) continue;
                    ApplyAuraEffectToEnemy(kvp.Key, source, targetEnemy);
                }
            }
        }

        /// <summary>
        /// Process Regeneration aura heal for all registered Vampire sources.
        /// Called at ENEMY_AURA_UPDATE_INTERVAL (5Hz).
        /// </summary>
        private void ProcessRegenerationAuras()
        {
            foreach (var kvp in _enemyAuraSources)
            {
                var source = kvp.Value;
                if (source.SourceEnemy == null) continue;
                if (source.EffectType != StatusEffectType.Regeneration) continue;

                // Heal per tick = source MaxHealth * (value / 100) * interval
                float healPerSecond = source.SourceEnemy.MaxHealth * (source.Action.value * 0.01f);
                float healThisTick = healPerSecond * ENEMY_AURA_UPDATE_INTERVAL;

                int count = Physics2D.OverlapCircle(
                    source.SourceEnemy.transform.position,
                    source.Action.radius,
                    new ContactFilter2D { useLayerMask = true, layerMask = _enemyLayerMask },
                    _overlapBuffer);

                for (int i = 0; i < count; i++)
                {
                    var col = _overlapBuffer[i];
                    if (!col.TryGetComponent<EnemyAi>(out var targetEnemy)) continue;
                    if (targetEnemy.CurrentHealth >= targetEnemy.MaxHealth) continue;
                    if (!targetEnemy.gameObject.activeInHierarchy) continue;

                    targetEnemy.Heal(healThisTick);
                }
            }
        }

        /// <summary>
        /// Registers an enemy as an aura source for enemy-to-enemy auras.
        /// Called from EnemyAi.Initialize when enemy has aura effects.
        /// </summary>
        public void RegisterEnemyAuraSource(EnemyAi sourceEnemy)
        {
            if (sourceEnemy?.EnemyData?.effects == null) return;

            foreach (var effect in sourceEnemy.EnemyData.effects)
            {
                if (effect?.aura == null) continue;
                foreach (var action in effect.aura)
                {
                    if (action == null || action.effect == StatusEffectType.None) continue;
                    if (action.radius <= 0f || action.value <= 0f) continue;

                    var sourceId = new StatusSourceId(sourceEnemy.GetInstanceID(), (int)action.effect);
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

            _sourcesToRemove.Clear();
            foreach (var kvp in _enemyAuraSources)
                if (kvp.Value.SourceEnemy == sourceEnemy) _sourcesToRemove.Add(kvp.Key);

            foreach (var srcId in _sourcesToRemove)
            {
                _affectedEnemiesCleanup.Clear();
                _affectedEnemiesCleanup.AddRange(_enemyAffectedBy.Keys);
                foreach (var target in _affectedEnemiesCleanup)
                {
                    if (_enemyAffectedBy.TryGetValue(target, out var set) && set.Contains(srcId))
                    {
                        RemoveAuraEffectFromEnemy(srcId, target);
                        set.Remove(srcId);
                        if (set.Count == 0) _enemyAffectedBy.Remove(target);
                    }
                }
                _enemyAuraSources.Remove(srcId);
            }
        }

        private void ApplyAuraEffectToEnemy(StatusSourceId sourceId, AuraSource source, EnemyAi targetEnemy)
        {
            if (!_enemyAffectedBy.TryGetValue(targetEnemy, out var set))
            {
                set = new HashSet<StatusSourceId>();
                _enemyAffectedBy[targetEnemy] = set;
            }
            set.Add(sourceId);

            if (!targetEnemy.TryGetComponent<EnemyStatusEffectController>(out var controller)) return;

            switch (source.EffectType)
            {
                case StatusEffectType.DamageReduction:
                    if (targetEnemy.EnemyData?.effects != null)
                    {
                        foreach (var eff in targetEnemy.EnemyData.effects)
                        {
                            if (eff?.aura == null) continue;
                            foreach (var act in eff.aura)
                                if (act?.effect == StatusEffectType.DamageReduction) return;
                        }
                    }
                    var reduction = Mathf.Clamp01(source.Action.value * 0.01f);
                    controller.AddEffect(new DamageReductionStatus(reduction, float.MaxValue, sourceId.EnemyInstanceId, sourceId.EffectCode));
                    break;
                case StatusEffectType.Regeneration:
                    // Regeneration is processed centrally in ProcessRegenerationAuras()
                    // No status effect needs to be added to target
                    break;
                case StatusEffectType.Slow:
                    break;
                default:
                    Debug.LogWarning($"[EnemyAuraManager] Unknown enemy aura: {source.EffectType}");
                    break;
            }

            targetEnemy.RefreshEnemyStatus();
        }

        private void RemoveAuraEffectFromEnemy(StatusSourceId sourceId, EnemyAi targetEnemy)
        {
            if (targetEnemy == null) return;
            if (!targetEnemy.TryGetComponent<EnemyStatusEffectController>(out var controller)) return;

            if (_enemyAuraSources.TryGetValue(sourceId, out var src))
                controller.RemoveEffectImmediate(src.EffectType, sourceId.EnemyInstanceId, sourceId.EffectCode);
            else
                Debug.LogWarning($"[EnemyAuraManager] Missing source entry for removal: {sourceId}");

            targetEnemy.RefreshEnemyStatus();
        }

        private class AuraSource
        {
            public EnemyAi SourceEnemy;
            public EnemyEffectAction Action;
            public StatusEffectType EffectType;
        }

        /// <summary>
        /// Effect trigger discriminator for StatusSourceId.
        /// </summary>
        public enum EffectTrigger
        {
            Aura = 0,
            OnHit = 1,
            OnTakeDamage = 2
        }

        /// <summary>
        /// Compact key – combines enemy instance ID, effect enum value, and trigger type.
        /// </summary>
        public readonly struct StatusSourceId : IEquatable<StatusSourceId>
        {
            public readonly int EnemyInstanceId;
            public readonly int EffectCode; // (int)StatusEffectType
            public readonly EffectTrigger Trigger;

            public StatusSourceId(int enemyInstanceId, int effectCode, EffectTrigger trigger = EffectTrigger.Aura)
            {
                EnemyInstanceId = enemyInstanceId;
                EffectCode = effectCode;
                Trigger = trigger;
            }

            public bool Equals(StatusSourceId other) =>
                EnemyInstanceId == other.EnemyInstanceId && EffectCode == other.EffectCode && Trigger == other.Trigger;

            public override bool Equals(object obj) => obj is StatusSourceId other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(EnemyInstanceId, EffectCode, Trigger);

            public override string ToString() => $"Src_{EnemyInstanceId}_{EffectCode}_{Trigger}";
        }

        /// <summary>
        /// Called when an enemy dies to immediately remove its auras (both player-targeted and enemy-targeted).
        /// Does NOT remove timed effects (OnHit/OnTakeDamage) - those persist until their duration expires.
        /// </summary>
        public void OnEnemyDeath(EnemyAi enemy)
        {
            // Clean up player-targeted auras only (not timed effects)
            if (enemy?.EnemyData?.effects != null)
            {
                foreach (var effect in enemy.EnemyData.effects)
                {
                    if (effect?.aura == null) continue;
                    foreach (var action in effect.aura)
                    {
                        var srcId = new StatusSourceId(enemy.GetInstanceID(), (int)action.effect, EffectTrigger.Aura);
                        if (_activePlayerAuraSources.TryGetValue(srcId, out var et))
                        {
                            RemoveAuraEffectFromPlayer(srcId, et);
                            _activePlayerAuraSources.Remove(srcId);
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
            Debug.Log($"[EnemyAuraManager] Player auras: {_activePlayerAuraSources.Count}");
            foreach (var kvp in _activePlayerAuraSources) Debug.Log($"  - {kvp.Key}: {kvp.Value}");

            Debug.Log($"[EnemyAuraManager] Enemy sources: {_enemyAuraSources.Count}");
            foreach (var kvp in _enemyAuraSources)
                Debug.Log($"  - {kvp.Key}: {kvp.Value.EffectType} r={kvp.Value.Action.radius}");

            Debug.Log($"[EnemyAuraManager] Affected enemies: {_enemyAffectedBy.Count}");
        }
    }
}