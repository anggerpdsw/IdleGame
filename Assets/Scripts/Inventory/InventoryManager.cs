using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Facade over InventoryService for legacy callers (CardManager, DailyRewardService).
    /// Single source of truth for items is InventoryService slots.
    /// Keep this class so old callers compile unchanged, but storage lives in InventoryService.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager _instance;
        public static InventoryManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AddItem(string itemId, long amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;

            // ---- Guard: equipment must not use this path ----
            var data = ItemDatabase.Instance?.GetItem(itemId);
            if (data != null && data.Category == ItemCategory.Equipment)
            {
                Debug.LogError(
                    $"[InventoryManager] AddItem cannot be used for equipment '{itemId}'. " +
                    "Generated equipment must be added via InventoryService.AddItemInstance(generatedItem) or AddGeneratedItem()."
                );
                return;
            }
            // ------------------------------------------------

            InventoryService.Instance?.AddItem(itemId, (int)amount);
        }

        public bool ConsumeItem(string itemId, long amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            return InventoryService.Instance != null && InventoryService.Instance.RemoveItemById(itemId, (int)amount) >= amount;
        }

        public long GetItemCount(string itemId)
        {
            return InventoryService.Instance?.GetTotalQuantity(itemId) ?? 0L;
        }

        public Dictionary<string, long> GetSaveData()
        {
            var save = InventoryService.Instance?.GetSaveData();
            return save != null ? AggregateById(save) : new Dictionary<string, long>();
        }

        public void LoadInventory(Dictionary<string, long> savedItems)
        {
            // No-op: InventoryService is the storage now. Kept for API compatibility.
        }

        private static Dictionary<string, long> AggregateById(InventorySaveData save)
        {
            var map = new Dictionary<string, long>();
            foreach (var data in save.Items ?? Array.Empty<InventoryItemData>())
            {
                if (data == null) continue;
                if (!map.TryGetValue(data.ItemId, out var cur)) cur = 0;
                map[data.ItemId] = cur + data.Quantity;
            }
            return map;
        }

        //----------------------------------------------------
        // Scene Navigation
        //----------------------------------------------------
        public void OpenInventory() => SceneLoader.Instance.LoadInventory();
    }
}