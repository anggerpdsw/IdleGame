using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Modifier;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Global stat modifier manager.
    /// Cached for O(1) stat lookup.
    ///
    /// Supports:
    /// - Flat modifiers
    /// - Percent modifiers
    /// - Permanent modifiers
    /// - Temporary modifiers (ExpireUtc)
    /// - Runtime add/remove
    ///
    /// Final Formula:
    /// (Base + Flat) * (1 + Percent / 100)
    /// </summary>
    public sealed class ModifierManager : MonoBehaviour
    {
        #region Singleton

        public static ModifierManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        public int ModifierCount => _modifierLookup.Count;
        public int SourceCount => _sources.Count;

        #region Data

        /// <summary>
        /// Source -> (ModifierId -> Modifier)
        /// Primary storage.
        /// </summary>
        private readonly Dictionary<ModifierSource, Dictionary<string, StatModifier>> _sources = new();

        /// <summary>
        /// Fast lookup by modifier id.
        /// O(1)
        /// </summary>
        private readonly Dictionary<string, StatModifier> _modifierLookup = new();

        /// <summary>
        /// Cached value per stat.
        /// </summary>
        private readonly Dictionary<SkillType, CachedModifier> _cache = new();

        /// <summary>
        /// Dirty stat cache.
        /// Only rebuild changed stat.
        /// </summary>
        private readonly HashSet<SkillType> _dirtyStats = new();
        private readonly Dictionary<SkillType, Dictionary<string, StatModifier>> 
            _statIndex = new();
        private static readonly SkillType[] AllStats =
            (SkillType[])Enum.GetValues(typeof(SkillType));

        /// <summary>
        /// Temporary buffer.
        /// </summary>
        private readonly List<string> _removeBuffer = new(8);

        public event Action OnModifierChanged;

        private readonly struct CachedModifier
        {
            public readonly float Flat;
            public readonly float Percent;

            public CachedModifier(float flat, float percent)
            {
                Flat = flat;
                Percent = percent;
            }
        }

        #endregion
        
        #region Public API

        /// <summary>
        /// Add or replace a single modifier.
        /// </summary>
        public void AddModifier(StatModifier modifier) => AddModifierInternal(modifier, true);
        /// <summary>
        /// Add or replace a multiplier modifiers.
        /// </summary>
        public void AddStatModifiers(ModifierSource source, string idPrefix,
            params (SkillType stat, ModifierMode mode, float value)[] modifiers)
        {
            foreach (var (stat, mode, value) in modifiers)
            {
                AddModifierInternal(new StatModifier
                {
                    Id = $"{idPrefix}_{stat}",
                    Source = source,
                    Stat = stat,
                    Mode = mode,
                    Value = value
                }, false);
            }

            Notify();
        }

        /// <summary>
        /// Internal add implementation.
        /// Every add operation goes through this method.
        /// </summary>
        private void AddModifierInternal(StatModifier modifier, bool notify)
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.Id)) return;

            // Replace existing modifier
            RemoveModifierInternal(modifier.Id, false);

            // Add to source index
            if (!_sources.TryGetValue(modifier.Source, out var source))
            {
                source = new Dictionary<string, StatModifier>(8);
                _sources.Add(modifier.Source, source);
            }

            source[modifier.Id] = modifier;

            // Add to stat index
            if (!_statIndex.TryGetValue(modifier.Stat, out var statModifiers))
            {
                statModifiers = new Dictionary<string, StatModifier>(8);
                _statIndex.Add(modifier.Stat, statModifiers);
            }

            statModifiers[modifier.Id] = modifier;

            // Add to global lookup
            _modifierLookup[modifier.Id] = modifier;

            // Mark cache dirty
            MarkDirty(modifier.Stat);
            if (notify) Notify();
        }

        /// <summary>
        /// Remove one modifier.
        /// </summary>
        public bool RemoveModifier(string id) => RemoveModifierInternal(id, true);
        /// <summary>
        /// Remove multiple modifiers.
        /// </summary>
        public void RemoveStatModifiers(string idPrefix, params SkillType[] stats)
        {
            foreach (var stat in stats)
                RemoveModifierInternal($"{idPrefix}_{stat}", false);
                
            Notify();
        }

        /// <summary>
        /// Update existing modifier.
        /// If modifier does not exist it will be added.
        /// </summary>
        public void UpdateModifier(StatModifier modifier)
        {
            if (modifier == null) return;
            RemoveModifierInternal(modifier.Id, false);
            AddModifierInternal(modifier, false);

            OnModifierChanged?.Invoke();
        }

        /// <summary>
        /// Remove every modifier belonging to a source.
        /// Example:
        /// RemoveSource(ModifierSource.Skin)
        /// </summary>
        public void RemoveSource(ModifierSource source)
        {
            if (!_sources.TryGetValue(source, out var modifiers)) return;

            _removeBuffer.Clear();

            foreach (var id in modifiers.Keys)
                _removeBuffer.Add(id);

            for (int i = 0; i < _removeBuffer.Count; i++)
                RemoveModifierInternal(_removeBuffer[i], false);

            OnModifierChanged?.Invoke();
        }

        /// <summary>
        /// Internal remove implementation.
        /// Every remove operation goes through this method.
        /// </summary>
        private bool RemoveModifierInternal(string id, bool notify)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (!_modifierLookup.TryGetValue(id, out var modifier)) return false;

            // Remove from source index
            if (_sources.TryGetValue(modifier.Source, out var source))
            {
                source.Remove(id);
                if (source.Count == 0) _sources.Remove(modifier.Source);
            }

            // Remove from stat index
            if (_statIndex.TryGetValue(modifier.Stat, out var statModifiers))
            {
                statModifiers.Remove(id);

                if (statModifiers.Count == 0)
                {
                    _statIndex.Remove(modifier.Stat);
                    _cache.Remove(modifier.Stat);
                }
            }

            // Remove global lookup
            _modifierLookup.Remove(id);

            // Remove cache if no modifier left on this stat
            if (!_statIndex.ContainsKey(modifier.Stat)) _cache.Remove(modifier.Stat);

            // Mark stat dirty
            MarkDirty(modifier.Stat);
            if (notify) Notify();

            return true;
        }

        /// <summary>
        /// Replace all modifiers belonging to one source.
        /// Very useful for Equipment, Skin, Pet.
        /// </summary>
        public void SetSource(ModifierSource source, IReadOnlyCollection<StatModifier> modifiers)
        {
            RemoveSource(source);

            if (modifiers == null) return;

            foreach (var modifier in modifiers)
            {
                modifier.Source = source;
                AddModifierInternal(modifier, false);
            }

            OnModifierChanged?.Invoke();
        }

        /// <summary>
        /// Remove all modifiers.
        /// </summary>
        public void ClearAll()
        {
            _sources.Clear();
            _modifierLookup.Clear();
            _statIndex.Clear();
            _cache.Clear();
            _dirtyStats.Clear();
            OnModifierChanged?.Invoke();
        }

        /// <summary>
        /// Returns true if source has modifiers.
        /// </summary>
        public bool HasSource(ModifierSource source) => _sources.ContainsKey(source);
                
        public void CleanupExpired()
        {
            if (_modifierLookup.Count == 0) return;
            _removeBuffer.Clear();

            DateTime now = DateTime.UtcNow;
            foreach (var pair in _modifierLookup)
            {
                var modifier = pair.Value;
                if (modifier.Permanent || modifier.ExpireUtc is null) continue;
                if (modifier.ExpireUtc.Value <= now) _removeBuffer.Add(pair.Key);
            }

            if (_removeBuffer.Count == 0) return;

            for (int i = 0; i < _removeBuffer.Count; i++)
                RemoveModifierInternal(_removeBuffer[i], false);

            OnModifierChanged?.Invoke();
        }

        public bool TryGetModifier(string id, out StatModifier modifier)
        {
            return _modifierLookup.TryGetValue(id, out modifier);
        }

        public IReadOnlyCollection<StatModifier> GetSourceModifiers(ModifierSource source)
        {
            if (!_sources.TryGetValue(source, out var sourceModifiers))
                return Array.Empty<StatModifier>();

            return sourceModifiers.Values;
        }

        public IReadOnlyCollection<StatModifier> GetStatModifiers(SkillType stat)
        {
            if (!_statIndex.TryGetValue(stat, out var dict))
                return Array.Empty<StatModifier>();

            return dict.Values;
        }

        #endregion

        private void MarkDirty(SkillType stat) => _dirtyStats.Add(stat);
        private void Notify() => OnModifierChanged?.Invoke();

        public bool HasModifier(string id) => _modifierLookup.ContainsKey(id);
        public bool IsDirty(SkillType stat) => _dirtyStats.Contains(stat);

        #region Cache

        /// <summary>
        /// Returns final stat.
        /// O(1) after cache.
        /// </summary>
        public float ApplyModifiers(SkillType stat, float baseValue)
        {
            if (_dirtyStats.Remove(stat)) RebuildCache(stat);
            if (!_cache.TryGetValue(stat, out var cache)) return baseValue;

            return ModifierCalculator.Calculate(baseValue, cache.Flat, cache.Percent);
        }

        /// <summary>
        /// Backward compatibility.
        /// </summary>
        public float Calculate(SkillType stat, float baseValue) => ApplyModifiers(stat, baseValue);

        /// <summary>
        /// Rebuild one stat cache only.
        /// </summary>
        private void RebuildCache(SkillType stat)
        {
            float flat = 0f;
            float percent = 0f;

            if (!_statIndex.TryGetValue(stat, out var modifiers))
            {
                _cache.Remove(stat);
                return;
            }

            DateTime now = DateTime.UtcNow;

            foreach (var modifier in modifiers.Values)
            {
                if (!modifier.Permanent &&
                    modifier.ExpireUtc.HasValue &&
                    modifier.ExpireUtc.Value <= now)
                    continue;

                switch(modifier.Mode)
                {
                    case ModifierMode.Flat: flat += modifier.Value; break;
                    case ModifierMode.Percent: percent += modifier.Value; break;
                }
            }

            _cache[stat] = new CachedModifier(flat, percent);
        }

        /// <summary>
        /// Force rebuild every dirty stat.
        /// Usually called after loading save.
        /// </summary>
        public void RebuildAllDirty()
        {
            if (_dirtyStats.Count == 0) return;

            SkillType[] dirty = new SkillType[_dirtyStats.Count];
            _dirtyStats.CopyTo(dirty);

            for (int i = 0; i < dirty.Length; i++)
                RebuildCache(dirty[i]);
        }

        /// <summary>
        /// Force rebuild every stat.
        /// Rarely used.
        /// </summary>
        public void RebuildAll()
        {
            _cache.Clear();
            _dirtyStats.Clear();

            foreach (var stat in AllStats)
                _dirtyStats.Add(stat);

            RebuildAllDirty();
        }

        #endregion

    }
}