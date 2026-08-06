using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class SaveData
    {
        public int version = GameConstants.CURRENT_SAVE_VERSION;
        public long saveTimestamp = DateTime.Now.Ticks;
        public AccountData account;
        public CurrencyData currency;
        public SpendingData spending;
        public VipData vip;
        public GameStateData gameState;
        public WaveProgressData waveProgress;
        public IdleRewardData idleReward;
        public DailyRewardSaveData dailyReward;
        public Dictionary<string, long> inventory;

        // Card system data
        public CardInventoryData cardInventory;

        // New Inventory & Equipment systems
        public InventorySaveData inventoryData;
        public EquipmentSaveData equipmentData;

        // Crafting system
        public CraftQueueSaveData craftQueue;
    }
}