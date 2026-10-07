using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Event System save data.
    /// Added in save version 7.
    /// </summary>
    [Serializable]
    public class EventSaveData
    {
        public string activeEventId;    // null = no active event
        public long eventEndsAt;        // UTC ticks
        public int threat;              // [0-100]
        public long eventScore;
        public long eventCurrency;      // AbyssEssence, etc.
        public Dictionary<string, int> objectiveProgress = new();
        public HashSet<string> objectiveClaimed = new();
        public List<string> choiceHistory = new();
        public HashSet<string> shopPurchases = new();
        public HashSet<string> codexEntries = new();
        public bool catastrophicTriggered;
        public int chestPity;
        public List<string> completedEventIds = new();
    }
}
