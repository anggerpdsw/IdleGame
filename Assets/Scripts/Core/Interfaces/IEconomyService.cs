using System;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Contract for the currency service.
    /// All code that manipulates currency should go through this interface.
    /// </summary>
    public interface IEconomyService
    {
        long Gold { get; }
        long Gem   { get; }
        long Meat  { get; }

        void AddCurrency(CurrencyType type, long amount, string reason = "");
        bool TrySpendCurrency(CurrencyType type, long amount, string reason = "");
        bool HasEnoughCurrency(CurrencyType type, long amount);
        long GetCurrency(CurrencyType type);
        void SetCurrency(CurrencyType type, long amount);

        CurrencyData GetCurrencyData();
        void SetCurrencyData(CurrencyData data);

        event Action<CurrencyType, long, long> OnCurrencyChanged;
    }
}