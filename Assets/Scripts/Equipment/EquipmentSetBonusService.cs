using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Set piece counting + tier activation. Tier effects: stat modifiers via
    /// EquipmentModifierService, special effects via EffectFactory.
    /// </summary>
    public sealed class EquipmentSetBonusService
    {
        private readonly IEquipmentRepository _repo;
        private readonly EquipmentModifierService _modifiers;
        private readonly EquipmentEventDispatcher _events;

        private readonly Dictionary<string, List<IEquipmentEffect>> _activeTierEffects = new();

        public EquipmentSetBonusService(IEquipmentRepository repo, EquipmentModifierService modifiers, EquipmentEventDispatcher events)
        {
            _repo = repo;
            _modifiers = modifiers;
            _events = events;
        }

        // ============ Piece counts ============

        public int GetSetPieceCount(string setId) => _repo.GetSetPieceCount(setId);

        public void CheckSetBonusTier(SetBonusData setData, int previousCount, int newCount)
        {
            if (setData?.Tiers == null) return;

            bool changed = false;
            foreach (var tier in setData.Tiers)
            {
                bool wasActive = tier.IsActive(previousCount);
                bool isActive = tier.IsActive(newCount);
                if (wasActive == isActive) continue;

                changed = true;
                if (isActive) ActivateTier(setData, tier);
                else DeactivateTier(setData, tier);
            }

            if (changed)
            {
                // Attribute set bonuses feed the attribute pool; re-apply once per change.
                AttributeModifierManager.Instance?.Apply();
                _events.NotifySetBonusChanged(setData.SetId, previousCount, newCount);
            }
        }

        private void ActivateTier(SetBonusData setData, SetBonusTier tier)
        {
            _modifiers.ApplySetTier(setData, tier);

            if (tier.SpecialEffects != null)
            {
                var created = new List<IEquipmentEffect>();
                foreach (var effectEntry in tier.SpecialEffects)
                {
                    if (!effectEntry.IsActive) continue;
                    var effect = EffectFactory.Create(effectEntry.EffectType, effectEntry, null, EquipmentType.None);
                    if (effect == null) continue;
                    effect.OnActivate(new EquipmentContext());
                    _repo.AddActiveEffect(effect);
                    created.Add(effect);
                }
                if (created.Count > 0) _activeTierEffects[setData.SetId] = created;
            }
        }

        private void DeactivateTier(SetBonusData setData, SetBonusTier tier)
        {
            _modifiers.RemoveSetTier(setData, tier);

            if (_activeTierEffects.TryGetValue(setData.SetId, out var effects))
            {
                foreach (var effect in effects)
                {
                    effect.OnDeactivate(new EquipmentContext());
                    _repo.RemoveActiveEffect(effect);
                }
                _activeTierEffects.Remove(setData.SetId);
            }
        }

        // ============ Queries ============

        public IReadOnlyList<SetBonusTier> GetActiveTiers(string setId)
        {
            var setData = ItemDatabase.Instance?.GetSet(setId);
            if (setData?.Tiers == null) return Array.Empty<SetBonusTier>();

            int count = GetSetPieceCount(setId);
            return setData.Tiers.Where(t => t.IsActive(count)).ToList();
        }

        public IReadOnlyDictionary<string, IReadOnlyList<SetBonusTier>> GetAllActiveBonuses()
        {
            var result = new Dictionary<string, IReadOnlyList<SetBonusTier>>();
            foreach (string setId in _repo.ActiveSetIds)
            {
                var tiers = GetActiveTiers(setId);
                if (tiers.Count > 0) result[setId] = tiers;
            }
            return result;
        }

        public bool IsTierActive(string setId, int tierIndex)
        {
            var setData = ItemDatabase.Instance?.GetSet(setId);
            if (setData?.Tiers == null || tierIndex < 0 || tierIndex >= setData.Tiers.Length) return false;
            return setData.Tiers[tierIndex].IsActive(GetSetPieceCount(setId));
        }

        public void Clear()
        {
            foreach (var effects in _activeTierEffects.Values)
            {
                foreach (var effect in effects)
                {
                    effect.OnDeactivate(new EquipmentContext());
                    _repo.RemoveActiveEffect(effect);
                }
            }
            _activeTierEffects.Clear();
        }
    }
}