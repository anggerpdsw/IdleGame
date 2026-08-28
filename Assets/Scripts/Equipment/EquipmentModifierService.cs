using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Applies/removes item + set-bonus stat modifiers on ModifierManager.
    /// All modifier ids derive from EquipmentStatCalculator so add/remove stay symmetric.
    /// </summary>
    public sealed class EquipmentModifierService
    {
        private readonly IEquipmentRepository _repo;

        public EquipmentModifierService(IEquipmentRepository repo)
        {
            _repo = repo;
        }

        public void ApplyItemStatModifiers(InventoryItem item, EquipmentType slot, bool add)
        {
            if (item == null) return;

            if (add)
            {
                foreach (var modifier in EquipmentStatCalculator.CreateStatModifiers(item))
                {
                    if (modifier.Stat != SkillType.None)
                        ModifierManager.Instance?.AddModifier(modifier);
                }
            }
            else
            {
                foreach (var modifier in EquipmentStatCalculator.CreateStatModifiers(item))
                {
                    if (modifier.Stat != SkillType.None)
                        ModifierManager.Instance?.RemoveModifier(modifier.Id);
                }
            }

            NotifyManagers();
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Item);
        }

        /// <summary>Applies one set-bonus tier's stat bonuses.</summary>
        public void ApplySetTier(SetBonusData setData, SetBonusTier tier)
        {
            if (tier.StatBonuses == null) return;

            foreach (var statEntry in tier.StatBonuses)
            {
                if (statEntry.Stat == SecondaryStat.None) continue;
                var skillType = SecondaryStatExtensions.SecondaryStatToSkillType(statEntry.Stat);
                if (skillType == SkillType.None) continue;

                ModifierManager.Instance?.AddModifier(new StatModifier
                {
                    Id = $"Set:{setData.SetId}:{tier.RequiredPieces}:{statEntry.Stat}",
                    Source = ModifierSource.Equipment,
                    Stat = skillType,
                    SecondaryStat = statEntry.Stat,
                    Mode = (ModifierMode)statEntry.Mode,
                    Value = statEntry.GetValue(1),
                    Permanent = true
                });
            }
        }

        /// <summary>Removes one set-bonus tier's stat bonuses.</summary>
        public void RemoveSetTier(SetBonusData setData, SetBonusTier tier)
        {
            if (tier.StatBonuses == null) return;

            foreach (var statEntry in tier.StatBonuses)
            {
                if (statEntry.Stat == SecondaryStat.None) continue;
                ModifierManager.Instance?.RemoveModifier($"Set:{setData.SetId}:{tier.RequiredPieces}:{statEntry.Stat}");
            }
        }

        private static void NotifyManagers()
        {
            ModifierManager.Instance?.CleanupExpired();
            PlayerStatsManager.Instance?.RefreshStats();
        }
    }
}