using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Central controller for managing all status effects on an enemy.
    /// Handles application, ticking, stacking, and expiration of effects.
    /// Uses a scheduled tick system for performance (ticks DoT effects at fixed intervals).
    /// </summary>
    public sealed class EnemyStatusEffectController : MonoBehaviour
    {
        private readonly List<IStatusEffect> _effects = new();
        private readonly List<IStatusEffect> _effectsToRemove = new();
        private readonly List<IStatusEffect> _effectsToAdd = new();

        // Scheduled tick system for DoT effects (more efficient than every frame)
        private float _dotTickTimer;
        private const float DOT_TICK_INTERVAL = 0.1f; // Tick DoT effects 10 times per second
        private bool _isInitialized;

        private EnemyAi _enemy;
        private EnemyAi Enemy => _enemy ??= GetComponent<EnemyAi>();

        #region Events
        public event Action<IStatusEffect> OnEffectApplied;
        public event Action<IStatusEffect> OnEffectRemoved;
        public event Action<IStatusEffect> OnEffectStacked;
        public event Action OnEffectsChanged;
        #endregion

        #region Properties
        public IReadOnlyList<IStatusEffect> ActiveEffects => _effects;
        public int EffectCount => _effects.Count;
        public bool HasAnyEffect => _effects.Count > 0;
        #endregion

        private void Awake()
        {
            _enemy = GetComponent<EnemyAi>();
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            float deltaTime = Time.deltaTime;

            // Process pending additions
            if (_effectsToAdd.Count > 0)
            {
                foreach (var effect in _effectsToAdd)
                {
                    ApplyEffectInternal(effect);
                }
                _effectsToAdd.Clear();
                OnEffectsChanged?.Invoke();
            }

            // Tick all effects every frame (for CC effects like freeze, stun, root, fear)
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];

                // Skip DoT effects here - they're handled by scheduled tick
                if (effect.Category == StatusEffectCategory.DamageOverTime)
                    continue;

                effect.Tick(Enemy, deltaTime);

                if (effect.IsExpired)
                {
                    _effectsToRemove.Add(effect);
                }
            }

            // Scheduled tick for Damage Over Time effects
            _dotTickTimer += deltaTime;
            if (_dotTickTimer >= DOT_TICK_INTERVAL)
            {
                _dotTickTimer = 0f;

                for (int i = _effects.Count - 1; i >= 0; i--)
                {
                    var effect = _effects[i];

                    if (effect.Category != StatusEffectCategory.DamageOverTime)
                        continue;

                    effect.Tick(Enemy, DOT_TICK_INTERVAL);

                    if (effect.IsExpired)
                    {
                        _effectsToRemove.Add(effect);
                    }
                }
            }

            // Process removals
            if (_effectsToRemove.Count > 0)
            {
                foreach (var effect in _effectsToRemove)
                {
                    RemoveEffectInternal(effect);
                }
                _effectsToRemove.Clear();
                OnEffectsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Adds a status effect to the enemy. Handles stacking logic automatically.
        /// </summary>
        public void AddEffect(IStatusEffect effect)
        {
            if (effect == null) return;

            // Check for existing effect of same type
            for (int i = 0; i < _effects.Count; i++)
            {
                var existing = _effects[i];
                if (existing.Type == effect.Type)
                {
                    HandleStacking(existing, effect);
                    return;
                }
            }

            // No existing effect, queue for addition
            _effectsToAdd.Add(effect);
        }

        /// <summary>
        /// Adds a status effect immediately (bypasses queue).
        /// </summary>
        public void AddEffectImmediate(IStatusEffect effect)
        {
            if (effect == null) return;

            // Check for existing effect of same type
            for (int i = 0; i < _effects.Count; i++)
            {
                var existing = _effects[i];
                if (existing.Type == effect.Type)
                {
                    HandleStacking(existing, effect);
                    return;
                }
            }

            ApplyEffectInternal(effect);
            OnEffectsChanged?.Invoke();
        }

        private void HandleStacking(IStatusEffect existing, IStatusEffect newEffect)
        {
            int newStackCount = Mathf.Min(existing.StackCount + 1, existing.MaxStacks);

            switch (existing.StackPolicy)
            {
                case StackPolicy.RefreshDuration:
                    existing.OnStackAdded(Enemy, existing.StackCount); // Keep same stack count, refresh duration
                    break;

                case StackPolicy.Additive:
                    existing.OnStackAdded(Enemy, newStackCount);
                    OnEffectStacked?.Invoke(existing);
                    break;

                case StackPolicy.MaximumValue:
                    // Keep the one with higher value
                    if (newEffect.GetCurrentValue() > existing.GetCurrentValue())
                    {
                        // Replace with new effect
                        RemoveEffectInternal(existing);
                        _effectsToAdd.Add(newEffect);
                    }
                    else
                    {
                        // Refresh duration of existing
                        existing.OnStackAdded(Enemy, existing.StackCount);
                    }
                    break;

                case StackPolicy.Replace:
                    RemoveEffectInternal(existing);
                    _effectsToAdd.Add(newEffect);
                    break;

                case StackPolicy.Multiplicative:
                    existing.OnStackAdded(Enemy, newStackCount);
                    OnEffectStacked?.Invoke(existing);
                    break;

                case StackPolicy.Custom:
                    existing.OnStackAdded(Enemy, newStackCount);
                    OnEffectStacked?.Invoke(existing);
                    break;
            }
        }

        private void ApplyEffectInternal(IStatusEffect effect)
        {
            effect.OnApply(Enemy);
            _effects.Add(effect);
            OnEffectApplied?.Invoke(effect);
        }

        private void RemoveEffectInternal(IStatusEffect effect)
        {
            effect.OnExpire(Enemy);
            _effects.Remove(effect);
            OnEffectRemoved?.Invoke(effect);
        }

        /// <summary>
        /// Removes a specific status effect by type.
        /// </summary>
        public bool RemoveEffect(StatusEffectType type)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Type == type)
                {
                    _effectsToRemove.Add(_effects[i]);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes a specific status effect instance.
        /// </summary>
        public bool RemoveEffect(IStatusEffect effect)
        {
            if (_effects.Contains(effect))
            {
                _effectsToRemove.Add(effect);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes all effects of a specific category.
        /// </summary>
        public void RemoveEffectsByCategory(StatusEffectCategory category)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Category == category)
                {
                    _effectsToRemove.Add(_effects[i]);
                }
            }
        }

        /// <summary>
        /// Clears all status effects.
        /// </summary>
        public void ClearAllEffects()
        {
            _effectsToRemove.AddRange(_effects);
            _effectsToAdd.Clear();
        }

        /// <summary>
        /// Checks if the enemy has a specific status effect.
        /// </summary>
        public bool HasEffect(StatusEffectType type)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a specific status effect by type.
        /// </summary>
        public IStatusEffect GetEffect(StatusEffectType type)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Type == type) return _effects[i];
            }
            return null;
        }

        /// <summary>
        /// Gets the first effect of a specific category.
        /// </summary>
        public IStatusEffect GetEffectByCategory(StatusEffectCategory category)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Category == category) return _effects[i];
            }
            return null;
        }

        /// <summary>
        /// Gets all effects of a specific category.
        /// </summary>
        public List<IStatusEffect> GetEffectsByCategory(StatusEffectCategory category)
        {
            var result = new List<IStatusEffect>();
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Category == category) result.Add(_effects[i]);
            }
            return result;
        }

        /// <summary>
        /// Gets the total damage per second from all DoT effects.
        /// </summary>
        public float GetTotalDoTDamagePerSecond()
        {
            float total = 0f;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Category == StatusEffectCategory.DamageOverTime)
                {
                    total += _effects[i].GetCurrentValue();
                }
            }
            return total;
        }

        /// <summary>
        /// Gets the effective slow multiplier from all slow effects.
        /// </summary>
        public float GetEffectiveSlowMultiplier()
        {
            float multiplier = 1f;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] is SlowStatus slow)
                {
                    multiplier *= (1f - slow.GetCurrentValue());
                }
            }
            return multiplier;
        }

        /// <summary>
        /// Checks if enemy is currently crowd controlled (stunned, frozen, rooted, feared, etc.).
        /// </summary>
        public bool IsCrowdControlled()
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                var effect = _effects[i];
                if (effect.Category == StatusEffectCategory.CrowdControl)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if enemy is immune to a specific effect category.
        /// </summary>
        public bool IsImmuneTo(StatusEffectCategory category)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Category == StatusEffectCategory.Immunity)
                {
                    // Check specific immunity type
                    switch (category)
                    {
                        case StatusEffectCategory.CrowdControl:
                            if (_effects[i].Type == StatusEffectType.ControlImmune) return true;
                            break;
                        case StatusEffectCategory.DamageOverTime:
                            if (_effects[i].Type == StatusEffectType.DamageOverTimeImmune) return true;
                            break;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets remaining duration of a specific effect type.
        /// </summary>
        public float GetRemainingDuration(StatusEffectType type)
        {
            var effect = GetEffect(type);
            if (effect == null) return 0f;
            return Mathf.Max(0f, effect.Duration - effect.ElapsedTime);
        }

        /// <summary>
        /// Refreshes duration of an existing effect.
        /// </summary>
        public bool RefreshEffect(StatusEffectType type, float newDuration = -1f)
        {
            var effect = GetEffect(type);
            if (effect == null) return false;

            effect.ElapsedTime = 0f;
            if (newDuration > 0f) effect.Duration = newDuration;
            return true;
        }

        private void OnDisable()
        {
            // Clear all effects when disabled (for object pooling)
            ClearAllEffects();
        }

        /// <summary>
        /// For debugging - logs all active effects.
        /// </summary>
        public void LogActiveEffects()
        {
            if (_effects.Count == 0)
            {
                Debug.Log($"[{Enemy.name}] No active status effects");
                return;
            }

            var log = $"[{Enemy.name}] Active Effects ({_effects.Count}):\n";
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                log += $"  - {e.Type} (Stacks: {e.StackCount}/{e.MaxStacks}, Duration: {e.Duration - e.ElapsedTime:F1}s/{e.Duration}s, Value: {e.GetCurrentValue():F2})\n";
            }
            Debug.Log(log);
        }
    }
}