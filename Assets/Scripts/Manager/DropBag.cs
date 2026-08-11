using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Pure runtime aggregation of item drops for one game run (ItemId -> total quantity).
    /// No Unity dependencies — EditMode-testable. Owned by DropBagManager.
    /// </summary>
    public sealed class DropBag
    {
        private readonly Dictionary<string, int> _items = new();

        /// <summary>ItemId -> total quantity for the current run.</summary>
        public IReadOnlyDictionary<string, int> Items => _items;

        /// <summary>
        /// False after Victory/Defeat: late monster drop events are ignored.
        /// Toggled by WaveManager (run lifecycle owner).
        /// </summary>
        public bool IsRunActive { get; set; } = true;

        public event Action<string, int> OnDropAdded;
        public event Action OnCleared;

        /// <summary>Record ONE successful drop. Aggregate by ItemId.</summary>
        public void AddDrop(string itemId, int quantity)
        {
            if (!IsRunActive || string.IsNullOrEmpty(itemId) || quantity <= 0) return;
            _items[itemId] = _items.TryGetValue(itemId, out var q) ? q + quantity : quantity;
            OnDropAdded?.Invoke(itemId, _items[itemId]);
        }

        /// <summary>Clear runtime data only — run-start only, never touches InventoryService.</summary>
        public void Clear()
        {
            _items.Clear();
            OnCleared?.Invoke();
        }
    }
}
