using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using PlayerClass = IdleDefenseSurvival.Player.Player;

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
        [Tooltip("Optional — potion 'use' cooldown. Shared across every slot. 0 = no cooldown.")]
        [SerializeField] private float _defaultCooldown = 5f;

        private readonly List<ItemConsumableUI> _slots = new();
        private readonly Dictionary<string, ItemConsumableUI> _slotByItemId = new();
        private readonly Dictionary<string, float> _remainingCooldown = new();
        private bool _isInitialized;

        private void Start()
        {
            Initialize();
            RefreshAll();
        }

        /// <summary>Builds the visible potion slots for the items the player owns.</summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            var inv = InventoryService.Instance;
            var db = ItemDatabase.Instance;
            if (inv == null || db == null) return;

            // One slot per potion type the player currently owns.
            foreach (var data in db.GetItemsByCategory(ItemCategory.Consumable))
            {
                if (!IsPotion(data)) continue;
                if (inv.GetTotalQuantity(data.Id) <= 0) continue;
                CreatePotionSlot(data.Id);
            }
        }

        private static bool IsPotion(ItemData data) =>
            data != null &&
                data.Id.StartsWith("potion_", StringComparison.OrdinalIgnoreCase);
        private static bool IsPotion(InventoryItem item) =>
            item != null &&
                item.ItemId.StartsWith("potion_", StringComparison.OrdinalIgnoreCase);

        private void OnEnable()
        {
            Subscribe();
            if (_isInitialized) RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            var inv = InventoryService.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged += HandleInventoryChanged;
                inv.OnItemQuantityChanged += HandleItemQuantityChanged;
            }
        }

        private void Unsubscribe()
        {
            var inv = InventoryService.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged -= HandleInventoryChanged;
                inv.OnItemQuantityChanged -= HandleItemQuantityChanged;
            }
        }

        private void HandleInventoryChanged(InventoryChangedEventArgs args)
        {
            if (args.Item == null)
            {
                RefreshAll();
                return;
            }
            if (!IsPotion(args.Item)) return;
            RefreshPotionSlot(args.Item.ItemId);
        }
        private void HandleItemQuantityChanged(InventoryItem item, int _)
        {
            if (!IsPotion(item)) return;
            RefreshPotionSlot(item.ItemId);
        }

        /// <summary>Rebuilds the slot list from the inventory (new potion type or last one used up).</summary>
        private void RefreshPotionSlot(string itemId)
        {
            var inv = InventoryService.Instance;
            var db = ItemDatabase.Instance;
            if (inv == null || db == null) return;
            int quantity = inv.GetTotalQuantity(itemId);
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
        private void RemovePotionSlot(string itemId)
        {
            if (!_slotByItemId.TryGetValue(itemId, out var slot)) return;
            _slotByItemId.Remove(itemId);
            _slots.Remove(slot);
            if (slot != null) Destroy(slot.gameObject);
        }
        private void CreatePotionSlot(string itemId)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return;
            var data = db.GetItem(itemId);
            if (!IsPotion(data)) return;
            var slotObject = _slotPrefab != null
                ? Instantiate(_slotPrefab.gameObject, _slotContainer)
                : new GameObject($"Potion_{itemId}", typeof(RectTransform));
            if (!slotObject.TryGetComponent<ItemConsumableUI>(out var slot))
                slot = slotObject.AddComponent<ItemConsumableUI>();
            slot.Initialize(itemId);
            slot.BindClick(() => UsePotion(itemId));
            _slots.Add(slot);
            _slotByItemId[itemId] = slot;
            int quantity = InventoryService.Instance.GetTotalQuantity(itemId);
            slot.SetQuantity(quantity);
            if (_remainingCooldown.TryGetValue(itemId, out var remaining))
            {
                float fill = _defaultCooldown > 0f ? remaining / _defaultCooldown : 0f;
                slot.SetCooldown(fill);
            }
        }

        /// <summary>Refreshes every slot's quantity from the inventory.</summary>
        private void RefreshAll()
        {
            var inv = InventoryService.Instance;
            if (inv == null) return;
            foreach (var itemId in _slotByItemId.Keys.ToList())
            {
                int quantity = inv.GetTotalQuantity(itemId);
                if (quantity <= 0) 
                    RemovePotionSlot(itemId);
                else if (_slotByItemId.TryGetValue(itemId, out var slot))
                    slot.SetQuantity(quantity);
            }
            UpdateCooldownVisuals();
        }

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
            if (_defaultCooldown > 0f)
                _remainingCooldown[itemId] = _defaultCooldown;
            UpdateCooldownVisuals();
        }

        /// <summary>Applies visual cooldown state (radial fill + block click) for all tracked potions.</summary>
        private void UpdateCooldownVisuals()
        {
            foreach (var (itemId, remaining) in _remainingCooldown)
            {
                if (_slotByItemId.TryGetValue(itemId, out var slot))
                {
                    float fill = _defaultCooldown > 0f ? remaining / _defaultCooldown : 0f;
                    slot.SetCooldown(fill);
                }
            }
        }

        /// <summary>Runs the potion described by its ItemData. Returns false on any usable-fail (no target / conflict).</summary>
        private bool ApplyEffect(string itemId)
        {
            return itemId switch
            {
                "potion_ap" => CleanDebuff(),
                "potion_hp" => Heal(),
                "potion_mp" => GainMana(),
                "potion_sp" => RestoreStamina(),
                _           => false
            };
        }

        private bool CleanDebuff()
        {
            // Player doesn't have a debuff system yet. Don't consume item.
            return false;
        }

        private bool Heal()
        {
            PlayerClass.Instance.StartHealOverTime(500f, 10f);
            return true;
        }

        private bool GainMana()
        {
            PlayerClass.Instance.StartManaOverTime(500f, 10f);
            return true;
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
                _remainingCooldown[itemId] -= Time.deltaTime;
                if (_remainingCooldown[itemId] <= 0f)
                {
                    _remainingCooldown.Remove(itemId);
                    if (_slotByItemId.TryGetValue(itemId, out var slot))
                        slot.SetCooldown(0f);
                    continue;
                }
                if (_slotByItemId.TryGetValue(itemId, out var activeSlot))
                {
                    float fill = _remainingCooldown[itemId] / _defaultCooldown;
                    activeSlot.SetCooldown(fill);
                }
            }
        }
    }
}