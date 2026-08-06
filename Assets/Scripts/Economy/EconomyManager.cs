using System;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using UnityEngine;
using UnityEngine.Events;

namespace IdleDefenseSurvival.Economy
{
    /// <summary>
    /// Singleton manager for all currency operations (Gold, Gem, Meat).
    /// Handles currency tracking, save/load, and event notifications.
    /// </summary>
    public class EconomyManager : MonoBehaviour, IEconomyService
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static EconomyManager _instance;
        public static EconomyManager Instance => _instance;

        [SerializeField] private bool _debug = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_debug) Debug.Log($"Economy Awake {gameObject.name}");
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------
        // Events for UI/Systems
        // -------------------------------------------------------------------
        [Serializable] public class CurrencyChangedEvent : UnityEvent<CurrencyType, long, long> { }

        [Header("Events")]
        public CurrencyChangedEvent OnCurrencyChanged { get; } = new();

        // -------------------------------------------------------------------
        // Currency Data
        // -------------------------------------------------------------------
        private CurrencyData _currency = new();

        public long Gold => _currency.gold;
        public long Gem => _currency.gem;
        public long Meat => _currency.meat;

        // -------------------------------------------------------------------
        // Currency Operations
        // -------------------------------------------------------------------

        /// <summary>
        /// Add currency of a specific type.
        /// </summary>
        /// <param name="type">Currency type</param>
        /// <param name="amount">Amount to add</param>
        /// <param name="reason">Optional reason for logging</param>
        public void AddCurrency(CurrencyType type, long amount, string reason = "")
        {
            if (amount <= 0) return;

            long oldAmount = _currency.Get(type);
            _currency.Set(type, oldAmount + amount);

            // Catat earning
            SaveManager.Instance?.AddEarn(type, amount);

            // Fire event
            OnCurrencyChanged?.Invoke(type, oldAmount, _currency.Get(type));

            // Log
            if (!string.IsNullOrEmpty(reason) && _debug)
            {
                Debug.Log($"[Economy] +{amount} {type} from {reason} (Total: {_currency.Get(type)})");
            }
        }

        /// <summary>
        /// Subtract currency if available.
        /// </summary>
        /// <returns>True if successful, false if insufficient funds</returns>
        public bool TrySpendCurrency(CurrencyType type, long amount, string reason = "")
        {
            if (!_currency.HasEnough(type, amount))
            {
                if (_debug)
                    Debug.LogWarning($"[Economy] Insufficient {type}: need {amount}, have {_currency.Get(type)}");
                return false;
            }

            long oldAmount = _currency.Get(type);
            _currency.Set(type, oldAmount - amount);

            // Catat spending
            SaveManager.Instance?.AddSpending(type, amount);
            
            // Fire event
            OnCurrencyChanged?.Invoke(type, oldAmount, _currency.Get(type));

            // Log
            if (!string.IsNullOrEmpty(reason) && _debug)
            {
                Debug.Log($"[Economy] -{amount} {type} for {reason} (Remaining: {_currency.Get(type)})");
            }

            return true;
        }

        /// <summary>
        /// Check if player has enough currency.
        /// </summary>
        public bool HasEnoughCurrency(CurrencyType type, long amount) => _currency.HasEnough(type, amount);

        /// <summary>
        /// Get current currency amount.
        /// </summary>
        public long GetCurrency(CurrencyType type) => _currency.Get(type);

        /// <summary>
        /// Set currency amount directly (use for load/save).
        /// </summary>
        public void SetCurrency(CurrencyType type, long amount)
        {
            long oldAmount = _currency.Get(type);
            _currency.Set(type, amount);
            OnCurrencyChanged?.Invoke(type, oldAmount, amount);
        }

        // -------------------------------------------------------------------
        // Save/Load
        // -------------------------------------------------------------------

        public CurrencyData GetCurrencyData() => _currency;

        public void SetCurrencyData(CurrencyData data)
        {
            if (data == null) return;

            long oldGold = _currency.gold;
            long oldGem  = _currency.gem;
            long oldMeat = _currency.meat;

            _currency = data;

            // Fire events for each currency type
            if (oldGold != _currency.gold)
                OnCurrencyChanged?.Invoke(CurrencyType.Gold, oldGold, _currency.gold);
            if (oldGem != _currency.gem)
                OnCurrencyChanged?.Invoke(CurrencyType.Gem, oldGem, _currency.gem);
            if (oldMeat != _currency.meat)
                OnCurrencyChanged?.Invoke(CurrencyType.Meat, oldMeat, _currency.meat);
        }

    }
}
