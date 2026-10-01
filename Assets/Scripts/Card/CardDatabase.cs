using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Loads and caches card definitions from dataCard.json (Resources).
    /// </summary>
    public class CardDatabase : MonoBehaviour
    {
        private bool _debug = false;
        
        #region Singleton
        private static CardDatabase _instance;
        public static CardDatabase Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

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

        private readonly Dictionary<string, CardData> _cards = new();
        private readonly Dictionary<Rarity, RarityConfig> _rarities = new();
        private readonly Dictionary<Rarity, List<string>> _cardsByRarity = new();
        private bool _initialized = false;
        private float _totalRarityWeight;
        public CardProgressionConfig Progression { get; private set; }

        public void Initialize()
        {
            if (_initialized) return;
            LoadFromResources();
            _initialized = Progression != null && _cards.Count > 0 && _rarities.Count > 0;
        }

        private void LoadFromResources()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/Card/dataCard");
            if (jsonAsset == null)
            {
                Debug.LogError("[CardDatabase] dataCard.json not found in Resources/Data/");
                return;
            }

            CardDataContainer container = JsonConvert.DeserializeObject<CardDataContainer>(jsonAsset.text);
            if (container == null || container.Cards == null || container.RarityConfig == null)
            {
                Debug.LogError("[CardDatabase] Failed to parse dataCard.json");
                return;
            }

            if (!IsProgressionConfigValid(container.Progression))
            {
                Debug.LogError("[CardDatabase] dataCard.json has invalid progression configuration.");
                return;
            }
            Progression = container.Progression;

            // -----------------------------------------
            // Cards
            // -----------------------------------------
            _cards.Clear();
            foreach (CardData card in container.Cards)
            {
                if (!IsCardDefinitionValid(card) || _cards.ContainsKey(card.Id))
                {
                    Debug.LogError($"[CardDatabase] Invalid or duplicate card definition: {card?.Id ?? "<null>"}.");
                    _cards.Clear();
                    Progression = null;
                    return;
                }
                _cards.Add(card.Id, card);
            }

            // -----------------------------------------
            // Rarity Config
            // -----------------------------------------
            _rarities.Clear();
            foreach (RarityConfig rarity in container.RarityConfig)
                _rarities[rarity.Id] = rarity;

            // -----------------------------------------
            // Cards By Rarity
            // -----------------------------------------
            _cardsByRarity.Clear();
            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
                _cardsByRarity[rarity] = new List<string>();

            foreach (CardData card in _cards.Values)
                if (_cardsByRarity.TryGetValue(card.CardRarity, out var cards))
                    cards.Add(card.Id);

            // -----------------------------------------
            // Total Rarity Weight
            // -----------------------------------------
            _totalRarityWeight = 0f;
            foreach (RarityConfig rarity in _rarities.Values)
                _totalRarityWeight += rarity.Multiplier;

            if (_debug) 
                Debug.Log(
                $"[CardDatabase] Loaded {_cards.Count} cards " +
                $"across {_rarities.Count} rarities");
        }

        public float TotalRarityWeight => _totalRarityWeight;

        public IReadOnlyDictionary<string, CardData> AllCards => _cards;
        public CardData GetCard(string cardId) => 
            _cards.TryGetValue(cardId, out var c) ? c : null;

        public IReadOnlyDictionary<Rarity, RarityConfig> RarityConfigs => _rarities;
        public IReadOnlyList<string> GetCardsByRarity(Rarity rarity)
        {
             if (_cardsByRarity.TryGetValue(rarity, out var cards))
                return cards;

            return System.Array.Empty<string>();
        }
        public bool HasCards(Rarity rarity) => GetCardsByRarity(rarity).Count > 0;

        public float GetRarityMultiplier(Rarity rarityId) =>
            _rarities.TryGetValue(rarityId, out var r) ? r.Multiplier : 1f;

        /// <summary>
        /// Gets cards of the given rarity that are not at max level.
        /// </summary>
        public IReadOnlyList<string> GetAvailableCardsByRarity(Rarity rarity)
        {
            var allCards = GetCardsByRarity(rarity);
            var inventory = CardInventory.Instance;
            if (inventory == null) return allCards;

            var available = new List<string>();
            foreach (var cardId in allCards)
            {
                var ownedCard = inventory.GetOwnedCard(cardId);
                if (ownedCard == null || ownedCard.Level < Progression.MaximumLevel)
                {
                    available.Add(cardId);
                }
            }
            return available;
        }

        /// <summary>
        /// Checks if there are any available (non-max-level) cards of the given rarity.
        /// </summary>
        public bool HasAvailableCards(Rarity rarity)
        {
            return GetAvailableCardsByRarity(rarity).Count > 0;
        }

        private static bool IsProgressionConfigValid(CardProgressionConfig config)
        {
            return config != null
                && config.StartingSlots > 0
                && config.MaximumSlots >= config.StartingSlots
                && config.MaximumLevel > 1
                && config.RollCosts != null
                && config.RollCosts.Single >= 0
                && config.RollCosts.Ten >= 0
                && config.RollCosts.Hundred >= 0
                && config.PityThresholds != null
                && config.PityThresholds.Epic > 0
                && config.PityThresholds.Legendary > 0
                && config.PityThresholds.Mythic > 0
                && config.SlotExpansionCosts != null
                && config.SlotExpansionCosts.Count == config.MaximumSlots
                && config.DuplicateRequirements != null
                && config.DuplicateRequirements.Count == config.MaximumLevel - 1;
        }

        private static bool IsCardDefinitionValid(CardData card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id)) return false;
            if (string.IsNullOrWhiteSpace(card.Mode)
                || (!string.Equals(card.Mode, "Flat", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(card.Mode, "Percent", StringComparison.OrdinalIgnoreCase)))
                return false;
            if (!Enum.IsDefined(typeof(Rarity), card.CardRarity) || card.CardRarity == Rarity.None)
                return false;

            bool hasEffectType = !string.IsNullOrWhiteSpace(card.EffectType);
            bool hasSkillType = !string.IsNullOrWhiteSpace(card.SkillType);
            if (hasEffectType == hasSkillType) return false;

            if (hasEffectType
                && (!Enum.TryParse(card.EffectType, true, out CardEffectType effectType)
                    || !Enum.IsDefined(typeof(CardEffectType), effectType)
                    || effectType == CardEffectType.None))
                return false;

            if (hasSkillType
                && (!Enum.TryParse(card.SkillType, true, out Stats.SkillType skillType)
                    || !Enum.IsDefined(typeof(Stats.SkillType), skillType)
                    || skillType == Stats.SkillType.None))
                return false;

            if (card.Parameters != null)
            {
                foreach (float value in card.Parameters.Values)
                    if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            }

            return true;
        }

    }

}