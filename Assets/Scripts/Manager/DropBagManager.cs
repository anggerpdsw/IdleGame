using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Singleton facade over the run DropBag (ItemId -> total quantity).
    /// Cleared ONLY on new run start (WaveManager.InitializeRun). Never persisted.
    /// </summary>
    public sealed class DropBagManager : MonoBehaviour
    {
        private static DropBagManager _instance;
        public static DropBagManager Instance => _instance;

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

        /// <summary>Pure aggregation core (testable without Unity).</summary>
        public DropBag Bag { get; } = new();

        public IReadOnlyDictionary<string, int> Items => Bag.Items;

        public bool IsRunActive
        {
            get => Bag.IsRunActive;
            set => Bag.IsRunActive = value;
        }

        public event Action<string, int> OnDropAdded
        {
            add => Bag.OnDropAdded += value;
            remove => Bag.OnDropAdded -= value;
        }

        public event Action OnCleared
        {
            add => Bag.OnCleared += value;
            remove => Bag.OnCleared -= value;
        }

        public void AddDrop(string itemId, int quantity) => Bag.AddDrop(itemId, quantity);

        /// <summary>Clear runtime data only — NEVER touches InventoryService (no Remove/Clear/Reset).</summary>
        public void Clear() => Bag.Clear();
    }
}
