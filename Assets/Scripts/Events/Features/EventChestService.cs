using System;
using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Events.Domain;

namespace IdleDefenseSurvival.Events.Features
{
    /// <summary>
    /// Event chest gacha service with pity system.
    /// </summary>
    public static class EventChestService
    {
        /// <summary>
        /// Roll event chest. Returns reward type.
        /// </summary>
        public static ChestReward RollChest()
        {
            var eventService = ServiceLocator.EventService;
            if (eventService == null) return null;

            var evt = eventService.GetActiveEvent();
            if (evt?.chest == null) return null;

            var saveData = (eventService as EventService)?.GetSaveData();
            if (saveData == null) return null;

            // Increment pity counter
            saveData.chestPity++;

            // Check pity threshold
            bool guaranteedRelic = saveData.chestPity >= evt.chest.pity;

            // Generate reward
            var reward = GenerateReward(evt, guaranteedRelic);

            // Reset pity if relic obtained
            if (reward.type == "EventRelic")
            {
                saveData.chestPity = 0;
            }

            // Grant reward
            GrantReward(reward);

            // Save
            Manager.SaveManager.Instance?.SaveAll();

            Debug.Log($"[EventChestService] Rolled chest: {reward.type} x{reward.amount}, pity={saveData.chestPity}");
            return reward;
        }

        private static ChestReward GenerateReward(EventDefinition evt, bool guaranteedRelic)
        {
            if (guaranteedRelic)
            {
                // Guaranteed relic at pity
                return new ChestReward
                {
                    type = "EventRelic",
                    itemId = evt.rewards?.relics?[0] ?? "default_relic",
                    amount = 1
                };
            }

            // Random roll (simplified - real implementation would use weighted table)
            float roll = UnityEngine.Random.value;
            if (roll < 0.05f) // 5% relic
            {
                return new ChestReward
                {
                    type = "EventRelic",
                    itemId = evt.rewards?.relics?[0] ?? "default_relic",
                    amount = 1
                };
            }
            else if (roll < 0.25f) // 20% gem
            {
                return new ChestReward
                {
                    type = "Gem",
                    amount = UnityEngine.Random.Range(10, 50)
                };
            }
            else if (roll < 0.60f) // 35% gold
            {
                return new ChestReward
                {
                    type = "Gold",
                    amount = UnityEngine.Random.Range(5000, 20000)
                };
            }
            else // 40% event currency
            {
                return new ChestReward
                {
                    type = "EventCurrency",
                    amount = UnityEngine.Random.Range(100, 500)
                };
            }
        }

        private static void GrantReward(ChestReward reward)
        {
            switch (reward.type)
            {
                case "EventRelic":
                    ServiceLocator.InventoryService?.AddItem(reward.itemId, (int)reward.amount);
                    break;
                case "Gold":
                    ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gold, reward.amount, "EventChest");
                    break;
                case "Gem":
                    ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gem, reward.amount, "EventChest");
                    break;
                case "EventCurrency":
                    ServiceLocator.EventService?.AddEventCurrency(reward.amount);
                    break;
            }
        }

        /// <summary>
        /// Get current pity counter.
        /// </summary>
        public static int GetPityCount()
        {
            var eventService = ServiceLocator.EventService;
            if (eventService == null) return 0;

            var saveData = (eventService as EventService)?.GetSaveData();
            return saveData?.chestPity ?? 0;
        }
    }

    [Serializable]
    public class ChestReward
    {
        public string type;
        public string itemId;
        public long amount;
    }
}
