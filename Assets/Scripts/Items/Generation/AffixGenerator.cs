using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;
using System.Linq;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for item affixes (prefixes/suffixes that grant stats).
    /// </summary>
    public sealed class AffixGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly AffixGeneratorConfig _config;
        private readonly Dictionary<string, AffixData> _affixDatabase;

        public AffixGenerator(IRandomProvider rng, AffixGeneratorConfig config = null, Dictionary<string, AffixData> affixDatabase = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _config = config ?? AffixGeneratorConfig.Default;
            _affixDatabase = affixDatabase;
        }

        /// <summary>
        /// Affix pool from dataItems.json via ItemDatabase. Empty dict when database
        /// not loaded yet or no affixes defined — generator then yields none.
        /// </summary>
        private IReadOnlyDictionary<string, AffixData> GetDatabase()
        {
            if (_affixDatabase != null) return _affixDatabase;
            return ItemDatabase.Instance?.AllAffixes ?? new Dictionary<string, AffixData>();
        }

        /// <summary>
        /// Generates affixes for an equipment item.
        /// </summary>
        public AffixInstanceData[] GenerateAffixes(EquipmentData baseEquipment, ItemRarity rarity, ItemGenerationContext context)
        {
            int affixCount = GetAffixCount(rarity, context);
            if (affixCount <= 0) return Array.Empty<AffixInstanceData>();

            var results = new List<AffixInstanceData>();
            var availableAffixes = GetAvailableAffixes(baseEquipment, rarity);

            // Prefix + suffix alternate each roll (prefix (Type 0) first, then suffix (Type 1)),
            // so a 2-affix item gets one of each instead of two prefixes. Same-type affixes
            // are removed after each pick — no duplicate prefix/suffix on one item.
            AffixType? lastType = null;
            for (int i = 0; i < affixCount && availableAffixes.Count > 0; i++)
            {
                var affix = PickWeighted(availableAffixes, lastType);
                if (affix == null) break;
                lastType = affix.Type;
                availableAffixes.RemoveAll(a => a.AffixId == affix.AffixId);

                var instance = CreateAffixInstance(affix, rarity, context);
                results.Add(instance);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Weighted pick among pool slots. Prefers the opposite AffixType to the one just
        /// rolled (prefix after suffix, suffix after prefix); falls back to any type.
        /// </summary>
        private AffixData PickWeighted(List<AffixData> pool, AffixType? preferType)
        {
            var candidates = pool;
            if (preferType.HasValue)
            {
                var oppositeType = preferType.Value == AffixType.Prefix ? AffixType.Suffix : AffixType.Prefix;
                var opposite = pool.FindAll(a => a.Type == oppositeType);
                if (opposite.Count > 0) candidates = opposite;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++) totalWeight += Math.Max(0f, candidates[i].Weight);

            float roll = _rng.Range(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Math.Max(0f, candidates[i].Weight);
                if (roll <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private int GetAffixCount(ItemRarity rarity, ItemGenerationContext context)
        {
            int baseCount = _config.AffixCountPerRarity.TryGetValue(rarity, out var c) ? c : 0;

            // Tier bonus
            baseCount += context.Tier / 15;

            // Event modifiers
            if (context.EventModifiers != null)
            {
                foreach (var mod in context.EventModifiers)
                {
                    if (mod is IAffixCountModifier affixMod)
                    {
                        baseCount += affixMod.GetExtraAffixCount(context);
                    }
                }
            }

            return Math.Max(0, baseCount);
        }

        private List<AffixData> GetAvailableAffixes(EquipmentData baseEquipment, ItemRarity rarity)
        {
            var result = new List<AffixData>();

            var db = GetDatabase();
            foreach (var kvp in db)
            {
                var affix = kvp.Value;
                if (affix.MinRarity <= rarity && affix.MaxRarity >= rarity)
                {
                    // Check equipment type compatibility
                    if (affix.ApplicableTypes == null || affix.ApplicableTypes.Length == 0 ||
                        affix.ApplicableTypes.Contains(baseEquipment.EquipmentType))
                    {
                        result.Add(affix);
                    }
                }
            }

            return result;
        }

        private AffixInstanceData CreateAffixInstance(AffixData affix, ItemRarity rarity, ItemGenerationContext context)
        {
            return new AffixInstanceData
            {
                AffixId = affix.AffixId,
                Tier = DetermineAffixTier(rarity, context),
                Level = 1,
                Experience = 0,
                StatValues = GenerateStatValues(affix, rarity, context),
                AttributeValues = GenerateAttributeValues(affix, rarity, context),
                PassiveEffect = affix?.PassiveEffect,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private int DetermineAffixTier(ItemRarity rarity, ItemGenerationContext context)
        {
            int baseTier = (int)rarity;
            int maxTier = _config.MaxTierPerRarity.TryGetValue(rarity, out var m) ? m : baseTier;
            return Math.Min(baseTier + context.Tier / 10, maxTier);
        }

        private Dictionary<SecondaryStat, float> GenerateStatValues(AffixData affix, ItemRarity rarity, ItemGenerationContext context)
        {
            var values = new Dictionary<SecondaryStat, float>();

            if (affix.Stats != null)
            {
                foreach (var statEntry in affix.Stats)
                {
                    float rarityMult = rarity.GetDefaultStatMultiplier();
                    float tierMult = 1f + context.Tier * 0.02f;
                    float variance = _rng.Range(0.9f, 1.1f);

                    float value = statEntry.BaseValue * rarityMult * tierMult * variance;
                    values[statEntry.Stat] = value;
                }
            }

            return values;
        }

        private Dictionary<MainAttribute, float> GenerateAttributeValues(AffixData affix, ItemRarity rarity, ItemGenerationContext context)
        {
            var values = new Dictionary<MainAttribute, float>();

            if (affix?.AttributeStats != null)
            {
                foreach (var attrEntry in affix.AttributeStats)
                {
                    float rarityMult = rarity.GetDefaultStatMultiplier();
                    float tierMult = 1f + context.Tier * 0.02f;
                    float variance = _rng.Range(0.9f, 1.1f);

                    float value = attrEntry.BaseValue * rarityMult * tierMult * variance;
                    values[attrEntry.Attribute] = value;
                }
            }

            return values;
        }
    }

    /// <summary>
    /// Configuration for affix generation.
    /// </summary>
    [Serializable]
    public class AffixGeneratorConfig
    {
        public Dictionary<ItemRarity, int> AffixCountPerRarity = new()
        {
            { ItemRarity.Common, 0 },
            { ItemRarity.Rare, 1 },
            { ItemRarity.Epic, 2 },
            { ItemRarity.Legendary, 2 },
            { ItemRarity.Mythic, 3 },
            { ItemRarity.Divine, 4 }
        };

        public Dictionary<ItemRarity, int> MaxTierPerRarity = new()
        {
            { ItemRarity.Common, 1 },
            { ItemRarity.Rare, 3 },
            { ItemRarity.Epic, 4 },
            { ItemRarity.Legendary, 5 },
            { ItemRarity.Mythic, 6 },
            { ItemRarity.Divine, 8 }
        };

        public static AffixGeneratorConfig Default => new();
    }

    /// <summary>
    /// Affix data definition (loaded from data files).
    /// </summary>
    [Serializable]
    public class AffixData
    {
        public string AffixId;
        public string Name;
        public AffixType Type; // Prefix or Suffix
        public ItemRarity MinRarity;
        public ItemRarity MaxRarity;
        public EquipmentType[] ApplicableTypes;
        public CombatStatEntry[] Stats;            // SecondaryStat bonuses (combat stats)
        public AttributeStatEntry[] AttributeStats; // MainAttribute bonuses (CON/STR/INT/DEX)
        public SpecialEffectEntry PassiveEffect; // Optional on-hit passive (e.g. FreezeEnemy)
        public float Weight = 1f;
    }

    /// <summary>
    /// Instance of an affix on an item.
    /// </summary>
    [Serializable]
    public class AffixInstanceData
    {
        public string AffixId;
        public int Tier;
        public int Level;
        public int Experience;
        public Dictionary<SecondaryStat, float> StatValues;
        public Dictionary<MainAttribute, float> AttributeValues; // from AffixData.AttributeStats
        public SpecialEffectEntry PassiveEffect; // from AffixData.PassiveEffect (same affix, enabled)
        public long AcquiredTimestamp;
        /// <summary>Owning item instance. Stamped on generation so runtime can key
        /// passive-effect activation/deactivation to a concrete equipped item.</summary>
        public string ItemInstanceId;
    }

    public enum AffixType
    {
        Prefix = 0,
        Suffix = 1
    }

    public interface IAffixCountModifier
    {
        int GetExtraAffixCount(ItemGenerationContext context);
    }
}