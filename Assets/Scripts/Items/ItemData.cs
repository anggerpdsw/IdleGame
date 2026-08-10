using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Generation;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Base data class for all items in the game.
    /// Used for both equipment and non-equipment items.
    /// </summary>
    [Serializable]
    public class ItemData
    {
        // ============ Identity ============
        public string Id;
        public string Name;
        [TextArea] public string Description;
        public string FlavorText;

        // ============ Classification ============
        public ItemCategory Category = ItemCategory.None;
        public Rarity ItemRarity = Rarity.Common;
        public EquipmentType EquipmentType = EquipmentType.None; // Only for equipment

        // ============ Visual ============
        public Sprite Icon;
        public string IconKey; // Sprite filename in Resources/Art/Item/ (resolved at load when Icon is null)
        public GameObject Prefab; // World drop prefab
        public Sprite BorderSprite; // Overrides rarity default
        public GameObject GlowEffect; // Overrides rarity default
        public GameObject ParticleEffect; // Overrides rarity default

        // ============ Economy ============
        public long SellPrice = 0;
        public long BuyPrice = 0;
        public float Weight = 1f; // For inventory weight system
        public int StackSize = 1; // Max stack size (1 = non-stackable)

        // ============ Quality/Tier/Progression ============
        public int Tier = 1; // Item tier (1-10+)
        public int StarRating = 0; // Star rating (0-5+)
        public int CorruptionTier = 0; // Corruption tier (0-10+)
        public float QualityMultiplier = 1f; // Quality multiplier (1.0 = normal, 1.5 = superior, etc.)

        // ============ Requirements ============
        public int RequiredLevel = 1;
        public int RequiredTier = 1;
        public string[] RequiredQuests; // Quest IDs that must be completed

        // ============ Level/Progression ============
        public int BaseLevel = 1;
        public int MaxLevel = 100;
        public ItemLevelType[] SupportedLevelTypes; // Which progression systems this item supports

        // ============ Socket/Gem System ============
        public int MaxSockets = 0;
        // AllowedGemTypes moved to SocketConfigData.SocketRules for per-socket flexibility

        // ============ Set System ============
        public string SetId; // Empty = no set

        // ============ Crafting ============
        public CraftRecipeData CraftRecipe; // Null = cannot be crafted

        // ============ Loot ============
        public DropTableData DropTable; // For generating this item as loot

        // ============ Audio ============
        public AudioClip ObtainSound;
        public AudioClip UseSound;
        public AudioClip EquipSound;
        public AudioClip UpgradeSound;

        // ============ Validation ============
        public bool IsValid() =>
            !string.IsNullOrEmpty(Id) &&
            !string.IsNullOrEmpty(Name) &&
            Category != ItemCategory.None &&
            ItemRarity != Rarity.None;

        public bool IsEquipment => Category == ItemCategory.Equipment && EquipmentType != EquipmentType.None;
        public bool IsStackable => StackSize > 1;
        public bool HasSockets => MaxSockets > 0;
        public bool HasSet => !string.IsNullOrEmpty(SetId);
    }

    /// <summary>
    /// Equipment-specific data extending ItemData.
    /// </summary>
    [Serializable]
    public class EquipmentData : ItemData
    {
        // ============ Attribute Stats ============
        public AttributeStatEntry[] AttributeStats; // Primary: 4 core attributes (CON/STR/INT/DEX)

        // ============ Combat Stats ============
        // The 4 base attributes (CON/STR/INT/DEX) live in AttributeStats above.
        // This is the item's flat combat/specialization stats (Crit, LifeSteal,
        // CooldownReduction, element damage, ...) — not "main attributes".
        public CombatStatEntry[] CombatStats; // Secondary combat stats (Crit, LifeSteal, AttackSpeed, ...)

        // ============ Secondary Stats (roll pool) ============
        // Candidate SecondaryStats rolled at generation into item.CustomData["SecondaryStats"].
        public SecondaryStatRow[] SecondaryStats; // Additional stat modifiers

        // ============ Special Effects ============
        public SpecialEffectEntry[] SpecialEffects; // Passive/triggered effects

        // ============ Passive Skills ============
        public PassiveSkillEntry[] PassiveSkills; // Passive skill bonuses

        // ============ Durability ============
        // Equipment-only: non-equipment items (consumables, materials) have no durability.
        public int MaxDurability = 100;
        public int DurabilityLossPerUse = 1;
        public long RepairCostPerDurability = 10;

        // ============ Enchantment ============
        public EnchantmentData BaseEnchantment; // Fixed enchantment on this equipment

        // ============ Visual (Equipment-specific) ============
        public GameObject EquippedModelPrefab; // 3D/2D model when equipped
        public Vector3 EquippedPositionOffset;
        public Vector3 EquippedRotationOffset;
        public Vector3 EquippedScale = Vector3.one;

        // ============ Upgrade ============
        public UpgradeCurveData UpgradeCurve; // How stats scale with level/enhance

        public void InitializeDefaults()
        {
            Category = ItemCategory.Equipment;
            if (AttributeStats == null) AttributeStats = Array.Empty<AttributeStatEntry>();
            if (CombatStats == null) CombatStats = Array.Empty<CombatStatEntry>();
            if (SecondaryStats == null) SecondaryStats = Array.Empty<SecondaryStatRow>();
            if (SpecialEffects == null) SpecialEffects = Array.Empty<SpecialEffectEntry>();
            if (PassiveSkills == null) PassiveSkills = Array.Empty<PassiveSkillEntry>();
            // AllowedGemTypes moved to SocketConfigData.SocketRules
            if (RequiredQuests == null) RequiredQuests = Array.Empty<string>();
        }
    }

    /// <summary>
    /// Main stat entry - defines a single main stat with value and scaling.
    /// </summary>
    [Serializable]
    public class CombatStatEntry
    {
        public SecondaryStat Stat = SecondaryStat.None;
        public float BaseValue = 0f;
        public float ValuePerLevel = 0f; // Scaling per level
        public float ValuePerEnhance = 0f; // Scaling per enhance level
        public SecondaryStatMode Mode = SecondaryStatMode.Flat;
        public bool IsPercent = false; // Legacy - use Mode instead

        public float GetValue(int level, int enhanceLevel = 0)
        {
            float value = BaseValue + ValuePerLevel * (level - 1) + ValuePerEnhance * enhanceLevel;
            return Mode == SecondaryStatMode.Percent ? value * 0.01f : value;
        }
    }

    /// <summary>
    /// Secondary stat row - defines a stat modifier with complex application mode.
    /// Renamed from SecondaryStatEntry to avoid collision with the SecondaryStat enum.
    /// </summary>
    [Serializable]
    public class SecondaryStatRow
    {
        public SecondaryStat Stat = SecondaryStat.None;
        public float Value = 0f;
        public SecondaryStatMode Mode = SecondaryStatMode.Flat;
        public string Condition; // For Conditional mode - JSON condition string

        public float Apply(float baseValue)
        {
            return Mode.Calculate(baseValue, Value);
        }
    }

    /// <summary>
    /// Attribute stat entry - one of the four core attributes with scaling.
    /// Feeds the attribute pool (AttributeModifierManager) instead of ModifierManager directly.
    /// </summary>
    [Serializable]
    public class AttributeStatEntry
    {
        public MainAttribute Attribute = MainAttribute.Constitution;
        public float BaseValue = 0f;
        public float ValuePerLevel = 0f; // Scaling per level
        public float ValuePerEnhance = 0f; // Scaling per enhance level

        public float GetValue(int level, int enhanceLevel = 0)
        {
            return BaseValue + ValuePerLevel * (level - 1) + ValuePerEnhance * enhanceLevel;
        }
    }

    /// <summary>
    /// Special effect entry - defines a passive/triggered effect.
    /// </summary>
    [Serializable]
    public class SpecialEffectEntry
    {
        public SpecialEffectType EffectType = SpecialEffectType.None;
        public float Value = 1f; // Effect magnitude
        public float Chance = 100f; // Trigger chance (0-100)
        public float Cooldown = 0f; // Internal cooldown in seconds
        public string[] Conditions; // Additional conditions (JSON)
        public int RequiredLevel = 1; // Minimum item level to activate
        public int RequiredEnhance = 0; // Minimum enhance level to activate
        public bool IsActive = true; // Can be toggled

        public bool CanActivate(int itemLevel, int enhanceLevel) =>
            IsActive && itemLevel >= RequiredLevel && enhanceLevel >= RequiredEnhance;
    }

    /// <summary>
    /// Passive skill entry - defines a passive skill bonus.
    /// </summary>
    [Serializable]
    public class PassiveSkillEntry
    {
        public string SkillId;
        public string SkillName;
        [TextArea] public string Description;
        public int MaxLevel = 10;
        public float ValuePerLevel = 1f;
        public SecondaryStatMode Mode = SecondaryStatMode.Flat;
        public SecondaryStat[] AffectedStats; // Which stats this passive affects
    }

    /// <summary>
    /// Enchantment data - fixed enchantment on equipment.
    /// </summary>
    [Serializable]
    public class EnchantmentData
    {
        public string EnchantmentId;
        public CombatStatEntry[] StatBonuses;
        public SpecialEffectEntry[] Effects;
        public int Level = 1;
        public int MaxLevel = 5;
    }

    /// <summary>
    /// Upgrade curve data - defines how stats scale with upgrades.
    /// </summary>
    [Serializable]
    public class UpgradeCurveData
    {
        public AnimationCurve LevelCurve = AnimationCurve.Linear(0, 1, 100, 2);
        public AnimationCurve EnhanceCurve = AnimationCurve.Linear(0, 1, 20, 3);
        public AnimationCurve LimitBreakCurve = AnimationCurve.Linear(0, 1, 5, 5);
        public float RarityMultiplier = 1f;
    }

    /// <summary>
    /// Drop table data for loot generation.
    /// </summary>
    [Serializable]
    public class DropTableData
    {
        public string TableId;
        public string TableName;
        public DropEntry[] Entries;
        public float GlobalWeight = 1f;
        public DropCondition[] Conditions;

        /// <summary>
        /// Rolls this drop table and returns generated items.
        /// </summary>
        public InventoryItem[] Roll(int tier, int wave, float luckBonus = 0f, int maxItems = 5)
        {
            var items = new List<InventoryItem>();

            if (Entries == null || Entries.Length == 0) return items.ToArray();

            // Check conditions
            if (Conditions != null && Conditions.Length > 0)
            {
                foreach (var condition in Conditions)
                {
                    if (!condition.Check(tier, wave)) return items.ToArray();
                }
            }

            float qualityMod = 1f + tier * 0.05f + luckBonus;

            foreach (var entry in Entries)
            {
                if (!ShouldDrop(entry, qualityMod)) continue;

                int count = UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1);
                for (int i = 0; i < count && items.Count < maxItems; i++)
                {
                    var item = GenerateItem(entry, tier, qualityMod);
                    if (item != null) items.Add(item);
                }
            }

            return items.ToArray();
        }

        private bool ShouldDrop(DropEntry entry, float qualityMod)
        {
            if (entry == null) return false;

            // Weight-based roll (0-100 scale)
            float effectiveWeight = entry.Weight * qualityMod;
            float roll = UnityEngine.Random.Range(0f, 100f);
            return roll < effectiveWeight;
        }

        private InventoryItem GenerateItem(DropEntry entry, int tier, float qualityMod)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ItemId)) return null;

            var itemData = ItemDatabase.Instance?.GetItem(entry.ItemId);
            if (itemData == null) return null;

            int level = UnityEngine.Random.Range(entry.MinLevel, entry.MaxLevel + 1);
            Rarity rarity = DetermineRarity(entry.MinRarity, entry.MaxRarity, qualityMod);

            return itemData.Category switch
            {
                ItemCategory.Equipment => itemData is EquipmentData equipmentData ?
                    ItemGenerator.Instance.Equipment.Generate(equipmentData, ItemGenerationContext.Equipment(equipmentData.EquipmentType, rarity, level, tier)) : null,
                ItemCategory.Gem => ItemDatabase.Instance?.GetGem(entry.ItemId) is GemData gemData ?
                    ItemGenerator.Instance.Gem.Generate(gemData, ItemGenerationContext.Gem(gemData.GemType, rarity, level)) : null,
                ItemCategory.Consumable or ItemCategory.Material or ItemCategory.UpgradeStone or ItemCategory.SkillBook
                    => ItemGenerator.Instance.Consumable.Generate(itemData, ItemGenerationContext.Drop(tier, 0).With(category: itemData.Category, forcedQuality: (int)rarity)),
                _ => new InventoryItem
                {
                    ItemId = entry.ItemId,
                    Quantity = UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1),
                    Level = level,
                    AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };
        }

        private Rarity DetermineRarity(Rarity minRarity, Rarity maxRarity, float qualityMod)
        {
            int min = (int)minRarity;
            int max = (int)maxRarity;
            max = Mathf.Min(8, max + Mathf.RoundToInt(qualityMod * 2));
            int roll = UnityEngine.Random.Range(min, max + 1);
            return (Rarity)Math.Clamp(roll, min, max);
        }
    }

    /// <summary>
    /// Condition for drop table entries.
    /// </summary>
    [Serializable]
    public class DropCondition
    {
        public string ConditionType; // "Tier", "Wave", "Quest", "Flag"
        public string Parameter;
        public float MinValue;
        public float MaxValue = float.MaxValue;

        public bool Check(int tier, int wave)
        {
            float value = ConditionType switch
            {
                "Tier" => tier,
                "Wave" => wave,
                "Quest" => CheckQuest(Parameter) ? 1f : 0f,
                "Flag" => CheckFlag(Parameter) ? 1f : 0f,
                _ => 0f
            };

            return value >= MinValue && value <= MaxValue;
        }

        private bool CheckQuest(string _questId)
        {
            // TODO: Implement quest checking
            return true;
        }

        private bool CheckFlag(string _flagId)
        {
            // TODO: Implement flag checking
            return true;
        }
    }

    /// <summary>
    /// Entry for a drop table - defines what can drop and with what probability.
    /// </summary>
    [Serializable]
    public class DropEntry
    {
        public string ItemId;
        public int MinCount = 1;
        public int MaxCount = 1;
        public float Weight = 1f;
        public int MinLevel = 1;
        public int MaxLevel = 1;
        public Rarity MinRarity = Rarity.Common;
        public Rarity MaxRarity = Rarity.Common;
        public string Condition; // JSON condition for conditional drops
    }

    /// <summary>
    /// Gem types for socket system.
    /// </summary>
    public enum GemType
    {
        None = 0,
        Ruby = 1,        // Fire/Attack
        Sapphire = 2,    // Water/Crit
        Emerald = 3,     // Wind/Dodge
        Topaz = 4,       // Earth/Defense
        Amethyst = 5,    // Lightning/Speed
        Diamond = 6,     // Light/HP
        Onyx = 7,        // Dark/Lifesteal
        Pearl = 8,       // Holy/Shield
        Opal = 9,        // Chaos/Random
        Prismatic = 10,  // Universal/Any
    }

    public static class GemTypeExtensions
    {
        public static string GetDisplayName(this GemType type) => type switch
        {
            GemType.Ruby => "Ruby",
            GemType.Sapphire => "Sapphire",
            GemType.Emerald => "Emerald",
            GemType.Topaz => "Topaz",
            GemType.Amethyst => "Amethyst",
            GemType.Diamond => "Diamond",
            GemType.Onyx => "Onyx",
            GemType.Pearl => "Pearl",
            GemType.Opal => "Opal",
            GemType.Prismatic => "Prismatic",
            _ => "Unknown"
        };

        public static Color GetColor(this GemType type) => type switch
        {
            GemType.Ruby => GameColors.gemRuby,
            GemType.Sapphire => GameColors.gemSapphire,
            GemType.Emerald => GameColors.gemEmerald,
            GemType.Topaz => GameColors.gemTopaz,
            GemType.Amethyst => GameColors.gemAmethyst,
            GemType.Diamond => GameColors.gemDiamond,
            GemType.Onyx => GameColors.gemOnyx,
            GemType.Pearl => GameColors.gemPearl,
            GemType.Opal => GameColors.gemOpal,
            GemType.Prismatic => GameColors.gemPrismatic,
            _ => Color.white
        };
    }
}