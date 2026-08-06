using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Paper-doll slot: shows equipped item icon in scene art slot.
    /// </summary>
    public class InventoryDollSlot : MonoBehaviour
    {
        [SerializeField] private EquipmentType _type;
        [SerializeField] private Image _icon;

        private void OnDestroy()
        {
            if (EquipmentService.Instance != null)
                EquipmentService.Instance.OnEquipmentChanged -= OnEquipChanged;
        }

        private void OnEquipChanged(EquipmentChangedEventArgs args) => Refresh();

        private void Refresh()
        {
            var item = EquipmentService.Instance?.EquippedItems.GetValueOrDefault(_type);
            if (item == null)
            {
                _icon.sprite = null;
                _icon.enabled = false;
                return;
            }
            var data = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (data?.Icon != null)
            {
                _icon.sprite = data.Icon;
                _icon.enabled = true;
            }
        }
    }

}