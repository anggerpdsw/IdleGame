using System.Collections.Generic;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Info panel for selected inventory item.
    /// </summary>
    public class InventoryInfoPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemDescriptionText;
        [SerializeField] private TextMeshProUGUI _itemRarityText;
        [SerializeField] private Image _itemIconImage;
        [SerializeField] private TextMeshProUGUI _itemStatsText;
        [SerializeField] private TextMeshProUGUI _itemValueText;
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _splitButton;
        [SerializeField] private Button _destroyButton;
        [SerializeField] private Toggle _favoriteToggle;
        [SerializeField] private Toggle _lockToggle;
        [SerializeField] private Image _lockIcon;

        private InventoryItem _currentItem;
        private InventoryUI _parentUI;
        private const string ValueFormat = "0.##";

        public void Initialize(InventoryUI parentUI)
        {
            _parentUI = parentUI;
            if (_useButton != null)
                _useButton.onClick.AddListener(OnUse);
            if (_equipButton != null)
                _equipButton.onClick.AddListener(OnEquip);
            if (_sellButton != null)
                _sellButton.onClick.AddListener(OnSell);
            if (_splitButton != null)
                _splitButton.onClick.AddListener(OnSplit);
            if (_destroyButton != null)
                _destroyButton.onClick.AddListener(OnDestroy);
            if (_favoriteToggle != null)
                _favoriteToggle.onValueChanged.AddListener(OnFavoriteChanged);
            if (_lockToggle != null)
                _lockToggle.onValueChanged.AddListener(OnLockChanged);

            SubscribeEquipmentEvents();
            Hide();
        }

        private void SubscribeEquipmentEvents()
        {
            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnItemEquipped += OnItemEquipped;
                EquipmentService.Instance.OnItemUnequipped += OnItemUnequipped;
            }
        }

        private void OnDisable()
        {
            UnsubscribeEquipmentEvents();
        }

        private void UnsubscribeEquipmentEvents()
        {
            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnItemEquipped -= OnItemEquipped;
                EquipmentService.Instance.OnItemUnequipped -= OnItemUnequipped;
            }
        }

        private void OnItemEquipped(EquipmentType slot, InventoryItem item)
        {
            // Hide panel if the equipped item is the one currently displayed
            if (_currentItem != null && ReferenceEquals(_currentItem, item))
                Hide();
        }

        private void OnItemUnequipped(EquipmentType slot, InventoryItem item)
        {
            // Optional: could refresh panel if this item becomes selected again
        }

        public void ShowItem(InventoryItem item)
        {
            _currentItem = item;
            if (item == null)
            {
                Hide();
                return;
            }

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            if (_itemNameText != null)
            {
                _itemNameText.text = itemData.Name;
                _itemNameText.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_itemDescriptionText != null)
                _itemDescriptionText.text = itemData.Description;

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

            if (_itemStatsText != null)
            {
                // Build stats string from item
                _itemStatsText.text = BuildStatsString(item);
            }

            if (_itemValueText != null)
            {
                long sellPrice = ItemDatabase.Instance?.GetSellPrice(item.ItemId) ?? 0;
                _itemValueText.text = $"Sell: {sellPrice * item.Quantity:N0} Gold";
            }

            // Update button visibility
            if (_useButton != null)
                _useButton.gameObject.SetActive(itemData.IsConsumable());
            if (_equipButton != null)
                _equipButton.gameObject.SetActive(itemData.IsEquippable() && !item.IsEquipped);
            if (_sellButton != null)
                _sellButton.gameObject.SetActive(!item.IsEquipped && !item.IsLocked);
            if (_splitButton != null)
                _splitButton.gameObject.SetActive(item.Quantity > 1);
            if (_destroyButton != null)
                _destroyButton.gameObject.SetActive(!item.IsEquipped && !item.IsLocked);

            // Update toggles
            if (_favoriteToggle != null)
            {
                _favoriteToggle.isOn = item.IsFavorite;
                _favoriteToggle.interactable = !item.IsLocked;
            }
            if (_lockToggle != null && _lockIcon != null)
            {
                _lockToggle.isOn = item.IsLocked;
                RefreshIconLock(item.IsLocked);
            }
        }

        private string BuildStatsString(InventoryItem item)
        {
            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return "";

            var lines = new List<string>
            {
                $"Level: {item.Level}",
                $"Durability: {item.CurrentDurability}/{item.MaxDurability}"
            };

            if (itemData?.CombatStats != null)
            {
                foreach (var stat in itemData.CombatStats)
                {
                    float value = stat.GetValue(item.Level);
                    string sign = value >= 0 ? "+" : "";
                    lines.Add($"{stat.Stat.GetSkillDisplayName()}: {sign}{value:F2}");
                }
            }

            // Instance attributes from AttributeData (MainAttribute + SecondAttribute)
            if (item.AttributeData != null)
            {
                // Main Attributes (STR/CON/INT/DEX)
                if (item.AttributeData.MainAttribute != null)
                {
                    foreach (var attr in item.AttributeData.MainAttribute)
                    {
                        if (attr.BaseValue == 0f) continue;
                        string sign = attr.BaseValue >= 0 ? "+" : "";
                        lines.Add($"{attr.Attribute.GetMainDisplayName()}: {sign}{attr.BaseValue.ToString(ValueFormat)}");
                    }
                }

                // Second Attributes (specialization stats)
                if (item.AttributeData.SecondAttribute != null)
                {
                    foreach (var attr in item.AttributeData.SecondAttribute)
                    {
                        if (attr.BaseValue == 0f) continue;
                        var secStat = (SecondaryStat)(int)attr.Attribute;
                        if (secStat == SecondaryStat.None) continue;
                        string sign = attr.BaseValue >= 0 ? "+" : "";
                        lines.Add($"{secStat.GetSkillDisplayName()}: {sign}{attr.BaseValue.ToString(ValueFormat)}");
                    }
                }

            }

            return string.Join("\n", lines);
        }

        public void Hide()
        {
            _currentItem = null;
            gameObject.SetActive(false);
        }

        private void OnUse()
        {
            if (_currentItem != null)
                InventoryService.Instance?.UseItem(_currentItem.GetStackKey() ?? _currentItem.InstanceId);
        }

        private void OnEquip()
        {
            if (_currentItem != null)
                EquipmentService.Instance?.Equip(_currentItem);
        }

        private void OnSell()
        {
            if (_currentItem != null)
            {
                long price = ItemDatabase.Instance?.GetSellPrice(_currentItem.ItemId) ?? 0;
                price *= _currentItem.Quantity;
                InventoryService.Instance?.RemoveItem(_currentItem.GetStackKey() ?? _currentItem.InstanceId, _currentItem.Quantity);
                EconomyManager.Instance?.AddCurrency(CurrencyType.Gold, price);
            }
        }

        private void OnSplit()
        {
            if (_currentItem != null && _currentItem.Quantity > 1)
            {
                // Show split dialog - for now split in half
                int half = _currentItem.Quantity / 2;
                var splitItem = InventoryService.Instance?.SplitStack(_currentItem.GetStackKey() ?? _currentItem.InstanceId, half);
            }
        }

        private void OnDestroy()
        {
            // Do not destroy item - panel only displays it, doesn't own it.
        }

        private void OnFavoriteChanged(bool isFavorite)
        {
            if (_currentItem != null)
                InventoryService.Instance?.SetFavorite(_currentItem.GetStackKey() ?? _currentItem.InstanceId, isFavorite);
        }

        private void OnLockChanged(bool isLocked)
        {
            if (_currentItem != null)
            {
                InventoryService.Instance?.SetLocked(_currentItem.GetStackKey() ?? _currentItem.InstanceId, isLocked);
                // Refresh button visibility (sell/destroy buttons depend on IsLocked)
                _currentItem.IsLocked = isLocked;
                UpdateButtonVisibility(_currentItem);
            }
            RefreshIconLock(isLocked);
        }

        private void UpdateButtonVisibility(InventoryItem item)
        {
            if (_sellButton != null)
                _sellButton.gameObject.SetActive(!item.IsEquipped && !item.IsLocked);
            if (_destroyButton != null)
                _destroyButton.gameObject.SetActive(!item.IsEquipped && !item.IsLocked);
        }

        private void RefreshIconLock(bool isLocked)
        {
            _lockIcon.sprite = isLocked ? ItemResources.GetItemSource("lock_unlock/lock") : ItemResources.GetItemSource("lock_unlock/unlock");
        }
    }
}