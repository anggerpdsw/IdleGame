using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Events.Domain
{
    /// <summary>
    /// Event definition loaded from dataEvent.json.
    /// Single source of truth for event configuration.
    /// </summary>
    [Serializable]
    public class EventDefinition
    {
        public string eventId;
        public string displayName;
        public string description;
        public EventSchedule schedule;
        public EventThreatConfig threat;
        public EventChoice[] choices;
        public EventEscalationLevel[] escalation;
        public EventObjective[] objectives;
        public EventIncident[] incidents;
        public EventBoss boss;
        public EventRewards rewards;
        public EventShop shop;
        public EventCodex codex;
        public EventChest chest;
        public EventPet pet;
    }

    [Serializable]
    public class EventSchedule
    {
        public int durationDays;
        public string rotationId;
        public List<string> rotationPool;
    }

    [Serializable]
    public class EventThreatConfig
    {
        public int initial;
        public int min;
        public int max;
        public int catastrophicThreshold;  // Boss warning trigger (default: 90% of max)
        public int collapseThreshold;      // Boss spawn trigger (default: 100% of max)
        public ThreatRates rates;
    }

    [Serializable]
    public class ThreatRates
    {
        public int voidKill;
        public int eliteKill;
        public int bossKill;
        public int sealRift;
        public int harvestRift;
        public int feedRift;
    }

    [Serializable]
    public class EventChoice
    {
        public string choiceId;
        public string displayName;
        public int threatDelta;
        public float rewardMultiplier;
    }

    [Serializable]
    public class EventEscalationLevel
    {
        public int threshold;
        public EventModifier[] modifiers;
    }

    [Serializable]
    public class EventModifier
    {
        public string type;
        public float value;
    }

    [Serializable]
    public class EventObjective
    {
        public string id;
        public string type;
        public string targetId;
        public int target;
        public string claimPolicy;
        public EventObjectiveReward reward;
    }

    [Serializable]
    public class EventObjectiveReward
    {
        public long eventCurrency;
        public long gold;
        public long gem;
        public long meat;
    }

    [Serializable]
    public class EventIncident
    {
        public string id;
        public string trigger;
        public EventIncidentConditions conditions;
        public string description;
    }

    [Serializable]
    public class EventIncidentConditions
    {
        public int minWave;
        public int minThreat;
    }

    [Serializable]
    public class EventBoss
    {
        public string enemyId;
        public Dictionary<string, string> stateMap;
    }

    [Serializable]
    public class EventRewards
    {
        public string eventCurrency;
        public string[] relics;
    }

    [Serializable]
    public class EventShop
    {
        public EventShopItem[] items;
    }

    [Serializable]
    public class EventShopItem
    {
        public string itemId;
        public long price;
    }

    [Serializable]
    public class EventCodex
    {
        public string[] entries;
    }

    [Serializable]
    public class EventChest
    {
        public int pity;
        public string[] rewards;
    }

    [Serializable]
    public class EventPet
    {
        public string petId;
    }
}
