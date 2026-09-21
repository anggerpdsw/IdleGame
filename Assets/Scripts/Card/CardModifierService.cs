using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Stats;
using PlayerClass = IdleDefenseSurvival.Player.Player;
using UnityEngine;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Central service for applying card modifiers to player stats and handling card effects.
    /// Uses consistent modifier IDs: "Card:{cardId}"
    /// All card effects are stored in a dictionary keyed by CardEffectType for scalability.
    /// </summary>
    public static class CardModifierService
    {
        public static event Action OnModifierChanged;

        /// <summary>
        /// Dictionary storing the current active value for each card effect type.
        /// Key: CardEffectType, Value: Calculated effect value (e.g., slow percentage for FrostAura).
        /// </summary>
        private static readonly Dictionary<CardEffectType, CardEffectValue> _effectValues = new();

        /// <summary>
        /// Tracks which card IDs have stat modifiers applied in ModifierManager for proper cleanup.
        /// </summary>
        private static readonly HashSet<string> _cardsWithStatModifiers = new();

        // Berserker dynamic modifier
        private const string BerserkerModifierId = "Card:BerserkerDynamic";
        private static float _berserkerMaxPercent = 0f;
        private static bool _berserkerSubscribed = false;

        // HealOnKill subscription
        private static bool _healOnKillSubscribed = false;

        // Angel (Immortal) cooldown tracking
        private static float _angelCooldownRemaining = 0f;
        private static float _angelCooldownMax = 0f;
        private static int _angelImmunityWavesRemaining = 0;

        /// <summary>
        /// Clears all existing card modifiers and re-applies modifiers from currently equipped cards.
        /// Called when cards are equipped/unequipped/upgraded or on game load.
        /// </summary>
        public static void Refresh()
        {
            // Clear all existing stat modifiers from ModifierManager
            foreach (string cardId in _cardsWithStatModifiers)
            {
                string modifierId = $"Card:{cardId}";
                ModifierManager.Instance.RemoveModifier(modifierId);
            }
            _cardsWithStatModifiers.Clear();
            _effectValues.Clear();

            // Reset Berserker
            _berserkerMaxPercent = 0f;
            ModifierManager.Instance.RemoveModifier(BerserkerModifierId);

            // Track visual effects
            bool hasHealOnKill = false;

            // Re-apply modifiers from currently equipped cards
            var equipped = CardEquipmentService.Instance.EquippedCards;
            foreach (string cardId in equipped)
            {
                if (string.IsNullOrEmpty(cardId)) continue;

                var cardData = CardDatabase.Instance.GetCard(cardId);
                if (cardData == null) continue;

                var inventory = CardInventory.Instance.GetOwnedCard(cardId);
                int level = inventory?.Level ?? 1;

                float value = cardData.CalculateValue(level);
                string modifierId = $"Card:{cardId}";

                // Handle card effects (non-stat effects like auras)
                if (!string.IsNullOrEmpty(cardData.EffectType))
                {
                    var effectType = ParseEffectType(cardData.EffectType);
                    if (effectType != CardEffectType.None) {
                        _effectValues[effectType] = new CardEffectValue
                        {
                            Mode = ParseModifierMode(cardData.Mode),
                            Value = value
                        };

                        // Berserker: store max cap
                        if (effectType == CardEffectType.Berserker)
                            _berserkerMaxPercent = value;

                        // HealOnKill: ensure subscription and track visual
                        if (effectType == CardEffectType.HealOnKill)
                        {
                            EnsureHealOnKillSubscription();
                            hasHealOnKill = true;
                        }

                        // Immortal (Angel): store max cooldown
                        if (effectType == CardEffectType.Immortal)
                            _angelCooldownMax = value;
                    }
                }

                // Handle stat modifiers (traditional stat bonuses)
                if (!string.IsNullOrEmpty(cardData.SkillType))
                {
                    var statType = ParseSkillType(cardData.SkillType);
                    if (statType != SkillType.None)
                    {
                        var modifier = new StatModifier
                        {
                            Id = modifierId,
                            Source = ModifierSource.Card,
                            Stat = statType,
                            Mode = ParseModifierMode(cardData.Mode),
                            Value = value,
                            Permanent = true
                        };

                        ModifierManager.Instance.AddModifier(modifier);
                        _cardsWithStatModifiers.Add(cardId);
                    }
                }
            }

            // Update visual effects
            var player = PlayerClass.Instance;
            if (player != null)
            {
                player.SetVampireEffect(hasHealOnKill);
            }

            OnModifierChanged?.Invoke();
        }

        /// <summary>
        /// Update Angel cooldown timer (call from Player.Update or dedicated manager).
        /// </summary>
        public static void UpdateAngelCooldown(float deltaTime)
        {
            if (_angelCooldownRemaining > 0f)
                _angelCooldownRemaining -= deltaTime;
        }

        /// <summary>
        /// Notify wave completed - decrement Angel immunity counter.
        /// </summary>
        public static void OnWaveCompleted()
        {
            if (_angelImmunityWavesRemaining > 0)
                _angelImmunityWavesRemaining--;

            // Disable barrier visual when immunity expires
            if (_angelImmunityWavesRemaining == 0 && PlayerClass.Instance != null)
                PlayerClass.Instance.SetBarrierEffect(false);
        }

        /// <summary>
        /// Check if Angel card can trigger (cooldown ready + equipped).
        /// </summary>
        public static bool CanTriggerAngel()
        {
            if (_angelCooldownRemaining > 0f) return false;
            if (!HasEffect(CardEffectType.Immortal)) return false;
            return true;
        }

        /// <summary>
        /// Trigger Angel effect: grant immunity for 1 wave, start cooldown.
        /// Returns true if triggered successfully.
        /// </summary>
        public static bool TriggerAngel()
        {
            if (!CanTriggerAngel()) return false;

            // Get cooldown duration from card level
            float cooldownSeconds = GetEffectResult(CardEffectType.Immortal, 550f);
            _angelCooldownRemaining = cooldownSeconds;
            _angelImmunityWavesRemaining = 1;

            return true;
        }

        /// <summary>
        /// Check if player currently has Angel immunity active.
        /// </summary>
        public static bool HasAngelImmunity() => _angelImmunityWavesRemaining > 0;

        /// <summary>
        /// Get current Angel cooldown remaining (for UI).
        /// </summary>
        public static float GetAngelCooldownRemaining() => Mathf.Max(_angelCooldownRemaining, 0f);

        /// <summary>
        /// Get max Angel cooldown (for UI fill calculation).
        /// </summary>
        public static float GetAngelMaxCooldown() => _angelCooldownMax;

        /// <summary>
        /// Ensure berserker health-change subscription is set.
        /// Call after Player instance is ready (e.g. in Player.Start).
        /// </summary>
        public static void EnsureBerserkerSubscription()
        {
            var player = PlayerClass.Instance;
            if (player == null)
            {
                Debug.LogWarning("[Berserker] Player.Instance still null");
                return;
            }

            if (_berserkerSubscribed) return;

            player.OnHealthChanged += UpdateBerserkerModifier;
            _berserkerSubscribed = true;
            UpdateBerserkerModifier(); // Init with current HP
        }

        /// <summary>
        /// Checks if a specific card effect type is currently active (has a non-zero value).
        /// </summary>
        /// <param name="effect">The card effect type to check.</param>
        /// <returns>True if the effect is active with a value > 0.</returns>
        public static bool HasEffect(CardEffectType effect)
        {
            return _effectValues.TryGetValue(effect, out var data) && data.Value > 0f;
        }
        public static bool HasAuraEffect()
        {
            return HasEffect(CardEffectType.FrostAura);
        }

        /// <summary>
        /// Gets the current value of a card effect (0 if not active).
        /// </summary>
        /// <param name="effect">The card effect type to query.</param>
        /// <returns>The effect value, or 0 if not active.</returns>
        public static float GetEffectResult(CardEffectType effect, float fallback = 0f)
        {
            if (!_effectValues.TryGetValue(effect, out var data)) return fallback;
            return data.Mode switch
            {
                ModifierMode.Percent => data.Value * 0.01f,
                ModifierMode.Flat => data.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(data.Mode), data.Mode, null)
            };
        }

        private static SkillType ParseSkillType(string skillType) =>
            Enum.TryParse<SkillType>(skillType, true, out var st) ? st : SkillType.None;

        private static CardEffectType ParseEffectType(string effectType) =>
            Enum.TryParse<CardEffectType>(effectType, true, out var et) ? et : CardEffectType.None;

        private static ModifierMode ParseModifierMode(string mode) =>
            Enum.TryParse<ModifierMode>(mode, true, out var m) ? m : ModifierMode.Percent;

        /// <summary>
        /// Recalculates Berserker bonus based on missing HP.
        /// 1% missing HP = 1% AttackDamage, capped by card level.
        /// </summary>
        private static void UpdateBerserkerModifier()
        {
            var player = PlayerClass.Instance;
            if (player == null)
            {
                Debug.LogWarning("[Berserker] UpdateBerserkerModifier: Player.Instance is null");
                return;
            }

            if (_berserkerMaxPercent <= 0f)
            {
                player.SetBerserkerEffect(false);
                return;
            }

            float maxHp = player.MaxHealth;
            float curHp = player.CurrentHealth;
            if (maxHp <= 0f) return;

            float missingPercent = (maxHp - curHp) / maxHp * 100f;
            float bonusPercent = Mathf.Min(missingPercent, _berserkerMaxPercent);

            ModifierManager.Instance.RemoveModifier(BerserkerModifierId);

            bool isActive = bonusPercent > 0f;
            if (isActive)
            {
                var mod = new StatModifier
                {
                    Id = BerserkerModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(mod);
            }

            // Visual: show when HP < 100%, hide when full
            player.SetBerserkerEffect(isActive);
        }

        /// <summary>
        /// Ensure HealOnKill subscription is set.
        /// Called when Bat Stalker card is equipped.
        /// </summary>
        private static void EnsureHealOnKillSubscription()
        {
            if (_healOnKillSubscribed) return;
            EnemyDeathHandler.OnEnemyKilled += OnEnemyKilledHandler;
            _healOnKillSubscribed = true;
        }

        /// <summary>
        /// Handle enemy death for HealOnKill effect (Bat Stalker card).
        /// Only heals when player is the kill source.
        /// </summary>
        private static void OnEnemyKilledHandler(EnemyAi enemy, string damageSource)
        {
            if (enemy == null) return;

            // Only heal on player kills
            if (damageSource != UltimateDMG.Player.ToString()) return;

            // Find equipped HealOnKill cards
            var equipped = CardEquipmentService.Instance.EquippedCards;
            foreach (string cardId in equipped)
            {
                if (string.IsNullOrEmpty(cardId)) continue;

                var cardData = CardDatabase.Instance.GetCard(cardId);
                if (cardData == null) continue;

                var effectType = ParseEffectType(cardData.EffectType);
                if (effectType != CardEffectType.HealOnKill) continue;

                var inventory = CardInventory.Instance.GetOwnedCard(cardId);
                int level = inventory?.Level ?? 1;
                float percent = cardData.CalculateValue(level);

                float healAmount = enemy.MaxHealth * (percent * 0.01f);
                PlayerClass.Instance?.Heal(healAmount);
            }
        }
    }
}
