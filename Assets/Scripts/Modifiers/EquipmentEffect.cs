using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Modifiers
{
    /// <summary>
    /// Interface for equipment effects - Strategy Pattern for special effects.
    /// Each effect type implements this interface.
    /// </summary>
    public interface IEquipmentEffect
    {
        /// <summary>Unique effect type identifier.</summary>
        SpecialEffectType EffectType { get; }

        /// <summary>Effect category for grouping.</summary>
        EffectCategory Category { get; }

        /// <summary>Display name for UI.</summary>
        string DisplayName { get; }

        /// <summary>Description for tooltip.</summary>
        string Description { get; }

        /// <summary>Icon for UI display.</summary>
        Sprite Icon { get; }

        /// <summary>Color for UI theming.</summary>
        Color EffectColor { get; }

        /// <summary>
        /// Initializes the effect with configuration data.
        /// Called when effect is first applied to an item.
        /// </summary>
        void Initialize(SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot);

        /// <summary>
        /// Called when the effect is activated (item equipped).
        /// </summary>
        void OnActivate(EquipmentContext context);

        /// <summary>
        /// Called when the effect is deactivated (item unequipped).
        /// </summary>
        void OnDeactivate(EquipmentContext context);

        /// <summary>
        /// Called every frame while effect is active.
        /// Return true if effect should continue, false to auto-deactivate.
        /// </summary>
        bool OnUpdate(EquipmentContext context, float deltaTime);

        /// <summary>
        /// Called when a specific trigger occurs (on hit, on kill, on crit, etc.).
        /// Returns true if effect triggered and consumed its cooldown.
        /// </summary>
        bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data);

        /// <summary>
        /// Gets the current effect value for UI display.
        /// </summary>
        float GetCurrentValue(EquipmentContext context);

        /// <summary>
        /// Checks if effect can trigger (cooldown, conditions).
        /// </summary>
        bool CanTrigger(EquipmentContext context, EffectTriggerType trigger);

        /// <summary>
        /// Gets the remaining cooldown time.
        /// </summary>
        float GetRemainingCooldown(EquipmentContext context);

        /// <summary>
        /// Resets the effect state (for load/save or re-initialization).
        /// </summary>
        void Reset(EquipmentContext context);

        /// <summary>
        /// Serializes effect runtime state for saving.
        /// </summary>
        EffectRuntimeData GetRuntimeData();

        /// <summary>
        /// Deserializes effect runtime state from save.
        /// </summary>
        void LoadRuntimeData(EffectRuntimeData data);
    }

    /// <summary>
    /// Context passed to equipment effects for accessing game state.
    /// </summary>
    public class EquipmentContext
    {
        public InventoryItem Item { get; set; }
        public EquipmentSlot Slot { get; set; }
        public IEquipmentService EquipmentService { get; set; }
        public IInventoryService InventoryService { get; set; }
        public Player.Player Player { get; set; }
        public float CurrentTime { get; set; }
        public Dictionary<string, object> SharedData { get; } = new();

        // Combat data
        public DamageData LastDamageDealt { get; set; }
        public DamageData LastDamageTaken { get; set; }
        public Enemy.EnemyAi LastEnemyHit { get; set; }
        public Enemy.EnemyAi LastEnemyKilled { get; set; }

        // Stat access
        public float GetStat(MainStat stat) => PlayerStatsManager.Instance?.GetStat((SkillType)stat) ?? 0f;
        public float GetBaseStat(MainStat stat) => PlayerStatsManager.Instance?.GetBaseStat((SkillType)stat) ?? 0f;

        // Helper methods
        public bool HasSetBonus(string setId) => EquipmentService?.GetSetPieceCount(setId) > 0;
        public int GetSetPieceCount(string setId) => EquipmentService?.GetSetPieceCount(setId) ?? 0;
    }

    /// <summary>
    /// Trigger types for effect activation.
    /// </summary>
    public enum EffectTriggerType
    {
        None = 0,
        OnEquip = 1,
        OnUnequip = 2,
        OnHit = 10,
        OnKill = 11,
        OnCriticalHit = 12,
        OnCriticalKill = 13,
        OnDamageTaken = 20,
        OnKillEnemy = 21,
        OnLevelUp = 30,
        OnWaveStart = 31,
        OnWaveEnd = 32,
        OnTierComplete = 33,
        OnUltimateUsed = 40,
        OnSkillUsed = 41,
        OnItemUsed = 42,
        OnGoldGained = 50,
        OnExpGained = 51,
        OnGemGained = 52,
        OnMeatGained = 53,
        OnHealthChanged = 60,
        OnManaChanged = 61,
        OnShieldGained = 62,
        OnShieldLost = 63,
        OnEnemySpawned = 70,
        OnEnemyDeath = 71,
        OnPlayerDeath = 72,
        OnPlayerRevive = 73,
        Periodic = 100, // Every X seconds
        OnAttack = 101,
        OnProjectileHit = 102,
        OnProjectileKill = 103,
    }

    /// <summary>
    /// Data passed with triggers.
    /// </summary>
    public class TriggerData
    {
        public DamageData DamageData;
        public Enemy.EnemyAi Enemy;
        public Vector3 Position;
        public float Value;
        public Dictionary<string, object> CustomData;
    }

    /// <summary>
    /// Runtime data for effect persistence.
    /// </summary>
    [Serializable]
    public class EffectRuntimeData
    {
        public SpecialEffectType EffectType;
        public float LastTriggerTime;
        public float CooldownRemaining;
        public int TriggerCount;
        public Dictionary<string, object> CustomState;
        public bool IsActive;
    }

    /// <summary>
    /// Interface for effect registry - manages effect implementations.
    /// </summary>
    public interface IEffectRegistry
    {
        // ============ Registration ============
        void RegisterEffect<T>() where T : IEquipmentEffect, new();
        void RegisterEffect(SpecialEffectType type, IEffectFactory factory);
        void UnregisterEffect(SpecialEffectType type);
        bool IsRegistered(SpecialEffectType type);

        // ============ Factory ============
        IEquipmentEffect CreateEffect(SpecialEffectType type);
        IEquipmentEffect CreateEffect(SpecialEffectType type, SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot);

        // ============ Lookup ============
        IEquipmentEffect GetEffect(SpecialEffectType type);
        IReadOnlyList<SpecialEffectType> GetRegisteredEffects();
        IReadOnlyList<SpecialEffectType> GetEffectsByCategory(EffectCategory category);

        // ============ Validation ============
        void ValidateAllEffects();
    }

    /// <summary>
    /// Factory interface for creating effects.
    /// </summary>
    public interface IEffectFactory
    {
        SpecialEffectType EffectType { get; }
        IEquipmentEffect Create();
        IEquipmentEffect Create(SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot);
    }

    /// <summary>
    /// Base class for equipment effects - provides common functionality.
    /// </summary>
    public abstract class BaseEquipmentEffect : IEquipmentEffect
    {
        public abstract SpecialEffectType EffectType { get; }
        public virtual EffectCategory Category => EffectType.GetCategory();
        public virtual string DisplayName => EffectType.GetDisplayName();
        public virtual string Description => "";
        public virtual Sprite Icon => null;
        public virtual Color EffectColor => Color.white;

        protected SpecialEffectEntry _config;
        protected InventoryItem _item;
        protected EquipmentSlot _slot;
        protected EquipmentContext _context;

        public virtual void Initialize(SpecialEffectEntry config, InventoryItem item, EquipmentSlot slot)
        {
            _config = config;
            _item = item;
            _slot = slot;
        }

        public virtual void OnActivate(EquipmentContext context)
        {
            _context = context;
        }

        public virtual void OnDeactivate(EquipmentContext context)
        {
            _context = null;
        }

        public virtual bool OnUpdate(EquipmentContext context, float deltaTime) => true;

        public virtual bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data) => false;

        public virtual float GetCurrentValue(EquipmentContext context) => _config?.Value ?? 0f;

        public virtual bool CanTrigger(EquipmentContext context, EffectTriggerType trigger)
        {
            if (_config == null || !_config.IsActive) return false;
            if (_config.Chance < 100f && UnityEngine.Random.Range(0f, 100f) > _config.Chance) return false;
            if (_config.Cooldown > 0f)
            {
                float remaining = GetRemainingCooldown(context);
                if (remaining > 0f) return false;
            }
            return true;
        }

        public virtual float GetRemainingCooldown(EquipmentContext context)
        {
            if (_config == null || _config.Cooldown <= 0f) return 0f;
            float elapsed = context.CurrentTime - GetLastTriggerTime();
            float remaining = _config.Cooldown - elapsed;
            return Math.Max(0f, remaining);
        }

        protected virtual float GetLastTriggerTime() => 0f;
        protected virtual void SetLastTriggerTime(float time) { }

        public virtual void Reset(EquipmentContext context)
        {
            SetLastTriggerTime(0f);
        }

        public virtual EffectRuntimeData GetRuntimeData() => new()
        {
            EffectType = EffectType,
            LastTriggerTime = GetLastTriggerTime(),
            CooldownRemaining = GetRemainingCooldown(_context),
            IsActive = _config?.IsActive ?? false
        };

        public virtual void LoadRuntimeData(EffectRuntimeData data)
        {
            if (data != null)
            {
                SetLastTriggerTime(data.LastTriggerTime);
            }
        }

        protected bool CheckConditions(string[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return true;
            // TODO: Implement condition parsing/evaluation
            return true;
        }

        protected float CalculateScaledValue(float baseValue, int itemLevel, int enhanceLevel)
        {
            // Base scaling - can be overridden
            return baseValue;
        }
    }
}