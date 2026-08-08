using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using System.Linq;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>
    /// Comparison panel for equipment items.
    /// </summary>
    public class EquipmentComparePanel : MonoBehaviour
    {
        [Header("Current Item (Left)")]
        [SerializeField] private TextMeshProUGUI _currentNameText;
        [SerializeField] private TextMeshProUGUI _currentRarityText;
        [SerializeField] private Image _currentIconImage;
        [SerializeField] private Transform _currentStatsContainer;
        [SerializeField] private GameObject _statEntryPrefab;

        [Header("New Item (Right)")]
        [SerializeField] private TextMeshProUGUI _newNameText;
        [SerializeField] private TextMeshProUGUI _newRarityText;
        [SerializeField] private Image _newIconImage;
        [SerializeField] private Transform _newStatsContainer;
        [SerializeField] private GameObject _newStatEntryPrefab;

        [Header("Comparison Stats")]
        [SerializeField] private Transform _comparisonStatsContainer;
        [SerializeField] private GameObject _comparisonStatPrefab;

        [Header("Effects Comparison")]
        [SerializeField] private Transform _currentEffectsContainer;
        [SerializeField] private Transform _newEffectsContainer;
        [SerializeField] private GameObject _effectEntryPrefab;

        [Header("Actions")]
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _closeButton;

        private InventoryItem _currentItem;
        private InventoryItem _newItem;

        private void Awake()
        {
            if (_equipButton != null) _equipButton.onClick.AddListener(OnEquip);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        public void ShowComparison(InventoryItem current, InventoryItem candidate)
        {
            _currentItem = current;
            _newItem = candidate;

            gameObject.SetActive(true);

            // Current item
            ShowItemSide(_currentItem, _currentNameText, _currentRarityText, _currentIconImage, _currentStatsContainer, _currentEffectsContainer);

            // New item
            ShowItemSide(_newItem, _newNameText, _newRarityText, _newIconImage, _newStatsContainer, _newEffectsContainer);

            // Comparison
            BuildComparison(_currentItem, _newItem);
        }

        private void ShowItemSide(InventoryItem item, TextMeshProUGUI nameText, TextMeshProUGUI rarityText, Image iconImage, Transform statsContainer, Transform effectsContainer)
        {
            if (item == null)
            {
                if (nameText != null) nameText.text = "Empty";
                if (rarityText != null) rarityText.text = "";
                if (iconImage != null) iconImage.enabled = false;
                return;
            }

            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return;

            if (nameText != null)
            {
                nameText.text = itemData.Name;
                nameText.color = itemData.ItemRarity.GetDefaultColor();
            }
            if (rarityText != null)
            {
                rarityText.text = itemData.ItemRarity.GetDisplayName();
                rarityText.color = itemData.ItemRarity.GetDefaultColor();
            }
            if (iconImage != null && itemData.Icon != null)
            {
                iconImage.sprite = itemData.Icon;
                iconImage.enabled = true;
            }

            // Stats
            if (statsContainer != null && _statEntryPrefab != null)
            {
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);

                var bonuses = EquipmentComparer.GetTotalStatBonuses(item);
                foreach (var kvp in bonuses.OrderByDescending(k => Math.Abs(k.Value)))
                {
                    if (Math.Abs(kvp.Value) < 0.001f) continue;
                    var entryObj = Instantiate(_statEntryPrefab, statsContainer);
                    var entryUI = entryObj.GetComponent<EquipmentStatEntryUI>();
                    if (entryUI != null)
                    {
                        string sign = kvp.Value >= 0 ? "+" : "";
                        entryUI.Initialize(kvp.Key.GetDisplayName(), $"{sign}{kvp.Value:F1}", kvp.Value >= 0 ? Color.green : Color.red);
                    }
                }
            }

            // Effects
            if (effectsContainer != null && _effectEntryPrefab != null && itemData?.SpecialEffects != null)
            {
                foreach (Transform child in effectsContainer)
                    Destroy(child.gameObject);

                foreach (var effect in itemData.SpecialEffects)
                {
                    if (effect.IsActive)
                    {
                        var entryObj = Instantiate(_effectEntryPrefab, effectsContainer);
                        var entryUI = entryObj.GetComponent<EquipmentEffectEntryUI>();
                        if (entryUI != null)
                            entryUI.Initialize(effect.EffectType.GetDisplayName(), effect.Value, effect.Chance);
                    }
                }
            }
        }

        private void BuildComparison(InventoryItem current, InventoryItem candidate)
        {
            if (_comparisonStatsContainer == null || _comparisonStatPrefab == null) return;

            foreach (Transform child in _comparisonStatsContainer)
                Destroy(child.gameObject);

            if (current == null && candidate == null) return;

            var comparison = EquipmentComparer.Compare(current, candidate, candidate?.GetEquipmentType() ?? EquipmentType.None);
            if (comparison == null) return;

            foreach (var kvp in comparison.StatComparisons.OrderByDescending(k => Math.Abs(k.Value.Difference)))
            {
                if (Math.Abs(kvp.Value.Difference) < 0.001f) continue;

                var entryObj = Instantiate(_comparisonStatPrefab, _comparisonStatsContainer);
                var entryUI = entryObj.GetComponent<EquipmentComparisonStatUI>();
                if (entryUI != null)
                    entryUI.Initialize(kvp.Value);
            }
        }

        private void OnEquip()
        {
            if (_newItem != null)
                EquipmentService.Instance?.Equip(_newItem);
            Hide();
        }

        public void Hide()
        {
            _currentItem = null;
            _newItem = null;
            gameObject.SetActive(false);
        }
    }
}