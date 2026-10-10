using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Events.Domain;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Event System save data.
    /// Added in save version 7.
    /// </summary>
    [Serializable]
    public class EventSaveData
    {
        public EventState state = EventState.Idle;
        public string activeEventId;    // null = no active event
        public long eventEndsAt;        // UTC ticks
        public long nextEventStartAt;   // UTC ticks - kapan event berikutnya bisa dimulai setelah cooldown
        public int threat;              // [0-100]
        public long eventScore;
        public long eventCurrency;      // AbyssEssence, etc.
        public Dictionary<string, int> objectiveProgress = new();
        public HashSet<string> objectiveClaimed = new();
        public List<string> choiceHistory = new();
        public HashSet<string> shopPurchases = new();
        public HashSet<string> codexEntries = new();
        public HashSet<string> triggeredIncidents = new(); // dedup incident triggers
        public bool catastrophicTriggered;
        public bool isCollapsed;
        public bool rewardsGranted;         // Prevents double-reward on crash/reload
        public int chestPity;
        public List<string> completedEventIds = new();
    }
}
