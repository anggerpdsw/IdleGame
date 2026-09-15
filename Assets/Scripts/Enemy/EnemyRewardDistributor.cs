using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Item;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Handles enemy reward distribution (gold/exp instant, gem/meat pickup spawn).
    /// Also handles material drop rolls from EnemyData.dropItems.
    /// Stateless service - all state passed as parameters.
    /// </summary>
    public static class EnemyRewardDistributor
    {
        /// <summary>
        /// Drop currency rewards on enemy death.
        /// Gold/EXP: instant (via RewardManager).
        /// Gem: physical pickup (respects daily limit at death time).
        /// Meat: physical pickup.
        /// </summary>
        public static void DropRewards(
            long goldReward,
            long gemReward,
            long meatReward,
            int expReward,
            Vector3 deathPosition,
            GameObject itemPrefab,
            string enemyName,
            SaveManager saveManager,
            EconomyManager economyManager)
        {
            if (economyManager == null) return;

            // Gold & Exp: instant add (no pickup)
            RewardManager.Instance.GiveEnemyReward(goldReward, expReward, enemyName);

            // Gem: spawn physical pickup with re-check of daily limit
            if (gemReward > 0 && saveManager != null)
            {
                // Re-check if daily limit reached at death time
                if (!saveManager.HasReachedDailyGemLimit())
                {
                    // Record gem drop and spawn (re-enforce limit)
                    int actualGems = saveManager.RecordGemDrop(1);
                    if (actualGems > 0)
                        SpawnCurrencyItem(CurrencyType.Gem, actualGems, deathPosition, itemPrefab, economyManager);
                }
            }

            // Meat: spawn physical pickup
            if (meatReward > 0)
                SpawnCurrencyItem(CurrencyType.Meat, meatReward, deathPosition, itemPrefab, economyManager);
        }

        /// <summary>
        /// Spawn currency pickup item at death position.
        /// Item handles spread animation and magnetic collection.
        /// </summary>
        private static void SpawnCurrencyItem(
            CurrencyType currencyType,
            long amount,
            Vector3 position,
            GameObject itemPrefab,
            EconomyManager economyManager)
        {
            if (economyManager == null) return;

            if (itemPrefab == null)
            {
                Debug.LogWarning($"[EnemyRewardDistributor] Item prefab not assigned! Adding {currencyType} directly.");
                economyManager.AddCurrency(currencyType, amount, "Enemy drop (no prefab)");
                return;
            }

            GameObject itemObj = Object.Instantiate(
                itemPrefab,
                position,
                Quaternion.identity,
                UIManager.Instance.DropRoot);

            if (itemObj.TryGetComponent<CurrencyPickup>(out var item))
            {
                item.Initialize(currencyType, amount);
            }
            else
            {
                Debug.LogError($"[EnemyRewardDistributor] Item prefab missing CurrencyPickup component!");
                Object.Destroy(itemObj);
            }
        }

        /// <summary>
        /// Roll material drops from EnemyData.dropItems and grant to inventory.
        /// Each entry rolls independently. Weight is percent (0-100).
        /// Normal (non-boss) enemies capped at 2 successful drops to prevent flooding.
        /// </summary>
        public static void DropMaterialItems(EnemyData enemyData)
        {
            if (enemyData?.dropItems == null || enemyData.dropItems.Length == 0) return;

            var inventory = InventoryService.Instance;
            if (inventory == null) return;

            int currentTier = WaveManager.Instance?.CurrentTier ?? 1;
            int droppedCount = 0;

            foreach (var entry in enemyData.dropItems)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;

                // Tier gate: material must be unlocked at current tier
                if (entry.MinTier > currentTier) continue;

                // Roll drop chance with DropRate modifier
                float finalWeight = Utilityku.DropRateIncrease(entry.Weight);
                if (!Utilityku.Chance(finalWeight)) continue;

                int min = Mathf.Max(1, entry.MinCount);
                int max = Mathf.Max(min, entry.MaxCount);
                int quantity = Random.Range(min, max + 1);

                // Validate item exists
                if (ItemDatabase.Instance != null &&
                    ItemDatabase.Instance.GetItem(entry.ItemId) == null)
                {
                    Debug.LogWarning($"[EnemyRewardDistributor] Drop item not found: {entry.ItemId}");
                    continue;
                }

                inventory.AddItem(entry.ItemId, quantity);
                droppedCount++;

                // Record in drop bag ONLY after successful add
                if (DropBagManager.Instance != null)
                    DropBagManager.Instance.AddDrop(entry.ItemId, quantity);

                // Cap normal enemies at 2 item drops; bosses keep full potential
                if (!enemyData.IsBoss && droppedCount >= 2) break;
            }
        }
    }
}
