using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Events.Domain
{
    /// <summary>
    /// Runtime state for active event.
    /// Separate from EventDefinition (immutable config) and EventSaveData (persistence).
    /// </summary>
    public class EventRuntimeState
    {
        public EventState State { get; set; } = EventState.Idle;
        public string EventId { get; set; }
        public int Threat { get; set; }
        public long Score { get; set; }
        public long EventCurrency { get; set; }
        public Dictionary<string, int> ObjectiveProgress { get; private set; }
        public HashSet<string> ObjectiveClaimed { get; private set; }
        public List<string> ChoiceHistory { get; private set; }
        public HashSet<string> CodexEntries { get; private set; }
        public HashSet<string> TriggeredIncidents { get; private set; }
        public HashSet<string> ShopPurchases { get; private set; }
        public List<string> CompletedEventIds { get; private set; }
        public int ChestPity { get; set; }
        public bool IsCollapsed { get; set; }
        public bool RewardsGranted { get; set; }
        public long EventEndsAt { get; set; }
        public long NextEventStartAt { get; set; }
        public bool CatastrophicTriggered { get; set; }

        public EventRuntimeState()
        {
            ObjectiveProgress = new Dictionary<string, int>();
            ObjectiveClaimed = new HashSet<string>();
            ChoiceHistory = new List<string>();
            CodexEntries = new HashSet<string>();
            TriggeredIncidents = new HashSet<string>();
            ShopPurchases = new HashSet<string>();
            CompletedEventIds = new List<string>();
        }

        public void Reset()
        {
            State = EventState.Idle;
            EventId = null;
            Threat = 0;
            Score = 0;
            EventCurrency = 0;
            ObjectiveProgress.Clear();
            ObjectiveClaimed.Clear();
            ChoiceHistory.Clear();
            CodexEntries.Clear();
            TriggeredIncidents.Clear();
            ShopPurchases.Clear();
            CompletedEventIds.Clear();
            EventEndsAt = 0;
            NextEventStartAt = 0;
            IsCollapsed = false;
            CatastrophicTriggered = false;
            ChestPity = 0;
            RewardsGranted = false;
        }

        /// <summary>
        /// Load state from save data.
        /// </summary>
        public void LoadFrom(Data.EventSaveData save)
        {
            if (save == null) return;

            State = save.state;
            EventId = save.activeEventId;
            Threat = save.threat;
            Score = save.eventScore;
            EventCurrency = save.eventCurrency;
            EventEndsAt = save.eventEndsAt;
            NextEventStartAt = save.nextEventStartAt;

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

            TriggeredIncidents.Clear();
            if (save.triggeredIncidents != null)
            {
                foreach (var id in save.triggeredIncidents)
                {
                    TriggeredIncidents.Add(id);
                }
            }

            ShopPurchases.Clear();
            if (save.shopPurchases != null)
            {
                foreach (var id in save.shopPurchases)
                {
                    ShopPurchases.Add(id);
                }
            }

            CompletedEventIds.Clear();
            if (save.completedEventIds != null)
            {
                CompletedEventIds.AddRange(save.completedEventIds);
            }

            CatastrophicTriggered = save.catastrophicTriggered;
            IsCollapsed = save.isCollapsed;
            RewardsGranted = save.rewardsGranted;
            ChestPity = save.chestPity;
        }

        /// <summary>
        /// Save state to save data.
        /// </summary>
        public Data.EventSaveData ToSaveData()
        {
            return new Data.EventSaveData
            {
                state = State,
                activeEventId = EventId,
                threat = Threat,
                eventScore = Score,
                eventCurrency = EventCurrency,
                eventEndsAt = EventEndsAt,
                nextEventStartAt = NextEventStartAt,
                objectiveProgress = new Dictionary<string, int>(ObjectiveProgress),
                objectiveClaimed = new HashSet<string>(ObjectiveClaimed),
                choiceHistory = new List<string>(ChoiceHistory),
                codexEntries = new HashSet<string>(CodexEntries),
                triggeredIncidents = new HashSet<string>(TriggeredIncidents),
                shopPurchases = new HashSet<string>(ShopPurchases),
                completedEventIds = new List<string>(CompletedEventIds),
                catastrophicTriggered = CatastrophicTriggered,
                isCollapsed = IsCollapsed,
                rewardsGranted = RewardsGranted,
                chestPity = ChestPity
            };
        }
    }
}
