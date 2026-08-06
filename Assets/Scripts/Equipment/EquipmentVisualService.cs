using IdleDefenseSurvival.Items;
using UnityEngine;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Visual/model lookup for equipped slots.
    /// </summary>
    public sealed class EquipmentVisualService
    {
        private readonly IEquipmentRepository _repo;

        public EquipmentVisualService(IEquipmentRepository repo)
        {
            _repo = repo;
        }

        public GameObject GetEquippedModel(EquipmentType slot)
        {
            if (!_repo.TryGetEquipped(slot, out var item)) return null;
            return (ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData)?.EquippedModelPrefab;
        }
    }
}