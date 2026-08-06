using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using UnityEngine;

namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Holds owned cards, duplicate counts, and level.
    /// </summary>
    public class CardInventory : MonoBehaviour
    {
        #region Singleton
        private static CardInventory _instance;
        public static CardInventory Instance => _instance;

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

        // CardId -> OwnedCardData (level, duplicate count, etc.)
        private readonly Dictionary<string, OwnedCardData> _owned = new();

        // Dirty flag for auto-save
        private bool _dirty = false;
        public bool IsDirty => _dirty;

        public void Initialize()
        {
            _owned.Clear();
            _dirty = false;
            // SaveManager.LoadAll() will call LoadInventory to populate
        }

        public void MarkDirty()
        {
            _dirty = true;
            // SaveManager auto-save loop will pick it up
        }

        public void ClearDirty() => _dirty = false;

        // ----------------------------------------------------
        // Public API
        // ----------------------------------------------------

        public bool HasCard(string cardId) => _owned.ContainsKey(cardId);

        public OwnedCardData GetOwnedCard(string cardId) => _owned.TryGetValue(cardId, out var c) ? c : null;

        public IReadOnlyDictionary<string, OwnedCardData> AllOwned => _owned;

        public bool AddNewCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (_owned.ContainsKey(cardId)) return false;

            _owned[cardId] = new OwnedCardData { CardId = cardId, Level = 1, DuplicateCount = 0 };
            MarkDirty();
            return true;
        }

        public bool AddDuplicate(string cardId, int count = 1)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (count <= 0) return false;
            if (!_owned.TryGetValue(cardId, out var card)) return false;

            card.DuplicateCount += count;
            MarkDirty();
            return true;
        }

        public string RefreshDuplicateProgress(string cardId)
        {
            if (!_owned.TryGetValue(cardId, out OwnedCardData ownedCard)) return "";
            if (ownedCard == null) return "";

            if (ownedCard.Level >= GameConstants.CARD_MAX_LEVEL) return "MAX";

            int required = CardUpgradeService.GetRequiredDuplicates(ownedCard.Level);

            return $"{ownedCard.DuplicateCount} / {required}";
        }

        // Called by SaveManager when loading saved data
        public void LoadInventory(Dictionary<string, OwnedCardData> savedCards)
        {
            _owned.Clear();
            foreach (var kvp in savedCards)
                _owned[kvp.Key] = kvp.Value;
            _dirty = false;
        }

        // Called by SaveManager to get serializable data
        public Dictionary<string, OwnedCardData> GetSaveData() => new(_owned);
    }

}