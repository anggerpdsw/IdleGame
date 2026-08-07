using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment orchestrator - coordinates slot, effect, modifier, set bonus,
    /// durability, comparison, persistence, auto-equip and visual subsystems.
    /// Public API preserved for UI/services; logic lives in sub-services.
    /// </summary>
    public sealed class EquipmentService : MonoBehaviour, IEquipmentService, IEquipmentRepository
    {
        #region Singleton
        private static EquipmentService _instance;
        public static EquipmentService Instance => _instance;

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
            Initialize();
        }
        #endregion

        #region Events (IEquipmentService re-raises dispatcher events)
        public event Action<EquipmentChangedEventArgs> OnEquipmentChanged;
        public event Action<EquipmentType, InventoryItem> OnItemEquipped;
        public event Action<EquipmentType, InventoryItem> OnItemUnequipped;
        public event Action<EquipmentType> OnSlotUnlocked;
        public event Action OnSetBonusChanged;
        public event Action<EquipmentType> OnDurabilityChanged;
        #endregion

        #region Fields
        private readonly Dictionary<EquipmentType, InventoryItem> _equippedItems = new();
        private readonly Dictionary<string, int> _setPieceCounts = new();
        private readonly HashSet<EquipmentType> _unlockedSlots = new();
        private readonly List<IEquipmentEffect> _activeEffects = new();

        private EquipmentEventDispatcher _events;
        private EquipmentSlotService _slots;
        private EquipmentEffectService _effectService;
        private EquipmentModifierService _modifierService;
        private EquipmentSetBonusService _setBonusService;
        private EquipmentDurabilityService _durabilityService;
        private EquipmentComparisonService _comparisonService;
        private EquipmentPersistenceService _persistenceService;
        private EquipmentAutoEquipService _autoEquipService;
        private EquipmentVisualService _visualService;
        #endregion

        #region IEquipmentRepository
        IReadOnlyDictionary<EquipmentType, InventoryItem> IEquipmentRepository.EquippedItems => _equippedItems;
        IReadOnlyCollection<EquipmentType> IEquipmentRepository.UnlockedSlots => _unlockedSlots;
        IReadOnlyList<IEquipmentEffect> IEquipmentRepository.ActiveEffects => _activeEffects;
        IEnumerable<string> IEquipmentRepository.ActiveSetIds => _setPieceCounts.Keys;

        bool IEquipmentRepository.TryGetEquipped(EquipmentType slot, out InventoryItem item) =>
            _equippedItems.TryGetValue(slot, out item);

        void IEquipmentRepository.SetEquipped(EquipmentType slot, InventoryItem item) => _equippedItems[slot] = item;
        bool IEquipmentRepository.RemoveEquipped(EquipmentType slot, out InventoryItem item) => _equippedItems.Remove(slot, out item);

        bool IEquipmentRepository.IsSlotUnlocked(EquipmentType slot) => _unlockedSlots.Contains(slot);
        void IEquipmentRepository.SetSlotUnlocked(EquipmentType slot, bool unlocked)
        {
            if (unlocked) _unlockedSlots.Add(slot);
            else _unlockedSlots.Remove(slot);
        }

        void IEquipmentRepository.UpdateSetPieceCount(string setId, int newCount)
        {
            if (newCount > 0) _setPieceCounts[setId] = newCount;
            else _setPieceCounts.Remove(setId);
        }
        int IEquipmentRepository.GetSetPieceCount(string setId) => _setPieceCounts.GetValueOrDefault(setId, 0);
        void IEquipmentRepository.ClearSetCounts() => _setPieceCounts.Clear();
        IReadOnlyDictionary<string, int> IEquipmentRepository.SnapshotSetCounts() =>
            new Dictionary<string, int>(_setPieceCounts);

        void IEquipmentRepository.AddActiveEffect(IEquipmentEffect effect) => _activeEffects.Add(effect);
        void IEquipmentRepository.RemoveActiveEffect(IEquipmentEffect effect) => _activeEffects.Remove(effect);
        void IEquipmentRepository.ClearActiveEffects() => _activeEffects.Clear();
        #endregion

        #region Initialization
        private void Initialize()
        {
            _equippedItems.Clear();
            _unlockedSlots.Clear();
            _setPieceCounts.Clear();
            _activeEffects.Clear();

            _events = new EquipmentEventDispatcher();
            _slots = new EquipmentSlotService(this);
            _effectService = new EquipmentEffectService(this);
            _modifierService = new EquipmentModifierService(this);
            _setBonusService = new EquipmentSetBonusService(this, _modifierService, _events);
            _durabilityService = new EquipmentDurabilityService(this, _events);
            _comparisonService = new EquipmentComparisonService(this);
            _persistenceService = new EquipmentPersistenceService(this, _slots, _events);
            _autoEquipService = new EquipmentAutoEquipService(this);
            _visualService = new EquipmentVisualService(this);

            // Default free slots
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot != EquipmentType.None && slot is EquipmentType.Hat or EquipmentType.Armor or EquipmentType.Pants)
                    _unlockedSlots.Add(slot);
            }

            // Re-raise dispatcher events on the public API
            _events.ItemEquipped += (s, i) => OnItemEquipped?.Invoke(s, i);
            _events.ItemUnequipped += (s, i) => OnItemUnequipped?.Invoke(s, i);
            _events.SlotUnlocked += s => OnSlotUnlocked?.Invoke(s);
            _events.SetBonusChanged += () => OnSetBonusChanged?.Invoke();
            _events.DurabilityChanged += s => OnDurabilityChanged?.Invoke(s);
            _events.Changed += e => OnEquipmentChanged?.Invoke(e);
        }
        #endregion

        #region Core Operations
        public bool Equip(InventoryItem item, EquipmentType slot = EquipmentType.None)
        {
            if (item == null || !item.IsEquippable()) return false;

            EquipmentType itemType = item.GetEquipmentType();
            EquipmentType targetSlot = slot != EquipmentType.None ? slot : itemType;
            if (targetSlot == EquipmentType.None) return false;

            if (!CanEquip(item, targetSlot, out string reason))
            {
                Debug.Log($"[EquipmentService] Cannot equip {item.ItemId} in {targetSlot}: {reason}");
                return false;
            }

            if (_equippedItems.TryGetValue(targetSlot, out var currentItem))
                UnequipInternal(targetSlot, currentItem);

            return EquipInternal(targetSlot, item);
        }

        public bool EquipByInstanceId(string instanceId, EquipmentType slot = EquipmentType.None)
        {
            var item = InventoryService.Instance?.GetItem(instanceId);
            return item != null && Equip(item, slot);
        }

        public InventoryItem Unequip(EquipmentType slot)
        {
            return _equippedItems.TryGetValue(slot, out var item) ? UnequipInternal(slot, item) : null;
        }

        public InventoryItem UnequipByInstanceId(string instanceId)
        {
            foreach (var (slot, item) in _equippedItems)
            {
                if (item?.InstanceId == instanceId)
                    return UnequipInternal(slot, item);
            }
            return null;
        }

        public bool SwapEquipment(EquipmentType slotA, EquipmentType slotB)
        {
            if (!_equippedItems.TryGetValue(slotA, out var itemA) || !_equippedItems.TryGetValue(slotB, out var itemB))
                return false;

            if (itemA.GetEquipmentType() != slotB || itemB.GetEquipmentType() != slotA)
                return false;

            UnequipInternal(slotA, itemA);
            UnequipInternal(slotB, itemB);
            EquipInternal(slotA, itemB);
            EquipInternal(slotB, itemA);

            _events.Swapped(slotA, slotB, itemA, itemB);
            return true;
        }

        public int AutoEquipBest() => _autoEquipService.AutoEquipBest(
            (item, slot) => CanEquip(item, slot, out _), Equip);

        public IReadOnlyList<InventoryItem> UnequipAll()
        {
            var items = _equippedItems.Values.ToList();
            foreach (var (slot, item) in _equippedItems.ToList())
                UnequipInternal(slot, item);
            return items;
        }
        #endregion

        #region Internal Equip/Unequip
        private bool EquipInternal(EquipmentType slot, InventoryItem item)
        {
            item.IsEquipped = true;
            item.EquippedSlot = slot;

            _equippedItems[slot] = item;
            _slots.BindItem(slot, item);

            string setId = item.GetSetId();
            int newCount = 0;
            if (!string.IsNullOrEmpty(setId))
            {
                int previousCount = _setPieceCounts.GetValueOrDefault(setId, 0);
                newCount = previousCount + 1;
                _setPieceCounts[setId] = newCount;

                var setData = ItemDatabase.Instance?.GetSet(setId);
                if (setData != null)
                    _setBonusService.CheckSetBonusTier(setData, previousCount, newCount);
            }

            _effectService.ActivateItemEffects(item, slot);
            _modifierService.ApplyItemStatModifiers(item, slot, true);

            _events.Equipped(slot, item, setId, newCount);
            Inventory.InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Item);

            return true;
        }

        private InventoryItem UnequipInternal(EquipmentType slot, InventoryItem item)
        {
            _equippedItems.Remove(slot);
            _slots.BindItem(slot, null);

            string setId = item.GetSetId();
            int newCount = 0;
            if (!string.IsNullOrEmpty(setId))
            {
                int previousCount = _setPieceCounts.GetValueOrDefault(setId, 0);
                newCount = Math.Max(0, previousCount - 1);
                if (newCount > 0) _setPieceCounts[setId] = newCount;
                else _setPieceCounts.Remove(setId);

                var setData = ItemDatabase.Instance?.GetSet(setId);
                if (setData != null)
                    _setBonusService.CheckSetBonusTier(setData, previousCount, newCount);
            }

            _effectService.DeactivateItemEffects(item, slot);
            _modifierService.ApplyItemStatModifiers(item, slot, false);

            item.IsEquipped = false;
            item.EquippedSlot = EquipmentType.None;

            _events.Unequipped(slot, item, setId, newCount);
            Inventory.InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Item);

            return item;
        }
        #endregion

        #region Validation
        public bool CanEquip(InventoryItem item, EquipmentType slot, out string reason)
        {
            reason = string.Empty;

            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equippable"; return false; }
            if (!IsSlotAvailable(slot)) { reason = $"Slot {slot} is not available"; return false; }
            if (item.GetEquipmentType() != slot)
            {
                reason = $"Item type {item.GetEquipmentType()} does not match slot {slot}";
                return false;
            }
            if (!MeetsRequirements(item, out reason)) return false;

            return true;
        }

        public bool MeetsRequirements(InventoryItem item, out string reason)
        {
            reason = string.Empty;

            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return true;

            int playerLevel = PlayerStatsManager.Instance?.GetStatInt(SkillType.HealthPoint) ?? 1;
            if (itemData.RequiredLevel > playerLevel)
            {
                reason = $"Requires level {itemData.RequiredLevel}";
                return false;
            }

            return true;
        }

        public bool IsSlotAvailable(EquipmentType slot) => _slots.IsUnlocked(slot);
        #endregion

        #region Slot Management
        public bool UnlockSlot(EquipmentType slot)
        {
            if (!_slots.Unlock(slot)) return false;
            _events.NotifySlotUnlocked(slot);
            return true;
        }

        public long GetSlotUnlockCost(EquipmentType slot) => _slots.GetUnlockCost(slot);
        public EquipmentType? GetNextUnlockableSlot() => _slots.GetNextUnlockable();

        public IReadOnlyDictionary<EquipmentType, InventoryItem> EquippedItems => _equippedItems;
        public IReadOnlyList<EquipmentSlotData> SlotData => _slots.GetAllSlotData();
        public int UnlockedSlotCount => _slots.UnlockedCount;
        public int TotalEquippedCount => _equippedItems.Count;
        public Dictionary<string, int> EquippedSetCounts => new(_setPieceCounts);
        #endregion

        #region Set Bonuses
        public IReadOnlyList<SetBonusTier> GetActiveSetBonuses(string setId) => _setBonusService.GetActiveTiers(setId);
        public IReadOnlyDictionary<string, IReadOnlyList<SetBonusTier>> GetAllActiveSetBonuses() => _setBonusService.GetAllActiveBonuses();
        public bool IsSetBonusActive(string setId, int tierIndex) => _setBonusService.IsTierActive(setId, tierIndex);
        public int GetSetPieceCount(string setId) => _setBonusService.GetSetPieceCount(setId);
        #endregion

        #region Stat Calculation
        public Dictionary<SecondaryStat, float> GetTotalStatBonuses() =>
            EquipmentStatCalculator.GetTotalStatBonuses(ItemDatabase.Instance, _equippedItems, _setPieceCounts);

        public Dictionary<SecondaryStat, float> GetSlotStatBonuses(EquipmentType slot) =>
            _equippedItems.TryGetValue(slot, out var item)
                ? EquipmentStatCalculator.GetItemStatBonuses(item)
                : new Dictionary<SecondaryStat, float>();

        public IReadOnlyList<ActiveSpecialEffect> GetActiveSpecialEffects()
        {
            var effects = new List<ActiveSpecialEffect>();

            foreach (var (slot, item) in _equippedItems)
            {
                var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
                if (itemData?.SpecialEffects == null) continue;

                foreach (var effectEntry in itemData.SpecialEffects)
                {
                    if (!effectEntry.CanActivate(item.Level, item.EnhanceLevel)) continue;
                    effects.Add(new ActiveSpecialEffect
                    {
                        EffectType = effectEntry.EffectType,
                        Value = effectEntry.Value,
                        Chance = effectEntry.Chance,
                        Cooldown = effectEntry.Cooldown,
                        SourceSlot = slot,
                        SourceItemId = item.ItemId,
                        SourceInstanceId = item.InstanceId,
                        IsActive = effectEntry.IsActive
                    });
                }
            }

            foreach (var (setId, count) in _setPieceCounts)
            {
                var setData = ItemDatabase.Instance?.GetSet(setId);
                if (setData?.Tiers == null) continue;

                foreach (var tier in setData.Tiers.Where(t => t.IsActive(count)))
                {
                    if (tier.SpecialEffects == null) continue;
                    foreach (var effectEntry in tier.SpecialEffects)
                    {
                        effects.Add(new ActiveSpecialEffect
                        {
                            EffectType = effectEntry.EffectType,
                            Value = effectEntry.Value,
                            Chance = effectEntry.Chance,
                            Cooldown = effectEntry.Cooldown,
                            SourceSlot = EquipmentType.None,
                            SourceItemId = $"Set:{setId}",
                            SourceInstanceId = $"Set:{setId}",
                            IsActive = effectEntry.IsActive
                        });
                    }
                }
            }

            return effects;
        }

        public void ApplyItemStatModifiers(InventoryItem item, EquipmentType slot, bool add) =>
            _modifierService.ApplyItemStatModifiers(item, slot, add);
        #endregion

        #region Durability
        public void DamageDurability(EquipmentType slot, int amount)
        {
            bool broken = _durabilityService.DamageDurability(slot, amount);
            if (broken && _equippedItems.TryGetValue(slot, out var item))
                UnequipInternal(slot, item);
        }

        public long RepairAll() => _durabilityService.RepairAll();
        public long RepairSlot(EquipmentType slot) => _durabilityService.RepairSlot(slot);
        public long GetTotalRepairCost() => _durabilityService.GetTotalRepairCost();
        #endregion

        #region Visual
        public GameObject GetEquippedModel(EquipmentType slot) => _visualService.GetEquippedModel(slot);
        #endregion

        #region Persistence
        public EquipmentSaveData GetSaveData() => _persistenceService.GetSaveData();

        public void LoadFromSaveData(EquipmentSaveData data)
        {
            if (data == null) return;

            UnequipAll();
            _setPieceCounts.Clear();
            _activeEffects.Clear();

            _slots.RestoreUnlocks((data.UnlockedSlots ?? Array.Empty<UnlockedSlotData>()).Select(s => s.Slot));

            foreach (var equipData in data.EquippedItems ?? Array.Empty<EquippedItemData>())
            {
                if (equipData.Item != null)
                    EquipInternal(equipData.Slot, equipData.Item);
            }
        }

        public void Reset()
        {
            UnequipAll();
            _setPieceCounts.Clear();
            _activeEffects.Clear();
            _slots.ResetUnlocks();
        }
        #endregion

        #region Comparison
        public EquipmentComparison CompareWithEquipped(InventoryItem item) => _comparisonService.CompareWithEquipped(item);

        public InventoryItem GetBestItemForSlot(EquipmentType slot) =>
            _autoEquipService.GetBestItemForSlot(slot, (item, s) => CanEquip(item, s, out _));
        #endregion
    }
}