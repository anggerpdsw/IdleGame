using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Inventory;

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

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private Dictionary<string, EventDefinition> _eventDefinitions = new();
        private EventRuntimeState _runtimeState = new();
        private HashSet<int> _appliedEscalations = new HashSet<int>();

        public string ActiveEventId => _runtimeState.EventId;
        public int CurrentThreat => _runtimeState.Threat;
        public long EventScore => _runtimeState.Score;

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------
        private void LoadEventDefinitions()
        {
            var asset = Resources.Load<TextAsset>("Data/Event/dataEvent");
            if (asset == null)
            {
                if (_debug) Debug.LogWarning("[EventService] dataEvent.json not found.");
                return;
            }

            try
            {
                var wrapper = JsonConvert.DeserializeObject<EventDefinitionWrapper>(asset.text);
                if (wrapper?.events == null) return;

                _eventDefinitions.Clear();
                foreach (var evt in wrapper.events)
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
        public void LoadState(Data.EventSaveData save)
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
            }

            if (_debug) Debug.Log($"[EventService] State loaded. Active: {_runtimeState.EventId ?? "none"}");
        }

        /// <summary>
        /// Get save data for persistence.
        /// Called by SaveManager before save.
        /// Also exposed via IEventService for shop/chest/codex services.
        /// </summary>
        public Data.EventSaveData GetSaveData()
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
            // Rotation: resolve actual event from pool
            if (string.IsNullOrEmpty(eventId) || eventId == "auto")
            {
                eventId = GetRotatedEventId();
                if (eventId == null)
                {
                    if (_debug) Debug.LogWarning("[EventService] No rotated event available.");
                    return false;
                }
            }

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

            _runtimeState.Reset();
            _runtimeState.EventId = eventId;
            _runtimeState.Threat = evt.threat.initial;

            // Set end time based on schedule type
            if (evt.schedule.type == ScheduleType.OneTime && DateTime.TryParse(evt.schedule.endUtc, out var endTime))
            {
                _runtimeState.EventEndsAt = endTime.Ticks;
            }
            else if (evt.schedule.durationDays > 0)
            {
                _runtimeState.EventEndsAt = DateTime.UtcNow.AddDays(evt.schedule.durationDays).Ticks;
            }
            else
            {
                _runtimeState.EventEndsAt = DateTime.UtcNow.AddHours(24).Ticks;
            }

            OnEventStarted?.Invoke(eventId);
            if (_debug) Debug.Log($"[EventService] Event started: {eventId}");

            return true;
        }

        /// <summary>
        /// Get current event from rotation pool.
        /// Returns null if no rotation event defined.
        /// ponytail: supports only Rotation type. Add Weekly/Monthly when needed.
        /// </summary>
        private string GetRotatedEventId()
        {
            foreach (var evt in _eventDefinitions.Values)
            {
                if (evt.schedule.type == ScheduleType.Rotation && evt.schedule.rotationPool?.Count > 0)
                {
                    if (!DateTime.TryParse(evt.schedule.startUtc, out var anchor)) continue;

                    var daysSinceLaunch = (DateTime.UtcNow - anchor).Days;
                    var index = daysSinceLaunch / evt.schedule.durationDays % evt.schedule.rotationPool.Count;
                    return evt.schedule.rotationPool[index];
                }
            }
            return null;
        }

        public void EndEvent(bool success)
        {
            if (string.IsNullOrEmpty(_runtimeState.EventId)) return;

            var eventId = _runtimeState.EventId;

            // Unsubscribe from boss death handler if still subscribed
            EnemyDeathHandler.OnEnemyKilled -= HandleBossDeath;

            // Distribute rewards if success
            if (success)
            {
                DistributeEventRewards();
            }

            OnEventEnded?.Invoke(eventId, success);
            if (_debug) Debug.Log($"[EventService] Event ended: {eventId}, success={success}");

            _runtimeState.Reset();
            _appliedEscalations.Clear();
        }

        public bool IsEventActive()
        {
            if (string.IsNullOrEmpty(_runtimeState.EventId)) return false;

            // Check expiry
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
            if (evt == null) return;

            int delta = 0;
            if (isBoss)
                delta = evt.threat.rates.bossKill;
            else if (isElite)
                delta = evt.threat.rates.eliteKill;
            else
                delta = evt.threat.rates.voidKill;

            AddThreat(delta);

            // Codex discovery
            RegisterEnemyDiscovered(enemyId);

            // Update kill objectives
            foreach (var obj in evt.objectives)
            {
                if (obj.type == "KillEnemies" && obj.targetId == enemyId)
                {
                    IncrementObjective(obj.id, 1);
                }
            }
        }

        /// <summary>
        /// Register enemy discovery for codex unlock.
        /// </summary>
        private void RegisterEnemyDiscovered(string enemyId)
        {
            var evt = GetActiveEvent();
            if (evt?.codex?.entries == null) return;

            // Check if enemy is in codex entries
            bool isCodexEnemy = false;
            foreach (var entryId in evt.codex.entries)
            {
                if (entryId == enemyId)
                {
                    isCodexEnemy = true;
                    break;
                }
            }

            if (!isCodexEnemy) return;

            // Check if already unlocked
            if (_runtimeState.CodexEntries.Contains(enemyId)) return;

            // Unlock codex entry
            _runtimeState.CodexEntries.Add(enemyId);
            AddEventCurrency(100); // Discovery reward

            if (_debug) Debug.Log($"[EventService] Codex entry unlocked: {enemyId}");
        }

        public void RegisterWaveCompleted()
        {
            var evt = GetActiveEvent();
            if (evt == null) return;

            // Update wave-based objectives
            foreach (var obj in evt.objectives)
            {
                if (obj.type == "SurviveWaves")
                {
                    IncrementObjective(obj.id, 1);
                }
            }

            // Check wave-based incidents
            if (evt.incidents != null)
            {
                int currentWave = Manager.WaveManager.Instance?.CurrentWave ?? 0;
                foreach (var incident in evt.incidents)
                {
                    if (incident.trigger == "wave" && currentWave % incident.conditions.waveCount == 0)
                    {
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

            if (_runtimeState.Threat != oldThreat)
            {
                OnThreatChanged?.Invoke(oldThreat, _runtimeState.Threat);

                // Check catastrophic threshold
                if (_runtimeState.Threat >= 90 && !_runtimeState.CatastrophicTriggered)
                {
                    _runtimeState.CatastrophicTriggered = true;
                    OnIncidentTriggered?.Invoke("catastrophic");
                }

                // Check for collapse → trigger boss spawn
                if (_runtimeState.Threat >= 100 && !_runtimeState.IsCollapsed)
                {
                    _runtimeState.IsCollapsed = true;
                    TriggerBossSpawn();
                }
            }
        }

        /// <summary>
        /// Spawn event boss when threat reaches 100.
        /// </summary>
        private void TriggerBossSpawn()
        {
            var evt = GetActiveEvent();
            if (evt?.boss?.enemyId == null)
            {
                if (_debug) Debug.LogWarning("[EventService] No boss defined for this event. Ending as failure.");
                EndEvent(false);
                return;
            }

            // Get boss data from database
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

            // Spawn boss via EnemySpawner
            var spawner = EnemySpawner.Instance;
            if (spawner != null)
            {
                EnemyData scaledBoss = Utilityku.CreateScaledEnemy(bossData);
                spawner.SpawnSpecificEnemy(scaledBoss);

                // Subscribe to boss death
                EnemyDeathHandler.OnEnemyKilled += HandleBossDeath;

                if (_debug) Debug.Log($"[EventService] Boss '{evt.boss.enemyId}' spawned. Threat collapsed.");
            }
            else
            {
                EndEvent(false);
            }
        }

        /// <summary>
        /// Handle event boss death.
        /// </summary>
        private void HandleBossDeath(EnemyAi enemy, string source)
        {
            var evt = GetActiveEvent();
            if (evt?.boss?.enemyId == null) return;
            if (enemy?.EnemyData?.id != evt.boss.enemyId) return;

            // Boss defeated - event success
            EnemyDeathHandler.OnEnemyKilled -= HandleBossDeath;

            if (_debug) Debug.Log($"[EventService] Boss defeated. Event success.");
            EndEvent(true);
        }

        // -------------------------------------------------------------------
        // Choice System
        // -------------------------------------------------------------------
        public bool MakeChoice(string choiceId)
        {
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

            // Apply reward multiplier to next rewards
            // (Implementation detail: stored in runtime state, applied in DistributeEventRewards)

            OnChoiceMade?.Invoke(choiceId);
            if (_debug) Debug.Log($"[EventService] Choice made: {choiceId}, threat delta={choice.threatDelta}");

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

        public bool ClaimObjective(string objectiveId)
        {
            if (_runtimeState.ObjectiveClaimed.Contains(objectiveId)) return false;

            var evt = GetActiveEvent();
            if (evt == null) return false;

            var obj = evt.objectives.FirstOrDefault(o => o.id == objectiveId);
            if (obj == null) return false;

            // Check progress
            var progress = _runtimeState.ObjectiveProgress.ContainsKey(objectiveId)
                ? _runtimeState.ObjectiveProgress[objectiveId]
                : 0;

            if (progress < obj.target) return false;

            // Grant rewards
            if (obj.reward.eventCurrency > 0)
                AddEventCurrency(obj.reward.eventCurrency);
            if (obj.reward.gold > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gold, obj.reward.gold, "EventObjective");
            if (obj.reward.gem > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Gem, obj.reward.gem, "EventObjective");
            if (obj.reward.meat > 0)
                ServiceLocator.EconomyService?.AddCurrency(CurrencyType.Meat, obj.reward.meat, "EventObjective");

            _runtimeState.ObjectiveClaimed.Add(objectiveId);
            return true;
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
            var evt = GetActiveEvent();
            if (evt == null) return;

            // 1. Grant event currency based on score (example formula)
            long currencyReward = _runtimeState.Score / 10;
            AddEventCurrency(currencyReward);

            // 2. Grant relics (add to inventory)
            if (evt.rewards?.relics != null && InventoryService.Instance != null)
            {
                foreach (var relicId in evt.rewards.relics)
                {
                    InventoryService.Instance.AddItem(relicId, 1);
                }
            }

            // 3. Pet unlock/duplicate handling
            if (!string.IsNullOrEmpty(evt.pet?.petId))
            {
                GrantEventPet(evt.pet.petId);
            }

            if (_debug) Debug.Log($"[EventService] Rewards distributed: {currencyReward} currency, {evt.rewards?.relics?.Length ?? 0} relics");
        }

        /// <summary>
        /// Grant event pet via PetService.
        /// If already owned, grant duplicate currency.
        /// </summary>
        private void GrantEventPet(string petId)
        {
            var petService = Core.ServiceLocator.PetService;
            if (petService == null) return;

            // Check if pet already owned
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
                // Unlock new pet
                petService.GrantPet(petId);
                if (_debug) Debug.Log($"[EventService] Pet unlocked: {petId}");
            }
            else
            {
                // Duplicate → grant event currency instead
                AddEventCurrency(5000);
                if (_debug) Debug.Log($"[EventService] Pet duplicate converted to 5000 event currency");
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        [Serializable]
        private class EventDefinitionWrapper
        {
            public EventDefinition[] events;
        }
    }
}
