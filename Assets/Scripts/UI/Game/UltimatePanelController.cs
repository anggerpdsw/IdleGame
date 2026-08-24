using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.UI.Game
{
    public class UltimatePanelController : MonoBehaviour
    {
        [SerializeField] private UltimateUI _slotPrefab;
        [SerializeField] private RectTransform _slotContainer;

        private readonly List<UltimateUI> _slots = new();
        private readonly Dictionary<string, UltimateUI> _slotByUltimateID = new();
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
                if (!db.IsPotion(data.Id)) continue;
                if (inv.GetTotalQuantity(data.Id) <= 0) continue;
                CreatePotionSlot(data.Id);
            }
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
            if (!ItemDatabase.Instance.IsPotion(args.Item.ItemId)) return;
            RefreshPotionSlot(args.Item.ItemId);
        }
        private void HandleItemQuantityChanged(InventoryItem item, int _)
        {
            if (!ItemDatabase.Instance.IsPotion(item.ItemId)) return;
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
            if (_slotByUltimateID.TryGetValue(itemId, out var existingSlot))
            {
                existingSlot.SetQuantity(quantity);
                return;
            }
            CreatePotionSlot(itemId);
        }
        private void RemovePotionSlot(string itemId)
        {
            if (!_slotByUltimateID.TryGetValue(itemId, out var slot)) return;
            _slotByUltimateID.Remove(itemId);
            _slots.Remove(slot);
            if (slot != null) Destroy(slot.gameObject);
        }
        private void CreatePotionSlot(string itemId)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return;
            var data = db.GetPotion(itemId);
            if (!db.IsPotion(itemId)) return;
            var slotObject = _slotPrefab != null
                ? Instantiate(_slotPrefab.gameObject, _slotContainer)
                : new GameObject($"Potion_{itemId}", typeof(RectTransform));
            if (!slotObject.TryGetComponent<UltimateUI>(out var slot))
                slot = slotObject.AddComponent<UltimateUI>();
            slot.Initialize(itemId);
            slot.BindClick(() => UsePotion(itemId));
            _slots.Add(slot);
            _slotByUltimateID[itemId] = slot;
            int quantity = InventoryService.Instance.GetTotalQuantity(itemId);
            slot.SetQuantity(quantity);
            if (_remainingCooldown.TryGetValue(itemId, out var remaining))
            {
                float cooldown = GetCooldown(data);
                float fill = cooldown > 0f ? remaining / cooldown : 0f;
                slot.SetCooldown(fill);
            }
        }

        /// <summary>Refreshes every slot's quantity from the inventory.</summary>
        private void RefreshAll()
        {
            var inv = InventoryService.Instance;
            if (inv == null) return;
            foreach (var itemId in _slotByUltimateID.Keys.ToList())
            {
                int quantity = inv.GetTotalQuantity(itemId);
                if (quantity <= 0) 
                    RemovePotionSlot(itemId);
                else if (_slotByUltimateID.TryGetValue(itemId, out var slot))
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
                if (_slotByUltimateID.TryGetValue(itemId, out var slot))
                {
                    var potion = GetPotion(itemId);
                    float cd = GetCooldown(potion);
                    float fill = cd > 0f ? remaining / cd : 0f;
                    slot.SetCooldown(fill);
                }
            }
        }

        private static PotionData GetPotion(string itemId)
            => ItemDatabase.Instance?.GetPotion(itemId);
        private static float GetCooldown(PotionData potion)
            => Mathf.Max(0f, potion?.Cooldown ?? 0f);
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
                    if (_slotByUltimateID.TryGetValue(itemId, out var slot)) slot.SetCooldown(0f);
                    continue;
                }
                _remainingCooldown[itemId] = remaining;
                if (!_slotByUltimateID.TryGetValue(itemId, out var activeSlot)) continue;
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