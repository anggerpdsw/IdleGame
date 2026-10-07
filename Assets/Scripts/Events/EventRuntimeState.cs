using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Events
{
    /// <summary>
    /// Runtime state for active event.
    /// Separate from EventDefinition (immutable config) and EventSaveData (persistence).
    /// </summary>
    public class EventRuntimeState
    {
        public string EventId { get; set; }
        public int Threat { get; set; }
        public long Score { get; set; }
        public long EventCurrency { get; set; }
        public Dictionary<string, int> ObjectiveProgress { get; private set; }
        public HashSet<string> ObjectiveClaimed { get; private set; }
        public List<string> ChoiceHistory { get; private set; }
        public HashSet<string> CodexEntries { get; private set; }
        public long EventEndsAt { get; set; } // UTC ticks
        public bool IsCollapsed { get; set; }
        public bool CatastrophicTriggered { get; set; }

        public EventRuntimeState()
        {
            ObjectiveProgress = new Dictionary<string, int>();
            ObjectiveClaimed = new HashSet<string>();
            ChoiceHistory = new List<string>();
            CodexEntries = new HashSet<string>();
        }

        public void Reset()
        {
            EventId = null;
            Threat = 0;
            Score = 0;
            EventCurrency = 0;
            ObjectiveProgress.Clear();
            ObjectiveClaimed.Clear();
            ChoiceHistory.Clear();
            CodexEntries.Clear();
            EventEndsAt = 0;
            IsCollapsed = false;
            CatastrophicTriggered = false;
        }

        /// <summary>
        /// Load state from save data.
        /// </summary>
        public void LoadFrom(Data.EventSaveData save)
        {
            if (save == null) return;

            EventId = save.activeEventId;
            Threat = save.threat;
            Score = save.eventScore;
            EventCurrency = save.eventCurrency;
            EventEndsAt = save.eventEndsAt;

            ObjectiveProgress.Clear();
            if (save.objectiveProgress != null)
            {
                foreach (var kvp in save.objectiveProgress)
                {
                    ObjectiveProgress[kvp.Key] = kvp.Value;
                }
            }

            ObjectiveClaimed.Clear();
            if (save.objectiveClaimed != null)
            {
                foreach (var id in save.objectiveClaimed)
                {
                    ObjectiveClaimed.Add(id);
                }
            }

            ChoiceHistory.Clear();
            if (save.choiceHistory != null)
            {
                ChoiceHistory.AddRange(save.choiceHistory);
            }

            CodexEntries.Clear();
            if (save.codexEntries != null)
            {
                foreach (var id in save.codexEntries)
                {
                    CodexEntries.Add(id);
                }
            }

            CatastrophicTriggered = save.catastrophicTriggered;
        }

        /// <summary>
        /// Save state to save data.
        /// </summary>
        public Data.EventSaveData ToSaveData()
        {
            return new Data.EventSaveData
            {
                activeEventId = EventId,
                threat = Threat,
                eventScore = Score,
                eventCurrency = EventCurrency,
                eventEndsAt = EventEndsAt,
                objectiveProgress = new Dictionary<string, int>(ObjectiveProgress),
                objectiveClaimed = new HashSet<string>(ObjectiveClaimed),
                choiceHistory = new List<string>(ChoiceHistory),
                codexEntries = new HashSet<string>(CodexEntries),
                catastrophicTriggered = CatastrophicTriggered,
                shopPurchases = new HashSet<string>(),
                chestPity = 0,
                completedEventIds = new List<string>()
            };
        }
    }
}
