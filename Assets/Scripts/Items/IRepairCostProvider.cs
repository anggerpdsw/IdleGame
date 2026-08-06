using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Interface for repair cost providers.
    /// Allows different payment methods: Gold, Repair Kits, Premium Currency, etc.
    /// Providers are checked in priority order.
    /// </summary>
    public interface IRepairCostProvider
    {
        /// <summary>
        /// Provider priority (lower = higher priority, checked first).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Display name for UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Icon/resource key for UI.
        /// </summary>
        string IconKey { get; }

        /// <summary>
        /// Checks if this provider can cover the cost.
        /// </summary>
        bool CanPay(long cost);

        /// <summary>
        /// Gets available amount for this provider.
        /// </summary>
        long GetAvailableAmount();

        /// <summary>
        /// Pays the cost using this provider.
        /// </summary>
        /// <param name="cost">Cost to pay</param>
        /// <param name="reason">Reason for payment</param>
        /// <returns>True if payment successful</returns>
        bool Pay(long cost, string reason);

        /// <summary>
        /// Gets a preview of what would be consumed (for UI tooltips).
        /// </summary>
        string GetPaymentPreview(long cost);
    }

    /// <summary>
    /// Repair cost provider using Gold currency.
    /// </summary>
    public sealed class GoldRepairCostProvider : IRepairCostProvider
    {
        public int Priority => 100;
        public string DisplayName => "Gold";
        public string IconKey => "currency_gold";

        public bool CanPay(long cost) => EconomyManager.Instance?.HasEnoughCurrency(CurrencyType.Gold, cost) ?? false;

        public long GetAvailableAmount() => EconomyManager.Instance?.Gold ?? 0;

        public bool Pay(long cost, string reason)
        {
            if (!CanPay(cost)) return false;
            return EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, reason);
        }

        public string GetPaymentPreview(long cost) => $"{cost:N0} Gold";
    }

    /// <summary>
    /// Repair cost provider using Repair Kit items.
    /// </summary>
    public sealed class RepairKitCostProvider : IRepairCostProvider
    {
        private const string RepairKitItemId = "repair_kit";

        public int Priority => 50; // Higher priority than gold
        public string DisplayName => "Repair Kit";
        public string IconKey => "item_repair_kit";

        public bool CanPay(long cost)
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return false;

            int kitCount = inventory.GetTotalQuantity(RepairKitItemId);
            // 1 Repair Kit = 100 durability points worth of repair
            long kitValue = kitCount * 100;
            return kitValue >= cost;
        }

        public long GetAvailableAmount()
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return 0;

            int kitCount = inventory.GetTotalQuantity(RepairKitItemId);
            return kitCount * 100;
        }

        public bool Pay(long cost, string reason)
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return false;

            int kitCount = inventory.GetTotalQuantity(RepairKitItemId);
            long kitValue = kitCount * 100;

            if (kitValue < cost) return false;

            // Calculate how many kits needed (each kit = 100 durability points)
            int kitsNeeded = Mathf.CeilToInt(cost / 100f);
            kitsNeeded = Math.Min(kitsNeeded, kitCount);

            return inventory.RemoveItemById(RepairKitItemId, kitsNeeded) > 0;
        }

        public string GetPaymentPreview(long cost)
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return "No kits";

            int kitCount = inventory.GetTotalQuantity(RepairKitItemId);
            int kitsNeeded = Mathf.CeilToInt(cost / 100f);
            return $"{kitsNeeded} Repair Kit{(kitsNeeded > 1 ? "s" : "")} (have {kitCount})";
        }
    }

    /// <summary>
    /// Repair cost provider using Premium Currency (Gems).
    /// </summary>
    public sealed class GemRepairCostProvider : IRepairCostProvider
    {
        public int Priority => 200; // Lower priority (last resort)
        public string DisplayName => "Gems";
        public string IconKey => "currency_gem";

        public bool CanPay(long cost) => EconomyManager.Instance?.HasEnoughCurrency(CurrencyType.Gem, cost) ?? false;

        public long GetAvailableAmount() => EconomyManager.Instance?.Gem ?? 0;

        public bool Pay(long cost, string reason)
        {
            if (!CanPay(cost)) return false;
            return EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, cost, reason);
        }

        public string GetPaymentPreview(long cost) => $"{cost:N0} Gems";
    }

    /// <summary>
    /// Registry for repair cost providers.
    /// Manages provider priority and selection.
    /// </summary>
    public static class RepairCostProviderRegistry
    {
        private static readonly List<IRepairCostProvider> _providers = new();
        private static bool _initialized = false;

        public static IReadOnlyList<IRepairCostProvider> Providers => _providers;

        public static void Initialize()
        {
            if (_initialized) return;

            _providers.Clear();
            _providers.Add(new RepairKitCostProvider());
            _providers.Add(new GoldRepairCostProvider());
            _providers.Add(new GemRepairCostProvider());

            // Sort by priority (ascending)
            _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            _initialized = true;
        }

        public static void RegisterProvider(IRepairCostProvider provider)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
                _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        public static void UnregisterProvider(IRepairCostProvider provider) => _providers.Remove(provider);

        /// <summary>
        /// Gets the best available provider for a given cost.
        /// </summary>
        public static IRepairCostProvider GetBestProvider(long cost)
        {
            Initialize();
            foreach (var provider in _providers)
            {
                if (provider.CanPay(cost))
                    return provider;
            }
            return null;
        }

        /// <summary>
        /// Gets all providers that can pay the cost.
        /// </summary>
        public static IEnumerable<IRepairCostProvider> GetAvailableProviders(long cost)
        {
            Initialize();
            foreach (var provider in _providers)
            {
                if (provider.CanPay(cost))
                    yield return provider;
            }
        }

        /// <summary>
        /// Attempts to pay using the best available provider.
        /// </summary>
        public static bool TryPay(long cost, string reason, out IRepairCostProvider usedProvider)
        {
            Initialize();
            usedProvider = null;

            foreach (var provider in _providers)
            {
                if (provider.CanPay(cost))
                {
                    if (provider.Pay(cost, reason))
                    {
                        usedProvider = provider;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}