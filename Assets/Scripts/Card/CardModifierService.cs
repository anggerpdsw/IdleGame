using System;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Card.Behavior;
using IdleDefenseSurvival.Card.Behavior.Implementations;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Thin façade preserving legacy static query API.
    /// All runtime behavior is handled by CardRuntimeManager & ICardBehavior implementations.
    /// </summary>
    public static class CardModifierService
    {
        #region Events
        public static event Action OnModifierChanged;
        #endregion

        #region Refresh — called after equip/unequip/upgrade or on load
        public static void Refresh()
        {
            CardRuntimeManager.Instance?.Initialize();
            OnModifierChanged?.Invoke();

            // Trigger PlayerStatsManager to recompute derived stats and fire OnStatsChanged
            // so PlayerUI.RefreshValues() updates BossDamage/EliteDamage rows
            PlayerStatsManager.Instance?.RefreshStats();
        }
        #endregion

        #region Query API (backward-compatible)
        public static bool HasEffect(CardEffectType effect)
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return false;
            foreach (var bh in mgr.ActiveBehaviors.Values)
                if (bh.EffectType == effect) return true;
            return false;
        }

        public static float GetEffectResult(CardEffectType effect, float fallback = 0f)
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return fallback;
            foreach (var entry in mgr.ActiveStates)
            {
                var behavior = mgr.GetBehavior(entry.Key);
                if (behavior?.EffectType != effect) continue;

                float value = behavior.GetCurrentValue();
                return string.Equals(
                    entry.Value.Definition?.Mode,
                    "Percent",
                    StringComparison.OrdinalIgnoreCase)
                    ? value * 0.01f
                    : value;
            }
            return fallback;
        }

        public static float GetCardParameter(string cardId, string parameter, float fallback = 0f)
        {
            var state = CardRuntimeManager.Instance?.GetState(cardId);
            return state?.Definition?.GetParameter(parameter, fallback) ?? fallback;
        }

        public static float GetWorldBreakerDamageMultiplier()
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("world_breaker") as WorldBreakerCardBehavior;
            return behavior?.GetDamageMultiplier() ?? 1f;
        }

        public static float GetWorldBreakerDefenseIgnoreFraction()
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("world_breaker") as WorldBreakerCardBehavior;
            return behavior?.GetDefenseIgnoreFraction() ?? 0f;
        }

        public static float GetDevourerDamageMultiplier(float targetHealthFraction)
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("apex_evolution") as ApexEvolutionCardBehavior;
            return behavior?.GetDevourerDamageMultiplier(targetHealthFraction) ?? 1f;
        }

        public static float GetProjectileEffectTriggerRateMultiplier()
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("apex_evolution") as ApexEvolutionCardBehavior;
            return behavior?.GetProjectileEffectTriggerRateMultiplier() ?? 1f;
        }

        public static int GetWorldBreakerStackCount()
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("world_breaker") as WorldBreakerCardBehavior;
            return behavior?.GetStackCount() ?? 0;
        }

        public static bool HasCard(string cardId)
            => CardRuntimeManager.Instance?.IsCardActive(cardId) ?? false;

        public static int GetCardLevel(string cardId)
        {
            var state = CardRuntimeManager.Instance?.GetState(cardId);
            return state?.Level ?? -1;
        }


        // 2. VoidOverlord — query runtime behavior's timer state
        public static bool IsVoidOverlordActive()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return false;
            var behavior = mgr.GetBehavior("void_overlord") as VoidOverlordCardBehavior;
            return behavior?.IsActive() ?? false;
        }
        // Angel (Immortal) — query behavior state
        public static float GetAngelMaxCooldown()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return 0f;
            var behavior = mgr.GetBehavior("angel") as AngelCardBehavior;
            return behavior?.GetCurrentValue() ?? 0f;
        }

        public static float GetAngelCooldownRemaining()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return 0f;
            var behavior = mgr.GetBehavior("angel") as AngelCardBehavior;
            return behavior?.GetCooldownRemaining() ?? 0f;
        }

        public static bool TryTriggerAngel()
        {
            var behavior = CardRuntimeManager.Instance?.GetBehavior("angel") as AngelCardBehavior;
            return behavior?.TriggerAngel() ?? false;
        }

        public static bool HasAngelImmunity()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return false;
            var behavior = mgr.GetBehavior("angel") as AngelCardBehavior;
            return behavior?.HasImmunity() ?? false;
        }

        public static float GetCrazyGamblerBonus()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return 0f;
            var behavior = mgr.GetBehavior("crazy_gambler") as CrazyGamblerCardBehavior;
            return behavior?.GetBonus() ?? 0f;
        }

        public static float GetDesperadosBonus()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return 0f;
            var behavior = mgr.GetBehavior("desperados") as DesperadosCardBehavior;
            return behavior?.GetBonus() ?? 0f;
        }

        // DeathChain — query behavior's current stack count
        public static int GetDeathChainStack()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return 0;
            var behavior = mgr.GetBehavior("death_chain") as DeathChainCardBehavior;
            return behavior?.GetStackCount() ?? 0;
        }

        // Aura effect detection — used by AuraCollider
        public static bool HasAuraEffect()
        {
            var mgr = CardRuntimeManager.Instance;
            if (mgr == null) return false;
            foreach (var bh in mgr.ActiveBehaviors.Values)
                if (bh.EffectType == CardEffectType.FrostAura) return true;
            return false;
        }

        #endregion
    }
}