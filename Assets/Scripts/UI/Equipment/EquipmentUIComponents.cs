using TMPro;
using System;
using System.Collections.Generic;
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
    /// Info panel for selected equipment item.
    /// </summary>
    public class EquipmentInfoPanel : MonoBehaviour
    {
        [Header("Basic Info")]
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemRarityText;
        [SerializeField] private Image _itemIconImage;
        [SerializeField] private TextMeshProUGUI _equipSlotText;

        [Header("Stats")]
        [SerializeField] private Transform _statsContainer;
        [SerializeField] private GameObject _statEntryPrefab;

        [Header("Effects")]
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectEntryPrefab;

        [Header("Set Bonus")]
        [SerializeField] private Transform _setBonusContainer;
        [SerializeField] private GameObject _setBonusEntryPrefab;

        [Header("Gems")]
        [SerializeField] private Transform _gemsContainer;
        [SerializeField] private GameObject _gemSocketPrefab;

        [Header("Durability")]
        [SerializeField] private Slider _durabilityBar;
        [SerializeField] private TextMeshProUGUI _durabilityText;

        [Header("Enhancement")]
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _enhanceText;
        [SerializeField] private TextMeshProUGUI _refineText;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _enhanceButton;
        [SerializeField] private Button _refineButton;
        [SerializeField] private Button _awakenButton;

        [Header("Actions")]
        [SerializeField] private Button _unequipButton;
        [SerializeField] private Button _repairButton;
        [SerializeField] private Button _socketGemButton;
        [SerializeField] private Button _removeGemButton;

        [Header("Toggles")]
        [SerializeField] private Toggle _favoriteToggle;
        [SerializeField] private Toggle _lockToggle;

        private InventoryItem _currentItem;
        private EquipmentUI _parentUI;

        public void Initialize(EquipmentUI parentUI)
        {
            _parentUI = parentUI;

            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(OnUpgrade);
            if (_enhanceButton != null) _enhanceButton.onClick.AddListener(OnEnhance);
            if (_refineButton != null) _refineButton.onClick.AddListener(OnRefine);
            if (_awakenButton != null) _awakenButton.onClick.AddListener(OnAwaken);
            if (_unequipButton != null) _unequipButton.onClick.AddListener(OnUnequip);
            if (_repairButton != null) _repairButton.onClick.AddListener(OnRepair);
            if (_socketGemButton != null) _socketGemButton.onClick.AddListener(OnSocketGem);
            if (_removeGemButton != null) _removeGemButton.onClick.AddListener(OnRemoveGem);
            if (_favoriteToggle != null) _favoriteToggle.onValueChanged.AddListener(OnFavoriteChanged);
            if (_lockToggle != null) _lockToggle.onValueChanged.AddListener(OnLockChanged);

            Hide();
        }

        public void ShowItem(InventoryItem item)
        {
            _currentItem = item;
            if (item == null)
            {
                Hide();
                return;
            }

            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            // Basic info
            if (_itemNameText != null)
            {
                _itemNameText.text = itemData.Name;
                _itemNameText.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_itemRarityText != null)
            {
                _itemRarityText.text = itemData.ItemRarity.GetDisplayName();
                _itemRarityText.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_itemIconImage != null && itemData.Icon != null)
            {
                _itemIconImage.sprite = itemData.Icon;
                _itemIconImage.enabled = true;
            }

            if (_equipSlotText != null)
                _equipSlotText.text = item.GetEquipmentType().GetDisplayName();

            // Level/Enhance/Refine
            if (_levelText != null)
                _levelText.text = $"Lv.{item.Level}";
            if (_enhanceText != null)
                _enhanceText.text = item.EnhanceLevel > 0 ? $"+{item.EnhanceLevel}" : "";
            if (_refineText != null)
                _refineText.text = item.RefineLevel > 0 ? $"Ref.{item.RefineLevel}" : "";

            // Durability
            if (_durabilityBar != null)
            {
                float p = item.GetDurabilityPercent();
                _durabilityBar.value = p;
                var fill = _durabilityBar.fillRect != null ? _durabilityBar.fillRect.GetComponent<Image>() : null;
                if (fill != null) fill.color = item.GetDurabilityColor();
            }
            if (_durabilityText != null)
                _durabilityText.text = $"{item.CurrentDurability}/{item.MaxDurability}";

            // Stats
            BuildStats(item, itemData);

            // Effects
            BuildEffects(itemData);

            // Set Bonus
            BuildSetBonuses(item, itemData);

            // Gems
            BuildGemSockets(item);

            // Update buttons
            UpdateButtons(item);

            // Toggles
            if (_favoriteToggle != null)
            {
                _favoriteToggle.isOn = item.IsFavorite;
                _favoriteToggle.interactable = !item.IsLocked;
            }
            if (_lockToggle != null)
            {
                _lockToggle.isOn = item.IsLocked;
            }
        }

        private void BuildStats(InventoryItem item, EquipmentData itemData)
        {
            if (_statsContainer == null || _statEntryPrefab == null) return;

            foreach (Transform child in _statsContainer)
                Destroy(child.gameObject);

            var bonuses = EquipmentComparer.GetTotalStatBonuses(item);
            foreach (var kvp in bonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                if (Math.Abs(kvp.Value) < 0.001f) continue;

                var entryObj = Instantiate(_statEntryPrefab, _statsContainer);
                var entryUI = entryObj.GetComponent<EquipmentStatEntryUI>();
                if (entryUI != null)
                {
                    string sign = kvp.Value >= 0 ? "+" : "";
                    entryUI.Initialize(kvp.Key.GetDisplayName(), $"{sign}{kvp.Value:F1}", kvp.Value >= 0 ? Color.green : Color.red);
                }
            }
        }

        private void BuildEffects(EquipmentData itemData)
        {
            if (_effectsContainer == null || _effectEntryPrefab == null) return;

            foreach (Transform child in _effectsContainer)
                Destroy(child.gameObject);

            if (itemData?.SpecialEffects != null)
            {
                foreach (var effect in itemData.SpecialEffects)
                {
                    if (effect.IsActive)
                    {
                        var entryObj = Instantiate(_effectEntryPrefab, _effectsContainer);
                        var entryUI = entryObj.GetComponent<EquipmentEffectEntryUI>();
                        if (entryUI != null)
                            entryUI.Initialize(effect.EffectType.GetDisplayName(), effect.Value, effect.Chance);
                    }
                }
            }
        }

        private void BuildSetBonuses(InventoryItem item, EquipmentData itemData)
        {
            if (_setBonusContainer == null || _setBonusEntryPrefab == null) return;

            foreach (Transform child in _setBonusContainer)
                Destroy(child.gameObject);

            string setId = item.GetSetId();
            if (string.IsNullOrEmpty(setId)) return;

            var setData = ItemDatabase.Instance?.GetSet(setId);
            if (setData?.Tiers == null) return;

            int pieceCount = EquipmentService.Instance?.GetSetPieceCount(setId) + 1 ?? 1;

            foreach (var tier in setData.Tiers.Where(t => t.IsActive(pieceCount)))
            {
                var entryObj = Instantiate(_setBonusEntryPrefab, _setBonusContainer);
                var entryUI = entryObj.GetComponent<EquipmentSetBonusEntryUI>();
                if (entryUI != null)
                    entryUI.Initialize(setData.SetName, tier.TierName, tier.Description, pieceCount, tier.RequiredPieces);
            }
        }

        private void BuildGemSockets(InventoryItem item)
        {
            if (_gemsContainer == null || _gemSocketPrefab == null) return;

            foreach (Transform child in _gemsContainer)
                Destroy(child.gameObject);

            if (item.Sockets == null) return;

            foreach (var socket in item.Sockets)
            {
                var entryObj = Instantiate(_gemSocketPrefab, _gemsContainer);
                var entryUI = entryObj.GetComponent<EquipmentGemSocketUI>();
                if (entryUI != null)
                    entryUI.Initialize(socket);
            }
        }

        private void UpdateButtons(InventoryItem item)
        {
            var canEnhance = UpgradeService.Instance?.CanEnhance(item, out _) ?? false;
            var canRefine = UpgradeService.Instance?.CanRefine(item, out _) ?? false;
            var canAwaken = UpgradeService.Instance?.CanAwaken(item, out _) ?? false;
            var canUpgrade = UpgradeService.Instance?.CanUpgradeLevel(item, out _) ?? false;

            if (_upgradeButton != null)
            {
                _upgradeButton.gameObject.SetActive(canUpgrade);
                _upgradeButton.interactable = canUpgrade && !item.IsLocked;
            }
            if (_enhanceButton != null)
            {
                _enhanceButton.gameObject.SetActive(canEnhance);
                _enhanceButton.interactable = canEnhance && !item.IsLocked;
            }
            if (_refineButton != null)
            {
                _refineButton.gameObject.SetActive(canRefine);
                _refineButton.interactable = canRefine && !item.IsLocked;
            }
            if (_awakenButton != null)
            {
                _awakenButton.gameObject.SetActive(canAwaken);
                _awakenButton.interactable = canAwaken && !item.IsLocked;
            }

            if (_unequipButton != null)
                _unequipButton.interactable = !item.IsLocked;
            if (_repairButton != null)
                _repairButton.interactable = item.CurrentDurability < item.MaxDurability && !item.IsLocked;
            if (_socketGemButton != null)
                _socketGemButton.interactable = !item.IsLocked && GemService.Instance?.GetUnlockedSockets(item).Length > 0;
            if (_removeGemButton != null)
                _removeGemButton.interactable = !item.IsLocked && GemService.Instance?.GetTotalSocketedGems(item) > 0;
        }

        public void Hide()
        {
            _currentItem = null;
            gameObject.SetActive(false);
        }

        private void OnUpgrade() => UpgradeService.Instance?.UpgradeLevel(_currentItem);
        private void OnEnhance() => UpgradeService.Instance?.Enhance(_currentItem);
        private void OnRefine() => UpgradeService.Instance?.Refine(_currentItem);
        private void OnAwaken() => UpgradeService.Instance?.Awaken(_currentItem);
        private void OnUnequip() => EquipmentService.Instance?.Unequip(_currentItem.EquippedSlot);
        private void OnRepair() => RepairService.Instance?.RepairItem(_currentItem);
        private void OnSocketGem() { /* Open gem selection UI */ }
        private void OnRemoveGem() { /* Open gem removal UI */ }
        private void OnFavoriteChanged(bool isFavorite) => InventoryService.Instance?.SetFavorite(_currentItem.InstanceId, isFavorite);
        private void OnLockChanged(bool isLocked) => InventoryService.Instance?.SetLocked(_currentItem.InstanceId, isLocked);
    }

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

    // ============ Sub-components ============

    public class EquipmentStatEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;

        public void Initialize(string name, string value, Color color)
        {
            if (_statNameText != null) _statNameText.text = name;
            if (_statValueText != null)
            {
                _statValueText.text = value;
                _statValueText.color = color;
            }
        }
    }

    public class EquipmentEffectEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectValueText;
        [SerializeField] private TextMeshProUGUI _effectChanceText;

        public void Initialize(string name, float value, float chance)
        {
            if (_effectNameText != null) _effectNameText.text = name;
            if (_effectValueText != null) _effectValueText.text = value.ToString("F1");
            if (_effectChanceText != null)
            {
                if (chance < 100) _effectChanceText.text = $"{chance:F0}% chance";
                else _effectChanceText.text = "";
            }
        }
    }

    public class EquipmentSetBonusEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _setNameText;
        [SerializeField] private TextMeshProUGUI _tierText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        public void Initialize(string setName, string tierName, string description, int currentPieces, int requiredPieces)
        {
            if (_setNameText != null) _setNameText.text = setName;
            if (_tierText != null) _tierText.text = tierName;
            if (_descriptionText != null) _descriptionText.text = description;
            if (_progressBar != null) _progressBar.fillAmount = (float)currentPieces / requiredPieces;
            if (_progressText != null) _progressText.text = $"{currentPieces}/{requiredPieces}";
        }
    }

    public class EquipmentGemSocketUI : MonoBehaviour
    {
        [SerializeField] private Image _socketBackground;
        [SerializeField] private Image _gemIcon;
        [SerializeField] private Image _gemTypeColor;
        [SerializeField] private TextMeshProUGUI _gemLevelText;
        [SerializeField] private GameObject _lockedOverlay;

        public void Initialize(SocketData socket)
        {
            if (socket.IsUnlocked)
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(false);
            }
            else
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(true);
            }

            if (socket.IsEmpty)
            {
                if (_gemIcon != null) _gemIcon.enabled = false;
                if (_gemTypeColor != null) _gemTypeColor.enabled = false;
                if (_gemLevelText != null) _gemLevelText.enabled = false;
            }
            else
            {
                var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
                if (gemData != null)
                {
                    if (_gemIcon != null && gemData.Icon != null)
                    {
                        _gemIcon.sprite = gemData.Icon;
                        _gemIcon.enabled = true;
                    }
                    if (_gemTypeColor != null)
                    {
                        _gemTypeColor.color = gemData.GemColor;
                        _gemTypeColor.enabled = true;
                    }
                    if (_gemLevelText != null)
                    {
                        _gemLevelText.text = $"Lv.{socket.GemLevel}";
                        _gemLevelText.enabled = true;
                    }
                }
            }
        }
    }

    public class EquipmentComparisonStatUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _currentValueText;
        [SerializeField] private TextMeshProUGUI _newValueText;
        [SerializeField] private TextMeshProUGUI _differenceText;

        public void Initialize(StatComparison comp)
        {
            if (_statNameText != null) _statNameText.text = comp.Stat.GetShortName();
            if (_currentValueText != null) _currentValueText.text = comp.CurrentValue.ToString("F1");
            if (_newValueText != null) _newValueText.text = comp.NewValue.ToString("F1");
            if (_differenceText != null)
            {
                string sign = comp.Difference >= 0 ? "+" : "";
                _differenceText.text = $"{sign}{comp.Difference:F1} ({comp.PercentChange:+0.0;-0.0}%)";
                _differenceText.color = comp.Difference >= 0 ? Color.green : Color.red;
            }
        }
    }
}