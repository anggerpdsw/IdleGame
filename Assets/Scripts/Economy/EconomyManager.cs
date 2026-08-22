using System;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Mission;
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
        [SerializeField] private CurrencyChangedEvent _onCurrencyChanged = new();
        public CurrencyChangedEvent OnCurrencyChanged => _onCurrencyChanged;

        // IEconomyService.OnCurrencyChanged — explicit interface implementation.
        // Action backing field is the source of truth; bridge dispatches to both
        // Action subscribers (interface consumers) and UnityEvent subscribers (Inspector/UI).
        private event Action<CurrencyType, long, long> _currencyChanged;
        event Action<CurrencyType, long, long> IEconomyService.OnCurrencyChanged
        {
            add { _currencyChanged += value; }
            remove { _currencyChanged -= value; }
        }

        private void RaiseCurrencyChanged(CurrencyType type, long oldAmount, long newAmount)
        {
            _currencyChanged?.Invoke(type, oldAmount, newAmount);
            _onCurrencyChanged?.Invoke(type, oldAmount, newAmount);
        }

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

            // Overflow guard: amount + oldAmount must not wrap past long.MaxValue.
            long oldAmount = _currency.Get(type);
            if (amount > long.MaxValue - oldAmount)
            {
                if (_debug)
                    Debug.LogWarning($"[Economy] {type} overflow prevented (have {oldAmount}, add {amount}).");
                return;
            }

            long newAmount = oldAmount + amount;
            _currency.Set(type, newAmount);

            // Catat earning
            SaveManager.Instance?.AddEarn(type, amount);

            // =====================================================
            // MISSION PROGRESS
            // Currency sudah benar-benar berhasil ditambahkan.
            // =====================================================
            MissionService.Instance?.UpdateProgress(
                MissionEventType.CurrencyEarned, type.ToString(), amount);

            // Fire event (action + UnityEvent)
            RaiseCurrencyChanged(type, oldAmount, newAmount);

            // Log
            if (!string.IsNullOrEmpty(reason) && _debug)
            {
                Debug.Log($"[Economy] +{amount} {type} from {reason} (Total: {newAmount})");
            }
        }

        /// <summary>
        /// Subtract currency if available.
        /// </summary>
        /// <returns>True if successful, false if insufficient funds</returns>
        public bool TrySpendCurrency(CurrencyType type, long amount, string reason = "")
        {
            if (amount <= 0) return false;

            if (!_currency.HasEnough(type, amount))
            {
                if (_debug)
                    Debug.LogWarning($"[Economy] Insufficient {type}: need {amount}, have {_currency.Get(type)}");
                return false;
            }

            long oldAmount = _currency.Get(type);
            long newAmount = oldAmount - amount;
            _currency.Set(type, newAmount);

            // Catat spending
            SaveManager.Instance?.AddSpending(type, amount);

            // Fire event (action + UnityEvent)
            RaiseCurrencyChanged(type, oldAmount, newAmount);

            // Log
            if (!string.IsNullOrEmpty(reason) && _debug)
            {
                Debug.Log($"[Economy] -{amount} {type} for {reason} (Remaining: {newAmount})");
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
            long validated = amount < 0 ? 0 : amount;
            _currency.Set(type, validated);
            RaiseCurrencyChanged(type, oldAmount, validated);
        }

        // -------------------------------------------------------------------
        // Save/Load
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns a defensive copy so external code cannot mutate Economy state by reference.
        ///</summary>
        public CurrencyData GetCurrencyData() => new(
            _currency.gold,
            _currency.gem,
            _currency.meat);

        public void SetCurrencyData(CurrencyData data)
        {
            if (data == null) return;

            long oldGold = _currency.gold;
            long oldGem  = _currency.gem;
            long oldMeat = _currency.meat;

            // Defensive copy + clamp negatives so a tampered save cannot poison runtime state.
            _currency = new CurrencyData(
                Math.Max(0L, data.gold),
                Math.Max(0L, data.gem),
                Math.Max(0L, data.meat));

            // Fire events for each currency type (action + UnityEvent)
            if (oldGold != _currency.gold)
                RaiseCurrencyChanged(CurrencyType.Gold, oldGold, _currency.gold);
            if (oldGem != _currency.gem)
                RaiseCurrencyChanged(CurrencyType.Gem, oldGem, _currency.gem);
            if (oldMeat != _currency.meat)
                RaiseCurrencyChanged(CurrencyType.Meat, oldMeat, _currency.meat);
        }

    }
}
