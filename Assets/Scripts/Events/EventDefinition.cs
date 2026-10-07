using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Events
{
    public enum ScheduleType { OneTime, Weekly, Monthly, Rotation, Recurring }

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
        public ScheduleType type;
        public string startUtc;  // for OneTime/Recurring/Rotation anchor
        public string endUtc;    // for OneTime
        public List<string> rotationPool;  // for Rotation
        public int durationDays;           // for Rotation/Weekly/Monthly
        public string rotationId;          // legacy, optional
    }

    [Serializable]
    public class EventThreatConfig
    {
        public int initial;
        public int min;
        public int max;
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
        public string type; // voidActivity, spawnWeight, mutatedElite, catastrophic
        public float value;
    }

    [Serializable]
    public class EventObjective
    {
        public string id;
        public string type; // SurviveWaves, KillEnemies, CollectCurrency, MakeChoices
        public string targetId; // enemy ID for KillEnemies
        public int target;
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
        public string trigger; // wave, threat, time
        public EventIncidentConditions conditions;
        public string description;
    }

    [Serializable]
    public class EventIncidentConditions
    {
        public int waveCount;
        public int threatLevel;
    }

    [Serializable]
    public class EventBoss
    {
        public string enemyId;
        public Dictionary<string, string> stateMap; // threat range → boss state
    }

    [Serializable]
    public class EventRewards
    {
        public string eventCurrency; // AbyssEssence, VoidFragment, etc.
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
        public long price; // event currency
    }

    [Serializable]
    public class EventCodex
    {
        public string[] entries; // enemy IDs to unlock in codex
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
        public string petId; // pet unlocked on event completion
    }
}
