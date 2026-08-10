using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Read-only socket configuration interface.
    /// Prevents runtime mutation of socket rules.
    /// </summary>
    public interface IReadOnlySocketConfig
    {
        int MaxSocketsPerItem { get; }
        IReadOnlyList<SocketRule> SocketRules { get; }
        bool CanAddSockets { get; }
        SocketCurrencyCost AddSocketCost { get; }
        int MaxAdditionalSockets { get; }
        bool CanRemoveGems { get; }
        bool CanDestroyGems { get; }
        float GemRemovalGoldCost { get; }
        float GemDestructionReturnRate { get; }
        bool IsSocketUnlocked(int socketIndex, int enhanceLevel);
        int GetUnlockRequirement(int socketIndex);
    }

    /// <summary>
    /// Currency cost for socket operations.
    /// </summary>
    [Serializable]
    public sealed class SocketCurrencyCost
    {
        public CurrencyType CurrencyType = CurrencyType.Gold;
        public long Amount = 0;
    }

    /// <summary>
    /// Socket rule - immutable configuration loaded from JSON.
    /// </summary>
    [Serializable]
    public sealed class SocketRule
    {
        public int SocketIndex;
        public int UnlockEnhanceLevel;
        public int UnlockPlayerLevel = 1;
        public Rarity MinimumRarity = Rarity.Common;
        public bool AllowAnyGem = true;
        public GemType[] AllowedGemTypes = Array.Empty<GemType>();
        public bool IsLocked;
        public string SocketShape = "Circle";
        public Color SocketColor = Color.white;

        public bool CanInsertGem(GemType gemType)
        {
            if (AllowAnyGem) return true;
            if (AllowedGemTypes == null || AllowedGemTypes.Length == 0) return false;
            return Array.Exists(AllowedGemTypes, t => t == gemType);
        }
    }

    /// <summary>
    /// Socket configuration loaded from JSON.
    /// </summary>
    [Serializable]
    public sealed class SocketConfigData : IReadOnlySocketConfig
    {
        public int MaxSocketsPerItem = 4;
        public SocketRule[] SocketRules = Array.Empty<SocketRule>();
        public bool CanAddSockets = true;
        public SocketCurrencyCost AddSocketCost = new() { CurrencyType = CurrencyType.Gold, Amount = 10000 };
        public int MaxAdditionalSockets = 2;
        public bool CanRemoveGems = true;
        public bool CanDestroyGems = true;
        public float GemRemovalGoldCost = 100f;
        public float GemDestructionReturnRate = 0.5f;

        IReadOnlyList<SocketRule> IReadOnlySocketConfig.SocketRules => SocketRules;
        SocketCurrencyCost IReadOnlySocketConfig.AddSocketCost => AddSocketCost;

        int IReadOnlySocketConfig.MaxSocketsPerItem => MaxSocketsPerItem;
        bool IReadOnlySocketConfig.CanAddSockets => CanAddSockets;
        int IReadOnlySocketConfig.MaxAdditionalSockets => MaxAdditionalSockets;
        bool IReadOnlySocketConfig.CanRemoveGems => CanRemoveGems;
        bool IReadOnlySocketConfig.CanDestroyGems => CanDestroyGems;
        float IReadOnlySocketConfig.GemRemovalGoldCost => GemRemovalGoldCost;
        float IReadOnlySocketConfig.GemDestructionReturnRate => GemDestructionReturnRate;

        public bool IsSocketUnlocked(int socketIndex, int enhanceLevel)
        {
            if (SocketRules == null) return false;
            if (socketIndex < 0 || socketIndex >= SocketRules.Length) return false;
            return enhanceLevel >= SocketRules[socketIndex].UnlockEnhanceLevel;
        }

        public int GetUnlockRequirement(int socketIndex)
        {
            if (SocketRules == null) return int.MaxValue;
            if (socketIndex < 0 || socketIndex >= SocketRules.Length) return int.MaxValue;
            return SocketRules[socketIndex].UnlockEnhanceLevel;
        }
    }
}