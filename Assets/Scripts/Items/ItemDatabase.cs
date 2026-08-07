using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items.Generation;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Item database implementation - central repository for all item definitions.
    /// Loads from Resources/Data/ and supports runtime registration.
    /// </summary>
    public sealed class ItemDatabase : MonoBehaviour, IItemDatabase
    {
        #region Singleton
        private static ItemDatabase _instance;
        public static ItemDatabase Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

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
        #endregion

        #region Events
        public event Action OnDatabaseLoaded;
        public event Action<string> OnItemDataAdded;
        public event Action<string> OnItemDataRemoved;
        #endregion

        #region Fields
        private readonly Dictionary<string, ItemData> _items = new();
        private readonly Dictionary<string, EquipmentData> _equipment = new();
        private readonly Dictionary<string, GemData> _gems = new();
        private readonly Dictionary<string, SetBonusData> _sets = new();
        private readonly Dictionary<string, AffixData> _affixes = new();
        private bool _isLoaded = false;
        #endregion

        #region Properties
        public bool IsLoaded => _isLoaded;
        public int ItemCount => _items.Count;
        public IReadOnlyDictionary<string, ItemData> AllItems => _items;
        public IReadOnlyDictionary<string, EquipmentData> AllEquipment => _equipment;
        public IReadOnlyDictionary<string, GemData> AllGems => _gems;
        public IReadOnlyDictionary<string, SetBonusData> AllSets => _sets;
        public IReadOnlyDictionary<string, AffixData> AllAffixes => _affixes;
        #endregion

        #region Initialization
        private void Start()
        {
            if (!_isLoaded) LoadFromResources();
        }

        public void Initialize()
        {
            if (!_isLoaded) LoadFromResources();
        }

        public void LoadFromResources()
        {
            // Single file: all item types (items, equipment, gems, sets) live in dataItems.json
            var jsonAsset = Resources.Load<TextAsset>("Data/dataItems");
            if (jsonAsset == null)
            {
                Debug.LogWarning("[ItemDatabase] No item data found at Data/dataItems");
                return;
            }

            try
            {
                var container = JsonConvert.DeserializeObject<AllItemDataContainer>(jsonAsset.text);

                if (container?.Items != null)
                    foreach (var item in container.Items) RegisterItem(item);

                if (container?.Equipment != null)
                    foreach (var equip in container.Equipment) RegisterItem(equip);

                if (container?.Gems != null)
                    foreach (var gem in container.Gems) RegisterGem(gem);

                if (container?.Sets != null)
                    foreach (var set in container.Sets) RegisterSet(set);

                if (container?.Affixes != null)
                    foreach (var affix in container.Affixes) RegisterAffix(affix);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ItemDatabase] Failed to load items: {e.Message}");
            }

            _isLoaded = true;
            OnDatabaseLoaded?.Invoke();
            Debug.Log($"[ItemDatabase] Loaded {_items.Count} items, {_equipment.Count} equipment, {_gems.Count} gems, {_sets.Count} sets, {_affixes.Count} affixes");
        }
        #endregion

        #region Lookup
        public ItemData GetItem(string itemId) => _items.TryGetValue(itemId, out var item) ? item : null;
        public EquipmentData GetEquipment(string itemId) => _equipment.TryGetValue(itemId, out var equip) ? equip : null;
        public GemData GetGem(string gemId) => _gems.TryGetValue(gemId, out var gem) ? gem : null;
        public SetBonusData GetSet(string setId) => _sets.TryGetValue(setId, out var set) ? set : null;
        public AffixData GetAffix(string affixId) => _affixes.TryGetValue(affixId, out var affix) ? affix : null;

        public bool TryGetItem(string itemId, out ItemData item) => _items.TryGetValue(itemId, out item);
        public bool TryGetEquipment(string itemId, out EquipmentData equipment) => _equipment.TryGetValue(itemId, out equipment);
        public bool TryGetGem(string gemId, out GemData gem) => _gems.TryGetValue(gemId, out gem);
        public bool TryGetSet(string setId, out SetBonusData set) => _sets.TryGetValue(setId, out set);
        public bool TryGetAffix(string affixId, out AffixData affix) => _affixes.TryGetValue(affixId, out affix);
        public IReadOnlyList<AffixData> GetAllAffixes() => _affixes.Values.ToList();
        #endregion

        #region Queries
        public IReadOnlyList<ItemData> GetItemsByCategory(ItemCategory category)
        {
            return _items.Values.Where(i => i.Category == category).ToList();
        }

        public IReadOnlyList<ItemData> GetItemsByRarity(ItemRarity rarity)
        {
            return _items.Values.Where(i => i.ItemRarity == rarity).ToList();
        }

        public IReadOnlyList<EquipmentData> GetEquipmentByType(EquipmentType type)
        {
            return _equipment.Values.Where(e => e.EquipmentType == type).ToList();
        }

        public IReadOnlyList<EquipmentData> GetEquipmentBySlot(EquipmentType slot)
        {
            return _equipment.Values.Where(e => e.EquipmentType == slot).ToList();
        }

        public IReadOnlyList<EquipmentData> GetEquipmentBySet(string setId)
        {
            return _equipment.Values.Where(e => e.SetId == setId).ToList();
        }

        public IReadOnlyList<GemData> GetGemsByType(GemType type)
        {
            return _gems.Values.Where(g => g.GemType == type).ToList();
        }

        public IReadOnlyList<ItemData> SearchItems(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return Array.Empty<ItemData>();

            string lowerSearch = searchText.ToLower();
            return _items.Values.Where(i =>
                i.Name.ToLower().Contains(lowerSearch) ||
                i.Id.ToLower().Contains(lowerSearch) ||
                i.Description.ToLower().Contains(lowerSearch)
            ).ToList();
        }
        #endregion

        #region Item Properties
        public int GetMaxStackSize(string itemId) => GetItem(itemId)?.StackSize ?? 1;
        public int GetMaxEnhanceLevel(string itemId) => GetEquipment(itemId)?.MaxLevel ?? 20;
        public int GetMaxLimitBreak(string itemId) => GetEquipment(itemId)?.MaxLevel ?? 5;
        public int GetMaxSockets(string itemId) => GetEquipment(itemId)?.MaxSockets ?? 0;
        public GemType[] GetAllowedGemTypes(string itemId) => SocketService.Instance?.Config.SocketRules[0]?.AllowedGemTypes ?? Array.Empty<GemType>();
        public long GetSellPrice(string itemId) => GetItem(itemId)?.SellPrice ?? 0;
        public long GetBuyPrice(string itemId) => GetItem(itemId)?.BuyPrice ?? 0;
        public int GetBaseDurability(string itemId) => GetEquipment(itemId)?.MaxDurability ?? 0;
        public long GetRepairCostPerDurability(string itemId) => GetEquipment(itemId)?.RepairCostPerDurability ?? 0;
        public ItemLevelType[] GetSupportedLevelTypes(string itemId) => GetEquipment(itemId)?.SupportedLevelTypes ?? Array.Empty<ItemLevelType>();
        #endregion

        #region Validation
        public bool IsValidItemId(string itemId) => _items.ContainsKey(itemId);
        public bool IsEquipment(string itemId) => _equipment.ContainsKey(itemId);
        public bool IsStackable(string itemId) => GetItem(itemId)?.IsStackable ?? false;
        public bool IsConsumable(string itemId)
        {
            var item = GetItem(itemId);
            return item?.Category == ItemCategory.Consumable || item?.Category == ItemCategory.Chest ||
                   item?.Category == ItemCategory.SkillBook || item?.Category == ItemCategory.UpgradeStone;
        }
        public bool HasSockets(string itemId) => GetMaxSockets(itemId) > 0;
        #endregion

        #region Registration
        public void RegisterItem(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return;

            if (_items.ContainsKey(item.Id))
            {
                Debug.LogWarning($"[ItemDatabase] Item already registered: {item.Id}");
                return;
            }

            if (item is EquipmentData equipData)
                equipData.InitializeDefaults();

            // Resolve icon from Resources when not set in data (Sprite cannot be stored in JSON)
            if (item.Icon == null && !string.IsNullOrEmpty(item.IconKey))
                item.Icon = ItemResources.GetItemSource(item.IconKey);

            _items[item.Id] = item;

            if (item.IsEquipment)
            {
                _equipment[item.Id] = item as EquipmentData;
            }

            OnItemDataAdded?.Invoke(item.Id);
        }

        public void RegisterEquipment(EquipmentData equipment)
        {
            RegisterItem(equipment);
        }

        public void RegisterGem(GemData gem)
        {
            if (gem == null || string.IsNullOrEmpty(gem.GemId)) return;

            if (_gems.ContainsKey(gem.GemId))
            {
                Debug.LogWarning($"[ItemDatabase] Gem already registered: {gem.GemId}");
                return;
            }

            // Resolve gem icon from Resources when not set in data
            if (gem.Icon == null && !string.IsNullOrEmpty(gem.IconKey))
                gem.Icon = ItemResources.GetItemSource(gem.IconKey);

            _gems[gem.GemId] = gem;
            _items[gem.GemId] = new ItemData
            {
                Id = gem.GemId,
                Name = gem.Name,
                Description = gem.Description,
                Category = ItemCategory.Gem,
                ItemRarity = gem.ItemRarity,
                Icon = gem.Icon,
                StackSize = 1
            };

            OnItemDataAdded?.Invoke(gem.GemId);
        }

        public void RegisterSet(SetBonusData set)
        {
            if (set == null || string.IsNullOrEmpty(set.SetId)) return;

            if (_sets.ContainsKey(set.SetId))
            {
                Debug.LogWarning($"[ItemDatabase] Set already registered: {set.SetId}");
                return;
            }

            _sets[set.SetId] = set;
        }

        public void RegisterAffix(AffixData affix)
        {
            if (affix == null || string.IsNullOrEmpty(affix.AffixId)) return;

            if (_affixes.ContainsKey(affix.AffixId))
            {
                Debug.LogWarning($"[ItemDatabase] Affix already registered: {affix.AffixId}");
                return;
            }

            _affixes[affix.AffixId] = affix;
        }

        public void UnregisterItem(string itemId)
        {
            if (_items.Remove(itemId))
            {
                _equipment.Remove(itemId);
                _gems.Remove(itemId);
                OnItemDataRemoved?.Invoke(itemId);
            }
        }
        #endregion

        #region Runtime Generation
        public EquipmentData GenerateEquipment(string baseId, ItemRarity rarity, int level, EquipmentType type)
        {
            var baseItem = GetEquipment(baseId);
            if (baseItem == null) return null;

            var generated = new EquipmentData
            {
                Id = $"{baseId}_{rarity}_{level}_{Guid.NewGuid().ToString("N")[..8]}",
                Name = $"{rarity} {baseItem.Name}",
                Description = baseItem.Description,
                Category = ItemCategory.Equipment,
                ItemRarity = rarity,
                EquipmentType = type,
                Icon = baseItem.Icon,
                MaxLevel = level,
                BaseLevel = level,
                MaxDurability = baseItem.MaxDurability,
                SellPrice = (long)(baseItem.SellPrice * rarity.GetDefaultSellMultiplier()),
                BuyPrice = (long)(baseItem.BuyPrice * rarity.GetDefaultUpgradeMultiplier()),
                RequiredLevel = level,
                CombatStats = GenerateScaledStats(baseItem.CombatStats, rarity, level),
                SecondaryStats = baseItem.SecondaryStats,
                SpecialEffects = baseItem.SpecialEffects,
                PassiveSkills = baseItem.PassiveSkills,
                MaxSockets = baseItem.MaxSockets,
                SetId = baseItem.SetId,
                UpgradeCurve = baseItem.UpgradeCurve
            };

            return generated;
        }

        public GemData GenerateGem(GemType type, ItemRarity rarity, int level)
        {
            var baseGem = _gems.Values.FirstOrDefault(g => g.GemType == type);
            if (baseGem == null) return null;

            var generated = new GemData
            {
                GemId = $"{type}_{rarity}_{level}_{Guid.NewGuid().ToString("N")[..8]}",
                Name = $"{rarity} {baseGem.Name}",
                Description = baseGem.Description,
                GemType = type,
                ItemRarity = rarity,
                Icon = baseGem.Icon,
                MaxLevel = level,
                BaseStats = GenerateScaledStats(baseGem.BaseStats, rarity, level),
                RandomStats = baseGem.RandomStats,
                RandomStatCount = baseGem.RandomStatCount,
                GemColor = baseGem.GemColor,
                UpgradeData = baseGem.UpgradeData
            };

            return generated;
        }

        private CombatStatEntry[] GenerateScaledStats(CombatStatEntry[] baseStats, ItemRarity rarity, int level)
        {
            if (baseStats == null) return Array.Empty<CombatStatEntry>();

            float multiplier = rarity.GetDefaultStatMultiplier();
            return baseStats.Select(s => new CombatStatEntry
            {
                Stat = s.Stat,
                BaseValue = s.BaseValue * multiplier,
                ValuePerLevel = s.ValuePerLevel * multiplier,
                ValuePerEnhance = s.ValuePerEnhance * multiplier,
                Mode = s.Mode
            }).ToArray();
        }
        #endregion
    }

    // ============ JSON Container Classes ============

    /// <summary>
    /// Single-file container: all item types live in dataItems.json.
    /// </summary>
    [Serializable]
    public class AllItemDataContainer
    {
        public List<ItemData> Items;
        public List<EquipmentData> Equipment;
        public List<GemData> Gems;
        public List<SetBonusData> Sets;
        public List<AffixData> Affixes;
    }
}