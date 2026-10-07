using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Events
{
    /// <summary>
    /// Event System service interface.
    /// Manages event lifecycle, threat tracking, player choices, and rewards.
    /// </summary>
    public interface IEventService
    {
        /// <summary>
        /// Fired when an event starts.
        /// </summary>
        event Action<string> OnEventStarted;

        /// <summary>
        /// Fired when an event ends (success or collapse).
        /// </summary>
        event Action<string, bool> OnEventEnded;

        /// <summary>
        /// Fired when threat level changes.
        /// </summary>
        event Action<int, int> OnThreatChanged;

        /// <summary>
        /// Fired when a choice is made.
        /// </summary>
        event Action<string> OnChoiceMade;

        /// <summary>
        /// Fired when an incident triggers (catastrophic, etc.).
        /// </summary>
        event Action<string> OnIncidentTriggered;

        /// <summary>
        /// Current active event ID, null if none.
        /// </summary>
        string ActiveEventId { get; }

        /// <summary>
        /// Current threat level [0-100].
        /// </summary>
        int CurrentThreat { get; }

        /// <summary>
        /// Current event score.
        /// </summary>
        long EventScore { get; }

        /// <summary>
        /// Get active event definition, null if none.
        /// </summary>
        EventDefinition GetActiveEvent();

        /// <summary>
        /// Start an event by ID. Returns false if event not found or already active.
        /// </summary>
        bool StartEvent(string eventId);

        /// <summary>
        /// End current event (success = rewards, collapse = penalties).
        /// </summary>
        void EndEvent(bool success);

        /// <summary>
        /// Register enemy kill for threat tracking.
        /// </summary>
        void RegisterKill(bool isElite, bool isBoss, string enemyId);

        /// <summary>
        /// Register wave completion for objectives.
        /// </summary>
        void RegisterWaveCompleted();

        /// <summary>
        /// Make a player choice (Seal/Harvest/Feed).
        /// </summary>
        bool MakeChoice(string choiceId);

        /// <summary>
        /// Get spawn weight modifier for current threat level.
        /// </summary>
        float GetSpawnWeightModifier(string enemyId);

        /// <summary>
        /// Get stat modifier (HP/Damage/Speed) for current threat level.
        /// </summary>
        float GetStatModifier(string modifierType);

        /// <summary>
        /// Claim objective reward.
        /// </summary>
        bool ClaimObjective(string objectiveId);

        /// <summary>
        /// Add event currency.
        /// </summary>
        void AddEventCurrency(long amount);

        /// <summary>
        /// Spend event currency (shop purchases).
        /// </summary>
        bool SpendEventCurrency(long amount);

        /// <summary>
        /// Check if event is scheduled to run now.
        /// </summary>
        bool IsEventActive();

        /// <summary>
        /// Get save data for EventService internal state access (shop, chest, codex).
        /// </summary>
        Data.EventSaveData GetSaveData();
    }
}
