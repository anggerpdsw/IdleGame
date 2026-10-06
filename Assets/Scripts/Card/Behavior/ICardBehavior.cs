using System;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior
{
    /// <summary>
    /// Runtime state for an equipped card instance.
    /// Separated from static CardData definition.
    /// </summary>
    [Serializable]
    public sealed class CardRuntimeState
    {
        public string CardId { get; }
        public int Level { get; private set; }
        public int StackCount { get; set; }
        public float RemainingDuration { get; set; }
        public float CooldownRemaining { get; set; }
        public bool IsActive { get; set; }
        public CardData Definition { get; }

        // Computed value at current level (for stat modifiers)
        public float CurrentValue { get; private set; }

        public CardRuntimeState(string cardId, int level, CardData definition)
        {
            CardId = cardId;
            Level = level;
            Definition = definition;
            IsActive = true;
            RecalculateValue();
        }

        public void SetLevel(int newLevel)
        {
            Level = newLevel;
            RecalculateValue();
        }

        private void RecalculateValue()
        {
            if (Definition != null)
            {
                CurrentValue = Definition.CalculateValue(Level);
            }
        }
    }

    /// <summary>
    /// Interface for all card behavior implementations.
    /// Behaviors encapsulate the runtime logic for special card effects beyond simple stat modifiers.
    /// </summary>
    public interface ICardBehavior : IDisposable
    {
        /// <summary>
        /// Gets the card ID this behavior responds to
        /// </summary>
        string CardId { get; }

        /// <summary>
        /// Gets the effect type this behavior handles
        /// </summary>
        CardEffectType EffectType { get; }

        /// <summary>
        /// Called when the card is equipped.
        /// </summary>
        void OnEquip(CardRuntimeState state);

        /// <summary>
        /// Called when the card is unequipped.
        /// </summary>
        void OnUnequip(CardRuntimeState state);

        /// <summary>
        /// Called when the card is upgraded (level changed).
        /// </summary>
        void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel);

        /// <summary>
        /// Called when a wave starts.
        /// </summary>
        void OnWaveStart(int waveNumber);

        /// <summary>
        /// Called when a wave completes.
        /// </summary>
        void OnWaveComplete(int waveNumber);

        /// <summary>
        /// Called when the player takes damage.
        /// </summary>
        void OnPlayerDamaged(float damage, float currentHp, float maxHp);

        /// <summary>
        /// Called when the player heals.
        /// </summary>
        void OnPlayerHealed(float healAmount, float currentHp, float maxHp);

        /// <summary>
        /// Called when an enemy is killed.
        /// </summary>
        void OnEnemyKilled(int count, string enemyType, string damageSource);

        void OnEnemyKilled(EnemyAi enemy, string damageSource);

        void OnPlayerLifeSteal(float healAmount, float currentHp, float maxHp);

        /// <summary>
        /// Called when player attacks.
        /// </summary>
        void OnPlayerAttack();

        /// <summary>
        /// Called when the player lands a critical hit.
        /// </summary>
        void OnCriticalHit(bool isCriticalHit, float damage, Vector2 position);

        /// <summary>
        /// Called every frame for time-based effects.
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Get the current computed value for this behavior (for stat modifiers)
        /// </summary>
        float GetCurrentValue();

        /// <summary>
        /// Events this behavior subscribes to. Override in concrete behaviors.
        /// </summary>
        CardEventType[] SubscribedEvents { get; }
    }

    /// <summary>
    /// Base implementation providing common functionality for card behaviors.
    /// </summary>
    public abstract class CardBehaviorBase : ICardBehavior
    {
        public string CardId { get; protected set; }
        public abstract CardEffectType EffectType { get; }

        protected CardRuntimeState _runtimeState;
        protected CardData _definition;
        protected int _level;

        public virtual void OnEquip(CardRuntimeState state)
        {
            _runtimeState = state;
            _definition = state.Definition;
            _level = state.Level;
            CardId = state.CardId;
        }

        public virtual void OnUnequip(CardRuntimeState state) { }

        public virtual void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            _level = newLevel;
        }

        public virtual void OnWaveStart(int waveNumber) { }
        public virtual void OnWaveComplete(int waveNumber) { }
        public virtual void OnPlayerDamaged(float damage, float currentHp, float maxHp) { }
        public virtual void OnPlayerHealed(float healAmount, float currentHp, float maxHp) { }
        public virtual void OnEnemyKilled(int count, string enemyType, string damageSource) { }
        public virtual void OnEnemyKilled(EnemyAi enemy, string damageSource) { }
        public virtual void OnPlayerLifeSteal(float healAmount, float currentHp, float maxHp) { }
        public virtual void OnPlayerAttack() { }

        public virtual void OnCriticalHit(bool isCriticalHit, float damage, Vector2 position) { }
        public virtual void Update(float deltaTime) { }

        public virtual float GetCurrentValue() => _runtimeState?.CurrentValue ?? 0f;

        public virtual CardEventType[] SubscribedEvents => Array.Empty<CardEventType>();

        public virtual void Dispose() { }

        protected float GetParameter(string key, float defaultValue = 0f)
        {
            return _definition?.GetParameter(key, defaultValue) ?? defaultValue;
        }
    }

    /// <summary>
    /// Default behavior for stat-only cards (Gold, Meat, FrostAura, Shield, etc.)
    /// Simply applies stat modifiers through ModifierManager.
    /// </summary>
    public sealed class DefaultCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType { get; }

        public DefaultCardBehavior(CardData data, int level, CardEffectType effectType)
        {
            EffectType = effectType;
        }

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            ApplyStatModifier();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            RemoveStatModifier();
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            RemoveStatModifier();
            ApplyStatModifier();
        }

        private void ApplyStatModifier()
        {
            if (_runtimeState == null || _definition == null) return;

            var modifierId = $"Card:{_runtimeState.CardId}";
            var mode = ParseModifierMode(_definition.Mode);
            var stat = ParseSkillType(_definition.SkillType);
            var value = _runtimeState.CurrentValue;

            if (stat != SkillType.None)
            {
                var modifier = new StatModifier
                {
                    Id = modifierId,
                    Source = ModifierSource.Card,
                    Stat = stat,
                    Mode = mode,
                    Value = value,
                    Permanent = true
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        private void RemoveStatModifier()
        {
            if (string.IsNullOrEmpty(CardId)) return;
            ModifierManager.Instance.RemoveModifier($"Card:{CardId}");
        }

        private SkillType ParseSkillType(string skillType)
            => Enum.TryParse(skillType, true, out SkillType result) ? result : SkillType.None;

        private ModifierMode ParseModifierMode(string mode)
            => Enum.TryParse(mode, true, out ModifierMode result) ? result : ModifierMode.Percent;
    }

    /// <summary>
    /// Standard card event types for behavior subscription.
    /// </summary>
    public enum CardEventType
    {
        None = 0,
        OnEnemyKilled,
        OnPlayerAttack,
        OnPlayerDamaged,
        OnPlayerHealed,
        OnLifeSteal,
        OnCriticalHit,
        OnWaveComplete,
        OnWaveStart,
        OnHealthChanged
    }
}