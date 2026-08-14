using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Items.Tests
{
    /// <summary>
    /// Shared mock implementations for crafting EditMode tests.
    /// </summary>
    internal static class CraftTestMocks
    {
        /// <summary>
        /// Mock IInventoryService for EditMode testing.
        /// Supports configurable ApplyReward, RemoveItemById, and call tracking.
        /// </summary>
        internal class MockInventoryService : IInventoryService
        {
            public Func<InventoryItem, string, ApplyResult> ApplyRewardFunc = (item, opId) => ApplyResult.Success;
            public int SuccessfulAddCount = 0;
            public Dictionary<string, int> RemoveItemByIdResults = new();
            public Dictionary<string, int> RemoveCallCount = new();

            public ApplyResult ApplyReward(InventoryItem item, string rewardOperationId)
            {
                var result = ApplyRewardFunc(item, rewardOperationId);
                if (result == ApplyResult.Success) SuccessfulAddCount++;
                return result;
            }

            public bool HasAppliedOperation(string rewardOperationId) => false;
            public IReadOnlyCollection<string> GetAppliedRewardOperationIds() => Array.Empty<string>();

            public int RemoveItemById(string itemId, int quantity)
            {
                if (!RemoveCallCount.ContainsKey(itemId)) RemoveCallCount[itemId] = 0;
                RemoveCallCount[itemId]++;
                if (RemoveItemByIdResults.TryGetValue(itemId, out var result))
                {
                    if (result < 0) throw new InvalidOperationException($"Mock removal failed for {itemId}");
                    return result;
                }
                return quantity;
            }

            // Minimal interface implementations
            public int Capacity => 100;
            public int UsedSlots => 0;
            public int FreeSlots => 100;
            public int MaxStackSize => 999;
            public InventoryConfig Config => null;
            public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
            public IReadOnlyList<InventoryItem> AllItems => Array.Empty<InventoryItem>();
            public event Action<InventoryChangedEventArgs> OnInventoryChanged;
            public event Action<InventoryItem> OnItemAdded;
            public event Action<InventoryItem> OnItemRemoved;
            public event Action<InventoryItem, int> OnItemQuantityChanged;
            public event Action<int> OnCapacityChanged;
            public event Action OnInventoryFiltered;

            public string AddItem(string itemId, int quantity = 1, Dictionary<string, object> customData = null) => Guid.NewGuid().ToString();
            public bool AddItemInstance(InventoryItem item) => true;
            public int RemoveItem(string instanceId, int quantity = 1) => quantity;
            public bool MoveItem(int fromSlot, int toSlot) => true;
            public bool SwapItems(int slotA, int slotB) => true;
            public InventoryItem SplitStack(string instanceId, int amount) => null;
            public int MergeStacks() => 0;
            public InventoryItem GetItem(string instanceId) => null;
            public InventoryItem GetItemAtSlot(int slotIndex) => null;
            public IReadOnlyList<InventoryItem> GetItemsById(string itemId) => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetItemsByCategory(ItemCategory category) => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetItemsByRarity(Rarity rarity) => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetEquipments() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetEquipmentsByType(EquipmentType type) => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetMergeableStacks() => Array.Empty<InventoryItem>();
            public int GetTotalQuantity(string itemId) => 999;
            public bool HasSpaceFor(string itemId, int quantity = 1) => true;
            public bool HasItem(string itemId, int quantity = 1) => true;
            public void SetFilter(InventoryFilter filter) { }
            public void ClearFilter() { }
            public InventoryFilter GetCurrentFilter() => null;
            public void SetFavorite(string instanceId, bool favorite) { }
            public void SetLocked(string instanceId, bool locked) { }
            public void MarkAsSeen(string instanceId) { }
            public bool ExpandCapacity(int slots = 1) => true;
            public int GetExpansionCost() => 0;
            public int GetMaxCapacity() => 100;
            public long QuickSell(IEnumerable<string> instanceIds) => 0;
            public long QuickSellByFilter(InventoryFilter filter) => 0;
            public long QuickSellJunk() => 0;
            public long QuickSellAllExceptFavorites() => 0;
            public bool ConsumeItem(string instanceId, int quantity = 1) => true;
            public bool UseItem(string instanceId) => true;
            public bool DestroyItem(string instanceId, int quantity = 1) => true;
            public int DestroyItemsByFilter(InventoryFilter filter) => 0;
            public InventorySaveData GetSaveData() => InventorySaveData.CreateEmpty();
            public void LoadFromSaveData(InventorySaveData data) { }
            public void Reset() { }
            public void ValidateIntegrity() { }
            public int CleanupEmptySlots() => 0;
            public void FlushDirtySlots() { }
            public void MarkItemDirty(string instanceId, DirtyType dirtyType) { }
            public void MarkItemDirty(string instanceId, params DirtyType[] dirtyTypes) { }
        }

        /// <summary>
        /// Mock IEconomyService for EditMode testing.
        /// </summary>
        internal class MockEconomyService : IEconomyService
        {
            public bool TrySpendCurrencyResult = true;
            public int SpendCallCount = 0;
            public CurrencyType LastSpendCurrency;
            public long LastSpendAmount;
            private CurrencyData _currencyData = new CurrencyData();

            public long Gold => _currencyData.Get(CurrencyType.Gold);
            public long Gem => _currencyData.Get(CurrencyType.Gem);
            public long Meat => _currencyData.Get(CurrencyType.Meat);

            public bool TrySpendCurrency(CurrencyType currency, long amount, string reason = "")
            {
                SpendCallCount++;
                LastSpendCurrency = currency;
                LastSpendAmount = amount;
                return TrySpendCurrencyResult;
            }

            public bool HasEnoughCurrency(CurrencyType currency, long amount) => true;
            public void AddCurrency(CurrencyType currency, long amount, string reason = "") { }
            public long GetCurrency(CurrencyType currency) => 999999;
            public void SetCurrency(CurrencyType currency, long amount) { }
            public CurrencyData GetCurrencyData() => _currencyData;
            public void SetCurrencyData(CurrencyData data) => _currencyData = data;
            public event Action<CurrencyType, long, long> OnCurrencyChanged;
        }

        /// <summary>
        /// Mock ISaveService for EditMode testing.
        /// </summary>
        internal class MockSaveService : ISaveService
        {
            public int PersistCallCount = 0;
            public void PersistCurrentStateDurably() => PersistCallCount++;
            public void RegisterJournal(CraftTransactionJournal journal) { }
            public void RegisterService(object service) { }
            public void SaveNow() { }

            // ISaveService required members
            public void SaveAll() { }
            public void LoadAll() { }
            public void DeleteAll() { }
            public int GetHighestWave(int tier) => 0;
            public void UpdateHighestWave(int tier, int wave) { }
            public bool IsTierUnlocked(int tier) => true;
            public void RecordEnemyKill(string enemyId, string damageSource, string role) { }
            public bool HasReachedDailyGemLimit() => false;
            public int GetRemainingDailyGems() => 20;
            public int RecordGemDrop(int gemCount) => gemCount;
            public int GetTodaysGemEarnings() => 0;
            public void ResetDailyGemCounter() { }
            public void SetAutoCollect(bool enabled) { }
            public bool IsAutoCollectEnabled() => false;
            public void SetMaxSpeed(bool enabled) { }
            public bool IsMaxSpeedEnabled() => false;
            public void AddSpending(CurrencyType type, long amount) { }
            public void AddEarn(CurrencyType type, long amount) { }
        }

        /// <summary>
        /// Mock random provider for deterministic rolls.
        /// </summary>
        internal class TestRandomProvider : IRandomProvider
        {
            private int _nextInt = 1;
            public int NextInt(int minInclusive, int maxExclusive) => _nextInt++;
            public int NextInt(int maxExclusive) => _nextInt++;
            public float NextFloat() => 0.5f;
            public double NextDouble() => 0.5;
            public bool Chance(float probability) => probability > 0.5f;
            public bool ChancePercent(float percent) => percent > 50f;
            public float Range(float min, float max) => 0.5f;
            public int Range(int minInclusive, int maxExclusive) => _nextInt++;
            public T Choice<T>(T[] array) => array.Length > 0 ? array[0] : default;
            public T Choice<T>(System.Collections.Generic.IReadOnlyList<T> list) => list.Count > 0 ? list[0] : default;
            public void Shuffle<T>(T[] array) { }
            public void Shuffle<T>(System.Collections.Generic.IList<T> list) { }
        }
    }
}