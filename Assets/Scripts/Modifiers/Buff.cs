using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using System.Linq;

namespace IdleDefenseSurvival.Modifiers
{
    /// <summary>
    /// Buff - temporary positive effect applied to player/enemies.
    /// </summary>
    [Serializable]
    public class Buff
    {
        public string BuffId;
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;
        public BuffCategory Category;
        public float Duration; // 0 = permanent
        public float RemainingTime;
        public int StackCount = 1;
        public int MaxStacks = 1;
        public bool IsDebuff = false;
        public StatModifier[] StatModifiers;
        public SpecialEffectEntry[] SpecialEffects;
        public BuffFlags Flags;

        public bool IsExpired => Duration > 0 && RemainingTime <= 0;
        public bool CanStack => MaxStacks > 1;
        public float NormalizedTime => Duration > 0 ? 1f - (RemainingTime / Duration) : 0f;

        public Buff Clone()
        {
            var clone = (Buff)MemberwiseClone();
            if (StatModifiers != null)
            {
                clone.StatModifiers = new StatModifier[StatModifiers.Length];
                Array.Copy(StatModifiers, clone.StatModifiers, StatModifiers.Length);
            }
            if (SpecialEffects != null)
            {
                clone.SpecialEffects = new SpecialEffectEntry[SpecialEffects.Length];
                Array.Copy(SpecialEffects, clone.SpecialEffects, SpecialEffects.Length);
            }
            return clone;
        }
    }

    /// <summary>
    /// Buff categories for UI grouping and filtering.
    /// </summary>
    public enum BuffCategory
    {
        None = 0,
        Offensive = 1,    // Damage, crit, attack speed
        Defensive = 2,    // Defense, damage reduction, shield
        Utility = 3,      // Move speed, cooldown reduction, range
        Economy = 4,      // Gold gain, exp gain, drop rate
        Elemental = 5,    // Fire, ice, lightning, etc.
        Special = 6,      // Unique effects
    }

    /// <summary>
    /// Buff flags for special behavior.
    /// </summary>
    [Flags]
    public enum BuffFlags
    {
        None = 0,
        PersistThroughDeath = 1,      // Doesn't clear on death
        Dispellable = 2,              // Can be removed by dispel
        StacksRefreshDuration = 4,    // Refreshing stacks resets duration
        StacksIncreaseDuration = 8,   // Each stack adds duration
        Hidden = 16,                  // Not shown in UI
        Aura = 32,                    // Affects nearby allies/enemies
        CannotBePurged = 64,          // Immune to purge effects
    }

    /// <summary>
    /// Buff manager - handles active buffs on player.
    /// </summary>
    public sealed class BuffManager : MonoBehaviour
    {
        #region Singleton
        private static BuffManager _instance;
        public static BuffManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

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
        #endregion

        #region Events
        public event Action<Buff> OnBuffAdded;
        public event Action<Buff> OnBuffRemoved;
        public event Action<Buff, int> OnBuffStackChanged; // buff, newStackCount
        public event Action<Buff> OnBuffRefreshed;
        #endregion

        #region Fields
        private readonly Dictionary<string, Buff> _activeBuffs = new();
        private readonly List<Buff> _buffsToRemove = new();
        #endregion

        #region Public API
        /// <summary>
        /// Adds or refreshes a buff.
        /// </summary>
        public Buff AddBuff(Buff buff)
        {
            if (buff == null || string.IsNullOrEmpty(buff.BuffId)) return null;

            if (_activeBuffs.TryGetValue(buff.BuffId, out var existing))
            {
                // Stack or refresh
                if (existing.CanStack && existing.StackCount < existing.MaxStacks)
                {
                    existing.StackCount++;
                    existing.RemainingTime = Mathf.Max(existing.RemainingTime, buff.Duration);
                    OnBuffStackChanged?.Invoke(existing, existing.StackCount);
                    return existing;
                }
                else
                {
                    // Refresh duration
                    existing.RemainingTime = buff.Duration;
                    OnBuffRefreshed?.Invoke(existing);
                    return existing;
                }
            }

            // New buff
            buff.RemainingTime = buff.Duration;
            _activeBuffs[buff.BuffId] = buff;

            // Apply stat modifiers
            ApplyBuffModifiers(buff, true);

            OnBuffAdded?.Invoke(buff);
            return buff;
        }

        /// <summary>
        /// Removes a buff by ID.
        /// </summary>
        public bool RemoveBuff(string buffId)
        {
            if (!_activeBuffs.TryGetValue(buffId, out var buff)) return false;

            // Remove stat modifiers
            ApplyBuffModifiers(buff, false);

            _activeBuffs.Remove(buffId);
            OnBuffRemoved?.Invoke(buff);
            return true;
        }

        /// <summary>
        /// Removes all buffs matching a category.
        /// </summary>
        public int RemoveBuffsByCategory(BuffCategory category)
        {
            _buffsToRemove.Clear();
            foreach (var buff in _activeBuffs.Values)
            {
                if (buff.Category == category)
                    _buffsToRemove.Add(buff);
            }

            foreach (var buff in _buffsToRemove)
            {
                RemoveBuff(buff.BuffId);
            }

            return _buffsToRemove.Count;
        }

        /// <summary>
        /// Removes all debuffs.
        /// </summary>
        public int RemoveAllDebuffs()
        {
            _buffsToRemove.Clear();
            foreach (var buff in _activeBuffs.Values)
            {
                if (buff.IsDebuff)
                    _buffsToRemove.Add(buff);
            }

            foreach (var buff in _buffsToRemove)
            {
                RemoveBuff(buff.BuffId);
            }

            return _buffsToRemove.Count;
        }

        /// <summary>
        /// Removes all buffs (except those with PersistThroughDeath flag).
        /// </summary>
        public void ClearAllBuffs(bool allowPersist = true)
        {
            _buffsToRemove.Clear();
            foreach (var buff in _activeBuffs.Values)
            {
                if (!allowPersist || !buff.Flags.HasFlag(BuffFlags.PersistThroughDeath))
                    _buffsToRemove.Add(buff);
            }

            foreach (var buff in _buffsToRemove)
            {
                RemoveBuff(buff.BuffId);
            }
        }

        /// <summary>
        /// Gets a buff by ID.
        /// </summary>
        public Buff GetBuff(string buffId) => _activeBuffs.TryGetValue(buffId, out var buff) ? buff : null;

        /// <summary>
        /// Checks if a buff is active.
        /// </summary>
        public bool HasBuff(string buffId) => _activeBuffs.ContainsKey(buffId);

        /// <summary>
        /// Gets all active buffs.
        /// </summary>
        public IReadOnlyList<Buff> GetAllBuffs() => _activeBuffs.Values.ToList();

        /// <summary>
        /// Gets all active buffs of a category.
        /// </summary>
        public IReadOnlyList<Buff> GetBuffsByCategory(BuffCategory category)
        {
            return _activeBuffs.Values.Where(b => b.Category == category).ToList();
        }

        /// <summary>
        /// Gets all active debuffs.
        /// </summary>
        public IReadOnlyList<Buff> GetAllDebuffs()
        {
            return _activeBuffs.Values.Where(b => b.IsDebuff).ToList();
        }

        /// <summary>
        /// Updates all buff timers (call from Update).
        /// </summary>
        public void UpdateBuffs(float deltaTime)
        {
            _buffsToRemove.Clear();

            foreach (var buff in _activeBuffs.Values)
            {
                if (buff.Duration > 0)
                {
                    buff.RemainingTime -= deltaTime;
                    if (buff.RemainingTime <= 0)
                    {
                        _buffsToRemove.Add(buff);
                    }
                }
            }

            foreach (var buff in _buffsToRemove)
            {
                RemoveBuff(buff.BuffId);
            }
        }

        /// <summary>
        /// Gets combined stat modifiers from all active buffs.
        /// </summary>
        public Dictionary<SecondaryStat, float> GetCombinedStatModifiers()
        {
            var modifiers = new Dictionary<SecondaryStat, float>();

            foreach (var buff in _activeBuffs.Values)
            {
                if (buff.StatModifiers != null)
                {
                    foreach (var mod in buff.StatModifiers)
                    {
                        float value = mod.Value * buff.StackCount;
                        SecondaryStat mainStat = mod.UsesSecondaryStat ? mod.SecondaryStat : SecondaryStatExtensions.SkillTypeToSecondaryStat(mod.Stat);
                        if (mainStat != SecondaryStat.None)
                        {
                            if (modifiers.ContainsKey(mainStat))
                                modifiers[mainStat] += value;
                            else
                                modifiers[mainStat] = value;
                        }
                    }
                }
            }

            return modifiers;
        }
        #endregion

        #region Internal
        private void ApplyBuffModifiers(Buff buff, bool add)
        {
            if (buff.StatModifiers == null) return;

            foreach (var mod in buff.StatModifiers)
            {
                float value = mod.Value * buff.StackCount;
                var modifier = new StatModifier
                {
                    Id = $"Buff:{buff.BuffId}:{mod.Stat}",
                    Source = ModifierSource.Buff,
                    Stat = mod.Stat,
                    Mode = mod.Mode,
                    Value = value,
                    Permanent = false,
                    ExpireUtc = buff.Duration > 0 ? DateTime.UtcNow.AddSeconds(buff.RemainingTime) : null
                };

                if (add) ModifierManager.Instance?.AddModifier(modifier);
                else ModifierManager.Instance?.RemoveModifier(modifier.Id);
            }
        }
        #endregion
    }
}