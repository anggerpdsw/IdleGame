using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.CardCollection
{
    /// <summary>
    /// Represents a single card item inside the card collection UI.
    ///
    /// Responsibilities:
    /// - Display card identity.
    /// - Display card rarity.
    /// - Display ownership state.
    /// - Display card level.
    /// - Display locked/unlocked state.
    /// - Display selection state.
    /// - Forward user selection to the collection controller.
    ///
    /// This class does NOT:
    /// - Modify inventory.
    /// - Upgrade cards.
    /// - Equip cards.
    /// - Perform card transactions.
    /// - Query card definitions directly.
    /// </summary>
    public sealed class CardCollectionItemUI : MonoBehaviour
    {
        #region Inspector - Root
        [Header("Root")]
        [SerializeField] private Button _button;
        #endregion

        #region Inspector - Card Visual
        [Header("Card Visual")]
        [SerializeField] private Image _cardIcon;
        [SerializeField] private Image _cardFrame;
        #endregion

        #region Inspector - Selection
        [Header("Selection")]
        [SerializeField] private TextMeshProUGUI _cardLevel;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private GameObject _selectionHighlight;
        [SerializeField] private GameObject _equippedIndicator;
        #endregion

        #region Inspector - Optional
        [Header("Optional")]
        [SerializeField] private CanvasGroup _canvasGroup;
        #endregion

        #region Runtime State
        private CardData _cardData;
        private bool _isOwned;
        private bool _isSelected;
        private Action<CardData> _onSelected;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            ValidateReferences();
            ConfigureButton();
            ApplySelectionVisual();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Initializes the UI item.
        /// </summary>
        public void Setup(CardData cardData, bool isOwned, Action<CardData> onSelected)
        {
            if (cardData == null)
            {
                Debug.LogError($"[{this}] CardData is null.", this);
                return;
            }

            _cardData = cardData;
            _isOwned = isOwned;
            _onSelected = onSelected;

            Refresh();
        }

        /// <summary>
        /// Refreshes all visual states.
        /// </summary>
        public void Refresh()
        {
            if (_cardData == null) return;
            RefreshOwnership();
            RefreshIcon();
            RefreshRarity();
            RefreshLevel();
            ApplySelectionVisual();
        }

        /// <summary>
        /// Sets selected state.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            ApplySelectionVisual();
        }

        public void SetEquipped(bool equipped)
        {
            if (_equippedIndicator == null) return;
            _equippedIndicator.SetActive(equipped);
        }

        private void ApplySelectionVisual()
        {
            if (_selectionHighlight == null) return;
            _selectionHighlight.SetActive(_isSelected);
        }
        #endregion

        /// <summary>
        /// Returns the card represented by this UI item.
        /// </summary>
        public CardData GetCardData() => _cardData;
        
        /// <summary>
        /// Returns whether the player owns this card.
        /// </summary>
        public bool IsOwned() => _isOwned;

        #region Ownership
        private void RefreshOwnership()
        {
            if (_cardData == null) return;
            // Update ownership state from inventory
            _isOwned = CardManager.Instance != null && CardManager.Instance.Inventory.HasCard(_cardData.Id);
            ApplyOwnershipVisual();
        }

        private void ApplyOwnershipVisual()
        {
            if (_cardIcon != null) 
                _cardIcon.gameObject.SetActive(_isOwned);
            if (_lockIcon != null) 
                _lockIcon.SetActive(!_isOwned);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isOwned ? 1f : 0.65f;
                _canvasGroup.interactable = _isOwned;
                _canvasGroup.blocksRaycasts = _isOwned;
            }
        }
        #endregion


        #region Visual
        private void RefreshIcon() => _cardIcon.sprite = CardResources.GetIcon(_cardData.Id);
        private void RefreshRarity() => _cardFrame.sprite = CardResources.GetFrame($"{_cardData.CardRarity}");

        private void RefreshLevel()
        {
            if (_cardLevel == null || _cardData == null) return;
            if (!_isOwned)
            {
                _cardLevel.text = string.Empty;
                return;
            }

            OwnedCardData ownedCard = CardManager.Instance.Inventory.GetOwnedCard(_cardData.Id);
            if (ownedCard == null)
            {
                _cardLevel.text = string.Empty;
                return;
            }

            _cardLevel.text = ownedCard.Level == 10 ? string.Empty : $"Lv. {ownedCard.Level}";
        }
        #endregion


        #region Interaction
        private void ConfigureButton()
        {
            if (_button == null)
            {
                Debug.LogError($"[{this}] Button reference is missing. Card item will not be clickable.", this);
                return;
            }

            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
            _button.interactable = true;
        }

        public void HandleClick()
        {
            if (_cardData == null || !_isOwned) return;
            _onSelected?.Invoke(_cardData);
        }

        private void ValidateReferences()
        {
            if (_button == null)
                Debug.LogError($"[{this}] Missing Button reference.", this);

            if (_cardIcon == null)
                Debug.LogWarning($"[{this}] Card Icon reference is missing.", this);

            if (_cardFrame == null)
                Debug.LogWarning($"[{this}] Card Frame reference is missing.", this );

            if (_selectionHighlight == null)
                Debug.LogWarning($"[{this}] Selection Highlight reference is missing.", this);
        }
        #endregion
    }
}