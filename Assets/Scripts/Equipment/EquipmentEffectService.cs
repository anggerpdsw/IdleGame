using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival.Player;
using UnityEngine;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Lifecycle of item special effects (equip -> activate, unequip -> deactivate).
    /// Set bonus effects live in EquipmentSetBonusService.
    /// </summary>
    public sealed class EquipmentEffectService
    {
        private readonly IEquipmentRepository _repo;

        public EquipmentEffectService(IEquipmentRepository repo)
        {
            _repo = repo;
        }

        public void ActivateItemEffects(InventoryItem item, EquipmentType slot)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
            if (itemData?.SpecialEffects == null) return;

            foreach (var effectEntry in itemData.SpecialEffects)
            {
                if (!effectEntry.CanActivate(item.Level, item.EnhanceLevel)) continue;

                var effect = EffectFactory.Create(effectEntry.EffectType, effectEntry, item, slot);
                if (effect == null) continue;

                var context = BuildContext(item, slot);
                effect.OnActivate(context);
                _repo.AddActiveEffect(effect);
            }
        }

        public void DeactivateItemEffects(InventoryItem item, EquipmentType slot)
        {
            for (int i = _repo.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var effect = _repo.ActiveEffects[i];
                var data = effect.GetRuntimeData();
                bool isFromItem = data.CustomState?.TryGetValue("ItemInstanceId", out var idObj) == true
                    && idObj?.ToString() == item.InstanceId;
                if (!isFromItem) continue;

                effect.OnDeactivate(BuildContext(item, slot));
                _repo.RemoveActiveEffect(effect);
            }
        }

        private static EquipmentContext BuildContext(InventoryItem item, EquipmentType slot) => new()
        {
            Item = item,
            Slot = slot,
            EquipmentService = EquipmentService.Instance,
            InventoryService = Inventory.InventoryService.Instance,
            Player = Player.Player.Instance,
            CurrentTime = Time.time
        };
    }
}