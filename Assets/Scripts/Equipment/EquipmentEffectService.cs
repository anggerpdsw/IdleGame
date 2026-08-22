using System;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Generation;
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
        private static bool _effectsRegistered;

        public EquipmentEffectService(IEquipmentRepository repo)
        {
            _repo = repo;
            EnsureEffectsRegistered();
        }

        /// <summary>
        /// Registers the built-in effect implementations once. EffectRegistry starts
        /// empty (zero production call sites previously) so CreateEffect always returned
        /// null — affix/item passives were dead code. Registration makes them real.
        /// </summary>
        private static void EnsureEffectsRegistered()
        {
            if (_effectsRegistered) return;
            _effectsRegistered = true;

            var registry = EffectRegistry.Instance;
            registry.RegisterEffect<Modifiers.Effects.HealEverySecondEffect>();
            registry.RegisterEffect<Modifiers.Effects.AutoHealEffect>();
            registry.RegisterEffect<Modifiers.Effects.CriticalHealEffect>();
            registry.RegisterEffect<Modifiers.Effects.BurnEnemyEffect>();
            registry.RegisterEffect<Modifiers.Effects.FreezeEnemyEffect>();
            registry.RegisterEffect<Modifiers.Effects.PoisonEffect>();
            registry.RegisterEffect<Modifiers.Effects.ExplosionOnKillEffect>();
            registry.RegisterEffect<Modifiers.Effects.ChainLightningEffect>();
            registry.RegisterEffect<Modifiers.Effects.MultiShotEffect>();
            registry.RegisterEffect<Modifiers.Effects.SummonSkeletonEffect>();
            registry.RegisterEffect<Modifiers.Effects.ReflectDamageEffect>();
            registry.RegisterEffect<Modifiers.Effects.ShieldEvery10SecondsEffect>();
            registry.RegisterEffect<Modifiers.Effects.DashAttackEffect>();
            registry.RegisterEffect<Modifiers.Effects.ExtraCoinEffect>();
            registry.RegisterEffect<Modifiers.Effects.DamagePerGoldEffect>();
            registry.RegisterEffect<Modifiers.Effects.UltimateDamageEffect>();
            registry.RegisterEffect<Modifiers.Effects.InstantKillChanceEffect>();
        }

        public void ActivateItemEffects(InventoryItem item, EquipmentType slot)
        {
            var itemData = item.GetEquipmentData();
            if (itemData?.SpecialEffects != null)
            {
                foreach (var effectEntry in itemData.SpecialEffects)
                {
                    if (!effectEntry.CanActivate(item.Level)) continue;
                    ActivateEffect(item, slot, effectEntry);
                }
            }

            // Affix passives — stored in AttributeData (SecondAttribute for effects, MainAttribute for stats)
            // Affix effects with PassiveEffect are handled separately via affix data in generation
            // For now, affix passives are not stored in AttributeData - they would need a separate field
            // This is a known limitation: affix PassiveEffect is lost in new structure
        }

        private void ActivateEffect(InventoryItem item, EquipmentType slot, SpecialEffectEntry entry)
        {
            if (!entry.CanActivate(item.Level)) return;

            var effect = EffectFactory.Create(entry.EffectType, entry, item, slot);
            if (effect == null) return;

            var context = BuildContext(item, slot);
            effect.OnActivate(context);
            _repo.AddActiveEffect(effect);
        }

        /// <summary>
        /// Pumps a combat event (e.g. OnHit) into every active equipment effect.
        /// Called from the projectile hit path; affix passives (FreezeEnemy, BurnEnemy...)
        /// react here. No-op when no effects are active — zero hot-path cost.
        /// </summary>
        public void TriggerEffects(EffectTriggerType trigger, TriggerData data)
        {
            var effects = _repo.ActiveEffects;
            if (effects == null || effects.Count == 0) return;

            var context = new EquipmentContext
            {
                EquipmentService = EquipmentService.Instance,
                InventoryService = InventoryService.Instance,
                Player = Player.Player.Instance,
                LastEnemyHit = data?.Enemy,
                CurrentTime = Time.time
            };

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                try
                {
                    effect.OnTrigger(context, trigger, data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EquipmentEffectService] Effect {effect.EffectType} triggered error: {e.Message}");
                }
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
            InventoryService = InventoryService.Instance,
            Player = Player.Player.Instance,
            CurrentTime = Time.time
        };
    }
}