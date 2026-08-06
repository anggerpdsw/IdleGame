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
    /// </summary>
    public static class EquipmentStatCalculator
    {
        public static Dictionary<MainStat, float> GetItemStatBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<MainStat, float>();
            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return bonuses;

            if (itemData.MainStats != null)
                foreach (var statEntry in itemData.MainStats)
                    Add(bonuses, statEntry.Stat, statEntry.GetValue(item.Level, item.EnhanceLevel));

            if (item.Enchantment?.StatBonuses != null)
                foreach (var statEntry in item.Enchantment.StatBonuses)
                    Add(bonuses, statEntry.Stat, statEntry.GetValue(item.Enchantment.Level, 0));

            // Gem stats (via GemService like EquipmentComparer.GetTotalStatBonuses)
            if (item.Sockets != null)
            {
                foreach (var socket in item.Sockets)
                {
                    if (!socket.IsFilled) continue;
                    var gemStats = Items.GemService.Instance?.GetGemStats(socket.GemId, socket.GemLevel);
                    if (gemStats == null) continue;
                    foreach (var statEntry in gemStats)
                        Add(bonuses, statEntry.Stat, statEntry.GetValue(socket.GemLevel, 0));
                }
            }

            return bonuses;
        }

        public static Dictionary<MainStat, float> GetSetStatBonuses(ItemDatabase db,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<MainStat, float>();

            foreach (var (setId, count) in setPieceCounts)
            {
                var setData = db?.GetSet(setId);
                if (setData?.Tiers == null) continue;

                foreach (var tier in setData.Tiers.Where(t => t.IsActive(count)))
                {
                    if (tier.StatBonuses == null) continue;
                    foreach (var statEntry in tier.StatBonuses)
                        Add(totals, statEntry.Stat, statEntry.GetValue(1, 0));
                }
            }

            return totals;
        }

        public static Dictionary<MainStat, float> GetTotalStatBonuses(ItemDatabase db,
            IReadOnlyDictionary<EquipmentType, InventoryItem> equippedItems,
            IReadOnlyDictionary<string, int> setPieceCounts)
        {
            var totals = new Dictionary<MainStat, float>();

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
        public static Dictionary<MainStat, float> GetItemBonusesWithSet(InventoryItem item,
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
                    Add(bonuses, statEntry.Stat, statEntry.GetValue(1, 0));
            }

            return bonuses;
        }

        public static Dictionary<MainAttribute, float> GetItemAttributeBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<MainAttribute, float>();
            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return bonuses;

            if (itemData.AttributeStats != null)
                foreach (var attrEntry in itemData.AttributeStats)
                    AddAsAttribute(bonuses, attrEntry.Attribute, attrEntry.GetValue(item.Level, item.EnhanceLevel));

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
                        AddAsAttribute(totals, attrEntry.Attribute, attrEntry.GetValue(1, 0));
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
            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) yield break;

            string prefix = $"Equip:{item.InstanceId}";
            var builder = new ModifierBuilder();

            if (itemData.MainStats != null)
                foreach (var statEntry in itemData.MainStats)
                {
                    float value = statEntry.GetValue(item.Level, item.EnhanceLevel);
                    if (value != 0)
                        builder.Add(prefix, statEntry.Stat, statEntry.Mode, value);
                }

            if (item.Enchantment?.StatBonuses != null)
                foreach (var statEntry in item.Enchantment.StatBonuses)
                {
                    float value = statEntry.GetValue(item.Enchantment.Level, 0);
                    if (value != 0)
                        builder.Add(prefix + "_Enchant", statEntry.Stat, statEntry.Mode, value);
                }

            foreach (var modifier in builder.Modifiers)
                yield return modifier;
        }

        private static void Add(Dictionary<MainStat, float> dict, MainStat stat, float value)
        {
            if (stat == MainStat.None || value == 0f) return;
            dict.TryGetValue(stat, out float current);
            dict[stat] = current + value;
        }

        private static void AddAsAttribute(Dictionary<MainAttribute, float> dict, MainAttribute attr, float value)
        {
            if (value == 0f) return;
            dict.TryGetValue(attr, out float current);
            dict[attr] = current + value;
        }

        private sealed class ModifierBuilder
        {
            public readonly List<StatModifier> Modifiers = new();

            public void Add(string idPrefix, MainStat stat, SecondaryStatMode mode, float value) =>
                Modifiers.Add(new StatModifier
                {
                    Id = $"{idPrefix}_{stat}",
                    Source = ModifierSource.Equipment,
                    Stat = stat.ToSkillType(),
                    MainStat = stat,
                    Mode = (ModifierMode)mode,
                    Value = value,
                    Permanent = true
                });
        }
    }
}