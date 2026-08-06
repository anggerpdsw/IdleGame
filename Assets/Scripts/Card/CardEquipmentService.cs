using System.Collections.Generic;
using IdleDefenseSurvival.Economy;
using UnityEngine;
using IdleDefenseSurvival.Card;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Handles card equipment slots: equipping, unequipping, expanding.
    /// </summary>
    public class CardEquipmentService : MonoBehaviour
    {
        #region Singleton
        private static CardEquipmentService _instance;
        public static CardEquipmentService Instance => _instance;

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

        // Slot index -> CardId (empty string if empty)
        private readonly List<string> _equipped = new();
        private const string EmptySlot = "";

        public IReadOnlyList<string> EquippedCards => _equipped;
        public int UnlockedSlotCount => _equipped.Count;
        public int MaxSlots => GameConstants.CARD_MAX_SLOT;
        public int NextSlotCostGem
        {
            get
            {
                if (_equipped.Count >= GameConstants.CARD_MAX_SLOT) return 0;
                return GameConstants.CARD_SLOT_EXPANSION_COSTS[_equipped.Count];
            }
        }
        public int EquippedCardCount
        {
            get
            {
                int count = 0;
                foreach (string cardId in _equipped)
                    if (!string.IsNullOrEmpty(cardId))
                        count++;
                return count;
            }
        }

        private bool HasValidExpansionCost()
        {
            return _equipped.Count >= 0
                && _equipped.Count < GameConstants.CARD_SLOT_EXPANSION_COSTS.Length;
        }

        public void Initialize()
        {
            _equipped.Clear();
            for (int i = 0; i < GameConstants.CARD_START_SLOT; i++)
                _equipped.Add(EmptySlot);
        }

        /// <summary>
        /// Try to equip a card at the given slot. Returns true on success.
        /// </summary>
        public bool Equip(string cardId, int slot)
        {
            if (!IsValidSlot(slot)) return false;
            if (!CanEquip(cardId)) return false;

            int equippedIndex = _equipped.IndexOf(cardId);
            if (equippedIndex >= 0) _equipped[equippedIndex] = EmptySlot;

            _equipped[slot] = cardId;
            CardInventory.Instance.MarkDirty();
            return true;
        }

        /// <summary>
        /// Unequip card from slot. Returns true if there was a card.
        /// </summary>
        public bool Unequip(int slot)
        {
            if (slot < 0 || slot >= _equipped.Count) return false;
            if (string.IsNullOrEmpty(_equipped[slot])) return false;

            _equipped[slot] = EmptySlot;
            CardInventory.Instance.MarkDirty();
            return true;
        }

        /// <summary>
        /// Expand slot count by 1 if not at max. Costs gems.
        /// </summary>
        public bool ExpandSlot()
        {
            if (_equipped.Count >= GameConstants.CARD_MAX_SLOT) return false;
            if (!HasValidExpansionCost())
            {
                Debug.LogError($"Missing card slot expansion cost for slot {_equipped.Count + 1}.");
                return false;
            }
            int cost = NextSlotCostGem;
            int nextSlot = _equipped.Count + 1;
            if (!EconomyManager.Instance.TrySpendCurrency(
                CurrencyType.Gem, cost, $"Expand Card Slot {nextSlot}"))
                return false;

            _equipped.Add(EmptySlot);
            CardInventory.Instance.MarkDirty();
            return true;
        }

        private bool IsValidSlot(int slot) => slot >= 0 && slot < _equipped.Count;
        private bool CanEquip(string cardId)
        {
            return !string.IsNullOrEmpty(cardId)
                && CardDatabase.Instance.GetCard(cardId) != null
                && CardInventory.Instance.HasCard(cardId);
        }

        public bool IsEquipped(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            return _equipped.Contains(cardId);
        }
        
        public int GetEquippedSlot(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return -1;
            return _equipped.IndexOf(cardId);
        }

        public int GetFirstEmptySlot()
        {
            for (int i = 0; i < _equipped.Count; i++)
                if (string.IsNullOrEmpty(_equipped[i])) return i;
            return -1;
        }

        // Called by SaveManager to load/save
        public void LoadEquipment(List<string> savedEquipped)
        {
            _equipped.Clear();

            if (savedEquipped != null)
            {
                foreach (string cardId in savedEquipped)
                {
                    if (string.IsNullOrEmpty(cardId))
                    {
                        _equipped.Add(EmptySlot);
                    }
                    else if (CardDatabase.Instance.GetCard(cardId) != null)
                    {
                        _equipped.Add(cardId);
                    }
                    else
                    {
                        _equipped.Add(EmptySlot);
                    }

                    if (_equipped.Count >= GameConstants.CARD_MAX_SLOT) break;
                }
            }

            while (_equipped.Count < GameConstants.CARD_START_SLOT) _equipped.Add(EmptySlot);
        }

        public List<string> GetSaveData() => new(_equipped);
    }
}