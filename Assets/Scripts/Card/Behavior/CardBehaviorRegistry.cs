using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card.Behavior.Implementations;

namespace IdleDefenseSurvival.Card.Behavior
{
    /// <summary>
    /// Central registry mapping CardEffectType to behavior implementations.
    /// Resolves behavior once at equip time, eliminating per-frame switch statements.
    /// </summary>
    public static class CardBehaviorRegistry
    {
        private static readonly Dictionary<CardEffectType, Func<CardData, int, ICardBehavior>> _factories
            = new();

        private static readonly HashSet<CardEffectType> _statOnlyEffects
            = new();

        static CardBehaviorRegistry()
        {
            // Register all stat-only effects (handled by DefaultCardBehavior)
            RegisterStatOnly(CardEffectType.Gold);
            RegisterStatOnly(CardEffectType.Meat);
            RegisterStatOnly(CardEffectType.FrostAura);
            RegisterStatOnly(CardEffectType.Shield);
            RegisterStatOnly(CardEffectType.TimeFast);
            RegisterStatOnly(CardEffectType.AddTank);
            RegisterStatOnly(CardEffectType.EnemyBalance);

            // Register custom behavior implementations
            Register<AngelCardBehavior>(CardEffectType.Immortal);
            Register<BerserkerCardBehavior>(CardEffectType.Berserker);
            Register<CrazyGamblerCardBehavior>(CardEffectType.CrazyGambler);
            Register<DesperadosCardBehavior>(CardEffectType.Desperados);
            Register<BatStalkerCardBehavior>(CardEffectType.BatStalker);
            Register<HealOnKillCardBehavior>(CardEffectType.HealOnKill);
            Register<DeathChainCardBehavior>(CardEffectType.DeathChain);
            Register<BulletStormCardBehavior>(CardEffectType.BulletStorm);
            Register<ExecutionProtocolCardBehavior>(CardEffectType.ExecutionProtocol);
            Register<OverkillCardBehavior>(CardEffectType.Overkill);
            Register<VampiricFrenzyCardBehavior>(CardEffectType.VampiricFrenzy);
            Register<GuardianInstinctCardBehavior>(CardEffectType.GuardianInstinct);
            Register<CriticalCascadeCardBehavior>(CardEffectType.CriticalCascade);
            Register<WarMachineCardBehavior>(CardEffectType.WarMachine);
            Register<ApocalypseEngineCardBehavior>(CardEffectType.ApocalypseEngine);
            Register<InfiniteArsenalCardBehavior>(CardEffectType.InfiniteArsenal);
            Register<SoulHarvesterCardBehavior>(CardEffectType.SoulHarvester);
            Register<DeathReversalCardBehavior>(CardEffectType.DeathReversal);
            Register<VoidOverlordCardBehavior>(CardEffectType.VoidOverlord);
            Register<ChainReactionCardBehavior>(CardEffectType.ChainReaction);
        }

        /// <summary>
        /// Register a stat-only effect (uses DefaultCardBehavior).
        /// </summary>
        public static void RegisterStatOnly(CardEffectType effectType)
        {
            _statOnlyEffects.Add(effectType);
            _factories[effectType] = (data, level) => new DefaultCardBehavior(data, level, effectType);
        }

        /// <summary>
        /// Register a custom behavior implementation.
        /// </summary>
        public static void Register<T>(CardEffectType effectType) where T : ICardBehavior, new()
        {
            _factories[effectType] = (data, level) => new T();
        }

        /// <summary>
        /// Create behavior instance for the given effect type.
        /// Returns null if effect type is not registered.
        /// </summary>
        public static ICardBehavior CreateBehavior(CardEffectType effectType, CardData cardData, int level)
        {
            if (_factories.TryGetValue(effectType, out var factory))
            {
                return factory(cardData, level);
            }

            // Fallback to default behavior for unregistered types
            return new DefaultCardBehavior(cardData, level, effectType);
        }

        /// <summary>
        /// Check if an effect type has a custom behavior (beyond stat modification).
        /// </summary>
        public static bool HasCustomBehavior(CardEffectType effectType)
        {
            return _factories.ContainsKey(effectType) && !_statOnlyEffects.Contains(effectType);
        }

        public static bool IsRegistered(CardEffectType effectType)
        {
            return _factories.ContainsKey(effectType);
        }

        /// <summary>
        /// Get all registered effect types.
        /// </summary>
        public static IReadOnlyCollection<CardEffectType> RegisteredEffects => _factories.Keys;
    }
}