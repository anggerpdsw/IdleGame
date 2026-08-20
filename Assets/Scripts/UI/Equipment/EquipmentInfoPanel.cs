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
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _enhanceButton;

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

            // Level/Enhance
            if (_levelText != null)
                _levelText.text = $"Lv.{item.Level}";
            if (_enhanceText != null)
                _enhanceText.text = item.EnhanceLevel > 0 ? $"+{item.EnhanceLevel}" : "";

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
                if (entryObj.TryGetComponent<EquipmentStatEntryUI>(out var entryUI))
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
                        if (entryObj.TryGetComponent<EquipmentEffectEntryUI>(out var entryUI))
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
                if (entryObj.TryGetComponent<EquipmentSetBonusEntryUI>(out var entryUI))
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
                if (entryObj.TryGetComponent<EquipmentGemSocketUI>(out var entryUI))
                    entryUI.Initialize(socket);
            }
        }

        private void UpdateButtons(InventoryItem item)
        {
            var canEnhance = UpgradeService.Instance?.CanEnhance(item, out _) ?? false;
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
        private void OnUnequip() => EquipmentService.Instance?.Unequip(_currentItem.EquippedSlot);
        private void OnRepair() => RepairService.Instance?.RepairItem(_currentItem);
        private void OnSocketGem() { /* Open gem selection UI */ }
        private void OnRemoveGem() { /* Open gem removal UI */ }
        private void OnFavoriteChanged(bool isFavorite) => InventoryService.Instance?.SetFavorite(_currentItem.InstanceId, isFavorite);
        private void OnLockChanged(bool isLocked) => InventoryService.Instance?.SetLocked(_currentItem.InstanceId, isLocked);
    }

}