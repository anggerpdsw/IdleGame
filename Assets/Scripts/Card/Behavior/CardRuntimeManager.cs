using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Card.Behavior.Implementations;

namespace IdleDefenseSurvival.Card.Behavior
{
    /// <summary>
    /// Runtime orchestrator for equipped card behaviors.
    /// Manages CardRuntimeState lifecycle, event dispatch, and behavior updates.
    /// Replaces per-frame logic in CardModifierService.
    /// </summary>
    public class CardRuntimeManager : MonoBehaviour
    {
        #region Singleton
        private static CardRuntimeManager _instance;
        public static CardRuntimeManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        #endregion

        // CardId -> RuntimeState + Behavior
        private readonly Dictionary<string, CardRuntimeState> _activeStates = new();
        private readonly Dictionary<string, ICardBehavior> _activeBehaviors = new();

        // Event subscriptions per event type (optimized dispatch)
        private readonly Dictionary<CardEventType, List<ICardBehavior>> _eventSubscriptions
            = new();

        public event Action OnApexEvolutionChoicesUpdated;

        public void Initialize()
        {
            // Remove ALL previously-active cards (both state + behavior)
            var toRemove = new List<string>(_activeBehaviors.Keys);
            foreach (var cardId in toRemove)
                UnequipCard(cardId);          // removes from both dictionaries

            // Safety net: clear any orphaned states
            _activeStates.Clear();

            var equipped = CardEquipmentService.Instance?.EquippedCards;
            if (equipped == null) return;

            var inventory = CardInventory.Instance;
            if (inventory == null) return;

            foreach (var cardId in equipped)
            {
                if (string.IsNullOrEmpty(cardId)) continue;
                var level = inventory.GetCardLevel(cardId);
                if (level < 1) level = 1;
                EquipCard(cardId, level);
            }
        }

        /// <summary>
        /// Equip a card - create runtime state, resolve behavior, subscribe to events.
        /// </summary>
        public void EquipCard(string cardId, int level)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (_activeStates.ContainsKey(cardId)) return;

            var cardData = CardDatabase.Instance.GetCard(cardId);
            if (cardData == null) return;

            var state = new CardRuntimeState(cardId, level, cardData);
            _activeStates[cardId] = state;

            // Determine effect type from definition
            CardEffectType effectType = CardEffectType.None;
            if (!string.IsNullOrEmpty(cardData.EffectType))
                Enum.TryParse(cardData.EffectType, true, out effectType);

            if (effectType != CardEffectType.None && !CardBehaviorRegistry.IsRegistered(effectType))
            {
                Debug.LogError($"[CardRuntimeManager] No behavior registered for {cardId} ({effectType}).");
                _activeStates.Remove(cardId);
                return;
            }

            var behavior = CardBehaviorRegistry.CreateBehavior(effectType, cardData, level);
            behavior.OnEquip(state);
            _activeBehaviors[cardId] = behavior;

            // Subscribe to relevant events
            SubscribeToEvents(behavior, effectType);
        }

        /// <summary>
        /// Unequip a card - unsubscribe events, cleanup behavior.
        /// </summary>
        public void UnequipCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            if (!_activeStates.TryGetValue(cardId, out var state)) return;

            if (_activeBehaviors.TryGetValue(cardId, out var behavior))
            {
                UnsubscribeFromEvents(behavior);
                behavior.OnUnequip(state);
                behavior.Dispose();
                _activeBehaviors.Remove(cardId);
            }

            _activeStates.Remove(cardId);
        }

        /// <summary>
        /// Upgrade an equipped card.
        /// </summary>
        public void UpgradeCard(string cardId, int oldLevel, int newLevel)
        {
            if (!_activeStates.TryGetValue(cardId, out var state)) return;
            if (!_activeBehaviors.TryGetValue(cardId, out var behavior)) return;

            state.SetLevel(newLevel);
            behavior.OnUpgrade(state, oldLevel, newLevel);
        }

        private void SubscribeToEvents(ICardBehavior behavior, CardEffectType effectType)
        {
            var events = GetEventsForEffect(effectType);
            foreach (var evt in events)
            {
                if (!_eventSubscriptions.TryGetValue(evt, out var list))
                {
                    list = new List<ICardBehavior>();
                    _eventSubscriptions[evt] = list;
                }
                if (!list.Contains(behavior))
                    list.Add(behavior);
            }
        }

        private void UnsubscribeFromEvents(ICardBehavior behavior)
        {
            foreach (var list in _eventSubscriptions.Values)
                list.Remove(behavior);
        }

        private static CardEventType[] GetEventsForEffect(CardEffectType effectType)
        {
            return effectType switch
            {
                CardEffectType.Berserker
                    => new[] { CardEventType.OnPlayerDamaged, CardEventType.OnPlayerHealed },
                CardEffectType.DeathChain or
                CardEffectType.SoulHarvester or 
                CardEffectType.ChainReaction or 
                CardEffectType.ApocalypseEngine or
                CardEffectType.WorldBreaker
                    => new[] { CardEventType.OnEnemyKilled },
                CardEffectType.VampiricFrenzy
                    => new[] { CardEventType.OnLifeSteal },
                CardEffectType.BulletStorm or 
                CardEffectType.InfiniteArsenal or 
                CardEffectType.WarMachine or
                CardEffectType.CelestialArsenal
                    => new[] { CardEventType.OnPlayerAttack },
                CardEffectType.GuardianInstinct
                    => new[] { CardEventType.OnPlayerDamaged },
                CardEffectType.DivineRetribution
                    => new[] { CardEventType.OnPlayerDamaged },
                CardEffectType.DeathReversal 
                    => new[] { CardEventType.OnPlayerDamaged, CardEventType.OnWaveStart },
                CardEffectType.CrazyGambler or 
                CardEffectType.Desperados 
                    => new[] { CardEventType.OnWaveComplete },
                CardEffectType.Immortal 
                    => new[] { CardEventType.OnWaveComplete, CardEventType.OnWaveStart },
                CardEffectType.BatStalker or 
                CardEffectType.HealOnKill
                    => new[] { CardEventType.OnEnemyKilled },
                CardEffectType.ApexEvolution
                    => new[] { CardEventType.OnEnemyKilled, CardEventType.OnWaveStart },
                _ => Array.Empty<CardEventType>(),
            };

        }

        // Public event dispatch methods called by game systems
        public void DispatchEnemyKilled(int count, string enemyType, string damageSource, EnemyAi enemy = null)
            => Dispatch(CardEventType.OnEnemyKilled, b =>
            {
                b.OnEnemyKilled(count, enemyType, damageSource);
                if (enemy != null) b.OnEnemyKilled(enemy, damageSource);
            });

        public void DispatchPlayerAttack()
            => Dispatch(CardEventType.OnPlayerAttack, b => b.OnPlayerAttack());

        public void DispatchPlayerDamaged(float damage, float currentHp, float maxHp)
            => Dispatch(CardEventType.OnPlayerDamaged, b => b.OnPlayerDamaged(damage, currentHp, maxHp));

        public void DispatchPlayerHealed(float healAmount, float currentHp, float maxHp)
            => Dispatch(CardEventType.OnPlayerHealed, b => b.OnPlayerHealed(healAmount, currentHp, maxHp));

        public void DispatchPlayerLifeSteal(float healAmount, float currentHp, float maxHp)
            => Dispatch(CardEventType.OnLifeSteal, b => b.OnPlayerLifeSteal(healAmount, currentHp, maxHp));

        public void DispatchWaveStart(int waveNumber)
            => Dispatch(CardEventType.OnWaveStart, b => b.OnWaveStart(waveNumber));

        public void DispatchWaveComplete(int waveNumber)
            => Dispatch(CardEventType.OnWaveComplete, b => b.OnWaveComplete(waveNumber));

        public void NotifyApexEvolutionChoicesUpdated()
            => OnApexEvolutionChoicesUpdated?.Invoke();

        public IReadOnlyList<CardMutationDefinition> GetApexEvolutionChoices()
            => (GetBehavior("apex_evolution") as ApexEvolutionCardBehavior)?.PendingChoices
                ?? Array.Empty<CardMutationDefinition>();

        public bool SelectApexEvolutionMutation(string mutationId)
            => (GetBehavior("apex_evolution") as ApexEvolutionCardBehavior)?.SelectMutation(mutationId) ?? false;

        private void Dispatch(CardEventType eventType, Action<ICardBehavior> action)
        {
            if (!_eventSubscriptions.TryGetValue(eventType, out var list)) return;
            foreach (var behavior in list)
                action(behavior);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var behavior in _activeBehaviors.Values)
                behavior.Update(dt);
        }

        // Query API
        public bool IsCardActive(string cardId) => _activeStates.ContainsKey(cardId);
        public CardRuntimeState GetState(string cardId) => _activeStates.TryGetValue(cardId, out var s) ? s : null;
        public ICardBehavior GetBehavior(string cardId) => _activeBehaviors.TryGetValue(cardId, out var b) ? b : null;
        public IReadOnlyDictionary<string, CardRuntimeState> ActiveStates => _activeStates;
        public IReadOnlyDictionary<string, ICardBehavior> ActiveBehaviors => _activeBehaviors;
    }
}