using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Events.Domain;

namespace IdleDefenseSurvival.Events
{
    /// <summary>
    /// Event System orchestrator.
    /// Loads event definitions from dataEvent.json, manages lifecycle, threat tracking, choices.
    /// Single source of truth for active event state.
    /// </summary>
    public class EventService : MonoBehaviour, IEventService
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static EventService _instance;
        public static EventService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        [SerializeField] private bool _debug = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadEventDefinitions();
        }

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------
        public event Action<string> OnEventStarted;
        public event Action<string, bool> OnEventEnded;
        public event Action<int, int> OnThreatChanged;
        public event Action<string> OnChoiceMade;
        public event Action<string> OnIncidentTriggered;
        public event Action<EventState, EventState> OnStateChanged;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private Dictionary<string, EventDefinition> _eventDefinitions = new();
        private EventRuntimeState _runtimeState = new();
        private HashSet<int> _appliedEscalations = new();

        public string ActiveEventId => _runtimeState.EventId;
        public int CurrentThreat => _runtimeState.Threat;
        public long EventScore => _runtimeState.Score;
        public EventState CurrentState => _runtimeState.State;

        // -------------------------------------------------------------------
        // State Machine Validation
        // -------------------------------------------------------------------
        private bool TransitionTo(EventState newState)
        {
            var current = _runtimeState.State;
            if (current == newState) return true;

            bool valid = (current, newState) switch
            {
                (EventState.Idle, EventState.Active) => true,
                (EventState.Active, EventState.AwaitingChoice) => true,
                (EventState.AwaitingChoice, EventState.Active) => true,
                (EventState.Active, EventState.Resolving) => true,
                (EventState.Resolving, EventState.Completed) => true,
                (EventState.Resolving, EventState.Failed) => true,
                (EventState.Completed, EventState.Cooldown) => true,
                (EventState.Failed, EventState.Cooldown) => true,
                (EventState.Cooldown, EventState.Idle) => true,
                (_, EventState.Idle) => true, // emergency reset
                _ => false
            };

            if (!valid)
            {
                if (_debug) Debug.LogWarning($"[EventService] Illegal state transition: {current} → {newState}");
                return false;
            }

            var previousState = _runtimeState.State;
            _runtimeState.State = newState;
            OnStateChanged?.Invoke(previousState, newState);

            if (_debug) Debug.Log($"[EventService] State transition: {previousState} → {newState}");
            return true;
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------
        private void LoadEventDefinitions()
        {
            try
            {
                var database = DatabaseJSONCache.DatabaseEvent;
                if (database?.events == null) return;

                _eventDefinitions.Clear();
                foreach (var evt in database.events)
                {
                    _eventDefinitions[evt.eventId] = evt;
                }

                if (_debug) Debug.Log($"[EventService] Loaded {_eventDefinitions.Count} event definitions.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventService] Failed to parse dataEvent.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Load runtime state from save data.
        /// Called by SaveManager after load.
        /// </summary>
        public void LoadState(EventSaveData save)
        {
            if (save == null)
            {
                _runtimeState.Reset();
                return;
            }

            _runtimeState.LoadFrom(save);

            // Validate active event still exists
            if (!string.IsNullOrEmpty(_runtimeState.EventId) && !_eventDefinitions.ContainsKey(_runtimeState.EventId))
            {
                if (_debug) Debug.LogWarning($"[EventService] Active event '{_runtimeState.EventId}' no longer exists. Clearing.");
                _runtimeState.Reset();
                return;
            }

            // RECOVERY: restore boss encounter listener if collapsed but not rewarded
            if (_runtimeState.IsCollapsed && !_runtimeState.RewardsGranted)
            {
                EnemyDeathHandler.OnEnemyKilled -= HandleBossDeath;
                EnemyDeathHandler.OnEnemyKilled += HandleBossDeath;
                if (_debug) Debug.Log("[EventService] Restored boss death listener on load.");
            }

            if (_debug) Debug.Log($"[EventService] State loaded. Active: {_runtimeState.EventId ?? "none"}");
        }

        /// <summary>
        /// Get save data for persistence.
        /// Called by SaveManager before save.
        /// Also exposed via IEventService for shop/chest/codex services.
        /// </summary>
        public EventSaveData GetSaveData()
        {
            return _runtimeState.ToSaveData();
        }

        // -------------------------------------------------------------------
        // Event Lifecycle
        // -------------------------------------------------------------------
        public EventDefinition GetActiveEvent()
        {
            if (string.IsNullOrEmpty(_runtimeState.EventId)) return null;
            _eventDefinitions.TryGetValue(_runtimeState.EventId, out var evt);
            return evt;
        }

        public bool StartEvent(string eventId)
        {
            if (!_eventDefinitions.TryGetValue(eventId, out var evt))
            {
                if (_debug) Debug.LogWarning($"[EventService] Event '{eventId}' not found.");
                return false;
            }

            if (!string.IsNullOrEmpty(_runtimeState.EventId))
            {
                if (_debug) Debug.LogWarning($"[EventService] Event already active: {_runtimeState.EventId}");
                return false;
            }

            // Check cooldown
            if (_runtimeState.NextEventStartAt > 0 && DateTime.UtcNow.Ticks < _runtimeState.NextEventStartAt)
            {
                if (_debug) Debug.LogWarning($"[EventService] Event still in cooldown. Next start: {new DateTime(_runtimeState.NextEventStartAt):yyyy-MM-dd HH:mm:ss}");
                return false;
            }

            _runtimeState.Reset();
            _runtimeState.EventId = eventId;
            _runtimeState.Threat = evt.threat.initial;

            // Set end time
            if (evt.schedule.durationDays > 0)
            {
                _runtimeState.EventEndsAt = DateTime.UtcNow.AddDays(evt.schedule.durationDays).Ticks;
            }
            else
            {
                _runtimeState.EventEndsAt = DateTime.UtcNow.AddHours(24).Ticks;
            }

            TransitionTo(EventState.Active);
            OnEventStarted?.Invoke(eventId);
            if (_debug) Debug.Log($"[EventService] Event started: {eventId}, ends at: {new DateTime(_runtimeState.EventEndsAt):yyyy-MM-dd HH:mm:ss}");

            return true;
        }

        public void EndEvent(bool success)
        {
            if (string.IsNullOrEmpty(_runtimeState.EventId)) return;

            var eventId = _runtimeState.EventId;
            var evt = GetActiveEvent();

            // Unsubscribe from boss death handler if still subscribed
            EnemyDeathHandler.OnEnemyKilled -= HandleBossDeath;

            // Distribute rewards if success
            if (success)
            {
                DistributeEventRewards();
            }

            // Calculate next event start time
            if (evt?.schedule != null && !string.IsNullOrEmpty(evt.schedule.rotationId))
            {
                var parts = evt.schedule.rotationId.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int cooldownDays))
                {
                    _runtimeState.NextEventStartAt = DateTime.UtcNow.AddDays(cooldownDays).Ticks;
                    if (_debug) Debug.Log($"[EventService] Next event can start at: {new DateTime(_runtimeState.NextEventStartAt):yyyy-MM-dd HH:mm:ss} (cooldown: {cooldownDays} days)");
                }
            }

            OnEventEnded?.Invoke(eventId, success);
            if (_debug) Debug.Log($"[EventService] Event ended: {eventId}, success={success}");

            // Track completed event before reset
            if (!string.IsNullOrEmpty(eventId))
                _runtimeState.CompletedEventIds.Add(eventId);

            var nextStart = _runtimeState.NextEventStartAt;
            _runtimeState.Reset();
            _runtimeState.NextEventStartAt = nextStart;
            _runtimeState.RewardsGranted = false;
            _appliedEscalations.Clear();

            TransitionTo(EventState.Idle);
        }

        public bool IsEventActive()
        {
            if (string.IsNullOrEmpty(_runtimeState.EventId)) return false;

            if (_runtimeState.EventEndsAt > 0)
            {
                var now = DateTime.UtcNow.Ticks;
                if (now >= _runtimeState.EventEndsAt)
                {
                    EndEvent(false);
                    return false;
                }
            }

            return true;
        }

        // -------------------------------------------------------------------
        // Threat System
        // -------------------------------------------------------------------
        public void RegisterKill(bool isElite, bool isBoss, string enemyId)
        {
            var evt = GetActiveEvent();
            if (evt == null)
            {
                if (_debug) Debug.LogWarning($"[EventService] RegisterKill failed: No active event. State={_runtimeState.State}");
                return;
            }

            int delta = 0;
            if (isBoss)
                delta = evt.threat.rates.bossKill;
            else if (isElite)
                delta = evt.threat.rates.eliteKill;
            else
                delta = evt.threat.rates.voidKill;

            if (_debug) Debug.Log($"[EventService] RegisterKill: {enemyId} (elite={isElite}, boss={isBoss}) → threat +{delta}");

            AddThreat(delta);
            RegisterEnemyDiscovered(enemyId);

            foreach (var obj in evt.objectives)
            {
                if (obj.type == "KillEnemies" && obj.targetId == enemyId)
                {
                    IncrementObjective(obj.id, 1);
                    if (obj.claimPolicy == "auto")
                    {
                        TryAutoClaimObjective(obj);
                    }
                }
            }
        }

        private void RegisterEnemyDiscovered(string enemyId)
        {
            var evt = GetActiveEvent();
            if (evt?.codex?.entries == null) return;

            bool isCodexEnemy = false;
            foreach (var entryId in evt.codex.entries)
            {
                if (entryId == enemyId)
                {
                    isCodexEnemy = true;
                    break;
                }
            }

            if (!isCodexEnemy || _runtimeState.CodexEntries.Contains(enemyId)) return;

            _runtimeState.CodexEntries.Add(enemyId);
            AddEventCurrency(100);

            if (_debug) Debug.Log($"[EventService] Codex entry unlocked: {enemyId}");
        }

        public void RegisterWaveCompleted()
        {
            var evt = GetActiveEvent();
            if (evt == null) return;

            foreach (var obj in evt.objectives)
            {
                if (obj.type == "SurviveWaves")
                {
                    IncrementObjective(obj.id, 1);
                    if (obj.claimPolicy == "auto")
                    {
                        TryAutoClaimObjective(obj);
                    }
                }
            }

            if (evt.incidents != null)
            {
                int currentWave = Manager.WaveManager.Instance?.CurrentWave ?? 0;
                foreach (var incident in evt.incidents)
                {
                    if (_runtimeState.TriggeredIncidents.Contains(incident.id)) continue;

                    if (incident.trigger == "wave" && currentWave >= incident.conditions.minWave)
                    {
                        _runtimeState.TriggeredIncidents.Add(incident.id);
                        OnIncidentTriggered?.Invoke(incident.description);
                    }
                }
            }
        }

        private void AddThreat(int delta)
        {
            if (delta == 0) return;

            var evt = GetActiveEvent();
            if (evt == null) return;

            int oldThreat = _runtimeState.Threat;
            _runtimeState.Threat = Mathf.Clamp(_runtimeState.Threat + delta, evt.threat.min, evt.threat.max);

            if (_runtimeState.Threat == oldThreat) return;

            OnThreatChanged?.Invoke(oldThreat, _runtimeState.Threat);

            // Apply ALL crossed escalation thresholds (fixes jump from 40 → 80 skipping 50 & 75)
            if (evt.escalation != null)
            {
                foreach (var level in evt.escalation)
                {
                    if (oldThreat < level.threshold && _runtimeState.Threat >= level.threshold)
                    {
                        if (!_appliedEscalations.Contains(level.threshold))
                        {
                            _appliedEscalations.Add(level.threshold);
                            if (_debug) Debug.Log($"[EventService] Escalation threshold {level.threshold} triggered");
                        }
                    }
                }
            }

            // Threat-based incidents
            if (evt.incidents != null)
            {
                foreach (var incident in evt.incidents)
                {
                    if (_runtimeState.TriggeredIncidents.Contains(incident.id)) continue;

                    if (incident.trigger == "threat" && _runtimeState.Threat >= incident.conditions.minThreat)
                    {
                        _runtimeState.TriggeredIncidents.Add(incident.id);
                        OnIncidentTriggered?.Invoke(incident.description);
                    }
                }
            }

            // Use configurable thresholds instead of hardcoded 90/100
            int catastrophicThreshold = evt.threat.catastrophicThreshold > 0
                ? evt.threat.catastrophicThreshold
                : (int)(evt.threat.max * 0.9f); // fallback: 90% of max

            int collapseThreshold = evt.threat.collapseThreshold > 0
                ? evt.threat.collapseThreshold
                : evt.threat.max; // fallback: 100% of max

            if (_runtimeState.Threat >= catastrophicThreshold && !_runtimeState.CatastrophicTriggered)
            {
                _runtimeState.CatastrophicTriggered = true;
                OnIncidentTriggered?.Invoke("catastrophic");
            }

            if (_runtimeState.Threat >= collapseThreshold && !_runtimeState.IsCollapsed)
            {
                _runtimeState.IsCollapsed = true;
                TriggerBossSpawn();
            }
        }

        private void TriggerBossSpawn()
        {
            var evt = GetActiveEvent();
            if (evt?.boss?.enemyId == null)
            {
                if (_debug) Debug.LogWarning("[EventService] No boss defined for this event. Ending as failure.");
                EndEvent(false);
                return;
            }

            var database = DatabaseJSONCache.DatabaseEnemy;
            if (database?.enemies == null)
            {
                EndEvent(false);
                return;
            }

            EnemyData bossData = null;
            foreach (var enemy in database.enemies)
            {
                if (enemy?.id == evt.boss.enemyId)
                {
                    bossData = enemy;
                    break;
                }
            }

            if (bossData == null)
            {
                if (_debug) Debug.LogWarning($"[EventService] Boss '{evt.boss.enemyId}' not found in database.");
                EndEvent(false);
                return;
            }

            var spawner = EnemySpawner.Instance;
            if (spawner != null)
            {
                EnemyData scaledBoss = Utilityku.CreateScaledEnemy(bossData);
                spawner.SpawnSpecificEnemy(scaledBoss);
                EnemyDeathHandler.OnEnemyKilled += HandleBossDeath;

                if (_debug) Debug.Log($"[EventService] Boss '{evt.boss.enemyId}' spawned. Threat collapsed.");
            }
            else
            {
                EndEvent(false);
            }
        }

        private void HandleBossDeath(EnemyAi enemy, string source)
        {
            var evt = GetActiveEvent();
            if (evt?.boss?.enemyId == null) return;
            if (enemy?.EnemyData?.id != evt.boss.enemyId) return;

            EnemyDeathHandler.OnEnemyKilled -= HandleBossDeath;

            if (_debug) Debug.Log($"[EventService] Boss defeated. Event success.");
            EndEvent(true);
        }

        // -------------------------------------------------------------------
        // Choice System
        // -------------------------------------------------------------------
        public bool MakeChoice(string choiceId)
        {
            if (_runtimeState.State != EventState.AwaitingChoice)
            {
                if (_debug) Debug.LogWarning("[EventService] MakeChoice called outside AwaitingChoice state");
                return false;
            }

            var evt = GetActiveEvent();
            if (evt == null) return false;

            var choice = evt.choices.FirstOrDefault(c => c.choiceId == choiceId);
            if (choice == null)
            {
                if (_debug) Debug.LogWarning($"[EventService] Invalid choice: {choiceId}");
                return false;
            }

            AddThreat(choice.threatDelta);
            _runtimeState.ChoiceHistory.Add(choiceId);

            OnChoiceMade?.Invoke(choiceId);
            if (_debug) Debug.Log($"[EventService] Choice made: {choiceId}, threat delta={choice.threatDelta}");

            TransitionTo(EventState.Active);
            return true;
        }

        // -------------------------------------------------------------------
        // Modifiers
        // -------------------------------------------------------------------
        public float GetSpawnWeightModifier(string enemyId)
        {
            var evt = GetActiveEvent();
            if (evt == null) return 1f;

            float modifier = 1f;
            foreach (var level in evt.escalation)
            {
                if (_runtimeState.Threat >= level.threshold)
                {
                    foreach (var mod in level.modifiers)
                    {
                        if (mod.type == "spawnWeight")
                        {
                            modifier *= 1f + mod.value;
                        }
                    }
                }
            }

            return modifier;
        }

        public float GetStatModifier(string modifierType)
        {
            var evt = GetActiveEvent();
            if (evt == null) return 1f;

            float modifier = 1f;
            foreach (var level in evt.escalation)
            {
                if (_runtimeState.Threat >= level.threshold)
                {
                    foreach (var mod in level.modifiers)
                    {
                        if (mod.type == modifierType)
                        {
                            modifier *= 1f + mod.value;
                        }
                    }
                }
            }

            return modifier;
        }

        // -------------------------------------------------------------------
        // Objectives
        // -------------------------------------------------------------------
        private void IncrementObjective(string objectiveId, int amount)
        {
            if (!_runtimeState.ObjectiveProgress.ContainsKey(objectiveId))
            {
                _runtimeState.ObjectiveProgress[objectiveId] = 0;
            }

            _runtimeState.ObjectiveProgress[objectiveId] += amount;
        }

        private void TryAutoClaimObjective(EventObjective obj)
        {
            if (_runtimeState.ObjectiveClaimed.Contains(obj.id)) return;

            var progress = _runtimeState.ObjectiveProgress.ContainsKey(obj.id)
                ? _runtimeState.ObjectiveProgress[obj.id]
                : 0;

            if (progress >= obj.target)
            {
                GrantObjectiveReward(obj);
                _runtimeState.ObjectiveClaimed.Add(obj.id);
            }
        }

        public bool ClaimObjective(string objectiveId)
        {
            if (_runtimeState.ObjectiveClaimed.Contains(objectiveId)) return false;

            var evt = GetActiveEvent();
            if (evt == null) return false;

            var obj = evt.objectives.FirstOrDefault(o => o.id == objectiveId);
            if (obj == null) return false;

            var progress = _runtimeState.ObjectiveProgress.ContainsKey(objectiveId)
                ? _runtimeState.ObjectiveProgress[objectiveId]
                : 0;

            if (progress < obj.target) return false;

            GrantObjectiveReward(obj);
            _runtimeState.ObjectiveClaimed.Add(objectiveId);
            return true;
        }

        private void GrantObjectiveReward(EventObjective obj)
        {
            if (obj.reward.eventCurrency > 0)
                AddEventCurrency(obj.reward.eventCurrency);
            if (obj.reward.gold > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gold, obj.reward.gold, "EventObjective");
            if (obj.reward.gem > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gem, obj.reward.gem, "EventObjective");
            if (obj.reward.meat > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Meat, obj.reward.meat, "EventObjective");
        }

        // -------------------------------------------------------------------
        // Currency
        // -------------------------------------------------------------------
        public void AddEventCurrency(long amount)
        {
            if (amount <= 0) return;
            _runtimeState.EventCurrency += amount;
        }

        public bool SpendEventCurrency(long amount)
        {
            if (amount <= 0 || _runtimeState.EventCurrency < amount) return false;
            _runtimeState.EventCurrency -= amount;
            return true;
        }

        // -------------------------------------------------------------------
        // Rewards
        // -------------------------------------------------------------------
        private void DistributeEventRewards()
        {
            if (_runtimeState.RewardsGranted) return;

            var evt = GetActiveEvent();
            if (evt == null) return;

            long currencyReward = _runtimeState.Score / 10;
            AddEventCurrency(currencyReward);

            if (evt.rewards?.relics != null && InventoryService.Instance != null)
            {
                foreach (var relicId in evt.rewards.relics)
                {
                    InventoryService.Instance.AddItem(relicId, 1);
                }
            }

            if (!string.IsNullOrEmpty(evt.pet?.petId))
            {
                GrantEventPet(evt.pet.petId);
            }

            if (_debug) Debug.Log($"[EventService] Rewards distributed: {currencyReward} currency, {evt.rewards?.relics?.Length ?? 0} relics");

            _runtimeState.RewardsGranted = true;
        }

        private void GrantEventPet(string petId)
        {
            var petService = Core.ServiceLocator.PetService;
            if (petService == null) return;

            var activePets = petService.GetActivePets();
            bool alreadyOwned = false;

            if (activePets != null)
            {
                foreach (var pet in activePets)
                {
                    if (pet.PetId == petId)
                    {
                        alreadyOwned = true;
                        break;
                    }
                }
            }

            if (!alreadyOwned)
            {
                petService.GrantPet(petId);
                if (_debug) Debug.Log($"[EventService] Pet unlocked: {petId}");
            }
            else
            {
                AddEventCurrency(5000);
                if (_debug) Debug.Log($"[EventService] Pet duplicate converted to 5000 event currency");
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        [Serializable]
        public class EventDefinitionWrapper
        {
            public EventDefinition[] events;
        }
    }
}
