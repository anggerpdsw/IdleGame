using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.UI.CardCollection;
using UnityEngine;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Controls the card collection screen.
    ///
    /// Responsibilities:
    /// - Generate all card collection items.
    /// - Display owned and unowned cards.
    /// - Display equipped cards.
    /// - Handle card selection.
    /// - Maintain selection state.
    /// - Update selection highlight.
    /// - Update equipped indicators.
    /// - Display selected card detail.
    /// - React to inventory, card, and equipment state changes.
    ///
    /// This class does NOT:
    /// - Modify card inventory.
    /// - Upgrade cards.
    /// - Equip cards.
    /// - Perform card transactions.
    /// </summary>
    public class CardCollectionUI : MonoBehaviour
    {
        #region Inspector
        [SerializeField] private Transform _allCardContent;
        [SerializeField] private Transform _equipedCardContent;
        [SerializeField] private CardCollectionItemUI _cardItemPrefab;
        [SerializeField] private CardDetailUI _cardDetailUI;
        #endregion

        #region Runtime State
        private readonly List<CardCollectionItemUI> _items = new();
        private readonly Dictionary<string, CardCollectionItemUI> _itemsByCardId = new();
        private readonly List<CardCollectionItemUI> _equippedItems = new();
        private readonly Dictionary<string, CardCollectionItemUI> _equippedItemsByCardId = new();
        private string _selectedCardId;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            CardManager.OnInventoryChanged += HandleInventoryChanged;
            CardManager.OnCardChanged += HandleCardChanged;
            CardManager.OnEquipmentChanged += HandleEquipmentChanged;
            Refresh();
        }

        private void OnDisable()
        {
            CardManager.OnInventoryChanged -= HandleInventoryChanged;
            CardManager.OnCardChanged -= HandleCardChanged;
            CardManager.OnEquipmentChanged -= HandleEquipmentChanged;
        }
        #endregion

        private void Refresh()
        {
            string previousSelection = _selectedCardId;

            ClearContent();
            ClearEquippedContent();

            BuildAllCardContent();
            BuildEquippedCardContent();

            RestoreSelection(previousSelection);
        }

        private void BuildAllCardContent()
        {
            if (_allCardContent == null)
            {
                Debug.LogWarning("[CardCollectionUI] All Card Content reference is missing.");
                return;
            }

            foreach (KeyValuePair<string, CardData> pair in CardManager.Instance.AllCards)
            {
                CardData cardData = pair.Value;
                if (cardData == null) continue;
                if (string.IsNullOrEmpty(cardData.Id))
                {
                    Debug.LogWarning("[CardCollectionUI] CardData has empty ID.");
                    continue;
                }
                bool isOwned = CardManager.Instance.Inventory.HasCard(cardData.Id);
                bool isEquipped = CardManager.Instance.IsCardEquipped(cardData.Id);

                CardCollectionItemUI item = Instantiate(_cardItemPrefab, _allCardContent);
                if (item == null) continue;
                item.Setup(cardData, isOwned, HandleCardSelected);
                // Show equipped indicator on the card
                // inside the All Cards collection.
                item.SetEquipped(isEquipped);
                _items.Add(item);
                _itemsByCardId[cardData.Id] = item;
            }
        }

        private void BuildEquippedCardContent()
        {
            if (_equipedCardContent == null)
            {
                Debug.LogWarning("[CardCollectionUI] Equipped Card Content reference is missing.");
                return;
            }

            foreach (KeyValuePair<string, CardData> pair in CardManager.Instance.AllCards)
            {
                CardData cardData = pair.Value;
                if (cardData == null) continue;
                if (string.IsNullOrEmpty(cardData.Id)) continue;
                if (!CardManager.Instance.IsCardEquipped(cardData.Id)) continue;

                // Only equipped cards are instantiated
                // into the Equipped Card Content.
                CardCollectionItemUI equippedItem = Instantiate(_cardItemPrefab, _equipedCardContent);
                if (equippedItem == null) continue;
                bool isOwned = CardManager.Instance.Inventory.HasCard(cardData.Id);
                equippedItem.Setup(cardData, isOwned, HandleCardSelected);
                equippedItem.SetEquipped(false);
                _equippedItems.Add(equippedItem);
                _equippedItemsByCardId[cardData.Id] = equippedItem;
            }
        }

        private void ClearContent()
        {
            for (int i = _allCardContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _allCardContent.GetChild(i).gameObject;
                Destroy(child);
            }

            _items.Clear();
            _itemsByCardId.Clear();
        }

        private void ClearEquippedContent()
        {
            for (int i = _equipedCardContent.childCount - 1; i > 0; i--)
            {
                GameObject child = _equipedCardContent.GetChild(i).gameObject;
                Destroy(child);
            }
            _equippedItems.Clear();
            _equippedItemsByCardId.Clear();
        }

        private void HandleCardSelected(CardData cardData)
        {
            if (cardData == null) return;
            if (string.IsNullOrEmpty(cardData.Id)) return;

            SelectCard(cardData.Id);
        }

        #region Selection
        private void SelectCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            if (!_itemsByCardId.TryGetValue(cardId, out CardCollectionItemUI selectedItem))
            {
                Debug.LogWarning(
                    $"[CardCollectionUI] Cannot select card '{cardId}'. " +
                    "Item was not found."
                );
                return;
            }

            _selectedCardId = cardId;
            UpdateSelectionVisual(selectedItem);
            ShowSelectedCardDetail(cardId);
        }

        private void UpdateSelectionVisual(CardCollectionItemUI selectedItem)
        {
            foreach (CardCollectionItemUI item in _items)
            {
                if (item == null) continue;
                bool isSelected = item == selectedItem;
                item.SetSelected(isSelected);
            }
        }

        private void ShowSelectedCardDetail(string cardId)
        {
            if (_cardDetailUI == null)
            {
                Debug.LogWarning("[CardCollectionUI] CardDetailUI reference is missing.");
                return;
            }

            CardManager cardManager = CardManager.Instance;
            if (cardManager == null) return;
            if (!cardManager.AllCards.TryGetValue(cardId, out CardData cardData))
            {
                Debug.LogWarning(
                    $"[CardCollectionUI] Card '{cardId}' " +
                    "was not found in AllCards."
                );
                return;
            }

            _cardDetailUI.Show(cardData);
        }

        private void RestoreSelection(string previousSelectionId)
        {
            if (string.IsNullOrEmpty(previousSelectionId)) return;
            if (!_itemsByCardId.ContainsKey(previousSelectionId)) return;
            SelectCard(previousSelectionId);
        }
        #endregion

        #region Event Handling
        private void HandleInventoryChanged()
        {
            Refresh();
            if (string.IsNullOrEmpty(_selectedCardId)) return;
            ShowSelectedCardDetail(_selectedCardId);
        }

        private void HandleCardChanged(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (_itemsByCardId.TryGetValue(cardId, out CardCollectionItemUI item))
                if (item != null) item.Refresh();

            // Refresh currently selected detail.
            if (string.Equals(_selectedCardId, cardId, StringComparison.Ordinal))
                ShowSelectedCardDetail(cardId);
        }

        private void HandleEquipmentChanged()
        {
            // Rebuild both collections:
            // 1. All Cards -> update Equipped indicator.
            // 2. Equipped Cards -> add/remove equipped card.
            Refresh();
        }
        #endregion

    }
}