using System;
using System.Collections.Generic;
using System.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays detailed information for the currently selected card.
    ///
    /// Responsibilities:
    /// - Display static card definition data.
    /// - Display ownership state.
    /// - Display current card level.
    /// - Display duplicate progression.
    /// - Display rarity visuals.
    /// - Display locked/unlocked state.
    /// - React to card state changes.
    ///
    /// This class does NOT:
    /// - Modify inventory.
    /// - Upgrade cards.
    /// - Equip cards.
    /// - Roll cards.
    /// - Perform card database queries repeatedly.
    /// </summary>
    public sealed class CardDetailUI : MonoBehaviour
    {
        #region Inspector - Card Identity
        [Header("Card Identity")]
        [SerializeField] private Image _cardIcon;
        [SerializeField] private Image _cardFrame;
         [SerializeField] private TextMeshProUGUI _cardName;
        [SerializeField] private TextMeshProUGUI _cardDescription;
        #endregion

        #region Inspector - Progression
        [Header("Progression")]
        [SerializeField] private TextMeshProUGUI _cardLevel;
        [SerializeField] private TextMeshProUGUI _duplicateProgress;
        [SerializeField] private Transform _levelValueContent;
        [SerializeField] private CardLevelValueItemUI _levelValuePrefab;
        private readonly List<CardLevelValueItemUI> _levelItems = new();
        #endregion

        #region Card Equipment
        [Header("State")]
        [SerializeField] private Button _equipUnequipButton;
        [SerializeField] private Image _equipUnequipImage;
        [SerializeField] private TextMeshProUGUI _equipUnequipText;
        [SerializeField] private GameObject _cardContentRoot;
        #endregion

        #region Runtime State
        private CardData _cardData;
        private bool _isVisible;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            HideImmediate();
            PrepareLevelValue();
        }

        private void OnEnable()
        {   
            _equipUnequipButton.onClick.AddListener(OnEquipUnequipClicked);

            CardManager.OnCardChanged += HandleCardChanged;
            CardManager.OnInventoryChanged += HandleInventoryChanged;

            RefreshEquipUnequipButton();
        }

        private void OnDisable()
        {
            _equipUnequipButton.onClick.RemoveListener(OnEquipUnequipClicked);

            CardManager.OnCardChanged -= HandleCardChanged;
            CardManager.OnInventoryChanged -= HandleInventoryChanged;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Displays the specified card.
        /// </summary>
        public void Show(CardData cardData)
        {
            if (cardData == null)
            {
                Hide();
                return;
            }

            _cardData = cardData;
            _isVisible = true;

            Refresh();
        }

        /// <summary>
        /// Hides the card detail panel.
        /// </summary>
        public void Hide()
        {
            _cardData = null;
            _isVisible = false;

            SetVisible(false);
        }

        /// <summary>
        /// Refreshes the currently displayed card.
        /// </summary>
        public void Refresh()
        {
            if (!_isVisible || _cardData == null) return;
            if (CardManager.Instance == null) return;

            RefreshVisibility();
            RefreshIdentity();
            RefreshProgression();
            RefreshEquipUnequipButton();
            RefreshLevelValueList();
        }

        #endregion

        #region Event Handling

        private void HandleCardChanged(string cardId)
        {
            if (!_isVisible || _cardData == null) return;
            if (string.IsNullOrEmpty(cardId)) return;
            if (!string.Equals(_cardData.Id, cardId, StringComparison.Ordinal)) return;
            Refresh();
        }

        private void HandleInventoryChanged()
        {
            if (!_isVisible || _cardData == null) return;
            Refresh();
        }

        #endregion

        #region Refresh

        private void RefreshVisibility()
        {
            SetVisible(true);
        }

        private void RefreshIdentity()
        {
            if (_cardData == null) return;

            RefreshIcon();
            RefreshFrame();
            RefreshName();
            RefreshDescription();
        }

        private void RefreshProgression()
        {
            OwnedCardData ownedCard = CardManager.Instance.Inventory.GetOwnedCard(_cardData.Id);

            if (ownedCard == null)
            {
                SetUnownedProgression();
                return;
            }

            SetOwnedProgression(ownedCard);
        }

        private void OnEquipUnequipClicked()
        {
            if (string.IsNullOrEmpty(_cardData.Id)) return;

            if (CardManager.Instance.IsCardEquipped(_cardData.Id))
            {
                UnequipCard();
            }
            else
            {
                EquipCard();
            }
        }

        private void EquipCard()
        {
            int emptySlot = CardManager.Instance.GetFirstEmptySlot();
            if (emptySlot < 0)
            {
                Debug.LogWarning("[CardDetailUI] Cannot equip card. No empty slot available.");
                return;
            }
            if (CardManager.Instance.Equip(_cardData.Id, emptySlot))
                RefreshEquipUnequipButton();;
        }

        private void UnequipCard()
        {
            int slotIndex = CardManager.Instance.GetEquippedSlot(_cardData.Id);
            if (slotIndex < 0) return;
            if (CardManager.Instance.Unequip(slotIndex))
                RefreshEquipUnequipButton();
        }

        private void RefreshEquipUnequipButton()
        {
            if (string.IsNullOrEmpty(_cardData.Id))
            {
                _equipUnequipButton.gameObject.SetActive(false);
                return;
            }

            bool isEquipped = CardManager.Instance.IsCardEquipped(_cardData.Id);

            _equipUnequipButton.gameObject.SetActive(true);

            if (isEquipped)
            {
                // Currently equipped.
                // Player can always remove it.
                _equipUnequipButton.interactable = true;
                _equipUnequipText.text = "REMOVE";
                if (_equipUnequipImage != null)
                    _equipUnequipImage.sprite = ButtonResources.GetColor("Yellow");

                return;
            }
            
            // Card is not equipped.
            // Check whether there is an empty equipment slot.
            bool hasEmptySlot = CardManager.Instance.GetFirstEmptySlot() >= 0;
            _equipUnequipButton.interactable = hasEmptySlot;
            _equipUnequipText.text = "EQUIP";
            if (_equipUnequipImage != null)
                _equipUnequipImage.sprite = ButtonResources.GetColor(hasEmptySlot ? "Green" : "Grey");
        }

        private void PrepareLevelValue()
        {
            if (_levelValuePrefab == null || _levelValueContent == null) return;
            _levelItems.Clear();
           for (int i = 0; i < CardDatabase.Instance.Progression.MaximumLevel; i++)
            {
                var item = Instantiate(_levelValuePrefab, _levelValueContent);
                _levelItems.Add(item);
            }
        }

        private void RefreshLevelValueList()
        {
            if (_cardData == null || _levelItems.Count == 0) return;
            int currentLevel = 1;
            OwnedCardData owned = CardManager.Instance.Inventory.GetOwnedCard(_cardData.Id);
            if (owned != null) currentLevel = owned.Level;

            int maximumLevel = CardDatabase.Instance.Progression.MaximumLevel;
            for (int level = 1; level <= maximumLevel; level++)
            {
                float value = _cardData.CalculateValue(level);
                Color color = Color.white;
                string levelText = $"Lv.{level}";

                if (level == currentLevel)
                {
                    color = GameColors.green;
                    levelText = $"▶ {levelText}";
                }
                else if (level == currentLevel + 1 && currentLevel < maximumLevel)
                {
                    color = GameColors.yellow;
                }

                _levelItems[level - 1].SetData(levelText, FormatValue(value), color);
            }
        }
        #endregion

        #region Identity Visuals

        private void RefreshIcon()
        {
            if (_cardIcon == null) return;
            _cardIcon.sprite = CardResources.GetIcon(_cardData.Id);
        }

        private void RefreshFrame()
        {
            if (_cardFrame == null) return;
            _cardFrame.sprite = CardResources.GetFrame($"{_cardData.CardRarity}");
        }

        private void RefreshName()
        {
            if (_cardName == null) return;
            bool isOwned = CardManager.Instance.Inventory.HasCard(_cardData.Id);
            _cardName.text = isOwned ? _cardData.Name : "???";
        }

        private void RefreshDescription()
        {
            if (_cardDescription == null) return;

            int currentLevel = 1;
            OwnedCardData ownedCard = CardManager.Instance.Inventory.GetOwnedCard(_cardData.Id);
            if (ownedCard != null)
                currentLevel = ownedCard.Level;

            float value = _cardData.CalculateValue(currentLevel);
            string description = _cardData.Description;

            var table = new DataTable();

            // Replace {expression} or {expression|color} with evaluated colored values
            description = System.Text.RegularExpressions.Regex.Replace(
                description,
                @"\{([^}|]+)(?:\|([^}]+))?\}",
                match =>
                {
                    string expr = match.Groups[1].Value.Trim();
                    expr = expr.Replace("CurrentValue", value.ToString(System.Globalization.CultureInfo.InvariantCulture));

                    // Parse optional color token (default = green)
                    string colorToken = match.Groups[2].Success ? match.Groups[2].Value.Trim().ToLower() : "green";

                    Color color = colorToken switch
                    {
                        "red" => GameColors.red,
                        "green" => GameColors.green,
                        "yellow" => GameColors.yellow,
                        "blue" => GameColors.blue,
                        _ => GameColors.green
                    };

                    string colorHex = ColorUtility.ToHtmlStringRGB(color);

                    try
                    {
                        object result = table.Compute(expr, null);
                        double numResult = Convert.ToDouble(result);
                        return $"<color=#{colorHex}>{numResult:0.##}</color>";
                    }
                    catch
                    {
                        return $"<color=#{colorHex}>{value:0.##}</color>";
                    }
                });

            _cardDescription.text = description;
        }

        #endregion

        #region Progression Visuals

        private void SetOwnedProgression(
            OwnedCardData ownedCard)
        {
            int level = ownedCard.Level;

            if (_cardLevel != null)
                _cardLevel.text = $"Lv. {level}";

            if (level >= CardDatabase.Instance.Progression.MaximumLevel)
            {
                SetMaxLevelProgression();
                return;
            }

            int required = CardUpgradeService.GetRequiredDuplicates(level);

            int current = Mathf.Max(0, ownedCard.DuplicateCount);

            current = Mathf.Min(current, required);

            if (_duplicateProgress != null)
                _duplicateProgress.text = $"{current} / {required}";
        }

        private void SetUnownedProgression()
        {
            if (_cardLevel != null)
                _cardLevel.text = string.Empty;

            if (_duplicateProgress != null)
                _duplicateProgress.text = string.Empty;
        }

        private void SetMaxLevelProgression()
        {
            if (_duplicateProgress != null)
                _duplicateProgress.text = "MAX";
        }

        private string FormatValue(float value)
        {
            return _cardData.Mode switch
            {
                "Percent" => $"{value:0.#}%",
                "Multiple" => $"{value:0.##}x",
                _ => value.ToString("0.#")
            };
        }
        #endregion

        #region Visibility

        private void SetVisible(bool visible)
        {
            if (_cardContentRoot != null)
                _cardContentRoot.SetActive(visible);
        }

        private void HideImmediate()
        {
            _isVisible = false;
            _cardData = null;

            if (_cardContentRoot != null)
                _cardContentRoot.SetActive(false);
        }

        #endregion

        #region Public State
        public CardData GetDisplayedCard() => _cardData;
        public bool HasDisplayedCard() => _isVisible && _cardData != null;
        #endregion
    }
}