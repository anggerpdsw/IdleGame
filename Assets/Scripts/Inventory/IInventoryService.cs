using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Inventory
{
    /// <summary>
    /// Dirty flags for granular UI updates.
    /// </summary>
    [Flags]
    public enum DirtyType
    {
        None        = 0,
        Item        = 1 << 0,  // Item added/removed/replaced
        Stack       = 1 << 1,  // Quantity changed
        Durability  = 1 << 2,  // Durability changed
        Lock        = 1 << 3,  // Lock state changed
        Favorite    = 1 << 4,  // Favorite state changed
        Socket      = 1 << 5,  // Socket/gem changed
        Upgrade     = 1 << 6,  // Enhance/limit break/refine changed
        Cooldown    = 1 << 7,  // Cooldown changed
        Selection   = 1 << 8,  // Selection state changed
        Tooltip     = 1 << 9,  // Tooltip data changed
        All         = ~0       // All flags
    }

    /// <summary>
    /// Interface for inventory service - core inventory operations.
    /// </summary>
    public interface IInventoryService
    {
        // ============ Events ============
        event Action<InventoryChangedEventArgs> OnInventoryChanged;
        event Action<InventoryItem> OnItemAdded;
        event Action<InventoryItem> OnItemRemoved;
        event Action<InventoryItem, int> OnItemQuantityChanged;
        event Action<int> OnCapacityChanged;
        event Action OnInventoryFiltered;

        // ============ Properties ============
        int Capacity { get; }
        int UsedSlots { get; }
        int FreeSlots { get; }
        int MaxStackSize { get; }
        InventoryConfig Config { get; }
        IReadOnlyList<InventorySlot> Slots { get; }
        IReadOnlyList<InventoryItem> AllItems { get; }

        // ============ Core Operations ============
        /// <summary>Adds an item to inventory. Returns the instance ID of added item or empty if failed.</summary>
        string AddItem(string itemId, int quantity = 1, Dictionary<string, object> customData = null);

        /// <summary>Adds a specific inventory item instance.</summary>
        bool AddItemInstance(InventoryItem item);

        /// <summary>Removes an item by instance ID. Returns quantity actually removed.</summary>
        int RemoveItem(string instanceId, int quantity = 1);

        /// <summary>Removes items by item ID (not instance ID). Useful for consuming materials.</summary>
        int RemoveItemById(string itemId, int quantity);

        /// <summary>Moves item from one slot to another.</summary>
        bool MoveItem(int fromSlot, int toSlot);

        /// <summary>Swaps items between two slots.</summary>
        bool SwapItems(int slotA, int slotB);

        /// <summary>Splits a stack into two.</summary>
        InventoryItem SplitStack(string instanceId, int amount);

        /// <summary>Merges stacks of the same item.</summary>
        int MergeStacks();

        // ============ Query Operations ============
        /// <summary>Gets item by instance ID.</summary>
        InventoryItem GetItem(string instanceId);

        /// <summary>Gets item at slot index.</summary>
        InventoryItem GetItemAtSlot(int slotIndex);

        /// <summary>Gets all items matching item ID.</summary>
        IReadOnlyList<InventoryItem> GetItemsById(string itemId);

        /// <summary>Gets all items matching category.</summary>
        IReadOnlyList<InventoryItem> GetItemsByCategory(ItemCategory category);

        /// <summary>Gets all items matching rarity.</summary>
        IReadOnlyList<InventoryItem> GetItemsByRarity(Rarity rarity);

        /// <summary>Gets all equipment items.</summary>
        IReadOnlyList<InventoryItem> GetEquipments();

        /// <summary>Gets all equipment items of specific type.</summary>
        IReadOnlyList<InventoryItem> GetEquipmentsByType(EquipmentType type);

        /// <summary>Gets all stackable items that can be merged.</summary>
        IReadOnlyList<InventoryItem> GetMergeableStacks();

        /// <summary>Gets total quantity of an item by ID.</summary>
        int GetTotalQuantity(string itemId);

        /// <summary>Checks if inventory has space for item.</summary>
        bool HasSpaceFor(string itemId, int quantity = 1);

        /// <summary>Checks if inventory has specific item.</summary>
        bool HasItem(string itemId, int quantity = 1);

        // ============ Filtering ============
        void SetFilter(InventoryFilter filter);
        void ClearFilter();
        InventoryFilter GetCurrentFilter();

        // ============ Item Flags ============
        void SetFavorite(string instanceId, bool favorite);
        void SetLocked(string instanceId, bool locked);
        void MarkAsSeen(string instanceId);

        // ============ Capacity Management ============
        bool ExpandCapacity(int slots = 1);
        int GetExpansionCost();
        int GetMaxCapacity();

        // ============ Quick Actions ============
        long QuickSell(IEnumerable<string> instanceIds);
        long QuickSellByFilter(InventoryFilter filter);
        long QuickSellJunk(); // Sells Common non-favorite non-locked items
        long QuickSellAllExceptFavorites();

        // ============ Consumption & Usage ============
        bool ConsumeItem(string instanceId, int quantity = 1);
        bool UseItem(string instanceId); // For consumables, chests, skill books, etc.

        // ============ Destruction ============
        bool DestroyItem(string instanceId, int quantity = 1);
        int DestroyItemsByFilter(InventoryFilter filter);

        // ============ Persistence ============
        InventorySaveData GetSaveData();
        void LoadFromSaveData(InventorySaveData data);
        void Reset();

        // ============ Utility ============
        void ValidateIntegrity(); // Removes null/invalid items, fixes stack counts
        int CleanupEmptySlots(); // Compacts inventory, returns items moved

        // ============ Dirty Tracking ============
        /// <summary>
        /// Flushes dirty slots to UI subscribers. Call after batch operations.
        /// </summary>
        void FlushDirtySlots();

        /// <summary>
        /// Marks an item dirty by instance ID. Useful for cross-service notifications.
        /// </summary>
        void MarkItemDirty(string instanceId, DirtyType dirtyType);

        /// <summary>
        /// Marks an item dirty by instance ID with multiple flags.
        /// </summary>
        void MarkItemDirty(string instanceId, params DirtyType[] dirtyTypes);
    }

    /// <summary>
    /// Event arguments for inventory changes.
    /// </summary>
    public class InventoryChangedEventArgs : EventArgs
    {
        public InventoryChangeType ChangeType;
        public string InstanceId;
        public string ItemId;
        public int SlotIndex;
        public int QuantityChange;
        public InventoryItem Item;

        public static InventoryChangedEventArgs CreateAdded(string instanceId, string itemId, int slot, int qty, InventoryItem item) =>
            new() { ChangeType = InventoryChangeType.Added, InstanceId = instanceId, ItemId = itemId, SlotIndex = slot, QuantityChange = qty, Item = item };

        public static InventoryChangedEventArgs CreateRemoved(string instanceId, string itemId, int slot, int qty, InventoryItem item) =>
            new() { ChangeType = InventoryChangeType.Removed, InstanceId = instanceId, ItemId = itemId, SlotIndex = slot, QuantityChange = qty, Item = item };

        public static InventoryChangedEventArgs CreateQuantityChanged(string instanceId, string itemId, int slot, int delta, InventoryItem item) =>
            new() { ChangeType = InventoryChangeType.QuantityChanged, InstanceId = instanceId, ItemId = itemId, SlotIndex = slot, QuantityChange = delta, Item = item };

        public static InventoryChangedEventArgs CreateMoved(string instanceId, string itemId, int fromSlot, int toSlot, InventoryItem item) =>
            new() { ChangeType = InventoryChangeType.Moved, InstanceId = instanceId, ItemId = itemId, SlotIndex = toSlot, QuantityChange = fromSlot, Item = item };
    }

    public enum InventoryChangeType
    {
        Added = 0,
        Removed = 1,
        QuantityChanged = 2,
        Moved = 3,
        Swapped = 4,
        Sorted = 5,
        Filtered = 6,
        CapacityChanged = 7,
    }

    /// <summary>
    /// Filter configuration for inventory.
    /// </summary>
    [Serializable]
    public class InventoryFilter
    {
        public string SearchText;
        public ItemCategory[] Categories;
        public Rarity[] Rarities;
        public EquipmentType[] EquipmentTypes;
        public int MinLevel;
        public int MaxLevel;
        public bool OnlyFavorites;
        public bool OnlyLocked;
        public bool OnlyNew;
        public bool OnlyEquippable;
        public bool OnlyBroken;
        public bool OnlyStackable;
        public bool HideEquipped;
        public bool HideMaxStack;

        public bool Matches(InventoryItem item)
        {
            if (item == null) return false;
            if (!string.IsNullOrEmpty(SearchText))
            {
                // Would need ItemDatabase lookup for name
                // Implementation in service
            }
            if (Categories?.Length > 0 && !Array.Exists(Categories, c => c == item.GetItemCategory())) return false;
            if (Rarities?.Length > 0 && !Array.Exists(Rarities, r => r == item.GetRarity())) return false;
            if (EquipmentTypes?.Length > 0 && !Array.Exists(EquipmentTypes, t => t == item.GetEquipmentType())) return false;
            if (MinLevel > 0 && item.Level < MinLevel) return false;
            if (MaxLevel > 0 && item.Level > MaxLevel) return false;
            if (OnlyFavorites && !item.IsFavorite) return false;
            if (OnlyLocked && !item.IsLocked) return false;
            if (OnlyNew && !item.IsNew) return false;
            if (OnlyEquippable && !item.IsEquippable()) return false;
            if (OnlyBroken && !item.IsBroken) return false;
            if (OnlyStackable && !item.IsStackable) return false;
            if (HideEquipped && item.IsEquipped) return false;
            if (HideMaxStack && item.IsMaxStack) return false;
            return true;
        }

        public bool IsEmpty =>
            string.IsNullOrEmpty(SearchText) &&
            (Categories == null || Categories.Length == 0) &&
            (Rarities == null || Rarities.Length == 0) &&
            (EquipmentTypes == null || EquipmentTypes.Length == 0) &&
            MinLevel == 0 && MaxLevel == 0 &&
            !OnlyFavorites && !OnlyLocked && !OnlyNew &&
            !OnlyEquippable && !OnlyBroken && !OnlyStackable &&
            !HideEquipped && !HideMaxStack;

        public InventoryFilter Clone() => new()
        {
            SearchText = SearchText,
            Categories = Categories != null ? (ItemCategory[])Categories.Clone() : null,
            Rarities = Rarities != null ? (Rarity[])Rarities.Clone() : null,
            EquipmentTypes = EquipmentTypes != null ? (EquipmentType[])EquipmentTypes.Clone() : null,
            MinLevel = MinLevel,
            MaxLevel = MaxLevel,
            OnlyFavorites = OnlyFavorites,
            OnlyLocked = OnlyLocked,
            OnlyNew = OnlyNew,
            OnlyEquippable = OnlyEquippable,
            OnlyBroken = OnlyBroken,
            OnlyStackable = OnlyStackable,
            HideEquipped = HideEquipped,
            HideMaxStack = HideMaxStack
        };
    }

    /// <summary>
/// Save data for inventory.
/// Category is NOT persisted - it is derived from ItemId via ItemDatabase (presentation concern).
/// Only capacity, flat items with explicit slot position, and timestamp are stored.
/// </summary>
[Serializable]
public class InventorySaveData
{
    public int Capacity; // BaseCapacity + expansions
    public long LastModifiedTimestamp;
    /// <summary>Flat list, one entry per occupied slot, ordered by SlotIndex. Stackables + equipment.</summary>
    public InventoryItemData[] Items;
    /// <summary>Socketed gem instances (GemInstanceId-keyed). Runtime for socketed gems — never part of a stack.</summary>
    public GemInstanceData[] SocketedGems;

    // Migration helper: captures old "Config" field from v3 saves during deserialization.
    // Not written back on save (ShouldSerialize pattern).
    [Newtonsoft.Json.JsonProperty("Config")]
    public InventoryConfig LegacyConfig { get; set; }

    public bool ShouldSerializeLegacyConfig() => false;

    public static InventorySaveData CreateEmpty() => new()
    {
        Capacity = 48, // BaseCapacity from config
        LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Items = Array.Empty<InventoryItemData>(),
        SocketedGems = Array.Empty<GemInstanceData>()
    };
}

/// <summary>
/// A single inventory stack/instance as persisted. Slot position is explicit; category is derived.
/// Equipment fields are nullable: only equipment (unique instances) sets them;
/// stackables persist identity + quantity + flags only (missing = default on load).
/// InstanceId: equipment only. Stackables use Key = ItemId + StackId ('a'..'z', null for default).
/// </summary>
[Serializable]
public class InventoryItemData
{
    // Identity
    public string InstanceId; // Equipment only (unique instance). Null for stackables.
    public string ItemId;

    // Stack state
    public int Quantity = 1;

    // Slot position (global grid index 0..Capacity-1)
    public int SlotIndex;

    // ---- Stack identity (instanceId replacement for stackables) ----
    /// <summary>Runtime + persistence key for a stack entry (ItemId + StackId). Fallback covers
    /// legacy saves that used InstanceId as the key; always non-empty after a save.</summary>
    public string KeyId = "";
    /// <summary>'a'..'z' distinguishing stacks of the same item in different slots. Missing/null = the canonical stack.</summary>
    public string StackId;

    // ---- Equipment-only (unique instances) ----
    public int? Level;
    public int? EnhanceLevel;
    public int? LimitBreakCount;
    public int? RefineLevel;
    public int? TranscendLevel;
    public int? EvolutionStage;
    public bool? IsAwakened;
    public bool? IsMasterwork;
    public int? CurrentDurability; // MaxDurability derived from EquipmentData
    public SocketData[] Sockets; // { IsUnlocked, GemInstanceId }
    public EnchantmentInstanceData Enchantment;
    public Dictionary<string, object> CustomData; // rolled affixes/secondaries

    // ---- Flags (user-facing, persisted) ----
    public bool IsFavorite = false;
    public bool IsLocked = false;
    public bool IsNew = true;
    public long AcquiredTimestamp = 0;
}
}