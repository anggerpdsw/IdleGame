
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Filter UI component for inventory.
    /// </summary>
    public class InventoryFilterUI : MonoBehaviour
    {
        [Header("Category Filters")]
        [SerializeField] private Toggle[] _categoryToggles;
        [SerializeField] private Toggle _allCategoriesToggle;

        [Header("ItemRarity Filters")]
        [SerializeField] private Toggle[] _rarityToggles;
        [SerializeField] private Toggle _allRaritiesToggle;

        [Header("Equipment Type Filters")]
        [SerializeField] private Toggle[] _equipmentTypeToggles;
        [SerializeField] private Toggle _allEquipmentTypesToggle;

        [Header("Other Filters")]
        [SerializeField] private Toggle _favoritesOnlyToggle;
        [SerializeField] private Toggle _lockedOnlyToggle;
        [SerializeField] private Toggle _newOnlyToggle;
        [SerializeField] private Toggle _equippableOnlyToggle;
        [SerializeField] private Toggle _brokenOnlyToggle;
        [SerializeField] private Toggle _stackableOnlyToggle;
        [SerializeField] private Toggle _hideEquippedToggle;
        [SerializeField] private Toggle _hideMaxStackToggle;
        [SerializeField] private InputField _minLevelInput;
        [SerializeField] private InputField _maxLevelInput;
        [SerializeField] private Button _clearFiltersButton;

        private InventoryFilter _currentFilter = new();

        public event Action<InventoryFilter> OnFilterChanged;

        private void Awake()
        {
            InitializeToggles();
        }

        private void InitializeToggles()
        {
            // Category toggles
            if (_categoryToggles != null)
            {
                foreach (var toggle in _categoryToggles)
                {
                    toggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
                }
            }
            if (_allCategoriesToggle != null)
                _allCategoriesToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());

            // ItemRarity toggles
            if (_rarityToggles != null)
            {
                foreach (var toggle in _rarityToggles)
                {
                    toggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
                }
            }
            if (_allRaritiesToggle != null)
                _allRaritiesToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());

            // Equipment type toggles
            if (_equipmentTypeToggles != null)
            {
                foreach (var toggle in _equipmentTypeToggles)
                {
                    toggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
                }
            }
            if (_allEquipmentTypesToggle != null)
                _allEquipmentTypesToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());

            // Other toggles
            if (_favoritesOnlyToggle != null)
                _favoritesOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_lockedOnlyToggle != null)
                _lockedOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_newOnlyToggle != null)
                _newOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_equippableOnlyToggle != null)
                _equippableOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_brokenOnlyToggle != null)
                _brokenOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_stackableOnlyToggle != null)
                _stackableOnlyToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_hideEquippedToggle != null)
                _hideEquippedToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_hideMaxStackToggle != null)
                _hideMaxStackToggle.onValueChanged.AddListener(_ => OnFilterChangedInternal());

            // Level inputs
            if (_minLevelInput != null)
                _minLevelInput.onValueChanged.AddListener(_ => OnFilterChangedInternal());
            if (_maxLevelInput != null)
                _maxLevelInput.onValueChanged.AddListener(_ => OnFilterChangedInternal());

            // Clear button
            if (_clearFiltersButton != null)
                _clearFiltersButton.onClick.AddListener(ClearFilters);
        }

        private void OnFilterChangedInternal()
        {
            _currentFilter = BuildFilter();
            OnFilterChanged?.Invoke(_currentFilter);
        }

        private InventoryFilter BuildFilter()
        {
            var filter = new InventoryFilter();

            // Categories
            if (_categoryToggles != null && _allCategoriesToggle != null && !_allCategoriesToggle.isOn)
            {
                var categories = new List<ItemCategory>();
                for (int i = 0; i < _categoryToggles.Length; i++)
                {
                    if (_categoryToggles[i].isOn)
                    {
                        categories.Add((ItemCategory)(i + 1)); // Assuming enum order matches toggle order
                    }
                }
                filter.Categories = categories.ToArray();
            }

            // Rarities
            if (_rarityToggles != null && _allRaritiesToggle != null && !_allRaritiesToggle.isOn)
            {
                var rarities = new List<ItemRarity>();
                for (int i = 0; i < _rarityToggles.Length; i++)
                {
                    if (_rarityToggles[i].isOn)
                    {
                        rarities.Add((ItemRarity)(i + 1));
                    }
                }
                filter.Rarities = rarities.ToArray();
            }

            // Equipment types
            if (_equipmentTypeToggles != null && _allEquipmentTypesToggle != null && !_allEquipmentTypesToggle.isOn)
            {
                var types = new List<EquipmentType>();
                for (int i = 0; i < _equipmentTypeToggles.Length; i++)
                {
                    if (_equipmentTypeToggles[i].isOn)
                    {
                        types.Add((EquipmentType)(i + 1));
                    }
                }
                filter.EquipmentTypes = types.ToArray();
            }

            // Other filters
            filter.OnlyFavorites = _favoritesOnlyToggle?.isOn ?? false;
            filter.OnlyLocked = _lockedOnlyToggle?.isOn ?? false;
            filter.OnlyNew = _newOnlyToggle?.isOn ?? false;
            filter.OnlyEquippable = _equippableOnlyToggle?.isOn ?? false;
            filter.OnlyBroken = _brokenOnlyToggle?.isOn ?? false;
            filter.OnlyStackable = _stackableOnlyToggle?.isOn ?? false;
            filter.HideEquipped = _hideEquippedToggle?.isOn ?? false;
            filter.HideMaxStack = _hideMaxStackToggle?.isOn ?? false;

            // Level range
            if (int.TryParse(_minLevelInput?.text, out int minLevel))
                filter.MinLevel = minLevel;
            if (int.TryParse(_maxLevelInput?.text, out int maxLevel))
                filter.MaxLevel = maxLevel;

            return filter;
        }

        public void SetFilter(InventoryFilter filter)
        {
            _currentFilter = filter;
            UpdateUIFromFilter(filter);
        }

        public void ClearFilters()
        {
            if (_allCategoriesToggle != null) _allCategoriesToggle.isOn = true;
            if (_allRaritiesToggle != null) _allRaritiesToggle.isOn = true;
            if (_allEquipmentTypesToggle != null) _allEquipmentTypesToggle.isOn = true;

            if (_favoritesOnlyToggle != null) _favoritesOnlyToggle.isOn = false;
            if (_lockedOnlyToggle != null) _lockedOnlyToggle.isOn = false;
            if (_newOnlyToggle != null) _newOnlyToggle.isOn = false;
            if (_equippableOnlyToggle != null) _equippableOnlyToggle.isOn = false;
            if (_brokenOnlyToggle != null) _brokenOnlyToggle.isOn = false;
            if (_stackableOnlyToggle != null) _stackableOnlyToggle.isOn = false;
            if (_hideEquippedToggle != null) _hideEquippedToggle.isOn = false;
            if (_hideMaxStackToggle != null) _hideMaxStackToggle.isOn = false;

            if (_minLevelInput != null) _minLevelInput.text = "";
            if (_maxLevelInput != null) _maxLevelInput.text = "";

            _currentFilter = new InventoryFilter();
            OnFilterChanged?.Invoke(_currentFilter);
        }

        private void UpdateUIFromFilter(InventoryFilter filter)
        {
            // Implementation would update toggle states based on filter
            // This is a simplified version
        }

        public InventoryFilter GetCurrentFilter() => _currentFilter;
    }
}