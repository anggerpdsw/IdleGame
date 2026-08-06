using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Inventory;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Main entry point for card system. Coordinates CardInventory, CardEquipmentService,
    /// CardRollService, CardUpgradeService, and CardModifierService.
    /// Exposes static events for UI to subscribe without polling.
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        #region Singleton
        private static CardManager _instance;
        public static CardManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // CardRoll lives in InventoryService slots; rebroadcast its changes
            // (InventoryService may not exist yet at Awake; retry in Start)
            TrySubscribeInventory();

            Initialize();
        }

        private void Start()
        {
            TrySubscribeInventory();
        }

        private void TrySubscribeInventory()
        {
            if (_subscribedInventory) return;
            if (InventoryService.Instance != null)
            {
                InventoryService.Instance.OnInventoryChanged += OnInventoryServiceChanged;
                _subscribedInventory = true;
            }
        }

        private void OnDestroy()
        {
            if (InventoryService.Instance != null)
                InventoryService.Instance.OnInventoryChanged -= OnInventoryServiceChanged;
        }

        private void OnInventoryServiceChanged(InventoryChangedEventArgs args)
        {
            NotifyInventoryChanged();
        }
        #endregion

        private static bool _debug = false;
        private bool _subscribedInventory = false;

        //----------------------------------------------------
        // Static Events (UI subscribes, no Update() polling)
        //----------------------------------------------------
        /// <summary>
        /// Raised when a specific card's state changes (level, duplicates, newly acquired).
        /// Parameter: cardId of the affected card.
        /// Use for granular UI updates (single card item, detail panel).
        /// </summary>
        public static event Action<string> OnCardChanged;

        /// <summary>
        /// Raised when the inventory has undergone a global structural change
        /// that requires dependent UI systems to fully refresh.
        /// Use sparingly - only for bulk operations (multi-roll, load game, reset).
        /// </summary>
        public static event Action OnInventoryChanged;
        public static event Action OnEquipmentChanged;
        public static event Action OnCardUpgraded;
        public static event Action OnSlotExpanded;
        public static event Action OnModifierChanged;

        //----------------------------------------------------
        // Internal Event Helpers
        //----------------------------------------------------
        /// <summary>
        /// Notifies subscribers that a specific card has changed.
        /// Safe against null/empty cardId.
        /// </summary>
        private static void NotifyCardChanged(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            OnCardChanged?.Invoke(cardId);
        }

        /// <summary>
        /// Notifies subscribers that the entire inventory has changed structurally.
        /// Call from external systems (e.g., SaveManager after loading).
        /// </summary>
        public static void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

        //----------------------------------------------------
        // Public Properties
        //----------------------------------------------------
        public CardDatabase Database => CardDatabase.Instance;
        public CardInventory Inventory => CardInventory.Instance;

        //----------------------------------------------------
        // Initialization
        //----------------------------------------------------
        public void Initialize()
        {
            CardDatabase.Instance.Initialize();
            CardInventory.Instance.Initialize();
            CardEquipmentService.Instance.Initialize();
            CardModifierService.Refresh();
        }

        public IReadOnlyDictionary<string, CardData> AllCards => Database.AllCards;
        public long CardRollCount => InventoryManager.Instance.GetItemCount("CardRoll");
        //----------------------------------------------------
        // Roll – supports 1, 10, 50, etc.
        //----------------------------------------------------
        public bool Roll(int amount = 1)
        {
            if (amount <= 0) return false;

            long cost = CardRollService.CalculateRollGemCost(amount);
            bool useCardRoll = CardRollCount >= amount;
            if (useCardRoll)
            {
                InventoryManager.Instance.ConsumeItem("CardRoll", amount);
            }
            else
            {
                if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, cost, $"Roll {amount} Card"))
                    return false;
            }

            // Get current pity counters from SaveManager
            var saveManager = SaveManager.Instance;
            int rollsSinceEpic = (saveManager != null) ? saveManager.GetInt(GameConstants.KEY_PITY_EPIC) : 0;
            int rollsSinceLegendary = (saveManager != null) ? saveManager.GetInt(GameConstants.KEY_PITY_LEGENDARY) : 0;
            int rollsSinceMythic = (saveManager != null) ? saveManager.GetInt(GameConstants.KEY_PITY_MYTHIC) : 0;

            // Create virtual inventory snapshot for batch roll
            var virtualInventory = VirtualCardInventorySnapshot.FromInventory(CardInventory.Instance);

            // Perform the roll(s) - PURE function, no mutations
            var batchResult = CardRollService.Roll(
                amount,
                rollsSinceEpic,
                rollsSinceLegendary,
                rollsSinceMythic,
                virtualInventory
            );

            // Persist updated pity counters returned by pure function
            if (saveManager != null)
            {
                saveManager.SetInt(GameConstants.KEY_PITY_EPIC, batchResult.RollResult.RollsSinceEpic);
                saveManager.SetInt(GameConstants.KEY_PITY_LEGENDARY, batchResult.RollResult.RollsSinceLegendary);
                saveManager.SetInt(GameConstants.KEY_PITY_MYTHIC, batchResult.RollResult.RollsSinceMythic);
            }

            // If gems / CardRoll were refunded (all cards at max level), add them back
            // convert CardRoll = 0.5 gems
            long GemRefunded = batchResult.RollResult.GemRefunded;
            if (GemRefunded > 0)
            {
                if (useCardRoll) GemRefunded /= 2;
                EconomyManager.Instance.AddCurrency(CurrencyType.Gem, GemRefunded, $"Refund: All cards at max level");
                if (_debug) Debug.Log($"[CardManager] Refunded {GemRefunded} gems (all cards at max level)");
            }

            // Apply virtual inventory to real inventory (mutations happen here)
            virtualInventory.ApplyToInventory(CardInventory.Instance);

            // Process auto-upgrades for all changed cards
            var changedCardIds = new HashSet<string>(virtualInventory.OwnedCardIds);
            foreach (var cardId in changedCardIds)
            {
                ProcessAutoUpgrade(cardId);
            }

            // Single global notification for bulk operations
            NotifyInventoryChanged();
            RewardManager.Instance.Show(batchResult.RollResult);

            return true;
        }

        /// <summary>
        /// Processes auto-upgrade for a card if it has enough duplicates.
        /// </summary>
        public bool ProcessAutoUpgrade(string cardId)
        {
            bool success = CardUpgradeService.ProcessAutoUpgrade(cardId);
            if (!success) return false;

            CardModifierService.Refresh();
            OnCardUpgraded?.Invoke();
            OnModifierChanged?.Invoke();
            NotifyCardChanged(cardId); // Notify UI that this specific card changed
            CardInventory.Instance.MarkDirty();
            return true;
        }

        //----------------------------------------------------
        // Equip / Unequip
        //----------------------------------------------------
        public bool Equip(string cardId, int slot)
        {
            bool success = CardEquipmentService.Instance.Equip(cardId, slot);
            if (!success) return false;

            // Auto-refresh modifiers and fire events
            CardModifierService.Refresh();
            OnEquipmentChanged?.Invoke();
            OnModifierChanged?.Invoke();

            // Mark dirty for auto-save
            CardInventory.Instance.MarkDirty();
            return true;
        }

        public bool Unequip(int slot)
        {
            bool success = CardEquipmentService.Instance.Unequip(slot);
            if (!success) return false;

            CardModifierService.Refresh();
            OnEquipmentChanged?.Invoke();
            OnModifierChanged?.Invoke();

            CardInventory.Instance.MarkDirty();
            return true;
        }

        //----------------------------------------------------
        // Slot Expansion
        //----------------------------------------------------
        public bool ExpandSlot()
        {
            bool success = CardEquipmentService.Instance.ExpandSlot();
            if (!success) return false;

            OnSlotExpanded?.Invoke();
            CardInventory.Instance.MarkDirty();
            return true;
        }

        private CardEquipmentService _cardEquip = CardEquipmentService.Instance;
        public int EquippedCardCount => _cardEquip.EquippedCardCount;
        public int UnlockedSlotCount => _cardEquip.UnlockedSlotCount;
        public int MaxSlots => _cardEquip.MaxSlots;
        public IReadOnlyList<string> EquippedCards => _cardEquip.EquippedCards;
        public bool IsCardEquipped(string cardId) => _cardEquip.IsEquipped(cardId);
        public int GetEquippedSlot(string cardId) => _cardEquip.GetEquippedSlot(cardId);
        public int GetFirstEmptySlot() => _cardEquip.GetFirstEmptySlot();

        //----------------------------------------------------
        // Scene Navigation
        //----------------------------------------------------
        public void OpenCollection() => SceneLoader.Instance.LoadCardCollection();
    }
}