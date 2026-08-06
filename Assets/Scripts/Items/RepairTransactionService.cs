using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Result of a repair operation.
    /// </summary>
    [Serializable]
    public struct RepairResult
    {
        public bool Success;
        public long TotalCost;
        public int ItemsRepaired;
        public int TotalDurabilityRestored;
        public IRepairCostProvider ProviderUsed;
        public List<RepairedItemInfo> RepairedItems;
        public string FailureReason;

        public static RepairResult SuccessResult(long cost, int count, int durability, IRepairCostProvider provider, List<RepairedItemInfo> items) =>
            new() { Success = true, TotalCost = cost, ItemsRepaired = count, TotalDurabilityRestored = durability, ProviderUsed = provider, RepairedItems = items };

        public static RepairResult Failed(string reason) => new() { Success = false, FailureReason = reason };
    }

    /// <summary>
    /// Information about a single repaired item.
    /// </summary>
    [Serializable]
    public struct RepairedItemInfo
    {
        public string InstanceId;
        public string ItemId;
        public int DurabilityBefore;
        public int DurabilityAfter;
        public int AmountRepaired;
        public long Cost;
        public bool WasFree;
    }

    /// <summary>
    /// Repair transaction service - atomic payment → apply repair → rollback if needed.
    /// Emits domain events only (started / itemRepaired / completed / failed).
    /// </summary>
    public sealed class RepairTransactionService
    {
        /// <summary>Fired when repair process starts.</summary>
        public event Action<RepairMode, int> OnRepairStarted; // mode, itemCount

        /// <summary>Fired when a single item is repaired.</summary>
        public event Action<InventoryItem, int, long, bool> OnItemRepaired; // item, durabilityRestored, cost, wasFree

        /// <summary>Fired when repair batch completes successfully.</summary>
        public event Action<RepairResult> OnRepairCompleted;

        /// <summary>Fired when repair fails (atomic rollback).</summary>
        public event Action<RepairMode, string> OnRepairFailed; // mode, reason

        private readonly RepairConfig _config;
        private readonly RepairCostCalculator _costCalculator;

        public RepairTransactionService(RepairConfig config, RepairCostCalculator costCalculator)
        {
            _config = config;
            _costCalculator = costCalculator;
        }

        /// <summary>
        /// Repairs a collection of items atomically.
        /// Calculates total cost first, then pays once, then repairs all.
        /// </summary>
        /// <param name="items">Items to repair</param>
        /// <param name="mode">Repair mode for events</param>
        /// <returns>Repair result</returns>
        public RepairResult RepairItems(IEnumerable<InventoryItem> items, RepairMode mode = RepairMode.Selected)
        {
            var itemList = items != null
                ? new List<InventoryItem>()
                : null;

            if (itemList != null)
            {
                foreach (var i in items)
                {
                    if (i != null && i.IsEquippable() && i.CurrentDurability < i.MaxDurability)
                        itemList.Add(i);
                }
            }

            if (itemList == null || itemList.Count == 0)
                return RepairResult.SuccessResult(0, 0, 0, null, new List<RepairedItemInfo>());

            OnRepairStarted?.Invoke(mode, itemList.Count);

            // Phase 1: Calculate all costs
            var repairCalculations = new List<RepairCalculation>();
            long totalCost = 0;

            foreach (var item in itemList)
            {
                int needed = item.MaxDurability - item.CurrentDurability;
                if (needed <= 0) continue;

                long cost = _costCalculator.CalculateRepairCost(item, needed);
                bool isFree = _costCalculator.IsFreeRepair(item);

                if (isFree) cost = 0;

                repairCalculations.Add(new RepairCalculation
                {
                    Item = item,
                    NeededDurability = needed,
                    Cost = cost,
                    IsFree = isFree,
                    DurabilityBefore = item.CurrentDurability
                });

                totalCost += cost;
            }

            if (totalCost == 0)
            {
                // All free repairs - apply immediately
                return ApplyFreeRepairs(repairCalculations, mode);
            }

            // Phase 2: Attempt atomic payment
            if (!RepairCostProviderRegistry.TryPay(totalCost, $"Repair {itemList.Count} items ({mode})", out var provider))
            {
                var failedResult = RepairResult.Failed($"Insufficient funds. Need {totalCost:N0}, best provider: {provider?.DisplayName ?? "None"}");
                OnRepairFailed?.Invoke(mode, failedResult.FailureReason);
                return failedResult;
            }

            // Phase 3: Apply all repairs (payment succeeded)
            return ApplyPaidRepairs(repairCalculations, totalCost, provider, mode);
        }

        /// <summary>
        /// Repairs a single item (convenience method).
        /// </summary>
        public RepairResult RepairItem(InventoryItem item)
        {
            return RepairItems(new[] { item }, RepairMode.Selected);
        }

        /// <summary>
        /// Repairs an item by a specific amount.
        /// </summary>
        public RepairResult RepairItemByAmount(InventoryItem item, int amount)
        {
            if (item == null || amount <= 0 || !item.IsEquippable())
                return RepairResult.Failed("Invalid item or amount");

            int actualAmount = Math.Min(amount, item.MaxDurability - item.CurrentDurability);
            if (actualAmount <= 0) return RepairResult.SuccessResult(0, 0, 0, null, new());

            long cost = _costCalculator.CalculateRepairCost(item, actualAmount);
            bool isFree = _costCalculator.IsFreeRepair(item);
            if (isFree) cost = 0;

            if (cost > 0)
            {
                if (!RepairCostProviderRegistry.TryPay(cost, $"Repair {item.ItemId} by {actualAmount}", out var provider))
                    return RepairResult.Failed("Insufficient funds");

                return ApplySingleRepair(item, actualAmount, cost, provider, item.CurrentDurability);
            }

            // Free repair
            DurabilityService.Instance.Repair(item, actualAmount, DurabilityService.DurabilityChangeReason.Repair);
            var info = new RepairedItemInfo
            {
                InstanceId = item.InstanceId,
                ItemId = item.ItemId,
                DurabilityBefore = item.CurrentDurability,
                DurabilityAfter = item.CurrentDurability + actualAmount,
                AmountRepaired = actualAmount,
                Cost = 0,
                WasFree = true
            };
            OnItemRepaired?.Invoke(item, actualAmount, 0, true);
            return RepairResult.SuccessResult(0, 1, actualAmount, null, new List<RepairedItemInfo> { info });
        }

        private RepairResult ApplyFreeRepairs(List<RepairCalculation> calculations, RepairMode mode)
        {
            var repairedItems = new List<RepairedItemInfo>();
            int totalDurability = 0;

            foreach (var calc in calculations)
            {
                int amountRepaired = DurabilityService.Instance.Repair(calc.Item, calc.NeededDurability, DurabilityService.DurabilityChangeReason.Repair);
                if (amountRepaired > 0)
                {
                    var info = new RepairedItemInfo
                    {
                        InstanceId = calc.Item.InstanceId,
                        ItemId = calc.Item.ItemId,
                        DurabilityBefore = calc.DurabilityBefore,
                        DurabilityAfter = calc.Item.CurrentDurability,
                        AmountRepaired = amountRepaired,
                        Cost = 0,
                        WasFree = true
                    };
                    repairedItems.Add(info);
                    totalDurability += amountRepaired;
                    OnItemRepaired?.Invoke(calc.Item, amountRepaired, 0, true);
                }
            }

            var result = RepairResult.SuccessResult(0, repairedItems.Count, totalDurability, null, repairedItems);
            OnRepairCompleted?.Invoke(result);
            return result;
        }

        private RepairResult ApplyPaidRepairs(List<RepairCalculation> calculations, long totalCost, IRepairCostProvider provider, RepairMode mode)
        {
            var repairedItems = new List<RepairedItemInfo>();
            int totalDurability = 0;

            foreach (var calc in calculations)
            {
                int amountRepaired = DurabilityService.Instance.Repair(calc.Item, calc.NeededDurability, DurabilityService.DurabilityChangeReason.Repair);
                if (amountRepaired > 0)
                {
                    var info = new RepairedItemInfo
                    {
                        InstanceId = calc.Item.InstanceId,
                        ItemId = calc.Item.ItemId,
                        DurabilityBefore = calc.DurabilityBefore,
                        DurabilityAfter = calc.Item.CurrentDurability,
                        AmountRepaired = amountRepaired,
                        Cost = calc.Cost,
                        WasFree = calc.IsFree
                    };
                    repairedItems.Add(info);
                    totalDurability += amountRepaired;
                    OnItemRepaired?.Invoke(calc.Item, amountRepaired, calc.Cost, calc.IsFree);
                }
            }

            var result = RepairResult.SuccessResult(totalCost, repairedItems.Count, totalDurability, provider, repairedItems);
            OnRepairCompleted?.Invoke(result);
            return result;
        }

        private RepairResult ApplySingleRepair(InventoryItem item, int amount, long cost, IRepairCostProvider provider, int durabilityBefore)
        {
            int amountRepaired = DurabilityService.Instance.Repair(item, amount, DurabilityService.DurabilityChangeReason.Repair);

            var info = new RepairedItemInfo
            {
                InstanceId = item.InstanceId,
                ItemId = item.ItemId,
                DurabilityBefore = durabilityBefore,
                DurabilityAfter = item.CurrentDurability,
                AmountRepaired = amountRepaired,
                Cost = cost,
                WasFree = false
            };

            OnItemRepaired?.Invoke(item, amountRepaired, cost, false);
            var result = RepairResult.SuccessResult(cost, 1, amountRepaired, provider, new List<RepairedItemInfo> { info });
            OnRepairCompleted?.Invoke(result);
            return result;
        }

        private class RepairCalculation
        {
            public InventoryItem Item;
            public int NeededDurability;
            public long Cost;
            public bool IsFree;
            public int DurabilityBefore;
        }
    }
}