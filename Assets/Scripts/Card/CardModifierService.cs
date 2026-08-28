using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Stats;

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

            OnModifierChanged?.Invoke();
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
    }
}