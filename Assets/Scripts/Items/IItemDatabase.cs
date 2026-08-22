using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Interface for item database - provides access to item definitions.
    /// </summary>
    public interface IItemDatabase
    {
        // ============ Events ============
        event Action OnDatabaseLoaded;
        event Action<string> OnItemDataAdded;
        event Action<string> OnItemDataRemoved;

        // ============ Properties ============
        bool IsLoaded { get; }
        int ItemCount { get; }
        IReadOnlyDictionary<string, ItemData> AllItems { get; }
        IReadOnlyDictionary<string, EquipmentData> AllEquipment { get; }
        IReadOnlyDictionary<string, GemData> AllGems { get; }
        IReadOnlyDictionary<string, SetBonusData> AllSets { get; }

        // ============ Lookup ============
        ItemData GetItem(string itemId);
        EquipmentData GetEquipment(string itemId);
        GemData GetGem(string gemId);
        SetBonusData GetSet(string setId);

        bool TryGetItem(string itemId, out ItemData item);
        bool TryGetEquipment(string itemId, out EquipmentData equipment);
        bool TryGetGem(string gemId, out GemData gem);
        bool TryGetSet(string setId, out SetBonusData set);

        // ============ Queries ============
        IReadOnlyList<ItemData> GetItemsByCategory(ItemCategory category);
        IReadOnlyList<ItemData> GetItemsByRarity(Rarity rarity);
        IReadOnlyList<EquipmentData> GetEquipmentByType(EquipmentType type);
        IReadOnlyList<EquipmentData> GetEquipmentBySlot(EquipmentType slot);
        IReadOnlyList<EquipmentData> GetEquipmentBySet(string setId);
        IReadOnlyList<GemData> GetGemsByType(GemType type);
        IReadOnlyList<ItemData> SearchItems(string searchText);

        // ============ Item Properties ============
        int GetMaxStackSize(string itemId);
        int GetMaxLevel(string itemId);
        int GetMaxLimitBreak(string itemId);
        int GetMaxSockets(string itemId);
        // GemType[] GetAllowedGemTypes(string itemId); // Moved to SocketConfigData.SocketRules
        long GetSellPrice(string itemId);
        long GetBuyPrice(string itemId);
        int GetBaseDurability(string itemId);
        long GetRepairCostPerDurability(string itemId);
        ItemLevelType[] GetSupportedLevelTypes(string itemId);

        // ============ Validation ============
        bool IsValidItemId(string itemId);
        bool IsEquipment(string itemId);
        bool IsStackable(string itemId);
        bool IsConsumable(string itemId);
        bool HasSockets(string itemId);

        // ============ Initialization ============
        void Initialize();
        void LoadFromResources(); // Load from Resources/Data/
        void RegisterItem(ItemData item);
        void UnregisterItem(string itemId);

        // ============ Runtime Generation ============
        EquipmentData GenerateEquipment(string baseId, Rarity rarity, int level, EquipmentType type);
        GemData GenerateGem(GemType type, Rarity rarity, int level);
    }
}