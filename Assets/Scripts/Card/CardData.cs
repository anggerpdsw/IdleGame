using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    [Serializable] public class CardDataContainer
    {
        public int Version;
        public List<RarityConfig> RarityConfig;
        public List<CardData> Cards;
    }

    [Serializable] public class RarityConfig
    {
        public Rarity Id;
        public float Multiplier;
    }

    [Serializable] public class CardData
    {
        public string Id;
        public string Name;
        public string Description;
		public string EffectType;  // CardEffectType: FrostAura, OnHitBurn, etc.
        public string SkillType;
        public string Mode; // "Percent" or "Flat"
        public float BaseValue;
        public float ValuePerLevel;
        public Rarity CardRarity;

        // NEW: Structured parameters for complex mechanics (backward compatible)
        // JSON-serialized dictionary for mechanic-specific values
        // Example: {"Duration": 5, "MaxStacks": 15, "Threshold": 50}
        public Dictionary<string, float> Parameters;

        // NEW: Multi-effect support for cards affecting multiple stats
        // Example: Apocalypse Engine boosts ATK, AS, and CRIT DMG
        public List<CardEffectDefinition> Effects;

        public float CalculateValue(int level) => BaseValue + ValuePerLevel * (level - 1);

        /// <summary>
        /// Helper to get parameter value with fallback to default.
        /// Returns defaultValue if Parameters is null or key not found.
        /// </summary>
        public float GetParameter(string key, float defaultValue = 0f)
        {
            if (Parameters == null) return defaultValue;
            return Parameters.TryGetValue(key, out var val) ? val : defaultValue;
        }

        /// <summary>
        /// Checks if this card uses the new Effects system.
        /// </summary>
        public bool HasMultipleEffects => Effects != null && Effects.Count > 0;
    }

    /// <summary>
    /// Individual effect definition for multi-effect cards.
    /// Used when a single card modifies multiple stats.
    /// </summary>
    [Serializable] public class CardEffectDefinition
    {
        public string Target;  // SkillType or stat name as string
        public string Mode;    // "Percent" or "Flat"
        public float BaseValue;
        public float ValuePerLevel;

        public float CalculateValue(int level) => BaseValue + ValuePerLevel * (level - 1);
    }

    [Serializable] public struct CardEffectValue
    {
        public ModifierMode Mode;
        public float Value;
    }

    [Serializable] public class CardInventoryData
    {
        public Dictionary<string, OwnedCardData> ownedCards = new();
        public List<string> equippedCards = new();
        public int rollsSinceEpic = 0;
        public int rollsSinceLegendary = 0;
        public int rollsSinceMythic = 0;
    }

    [Serializable] public class CardReward
    {
        // Unique identifier for this card definition
        public string CardId;

        // CardRarity of the rolled card
        public Rarity CardRarity;

        // Number of copies rolled (e.g., from a multi-roll)
        public int Quantity = 1;

        // Is this a duplicate of an already owned card?
        public bool IsDuplicate;

        // Is this a brand new card (not previously owned)?
        public bool IsNewCard;

        // Was this card guaranteed by the pity system?
        public bool IsPityGuaranteed;

        // Calculated display quantity considering duplicates
        public int DisplayQuantity => IsDuplicate ? Quantity : 1;
    }

    [Serializable] public class OwnedCardData
    {
        public string CardId;
        public int Level = 1;
        public int DuplicateCount = 0;
    }

    [Serializable]
    public struct CardRollResult
    {
        // The rolled cards from this roll operation
        public List<CardReward> Cards;

        // How many gems were spent for this roll
        public long GemSpent;

        // How many gems were refunded (for max-level cards)
        public long GemRefunded;

        // Indicator that this roll contained at least one special/new card or lucky outcome
        public bool IsLucky;

        // Indicator that a new card was acquired (not a duplicate)
        public bool HasNewCard;

        // Pity progress for UI display
        public int RollsSinceEpic;
        public int RollsSinceLegendary;
        public int RollsSinceMythic;
    }

    
}