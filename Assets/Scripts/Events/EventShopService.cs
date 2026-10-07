using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Events
{
    /// <summary>
    /// Event Shop transaction service.
    /// Validates purchases, spends event currency, grants items.
    /// </summary>
    public static class EventShopService
    {
        /// <summary>
        /// Purchase item from event shop.
        /// Returns true if purchase successful.
        /// </summary>
        public static bool PurchaseItem(string itemId, long price)
        {
            var eventService = ServiceLocator.EventService;
            if (eventService == null) return false;

            var evt = eventService.GetActiveEvent();
            if (evt?.shop?.items == null) return false;

            // 1. Validate item exists in shop
            bool itemFound = false;
            long itemPrice = 0;
            foreach (var shopItem in evt.shop.items)
            {
                if (shopItem.itemId == itemId)
                {
                    itemFound = true;
                    itemPrice = shopItem.price;
                    break;
                }
            }

            if (!itemFound)
            {
                Debug.LogWarning($"[EventShopService] Item '{itemId}' not found in shop.");
                return false;
            }

            // 2. Check already purchased
            var saveData = (eventService as EventService)?.GetSaveData();
            if (saveData?.shopPurchases?.Contains(itemId) == true)
            {
                Debug.LogWarning($"[EventShopService] Item '{itemId}' already purchased.");
                return false;
            }

            // 3. Check currency
            if (saveData == null || saveData.eventCurrency < itemPrice)
            {
                Debug.LogWarning($"[EventShopService] Insufficient currency. Need {itemPrice}, have {saveData?.eventCurrency ?? 0}");
                return false;
            }

            // 4. Spend currency
            if (!eventService.SpendEventCurrency(itemPrice))
            {
                return false;
            }

            // 5. Grant item via InventoryService
            var inventory = ServiceLocator.InventoryService;
            inventory?.AddItem(itemId, 1);

            // 6. Record purchase
            saveData?.shopPurchases?.Add(itemId);

            // 7. Save
            Manager.SaveManager.Instance?.SaveAll();

            Debug.Log($"[EventShopService] Purchased '{itemId}' for {itemPrice} event currency.");
            return true;
        }

        /// <summary>
        /// Check if item already purchased.
        /// </summary>
        public static bool IsPurchased(string itemId)
        {
            var eventService = ServiceLocator.EventService;
            if (eventService == null) return false;

            var saveData = (eventService as EventService)?.GetSaveData();
            return saveData?.shopPurchases?.Contains(itemId) == true;
        }

        /// <summary>
        /// Get shop items for current event.
        /// </summary>
        public static EventShopItem[] GetShopItems()
        {
            var eventService = ServiceLocator.EventService;
            if (eventService == null) return null;

            var evt = eventService.GetActiveEvent();
            return evt?.shop?.items;
        }
    }
}
