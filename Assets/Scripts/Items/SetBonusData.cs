using System;
using IdleDefenseSurvival.Equipment;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Set bonus data - defines bonuses for equipping multiple pieces of a set.
    /// </summary>
    [Serializable]
    public class SetBonusData
    {
        public string SetId;
        public string SetName;
        [TextArea] public string Description;
        public EquipmentType[] EquipmentTypes; // Which equipment types belong to this set
        public SetBonusTier[] Tiers; // Bonuses at 2/3/4/5/6/8/11 pieces
        public Rarity SetRarity = Rarity.Common; // Visual rarity of the set
        public Sprite SetIcon; // Icon for the set
        public GameObject SetEffectPrefab; // Visual effect when full set equipped

        public int GetRequiredPiecesForTier(int tierIndex) => Tiers?[tierIndex]?.RequiredPieces ?? 0;

        public SetBonusTier GetTierForPieceCount(int pieceCount)
        {
            if (Tiers == null) return null;
            for (int i = Tiers.Length - 1; i >= 0; i--)
            {
                if (pieceCount >= Tiers[i].RequiredPieces)
                    return Tiers[i];
            }
            return null;
        }
    }

    /// <summary>
    /// A single tier of set bonus (e.g., 2-piece, 4-piece, etc.)
    /// </summary>
    [Serializable]
    public class SetBonusTier
    {
        public int RequiredPieces = 2; // 2, 3, 4, 5, 6, 8, 11
        public string TierName; // "2-Piece Bonus", "Full Set Bonus"
        [TextArea] public string Description;
        public AttributeStatEntry[] AttributeBonuses; // Core attribute bonuses at this tier (CON/STR/INT/DEX)
        public CombatStatEntry[] StatBonuses; // Combat stat bonuses at this tier
        public SpecialEffectEntry[] SpecialEffects; // Special effects at this tier
        public PassiveSkillEntry[] PassiveSkills; // Passive skills at this tier
        public GameObject VisualEffect; // Visual effect when this tier active

        public bool IsActive(int equippedCount) => equippedCount >= RequiredPieces;
    }

    /// <summary>
    /// Gem data - defines a gem type and its properties.
    /// </summary>
    [Serializable]
    public class GemData
    {
        public string GemId;
        public string Name;
        [TextArea] public string Description;
        public GemType GemType = GemType.None;
        public Rarity ItemRarity = Rarity.Common;
        public Sprite Icon;
        public string IconKey; // Sprite filename in Resources/Art/Item/
        public int MaxLevel = 10;
        public int BaseExperience = 100;
        public float ExperienceGrowth = 1.5f;

        // Stat generation
        public CombatStatEntry[] BaseStats; // Guaranteed stats
        public CombatStatEntry[] RandomStats; // Random stats (rolled on creation)
        public int RandomStatCount = 1; // How many random stats to roll

        // Upgrade
        public GemUpgradeData UpgradeData;

        // Visual
        public Color GemColor = Color.white;
        public GameObject SocketedEffectPrefab; // Effect when socketed

        public long GetUpgradeCost(int fromLevel, int toLevel)
        {
            if (UpgradeData == null) return 0;
            long total = 0;
            for (int lvl = fromLevel; lvl < toLevel; lvl++)
                total += UpgradeData.GetCost(lvl);
            return total;
        }

        public int GetExperienceForLevel(int level)
        {
            if (level <= 1) return 0;
            float exp = BaseExperience;
            for (int i = 2; i <= level; i++)
                exp *= ExperienceGrowth;
            return Mathf.RoundToInt(exp);
        }
    }

    /// <summary>
    /// Gem upgrade cost data.
    /// </summary>
    [Serializable]
    public class GemUpgradeData
    {
        public long BaseGoldCost = 100;
        public float GoldCostGrowth = 1.3f;
        public long BaseGemCost = 0;
        public float GemCostGrowth = 1.5f;
        public int MaxLevel = 10;

        public long GetCost(int level)
        {
            if (level >= MaxLevel) return 0;
            float cost = BaseGoldCost * Mathf.Pow(GoldCostGrowth, level - 1);
            return Mathf.RoundToInt(cost);
        }
    }

    /// <summary>
    /// Equipment set collection for managing all set bonuses.
    /// </summary>
    [Serializable]
    public class SetCollectionData
    {
        public string CollectionId;
        public string CollectionName;
        public SetBonusData[] Sets;

        public SetBonusData GetSet(string setId)
        {
            if (Sets == null) return null;
            foreach (var set in Sets)
                if (set.SetId == setId) return set;
            return null;
        }
    }
}