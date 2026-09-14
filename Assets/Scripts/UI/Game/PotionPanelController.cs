using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using PlayerClass = IdleDefenseSurvival.Player.Player;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Controller;

namespace IdleDefenseSurvival.UI.Game
{
    /// <summary>
    /// HUD potion bar. Displays owned potion items and allows the player to use them
    /// slots and refreshes them in realtime (icon + count) from InventoryService events.
    /// Slots auto-hide at 0 copies and re-appear when the player picks the potion up again.
    ///
    /// Effect wiring can drive gameplay through the holders (Heal / GainMana); anything
    /// without a known target falls back to "consume only" until the matching system lands.
    /// </summary>
    public class PotionPanelController : MonoBehaviour
    {
        [Tooltip("Potion slot template — the ItemConsumable prefab, a child of this panel.")]
        [SerializeField] private ItemConsumableUI _slotPrefab;
        [Tooltip("Potion slots layout (GridLayoutGroup/HorizontalLayoutGroup, etc.).")]
        [SerializeField] private RectTransform _slotContainer;

        // ─────────────────────────────────────────────────────────────
        // Runtime collections
        // ─────────────────────────────────────────────────────────────
        private readonly List<ItemConsumableUI> _slots = new();
        private readonly Dictionary<string, ItemConsumableUI> _slotByItemId = new();
        private readonly Dictionary<string, float> _remainingCooldown = new();
        // Cached potion metadata.
        private readonly Dictionary<string, PotionData> _potionByItemId = new();
        // Cached sorted IDs by potion type.
        private readonly Dictionary<PotionType, List<string>> _sortedPotionIds = new();
        // ─────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────
        private bool _isInitialized;
        private bool _isSubscribed;
        private PlayerClass _player;
        private SettingsController _settings;
        private InventoryService _inventory;
        private ItemDatabase _database;

        private void Start()
        {
            Initialize();
            _player = PlayerClass.Instance;
            _settings = SettingsController.Instance;
            if (_player != null)
            {
                _player.OnHealthChanged += OnPlayerHealthChanged;
                _player.OnManaChanged += OnPlayerManaChanged;
            }
            RefreshAll();
        }

        private void OnEnable()
        {
            Subscribe();
            if (_isInitialized) RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.OnHealthChanged -= OnPlayerHealthChanged;
                _player.OnManaChanged -= OnPlayerManaChanged;
            }
            Unsubscribe();
        }

        /// <summary>Builds the visible potion slots for the items the player owns.</summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            _inventory = InventoryService.Instance;
            _database = ItemDatabase.Instance;
            if (_inventory == null || _database == null) return;

            _isInitialized = true;

            CachePotionDatabase();

            foreach (var pair in _potionByItemId)
            {
                string itemId = pair.Key;
                if (_inventory.GetTotalQuantity(itemId) > 0)
                    CreatePotionSlot(itemId);
            }
        }
        private void CachePotionDatabase()
        {
            _potionByItemId.Clear();
            _sortedPotionIds.Clear();
            var consumables = _database.GetItemsByCategory(ItemCategory.Consumable);
            foreach (var data in consumables)
            {
                if (data == null) continue;
                string itemId = data.Id;
                if (!_database.IsPotion(itemId)) continue;

                PotionData potion = _database.GetPotion(itemId);
                if (potion == null) continue;
                _potionByItemId[itemId] = potion;

                if (!_sortedPotionIds.TryGetValue(potion.PotionType, out var ids))
                {
                    ids = new List<string>();
                    _sortedPotionIds[potion.PotionType] = ids;
                }
                ids.Add(itemId);
            }

            foreach (var pair in _sortedPotionIds)
            {
                pair.Value.Sort(ComparePotionIds);
            }
        }

        private static int ComparePotionIds(string a, string b)
        {
            return ExtractPotionNumber(a).CompareTo(ExtractPotionNumber(b));
        }

        private static int ExtractPotionNumber(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int separator = itemId.LastIndexOf('_');
            if (separator < 0 || separator >= itemId.Length - 1) return 0;
            return int.TryParse(itemId.AsSpan(separator + 1), out int number) ? number : 0;
        }

        // ─────────────────────────────────────────────────────────────
        // Inventory events
        // ─────────────────────────────────────────────────────────────
        private void Subscribe()
        {
            if (_isSubscribed) return;
            _inventory ??= InventoryService.Instance;
            if (_inventory == null) return;
            _inventory.OnInventoryChanged += HandleInventoryChanged;
            _inventory.OnItemQuantityChanged += HandleItemQuantityChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _inventory == null) return;
            _inventory.OnInventoryChanged -= HandleInventoryChanged;
            _inventory.OnItemQuantityChanged -= HandleItemQuantityChanged;
            _isSubscribed = false;
        }

        private void HandleInventoryChanged(InventoryChangedEventArgs args)
        {
            if (args.Item == null)
            {
                RefreshAll();
                return;
            }
            RefreshPotionSlot(args.Item.ItemId);
        }
        private void HandleItemQuantityChanged(InventoryItem item, int _)
        {
            if (item == null) return;
            RefreshPotionSlot(item.ItemId);
        }

        // ─────────────────────────────────────────────────────────────
        // Slot management
        // ─────────────────────────────────────────────────────────────
        /// <summary>Rebuilds the slot list from the inventory (new potion type or last one used up).</summary>
        private void RefreshPotionSlot(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            if (!_potionByItemId.ContainsKey(itemId)) return;
            int quantity = _inventory.GetTotalQuantity(itemId);
            if (quantity <= 0)
            {
                RemovePotionSlot(itemId);
                return;
            }
            if (_slotByItemId.TryGetValue(itemId, out var existingSlot))
            {
                existingSlot.SetQuantity(quantity);
                return;
            }
            CreatePotionSlot(itemId);
        }
        private void CreatePotionSlot(string itemId)
        {
            if (_slotByItemId.ContainsKey(itemId)) return;
            if (!_potionByItemId.TryGetValue(itemId, out var potion)) return;

            GameObject slotObject;

            if (_slotPrefab == null)
            {
                Debug.LogWarning($"[PotionPanelController] Prefabs _slotPrefab empty");
                return;
            }
            slotObject = Instantiate(_slotPrefab.gameObject, _slotContainer);
            if (!slotObject.TryGetComponent<ItemConsumableUI>(out var slot))
                slot = slotObject.AddComponent<ItemConsumableUI>();
            slot.Initialize(itemId);
            slot.BindClick(() => UsePotion(itemId));
            _slots.Add(slot);
            _slotByItemId[itemId] = slot;
            slot.SetQuantity(_inventory.GetTotalQuantity(itemId));
            if (_remainingCooldown.TryGetValue(itemId, out float remaining))
            {
                float cooldown = GetCooldown(potion);
                slot.SetCooldown(GetCooldownFill(remaining, cooldown));
            }
        }
        private void RemovePotionSlot(string itemId)
        {
            if (!_slotByItemId.TryGetValue(itemId, out var slot)) return;
            _slotByItemId.Remove(itemId);
            _slots.Remove(slot);
            if (slot != null) Destroy(slot.gameObject);
        }

        /// <summary>Refreshes every slot's quantity from the inventory.</summary>
        private void RefreshAll()
        {
            if (!_isInitialized || _inventory == null) return;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot == null)
                {
                    _slots.RemoveAt(i);
                    continue;
                }
                // Reverse lookup is avoided by refreshing through dictionary.
            }
            foreach (var pair in _slotByItemId)
            {
                int quantity = _inventory.GetTotalQuantity(pair.Key);
                pair.Value.SetQuantity(quantity);
            }
            UpdateCooldownVisuals();
        }

        // ─────────────────────────────────────────────────────────────
        // Auto potion
        // ─────────────────────────────────────────────────────────────
        private void OnPlayerHealthChanged()
        {
            if (!_settings.AutoPotion) return;
            if (_player == null) return;

            float percent = _player.MaxHealth > 0f ? _player.CurrentHealth / _player.MaxHealth : 1f;
            if (percent > _settings.HealthPotionThreshold) return;

            foreach (var id in GetSortedPotionIds(PotionType.Health))
            {
                if (_remainingCooldown.TryGetValue(id, out var cd) && cd > 0f) continue;
                if (InventoryService.Instance.GetTotalQuantity(id) <= 0) continue;
                UsePotion(id);
                break;
            }
        }

        private void OnPlayerManaChanged()
        {
            if (!_settings.AutoPotion) return;
            if (_player == null) return;

            float percent = _player.MaxMana > 0f ? _player.CurrentMana / _player.MaxMana : 1f;
            if (percent > _settings.ManaPotionThreshold) return;

            foreach (var id in GetSortedPotionIds(PotionType.Mana))
            {
                if (_remainingCooldown.TryGetValue(id, out var cd) && cd > 0f) continue;
                if (InventoryService.Instance.GetTotalQuantity(id) <= 0) continue;
                UsePotion(id);
                break;
            }
        }

        private bool CanAutoPotion()
            =>  _settings != null && _settings.AutoPotion &&
                _player != null && _inventory != null;

        /// <summary>Uses one copy of the potion + starts its cooldown timer.</summary>
        private void UsePotion(string itemId)
        {
            var inv = InventoryService.Instance;
            if (inv == null) return;
            if (inv.GetTotalQuantity(itemId) <= 0) return;
            if (_remainingCooldown.TryGetValue(itemId, out var remaining) && remaining > 0f)
                return;
            if (!ApplyEffect(itemId)) return;
            if (inv.RemoveItemById(itemId, 1) <= 0) return;
            var potion = GetPotion(itemId);
            float cd = GetCooldown(potion);
            if (cd > 0f) _remainingCooldown[itemId] = cd;
            UpdateCooldownVisuals();
        }

        /// <summary>Applies visual cooldown state (radial fill + block click) for all tracked potions.</summary>
        private void UpdateCooldownVisuals()
        {
            foreach (var (itemId, remaining) in _remainingCooldown)
            {
                if (_slotByItemId.TryGetValue(itemId, out var slot))
                {
                    var potion = GetPotion(itemId);
                    float cd = GetCooldown(potion);
                    float fill = cd > 0f ? remaining / cd : 0f;
                    slot.SetCooldown(fill);
                }
            }
        }

        private List<string> GetSortedPotionIds(PotionType type)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return new List<string>();

            var ids = db.GetItemsByCategory(ItemCategory.Consumable)
                        .Where(d => db.IsPotion(d.Id))
                        .Select(d => d.Id)
                        .Where(id => db.GetPotion(id)?.PotionType == type)
                        .ToList();

            ids.Sort((a, b) =>
            {
                static int GetNum(string s)
                {
                    var parts = s.Split('_');
                    return int.TryParse(parts.Last(), out var n) ? n : 0;
                }
                return GetNum(a).CompareTo(GetNum(b));
            });

            return ids;
        }

        private static PotionData GetPotion(string itemId)
            => ItemDatabase.Instance?.GetPotion(itemId);
        private static float GetCooldown(PotionData potion)
            => Mathf.Max(0f, potion?.Cooldown ?? 0f);
        private static float GetCooldownFill(float remaining, float cooldown)
            => cooldown > 0f ? Mathf.Clamp01(remaining / cooldown) : 0f;

        /// <summary>
        /// Runs the potion effect described by its item ID.
        /// Returns false when the item cannot be used.
        /// </summary>
        private bool ApplyEffect(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var potion = GetPotion(itemId);
            if (potion == null) return false;
            return ApplyPotion(potion);
        }

        private bool ApplyPotion(PotionData potion)
        {
            if (potion == null) return false;
            return potion.PotionType switch
            {
                PotionType.Health => ApplyHealthPotion(potion),
                PotionType.Mana => ApplyManaPotion(potion),
                PotionType.Stamina => RestoreStamina(),
                PotionType.DebuffCleanse => CleanDebuff(),
                _ => false
            };
        }

        private bool ApplyHealthPotion(PotionData potion)
        {
            float maxHealth = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            float amount = potion.CalculateAmount(maxHealth);
            PlayerClass.Instance.StartHealOverTime(amount, potion.EffectDuration);
            return true;
        }
        private bool ApplyManaPotion(PotionData potion)
        {
            float maxMana = PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint);
            float amount = potion.CalculateAmount(maxMana);
            PlayerClass.Instance.StartManaOverTime(amount,potion.EffectDuration);
            return true;
        }

        private bool CleanDebuff()
        {
            // Player doesn't have a debuff system yet. Don't consume item.
            return false;
        }

        private static bool RestoreStamina()
        {
            // Player doesn't have a stamina system yet. Don't consume item.
            return false;
        }

        /// <summary>Visual cooldown: updates radial fill on icon while timer runs.</summary>
        private void Update()
        {
            if (_remainingCooldown.Count == 0) return;
            foreach (var itemId in _remainingCooldown.Keys.ToList())
            {
                float remaining = _remainingCooldown[itemId] - Time.deltaTime;
                if (remaining <= 0f)
                {
                    _remainingCooldown.Remove(itemId);
                    if (_slotByItemId.TryGetValue(itemId, out var slot)) slot.SetCooldown(0f);
                    continue;
                }
                _remainingCooldown[itemId] = remaining;
                if (!_slotByItemId.TryGetValue(itemId, out var activeSlot)) continue;
                var potion = GetPotion(itemId);
                if (potion == null)
                {
                    activeSlot.SetCooldown(0f);
                    continue;
                }
                float cooldown = GetCooldown(potion);
                float fill = cooldown > 0f ? Mathf.Clamp01(remaining / cooldown) : 0f;
                activeSlot.SetCooldown(fill);
            }
        }
    }
}