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
        public CardRarity Id;
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
        public CardRarity CardRarity;
        
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
        public CardRarity CardRarity;

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