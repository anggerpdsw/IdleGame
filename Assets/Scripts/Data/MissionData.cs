using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Mission template loaded from dataMission.json - defines the structure of a mission type
    /// </summary>
    [Serializable]
    public class MissionTemplate
    {
        public string id;
        public string name;
        public string description;
        public MissionEventType type;     // KillEnemy, KillSpecificEnemy, CollectCurrency, CompleteWaves
        public string targetId; // Specific enemy ID or currency type, empty for generic
        public int minCount;
        public int maxCount;
        public MissionReward reward;
        public int claimCooldownMinutes;
        public int cancelCooldownMinutes;
    }

    /// <summary>
    /// Reward configuration for a mission
    /// </summary>
    [Serializable]
    public class MissionReward
    {
        public long gold;
        public long gem;
        public long meat;
    }

    /// <summary>
    /// All missions from dataMission.json
    /// </summary>
    [Serializable]
    public class MissionTemplateData
    {
        public List<MissionTemplate> missions = new();
    }

    /// <summary>
    /// Runtime mission instance - persistent data for a specific mission assigned to a player
    /// </summary>
    [Serializable]
    public class MissionInstance
    {
        public string instanceId;
        public string missionId;        // References MissionTemplate.id
        public string targetId;         // <-- enemy/item/etc yang ditarget
        public long targetCount;        // Random value between minCount and maxCount
        public long currentCount;
        public MissionStatus status;
        public string createdAt;        // ISO 8601 UTC timestamp
        public string completedAt;      // ISO 8601 UTC timestamp, null if not completed
        public string cooldownUntil;    // ISO 8601 UTC timestamp, null if no cooldown
        public bool rewardClaimed;
        public int slotIndex;       // Which mission slot this occupies (0 to MaxMission-1)

        // Cached reward (rolled at generation time, not at claim time)
        public MissionReward reward;
    }

    /// <summary>
    /// Event data for mission progress updates
    /// </summary>
    public class MissionProgressEvent
    {
        public MissionEventType eventType;
        public string targetId;    // Enemy ID, currency type, etc.
        public int amount;
    }
}