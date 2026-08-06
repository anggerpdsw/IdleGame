using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.Modifiers
{
    /// <summary>
    /// Central registry for all equipment effects.
    /// Uses Strategy Pattern - effects are registered by type and created on demand.
    /// Extensible without modifying core code - just register new IEquipmentEffect implementations.
    /// </summary>
    public sealed class EffectRegistry : IEffectRegistry
    {
        private static EffectRegistry _instance;
        public static EffectRegistry Instance
        {
            get
            {
                if (_instance == null) _instance = new EffectRegistry();
                return _instance;
            }
        }

        // ============ Internal Storage ============
        private readonly Dictionary<SpecialEffectType, IEffectFactory> _factories = new();
        private readonly Dictionary<SpecialEffectType, IEquipmentEffect> _prototypes = new();

        // ============ Registration ============
        public void RegisterEffect<T>() where T : IEquipmentEffect, new()
        {
            var effect = new T();
            RegisterEffect(effect.EffectType, new GenericEffectFactory<T>());
        }

        public void RegisterEffect(SpecialEffectType type, IEffectFactory factory)
        {
            if (type == SpecialEffectType.None) return;
            if (factory == null)
            {
                Debug.LogError($"[EffectRegistry] Cannot register null factory for {type}");
                return;
            }

            _factories[type] = factory;
            _prototypes[type] = factory.Create();
        }

        public void UnregisterEffect(SpecialEffectType type)
        {
            _factories.Remove(type);
            _prototypes.Remove(type);
        }

        public bool IsRegistered(SpecialEffectType type) => _factories.ContainsKey(type);

        // ============ Factory ============
        public IEquipmentEffect CreateEffect(SpecialEffectType type)
        {
            if (!_factories.TryGetValue(type, out var factory))
            {
                Debug.LogWarning($"[EffectRegistry] No factory registered for effect type: {type}");
                return null;
            }
            return factory.Create();
        }

        public IEquipmentEffect CreateEffect(SpecialEffectType type, SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot)
        {
            if (!_factories.TryGetValue(type, out var factory))
            {
                Debug.LogWarning($"[EffectRegistry] No factory registered for effect type: {type}");
                return null;
            }
            return factory.Create(config, item, slot);
        }

        // ============ Lookup ============
        public IEquipmentEffect GetEffect(SpecialEffectType type) => _prototypes.TryGetValue(type, out var effect) ? effect : null;

        public IReadOnlyList<SpecialEffectType> GetRegisteredEffects() => _factories.Keys.ToList();

        public IReadOnlyList<SpecialEffectType> GetEffectsByCategory(EffectCategory category)
        {
            return _prototypes.Values
                .Where(e => e.Category == category)
                .Select(e => e.EffectType)
                .ToList();
        }

        // ============ Validation ============
        public void ValidateAllEffects()
        {
            foreach (var kvp in _prototypes)
            {
                try
                {
                    var effect = kvp.Value;
                    if (effect.EffectType == SpecialEffectType.None)
                    {
                        Debug.LogError($"[EffectRegistry] Effect {effect.GetType().Name} has invalid EffectType.None");
                    }
                    if (string.IsNullOrEmpty(effect.DisplayName))
                    {
                        Debug.LogWarning($"[EffectRegistry] Effect {effect.EffectType} has empty DisplayName");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EffectRegistry] Validation failed for {kvp.Key}: {e.Message}");
                }
            }
            Debug.Log($"[EffectRegistry] Validated {_prototypes.Count} effects");
        }

        // ============ Internal ============
        private class GenericEffectFactory<T> : IEffectFactory where T : IEquipmentEffect, new()
        {
            public SpecialEffectType EffectType => new T().EffectType;

            public IEquipmentEffect Create() => new T();

            public IEquipmentEffect Create(SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot)
            {
                var effect = new T();
                effect.Initialize(config, item, slot);
                return effect;
            }
        }
    }

    /// <summary>
    /// Static factory class for creating effects with common patterns.
    /// </summary>
    public static class EffectFactory
    {
        /// <summary>
        /// Creates an effect from registry and initializes it.
        /// </summary>
        public static IEquipmentEffect Create(SpecialEffectType type, SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot)
        {
            return EffectRegistry.Instance.CreateEffect(type, config, item, slot);
        }

        /// <summary>
        /// Creates multiple effects from a list of entries.
        /// </summary>
        public static IEquipmentEffect[] CreateAll(SpecialEffectEntry[] entries, InventoryItem item, EquipmentSlot slot)
        {
            if (entries == null || entries.Length == 0) return Array.Empty<IEquipmentEffect>();

            var effects = new List<IEquipmentEffect>(entries.Length);
            foreach (var entry in entries)
            {
                if (!entry.IsActive) continue;
                var effect = Create(entry.EffectType, entry, item, slot);
                if (effect != null) effects.Add(effect);
            }
            return effects.ToArray();
        }

        /// <summary>
        /// Gets all registered effect types for UI dropdowns.
        /// </summary>
        public static SpecialEffectType[] GetAllRegisteredTypes() => EffectRegistry.Instance.GetRegisteredEffects().ToArray();

        /// <summary>
        /// Gets effect types by category for UI grouping.
        /// </summary>
        public static SpecialEffectType[] GetTypesByCategory(EffectCategory category) => EffectRegistry.Instance.GetEffectsByCategory(category).ToArray();
    }
}