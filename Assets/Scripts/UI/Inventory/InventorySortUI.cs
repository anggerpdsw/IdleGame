using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Sort UI component for inventory.
    /// </summary>
    public class InventorySortUI : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown _sortDropdown;
        [SerializeField] private Toggle _ascendingToggle;

        public event Action<InventorySortType, bool> OnSortChanged;

        private void Awake()
        {
            if (_sortDropdown != null)
            {
                _sortDropdown.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();
                foreach (InventorySortType type in Enum.GetValues(typeof(InventorySortType)))
                {
                    if (type != InventorySortType.None)
                        options.Add(new TMP_Dropdown.OptionData(type.ToString()));
                }
                _sortDropdown.AddOptions(options);
                _sortDropdown.onValueChanged.AddListener(_ => OnSortChangedInternal());
            }

            if (_ascendingToggle != null)
                _ascendingToggle.onValueChanged.AddListener(_ => OnSortChangedInternal());
        }

        private void OnSortChangedInternal()
        {
            if (_sortDropdown == null) return;

            InventorySortType sortType = (InventorySortType)(_sortDropdown.value + 1);
            bool ascending = _ascendingToggle?.isOn ?? true;

            OnSortChanged?.Invoke(sortType, ascending);
        }

        public void SetSort(InventorySortType type, bool ascending)
        {
            if (_sortDropdown != null)
                _sortDropdown.value = (int)type - 1;
            if (_ascendingToggle != null)
                _ascendingToggle.isOn = ascending;
        }
    }

}