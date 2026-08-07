using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;
using System.Linq;

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
            _affixDatabase = affixDatabase ?? new Dictionary<string, AffixData>();
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

            for (int i = 0; i < affixCount && availableAffixes.Count > 0; i++)
            {
                var affix = _rng.Choice(availableAffixes);
                availableAffixes.Remove(affix);

                var instance = CreateAffixInstance(affix, rarity, context);
                results.Add(instance);
            }

            return results.ToArray();
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

            foreach (var kvp in _affixDatabase)
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
            { ItemRarity.Uncommon, 1 },
            { ItemRarity.Rare, 1 },
            { ItemRarity.Epic, 2 },
            { ItemRarity.Legendary, 2 },
            { ItemRarity.Mythic, 3 },
            { ItemRarity.Ancient, 3 },
            { ItemRarity.Divine, 4 }
        };

        public Dictionary<ItemRarity, int> MaxTierPerRarity = new()
        {
            { ItemRarity.Common, 1 },
            { ItemRarity.Uncommon, 2 },
            { ItemRarity.Rare, 3 },
            { ItemRarity.Epic, 4 },
            { ItemRarity.Legendary, 5 },
            { ItemRarity.Mythic, 6 },
            { ItemRarity.Ancient, 7 },
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
        public MainStatEntry[] Stats;
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
        public long AcquiredTimestamp;
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