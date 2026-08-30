using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Pure stat aggregation: main stats, enchant, socket gems, set bonuses.
    /// No ModifierManager or PlayerStatsManager dependencies.
    ///
    /// Hierarchy:
    /// Main Attribute (STR/CON/INT/DEX) -> SkillType (combat runtime stats) <- 80% power
    /// SecondaryStat (equipment specialization) -> SkillType mapping <- 20% power
    /// </summary>
    public static class EquipmentStatCalculator
    {
        public static Dictionary<SecondaryStat, float> GetItemStatBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<SecondaryStat, float>();
            var itemData = item.GetEquipmentData();
            if (itemData == null) return bonuses;

            if (itemData.CombatStats != null)
                foreach (var statEntry in itemData.CombatStats)
                    Add(bonuses, statEntry.Stat, statEntry.GetValue(item.Level));

            // Gem stats
            if (item.Sockets != null)
            {
                foreach (var socket in item.Sockets)
                {
                    if (socket.IsEmpty) continue;
                    var gemStats = GemService.Instance?.GetGemStats(socket.GemId, socket.GemLevel);
                    if (gemStats == null) continue;
                    foreach (var statEntry in gemStats)
                        Add(bonuses, statEntry.Stat, statEntry.GetValue(socket.GemLevel));
                }
            }

            return bonuses;
        }

        public static Dictionary<SecondaryStat, float> GetSetStatBonuses(ItemDatabase db,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<SecondaryStat, float>();

            foreach (var (setId, count) in setPieceCounts)
            {
                var setData = db?.GetSet(setId);
                if (setData?.Tiers == null) continue;

                foreach (var tier in setData.Tiers.Where(t => t.IsActive(count)))
                {
                    if (tier.StatBonuses == null) continue;
                    foreach (var statEntry in tier.StatBonuses)
                        Add(totals, statEntry.Stat, statEntry.GetValue(1));
                }
            }

            return totals;
        }

        public static Dictionary<SecondaryStat, float> GetTotalStatBonuses(ItemDatabase db,
            IReadOnlyDictionary<EquipmentType, InventoryItem> equippedItems,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<SecondaryStat, float>();

            foreach (var item in equippedItems.Values)
            {
                foreach (var (stat, value) in GetItemStatBonuses(item))
                    Add(totals, stat, value);
            }

            var setBonuses = GetSetStatBonuses(db, setPieceCounts);
            foreach (var (stat, value) in setBonuses)
                Add(totals, stat, value);

            return totals;
        }

        /// <summary>Aggregate a single item's bonuses incl. its would-be set tier (for auto-equip scoring).</summary>
        public static Dictionary<SecondaryStat, float> GetItemBonusesWithSet(InventoryItem item,
            ItemDatabase db, IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var bonuses = GetItemStatBonuses(item);

            string setId = item.GetSetId();
            if (string.IsNullOrEmpty(setId) || db?.GetSet(setId)?.Tiers == null) return bonuses;

            int newCount = setPieceCounts.GetValueOrDefault(setId, 0) + 1;
            foreach (var tier in db.GetSet(setId).Tiers.Where(t => t.IsActive(newCount)))
            {
                if (tier.StatBonuses == null) continue;
                foreach (var statEntry in tier.StatBonuses)
                    Add(bonuses, statEntry.Stat, statEntry.GetValue(1));
            }

            return bonuses;
        }

        public static Dictionary<MainAttribute, float> GetItemAttributeBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<MainAttribute, float>();
            var itemData = item.GetEquipmentData();
            if (itemData == null) return bonuses;

            // Main Attributes from itemData (ItemDatabase) — base template
            if (itemData.AttributeStats != null)
                foreach (var attrEntry in itemData.AttributeStats)
                    AddAsAttribute(bonuses, attrEntry.Attribute, attrEntry.GetValue(item.Level));

            // Main Attributes from item.AttributeData (instance data - e.g. equipping gives STR +6)
            if (item.AttributeData?.MainAttribute != null)
                foreach (var attrEntry in item.AttributeData.MainAttribute)
                    AddAsAttribute(bonuses, attrEntry.Attribute, attrEntry.BaseValue);

            return bonuses;
        }

        public static Dictionary<MainAttribute, float> GetSetAttributeBonuses(ItemDatabase db,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<MainAttribute, float>();

            foreach (var (setId, count) in setPieceCounts)
            {
                var setData = db?.GetSet(setId);
                if (setData?.Tiers == null) continue;

                foreach (var tier in setData.Tiers.Where(t => t.IsActive(count)))
                {
                    if (tier.AttributeBonuses == null) continue;
                    foreach (var attrEntry in tier.AttributeBonuses)
                        AddAsAttribute(totals, attrEntry.Attribute, attrEntry.GetValue(1));
                }
            }

            return totals;
        }

        public static Dictionary<MainAttribute, float> GetTotalAttributeBonuses(ItemDatabase db,
            IReadOnlyDictionary<EquipmentType, InventoryItem> equippedItems,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<MainAttribute, float>();

            foreach (var item in equippedItems.Values)
            {
                foreach (var (attr, value) in GetItemAttributeBonuses(item))
                    AddAsAttribute(totals, attr, value);
            }

            var setBonuses = GetSetAttributeBonuses(db, setPieceCounts);
            foreach (var (attr, value) in setBonuses)
                AddAsAttribute(totals, attr, value);

            return totals;
        }

        /// <summary>Builds `Equip:{instanceId}_{stat}` modifiers. Single source for add/remove symmetry.</summary>
        public static IEnumerable<StatModifier> CreateStatModifiers(InventoryItem item)
        {
            var itemData = item.GetEquipmentData();
            if (itemData == null) yield break;

            string prefix = $"Equip:{item.InstanceId}";
            var builder = new ModifierBuilder();

            if (itemData.CombatStats != null)
                foreach (var statEntry in itemData.CombatStats)
                {
                    float value = statEntry.GetValue(item.Level);
                    if (value != 0)
                        builder.Add(prefix, statEntry.Stat, statEntry.Mode, value);
                }

            // Second Attributes from item.AttributeData (specialization stats) — stored as SecondaryStat in Attribute field
            if (item.AttributeData?.SecondAttribute != null)
                foreach (var attrEntry in item.AttributeData.SecondAttribute)
                {
                    // SecondAttribute uses SecondaryStat enum values in Attribute field (cast from int)
                    var secStat = (SecondaryStat)(int)attrEntry.Attribute;
                    if (secStat != SecondaryStat.None && attrEntry.BaseValue != 0f)
                        builder.Add(prefix + "_SecondAttr", secStat, SecondaryStatMode.Flat, attrEntry.BaseValue);
                }

            foreach (var modifier in builder.Modifiers)
                yield return modifier;
        }

        private static void Add(Dictionary<SecondaryStat, float> dict, SecondaryStat stat, float value)
        {
            if (stat == SecondaryStat.None || value == 0f) return;
            dict.TryGetValue(stat, out float current);
            dict[stat] = current + value;
        }

        private sealed class ModifierBuilder
        {
            public readonly List<StatModifier> Modifiers = new();

            public void Add(string idPrefix, SecondaryStat stat, SecondaryStatMode mode, float value) =>
                Modifiers.Add(new StatModifier
                {
                    Id = $"{idPrefix}_{stat}",
                    Source = ModifierSource.Equipment,
                    Stat = SecondaryStatExtensions.SecondaryStatToSkillType(stat),
                    SecondaryStat = stat,
                    Mode = (ModifierMode)mode,
                    Value = value,
                    Permanent = true
                });
        }
        
        private static void AddAsAttribute(Dictionary<MainAttribute, float> dict, MainAttribute attr, float value)
        {
            if (value == 0f) return;
            dict.TryGetValue(attr, out float current);
            dict[attr] = current + value;
        }
    }
}